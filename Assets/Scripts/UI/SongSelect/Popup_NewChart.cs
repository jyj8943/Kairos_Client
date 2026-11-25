using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 새 채보 팝업 (기획서 3-7 ④ 「새 채보 팝업 (Popup_NewChart, 11/18)」). SongSelectScene의 Canvas_SongSelect 아래에 두고, 곡 선택이 Init(SongData, …)을 부른 뒤 Open한다.
/// - 언제: 나만의 채보 탭에서 "+ 새 채보 생성" 줄을 고르고 Enter(또는 클릭).
/// - 모양: 가로 600, 화면 가운데, 공용 팝업 톤 (PopupBackdrop 블러 6 + 덮개 42%, 패널 86% · 모서리 20 · 그림자).
///   위에서부터 제목 "새 채보 생성 NEW CHART" + 하늘색 밑줄 → 곡 카드(작은 앨범 · 제목 · 아티스트 · BPM · 길이)
///   → 채보 이름(Input_ChartName, 높이 52) + 레벨 ◀ 7 ▶ (1~15, 와이어프레임대로 한 줄) → 오류 글자(빨강) → 취소 · 만들기(하늘색, 높이 56) → 키 안내 줄.
/// - 조작: ↑↓ 항목 (이름 → 레벨 → 버튼), ←→ 레벨 바꾸기(Btn_Level의 UINavigateStepper) / 버튼 고르기, Enter 결정 (입력칸에서 Enter도 만들기).
///   ESC는 UIManager가 받는다: 입력 중이면 입력만 끝내고(글자 유지, Restore Original Text On Escape 끔), 아니면 OnCancelRequested → 닫고 + 줄을 다시 고른다.
/// - 오류: 이름이 비면(공백만 포함) 입력칸 빨강 2px 테두리 + "채보 이름을 입력해 주세요". 이름을 고치면 오류가 사라진다.
/// - 만들기: NewChartRules로 검사해 EditorEntryData { songId, chartName, level, chartId = null }를 곡 선택(onCreate)에 넘긴다 (InGameLauncher.StartEditor로 에디터에 간다).
/// - 팝업을 연 Enter는 팝업 안에서 쓰지 않는다: 열릴 때 Enter(SongSelect/Start, 팝업을 여는 키)가 눌려 있었으면 그 Enter를 한 번 뗄 때까지(액션 canceled)
///   결정(입력칸 onSubmit · 만들기 버튼)을 무시한다. 입력칸이 처음 선택이라 TMP가 같은 Enter를 onSubmit으로 받아 바로 만들어 버리기 때문이다.
///   무시한 onSubmit 뒤에는 다음 프레임에 입력칸을 다시 활성화해 바로 이어서 타이핑·Enter할 수 있게 둔다. 마우스로 열었으면(Enter 안 눌림) 기다리지 않는다.
///   canceled 구독은 열려 있는 동안만 (OnOpened에서 켜고 OnClosed·OnDestroy에서 끊는다). 액션은 다른 곳과 함께 쓰므로 Disable하지 않는다.
/// - 초기화는 Init에서만 한다. Open/Close는 열고 닫기만.
/// </summary>
public class Popup_NewChart : UIPopup
{
    [Header("제목")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text titleEnText;

    [Header("곡 카드")]
    [SerializeField] private Image albumImage;
    [SerializeField] private TMP_Text songTitleText;
    [SerializeField] private TMP_Text songInfoText;

    [Header("입력")]
    [SerializeField] private TMP_Text nameLabelText;
    [SerializeField] private TMP_InputField chartNameInput;
    [Tooltip("이름이 비었을 때 켜는 빨강 2px 테두리 (Input_ChartName/Img_ErrorBorder)")]
    [SerializeField] private Image nameErrorBorder;
    [SerializeField] private TMP_Text levelLabelText;
    [Tooltip("레벨 줄 (Btn_Level, ←→로 값을 바꾸는 선택 대상, UINavigateStepper)")]
    [SerializeField] private Button levelRow;
    [SerializeField] private Button levelDownButton;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TMP_Text errorText;

    [Header("버튼 · 키 안내")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelText;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text createText;
    [SerializeField] private KeyGuideBar keyGuideBar;

    [Header("입력")]
    [Tooltip("팝업을 여는 Enter (SongSelect/Start). 열 때 눌려 있었으면 뗄 때까지 결정을 무시한다")]
    [SerializeField] private InputActionReference openKeyAction;

    private SongData song;
    private bool isWaitingOpenKeyRelease;
    private bool isOpenKeySubscribed;
    private int level;
    private Func<EditorEntryData, bool> onCreate;
    private Action onCanceled;
    private UINavigateStepper levelStepper;
    private bool isAnswered;

    /// <summary>지금 곡 (확인용)</summary>
    public SongData Song => song;

    /// <summary>지금 레벨 (확인용)</summary>
    public int Level => level;

    /// <summary>팝업을 연 Enter를 아직 떼지 않아 결정을 무시하는 중이면 true (확인용)</summary>
    public bool IsWaitingOpenKeyRelease => isWaitingOpenKeyRelease;

    /// <summary>오류가 보이는 중이면 true (확인용)</summary>
    public bool HasError => errorText != null && errorText.gameObject.activeSelf;

    /// <summary>이름 입력칸 (확인용)</summary>
    public TMP_InputField ChartNameInput => chartNameInput;

    /// <summary>만들기 버튼 (확인용)</summary>
    public Button CreateButton => createButton;

    /// <summary>오류 글자 (확인용)</summary>
    public TMP_Text ErrorText => errorText;

    /// <summary>곡 제목 (확인용)</summary>
    public TMP_Text SongTitleText => songTitleText;

    /// <summary>
    /// 이 곡으로 내용을 채우고 콜백을 연결한다 (열기 전에 한 번). 닫힌 상태로 둔다 (곡 선택이 Init 직후 Open을 부른다).
    /// </summary>
    /// <param name="existingChartCount">그 곡의 나만의 채보 수 (처음 이름 "새 채보 n")</param>
    /// <param name="onCreate">만들기: 에디터로 넘길 데이터. 씬 이동을 시작했으면 true (false면 다시 누를 수 있다)</param>
    /// <param name="onCanceled">취소·ESC: 닫힌 뒤 부른다 (곡 선택이 + 줄을 다시 고른다)</param>
    public void Init(SongData song, int existingChartCount, Func<EditorEntryData, bool> onCreate, Action onCanceled)
    {
        this.song = song;
        this.onCreate = onCreate;
        this.onCanceled = onCanceled;
        isAnswered = false;

        InitPopupTone();
        UITheme theme = UITheme.Current;
        titleText.text = GameConstants.SongSelect.NewChartTitle;
        titleEnText.text = GameConstants.SongSelect.NewChartTitleEn;
        nameLabelText.text = GameConstants.SongSelect.NewChartNameLabel;
        levelLabelText.text = string.Format(GameConstants.SongSelect.NewChartLevelLabelFormat, GameConstants.Editor.MinLevel, GameConstants.Editor.MaxLevel);
        cancelText.text = GameConstants.SongSelect.NewChartCancelLabel;
        createText.text = GameConstants.SongSelect.NewChartCreateLabel;
        errorText.text = GameConstants.SongSelect.NewChartNameEmpty;
        errorText.color = theme.danger;
        if (nameErrorBorder != null) nameErrorBorder.color = theme.danger;

        // 곡 카드
        if (song != null)
        {
            albumImage.sprite = song.Cover;
            albumImage.color = song.Cover != null ? Color.white : theme.emptyCover;
            songTitleText.text = song.Title;
            string artist = string.IsNullOrEmpty(song.Artist) ? GameConstants.SongSelect.EmptyValue : song.Artist;
            // 아직 채보가 없으므로 곡의 BPM 구간(새 채보가 시작할 구간)으로 표기한다
            string bpm = BpmDisplayRules.Format(song, null);
            songInfoText.text = string.Format(GameConstants.SongSelect.NewChartSongInfoFormat, artist, bpm, song.GetLengthText());
        }

        chartNameInput.restoreOriginalTextOnEscape = false;
        chartNameInput.SetTextWithoutNotify(NewChartRules.DefaultName(existingChartCount));
        SetLevel(GameConstants.SongSelect.NewChartDefaultLevel);
        ShowError(false);

        AddListeners();
        LinkNavigation();
        InitKeyGuide();
        HideImmediate();
    }

    /// <summary>ESC = 취소 (입력 중 ESC는 UIManager가 입력만 끝낸다)</summary>
    public override void OnCancelRequested()
    {
        Cancel();
    }

    protected override void OnOpened()
    {
        // 팝업을 연 Enter가 아직 눌려 있으면 뗄 때까지 결정을 무시한다
        InputAction openKey = openKeyAction != null ? openKeyAction.action : null;
        isWaitingOpenKeyRelease = openKey != null && openKey.IsPressed();
        SubscribeOpenKey();

        // 열리면 이름 끝에 커서를 둔다 (처음 선택 = 입력칸, 선택되면 입력이 시작된다)
        chartNameInput.MoveTextEnd(false);
    }

    protected override void OnClosed()
    {
        UnsubscribeOpenKey();
        isWaitingOpenKeyRelease = false;
    }

    private void OnDestroy()
    {
        UnsubscribeOpenKey();
        RemoveListeners();
    }

    private void SubscribeOpenKey()
    {
        if (isOpenKeySubscribed || openKeyAction == null) return;
        openKeyAction.action.canceled += OnOpenKeyReleased;
        isOpenKeySubscribed = true;
    }

    /// <summary>액션은 곡 선택과 함께 쓰므로 Disable하지 않고 구독만 끊는다</summary>
    private void UnsubscribeOpenKey()
    {
        if (!isOpenKeySubscribed) return;
        openKeyAction.action.canceled -= OnOpenKeyReleased;
        isOpenKeySubscribed = false;
    }

    /// <summary>팝업을 연 Enter를 뗐다: 이제부터 Enter로 결정할 수 있다</summary>
    private void OnOpenKeyReleased(InputAction.CallbackContext context)
    {
        isWaitingOpenKeyRelease = false;
    }

    private void AddListeners()
    {
        RemoveListeners();
        cancelButton.onClick.AddListener(Cancel);
        createButton.onClick.AddListener(Create);
        levelDownButton.onClick.AddListener(OnClickLevelDown);
        levelUpButton.onClick.AddListener(OnClickLevelUp);
        chartNameInput.onValueChanged.AddListener(OnNameChanged);
        chartNameInput.onSubmit.AddListener(OnNameSubmit);
        if (levelStepper == null) levelStepper = levelRow.GetComponent<UINavigateStepper>();
        if (levelStepper != null) levelStepper.Stepped += StepLevel;
    }

    private void RemoveListeners()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        if (createButton != null) createButton.onClick.RemoveListener(Create);
        if (levelDownButton != null) levelDownButton.onClick.RemoveListener(OnClickLevelDown);
        if (levelUpButton != null) levelUpButton.onClick.RemoveListener(OnClickLevelUp);
        if (chartNameInput != null)
        {
            chartNameInput.onValueChanged.RemoveListener(OnNameChanged);
            chartNameInput.onSubmit.RemoveListener(OnNameSubmit);
        }
        if (levelStepper != null) levelStepper.Stepped -= StepLevel;
    }

    /// <summary>↑↓: 이름 → 레벨 → 버튼 (취소 · 만들기는 ←→). 레벨 줄의 ←→는 UINavigateStepper가 값을 바꾼다. ◀▶는 마우스 전용</summary>
    private void LinkNavigation()
    {
        chartNameInput.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = levelRow };
        levelRow.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = chartNameInput, selectOnDown = createButton };
        cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = levelRow, selectOnRight = createButton };
        createButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = levelRow, selectOnLeft = cancelButton };
        levelDownButton.navigation = new Navigation { mode = Navigation.Mode.None };
        levelUpButton.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    /// <summary>키 안내 줄: ↑↓ 항목 · ←→ 레벨 · 버튼 · Enter 결정 · ESC 취소</summary>
    private void InitKeyGuide()
    {
        if (keyGuideBar == null) return;
        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.NewChartItem, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.NewChartLevelOrButton, KeyGuideKey.Fixed(GameConstants.KeyGuide.LeftRightKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.ConfirmDecide, KeyGuideKey.Fixed(GameConstants.KeyGuide.EnterKey)),
            new KeyGuideEntry(GameConstants.KeyGuide.ConfirmCancel, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    private void OnClickLevelDown() => StepLevel(-1);
    private void OnClickLevelUp() => StepLevel(1);

    private void StepLevel(int direction)
    {
        SetLevel(level + direction);
    }

    private void SetLevel(int value)
    {
        level = NewChartRules.ClampLevel(value);
        levelText.SetText("{0}", level);
    }

    private void OnNameChanged(string value)
    {
        if (HasError) ShowError(false);
    }

    /// <summary>
    /// 입력칸에서 Enter: 만들기 (Enter 결정). 팝업을 연 Enter를 아직 떼지 않았으면 무시하고, TMP가 Enter 뒤에 입력을 끝내므로 다음 프레임에 다시 활성화한다.
    /// </summary>
    private void OnNameSubmit(string value)
    {
        if (isWaitingOpenKeyRelease)
        {
            Debug.Log("[Popup_NewChart] 팝업을 연 Enter라 결정하지 않습니다 (Enter를 뗀 뒤부터 결정).", this);
            if (IsOpen && isActiveAndEnabled) StartCoroutine(ReactivateInputNextFrame());
            return;
        }
        Create();
    }

    private IEnumerator ReactivateInputNextFrame()
    {
        yield return null;
        if (!IsOpen || chartNameInput == null) yield break;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(chartNameInput.gameObject);
        chartNameInput.ActivateInputField();
    }

    private void ShowError(bool show)
    {
        errorText.gameObject.SetActive(show);
        if (nameErrorBorder != null) nameErrorBorder.gameObject.SetActive(show);
    }

    /// <summary>
    /// 만들기: 이름이 비면 오류를 띄우고 이름 칸을 고른다. 아니면 닫지 않고 곡 선택에 넘긴다 (씬 이동이 시작되면 그대로 떠난다).
    /// </summary>
    public void Create()
    {
        if (!IsOpen || isAnswered) return;
        if (isWaitingOpenKeyRelease)
        {
            Debug.Log("[Popup_NewChart] 팝업을 연 Enter라 만들지 않습니다.", this);
            return;
        }

        EditorEntryData data = NewChartRules.CreateEntry(song != null ? song.SongId : null, chartNameInput.text, level);
        if (data == null)
        {
            ShowError(true);
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(chartNameInput.gameObject);
            Debug.Log("[Popup_NewChart] 채보 이름이 비어 만들지 않습니다.", this);
            return;
        }

        isAnswered = true;
        Debug.Log($"[Popup_NewChart] 만들기: songId={data.songId}, 이름=\"{data.chartName}\", Lv.{data.level}", this);
        if (onCreate == null || !onCreate(data)) isAnswered = false;
    }

    /// <summary>취소 (버튼 · ESC): 닫고 곡 선택에 알린다</summary>
    private void Cancel()
    {
        if (!IsOpen || isAnswered) return;

        isAnswered = true;
        Close();
        Debug.Log("[Popup_NewChart] 취소", this);
        onCanceled?.Invoke();
    }
}
