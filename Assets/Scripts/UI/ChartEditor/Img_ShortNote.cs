using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 숏노트 표시 (Img_ShortNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane)을 호출한다.
/// - 위치·크기는 EditorNoteLayer가 정한다. 색은 인게임 노트와 같은 UITheme.editorNote(#F4F1F9, 모든 라인 같음, 11/19).
/// - 클릭은 노트가 아니라 GridArea에서 받으므로 raycastTarget을 끈다.
/// </summary>
public class Img_ShortNote : MonoBehaviour
{
    private Image image;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform</summary>
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        image = GetComponent<Image>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 노트 색을 적용한다 (라인과 상관없이 같은 색).
    /// </summary>
    public void Init(int lane)
    {
        if (lane < 0 || lane >= GameConstants.Editor.LaneCount)
        {
            Debug.LogWarning($"[Img_ShortNote] 라인 번호가 범위 밖입니다: lane {lane}", this);
            return;
        }

        image.color = UITheme.Current.editorNote;
    }
}
