using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채보 그리드 표시. EditorScene.unity의 GridArea에 붙이고, EditorScene이 Init()을 호출한다.
/// - 위로 갈수록 뒤 시간이다. 현재 위치 선은 그리드 아래쪽에 고정하고, 현재 beat가 바뀌면 그리드가 움직인다.
/// - 보이는 구간(위아래 여유 마디 포함)의 선과 마디 번호만 오브젝트 풀에서 꺼내 배치한다.
/// - 계산은 BeatGridMath에 두고, 여기서는 배치와 색만 맡는다.
/// - 높이는 시간 기준이다: y = (beat의 시간 − 현재 beat의 시간) × PixelsPerSecond × 그리드 높이 배율 (박 ↔ 시간은 Timing).
/// - 선 색·굵기는 UITheme 에디터 묶음(기획서 3-6 「에디터 라벤더 톤 개편 (11/19)」, 음악상 길이 기준)에서 읽는다:
///   마디 흰 70% 2px · 1박 분홍 50% · 8분 하늘 32% · 16분 하늘 14% · 32분 하늘 8% · 셋잇단 남보라 30% · 12분 남보라 14% · 나머지 흰 6% · 라인 구분선 흰 10%.
/// - 현재 위치 선은 판정선과 같은 분홍 → 보라 가로 그라데이션(코드로 만든 텍스처) + 왼쪽 분홍 삼각형(씬), 박마다 흰 덮개(Img_CurrentLineFlash)로 밝아진다.
/// - 곡 시작 전(0박 = 마디 1 아래)은 흰 4% 빗금(Img_PreStartHatch, 코드로 만든 반복 텍스처)으로 덮는다.
/// </summary>
public class ChartGridView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("검은 그리드 영역 (Img_Grid). 선 위치의 기준이다")]
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private RectTransform laneDividerParent;
    [SerializeField] private RectTransform beatLineParent;
    [SerializeField] private RectTransform measureLabelParent;
    [SerializeField] private RectTransform currentLine;

    [Header("프리팹")]
    [SerializeField] private Image beatLinePrefab;
    [SerializeField] private TMP_Text measureNumberPrefab;

    [Header("배치")]
    [Tooltip("현재 위치 선의 높이 (그리드 아래에서부터의 비율)")]
    [Range(0f, 1f)] [SerializeField] private float currentLineRatio = 0.15f;
    [Tooltip("보이는 구간 위아래로 미리 배치할 마디 수")]
    [Min(0)] [SerializeField] private int marginMeasures = 1;
    [Tooltip("마디 번호 표시 형식. {0}에 마디 번호가 들어간다")]
    [SerializeField] private string measureLabelFormat = "마디 {0}";


    [Header("현재 위치 선 · 곡 시작 전 빗금")]
    [Tooltip("현재 위치 선 위에 겹친 흰 덮개 (Img_CurrentLineFlash). 박마다 알파가 오른다")]
    [SerializeField] private Image currentLineFlash;
    [Tooltip("현재 위치 선 왼쪽 분홍 삼각형 (Txt_CurrentLineArrow)")]
    [SerializeField] private Graphic currentLineArrow;
    [Tooltip("곡 시작 전 빗금 (Img_PreStartHatch, RawImage). 그리드 아래 끝 ~ 0박 높이")]
    [SerializeField] private RawImage preStartHatch;

    /// <summary>빗금 텍스처 한 칸 크기 (px)</summary>
    private const int HatchTextureSize = 16;
    /// <summary>현재 위치 선 그라데이션 텍스처 가로 칸 수</summary>
    private const int CurrentLineTextureWidth = 64;

    private Image currentLineImage;
    private Texture2D currentLineTexture;
    private Sprite currentLineSprite;
    private Texture2D hatchTexture;

    private ComponentPool<Image> linePool;
    private ComponentPool<TMP_Text> labelPool;
    private bool isInitialized;

    /// <summary>곡 전체 길이 (박)</summary>
    public float TotalBeats { get; private set; }

    /// <summary>현재 위치 선이 가리키는 beat</summary>
    public float CurrentBeat { get; private set; }

    /// <summary>박자 스냅 n (1/n)</summary>
    public int SnapDivision { get; private set; }

    /// <summary>그리드 높이 배율</summary>
    public float GridHeightScale { get; private set; }

    /// <summary>높이 계산에 쓰는 박 ↔ 시간 변환 (편집 중인 채보의 BPM·offset). 1초 = PixelsPerSecond × 배율</summary>
    public TimingMap Timing { get; private set; }

    /// <summary>곡 전체 마디 수</summary>
    public int TotalMeasures => Mathf.Max(1, Mathf.CeilToInt(TotalBeats / GameConstants.Editor.BeatsPerMeasure));

    /// <summary>현재 beat가 속한 마디 번호 (1부터, 마지막 마디를 넘지 않음)</summary>
    public int CurrentMeasure => Mathf.Min(BeatGridMath.BeatToMeasure(CurrentBeat), TotalMeasures);

    /// <summary>지금 켜져 있는 박자선 수 (확인용)</summary>
    public int ActiveLineCount => linePool != null ? linePool.ActiveCount : 0;

    /// <summary>박자선 풀의 전체 인스턴스 수 (확인용)</summary>
    public int PooledLineCount => linePool != null ? linePool.TotalCount : 0;

    /// <summary>현재 beat·스냅·높이 배율·크기가 바뀌어 선을 다시 배치한 뒤 호출된다 (노트 표시 갱신용)</summary>
    public event System.Action OnViewChanged;

    /// <summary>그리드 영역 (Img_Grid). 클릭 위치 변환의 기준이다</summary>
    public RectTransform GridRect => gridRect;

    /// <summary>현재 위치 선의 높이 (그리드 아래에서부터, 그리드 로컬 단위)</summary>
    public float CurrentLineY => gridRect.rect.height * currentLineRatio;

    /// <summary>라인 한 칸의 폭 (그리드 로컬 단위)</summary>
    public float LaneWidth => gridRect.rect.width / GameConstants.Editor.LaneCount;

    /// <summary>보이는 구간 위아래로 미리 배치하는 박 수</summary>
    public float MarginBeats => marginMeasures * GameConstants.Editor.BeatsPerMeasure;

    /// <summary>
    /// beat의 y 위치 (그리드 아래에서부터). 선·노트 배치에 같은 값을 쓴다.
    /// </summary>
    public float BeatToLocalY(float beat)
    {
        return CurrentLineY + BeatGridMath.BeatToY(Timing, beat, CurrentBeat, GridHeightScale);
    }

    /// <summary>
    /// y 위치(그리드 아래에서부터)의 beat. BeatToLocalY의 반대 변환이다.
    /// </summary>
    public float LocalYToBeat(float y)
    {
        return BeatGridMath.YToBeat(Timing, y - CurrentLineY, CurrentBeat, GridHeightScale);
    }

    /// <summary>
    /// 지금 화면에 배치하는 구간(위아래 여유 포함)의 beat 범위. 0 ~ 곡 길이로 제한한다.
    /// </summary>
    public void GetVisibleBeatRange(out float minBeat, out float maxBeat)
    {
        // 그리드 아래 끝(y = 0)과 위 끝(y = 높이)의 박에 여유 마디를 더한다
        minBeat = Mathf.Max(0f, LocalYToBeat(0f) - MarginBeats);
        maxBeat = Mathf.Min(TotalBeats, LocalYToBeat(gridRect.rect.height) + MarginBeats);
    }

    /// <summary>
    /// 곡 길이와 박 ↔ 시간 변환으로 그리드를 초기화한다. 0박, 기본 스냅, 기본 높이에서 시작한다.
    /// </summary>
    /// <param name="timing">높이 계산에 쓰는 박 ↔ 시간 변환 (편집 중인 채보의 BPM·offset). null이면 120 BPM으로 본다</param>
    public void Init(float totalBeats, TimingMap timing)
    {
        TotalBeats = Mathf.Max(0f, totalBeats);
        if (timing == null) Debug.LogWarning("[ChartGridView] 박 ↔ 시간 변환이 없어 120 BPM으로 그립니다.", this);
        Timing = timing ?? TimingMap.Single(120f, 0);
        CurrentBeat = 0f;
        SnapDivision = GameConstants.Editor.DefaultSnapDivision;
        GridHeightScale = GameConstants.Editor.DefaultGridHeight;

        if (currentLineImage == null && currentLine != null) currentLineImage = currentLine.GetComponent<Image>();
        ApplyThemeGraphics();
        SetCurrentLineHighlight(0f);

        if (linePool == null) linePool = new ComponentPool<Image>(beatLinePrefab, beatLineParent);
        if (labelPool == null) labelPool = new ComponentPool<TMP_Text>(measureNumberPrefab, measureLabelParent);

        CreateLaneDividers();
        PrewarmPools();

        isInitialized = true;
        Refresh();
    }

    /// <summary>
    /// 박자 스냅을 바꾼다. 현재 beat는 그대로 두고, 고른 스냅 단위까지의 선만 다시 그린다.
    /// </summary>
    public void SetSnap(int snapDivision)
    {
        if (snapDivision <= 0)
        {
            Debug.LogWarning($"[ChartGridView] 잘못된 박자 스냅입니다: {snapDivision}", this);
            return;
        }

        SnapDivision = snapDivision;
        Refresh();
    }

    /// <summary>
    /// 그리드 높이 배율을 바꾼다. 허용 범위로 제한한다.
    /// </summary>
    public void SetGridHeight(float scale)
    {
        GridHeightScale = Mathf.Clamp(scale, GameConstants.Editor.MinGridHeight, GameConstants.Editor.MaxGridHeight);
        Refresh();
    }

    /// <summary>
    /// 현재 beat를 바꾼다. 0 ~ 곡 길이로 제한한다.
    /// </summary>
    public void SetCurrentBeat(float beat)
    {
        CurrentBeat = Mathf.Clamp(beat, 0f, TotalBeats);
        Refresh();
    }

    /// <summary>
    /// 높이 계산에 쓰는 박 ↔ 시간 변환을 바꾼다 (BPM·offset 조정 도구). 선이 촘촘해질 수 있으므로 선 풀도 다시 채운다.
    /// </summary>
    public void SetTiming(TimingMap timing)
    {
        if (timing == null)
        {
            Debug.LogWarning("[ChartGridView] 박 ↔ 시간 변환이 없습니다.", this);
            return;
        }

        Timing = timing;
        if (!isInitialized) return;

        PrewarmPools();
        Refresh();
    }

    /// <summary>
    /// 곡 끝 박을 바꾼다 (곡 BPM·offset을 바꿨을 때). 현재 beat는 새 범위로 제한한다.
    /// </summary>
    public void SetTotalBeats(float totalBeats)
    {
        TotalBeats = Mathf.Max(0f, totalBeats);
        CurrentBeat = Mathf.Clamp(CurrentBeat, 0f, TotalBeats);
        if (isInitialized) Refresh();
    }

    /// <summary>
    /// 현재 위치 선의 밝기를 바꾼다 (재생 중 박마다 밝아지게). 0은 원래 색, 1은 강조 색이다.
    /// </summary>
    public void SetCurrentLineHighlight(float amount)
    {
        if (currentLineFlash == null) return;
        Color flash = UITheme.Current.editorCurrentLineFlash;
        currentLineFlash.color = UITheme.WithAlpha(flash, flash.a * Mathf.Clamp01(amount));
    }

    /// <summary>지금 현재 위치 선 흰 덮개 알파 (확인용)</summary>
    public float CurrentLineFlashAlpha => currentLineFlash != null ? currentLineFlash.color.a : 0f;

    /// <summary>현재 위치 선 그라데이션 색 (t = 0 왼쪽 ~ 1 오른쪽, 확인용)</summary>
    public Color CurrentLineColorAt(float t) => currentLineTexture != null ? currentLineTexture.GetPixelBilinear(Mathf.Clamp01(t), 0.5f) : Color.clear;

    /// <summary>곡 시작 전 빗금 높이 (확인용)</summary>
    public float PreStartHatchHeight => preStartHatch != null && preStartHatch.gameObject.activeSelf ? preStartHatch.rectTransform.rect.height : 0f;

    /// <summary>선 종류의 모양 (UITheme 에디터 묶음, 확인용)</summary>
    public static GridLineStyle StyleOf(GridLineType type) => GetStyle(type);

    /// <summary>
    /// 현재 위치 선 그라데이션과 곡 시작 전 빗금 텍스처를 UITheme 값으로 만든다 (Init에서).
    /// </summary>
    private void ApplyThemeGraphics()
    {
        UITheme theme = UITheme.Current;
        if (currentLineImage != null)
        {
            if (currentLineTexture == null)
            {
                currentLineTexture = new Texture2D(CurrentLineTextureWidth, 1, TextureFormat.RGBA32, false)
                {
                    name = "EditorCurrentLineGradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                currentLineSprite = Sprite.Create(currentLineTexture, new Rect(0, 0, CurrentLineTextureWidth, 1), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                currentLineSprite.name = "EditorCurrentLineGradient";
                currentLineSprite.hideFlags = HideFlags.DontSave;
            }
            Color[] pixels = new Color[CurrentLineTextureWidth];
            for (int x = 0; x < CurrentLineTextureWidth; x++)
                pixels[x] = Color.Lerp(theme.editorCurrentLineStart, theme.editorCurrentLineEnd, x / (float)(CurrentLineTextureWidth - 1));
            currentLineTexture.SetPixels(pixels);
            currentLineTexture.Apply(false);
            currentLineImage.sprite = currentLineSprite;
            currentLineImage.type = Image.Type.Simple;
            currentLineImage.color = Color.white;
        }
        if (currentLineArrow != null) currentLineArrow.color = theme.editorCurrentLineStart;

        if (preStartHatch != null)
        {
            if (hatchTexture == null)
            {
                hatchTexture = new Texture2D(HatchTextureSize, HatchTextureSize, TextureFormat.RGBA32, false)
                {
                    name = "EditorPreStartHatch", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
            }
            // 45도 빗금: 한 칸에 선 하나 (폭 3px)
            Color[] pixels = new Color[HatchTextureSize * HatchTextureSize];
            for (int y = 0; y < HatchTextureSize; y++)
                for (int x = 0; x < HatchTextureSize; x++)
                    pixels[y * HatchTextureSize + x] = ((x + y) % HatchTextureSize) < 3 ? theme.editorGridHatch : Color.clear;
            hatchTexture.SetPixels(pixels);
            hatchTexture.Apply(false);
            preStartHatch.texture = hatchTexture;
            preStartHatch.color = Color.white;
            preStartHatch.raycastTarget = false;
        }
    }

    /// <summary>곡 시작 전 빗금을 그리드 아래 끝 ~ 0박 높이로 맞춘다</summary>
    private void RefreshPreStartHatch()
    {
        if (preStartHatch == null) return;
        float top = Mathf.Min(BeatToLocalY(0f), gridRect.rect.height);
        bool visible = top > 0.5f;
        if (preStartHatch.gameObject.activeSelf != visible) preStartHatch.gameObject.SetActive(visible);
        if (!visible) return;

        RectTransform rect = preStartHatch.rectTransform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, top);
        float width = gridRect.rect.width;
        // 빗금 무늬가 0박 선에 붙어 같이 움직이게 위쪽 끝을 기준으로 둔다
        preStartHatch.uvRect = new Rect(0f, -top / HatchTextureSize, width / HatchTextureSize, top / HatchTextureSize);
    }

    private void OnDestroy()
    {
        if (currentLineSprite != null) DestroyGenerated(currentLineSprite);
        if (currentLineTexture != null) DestroyGenerated(currentLineTexture);
        if (hatchTexture != null) DestroyGenerated(hatchTexture);
    }

    private static void DestroyGenerated(Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    /// <summary>
    /// 창 크기가 바뀌면 보이는 구간이 달라지므로 다시 배치한다.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (isInitialized) Refresh();
    }

    /// <summary>
    /// 보이는 구간의 박자선과 마디 번호를 풀에서 꺼내 배치하고, 나머지는 끈다.
    /// </summary>
    private void Refresh()
    {
        currentLine.anchoredPosition = new Vector2(currentLine.anchoredPosition.x, CurrentLineY);
        RefreshPreStartHatch();

        GetVisibleBeatRange(out float minBeat, out float maxBeat);

        // 선은 0박부터 센 스냅 칸 번호(정수)로 순회해 부동소수 오차 없이 종류를 판정한다
        int n = SnapDivision;
        int firstIndex = Mathf.CeilToInt(minBeat * n - 0.0001f);
        int lastIndex = Mathf.FloorToInt(maxBeat * n + 0.0001f);

        linePool.BeginFrame();
        labelPool.BeginFrame();

        for (int i = firstIndex; i <= lastIndex; i++)
        {
            float beat = (float)i / n;
            float y = BeatToLocalY(beat);
            GridLineType type = BeatGridMath.GetLineType(i, n);
            GridLineStyle style = GetStyle(type);

            Image line = linePool.Get();
            RectTransform lineRect = line.rectTransform;
            lineRect.anchoredPosition = new Vector2(0f, y);
            lineRect.sizeDelta = new Vector2(0f, style.thickness);
            line.color = style.color;

            // 곡 끝 박에 걸린 마디선에는 번호를 붙이지 않는다 (그 마디는 곡에 없다)
            if (type == GridLineType.Measure && beat < TotalBeats)
            {
                TMP_Text label = labelPool.Get();
                label.color = UITheme.Current.editorMeasureNumber;
                label.rectTransform.anchoredPosition = new Vector2(label.rectTransform.anchoredPosition.x, y);
                // 재생 중 매 프레임 불리므로 문자열을 새로 만들지 않는 SetText를 쓴다
                label.SetText(measureLabelFormat, BeatGridMath.BeatToMeasure(beat));
            }
        }

        linePool.EndFrame();
        labelPool.EndFrame();

        OnViewChanged?.Invoke();
    }

    /// <summary>
    /// 라인 수에 맞춰 세로 구분선을 만든다. 라인 사이마다 하나씩 (라인 수 − 1개).
    /// </summary>
    private void CreateLaneDividers()
    {
        for (int i = laneDividerParent.childCount - 1; i >= 0; i--)
            Destroy(laneDividerParent.GetChild(i).gameObject);

        int laneCount = GameConstants.Editor.LaneCount;
        for (int lane = 1; lane < laneCount; lane++)
        {
            Image divider = Instantiate(beatLinePrefab, laneDividerParent);
            divider.name = "Img_LaneDivider";
            GridLineStyle dividerStyle = UITheme.Current.editorLaneDivider;
            divider.color = dividerStyle.color;

            RectTransform rect = divider.rectTransform;
            float x = (float)lane / laneCount;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(dividerStyle.thickness, 0f);
            divider.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 가장 촘촘한 경우(최소 높이 배율, 가장 잘게 나눈 스냅)에 필요한 수만큼 미리 만든다.
    /// 스크롤 중에는 새로 만들지 않도록 하기 위해서다.
    /// </summary>
    private void PrewarmPools()
    {
        int maxSnap = 1;
        foreach (GameConstants.Editor.SnapOption option in GameConstants.Editor.SnapOptions)
            maxSnap = Mathf.Max(maxSnap, option.Division);

        // 풀 크기 계산용: 가장 높은 BPM 구간에서 가장 낮은 그리드 높이 배율일 때의 1박 높이 (화면 규칙이 아니다)
        float poolMinBeatHeight = BeatGridMath.GetBeatHeight(Timing.MaxBpm, GameConstants.Editor.MinGridHeight);
        float visibleBeats = gridRect.rect.height / poolMinBeatHeight
                             + 2f * marginMeasures * GameConstants.Editor.BeatsPerMeasure;

        linePool.Prewarm(Mathf.CeilToInt(visibleBeats * maxSnap) + 2);
        labelPool.Prewarm(Mathf.CeilToInt(visibleBeats / GameConstants.Editor.BeatsPerMeasure) + 2);
    }

    /// <summary>선 종류의 모양 (UITheme 에디터 묶음, 음악상 길이 기준: Half = 8분, Quarter = 16분, Eighth = 32분)</summary>
    private static GridLineStyle GetStyle(GridLineType type)
    {
        UITheme theme = UITheme.Current;
        switch (type)
        {
            case GridLineType.Measure: return theme.editorLineMeasure;
            case GridLineType.Beat: return theme.editorLineBeat;
            case GridLineType.Half: return theme.editorLine8th;
            case GridLineType.Quarter: return theme.editorLine16th;
            case GridLineType.Eighth: return theme.editorLine32nd;
            case GridLineType.Third: return theme.editorLineTriplet;
            case GridLineType.Twelfth: return theme.editorLineTripletFine;
            default: return theme.editorLineOther;
        }
    }
}
