using NUnit.Framework;

/// <summary>
/// BeatGridMath 박 ↔ 시간 변환 EditMode 테스트 (0박 = 음원 시작 + offsetMs).
/// </summary>
public class BeatGridMathTests
{
    /// <summary>왕복 비교 허용 오차 (초 / 박)</summary>
    private const float Tolerance = 0.0005f;

    /// <summary>offset 0·18·120ms에서 박 → 초 → 박, 초 → 박 → 초 왕복 값이 같다</summary>
    [TestCase(150.61f, 0)]
    [TestCase(150.61f, 18)]
    [TestCase(216f, 120)]
    [TestCase(216f, 18)]
    public void BeatAndSeconds_RoundTrip(float bpm, int offsetMs)
    {
        float[] beats = { 0f, 0.25f, 1f / 3f, 17.75f, 100f + 15f / 16f, 350f };
        foreach (float beat in beats)
        {
            float seconds = BeatGridMath.BeatToSeconds(beat, bpm, offsetMs);
            Assert.AreEqual(beat, BeatGridMath.SecondsToBeat(seconds, bpm, offsetMs), Tolerance, $"beat {beat}");
        }

        float[] times = { 0f, offsetMs / 1000f, 1.5f, 60f, 139.56f };
        foreach (float seconds in times)
        {
            float beat = BeatGridMath.SecondsToBeat(seconds, bpm, offsetMs);
            Assert.AreEqual(seconds, BeatGridMath.BeatToSeconds(beat, bpm, offsetMs), Tolerance, $"seconds {seconds}");
        }
    }

    /// <summary>0박은 offset 시간이고, offset 이전 시간은 음수 박이다</summary>
    [Test]
    public void ZeroBeat_IsOffset()
    {
        Assert.AreEqual(0.018f, BeatGridMath.BeatToSeconds(0f, 150.61f, 18), Tolerance);
        Assert.AreEqual(0.12f, BeatGridMath.BeatToSeconds(0f, 216f, 120), Tolerance);
        Assert.AreEqual(0f, BeatGridMath.SecondsToBeat(0.12f, 216f, 120), Tolerance);
        Assert.Less(BeatGridMath.SecondsToBeat(0f, 216f, 120), 0f);
    }

    /// <summary>곡 끝 박: (139.56 − 0.018) × 150.61 ÷ 60 ≈ 350.3, (142.20 − 0.12) × 216 ÷ 60 ≈ 511.5</summary>
    [Test]
    public void SongEndBeat_MatchesSongs()
    {
        float criticalVelocity = BeatGridMath.SecondsToBeat(139.56f, 150.61f, 18);
        float dMinorDescent = BeatGridMath.SecondsToBeat(142.2f, 216f, 120);

        Assert.AreEqual(350.27f, criticalVelocity, 0.05f);
        Assert.AreEqual(511.49f, dMinorDescent, 0.05f);
        Assert.AreEqual(88, BeatGridMath.BeatToMeasure(criticalVelocity));
        Assert.AreEqual(128, BeatGridMath.BeatToMeasure(dMinorDescent));
    }
}
