using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 노트 모양 계산 확인 (11/20, 인게임 노트와 같은 모양 · 색). 순수 함수와 에셋 값만 본다 (PlayMode 확인은 LongNoteEditorSceneTests E8~E10).
/// </summary>
public class EditorNoteShapeTests
{
    private const float NoteHeight = 22f;

    [Test]
    public void Constants_SizeValues()
    {
        Assert.AreEqual(0.70f, GameConstants.Editor.EditorLongBodyWidthRatio, 1e-6f, "몸통 폭 = 라인 폭 × 0.70");
        Assert.AreEqual(6f, GameConstants.Editor.EditorLongTailBarHeightPx, "꼬리 끝 막대 높이 6");
        Assert.AreEqual(3f, GameConstants.Editor.EditorLongTailBarRadiusPx, "꼬리 끝 막대 모서리 3");
        Assert.AreEqual(3f, GameConstants.Editor.NoteHoverPaddingPx, "흰 테두리 바깥 여백 3");
        Assert.AreEqual(2f, GameConstants.Editor.NoteHoverBorderPx, "흰 테두리 선 2");
        Assert.AreEqual(6f, GameConstants.Editor.NoteHoverRadiusPx, "흰 테두리 모서리 6");
    }

    /// <summary>롱노트 드래그 조건: 1/4박 이상이면서 세로 12px 이상일 때만 (경계 포함, 위아래 같음)</summary>
    [TestCase(0, 0f, false)]    // 클릭
    [TestCase(12, 12f, true)]   // 1/4박 · 12px 딱
    [TestCase(-12, -12f, true)] // 아래로 끌기
    [TestCase(48, 120f, true)]
    [TestCase(12, 11.9f, false)] // 한 칸이지만 12px 미만
    [TestCase(48, 0f, false)]    // 마우스는 그대로, 스크롤로 틱만 바뀜
    [TestCase(11, 40f, false)]   // 12px 이상이지만 1/4박 미만
    [TestCase(0, 40f, false)]
    public void IsLongNoteDrag_NeedsQuarterBeatAnd12Px(int tickDelta, float dragPx, bool expected)
    {
        Assert.AreEqual(12, GameConstants.Editor.MinLongNoteTicks);
        Assert.AreEqual(12f, GameConstants.Editor.LongNoteDragMinPx);
        Assert.AreEqual(expected, EditorNotePlacer.IsLongNoteDrag(192, 192 + tickDelta, dragPx));
    }

    [Test]
    public void NoteRect_ShortAndLong()
    {
        Rect shortRect = EditorNoteLayer.NoteRect(75f, 146f, 100f, 100f, false, NoteHeight);
        Assert.AreEqual(new Rect(2f, 89f, 146f, 22f), shortRect, "숏노트 = 시작 y 가운데 22px");

        Rect longRect = EditorNoteLayer.NoteRect(75f, 146f, 100f, 300f, true, NoteHeight);
        Assert.AreEqual(89f, longRect.yMin, 1e-4f, "롱노트 아래 = 머리 아래 끝");
        Assert.AreEqual(303f, longRect.yMax, 1e-4f, "롱노트 위 = 꼬리 끝 막대 위 끝 (끝 y + 3)");

        Rect zeroRect = EditorNoteLayer.NoteRect(75f, 146f, 100f, 100f, true, NoteHeight);
        Assert.AreEqual(111f, zeroRect.yMax, 1e-4f, "길이 0 롱노트는 머리 위 끝까지 (막대는 머리 안에 숨는다)");
    }

    /// <summary>롱노트 커서 대상 구간 위 끝: 다음 노트가 없으면 끝 y 바로 아래, 있으면 다음 노트 아래 끝(끝 y − 11). 시작 y보다 아래로 안 내려감</summary>
    [TestCase(100f, 300f, false, 299.5f)]
    [TestCase(100f, 300f, true, 289f)]
    [TestCase(100f, 111f, true, 100f)] // 딱 반 칸
    [TestCase(100f, 105f, true, 100f)] // 반 칸보다 짧음
    [TestCase(100f, 100f, false, 100f)]
    public void LongHitTop_EndTickBelongsToNextNote(float startY, float endY, bool hasNext, float expected)
    {
        Assert.AreEqual(expected, EditorNoteLayer.LongHitTop(startY, endY, NoteHeight, hasNext), 1e-4f);
    }

    [Test]
    public void HoverRect_GrowsByPaddingAndBorder()
    {
        Rect hover = EditorNoteLayer.HoverRect(new Rect(2f, 89f, 146f, 22f));
        Assert.AreEqual(new Rect(-3f, 84f, 156f, 32f), hover, "둘레 3px 여백 + 2px 선 = 5px씩 넓힘");
    }

    [Test]
    public void VerticalAlpha_MatchesLongNoteAlphaAt()
    {
        UITheme theme = Resources.Load<UITheme>(UITheme.ResourcePath);
        float bottom = theme.longNoteBottomAlpha;
        float top = theme.longNoteTopAlpha;
        Assert.AreEqual(0.70f, UIVerticalAlpha.AlphaAt(0f, 0f, 200f, bottom, top), 1e-5f, "아래(시작) 70%");
        Assert.AreEqual(0.575f, UIVerticalAlpha.AlphaAt(100f, 0f, 200f, bottom, top), 1e-5f, "가운데");
        Assert.AreEqual(0.45f, UIVerticalAlpha.AlphaAt(200f, 0f, 200f, bottom, top), 1e-5f, "위(끝) 45%");
        Assert.AreEqual(0.70f, UIVerticalAlpha.AlphaAt(0f, 0f, 0f, bottom, top), 1e-5f, "높이 0이면 아래 값");
        Assert.AreEqual(UITheme.LongNoteAlphaAt(0.3f, bottom, top), UIVerticalAlpha.AlphaAt(-40f, -100f, 200f, bottom, top), 1e-5f, "LongNoteAlphaAt과 같은 계산");
    }

    [Test]
    public void TailBar_RoundedSpriteRadius3()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Common/RoundedRect8.png");
        Assert.IsNotNull(sprite);
        float multiplier = Img_LongNote.TailBarPixelsPerUnitMultiplier(sprite);
        float radiusPx = sprite.border.x / (sprite.pixelsPerUnit / 100f * multiplier);
        Assert.AreEqual(3f, radiusPx, 1e-4f, "꼬리 끝 막대 모서리 3px");
        Assert.AreEqual(1f, Img_LongNote.TailBarPixelsPerUnitMultiplier(null), "스프라이트 없으면 1");
    }

    /// <summary>9-slice 위아래 테두리가 노트 높이에서 차지하는 비율이 인게임 Note 프리팹과 같다 (pixelsPerUnitMultiplier)</summary>
    [Test]
    public void ShortNoteSlice_SameRatioAsInGame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/InGameScene/Note.prefab");
        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        Sprite sprite = renderer.sprite;
        Assert.AreEqual("ShortNote", sprite.name);

        // 인게임: 카메라 orthographicSize 5 → 화면 높이 10 유닛 = 1080px
        float unitsPerPx = 10f / GameConstants.Play.KeyCellReferenceHeightPx;
        float inGameBorderPx = sprite.border.y / sprite.pixelsPerUnit / unitsPerPx;
        float inGameHeightPx = renderer.size.y / unitsPerPx;

        // 에디터: Canvas referencePixelsPerUnit 100
        float editorBorderPx = sprite.border.y / (sprite.pixelsPerUnit / 100f * GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier);
        Debug.Log($"[EditorNoteShapeTest] 테두리 인게임 {inGameBorderPx:0.00}px / 노트 {inGameHeightPx:0.00}px = {inGameBorderPx / inGameHeightPx:0.000}, " +
                  $"에디터 {editorBorderPx:0.00}px / 노트 {NoteHeight}px = {editorBorderPx / NoteHeight:0.000} (배율 {GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier})");
        Assert.AreEqual(inGameBorderPx / inGameHeightPx, editorBorderPx / NoteHeight, 0.005f, "테두리 비율");
    }

    [Test]
    public void Shine_SameRuleAsInGame()
    {
        Vector2 size = new Vector2(146f, NoteHeight);
        Assert.AreEqual(new Vector2(140f, 4f), NoteShape.ShineSize(size, 1f), "광택 (폭 − 6) × 4");
        Assert.AreEqual(11f - 2f - 2f, NoteShape.ShineLocalY(size, 1f), 1e-4f, "광택 가운데 = 위 끝 − 2 − 2");
    }

    [Test]
    public void Theme_NoEditorNoteColor()
    {
        Assert.IsNull(typeof(UITheme).GetField("editorNote"), "에디터 전용 노트 색은 없다 (NoteColor를 같이 쓴다)");
        Assert.IsNull(typeof(UITheme).GetField("editorLongNoteBodyAlpha"));
        Assert.IsNull(typeof(GameConstants.Play).GetField("LaneColors"));
    }
}
