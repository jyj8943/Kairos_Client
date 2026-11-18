using UnityEngine;

/// <summary>
/// 인게임 숏노트 하나 (Note 프리팹, SpriteRenderer). NoteLayer가 풀에서 꺼내 Init()과 SetPosition()을 호출한다.
/// - 스프라이트는 ShortNote (Draw Mode Sliced, size 0.94 × 0.22, 스케일 1). 크기는 프리팹의 size로 정하고 여기서는 바꾸지 않는다.
/// - 색: UITheme 인게임 묶음의 숏노트 색(#F7F5FA, 모든 라인 같음) + 자식 Glow(SoftGlow 스프라이트, 은은한 하늘색 빛).
///   빛은 후처리 없이 부드러운 가장자리 스프라이트 한 장이다. 모든 노트의 빛을 노트보다 한 단계 아래 sortingOrder에 두어
///   빛끼리·노트끼리 묶어 그린다 (고배속에서 노트가 많아도 드로우 콜이 노트 수만큼 늘지 않게).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NoteView : MonoBehaviour
{
    [Tooltip("노트 빛 (자식 Glow, SoftGlow 스프라이트 Sliced). 노트보다 한 단계 아래 sortingOrder")]
    [SerializeField] private SpriteRenderer glow;

    private SpriteRenderer spriteRenderer;

    /// <summary>노트 빛 (확인용)</summary>
    public SpriteRenderer Glow => glow;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 노트·빛 색을 칠한다 (UITheme 인게임 묶음). 모든 라인이 같은 색이다.
    /// </summary>
    public void Init(int lane)
    {
        UITheme theme = UITheme.Current;
        spriteRenderer.color = theme.shortNote;
        if (glow != null) glow.color = theme.shortNoteGlow;
    }

    /// <summary>
    /// 위치를 바꾼다 (부모 Notes 기준 로컬).
    /// </summary>
    public void SetPosition(float x, float y)
    {
        transform.localPosition = new Vector3(x, y, 0f);
    }
}
