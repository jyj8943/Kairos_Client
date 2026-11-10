using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 롱노트 표시 (Img_LongNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane, alpha)를 호출한다.
/// - 이 오브젝트의 Image가 몸통이고, 자식 Img_LongHead(아래 끝, 시작)와 Img_LongTail(위 끝, 끝)을 몸통 위에 그린다.
/// - 피벗은 아래 가운데다. 위치·크기(시작 ~ 끝 높이)는 EditorNoteLayer가 정하고, 여기서는 색과 머리·꼬리 높이만 맡는다.
/// - 색은 라인 색(GameConstants.Play.LaneColors). 몸통은 LongNoteBodyBrightness만큼 어둡게 한다.
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 모든 Image의 raycastTarget을 끈다.
/// </summary>
public class Img_LongNote : MonoBehaviour
{
    [Tooltip("시작(아래 끝) 표시")]
    [SerializeField] private Image head;
    [Tooltip("끝(위 끝) 표시")]
    [SerializeField] private Image tail;

    private Image body;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform (피벗: 아래 가운데)</summary>
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        body = GetComponent<Image>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 라인 색과 불투명도를 적용한다. alpha는 드래그 미리보기일 때 1보다 작다.
    /// </summary>
    public void Init(int lane, float alpha)
    {
        Color32[] colors = GameConstants.Play.LaneColors;
        if (lane < 0 || lane >= colors.Length)
        {
            Debug.LogWarning($"[Img_LongNote] 라인 색이 없습니다: lane {lane} (색 {colors.Length}개)", this);
            return;
        }

        Color laneColor = colors[lane];
        laneColor.a *= alpha;

        float brightness = GameConstants.Editor.LongNoteBodyBrightness;
        Color bodyColor = new Color(laneColor.r * brightness, laneColor.g * brightness, laneColor.b * brightness, laneColor.a);

        body.color = bodyColor;
        if (head != null) head.color = laneColor;
        if (tail != null) tail.color = laneColor;
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
