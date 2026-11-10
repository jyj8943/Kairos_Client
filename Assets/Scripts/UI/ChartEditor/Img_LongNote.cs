using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 롱노트 표시 (Img_LongNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane, alpha)와 SetCapHeight(height)를 호출한다.
/// - 이 오브젝트의 Image가 몸통이다 (LongNoteBody 스프라이트, Sliced). 막대 전체 높이(아래 끝 ~ 위 끝)를 채우고,
///   라인 색에 LongNoteBodyAlpha(반투명)를 곱해 뒤 그리드 선이 비쳐 보이게 한다.
/// - 자식 Img_LongHead(아래 끝, 시작)·Img_LongTail(위 끝, 끝)은 숏노트와 같은 ShortNote 스프라이트·Sliced·배율로 그린다 (불투명 라인 색).
///   높이는 숏노트 높이, 폭은 몸통 폭이라 시작·끝 틱에 숏노트를 놓았을 때와 같은 자리에 온다. 몸통보다 뒤 형제라 위에 그려진다.
/// - 피벗은 아래 가운데다. 위치·높이는 EditorNoteLayer가 정한다.
/// - 드래그 미리보기는 몸통·머리·꼬리 모두에 미리보기 alpha를 곱한다.
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 모든 Image의 raycastTarget을 끈다.
/// </summary>
public class Img_LongNote : MonoBehaviour
{
    [Tooltip("시작(아래 끝) 표시 Img_LongHead")]
    [SerializeField] private Image head;
    [Tooltip("끝(위 끝) 표시 Img_LongTail")]
    [SerializeField] private Image tail;

    private Image body;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform (피벗: 아래 가운데)</summary>
    public RectTransform RectTransform { get; private set; }

    /// <summary>머리 RectTransform (확인용)</summary>
    public RectTransform HeadRect => head != null ? head.rectTransform : null;

    /// <summary>꼬리 RectTransform (확인용)</summary>
    public RectTransform TailRect => tail != null ? tail.rectTransform : null;

    private void Awake()
    {
        body = GetComponent<Image>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 라인 색과 불투명도를 적용한다. alpha는 드래그 미리보기일 때 1보다 작다.
    /// 몸통은 alpha × LongNoteBodyAlpha, 머리·꼬리는 alpha.
    /// </summary>
    public void Init(int lane, float alpha)
    {
        Color32[] colors = GameConstants.Play.LaneColors;
        if (lane < 0 || lane >= colors.Length)
        {
            Debug.LogWarning($"[Img_LongNote] 라인 색이 없습니다: lane {lane} (색 {colors.Length}개)", this);
            return;
        }

        Color capColor = colors[lane];
        capColor.a *= alpha;
        Color bodyColor = capColor;
        bodyColor.a *= GameConstants.Editor.LongNoteBodyAlpha;

        body.color = bodyColor;
        if (head != null) head.color = capColor;
        if (tail != null) tail.color = capColor;
    }

    /// <summary>
    /// 머리·꼬리 높이를 정한다 (숏노트 높이와 같게).
    /// </summary>
    public void SetCapHeight(float height)
    {
        if (head != null) head.rectTransform.sizeDelta = new Vector2(head.rectTransform.sizeDelta.x, height);
        if (tail != null) tail.rectTransform.sizeDelta = new Vector2(tail.rectTransform.sizeDelta.x, height);
    }
}
