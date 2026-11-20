using UnityEngine;

/// <summary>
/// 인게임 숏노트 하나 (Note 프리팹, SpriteRenderer). NoteLayer가 풀에서 꺼내 Init()과 SetPosition()을 호출한다.
/// - 스프라이트는 ShortNote (Draw Mode Sliced, size 0.94 × 0.22, 스케일 1). 크기는 프리팹의 size로 정하고 여기서는 바꾸지 않는다.
/// - 색 (11/20, 기획서 3-4 「노트 · 판정 이펙트」): 본체 = 라인 색(UITheme.NoteColor, 바깥 라벤더 · 안쪽 하늘),
///   자식 Shine(ShortNote_Shine, Sliced) = 흰색 75% 광택 (라인 색을 입히지 않음), 자식 Glow(SoftGlow) = 라인 색 × noteGlowAlpha.
/// - 광택 크기·위치는 NoteShape(1080 기준 px → 월드)로 정한다: (노트 폭 − 6) × 4, 노트 위 끝에서 2px 아래.
/// - 그리는 순서 (GameConstants.InGameSort): 빛 NoteGlow → 본체 Note → 광택 NoteShine. 같은 종류끼리 묶여 그려져 드로우 콜이 노트 수만큼 늘지 않는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NoteView : MonoBehaviour
{
    [Tooltip("노트 빛 (자식 Glow, SoftGlow 스프라이트 Sliced). 노트보다 아래 sortingOrder")]
    [SerializeField] private SpriteRenderer glow;
    [Tooltip("노트 위 광택 (자식 Shine, ShortNote_Shine 스프라이트 Sliced). 노트보다 위 sortingOrder")]
    [SerializeField] private SpriteRenderer shine;

    private SpriteRenderer spriteRenderer;

    /// <summary>노트 빛 (확인용)</summary>
    public SpriteRenderer Glow => glow;

    /// <summary>노트 광택 (확인용)</summary>
    public SpriteRenderer Shine => shine;

    /// <summary>노트 본체 색 (확인용)</summary>
    public Color BodyColor => spriteRenderer != null ? spriteRenderer.color : Color.clear;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 라인 색으로 본체·빛을 칠하고 광택을 놓는다 (UITheme 인게임 묶음).
    /// </summary>
    /// <param name="lane">라인 번호 (0 ~ LaneCount − 1)</param>
    /// <param name="unitsPerPx">1080 화면 기준 1px의 월드 길이 (GearView.UnitsPerReferencePx)</param>
    public void Init(int lane, float unitsPerPx)
    {
        UITheme theme = UITheme.Current;
        spriteRenderer.color = theme.NoteColor(lane);
        spriteRenderer.sortingOrder = GameConstants.InGameSort.Note;
        if (glow != null)
        {
            glow.color = theme.NoteGlowColor(lane);
            glow.sortingOrder = GameConstants.InGameSort.NoteGlow;
        }
        if (shine != null) NoteShape.ApplyShine(shine, spriteRenderer.size, unitsPerPx, theme.noteShine);
    }

    /// <summary>
    /// 위치를 바꾼다 (부모 Notes 기준 로컬).
    /// </summary>
    public void SetPosition(float x, float y)
    {
        transform.localPosition = new Vector3(x, y, 0f);
    }
}
