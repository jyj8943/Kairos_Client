using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// Txt_Combo가 ComboMotion 값을 화면에 적용하는지 EditMode 테스트 (숫자 위치·알파, Combo 그룹 알파·위치, 제목은 그대로).
/// </summary>
public class TxtComboViewTests
{
    private GameObject root;
    private RectTransform groupRect;
    private CanvasGroup group;
    private RectTransform titleRect;
    private TextMeshProUGUI number;
    private Txt_Combo combo;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        var groupGo = new GameObject("Combo", typeof(RectTransform), typeof(CanvasGroup));
        groupRect = (RectTransform)groupGo.transform;
        groupRect.SetParent(root.transform, false);
        groupRect.anchoredPosition = new Vector2(0f, 200f);
        group = groupGo.GetComponent<CanvasGroup>();

        var titleGo = new GameObject("Txt_ComboTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleRect = (RectTransform)titleGo.transform;
        titleRect.SetParent(groupRect, false);
        titleRect.anchoredPosition = new Vector2(0f, 66f);

        var numberGo = new GameObject("Txt_Combo", typeof(RectTransform), typeof(TextMeshProUGUI));
        numberGo.transform.SetParent(groupRect, false);
        number = numberGo.GetComponent<TextMeshProUGUI>();
        number.color = new Color(1f, 1f, 1f, 0.7f);
        combo = numberGo.AddComponent<Txt_Combo>();

        var so = new UnityEditor.SerializedObject(combo);
        so.FindProperty("comboGroup").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
        combo.ResetView();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    /// <summary>처음 상태: 그룹 알파 0, 숫자 비어 있음</summary>
    [Test]
    public void ResetView_Hidden()
    {
        Assert.AreEqual(0f, group.alpha, 1e-4f);
        Assert.AreEqual(string.Empty, number.text);
    }

    /// <summary>오르면 숫자만 30px 아래 투명에서 시작해 0.13초 뒤 제자리·기본 알파(0.7). 제목은 그대로</summary>
    [Test]
    public void SetCombo_AppliesRise()
    {
        combo.SetCombo(12);
        Assert.AreEqual("12", number.text);
        Assert.AreEqual(1f, group.alpha, 1e-4f);
        Assert.AreEqual(-30f, number.rectTransform.anchoredPosition.y, 1e-3f);
        Assert.AreEqual(0f, number.color.a, 1e-4f);

        combo.Tick(0.13f);
        Assert.AreEqual(0f, number.rectTransform.anchoredPosition.y, 1e-3f);
        Assert.AreEqual(0.7f, number.color.a, 1e-4f, "기본 알파(Inspector 색)에 곱한다");
        Assert.AreEqual(66f, titleRect.anchoredPosition.y, 1e-4f, "COMBO 제목은 움직이지 않는다");
        Assert.AreEqual(Vector3.one, number.rectTransform.localScale, "크기는 바꾸지 않는다");
    }

    /// <summary>쉬면 그룹이 8px 내려가며 사라지고, 다시 오르면 그룹 위치·알파가 바로 돌아온다</summary>
    [Test]
    public void Idle_HidesGroupAndRestores()
    {
        combo.SetCombo(3);
        combo.Tick(0.74f);
        Assert.AreEqual(0f, group.alpha, 1e-4f);
        Assert.AreEqual(192f, groupRect.anchoredPosition.y, 1e-3f);

        combo.SetCombo(4);
        Assert.AreEqual(1f, group.alpha, 1e-4f);
        Assert.AreEqual(200f, groupRect.anchoredPosition.y, 1e-3f);
        Assert.AreEqual("4", number.text);
    }

    /// <summary>0이면 바로 숨김, ResetView는 위치·알파를 처음으로 되돌린다 (재시작)</summary>
    [Test]
    public void Zero_AndResetView()
    {
        combo.SetCombo(8);
        combo.Tick(0.7f);
        combo.SetCombo(0);
        Assert.AreEqual(0f, group.alpha, 1e-4f);
        Assert.AreEqual(200f, groupRect.anchoredPosition.y, 1e-3f);
        Assert.AreEqual(string.Empty, number.text);

        combo.SetCombo(5);
        combo.ResetView();
        Assert.AreEqual(0f, group.alpha, 1e-4f);
        Assert.AreEqual(0f, number.rectTransform.anchoredPosition.y, 1e-3f);
        Assert.AreEqual(0, combo.Motion.Combo);
    }
}
