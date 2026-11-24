using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;

/// <summary>
/// 긴 제목 전광판(UIMarqueeText · MarqueeTimeline) EditMode 확인: 시간표, 넘침 판단, 정렬 유지, 처음 상태로 되돌림, 프레임당 GC 할당 0.
/// 화면 연결(곡 선택 · 목록 · 곡 로딩 · 인게임 · 에디터)은 MarqueePlayTests, 결과 화면은 ResultIntroPlayTests I6이 본다.
/// </summary>
public class MarqueeTests
{
    private const string LongTitle = "Send My Love Tonight Send My Love Tonight";
    private const float Cell = 200f;

    private GameObject root;
    private UIMarqueeText marquee;
    private TMP_Text text;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Canvas_Test", typeof(RectTransform), typeof(Canvas));
        GameObject viewport = new GameObject("TitleViewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        ((RectTransform)viewport.transform).sizeDelta = new Vector2(Cell, 40f);
        GameObject textObject = new GameObject("Txt_Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(viewport.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        text = textObject.GetComponent<TMP_Text>();
        text.fontSize = 24f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        marquee = viewport.AddComponent<UIMarqueeText>();
        Set("text", text);
        Set("textGroup", textObject.GetComponent<CanvasGroup>());
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    private void Set(string field, object value)
    {
        typeof(UIMarqueeText).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(marquee, value);
    }

    /// <summary>한 바퀴: 멈춤 1.5 → 이동 120px ÷ 60 = 2초 → 멈춤 1.0 → 사라짐 0.25 → 처음에서 나타남 0.25</summary>
    [TestCase(0f, 0f, 1f)]
    [TestCase(1.5f, 0f, 1f)]
    [TestCase(2.5f, 60f, 1f)]
    [TestCase(3.5f, 120f, 1f)]
    [TestCase(4.5f, 120f, 1f)]
    [TestCase(4.625f, 120f, 0.5f)]
    [TestCase(4.75f, 0f, 0f)]
    [TestCase(4.875f, 0f, 0.5f)]
    [TestCase(5f, 0f, 1f)]
    public void Timeline_Overflow120(float time, float offset, float alpha)
    {
        MarqueeTimeline.Evaluate(time, 120f, out float actualOffset, out float actualAlpha);
        Assert.AreEqual(offset, actualOffset, 1e-3f, "위치");
        Assert.AreEqual(alpha, actualAlpha, 1e-3f, "알파");
        Assert.AreEqual(5f, MarqueeTimeline.CycleSeconds(120f), 1e-4f);
    }

    [Test]
    public void Timeline_NoOverflow_NeverMoves()
    {
        for (float t = 0f; t < 10f; t += 0.1f)
        {
            MarqueeTimeline.Evaluate(t, 0f, out float offset, out float alpha);
            Assert.AreEqual(0f, offset);
            Assert.AreEqual(1f, alpha);
        }
    }

    /// <summary>칸에 들어오면 움직이지 않고, 글자 칸 = 칸 폭이라 왼쪽 · 가운데 · 오른쪽 정렬이 그대로다</summary>
    [TestCase(TextAlignmentOptions.Left)]
    [TestCase(TextAlignmentOptions.Center)]
    [TestCase(TextAlignmentOptions.Right)]
    public void Short_DoesNotScroll_KeepsAlignment(TextAlignmentOptions alignment)
    {
        text.alignment = alignment;
        marquee.SetText("Hi");
        for (int i = 0; i < 200; i++) marquee.Tick(0.05f);

        Assert.IsFalse(marquee.IsScrolling);
        Assert.AreEqual(0f, marquee.OverflowWidth);
        Assert.AreEqual(0f, marquee.CurrentOffset);
        Assert.AreEqual(alignment, text.alignment);
        Assert.AreEqual(Cell, text.rectTransform.rect.width, 0.01f, "글자 칸 = 칸");
        Assert.IsFalse(marquee.GetComponent<RectMask2D>().enabled, "자를 것이 없으면 마스크도 끔");
    }

    [Test]
    public void Long_Scrolls_EndTouchesRightEdge_ThenReturns()
    {
        marquee.SetText(LongTitle);
        marquee.Tick(0f);
        Assert.IsTrue(marquee.IsScrolling);
        Assert.AreEqual(text.preferredWidth - Cell, marquee.OverflowWidth, 0.01f, "이동 거리 = 글자 폭 − 칸 폭");
        Assert.AreEqual(TextOverflowModes.Overflow, text.overflowMode);
        Assert.Greater(text.rectTransform.rect.width, marquee.TextWidth, "글자 칸이 글자보다 넓음");

        marquee.Tick(GameConstants.Marquee.StartDelay + 1f);
        Assert.AreEqual(60f, marquee.CurrentOffset, 0.01f, "1초에 60px");
        Assert.AreEqual(-60f, text.rectTransform.anchoredPosition.x, 0.01f);

        marquee.Tick(MarqueeTimeline.MoveSeconds(marquee.OverflowWidth) - 1f + 0.5f);
        Assert.AreEqual(marquee.OverflowWidth, marquee.CurrentOffset, 0.01f, "끝 글자가 칸 오른쪽 끝");
        Assert.AreEqual(Cell, marquee.TextWidth - marquee.CurrentOffset, 0.01f);

        marquee.Tick(0.5f + GameConstants.Marquee.ReturnFade * 0.5f);
        Assert.AreEqual(0.5f, text.GetComponent<CanvasGroup>().alpha, 0.01f, "사라지는 중");
        marquee.Tick(GameConstants.Marquee.ReturnFade * 1.5f + 0.1f);
        Assert.AreEqual(0f, marquee.CurrentOffset, "다음 바퀴 처음 멈춤");
        Assert.AreEqual(1f, text.GetComponent<CanvasGroup>().alpha);
    }

    /// <summary>처음 · 끝에 멈춘 글자 획은 경계 흐림이 닿지 않고(흐림이 칸 밖), 중간에는 흐림이 칸 안에 있다</summary>
    [Test]
    public void EdgeSoftness_OutsideCellAtBothEnds()
    {
        RectMask2D mask = marquee.GetComponent<RectMask2D>();
        marquee.SetText(LongTitle);
        marquee.Tick(0f);
        Assert.AreEqual(new Vector2Int(12, 0), mask.softness);
        Assert.AreEqual(-12f, mask.padding.x, "처음: 왼쪽 흐림은 칸 밖");
        Assert.AreEqual(0f, mask.padding.z, "처음: 오른쪽 흐림은 칸 안");
        marquee.Tick(GameConstants.Marquee.StartDelay + 1f);
        Assert.AreEqual(0f, mask.padding.x);
        Assert.AreEqual(0f, mask.padding.z);
        marquee.Tick(MarqueeTimeline.MoveSeconds(marquee.OverflowWidth) - 1f + 0.5f);
        Assert.AreEqual(0f, mask.padding.x);
        Assert.AreEqual(-12f, mask.padding.z, "끝: 오른쪽 흐림은 칸 밖");
    }

    [Test]
    public void TextOrFontSizeChange_RestartsAndRemeasures()
    {
        marquee.SetText(LongTitle);
        marquee.Tick(3f);
        Assert.Greater(marquee.CurrentOffset, 0f);
        float overflow = marquee.OverflowWidth;

        text.text = LongTitle + "!!!!"; // SetText를 거치지 않은 변경도 감지
        marquee.Tick(0.1f);
        Assert.AreEqual(0f, marquee.CurrentOffset, "글자가 바뀌면 처음부터");
        Assert.Greater(marquee.OverflowWidth, overflow);

        marquee.Tick(3f);
        text.fontSize = 20f;
        marquee.Tick(0.1f);
        Assert.AreEqual(0f, marquee.CurrentOffset, "글자 크기가 바뀌면 처음부터");
        Assert.AreEqual(text.preferredWidth - Cell, marquee.OverflowWidth, 0.01f);

        marquee.SetText("Hi");
        marquee.Tick(0.1f);
        Assert.IsFalse(marquee.IsScrolling);
        Assert.AreEqual(Cell, text.rectTransform.rect.width, 0.01f);
    }

    /// <summary>목록 칸: 고르지 않으면 …, 고르면 흐르고, 다시 풀면 처음 위치의 …</summary>
    [Test]
    public void StoppedWithEllipsis_UsesEllipsis_PlayingUsesOverflow()
    {
        Set("playOnStart", false);
        Set("ellipsisWhenStopped", true);
        marquee.SetText(LongTitle);
        marquee.Tick(5f);
        Assert.IsFalse(marquee.IsScrolling);
        Assert.AreEqual(0f, marquee.CurrentOffset);
        Assert.AreEqual(TextOverflowModes.Ellipsis, text.overflowMode);
        Assert.AreEqual(Cell, text.rectTransform.rect.width, 0.01f);

        marquee.SetPlaying(true);
        marquee.Tick(0f);
        Assert.IsTrue(marquee.IsScrolling);
        Assert.AreEqual(TextOverflowModes.Overflow, text.overflowMode);
        marquee.Tick(2.5f);
        Assert.AreEqual(60f, marquee.CurrentOffset, 0.01f);

        marquee.SetPlaying(false);
        marquee.Tick(0.1f);
        Assert.AreEqual(0f, marquee.CurrentOffset);
        Assert.AreEqual(TextOverflowModes.Ellipsis, text.overflowMode);
        Assert.AreEqual(Cell, text.rectTransform.rect.width, 0.01f);
    }

    /// <summary>결과 연출처럼 멈춰 두면(… 없음) 처음 위치에 그대로 있다가, 켜면 처음 멈춤부터</summary>
    [Test]
    public void StoppedWithoutEllipsis_HoldsAtStart()
    {
        Set("playOnStart", false);
        marquee.SetText(LongTitle);
        marquee.Tick(5f);
        Assert.AreEqual(0f, marquee.CurrentOffset);
        Assert.IsTrue(marquee.GetComponent<RectMask2D>().enabled, "넘친 부분은 칸에서 잘림");
        Assert.AreEqual(TextOverflowModes.Overflow, text.overflowMode);
        marquee.SetPlaying(true);
        marquee.Tick(1.4f);
        Assert.AreEqual(0f, marquee.CurrentOffset, "처음 멈춤");
        marquee.Tick(0.6f);
        Assert.AreEqual(30f, marquee.CurrentOffset, 0.1f);
    }

    /// <summary>흐르는 중 · 되돌리는 중 · 멈춘 동안 모두 GC 할당 0</summary>
    [Test]
    public void Tick_AllocatesNothing()
    {
        marquee.SetText(LongTitle);
        for (int i = 0; i < 400; i++) marquee.Tick(0.05f); // 한 번씩 지나가 준비
        marquee.Restart();
        Assert.That(() =>
        {
            for (int i = 0; i < 400; i++) marquee.Tick(0.05f);
        }, Is.Not.AllocatingGCMemory());
        Assert.IsTrue(marquee.IsScrolling);
    }
}
