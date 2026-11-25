using UnityEngine;

/// <summary>
/// 노트 모양 계산 (11/20, 기획서 3-4 「노트 · 판정 이펙트」 🔧 색 · 모양 상세). 크기는 1080 화면 기준 px를 unitsPerPx로 월드로 바꾼다.
/// 계산은 순수 함수라 EditMode 테스트로 검사하고, Apply 함수는 숏노트·롱노트 머리가 같이 쓴다.
/// - 광택(ShortNote_Shine): (노트 폭 − 좌우 3px × 2) × 4px, 위 끝 = 노트 위 끝 − 2px
/// - 롱노트 (11/20 정정, README 1-1): 몸통 = 폭 76px 평평한 띠, 세로 머리 y(가운데) ~ 끝 시각 y − LongNoteEndInset(11/25부터 0). 꼬리는 따로 그리지 않는다.
///   가운데 선 = 폭 2px × 몸통 길이. 누르는 동안 머리·몸통 아래 끝은 판정선에 붙고 꼬리만 내려온다 (ResolveLongNoteY).
/// </summary>
public static class NoteShape
{
    /// <summary>광택 크기 (월드): (노트 폭 − 6px) × 4px</summary>
    public static Vector2 ShineSize(Vector2 noteSize, float unitsPerPx)
    {
        float width = noteSize.x - GameConstants.Play.NoteShineSideInsetPx * 2f * unitsPerPx;
        return new Vector2(Mathf.Max(0f, width), GameConstants.Play.NoteShineHeightPx * unitsPerPx);
    }

    /// <summary>광택 세로 중심 (노트 중심 기준 로컬 y): 노트 위 끝 − 2px − 광택 두께 ÷ 2</summary>
    public static float ShineLocalY(Vector2 noteSize, float unitsPerPx)
    {
        return noteSize.y * 0.5f - (GameConstants.Play.NoteShineTopInsetPx + GameConstants.Play.NoteShineHeightPx * 0.5f) * unitsPerPx;
    }

    /// <summary>롱노트 몸통 폭 (월드): 76px</summary>
    public static float LongNoteBodyWidth(float unitsPerPx)
    {
        return GameConstants.Play.LongNoteBodyWidthPx * unitsPerPx;
    }

    /// <summary>
    /// 롱노트 몸통 세로 범위 (부모 기준 y): 아래 끝 = 머리 y(가운데), 위 끝 = 꼬리 y(ResolveLongNoteY가 정한 몸통 위 끝). 꼬리가 머리보다 아래면 높이 0.
    /// </summary>
    public static void LongNoteBodyRange(float headY, float tailY, out float bottom, out float top)
    {
        bottom = headY;
        top = Mathf.Max(headY, tailY);
    }

    /// <summary>롱노트 몸통 위 끝을 끝 시각 위치에서 내리는 양 (월드): LongNoteEndInsetPx(지금 0 = 끝 시각 위치 그대로). 배속과 상관없다</summary>
    public static float LongNoteEndInset(float unitsPerPx)
    {
        return GameConstants.Play.LongNoteEndInsetPx * unitsPerPx;
    }

    /// <summary>
    /// 상태에 따라 롱노트 머리 y와 몸통 위 끝 y를 정한다 (NoteLayer.Refresh). 순수 함수.
    /// - 몸통 위 끝 = 끝 시각 y − endInset. 머리보다 아래로는 내려가지 않는다 (짧은 롱노트 · 낮은 배속에서 몸통 길이 0).
    /// - 누르는 동안(Held): 머리는 판정선에 고정, 몸통 위 끝은 그대로 내려오되 판정선 아래로는 내려가지 않는다 (늦게 떼는 중 몸통 길이 0).
    /// - 판정 전(Waiting)·시작 MISS(Missed): 시간대로 그대로 지나간다 (머리를 판정선에 고정하지 않는다).
    /// </summary>
    /// <param name="tailY">넣을 때는 끝 시각 y, 나올 때는 몸통 위 끝 y</param>
    /// <param name="endInset">몸통 위 끝을 내리는 양 (LongNoteEndInset)</param>
    public static void ResolveLongNoteY(LongNoteState state, float judgeLineY, float endInset, ref float headY, ref float tailY)
    {
        if (state == LongNoteState.Held) headY = judgeLineY;
        tailY = Mathf.Max(headY, tailY - endInset);
    }

    /// <summary>롱노트 가운데 선 폭 (월드): 2px</summary>
    public static float CenterLineWidth(float unitsPerPx)
    {
        return GameConstants.Play.LongNoteCenterLineWidthPx * unitsPerPx;
    }

    /// <summary>
    /// 광택 렌더러를 노트 크기에 맞춰 놓는다 (Sliced, 흰색 75%, 순서 NoteShine). 숏노트와 롱노트 머리가 같이 쓴다.
    /// </summary>
    public static void ApplyShine(SpriteRenderer shine, Vector2 noteSize, float unitsPerPx, Color color)
    {
        shine.drawMode = SpriteDrawMode.Sliced;
        shine.size = ShineSize(noteSize, unitsPerPx);
        shine.transform.localPosition = new Vector3(0f, ShineLocalY(noteSize, unitsPerPx), 0f);
        shine.transform.localScale = Vector3.one;
        shine.color = color;
        shine.sortingOrder = GameConstants.InGameSort.NoteShine;
    }
}
