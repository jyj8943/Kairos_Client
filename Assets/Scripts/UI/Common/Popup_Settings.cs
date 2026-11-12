using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 어디서든 여는 공용 설정 창 (기획서 3-7). UIManager 오버레이 Canvas 아래에 두고, UIManager가 Init()을 호출한다. 여는 쪽은 GameManager.UI.OpenSettings().
/// - 사운드 탭: 슬라이더(0~100)를 바꾸는 즉시 SoundManager의 MasterVolume·MusicVolume·SfxVolume(0~1)에 넣는다. 열 때 SoundManager의 지금 값으로 맞춘다.
/// - 화면 탭: 창 모드·해상도 드롭다운을 바꾸는 즉시 ApplyScreen()으로 Screen.SetResolution을 부른다. 열 때 지금 화면(Screen.width·height·fullScreenMode)으로 맞춘다.
///   창 모드에서는 모니터보다 큰 해상도를 목록에서 뺀다. 에디터 Game 뷰에서는 SetResolution이 적용되지 않아 로그만 남는다 (실제 확인은 빌드).
/// - 값은 SettingsViewState(임시)에 모아 둔다.
///   TODO(설정 기능): 판정 오프셋 반영, 기본 배속 적용, 오프셋 측정.
///   TODO(설정 저장): settings.json 저장·불러오기.
/// - 탭 3개(사운드·게임·화면): 고른 탭은 검정 채움·흰 글자, 나머지는 흰 채움·테두리·검정 글자. 고른 탭의 내용만 보인다. 마지막 탭은 이 실행 동안 기억한다.
/// - 키보드는 EventSystem(UI 맵)의 Navigate·Submit을 그대로 쓴다.
///   ↑↓: 탭 줄 → 탭 안 항목 → (마지막에서 ↓) 탭 줄. 탭 줄에서 ↑는 닫기 버튼. Selectable Navigation을 Explicit으로 연결한다.
///   ←→: 탭 줄은 탭 사이 이동, 슬라이더는 기본 동작(값), 오프셋·기본 배속 줄은 UINavigateStepper로 ◀▶와 같게 값을 바꾼다.
///   Tab 키: 다음 탭 (입력 에셋 Settings/NextTab. 열 때 구독하고 닫을 때·OnDestroy에서 끊는다).
///   ESC: UIManager → OnCancelRequested() → 닫기 (자동 저장은 기능 작업 때).
/// - 열 때와 탭을 바꿀 때 선택은 지금 탭 버튼에 둔다. 바깥(어두운 배경) 클릭으로는 닫히지 않는다.
/// - 일시정지 메뉴 위에서 열면 닫을 때 일시정지 메뉴의 선택으로 돌아간다 (UIPopup이 이전 선택을 되돌린다).
/// </summary>
public class Popup_Settings : UIPopup
{
    [Header("위")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeText;

    [Header("탭")]
    [SerializeField] private Button soundTabButton;
    [SerializeField] private TMP_Text soundTabText;
    [SerializeField] private Button gameTabButton;
    [SerializeField] private TMP_Text gameTabText;
    [SerializeField] private Button screenTabButton;
    [SerializeField] private TMP_Text screenTabText;
    [Tooltip("탭 내용 (SoundTab · GameTab · ScreenTab, 정렬용 빈 오브젝트)")]
    [SerializeField] private GameObject soundTab;
    [SerializeField] private GameObject gameTab;
    [SerializeField] private GameObject screenTab;
    [SerializeField] private Color tabSelectedColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color tabSelectedTextColor = Color.white;
    [SerializeField] private Color tabNormalColor = Color.white;
    [SerializeField] private Color tabNormalTextColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

    [Header("사운드 탭")]
    [SerializeField] private TMP_Text masterVolumeLabel;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private TMP_Text musicVolumeLabel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text musicVolumeValueText;
    [SerializeField] private TMP_Text sfxVolumeLabel;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeValueText;

    [Header("게임 탭")]
    [SerializeField] private TMP_Text offsetLabel;
    [Tooltip("판정 오프셋 줄 (Btn_Offset). ←→로 값을 바꾸는 선택 대상")]
    [SerializeField] private Button offsetRow;
    [SerializeField] private Button offsetDownButton;
    [SerializeField] private TMP_Text offsetText;
    [SerializeField] private Button offsetUpButton;
    [SerializeField] private TMP_Text offsetRangeText;
    [SerializeField] private Button measureOffsetButton;
    [SerializeField] private TMP_Text measureOffsetText;
    [SerializeField] private TMP_Text defaultSpeedLabel;
    [Tooltip("기본 배속 줄 (Btn_DefaultSpeed). ←→로 값을 바꾸는 선택 대상")]
    [SerializeField] private Button defaultSpeedRow;
    [SerializeField] private Button defaultSpeedDownButton;
    [SerializeField] private TMP_Text defaultSpeedText;
    [SerializeField] private Button defaultSpeedUpButton;

    [Header("화면 탭")]
    [SerializeField] private TMP_Text resolutionLabel;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Text windowModeLabel;
    [SerializeField] private TMP_Dropdown windowModeDropdown;

    [Header("아래")]
    [SerializeField] private TMP_Text hintText;

    [Header("입력")]
    [Tooltip("입력 에셋의 Settings/NextTab (Tab 키)")]
    [SerializeField] private InputActionReference nextTabAction;

    /// <summary>보여주는 값 (임시). TODO(설정 저장): settings.json과 같은 설정 데이터 클래스로 바꾼다</summary>
    private readonly SettingsViewState state = new SettingsViewState();

    /// <summary>창 모드 드롭다운에 넣은 항목 (드롭다운 번호 = 이 목록의 번호)</summary>
    private readonly List<FullScreenMode> windowModeOptions = new List<FullScreenMode>();

    /// <summary>해상도 드롭다운에 넣은 항목 (드롭다운 번호 = 이 목록의 번호). 창 모드에 따라 다시 만든다</summary>
    private readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();

    private UINavigateStepper offsetStepper;
    private UINavigateStepper defaultSpeedStepper;
    private bool isTabInputEnabled;

    /// <summary>지금 탭 (확인용)</summary>
    public SettingsTab CurrentTab => state.tab;

    /// <summary>보여주는 값 (확인용)</summary>
    public SettingsViewState State => state;

    /// <summary>지금 창 모드 드롭다운 항목 (확인용)</summary>
    public IReadOnlyList<FullScreenMode> WindowModeOptions => windowModeOptions;

    /// <summary>지금 해상도 드롭다운 항목 (확인용)</summary>
    public IReadOnlyList<Vector2Int> ResolutionOptions => resolutionOptions;

    /// <summary>메인 음량 슬라이더 (확인용)</summary>
    public Slider MasterVolumeSlider => masterVolumeSlider;

    /// <summary>음악 음량 슬라이더 (확인용)</summary>
    public Slider MusicVolumeSlider => musicVolumeSlider;

    /// <summary>효과음 음량 슬라이더 (확인용)</summary>
    public Slider SfxVolumeSlider => sfxVolumeSlider;

    /// <summary>해상도 드롭다운 (확인용)</summary>
    public TMP_Dropdown ResolutionDropdown => resolutionDropdown;

    /// <summary>창 모드 드롭다운 (확인용)</summary>
    public TMP_Dropdown WindowModeDropdown => windowModeDropdown;

    /// <summary>
    /// 문구, 버튼·슬라이더·드롭다운 리스너, 처음 값을 채우고 닫힌 상태로 둔다.
    /// </summary>
    public void Init()
    {
        titleText.text = GameConstants.Settings.Title;
        closeText.text = GameConstants.Settings.CloseLabel;
        soundTabText.text = GameConstants.Settings.TabSound;
        gameTabText.text = GameConstants.Settings.TabGame;
        screenTabText.text = GameConstants.Settings.TabScreen;
        masterVolumeLabel.text = GameConstants.Settings.MasterVolumeLabel;
        musicVolumeLabel.text = GameConstants.Settings.MusicVolumeLabel;
        sfxVolumeLabel.text = GameConstants.Settings.SfxVolumeLabel;
        offsetLabel.text = GameConstants.Settings.OffsetLabel;
        offsetRangeText.text = GameConstants.Settings.OffsetRangeHint;
        measureOffsetText.text = GameConstants.Settings.MeasureOffsetLabel;
        defaultSpeedLabel.text = GameConstants.Settings.DefaultSpeedLabel;
        resolutionLabel.text = GameConstants.Settings.ResolutionLabel;
        windowModeLabel.text = GameConstants.Settings.WindowModeLabel;
        hintText.text = GameConstants.Settings.HintText;

        RemoveListeners();
        closeButton.onClick.AddListener(Close);
        soundTabButton.onClick.AddListener(OnClickSoundTab);
        gameTabButton.onClick.AddListener(OnClickGameTab);
        screenTabButton.onClick.AddListener(OnClickScreenTab);

        foreach (Slider slider in new[] { masterVolumeSlider, musicVolumeSlider, sfxVolumeSlider })
        {
            slider.minValue = GameConstants.Settings.MinVolume;
            slider.maxValue = GameConstants.Settings.MaxVolume;
            slider.wholeNumbers = true;
        }
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        offsetDownButton.onClick.AddListener(OnClickOffsetDown);
        offsetUpButton.onClick.AddListener(OnClickOffsetUp);
        defaultSpeedDownButton.onClick.AddListener(OnClickDefaultSpeedDown);
        defaultSpeedUpButton.onClick.AddListener(OnClickDefaultSpeedUp);
        measureOffsetButton.onClick.AddListener(OnClickMeasureOffset);

        if (offsetStepper == null) offsetStepper = offsetRow.GetComponent<UINavigateStepper>();
        if (defaultSpeedStepper == null) defaultSpeedStepper = defaultSpeedRow.GetComponent<UINavigateStepper>();
        if (offsetStepper != null) offsetStepper.Stepped += StepOffset;
        if (defaultSpeedStepper != null) defaultSpeedStepper.Stepped += StepDefaultSpeed;

        FillWindowModeDropdown();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);

        // 처음 값: 음량은 SoundManager, 해상도·창 모드는 지금 화면, 오프셋 0 · 배속 1.0
        // TODO(설정 저장): settings.json에서 읽은 값으로 채운다
        SyncFromCurrent();
        RefreshAll();

        // 오프셋·배속 줄 안의 ◀▶는 마우스 전용 (키보드는 줄에서 ←→)
        foreach (Button button in new[] { offsetDownButton, offsetUpButton, defaultSpeedDownButton, defaultSpeedUpButton })
            button.navigation = new Navigation { mode = Navigation.Mode.None };

        HideImmediate();
    }

    /// <summary>
    /// 연다 (UIManager.OpenSettings). 마지막으로 연 탭을 보여주고 그 탭 버튼을 선택한다.
    /// </summary>
    public void Show()
    {
        if (IsOpen) return;

        // 열 때마다 지금 음량·화면으로 다시 맞춘다 (다른 곳에서 바뀌었을 수 있다)
        SyncFromCurrent();
        RefreshAll();
        ShowTab(state.tab, false);
        Open();
        SelectTabButton();
        Debug.Log($"[Popup_Settings] 열림: 탭 {state.tab}", this);
    }

    /// <summary>ESC = 닫기. TODO(설정 저장): 닫을 때 settings.json에 자동 저장</summary>
    public override void OnCancelRequested()
    {
        Close();
    }

    protected override void OnOpened()
    {
        EnableTabInput();
    }

    protected override void OnClosed()
    {
        DisableTabInput();
        Debug.Log("[Popup_Settings] 닫힘", this);
    }

    private void OnDestroy()
    {
        DisableTabInput();
        RemoveListeners();
    }

    // ───── 탭 ─────

    private void OnClickSoundTab() => ShowTab(SettingsTab.Sound, true);
    private void OnClickGameTab() => ShowTab(SettingsTab.Game, true);
    private void OnClickScreenTab() => ShowTab(SettingsTab.Screen, true);

    /// <summary>다음 탭으로 (Tab 키). 사운드 → 게임 → 화면 → 사운드</summary>
    public void ShowNextTab()
    {
        ShowTab((SettingsTab)(((int)state.tab + 1) % 3), true);
    }

    /// <summary>
    /// 탭을 바꾼다: 고른 탭 내용만 켜고, 탭 버튼 모양과 ↑↓ 연결을 다시 맞춘다.
    /// </summary>
    /// <param name="selectTabButton">true면 선택을 지금 탭 버튼에 둔다 (탭 안 첫 항목이 아니라)</param>
    private void ShowTab(SettingsTab tab, bool selectTabButton)
    {
        state.tab = tab;
        soundTab.SetActive(tab == SettingsTab.Sound);
        gameTab.SetActive(tab == SettingsTab.Game);
        screenTab.SetActive(tab == SettingsTab.Screen);

        ApplyTabStyle(soundTabButton, soundTabText, tab == SettingsTab.Sound);
        ApplyTabStyle(gameTabButton, gameTabText, tab == SettingsTab.Game);
        ApplyTabStyle(screenTabButton, screenTabText, tab == SettingsTab.Screen);
        LinkNavigation();

        if (selectTabButton) SelectTabButton();
    }

    private void ApplyTabStyle(Button button, TMP_Text text, bool selected)
    {
        button.image.color = selected ? tabSelectedColor : tabNormalColor;
        text.color = selected ? tabSelectedTextColor : tabNormalTextColor;
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = !selected;
    }

    private Button CurrentTabButton =>
        state.tab == SettingsTab.Sound ? soundTabButton : (state.tab == SettingsTab.Game ? gameTabButton : screenTabButton);

    private void SelectTabButton()
    {
        if (EventSystem.current != null && IsOpen) EventSystem.current.SetSelectedGameObject(CurrentTabButton.gameObject);
    }

    /// <summary>지금 탭 안의 선택 항목 (↑↓ 순서)</summary>
    public List<Selectable> GetTabItems(SettingsTab tab)
    {
        switch (tab)
        {
            case SettingsTab.Sound: return new List<Selectable> { masterVolumeSlider, musicVolumeSlider, sfxVolumeSlider };
            case SettingsTab.Game: return new List<Selectable> { offsetRow, measureOffsetButton, defaultSpeedRow };
            default: return new List<Selectable> { resolutionDropdown, windowModeDropdown };
        }
    }

    /// <summary>
    /// ↑↓·←→ 연결: 탭 줄 ←→ 탭끼리, ↑ 닫기, ↓ 지금 탭 첫 항목. 탭 안은 위아래로 잇고 마지막에서 ↓, 처음에서 ↑는 지금 탭 버튼.
    /// 항목의 ←→는 비워 둔다 (슬라이더는 값, 오프셋·배속 줄은 UINavigateStepper가 값을 바꾼다).
    /// </summary>
    private void LinkNavigation()
    {
        List<Selectable> items = GetTabItems(state.tab);
        Button current = CurrentTabButton;

        soundTabButton.navigation = TabNavigation(null, gameTabButton, items[0]);
        gameTabButton.navigation = TabNavigation(soundTabButton, screenTabButton, items[0]);
        screenTabButton.navigation = TabNavigation(gameTabButton, null, items[0]);
        closeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = current, selectOnLeft = screenTabButton };

        for (int i = 0; i < items.Count; i++)
        {
            items[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? current : items[i - 1],
                selectOnDown = i == items.Count - 1 ? current : items[i + 1]
            };
        }
    }

    private Navigation TabNavigation(Selectable left, Selectable right, Selectable down)
    {
        return new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = left,
            selectOnRight = right != null ? right : (Selectable)closeButton,
            selectOnUp = closeButton,
            selectOnDown = down
        };
    }

    // ───── 값 ─────

    /// <summary>메인 음량 슬라이더 → SoundManager.MasterVolume (모든 소리에 곱해진다)</summary>
    private void OnMasterVolumeChanged(float value)
    {
        state.masterVolume = Mathf.RoundToInt(value);
        if (SoundManager.HasInstance) GameManager.Sound.MasterVolume = ToVolume01(state.masterVolume);
        RefreshVolumes();
    }

    /// <summary>음악 음량 슬라이더 → SoundManager.MusicVolume (노래·BGM)</summary>
    private void OnMusicVolumeChanged(float value)
    {
        state.musicVolume = Mathf.RoundToInt(value);
        if (SoundManager.HasInstance) GameManager.Sound.MusicVolume = ToVolume01(state.musicVolume);
        RefreshVolumes();
    }

    /// <summary>효과음 음량 슬라이더 → SoundManager.SfxVolume (키음·효과음)</summary>
    private void OnSfxVolumeChanged(float value)
    {
        state.sfxVolume = Mathf.RoundToInt(value);
        if (SoundManager.HasInstance) GameManager.Sound.SfxVolume = ToVolume01(state.sfxVolume);
        RefreshVolumes();
    }

    /// <summary>슬라이더 값(0~100) → SoundManager 음량(0~1)</summary>
    public static float ToVolume01(int sliderValue)
    {
        return Mathf.Clamp01(sliderValue / (float)GameConstants.Settings.MaxVolume);
    }

    /// <summary>SoundManager 음량(0~1) → 슬라이더 값(0~100, 반올림)</summary>
    public static int ToSliderValue(float volume01)
    {
        return Mathf.Clamp(Mathf.RoundToInt(volume01 * GameConstants.Settings.MaxVolume),
            GameConstants.Settings.MinVolume, GameConstants.Settings.MaxVolume);
    }

    private void OnClickOffsetDown() => StepOffset(-1);
    private void OnClickOffsetUp() => StepOffset(1);
    private void OnClickDefaultSpeedDown() => StepDefaultSpeed(-1);
    private void OnClickDefaultSpeedUp() => StepDefaultSpeed(1);

    /// <summary>판정 오프셋을 5ms씩 바꾼다 (-200 ~ +200). TODO(설정 기능): 판정 시간에 반영</summary>
    private void StepOffset(int direction)
    {
        state.offsetMs = Mathf.Clamp(state.offsetMs + direction * GameConstants.Settings.OffsetStepMs,
            GameConstants.Settings.MinOffsetMs, GameConstants.Settings.MaxOffsetMs);
        RefreshOffset();
    }

    /// <summary>기본 배속을 0.1씩 바꾼다 (1.0 ~ 8.0, 공용 배속 단계). TODO(설정 기능): 곡 선택·에디터의 처음 배속으로 쓴다</summary>
    private void StepDefaultSpeed(int direction)
    {
        state.defaultSpeedSteps = ScrollSpeedRules.Step(state.defaultSpeedSteps, direction);
        RefreshDefaultSpeed();
    }

    /// <summary>TODO(설정 기능): 오프셋 측정 창을 연다. 지금은 눌린 표시만 된다</summary>
    private void OnClickMeasureOffset()
    {
        Debug.Log("[Popup_Settings] 오프셋 측정하기 (아직 기능 없음)", this);
    }

    /// <summary>해상도 드롭다운 → 고른 해상도를 지금 창 모드로 바로 적용한다</summary>
    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutionOptions.Count) return;
        state.resolution = resolutionOptions[index];
        ApplyScreen();
    }

    /// <summary>
    /// 창 모드 드롭다운 → 그 모드에 맞게 해상도 목록을 다시 만들고(창 모드면 모니터보다 큰 항목 뺌) 바로 적용한다.
    /// 고른 해상도가 새 목록에 없으면 기본값, 그것도 없으면 목록의 가장 큰 항목으로 바꾼다.
    /// </summary>
    private void OnWindowModeChanged(int index)
    {
        if (index < 0 || index >= windowModeOptions.Count) return;
        state.windowMode = windowModeOptions[index];
        RebuildResolutionOptions(state.windowMode);
        state.resolution = PickResolution(resolutionOptions, state.resolution);
        resolutionDropdown.SetValueWithoutNotify(resolutionOptions.IndexOf(state.resolution));
        ApplyScreen();
    }

    /// <summary>
    /// 고른 해상도·창 모드를 화면에 적용한다 (두 드롭다운이 함께 쓰는 한 곳). 요청 값과 다음 프레임의 실제 값을 로그로 남긴다.
    /// TODO(설정 저장): 적용한 해상도·창 모드를 settings.json에 저장한다.
    /// </summary>
    public void ApplyScreen()
    {
        Vector2Int size = state.resolution;
        FullScreenMode mode = state.windowMode;
        Screen.SetResolution(size.x, size.y, mode);
        Debug.Log($"[Popup_Settings] 화면 적용 요청: {size.x}×{size.y} {mode}", this);

        if (isActiveAndEnabled) StartCoroutine(LogScreenNextFrame(size, mode));
    }

    /// <summary>적용 뒤 다음 프레임의 실제 화면 값을 로그로 남긴다</summary>
    private IEnumerator LogScreenNextFrame(Vector2Int size, FullScreenMode mode)
    {
        yield return null;
        Debug.Log($"[Popup_Settings] 화면 적용 결과 (다음 프레임): 요청 {size.x}×{size.y} {mode} → 지금 {Screen.width}×{Screen.height} {Screen.fullScreenMode}", this);
    }

    /// <summary>오프셋 표시: 양수 "+15 ms", 0 "0 ms", 음수 "-15 ms"</summary>
    public static string FormatOffset(int offsetMs)
    {
        if (offsetMs > 0) return string.Format(GameConstants.Settings.OffsetPositiveFormat, offsetMs);
        if (offsetMs < 0) return string.Format(GameConstants.Settings.OffsetNegativeFormat, -offsetMs);
        return GameConstants.Settings.OffsetZeroText;
    }

    private void RefreshAll()
    {
        masterVolumeSlider.SetValueWithoutNotify(state.masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(state.musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(state.sfxVolume);
        windowModeDropdown.SetValueWithoutNotify(windowModeOptions.IndexOf(state.windowMode));
        resolutionDropdown.SetValueWithoutNotify(resolutionOptions.IndexOf(state.resolution));
        RefreshVolumes();
        RefreshOffset();
        RefreshDefaultSpeed();
    }

    private void RefreshVolumes()
    {
        masterVolumeValueText.SetText("{0}", state.masterVolume);
        musicVolumeValueText.SetText("{0}", state.musicVolume);
        sfxVolumeValueText.SetText("{0}", state.sfxVolume);
    }

    private void RefreshOffset()
    {
        offsetText.text = FormatOffset(state.offsetMs);
    }

    private void RefreshDefaultSpeed()
    {
        defaultSpeedText.text = string.Format(GameConstants.Settings.DefaultSpeedFormat, ScrollSpeedRules.FromSteps(state.defaultSpeedSteps));
    }

    /// <summary>
    /// 지금 값으로 맞춘다: 음량은 SoundManager(0~1 → 0~100 반올림), 창 모드·해상도는 지금 화면.
    /// 창 모드가 목록에 없으면(MaximizedWindow 등) 목록의 첫 항목, 해상도가 목록에 없으면 기본값 → 가장 큰 항목.
    /// </summary>
    private void SyncFromCurrent()
    {
        if (SoundManager.HasInstance)
        {
            state.masterVolume = ToSliderValue(GameManager.Sound.MasterVolume);
            state.musicVolume = ToSliderValue(GameManager.Sound.MusicVolume);
            state.sfxVolume = ToSliderValue(GameManager.Sound.SfxVolume);
        }

        FullScreenMode current = Screen.fullScreenMode;
        state.windowMode = windowModeOptions.Contains(current) ? current : windowModeOptions[0];
        RebuildResolutionOptions(state.windowMode);
        state.resolution = PickResolution(resolutionOptions, new Vector2Int(Screen.width, Screen.height));
    }

    /// <summary>창 모드 드롭다운을 GameConstants.Settings.WindowModes로 채운다 (한 번만)</summary>
    private void FillWindowModeDropdown()
    {
        windowModeOptions.Clear();
        windowModeOptions.AddRange(GameConstants.Settings.WindowModes);
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string>(GameConstants.Settings.WindowModeLabels));
    }

    /// <summary>해상도 목록을 창 모드에 맞게 다시 만들고 드롭다운을 채운다</summary>
    private void RebuildResolutionOptions(FullScreenMode mode)
    {
        resolutionOptions.Clear();
        resolutionOptions.AddRange(BuildResolutionOptions(mode, GetMonitorSize()));

        resolutionDropdown.ClearOptions();
        List<string> labels = new List<string>();
        foreach (Vector2Int size in resolutionOptions)
            labels.Add(string.Format(GameConstants.Settings.ResolutionFormat, size.x, size.y));
        resolutionDropdown.AddOptions(labels);
    }

    /// <summary>
    /// 창 모드에 맞는 해상도 목록. 창 모드(Windowed)면 모니터보다 큰 항목을 뺀다 (모두 빠지면 가장 작은 항목 하나는 남긴다).
    /// </summary>
    public static List<Vector2Int> BuildResolutionOptions(FullScreenMode mode, Vector2Int monitor)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Vector2Int size in GameConstants.Settings.Resolutions)
        {
            bool fits = size.x <= monitor.x && size.y <= monitor.y;
            if (mode != FullScreenMode.Windowed || fits) list.Add(size);
        }

        if (list.Count == 0)
        {
            Vector2Int smallest = GameConstants.Settings.Resolutions[0];
            foreach (Vector2Int size in GameConstants.Settings.Resolutions)
            {
                if (size.x * size.y < smallest.x * smallest.y) smallest = size;
            }
            list.Add(smallest);
        }
        return list;
    }

    /// <summary>목록에서 고른다: 원하는 크기가 있으면 그것, 없으면 기본값(DefaultResolution), 그것도 없으면 가장 큰 항목</summary>
    public static Vector2Int PickResolution(List<Vector2Int> options, Vector2Int wanted)
    {
        if (options.Contains(wanted)) return wanted;
        if (options.Contains(GameConstants.Settings.DefaultResolution)) return GameConstants.Settings.DefaultResolution;

        Vector2Int largest = options[0];
        foreach (Vector2Int size in options)
        {
            if (size.x * size.y > largest.x * largest.y) largest = size;
        }
        return largest;
    }

    /// <summary>게임 창이 있는 모니터의 해상도. 못 읽으면 Display.main의 systemWidth·systemHeight</summary>
    public static Vector2Int GetMonitorSize()
    {
        DisplayInfo info = Screen.mainWindowDisplayInfo;
        if (info.width > 0 && info.height > 0) return new Vector2Int(info.width, info.height);
        return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
    }

    // ───── Tab 키 ─────

    private void EnableTabInput()
    {
        if (isTabInputEnabled || nextTabAction == null) return;
        nextTabAction.action.performed += OnNextTab;
        nextTabAction.action.Enable();
        isTabInputEnabled = true;
    }

    /// <summary>액션은 여러 곳이 같이 쓰므로 Disable하지 않고 구독만 끊는다.</summary>
    private void DisableTabInput()
    {
        if (!isTabInputEnabled) return;
        nextTabAction.action.performed -= OnNextTab;
        isTabInputEnabled = false;
    }

    private void OnNextTab(InputAction.CallbackContext context)
    {
        // 맨 위 팝업일 때만 (위에 확인 팝업이 열리면 받지 않는다). 드롭다운 목록이 열려 있으면 무시한다
        if (!IsOpen || (UIManager.HasInstance && GameManager.UI.TopPopup != this)) return;
        if (resolutionDropdown.IsExpanded || windowModeDropdown.IsExpanded) return;
        ShowNextTab();
    }

    private void RemoveListeners()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (soundTabButton != null) soundTabButton.onClick.RemoveListener(OnClickSoundTab);
        if (gameTabButton != null) gameTabButton.onClick.RemoveListener(OnClickGameTab);
        if (screenTabButton != null) screenTabButton.onClick.RemoveListener(OnClickScreenTab);
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (offsetDownButton != null) offsetDownButton.onClick.RemoveListener(OnClickOffsetDown);
        if (offsetUpButton != null) offsetUpButton.onClick.RemoveListener(OnClickOffsetUp);
        if (defaultSpeedDownButton != null) defaultSpeedDownButton.onClick.RemoveListener(OnClickDefaultSpeedDown);
        if (defaultSpeedUpButton != null) defaultSpeedUpButton.onClick.RemoveListener(OnClickDefaultSpeedUp);
        if (measureOffsetButton != null) measureOffsetButton.onClick.RemoveListener(OnClickMeasureOffset);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        if (windowModeDropdown != null) windowModeDropdown.onValueChanged.RemoveListener(OnWindowModeChanged);
        if (offsetStepper != null) offsetStepper.Stepped -= StepOffset;
        if (defaultSpeedStepper != null) defaultSpeedStepper.Stepped -= StepDefaultSpeed;
    }
}
