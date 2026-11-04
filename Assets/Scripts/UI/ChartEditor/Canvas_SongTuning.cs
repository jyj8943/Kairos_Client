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
/// - 바꾼 값은 "SongData에 저장"을 눌러야 곡 에셋에 들어간다. 저장 전에는 세션 값만 바뀌므로 플레이를 끝내면 원래 값 그대로다.
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
        AssetDatabase.SaveAssets();

        statusText.text = $"SongData에 저장했습니다 ({AssetDatabase.GetAssetPath(song)})";
        Debug.Log($"[Canvas_SongTuning] SongData 저장: {song.SongId} bpm={session.Bpm}, offsetMs={session.OffsetMs}", song);
        Refresh();
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
