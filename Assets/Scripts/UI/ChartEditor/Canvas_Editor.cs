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
/// - 11/19 라벤더 톤 (기획서 3-6 「에디터 라벤더 톤 개편」): 배경 MenuBackground(아주 옅은 일러스트 + 세로 그라데이션), 흰 유리 패널,
///   상단 바 "에디터 EDITOR" 제목 · 앨범 · 곡 제목 / CUSTOM 뱃지 · 채보 이름 · Lv · "● 저장 안 됨"(변경이 있을 때만),
///   버튼 4개(저장 [Ctrl+S] · 자동 테스트 보라 ◎ · 테스트 플레이 하늘 ▶ · 나가기 [Esc]), 맨 아래 키 안내 줄(KeyGuideBar). 색은 모두 UITheme.
/// - 왼쪽 도구 토글 상태를 CurrentTool로 알려준다 (도구 동작은 각 처리 쪽에서 이 값을 읽는다).
///   고른 도구는 하늘 14% 바탕 · 하늘 1.5px 테두리 · 선택 링(흰 3 + 하늘 6) · 글자 #2F7FC8, 나머지는 흰 바탕 · 흐린 테두리 · 기본 글자 (UITheme 에디터 묶음).
///   아래에 고른 도구 사용법 한 줄. 키 칩 [1][2]는 입력 에셋 바인딩에서 읽는다.
/// - 테스트 플레이 배속: 마지막으로 쓴 값을 PlayerData(lastSelection.editorTestPlayMultiplier)에 저장하고, 들어올 때 그 값으로 시작한다 (처음 1.0, 인게임 배속과 따로).
/// - 「테스트 후 결과 보기」 스위치 (Toggle_ShowTestResult, 11/20): 테스트 플레이 배속 아래. 켜짐이면 테스트 끝 → 결과(연출 없음) → 에디터, 꺼짐이면 바로 에디터.
///   값은 PlayerData(lastSelection.editorShowTestResult, 처음 true)에 저장하고 Init에서 읽는다. 설명 줄(Txt_ShowTestResultHint)은 값에 따라 바뀐다.
/// - 저장 상태 글자(Txt_SaveStatus): 저장하지 않은 변경이 있으면 "● 저장 안 됨"(호박), 저장된 상태면 숨김.
///   잠깐 띄우는 안내(테스트 플레이 안내·BPM 구간 결과 등)와 저장 실패도 같은 자리에 띄우고, 시간이 지나면(EditorScene) 저장 상태로 되돌린다. 저장 성공은 "● 저장 안 됨"이 사라지는 것으로 알린다.
///   도구 단축키 1 · 2(ChartEditor/ToolNote·ToolBpmSection)는 이벤트로 알린다 (롱노트 드래그 중 확인은 EditorScene).
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
/// - 확인 팝업 등 팝업이 열려 있으면 Space·Ctrl+S·Ctrl+Z·Ctrl+Y·1·2·휠은 동작하지 않는다 (GameManager.UI.HasOpenPopup).
/// - 폴더 이름을 Editor로 두면 Unity 특수 폴더가 되므로 UI/ChartEditor/에 둔다.
/// </summary>
public class Canvas_Editor : UIBase
{
    [Header("배경")]
    [Tooltip("라벤더 + 아주 옅은 곡 일러스트 + 세로 그라데이션 (Prefabs/Common/MenuBackground)")]
    [SerializeField] private MenuBackground menuBackground;

    [Header("상단 바")]
    [SerializeField] private TMP_Text screenTitleText;
    [SerializeField] private TMP_Text screenTitleEnText;
    [Tooltip("곡 앨범 (Img_Album, 60×60)")]
    [SerializeField] private Image albumImage;
    [SerializeField] private TMP_Text songTitleText;
    [Tooltip("CUSTOM 뱃지 (Img_ChartBadge)")]
    [SerializeField] private Image chartBadge;
    [SerializeField] private TMP_Text chartBadgeText;
    [SerializeField] private TMP_Text chartNameText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("저장 상태 (Txt_SaveStatus). 변경이 있을 때만 \"● 저장 안 됨\"")]
    [SerializeField] private TMP_Text saveStatusText;
    [SerializeField] private TMP_Text saveLabel;
    [SerializeField] private TMP_Text autoTestLabel;
    [SerializeField] private TMP_Text testPlayLabel;
    [SerializeField] private TMP_Text exitLabel;
    [Tooltip("저장 버튼 오른쪽 키 칩 글자 (Ctrl+S, 바인딩)")]
    [SerializeField] private TMP_Text saveKeyText;
    [Tooltip("나가기 버튼 오른쪽 키 칩 글자 (Esc)")]
    [SerializeField] private TMP_Text exitKeyText;
    [Tooltip("자동 테스트 · 테스트 플레이 버튼 그림자 (PopupShadow 스프라이트, 채움 색 × 30%)")]
    [SerializeField] private Image autoTestShadow;
    [SerializeField] private Image testPlayShadow;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button testPlayButton;
    [Tooltip("자동 테스트 (Btn_AutoTestPlay). 테스트 플레이를 자동 플레이로 시작한다")]
    [SerializeField] private Button autoTestPlayButton;

    [Header("그리드")]
    [Tooltip("휠 입력을 받을 영역 (Img_Grid)")]
    [SerializeField] private RectTransform gridRect;

    [Header("맨 아래 키 안내 줄 (그리드 아래 조작 안내를 옮김, 11/19)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    [Header("왼쪽 패널: 노트 수 (흰 카드 2개)")]
    [SerializeField] private TMP_Text notesTitleText;
    [SerializeField] private TMP_Text notesCardLabel;
    [Tooltip("노트 수 값 (Txt_NotesValue, 숏노트 + 롱노트)")]
    [SerializeField] private TMP_Text notesValueText;
    [SerializeField] private TMP_Text longNotesCardLabel;
    [Tooltip("롱노트 수 값 (Txt_LongNotesValue)")]
    [SerializeField] private TMP_Text longNotesValueText;

    [Header("왼쪽 도구 (단축키 1 · 2)")]
    [Tooltip("노트 도구 (Toggle_ShortNote). Toggle_LongNote는 노트 도구로 합쳐져 씬에서 꺼 둔다 (디자인 반영 때 지움)")]
    [SerializeField] private Toggle shortNoteToggle;
    [SerializeField] private Toggle bpmSectionToggle;

    [SerializeField] private TMP_Text toolsTitleText;
    [Tooltip("도구 이름 (Txt_ShortNote · Txt_BpmSection)")]
    [SerializeField] private TMP_Text shortNoteLabel;
    [SerializeField] private TMP_Text bpmSectionLabel;
    [Tooltip("도구 키 칩 글자 [1][2] (바인딩)")]
    [SerializeField] private TMP_Text shortNoteKeyText;
    [SerializeField] private TMP_Text bpmSectionKeyText;
    [Tooltip("고른 도구 선택 링 (Img_FocusRing, 흰 3 + 하늘 6). 고른 도구만 켠다")]
    [SerializeField] private GameObject shortNoteRing;
    [SerializeField] private GameObject bpmSectionRing;
    [Tooltip("BPM 구간 도구 ♩ 아이콘 글자 (호박 둥근 사각 위 흰 글자)")]
    [SerializeField] private TMP_Text bpmSectionIcon;
    [Tooltip("노트 도구 아이콘 막대 (고르면 하늘, 아니면 흐린 회색)")]
    [SerializeField] private Image shortNoteIcon;
    [Tooltip("고른 도구 사용법 한 줄 (Txt_ToolHint, 13, textMuted)")]
    [SerializeField] private TMP_Text toolHintText;

    [Header("왼쪽 도구: 테스트 플레이 배속 (버튼으로만 바꾼다, 단축키 없음)")]
    [Tooltip("배속 내림 (Btn_SpeedDown, −0.1)")]
    [SerializeField] private Button speedDownButton;
    [Tooltip("배속 올림 (Btn_SpeedUp, +0.1)")]
    [SerializeField] private Button speedUpButton;
    [Tooltip("지금 테스트 플레이 배속 (Txt_Speed)")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text speedTitleText;

    [Header("오른쪽 패널")]
    [SerializeField] private TMP_Text snapTitleText;
    [SerializeField] private TMP_Dropdown beatSnapDropdown;
    [SerializeField] private TMP_Text zoomTitleText;
    [SerializeField] private TMP_Text gridHeightText;
    [Tooltip("그리드 높이 키 칩 [Ctrl] 글자 (바인딩, 옆 [휠]은 고정 글자)")]
    [SerializeField] private TMP_Text zoomKeyText;
    [SerializeField] private TMP_Text zoomWheelKeyText;
    [Tooltip("선 간격이 좁을 때 보여주는 안내 (Txt_GridHeightHint). 기준은 GameConstants.Editor.MinSnapLineSpacingPixels")]
    [SerializeField] private TMP_Text gridHeightHintText;

    [Header("오른쪽 패널: BPM (현재 위치, 흰 카드 + 왼쪽 호박 막대)")]
    [SerializeField] private TMP_Text currentBpmTitleText;
    [Tooltip("BPM 카드 왼쪽 호박 막대 (Img_CurrentBpmBar)")]
    [SerializeField] private Image currentBpmBar;
    [Tooltip("현재 위치 선의 BPM (Txt_CurrentBpm)")]
    [SerializeField] private TMP_Text currentBpmText;
    [Tooltip("다음 구간 \"다음: 마디 17 → 200\" (Txt_NextBpm). 다음 구간이 없으면 비운다")]
    [SerializeField] private TMP_Text nextBpmText;

    [Header("오른쪽 패널: BPM 구간")]
    [SerializeField] private TMP_Text bpmSectionsTitleText;
    [SerializeField] private ScrollView_BpmSections bpmSectionList;
    [Tooltip("곡 BPM 구간으로 되돌리기 (Btn_ResetBpm). 곡 BPM 구간과 같은 상태면 꺼짐(회색), 켜지면 빨간 글자 + 빨간 50% 테두리")]
    [SerializeField] private Button resetBpmButton;
    [SerializeField] private TMP_Text resetBpmLabel;
    [Tooltip("되돌리기 버튼 테두리 (Img_Frame)")]
    [SerializeField] private Image resetBpmFrame;
    [Tooltip("BPM 구간 추가 · 수정 창")]
    [SerializeField] private Popup_BpmSection bpmSectionPopup;

    [Header("타임라인")]
    [Tooltip("현재 시간 (Txt_TimeInfo, 20 Bold)")]
    [SerializeField] private TMP_Text timeInfoText;
    [Tooltip("\" / 곡 길이 · 마디 N\" (Txt_TimeTotal, Regular textDescription). 마디가 바뀔 때만 다시 쓴다")]
    [SerializeField] private TMP_Text timeTotalText;
    [SerializeField] private Button playButton;
    [Tooltip("재생 버튼 아이콘 글자 (Txt_Play). 정지 중 ▶, 재생 중 ❚❚")]
    [SerializeField] private TMP_Text playIconText;
    [SerializeField] private Slider_Timeline timelineSlider;

    [Header("재생 설정 (재생은 항상 1배, 체크박스 대신 스위치 52×30 · 켜짐 하늘)")]
    [SerializeField] private Toggle metronomeToggle;
    [Tooltip("에디터 키음 (Toggle_KeySound). 재생 중 노트가 현재 위치 선에 닿을 때 키음을 낸다")]
    [SerializeField] private Toggle keySoundToggle;
    [Tooltip("스위치 바탕 (Img_SwitchTrack) · 손잡이 (Img_SwitchKnob)")]
    [SerializeField] private Image metronomeTrack;
    [SerializeField] private RectTransform metronomeKnob;
    [SerializeField] private Image keySoundTrack;
    [SerializeField] private RectTransform keySoundKnob;

    [Header("테스트 후 결과 보기 (왼쪽 패널, 메트로놈 · 키음과 같은 스위치 52×30)")]
    [SerializeField] private Toggle showTestResultToggle;
    [SerializeField] private TMP_Text showTestResultLabel;
    [SerializeField] private Image showTestResultTrack;
    [SerializeField] private RectTransform showTestResultKnob;
    [Tooltip("설명 한 줄 (켜짐 / 꺼짐 문구, GameConstants.Editor.ShowTestResultHintOn · Off)")]
    [SerializeField] private TMP_Text showTestResultHint;

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
    [Tooltip("입력 에셋의 ChartEditor/ToolNote (1)")]
    [SerializeField] private InputActionReference toolNoteAction;
    [Tooltip("입력 에셋의 ChartEditor/ToolBpmSection (2)")]
    [SerializeField] private InputActionReference toolBpmSectionAction;

    /// <summary>도구 단축키 1 · 2 요청 (EditorScene이 롱노트 드래그 중이 아니면 SetTool)</summary>
    public event Action<EditorTool> ToolShortcutRequested;

    /// <summary>BPM 구간 목록 줄 클릭 (구간 tick)</summary>
    public event Action<int> BpmSectionChosen;

    /// <summary>곡 BPM 구간으로 되돌리기 (Btn_ResetBpm)</summary>
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

    private bool isInputEnabled;

    /// <summary>시간 뒤쪽 글자를 마지막으로 쓴 마디 (바뀔 때만 다시 쓴다)</summary>
    private int shownTimeMeasure = -1;
    private float shownSongLength = -1f;

    /// <summary>현재 BPM 글자·목록 강조를 마지막으로 맞춘 구간 번호. 구간이 바뀌면 −1로 돌려 다시 맞춘다</summary>
    private int shownSectionIndex = -1;

    private readonly List<float> markerPositions = new List<float>();
    private readonly List<string> markerLabels = new List<string>();

    /// <summary>
    /// 지금 켜진 도구. 토글이 모두 꺼져 있으면 노트 도구로 본다 (ToggleGroup이 하나는 켜 두므로 보통 생기지 않음).
    /// </summary>
    /// <summary>테스트 플레이 배속 단계 (배속 × 10, ScrollSpeedRules)</summary>
    private int testPlaySpeedSteps = ScrollSpeedRules.ToSteps(GameConstants.Play.DefaultScrollMultiplier);

    /// <summary>테스트 플레이·자동 테스트 플레이에 넘길 배속 (1.0 ~ 8.0)</summary>
    public float TestPlayScrollMultiplier => ScrollSpeedRules.FromSteps(testPlaySpeedSteps);

    /// <summary>「테스트 후 결과 보기」 스위치 값 (켜짐이면 테스트 끝 → 결과 → 에디터)</summary>
    public bool ShowTestResult => showTestResultToggle == null ? GameConstants.Editor.ShowTestResultDefaultOn : showTestResultToggle.isOn;

    /// <summary>「테스트 후 결과 보기」 스위치 · 설명 (확인용)</summary>
    public Toggle ShowTestResultToggle => showTestResultToggle;
    public TMP_Text ShowTestResultHint => showTestResultHint;

    public EditorTool CurrentTool
    {
        get
        {
            if (bpmSectionToggle != null && bpmSectionToggle.isOn) return EditorTool.BpmSection;
            return EditorTool.Note;
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

        // 테스트 플레이 배속: 마지막으로 쓴 값(PlayerData)으로 시작한다 (테스트 플레이에서 돌아오면 EditorScene이 SetTestPlaySpeed로 되돌린다)
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
        SetTestPlaySpeed(PlayerDataManager.HasInstance ? GameManager.PlayerData.LastEditorTestPlayMultiplier : GameConstants.Play.DefaultScrollMultiplier);

        // 테스트 후 결과 보기: 저장값으로 시작하고, 바꿀 때 저장한다
        if (showTestResultToggle != null)
        {
            if (showTestResultLabel != null) showTestResultLabel.text = GameConstants.Editor.ShowTestResultLabel;
            showTestResultToggle.onValueChanged.RemoveListener(OnShowTestResultToggled);
            showTestResultToggle.SetIsOnWithoutNotify(PlayerDataManager.HasInstance ? GameManager.PlayerData.LastEditorShowTestResult : GameConstants.Editor.ShowTestResultDefaultOn);
            showTestResultToggle.onValueChanged.AddListener(OnShowTestResultToggled);
        }
        else
        {
            Debug.LogWarning("[Canvas_Editor] showTestResultToggle(Toggle_ShowTestResult)이 연결되지 않았습니다.", this);
        }

        InitTheme(song, session);
        RefreshSaveStatus();

        // 도구: 선택 표시 색을 토글이 바뀔 때마다 맞춘다
        foreach (Toggle toggle in ToolToggles())
        {
            if (toggle == null) continue;
            toggle.onValueChanged.RemoveListener(OnToolToggled);
            toggle.onValueChanged.AddListener(OnToolToggled);
        }
        RefreshToolStyles();
        RefreshSwitches();

        // BPM 구간: 목록·되돌리기·입력 창
        bpmSectionList.Init(tick => BpmSectionChosen?.Invoke(tick));
        resetBpmButton.onClick.RemoveListener(RequestResetBpm);
        resetBpmButton.onClick.AddListener(RequestResetBpm);
        if (resetBpmLabel != null) resetBpmLabel.text = GameConstants.Editor.ResetBpmButtonLabel;
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
        RefreshSwitches();

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
    /// 고정 글자 · 곡 정보 · 키 칩 · 키 안내 줄 · 배경 · 버튼 그림자를 UITheme · GameConstants · 바인딩으로 채운다 (Init에서 한 번).
    /// </summary>
    private void InitTheme(SongData song, ChartEditSession session)
    {
        UITheme theme = UITheme.Current;
        // 바뀌지 않는 그래픽(패널·테두리·카드·글자)은 역할로 칠한다. 선택에 따라 바뀌는 색은 아래 Refresh 함수들이 덮어 칠한다
        UIThemeColor.ApplyAll(gameObject);
        if (menuBackground != null)
        {
            menuBackground.Init(new MenuBackgroundStyle
            {
                vertical = true,
                overlayTopAlpha = theme.editorOverlayTop,
                overlayBottomAlpha = theme.editorOverlayBottom,
                illustrationOpacity = theme.editorIllustrationOpacity,
                blurSigmaPx = theme.editorIllustrationBlur,
                saturation = theme.editorIllustrationSaturation,
                brightness = 1f
            });
            menuBackground.SetIllustration(song.Background, true);
        }

        SetText(screenTitleText, GameConstants.Editor.ScreenTitle);
        SetText(screenTitleEnText, GameConstants.Editor.ScreenTitleEn);
        if (albumImage != null)
        {
            albumImage.sprite = song.Cover;
            albumImage.color = song.Cover != null ? Color.white : theme.emptyCover;
        }
        SetText(songTitleText, song.Title);
        if (chartBadge != null) UIThemePaint.Badge(chartBadge, chartBadgeText, theme.difficultyCustom);
        SetText(chartBadgeText, GameConstants.Play.ChartBadgeCustomText);
        SetText(chartNameText, session.ChartName);
        SetText(levelText, string.Format(GameConstants.Editor.LevelFormat, session.Level));

        SetText(saveLabel, GameConstants.Editor.SaveButtonLabel);
        SetText(autoTestLabel, GameConstants.Editor.AutoTestButtonLabel);
        SetText(testPlayLabel, GameConstants.Editor.TestPlayButtonLabel);
        SetText(exitLabel, GameConstants.Editor.ExitButtonLabel);
        SetText(saveKeyText, KeyGuideBar.GetKeyText(saveAction != null ? saveAction.action : null, "Ctrl+S"));
        SetText(exitKeyText, GameConstants.Editor.KeyEsc);
        if (autoTestPlayButton != null && autoTestPlayButton.targetGraphic != null) autoTestPlayButton.targetGraphic.color = theme.editorAutoTest;
        if (testPlayButton != null && testPlayButton.targetGraphic != null) testPlayButton.targetGraphic.color = theme.accent;
        if (autoTestShadow != null) autoTestShadow.color = UITheme.WithAlpha(theme.editorAutoTest, theme.editorButtonShadowAlpha);
        if (testPlayShadow != null) testPlayShadow.color = UITheme.WithAlpha(theme.accent, theme.editorButtonShadowAlpha);

        SetText(notesTitleText, SectionLabel(GameConstants.Editor.NotesTitle, GameConstants.Editor.NotesTitleEn));
        SetText(notesCardLabel, GameConstants.Editor.NotesCardLabel);
        SetText(longNotesCardLabel, GameConstants.Editor.LongNotesCardLabel);
        SetText(toolsTitleText, SectionLabel(GameConstants.Editor.ToolsTitle, GameConstants.Editor.ToolsTitleEn));
        SetText(shortNoteLabel, GameConstants.Editor.ToolNoteName);
        SetText(bpmSectionLabel, GameConstants.Editor.ToolBpmSectionName);
        SetText(shortNoteKeyText, KeyGuideBar.GetKeyText(toolNoteAction != null ? toolNoteAction.action : null, "1"));
        SetText(bpmSectionKeyText, KeyGuideBar.GetKeyText(toolBpmSectionAction != null ? toolBpmSectionAction.action : null, "2"));
        if (bpmSectionIcon != null)
        {
            bpmSectionIcon.text = GameConstants.Editor.BpmSectionIcon;
            bpmSectionIcon.color = theme.onAccentText;
        }
        if (toolHintText != null) toolHintText.color = theme.textMuted;
        SetText(speedTitleText, SectionLabel(GameConstants.Editor.SpeedTitle, GameConstants.Editor.SpeedTitleEn));

        SetText(snapTitleText, SectionLabel(GameConstants.Editor.SnapTitle, GameConstants.Editor.SnapTitleEn));
        SetText(zoomTitleText, SectionLabel(GameConstants.Editor.ZoomTitle, GameConstants.Editor.ZoomTitleEn));
        SetText(zoomKeyText, KeyGuideBar.GetKeyText(zoomModifierAction != null ? zoomModifierAction.action : null, "Ctrl"));
        SetText(zoomWheelKeyText, GameConstants.Editor.KeyWheelUpDown);
        LayoutZoomKeys();
        SetText(currentBpmTitleText, SectionLabel(GameConstants.Editor.CurrentBpmTitle, GameConstants.Editor.CurrentBpmTitleEn));
        if (currentBpmBar != null) currentBpmBar.color = theme.editorAmber;

        if (timeTotalText != null) timeTotalText.color = theme.textDescription;
        shownTimeMeasure = -1;
        shownSongLength = -1f;

        InitKeyGuide();
    }

    /// <summary>
    /// 그리드 높이 칸의 키 칩 [Ctrl] + [휠 ▲▼]을 글자 폭에 맞춰 늘리고, 오른쪽 끝(ZoomKeyRightX)에 붙여 다시 놓는다 (11/24).
    /// 칩은 Img_ZoomKey · Txt_ZoomPlus · Img_ZoomWheelKey 순서이고, 셋 다 박스 왼쪽 위 기준 앵커다.
    /// </summary>
    private void LayoutZoomKeys()
    {
        if (zoomKeyText == null || zoomWheelKeyText == null) return;
        RectTransform ctrlChip = zoomKeyText.transform.parent as RectTransform;
        RectTransform wheelChip = zoomWheelKeyText.transform.parent as RectTransform;
        if (ctrlChip == null || wheelChip == null) return;
        Transform plus = ctrlChip.parent != null ? ctrlChip.parent.Find("Txt_ZoomPlus") : null;

        float wheelWidth = ChipWidth(zoomWheelKeyText);
        float ctrlWidth = ChipWidth(zoomKeyText);
        float wheelX = GameConstants.Editor.ZoomKeyRightX - wheelWidth;
        float plusX = wheelX - GameConstants.Editor.ZoomKeyPlusWidth;
        float ctrlX = plusX - ctrlWidth;

        wheelChip.sizeDelta = new Vector2(wheelWidth, wheelChip.sizeDelta.y);
        wheelChip.anchoredPosition = new Vector2(wheelX, wheelChip.anchoredPosition.y);
        ctrlChip.sizeDelta = new Vector2(ctrlWidth, ctrlChip.sizeDelta.y);
        ctrlChip.anchoredPosition = new Vector2(ctrlX, ctrlChip.anchoredPosition.y);
        if (plus is RectTransform plusRect) plusRect.anchoredPosition = new Vector2(plusX, plusRect.anchoredPosition.y);
    }

    /// <summary>키 칩 폭 = 글자 폭 + 좌우 여백 (최소 ZoomKeyChipMinWidth)</summary>
    private static float ChipWidth(TMP_Text text)
    {
        text.ForceMeshUpdate();
        float width = Mathf.Ceil(text.GetPreferredValues(text.text).x) + GameConstants.Editor.ZoomKeyChipPadding * 2f;
        return Mathf.Max(GameConstants.Editor.ZoomKeyChipMinWidth, width);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    /// <summary>묶음 제목 "굵은 한글 + 작은 회색 보조 글자" (보조 글자 색은 UITheme.textMuted)</summary>
    public static string SectionLabel(string title, string sub)
    {
        return string.Format(GameConstants.Editor.SectionLabelFormat, title, sub, ColorUtility.ToHtmlStringRGB(UITheme.Current.textMuted));
    }

    /// <summary>
    /// 맨 아래 키 안내 줄: Space 재생 · 휠 이동 · 클릭 놓기 · 우클릭 지우기 · 1 2 도구 · Ctrl+Z 실행 취소 · Ctrl+Y 다시 실행 · Ctrl+S 저장 · Esc 나가기.
    /// 키 글자는 바인딩에서 읽고, 마우스·Esc는 고정 글자.
    /// </summary>
    private void InitKeyGuide()
    {
        if (keyGuideBar == null) return;
        keyGuideBar.Init(new[]
        {
            new KeyGuideEntry(GameConstants.Editor.GuidePlay, KeyGuideKey.FromAction(playPauseAction, "Space")).WithId(GameConstants.Editor.GuidePlayId),
            new KeyGuideEntry(GameConstants.Editor.GuideMove, KeyGuideKey.Fixed(GameConstants.Editor.KeyWheel)),
            new KeyGuideEntry(GameConstants.Editor.GuidePlace, KeyGuideKey.Fixed(GameConstants.Editor.KeyClick)),
            new KeyGuideEntry(GameConstants.Editor.GuideRemove, KeyGuideKey.Fixed(GameConstants.Editor.KeyRightClick)),
            new KeyGuideEntry(GameConstants.Editor.GuideTools,
                KeyGuideKey.FromAction(toolNoteAction, "1"), KeyGuideKey.FromAction(toolBpmSectionAction, "2")),
            new KeyGuideEntry(GameConstants.Editor.GuideUndo, KeyGuideKey.FromAction(undoAction, "Ctrl+Z")),
            new KeyGuideEntry(GameConstants.Editor.GuideRedo, KeyGuideKey.FromAction(redoAction, "Ctrl+Y")),
            new KeyGuideEntry(GameConstants.Editor.GuideSave, KeyGuideKey.FromAction(saveAction, "Ctrl+S")),
            new KeyGuideEntry(GameConstants.Editor.GuideExit, KeyGuideKey.Fixed(GameConstants.Editor.KeyEsc))
        });
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>
    /// 재생 버튼 아이콘을 바꾼다 (정지 중 ▶, 재생 중 ❚❚). 키 안내 줄의 Space 항목도 "재생" ↔ "멈춤".
    /// </summary>
    public void SetPlaying(bool playing)
    {
        playIconText.text = playing ? pauseIcon : GameConstants.Editor.PlayIcon;
        if (keyGuideBar != null) keyGuideBar.SetLabel(GameConstants.Editor.GuidePlayId, playing ? GameConstants.Editor.GuidePause : GameConstants.Editor.GuidePlay);
    }

    /// <summary>
    /// BPM 구간 목록·현재 BPM·되돌리기 버튼·타임라인 눈금과 시간 표시를 세션 값으로 다시 맞춘다 (구간·offset이 바뀌었을 때).
    /// </summary>
    public void RefreshTiming()
    {
        bpmSectionList.SetSections(session.BpmSections);
        RefreshResetBpmButton();
        SetText(bpmSectionsTitleText, SectionLabel(GameConstants.Editor.BpmSectionsTitle,
            string.Format(GameConstants.Editor.BpmSectionsCountFormat, session.BpmSections.Count)));
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
            ? string.Format(GameConstants.Editor.NextBpmFormat, SongData.FormatBpm(sections[index + 1].Bpm))
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
        if (toolNoteAction != null && toolBpmSectionAction != null)
        {
            toolNoteAction.action.performed += OnToolNoteShortcut;
            toolBpmSectionAction.action.performed += OnToolBpmSectionShortcut;
            toolNoteAction.action.Enable();
            toolBpmSectionAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[Canvas_Editor] 도구 단축키 입력 액션이 연결되지 않아 1·2를 받을 수 없습니다.", this);
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
        if (toolNoteAction != null) toolNoteAction.action.performed -= OnToolNoteShortcut;
        if (toolBpmSectionAction != null) toolBpmSectionAction.action.performed -= OnToolBpmSectionShortcut;
        isInputEnabled = false;
    }

    /// <summary>
    /// 저장 결과를 표시한다. 성공: "● 저장 안 됨"이 사라진다 (숨김), 실패: 빨간 "저장 실패"를 띄운다 (이유는 EditorScene이 콘솔에 남긴다).
    /// </summary>
    public void ShowSaveResult(bool saved)
    {
        if (saved) RefreshSaveStatus();
        else ShowStatus(GameConstants.Editor.SaveStatusFailed, UITheme.Current.danger);
    }

    /// <summary>
    /// 저장 상태 자리에 안내를 잠깐 표시한다 (보조 글자색). 되돌릴 때는 RefreshSaveStatus를 부른다 (EditorScene이 시간을 잰다).
    /// </summary>
    public void ShowStatusMessage(string message)
    {
        ShowStatus(message, UITheme.Current.textDescription);
    }

    private void ShowStatus(string message, Color color)
    {
        saveStatusText.text = message;
        saveStatusText.color = color;
        saveStatusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    /// <summary>"● 저장 안 됨"이 보이면 true (확인용)</summary>
    public bool IsUnsavedShown => saveStatusText != null && saveStatusText.gameObject.activeSelf && saveStatusText.text == GameConstants.Editor.SaveStatusUnsaved;

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
        // 마지막으로 쓴 테스트 플레이 배속을 저장한다 (인게임 배속과 따로, 기획서 2-4 11/19)
        if (PlayerDataManager.HasInstance) GameManager.PlayerData.SetLastEditorTestPlayMultiplier(TestPlayScrollMultiplier);
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
        Toggle target = tool == EditorTool.BpmSection ? bpmSectionToggle : shortNoteToggle;
        if (target != null) target.isOn = true;
        RefreshToolStyles();
    }

    private IEnumerable<Toggle> ToolToggles()
    {
        yield return shortNoteToggle;
        yield return bpmSectionToggle;
    }

    private void OnToolToggled(bool on)
    {
        RefreshToolStyles();
    }

    /// <summary>
    /// 도구 버튼 모양을 선택 상태에 맞춘다 (UITheme 에디터 묶음): 바탕 = Toggle의 targetGraphic, 테두리 = 같은 오브젝트의 Outline,
    /// 이름 글자, 선택 링(고른 도구만). 아래 사용법 한 줄도 고른 도구로 바꾼다.
    /// </summary>
    private void RefreshToolStyles()
    {
        UITheme theme = UITheme.Current;
        PaintTool(shortNoteToggle, shortNoteLabel, shortNoteRing, theme);
        PaintTool(bpmSectionToggle, bpmSectionLabel, bpmSectionRing, theme);
        // 막대 아이콘: 고른 도구면 하늘, 아니면 흐린 회색 (BPM 구간 ♩는 늘 호박 사각)
        if (shortNoteIcon != null) shortNoteIcon.color = shortNoteToggle != null && shortNoteToggle.isOn ? theme.accent : theme.textMuted;

        if (toolHintText != null)
        {
            toolHintText.text = CurrentTool == EditorTool.BpmSection ? GameConstants.Editor.ToolBpmSectionHint : GameConstants.Editor.ToolNoteHint;
        }
    }

    private static void PaintTool(Toggle toggle, TMP_Text label, GameObject ring, UITheme theme)
    {
        if (toggle == null) return;
        bool on = toggle.isOn;
        if (toggle.targetGraphic != null) toggle.targetGraphic.color = on ? theme.toolSelectedBackground : theme.toolNormalBackground;
        if (toggle.TryGetComponent(out Outline outline))
        {
            outline.effectColor = on ? theme.toolSelectedBorder : theme.toolNormalBorder;
            float width = on ? theme.toolSelectedBorderWidth : theme.toolNormalBorderWidth;
            outline.effectDistance = new Vector2(width, -width);
        }
        if (label != null) label.color = on ? theme.toolSelectedText : theme.toolNormalText;
        if (ring != null && ring.activeSelf != on) ring.SetActive(on);
    }

    /// <summary>고른 도구 사용법 한 줄 (확인용)</summary>
    public string ToolHint => toolHintText != null ? toolHintText.text : string.Empty;

    /// <summary>
    /// 스위치(메트로놈 · 키음) 모양을 토글 상태에 맞춘다: 켜짐 = 하늘 바탕 + 손잡이 오른쪽, 꺼짐 = 흐린 바탕 + 손잡이 왼쪽.
    /// </summary>
    private void RefreshSwitches()
    {
        PaintSwitch(metronomeToggle, metronomeTrack, metronomeKnob);
        PaintSwitch(keySoundToggle, keySoundTrack, keySoundKnob);
        PaintSwitch(showTestResultToggle, showTestResultTrack, showTestResultKnob);
        if (showTestResultHint != null)
            showTestResultHint.text = ShowTestResult ? GameConstants.Editor.ShowTestResultHintOn : GameConstants.Editor.ShowTestResultHintOff;
    }

    private static void PaintSwitch(Toggle toggle, Image track, RectTransform knob)
    {
        if (toggle == null) return;
        UITheme theme = UITheme.Current;
        bool on = toggle.isOn;
        if (track != null) track.color = on ? theme.accent : theme.editorSwitchOff;
        if (knob != null)
        {
            knob.anchorMin = knob.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
            knob.pivot = new Vector2(on ? 1f : 0f, 0.5f);
            knob.anchoredPosition = new Vector2(on ? -GameConstants.Editor.SwitchKnobInset : GameConstants.Editor.SwitchKnobInset, 0f);
        }
    }

    /// <summary>
    /// 곡 BPM 구간으로 되돌리기 버튼: 편집 중인 구간이 곡 BPM 구간과 다르면 빨간 글자 + 빨간 50% 테두리로 켜고, 같으면 회색으로 끈다 (11/19 와이어프레임).
    /// </summary>
    private void RefreshResetBpmButton()
    {
        UITheme theme = UITheme.Current;
        bool on = !session.IsSongBpmSections;
        resetBpmButton.interactable = on;
        if (resetBpmButton.targetGraphic != null) resetBpmButton.targetGraphic.color = theme.toolNormalBackground;
        if (resetBpmLabel != null) resetBpmLabel.color = on ? theme.danger : theme.textMuted;
        if (resetBpmFrame != null) resetBpmFrame.color = on ? UITheme.WithAlpha(theme.danger, 0.5f) : theme.divider;
    }

    /// <summary>
    /// 잠깐 보여준 안내를 끝내고 저장 상태 표시로 되돌린다: 저장하지 않은 변경이 있으면 "● 저장 안 됨"(호박), 없으면 숨김.
    /// </summary>
    public void RefreshSaveStatus()
    {
        bool dirty = session != null && session.IsDirty;
        ShowStatus(dirty ? GameConstants.Editor.SaveStatusUnsaved : string.Empty, UITheme.Current.editorAmber);
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
        if (showTestResultToggle != null) showTestResultToggle.onValueChanged.RemoveListener(OnShowTestResultToggled);
        if (speedDownButton != null) speedDownButton.onClick.RemoveListener(OnSpeedDownClicked);
        if (speedUpButton != null) speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
        if (data != null) data.OnChanged -= RefreshNoteCountText;
        if (session != null) session.OnDirtyChanged -= OnDirtyChanged;
    }

    /// <summary>
    /// 저장 뒤 다시 편집하면 "● 저장 안 됨"을, 실행 취소로 저장 시점에 돌아오면 숨긴다.
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

    private void OnToolNoteShortcut(InputAction.CallbackContext context) => RequestToolShortcut(EditorTool.Note);
    private void OnToolBpmSectionShortcut(InputAction.CallbackContext context) => RequestToolShortcut(EditorTool.BpmSection);

    /// <summary>도구 단축키 1 · 2. 팝업이 열려 있거나 입력창에 글자를 쓰는 중이면 무시한다</summary>
    private void RequestToolShortcut(EditorTool tool)
    {
        if (IsShortcutBlocked()) return;
        ToolShortcutRequested?.Invoke(tool);
    }

    /// <summary>「테스트 후 결과 보기」를 바꾸면 모양 · 설명을 고치고 바로 저장한다</summary>
    private void OnShowTestResultToggled(bool on)
    {
        RefreshSwitches();
        if (PlayerDataManager.HasInstance) GameManager.PlayerData.SetLastEditorShowTestResult(on);
    }

    private void OnMetronomeToggled(bool on)
    {
        RefreshSwitches();
        MetronomeChanged?.Invoke(on);
    }

    private void OnKeySoundToggled(bool on)
    {
        RefreshSwitches();
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
        return (UIManager.HasInstance && GameManager.UI.IsShortcutBlocked) || IsTypingInInputField();
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
        if (UIManager.HasInstance && GameManager.UI.IsShortcutBlocked) return;

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
    /// "x1.5" 형식으로 그리드 높이 배율을 표시하고(키 안내는 오른쪽 키 칩 [Ctrl]+[휠]), 지금 스냅의 선 간격이 좁으면 안내를 보여준다.
    /// 스냅·그리드 높이·BPM이 바뀔 때 부른다.
    /// </summary>
    private void RefreshGridHeightText()
    {
        string scale = grid.GridHeightScale.ToString("0.0#", CultureInfo.InvariantCulture);
        gridHeightText.text = string.Format(GameConstants.Editor.GridHeightFormat, scale);

        // 선 간격은 현재 위치의 BPM으로 본다
        bool tooDense = BeatGridMath.IsSnapLineSpacingTooSmall(session.Timing.BpmAt(grid.CurrentBeat), grid.GridHeightScale, grid.SnapDivision);
        gridHeightHintText.text = GameConstants.Editor.DenseGridHint;
        gridHeightHintText.gameObject.SetActive(tooDense);
    }

    /// <summary>
    /// 노트 수 카드 두 개: 노트(숏노트 + 롱노트) · 롱노트.
    /// </summary>
    private void RefreshNoteCountText()
    {
        notesValueText.SetText("{0}", data.ShortCount + data.LongCount);
        longNotesValueText.SetText("{0}", data.LongCount);
    }

    /// <summary>노트 수 카드 값 (확인용: 노트, 롱노트)</summary>
    public string NotesValue => notesValueText != null ? notesValueText.text : string.Empty;
    public string LongNotesValue => longNotesValueText != null ? longNotesValueText.text : string.Empty;

    /// <summary>
    /// "0:00.169" (Bold) + " / 2:19 · 마디 1" (Regular)을 표시한다. 현재 위치 시간은 현재 박을 offset·BPM으로 바꾼 값이다.
    /// 뒤쪽 글자는 마디·곡 길이가 바뀔 때만 다시 쓴다 (재생 중 매 프레임 문자열 할당을 늘리지 않게).
    /// </summary>
    private void RefreshTimeInfoText()
    {
        float currentSeconds = (float)session.Timing.BeatToSeconds(grid.CurrentBeat);
        timeInfoText.text = TimeFormat.ToMinSecMillis(currentSeconds);

        int measure = grid.CurrentMeasure;
        float length = session.SongLengthSeconds;
        if (timeTotalText == null || (measure == shownTimeMeasure && Mathf.Approximately(length, shownSongLength))) return;
        if (!Mathf.Approximately(length, shownSongLength)) LayoutTimeTexts(length);
        shownTimeMeasure = measure;
        shownSongLength = length;
        timeTotalText.text = string.Format(GameConstants.Editor.TimeTotalFormat, TimeFormat.ToMinSec(length), measure);
    }

    /// <summary>
    /// 아래 재생 줄: 시간 글자 두 칸을 가장 긴 글자 폭에 맞추고, 남은 폭만큼 슬라이더를 줄인다 (11/24, "마디 83"이 "마디 …"로 잘리던 문제).
    /// 가장 긴 글자 = 곡 길이 기준 시간(숫자는 모두 8로) + 세 자리 마디. 곡 길이가 바뀔 때만 다시 잡아 재생 중에 슬라이더가 흔들리지 않는다.
    /// Txt_TimeInfo · Txt_TimeTotal은 오른쪽 위 앵커(pivot 왼쪽 위), Slider_Timeline은 좌우로 늘어나는 앵커다.
    /// </summary>
    private void LayoutTimeTexts(float songLength)
    {
        if (timeInfoText == null || timeTotalText == null) return;
        string widestInfo = WidestDigits(TimeFormat.ToMinSecMillis(songLength));
        string widestTotal = WidestDigits(string.Format(GameConstants.Editor.TimeTotalFormat, TimeFormat.ToMinSec(songLength), GameConstants.Editor.TimeLayoutMaxMeasure));
        float infoWidth = Mathf.Ceil(timeInfoText.GetPreferredValues(widestInfo).x) + 2f;
        float totalWidth = Mathf.Ceil(timeTotalText.GetPreferredValues(widestTotal).x) + 2f;

        RectTransform total = timeTotalText.rectTransform;
        RectTransform info = timeInfoText.rectTransform;
        float totalX = -GameConstants.Editor.TimeTextRightMargin - totalWidth;
        float infoX = totalX - GameConstants.Editor.TimeTextGap - infoWidth;
        total.sizeDelta = new Vector2(totalWidth, total.sizeDelta.y);
        total.anchoredPosition = new Vector2(totalX, total.anchoredPosition.y);
        info.sizeDelta = new Vector2(infoWidth, info.sizeDelta.y);
        info.anchoredPosition = new Vector2(infoX, info.anchoredPosition.y);

        if (timelineSlider != null && timelineSlider.transform is RectTransform slider)
        {
            slider.anchoredPosition = new Vector2(GameConstants.Editor.TimelineSliderLeft, slider.anchoredPosition.y);
            slider.sizeDelta = new Vector2(infoX - GameConstants.Editor.TimelineSliderGap - GameConstants.Editor.TimelineSliderLeft, slider.sizeDelta.y);
        }
    }

    /// <summary>숫자를 모두 8로 바꾼 글자 (숫자 폭이 글자마다 달라도 가장 넓게 잡는다)</summary>
    private static string WidestDigits(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++) if (char.IsDigit(chars[i])) chars[i] = '8';
        return new string(chars);
    }

    /// <summary>시간 글자 (확인용: 현재 시간 + 뒤쪽)</summary>
    public string TimeLabel => (timeInfoText != null ? timeInfoText.text : string.Empty) + " " + (timeTotalText != null ? timeTotalText.text : string.Empty);
}
