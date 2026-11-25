using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 롱노트 표시 (Img_LongNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane, isPreview)와 SetShape(...)를 호출한다.
/// 인게임 롱노트와 같은 모양 · 색에 에디터만의 꼬리 끝 막대를 더한다 (11/20, 기획서 3-6 「화면 구성」 노트).
/// - 이 오브젝트의 Image가 몸통이다: Square 스프라이트(Simple, 테두리 없음), 폭 = 라인 폭 × 0.70, 세로 = 머리 가운데(시작 틱) ~ 끝 틱 − 숏노트 높이 ÷ 2 (11/25).
///   색은 라인 색, 알파는 UIVerticalAlpha로 위(끝) 45% → 아래(시작) 70% (UITheme.longNoteTopAlpha / BottomAlpha, LongNoteAlphaAt과 같은 계산).
///   피벗은 아래 가운데라 anchoredPosition.y = 시작 틱 y다.
/// - 자식 (그리는 순서 = 형제 순서): Img_BodyFrame(드래그 미리 보기만, 하늘 2px 테두리) &lt; Img_CenterLine(가운데 흰 선 2px, 흰색 78%)
///   &lt; Img_LongTail(꼬리 끝 막대: 몸통 폭 × 6px, 모서리 3, 막대 위 끝 = 몸통 위 끝, 라인 색 100%) &lt; Img_LongHead(머리: 숏노트와 같은 모양 + 자식 Img_Shine, 시작 틱에 가운데).
/// - 드래그 미리 보기(isPreview): 몸통 = 하늘(editorPreview) × editorPreviewFillAlpha(22%) + 하늘 테두리 2px, 가운데 선 없음,
///   머리 · 꼬리 끝 막대 = 하늘 × editorPreviewPartAlpha(55%).
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 모든 그래픽의 raycastTarget을 끈다.
/// </summary>
public class Img_LongNote : MonoBehaviour
{
    [Tooltip("시작 표시 Img_LongHead (ShortNote 스프라이트, Sliced)")]
    [SerializeField] private Image head;
    [Tooltip("머리 위 광택 Img_LongHead/Img_Shine (ShortNote_Shine, Sliced)")]
    [SerializeField] private Image headShine;
    [Tooltip("꼬리 끝 막대 Img_LongTail (둥근 사각형 RoundedRect8, Sliced)")]
    [SerializeField] private Image tail;
    [Tooltip("몸통 가운데 선 Img_CenterLine (Square)")]
    [SerializeField] private Image centerLine;
    [Tooltip("드래그 미리 보기 몸통 테두리 Img_BodyFrame (UIRectFrame)")]
    [SerializeField] private UIRectFrame bodyFrame;

    private Image body;
    private UIVerticalAlpha bodyAlpha;

    /// <summary>몸통 RectTransform (피벗: 아래 가운데, y = 시작 틱)</summary>
    public RectTransform RectTransform { get; private set; }

    /// <summary>머리 RectTransform (확인용)</summary>
    public RectTransform HeadRect => head != null ? head.rectTransform : null;

    /// <summary>꼬리 끝 막대 RectTransform (확인용)</summary>
    public RectTransform TailRect => tail != null ? tail.rectTransform : null;

    /// <summary>가운데 선 (확인용)</summary>
    public Image CenterLine => centerLine;

    /// <summary>머리 광택 (확인용)</summary>
    public Image HeadShine => headShine;

    /// <summary>미리 보기 몸통 테두리 (확인용)</summary>
    public UIRectFrame BodyFrame => bodyFrame;

    /// <summary>몸통 세로 알파 효과 (확인용)</summary>
    public UIVerticalAlpha BodyAlpha => bodyAlpha;

    /// <summary>몸통 색 (확인용, 알파는 UIVerticalAlpha가 위·아래로 곱한다)</summary>
    public Color BodyColor => body != null ? body.color : Color.clear;

    /// <summary>머리 색 (확인용)</summary>
    public Color HeadColor => head != null ? head.color : Color.clear;

    /// <summary>꼬리 끝 막대 색 (확인용)</summary>
    public Color TailColor => tail != null ? tail.color : Color.clear;

    private void Awake()
    {
        body = GetComponent<Image>();
        bodyAlpha = GetComponent<UIVerticalAlpha>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 색을 칠한다. isPreview면 롱노트 드래그 미리 보기(하늘) 모양이다.
    /// </summary>
    public void Init(int lane, bool isPreview)
    {
        if (lane < 0 || lane >= GameConstants.Editor.LaneCount)
        {
            Debug.LogWarning($"[Img_LongNote] 라인 번호가 범위 밖입니다: lane {lane}", this);
            return;
        }

        UITheme theme = UITheme.Current;
        if (isPreview)
        {
            Color part = UITheme.WithAlpha(theme.editorPreview, theme.editorPreviewPartAlpha);
            body.color = theme.editorPreview;
            SetBodyAlphas(theme.editorPreviewFillAlpha, theme.editorPreviewFillAlpha);
            if (bodyFrame != null)
            {
                bodyFrame.enabled = true;
                bodyFrame.color = theme.editorPreview;
            }
            if (centerLine != null) centerLine.enabled = false;
            if (tail != null) tail.color = part;
            Img_ShortNote.ApplyColors(head, headShine, part, UITheme.WithAlpha(theme.noteShine, theme.noteShine.a * theme.editorPreviewPartAlpha));
            return;
        }

        Color lineColor = theme.NoteColor(lane);
        body.color = lineColor;
        SetBodyAlphas(theme.longNoteBottomAlpha, theme.longNoteTopAlpha);
        if (bodyFrame != null) bodyFrame.enabled = false;
        if (centerLine != null)
        {
            centerLine.enabled = true;
            centerLine.color = theme.longNoteCenterLine;
        }
        if (tail != null) tail.color = lineColor;
        Img_ShortNote.ApplyColors(head, headShine, lineColor, theme.noteShine);
    }

    private void SetBodyAlphas(float bottom, float top)
    {
        if (bodyAlpha != null) bodyAlpha.SetAlphas(bottom, top);
    }

    /// <summary>
    /// 모양을 정한다. 몸통 = bodyWidth × 길이(몸통 위 끝 y − 시작 y, 0 이상), 머리 = 숏노트 크기(headSize)로 시작 틱(아래 끝)에 가운데,
    /// 꼬리 끝 막대 = 몸통 폭 × 6px로 몸통 안쪽 위 가장자리 (막대 위 끝 = 몸통 위 끝). 위치(anchoredPosition)는 EditorNoteLayer가 정한다.
    /// </summary>
    public void SetShape(float bodyWidth, float length, Vector2 headSize)
    {
        RectTransform.sizeDelta = new Vector2(bodyWidth, Mathf.Max(0f, length));

        if (head != null)
        {
            RectTransform headRect = head.rectTransform;
            headRect.anchorMin = headRect.anchorMax = new Vector2(0.5f, 0f);
            headRect.pivot = new Vector2(0.5f, 0.5f);
            headRect.anchoredPosition = Vector2.zero;
            Img_ShortNote.ApplyShape(head, headShine, headSize);
        }

        if (tail != null)
        {
            RectTransform tailRect = tail.rectTransform;
            tailRect.anchorMin = tailRect.anchorMax = new Vector2(0.5f, 1f);
            tailRect.pivot = new Vector2(0.5f, 1f);
            tailRect.anchoredPosition = Vector2.zero;
            tailRect.sizeDelta = new Vector2(bodyWidth, GameConstants.Editor.EditorLongTailBarHeightPx);
            tail.type = Image.Type.Sliced;
            tail.pixelsPerUnitMultiplier = TailBarPixelsPerUnitMultiplier(tail.sprite);
        }

        if (centerLine != null)
        {
            RectTransform lineRect = centerLine.rectTransform;
            lineRect.anchorMin = new Vector2(0.5f, 0f);
            lineRect.anchorMax = new Vector2(0.5f, 1f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(GameConstants.Play.LongNoteCenterLineWidthPx, 0f);
        }

        if (bodyFrame != null)
        {
            RectTransform frameRect = bodyFrame.rectTransform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// 꼬리 끝 막대 둥근 사각형 스프라이트의 9-slice 배율: 스프라이트 모서리(px, 100 PPU 기준) ÷ 원하는 모서리 3px.
    /// 스프라이트가 없거나 테두리가 없으면 1.
    /// </summary>
    public static float TailBarPixelsPerUnitMultiplier(Sprite sprite)
    {
        if (sprite == null || sprite.border.x <= 0f) return 1f;
        float borderPx = sprite.border.x * 100f / sprite.pixelsPerUnit;
        return borderPx / GameConstants.Editor.EditorLongTailBarRadiusPx;
    }
}
