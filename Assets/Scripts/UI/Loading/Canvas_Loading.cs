using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일반 로딩 오버레이 (기획서 3-10 「일반 로딩」, 와이어프레임 13_GeneralLoading). 곡 로딩이 아닌 씬 이동(타이틀 ↔ 곡 선택, 곡 선택 → 에디터,
/// 에디터 → 곡 선택 · 테스트 플레이, 결과 → 곡 선택, 인게임 나가기)을 덮는다.
/// - Managers_DontDestroy/UIManager 아래에 있어 씬이 바뀌어도 살아 있다. 정렬 순서는 Canvas_Overlay 팝업 · 곡 로딩보다 위(GameConstants.Loading.SortOrder).
/// - Bootstrapper가 전역 매니저 Init 때 Init()을 한 번 부르고 숨겨 둔다. 일러스트 목록을 읽고, 폰트 글자를 채우고, 숨긴 채 한 번 그려 둔다.
/// - 흐름은 GameSceneManager.LoadWithLoading(오버레이 로딩 공용 루틴)이 정한다: Open(다음 씬 이름) → 다음 프레임부터 들어오기 0~0.5초 →
///   (씬 교체 · Init · 정리 · 들어오기 0초부터 1.5초) → CloseRoutine() 나가기 0.5초 → 다음 씬 OnEnter.
/// - 자식 (README 5절): Background(MenuBackground 대체 배경 + Img_Illust) · ScreenName(Txt_ScreenName + Txt_Destination) ·
///   Img_Band(Img_BandLine · TipRow(Img_TipBadge └ Txt_TipBadge, Txt_TipCategory) · Txt_Tip · Loading(Img_LoadingClock · Txt_NowLoading · Dots └ Img_Dot × 3)) ·
///   Img_InputBlocker(맨 앞, 투명, 마우스를 막는다).
/// - 일러스트: Resources/Images/Loading/의 스프라이트를 모두 읽어 열 때마다 무작위로 하나 (2장 이상이면 바로 앞 것은 피한다).
///   폴더가 비어 있으면 MenuBackground 라벤더 그라데이션으로 대신하고 경고를 한 번 남긴다. 확정 일러스트는 파일만 넣으면 된다.
/// - TIP: GameConstants.Loading.Tips에서 열 때마다 무작위로 한 줄 (바로 앞 팁은 피한다), 분류 글자도 같이 바꾼다.
/// - 시간은 unscaled 프레임 시각. 색은 UITheme에서 Init 때 칠한다 (씬 · 프리팹에 색을 직렬화하지 않는다).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class Canvas_Loading : UIBase, ILoadingOverlay
{
    [Header("배경")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private MenuBackground fallbackBackground;
    [SerializeField] private Image illust;
    [SerializeField] private AspectRatioFitter illustFitter;
    [Tooltip("마우스 클릭을 막는 투명 이미지 (맨 앞)")]
    [SerializeField] private Image inputBlocker;

    [Header("화면 이름")]
    [SerializeField] private CanvasGroup screenNameGroup;
    [SerializeField] private TMP_Text screenNameText;
    [SerializeField] private TMP_Text destinationText;

    [Header("아래 띠 (Img_Band, y 864~1080)")]
    [SerializeField] private Image band;
    [SerializeField] private Image bandLine;
    [SerializeField] private Image tipBadge;
    [SerializeField] private TMP_Text tipBadgeText;
    [SerializeField] private TMP_Text tipCategoryText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Image loadingClock;
    [SerializeField] private Image loadingClockHand;
    [SerializeField] private TMP_Text nowLoadingText;
    [SerializeField] private Image[] dots;

    private Canvas canvas;
    private RectTransform bandRect;
    private readonly List<Sprite> illusts = new List<Sprite>();
    private readonly System.Random random = new System.Random();
    private bool warnedEmpty;
    private bool isShown;
    private bool isClosing;
    private bool openStarted;
    private double openStart;
    private double shownStart;
    private Coroutine openRoutine;
    private int lastIllust = -1;
    private int lastTip = -1;

    public override UILayer Layer => UILayer.Overlay;

    // 오버레이 규약 (ILoadingOverlay)
    public string LogName => "일반 로딩";
    public string OpenStartStep => "들어오기 0초";
    public string OpenEndStep => "들어오기 끝";
    public string OpeningPhase => "들어오기 0~0.5초";
    public string ClosingPhase => "나가기";

    public bool IsShown => isShown;
    public bool HasOpenStarted => isShown && openStarted;
    public bool IsOpening => isShown && !isClosing && (!openStarted || OpenElapsed < GameConstants.Loading.InSeconds);
    public bool IsClosing => isClosing;
    public float OpenElapsed => isShown && openStarted ? (float)(Time.unscaledTimeAsDouble - openStart) : 0f;

    /// <summary>마지막으로 적용한 모습 (확인용)</summary>
    public LoadingFrame Frame { get; private set; }

    /// <summary>읽은 일러스트 수 (확인용)</summary>
    public int IllustCount => illusts.Count;

    /// <summary>마지막으로 고른 일러스트 · TIP 번호 (확인용, 없으면 -1)</summary>
    public int LastIllustIndex => lastIllust;
    public int LastTipIndex => lastTip;

    /// <summary>대체 배경(MenuBackground)을 쓰는 중이면 true (확인용)</summary>
    public bool IsFallbackBackground => fallbackBackground != null && fallbackBackground.gameObject.activeSelf;

    /// <summary>Init의 미리 그려 두기가 끝났으면 true</summary>
    public bool IsPrewarmed { get; private set; }

    /// <summary>확인용 참조</summary>
    public TMP_Text DestinationText => destinationText;
    public TMP_Text TipText => tipText;
    public TMP_Text TipCategoryText => tipCategoryText;
    public Image Illust => illust;
    public RectTransform BandRect => bandRect;
    public Image[] Dots => dots;

    private void CacheComponents()
    {
        if (canvas != null) return;
        canvas = GetComponent<Canvas>();
        if (band != null) bandRect = band.rectTransform;
    }

    /// <summary>
    /// 색 · 고정 글자를 맞추고, 일러스트 목록을 읽고, 숨긴다. 첫 열기 렉을 막으려고 폰트 글자를 채우고 숨긴 채 한 번 그려 둔다.
    /// Bootstrapper가 전역 매니저 Init 때 한 번 부른다.
    /// </summary>
    public void Init()
    {
        CacheComponents();
        UITheme theme = UITheme.Current;
        if (fallbackBackground != null) fallbackBackground.Init();
        if (illust != null) illust.color = Color.white;
        if (inputBlocker != null) inputBlocker.color = Color.clear;

        SetText(screenNameText, GameConstants.Loading.ScreenName, theme.textPrimary);
        Paint(destinationText, theme.textDescription);
        Paint(band, theme.loadingBand);
        Paint(bandLine, theme.loadingBandLine);
        Paint(tipBadge, theme.accent);
        SetText(tipBadgeText, GameConstants.Loading.TipBadge, theme.onAccentText);
        Paint(tipCategoryText, theme.textDescription);
        Paint(tipText, theme.textPrimary);
        Paint(loadingClock, theme.loadingClock);
        Paint(loadingClockHand, theme.loadingClock);
        SetText(nowLoadingText, GameConstants.Loading.NowLoading, theme.textPrimary);

        SetIllustrations(Resources.LoadAll<Sprite>(GameConstants.Loading.IllustFolder));
        Hide();
        StartCoroutine(PrewarmRoutine());
    }

    /// <summary>쓸 일러스트 목록을 바꾼다 (Init이 Resources 폴더로 부른다. 확인용 테스트는 빈 목록 · 여러 장으로 부른다)</summary>
    public void SetIllustrations(IList<Sprite> sprites)
    {
        illusts.Clear();
        if (sprites != null)
            foreach (Sprite s in sprites) if (s != null) illusts.Add(s);
        lastIllust = -1;
        if (illusts.Count == 0 && !warnedEmpty)
        {
            warnedEmpty = true;
            Debug.LogWarning($"[Canvas_Loading] Resources/{GameConstants.Loading.IllustFolder}에 로딩 일러스트가 없어 라벤더 그라데이션으로 대신합니다.", this);
        }
    }

    /// <summary>미리 채워 둘 글자 (숫자 · 영문 · 기호)</summary>
    private const string PrewarmCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .,:;/-+()·";

    private IEnumerator PrewarmRoutine()
    {
        System.Text.StringBuilder chars = new System.Text.StringBuilder(PrewarmCharacters);
        chars.Append(GameConstants.Loading.ScreenName).Append(GameConstants.Loading.NowLoading).Append(GameConstants.Loading.TipBadge)
            .Append(GameConstants.Loading.ToSongSelect).Append(GameConstants.Loading.ToEditor).Append(GameConstants.Loading.ToTitle).Append(GameConstants.Loading.ToTestPlay);
        foreach (GameConstants.LoadingTip tip in GameConstants.Loading.Tips) chars.Append(tip.Category).Append(tip.Text);
        string text = chars.ToString();
        HashSet<TMP_FontAsset> fonts = new HashSet<TMP_FontAsset>();
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            if (t.font != null && fonts.Add(t.font)) t.font.TryAddCharacters(text, out string _);

        if (!isShown)
        {
            SetContent(GameConstants.Scenes.SongSelect);
            lastIllust = -1;
            lastTip = -1;
            canvas.enabled = true;
            Apply(LoadingTimeline.Hidden);
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!isShown) canvas.enabled = false;
        }
        IsPrewarmed = true;
        Debug.Log($"[Canvas_Loading] 미리 그려 두기: 폰트 {fonts.Count}개에 글자 {text.Length}자, 일러스트 {illusts.Count}장, 숨긴 채 한 프레임 그림", this);
    }

    /// <summary>
    /// 다음 씬으로 가는 곳 문구 · 무작위 일러스트 · 무작위 TIP을 채우고 보이기 시작한다 (Enter 프레임). 들어오기 0초는 그다음 프레임부터 센다.
    /// </summary>
    public void Open(string nextScene)
    {
        CacheComponents();
        SetContent(nextScene);
        isShown = true;
        isClosing = false;
        openStarted = false;
        shownStart = Time.unscaledTimeAsDouble;
        canvas.enabled = true;
        if (inputBlocker != null) inputBlocker.raycastTarget = true;
        Apply(LoadingTimeline.EvaluateIn(0f));
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(OpenRoutine());
    }

    /// <summary>들어오기 연출이 끝날 때까지 (0.5초) 기다린다</summary>
    public IEnumerator OpenRoutine()
    {
        yield return null;
        openStart = Time.unscaledTimeAsDouble;
        openStarted = true;
        while (isShown && !isClosing && OpenElapsed < GameConstants.Loading.InSeconds)
        {
            Apply(LoadingTimeline.EvaluateIn(OpenElapsed));
            yield return null;
        }
        if (isShown && !isClosing) Apply(LoadingTimeline.Shown);
        openRoutine = null;
    }

    /// <summary>나가기 연출을 하고 (0.5초) 숨긴다</summary>
    public IEnumerator CloseRoutine()
    {
        if (!isShown) yield break;
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = null;
        isClosing = true;
        double closeStart = Time.unscaledTimeAsDouble;
        float elapsed = 0f;
        while (elapsed < GameConstants.Loading.OutSeconds)
        {
            Apply(LoadingTimeline.EvaluateOut(elapsed));
            yield return null;
            elapsed = (float)(Time.unscaledTimeAsDouble - closeStart);
        }
        Hide();
    }

    private void Update()
    {
        if (!isShown || dots == null) return;
        int lit = LoadingTimeline.LitDots((float)(Time.unscaledTimeAsDouble - shownStart));
        Color on = UITheme.Current.loadingClock;
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null) dots[i].color = UITheme.WithAlpha(on, i < lit ? 1f : GameConstants.Loading.DotOffAlpha);
    }

    /// <summary>가는 곳 · 일러스트 · TIP을 고른다</summary>
    public void SetContent(string nextScene)
    {
        CacheComponents();
        if (destinationText != null) destinationText.text = LoadingRules.DestinationOf(nextScene);

        lastIllust = LoadingRules.PickIndex(illusts.Count, lastIllust, random);
        bool hasIllust = lastIllust >= 0;
        if (illust != null)
        {
            illust.gameObject.SetActive(hasIllust);
            if (hasIllust)
            {
                Sprite sprite = illusts[lastIllust];
                illust.sprite = sprite;
                if (illustFitter != null && sprite.rect.height > 0f) illustFitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }
        if (fallbackBackground != null)
        {
            fallbackBackground.gameObject.SetActive(!hasIllust);
            if (!hasIllust) fallbackBackground.SetIllustration(null, true);
        }

        GameConstants.LoadingTip[] tips = GameConstants.Loading.Tips;
        lastTip = LoadingRules.PickIndex(tips.Length, lastTip, random);
        if (lastTip >= 0)
        {
            if (tipCategoryText != null) tipCategoryText.text = tips[lastTip].Category;
            if (tipText != null) tipText.text = tips[lastTip].Text;
        }
        if (tipBadge != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)tipBadge.transform.parent);
        if (destinationText != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)destinationText.transform.parent);
    }

    private void Hide()
    {
        CacheComponents();
        isShown = false;
        isClosing = false;
        openStarted = false;
        if (inputBlocker != null) inputBlocker.raycastTarget = false;
        Apply(LoadingTimeline.Hidden);
        canvas.enabled = false;
    }

    private void Apply(LoadingFrame f)
    {
        Frame = f;
        if (backgroundGroup != null) backgroundGroup.alpha = f.FrontAlpha;
        if (screenNameGroup != null) screenNameGroup.alpha = f.FrontAlpha;
        if (bandRect != null) bandRect.anchoredPosition = new Vector2(0f, -f.BandDrop);
    }

    private static void Paint(Graphic graphic, Color color)
    {
        if (graphic != null) graphic.color = color;
    }

    private static void SetText(TMP_Text text, string value, Color color)
    {
        if (text == null) return;
        text.text = value;
        text.color = color;
    }
}
