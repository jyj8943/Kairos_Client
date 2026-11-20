using UnityEngine;

/// <summary>
/// 노트 모양 계산 (11/20, 기획서 3-4 「노트 · 판정 이펙트」 🔧 색 · 모양 상세). 크기는 1080 화면 기준 px를 unitsPerPx로 월드로 바꾼다.
/// 계산은 순수 함수라 EditMode 테스트로 검사하고, Apply 함수는 숏노트·롱노트 머리가 같이 쓴다.
/// - 광택(ShortNote_Shine): (노트 폭 − 좌우 3px × 2) × 4px, 위 끝 = 노트 위 끝 − 2px
/// - 롱노트 꼬리 끝: ShortNote 76 × 10px
/// - 롱노트 가운데 선: 폭 2px × 몸통 길이
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

    /// <summary>롱노트 꼬리 끝 크기 (월드): 76 × 10px</summary>
    public static Vector2 TailSize(float unitsPerPx)
    {
        return new Vector2(GameConstants.Play.LongNoteTailWidthPx, GameConstants.Play.LongNoteTailHeightPx) * unitsPerPx;
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
