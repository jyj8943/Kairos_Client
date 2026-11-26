using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 로딩 오버레이 · 시계 (기획서 3-8 「곡 로딩」, 11/26 와이어프레임 21_SongLoading_Clock). 곡 선택 시작 · 결과 다시 하기 → 인게임 사이에 덮는다.
/// - Managers_DontDestroy/UIManager 아래에 있어 씬이 바뀌어도 살아 있다. 정렬 순서는 Canvas_Overlay 팝업보다 위(GameConstants.SongLoading.SortOrder).
/// - Bootstrapper가 전역 매니저 Init 때 Init()을 한 번 부르고 숨겨 둔다. Init이 숨긴 채로 한 번 그려 둔다 (폰트 글자 · 곡 배경 블러 RenderTexture · 첫 그리기).
/// - 흐름은 GameSceneManager.LoadWithSongLoading이 정한다: Open(진입 데이터, 글자 · 앨범을 먼저 채움) → 다음 프레임부터 열기 연출 0 ~ 0.95초 →
///   앞 씬 OnExit · 인게임 로드 · Init (SetLoadProgress로 진행을 알림) → 바늘이 12시에 닿음(T) → 반짝 · 암전 (IsBlackedOut) →
///   검은 화면에서 정리 → ReleaseBlackout → 걷힘 (CloseRoutine이 숨길 때까지 기다림) → 인게임 OnEnter.
/// - 자식 (뒤 → 앞): Background(Img_SongArt 흐린 곡 배경 + Img_Dim) · Clock(Img_ClockTicks · Img_ClockArc · Img_ClockDot · Img_ClockSpark) · Img_Glow ·
///   Img_Album(Mask) └ Img_AlbumCover · Img_AlbumFlash · Info(TitleViewport └ Txt_Title · Txt_Artist · BadgeRow · ChartViewport └ Txt_Chart) ·
///   Sd(Img_SdLine · Img_SD · Txt_Loading) · Img_Blackout · Img_InputBlocker(맨 앞, 투명, 마우스를 막는다).
/// - 시간표는 SongLoadingClockTimeline (순수 함수). 매 프레임은 값만 바꾸고 메모리를 새로 잡지 않는다.
/// - 같은 진입 데이터(곡 · 채보)로 글자를 그린다. 배속 · 기록은 쓰지 않는다. 싱글톤에 값을 남기지 않는다.
/// - 시간은 unscaled 프레임 시각(Time.unscaledTimeAsDouble)으로 센다. 색은 UITheme에서 Init 때 칠한다 (씬 · 프리팹에 색을 직렬화하지 않는다).
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
public class Canvas_SongLoading : UIBase, ILoadingOverlay
{
    [Header("배경")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [Tooltip("흐린 곡 가로 일러스트 (RawImage + UIBlurBackground)")]
    [SerializeField] private UIBlurBackground songArt;
    [SerializeField] private Image dim;

    [Header("시계 (Clock, 중심 960, 400)")]
    [SerializeField] private CanvasGroup clockGroup;
    [SerializeField] private Image clockTicks;
    [Tooltip("시곗바늘 호 (Filled · Radial 360 · Top · Clockwise)")]
    [SerializeField] private Image clockArc;
    [SerializeField] private Image clockDot;
    [SerializeField] private Image clockSpark;

    [Header("앨범")]
    [SerializeField] private Image glow;
    [Tooltip("Img_Album (Mask, 둥근 사각형)")]
    [SerializeField] private Image album;
    [SerializeField] private Image albumCover;
    [SerializeField] private Image albumFlash;

    [Header("글자 (Info)")]
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("제목 칸 (TitleViewport). 넘칠 때만 흐른다")]
    [SerializeField] private UIMarqueeText titleMarquee;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private RectTransform badgeRow;
    [SerializeField] private Image badge;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text chartText;
    [Tooltip("채보 이름 칸 (ChartViewport). 나만의 채보만 보이고 넘칠 때만 흐른다")]
    [SerializeField] private UIMarqueeText chartMarquee;

    [Header("SD · LOADING (Sd)")]
    [SerializeField] private CanvasGroup sdGroup;
    [SerializeField] private Image sdImage;
    [SerializeField] private Image sdLine;
    [SerializeField] private TMP_Text loadingText;

    [Header("끝 · 입력")]
    [SerializeField] private Image blackout;
    [Tooltip("마우스 클릭을 막는 투명 이미지 (맨 앞)")]
    [SerializeField] private Image inputBlocker;

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private RectTransform clockRect, glowRect, albumRect, infoRect, sdRect, dotRect;
    private Vector2 infoBase, sdBase;
    private Color albumCoverColor = Color.white;
    private Color glowColor, flashColor, sparkColor, blackoutColor;

    private bool isShown;
    private bool openStarted;
    private double openStart;
    private Coroutine runRoutine;

    private float loadProgress;
    private float fill;
    private bool reachedTwelve;
    private float twelveAt;
    private bool revealRequested;
    private float revealStart = GameConstants.SongLoading.RevealStart;

    public override UILayer Layer => UILayer.Overlay;

    // 오버레이 규약 (ILoadingOverlay) 로그 이름
    public string LogName => "곡 로딩";
    public string OpenStartStep => "열기 시작";
    public string OpenEndStep => "열기 끝 0.95";
    public string OpeningPhase => "열기 0~0.95초";
    public string ClosingPhase => "끝 (반짝 · 암전 · 걷힘)";
    /// <summary>곡 로딩은 최소 시간 대신 "로딩 진행 = 1"과 "바늘 2.4초" 둘 다를 기다린다 (GameSceneManager 곡 로딩 경로)</summary>
    public double MinSeconds => 0;
    public bool MinFromOpenEnd => true;

    /// <summary>오버레이가 보이는 중이면 true (Open ~ 걷힘 끝)</summary>
    public bool IsShown => isShown;

    /// <summary>열기 연출(0.95초) 중이면 true</summary>
    public bool IsOpening => isShown && !reachedTwelve && (!openStarted || OpenElapsed < GameConstants.SongLoading.OpenSeconds);

    /// <summary>열기 연출 0초를 센 뒤면 true (Open한 다음 프레임부터)</summary>
    public bool HasOpenStarted => isShown && openStarted;

    /// <summary>끝 연출(반짝 · 암전 · 걷힘) 중이면 true (바늘 12시 T부터 숨길 때까지)</summary>
    public bool IsClosing => isShown && reachedTwelve;

    /// <summary>Init의 미리 그려 두기(블러 RenderTexture · 폰트 글자 · 첫 그리기)가 끝났으면 true</summary>
    public bool IsPrewarmed { get; private set; }

    /// <summary>열기 시작부터 지난 시간 (초, unscaled 프레임 시각)</summary>
    public float OpenElapsed => isShown && openStarted ? (float)(Time.unscaledTimeAsDouble - openStart) : 0f;

    /// <summary>지금 로딩 진행 (0 ~ 1)</summary>
    public float LoadProgress => loadProgress;

    /// <summary>지금 바늘 fillAmount (0 ~ 1, 줄어들지 않는다)</summary>
    public float Fill => fill;

    /// <summary>바늘이 12시에 닿았으면 true (T 이후)</summary>
    public bool HasReachedTwelve => isShown && reachedTwelve;

    /// <summary>바늘이 12시에 닿은 시각 (열기 0초 기준 초, 확인용)</summary>
    public float TwelveAt => twelveAt;

    /// <summary>T부터 지난 시간 (끝 전이면 −1)</summary>
    public float EndElapsed => isShown && reachedTwelve ? OpenElapsed - twelveAt : -1f;

    /// <summary>암전이 끝나 화면이 다 검으면 true (T+0.65 이후)</summary>
    public bool IsBlackedOut => isShown && reachedTwelve && EndElapsed >= GameConstants.SongLoading.BlackedOutAt;

    /// <summary>걷히기 시작한 시각 (T 기준, 확인용)</summary>
    public float RevealStart => revealStart;

    /// <summary>마지막으로 적용한 모습 (확인용)</summary>
    public SongLoadingFrame Frame { get; private set; }

    /// <summary>마지막으로 그린 글자 값 (확인용)</summary>
    public SongLoadingInfo Info { get; private set; }

    /// <summary>확인용 참조</summary>
    public TMP_Text TitleText => titleText;
    public TMP_Text ArtistText => artistText;
    public TMP_Text BadgeText => badgeText;
    public TMP_Text LevelText => levelText;
    public TMP_Text ChartText => chartText;
    public TMP_Text LoadingText => loadingText;
    public UIMarqueeText TitleMarquee => titleMarquee;
    public UIMarqueeText ChartMarquee => chartMarquee;
    public Image Badge => badge;
    public RectTransform BadgeRow => badgeRow;
    public RectTransform AlbumRect => albumRect;
    public Image ClockArc => clockArc;
    public RectTransform ClockDot => dotRect;
    public Image SdImage => sdImage;
    public UIBlurBackground SongArt => songArt;

    private void CacheComponents()
    {
        if (canvas != null) return;
        canvas = GetComponent<Canvas>();
        rootGroup = GetComponent<CanvasGroup>();
        clockRect = clockGroup != null ? (RectTransform)clockGroup.transform : null;
        glowRect = glow != null ? glow.rectTransform : null;
        albumRect = album != null ? album.rectTransform : null;
        infoRect = infoGroup != null ? (RectTransform)infoGroup.transform : null;
        sdRect = sdImage != null ? sdImage.rectTransform : null;
        dotRect = clockDot != null ? clockDot.rectTransform : null;
        if (infoRect != null) infoBase = infoRect.anchoredPosition;
        if (sdRect != null) sdBase = sdRect.anchoredPosition;
    }

    /// <summary>
    /// 색 · 고정 글자 · 곡 배경 블러 세기를 맞추고 숨긴다. Bootstrapper가 전역 매니저 Init 때 한 번 부른다.
    /// </summary>
    public void Init()
    {
        CacheComponents();
        // 전체 알파(걷힘)만 이 CanvasGroup으로 바꾼다. 맨 앞 Img_InputBlocker가 마우스를 막도록 레이캐스트는 켜 둔다
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = false;
        UITheme theme = UITheme.Current;
        Paint(dim, theme.songLoadingDim);
        Paint(clockTicks, theme.songLoadingTicks);
        Paint(clockArc, theme.songLoadingArc);
        Paint(clockDot, theme.songLoadingDot);
        glowColor = theme.songLoadingGlow;
        flashColor = theme.songLoadingFlash;
        sparkColor = theme.songLoadingSpark;
        blackoutColor = theme.songLoadingBlackout;
        Paint(album, Color.white);
        Paint(titleText, theme.songLoadingTitle);
        Paint(artistText, theme.songLoadingSub);
        Paint(badgeText, theme.onDifficultyText);
        Paint(levelText, theme.songLoadingTitle);
        Paint(chartText, theme.songLoadingSub);
        Paint(sdImage, Color.white);
        Paint(sdLine, theme.songLoadingSdLine);
        SetText(loadingText, GameConstants.SongLoading.LoadingText, theme.songLoadingText);
        if (inputBlocker != null) inputBlocker.color = Color.clear;
        if (clockArc != null)
        {
            clockArc.type = Image.Type.Filled;
            clockArc.fillMethod = Image.FillMethod.Radial360;
            clockArc.fillOrigin = (int)Image.Origin360.Top;
            clockArc.fillClockwise = true;
        }
        if (songArt != null)
        {
            songArt.SetColorGrade(1f, 1f);
            songArt.SetBlurRadiusPx(GameConstants.SongLoading.BlurSigmaPx, GameConstants.SongLoading.BlurIterations);
        }

        Hide();
        StartCoroutine(PrewarmRoutine());
    }

    /// <summary>미리 채워 둘 글자 (숫자 · 영문 · 기호, 고정 문구는 따로 더한다)</summary>
    private const string PrewarmCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .,:;/-+x×()[]'\"!?·—―…";

    /// <summary>
    /// 첫 Open 렉을 줄이려고 숨긴 채로 한 번 그려 둔다 (Init에서 한 번):
    /// 쓰는 폰트에 글자(숫자 · 영문 · 고정 문구 · 곡 제목 · 아티스트)를 미리 채우고, 첫 곡으로 글자와 곡 배경 블러(RenderTexture)를 만든 뒤
    /// 오버레이 알파 0으로 Canvas를 한 프레임 그리고 끈다.
    /// </summary>
    private IEnumerator PrewarmRoutine()
    {
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        System.Text.StringBuilder chars = new System.Text.StringBuilder(PrewarmCharacters);
        chars.Append(GameConstants.SongLoading.ChartFormat).Append(GameConstants.SongLoading.LoadingText).Append(GameConstants.Play.ChartBadgeCustomText);
        SongData first = null;
        if (SongManager.HasInstance)
        {
            foreach (SongData song in GameManager.Songs.Songs)
            {
                if (song == null) continue;
                if (first == null) first = song;
                chars.Append(song.Title).Append(song.Artist);
            }
        }
        string text = chars.ToString();
        HashSet<TMP_FontAsset> fonts = new HashSet<TMP_FontAsset>();
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            if (t.font != null && fonts.Add(t.font)) t.font.TryAddCharacters(text, out string _);

        if (first != null && !isShown)
        {
            SetContent(new SongLoadingInfo
            {
                Title = first.Title, Artist = first.Artist, BadgeText = GameConstants.Chart.DifficultyNormal, LevelText = "Lv. 0", ChartText = string.Empty
            }, first);
        }
        double prepareMs = watch.Elapsed.TotalMilliseconds;

        // 오버레이 알파 0으로 한 프레임 그린다 (보이지 않음, 입력도 막지 않음)
        if (!isShown)
        {
            canvas.enabled = true;
            Apply(SongLoadingClockTimeline.Shown);
            rootGroup.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!isShown) Hide();
        }
        IsPrewarmed = true;
        Debug.Log($"[Canvas_SongLoading] 미리 그려 두기: 폰트 {fonts.Count}개에 글자 {text.Length}자, 곡 배경 블러 · 글자 준비 {prepareMs:0.0}ms, 숨긴 채 한 프레임 그림", this);
    }

    /// <summary>
    /// 진입 데이터로 글자 · 앨범 · 곡 배경을 먼저 채우고 보이기 시작한다 (Enter 프레임, 모두 알파 0).
    /// 열기 연출 0초는 그다음 프레임부터 센다 (채우는 프레임의 시간이 연출에 섞이지 않게).
    /// </summary>
    public void Open(InGameEntryData entry)
    {
        CacheComponents();
        SetContent(entry);
        // 숨겨져 있을 때도 돌고 있으므로 열 때마다 처음 상태부터
        if (titleMarquee != null) titleMarquee.Restart();
        if (chartMarquee != null) chartMarquee.Restart();
        isShown = true;
        openStarted = false;
        loadProgress = 0f;
        fill = 0f;
        reachedTwelve = false;
        twelveAt = 0f;
        revealRequested = false;
        revealStart = GameConstants.SongLoading.RevealStart;
        canvas.enabled = true;
        if (inputBlocker != null) inputBlocker.raycastTarget = true;
        Apply(SongLoadingClockTimeline.EvaluateOpen(0f));
        if (runRoutine != null) StopCoroutine(runRoutine);
        runRoutine = StartCoroutine(RunRoutine());
    }

    /// <summary>로딩 진행을 알린다 (0 ~ 1, SongLoadingClockTimeline.LoadProgress). 작아지지 않는다</summary>
    public void SetLoadProgress(float progress)
    {
        loadProgress = Mathf.Max(loadProgress, Mathf.Clamp01(progress));
    }

    /// <summary>
    /// 정리가 끝났으니 걷혀도 된다고 알린다. 걷힘은 T+0.8과 지금 중 늦은 때 시작한다 (정리가 길면 검은 화면이 그만큼 늘어난다).
    /// T 전에 부르면 T+0.8에 걷힌다.
    /// </summary>
    public void ReleaseBlackout()
    {
        if (revealRequested) return;
        revealRequested = true;
        revealStart = reachedTwelve ? Mathf.Max(GameConstants.SongLoading.RevealStart, OpenElapsed - twelveAt) : GameConstants.SongLoading.RevealStart;
    }

    /// <summary>
    /// 열기 · 바늘 · 끝 연출을 매 프레임 진행한다 (Open 다음 프레임부터 0초). 다 걷히면 숨긴다.
    /// </summary>
    private IEnumerator RunRoutine()
    {
        yield return null;
        openStart = Time.unscaledTimeAsDouble;
        openStarted = true;
        while (isShown)
        {
            float t = OpenElapsed;
            fill = SongLoadingClockTimeline.HandFill(t, loadProgress, fill);
            if (!reachedTwelve && fill >= 1f)
            {
                reachedTwelve = true;
                twelveAt = t;
            }
            float e = reachedTwelve ? t - twelveAt : -1f;
            float reveal = revealRequested ? revealStart : float.MaxValue;
            Apply(SongLoadingClockTimeline.Evaluate(t, e, reveal));
            if (revealRequested && e >= revealStart + GameConstants.SongLoading.RevealSeconds)
            {
                runRoutine = null;
                Hide();
                yield break;
            }
            yield return null;
        }
        runRoutine = null;
    }

    /// <summary>
    /// 로딩을 끝내고 숨길 때까지 기다린다: 진행 1 → 바늘 12시 → 반짝 · 암전 → 걷힘 (ReleaseBlackout을 아직 안 불렀으면 여기서 부른다).
    /// </summary>
    public IEnumerator CloseRoutine()
    {
        if (!isShown) yield break;
        SetLoadProgress(1f);
        ReleaseBlackout();
        while (isShown) yield return null;
    }

    /// <summary>숨긴다 (연출 없이)</summary>
    private void Hide()
    {
        CacheComponents();
        if (runRoutine != null) StopCoroutine(runRoutine);
        runRoutine = null;
        isShown = false;
        openStarted = false;
        if (inputBlocker != null) inputBlocker.raycastTarget = false;
        Apply(SongLoadingClockTimeline.Hidden);
        canvas.enabled = false;
    }

    /// <summary>진입 데이터로 곡 · 채보를 찾아 그린다</summary>
    public void SetContent(InGameEntryData entry)
    {
        CacheComponents();
        SongData song = entry != null && SongManager.HasInstance ? GameManager.Songs.Find(entry.songId) : null;
        SetContent(SongLoadingInfoRules.Build(entry, song, FindChart(entry)), song);
    }

    /// <summary>글자 값으로 그린다 (확인용 테스트가 상태별로 직접 부른다)</summary>
    public void SetContent(SongLoadingInfo info, SongData song)
    {
        CacheComponents();
        Info = info;
        UITheme theme = UITheme.Current;
        Sprite cover = song != null ? song.Cover : null;
        if (albumCover != null)
        {
            albumCover.sprite = cover;
            albumCoverColor = cover != null ? Color.white : theme.emptyCover;
        }
        // 곡 가로 일러스트 (없으면 앨범, SongData.Background 규칙)
        if (songArt != null) songArt.SetSource(song != null ? song.Background : null, true);

        titleText.text = info.Title;
        artistText.text = info.Artist;
        badgeText.text = info.BadgeText;
        badge.color = info.IsCustom ? theme.difficultyCustom : theme.GetDifficulty(info.BadgeText);
        levelText.text = info.LevelText;
        if (chartMarquee != null) chartMarquee.gameObject.SetActive(info.ShowChartName);
        chartText.text = info.ChartText;

        // 뱃지 줄은 글자 폭에 맞춘 레이아웃이라 바로 다시 잰다 (가운데 정렬)
        if (badgeRow != null) LayoutRebuilder.ForceRebuildLayoutImmediate(badgeRow);
    }

    /// <summary>진입 데이터의 채보를 찾는다 (chartId가 비면 그 곡 · 탭의 첫 채보)</summary>
    private static ChartData FindChart(InGameEntryData entry)
    {
        if (entry == null) return null;
        if (entry.testPlay != null) return entry.testPlay.chart;
        List<ChartData> charts = ChartRepository.GetCharts(entry.songId, entry.chartType);
        if (charts.Count == 0) return null;
        if (string.IsNullOrEmpty(entry.chartId)) return charts[0];
        foreach (ChartData chart in charts)
            if (chart.chartId == entry.chartId) return chart;
        return null;
    }

    /// <summary>계산값과 바늘을 오브젝트에 적용한다 (매 프레임, 메모리를 새로 잡지 않는다)</summary>
    private void Apply(SongLoadingFrame f)
    {
        Frame = f;
        if (rootGroup != null) rootGroup.alpha = f.RootAlpha;
        SetAlpha(backgroundGroup, f.BackgroundAlpha);
        SetAlpha(clockGroup, f.ClockAlpha);
        SetScale(clockRect, f.ClockScale);
        SetScale(albumRect, f.AlbumScale);
        SetScale(glowRect, f.GlowScale);
        PaintAlpha(albumCover, albumCoverColor, f.AlbumAlpha);
        PaintAlpha(albumFlash, flashColor, f.FlashAlpha);
        PaintAlpha(glow, glowColor, f.GlowAlpha);
        PaintAlpha(clockSpark, sparkColor, f.SparkAlpha);
        PaintAlpha(blackout, blackoutColor, f.BlackoutAlpha);
        SetAlpha(infoGroup, f.InfoAlpha);
        if (infoRect != null) infoRect.anchoredPosition = new Vector2(infoBase.x, infoBase.y + f.InfoOffsetY);
        SetAlpha(sdGroup, f.SdAlpha);
        if (sdRect != null) sdRect.anchoredPosition = new Vector2(sdBase.x, sdBase.y + f.SdOffsetY);
        if (clockArc != null && clockArc.fillAmount != fill) clockArc.fillAmount = fill;
        if (dotRect != null) dotRect.anchoredPosition = SongLoadingClockTimeline.DotPosition(fill, GameConstants.SongLoading.ClockRadius);
    }

    private static void SetScale(RectTransform rect, float scale)
    {
        if (rect == null) return;
        Vector3 s = new Vector3(scale, scale, 1f);
        if (rect.localScale != s) rect.localScale = s;
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null && group.alpha != alpha) group.alpha = alpha;
    }

    private static void PaintAlpha(Graphic graphic, Color color, float alpha)
    {
        if (graphic == null) return;
        color.a *= alpha;
        if (graphic.color != color) graphic.color = color;
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
