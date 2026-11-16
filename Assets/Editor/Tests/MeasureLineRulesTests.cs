using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 인게임 마디선 계산(MeasureLineRules) EditMode 테스트 (기획서 3-4 마디선).
/// </summary>
public class MeasureLineRulesTests
{
    private const double SecondsTolerance = 1e-9;

    private static NoteData Short(float beat) => new NoteData { beat = beat, lane = 0, type = NoteData.TypeShort };
    private static NoteData Long(float beat, float endBeat) => new NoteData { beat = beat, endBeat = endBeat, lane = 0, type = NoteData.TypeLong };

    /// <summary>1: BPM 하나(150.61)면 간격 = 4 × 60 ÷ 150.61초, 첫 선 = 0박 시각(offset 반영)</summary>
    [Test]
    public void SingleBpm_IntervalAndFirstLine()
    {
        TimingMap timing = TimingMap.Single(150.61f, 18);
        List<double> times = MeasureLineRules.GetMeasureTimes(timing, 30f, 0f);

        Assert.AreEqual(9, times.Count, "0·4·…·32박 (30박이 든 마디 28~32의 끝까지)");
        Assert.AreEqual(0.018, times[0], SecondsTolerance, "첫 선 = 0박 = offset");
        double interval = 4.0 * 60.0 / 150.61f;
        for (int i = 1; i < times.Count; i++)
            Assert.AreEqual(interval, times[i] - times[i - 1], 1e-9, $"{i}번 간격");
    }

    /// <summary>2: 8박에서 BPM이 바뀌어도 선은 4박마다이고, 시각은 TimingMap.BeatToSeconds와 같다</summary>
    [Test]
    public void BpmChange_EveryFourBeats_MatchesTimingMap()
    {
        TimingMap timing = new TimingMap(new List<TimingPointData>
        {
            new TimingPointData { beat = 0f, bpm = 150f },
            new TimingPointData { beat = 8f, bpm = 200f },
        }, 120);

        List<int> beats = MeasureLineRules.GetMeasureBeats(19f, 0f);
        List<double> times = MeasureLineRules.GetMeasureTimes(timing, 19f, 0f);

        CollectionAssert.AreEqual(new[] { 0, 4, 8, 12, 16, 20 }, beats);
        for (int i = 0; i < beats.Count; i++)
            Assert.AreEqual(timing.BeatToSeconds(beats[i]), times[i], SecondsTolerance, $"{beats[i]}박");

        // 간격은 BPM 구간마다 다르다: 150 BPM 1.6초, 200 BPM 1.2초
        Assert.AreEqual(1.6, times[1] - times[0], 1e-9);
        Assert.AreEqual(1.2, times[3] - times[2], 1e-9);
    }

    /// <summary>3-1: 테스트 플레이 시작 박 앞의 선은 빠지고, 시작 박과 같은 선은 남는다</summary>
    [TestCase(10f, new[] { 12, 16, 20 })]
    [TestCase(12f, new[] { 12, 16, 20 })]
    [TestCase(0f, new[] { 0, 4, 8, 12, 16, 20 })]
    public void TestPlayStart_SkipsLinesBefore(float startBeat, int[] expected)
    {
        CollectionAssert.AreEqual(expected, MeasureLineRules.GetMeasureBeats(17f, startBeat));
    }

    /// <summary>3-2: 마지막 노트(롱노트면 끝)가 든 마디의 끝까지. 마디 경계의 노트는 그 경계에서 시작하는 마디에 들어 있다</summary>
    [Test]
    public void LastBeat_IncludesMeasureOfLastNoteOrLongEnd()
    {
        Assert.AreEqual(21f, MeasureLineRules.GetLastBeat(new List<NoteData> { Short(2f), Long(6f, 21f), Short(10f) }), "롱노트 끝 박");
        Assert.AreEqual(10f, MeasureLineRules.GetLastBeat(new List<NoteData> { Short(10f), Short(3f) }));
        Assert.AreEqual(-1f, MeasureLineRules.GetLastBeat(new List<NoteData>()), "노트 없음");

        Assert.AreEqual(24, Last(MeasureLineRules.GetMeasureBeats(21f, 0f)), "21박 → 20~24 마디의 끝 24");
        Assert.AreEqual(20, Last(MeasureLineRules.GetMeasureBeats(16f, 0f)), "16박(경계) → 16~20 마디의 끝 20");
        Assert.AreEqual(16, Last(MeasureLineRules.GetMeasureBeats(15.75f, 0f)), "15.75박 → 12~16 마디의 끝 16");
        Assert.AreEqual(0, MeasureLineRules.GetMeasureBeats(-1f, 0f).Count, "노트 없으면 선 없음");
    }

    private static int Last(List<int> list) => list[list.Count - 1];
}
