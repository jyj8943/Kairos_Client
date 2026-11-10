using UnityEngine;

/// <summary>
/// 인게임 롱노트 하나 (LongNote 프리팹). NoteLayer가 풀에서 꺼내 Init()과 SetLayout()을 호출한다.
/// - 자식 Body 하나(SpriteRenderer, LongNoteBody 스프라이트, Draw Mode Sliced)로 하나로 이어진 막대를 그린다.
///   높이는 localScale이 아니라 size로 바꾼다 (scale로 늘리면 위아래 둥근 끝이 찌그러진다).
/// - 막대 아래 끝 = 머리(시작) y − 숏노트 높이 ÷ 2, 위 끝 = 꼬리(끝) y + 숏노트 높이 ÷ 2.
///   숏노트를 시작·끝 위치에 놓았을 때의 아래·위 가장자리와 같다. 최소 높이는 숏노트 높이다.
/// - 위치는 부모 Notes 기준 로컬. 이 오브젝트는 라인 x에 두고, Body의 y·높이만 바꾼다.
/// - 색: 막대 전체를 라인 색(GameConstants.Play.LaneColors) 그대로 쓴다. 시작 MISS면 비활성 색(LongNoteMissedColor).
/// </summary>
public class LongNoteView : MonoBehaviour
{
    [Tooltip("막대 (LongNoteBody 스프라이트, Draw Mode Sliced)")]
    [SerializeField] private SpriteRenderer body;

    private int lane;
    private bool isMissed;
    private Vector2 noteSize = new Vector2(0.94f, 0.22f);

    /// <summary>
    /// 라인 색을 적용하고 보통 상태(회색 아님)로 시작한다.
    /// </summary>
    /// <param name="noteSize">숏노트 크기 (월드 단위, 폭·높이). 막대 폭과 위아래 여유에 쓴다</param>
    public void Init(int lane, Vector2 noteSize)
    {
        this.lane = lane;
        this.noteSize = noteSize;
        isMissed = false;
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

        body.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f, 0f);
        body.size = new Vector2(noteSize.x, top - bottom);
    }

    /// <summary>막대 아래 끝 y (부모 Notes 기준, 확인용)</summary>
    public float BottomY => body.transform.localPosition.y - body.size.y * 0.5f;

    /// <summary>막대 위 끝 y (부모 Notes 기준, 확인용)</summary>
    public float TopY => body.transform.localPosition.y + body.size.y * 0.5f;

    private void ApplyColor()
    {
        if (isMissed)
        {
            body.color = GameConstants.Play.LongNoteMissedColor;
            return;
        }

        Color32[] colors = GameConstants.Play.LaneColors;
        body.color = lane >= 0 && lane < colors.Length ? (Color)colors[lane] : Color.white;
    }
}
