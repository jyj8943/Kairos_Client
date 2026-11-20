using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 로딩 오버레이 (기획서 3-8 「곡 로딩」, 와이어프레임 12_SongLoading). 곡 선택 시작 · 결과 다시 하기 → 인게임 사이에 덮는다.
/// - Managers_DontDestroy/UIManager 아래에 있어 씬이 바뀌어도 살아 있다. 정렬 순서는 Canvas_Overlay 팝업보다 위(GameConstants.SongLoading.SortOrder).
/// - Bootstrapper가 전역 매니저 Init 때 Init()을 한 번 부르고 숨겨 둔다.
/// - 흐름은 GameSceneManager.LoadWithSongLoading이 정한다: Open(진입 데이터, 카드를 먼저 채움) → 다음 프레임부터 열기 연출 0~1.1초(화면만) →
///   열기 끝 뒤 앞 씬 Exit · 다음 씬 로드 · Init → 정리(UnloadUnusedAssets · GC) → CloseRoutine() → 다음 씬 OnEnter.
/// - Init이 숨긴 채로 한 번 그려 둔다(PrewarmRoutine): 폰트 글자 · 랭크 이미지 · 배경 블러 RenderTexture · 첫 Canvas 그리기.
///   Open은 열기 연출을 시작하고, 끝까지 기다리려면 OpenRoutine을, 닫기는 CloseRoutine을 기다린다.
/// - 자식 (README 6절): Background(MenuBackground + Img_ClockPattern) · Img_Album(Mask) └ Img_AlbumCover · ScreenName(Txt_ScreenName + Txt_ScreenNameKo) ·
///   Panel(Img_PanelBack · Img_PanelFrame · Txt_Phrase · Txt_Title · Txt_Artist · BadgeRow · ChartRow · Records) · Img_SD · Img_Bubble └ Txt_Bubble ·
///   Loading(Img_LoadingClock + Txt_Loading) · Img_InputBlocker(맨 앞, 투명, 마우스를 막는다).
/// - 같은 진입 데이터(곡 · 채보 · 배속)와 기록(PlayerData)으로 카드를 그린다. 싱글톤에 값을 남기지 않는다.
/// - 불러오는 중 시계 바늘은 공용 UILoadingClock(Img_LoadingClock)이 돌린다.
/// - 시간은 unscaled 프레임 시각(Time.unscaledTimeAsDouble, unscaledDeltaTime을 더한 것과 같음)으로 센다 (로딩 중 timeScale · 씬 변화와 상관없이). 색은 UITheme에서 Init 때 칠한다 (씬 · 프리팹에 색을 직렬화하지 않는다).
/// - Img_SD는 SD 일러스트가 아직 없어 스프라이트 없이 투명하다. 스프라이트를 넣으면 흰색으로 보인다.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class Canvas_SongLoading : UIBase, ILoadingOverlay
{
    /// <summary>AP 랭크 이미지 높이 (다른 랭크는 Img_Rank의 56)</summary>
    public const float ApRankHeight = 54f;
    /// <summary>최대 콤보 글자와 FC 이미지 사이 (px)</summary>
    public const float FullComboGap = 12f;

    [Header("배경")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [SerializeField] private MenuBackground menuBackground;
    [SerializeField] private Image clockPattern;
    [Tooltip("마우스 클릭을 막는 투명 이미지 (맨 앞)")]
    [SerializeField] private Image inputBlocker;

    [Header("앨범 (Img_Album, 곡 선택 큰 앨범과 같은 자리)")]
    [SerializeField] private CanvasGroup albumGroup;
    [SerializeField] private Image albumFrame;
    [SerializeField] private Image albumCover;

    [Header("화면 이름")]
    [SerializeField] private CanvasGroup screenNameGroup;
    [SerializeField] private TMP_Text screenNameText;
    [SerializeField] private TMP_Text screenNameKoText;

    [Header("정보 판 (Panel)")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Image panelBack;
    [SerializeField] private Image panelFrame;
    [SerializeField] private Image phraseLine;
    [SerializeField] private TMP_Text phraseText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private Image difficultyBadge;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image badgeDivider;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private GameObject chartRow;
    [SerializeField] private TMP_Text chartLabel;
    [SerializeField] private TMP_Text chartNameText;

    [Header("기록 3칸 (Records)")]
    [SerializeField] private Image recordsBack;
    [SerializeField] private Image[] recordDividers;
    [SerializeField] private TMP_Text[] recordLabels;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private Img_Rank bestRank;
    [SerializeField] private Image bestRankEmpty;
    [SerializeField] private Image bestRankEmptyDash;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private Img_FullCombo fullCombo;

    [Header("SD · 말풍선 · 불러오는 중")]
    [SerializeField] private CanvasGroup sdGroup;
    [SerializeField] private Image sdImage;
    [SerializeField] private CanvasGroup bubbleGroup;
    [SerializeField] private Image bubbleBack;
    [SerializeField] private Image bubbleFrame;
    [SerializeField] private Image bubbleTail;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private Image loadingClock;
    [SerializeField] private Image loadingClockHand;
    [SerializeField] private TMP_Text loadingText;

    private Canvas canvas;
    private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();
    private RectTransform albumRect, panelRect, sdRect, bubbleRect;
    private bool isShown;
    private bool isClosing;
    private double openStart;
    private double closeStart;
    private Coroutine openRoutine;
    private bool openStarted;
    private bool isPrewarming;

    public override UILayer Layer => UILayer.Overlay;

    // 오버레이 규약 (ILoadingOverlay) 로그 이름: 곡 로딩 단계 이름은 바꾸지 않는다
    public string LogName => "곡 로딩";
    public string OpenStartStep => "열기 연출 0초";
    public string OpenEndStep => "열기 끝";
    public string OpeningPhase => "열기 0~1.1초";
    public string ClosingPhase => "닫기";
    public double MinSeconds => GameConstants.SongLoading.MinLoadSeconds;
    public bool MinFromOpenEnd => true;

    /// <summary>오버레이가 보이는 중이면 true (열기 시작 ~ 닫기 끝)</summary>
    public bool IsShown => isShown;

    /// <summary>열기 연출(1.1초) 재생 중이면 true</summary>
    public bool IsOpening => isShown && !isClosing && (!openStarted || OpenElapsed < GameConstants.SongLoading.OpenSeconds);

    /// <summary>열기 연출 0초를 센 뒤면 true (Open한 다음 프레임부터)</summary>
    public bool HasOpenStarted => isShown && openStarted;

    /// <summary>Init의 미리 그려 두기(블러 RenderTexture · 폰트 글자 · 첫 그리기)가 끝났으면 true</summary>
    public bool IsPrewarmed { get; private set; }

    /// <summary>닫기 연출 재생 중이면 true</summary>
    public bool IsClosing => isClosing;

    /// <summary>열기 시작부터 지난 시간 (초, unscaled 프레임 시각 = Time.unscaledDeltaTime을 더한 값과 같다). 닫기를 시작해도 계속 센다</summary>
    public float OpenElapsed => isShown && openStarted ? (float)(Time.unscaledTimeAsDouble - openStart) : 0f;

    /// <summary>마지막으로 적용한 모습 (확인용)</summary>
    public SongLoadingFrame Frame { get; private set; }

    /// <summary>마지막으로 그린 카드 값 (확인용)</summary>
    public SongLoadingInfo Info { get; private set; }

    /// <summary>확인용 참조</summary>
    public TMP_Text TitleText => titleText;
    public TMP_Text ChartNameText => chartNameText;
    public TMP_Text BubbleText => bubbleText;
    public TMP_Text BestScoreText => bestScoreText;
    public TMP_Text MaxComboText => maxComboText;
    public TMP_Text LevelText => levelText;
    public TMP_Text SpeedText => speedText;
    public TMP_Text DifficultyText => difficultyText;
    public Image DifficultyBadge => difficultyBadge;
    public GameObject ChartRow => chartRow;
    public Img_Rank BestRank => bestRank;
    public Image BestRankEmpty => bestRankEmpty;
    public Img_FullCombo FullCombo => fullCombo;
    public RectTransform AlbumRect => albumRect;
    public Image SdImage => sdImage;

    private void CacheComponents()
    {
        if (canvas != null) return;
        canvas = GetComponent<Canvas>();
        albumRect = albumGroup != null ? (RectTransform)albumGroup.transform : null;
        panelRect = panelGroup != null ? (RectTransform)panelGroup.transform : null;
        sdRect = sdGroup != null ? (RectTransform)sdGroup.transform : null;
        bubbleRect = bubbleGroup != null ? (RectTransform)bubbleGroup.transform : null;
        foreach (RectTransform r in new[] { albumRect, panelRect, sdRect, bubbleRect })
            if (r != null) basePositions[r] = r.anchoredPosition;
    }

    /// <summary>
    /// 색 · 고정 글자 · 배경을 맞추고 숨긴다. Bootstrapper가 전역 매니저 Init 때 한 번 부른다.
    /// </summary>
    public void Init()
    {
        CacheComponents();
        UITheme theme = UITheme.Current;
        if (menuBackground != null) menuBackground.Init();
        Paint(clockPattern, theme.songLoadingClockPattern);
        Paint(albumFrame, Color.white);
        Paint(albumCover, Color.white);
        if (inputBlocker != null) inputBlocker.color = Color.clear;

        SetText(screenNameText, GameConstants.SongLoading.ScreenName, theme.textPrimary);
        SetText(screenNameKoText, GameConstants.SongLoading.ScreenNameKo, theme.textDescription);

        Paint(panelBack, theme.panel);
        Paint(panelFrame, theme.songLoadingPanelBorder);
        Paint(phraseLine, theme.accent);
        SetText(phraseText, GameConstants.SongLoading.Phrase, theme.accent);
        Paint(titleText, theme.textPrimary);
        Paint(artistText, theme.textDescription);
        Paint(difficultyText, theme.onDifficultyText);
        Paint(levelText, theme.textPrimary);
        Paint(badgeDivider, theme.divider);
        SetText(speedLabel, GameConstants.SongLoading.SpeedLabel, theme.textDescription);
        Paint(speedText, theme.textPrimary);
        SetText(chartLabel, GameConstants.SongLoading.ChartLabel, theme.textDescription);
        Paint(chartNameText, theme.textPrimary);

        Paint(recordsBack, theme.songLoadingRecordCell);
        if (recordDividers != null) foreach (Image d in recordDividers) Paint(d, theme.divider);
        string[] labels = { GameConstants.SongLoading.BestScoreLabel, GameConstants.SongLoading.BestRankLabel, GameConstants.SongLoading.MaxComboLabel };
        if (recordLabels != null)
            for (int i = 0; i < recordLabels.Length && i < labels.Length; i++) SetText(recordLabels[i], labels[i], theme.textDescription);
        Paint(bestScoreText, theme.textPrimary);
        Paint(maxComboText, theme.textPrimary);
        Paint(bestRankEmpty, theme.rankEmptyFill);
        Paint(bestRankEmptyDash, theme.rankEmptyDash);

        Paint(bubbleBack, theme.songLoadingBubble);
        Paint(bubbleTail, theme.songLoadingBubble);
        Paint(bubbleFrame, theme.songLoadingBubbleBorder);
        SetText(bubbleText, GameConstants.SongLoading.Bubble, theme.textPrimary);
        Paint(loadingClock, theme.songLoadingClock);
        Paint(loadingClockHand, theme.songLoadingClock);
        SetText(loadingText, GameConstants.SongLoading.Loading, theme.textDescription);
        ShowSd(sdImage != null ? sdImage.sprite : null);

        Hide();
        StartCoroutine(PrewarmRoutine());
    }

    /// <summary>미리 채워 둘 글자 (숫자 · 영문 · 기호, 고정 문구는 따로 더한다)</summary>
    private const string PrewarmCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .,:;/-+x×()[]'\"!?·—―…";

    /// <summary>
    /// 첫 Open 렉을 줄이려고 숨긴 채로 한 번 그려 둔다 (Init에서 한 번):
    /// 쓰는 폰트에 글자(숫자 · 영문 · 고정 문구 · 곡 제목 · 아티스트)를 미리 채우고, 랭크 · FC 이미지를 읽고,
    /// 첫 곡으로 카드와 배경 블러(RenderTexture)를 만든 뒤 모든 알파 0으로 Canvas를 한 프레임 그리고 끈다.
    /// </summary>
    private IEnumerator PrewarmRoutine()
    {
        isPrewarming = true;
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        System.Text.StringBuilder chars = new System.Text.StringBuilder(PrewarmCharacters);
        chars.Append(GameConstants.SongLoading.ScreenName).Append(GameConstants.SongLoading.ScreenNameKo).Append(GameConstants.SongLoading.Phrase)
            .Append(GameConstants.SongLoading.SpeedLabel).Append(GameConstants.SongLoading.ChartLabel).Append(GameConstants.SongLoading.BestScoreLabel)
            .Append(GameConstants.SongLoading.BestRankLabel).Append(GameConstants.SongLoading.MaxComboLabel).Append(GameConstants.SongLoading.Bubble)
            .Append(GameConstants.SongLoading.Loading).Append(GameConstants.Play.ChartBadgeCustomText).Append(GameConstants.SongSelect.EmptyValue);
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

        foreach (PlayRank rank in System.Enum.GetValues(typeof(PlayRank))) RankSprites.Get(rank);
        Sprite fullComboSprite = RankSprites.FullCombo;

        if (first != null && !isShown)
        {
            SetContent(new SongLoadingInfo
            {
                Title = first.Title, Artist = first.Artist, BadgeText = GameConstants.Chart.DifficultyNormal, LevelText = "Lv. 0", SpeedText = "x0.0",
                ChartName = string.Empty, Record = SongSelectRecordFormat.Build(null)
            }, first);
        }
        double prepareMs = watch.Elapsed.TotalMilliseconds;

        // 모든 알파 0으로 한 프레임 그린다 (보이지 않음, 입력도 막지 않음)
        if (!isShown)
        {
            canvas.enabled = true;
            Apply(new SongLoadingFrame());
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!isShown) canvas.enabled = false;
        }
        isPrewarming = false;
        IsPrewarmed = true;
        Debug.Log($"[Canvas_SongLoading] 미리 그려 두기: 폰트 {fonts.Count}개에 글자 {text.Length}자, 랭크 · FC 이미지({(fullComboSprite != null ? "있음" : "없음")}) · 블러 · 카드 준비 {prepareMs:0.0}ms, 숨긴 채 한 프레임 그림", this);
    }

    /// <summary>SD 일러스트를 넣는다 (없으면 투명). 아직 일러스트가 없어 Init이 빈 채로 부른다</summary>
    public void ShowSd(Sprite sprite)
    {
        if (sdImage == null) return;
        sdImage.sprite = sprite;
        sdImage.preserveAspect = true;
        sdImage.color = sprite != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// 진입 데이터로 카드(앨범 · 랭크 이미지 · 글자 · 배경 일러스트)를 먼저 채우고 알파 0으로 보이기 시작한다 (Enter 프레임).
    /// 열기 연출 0초는 그다음 프레임부터 센다 (채우는 프레임의 시간이 연출에 섞이지 않게). 끝까지 기다리려면 OpenRoutine을 쓴다.
    /// </summary>
    public void Open(InGameEntryData entry)
    {
        CacheComponents();
        SetContent(entry);
        isShown = true;
        isClosing = false;
        openStarted = false;
        canvas.enabled = true;
        if (inputBlocker != null) inputBlocker.raycastTarget = true;
        Apply(SongLoadingTimeline.EvaluateOpen(0f));
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(OpenRoutine());
    }

    /// <summary>열기 연출이 끝날 때까지 (1.1초) 기다린다</summary>
    public IEnumerator OpenRoutine()
    {
        // 채운 프레임 다음부터 0초
        yield return null;
        openStart = Time.unscaledTimeAsDouble;
        openStarted = true;
        while (isShown && !isClosing && OpenElapsed < GameConstants.SongLoading.OpenSeconds)
        {
            Apply(SongLoadingTimeline.EvaluateOpen(OpenElapsed));
            yield return null;
        }
        if (isShown && !isClosing) Apply(SongLoadingTimeline.Shown);
        openRoutine = null;
    }

    /// <summary>닫기 연출을 하고 (0.64초) 숨긴다. 끝날 때까지 기다릴 수 있다</summary>
    public IEnumerator CloseRoutine()
    {
        if (!isShown) yield break;
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = null;
        isClosing = true;
        closeStart = Time.unscaledTimeAsDouble;
        float closeElapsed = 0f;
        while (closeElapsed < GameConstants.SongLoading.CloseSeconds)
        {
            Apply(SongLoadingTimeline.EvaluateClose(closeElapsed));
            yield return null;
            closeElapsed = (float)(Time.unscaledTimeAsDouble - closeStart);
        }
        Hide();
    }

    /// <summary>숨긴다 (연출 없이)</summary>
    private void Hide()
    {
        CacheComponents();
        isShown = false;
        isClosing = false;
        openStarted = false;
        if (inputBlocker != null) inputBlocker.raycastTarget = false;
        Apply(new SongLoadingFrame());
        canvas.enabled = false;
    }

    /// <summary>진입 데이터로 곡 · 채보 · 기록을 찾아 카드를 그린다</summary>
    public void SetContent(InGameEntryData entry)
    {
        CacheComponents();
        SongData song = entry != null && SongManager.HasInstance ? GameManager.Songs.Find(entry.songId) : null;
        ChartData chart = FindChart(entry);
        ChartRecordData record = chart != null && !string.IsNullOrEmpty(chart.chartId) && PlayerDataManager.HasInstance
            ? GameManager.PlayerData.FindRecord(chart.chartId) : null;
        SetContent(SongLoadingInfoRules.Build(entry, song, chart, record), song);
    }

    /// <summary>카드 값으로 그린다 (확인용 테스트가 상태별로 직접 부른다)</summary>
    public void SetContent(SongLoadingInfo info, SongData song)
    {
        CacheComponents();
        Info = info;
        UITheme theme = UITheme.Current;
        Sprite cover = song != null ? song.Cover : null;
        if (albumCover != null)
        {
            albumCover.sprite = cover;
            albumCover.enabled = cover != null;
        }
        if (albumFrame != null) albumFrame.color = cover != null ? Color.white : theme.emptyCover;
        if (menuBackground != null) menuBackground.SetIllustration(song != null ? song.Background : null, true);

        titleText.text = info.Title;
        artistText.text = info.Artist;
        difficultyText.text = info.BadgeText;
        difficultyBadge.color = info.IsCustom ? theme.difficultyCustom : theme.GetDifficulty(info.BadgeText);
        levelText.text = info.LevelText;
        speedText.text = info.SpeedText;
        chartRow.SetActive(info.ShowChartName);
        chartNameText.text = info.ChartName;

        RecordTexts record = info.Record;
        bestScoreText.text = record.Score;
        maxComboText.text = record.Combo;
        bestRank.Show(record.HasRecord, record.Rank);
        if (record.HasRecord && record.Rank == PlayRank.AP)
        {
            RectTransform r = (RectTransform)bestRank.transform;
            r.sizeDelta *= ApRankHeight / r.sizeDelta.y;
        }
        fullCombo.SetFullCombo(record.HasRecord && record.IsFullCombo);
        if (fullCombo.IsShown)
        {
            RectTransform comboRect = maxComboText.rectTransform;
            float width = maxComboText.GetPreferredValues(record.Combo, float.PositiveInfinity, float.PositiveInfinity).x;
            RectTransform fc = (RectTransform)fullCombo.transform;
            fc.anchoredPosition = new Vector2(comboRect.anchoredPosition.x + width + FullComboGap, fc.anchoredPosition.y);
        }

        // 뱃지 줄 · 말풍선은 글자 폭에 맞춘 레이아웃이라 바로 다시 잰다
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)levelText.transform.parent);
        if (bubbleRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
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

    /// <summary>계산값을 오브젝트에 적용한다</summary>
    private void Apply(SongLoadingFrame f)
    {
        Frame = f;
        SetAlpha(backgroundGroup, f.BackgroundAlpha);
        SetAlpha(albumGroup, f.AlbumAlpha);
        SetAlpha(screenNameGroup, f.ScreenNameAlpha);
        SetAlpha(panelGroup, f.PanelAlpha);
        SetAlpha(sdGroup, f.SdAlpha);
        SetAlpha(loadingGroup, f.LoadingAlpha);
        SetAlpha(bubbleGroup, f.BubbleAlpha);
        Move(albumRect, f.AlbumOffsetX, 0f);
        Move(panelRect, f.PanelOffsetX, 0f);
        Move(sdRect, 0f, f.SdOffsetY);
        Move(bubbleRect, f.BubbleOffsetX, f.BubbleOffsetY);
    }

    private void Move(RectTransform rect, float x, float y)
    {
        if (rect == null || !basePositions.TryGetValue(rect, out Vector2 basePosition)) return;
        rect.anchoredPosition = basePosition + new Vector2(x, y);
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
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
