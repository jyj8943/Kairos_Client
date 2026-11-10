using NUnit.Framework;

/// <summary>
/// BeatGridMath 그리드 계산과 BPM 하나일 때의 박 ↔ 시간 변환(TimingMap.Single) EditMode 테스트 (0박 = 음원 시작 + offsetMs).
/// </summary>
public class BeatGridMathTests
{
    /// <summary>왕복 비교 허용 오차 (초 / 박)</summary>
    private const float Tolerance = 0.0005f;

    /// <summary>offset 0·18·120ms에서 박 → 초 → 박, 초 → 박 → 초 왕복 값이 같다 (BPM 구간 1개의 TimingMap)</summary>
    [TestCase(150.61f, 0)]
    [TestCase(150.61f, 18)]
    [TestCase(216f, 120)]
    [TestCase(216f, 18)]
    public void BeatAndSeconds_RoundTrip(float bpm, int offsetMs)
    {
        TimingMap timing = TimingMap.Single(bpm, offsetMs);
        float[] beats = { 0f, 0.25f, 1f / 3f, 17.75f, 100f + 15f / 16f, 350f };
        foreach (float beat in beats)
        {
            double seconds = timing.BeatToSeconds(beat);
            Assert.AreEqual(beat, timing.SecondsToBeat(seconds), Tolerance, $"beat {beat}");
        }

        float[] times = { 0f, offsetMs / 1000f, 1.5f, 60f, 139.56f };
        foreach (float seconds in times)
        {
            double beat = timing.SecondsToBeat(seconds);
            Assert.AreEqual(seconds, timing.BeatToSeconds(beat), Tolerance, $"seconds {seconds}");
        }
    }

    /// <summary>0박은 offset 시간이고, offset 이전 시간은 음수 박이다</summary>
    [Test]
    public void ZeroBeat_IsOffset()
    {
        Assert.AreEqual(0.018f, TimingMap.Single(150.61f, 18).BeatToSeconds(0), Tolerance);
        TimingMap dMinor = TimingMap.Single(216f, 120);
        Assert.AreEqual(0.12f, dMinor.BeatToSeconds(0), Tolerance);
        Assert.AreEqual(0f, dMinor.SecondsToBeat(0.12), Tolerance);
        Assert.Less(dMinor.SecondsToBeat(0), 0.0);
    }

    /// <summary>곡 끝 박: (139.56 − 0.018) × 150.61 ÷ 60 ≈ 350.3, (142.20 − 0.12) × 216 ÷ 60 ≈ 511.5</summary>
    [Test]
    public void SongEndBeat_MatchesSongs()
    {
        float criticalVelocity = (float)TimingMap.Single(150.61f, 18).SecondsToBeat(139.56f);
        float dMinorDescent = (float)TimingMap.Single(216f, 120).SecondsToBeat(142.2f);

        Assert.AreEqual(350.27f, criticalVelocity, 0.05f);
        Assert.AreEqual(511.49f, dMinorDescent, 0.05f);
        Assert.AreEqual(88, BeatGridMath.BeatToMeasure(criticalVelocity));
        Assert.AreEqual(128, BeatGridMath.BeatToMeasure(dMinorDescent));
    }

    /// <summary>1박 높이 = 250px × 60 ÷ BPM × 배율: 150.61 BPM 약 99.6px, 216 BPM 약 69.4px, x2.0이면 두 배</summary>
    [Test]
    public void BeatHeight_DependsOnBpm()
    {
        Assert.AreEqual(GameConstants.Editor.PixelsPerSecond * 60f / 150.61f, BeatGridMath.GetBeatHeight(150.61f, 1f), 1e-3);
        Assert.AreEqual(99.6f, BeatGridMath.GetBeatHeight(150.61f, 1f), 0.05f);
        Assert.AreEqual(69.4f, BeatGridMath.GetBeatHeight(216f, 1f), 0.05f);
        Assert.AreEqual(BeatGridMath.GetBeatHeight(216f, 1f) * 2f, BeatGridMath.GetBeatHeight(216f, 2f), 1e-3);

        // 216 BPM의 1박은 150.61 BPM의 약 70% 높이 (같은 1초 = 같은 높이)
        Assert.AreEqual(150.61f / 216f, BeatGridMath.GetBeatHeight(216f, 1f) / BeatGridMath.GetBeatHeight(150.61f, 1f), 1e-4);
    }

    /// <summary>BeatToY → YToBeat 왕복이 같은 박을 돌려준다 (두 BPM, 여러 배율)</summary>
    [TestCase(150.61f, 0.5f)]
    [TestCase(150.61f, 1f)]
    [TestCase(216f, 1.75f)]
    [TestCase(216f, 4f)]
    public void BeatToY_RoundTrip(float bpm, float scale)
    {
        TimingMap timing = TimingMap.Single(bpm, 120);
        float[] beats = { 0f, 3.25f, 10f + 1f / 3f, 87.9375f };
        foreach (float current in new[] { 0f, 12.5f })
        {
            foreach (float beat in beats)
            {
                float y = BeatGridMath.BeatToY(timing, beat, current, scale);
                Assert.AreEqual(beat, BeatGridMath.YToBeat(timing, y, current, scale), 1e-4, $"beat {beat}, current {current}");
            }
        }

        // 1박 위의 y 차이는 1박 높이
        Assert.AreEqual(BeatGridMath.GetBeatHeight(bpm, scale), BeatGridMath.BeatToY(timing, 13.5f, 12.5f, scale), 1e-3);
    }

    /// <summary>
    /// 그리드 동치: BPM 구간이 하나면 시간 기준 BeatToY·YToBeat가 예전 박 기준 계산
    /// (y = GetBeatHeight × (beat − currentBeat), beat = currentBeat + y ÷ GetBeatHeight)과 같다. offset에도 영향받지 않는다.
    /// </summary>
    [TestCase(150.61f, 18, 0.5f)]
    [TestCase(150.61f, 18, 1f)]
    [TestCase(216f, 120, 1f)]
    [TestCase(216f, 120, 2.5f)]
    [TestCase(120f, -120, 4f)]
    [TestCase(300f, 0, 0.25f)]
    public void BeatToY_MatchesBeatHeightFormula(float bpm, int offsetMs, float scale)
    {
        TimingMap timing = TimingMap.Single(bpm, offsetMs);
        float beatHeight = BeatGridMath.GetBeatHeight(bpm, scale);
        float[] beats = { 0f, 0.25f, 3.25f, 10f + 1f / 3f, 28f, 87.9375f, 511.5f };
        foreach (float current in new[] { 0f, 12.5f, 28f, 300.125f })
        {
            foreach (float beat in beats)
            {
                float expectedY = (beat - current) * beatHeight;
                float y = BeatGridMath.BeatToY(timing, beat, current, scale);
                // y는 수천 px까지 커지므로 상대 오차로 본다 (float 반올림 차이만 허용)
                Assert.AreEqual(expectedY, y, 1e-3 + System.Math.Abs(expectedY) * 1e-5, $"BeatToY beat {beat}, current {current}");

                float expectedBeat = current + expectedY / beatHeight;
                Assert.AreEqual(expectedBeat, BeatGridMath.YToBeat(timing, expectedY, current, scale), 1e-4, $"YToBeat beat {beat}, current {current}");
            }
        }
    }

    /// <summary>선 간격 안내: 216 BPM·1/16·x1.0은 약 4.3px라 안내, x2.0은 약 8.7px라 숨김</summary>
    [Test]
    public void DenseGridHint_Threshold()
    {
        Assert.AreEqual(69.444f / 16f, BeatGridMath.GetSnapLineSpacing(216f, 1f, 16), 0.01f);
        Assert.IsTrue(BeatGridMath.IsSnapLineSpacingTooSmall(216f, 1f, 16));
        Assert.IsFalse(BeatGridMath.IsSnapLineSpacingTooSmall(216f, 2f, 16));

        // 150.61 BPM·1/16·x1.0은 약 6.2px로 기준(6px) 이상이라 숨김
        Assert.IsFalse(BeatGridMath.IsSnapLineSpacingTooSmall(150.61f, 1f, 16));
        Assert.IsFalse(BeatGridMath.IsSnapLineSpacingTooSmall(216f, 1f, 4));
    }
}
