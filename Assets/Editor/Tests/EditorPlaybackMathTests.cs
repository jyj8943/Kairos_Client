using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 에디터 재생 계산(EditorPlaybackMath)·스냅 맞추기·세션 박자 변경 EditMode 테스트.
/// </summary>
public class EditorPlaybackMathTests
{
    private const double Tolerance = 1e-6;
    private static double Delay => GameConstants.Editor.PlaybackStartDelaySeconds;

    /// <summary>1-1. 박 0에서 시작: offset 18ms → 음원 0.018초부터, 최소 지연만</summary>
    [Test]
    public void PlayStart_AtBeatZero()
    {
        EditorPlaybackMath.PlayStart start = EditorPlaybackMath.GetPlayStart(0f, TimingMap.Single(150.61f, 18));
        Assert.AreEqual(0.018, start.StartPositionSeconds, Tolerance);
        Assert.AreEqual(Delay, start.DelaySeconds, Tolerance);
    }

    /// <summary>1-2. offset 이전(음수 시간): 음원 0초부터, 모자란 만큼 더 기다린다 (−120ms offset이면 0박이 −0.12초)</summary>
    [Test]
    public void PlayStart_BeforeOffset()
    {
        EditorPlaybackMath.PlayStart start = EditorPlaybackMath.GetPlayStart(0f, TimingMap.Single(120f, -120));
        Assert.AreEqual(0.0, start.StartPositionSeconds, Tolerance);
        Assert.AreEqual(Delay + 0.12, start.DelaySeconds, 1e-5);
    }

    /// <summary>1-3. 곡 중간: 박 → 시간 그대로 (216 BPM, offset 120ms, 128박 = 35.68초)</summary>
    [Test]
    public void PlayStart_MiddleOfSong()
    {
        EditorPlaybackMath.PlayStart start = EditorPlaybackMath.GetPlayStart(128f, TimingMap.Single(216f, 120));
        Assert.AreEqual(128.0 * 60.0 / 216.0 + 0.12, start.StartPositionSeconds, 1e-4);
        Assert.AreEqual(Delay, start.DelaySeconds, Tolerance);
    }

    /// <summary>2-1. 박 중간에서 시작하면 첫 클릭은 다음 박, 박 위에서 시작하면 그 박</summary>
    [TestCase(0f, 0)]
    [TestCase(0.25f, 1)]
    [TestCase(7.999f, 8)]
    [TestCase(8f, 8)]
    [TestCase(10f + 1f / 3f, 11)]
    public void FirstClickBeat(float start, int expected)
    {
        Assert.AreEqual(expected, EditorPlaybackMath.GetFirstClickBeat(start));
    }

    /// <summary>2-2. 마디 첫 박 구분 (4/4: 0·4·8…박)</summary>
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(8, true)]
    [TestCase(350, false)]
    public void MeasureStart(int beat, bool expected)
    {
        Assert.AreEqual(expected, EditorPlaybackMath.IsMeasureStart(beat));
    }

    /// <summary>2-3. 클릭 예약 시각 = 박 시각: 박 → 초 → DSP → 초로 되돌려도 같다 (MusicClock 변환)</summary>
    [Test]
    public void ClickDspTime_MatchesBeatTime()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(1000.1, 30.0, 1000.0, 5.0); // 음원 30초 위치를 DSP 1000.1에 재생

        double beatTime = TimingMap.Single(150.61f, 18).BeatToSeconds(77);
        double dsp = clock.SongTimeToDsp(beatTime);
        Assert.AreEqual(1000.1 + (beatTime - 30.0), dsp, 1e-9);
    }

    /// <summary>3. 정지할 때 가장 가까운 스냅 선으로 맞춘다</summary>
    [TestCase(10.12f, 4, 10f)]
    [TestCase(10.13f, 4, 10.25f)]
    [TestCase(10.2f, 3, 1f / 3f + 10f)]
    [TestCase(10.49f, 1, 10f)]
    [TestCase(10.51f, 1, 11f)]
    [TestCase(10.04f, 16, 10.0625f)]
    public void SnapOnStop(float beat, int division, float expected)
    {
        Assert.AreEqual(expected, BeatGridMath.SnapBeat(beat, division), 1e-5);
    }

    /// <summary>현재 위치 선 밝기: 박 바로 뒤가 가장 밝고, 잠깐 뒤에는 꺼진다</summary>
    [Test]
    public void BeatFlash()
    {
        Assert.AreEqual(1f, EditorPlaybackMath.GetBeatFlash(4f), 1e-5);
        Assert.Greater(EditorPlaybackMath.GetBeatFlash(4.05f), 0f);
        Assert.AreEqual(0f, EditorPlaybackMath.GetBeatFlash(4.5f));
        Assert.AreEqual(0f, EditorPlaybackMath.GetBeatFlash(-0.5f));
    }

    /// <summary>세션 박자 변경: 노트 tick은 그대로, 곡 끝만 바뀐다</summary>
    [Test]
    public void Session_SetTiming_KeepsNotes()
    {
        GameObject owner = new GameObject("ChartEditSession_Timing");
        try
        {
            ChartEditSession session = owner.AddComponent<ChartEditSession>();
            session.Init(new EditorEntryData { songId = "__test__", chartName = "t", level = 1 }, "__test__", 120f, 0, 60f);
            Assert.AreEqual(120f, session.TotalBeats, 1e-3);

            session.AddNote(0, 48 * 10);
            session.SetTiming(240f, 500);

            Assert.AreEqual(240f, session.Bpm);
            Assert.AreEqual(500, session.OffsetMs);
            Assert.AreEqual(240f, session.Timing.BpmAt(0), "Timing도 새 BPM으로 다시 만든다");
            Assert.AreEqual(500, session.Timing.OffsetMs);
            Assert.AreEqual((60f - 0.5f) * 240f / 60f, session.TotalBeats, 1e-3);
            Assert.AreEqual(Mathf.FloorToInt(session.TotalBeats * GameConstants.Editor.TicksPerBeat), session.Data.TotalTicks);
            Assert.AreEqual(48 * 10, session.Data.Notes[0].Tick, "노트 tick은 그대로");
            Assert.IsTrue(session.IsDirty, "BPM이 바뀌면 수정 상태");
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
