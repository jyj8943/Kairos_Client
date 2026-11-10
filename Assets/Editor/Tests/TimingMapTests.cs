using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// TimingMap(박 ↔ 시간 변환) EditMode 테스트.
/// </summary>
public class TimingMapTests
{
    /// <summary>시간 비교 허용 오차 (초)</summary>
    private const double SecondsTolerance = 1e-6;

    /// <summary>박 비교 허용 오차 (박)</summary>
    private const double BeatTolerance = 1e-6;

    private static TimingPointData Point(float beat, float bpm)
    {
        return new TimingPointData { beat = beat, bpm = bpm };
    }

    /// <summary>offset 120ms, [0박 150 BPM, 16박 200 BPM] (클래스 주석의 예시)</summary>
    private static TimingMap TwoSegments()
    {
        return new TimingMap(new List<TimingPointData> { Point(0f, 150f), Point(16f, 200f) }, 120);
    }

    /// <summary>구간 1개: 기존 공식(beat × 60 ÷ bpm + offsetMs ÷ 1000)과 같다 (Critical Velocity·D Minor Descent 값)</summary>
    [TestCase(150.61f, 18)]
    [TestCase(216f, 120)]
    public void SingleSegment_MatchesOldFormula(float bpm, int offsetMs)
    {
        TimingMap timing = TimingMap.Single(bpm, offsetMs);
        Assert.AreEqual(1, timing.PointCount);
        Assert.AreEqual(offsetMs, timing.OffsetMs);

        double[] beats = { 0, 0.25, 1.0 / 3.0, 17.75, 28, 100 + 15.0 / 16.0, 350, 511.5 };
        foreach (double beat in beats)
        {
            double expected = beat * 60.0 / bpm + offsetMs / 1000.0;
            Assert.AreEqual(expected, timing.BeatToSeconds(beat), SecondsTolerance, $"beat {beat}");
            Assert.AreEqual(beat, timing.SecondsToBeat(expected), BeatTolerance, $"seconds {expected}");
            Assert.AreEqual(bpm, timing.BpmAt(beat));
        }
    }

    /// <summary>구간 2개: 16박 = 0.12 + 16 × 60 ÷ 150 = 6.52초, 20박 = 6.52 + 4 × 60 ÷ 200 = 7.72초</summary>
    [Test]
    public void TwoSegments_KnownValues()
    {
        TimingMap timing = TwoSegments();
        Assert.AreEqual(2, timing.PointCount);
        Assert.AreEqual(0.12, timing.BeatToSeconds(0), SecondsTolerance);
        Assert.AreEqual(6.52, timing.BeatToSeconds(16), SecondsTolerance);
        Assert.AreEqual(7.72, timing.BeatToSeconds(20), SecondsTolerance);
        Assert.AreEqual(16.0, timing.SecondsToBeat(6.52), BeatTolerance);
        Assert.AreEqual(20.0, timing.SecondsToBeat(7.72), BeatTolerance);

        Assert.AreEqual(150f, timing.BpmAt(15.999));
        Assert.AreEqual(200f, timing.BpmAt(16));
        Assert.AreEqual(200f, timing.MaxBpm);
    }

    /// <summary>왕복: SecondsToBeat(BeatToSeconds(b)) ≈ b (구간 경계 앞뒤 포함)</summary>
    [Test]
    public void RoundTrip_AroundBoundary()
    {
        TimingMap timing = TwoSegments();
        double[] beats = { -4, -0.5, 0, 0.001, 8, 15.999, 15.999999, 16, 16.000001, 16.001, 20, 100.25 };
        foreach (double beat in beats)
        {
            Assert.AreEqual(beat, timing.SecondsToBeat(timing.BeatToSeconds(beat)), BeatTolerance, $"beat {beat}");
        }

        double[] times = { -1, 0, 0.12, 6.519, 6.52, 6.521, 7.72, 60 };
        foreach (double seconds in times)
        {
            Assert.AreEqual(seconds, timing.BeatToSeconds(timing.SecondsToBeat(seconds)), SecondsTolerance, $"seconds {seconds}");
        }
    }

    /// <summary>음수 beat는 첫 구간 BPM으로 외삽한다 (−4박 = 0.12 − 4 × 60 ÷ 150 = −1.48초)</summary>
    [Test]
    public void NegativeBeat_ExtrapolatesFirstBpm()
    {
        TimingMap timing = TwoSegments();
        Assert.AreEqual(-1.48, timing.BeatToSeconds(-4), SecondsTolerance);
        Assert.AreEqual(-4.0, timing.SecondsToBeat(-1.48), BeatTolerance);
        Assert.AreEqual(150f, timing.BpmAt(-4));

        // offset 이전 시간(0초)은 음수 박
        Assert.AreEqual(-0.12 * 150.0 / 60.0, timing.SecondsToBeat(0), BeatTolerance);
    }

    /// <summary>첫 구간의 beat가 0이 아니면 첫 BPM이 0박부터 적용된 것으로 본다</summary>
    [Test]
    public void FirstPointNotAtZero_AppliesFromZero()
    {
        TimingMap timing = new TimingMap(new List<TimingPointData> { Point(4f, 150f), Point(16f, 200f) }, 120);
        Assert.AreEqual(6.52, timing.BeatToSeconds(16), SecondsTolerance);
        Assert.AreEqual(7.72, timing.BeatToSeconds(20), SecondsTolerance);
        Assert.AreEqual(150f, timing.BpmAt(1));
    }

    /// <summary>정렬되지 않은 입력도 beat 순으로 정렬해 쓰고, 원래 목록은 바꾸지 않는다</summary>
    [Test]
    public void UnsortedInput_IsSorted()
    {
        List<TimingPointData> points = new List<TimingPointData> { Point(16f, 200f), Point(0f, 150f) };
        TimingMap timing = new TimingMap(points, 120);

        Assert.AreEqual(6.52, timing.BeatToSeconds(16), SecondsTolerance);
        Assert.AreEqual(7.72, timing.BeatToSeconds(20), SecondsTolerance);
        Assert.AreEqual(16f, points[0].beat, "원래 목록 순서는 그대로");

        // 만든 뒤 원래 목록을 바꿔도 영향이 없다 (복사해 둔다)
        points[0].bpm = 50f;
        Assert.AreEqual(7.72, timing.BeatToSeconds(20), SecondsTolerance);
    }

    /// <summary>빈 목록·null·BPM 0 이하는 ArgumentException</summary>
    [Test]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TimingMap(new List<TimingPointData>(), 0));
        Assert.Throws<ArgumentException>(() => new TimingMap(null, 0));
        Assert.Throws<ArgumentException>(() => new TimingMap(new List<TimingPointData> { Point(0f, 0f) }, 0));
        Assert.Throws<ArgumentException>(() => new TimingMap(new List<TimingPointData> { Point(0f, 150f), Point(16f, -1f) }, 0));
        Assert.Throws<ArgumentException>(() => new TimingMap(new List<TimingPointData> { Point(0f, 150f), null }, 0));
        Assert.Throws<ArgumentException>(() => TimingMap.Single(0f, 0));
        Assert.Throws<ArgumentException>(() => TimingMap.FromChart(null, 0));
        Assert.Throws<ArgumentException>(() => TimingMap.FromChart(new ChartData { timingPoints = new List<TimingPointData>() }, 0));
    }

    /// <summary>FromChart는 채보의 timingPoints와 곡 offset을 쓴다</summary>
    [Test]
    public void FromChart_UsesTimingPoints()
    {
        ChartData chart = new ChartData { timingPoints = new List<TimingPointData> { Point(0f, 150f), Point(16f, 200f) } };
        TimingMap timing = TimingMap.FromChart(chart, 120);
        Assert.AreEqual(2, timing.PointCount);
        Assert.AreEqual(120, timing.OffsetMs);
        Assert.AreEqual(7.72, timing.BeatToSeconds(20), SecondsTolerance);
    }
}
