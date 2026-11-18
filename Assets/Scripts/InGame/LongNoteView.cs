using UnityEngine;

/// <summary>
/// 인게임 롱노트 하나 (LongNote 프리팹). NoteLayer가 풀에서 꺼내 Init()과 SetLayout()을 호출한다.
/// - 자식 Body 하나(SpriteRenderer, LongNoteBody 스프라이트, Draw Mode Sliced)로 하나로 이어진 막대를 그린다.
///   높이는 localScale이 아니라 size로 바꾼다 (scale로 늘리면 위아래 둥근 끝이 찌그러진다).
/// - 막대 아래 끝 = 머리(시작) y − 숏노트 높이 ÷ 2, 위 끝 = 꼬리(끝) y + 숏노트 높이 ÷ 2.
///   숏노트를 시작·끝 위치에 놓았을 때의 아래·위 가장자리와 같다. 최소 높이는 숏노트 높이다.
/// - 위치는 부모 Notes 기준 로컬. 이 오브젝트는 라인 x에 두고, Body의 y·높이만 바꾼다.
/// - 색 (UITheme 인게임 묶음): 흰색 반투명, 아래 끝(머리) 85% → 위 끝(꼬리) 55% 세로 그라데이션. 시작 MISS면 비활성 색(longNoteMissed)에 같은 그라데이션.
///   그라데이션은 셰이더 Kairos/LongNoteBody(머티리얼 Assets/Materials/LongNoteBody.mat)가 막대 메시의 로컬 y ÷ 막대 높이로 계산한다.
///   막대 높이는 MaterialPropertyBlock(_Height)으로 넘기므로 길이가 바뀌어도 머리·꼬리를 포함한 막대 전체에 위 55% → 아래 85%가 유지된다.
/// - 숏노트와 구분: 숏노트는 불투명 #F7F5FA + 하늘색 빛, 롱노트는 머리부터 반투명 흰 막대(빛 없음)라 머리도 숏노트보다 흐리게 보인다.
/// </summary>
public class LongNoteView : MonoBehaviour
{
    private static readonly int HeightId = Shader.PropertyToID("_Height");
    private static readonly int TopAlphaId = Shader.PropertyToID("_TopAlpha");
    private static readonly int BottomAlphaId = Shader.PropertyToID("_BottomAlpha");

    [Tooltip("막대 (LongNoteBody 스프라이트, Draw Mode Sliced, 머티리얼 LongNoteBody.mat)")]
    [SerializeField] private SpriteRenderer body;

    private MaterialPropertyBlock block;
    private float shownHeight = -1f;

    private bool isMissed;
    private Vector2 noteSize = new Vector2(0.94f, 0.22f);

    /// <summary>
    /// 몸통 색·그라데이션 값을 적용하고 보통 상태(회색 아님)로 시작한다.
    /// </summary>
    /// <param name="noteSize">숏노트 크기 (월드 단위, 폭·높이). 막대 폭과 위아래 여유에 쓴다</param>
    public void Init(int lane, Vector2 noteSize)
    {
        this.noteSize = noteSize;
        isMissed = false;
        shownHeight = -1f;
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
    /// 라인 x와 머리(시작)·꼬리(끝)의 중심 y를 받아 막대를 놓는다.
    /// 막대는 (headY − 숏노트 높이 ÷ 2) ~ (tailY + 숏노트 높이 ÷ 2)이고, 높이는 숏노트 높이보다 작아지지 않는다.
    /// </summary>
    public void SetLayout(float x, float headY, float tailY)
    {
        transform.localPosition = new Vector3(x, 0f, 0f);

        float halfHeight = noteSize.y * 0.5f;
        float bottom = headY - halfHeight;
        float top = Mathf.Max(tailY + halfHeight, bottom + noteSize.y);

        float height = top - bottom;
        body.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f, 0f);
        body.size = new Vector2(noteSize.x, height);
        if (!Mathf.Approximately(height, shownHeight)) SetHeight(height);
    }

    /// <summary>셰이더 그라데이션 기준 높이 (막대 높이, 월드 단위)를 넘긴다. 높이가 바뀔 때만 부른다</summary>
    private void SetHeight(float height)
    {
        shownHeight = height;
        UITheme theme = UITheme.Current;
        if (block == null) block = new MaterialPropertyBlock();
        body.GetPropertyBlock(block);
        block.SetFloat(HeightId, height);
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

    /// <summary>막대 아래 끝 y (부모 Notes 기준, 확인용)</summary>
    public float BottomY => body.transform.localPosition.y - body.size.y * 0.5f;

    /// <summary>막대 위 끝 y (부모 Notes 기준, 확인용)</summary>
    public float TopY => body.transform.localPosition.y + body.size.y * 0.5f;

    private void ApplyColor()
    {
        UITheme theme = UITheme.Current;
        body.color = isMissed ? theme.longNoteMissed : theme.longNoteBody;
    }
}
