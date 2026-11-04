using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채보 그리드 표시. EditorScene.unity의 GridArea에 붙이고, EditorScene이 Init()을 호출한다.
/// - 위로 갈수록 뒤 시간이다. 현재 위치 선은 그리드 아래쪽에 고정하고, 현재 beat가 바뀌면 그리드가 움직인다.
/// - 보이는 구간(위아래 여유 마디 포함)의 선과 마디 번호만 오브젝트 풀에서 꺼내 배치한다.
/// - 계산은 BeatGridMath에 두고, 여기서는 배치와 색만 맡는다.
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

    [Header("선 모양")]
    [SerializeField] private GridLineStyle measureStyle = new GridLineStyle(Color.white, 4f);
    [SerializeField] private GridLineStyle beatStyle = new GridLineStyle(new Color(1f, 1f, 1f, 0.7f), 2f);
    [SerializeField] private GridLineStyle halfStyle = new GridLineStyle(new Color32(0xE5, 0x39, 0x35, 0xFF), 2f);
    [SerializeField] private GridLineStyle quarterStyle = new GridLineStyle(new Color32(0x1E, 0x88, 0xE5, 0xFF), 2f);
    [SerializeField] private GridLineStyle eighthStyle = new GridLineStyle(new Color32(0xFD, 0xD8, 0x35, 0xFF), 2f);
    [SerializeField] private GridLineStyle sixteenthStyle = new GridLineStyle(new Color32(0x75, 0x75, 0x75, 0xFF), 2f);
    [SerializeField] private GridLineStyle thirdStyle = new GridLineStyle(new Color32(0xAB, 0x47, 0xBC, 0xFF), 2f);
    [SerializeField] private GridLineStyle twelfthStyle = new GridLineStyle(new Color32(0x6A, 0x1B, 0x9A, 0xFF), 2f);
    [SerializeField] private GridLineStyle laneDividerStyle = new GridLineStyle(new Color32(0x5A, 0x5A, 0x5E, 0xFF), 2f);

    [Header("재생")]
    [Tooltip("재생 중 박마다 현재 위치 선을 밝게 할 때의 색")]
    [SerializeField] private Color currentLineFlashColor = new Color32(0xFF, 0xF3, 0xB0, 0xFF);

    private Image currentLineImage;
    private Color currentLineNormalColor;

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
        return CurrentLineY + BeatGridMath.BeatToY(beat, CurrentBeat, GameConstants.Editor.PixelsPerBeat, GridHeightScale);
    }

    /// <summary>
    /// y 위치(그리드 아래에서부터)의 beat. BeatToLocalY의 반대 변환이다.
    /// </summary>
    public float LocalYToBeat(float y)
    {
        return BeatGridMath.YToBeat(y - CurrentLineY, CurrentBeat, GameConstants.Editor.PixelsPerBeat, GridHeightScale);
    }

    /// <summary>
    /// 지금 화면에 배치하는 구간(위아래 여유 포함)의 beat 범위. 0 ~ 곡 길이로 제한한다.
    /// </summary>
    public void GetVisibleBeatRange(out float minBeat, out float maxBeat)
    {
        float height = gridRect.rect.height;
        float beatHeight = GameConstants.Editor.PixelsPerBeat * GridHeightScale;
        minBeat = Mathf.Max(0f, CurrentBeat - CurrentLineY / beatHeight - MarginBeats);
        maxBeat = Mathf.Min(TotalBeats, CurrentBeat + (height - CurrentLineY) / beatHeight + MarginBeats);
    }

    /// <summary>
    /// 곡 길이로 그리드를 초기화한다. 0박, 기본 스냅, 기본 높이에서 시작한다.
    /// </summary>
    public void Init(float totalBeats)
    {
        TotalBeats = Mathf.Max(0f, totalBeats);
        CurrentBeat = 0f;
        SnapDivision = GameConstants.Editor.DefaultSnapDivision;
        GridHeightScale = GameConstants.Editor.DefaultGridHeight;

        if (currentLineImage == null && currentLine != null)
        {
            currentLineImage = currentLine.GetComponent<Image>();
            if (currentLineImage != null) currentLineNormalColor = currentLineImage.color;
        }
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
        if (currentLineImage == null) return;
        currentLineImage.color = Color.Lerp(currentLineNormalColor, currentLineFlashColor, Mathf.Clamp01(amount));
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
            divider.color = laneDividerStyle.color;

            RectTransform rect = divider.rectTransform;
            float x = (float)lane / laneCount;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(laneDividerStyle.thickness, 0f);
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

        float minBeatHeight = GameConstants.Editor.PixelsPerBeat * GameConstants.Editor.MinGridHeight;
        float visibleBeats = gridRect.rect.height / minBeatHeight
                             + 2f * marginMeasures * GameConstants.Editor.BeatsPerMeasure;

        linePool.Prewarm(Mathf.CeilToInt(visibleBeats * maxSnap) + 2);
        labelPool.Prewarm(Mathf.CeilToInt(visibleBeats / GameConstants.Editor.BeatsPerMeasure) + 2);
    }

    private GridLineStyle GetStyle(GridLineType type)
    {
        switch (type)
        {
            case GridLineType.Measure: return measureStyle;
            case GridLineType.Beat: return beatStyle;
            case GridLineType.Half: return halfStyle;
            case GridLineType.Quarter: return quarterStyle;
            case GridLineType.Eighth: return eighthStyle;
            case GridLineType.Sixteenth: return sixteenthStyle;
            case GridLineType.Third: return thirdStyle;
            case GridLineType.Twelfth: return twelfthStyle;
            default: return sixteenthStyle;
        }
    }
}
