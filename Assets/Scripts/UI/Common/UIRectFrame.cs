using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 직사각형 테두리만 그리는 UI Graphic (안쪽은 비어 있음, 모서리 각짐).
/// - 선은 RectTransform 안쪽으로 thickness만큼 그린다 (사각형 4개). 색은 Graphic.color.
/// - 에디터 롱노트 드래그 미리 보기의 몸통 하늘 테두리 2px(Img_BodyFrame)에 쓴다. Outline 효과는 반투명 몸통 뒤에 복사본이 비쳐 쓰지 않는다.
/// - 클릭은 받지 않는다 (raycastTarget 끔).
/// </summary>
public class UIRectFrame : MaskableGraphic
{
    [Tooltip("선 두께 (px)")]
    [Min(0f)] [SerializeField] private float thickness = 2f;

    /// <summary>선 두께 (px)</summary>
    public float Thickness
    {
        get => thickness;
        set
        {
            if (Mathf.Approximately(thickness, value)) return;
            thickness = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f || thickness <= 0f) return;

        float t = Mathf.Min(thickness, r.width * 0.5f, r.height * 0.5f);
        AddQuad(vh, new Rect(r.xMin, r.yMin, r.width, t));                    // 아래
        AddQuad(vh, new Rect(r.xMin, r.yMax - t, r.width, t));                // 위
        AddQuad(vh, new Rect(r.xMin, r.yMin + t, t, r.height - t * 2f));      // 왼쪽
        AddQuad(vh, new Rect(r.xMax - t, r.yMin + t, t, r.height - t * 2f));  // 오른쪽
    }

    private void AddQuad(VertexHelper vh, Rect q)
    {
        if (q.width <= 0f || q.height <= 0f) return;
        int start = vh.currentVertCount;
        Color32 c = color;
        vh.AddVert(new Vector3(q.xMin, q.yMin), c, Vector4.zero);
        vh.AddVert(new Vector3(q.xMin, q.yMax), c, Vector4.zero);
        vh.AddVert(new Vector3(q.xMax, q.yMax), c, Vector4.zero);
        vh.AddVert(new Vector3(q.xMax, q.yMin), c, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
