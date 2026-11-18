using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면 UI (기획서 3-3 곡 선택 화면 개편 · 「나만의 채보 탭 (11/17, 11/18 개정)」 · 「에디터 진입을 나만의 채보 탭으로 합침 (11/18)」). SongSelectScene이 Init()을 호출한다.
/// - 배경: MenuBackground (바탕색 → 고른 곡의 1920×1080 일러스트를 흐리게·채도 낮춰 → 라벤더빛 덮기). 곡을 바꾸면 크로스페이드, 들어올 때는 바로.
/// - 색: 메뉴 화면 톤 UITheme 한 곳 (기획서 3장 UI 톤 · 라벤더 안개). 고정 그래픽은 UIThemeColor 역할, 바뀌는 색(탭·목록 선택·난이도·랭크)은 UIThemePaint.
/// - 공통(왼쪽): 앨범 · 곡 정보 · 최고 기록 카드(BEST SCORE · RATE/COMBO · RANK + FC/AP) · 배속. 하단 바: 키 안내 + 시작 버튼(Enter).
/// - 공식 채보 탭(OfficialPanel): 난이도 카드 3개 + 오른쪽 곡 목록(Btn_SongItem). ↑↓ 곡(EventSystem 내비게이션), ←→ 난이도.
/// - 나만의 채보 탭(MyChartPanel): 모든 곡을 묶음으로 보여 준다. 곡 머리 줄(SongGroupHeader, 채보가 없으면 NO CHART) 아래 채보 줄(Btn_MyChartItem),
///   지금 고른 곡 묶음의 끝에만 "+ 새 채보 만들기" 줄(Btn_NewChart, 하나를 옮겨 쓴다). 정지점 = [채보 줄들 + 그 곡의 + 줄]을 곡 순서로 이은 목록 (MyChartListNavigator).
///   ↑↓ 정지점 한 칸씩 (EventSystem 이동 이벤트를 줄이 넘겨 준다), ←→ 이전·다음 곡 묶음의 첫 정지점. 왼쪽과 배경은 고른 정지점의 곡을 따른다.
///   + 줄을 고르면 왼쪽 고른 채보 카드 자리에 점선 카드(새 채보), 최고 기록 카드는 비우고 흐리게(0.35), 시작 버튼은 "새 채보 / NEW".
///   Enter·시작 버튼·+ 줄 클릭 → 새 채보 팝업(Popup_NewChart) → 만들기 → 에디터. 채보 줄에서 E → 그 채보를 에디터로. 에디터 진입은 InGameLauncher.StartEditor (인게임과 같은 진입 함수).
/// - 들어오면 (기획서 2-4 「마지막 선택」 11/18, 탭마다 한 칸): 진입 데이터의 탭으로 시작한다 (없으면 공식). 진입 데이터에 곡이 있으면 그 탭은 그것
///   (나만의 채보 탭은 chartId → 그 채보, 없으면 그 곡의 + 줄), 곡이 없으면(타이틀 플레이 · 에디터) 그 탭의 마지막 선택 칸. 보이지 않는 탭도 자기 칸으로 맞춰 두어
///   Tab으로 바꾸면 그 탭의 마지막 선택이 보인다. 칸이 비었거나 곡·채보가 없으면 공식은 첫 곡, 나만의 채보는 첫 정지점.
///   배속은 진입 데이터에 값이 있을 때만 그 값, 없으면 저장된 마지막 배속 (ResolveStartSpeed).
/// - 조작 (기획서 3-8): Tab 탭 전환, 1 / 2 배속(꾹 누르면 KeyRepeat로 계속), Enter 시작(+ 줄이면 새 채보 팝업), E 편집(나만의 채보 탭 채보 줄), ESC 타이틀(UIManager → SongSelectScene.OnBack).
///   팝업이 열려 있으면 키보드 단축키는 동작하지 않는다 (GameManager.UI.HasOpenPopup). 마우스로 곡·난이도·채보 클릭, 시작 버튼, 휠 스크롤.
/// - 키보드 선택(EventSystem)은 항상 지금 탭의 고른 항목(곡 / 채보)에 둔다. 마우스로 다른 것을 누르면 다음 프레임에 되돌린다.
/// </summary>
public class Canvas_SongSelect : UIBase
{
    private const string LogPrefix = "[Canvas_SongSelect]";

    [Header("배경 (Prefabs/Common/MenuBackground: 바탕색 + 흐린 곡 일러스트 + 라벤더빛 덮기)")]
    [SerializeField] private MenuBackground menuBackground;

    [Header("화면 이름")]
    [SerializeField] private TMP_Text screenNameText;

    [Header("탭")]
    [SerializeField] private Button officialTabButton;
    [SerializeField] private TMP_Text officialTabText;
    [SerializeField] private TMP_Text officialTabEnText;
    [SerializeField] private Button myChartTabButton;
    [SerializeField] private TMP_Text myChartTabText;
    [SerializeField] private TMP_Text myChartTabEnText;
    [Tooltip("탭 옆 Tab 키 글자 (바인딩에서 읽는다)")]
    [SerializeField] private TMP_Text tabKeyText;
    [SerializeField] private GameObject officialPanel;
    [SerializeField] private GameObject myChartPanel;

    [Header("곡 정보")]
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text songTitleText;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private TMP_Text songInfoText;

    [Header("공식 채보 탭")]
    [Tooltip("EASY · NORMAL · HARD 순서 (GameConstants.Chart.Difficulties와 같은 순서)")]
    [SerializeField] private Btn_Difficulty[] difficultyButtons;
    [SerializeField] private TMP_Text officialEmptyText;
    [SerializeField] private Btn_SongItem songItemPrefab;
    [Tooltip("ScrollView_SongList")]
    [SerializeField] private ScrollRect songListScrollRect;
    [Tooltip("ScrollView_SongList/Viewport/Content")]
    [SerializeField] private RectTransform songListContent;
    [Tooltip("곡 목록 위쪽 숫자 (01 / 08)")]
    [SerializeField] private TMP_Text songCountText;

    [Header("나만의 채보 탭")]
    [Tooltip("고른 채보 카드 머리글 (Txt_MyChartHeader: MY CHART · 고른 채보 / + 줄이면 NEW CHART · 새 채보)")]
    [SerializeField] private TMP_Text myChartHeaderText;
    [Tooltip("고른 채보 정보 카드 (CUSTOM · 채보 이름 · Lv · 노트 수 · 롱노트 수 · 마지막 수정일)")]
    [SerializeField] private GameObject myChartCard;
    [Tooltip("+ 줄을 골랐을 때 고른 채보 카드 자리에 보이는 점선 카드 (Img_NewChartCard)")]
    [SerializeField] private GameObject newChartCard;
    [SerializeField] private TMP_Text newChartCardTitleText;
    [SerializeField] private TMP_Text newChartCardHintText;
    [Tooltip("\"+ 새 채보 만들기\" 줄 프리팹 (Prefabs/SongSelect/Btn_NewChart). 하나를 만들어 지금 고른 곡 묶음 끝으로 옮겨 쓴다")]
    [SerializeField] private Btn_NewChart newChartRowPrefab;
    [Tooltip("새 채보 팝업 (Canvas_SongSelect/Popup_NewChart)")]
    [SerializeField] private Popup_NewChart newChartPopup;
    [SerializeField] private TMP_Text myChartNameText;
    [SerializeField] private TMP_Text myChartLevelText;
    [SerializeField] private TMP_Text myChartStatsText;
    [SerializeField] private Btn_MyChartItem myChartItemPrefab;
    [SerializeField] private SongGroupHeader songGroupHeaderPrefab;
    [Tooltip("ScrollView_MyChart")]
    [SerializeField] private ScrollRect myChartScrollRect;
    [Tooltip("ScrollView_MyChart/Viewport/Content")]
    [SerializeField] private RectTransform myChartContent;
    [Tooltip("목록 위쪽 숫자 (곡 02 / 08 · 채보 5)")]
    [SerializeField] private TMP_Text myChartCountText;

    [Header("최고 기록 카드 (기록이 없으면 점수·RATE·COMBO \"—\", 랭크는 점선 빈 자리)")]
    [Tooltip("+ 줄을 고르면 흐리게 (Img_RecordCard의 CanvasGroup, 알파 0.35)")]
    [SerializeField] private CanvasGroup recordCardGroup;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text rateText;
    [SerializeField] private TMP_Text comboText;
    [Tooltip("랭크 칸 (94×62, 세로 구분선 오른쪽). 기록이 없으면 Img_RankEmpty")]
    [SerializeField] private Img_Rank recordRank;
    [Tooltip("COMBO 글자 옆 FC 이미지 (폭 30)")]
    [SerializeField] private Img_FullCombo recordFullCombo;

    [Header("배속")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text speedDownKeyText;
    [SerializeField] private TMP_Text speedUpKeyText;

    [Header("하단 바")]
    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;
    [SerializeField] private Button startButton;
    [Tooltip("시작 버튼 안 Enter 키 글자 (바인딩에서 읽는다)")]
    [SerializeField] private TMP_Text startKeyText;
    [Tooltip("시작 버튼 이름 (시작 / + 줄이면 새 채보)과 영문 (START / NEW)")]
    [SerializeField] private TMP_Text startLabelText;
    [SerializeField] private TMP_Text startLabelEnText;
    [SerializeField] private UISelectionKeeper selectionKeeper;

    [Header("입력 (입력 에셋의 SongSelect 맵. ESC는 UIManager)")]
    [SerializeField] private InputActionReference prevChartAction;
    [SerializeField] private InputActionReference nextChartAction;
    [SerializeField] private InputActionReference switchTabAction;
    [SerializeField] private InputActionReference speedUpAction;
    [SerializeField] private InputActionReference speedDownAction;
    [SerializeField] private InputActionReference startAction;
    [Tooltip("SongSelect/EditChart (E): 나만의 채보 탭 채보 줄을 에디터로")]
    [SerializeField] private InputActionReference editChartAction;

    private readonly List<Btn_SongItem> songItems = new List<Btn_SongItem>();
    private readonly Dictionary<string, SongData> songsById = new Dictionary<string, SongData>();

    /// <summary>나만의 채보 목록: 채보 줄 (목록 전체 순서) · 곡 묶음마다 머리 줄 · 곡 · 채보 줄 · 채보 ID</summary>
    private readonly List<Btn_MyChartItem> myChartRows = new List<Btn_MyChartItem>();
    private readonly List<SongGroupHeader> songGroupHeaders = new List<SongGroupHeader>();
    private readonly List<SongData> groupSongs = new List<SongData>();
    private readonly List<List<Btn_MyChartItem>> groupRows = new List<List<Btn_MyChartItem>>();
    private readonly List<IReadOnlyList<string>> groupChartIds = new List<IReadOnlyList<string>>();
    private readonly List<string> groupSongIds = new List<string>();
    private MyChartListNavigator myChartNavigator = new MyChartListNavigator(null);
    private Btn_NewChart newChartRow;

    private readonly Vector3[] worldCorners = new Vector3[4];

    private Btn_SongItem chosenItem;
    private ChartType currentTab = ChartType.Official;
    private Btn_Difficulty chosenDifficulty;

    /// <summary>나만의 채보 탭에서 고른 정지점 (MyChartListNavigator 순번, 없으면 -1)</summary>
    private int chosenStop = -1;

    /// <summary>배속 × GameConstants.Play.ScrollMultiplierScale (0.1 단위 오차 없이 계산)</summary>
    private int speedSteps;

    private bool isInputEnabled;
    private bool isLeaving;

    // 배속 키 반복 (인게임과 같은 값: 누른 순간 한 번, 0.4초 뒤부터 0.08초마다)
    private readonly KeyRepeat speedDownRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);
    private readonly KeyRepeat speedUpRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);

    /// <summary>지금 보이는 탭</summary>
    public ChartType CurrentTab => currentTab;

    /// <summary>지금 탭의 곡 (공식: 고른 곡, 나만의 채보: 고른 정지점이 속한 곡). 없으면 null</summary>
    public SongData CurrentSong => currentTab == ChartType.Official
        ? (chosenItem != null ? chosenItem.Song : null)
        : ChosenGroupSong;

    /// <summary>지금 탭의 곡 ID (없으면 null)</summary>
    public string ChosenSongId => CurrentSong != null ? CurrentSong.SongId : null;

    /// <summary>지금 탭에서 고른 채보 (없으면 null)</summary>
    public ChartData ChosenChart => currentTab == ChartType.Official
        ? (chosenDifficulty != null ? chosenDifficulty.Chart : null)
        : ChosenMyChart;

    /// <summary>지금 배속</summary>
    public float ScrollMultiplier => ScrollSpeedRules.FromSteps(speedSteps);

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>배경 블러 (확인용)</summary>
    public UIBlurBackground BlurBackground => menuBackground != null ? menuBackground.Blur : null;

    /// <summary>나만의 채보 목록 채보 줄 (목록 전체 순서, 확인용)</summary>
    public IReadOnlyList<Btn_MyChartItem> MyChartRows => myChartRows;

    /// <summary>나만의 채보 목록 곡 머리 줄 (곡 순서, 확인용)</summary>
    public IReadOnlyList<SongGroupHeader> SongGroupHeaders => songGroupHeaders;

    /// <summary>"+ 새 채보 만들기" 줄 (확인용)</summary>
    public Btn_NewChart NewChartRow => newChartRow;

    /// <summary>새 채보 팝업 (확인용)</summary>
    public Popup_NewChart NewChartPopup => newChartPopup;

    /// <summary>나만의 채보 탭 정지점 이동 규칙 (확인용)</summary>
    public MyChartListNavigator MyChartNavigator => myChartNavigator;

    /// <summary>나만의 채보 탭에서 고른 정지점 (확인용)</summary>
    public MyChartStop ChosenStop => myChartNavigator.Get(chosenStop);

    /// <summary>나만의 채보 탭에서 + 줄을 골랐으면 true</summary>
    public bool IsNewChartRowChosen => currentTab == ChartType.Custom && myChartNavigator.Get(chosenStop).IsAddRow;

    /// <summary>위쪽 숫자 글자 (나만의 채보 탭, 확인용)</summary>
    public TMP_Text MyChartCountText => myChartCountText;

    /// <summary>최고 기록 카드 불투명도 (확인용)</summary>
    public float RecordCardAlpha => recordCardGroup != null ? recordCardGroup.alpha : 1f;

    /// <summary>시작 버튼 이름 (확인용)</summary>
    public string StartLabel => startLabelText != null ? startLabelText.text : null;

    /// <summary>점선 새 채보 카드가 보이면 true (확인용)</summary>
    public bool IsNewChartCardShown => newChartCard != null && newChartCard.activeSelf;

    /// <summary>곡 목록 항목 (확인용)</summary>
    public IReadOnlyList<Btn_SongItem> SongItems => songItems;

    /// <summary>나만의 채보 탭에서 고른 채보 (+ 줄이거나 없으면 null)</summary>
    private ChartData ChosenMyChart
    {
        get
        {
            MyChartStop stop = myChartNavigator.Get(chosenStop);
            return stop.Kind == MyChartStopKind.Chart && stop.ChartIndex >= 0 && stop.ChartIndex < myChartRows.Count ? myChartRows[stop.ChartIndex].Chart : null;
        }
    }

    /// <summary>나만의 채보 탭에서 고른 정지점의 곡 (없으면 null)</summary>
    private SongData ChosenGroupSong
    {
        get
        {
            int group = myChartNavigator.Get(chosenStop).Group;
            return group >= 0 && group < groupSongs.Count ? groupSongs[group] : null;
        }
    }

    /// <summary>
    /// 곡 목록·채보 목록을 만들고 버튼 리스너를 연결한 뒤, 처음 상태(탭·곡·채보·배속)로 시작한다.
    /// </summary>
    /// <param name="entry">곡 선택으로 돌아온 경우의 선택 상태. null이면 PlayerData의 마지막 선택(없으면 공식 첫 곡)·저장된 마지막 배속</param>
    public void Init(IReadOnlyList<SongData> songs, SongSelectEntryData entry)
    {
        isLeaving = false;

        // 메뉴 화면 톤 (UITheme): 고정 그래픽(UIThemeColor)과 배경을 먼저 칠한다. 선택에 따라 바뀌는 색은 각 표시 함수가 칠한다
        UIThemeColor.ApplyAll(gameObject);
        if (menuBackground != null) menuBackground.Init();
        InitStaticTexts();
        InitDifficultyButtons();
        CreateSongItems(songs);
        ConnectSongNavigation();
        CreateMyChartList();
        AddButtonListeners();
        InitKeyGuide();

        // 배속: 인게임·결과에서 돌아오면 넘겨받은 배속, 값이 없으면(타이틀·에디터에서 들어옴) PlayerData.json에 저장된 마지막 배속 (없으면 1.0)
        float savedSpeed = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : GameConstants.Play.DefaultScrollMultiplier;
        speedSteps = ScrollSpeedRules.ToSteps(ResolveStartSpeed(entry, savedSpeed));
        SetSpeedText(ScrollMultiplier);

        // 탭마다 처음 고를 줄 (기획서 2-4 「마지막 선택」 11/18): 진입 데이터에 곡이 있으면 그 탭은 그것, 아니면 그 탭의 마지막 선택 칸.
        // 보이지 않는 탭도 자기 칸으로 맞춰 두어, Tab으로 바꾸면 그 탭의 마지막 선택이 보인다 (같은 화면 안에서 옮긴 위치는 그대로 남는다)
        currentTab = entry != null ? entry.chartType : ChartType.Official;
        bool entryHasSong = entry != null && !string.IsNullOrEmpty(entry.songId);
        LastSelectionSaveData last = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastSelection : null;
        List<string> songIds = new List<string>();
        foreach (Btn_SongItem item in songItems) songIds.Add(item.Song.SongId);

        // 공식 탭: 곡 · 난이도 (없으면 첫 곡의 가장 낮은 난이도)
        StartSelection official = entryHasSong && entry.chartType == ChartType.Official
            ? new StartSelection(entry.songId, entry.chartId, ChartType.Official, false)
            : PlayerDataRules.ResolveOfficialStart(last, songIds, ChartExists);
        Btn_SongItem firstItem = songItems.Find(item => item.Song.SongId == official.SongId) ?? (songItems.Count > 0 ? songItems[0] : null);
        if (firstItem != null) ChooseSongItem(firstItem, official.ChartId);

        // 나만의 채보 탭: 채보 줄 · 그 곡의 + 줄(채보 ID 없음) · 첫 정지점(칸이 비었거나 없는 곡·채보)
        TabSelectionSaveData custom = entryHasSong && entry.chartType == ChartType.Custom
            ? new TabSelectionSaveData { songId = entry.songId, chartId = entry.chartId ?? string.Empty }
            : PlayerDataRules.ResolveTab(last != null ? last.Get(ChartType.Custom) : null, ChartType.Custom, songIds, ChartExists);
        SetChosenStop(myChartNavigator.ResolveEntryStop(groupSongIds, groupChartIds, custom.songId, custom.chartId), false);

        ShowTab(currentTab, true);
        LogState(entryHasSong ? "시작 (진입 데이터)" : "시작 (탭 마지막 선택)");
    }

    /// <summary>
    /// 시작 배속 (순수 함수): 진입 데이터에 배속 값이 있으면(0보다 큼) 그 값, 없으면(진입 데이터 없음 · 에디터·타이틀에서 들어옴) 저장된 마지막 배속.
    /// </summary>
    public static float ResolveStartSpeed(SongSelectEntryData entry, float savedSpeed)
    {
        return entry != null && entry.scrollMultiplier > SongSelectEntryData.NoScrollMultiplier ? entry.scrollMultiplier : savedSpeed;
    }

    /// <summary>시작 선택 확인용: 그 곡의 채보가 지금 있는지 (공식: Resources, 나만의 채보: 목록에 읽힌 파일)</summary>
    private bool ChartExists(string songId, string chartId, ChartType type)
    {
        if (type == ChartType.Custom) return myChartRows.Exists(row => row.Chart.songId == songId && row.Chart.chartId == chartId);
        return ChartRepository.GetCharts(songId, ChartType.Official).Exists(chart => chart.chartId == chartId);
    }

    /// <summary>화면 고정 글자 (상수에서): 화면 이름 간격, 탭 이름, 빈 상태 문구, 키 글자</summary>
    private void InitStaticTexts()
    {
        if (screenNameText != null)
        {
            screenNameText.text = GameConstants.SongSelect.ScreenName;
            screenNameText.characterSpacing = GameConstants.SongSelect.ScreenNameCharacterSpacing;
        }
        officialTabText.text = GameConstants.SongSelect.TabOfficial;
        myChartTabText.text = GameConstants.SongSelect.TabMyChart;
        if (officialTabEnText != null) officialTabEnText.text = GameConstants.SongSelect.TabOfficialEn;
        if (myChartTabEnText != null) myChartTabEnText.text = GameConstants.SongSelect.TabMyChartEn;
        officialEmptyText.text = GameConstants.SongSelect.OfficialEmpty;
        if (newChartCardTitleText != null) newChartCardTitleText.text = GameConstants.SongSelect.NewChartCardTitle;
        if (newChartCardHintText != null) newChartCardHintText.text = GameConstants.SongSelect.NewChartCardHint;

        if (tabKeyText != null) tabKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(switchTabAction));
        if (speedDownKeyText != null) speedDownKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(speedDownAction));
        if (speedUpKeyText != null) speedUpKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(speedUpAction));
        if (startKeyText != null) startKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(startAction, GameConstants.KeyGuide.EnterKey));
    }

    /// <summary>
    /// 키 안내 줄 (기획서 3-8): 공식 탭 = ↑↓ 곡 · ←→ 난이도 · Tab 공식 ↔ 나만의 채보 · 1 2 배속 · Enter 시작 · ESC 타이틀.
    /// 나만의 채보 탭 = ↑↓ 채보 · 새 채보 줄 · ←→ 이전 · 다음 곡 묶음 · Tab · 1 2 배속 · E 편집(채보 줄일 때만) · ESC 타이틀 (시작은 시작 버튼에 Enter가 보인다).
    /// ↑↓ ←→는 한 액션으로 보이기 어려워 고정 글자, ESC는 UIManager(UI/Cancel)가 받아 고정 글자. 탭·고른 줄이 바뀌면 이름과 보이는 항목을 바꾼다 (RefreshKeyGuide).
    /// </summary>
    private void InitKeyGuide()
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning($"{LogPrefix} keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Song, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)).WithId(GameConstants.KeyGuide.VerticalMoveId),
            new KeyGuideEntry(GameConstants.KeyGuide.Difficulty, KeyGuideKey.Fixed(GameConstants.KeyGuide.LeftRightKeys)).WithId(GameConstants.KeyGuide.ChartMoveId),
            new KeyGuideEntry(GameConstants.KeyGuide.TabSwitch, KeyGuideKey.FromAction(switchTabAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Speed, KeyGuideKey.FromAction(speedDownAction), KeyGuideKey.FromAction(speedUpAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Start, KeyGuideKey.FromAction(startAction, GameConstants.KeyGuide.EnterKey)).WithId(GameConstants.KeyGuide.StartId),
            new KeyGuideEntry(GameConstants.KeyGuide.Edit, KeyGuideKey.FromAction(editChartAction, GameConstants.KeyGuide.EditKeyFallback)).WithId(GameConstants.KeyGuide.EditId),
            new KeyGuideEntry(GameConstants.KeyGuide.ToTitle, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    /// <summary>
    /// 키 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        Subscribe(prevChartAction, OnPrevChart);
        Subscribe(nextChartAction, OnNextChart);
        Subscribe(switchTabAction, OnSwitchTab);
        SubscribeHold(speedUpAction, OnSpeedUpStarted, OnSpeedUpCanceled);
        SubscribeHold(speedDownAction, OnSpeedDownStarted, OnSpeedDownCanceled);
        Subscribe(startAction, OnStartKey);
        Subscribe(editChartAction, OnEditKey);
        isInputEnabled = true;
    }

    /// <summary>
    /// 키 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        ReleaseSpeedKeys();
        if (!isInputEnabled) return;

        Unsubscribe(prevChartAction, OnPrevChart);
        Unsubscribe(nextChartAction, OnNextChart);
        Unsubscribe(switchTabAction, OnSwitchTab);
        UnsubscribeHold(speedUpAction, OnSpeedUpStarted, OnSpeedUpCanceled);
        UnsubscribeHold(speedDownAction, OnSpeedDownStarted, OnSpeedDownCanceled);
        Unsubscribe(startAction, OnStartKey);
        Unsubscribe(editChartAction, OnEditKey);
        isInputEnabled = false;
    }

    private void Subscribe(InputActionReference reference, Action<InputAction.CallbackContext> handler)
    {
        if (reference == null)
        {
            Debug.LogWarning($"{LogPrefix} 연결되지 않은 입력 액션이 있습니다.", this);
            return;
        }

        reference.action.performed += handler;
        reference.action.Enable();
    }

    private static void Unsubscribe(InputActionReference reference, Action<InputAction.CallbackContext> handler)
    {
        if (reference != null) reference.action.performed -= handler;
    }

    /// <summary>꾹 누르는 키(배속): 누른 순간(started)과 뗀 순간(canceled)을 구독한다</summary>
    private void SubscribeHold(InputActionReference reference, Action<InputAction.CallbackContext> onStarted, Action<InputAction.CallbackContext> onCanceled)
    {
        if (reference == null)
        {
            Debug.LogWarning($"{LogPrefix} 연결되지 않은 입력 액션이 있습니다.", this);
            return;
        }

        reference.action.started += onStarted;
        reference.action.canceled += onCanceled;
        reference.action.Enable();
    }

    private static void UnsubscribeHold(InputActionReference reference, Action<InputAction.CallbackContext> onStarted, Action<InputAction.CallbackContext> onCanceled)
    {
        if (reference == null) return;
        reference.action.started -= onStarted;
        reference.action.canceled -= onCanceled;
    }

    /// <summary>
    /// 키보드 선택을 지금 탭의 고른 항목(곡 / 채보)에 둔다. 마우스로 탭·난이도·시작을 눌러 선택이 옮겨 가면 되돌린다
    /// (↑↓가 계속 목록을 움직이게).
    /// </summary>
    private void Update()
    {
        UpdateSpeedRepeat();

        // 팝업이 열려 있으면 팝업 버튼의 선택을 빼앗지 않는다
        if (IsShortcutBlocked) return;

        EventSystem eventSystem = EventSystem.current;
        GameObject target = CurrentFocusTarget;
        if (eventSystem == null || target == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == target) return;
        if (selected != null && currentTab == ChartType.Official && selected.GetComponent<Btn_SongItem>() != null) return;
        if (selected != null && currentTab == ChartType.Custom && (selected.GetComponent<Btn_MyChartItem>() != null || selected.GetComponent<Btn_NewChart>() != null)) return;

        eventSystem.SetSelectedGameObject(target);
    }

    /// <summary>지금 탭에서 키보드 선택을 둘 오브젝트 (공식: 고른 곡 항목, 나만의 채보: 고른 채보 줄 또는 + 줄)</summary>
    private GameObject CurrentFocusTarget
    {
        get
        {
            if (currentTab == ChartType.Official) return chosenItem != null ? chosenItem.gameObject : null;
            return StopTarget(chosenStop);
        }
    }

    /// <summary>정지점의 오브젝트 (채보 줄 / + 줄). 없으면 null</summary>
    private GameObject StopTarget(int stop)
    {
        MyChartStop value = myChartNavigator.Get(stop);
        if (value.Group < 0) return null;
        if (value.IsAddRow) return newChartRow != null ? newChartRow.gameObject : null;
        return value.ChartIndex >= 0 && value.ChartIndex < myChartRows.Count ? myChartRows[value.ChartIndex].gameObject : null;
    }

    private void OnDestroy()
    {
        DisableInput();
        RemoveButtonListeners();
    }

    private void AddButtonListeners()
    {
        RemoveButtonListeners();
        officialTabButton.onClick.AddListener(OnClickOfficialTab);
        myChartTabButton.onClick.AddListener(OnClickMyChartTab);
        startButton.onClick.AddListener(StartGame);
        foreach (Btn_Difficulty button in difficultyButtons)
        {
            Btn_Difficulty target = button;
            target.Button.onClick.AddListener(() => ChooseDifficulty(target));
        }
    }

    private void RemoveButtonListeners()
    {
        if (officialTabButton != null) officialTabButton.onClick.RemoveListener(OnClickOfficialTab);
        if (myChartTabButton != null) myChartTabButton.onClick.RemoveListener(OnClickMyChartTab);
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (difficultyButtons == null) return;
        foreach (Btn_Difficulty button in difficultyButtons)
        {
            if (button != null && button.Button != null) button.Button.onClick.RemoveAllListeners();
        }
    }

    // ───── 탭 ─────

    private void OnClickOfficialTab()
    {
        ChangeTab(ChartType.Official);
    }

    private void OnClickMyChartTab()
    {
        ChangeTab(ChartType.Custom);
    }

    private void OnSwitchTab(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        ChangeTab(currentTab == ChartType.Official ? ChartType.Custom : ChartType.Official);
    }

    private void ChangeTab(ChartType tab)
    {
        if (isLeaving || tab == currentTab) return;

        ShowTab(tab, false);
        LogState("탭 전환");
    }

    /// <summary>
    /// 탭 버튼 모양, 패널, 조작 안내, 왼쪽 곡 정보·배경, 키보드 선택을 지금 탭에 맞춘다.
    /// </summary>
    /// <param name="instantBackground">true면 배경을 페이드 없이 바로 바꾼다 (화면에 들어올 때)</param>
    private void ShowTab(ChartType tab, bool instantBackground)
    {
        currentTab = tab;
        bool isOfficial = tab == ChartType.Official;

        officialPanel.SetActive(isOfficial);
        myChartPanel.SetActive(!isOfficial);

        SetTabVisual(officialTabButton, officialTabText, officialTabEnText, isOfficial);
        SetTabVisual(myChartTabButton, myChartTabText, myChartTabEnText, !isOfficial);

        ShowCurrentSong(instantBackground);
        RefreshChosenChart();
        RefreshCounts();

        Selectable focus = CurrentFocusTarget != null ? CurrentFocusTarget.GetComponent<Selectable>() : null;
        if (focus != null) selectionKeeper.SetDefaultSelectable(focus);
        FocusCurrent();

        if (isOfficial && chosenItem != null) ScrollSongListToShow(chosenItem);
        if (!isOfficial && chosenStop >= 0) ScrollMyChartToShow(chosenStop);
    }

    /// <summary>
    /// 키 안내 줄 이름·항목을 지금 탭과 고른 줄에 맞춘다: 공식 = ↑↓ 곡 · ←→ 난이도 · Enter 시작, 나만의 채보 = ↑↓ 채보 · 새 채보 줄 · ←→ 이전 · 다음 곡 묶음 · E 편집(채보 줄일 때만).
    /// </summary>
    private void RefreshKeyGuide()
    {
        if (keyGuideBar == null) return;

        bool isOfficial = currentTab == ChartType.Official;
        keyGuideBar.SetLabel(GameConstants.KeyGuide.VerticalMoveId, isOfficial ? GameConstants.KeyGuide.Song : GameConstants.KeyGuide.MyChartVertical);
        keyGuideBar.SetLabel(GameConstants.KeyGuide.ChartMoveId, isOfficial ? GameConstants.KeyGuide.Difficulty : GameConstants.KeyGuide.PrevNextSongGroup);
        keyGuideBar.SetVisible(GameConstants.KeyGuide.StartId, isOfficial);
        keyGuideBar.SetVisible(GameConstants.KeyGuide.EditId, !isOfficial && ChosenMyChart != null);
    }

    /// <summary>탭 모양 (UITheme): 고른 탭 = 포인트색 바탕 + 흰 글자, 고르지 않은 탭 = 패널색 + 보조 글자</summary>
    private static void SetTabVisual(Button tabButton, TMP_Text label, TMP_Text labelEn, bool chosen)
    {
        UITheme theme = UITheme.Current;
        if (tabButton.targetGraphic != null) tabButton.targetGraphic.color = chosen ? theme.accent : theme.panel;

        Color text = chosen ? theme.onAccentText : theme.textSecondary;
        label.color = text;
        label.fontStyle = chosen ? FontStyles.Bold : FontStyles.Normal;
        if (labelEn != null) labelEn.color = chosen ? theme.onAccentText : theme.textDescription;
    }

    /// <summary>탭 버튼 바탕색 (확인용)</summary>
    public Color GetTabColor(ChartType tab)
    {
        Button button = tab == ChartType.Official ? officialTabButton : myChartTabButton;
        return button != null && button.targetGraphic != null ? button.targetGraphic.color : Color.clear;
    }

    /// <summary>시작 버튼 바탕색 (확인용)</summary>
    public Color StartButtonColor => startButton != null && startButton.targetGraphic != null ? startButton.targetGraphic.color : Color.clear;

    // ───── 왼쪽 곡 정보 · 배경 ─────

    /// <summary>
    /// 지금 탭의 곡으로 앨범·곡 정보·배경을 맞춘다. 곡이 없으면(나만의 채보 0개) 공식 탭에서 고른 곡을 그대로 보여 준다.
    /// </summary>
    private void ShowCurrentSong(bool instantBackground)
    {
        SongData song = CurrentSong ?? (chosenItem != null ? chosenItem.Song : null);
        if (song == null) return;

        coverImage.sprite = song.Cover;
        coverImage.color = song.Cover != null ? Color.white : UITheme.Current.emptyCover;
        songTitleText.text = song.Title;
        artistText.text = string.IsNullOrEmpty(song.Artist) ? GameConstants.SongSelect.EmptyValue : song.Artist;

        string bpmText = song.Bpm > 0f ? SongData.FormatBpm(song.Bpm) : GameConstants.SongSelect.EmptyValue;
        songInfoText.text = string.Format(GameConstants.SongSelect.SongInfoFormat, bpmText, song.GetLengthText());

        if (menuBackground != null) menuBackground.SetIllustration(song.Background, instantBackground);
    }

    private SongData FindSong(string songId)
    {
        if (string.IsNullOrEmpty(songId)) return null;
        return songsById.TryGetValue(songId, out SongData song) ? song : null;
    }

    private void FocusCurrent()
    {
        GameObject target = CurrentFocusTarget;
        if (EventSystem.current != null && target != null && EventSystem.current.currentSelectedGameObject != target)
            EventSystem.current.SetSelectedGameObject(target);
    }

    // ───── 공식 채보 탭: 곡 목록 · 난이도 ─────

    /// <summary>
    /// 난이도 카드에 GameConstants.Chart.Difficulties 순서대로 난이도 이름을 정한다.
    /// </summary>
    private void InitDifficultyButtons()
    {
        string[] difficulties = GameConstants.Chart.Difficulties;
        if (difficultyButtons.Length != difficulties.Length)
            Debug.LogWarning($"{LogPrefix} 난이도 카드 수({difficultyButtons.Length})와 난이도 수({difficulties.Length})가 다릅니다.", this);

        for (int i = 0; i < difficultyButtons.Length && i < difficulties.Length; i++)
            difficultyButtons[i].Init(difficulties[i]);
    }

    /// <summary>
    /// Content의 기존 자식을 비우고 곡마다 아이템을 만든다. 오른쪽 난이도 칸(레벨·최고 랭크·도장)도 채운다.
    /// </summary>
    private void CreateSongItems(IReadOnlyList<SongData> songs)
    {
        for (int i = songListContent.childCount - 1; i >= 0; i--)
            Destroy(songListContent.GetChild(i).gameObject);

        songItems.Clear();
        songsById.Clear();

        foreach (SongData song in songs)
        {
            if (song == null) continue;
            songsById[song.SongId] = song;

            Btn_SongItem item = Instantiate(songItemPrefab, songListContent);
            item.name = songItemPrefab.name;
            UIThemeColor.ApplyAll(item.gameObject);
            item.Init(song, OnSongChosen);
            item.SetDifficulties(ChartRepository.GetCharts(song.SongId, ChartType.Official), FindRecord);
            songItems.Add(item);
        }
    }

    /// <summary>
    /// 곡 아이템끼리 위아래로만 이동하게 연결한다 (순환 없음). ←→는 난이도 이동에 쓰므로 연결하지 않는다.
    /// </summary>
    private void ConnectSongNavigation()
    {
        for (int i = 0; i < songItems.Count; i++)
        {
            songItems[i].Button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? songItems[i - 1].Button : null,
                selectOnDown = i < songItems.Count - 1 ? songItems[i + 1].Button : null
            };
        }
    }

    /// <summary>
    /// 곡 아이템이 선택되면 호출된다(↑↓, 마우스). 난이도를 다시 채우고 왼쪽·배경을 바꾼다.
    /// </summary>
    private void OnSongChosen(SongData song)
    {
        if (isLeaving) return;

        Btn_SongItem item = songItems.Find(view => view.Song == song);
        if (item == null || item == chosenItem) return;

        ChooseSongItem(item, null);
        if (currentTab != ChartType.Official) return;

        selectionKeeper.SetDefaultSelectable(item.Button);
        ShowCurrentSong(false);
        RefreshChosenChart();
        RefreshCounts();
        ScrollSongListToShow(item);
        LogState("곡 변경");
    }

    /// <summary>공식 탭 곡을 고르고 난이도를 다시 채운다 (왼쪽 표시는 부르는 쪽이 맞춘다)</summary>
    /// <param name="preferredChartId">이 채보가 있으면 고른다. 없으면 가장 낮은 난이도</param>
    private void ChooseSongItem(Btn_SongItem item, string preferredChartId)
    {
        if (chosenItem != null) chosenItem.SetChosen(false);
        item.SetChosen(true);
        chosenItem = item;
        RefreshDifficulties(preferredChartId);
    }

    /// <summary>
    /// 고른 곡의 공식 채보로 난이도 카드를 채우고 난이도를 고른다. 하나도 없으면 빈 상태 문구를 켠다.
    /// </summary>
    /// <param name="preferredChartId">이 채보가 있으면 고른다. 없으면 가장 낮은 난이도</param>
    private void RefreshDifficulties(string preferredChartId)
    {
        List<ChartData> charts = ChartRepository.GetCharts(chosenItem.Song.SongId, ChartType.Official);

        foreach (Btn_Difficulty button in difficultyButtons)
            button.SetChart(charts.Find(chart => string.Equals(chart.difficulty, button.Difficulty, StringComparison.OrdinalIgnoreCase)));

        officialEmptyText.gameObject.SetActive(charts.Count == 0);

        Btn_Difficulty preferred = null;
        if (!string.IsNullOrEmpty(preferredChartId))
            preferred = Array.Find(difficultyButtons, button => button.Chart != null && button.Chart.chartId == preferredChartId);

        // 난이도 카드는 낮은 난이도부터 놓여 있으므로 처음 만나는 채보 있는 카드가 가장 낮은 난이도다
        chosenDifficulty = null;
        SetChosenDifficulty(preferred ?? Array.Find(difficultyButtons, button => button.Chart != null));
    }

    private void SetChosenDifficulty(Btn_Difficulty target)
    {
        if (chosenDifficulty != null) chosenDifficulty.SetChosen(false);
        chosenDifficulty = target;
        if (chosenDifficulty != null) chosenDifficulty.SetChosen(true);
        RefreshChosenChart();
    }

    /// <summary>
    /// 난이도 카드 클릭. 채보가 없는 난이도는 버튼이 비활성이라 불리지 않는다.
    /// </summary>
    private void ChooseDifficulty(Btn_Difficulty target)
    {
        if (isLeaving || target.Chart == null) return;

        SetChosenDifficulty(target);
        LogState("난이도 선택");
    }

    /// <summary>
    /// ←→ (공식 탭): 채보가 있는 난이도 중 옆의 것으로 옮긴다. 끝에서는 멈춘다.
    /// </summary>
    private void MoveDifficulty(int direction)
    {
        int current = Array.IndexOf(difficultyButtons, chosenDifficulty);
        if (current < 0) return;

        for (int i = current + direction; i >= 0 && i < difficultyButtons.Length; i += direction)
        {
            if (difficultyButtons[i].Chart == null) continue;

            SetChosenDifficulty(difficultyButtons[i]);
            LogState("난이도 이동");
            return;
        }
    }

    // ───── 나만의 채보 탭: 곡별로 묶은 채보 목록 ─────

    /// <summary>
    /// 모든 곡을 곡 순서대로 묶어 목록을 만든다 (11/18): 곡 머리 줄(선택 안 됨, 채보가 없으면 NO CHART) 아래 채보 줄 (곡 안에서는 chartId 내림차순, 늦게 만든 것이 위).
    /// "+ 새 채보 만들기" 줄은 하나만 만들어 두고 고른 곡 묶음 끝으로 옮긴다 (SetChosenStop). ↑↓는 줄이 넘겨 준 이동 이벤트로 MyChartListNavigator가 정한다.
    /// </summary>
    private void CreateMyChartList()
    {
        for (int i = myChartContent.childCount - 1; i >= 0; i--)
            Destroy(myChartContent.GetChild(i).gameObject);

        myChartRows.Clear();
        songGroupHeaders.Clear();
        groupSongs.Clear();
        groupRows.Clear();
        groupChartIds.Clear();
        groupSongIds.Clear();
        chosenStop = -1;
        List<int> groupSizes = new List<int>();

        foreach (Btn_SongItem songItem in songItems)
        {
            SongData song = songItem.Song;
            List<ChartData> charts = ChartRepository.GetCharts(song.SongId, ChartType.Custom);
            charts.Sort((a, b) => string.CompareOrdinal(b.chartId, a.chartId));

            SongGroupHeader header = Instantiate(songGroupHeaderPrefab, myChartContent);
            header.name = songGroupHeaderPrefab.name;
            UIThemeColor.ApplyAll(header.gameObject);
            header.Init(song, charts.Count);
            songGroupHeaders.Add(header);

            List<Btn_MyChartItem> rows = new List<Btn_MyChartItem>();
            List<string> ids = new List<string>();
            foreach (ChartData chart in charts)
            {
                Btn_MyChartItem row = Instantiate(myChartItemPrefab, myChartContent);
                row.name = myChartItemPrefab.name;
                UIThemeColor.ApplyAll(row.gameObject);
                row.Init(chart, OnMyChartClicked);
                row.SetSelectCallback(OnMyChartSelected);
                row.SetMoveCallback(MoveMyChartVertical);
                row.SetRecord(FindRecord(chart.chartId));
                myChartRows.Add(row);
                rows.Add(row);
                ids.Add(chart.chartId);
            }

            groupSongs.Add(song);
            groupSongIds.Add(song.SongId);
            groupRows.Add(rows);
            groupChartIds.Add(ids);
            groupSizes.Add(charts.Count);
        }

        myChartNavigator = new MyChartListNavigator(groupSizes);

        if (newChartRowPrefab != null)
        {
            newChartRow = Instantiate(newChartRowPrefab, myChartContent);
            newChartRow.name = newChartRowPrefab.name;
            UIThemeColor.ApplyAll(newChartRow.gameObject);
            newChartRow.Init(KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(startAction, GameConstants.KeyGuide.EnterKey)),
                OnNewChartRowSelected, OpenNewChartPopup, MoveMyChartVertical);
            newChartRow.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} newChartRowPrefab이 연결되지 않아 + 새 채보 만들기 줄을 만들지 않습니다.", this);
        }

        myChartScrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>채보 줄이 선택됐을 때(↑↓, 마우스 올림): 그 채보를 고른다</summary>
    private void OnMyChartSelected(ChartData chart)
    {
        if (isLeaving || currentTab != ChartType.Custom) return;

        int stop = myChartNavigator.StopOfChart(myChartRows.FindIndex(row => row.Chart == chart));
        if (stop < 0 || stop == chosenStop) return;
        ChooseStop(stop, "채보 이동");
    }

    /// <summary>채보 줄 클릭 (마우스): 그 채보를 고른다 (시작은 Enter · 시작 버튼)</summary>
    private void OnMyChartClicked(ChartData chart)
    {
        if (isLeaving) return;

        int stop = myChartNavigator.StopOfChart(myChartRows.FindIndex(row => row.Chart == chart));
        if (stop >= 0 && stop != chosenStop) ChooseStop(stop, "채보 선택");
    }

    /// <summary>+ 줄이 선택됐을 때(마우스 올림 등): 지금 곡의 + 줄을 고른다</summary>
    private void OnNewChartRowSelected()
    {
        if (isLeaving || currentTab != ChartType.Custom) return;

        int stop = myChartNavigator.AddRowOfGroup(myChartNavigator.Get(chosenStop).Group);
        if (stop >= 0 && stop != chosenStop) ChooseStop(stop, "+ 줄 선택");
    }

    /// <summary>
    /// ↑(-1)·↓(+1) (나만의 채보 탭, 줄이 넘긴 EventSystem 이동 이벤트): 정지점 한 칸. 끝에서는 멈춘다.
    /// </summary>
    private void MoveMyChartVertical(int direction)
    {
        if (IsShortcutBlocked || currentTab != ChartType.Custom) return;

        int next = myChartNavigator.Vertical(chosenStop, direction);
        if (next < 0 || next == chosenStop) return;
        ChooseStop(next, "정지점 이동");
    }

    /// <summary>
    /// ←→ (나만의 채보 탭): 이전·다음 곡 묶음의 첫 정지점으로. 끝에서는 멈춘다.
    /// </summary>
    private void MoveMyChartGroup(int direction)
    {
        int next = myChartNavigator.Group(chosenStop, direction);
        if (next < 0 || next == chosenStop) return;
        ChooseStop(next, "곡 묶음 이동");
    }

    /// <summary>정지점을 고르고 왼쪽·배경·숫자·스크롤·키보드 선택을 맞춘다</summary>
    private void ChooseStop(int stop, string reason)
    {
        int previousGroup = myChartNavigator.Get(chosenStop).Group;
        SetChosenStop(stop, true);

        GameObject target = StopTarget(chosenStop);
        Selectable focus = target != null ? target.GetComponent<Selectable>() : null;
        if (focus != null) selectionKeeper.SetDefaultSelectable(focus);
        if (previousGroup != myChartNavigator.Get(chosenStop).Group) ShowCurrentSong(false);
        RefreshChosenChart();
        RefreshCounts();
        ScrollMyChartToShow(chosenStop);
        FocusCurrent();
        LogState(reason);
    }

    /// <summary>
    /// 고른 정지점 표시를 바꾼다: 채보 줄 강조, 곡 머리 줄 하늘색, + 줄을 고른 곡 묶음 끝(채보가 없는 곡은 머리 줄 바로 아래)으로 옮기고 높이(46 / 60)를 맞춘다.
    /// keepAnchor면 + 줄이 옮겨져도 목록이 튀지 않게, 새로 고른 줄(+ 줄이면 그 곡 머리 줄)의 화면 위치를 옮기기 전과 같게 둔다.
    /// </summary>
    private void SetChosenStop(int stop, bool keepAnchor)
    {
        MyChartStop previous = myChartNavigator.Get(chosenStop);
        MyChartStop next = myChartNavigator.Get(stop);

        RectTransform anchor = null;
        float anchorTop = 0f;
        if (keepAnchor && next.Group >= 0 && myChartPanel.activeInHierarchy)
        {
            anchor = next.IsAddRow ? (RectTransform)songGroupHeaders[next.Group].transform : (RectTransform)StopTarget(stop).transform;
            anchorTop = ViewportTop(anchor);
        }

        if (previous.Kind == MyChartStopKind.Chart && previous.ChartIndex >= 0 && previous.ChartIndex < myChartRows.Count)
            myChartRows[previous.ChartIndex].SetChosen(false);
        if (previous.Group >= 0 && previous.Group < songGroupHeaders.Count && previous.Group != next.Group)
            songGroupHeaders[previous.Group].SetChosen(false);

        chosenStop = next.Group >= 0 ? stop : -1;
        if (next.Group < 0)
        {
            if (newChartRow != null) newChartRow.gameObject.SetActive(false);
            return;
        }

        songGroupHeaders[next.Group].SetChosen(true);
        if (next.Kind == MyChartStopKind.Chart) myChartRows[next.ChartIndex].SetChosen(true);

        if (newChartRow != null)
        {
            // + 줄: 고른 곡 묶음의 마지막 채보 줄 바로 아래 (채보가 없으면 머리 줄 바로 아래)
            Transform header = songGroupHeaders[next.Group].transform;
            int index = header.GetSiblingIndex() + groupRows[next.Group].Count + 1;
            if (newChartRow.transform.GetSiblingIndex() < index) index--;
            newChartRow.transform.SetSiblingIndex(index);
            newChartRow.gameObject.SetActive(true);
            newChartRow.SetChosen(next.IsAddRow);
        }

        if (anchor != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(myChartContent);
            float shift = anchorTop - ViewportTop(anchor);
            if (Mathf.Abs(shift) > 0.01f)
            {
                myChartScrollRect.StopMovement();
                Vector2 position = myChartContent.anchoredPosition;
                position.y -= shift;
                myChartContent.anchoredPosition = position;
            }
        }
    }

    /// <summary>항목 위 끝의 Viewport 기준 y</summary>
    private float ViewportTop(RectTransform item)
    {
        item.GetWorldCorners(worldCorners);
        return myChartScrollRect.viewport.InverseTransformPoint(worldCorners[1]).y;
    }

    // ───── 새 채보 · 편집 ─────

    /// <summary>
    /// + 줄(Enter · 시작 버튼 · 클릭): 고른 곡으로 새 채보 팝업을 연다. 이미 열려 있으면 그대로 둔다.
    /// </summary>
    private void OpenNewChartPopup()
    {
        if (isLeaving || currentTab != ChartType.Custom || newChartPopup == null || newChartPopup.IsOpen) return;

        MyChartStop stop = myChartNavigator.Get(chosenStop);
        if (stop.Group < 0) return;
        if (!stop.IsAddRow) ChooseStop(myChartNavigator.AddRowOfGroup(stop.Group), "+ 줄 클릭");

        newChartPopup.Init(groupSongs[stop.Group], groupRows[stop.Group].Count, OnNewChartCreate, OnNewChartCanceled);
        newChartPopup.Open();
        LogState("새 채보 팝업");
    }

    /// <summary>새 채보 팝업 만들기: 인게임과 같은 진입 함수(InGameLauncher.StartEditor)로 에디터에 간다</summary>
    private bool OnNewChartCreate(EditorEntryData data)
    {
        if (isLeaving || !InGameLauncher.StartEditor(data)) return false;
        SetLeaving();
        return true;
    }

    /// <summary>새 채보 팝업 취소: + 줄을 다시 고른다</summary>
    private void OnNewChartCanceled()
    {
        FocusCurrent();
    }

    private void OnEditKey(InputAction.CallbackContext context)
    {
        EditChosenChart();
    }

    /// <summary>
    /// E (나만의 채보 탭 채보 줄): 그 채보를 에디터로 연다 (InGameLauncher.StartEditor). 공식 탭 · + 줄 · 팝업이 열려 있으면 아무것도 하지 않는다.
    /// </summary>
    public bool EditChosenChart()
    {
        if (IsShortcutBlocked || currentTab != ChartType.Custom) return false;

        ChartData chart = ChosenMyChart;
        if (chart == null) return false;

        EditorEntryData data = new EditorEntryData { songId = chart.songId, chartId = chart.chartId, chartName = chart.chartName, level = chart.level };
        LogState("편집");
        if (!InGameLauncher.StartEditor(data)) return false;
        SetLeaving();
        return true;
    }

    /// <summary>
    /// 고른 채보 정보 카드: CUSTOM · 채보 이름 · Lv · 노트 수 · 롱노트 수 · 마지막 수정일 (파일 수정 시각, 없으면 —).
    /// </summary>
    private void ShowMyChartCard(ChartData chart)
    {
        if (myChartCard == null) return;
        myChartCard.SetActive(chart != null);
        if (chart == null) return;

        int noteCount = chart.notes != null ? chart.notes.Count : 0;
        int longCount = chart.notes != null ? chart.notes.FindAll(note => note.type == NoteData.TypeLong).Count : 0;
        myChartNameText.text = chart.chartName;
        myChartLevelText.text = string.Format(GameConstants.SongSelect.MyChartLevelFormat, chart.level);
        myChartStatsText.text = string.Format(GameConstants.SongSelect.MyChartStatsFormat, noteCount, longCount, GetModifiedDateText(chart));
    }

    private static string GetModifiedDateText(ChartData chart)
    {
        string path = ChartRepository.GetChartPath(chart.songId, chart.chartId, ChartType.Custom);
        return File.Exists(path)
            ? File.GetLastWriteTime(path).ToString(GameConstants.SongSelect.ModifiedDateFormat, CultureInfo.InvariantCulture)
            : GameConstants.SongSelect.EmptyValue;
    }

    // ───── 키 입력 ─────

    private void OnPrevChart(InputAction.CallbackContext context)
    {
        MoveChart(-1);
    }

    private void OnNextChart(InputAction.CallbackContext context)
    {
        MoveChart(1);
    }

    private void MoveChart(int direction)
    {
        if (IsShortcutBlocked) return;

        if (currentTab == ChartType.Official) MoveDifficulty(direction);
        else MoveMyChartGroup(direction);
    }

    private void OnSpeedUpStarted(InputAction.CallbackContext context) => PressSpeedKey(speedUpRepeat, 1);
    private void OnSpeedDownStarted(InputAction.CallbackContext context) => PressSpeedKey(speedDownRepeat, -1);
    private void OnSpeedUpCanceled(InputAction.CallbackContext context) => speedUpRepeat.Release();
    private void OnSpeedDownCanceled(InputAction.CallbackContext context) => speedDownRepeat.Release();

    /// <summary>배속 키를 누른 순간: 한 번 바꾸고 반복을 시작한다. 팝업이 열려 있거나 떠나는 중이면 무시한다 (반복도 시작하지 않는다)</summary>
    private void PressSpeedKey(KeyRepeat repeat, int delta)
    {
        if (IsShortcutBlocked) return;

        repeat.Press(Time.realtimeSinceStartupAsDouble);
        ChangeSpeed(delta);
    }

    /// <summary>
    /// 누르고 있는 배속 키의 반복 (매 프레임, 인게임과 같은 처리). 팝업이 열려 있거나 떠나는 중이면 반복 시각만 지나가고 배속은 바꾸지 않는다.
    /// </summary>
    private void UpdateSpeedRepeat()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        int down = speedDownRepeat.Tick(now);
        int up = speedUpRepeat.Tick(now);
        if (down + up == 0 || IsShortcutBlocked) return;
        ChangeSpeed(up - down);
    }

    /// <summary>배속 키 반복을 멈춘다 (입력을 끊을 때, 씬을 떠날 때)</summary>
    private void ReleaseSpeedKeys()
    {
        speedUpRepeat.Release();
        speedDownRepeat.Release();
    }

#if UNITY_EDITOR
    /// <summary>테스트 전용 (Unity 에디터): 배속 키를 누르고 있으면 true</summary>
    public bool TestIsSpeedKeyHeld(bool up) => (up ? speedUpRepeat : speedDownRepeat).IsHeld;
#endif

    /// <summary>
    /// 배속을 delta 단계(0.1배)만큼 올리거나 내린다 (2 / 1, 꾹 누르면 반복). 범위(1.0 ~ 8.0) 끝에서는 멈춘다.
    /// 인게임·에디터와 같은 ScrollSpeedRules로 정수 단계 계산을 한다.
    /// </summary>
    private void ChangeSpeed(int step)
    {
        if (step == 0 || IsShortcutBlocked) return;

        int next = ScrollSpeedRules.Step(speedSteps, step);
        if (next == speedSteps) return;

        speedSteps = next;
        SetSpeedText(ScrollMultiplier);
    }

    private void SetSpeedText(float multiplier)
    {
        speedText.text = string.Format(CultureInfo.InvariantCulture, GameConstants.SongSelect.SpeedFormat, multiplier);
    }

    private void OnStartKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        StartGame();
    }

    /// <summary>
    /// 키보드 단축키를 막아야 하면 true: 씬을 떠나는 중이거나 팝업이 열려 있을 때.
    /// </summary>
    private bool IsShortcutBlocked => isLeaving || (UIManager.HasInstance && GameManager.UI.HasOpenPopup);

    /// <summary>
    /// 타이틀로 나간다 (ESC → SongSelectScene.OnBack).
    /// </summary>
    public void ExitToTitle()
    {
        if (isLeaving) return;

        if (GameManager.Scene.Load(GameConstants.Scenes.Title))
            SetLeaving();
    }

    // ───── 시작 ─────

    /// <summary>
    /// 고른 채보가 바뀌었을 때: 시작 버튼(이름 · 활성), 최고 기록 카드, 고른 채보 카드 / 점선 새 채보 카드, 키 안내 줄을 다시 맞춘다.
    /// 나만의 채보 탭에서 + 줄을 고르면 점선 카드, 최고 기록 카드는 비우고 흐리게(0.35), 시작 버튼은 "새 채보 / NEW".
    /// </summary>
    private void RefreshChosenChart()
    {
        bool addRow = IsNewChartRowChosen;
        startButton.interactable = !isLeaving && (ChosenChart != null || addRow);
        ShowRecord(ChosenChart);
        if (recordCardGroup != null) recordCardGroup.alpha = addRow ? GameConstants.SongSelect.NewChartRecordAlpha : 1f;
        if (startLabelText != null) startLabelText.text = addRow ? GameConstants.SongSelect.NewChartStartLabel : GameConstants.SongSelect.StartLabel;
        if (startLabelEnText != null) startLabelEnText.text = addRow ? GameConstants.SongSelect.NewChartStartLabelEn : GameConstants.SongSelect.StartLabelEn;

        if (currentTab == ChartType.Custom)
        {
            ShowMyChartCard(ChosenMyChart);
            if (newChartCard != null) newChartCard.SetActive(addRow);
            if (myChartHeaderText != null) myChartHeaderText.text = addRow ? GameConstants.SongSelect.NewChartHeader : GameConstants.SongSelect.MyChartHeader;
        }
        RefreshKeyGuide();
    }

    /// <summary>
    /// 고른 채보로 인게임을 시작한다 (Enter, 시작 버튼). 나만의 채보 탭에서 + 줄을 골랐으면 새 채보 팝업을 연다. 고른 채보가 없으면 아무것도 하지 않는다.
    /// 결과 다시 하기와 같은 경로(InGameLauncher)로 들어가며, 일반 플레이라 그 안에서 마지막 배속과 마지막으로 플레이한 곡·채보·탭을 저장한다.
    /// </summary>
    private void StartGame()
    {
        if (IsNewChartRowChosen)
        {
            OpenNewChartPopup();
            return;
        }

        ChartData chart = ChosenChart;
        SongData song = CurrentSong;
        if (isLeaving || song == null || chart == null) return;

        InGameEntryData data = InGameLauncher.CreateNormalEntry(song.SongId, chart.chartId, currentTab, ScrollMultiplier);
        LogState("시작");
        if (InGameLauncher.Start(data)) SetLeaving();
    }

    /// <summary>
    /// 씬 이동을 시작하면 입력과 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void SetLeaving()
    {
        isLeaving = true;
        ReleaseSpeedKeys();
        officialTabButton.interactable = false;
        myChartTabButton.interactable = false;
        startButton.interactable = false;
        foreach (Btn_SongItem item in songItems) item.Button.interactable = false;
        foreach (Btn_Difficulty button in difficultyButtons) button.Button.interactable = false;
        foreach (Btn_MyChartItem item in myChartRows) item.Button.interactable = false;
        if (newChartRow != null && newChartRow.Button != null) newChartRow.Button.interactable = false;
    }

    // ───── 스크롤 ─────

    private void ScrollSongListToShow(Btn_SongItem item)
    {
        if (!officialPanel.activeInHierarchy) return;
        ScrollToShow(songListScrollRect, songListContent, (RectTransform)item.transform);
    }

    /// <summary>고른 정지점이 보이게 스크롤한다. 곡 묶음의 첫 정지점이면 곡 머리 줄도 보이게 한다</summary>
    private void ScrollMyChartToShow(int stop)
    {
        MyChartStop value = myChartNavigator.Get(stop);
        GameObject target = StopTarget(stop);
        if (!myChartPanel.activeInHierarchy || value.Group < 0 || target == null) return;

        if (stop == myChartNavigator.FirstStopOfGroup(value.Group) && value.Group < songGroupHeaders.Count)
            ScrollToShow(myChartScrollRect, myChartContent, (RectTransform)songGroupHeaders[value.Group].transform);
        ScrollToShow(myChartScrollRect, myChartContent, (RectTransform)target.transform);
    }

    /// <summary>
    /// 항목이 Viewport 밖에 있으면 보이도록 목록을 스크롤한다.
    /// </summary>
    private void ScrollToShow(ScrollRect scrollRect, RectTransform content, RectTransform item)
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        RectTransform viewport = scrollRect.viewport;
        item.GetWorldCorners(worldCorners);
        float itemBottom = viewport.InverseTransformPoint(worldCorners[0]).y;
        float itemTop = viewport.InverseTransformPoint(worldCorners[1]).y;
        Rect view = viewport.rect;

        Vector2 position = content.anchoredPosition;
        if (itemTop > view.yMax) position.y -= itemTop - view.yMax;
        else if (itemBottom < view.yMin) position.y += view.yMin - itemBottom;
        else return;

        scrollRect.StopMovement();
        content.anchoredPosition = position;
    }

    /// <summary>항목이 Viewport 안에 다 보이면 true (확인용)</summary>
    public bool IsVisibleInList(RectTransform item)
    {
        ScrollRect scrollRect = item.GetComponentInParent<ScrollRect>();
        if (scrollRect == null) return false;

        Canvas.ForceUpdateCanvases();
        RectTransform viewport = scrollRect.viewport;
        item.GetWorldCorners(worldCorners);
        float itemBottom = viewport.InverseTransformPoint(worldCorners[0]).y;
        float itemTop = viewport.InverseTransformPoint(worldCorners[1]).y;
        Rect view = viewport.rect;
        return itemTop <= view.yMax + 0.5f && itemBottom >= view.yMin - 0.5f;
    }

    // ───── 표시 ─────

    /// <summary>목록 위쪽 숫자: 공식 "01 / 08" (곡 순번), 나만의 채보 "곡 02 / 08 · 채보 5" (고른 곡 순번 / 전체 곡 수 · 전체 채보 수). 곡이 없으면 숨긴다</summary>
    private void RefreshCounts()
    {
        if (songCountText != null)
        {
            int index = chosenItem != null ? songItems.IndexOf(chosenItem) + 1 : 0;
            songCountText.text = string.Format(GameConstants.SongSelect.SongCountFormat, index, songItems.Count);
        }
        if (myChartCountText != null)
        {
            myChartCountText.gameObject.SetActive(myChartNavigator.GroupCount > 0);
            myChartCountText.text = FormatMyChartCount(myChartNavigator.Get(chosenStop).Group, myChartNavigator.GroupCount, myChartNavigator.ChartCount);
        }
    }

    /// <summary>나만의 채보 탭 위쪽 숫자 (순수 함수): "곡 {고른 곡 순번:00} / {전체 곡:00} · 채보 {전체 채보}"</summary>
    public static string FormatMyChartCount(int chosenGroup, int groupCount, int chartCount)
    {
        return string.Format(GameConstants.SongSelect.MyChartCountFormat, Math.Max(0, chosenGroup + 1), groupCount, chartCount);
    }

    private static ChartRecordData FindRecord(string chartId)
    {
        return PlayerDataManager.HasInstance ? GameManager.PlayerData.FindRecord(chartId) : null;
    }

    /// <summary>
    /// 최고 기록 카드: 고른 채보(공식 난이도 / 나만의 채보)의 기록 (SongSelectRecordFormat). 채보가 없거나 기록이 없으면 각 칸 "—".
    /// </summary>
    private void ShowRecord(ChartData chart)
    {
        RecordTexts texts = SongSelectRecordFormat.Build(chart != null ? FindRecord(chart.chartId) : null);
        bestScoreText.text = texts.Score;
        rateText.text = texts.Rate;
        comboText.text = texts.Combo;
        UITextLevel valueLevel = texts.HasRecord ? UITextLevel.Primary : UITextLevel.Muted;
        UIThemePaint.Text(bestScoreText, valueLevel);
        UIThemePaint.Text(rateText, valueLevel);
        UIThemePaint.Text(comboText, valueLevel);
        recordRank.Show(texts.HasRecord, texts.Rank);
        recordFullCombo.SetFullCombo(texts.HasRecord && texts.IsFullCombo);
    }

    /// <summary>최고 기록 카드 랭크 칸 · FC (확인용)</summary>
    public Img_Rank RecordRank => recordRank;
    public Img_FullCombo RecordFullCombo => recordFullCombo;

    /// <summary>
    /// 지금 선택 상태를 로그로 남긴다 (확인용).
    /// </summary>
    private void LogState(string reason)
    {
        ChartData chart = ChosenChart;
        Debug.Log($"{LogPrefix} {reason}: 곡={ChosenSongId}, 탭={currentTab}, 채보={(chart != null ? $"{chart.chartId}({chart.difficulty} Lv.{chart.level})" : "없음")}, " +
                  $"배속=x{ScrollMultiplier:0.0}, 시작 버튼={(startButton.interactable ? "활성" : "비활성")}", this);
    }
}
