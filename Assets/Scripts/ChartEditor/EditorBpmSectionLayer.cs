using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그리드의 BPM 구간선과 라벨 표시. EditorScene.unity GridArea 아래 "BpmSectionLayer"(RectMask2D, Img_Grid와 같은 높이)에 붙이고,
/// EditorScene이 Init()을 호출한다.
/// - 모든 구간(곡 시작 포함)에 청록 구간선(그리드 폭 + 양옆 lineOverhang, 왼쪽 끝 ▶)을 그리고,
///   그리드 오른쪽 바깥에 "♩ 200 BPM" 라벨과 아래 작은 "150.61 → 200"(곡 시작은 "곡 시작")을 붙인다.
/// - 보이는 구간만 풀(ComponentPool)에서 꺼내 배치한다 (박자선·노트 표시와 같은 방식). 클릭은 받지 않는다 (raycastTarget 끔).
/// - 라벨 글자는 구간이 바뀔 때(OnTimingChanged)만 만들고, 스크롤·재생 중에는 위치만 옮긴다.
/// - 표시 템플릿 Img_BpmSectionLine의 자식 순서: 0 Txt_BpmSectionArrow, 1 Img_BpmSectionLabel(자식 Txt_BpmSectionLabel), 2 Txt_BpmSectionChange.
/// </summary>
public class EditorBpmSectionLayer : MonoBehaviour
{
    [Header("템플릿 (꺼 둔 자식)")]
    [Tooltip("구간선 하나의 템플릿 (Img_BpmSectionLine). 자식: ▶, 라벨 상자, 아래 작은 글자")]
    [SerializeField] private Image lineTemplate;

    [Header("모양")]
    [Tooltip("구간선이 그리드 양옆으로 더 나가는 길이 (px)")]
    [Min(0f)] [SerializeField] private float lineOverhang = 6f;
    [Tooltip("구간선 두께 (px)")]
    [Min(1f)] [SerializeField] private float lineThickness = 4f;
    [Tooltip("라벨이 구간선 오른쪽 끝에서 떨어진 거리 (px)")]
    [SerializeField] private float labelGap = 9f;

    private struct Parts
    {
        public TMP_Text Label;
        public TMP_Text Change;
    }

    private ChartGridView grid;
    private ChartEditSession session;
    private ComponentPool<Image> pool;
    private readonly Dictionary<Image, Parts> parts = new Dictionary<Image, Parts>();
    private readonly List<string> labelTexts = new List<string>();
    private readonly List<string> changeTexts = new List<string>();
    private RectTransform rectTransform;

    /// <summary>지금 그린 구간선 수 (확인용)</summary>
    public int ActiveCount => pool != null ? pool.ActiveCount : 0;

    /// <summary>i번째 구간 라벨 글자 (확인용)</summary>
    public string GetLabelText(int index) => index >= 0 && index < labelTexts.Count ? labelTexts[index] : null;

    /// <summary>i번째 구간 아래 작은 글자 (확인용)</summary>
    public string GetChangeText(int index) => index >= 0 && index < changeTexts.Count ? changeTexts[index] : null;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 그리드·세션을 받고 화면 갱신·구간 변경을 구독한다.
    /// </summary>
    public void Init(ChartGridView grid, ChartEditSession session)
    {
        if (rectTransform == null) rectTransform = (RectTransform)transform;
        Unsubscribe();
        this.grid = grid;
        this.session = session;

        if (lineTemplate == null)
        {
            Debug.LogError("[EditorBpmSectionLayer] 구간선 템플릿(lineTemplate)이 연결되지 않았습니다.", this);
            return;
        }

        lineTemplate.gameObject.SetActive(false);
        if (pool == null)
        {
            pool = new ComponentPool<Image>(lineTemplate, transform);
            pool.Prewarm(GameConstants.Editor.BpmSectionLinePoolSize);
        }

        grid.OnViewChanged += Refresh;
        session.OnTimingChanged += OnTimingChanged;
        OnTimingChanged();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (grid != null) grid.OnViewChanged -= Refresh;
        if (session != null) session.OnTimingChanged -= OnTimingChanged;
    }

    /// <summary>구간이 바뀌면 라벨 글자를 다시 만들고 다시 그린다</summary>
    private void OnTimingChanged()
    {
        labelTexts.Clear();
        changeTexts.Clear();
        IReadOnlyList<BpmSection> sections = session.BpmSections;
        for (int i = 0; i < sections.Count; i++)
        {
            string bpm = SongData.FormatBpm(sections[i].Bpm);
            labelTexts.Add(string.Format(GameConstants.Editor.BpmSectionLabelFormat, bpm));
            changeTexts.Add(i == 0
                ? GameConstants.Editor.BpmSectionStartLabel
                : string.Format(GameConstants.Editor.BpmSectionChangeFormat, SongData.FormatBpm(sections[i - 1].Bpm), bpm));
        }
        Refresh();
    }

    /// <summary>
    /// 보이는 구간의 구간선을 배치한다. y는 그리드와 같은 BeatToLocalY(Img_Grid 아래 끝 기준)를 쓴다.
    /// </summary>
    public void Refresh()
    {
        if (pool == null || grid == null || session == null) return;

        RectTransform gridRect = grid.GridRect;
        Rect rect = gridRect.rect;
        // 그리드 왼쪽 아래·오른쪽 끝을 이 레이어 좌표로 바꾼다 (레이어는 Img_Grid와 높이가 같고 가로만 넓다)
        Vector3 gridBottomLeft = rectTransform.InverseTransformPoint(gridRect.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f)));
        Vector3 gridBottomRight = rectTransform.InverseTransformPoint(gridRect.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f)));
        Rect layerRect = rectTransform.rect;
        float left = gridBottomLeft.x - layerRect.xMin - lineOverhang;
        float width = gridBottomRight.x - gridBottomLeft.x + lineOverhang * 2f;
        float bottom = gridBottomLeft.y - layerRect.yMin;

        grid.GetVisibleBeatRange(out float minBeat, out float maxBeat);
        IReadOnlyList<BpmSection> sections = session.BpmSections;

        pool.BeginFrame();
        for (int i = 0; i < sections.Count; i++)
        {
            float beat = sections[i].Beat;
            if (beat < minBeat || beat > maxBeat) continue;

            Image line = pool.Get();
            Parts p = GetParts(line);
            RectTransform lineRect = line.rectTransform;
            lineRect.anchoredPosition = new Vector2(left, bottom + grid.BeatToLocalY(beat));
            lineRect.sizeDelta = new Vector2(width, lineThickness);
            if (i < labelTexts.Count)
            {
                p.Label.text = labelTexts[i];
                p.Change.text = changeTexts[i];
            }
        }
        pool.EndFrame();
    }

    private Parts GetParts(Image line)
    {
        if (parts.TryGetValue(line, out Parts p)) return p;

        Transform t = line.transform;
        RectTransform label = (RectTransform)t.GetChild(1);
        label.anchoredPosition = new Vector2(labelGap, label.anchoredPosition.y);
        RectTransform change = (RectTransform)t.GetChild(2);
        change.anchoredPosition = new Vector2(labelGap, change.anchoredPosition.y);

        p = new Parts
        {
            Label = label.GetComponentInChildren<TMP_Text>(true),
            Change = change.GetComponent<TMP_Text>()
        };
        parts[line] = p;
        return p;
    }
}
