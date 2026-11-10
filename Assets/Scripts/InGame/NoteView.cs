using UnityEngine;

/// <summary>
/// 인게임 숏노트 하나 (Note 프리팹, SpriteRenderer). NoteLayer가 풀에서 꺼내 Init()과 SetPosition()을 호출한다.
/// - 스프라이트는 ShortNote (Draw Mode Sliced, size 0.94 × 0.22, 스케일 1). 크기는 프리팹의 size로 정하고 여기서는 바꾸지 않는다.
/// - 라인 색은 GameConstants.Play.LaneColors (에디터 Img_ShortNote와 같은 값)를 스프라이트에 곱한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NoteView : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 라인 색을 적용한다.
    /// </summary>
    public void Init(int lane)
    {
        Color32[] colors = GameConstants.Play.LaneColors;
        if (lane >= 0 && lane < colors.Length) spriteRenderer.color = colors[lane];
    }

    /// <summary>
    /// 위치를 바꾼다 (부모 Notes 기준 로컬).
    /// </summary>
    public void SetPosition(float x, float y)
    {
        transform.localPosition = new Vector3(x, y, 0f);
    }
}
