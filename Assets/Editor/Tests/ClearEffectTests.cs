using NUnit.Framework;

/// <summary>
/// FULL COMBO / ALL PERFECT 연출 선택(ClearEffectRules)·시간 계산(ClearEffectAnimation) EditMode 테스트.
/// </summary>
public class ClearEffectTests
{
    private const float Eps = 1e-4f;

    private static ClearEffectAnimation Anim() => new ClearEffectAnimation();

    /// <summary>1. 연출 선택: AP → ALL PERFECT, FC만 → FULL COMBO, MISS 있음 → 없음, 자동 플레이 → 없음, 테스트 플레이 + AP → ALL PERFECT</summary>
    [Test]
    public void Select()
    {
        Assert.AreEqual(ClearKind.AllPerfect, ClearEffectRules.Select(false, true, true), "AP (AP면 FC이기도 하다)");
        Assert.AreEqual(ClearKind.FullCombo, ClearEffectRules.Select(false, false, true), "FC만");
        Assert.AreEqual(ClearKind.None, ClearEffectRules.Select(false, false, false), "MISS 있음");
        Assert.AreEqual(ClearKind.None, ClearEffectRules.Select(true, true, true), "자동 플레이는 AP여도 없음");
        Assert.AreEqual(ClearKind.None, ClearEffectRules.Select(true, false, true), "자동 플레이 FC");

        // 테스트 플레이(자동 아님) + AP: 실제 점수 계산으로
        ScoreCalculator score = new ScoreCalculator(3);
        for (int i = 0; i < 3; i++) score.Add(JudgeType.Perfect);
        Assert.AreEqual(ClearKind.AllPerfect, ClearEffectRules.Select(false, score.IsAllPerfect, score.IsFullCombo), "테스트 플레이 + AP");

        ScoreCalculator fc = new ScoreCalculator(3);
        fc.Add(JudgeType.Perfect); fc.Add(JudgeType.Great); fc.Add(JudgeType.Good);
        Assert.AreEqual(ClearKind.FullCombo, ClearEffectRules.Select(false, fc.IsAllPerfect, fc.IsFullCombo));

        ScoreCalculator miss = new ScoreCalculator(2);
        miss.Add(JudgeType.Perfect); miss.Add(JudgeType.Miss);
        Assert.AreEqual(ClearKind.None, ClearEffectRules.Select(false, miss.IsAllPerfect, miss.IsFullCombo));
    }

    /// <summary>2. 0.0 / 0.29: 모두 숨김 (알파 0)</summary>
    [TestCase(0f)]
    [TestCase(0.29f)]
    public void Hidden_BeforeStart(float t)
    {
        ClearEffectFrame f = Anim().Evaluate(t);
        Assert.IsFalse(f.Visible);
        Assert.AreEqual(0f, f.GroupAlpha, Eps);
        Assert.AreEqual(0f, f.BandAlpha, Eps);
        Assert.AreEqual(0f, f.TitleAlpha, Eps);
        Assert.AreEqual(0f, f.FlashAlpha, Eps);
        Assert.AreEqual(0f, f.AfterimageAlpha, Eps);
        Assert.IsFalse(f.ShineVisible);
    }

    /// <summary>3. 0.40: 글자 크기 1.4·알파 0, 화면 번쩍임 0.35 (띠는 등장 중)</summary>
    [Test]
    public void At040_TitleStartAndFlash()
    {
        ClearEffectFrame f = Anim().Evaluate(0.40f);
        Assert.IsTrue(f.Visible);
        Assert.AreEqual(1.4f, f.TitleScale, Eps);
        Assert.AreEqual(0f, f.TitleAlpha, Eps);
        Assert.AreEqual(0.35f, f.FlashAlpha, Eps);
        Assert.Greater(f.BandScaleX, 0f);
        Assert.Less(f.BandScaleX, 1f);
    }

    /// <summary>띠: 0.30에 0, 0.50에 가로 1·알파 0.55, 중간은 ease-out (절반 시간에 75%)</summary>
    [Test]
    public void Band()
    {
        ClearEffectAnimation a = Anim();
        Assert.AreEqual(0f, a.Evaluate(0.30f).BandScaleX, Eps);
        Assert.AreEqual(0.75f, a.Evaluate(0.40f).BandScaleX, Eps);
        Assert.AreEqual(1f, a.Evaluate(0.50f).BandScaleX, Eps);
        Assert.AreEqual(0.55f, a.Evaluate(0.50f).BandAlpha, Eps);
    }

    /// <summary>글자 등장은 ease-out back: 끝나기 전에 1.0 아래로 살짝 내려갔다 1.0으로 돌아온다 (튕김)</summary>
    [Test]
    public void Title_Overshoots()
    {
        ClearEffectAnimation a = Anim();
        a.pulseEnabled = false;
        float min = float.MaxValue;
        for (float t = 0.40f; t <= 0.65f; t += 0.005f) min = System.Math.Min(min, a.Evaluate(t).TitleScale);
        Assert.Less(min, 1f, "1.0 아래로 튕김");
        Assert.Greater(min, 0.9f, "너무 크게 튕기지 않음");
        Assert.AreEqual(1f, a.Evaluate(0.65f).TitleScale, Eps);
    }

    /// <summary>4. 0.65: 글자 크기 1.0·알파 1, 잔상 시작 (크기 1, 알파 0.8)</summary>
    [Test]
    public void At065_TitleSettledAfterimageStarts()
    {
        ClearEffectFrame f = Anim().Evaluate(0.65f);
        Assert.AreEqual(1f, f.TitleScale, 1e-3f);
        Assert.AreEqual(1f, f.TitleAlpha, Eps);
        Assert.AreEqual(1f, f.AfterimageScale, Eps);
        Assert.AreEqual(0.8f, f.AfterimageAlpha, Eps);

        ClearEffectFrame mid = Anim().Evaluate(0.80f);
        Assert.Greater(mid.AfterimageScale, 1f);
        Assert.Less(mid.AfterimageAlpha, 0.8f);
        Assert.AreEqual(0f, Anim().Evaluate(0.95f).AfterimageAlpha, Eps, "0.95에 잔상 끝");
    }

    /// <summary>5. 0.52: 화면 번쩍임 0 (0.40부터 0.12 동안 선형으로 줄어듦)</summary>
    [Test]
    public void At052_FlashGone()
    {
        Assert.AreEqual(0.35f * 0.5f, Anim().Evaluate(0.46f).FlashAlpha, Eps, "선형 절반");
        Assert.AreEqual(0f, Anim().Evaluate(0.52f).FlashAlpha, Eps);
        Assert.AreEqual(0f, Anim().Evaluate(1.0f).FlashAlpha, Eps);
    }

    /// <summary>빛 훑기: 0.90 ~ 1.25만 보이고 왼쪽 밖(0) → 오른쪽 밖(1), ease-in-out (절반 시간에 절반)</summary>
    [Test]
    public void Shine()
    {
        ClearEffectAnimation a = Anim();
        Assert.IsFalse(a.Evaluate(0.89f).ShineVisible);
        Assert.IsTrue(a.Evaluate(0.90f).ShineVisible);
        Assert.AreEqual(0f, a.Evaluate(0.90f).ShineProgress, Eps);
        Assert.AreEqual(0.5f, a.Evaluate(1.075f).ShineProgress, Eps);
        Assert.Less(a.Evaluate(0.95f).ShineProgress, (0.95f - 0.90f) / 0.35f, "처음은 가속 (선형보다 느림)");
        Assert.IsFalse(a.Evaluate(1.25f).ShineVisible);
    }

    /// <summary>6. 1.5: 띠 알파 0.55, 글자 알파 1, 크기 1.0~1.02 (맥동). 맥동을 끄면 1.0</summary>
    [Test]
    public void At150_HoldAndPulse()
    {
        ClearEffectAnimation a = Anim();
        ClearEffectFrame f = a.Evaluate(1.5f);
        Assert.AreEqual(0.55f, f.BandAlpha, Eps);
        Assert.AreEqual(1f, f.TitleAlpha, Eps);
        Assert.GreaterOrEqual(f.TitleScale, 1f - Eps);
        Assert.LessOrEqual(f.TitleScale, 1.02f + Eps);
        Assert.AreEqual(1f, f.GroupAlpha, Eps);

        float max = 0f;
        for (float t = 0.65f; t <= 2.4f; t += 0.01f) max = System.Math.Max(max, a.Evaluate(t).TitleScale);
        Assert.AreEqual(1.02f, max, 1e-3f, "맥동 최대 1.02");
        Assert.AreEqual(a.Evaluate(2.4f).TitleScale, a.Evaluate(2.6f).TitleScale, Eps, "2.40 뒤 맥동 멈춤 (값 유지, 튀지 않음)");

        a.pulseEnabled = false;
        Assert.AreEqual(1f, a.Evaluate(1.5f).TitleScale, Eps);
    }

    /// <summary>7. 2.40부터 0.40 동안 전체 알파 1 → 0, 2.80 이후 숨김</summary>
    [Test]
    public void FadeOutAndHidden()
    {
        ClearEffectAnimation a = Anim();
        Assert.AreEqual(1f, a.Evaluate(2.40f).GroupAlpha, Eps);
        Assert.AreEqual(0.5f, a.Evaluate(2.60f).GroupAlpha, Eps);
        Assert.IsTrue(a.Evaluate(2.79f).Visible);
        Assert.IsFalse(a.Evaluate(2.80f).Visible);
        Assert.IsFalse(a.Evaluate(5f).Visible);
        Assert.AreEqual(0f, a.Evaluate(2.80f).GroupAlpha, Eps);
    }

    /// <summary>8. 연출 총 길이 ≤ 결과로 가는 시간 (ResultDelaySeconds)</summary>
    [Test]
    public void TotalWithinResultDelay()
    {
        ClearEffectAnimation a = Anim();
        Assert.AreEqual(2.8f, a.TotalSeconds, Eps);
        Assert.LessOrEqual(a.TotalSeconds, (float)GameConstants.Play.ResultDelaySeconds);
    }
}
