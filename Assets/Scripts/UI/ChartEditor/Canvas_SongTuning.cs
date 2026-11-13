#if UNITY_EDITOR
using System;
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 곡 BPM·offsetMs 조정 패널 (개발용, Unity 에디터 전용). EditorScene.unity의 "Canvas_SongTuning"에 붙이고, EditorScene이 Init()을 호출한다.
/// - 파일 전체가 #if UNITY_EDITOR 안에 있고, 오브젝트는 EditorOnly 태그라 빌드에서 오브젝트째 빠진다.
/// - F1(ChartEditor/ToggleTuning)로 여닫는다. offsetMs −10/−1/+1/+10 버튼과 BPM 입력칸으로 값을 바꾸면
///   EditorScene이 그리드·메트로놈·시간 표시에 바로 반영한다 (재생 중이면 그 자리에서 다시 재생).
/// - BPM 입력칸은 곡 시작 구간(0박) BPM만 바꾼다. 다른 BPM 구간은 그대로 두고, 바꾼 것은 구간 편집처럼 Ctrl+Z로 되돌릴 수 있다.
///   offset은 곡 정보 값이라 채보 수정(저장 안 됨 표시)에 넣지 않는다.
/// - 바꾼 값은 "SongData에 저장"을 눌러야 곡 에셋에 들어간다. 저장 전에는 세션 값만 바뀌므로 플레이를 끝내면 원래 값 그대로다.
/// - 공식 채보로 추가: 편집 중인 노트를 난이도·레벨을 정해 공식 채보(Assets/Resources/Data/OfficialCharts/{songId}/{난이도}.json)로 저장한다.
///   그 난이도 파일이 있으면 확인 팝업으로 덮어쓸지 묻고, 저장 뒤 바로 임포트해 같은 플레이 중에도 곡 선택 씬에서 보인다.
///   편집 중인 나만의 채보(세션의 chartId·수정 여부·파일)는 건드리지 않는다.
/// </summary>
public class Canvas_SongTuning : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text valuesText;
    [SerializeField] private Button offsetMinus10Button;
    [SerializeField] private Button offsetMinus1Button;
    [SerializeField] private Button offsetPlus1Button;
    [SerializeField] private Button offsetPlus10Button;
    [SerializeField] private TMP_InputField bpmInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_Text statusText;

    [Header("공식 채보로 추가")]
    [Tooltip("난이도 (EASY / NORMAL / HARD, GameConstants.Chart.Difficulties 순서)")]
    [SerializeField] private TMP_Dropdown officialDifficultyDropdown;
    [Tooltip("레벨 (1~15). 기본값은 편집 중인 채보의 레벨")]
    [SerializeField] private TMP_InputField officialLevelInput;
    [SerializeField] private Button addOfficialButton;
    [Tooltip("공식 채보로 추가 결과 한 줄")]
    [SerializeField] private TMP_Text officialStatusText;

    [Header("입력")]
    [Tooltip("입력 에셋의 ChartEditor/ToggleTuning (F1)")]
    [SerializeField] private InputActionReference toggleAction;

    private SongData song;
    private ChartEditSession session;
    private Action<float, int> applyTiming;
    private bool isInputEnabled;

    /// <summary>
    /// 곡·세션과 값을 바꿀 함수를 받는다. 패널은 닫은 상태로 시작한다.
    /// </summary>
    /// <param name="applyTiming">(BPM, offsetMs)를 에디터에 반영하는 함수 (EditorScene)</param>
    public void Init(SongData song, ChartEditSession session, Action<float, int> applyTiming)
    {
        this.song = song;
        this.session = session;
        this.applyTiming = applyTiming;

        offsetMinus10Button.onClick.RemoveAllListeners();
        offsetMinus1Button.onClick.RemoveAllListeners();
        offsetPlus1Button.onClick.RemoveAllListeners();
        offsetPlus10Button.onClick.RemoveAllListeners();
        saveButton.onClick.RemoveAllListeners();
        offsetMinus10Button.onClick.AddListener(() => ChangeOffset(-10));
        offsetMinus1Button.onClick.AddListener(() => ChangeOffset(-1));
        offsetPlus1Button.onClick.AddListener(() => ChangeOffset(1));
        offsetPlus10Button.onClick.AddListener(() => ChangeOffset(10));
        saveButton.onClick.AddListener(SaveToSongData);
        bpmInput.onEndEdit.RemoveListener(OnBpmEdited);
        bpmInput.onEndEdit.AddListener(OnBpmEdited);

        statusText.text = string.Empty;
        InitOfficialChart();
        panel.SetActive(false);
        Refresh();
    }

    /// <summary>F1 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.</summary>
    public void EnableInput()
    {
        if (isInputEnabled || toggleAction == null) return;

        toggleAction.action.performed += OnToggle;
        toggleAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>F1 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.</summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        toggleAction.action.performed -= OnToggle;
        isInputEnabled = false;
    }

    private void OnDestroy()
    {
        DisableInput();
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        // 팝업이 열려 있으면 F1을 받지 않는다
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;

        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Refresh();
    }

    private void ChangeOffset(int deltaMs)
    {
        applyTiming?.Invoke(session.Bpm, session.OffsetMs + deltaMs);
        Refresh();
    }

    private void OnBpmEdited(string text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm) || bpm <= 0f)
        {
            statusText.text = $"BPM은 0보다 큰 숫자로 입력하세요: {text}";
            Refresh();
            return;
        }

        applyTiming?.Invoke(bpm, session.OffsetMs);
        Refresh();
    }

    /// <summary>
    /// 지금 세션의 BPM·offsetMs를 SongData 에셋에 저장한다.
    /// </summary>
    private void SaveToSongData()
    {
        SerializedObject serialized = new SerializedObject(song);
        serialized.FindProperty("bpm").floatValue = session.Bpm;
        serialized.FindProperty("offsetMs").intValue = session.OffsetMs;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(song);
        // 곡 기본 BPM이 바뀌었으므로 "곡 기본 BPM으로 되돌리기"도 새 값을 쓴다
        session.SetSongDefaultBpm(session.Bpm);
        AssetDatabase.SaveAssets();

        statusText.text = $"SongData에 저장했습니다 ({AssetDatabase.GetAssetPath(song)})";
        Debug.Log($"[Canvas_SongTuning] SongData 저장: {song.SongId} bpm={session.Bpm}, offsetMs={session.OffsetMs}", song);
        Refresh();
    }

    /// <summary>
    /// 공식 채보로 추가 영역을 채운다: 난이도 목록, 레벨(세션 레벨), 버튼.
    /// </summary>
    private void InitOfficialChart()
    {
        officialDifficultyDropdown.ClearOptions();
        officialDifficultyDropdown.AddOptions(new System.Collections.Generic.List<string>(GameConstants.Chart.Difficulties));
        officialDifficultyDropdown.SetValueWithoutNotify(0);

        officialLevelInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        officialLevelInput.SetTextWithoutNotify(ClampLevel(session.Level).ToString(CultureInfo.InvariantCulture));
        officialLevelInput.onEndEdit.RemoveListener(OnOfficialLevelEdited);
        officialLevelInput.onEndEdit.AddListener(OnOfficialLevelEdited);

        addOfficialButton.onClick.RemoveListener(RequestAddOfficial);
        addOfficialButton.onClick.AddListener(RequestAddOfficial);
        officialStatusText.text = string.Empty;
    }

    private static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, GameConstants.Editor.MinLevel, GameConstants.Editor.MaxLevel);
    }

    /// <summary>레벨 입력을 1~15로 맞춘다 (숫자가 아니면 세션 레벨)</summary>
    private void OnOfficialLevelEdited(string text)
    {
        int level = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : session.Level;
        officialLevelInput.SetTextWithoutNotify(ClampLevel(level).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// "공식 채보로 추가" 버튼. 노트가 없으면 안내만 하고, 그 난이도 파일이 있으면 덮어쓸지 묻는다.
    /// </summary>
    private void RequestAddOfficial()
    {
        if (session.Data.Notes.Count == 0)
        {
            officialStatusText.text = "노트가 없어 공식 채보로 추가하지 않았습니다.";
            return;
        }

        string difficulty = GameConstants.Chart.Difficulties[officialDifficultyDropdown.value];
        OnOfficialLevelEdited(officialLevelInput.text);

        if (ChartRepository.OfficialChartExists(session.SongId, difficulty))
        {
            string message = string.Format(GameConstants.Confirm.OfficialOverwriteMessageFormat, song.Title, difficulty);
            GameManager.UI.Confirm(message, GameConstants.Confirm.OfficialOverwriteLabel,
                () => SaveOfficialChart(difficulty),
                () => officialStatusText.text = $"{song.Title} {difficulty}: 덮어쓰지 않았습니다.");
            return;
        }

        SaveOfficialChart(difficulty);
    }

    /// <summary>
    /// 세션 노트로 공식 채보를 새로 만들어 저장하고 바로 임포트한다. 세션(chartId·수정 여부)은 바꾸지 않는다.
    /// </summary>
    private void SaveOfficialChart(string difficulty)
    {
        int level = int.Parse(officialLevelInput.text, CultureInfo.InvariantCulture);

        ChartData chart = session.BuildChartData();
        chart.type = ChartData.TypeOfficial;
        chart.chartId = session.SongId + "_" + difficulty.ToLowerInvariant();
        chart.difficulty = difficulty;
        chart.level = level;
        chart.chartName = song.Title + " " + difficulty;
        chart.author = GameConstants.Chart.OfficialAuthor;

        if (!ChartRepository.TrySave(chart, out string error))
        {
            officialStatusText.text = $"공식 채보로 추가하지 못했습니다: {error}";
            Debug.LogWarning($"[Canvas_SongTuning] 공식 채보 저장 실패: {error}", this);
            return;
        }

        // 프로젝트 안 경로(Assets/...)로 바꿔 바로 임포트한다 (같은 플레이 중 Resources.LoadAll에 보이게)
        string fullPath = ChartRepository.GetOfficialChartPath(chart.songId, difficulty).Replace('\\', '/');
        string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        officialStatusText.text = $"공식 채보로 추가했습니다: {chart.chartName} Lv.{level}, 노트 {chart.notes.Count}개";
        Debug.Log($"[Canvas_SongTuning] 공식 채보 저장: {assetPath} (chartId={chart.chartId}, level={level}, 노트 {chart.notes.Count}개)", this);
    }

    private void Refresh()
    {
        if (session == null || song == null) return;

        bool changed = !Mathf.Approximately(song.Bpm, session.Bpm) || song.OffsetMs != session.OffsetMs;
        valuesText.text =
            $"{song.Title}\n" +
            $"BPM  {SongData.FormatBpm(session.Bpm)}    offsetMs  {session.OffsetMs}\n" +
            (changed ? $"<color=#F97316>저장 안 함 (SongData: BPM {SongData.FormatBpm(song.Bpm)}, offsetMs {song.OffsetMs})</color>" : "SongData와 같음");
        bpmInput.SetTextWithoutNotify(SongData.FormatBpm(session.Bpm));
    }
}
#endif
