using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 화면 톤 (UITheme, 기획서 3장 「UI 톤 · 라벤더 안개 (11/17)」) EditMode 확인.
/// 1. 기본값 = 기획서 표 (색 몇 개 + 덮는 색 불투명도 3개), 에셋(Resources/Data/UITheme)이 있음
/// 2. 도장 규칙: AP+FC → AP, FC만 → FC, 둘 다 아님 → 없음 (Img_Stamp가 한 함수 SongSelectRecordFormat.GetStamp를 따른다)
/// 3. 덮는 색 그라데이션: 왼쪽·가운데·오른쪽 알파 = 0.80 · 0.62 · 0.82 (함수와 MenuBackground가 만든 텍스처 둘 다)
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
        AssertHex("B7861E", 1f, theme.judgePerfect, "PERFECT");
        AssertHex("2F7FC8", 1f, theme.judgeGreat, "GREAT");
        AssertHex("2E9C6A", 1f, theme.judgeGood, "GOOD");
        AssertHex("C9504A", 1f, theme.judgeMiss, "MISS");
        AssertHex("2E9C8F", 1f, theme.judgeFast, "FAST");
        AssertHex("D27A3C", 1f, theme.judgeSlow, "SLOW");
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
    public void Stamp_ApWins_FcOnly_None()
    {
        root = new GameObject("Img_Stamp", typeof(RectTransform), typeof(Image));
        GameObject label = new GameObject("Txt_Stamp", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        Img_Stamp stamp = root.AddComponent<Img_Stamp>();
        SerializedObject so = new SerializedObject(stamp);
        so.FindProperty("label").objectReferenceValue = label.GetComponent<TMP_Text>();
        so.ApplyModifiedPropertiesWithoutUndo();

        stamp.SetStamp(SongSelectRecordFormat.GetStamp(true, true));
        Assert.AreEqual(RecordStamp.AllPerfect, stamp.Stamp, "AP+FC → AP만");
        Assert.AreEqual("AP", label.GetComponent<TMP_Text>().text);
        Assert.IsTrue(root.activeSelf);
        AssertHex("B7861E", 1f, root.GetComponent<Image>().color, "AP = PERFECT 색");

        stamp.SetStamp(SongSelectRecordFormat.GetStamp(true, false));
        Assert.AreEqual("FC", label.GetComponent<TMP_Text>().text, "FC만 → FC");
        AssertHex("3E9BE0", 1f, root.GetComponent<Image>().color, "FC = 포인트색");

        stamp.SetStamp(SongSelectRecordFormat.GetStamp(false, false));
        Assert.IsFalse(root.activeSelf, "둘 다 아님 → 도장 없음");
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

    private static void AssertHex(string hex, float alpha, Color actual, string message)
    {
        Assert.IsTrue(ColorUtility.TryParseHtmlString("#" + hex, out Color expected));
        Assert.AreEqual(expected.r, actual.r, Tolerance, message + " (r)");
        Assert.AreEqual(expected.g, actual.g, Tolerance, message + " (g)");
        Assert.AreEqual(expected.b, actual.b, Tolerance, message + " (b)");
        Assert.AreEqual(alpha, actual.a, Tolerance, message + " (a)");
    }
}
