using UnityEngine;

/// <summary>
/// 인게임 롱노트 하나 (LongNote 프리팹). NoteLayer가 풀에서 꺼내 Init()과 SetLayout()을 호출한다.
/// 모양 (11/20 정정, 기획서 3-4 「노트 · 판정 이펙트」, README 1-1 · 06_롱노트_구조_전체):
/// - Body: Square 스프라이트(흰 4×4, Draw Mode Sliced, 테두리 없음)를 폭 76px(라인 x+16 ~ x+92) 평평한 띠로 늘린다.
///   세로는 머리 y(가운데) ~ 끝 시각 y − LongNoteEndInsetPx(11/25부터 0). 위 끝이 곧 꼬리이고 꼬리는 따로 그리지 않는다. 높이는 localScale이 아니라 size로 바꾼다.
/// - CenterLine: LongNoteCenterLine(흰색 78%, 폭 2px, 라인 가운데 x+53 ~ x+55)을 몸통과 같은 길이로 늘린다.
/// - Head: 숏노트와 같은 모양 (ShortNote 100×22 라인 색 + 자식 Shine 광택), 머리 y에 가운데.
/// - 누르는 동안: NoteLayer가 머리 y를 판정선에 고정해 넘기므로 머리는 판정선 위에 계속 보이고, 몸통 아래 끝도 판정선에 붙는다.
/// - 색 (UITheme 인게임 묶음): 몸통·머리 = 라인 색(NoteColor). 몸통은 아래 끝(머리) 70% → 위 끝(꼬리) 45% 세로 그라데이션.
///   시작 MISS면 몸통·머리 모두 회색(longNoteMissed, 몸통은 같은 그라데이션)이고 광택·가운데 선은 끈다.
///   그라데이션은 셰이더 Kairos/LongNoteBody(머티리얼 Assets/Materials/LongNoteBody.mat)가 막대 메시의 로컬 y ÷ 막대 높이로 계산한다.
///   막대 높이는 MaterialPropertyBlock(_Height)으로 넘기므로 길이가 바뀌어도 막대 전체에 위 45% → 아래 70%가 유지된다.
/// - 그리는 순서 (GameConstants.InGameSort): 판정선 3 &lt; 몸통 5 &lt; 가운데 선 6 &lt; 머리 7 &lt; 머리 광택 8 &lt; 판정 이펙트 9.
/// </summary>
public class LongNoteView : MonoBehaviour
{
    private static readonly int HeightId = Shader.PropertyToID("_Height");
    private static readonly int TopAlphaId = Shader.PropertyToID("_TopAlpha");
    private static readonly int BottomAlphaId = Shader.PropertyToID("_BottomAlpha");

    /// <summary>셰이더에 넘기는 최소 높이 (높이 0으로 나누지 않게)</summary>
    private const float MinShaderHeight = 1e-4f;

    [Tooltip("몸통 (Square 스프라이트, Draw Mode Sliced, 머티리얼 LongNoteBody.mat)")]
    [SerializeField] private SpriteRenderer body;
    [Tooltip("몸통 가운데 흰 선 (LongNoteCenterLine 스프라이트, Sliced)")]
    [SerializeField] private SpriteRenderer centerLine;
    [Tooltip("머리 (ShortNote 스프라이트, Sliced, 숏노트와 같은 크기)")]
    [SerializeField] private SpriteRenderer head;
    [Tooltip("머리 광택 (Head/Shine, ShortNote_Shine 스프라이트, Sliced)")]
    [SerializeField] private SpriteRenderer headShine;

    private MaterialPropertyBlock block;
    private float shownHeight = -1f;

    private bool isMissed;
    private int lane;
    private float unitsPerPx = 1f / 108f;
    private Vector2 noteSize = new Vector2(0.94f, 0.22f);

    /// <summary>
    /// 라인 색·크기를 적용하고 보통 상태(회색 아님)로 시작한다.
    /// </summary>
    /// <param name="lane">라인 번호 (색을 고른다)</param>
    /// <param name="noteSize">숏노트 크기 (월드 단위, 폭·높이). 머리 크기에 쓴다</param>
    /// <param name="unitsPerPx">1080 화면 기준 1px의 월드 길이 (GearView.UnitsPerReferencePx)</param>
    public void Init(int lane, Vector2 noteSize, float unitsPerPx)
    {
        this.lane = lane;
        this.noteSize = noteSize;
        this.unitsPerPx = unitsPerPx;
        isMissed = false;
        shownHeight = -1f;

        UITheme theme = UITheme.Current;
        body.drawMode = SpriteDrawMode.Sliced;
        body.sortingOrder = GameConstants.InGameSort.LongNoteBody;
        if (centerLine != null)
        {
            centerLine.drawMode = SpriteDrawMode.Sliced;
            centerLine.color = theme.longNoteCenterLine;
            centerLine.sortingOrder = GameConstants.InGameSort.LongNoteCenterLine;
        }
        if (head != null)
        {
            head.drawMode = SpriteDrawMode.Sliced;
            head.size = noteSize;
            head.sortingOrder = GameConstants.InGameSort.Note;
        }
        if (headShine != null) NoteShape.ApplyShine(headShine, noteSize, unitsPerPx, theme.noteShine);
        ApplyColor();
    }

    /// <summary>
    /// 시작 MISS 상태로 바꾼다 (회색). 이미 바꿨으면 아무것도 하지 않는다.
    /// </summary>
    public void SetMissed(bool missed)
    {
        if (isMissed == missed) return;
        isMissed = missed;
        ApplyColor();
    }

    /// <summary>
    /// 라인 x와 머리(시작)·꼬리(몸통 위 끝)의 y를 받아 몸통·가운데 선·머리를 놓는다 (NoteShape.LongNoteBodyRange: 머리 y ~ 꼬리 y).
    /// 누르는 동안의 머리 고정은 NoteLayer가 headY에 판정선 y를 넘겨 처리한다.
    /// </summary>
    public void SetLayout(float x, float headY, float tailY)
    {
        transform.localPosition = new Vector3(x, 0f, 0f);

        NoteShape.LongNoteBodyRange(headY, tailY, out float bottom, out float top);
        float height = top - bottom;
        float centerY = (bottom + top) * 0.5f;
        body.transform.localPosition = new Vector3(0f, centerY, 0f);
        body.size = new Vector2(NoteShape.LongNoteBodyWidth(unitsPerPx), height);
        if (!Mathf.Approximately(height, shownHeight)) SetHeight(height);

        if (centerLine != null)
        {
            centerLine.transform.localPosition = new Vector3(0f, centerY, 0f);
            centerLine.size = new Vector2(NoteShape.CenterLineWidth(unitsPerPx), height);
        }
        if (head != null) head.transform.localPosition = new Vector3(0f, headY, 0f);
    }

    /// <summary>셰이더 그라데이션 기준 높이 (막대 높이, 월드 단위)를 넘긴다. 높이가 바뀔 때만 부른다</summary>
    private void SetHeight(float height)
    {
        shownHeight = height;
        UITheme theme = UITheme.Current;
        if (block == null) block = new MaterialPropertyBlock();
        body.GetPropertyBlock(block);
        block.SetFloat(HeightId, Mathf.Max(height, MinShaderHeight));
        block.SetFloat(TopAlphaId, theme.longNoteTopAlpha);
        block.SetFloat(BottomAlphaId, theme.longNoteBottomAlpha);
        body.SetPropertyBlock(block);
    }

    /// <summary>막대 위치 t(0 = 아래 끝 머리, 1 = 위 끝 꼬리)의 불투명도 (셰이더와 같은 식, 확인용)</summary>
    public float AlphaAt(float t)
    {
        UITheme theme = UITheme.Current;
        return body.color.a * UITheme.LongNoteAlphaAt(t, theme.longNoteBottomAlpha, theme.longNoteTopAlpha);
    }

    /// <summary>막대 색 (확인용)</summary>
    public Color BodyColor => body.color;

    /// <summary>머리 색 (확인용, 없으면 투명)</summary>
    public Color HeadColor => head != null ? head.color : Color.clear;

    /// <summary>시작 MISS(회색)인지 (확인용)</summary>
    public bool IsMissed => isMissed;

    /// <summary>자식 렌더러 (확인용)</summary>
    public SpriteRenderer Body => body;
    public SpriteRenderer CenterLine => centerLine;
    public SpriteRenderer Head => head;
    public SpriteRenderer HeadShine => headShine;

    /// <summary>막대 아래 끝 y (부모 Notes 기준, 확인용)</summary>
    public float BottomY => body.transform.localPosition.y - body.size.y * 0.5f;

    /// <summary>막대 위 끝 y (부모 Notes 기준, 확인용)</summary>
    public float TopY => body.transform.localPosition.y + body.size.y * 0.5f;

    /// <summary>머리 가운데 y (부모 Notes 기준, 확인용)</summary>
    public float HeadY => head != null ? head.transform.localPosition.y : float.NaN;

    private void ApplyColor()
    {
        UITheme theme = UITheme.Current;
        Color color = isMissed ? theme.longNoteMissed : theme.NoteColor(lane);
        body.color = color;
        if (head != null) head.color = color;
        if (headShine != null) headShine.enabled = !isMissed;
        if (centerLine != null) centerLine.enabled = !isMissed;
    }
}
