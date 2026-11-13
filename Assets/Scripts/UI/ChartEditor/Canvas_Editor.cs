using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 채보 편집 화면 UI. EditorScene이 그리드 Init 뒤에 Init()을 호출한다.
/// - 상단 바(채보 정보·저장 상태), 왼쪽 패널(노트 수·도구·테스트 플레이 배속),
///   오른쪽 패널(박자 스냅·그리드 높이·현재 위치 BPM·BPM 구간 목록·곡 기본 BPM으로 되돌리기·메트로놈/키음), 타임라인 정보를 갱신한다 (기획서 3-6).
/// - 왼쪽 도구 토글 상태를 CurrentTool로 알려준다 (도구 동작은 각 처리 쪽에서 이 값을 읽는다).
///   선택된 도구는 연한 주황 바탕 · 주황 테두리 · 진한 주황 글자, 나머지는 흰 바탕 · 회색 테두리 · 진한 회색 글자.
///   도구 단축키 1 · 2 · 3(ChartEditor/ToolShortNote·ToolLongNote·ToolBpmSection)은 이벤트로 알린다 (롱노트 드래그 중 확인은 EditorScene).
/// - BPM 구간: 목록 줄 클릭·Btn_ResetBpm은 이벤트로 알리고, 구간이 바뀌면(RefreshTiming) 목록·현재 BPM·타임라인 눈금을 다시 그린다.
///   현재 위치가 속한 구간이 바뀔 때만 현재 BPM 글자·목록 강조를 바꾼다 (재생 중 매 프레임 글자를 새로 만들지 않게).
/// - 마우스 휠은 입력 에셋의 ChartEditor 맵으로 받는다. 포인터가 그리드 위에 있을 때만 처리한다.
///   (휠: 현재 beat를 스냅 한 칸씩 이동, Ctrl+휠: 그리드 높이 배율 변경)
/// - 저장은 Btn_Save 클릭과 ChartEditor/Save(Ctrl+S)로 받아 EditorScene이 넘긴 저장 요청 함수를 부른다.
///   입력창에 글자를 쓰는 중이면 Ctrl+S를 무시한다.
/// - 재생은 Btn_Play 클릭과 ChartEditor/PlayPause(Space), 타임라인 슬라이더, 메트로놈 토글을 이벤트로 알린다
///   (재생 처리는 EditorPlayback이 맡는다). 입력창에 글자를 쓰는 중이면 Space를 무시한다.
/// - 실행 취소(ChartEditor/Undo, Ctrl+Z)·다시 실행(ChartEditor/Redo, Ctrl+Y · Ctrl+Shift+Z)은 이벤트로 알린다 (처리는 EditorScene).
///   입력창에 글자를 쓰는 중이면 무시한다. Ctrl+Shift+Z는 다시 실행만 받는다 (Undo 콜백에서 Shift가 눌려 있으면 무시).
/// - 확인 팝업 등 팝업이 열려 있으면 Space·Ctrl+S·Ctrl+Z·Ctrl+Y·1·2·3·휠은 동작하지 않는다 (GameManager.UI.HasOpenPopup).
/// - 폴더 이름을 Editor로 두면 Unity 특수 폴더가 되므로 UI/ChartEditor/에 둔다.
/// </summary>
public class Canvas_Editor : UIBase
{
    [Header("상단 바")]
    [SerializeField] private TMP_Text chartInfoText;
    [Tooltip("저장 상태 (Txt_SaveStatus). 처음에는 비워 둔다")]
    [SerializeField] private TMP_Text saveStatusText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button testPlayButton;
    [Tooltip("자동 테스트 (Btn_AutoTestPlay). 테스트 플레이를 자동 플레이로 시작한다")]
    [SerializeField] private Button autoTestPlayButton;

    [Header("그리드")]
    [Tooltip("휠 입력을 받을 영역 (Img_Grid)")]
    [SerializeField] private RectTransform gridRect;
    [Tooltip("그리드 아래 조작 안내 (Txt_ControlHint). 문구는 GameConstants.Editor.ControlHint")]
    [SerializeField] private TMP_Text controlHintText;

    [Header("왼쪽 패널: 노트 수")]
    [Tooltip("노트 수 (Txt_NoteCount). \"노트 413\" / \"롱노트 84\" 두 줄")]
    [SerializeField] private TMP_Text noteCountText;

    [Header("왼쪽 도구 (단축키 1 · 2 · 3)")]
    [SerializeField] private Toggle shortNoteToggle;
    [SerializeField] private Toggle longNoteToggle;
    [SerializeField] private Toggle bpmSectionToggle;

    [Header("왼쪽 도구: 선택 표시 색 (기획서 3-6)")]
    [SerializeField] private Color toolSelectedBackground = new Color32(0xFF, 0xF7, 0xED, 0xFF);
    [SerializeField] private Color toolSelectedBorder = new Color32(0xF9, 0x73, 0x16, 0xFF);
    [SerializeField] private Color toolSelectedText = new Color32(0xC2, 0x41, 0x0C, 0xFF);
    [SerializeField] private Color toolNormalBackground = Color.white;
    [SerializeField] private Color toolNormalBorder = new Color32(0xA1, 0xA1, 0xAA, 0xFF);
    [SerializeField] private Color toolNormalText = new Color32(0x27, 0x27, 0x2A, 0xFF);
    [Tooltip("선택된 도구 테두리 두께 (px). 선택 안 된 도구는 1px 더 얇다")]
    [Min(1f)] [SerializeField] private float toolSelectedBorderWidth = 3f;

    [Header("왼쪽 도구: 테스트 플레이 배속 (버튼으로만 바꾼다, 단축키 없음)")]
    [Tooltip("배속 내림 (Btn_SpeedDown, −0.1)")]
    [SerializeField] private Button speedDownButton;
    [Tooltip("배속 올림 (Btn_SpeedUp, +0.1)")]
    [SerializeField] private Button speedUpButton;
    [Tooltip("지금 테스트 플레이 배속 (Txt_Speed)")]
    [SerializeField] private TMP_Text speedText;

    [Header("오른쪽 패널")]
    [SerializeField] private TMP_Dropdown beatSnapDropdown;
    [SerializeField] private TMP_Text gridHeightText;
    [Tooltip("선 간격이 좁을 때 보여주는 안내 (Txt_GridHeightHint). 기준은 GameConstants.Editor.MinSnapLineSpacingPixels")]
    [SerializeField] private TMP_Text gridHeightHintText;

    [Header("오른쪽 패널: BPM (현재 위치)")]
    [Tooltip("현재 위치 선의 BPM (Txt_CurrentBpm)")]
    [SerializeField] private TMP_Text currentBpmText;
    [Tooltip("다음 구간 \"다음: 마디 17 → 200\" (Txt_NextBpm). 다음 구간이 없으면 비운다")]
    [SerializeField] private TMP_Text nextBpmText;

    [Header("오른쪽 패널: BPM 구간")]
    [SerializeField] private ScrollView_BpmSections bpmSectionList;
    [Tooltip("곡 기본 BPM으로 되돌리기 (Btn_ResetBpm). 곡 기본 BPM 1개 상태면 비활성")]
    [SerializeField] private Button resetBpmButton;
    [Tooltip("BPM 구간 추가 · 수정 창")]
    [SerializeField] private Popup_BpmSection bpmSectionPopup;

    [Header("타임라인")]
    [SerializeField] private TMP_Text timeInfoText;
    [SerializeField] private Button playButton;
    [Tooltip("재생 버튼 아이콘 글자 (Txt_Play). 정지 중 ▶, 재생 중 ❚❚")]
    [SerializeField] private TMP_Text playIconText;
    [SerializeField] private Slider_Timeline timelineSlider;

    [Header("재생 설정 (재생은 항상 1배)")]
    [SerializeField] private Toggle metronomeToggle;
    [Tooltip("에디터 키음 (Toggle_KeySound). 재생 중 노트가 현재 위치 선에 닿을 때 키음을 낸다")]
    [SerializeField] private Toggle keySoundToggle;

    [Header("입력")]
    [Tooltip("입력 에셋의 ChartEditor/Scroll (마우스 휠 y)")]
    [SerializeField] private InputActionReference scrollAction;
    [Tooltip("입력 에셋의 ChartEditor/ZoomModifier (Ctrl)")]
    [SerializeField] private InputActionReference zoomModifierAction;
    [Tooltip("입력 에셋의 ChartEditor/Point (포인터 위치)")]
    [SerializeField] private InputActionReference pointAction;
    [Tooltip("입력 에셋의 ChartEditor/Save (Ctrl+S)")]
    [SerializeField] private InputActionReference saveAction;
    [Tooltip("입력 에셋의 ChartEditor/PlayPause (Space)")]
    [SerializeField] private InputActionReference playPauseAction;
    [Tooltip("입력 에셋의 ChartEditor/Undo (Ctrl+Z)")]
    [SerializeField] private InputActionReference undoAction;
    [Tooltip("입력 에셋의 ChartEditor/Redo (Ctrl+Y · Ctrl+Shift+Z)")]
    [SerializeField] private InputActionReference redoAction;
    [Tooltip("입력 에셋의 ChartEditor/ToolShortNote (1)")]
    [SerializeField] private InputActionReference toolShortNoteAction;
    [Tooltip("입력 에셋의 ChartEditor/ToolLongNote (2)")]
    [SerializeField] private InputActionReference toolLongNoteAction;
    [Tooltip("입력 에셋의 ChartEditor/ToolBpmSection (3)")]
    [SerializeField] private InputActionReference toolBpmSectionAction;

    /// <summary>도구 단축키 1 · 2 · 3 요청 (EditorScene이 롱노트 드래그 중이 아니면 SetTool)</summary>
    public event Action<EditorTool> ToolShortcutRequested;

    /// <summary>BPM 구간 목록 줄 클릭 (구간 tick)</summary>
    public event Action<int> BpmSectionChosen;

    /// <summary>곡 기본 BPM으로 되돌리기 (Btn_ResetBpm)</summary>
    public event Action ResetBpmRequested;

    /// <summary>BPM 구간 추가 · 수정 창</summary>
    public Popup_BpmSection BpmSectionPopup => bpmSectionPopup;

    /// <summary>BPM 구간 목록 (확인용)</summary>
    public ScrollView_BpmSections BpmSectionList => bpmSectionList;

    /// <summary>실행 취소 요청 (Ctrl+Z)</summary>
    public event Action UndoRequested;

    /// <summary>다시 실행 요청 (Ctrl+Y · Ctrl+Shift+Z)</summary>
    public event Action RedoRequested;

    /// <summary>재생·정지 요청 (Btn_Play·Space)</summary>
    public event Action PlayToggleRequested;

    /// <summary>휠로 위치·높이를 바꾸기 직전 (재생 중이면 먼저 정지하도록)</summary>
    public event Action ManualMoveStarting;

    /// <summary>타임라인 슬라이더를 누르기 시작함</summary>
    public event Action SeekStarted;

    /// <summary>타임라인 슬라이더를 끄는 중 (0~1)</summary>
    public event Action<float> SeekDragged;

    /// <summary>타임라인 슬라이더를 놓음 (0~1)</summary>
    public event Action<float> SeekReleased;

    /// <summary>메트로놈 켜기·끄기</summary>
    public event Action<bool> MetronomeChanged;

    /// <summary>에디터 키음 켜기·끄기</summary>
    public event Action<bool> KeySoundChanged;

    /// <summary>에디터 키음 토글이 켜져 있으면 true</summary>
    public bool IsKeySoundOn => keySoundToggle != null && keySoundToggle.isOn;

    private string pauseIcon = GameConstants.Editor.PauseIcon;

    private Canvas canvas;
    private ChartGridView grid;
    private ChartEditSession session;
    private ChartEditData data;
    private Action onSaveRequested;
    private Action onExitRequested;
    private Action onTestPlayRequested;
    private Action onAutoTestPlayRequested;

    /// <summary>마지막 저장 결과 문구. 나가기 안내가 끝나면 이 문구로 되돌린다</summary>
    private string lastSaveStatus = string.Empty;
    private bool isInputEnabled;

    /// <summary>현재 BPM 글자·목록 강조를 마지막으로 맞춘 구간 번호. 구간이 바뀌면 −1로 돌려 다시 맞춘다</summary>
    private int shownSectionIndex = -1;

    private readonly List<float> markerPositions = new List<float>();
    private readonly List<string> markerLabels = new List<string>();

    /// <summary>
    /// 지금 켜진 도구. 토글이 모두 꺼져 있으면 숏노트로 본다 (ToggleGroup이 하나는 켜 두므로 보통 생기지 않음).
    /// </summary>
    /// <summary>테스트 플레이 배속 단계 (배속 × 10, ScrollSpeedRules)</summary>
    private int testPlaySpeedSteps = ScrollSpeedRules.ToSteps(GameConstants.Play.DefaultScrollMultiplier);

    /// <summary>테스트 플레이·자동 테스트 플레이에 넘길 배속 (1.0 ~ 8.0)</summary>
    public float TestPlayScrollMultiplier => ScrollSpeedRules.FromSteps(testPlaySpeedSteps);

    public EditorTool CurrentTool
    {
        get
        {
            if (longNoteToggle != null && longNoteToggle.isOn) return EditorTool.LongNote;
            if (bpmSectionToggle != null && bpmSectionToggle.isOn) return EditorTool.BpmSection;
            return EditorTool.ShortNote;
        }
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    /// <summary>
    /// 곡·채보 정보와 그리드·노트 상태로 화면을 채우고, 드롭다운·노트·수정 여부 변경과 저장 버튼을 구독한다.
    /// 노트는 ChartEditSession.Data를 읽기만 한다.
    /// </summary>
    /// <param name="song">편집 중인 곡 (제목·길이 표시용)</param>
    /// <param name="onSaveRequested">Btn_Save·Ctrl+S를 누르면 부를 저장 요청 (EditorScene)</param>
    /// <param name="onExitRequested">Btn_Exit를 누르면 부를 나가기 요청 (EditorScene)</param>
    /// <param name="onTestPlayRequested">Btn_TestPlay를 누르면 부를 테스트 플레이 요청 (EditorScene)</param>
    /// <param name="onAutoTestPlayRequested">Btn_AutoTestPlay를 누르면 부를 자동 테스트 요청 (EditorScene)</param>
    public void Init(ChartGridView grid, ChartEditSession session, SongData song, Action onSaveRequested, Action onExitRequested, Action onTestPlayRequested, Action onAutoTestPlayRequested)
    {
        this.grid = grid;
        this.onSaveRequested = onSaveRequested;
        this.onExitRequested = onExitRequested;
        this.onTestPlayRequested = onTestPlayRequested;
        this.onAutoTestPlayRequested = onAutoTestPlayRequested;

        if (data != null) data.OnChanged -= RefreshNoteCountText;
        data = session.Data;
        data.OnChanged += RefreshNoteCountText;

        if (this.session != null) this.session.OnDirtyChanged -= OnDirtyChanged;
        this.session = session;
        session.OnDirtyChanged += OnDirtyChanged;

        saveButton.onClick.RemoveListener(RequestSave);
        saveButton.onClick.AddListener(RequestSave);
        exitButton.onClick.RemoveListener(RequestExit);
        exitButton.onClick.AddListener(RequestExit);
        testPlayButton.onClick.RemoveListener(RequestTestPlay);
        testPlayButton.onClick.AddListener(RequestTestPlay);
        autoTestPlayButton.onClick.RemoveListener(RequestAutoTestPlay);
        autoTestPlayButton.onClick.AddListener(RequestAutoTestPlay);

        // 테스트 플레이 배속: 에디터에 들어올 때마다 1.0에서 시작한다 (테스트 플레이에서 돌아오면 EditorScene이 SetTestPlaySpeed로 되돌린다)
        if (speedDownButton != null && speedUpButton != null && speedText != null)
        {
            speedDownButton.onClick.RemoveListener(OnSpeedDownClicked);
            speedDownButton.onClick.AddListener(OnSpeedDownClicked);
            speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
            speedUpButton.onClick.AddListener(OnSpeedUpClicked);
        }
        else
        {
            Debug.LogWarning("[Canvas_Editor] 테스트 플레이 배속 버튼·글자가 연결되지 않았습니다.", this);
        }
        SetTestPlaySpeed(GameConstants.Play.DefaultScrollMultiplier);

        chartInfoText.text = string.Format(GameConstants.Editor.ChartInfoFormat, song.Title, session.ChartName, session.Level);
        lastSaveStatus = string.Empty;
        saveStatusText.text = lastSaveStatus;
        if (controlHintText != null) controlHintText.text = GameConstants.Editor.ControlHint;
        else Debug.LogWarning("[Canvas_Editor] 조작 안내(Txt_ControlHint)가 연결되지 않았습니다.", this);

        // 도구: 선택 표시 색을 토글이 바뀔 때마다 맞춘다
        foreach (Toggle toggle in ToolToggles())
        {
            if (toggle == null) continue;
            toggle.onValueChanged.RemoveListener(OnToolToggled);
            toggle.onValueChanged.AddListener(OnToolToggled);
        }
        RefreshToolStyles();

        // BPM 구간: 목록·되돌리기·입력 창
        bpmSectionList.Init(tick => BpmSectionChosen?.Invoke(tick));
        resetBpmButton.onClick.RemoveListener(RequestResetBpm);
        resetBpmButton.onClick.AddListener(RequestResetBpm);
        bpmSectionPopup.Init();

        // 재생: 버튼·슬라이더·메트로놈 (재생은 항상 1배)
        playButton.onClick.RemoveListener(RequestPlayToggle);
        playButton.onClick.AddListener(RequestPlayToggle);
        timelineSlider.Init(() => SeekStarted?.Invoke(), value => SeekDragged?.Invoke(value), value => SeekReleased?.Invoke(value));
        RefreshTiming();
        metronomeToggle.onValueChanged.RemoveListener(OnMetronomeToggled);
        metronomeToggle.SetIsOnWithoutNotify(false);
        metronomeToggle.onValueChanged.AddListener(OnMetronomeToggled);
        keySoundToggle.onValueChanged.RemoveListener(OnKeySoundToggled);
        keySoundToggle.SetIsOnWithoutNotify(GameConstants.Editor.KeySoundDefaultOn);
        keySoundToggle.onValueChanged.AddListener(OnKeySoundToggled);

        // 폰트에 ❚ 글자가 없으면 굵은 "II"로 대신한다
        pauseIcon = playIconText.font != null && playIconText.font.HasCharacters(GameConstants.Editor.PauseIcon)
            ? GameConstants.Editor.PauseIcon
            : "II";
        SetPlaying(false);

        InitBeatSnapDropdown();
        RefreshGridHeightText();
        RefreshTimeline();
        RefreshNoteCountText();
    }

    /// <summary>
    /// 재생 버튼 아이콘을 바꾼다 (정지 중 ▶, 재생 중 ❚❚).
    /// </summary>
    public void SetPlaying(bool playing)
    {
        playIconText.text = playing ? pauseIcon : GameConstants.Editor.PlayIcon;
    }

    /// <summary>
    /// BPM 구간 목록·현재 BPM·되돌리기 버튼·타임라인 눈금과 시간 표시를 세션 값으로 다시 맞춘다 (구간·offset이 바뀌었을 때).
    /// </summary>
    public void RefreshTiming()
    {
        bpmSectionList.SetSections(session.BpmSections);
        resetBpmButton.interactable = !session.IsSongDefaultBpm;
        RefreshTimelineMarkers();
        shownSectionIndex = -1;
        RefreshTimeline();
        RefreshGridHeightText();
    }

    /// <summary>
    /// 현재 위치가 속한 구간이 바뀌었으면 현재 BPM·다음 구간 글자와 목록 강조를 맞춘다.
    /// </summary>
    private void RefreshCurrentBpm()
    {
        IReadOnlyList<BpmSection> sections = session.BpmSections;
        int index = BpmSectionRules.IndexAt(sections, BeatGridMath.BeatToTick(grid.CurrentBeat));
        if (index == shownSectionIndex) return;

        shownSectionIndex = index;
        currentBpmText.text = SongData.FormatBpm(sections[index].Bpm);
        nextBpmText.text = index + 1 < sections.Count
            ? string.Format(GameConstants.Editor.NextBpmFormat, BpmSectionRules.FormatShortPosition(sections[index + 1].Tick), SongData.FormatBpm(sections[index + 1].Bpm))
            : string.Empty;
        bpmSectionList.SetCurrent(index);
        // 스냅 선 간격 안내는 현재 위치 BPM 기준이다
        RefreshGridHeightText();
    }

    /// <summary>타임라인 위 BPM 구간 눈금 (곡 시작 구간 제외)</summary>
    private void RefreshTimelineMarkers()
    {
        markerPositions.Clear();
        markerLabels.Clear();
        float length = session.SongLengthSeconds;
        IReadOnlyList<BpmSection> sections = session.BpmSections;
        for (int i = 1; i < sections.Count; i++)
        {
            float seconds = (float)session.Timing.BeatToSeconds(sections[i].Beat);
            markerPositions.Add(length > 0f ? seconds / length : 0f);
            markerLabels.Add(SongData.FormatBpm(sections[i].Bpm));
        }
        timelineSlider.SetBpmMarkers(markerPositions, markerLabels);
    }

    /// <summary>
    /// "현재 시간 / 곡 길이 · 마디 N"과 타임라인 슬라이더(현재 시간 ÷ 곡 길이)를 지금 위치로 맞춘다.
    /// 재생 중에는 매 프레임 불린다.
    /// </summary>
    public void RefreshTimeline()
    {
        RefreshTimeInfoText();
        RefreshCurrentBpm();

        float length = session.SongLengthSeconds;
        float current = (float)session.Timing.BeatToSeconds(grid.CurrentBeat);
        timelineSlider.SetPosition(length > 0f ? current / length : 0f);
    }

    /// <summary>
    /// 휠·저장 단축키 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (scrollAction == null || zoomModifierAction == null || pointAction == null || saveAction == null || playPauseAction == null)
        {
            Debug.LogWarning("[Canvas_Editor] 입력 액션이 연결되지 않아 휠·Ctrl+S·Space 조작을 받을 수 없습니다.", this);
            return;
        }

        scrollAction.action.performed += OnScroll;
        saveAction.action.performed += OnSaveShortcut;
        playPauseAction.action.performed += OnPlayPauseShortcut;
        if (undoAction != null && redoAction != null)
        {
            undoAction.action.performed += OnUndoShortcut;
            redoAction.action.performed += OnRedoShortcut;
            undoAction.action.Enable();
            redoAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[Canvas_Editor] 실행 취소·다시 실행 입력 액션이 연결되지 않아 Ctrl+Z·Ctrl+Y를 받을 수 없습니다.", this);
        }
        if (toolShortNoteAction != null && toolLongNoteAction != null && toolBpmSectionAction != null)
        {
            toolShortNoteAction.action.performed += OnToolShortNoteShortcut;
            toolLongNoteAction.action.performed += OnToolLongNoteShortcut;
            toolBpmSectionAction.action.performed += OnToolBpmSectionShortcut;
            toolShortNoteAction.action.Enable();
            toolLongNoteAction.action.Enable();
            toolBpmSectionAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[Canvas_Editor] 도구 단축키 입력 액션이 연결되지 않아 1·2·3을 받을 수 없습니다.", this);
        }
        scrollAction.action.Enable();
        zoomModifierAction.action.Enable();
        pointAction.action.Enable();
        saveAction.action.Enable();
        playPauseAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        scrollAction.action.performed -= OnScroll;
        saveAction.action.performed -= OnSaveShortcut;
        playPauseAction.action.performed -= OnPlayPauseShortcut;
        if (undoAction != null) undoAction.action.performed -= OnUndoShortcut;
        if (redoAction != null) redoAction.action.performed -= OnRedoShortcut;
        if (toolShortNoteAction != null) toolShortNoteAction.action.performed -= OnToolShortNoteShortcut;
        if (toolLongNoteAction != null) toolLongNoteAction.action.performed -= OnToolLongNoteShortcut;
        if (toolBpmSectionAction != null) toolBpmSectionAction.action.performed -= OnToolBpmSectionShortcut;
        isInputEnabled = false;
    }

    /// <summary>
    /// 저장 결과를 표시한다. 성공: "저장됨 14:32", 실패: "저장 실패" (이유는 EditorScene이 콘솔에 남긴다).
    /// </summary>
    public void ShowSaveResult(bool saved)
    {
        SetSaveStatus(saved
            ? string.Format(GameConstants.Editor.SaveStatusSaved, DateTime.Now.ToString(GameConstants.Editor.SaveTimeFormat, CultureInfo.InvariantCulture))
            : GameConstants.Editor.SaveStatusFailed);
    }

    /// <summary>
    /// 저장 상태 자리에 안내를 잠깐 표시한다. 되돌릴 때는 RefreshSaveStatus를 부른다 (EditorScene이 시간을 잰다).
    /// </summary>
    public void ShowStatusMessage(string message)
    {
        saveStatusText.text = message;
    }

    /// <summary>
    /// 도구 토글을 바꾼다 (테스트 플레이에서 돌아와 복원할 때).
    /// </summary>
    /// <summary>
    /// 테스트 플레이 배속을 정한다 (Init의 기본값, 테스트 플레이에서 돌아와 되돌릴 때). 범위·단위 밖이면 맞춘다.
    /// </summary>
    public void SetTestPlaySpeed(float multiplier)
    {
        testPlaySpeedSteps = ScrollSpeedRules.ToSteps(multiplier);
        RefreshSpeed();
    }

    private void OnSpeedDownClicked() => StepTestPlaySpeed(-1);
    private void OnSpeedUpClicked() => StepTestPlaySpeed(1);

    /// <summary>테스트 플레이 배속을 한 단계(0.1) 바꾼다. 범위(1.0 ~ 8.0) 끝에서는 멈춘다.</summary>
    private void StepTestPlaySpeed(int delta)
    {
        int next = ScrollSpeedRules.Step(testPlaySpeedSteps, delta);
        if (next == testPlaySpeedSteps) return;

        testPlaySpeedSteps = next;
        RefreshSpeed();
    }

    /// <summary>배속 글자를 고치고, 범위 끝이면 그쪽 버튼을 끈다.</summary>
    private void RefreshSpeed()
    {
        if (speedText != null) speedText.text = string.Format(GameConstants.Editor.TestPlaySpeedFormat, TestPlayScrollMultiplier);
        if (speedDownButton != null) speedDownButton.interactable = testPlaySpeedSteps > ScrollSpeedRules.MinSteps;
        if (speedUpButton != null) speedUpButton.interactable = testPlaySpeedSteps < ScrollSpeedRules.MaxSteps;
    }

    public void SetTool(EditorTool tool)
    {
        Toggle target = shortNoteToggle;
        if (tool == EditorTool.LongNote) target = longNoteToggle;
        else if (tool == EditorTool.BpmSection) target = bpmSectionToggle;

        if (target != null) target.isOn = true;
        RefreshToolStyles();
    }

    private IEnumerable<Toggle> ToolToggles()
    {
        yield return shortNoteToggle;
        yield return longNoteToggle;
        yield return bpmSectionToggle;
    }

    private void OnToolToggled(bool on)
    {
        RefreshToolStyles();
    }

    /// <summary>
    /// 도구 버튼 색을 선택 상태에 맞춘다: 바탕은 Toggle의 targetGraphic, 테두리는 같은 오브젝트의 Outline, 글자는 자식 TMP.
    /// </summary>
    private void RefreshToolStyles()
    {
        foreach (Toggle toggle in ToolToggles())
        {
            if (toggle == null) continue;
            bool on = toggle.isOn;
            if (toggle.targetGraphic != null) toggle.targetGraphic.color = on ? toolSelectedBackground : toolNormalBackground;
            Outline outline = toggle.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = on ? toolSelectedBorder : toolNormalBorder;
                float width = on ? toolSelectedBorderWidth : Mathf.Max(1f, toolSelectedBorderWidth - 1f);
                outline.effectDistance = new Vector2(width, -width);
            }
            TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = on ? toolSelectedText : toolNormalText;
        }
    }

    /// <summary>
    /// 잠깐 보여준 안내를 끝내고 저장 상태 표시(마지막 저장 결과 또는 "저장 안 됨")로 되돌린다.
    /// </summary>
    public void RefreshSaveStatus()
    {
        saveStatusText.text = session != null && session.IsDirty ? GameConstants.Editor.SaveStatusUnsaved : lastSaveStatus;
    }

    private void SetSaveStatus(string status)
    {
        lastSaveStatus = status;
        saveStatusText.text = status;
    }

    private void OnDestroy()
    {
        DisableInput();

        if (beatSnapDropdown != null) beatSnapDropdown.onValueChanged.RemoveListener(OnBeatSnapChanged);
        if (saveButton != null) saveButton.onClick.RemoveListener(RequestSave);
        if (resetBpmButton != null) resetBpmButton.onClick.RemoveListener(RequestResetBpm);
        foreach (Toggle toggle in ToolToggles())
            if (toggle != null) toggle.onValueChanged.RemoveListener(OnToolToggled);
        if (exitButton != null) exitButton.onClick.RemoveListener(RequestExit);
        if (testPlayButton != null) testPlayButton.onClick.RemoveListener(RequestTestPlay);
        if (autoTestPlayButton != null) autoTestPlayButton.onClick.RemoveListener(RequestAutoTestPlay);
        if (playButton != null) playButton.onClick.RemoveListener(RequestPlayToggle);
        if (metronomeToggle != null) metronomeToggle.onValueChanged.RemoveListener(OnMetronomeToggled);
        if (keySoundToggle != null) keySoundToggle.onValueChanged.RemoveListener(OnKeySoundToggled);
        if (speedDownButton != null) speedDownButton.onClick.RemoveListener(OnSpeedDownClicked);
        if (speedUpButton != null) speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
        if (data != null) data.OnChanged -= RefreshNoteCountText;
        if (session != null) session.OnDirtyChanged -= OnDirtyChanged;
    }

    /// <summary>
    /// 저장 뒤 다시 편집하면 "저장 안 됨"을, 실행 취소로 저장 시점에 돌아오면 마지막 저장 결과("저장됨 14:32")를 표시한다.
    /// 저장 성공 표시는 ShowSaveResult가 맡는다. 마지막 저장 결과 문구(lastSaveStatus)는 여기서 덮어쓰지 않는다.
    /// </summary>
    private void OnDirtyChanged(bool isDirty)
    {
        RefreshSaveStatus();
    }

    private void RequestSave()
    {
        onSaveRequested?.Invoke();
    }

    private void RequestResetBpm()
    {
        ResetBpmRequested?.Invoke();
    }

    private void RequestExit()
    {
        onExitRequested?.Invoke();
    }

    private void RequestTestPlay()
    {
        onTestPlayRequested?.Invoke();
    }

    private void RequestAutoTestPlay()
    {
        onAutoTestPlayRequested?.Invoke();
    }

    private void RequestPlayToggle()
    {
        PlayToggleRequested?.Invoke();
    }

    /// <summary>
    /// Space. 입력창에 글자를 쓰는 중이면 무시한다.
    /// </summary>
    private void OnPlayPauseShortcut(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked()) return;
        RequestPlayToggle();
    }

    /// <summary>
    /// Ctrl+Z. 입력창에 글자를 쓰는 중이거나 팝업이 열려 있으면 무시한다.
    /// Ctrl+Shift+Z는 다시 실행이므로 Shift가 같이 눌려 있으면 무시한다 (OneModifier 조합은 Shift가 더 눌려도 발동한다).
    /// </summary>
    private void OnUndoShortcut(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked()) return;
        if (context.control != null && context.control.device is Keyboard keyboard && keyboard.shiftKey.isPressed) return;
        UndoRequested?.Invoke();
    }

    /// <summary>
    /// Ctrl+Y · Ctrl+Shift+Z. 입력창에 글자를 쓰는 중이거나 팝업이 열려 있으면 무시한다.
    /// </summary>
    private void OnRedoShortcut(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked()) return;
        RedoRequested?.Invoke();
    }

    private void OnToolShortNoteShortcut(InputAction.CallbackContext context) => RequestToolShortcut(EditorTool.ShortNote);
    private void OnToolLongNoteShortcut(InputAction.CallbackContext context) => RequestToolShortcut(EditorTool.LongNote);
    private void OnToolBpmSectionShortcut(InputAction.CallbackContext context) => RequestToolShortcut(EditorTool.BpmSection);

    /// <summary>도구 단축키 1 · 2 · 3. 팝업이 열려 있거나 입력창에 글자를 쓰는 중이면 무시한다</summary>
    private void RequestToolShortcut(EditorTool tool)
    {
        if (IsShortcutBlocked()) return;
        ToolShortcutRequested?.Invoke(tool);
    }

    private void OnMetronomeToggled(bool on)
    {
        MetronomeChanged?.Invoke(on);
    }

    private void OnKeySoundToggled(bool on)
    {
        KeySoundChanged?.Invoke(on);
    }

    /// <summary>
    /// Ctrl+S. 입력창에 글자를 쓰는 중이면 무시한다.
    /// </summary>
    private void OnSaveShortcut(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked()) return;
        RequestSave();
    }

    /// <summary>
    /// 단축키를 막아야 하면 true: 팝업이 열려 있거나 입력창에 글자를 쓰는 중일 때.
    /// </summary>
    private static bool IsShortcutBlocked()
    {
        return (UIManager.HasInstance && GameManager.UI.HasOpenPopup) || IsTypingInInputField();
    }

    private static bool IsTypingInInputField()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null) return false;

        TMP_InputField input = eventSystem.currentSelectedGameObject.GetComponent<TMP_InputField>();
        return input != null && input.isFocused;
    }

    /// <summary>
    /// 드롭다운 선택지를 GameConstants의 스냅 목록(표시 이름)으로 채우고 그리드의 현재 스냅을 고른다.
    /// </summary>
    private void InitBeatSnapDropdown()
    {
        GameConstants.Editor.SnapOption[] snaps = GameConstants.Editor.SnapOptions;
        List<string> options = new List<string>(snaps.Length);
        int selected = 0;
        for (int i = 0; i < snaps.Length; i++)
        {
            options.Add(snaps[i].Label);
            if (snaps[i].Division == grid.SnapDivision) selected = i;
        }

        beatSnapDropdown.onValueChanged.RemoveListener(OnBeatSnapChanged);
        beatSnapDropdown.ClearOptions();
        beatSnapDropdown.AddOptions(options);
        beatSnapDropdown.SetValueWithoutNotify(selected);
        beatSnapDropdown.RefreshShownValue();
        beatSnapDropdown.onValueChanged.AddListener(OnBeatSnapChanged);
    }

    private void OnBeatSnapChanged(int index)
    {
        GameConstants.Editor.SnapOption[] snaps = GameConstants.Editor.SnapOptions;
        if (index < 0 || index >= snaps.Length) return;

        grid.SetSnap(snaps[index].Division);
        RefreshGridHeightText();
    }

    /// <summary>
    /// 휠 한 번에 한 칸. Windows는 한 칸에 120씩 들어오므로 값의 부호만 쓴다.
    /// 휠을 올리면 뒤 시간(위)으로 간다.
    /// </summary>
    private void OnScroll(InputAction.CallbackContext context)
    {
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;

        float value = context.ReadValue<float>();
        if (Mathf.Approximately(value, 0f) || grid == null || !IsPointerOverGrid()) return;

        int direction = value > 0f ? 1 : -1;

        if (zoomModifierAction.action.IsPressed())
        {
            grid.SetGridHeight(grid.GridHeightScale + direction * GameConstants.Editor.GridHeightStep);
            RefreshGridHeightText();
        }
        else
        {
            // 재생 중이면 먼저 정지한다 (정지하면 스냅 선에 맞춘 뒤 한 칸 이동)
            ManualMoveStarting?.Invoke();
            grid.SetCurrentBeat(BeatGridMath.StepBeat(grid.CurrentBeat, grid.SnapDivision, direction));
            RefreshTimeline();
        }
    }

    private bool IsPointerOverGrid()
    {
        Vector2 pointer = pointAction.action.ReadValue<Vector2>();
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(gridRect, pointer, eventCamera);
    }

    /// <summary>
    /// "x1.5 (Ctrl+휠)" 형식으로 그리드 높이 배율을 표시하고, 지금 스냅의 선 간격이 좁으면 안내를 보여준다.
    /// 스냅·그리드 높이·BPM이 바뀔 때 부른다.
    /// </summary>
    private void RefreshGridHeightText()
    {
        string scale = grid.GridHeightScale.ToString("0.0#", CultureInfo.InvariantCulture);
        gridHeightText.text = $"x{scale} (Ctrl+휠)";

        // 선 간격은 현재 위치의 BPM으로 본다
        bool tooDense = BeatGridMath.IsSnapLineSpacingTooSmall(session.Timing.BpmAt(grid.CurrentBeat), grid.GridHeightScale, grid.SnapDivision);
        gridHeightHintText.text = GameConstants.Editor.DenseGridHint;
        gridHeightHintText.gameObject.SetActive(tooDense);
    }

    /// <summary>
    /// "노트 N" / "롱노트 M" 두 줄(NoteCountFormat)로 노트 수를 표시한다. N은 숏노트와 롱노트를 합한 수다.
    /// </summary>
    private void RefreshNoteCountText()
    {
        noteCountText.text = string.Format(GameConstants.Editor.NoteCountFormat, data.ShortCount + data.LongCount, data.LongCount);
    }

    /// <summary>
    /// "현재 위치 시간 / 곡 길이 · 마디 N"을 표시한다. 현재 위치 시간은 현재 박을 offset·BPM으로 바꾼 값이다.
    /// </summary>
    private void RefreshTimeInfoText()
    {
        float currentSeconds = (float)session.Timing.BeatToSeconds(grid.CurrentBeat);
        timeInfoText.text = string.Format(GameConstants.Editor.TimeInfoFormat,
            TimeFormat.ToMinSecMillis(currentSeconds), TimeFormat.ToMinSec(session.SongLengthSeconds), grid.CurrentMeasure);
    }
}
