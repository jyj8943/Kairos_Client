using NUnit.Framework;

/// <summary>
/// MusicClock 재생 배율 EditMode 테스트 (테스트 전용 재생 배율, TestSpeed). dspTime과 realtime을 같은 간격으로 넣어 보간 오차가 없게 한다.
/// </summary>
public class MusicClockRateTests
{
    private const double Tolerance = 1e-9;
    private const double StartDsp = 10.0;
    private const double StartRealtime = 100.0;

    /// <summary>dsp와 realtime이 함께 elapsed초 지난 시점으로 Tick한다</summary>
    private static void TickAt(MusicClock clock, double dspBase, double realtimeBase, double elapsed)
    {
        clock.Tick(dspBase + elapsed, realtimeBase + elapsed);
    }

    private static MusicClock Scheduled(double rate, double startPosition = 0)
    {
        MusicClock clock = new MusicClock();
        clock.SetRate(rate, StartDsp, StartRealtime);
        clock.Schedule(StartDsp, startPosition, StartDsp, StartRealtime);
        return clock;
    }

    /// <summary>1-1: dspTime이 1초 지나면 곡 시간 = 1초 × 배율</summary>
    [TestCase(1.0)]
    [TestCase(2.0)]
    [TestCase(3.0)]
    public void OneDspSecond_AdvancesRateSeconds(double rate)
    {
        MusicClock clock = Scheduled(rate);
        TickAt(clock, StartDsp, StartRealtime, 1.0);
        Assert.AreEqual(rate, clock.SongTime, Tolerance);
    }

    /// <summary>1-2: 일시정지 동안 멈추고, 재개 뒤 같은 배율로 이어진다</summary>
    [TestCase(1.0)]
    [TestCase(2.0)]
    public void PauseResume_ContinuesWithRate(double rate)
    {
        MusicClock clock = Scheduled(rate);
        TickAt(clock, StartDsp, StartRealtime, 1.0);
        clock.Pause(StartDsp + 1.0, StartRealtime + 1.0);

        // 5초 쉬어도 곡 시간은 그대로
        TickAt(clock, StartDsp, StartRealtime, 6.0);
        Assert.AreEqual(rate, clock.SongTime, Tolerance, "멈춘 동안");

        clock.Resume(StartDsp + 6.0, StartRealtime + 6.0);
        TickAt(clock, StartDsp, StartRealtime, 7.0);
        Assert.AreEqual(rate * 2.0, clock.SongTime, Tolerance, "재개 1초 뒤");
    }

    /// <summary>1-3: 재개 여유(minLead, 실제 초)는 배율과 상관없이 실제 시간으로 지킨다</summary>
    [TestCase(1.0)]
    [TestCase(2.0)]
    public void ResumeMinLead_IsRealSeconds(double rate)
    {
        MusicClock clock = Scheduled(rate);
        TickAt(clock, StartDsp, StartRealtime, 1.0);
        clock.Pause(StartDsp + 1.0, StartRealtime + 1.0);
        clock.Resume(StartDsp + 3.0, StartRealtime + 3.0, 0.1);

        Assert.IsTrue(clock.IsHolding);
        Assert.AreEqual(StartDsp + 3.1, clock.SongTimeToDsp(clock.ResumePosition), Tolerance, "멈춘 위치는 실제 0.1초 뒤에 재생");
    }

    /// <summary>1-4: Seek(다른 위치에서 다시 예약) 뒤에도 시작 위치 + 경과 × 배율</summary>
    [TestCase(1.0)]
    [TestCase(2.0)]
    public void Seek_StartsFromPositionWithRate(double rate)
    {
        MusicClock clock = Scheduled(rate);
        TickAt(clock, StartDsp, StartRealtime, 1.0);

        double seekDsp = StartDsp + 2.0;
        double seekRealtime = StartRealtime + 2.0;
        clock.Schedule(seekDsp, 30.0, seekDsp, seekRealtime);
        Assert.AreEqual(30.0, clock.SongTime, Tolerance);
        TickAt(clock, seekDsp, seekRealtime, 0.5);
        Assert.AreEqual(30.0 + 0.5 * rate, clock.SongTime, Tolerance);
    }

    /// <summary>1-5: 재생 중에 배율을 바꿔도 곡 시간이 튀지 않고, 그 뒤로 새 배율로 늘어난다</summary>
    [Test]
    public void SetRateWhileRunning_NoJump()
    {
        MusicClock clock = Scheduled(1.0);
        TickAt(clock, StartDsp, StartRealtime, 1.0);
        clock.SetRate(2.0, StartDsp + 1.0, StartRealtime + 1.0);
        Assert.AreEqual(1.0, clock.SongTime, Tolerance);

        TickAt(clock, StartDsp, StartRealtime, 2.0);
        Assert.AreEqual(3.0, clock.SongTime, Tolerance);
    }

    /// <summary>2: 곡 시간 → DSP → 곡 시간 왕복이 원래 값 (DSP 시각에 Tick하면 그 곡 시간이 나온다)</summary>
    [TestCase(1.0, 5.0)]
    [TestCase(2.0, 5.0)]
    [TestCase(2.0, 0.75)]
    public void SongTimeDsp_RoundTrip(double rate, double songTime)
    {
        MusicClock clock = Scheduled(rate, 0.5);
        double dsp = clock.SongTimeToDsp(songTime);
        Assert.AreEqual(StartDsp + (songTime - 0.5) / rate, dsp, Tolerance, "DSP 시각");

        TickAt(clock, StartDsp, StartRealtime, dsp - StartDsp);
        Assert.AreEqual(songTime, clock.SongTime, Tolerance, "그 DSP 시각의 곡 시간");
        Assert.AreEqual(dsp, clock.SongTimeToDsp(clock.SongTime), Tolerance, "다시 DSP");
    }

    /// <summary>3: 입력 시각(realtime) → 곡 시간은 마지막 Tick에서 realtime 차이 × 배율만큼 옮긴다</summary>
    [TestCase(1.0)]
    [TestCase(2.0)]
    public void InputRealtime_ToSongTime_UsesRate(double rate)
    {
        MusicClock clock = Scheduled(rate);
        TickAt(clock, StartDsp, StartRealtime, 1.0);

        Assert.AreEqual(rate + 0.25 * rate, clock.GetSongTimeAtRealtime(StartRealtime + 1.25), Tolerance, "Tick 0.25초 뒤 입력");
        Assert.AreEqual(rate - 0.1 * rate, clock.GetSongTimeAtRealtime(StartRealtime + 0.9), Tolerance, "Tick 0.1초 앞 입력");
    }

    /// <summary>배율 1.0은 배율이 없던 식(dsp − 시작 DSP + 시작 위치)과 숫자가 완전히 같다</summary>
    [Test]
    public void RateOne_ExactlyMatchesOldFormula()
    {
        MusicClock clock = new MusicClock();
        double startDsp = 1234.56789;
        double startPosition = 12.3456;
        clock.Schedule(startDsp, startPosition, startDsp - 1.0, StartRealtime);
        Assert.AreEqual(17.0 - startPosition + startDsp, clock.SongTimeToDsp(17.0), 0.0);

        // realtime 예상과 크게 달라 dspTime 값으로 바로 맞추는 경우 (보간 보정 없이 식 그대로 비교)
        clock.Tick(startDsp + 0.37, StartRealtime + 5.0);
        Assert.AreEqual(startDsp + 0.37 - startDsp + startPosition, clock.SongTime, 0.0);
    }
}
