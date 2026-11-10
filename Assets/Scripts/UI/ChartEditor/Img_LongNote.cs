using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 롱노트 표시 (Img_LongNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane, alpha)를 호출한다.
/// - 이 오브젝트의 Image 하나(LongNoteBody 스프라이트, Image Type Sliced)로 하나로 이어진 막대를 그린다.
///   위아래 끝 둥근 부분은 늘리지 않는다 (Pixels Per Unit Multiplier로 크기를 맞춘다).
/// - 피벗은 아래 가운데다. 위치·높이(시작 숏노트 아래 가장자리 ~ 끝 숏노트 위 가장자리)는 EditorNoteLayer가 정한다.
/// - 색은 라인 색(GameConstants.Play.LaneColors) 그대로. 드래그 미리보기는 alpha만 낮춘다.
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 raycastTarget을 끈다.
/// </summary>
public class Img_LongNote : MonoBehaviour
{
    private Image image;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform (피벗: 아래 가운데)</summary>
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        image = GetComponent<Image>();
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

        Color color = colors[lane];
        color.a *= alpha;
        image.color = color;
    }
}
