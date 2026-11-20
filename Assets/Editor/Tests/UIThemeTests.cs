using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 화면 톤 (UITheme, 기획서 3장 「UI 톤 · 라벤더 안개 (11/17)」) EditMode 확인.
/// 1. 기본값 = 기획서 표 (색 몇 개 + 덮는 색 불투명도 3개), 에셋(Resources/Data/UITheme)이 있음
/// 2. 랭크 빈 자리 색: 바탕 흰색 55% · 점선 #2C2838 35% (기획서 3-3 「랭크·FC 표시 (11/18 개정)」)
/// 3. 덮는 색 그라데이션: 왼쪽·가운데·오른쪽 알파 = 0.80 · 0.62 · 0.82 (함수와 MenuBackground가 만든 텍스처 둘 다)
/// 4. 인게임 묶음 기본값 = 기획서 3-4 「인게임 톤 개편 (11/18)」 표 (기본값과 에셋 값 둘 다)
/// 5. 롱노트 몸통 알파: 위 0.45 / 아래 0.70 (11/20 정정) (셰이더 Kairos/LongNoteBody와 같은 식)
/// </summary>
public class UIThemeTests
{
    private const float Tolerance = 1.5f / 255f;

    private UITheme theme;
    private GameObject root;
    private Texture2D texture;

    [SetUp]
    public void SetUp()
    {
        theme = ScriptableObject.CreateInstance<UITheme>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(theme);
        if (root != null) Object.DestroyImmediate(root);
        if (texture != null) Object.DestroyImmediate(texture);
    }

    [Test]
    public void Defaults_MatchPlanTable()
    {
        AssertHex("E7E2EE", 1f, theme.backgroundBase, "배경 바탕색");
        Assert.AreEqual(0.45f, theme.illustrationSaturation, 1e-5f, "일러스트 채도 45%");
        Assert.AreEqual(1.2f, theme.illustrationBrightness, 1e-5f, "일러스트 밝기 120%");
        AssertHex("ECE8F2", 1f, theme.overlayColor, "덮는 색");
        Assert.AreEqual(0.80f, theme.overlayAlphaLeft, 1e-5f);
        Assert.AreEqual(0.62f, theme.overlayAlphaCenter, 1e-5f);
        Assert.AreEqual(0.82f, theme.overlayAlphaRight, 1e-5f);
        AssertHex("FFFFFF", 0.58f, theme.panel, "패널 흰색 58%");
        AssertHex("FFFFFF", 0.62f, theme.bottomBar, "하단 바 흰색 62%");
        AssertHex("3E9BE0", 1f, theme.accent, "포인트색");
        AssertHex("3E9BE0", 0.18f, theme.SelectedBackground, "선택 항목 18%");
        AssertHex("FFFFFF", 1f, theme.onAccentText, "포인트색 위 글자");
        AssertHex("2C2838", 1f, theme.textPrimary, "기본 글자");
        AssertHex("3E3852", 1f, theme.textSecondary, "보조 글자");
        AssertHex("5A5470", 1f, theme.textDescription, "설명 글자");
        AssertHex("7A7390", 1f, theme.textMuted, "흐린 글자");
        AssertHex("2C2838", 0.12f, theme.divider, "구분선");
        AssertHex("2E9C6A", 1f, theme.GetDifficulty("EASY"), "EASY");
        AssertHex("2F7FC8", 1f, theme.GetDifficulty("normal"), "NORMAL (대소문자 무시)");
        AssertHex("C2457E", 1f, theme.GetDifficulty("HARD"), "HARD");
        AssertHex("8B7FD1", 1f, theme.GetDifficulty("CUSTOM"), "CUSTOM");
        AssertHex("8B7FD1", 1f, theme.GetDifficulty("???"), "모르는 이름 = CUSTOM");
        AssertHex("FFFFFF", 1f, theme.onDifficultyText, "난이도 색 위 글자");

        Assert.IsNotNull(Resources.Load<UITheme>(UITheme.ResourcePath), "Resources/Data/UITheme.asset");
        Assert.IsNotNull(UITheme.Current);
    }

    [Test]
    public void RankEmpty_FillAndDashColors()
    {
        AssertHex("FFFFFF", 0.55f, theme.rankEmptyFill, "빈 자리 바탕");
        AssertHex("2C2838", 0.35f, theme.rankEmptyDash, "빈 자리 점선");
        UITheme asset = Resources.Load<UITheme>(UITheme.ResourcePath);
        AssertHex("FFFFFF", 0.55f, asset.rankEmptyFill, "에셋 빈 자리 바탕");
        AssertHex("2C2838", 0.35f, asset.rankEmptyDash, "에셋 빈 자리 점선");
    }

    [Test]
    public void Overlay_Alphas_LeftCenterRight()
    {
        Assert.AreEqual(0.80f, theme.OverlayAt(0f).a, 1e-5f, "왼쪽");
        Assert.AreEqual(0.62f, theme.OverlayAt(0.5f).a, 1e-5f, "가운데");
        Assert.AreEqual(0.82f, theme.OverlayAt(1f).a, 1e-5f, "오른쪽");
        Assert.AreEqual(0.71f, UITheme.OverlayAlphaAt(0.25f, 0.80f, 0.62f, 0.82f), 1e-5f, "사이는 직선");

        texture = MenuBackground.CreateOverlayTexture(theme, 65);
        Assert.AreEqual(0.80f, texture.GetPixel(0, 0).a, Tolerance, "텍스처 왼쪽 끝");
        Assert.AreEqual(0.62f, texture.GetPixel(32, 0).a, Tolerance, "텍스처 가운데");
        Assert.AreEqual(0.82f, texture.GetPixel(64, 0).a, Tolerance, "텍스처 오른쪽 끝");
        AssertHex("ECE8F2", 0.62f, texture.GetPixel(32, 0), "가운데 색");
    }

    [Test]
    public void ColorRole_UsesThemeValues()
    {
        Assert.AreEqual(theme.panel, UIThemeColor.GetColor(theme, UIColorRole.Panel));
        Assert.AreEqual(theme.bottomBar, UIThemeColor.GetColor(theme, UIColorRole.BottomBar));
        Assert.AreEqual(theme.divider, UIThemeColor.GetColor(theme, UIColorRole.Divider));
        Assert.AreEqual(theme.textDescription, UIThemeColor.GetColor(theme, UIColorRole.TextDescription));
        Assert.AreEqual(theme.difficultyCustom, UIThemeColor.GetColor(theme, UIColorRole.DifficultyCustom));
    }

    [Test]
    public void InGameDefaults_MatchPlanTable()
    {
        // 기본값과 실제로 쓰는 에셋(Resources/Data/UITheme) 둘 다 표와 같아야 한다
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            Assert.IsNotNull(target, "UITheme 에셋");
            string who = target == theme ? "기본값" : "에셋";
            AssertHex("17141F", 1f, target.gearBackground, who + " 기어 바탕 불투명 #17141F (11/19)");
            AssertHex("7CC4FA", 0.55f, target.gearBorder, who + " 기어 양옆 테두리");
            AssertHex("FFFFFF", 0.08f, target.laneDivider, who + " 라인 구분선");
            AssertHex("FFFFFF", 0.35f, target.measureLine, who + " 마디선");
            AssertHex("B4A6FF", 1f, target.noteOuter, who + " 바깥 라인 노트 라벤더 (11/20)");
            AssertHex("7CC4FA", 1f, target.noteInner, who + " 안쪽 라인 노트 하늘 (11/20)");
            AssertRgb(44, 40, 56, 1f, target.keyCell, who + " 키 칸 평소 불투명 (11/19)");
            AssertHex("FFFFFF", 0.12f, target.keyCellFrame, who + " 키 칸 테두리");
            AssertHex("FFFFFF", 1f, target.keyLabel, who + " 시작 키 글자 흰색 (11/20)");
            AssertHex("3E9BE0", 1f, target.keyCellPressed, who + " 키 칸 누름");
            AssertHex("FFFFFF", 1f, target.keyDiamondPressed, who + " 키 칸 마름모 누름 흰색 (11/20)");
            AssertHex("A9A3B8", 1f, target.comboTitle, who + " 콤보 라벨");
            AssertHex("FFFFFF", 1f, target.comboNumber, who + " 콤보 숫자");
            // 11/19 왼쪽 패널 라벤더 불투명 (배경 · BGA 와이어프레임)
            AssertHex("F4F1F8", 1f, target.inGamePanel, who + " 인게임 패널 #F4F1F8");
            AssertHex("FFFFFF", 1f, target.inGamePanelBorder, who + " 인게임 패널 테두리 흰색");
            AssertHex("FFFFFF", 1f, target.inGameStatBox, who + " RATE·SCORE 박스 흰색");
            AssertRgb(44, 40, 56, 0.14f, target.inGameStatBoxBorder, who + " 박스 테두리 14%");
            AssertHex("3E9BE0", 1f, target.inGameBrightnessBarOn, who + " 밝기 막대 켜짐");
            AssertRgb(44, 40, 56, 0.14f, target.inGameBrightnessBarOff, who + " 밝기 막대 꺼짐");
            Assert.AreEqual(target.inGamePanel, UIThemeColor.GetColor(target, UIColorRole.InGamePanel), who + " 역할 InGamePanel");
            Assert.AreEqual(target.inGameStatBoxBorder, UIThemeColor.GetColor(target, UIColorRole.InGameStatBoxBorder), who + " 역할 InGameStatBoxBorder");
        }
    }

    /// <summary>판정 색 B3안 (11/19, 11/20 GREAT · MISS · FAST · SLOW 바꿈): 메뉴용 6색 + PERFECT 그라데이션 두 색, 기본값과 에셋 둘 다</summary>
    [Test]
    public void JudgeColors_B3_DefaultsAndAsset()
    {
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            Assert.IsNotNull(target, "UITheme 에셋");
            string who = target == theme ? "기본값" : "에셋";
            // 메뉴용 (판정 패널 · 결과)
            AssertHex("B87A10", 1f, target.judgePerfectStart, who + " PERFECT 왼쪽 금");
            AssertHex("C8358C", 1f, target.judgePerfectEnd, who + " PERFECT 오른쪽 분홍");
            AssertHex("E0503E", 1f, target.judgeGreat, who + " GREAT (11/20)");
            AssertHex("2C9C66", 1f, target.judgeGood, who + " GOOD");
            AssertHex("6E6A7A", 1f, target.judgeMiss, who + " MISS (11/20)");
            AssertHex("3E63D6", 1f, target.judgeFast, who + " FAST (11/20)");
            AssertHex("A93BC2", 1f, target.judgeSlow, who + " SLOW (11/20)");
            // 기어 위 판정 글자는 색이 든 이미지 (JudgeSprites, 11/19)라 테마 색이 없다
            Assert.IsTrue(target.GetMenuJudge(JudgeType.Perfect).IsGradient, who + " 메뉴 PERFECT 그라데이션");
            Assert.IsFalse(target.GetMenuJudge(JudgeType.Great).IsGradient, who + " GREAT 단색");
            AssertHex("E0503E", 1f, target.GetMenuJudge(JudgeType.Great).Start, who + " 판정 패널 GREAT");
            // 난이도 색은 판정 색과 따로 (그대로)
            AssertHex("2F7FC8", 1f, target.difficultyNormal, who + " NORMAL 난이도 그대로");
        }
    }

    /// <summary>판정 글자 칠하기: PERFECT는 colorGradient(왼쪽 = 첫 색, 오른쪽 = 둘째 색), 단색은 그라데이션 끔</summary>
    [Test]
    public void JudgeText_GradientAndSolid()
    {
        GameObject go = new GameObject("Txt_JudgeColorTest", typeof(RectTransform));
        try
        {
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            JudgeColor perfect = theme.GetMenuJudge(JudgeType.Perfect);
            UIThemePaint.JudgeText(text, perfect, 0.5f);
            Assert.IsTrue(text.enableVertexGradient);
            Assert.AreEqual(perfect.Start, text.colorGradient.topLeft);
            Assert.AreEqual(perfect.Start, text.colorGradient.bottomLeft);
            Assert.AreEqual(perfect.End, text.colorGradient.topRight);
            Assert.AreEqual(perfect.End, text.colorGradient.bottomRight);
            Assert.AreEqual(new Color(1f, 1f, 1f, 0.5f), text.color, "글자 색은 흰색 × 알파");

            UIThemePaint.JudgeText(text, theme.GetMenuJudge(JudgeType.Great));
            Assert.IsFalse(text.enableVertexGradient, "단색은 그라데이션 끔");
            Assert.AreEqual(theme.judgeGreat, text.color);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LongNoteBody_TopAndBottomAlpha()
    {
        // 11/20 정정: 위(꼬리) 45% → 아래(머리) 70%, 기본값과 에셋 둘 다
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            Assert.AreEqual(0.45f, target.longNoteTopAlpha, 1e-5f, "위 45%");
            Assert.AreEqual(0.70f, target.longNoteBottomAlpha, 1e-5f, "아래 70%");
        }
        Assert.AreEqual(0.70f, UITheme.LongNoteAlphaAt(0f, theme.longNoteBottomAlpha, theme.longNoteTopAlpha), 1e-5f, "아래 끝(머리)");
        Assert.AreEqual(0.575f, UITheme.LongNoteAlphaAt(0.5f, theme.longNoteBottomAlpha, theme.longNoteTopAlpha), 1e-5f, "가운데");
        Assert.AreEqual(0.45f, UITheme.LongNoteAlphaAt(1f, theme.longNoteBottomAlpha, theme.longNoteTopAlpha), 1e-5f, "위 끝(꼬리)");
        Assert.AreEqual(0.45f, UITheme.LongNoteAlphaAt(3f, theme.longNoteBottomAlpha, theme.longNoteTopAlpha), 1e-5f, "범위 밖은 끝 값");
        AssertHex("FFFFFF", 0.78f, theme.longNoteCenterLine, "가운데 선 흰색 78% (11/20)");
        AssertHex("FFFFFF", 0.75f, theme.noteShine, "광택 흰색 75% (11/20)");
    }

    /// <summary>11/20 라인 색: 라인 0 · 3 라벤더, 1 · 2 하늘. 빛은 라인 색 35%. 기본값과 에셋 둘 다</summary>
    [Test]
    public void NoteColor_ByLane_DefaultsAndAsset()
    {
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            string who = target == theme ? "기본값" : "에셋";
            string[] expected = { "B4A6FF", "7CC4FA", "7CC4FA", "B4A6FF" };
            Assert.AreEqual(expected.Length, GameConstants.Editor.LaneCount, "라인 수");
            for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
            {
                AssertHex(expected[lane], 1f, target.NoteColor(lane), who + " 라인 " + lane + " 노트");
                AssertHex(expected[lane], 0.35f, target.NoteGlowColor(lane), who + " 라인 " + lane + " 빛");
            }
            Assert.IsTrue(UITheme.IsOuterLane(0) && UITheme.IsOuterLane(3), "바깥 라인 0 · 3");
            Assert.IsFalse(UITheme.IsOuterLane(1) || UITheme.IsOuterLane(2), "안쪽 라인 1 · 2");
        }
    }

    /// <summary>11/20 판정 이펙트 색: C 3색, MISS 없음, L = C와 흰색 55%</summary>
    [Test]
    public void HitFxColors_DefaultsAndAsset()
    {
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            string who = target == theme ? "기본값" : "에셋";
            Assert.IsTrue(target.TryGetHitFxColor(JudgeType.Perfect, out Color perfect));
            AssertHex("FFA698", 1f, perfect, who + " PERFECT");
            Assert.IsTrue(target.TryGetHitFxColor(JudgeType.Great, out Color great));
            AssertHex("FF7E6E", 1f, great, who + " GREAT");
            Assert.IsTrue(target.TryGetHitFxColor(JudgeType.Good, out Color good));
            AssertHex("6FE0A4", 1f, good, who + " GOOD");
            Assert.IsFalse(target.TryGetHitFxColor(JudgeType.Miss, out _), who + " MISS는 이펙트 없음");
            Assert.AreEqual(0.55f, target.hitFxLightMix, 1e-5f, who + " 밝은 색 섞는 비율");
        }
        Color light = UITheme.HitFxLight(new Color32(0xFF, 0x7E, 0x6E, 0xFF), 0.55f);
        Assert.AreEqual(1f, light.r, 1e-3f);
        Assert.AreEqual((0x7E + (255 - 0x7E) * 0.55f) / 255f, light.g, 1e-3f, "L = Lerp(C, 흰색, 0.55)");
        Assert.AreEqual(1f, light.a, 1e-5f);
    }

    private static void AssertRgb(int r, int g, int b, float alpha, Color actual, string message)
    {
        AssertHex($"{r:X2}{g:X2}{b:X2}", alpha, actual, message);
    }

    private static void AssertHex(string hex, float alpha, Color actual, string message)
    {
        Assert.IsTrue(ColorUtility.TryParseHtmlString("#" + hex, out Color expected));
        Assert.AreEqual(expected.r, actual.r, Tolerance, message + " (r)");
        Assert.AreEqual(expected.g, actual.g, Tolerance, message + " (g)");
        Assert.AreEqual(expected.b, actual.b, Tolerance, message + " (b)");
        Assert.AreEqual(alpha, actual.a, Tolerance, message + " (a)");
    }
}
