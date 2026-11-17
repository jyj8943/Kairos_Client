using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택 강조 (UISelectHighlight, 기획서 3-2 타이틀 메뉴) EditMode 확인.
/// - 모양 계산(GetLook): 선택 = 바탕·막대 표시, 한글 흰색 Bold, 영문 포인트색 / 선택 안 됨 = 바탕·막대 없음, 흐린 흰 글자
/// - 컴포넌트: OnSelect·OnDeselect가 바탕 색·막대 켜짐·글자색·굵기를 바꾼다
/// </summary>
public class UISelectHighlightTests
{
    private const float Tolerance = 1e-4f;

    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void GetLook_Selected_ShowsBackgroundAndBar_WhiteBoldMain_AccentSub()
    {
        UISelectHighlight.Look look = UISelectHighlight.GetLook(true);
        Color accent = GameConstants.SelectHighlight.AccentColor;

        Assert.IsTrue(look.ShowBackground);
        Assert.IsTrue(look.ShowBar);
        AssertColor(new Color(accent.r, accent.g, accent.b, GameConstants.SelectHighlight.SelectedBackgroundAlpha), look.BackgroundColor, "바탕 = 포인트색 반투명");
        AssertColor(Color.white, look.MainTextColor, "한글 흰색");
        AssertColor(accent, look.SubTextColor, "영문 포인트색");
        Assert.AreEqual(FontStyles.Bold, look.MainFontStyle);
    }

    [Test]
    public void GetLook_NotSelected_NoBackgroundNoBar_DimWhiteText()
    {
        UISelectHighlight.Look look = UISelectHighlight.GetLook(false);

        Assert.IsFalse(look.ShowBackground);
        Assert.IsFalse(look.ShowBar);
        Assert.AreEqual(0f, look.BackgroundColor.a, Tolerance, "바탕은 알파 0 (클릭은 그대로 받음)");
        AssertColor(new Color(1f, 1f, 1f, GameConstants.SelectHighlight.NormalMainTextAlpha), look.MainTextColor, "한글 흰색 약 70%");
        AssertColor(new Color(1f, 1f, 1f, GameConstants.SelectHighlight.NormalSubTextAlpha), look.SubTextColor, "영문 흰색 약 45%");
        Assert.AreEqual(FontStyles.Normal, look.MainFontStyle);
    }

    [Test]
    public void SelectThenDeselect_AppliesLookToGraphics()
    {
        root = new GameObject("Btn_Test", typeof(RectTransform), typeof(Image), typeof(Button));
        Image background = root.GetComponent<Image>();
        Image bar = CreateChild("Img_SelectBar").AddComponent<Image>();
        TextMeshProUGUI main = CreateChild("Txt_Test").AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI sub = CreateChild("Txt_TestEn").AddComponent<TextMeshProUGUI>();

        UISelectHighlight highlight = root.AddComponent<UISelectHighlight>();
        SerializedObject so = new SerializedObject(highlight);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("accentBar").objectReferenceValue = bar;
        so.FindProperty("mainLabel").objectReferenceValue = main;
        so.FindProperty("subLabel").objectReferenceValue = sub;
        so.ApplyModifiedPropertiesWithoutUndo();

        highlight.OnSelect(null);
        Assert.IsTrue(highlight.IsHighlighted);
        Assert.IsTrue(bar.gameObject.activeSelf, "선택: 막대 켜짐");
        Assert.AreEqual(GameConstants.SelectHighlight.SelectedBackgroundAlpha, background.color.a, Tolerance, "선택: 바탕 보임");
        AssertColor(Color.white, main.color, "선택: 한글 흰색");
        Assert.AreEqual(FontStyles.Bold, main.fontStyle, "선택: 한글 Bold");
        AssertColor(GameConstants.SelectHighlight.AccentColor, sub.color, "선택: 영문 포인트색");

        highlight.OnDeselect(null);
        Assert.IsFalse(highlight.IsHighlighted);
        Assert.IsFalse(bar.gameObject.activeSelf, "선택 해제: 막대 꺼짐");
        Assert.AreEqual(0f, background.color.a, Tolerance, "선택 해제: 바탕 안 보임");
        Assert.AreEqual(GameConstants.SelectHighlight.NormalMainTextAlpha, main.color.a, Tolerance, "선택 해제: 한글 흐림");
        Assert.AreEqual(FontStyles.Normal, main.fontStyle, "선택 해제: 한글 보통 굵기");
        Assert.AreEqual(GameConstants.SelectHighlight.NormalSubTextAlpha, sub.color.a, Tolerance, "선택 해제: 영문 흐림");
    }

    private GameObject CreateChild(string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        return child;
    }

    private static void AssertColor(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, Tolerance, message + " (r)");
        Assert.AreEqual(expected.g, actual.g, Tolerance, message + " (g)");
        Assert.AreEqual(expected.b, actual.b, Tolerance, message + " (b)");
        Assert.AreEqual(expected.a, actual.a, Tolerance, message + " (a)");
    }
}
