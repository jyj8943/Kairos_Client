using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 칸보다 긴 글자를 전광판처럼 왼쪽으로 흘려 보여 준다 (11/24). 글자의 부모인 정렬용 빈 오브젝트(TitleViewport, 칸)에 RectMask2D와 함께 붙인다.
/// - 글자 폭 ≤ 칸 폭: 움직이지 않고 원래 정렬 그대로다 (글자 칸 = 칸 크기).
/// - 글자 폭 > 칸 폭: MarqueeTimeline 시간표대로 반복한다 (처음 멈춤 → 이동 → 끝 멈춤 → 사라졌다 처음 위치에서 나타남). 시간은 unscaledDeltaTime.
/// - 글자·글자 크기·칸 폭이 바뀌면 다시 재고 처음 상태로 돌아간다. 꺼졌다 켜져도 처음 상태부터다.
/// - SetPlaying(false)면 처음 위치에 멈춘다. ellipsisWhenStopped면 그동안 "…"으로 자른다 (목록에서 고르지 않은 칸).
/// - 색·폰트·글자 크기·글자 내용은 건드리지 않는다. 되돌릴 때의 알파는 글자 오브젝트의 CanvasGroup으로만 바꾼다 (글자 색·결과 등장 연출과 겹치지 않게).
/// - 흐르는 동안 매 프레임 바꾸는 것은 글자의 anchoredPosition.x뿐이다. 양 끝 12px 안에서만 RectMask2D.padding도 바꿔,
///   처음·끝에 멈춘 글자의 끝 획이 경계 흐림(softness)에 흐려지지 않게 한다.
/// - 레이아웃 그룹 안에서는 글자 대신 이 칸이 자리를 차지한다 (ILayoutElement): 높이는 글자의 선호 높이, 폭은 0.
///   칸 폭이 글자 길이를 따라 늘어나 옆 요소(앨범 등)를 밀지 않게, 폭은 남는 자리(flexible · 강제 확장)로만 정한다.
/// </summary>
[RequireComponent(typeof(RectMask2D))]
[DisallowMultipleComponent]
public class UIMarqueeText : MonoBehaviour, ILayoutElement
{
    /// <summary>이보다 적게 넘치면 넘치지 않은 것으로 본다 (px)</summary>
    private const float MinOverflow = 0.5f;

    [Tooltip("흘려 보낼 글자 (이 오브젝트의 자식)")]
    [SerializeField] private TMP_Text text;
    [Tooltip("글자 오브젝트의 CanvasGroup (되돌릴 때 알파)")]
    [SerializeField] private CanvasGroup textGroup;
    [Tooltip("켜지면 바로 흐른다. 끄면 SetPlaying(true)부터")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("멈춰 있는 동안 넘친 글자를 …으로 자른다 (목록에서 고르지 않은 칸). 끄면 처음 위치에서 칸 폭만큼 보인다")]
    [SerializeField] private bool ellipsisWhenStopped;

    private RectTransform viewport;
    private RectTransform textRect;
    private RectMask2D mask;
    private bool cached;
    private bool isPlaying;
    private bool isDirty = true;
    private bool isSizeDirty;
    private string measuredText;
    private float measuredFontSize;
    private float cellWidth;
    private float textWidth;
    private float elapsed;
    private Vector4 appliedPadding;

    /// <summary>흐르는 글자 (확인용)</summary>
    public TMP_Text Text => text;

    /// <summary>넘쳐서 반복 중이면 true (처음·끝에 잠깐 멈춘 동안도 true)</summary>
    public bool IsScrolling => isPlaying && OverflowWidth > 0f;

    /// <summary>넘친 폭 = 이동 거리 (글자 폭 − 칸 폭, 넘치지 않으면 0)</summary>
    public float OverflowWidth { get; private set; }

    /// <summary>지금 왼쪽으로 밀린 거리 (px)</summary>
    public float CurrentOffset { get; private set; }

    /// <summary>흐르게 해 두었으면 true (넘치지 않아도 true)</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>한 바퀴 안의 지금 시각 (초, 확인용)</summary>
    public float Elapsed => elapsed;

    /// <summary>칸 폭 · 글자 폭 (확인용)</summary>
    public float CellWidth => cellWidth;
    public float TextWidth => textWidth;

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Cache();
        isDirty = true;
        Restart();
    }

    private void OnDisable()
    {
        Restart();
    }

    private void OnRectTransformDimensionsChange()
    {
        isSizeDirty = true;
    }

    private void LateUpdate()
    {
        Tick(Time.unscaledDeltaTime);
    }

    /// <summary>글자를 바꾸고 처음 상태부터 시작한다.</summary>
    public void SetText(string value)
    {
        Cache();
        text.text = value ?? string.Empty;
        isDirty = true;
        Restart();
    }

    /// <summary>흐르게 하거나 처음 위치에 멈춘다. 바뀔 때만 처음 상태로 돌아간다.</summary>
    public void SetPlaying(bool playing)
    {
        Cache();
        if (isPlaying == playing) return;
        isPlaying = playing;
        isDirty = true;
        ApplyOverflowMode();
        Restart();
    }

    /// <summary>처음 상태(위치 0, 알파 1, 처음 멈춤부터)로 되돌린다.</summary>
    public void Restart()
    {
        if (!cached) return;
        elapsed = 0f;
        Apply(0f, 1f);
    }

    /// <summary>한 프레임 진행한다 (LateUpdate, 테스트는 직접 부른다). 글자·크기가 그대로면 메모리를 새로 잡지 않는다.</summary>
    public void Tick(float deltaTime)
    {
        Cache();
        if (NeedsMeasure()) Measure();
        if (!IsScrolling) return;

        elapsed = Mathf.Repeat(elapsed + deltaTime, MarqueeTimeline.CycleSeconds(OverflowWidth));
        MarqueeTimeline.Evaluate(elapsed, OverflowWidth, out float offset, out float alpha);
        Apply(offset, alpha);
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        viewport = (RectTransform)transform;
        mask = GetComponent<RectMask2D>();
        textRect = text.rectTransform;
        isPlaying = playOnStart;
        mask.softness = new Vector2Int(GameConstants.Marquee.EdgeSoftness, 0);
        appliedPadding = mask.padding;
        ApplyOverflowMode();
    }

    private bool NeedsMeasure()
    {
        if (isDirty) return true;
        if (isSizeDirty)
        {
            isSizeDirty = false;
            if (!Mathf.Approximately(viewport.rect.width, cellWidth)) return true;
        }
        if (!Mathf.Approximately(text.fontSize, measuredFontSize)) return true;
        string current = text.text;
        return !ReferenceEquals(current, measuredText) && current != measuredText;
    }

    /// <summary>글자 폭과 칸 폭을 다시 재고 처음 상태로 되돌린다. 칸 크기만 바뀌었고 넘친 폭이 같으면 하던 대로 둔다.</summary>
    private void Measure()
    {
        bool restart = isDirty || text.text != measuredText;
        isDirty = false;
        isSizeDirty = false;
        ApplyOverflowMode();
        measuredText = text.text;
        measuredFontSize = text.fontSize;
        cellWidth = viewport.rect.width;
        textWidth = text.preferredWidth;

        float overflow = cellWidth > 0f ? textWidth - cellWidth : 0f;
        if (overflow < MinOverflow) overflow = 0f;
        if (!Mathf.Approximately(overflow, OverflowWidth)) restart = true;
        OverflowWidth = overflow;

        // 넘치면 글자 칸을 글자 폭으로 넓힌다 (칸 밖으로 밀려도 잘려 사라지지 않게). 아니면 칸 크기 그대로라 정렬이 유지된다.
        bool wide = overflow > 0f && !UsesEllipsis;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, wide ? Mathf.Ceil(textWidth) + 1f : Mathf.Max(0f, cellWidth));
        mask.enabled = !UsesEllipsis && (overflow > 0f || cellWidth <= 0f);

        // 글자가 바뀌면 글자의 부모인 이 칸이 레이아웃을 다시 잡게 한다 (글자의 부모가 레이아웃 그룹이 아니라 TMP가 알리지 못한다)
        LayoutRebuilder.MarkLayoutForRebuild(viewport);
        if (restart) Restart();
        else Apply(CurrentOffset, textGroup.alpha);
    }

    private bool UsesEllipsis => ellipsisWhenStopped && !isPlaying;

    private void ApplyOverflowMode()
    {
        TextOverflowModes mode = UsesEllipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        if (text.overflowMode != mode) text.overflowMode = mode;
    }

    private void Apply(float offset, float alpha)
    {
        CurrentOffset = offset;
        Vector2 position = textRect.anchoredPosition;
        if (position.x != -offset) textRect.anchoredPosition = new Vector2(-offset, position.y);
        if (textGroup.alpha != alpha) textGroup.alpha = alpha;

        // 글자의 처음·끝이 칸 경계에 있을 때는 흐림을 칸 밖으로 뺀다
        float softness = GameConstants.Marquee.EdgeSoftness;
        float left = -Mathf.Clamp(softness - offset, 0f, softness);
        float right = -Mathf.Clamp(softness - (OverflowWidth - offset), 0f, softness);
        Vector4 padding = new Vector4(left, -softness, right, -softness);
        if (appliedPadding == padding) return;
        appliedPadding = padding;
        mask.padding = padding;
    }

    // ───── ILayoutElement: 높이만 글자를 따른다 ─────

    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
    public float minWidth => -1f;
    public float preferredWidth => 0f;
    public float flexibleWidth => -1f;
    public float minHeight => -1f;
    public float preferredHeight => text != null ? text.preferredHeight : -1f;
    public float flexibleHeight => -1f;
    public int layoutPriority => 0;
}
