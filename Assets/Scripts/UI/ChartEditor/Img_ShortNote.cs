using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 숏노트 표시 (Img_ShortNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane)을 호출한다.
/// - 위치·크기는 EditorNoteLayer가 정한다. 여기서는 라인 색만 적용한다.
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 raycastTarget을 끈다.
/// </summary>
public class Img_ShortNote : MonoBehaviour
{
    [Tooltip("라인별 색 (A · S · ; · ' 순서, 라인 수만큼)")]
    [SerializeField] private Color[] laneColors =
    {
        new Color32(0x6E, 0xC8, 0xF5, 0xFF),
        Color.white,
        Color.white,
        new Color32(0x6E, 0xC8, 0xF5, 0xFF)
    };

    private Image image;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform</summary>
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        image = GetComponent<Image>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 라인 색을 적용한다.
    /// </summary>
    public void Init(int lane)
    {
        if (lane < 0 || lane >= laneColors.Length)
        {
            Debug.LogWarning($"[Img_ShortNote] 라인 색이 없습니다: lane {lane} (색 {laneColors.Length}개)", this);
            return;
        }

        image.color = laneColors[lane];
    }
}
