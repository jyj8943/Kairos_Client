using UnityEngine;

/// <summary>
/// 인게임 롱노트 하나 (LongNote 프리팹). NoteLayer가 풀에서 꺼내 Init()과 SetLayout()을 호출한다.
/// - 자식 Head(시작, 아래 끝)·Body(몸통)·Tail(끝, 위 끝) SpriteRenderer로 그린다. 머리·꼬리는 숏노트와 같은 크기다.
/// - 위치는 부모 Notes 기준 로컬. 이 오브젝트는 라인 x에 두고, 머리·꼬리·몸통의 y만 바꾼다.
/// - 색: 머리·꼬리는 라인 색(GameConstants.Play.LaneColors), 몸통은 LongNoteBodyBrightness만큼 어둡게.
///   시작 MISS면 전체를 비활성 색(LongNoteMissedColor)으로 바꿔 끝까지 내려보낸다.
/// </summary>
public class LongNoteView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer head;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer tail;

    private int lane;
    private bool isMissed;

    /// <summary>
    /// 라인 색을 적용하고 보통 상태(회색 아님)로 시작한다.
    /// </summary>
    public void Init(int lane)
    {
        this.lane = lane;
        isMissed = false;
        ApplyColors();
    }

    /// <summary>
    /// 시작 MISS 상태로 바꾼다 (회색). 이미 바꿨으면 아무것도 하지 않는다.
    /// </summary>
    public void SetMissed(bool missed)
    {
        if (isMissed == missed) return;
        isMissed = missed;
        ApplyColors();
    }

    /// <summary>
    /// 라인 x와 머리·꼬리의 y를 정한다 (몸통은 그 사이를 채운다). tailY가 headY보다 낮으면 몸통 길이 0.
    /// </summary>
    public void SetLayout(float x, float headY, float tailY)
    {
        transform.localPosition = new Vector3(x, 0f, 0f);
        if (tailY < headY) tailY = headY;

        head.transform.localPosition = new Vector3(0f, headY, 0f);
        tail.transform.localPosition = new Vector3(0f, tailY, 0f);

        Transform bodyTransform = body.transform;
        bodyTransform.localPosition = new Vector3(0f, (headY + tailY) * 0.5f, 0f);
        Vector3 scale = bodyTransform.localScale;
        scale.y = tailY - headY;
        bodyTransform.localScale = scale;
    }

    private void ApplyColors()
    {
        Color32[] colors = GameConstants.Play.LaneColors;
        Color laneColor = lane >= 0 && lane < colors.Length ? (Color)colors[lane] : Color.white;

        if (isMissed)
        {
            Color gray = GameConstants.Play.LongNoteMissedColor;
            head.color = gray;
            tail.color = gray;
            body.color = gray;
            return;
        }

        float brightness = GameConstants.Play.LongNoteBodyBrightness;
        head.color = laneColor;
        tail.color = laneColor;
        body.color = new Color(laneColor.r * brightness, laneColor.g * brightness, laneColor.b * brightness, laneColor.a);
    }
}
