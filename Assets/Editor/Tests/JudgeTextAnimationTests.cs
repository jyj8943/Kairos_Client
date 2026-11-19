using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판정 글자·FAST/SLOW 글자 애니메이션 계산(JudgeTextAnimation)과 적용(Img_Judge·Img_FastSlow, 11/19 이미지) EditMode 테스트 (DJMAX RESPECT V 기준 기본값).
/// </summary>
public class JudgeTextAnimationTests
{
    private const float Eps = 1e-4f;

    /// <summary>판정 글자 기본값: 0.7 → 1 (0.08초) · 투명도 0 → 1 (0.04초) · 0.82초 유지 · 0.13초 퇴장</summary>
    private static JudgeTextAnimation Judge() => new JudgeTextAnimation();

    /// <summary>FAST/SLOW 기본값 (Img_FastSlow와 같음): 바로 불투명 · 0.47초 유지 · 0.15초 퇴장</summary>
    private static JudgeTextAnimation FastSlow() => new JudgeTextAnimation
    {
        StartScale = 1f, ScaleInSeconds = 0f, FadeInSeconds = 0f, HoldSeconds = 0.47f, FadeOutSeconds = 0.15f
    };

    /// <summary>1. 판정 글자 t=0: 크기 0.7, 투명도 0 (보임)</summary>
    [Test]
    public void Judge_Start()
    {
        JudgeTextFrame f = Judge().Evaluate(0f);
        Assert.AreEqual(0.7f, f.Scale, Eps);
        Assert.AreEqual(0f, f.Alpha, Eps);
        Assert.IsTrue(f.Visible);
    }

    /// <summary>2. t=0.04: 투명도 1 / t=0.08: 크기 1.0. 크기는 ease-out quad (절반 시간에 75%), 투명도는 선형</summary>
    [Test]
    public void Judge_AppearTimes()
    {
        JudgeTextAnimation a = Judge();
        Assert.AreEqual(0.5f, a.Evaluate(0.02f).Alpha, Eps, "투명도 선형");
        Assert.AreEqual(1f, a.Evaluate(0.04f).Alpha, Eps);
        Assert.Less(a.Evaluate(0.04f).Scale, 1f, "0.04초에는 아직 크는 중");
        Assert.AreEqual(0.7f + 0.3f * 0.75f, a.Evaluate(0.04f).Scale, Eps, "ease-out quad: 절반 시간에 75%");
        Assert.AreEqual(1f, a.Evaluate(0.08f).Scale, Eps);
    }

    /// <summary>3. t=0.5: 크기 1, 투명도 1 (유지 구간)</summary>
    [Test]
    public void Judge_Hold()
    {
        JudgeTextFrame f = Judge().Evaluate(0.5f);
        Assert.AreEqual(1f, f.Scale, Eps);
        Assert.AreEqual(1f, f.Alpha, Eps);
        Assert.IsTrue(f.Visible);
    }

    /// <summary>4. t=0.82~0.95: 투명도 1 → 0 (크기 1 그대로), t ≥ 0.95: 숨김. Tick으로 끝까지 가면 재생이 멈춤</summary>
    [Test]
    public void Judge_FadeOutAndHide()
    {
        JudgeTextAnimation a = Judge();
        Assert.AreEqual(1f, a.Evaluate(0.82f).Alpha, Eps);
        float prev = 1f;
        for (float t = 0.84f; t < 0.945f; t += 0.02f)
        {
            JudgeTextFrame f = a.Evaluate(t);
            Assert.Less(f.Alpha, prev, $"t={t:0.00} 줄어듦");
            Assert.AreEqual(1f, f.Scale, Eps, "퇴장 중 크기 그대로");
            prev = f.Alpha;
        }
        Assert.AreEqual(0.5f, a.Evaluate(0.82f + 0.065f).Alpha, Eps, "퇴장 절반");
        Assert.IsFalse(a.Evaluate(0.95f).Visible);
        Assert.IsFalse(a.Evaluate(2f).Visible);

        a.Restart();
        for (int i = 0; i < 200; i++) a.Tick(1f / 165f);
        Assert.IsFalse(a.IsPlaying);
        Assert.IsFalse(a.Current.Visible);
    }

    /// <summary>5. 재시작: t=0.6에 새 판정 → 다시 크기 0.7, 투명도 0부터</summary>
    [Test]
    public void Judge_Restart()
    {
        JudgeTextAnimation a = Judge();
        a.Restart();
        a.Tick(0.6f);
        Assert.AreEqual(1f, a.Current.Alpha, Eps);
        a.Restart();
        Assert.AreEqual(0f, a.Elapsed, Eps);
        Assert.AreEqual(0.7f, a.Current.Scale, Eps);
        Assert.AreEqual(0f, a.Current.Alpha, Eps);
        Assert.IsTrue(a.Current.Visible);
    }

    /// <summary>6. FAST/SLOW t=0: 투명도 1 · 크기 1 / t=0.47까지 1 / t=0.62 이후 숨김</summary>
    [Test]
    public void FastSlow_Timeline()
    {
        JudgeTextAnimation a = FastSlow();
        Assert.AreEqual(1f, a.Evaluate(0f).Alpha, Eps, "바로 불투명");
        Assert.AreEqual(1f, a.Evaluate(0f).Scale, Eps, "크기 변화 없음");
        Assert.AreEqual(1f, a.Evaluate(0.47f).Alpha, Eps);
        Assert.Less(a.Evaluate(0.55f).Alpha, 1f);
        Assert.IsTrue(a.Evaluate(0.61f).Visible);
        Assert.IsFalse(a.Evaluate(0.62f).Visible);
    }

    /// <summary>9. Tick을 안 부르는 동안(일시정지·카운트다운)에는 경과 시간이 늘지 않고, 0·음수 Tick도 그대로. 풀리면 이어서</summary>
    [Test]
    public void Pause_NoTickFreezes()
    {
        JudgeTextAnimation a = Judge();
        a.Restart();
        a.Tick(0.3f);
        a.Tick(0f);
        a.Tick(-1f);
        Assert.AreEqual(0.3f, a.Elapsed, Eps);
        a.Tick(0.6f);
        Assert.AreEqual(0.9f, a.Elapsed, Eps, "이어서 진행");
        Assert.Less(a.Current.Alpha, 1f);
    }

    /// <summary>Stop: 바로 숨김 (Init·재시작)</summary>
    [Test]
    public void Stop_Hidden()
    {
        JudgeTextAnimation a = Judge();
        a.Restart();
        a.Stop();
        Assert.IsFalse(a.IsPlaying);
        Assert.IsFalse(a.Current.Visible);
    }

    // ───── 화면 적용 (Img_Judge · Img_FastSlow, 11/19 이미지) ─────

    private GameObject root;
    private Img_Judge judge;
    private Img_FastSlow fastSlow;
    private Image judgeImage;
    private Image fastSlowImage;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        var j = new GameObject("Img_Judge", typeof(RectTransform), typeof(Image));
        j.transform.SetParent(root.transform, false);
        judgeImage = j.GetComponent<Image>();
        judge = j.AddComponent<Img_Judge>();
        var f = new GameObject("Img_FastSlow", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(root.transform, false);
        fastSlowImage = f.GetComponent<Image>();
        fastSlow = f.AddComponent<Img_FastSlow>();
        judge.ResetView();
        fastSlow.ResetView();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    /// <summary>ResetView: 둘 다 숨김. Show: 이미지·크기(원본 × 0.5)를 바꾸고 크기 0.7 → 1, 알파 0 → 1, 퇴장 뒤 Image 끔</summary>
    [Test]
    public void View_ResetAndShow_ScaleAlphaSize()
    {
        Assert.IsFalse(judge.IsVisible);
        Assert.IsFalse(judgeImage.enabled);
        Assert.IsFalse(fastSlow.IsVisible);

        Sprite great = JudgeSprites.Get(JudgeType.Great);
        judge.Show(great);
        Assert.AreSame(great, judgeImage.sprite);
        Assert.AreEqual(JudgeSprites.DisplaySize(great), judgeImage.rectTransform.sizeDelta, "크기 = 원본 × 0.5");
        Assert.IsTrue(judgeImage.enabled);
        Assert.AreEqual(0.7f, judgeImage.rectTransform.localScale.x, Eps);
        Assert.AreEqual(0f, judgeImage.color.a, Eps);
        judge.Tick(0.1f);
        Assert.AreEqual(1f, judgeImage.rectTransform.localScale.x, Eps);
        Assert.AreEqual(Color.white, judgeImage.color, "색은 흰색 그대로 (색은 이미지 안)");
        judge.Tick(1f);
        Assert.IsFalse(judgeImage.enabled, "퇴장 뒤 숨김");
        Assert.AreEqual(1f, judgeImage.rectTransform.localScale.x, Eps);
    }

    /// <summary>7. FAST/SLOW 표시 중 PERFECT 판정이 나도 (판정 글자만 다시 재생) 남은 시간 그대로</summary>
    [Test]
    public void View_FastSlowSurvivesPerfect()
    {
        fastSlow.Show(JudgeSprites.Get(global::FastSlow.Fast));
        judge.Show(JudgeSprites.Get(JudgeType.Great));
        judge.Tick(0.2f);
        fastSlow.Tick(0.2f);

        judge.Show(JudgeSprites.Get(JudgeType.Perfect)); // 다음 판정 PERFECT: FAST/SLOW는 부르지 않는다
        Assert.AreEqual(0f, judge.Animation.Elapsed, Eps);
        Assert.AreEqual(0.2f, fastSlow.Animation.Elapsed, Eps, "FAST 시간 그대로");
        Assert.IsTrue(fastSlow.IsVisible);
        Assert.AreSame(JudgeSprites.Get(global::FastSlow.Fast), fastSlow.ShownSprite);

        judge.Tick(0.45f);
        fastSlow.Tick(0.45f);
        Assert.IsFalse(fastSlow.IsVisible, "0.65초 → FAST 사라짐");
        Assert.IsTrue(judge.IsVisible, "PERFECT는 0.45초째라 보임");
    }

    /// <summary>8. FAST/SLOW 표시 중 새 FAST/SLOW → 시간 재시작, 이미지 바뀜</summary>
    [Test]
    public void View_NewFastSlowRestarts()
    {
        fastSlow.Show(JudgeSprites.Get(global::FastSlow.Fast));
        fastSlow.Tick(0.4f);
        Sprite slow = JudgeSprites.Get(global::FastSlow.Slow);
        fastSlow.Show(slow);
        Assert.AreEqual(0f, fastSlow.Animation.Elapsed, Eps);
        Assert.AreSame(slow, fastSlowImage.sprite, "이미지 바뀜");
        Assert.AreEqual(JudgeSprites.DisplaySize(slow), fastSlowImage.rectTransform.sizeDelta, "SLOW 크기");
        Assert.AreEqual(1f, fastSlowImage.color.a, Eps, "바로 불투명");
        fastSlow.Tick(0.4f);
        Assert.IsTrue(fastSlow.IsVisible, "다시 센 지 0.4초 → 아직 보임");
    }
}
