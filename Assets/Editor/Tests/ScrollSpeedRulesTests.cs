using NUnit.Framework;

/// <summary>
/// 배속 계산(ScrollSpeedRules)·키 반복(KeyRepeat) EditMode 테스트 (기획서 2-1 6, 1.0배 보이는 시간 2.69초).
/// </summary>
public class ScrollSpeedRulesTests
{
    /// <summary>1.0에서 내리면 1.0, 8.0에서 올리면 8.0 (범위 끝에서 멈춤)</summary>
    [Test]
    public void Step_ClampsAtEnds()
    {
        Assert.AreEqual(10, ScrollSpeedRules.MinSteps);
        Assert.AreEqual(80, ScrollSpeedRules.MaxSteps);
        Assert.AreEqual(10, ScrollSpeedRules.Step(10, -1));
        Assert.AreEqual(80, ScrollSpeedRules.Step(80, 1));
        Assert.AreEqual(1f, ScrollSpeedRules.StepMultiplier(1f, -1));
        Assert.AreEqual(8f, ScrollSpeedRules.StepMultiplier(8f, 1));
        Assert.AreEqual(8f, ScrollSpeedRules.StepMultiplier(7.95f, 5), "여러 단계도 범위 끝에서 멈춤");
    }

    /// <summary>0.1씩 70번 올려도 오차 없이 정확히 8.0, 중간값도 0.1 단위 그대로</summary>
    [Test]
    public void Step_SeventyTimesNoDrift()
    {
        float multiplier = 1f;
        float floatSum = 1f;
        for (int i = 1; i <= 70; i++)
        {
            multiplier = ScrollSpeedRules.StepMultiplier(multiplier, 1);
            floatSum += 0.1f;
            Assert.AreEqual(10 + i, ScrollSpeedRules.ToSteps(multiplier), $"{i}번째");
            Assert.AreEqual((10 + i) / 10f, multiplier, $"{i}번째 배속");
        }
        Assert.AreEqual(8f, multiplier, "정확히 8.0");
        Assert.AreNotEqual(8f, floatSum, "참고: float로 0.1씩 더하면 8.0이 정확히 나오지 않는다");

        for (int i = 0; i < 70; i++) multiplier = ScrollSpeedRules.StepMultiplier(multiplier, -1);
        Assert.AreEqual(1f, multiplier, "다시 70번 내리면 정확히 1.0");
    }

    /// <summary>범위·단위 밖 값은 가장 가까운 단계로 맞춘다</summary>
    [TestCase(0.5f, 10)]
    [TestCase(1.04f, 10)]
    [TestCase(1.06f, 11)]
    [TestCase(5.55f, 56)]
    [TestCase(9f, 80)]
    public void ToSteps_RoundsAndClamps(float multiplier, int expected)
    {
        Assert.AreEqual(expected, ScrollSpeedRules.ToSteps(multiplier));
    }

    /// <summary>보이는 시간 = 2.69 ÷ 배속, 낙하 속도 = 거리 ÷ 2.69 × 배속 (1.0·4.0·5.5·8.0)</summary>
    [TestCase(1f)]
    [TestCase(4f)]
    [TestCase(5.5f)]
    [TestCase(8f)]
    public void VisibleSecondsAndSpeed(float multiplier)
    {
        const float distance = 8.4f;
        float expectedSeconds = 2.69f / multiplier;
        Assert.AreEqual(2.69f, GameConstants.Play.BaseVisibleSeconds);
        Assert.AreEqual(expectedSeconds, ScrollSpeedRules.VisibleSeconds(multiplier), 1e-6);

        float speed = ScrollSpeedRules.ScrollSpeed(distance, multiplier);
        Assert.AreEqual(distance / 2.69f * multiplier, speed, 1e-5);
        Assert.AreEqual(expectedSeconds, distance / speed, 1e-5, "화면 위 끝 → 판정선 걸리는 시간");
    }

    /// <summary>키 반복: 누른 순간 1번, 0.4초 뒤부터 0.08초마다, 뗀 뒤에는 없음, 프레임이 늦으면 한꺼번에</summary>
    [Test]
    public void KeyRepeat_DelayAndInterval()
    {
        KeyRepeat repeat = new KeyRepeat(0.4, 0.08);
        Assert.AreEqual(1, repeat.Press(10.0));
        Assert.AreEqual(0, repeat.Tick(10.39));
        Assert.AreEqual(1, repeat.Tick(10.4001), "0.4초 뒤 첫 반복");
        Assert.AreEqual(0, repeat.Tick(10.47));
        Assert.AreEqual(1, repeat.Tick(10.4801), "0.08초 간격");
        Assert.AreEqual(3, repeat.Tick(10.7201), "늦은 프레임이면 지난 만큼 (10.56, 10.64, 10.72)");
        repeat.Release();
        Assert.AreEqual(0, repeat.Tick(20.0), "뗀 뒤에는 반복 없음");
        Assert.IsFalse(repeat.IsHeld);
    }
}
