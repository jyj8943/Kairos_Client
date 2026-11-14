using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면 UI (기획서 3-3). SongSelectScene이 Init()을 호출한다.
/// - 한 Canvas 안에서 공식 채보 탭(OfficialPanel)과 나만의 채보 탭(MyChartPanel)을 바꿔 보여준다.
/// - 공통: 곡 목록(Btn_SongItem), 앨범 이미지, 곡 정보, 최고 기록(지금은 "-"), 배속, 시작 버튼, 탭 버튼, 조작 안내.
/// - 공식 탭: 난이도 버튼(Btn_Difficulty) 3개. 나만의 채보 탭: 채보 목록(Btn_MyChartItem).
/// - 조작 (기획서 3-9): ↑↓ 곡(EventSystem 내비게이션), ←→ 난이도/채보, Tab 탭 전환, 1 / 2 배속(꾹 누르면 인게임과 같은 KeyRepeat로 계속), Enter 시작, ESC 타이틀.
///   아래쪽 키 안내 줄(KeyGuideBar)에 같은 순서로 보여주고, 키 이름은 바인딩에서 읽는다 (←→ 이름은 탭에 따라 난이도 ↔ 채보).
///   ↑↓ 외의 키는 입력 에셋의 SongSelect 맵으로 받는다. ESC는 UIManager가 받아 SongSelectScene.OnBack → ExitToTitle.
///   팝업이 열려 있으면 키보드 단축키는 동작하지 않는다 (GameManager.UI.HasOpenPopup). 마우스로도 곡·탭·난이도·채보·시작을 고른다.
/// - 키보드 선택(EventSystem)은 항상 고른 곡 항목에 둔다. 마우스로 다른 것을 누르면 다음 프레임에 곡 항목으로 되돌린다.
/// </summary>
public class Canvas_SongSelect : UIBase
{
    private const string LogPrefix = "[Canvas_SongSelect]";

    [Header("곡 목록")]
    [SerializeField] private Btn_SongItem songItemPrefab;
    [Tooltip("ScrollView_SongList")]
    [SerializeField] private ScrollRect songListScrollRect;
    [Tooltip("ScrollView_SongList/Viewport/Content")]
    [SerializeField] private RectTransform songListContent;
    [SerializeField] private UISelectionKeeper selectionKeeper;

    [Header("탭")]
    [SerializeField] private Button officialTabButton;
    [SerializeField] private TMP_Text officialTabText;
    [SerializeField] private Button myChartTabButton;
    [SerializeField] private TMP_Text myChartTabText;
    [SerializeField] private GameObject officialPanel;
    [SerializeField] private GameObject myChartPanel;
    [SerializeField] private Color chosenTabColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color chosenTabTextColor = Color.white;
    [SerializeField] private Color normalTabColor = Color.white;
    [SerializeField] private Color normalTabTextColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

    [Header("곡 정보")]
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text songTitleText;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private Color emptyCoverColor = new Color32(0xD4, 0xD4, 0xD8, 0xFF);

    [Header("공식 채보 탭")]
    [Tooltip("EASY · NORMAL · HARD 순서 (GameConstants.Chart.Difficulties와 같은 순서)")]
    [SerializeField] private Btn_Difficulty[] difficultyButtons;
    [SerializeField] private TMP_Text officialEmptyText;

    [Header("나만의 채보 탭")]
    [SerializeField] private Btn_MyChartItem myChartItemPrefab;
    [Tooltip("ScrollView_MyChart")]
    [SerializeField] private ScrollRect myChartScrollRect;
    [Tooltip("ScrollView_MyChart/Viewport/Content")]
    [SerializeField] private RectTransform myChartContent;
    [SerializeField] private TMP_Text myChartEmptyText;

    [Header("최고 기록 (고른 채보의 기록, 없으면 \"-\")")]
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text bestRankText;
    [SerializeField] private TMP_Text bestComboText;
    [SerializeField] private TMP_Text badgeText;

    [Header("하단")]
    [SerializeField] private TMP_Text speedText;
    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;
    [SerializeField] private Button startButton;

    [Header("입력 (입력 에셋의 SongSelect 맵. ESC는 UIManager)")]
    [SerializeField] private InputActionReference prevChartAction;
    [SerializeField] private InputActionReference nextChartAction;
    [SerializeField] private InputActionReference switchTabAction;
    [SerializeField] private InputActionReference speedUpAction;
    [SerializeField] private InputActionReference speedDownAction;
    [SerializeField] private InputActionReference startAction;

    private readonly List<Btn_SongItem> songItems = new List<Btn_SongItem>();

    /// <summary>나만의 채보 항목 풀. 곡을 바꿀 때 앞에서부터 다시 쓰고 남는 것은 끈다</summary>
    private readonly List<Btn_MyChartItem> myChartItemPool = new List<Btn_MyChartItem>();
    private readonly List<Btn_MyChartItem> activeMyChartItems = new List<Btn_MyChartItem>();
    private readonly Vector3[] worldCorners = new Vector3[4];

    private Btn_SongItem chosenItem;
    private SongData chosenSong;
    private ChartType currentTab = ChartType.Official;
    private Btn_Difficulty chosenDifficulty;
    private Btn_MyChartItem chosenMyChart;

    /// <summary>배속 × GameConstants.Play.ScrollMultiplierScale (0.1 단위 오차 없이 계산)</summary>
    private int speedSteps;

    /// <summary>곡을 처음 고를 때 이 채보를 고른다 (곡 선택으로 돌아온 경우). 한 번 쓰면 비운다</summary>
    private string pendingChartId;

    private bool isInputEnabled;
    private bool isLeaving;

    // 배속 키 반복 (인게임과 같은 값: 누른 순간 한 번, 0.4초 뒤부터 0.08초마다)
    private readonly KeyRepeat speedDownRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);
    private readonly KeyRepeat speedUpRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);

    /// <summary>지금 보이는 탭</summary>
    public ChartType CurrentTab => currentTab;

    /// <summary>고른 곡 ID (없으면 null)</summary>
    public string ChosenSongId => chosenSong != null ? chosenSong.SongId : null;

    /// <summary>지금 탭에서 고른 채보 (없으면 null)</summary>
    public ChartData ChosenChart => currentTab == ChartType.Official
        ? (chosenDifficulty != null ? chosenDifficulty.Chart : null)
        : (chosenMyChart != null ? chosenMyChart.Chart : null);

    /// <summary>지금 배속</summary>
    public float ScrollMultiplier => ScrollSpeedRules.FromSteps(speedSteps);

    /// <summary>
    /// 곡 목록과 난이도 버튼을 만들고 버튼 리스너를 연결한 뒤, 처음 상태(곡·탭·채보·배속)로 시작한다.
    /// </summary>
    /// <param name="entry">곡 선택으로 돌아온 경우의 선택 상태. null이면 첫 곡·공식 탭·저장된 마지막 배속</param>
    public void Init(IReadOnlyList<SongData> songs, SongSelectEntryData entry)
    {
        isLeaving = false;

        InitDifficultyButtons();
        CreateSongItems(songs);
        ConnectSongNavigation();
        AddButtonListeners();

        officialEmptyText.text = GameConstants.SongSelect.OfficialEmpty;
        myChartEmptyText.text = GameConstants.SongSelect.MyChartEmpty;
        ShowRecord(null);
        InitKeyGuide();

        // 인게임·결과에서 돌아오면 넘겨받은 배속, 타이틀에서 들어오면(entry 없음) PlayerData.json에 저장된 마지막 배속 (없으면 1.0)
        float startSpeed = entry != null
            ? entry.scrollMultiplier
            : (PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : GameConstants.Play.DefaultScrollMultiplier);
        speedSteps = ScrollSpeedRules.ToSteps(startSpeed);
        SetSpeedText(ScrollMultiplier);

        currentTab = entry != null ? entry.chartType : ChartType.Official;
        pendingChartId = entry != null ? entry.chartId : null;
        ShowTab(currentTab);

        if (songItems.Count == 0)
        {
            RefreshChosenChart();
            return;
        }

        Btn_SongItem firstItem = (entry != null ? songItems.Find(item => item.Song.SongId == entry.songId) : null) ?? songItems[0];
        OnSongChosen(firstItem.Song);
        pendingChartId = null;

        selectionKeeper.SetDefaultSelectable(firstItem.Button);
        FocusChosenSong();
        ScrollSongListToShow(firstItem);

        LogState("시작");
    }

    /// <summary>
    /// 키 안내 줄: ↑↓ 곡 · ←→ 난이도(나만의 채보 탭은 채보) · Tab 탭 · 1 2 배속 · Enter 시작 · ESC 타이틀.
    /// ↑↓ ←→는 한 액션으로 보이기 어려워 고정 글자, ESC는 UIManager(UI/Cancel)가 받아 고정 글자.
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
            new KeyGuideEntry(GameConstants.KeyGuide.Song, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.Difficulty, KeyGuideKey.Fixed(GameConstants.KeyGuide.LeftRightKeys)).WithId(GameConstants.KeyGuide.ChartMoveId),
            new KeyGuideEntry(GameConstants.KeyGuide.Tab, KeyGuideKey.FromAction(switchTabAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Speed, KeyGuideKey.FromAction(speedDownAction), KeyGuideKey.FromAction(speedUpAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Start, KeyGuideKey.FromAction(startAction, GameConstants.KeyGuide.EnterKey)),
            new KeyGuideEntry(GameConstants.KeyGuide.ToTitle, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

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
        isInputEnabled = false;
    }

    private void Subscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        if (reference == null)
        {
            Debug.LogWarning($"{LogPrefix} 연결되지 않은 입력 액션이 있습니다.", this);
            return;
        }

        reference.action.performed += handler;
        reference.action.Enable();
    }

    private static void Unsubscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        if (reference != null) reference.action.performed -= handler;
    }

    /// <summary>꾹 누르는 키(배속): 누른 순간(started)과 뗀 순간(canceled)을 구독한다</summary>
    private void SubscribeHold(InputActionReference reference, System.Action<InputAction.CallbackContext> onStarted, System.Action<InputAction.CallbackContext> onCanceled)
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

    private static void UnsubscribeHold(InputActionReference reference, System.Action<InputAction.CallbackContext> onStarted, System.Action<InputAction.CallbackContext> onCanceled)
    {
        if (reference == null) return;
        reference.action.started -= onStarted;
        reference.action.canceled -= onCanceled;
    }

    /// <summary>
    /// 키보드 선택을 고른 곡 항목에 둔다. 마우스로 탭·난이도·채보·시작을 눌러 선택이 옮겨 가면 곡 항목으로 되돌린다
    /// (↑↓가 계속 곡을 움직이게).
    /// </summary>
    private void Update()
    {
        UpdateSpeedRepeat();

        // 팝업이 열려 있으면 팝업 버튼의 선택을 빼앗지 않는다
        if (chosenItem == null || IsShortcutBlocked) return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null || selected == chosenItem.gameObject) return;
        if (selected.GetComponent<Btn_SongItem>() != null) return;

        eventSystem.SetSelectedGameObject(chosenItem.gameObject);
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

        ShowTab(tab);
        if (tab == ChartType.Custom && chosenMyChart != null) ScrollMyChartToShow(chosenMyChart);
        LogState("탭 전환");
    }

    /// <summary>
    /// 탭 버튼 모양, 패널, 조작 안내, 시작 버튼을 지금 탭에 맞춘다.
    /// </summary>
    private void ShowTab(ChartType tab)
    {
        currentTab = tab;
        bool isOfficial = tab == ChartType.Official;

        officialPanel.SetActive(isOfficial);
        myChartPanel.SetActive(!isOfficial);

        SetTabVisual(officialTabButton, officialTabText, isOfficial);
        SetTabVisual(myChartTabButton, myChartTabText, !isOfficial);

        if (keyGuideBar != null)
            keyGuideBar.SetLabel(GameConstants.KeyGuide.ChartMoveId, isOfficial ? GameConstants.KeyGuide.Difficulty : GameConstants.KeyGuide.Chart);
        RefreshChosenChart();
    }

    private void SetTabVisual(Button tabButton, TMP_Text label, bool chosen)
    {
        if (tabButton.targetGraphic != null)
            tabButton.targetGraphic.color = chosen ? chosenTabColor : normalTabColor;
        label.color = chosen ? chosenTabTextColor : normalTabTextColor;
    }

    // ───── 곡 ─────

    /// <summary>
    /// 난이도 버튼에 GameConstants.Chart.Difficulties 순서대로 난이도 이름을 정한다.
    /// </summary>
    private void InitDifficultyButtons()
    {
        string[] difficulties = GameConstants.Chart.Difficulties;
        if (difficultyButtons.Length != difficulties.Length)
            Debug.LogWarning($"{LogPrefix} 난이도 버튼 수({difficultyButtons.Length})와 난이도 수({difficulties.Length})가 다릅니다.", this);

        for (int i = 0; i < difficultyButtons.Length && i < difficulties.Length; i++)
            difficultyButtons[i].Init(difficulties[i]);
    }

    /// <summary>
    /// Content의 기존 자식을 비우고 곡마다 아이템을 생성한다.
    /// </summary>
    private void CreateSongItems(IReadOnlyList<SongData> songs)
    {
        for (int i = songListContent.childCount - 1; i >= 0; i--)
            Destroy(songListContent.GetChild(i).gameObject);

        songItems.Clear();

        foreach (SongData song in songs)
        {
            if (song == null) continue;

            Btn_SongItem item = Instantiate(songItemPrefab, songListContent);
            item.name = songItemPrefab.name;
            item.Init(song, OnSongChosen);
            songItems.Add(item);
        }
    }

    /// <summary>
    /// 곡 아이템끼리 위아래로만 이동하게 연결한다 (순환 없음). ←→는 난이도·채보 이동에 쓰므로 연결하지 않는다.
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
    /// 곡 아이템이 선택되면 호출된다(↑↓, 마우스). 곡 정보·난이도·나만의 채보 목록을 다시 채운다.
    /// 공식 탭은 그 곡에 있는 가장 낮은 난이도를, 나만의 채보 탭은 첫 채보를 고른다 (돌아온 경우는 그 채보).
    /// </summary>
    private void OnSongChosen(SongData song)
    {
        if (isLeaving) return;

        Btn_SongItem item = songItems.Find(view => view.Song == song);
        if (item == null || item == chosenItem) return;

        if (chosenItem != null) chosenItem.SetChosen(false);
        item.SetChosen(true);
        chosenItem = item;
        chosenSong = song;
        selectionKeeper.SetDefaultSelectable(item.Button);

        ShowSongInfo(song);
        RefreshDifficulties(currentTab == ChartType.Official ? pendingChartId : null);
        RefreshMyCharts(currentTab == ChartType.Custom ? pendingChartId : null);
        RefreshChosenChart();
        ScrollSongListToShow(item);

        if (pendingChartId == null) LogState("곡 변경");
    }

    private void ShowSongInfo(SongData song)
    {
        coverImage.sprite = song.Cover;
        coverImage.color = song.Cover != null ? Color.white : emptyCoverColor;
        songTitleText.text = song.Title;
        artistText.text = string.IsNullOrEmpty(song.Artist) ? GameConstants.SongSelect.EmptyValue : song.Artist;

        string bpmText = song.Bpm > 0f ? SongData.FormatBpm(song.Bpm) : GameConstants.SongSelect.EmptyValue;
        songInfoText.text = string.Format(GameConstants.SongSelect.SongInfoFormat, bpmText, song.GetLengthText());
    }

    private void FocusChosenSong()
    {
        if (EventSystem.current != null && chosenItem != null)
            EventSystem.current.SetSelectedGameObject(chosenItem.gameObject);
    }

    // ───── 공식 채보 (난이도) ─────

    /// <summary>
    /// 고른 곡의 공식 채보로 난이도 버튼을 채우고 난이도를 고른다. 하나도 없으면 빈 상태 문구를 켠다.
    /// </summary>
    /// <param name="preferredChartId">이 채보가 있으면 고른다. 없으면 가장 낮은 난이도</param>
    private void RefreshDifficulties(string preferredChartId)
    {
        List<ChartData> charts = ChartRepository.GetCharts(chosenSong.SongId, ChartType.Official);

        foreach (Btn_Difficulty button in difficultyButtons)
            button.SetChart(charts.Find(chart => chart.difficulty == button.Difficulty));

        officialEmptyText.gameObject.SetActive(charts.Count == 0);

        Btn_Difficulty preferred = null;
        if (!string.IsNullOrEmpty(preferredChartId))
            preferred = System.Array.Find(difficultyButtons, button => button.Chart != null && button.Chart.chartId == preferredChartId);

        // 난이도 버튼은 낮은 난이도부터 놓여 있으므로 처음 만나는 채보 있는 버튼이 가장 낮은 난이도다
        chosenDifficulty = null;
        SetChosenDifficulty(preferred ?? System.Array.Find(difficultyButtons, button => button.Chart != null));
    }

    private void SetChosenDifficulty(Btn_Difficulty target)
    {
        if (chosenDifficulty != null) chosenDifficulty.SetChosen(false);
        chosenDifficulty = target;
        if (chosenDifficulty != null) chosenDifficulty.SetChosen(true);
        RefreshChosenChart();
    }

    /// <summary>
    /// 난이도 버튼 클릭. 채보가 없는 난이도는 버튼이 비활성이라 불리지 않는다.
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
        int current = System.Array.IndexOf(difficultyButtons, chosenDifficulty);
        if (current < 0) return;

        for (int i = current + direction; i >= 0 && i < difficultyButtons.Length; i += direction)
        {
            if (difficultyButtons[i].Chart == null) continue;

            SetChosenDifficulty(difficultyButtons[i]);
            LogState("난이도 이동");
            return;
        }
    }

    // ───── 나만의 채보 ─────

    /// <summary>
    /// 고른 곡의 나만의 채보로 목록을 채우고 채보를 고른다 (chartId 내림차순, 만든 시각이 늦은 것이 위).
    /// 항목은 풀에서 앞에서부터 다시 쓰고, 모자랄 때만 새로 만든다.
    /// </summary>
    /// <param name="preferredChartId">이 채보가 있으면 고른다. 없으면 첫 채보</param>
    private void RefreshMyCharts(string preferredChartId)
    {
        List<ChartData> charts = ChartRepository.GetCharts(chosenSong.SongId, ChartType.Custom);
        charts.Sort((a, b) => string.CompareOrdinal(b.chartId, a.chartId));

        activeMyChartItems.Clear();
        for (int i = 0; i < charts.Count; i++)
        {
            if (i >= myChartItemPool.Count)
                myChartItemPool.Add(CreateMyChartItem());

            Btn_MyChartItem item = myChartItemPool[i];
            item.gameObject.SetActive(true);
            item.Init(charts[i], ChooseMyChart);
            activeMyChartItems.Add(item);
        }

        for (int i = charts.Count; i < myChartItemPool.Count; i++)
            myChartItemPool[i].gameObject.SetActive(false);

        myChartEmptyText.gameObject.SetActive(charts.Count == 0);
        myChartScrollRect.verticalNormalizedPosition = 1f;

        Btn_MyChartItem preferred = string.IsNullOrEmpty(preferredChartId)
            ? null
            : activeMyChartItems.Find(item => item.Chart.chartId == preferredChartId);

        chosenMyChart = null;
        SetChosenMyChart(preferred ?? (activeMyChartItems.Count > 0 ? activeMyChartItems[0] : null));
        if (chosenMyChart != null) ScrollMyChartToShow(chosenMyChart);
    }

    /// <summary>
    /// 나만의 채보 항목을 만든다. 키보드 선택이 곡 목록을 떠나지 않도록 마우스 올림 선택(UISelectOnHover)과 내비게이션을 끈다.
    /// </summary>
    private Btn_MyChartItem CreateMyChartItem()
    {
        Btn_MyChartItem created = Instantiate(myChartItemPrefab, myChartContent);
        created.name = myChartItemPrefab.name;

        UISelectOnHover selectOnHover = created.GetComponent<UISelectOnHover>();
        if (selectOnHover != null) selectOnHover.enabled = false;
        created.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
        return created;
    }

    private void SetChosenMyChart(Btn_MyChartItem target)
    {
        if (chosenMyChart != null) chosenMyChart.SetChosen(false);
        chosenMyChart = target;
        if (chosenMyChart != null) chosenMyChart.SetChosen(true);
        RefreshChosenChart();
    }

    /// <summary>
    /// 나만의 채보 항목 클릭.
    /// </summary>
    private void ChooseMyChart(ChartData chart)
    {
        if (isLeaving) return;

        Btn_MyChartItem item = activeMyChartItems.Find(view => view.Chart == chart);
        if (item == null) return;

        SetChosenMyChart(item);
        LogState("채보 선택");
    }

    /// <summary>
    /// ←→ (나만의 채보 탭): 목록에서 위(←)·아래(→) 채보로 옮긴다. 끝에서는 멈춘다.
    /// </summary>
    private void MoveMyChart(int direction)
    {
        int next = activeMyChartItems.IndexOf(chosenMyChart) + direction;
        if (chosenMyChart == null || next < 0 || next >= activeMyChartItems.Count) return;

        SetChosenMyChart(activeMyChartItems[next]);
        ScrollMyChartToShow(chosenMyChart);
        LogState("채보 이동");
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
        else MoveMyChart(direction);
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
        speedText.text = string.Format(GameConstants.SongSelect.SpeedFormat, multiplier);
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

    /// <summary>고른 채보가 바뀌었을 때: 시작 버튼과 최고 기록 칸을 다시 맞춘다</summary>
    private void RefreshChosenChart()
    {
        RefreshStartButton();
        ShowRecord(ChosenChart);
    }

    private void RefreshStartButton()
    {
        startButton.interactable = !isLeaving && ChosenChart != null;
    }

    /// <summary>
    /// 고른 채보로 인게임을 시작한다 (Enter, 시작 버튼). 고른 채보가 없으면 아무것도 하지 않는다.
    /// </summary>
    private void StartGame()
    {
        ChartData chart = ChosenChart;
        if (isLeaving || chosenSong == null || chart == null) return;

        InGameEntryData data = new InGameEntryData
        {
            songId = chosenSong.SongId,
            chartId = chart.chartId,
            chartType = currentTab,
            mode = InGameMode.Normal,
            scrollMultiplier = ScrollMultiplier
        };

        LogState("시작");
        if (GameManager.Scene.Load(GameConstants.Scenes.InGame, data))
        {
            SetLeaving();
            // 곡 선택에서 시작하는 것은 항상 일반 플레이다 (값이 같으면 파일을 쓰지 않는다)
            if (PlayerDataManager.HasInstance) GameManager.PlayerData.SetLastScrollMultiplier(data.scrollMultiplier);
        }
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
        foreach (Btn_MyChartItem item in activeMyChartItems) item.Button.interactable = false;
    }

    // ───── 스크롤 ─────

    private void ScrollSongListToShow(Btn_SongItem item)
    {
        ScrollToShow(songListScrollRect, songListContent, (RectTransform)item.transform);
    }

    private void ScrollMyChartToShow(Btn_MyChartItem item)
    {
        if (!myChartPanel.activeInHierarchy) return;
        ScrollToShow(myChartScrollRect, myChartContent, (RectTransform)item.transform);
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

    // ───── 표시 ─────

    /// <summary>
    /// 최고 기록 칸: 고른 채보(공식 난이도 / 나만의 채보)의 기록을 GameManager.PlayerData에서 찾아 최고 점수(천 단위 쉼표)·랭크·최대 콤보·FC/AP를 보여준다.
    /// 채보가 없거나 기록이 없으면 모두 "-".
    /// </summary>
    private void ShowRecord(ChartData chart)
    {
        ChartRecordData record = chart != null && PlayerDataManager.HasInstance ? GameManager.PlayerData.FindRecord(chart.chartId) : null;
        string empty = GameConstants.SongSelect.EmptyValue;
        if (record == null)
        {
            bestScoreText.text = empty;
            bestRankText.text = string.Format(GameConstants.SongSelect.RecordRankFormat, empty);
            bestComboText.text = string.Format(GameConstants.SongSelect.RecordComboFormat, empty);
            badgeText.text = empty;
            return;
        }

        bestScoreText.text = string.Format(CultureInfo.InvariantCulture, GameConstants.SongSelect.RecordScoreFormat, record.bestScore);
        bestRankText.text = string.Format(GameConstants.SongSelect.RecordRankFormat, PlayerDataRules.ParseRank(record.bestRank));
        bestComboText.text = string.Format(GameConstants.SongSelect.RecordComboFormat, record.maxCombo);
        badgeText.text = record.isAllPerfect ? GameConstants.Play.AllPerfectText
            : record.isFullCombo ? GameConstants.Play.FullComboText
            : empty;
    }

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
