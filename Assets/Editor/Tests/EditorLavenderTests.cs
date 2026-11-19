using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 에디터 라벤더 톤 개편 (기획서 3-6 「에디터 라벤더 톤 개편 (11/19)」) EditMode 확인.
/// 1. 테스트 플레이 배속 저장: 처음 1.0, 바꾼 값 저장 → 불러오기, 예전 JSON → 1.0, 범위 밖 → 범위 안, 인게임 배속과 서로 영향 없음
/// 2. UITheme 에디터 묶음 · 고른 도구 색 · 그리드 선 색 기본값이 기획서 표와 같음 (기본값과 실제 에셋 둘 다)
/// </summary>
public class EditorLavenderTests
{
    private const float Eps = 1.5f / 255f;

    private static void AssertRgba(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, Eps, message + " r");
        Assert.AreEqual(expected.g, actual.g, Eps, message + " g");
        Assert.AreEqual(expected.b, actual.b, Eps, message + " b");
        Assert.AreEqual(expected.a, actual.a, Eps, message + " a");
    }

    private static Color Hex(string hex, float alpha)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha;
        return c;
    }

    [Test]
    public void TestPlaySpeed_SaveLoad_Legacy_Range_Independent()
    {
        LastSelectionSaveData fresh = new LastSelectionSaveData();
        Assert.AreEqual(1f, fresh.editorTestPlayMultiplier, "처음 1.0");

        PlayerData data = new PlayerData();
        Assert.IsTrue(PlayerDataRules.TrySetEditorTestPlayMultiplier(data.lastSelection, 2.6f));
        Assert.IsFalse(PlayerDataRules.TrySetEditorTestPlayMultiplier(data.lastSelection, 2.6f), "같은 값이면 저장하지 않음");
        Assert.AreEqual(1f, data.lastSelection.scrollMultiplier, "인게임 배속은 그대로");
        PlayerData loaded = PlayerDataRules.Sanitize(PlayerDataStore.Parse(JsonUtility.ToJson(data)));
        Assert.AreEqual(2.6f, loaded.lastSelection.editorTestPlayMultiplier, 1e-4f, "저장 → 불러오기");
        Assert.AreEqual(2.6f, loaded.lastSelection.Clone().editorTestPlayMultiplier, 1e-4f, "복사본도 같은 값");

        // 인게임 배속을 바꿔도 테스트 플레이 배속은 그대로
        Assert.IsTrue(PlayerDataRules.TrySetLastScrollMultiplier(loaded.lastSelection, 4.2f));
        Assert.AreEqual(2.6f, loaded.lastSelection.editorTestPlayMultiplier, 1e-4f, "인게임 배속과 따로");

        const string legacy = "{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":2.5,\"official\":{\"songId\":\"\",\"chartId\":\"\"},\"custom\":{\"songId\":\"\",\"chartId\":\"\"}}}";
        PlayerData old = PlayerDataRules.Sanitize(PlayerDataStore.Parse(legacy));
        Assert.AreEqual(1f, old.lastSelection.editorTestPlayMultiplier, 1e-4f, "예전 JSON → 1.0");
        Assert.AreEqual(2.5f, old.lastSelection.scrollMultiplier, 1e-4f);

        LastSelectionSaveData odd = new LastSelectionSaveData { editorTestPlayMultiplier = 12.3f };
        PlayerDataRules.SanitizeLastSelection(odd);
        Assert.AreEqual(ScrollSpeedRules.FromSteps(ScrollSpeedRules.MaxSteps), odd.editorTestPlayMultiplier, 1e-4f, "범위 위 → 최대");
        odd.editorTestPlayMultiplier = 0.2f;
        PlayerDataRules.SanitizeLastSelection(odd);
        Assert.AreEqual(ScrollSpeedRules.FromSteps(ScrollSpeedRules.MinSteps), odd.editorTestPlayMultiplier, 1e-4f, "범위 아래 → 최소");
        odd.editorTestPlayMultiplier = 3.04f;
        PlayerDataRules.SanitizeLastSelection(odd);
        Assert.AreEqual(3.0f, odd.editorTestPlayMultiplier, 1e-4f, "0.1 단위로");
    }

    [Test]
    public void Theme_EditorGroup_ToolColors_GridLines()
    {
        foreach (UITheme target in new[] { ScriptableObject.CreateInstance<UITheme>(), Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            Assert.IsNotNull(target);
            string who = target.name == string.Empty ? "기본값" : "에셋";
            AssertRgba(Hex("7B6BD8", 1f), target.editorAutoTest, who + " 자동 테스트 보라");
            AssertRgba(Hex("C97418", 1f), target.editorAmber, who + " 호박");
            AssertRgba(Hex("FFB45C", 1f), target.editorAmberLight, who + " 밝은 호박");
            AssertRgba(Hex("17141F", 1f), target.editorGridBackground, who + " 그리드 바탕");
            AssertRgba(new Color(1f, 1f, 1f, 0.04f), target.editorGridHatch, who + " 곡 시작 전 빗금");
            AssertRgba(Hex("F4F1F9", 1f), target.editorNote, who + " 노트");
            AssertRgba(Hex("FF5FB4", 1f), target.editorCurrentLineStart, who + " 현재 위치 선 왼쪽");
            AssertRgba(Hex("9B6BFF", 1f), target.editorCurrentLineEnd, who + " 현재 위치 선 오른쪽");
            AssertRgba(Color.white, target.editorCurrentLineFlash, who + " 반짝임 흰색");
            Assert.AreEqual(0.22f, target.editorIllustrationOpacity, 1e-4f, who + " 일러스트 22%");
            Assert.AreEqual(40f, target.editorIllustrationBlur, 1e-4f, who + " 흐림 40");
            Assert.AreEqual(0.5f, target.editorIllustrationSaturation, 1e-4f, who + " 채도 50%");
            Assert.AreEqual(0.55f, target.editorOverlayTop, 1e-4f, who + " 그라데이션 위 55%");
            Assert.AreEqual(0.85f, target.editorOverlayBottom, 1e-4f, who + " 그라데이션 아래 85%");

            AssertRgba(Hex("3E9BE0", 0.14f), target.toolSelectedBackground, who + " 고른 도구 바탕");
            AssertRgba(Hex("3E9BE0", 1f), target.toolSelectedBorder, who + " 고른 도구 테두리");
            AssertRgba(Hex("2F7FC8", 1f), target.toolSelectedText, who + " 고른 도구 글자");
            Assert.AreEqual(1.5f, target.toolSelectedBorderWidth, 1e-4f, who + " 테두리 1.5");
            AssertRgba(Color.white, target.toolNormalBackground, who + " 도구 바탕");
            AssertRgba(Hex("2C2838", 0.10f), target.toolNormalBorder, who + " 도구 테두리");
            AssertRgba(Hex("2C2838", 1f), target.toolNormalText, who + " 도구 글자");

            AssertLine(target.editorLineMeasure, new Color(1f, 1f, 1f, 0.70f), 2f, who + " 마디");
            AssertLine(target.editorLineBeat, Hex("FF7AC6", 0.50f), 1f, who + " 1박");
            AssertLine(target.editorLine8th, Hex("7DD3FC", 0.32f), 1f, who + " 8분");
            AssertLine(target.editorLine16th, Hex("7DD3FC", 0.14f), 1f, who + " 16분");
            AssertLine(target.editorLine32nd, Hex("7DD3FC", 0.08f), 1f, who + " 32분");
            AssertLine(target.editorLineTriplet, Hex("A99CFF", 0.30f), 1f, who + " 셋잇단");
            AssertLine(target.editorLineTripletFine, Hex("A99CFF", 0.14f), 1f, who + " 12분");
            AssertLine(target.editorLineOther, new Color(1f, 1f, 1f, 0.06f), 1f, who + " 나머지");
            AssertLine(target.editorLaneDivider, new Color(1f, 1f, 1f, 0.10f), 1f, who + " 라인 구분선");
        }

        // 선 종류 → 색 연결 (음악상 길이 기준: Half = 8분, Quarter = 16분, Eighth = 32분)
        UITheme theme = UITheme.Current;
        Assert.AreSame(theme.editorLine8th, ChartGridView.StyleOf(GridLineType.Half));
        Assert.AreSame(theme.editorLine16th, ChartGridView.StyleOf(GridLineType.Quarter));
        Assert.AreSame(theme.editorLine32nd, ChartGridView.StyleOf(GridLineType.Eighth));
        Assert.AreSame(theme.editorLineOther, ChartGridView.StyleOf(GridLineType.Sixteenth));
        Assert.AreSame(theme.editorLineTriplet, ChartGridView.StyleOf(GridLineType.Third));
        Assert.AreSame(theme.editorLineTripletFine, ChartGridView.StyleOf(GridLineType.Twelfth));
    }

    private static void AssertLine(GridLineStyle style, Color color, float thickness, string message)
    {
        AssertRgba(color, style.color, message);
        Assert.AreEqual(thickness, style.thickness, 1e-4f, message + " 굵기");
    }

    [Test]
    public void Neighbors_PreviousNextOrNone()
    {
        var sections = new System.Collections.Generic.List<BpmSection>
        {
            new BpmSection(0, 150.61f), new BpmSection(GameConstants.Editor.TicksPerBeat * 16, 180f)
        };
        string add = Popup_BpmSection.FormatNeighbors(sections, GameConstants.Editor.TicksPerBeat * 4, false, Color.black);
        Assert.AreEqual("이전 구간 <color=#000000>150.61</color> · 다음 구간 <color=#000000>180</color>", add, "추가: 앞 · 뒤 구간");
        string edit = Popup_BpmSection.FormatNeighbors(sections, GameConstants.Editor.TicksPerBeat * 16, true, Color.black);
        Assert.AreEqual("이전 구간 <color=#000000>150.61</color> · 다음 구간 없음", edit, "수정: 자기 구간 앞 · 뒤");
    }
}
