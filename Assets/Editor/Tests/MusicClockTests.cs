using NUnit.Framework;

/// <summary>
/// MusicClock EditMode 테스트. dspTime은 오디오 버퍼 단위(여기서는 20ms)로만 바뀌고, realtime은 프레임마다(약 16.7ms) 바뀐다고 본다.
/// </summary>
public class MusicClockTests
{
    private const double FrameSeconds = 1.0 / 60.0;
    private const double DspBufferSeconds = 0.02;

    /// <summary>
    /// 실제 시각 t의 dspTime (버퍼 단위로 내림).
    /// </summary>
    private static double DspAt(double t)
    {
        return System.Math.Floor(t / DspBufferSeconds) * DspBufferSeconds;
    }

    /// <summary>4-1. 준비 시간 동안 음수에서 시작해 0을 지나 늘어나고, 뒤로 가지 않는다</summary>
    [Test]
    public void StartsNegative_PassesZero_NeverGoesBack()
    {
        MusicClock clock = new MusicClock();
        double start = 100.0;
        clock.Schedule(start + 2.0, 0, DspAt(start), start);

        Assert.AreEqual(-2.0, clock.SongTime, 0.03);

        double previous = clock.SongTime;
        bool passedZero = false;
        for (int frame = 1; frame <= 300; frame++)
        {
            double t = start + frame * FrameSeconds;
            clock.Tick(DspAt(t), t);

            Assert.GreaterOrEqual(clock.SongTime, previous, $"프레임 {frame}에서 시간이 뒤로 갔습니다");
            if (previous < 0 && clock.SongTime >= 0) passedZero = true;
            previous = clock.SongTime;

            // 실제 곡 시간(t − 시작 − 2초)과의 차이가 버퍼 한 칸 + 1프레임을 넘지 않는다
            Assert.AreEqual(t - start - 2.0, clock.SongTime, DspBufferSeconds + FrameSeconds, $"프레임 {frame}");
        }

        Assert.IsTrue(passedZero, "0을 지나야 한다");
        Assert.Greater(clock.SongTime, 2.9);
    }

    /// <summary>4-2. dspTime이 예상보다 늦게 갱신돼도(뒤로 맞춰야 할 때) 곡 시간은 뒤로 가지 않는다</summary>
    [Test]
    public void LateDspUpdate_DoesNotMoveBackward()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(10.0, 0, 10.0, 10.0);

        clock.Tick(10.0, 10.1);   // dsp 그대로 → realtime으로 0.1초 보간
        double interpolated = clock.SongTime;
        Assert.AreEqual(0.1, interpolated, 1e-9);

        clock.Tick(10.05, 10.11); // dsp가 0.05만 갱신 (보간 값보다 뒤) → 그대로 유지
        Assert.GreaterOrEqual(clock.SongTime, interpolated);
    }

    /// <summary>4-3. 일시정지한 시간은 곡 시간에서 빠진다</summary>
    [Test]
    public void PauseTimeIsExcluded()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(0.0, 0, 0.0, 0.0);

        clock.Tick(1.0, 1.0);
        Assert.AreEqual(1.0, clock.SongTime, 1e-9);

        clock.Pause(1.0, 1.0);
        clock.Tick(4.0, 4.0);                // 일시정지 중에는 멈춘다
        Assert.AreEqual(1.0, clock.SongTime, 1e-9);
        Assert.AreEqual(1.0, clock.GetSongTimeAtRealtime(4.0), 1e-9);

        clock.Resume(4.0, 4.0);              // 3초 일시정지
        clock.Tick(5.0, 5.0);
        Assert.AreEqual(2.0, clock.SongTime, 1e-6);
    }

    /// <summary>
    /// 일시정지: 노래 시작 전(준비 시간)에 멈추면 남은 대기 시간이 그대로 남고, 재개하면 그만큼 기다린 뒤 시작 위치부터 재생된다.
    /// </summary>
    [Test]
    public void PauseBeforeStart_KeepsRemainingDelay()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(102.0, 0, 100.0, 100.0);   // 2초 뒤 시작

        clock.Tick(100.5, 100.5);
        Assert.AreEqual(-1.5, clock.SongTime, 1e-9);

        clock.Pause(100.5, 100.5);
        Assert.IsTrue(clock.IsPausedBeforeStart);
        Assert.AreEqual(0.0, clock.ResumePosition, 1e-9, "준비 시간에 멈췄으면 시작 위치부터 다시 재생한다");

        clock.Tick(110.0, 110.0);                  // 9.5초 멈춤
        Assert.AreEqual(-1.5, clock.SongTime, 1e-9);

        clock.Resume(110.0, 110.0, 0.1);
        Assert.IsFalse(clock.IsHolding, "남은 대기 1.5초가 여유 0.1초보다 길면 붙잡지 않는다");
        Assert.AreEqual(111.5, clock.SongTimeToDsp(clock.ResumePosition), 1e-9, "남은 1.5초 뒤에 시작해야 한다");

        clock.Tick(111.0, 111.0);
        Assert.AreEqual(-0.5, clock.SongTime, 1e-9);
        clock.Tick(112.0, 112.0);
        Assert.AreEqual(0.5, clock.SongTime, 1e-9, "멈춘 9.5초만큼 밀리지 않는다");
    }

    /// <summary>
    /// 일시정지: 준비 시간이 거의 끝날 때 멈췄다가 재개하면, 예약 여유만큼 곡 시간이 잠깐 멈췄다가 이어지고 뒤로 가지 않는다.
    /// </summary>
    [Test]
    public void PauseJustBeforeStart_HoldsForLead()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(10.0, 0, 9.0, 9.0);

        clock.Tick(9.98, 9.98);                    // 시작 0.02초 전
        clock.Pause(9.98, 9.98);
        clock.Resume(20.0, 20.0, 0.1);

        Assert.IsTrue(clock.IsHolding);
        Assert.AreEqual(20.1, clock.SongTimeToDsp(clock.ResumePosition), 1e-9, "예약은 최소 0.1초 뒤");

        clock.Tick(20.05, 20.05);
        Assert.AreEqual(-0.02, clock.SongTime, 1e-9, "여유 동안 곡 시간은 멈춘 값에 머문다 (뒤로 가지 않는다)");
        Assert.AreEqual(-0.05, clock.GetSongTimeAtRealtime(20.05), 1e-9, "입력 시각은 실제로 흐르는 시간으로 바꾼다");

        clock.Tick(20.2, 20.2);
        Assert.IsFalse(clock.IsHolding);
        Assert.AreEqual(0.1, clock.SongTime, 1e-9);
    }

    /// <summary>
    /// 일시정지: 재생 중에 멈추면 멈춘 곡 시간 위치부터 다시 재생하고, 멈춘 시간만큼 밀리지 않는다 (여유 0.1초만큼만 늦게 이어진다).
    /// </summary>
    [Test]
    public void PauseWhilePlaying_ResumesFromPausedTime()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(0.0, 0, 0.0, 0.0);

        clock.Tick(5.0, 5.0);
        clock.Pause(5.3, 5.3);                     // 멈추는 순간 시간을 더 진행시키지 않는다 (마지막 Tick 값)
        Assert.AreEqual(5.0, clock.SongTime, 1e-9);
        Assert.IsFalse(clock.IsPausedBeforeStart);
        Assert.AreEqual(5.0, clock.ResumePosition, 1e-9, "멈춘 곡 시간 위치부터 다시 재생한다");

        clock.Resume(30.0, 30.0, 0.1);             // 25초 멈춤
        Assert.IsTrue(clock.IsHolding);
        Assert.AreEqual(30.1, clock.SongTimeToDsp(5.0), 1e-9, "5초 위치를 0.1초 뒤에 재생하도록 예약한다");

        double previous = clock.SongTime;
        for (int frame = 1; frame <= 60; frame++)
        {
            double t = 30.0 + frame * FrameSeconds;
            clock.Tick(DspAt(t), t);
            Assert.GreaterOrEqual(clock.SongTime, previous, $"프레임 {frame}에서 시간이 뒤로 갔습니다");
            previous = clock.SongTime;
        }

        // 재개 1초 뒤: 5초 + (1초 − 여유 0.1초)
        Assert.AreEqual(5.9, clock.SongTime, DspBufferSeconds + FrameSeconds);
    }

    /// <summary>입력 시각 변환: 마지막 Tick보다 이른 입력은 그만큼 이른 곡 시간이 된다. 시작 위치도 더한다</summary>
    [Test]
    public void SongTimeAtRealtime_AndStartPosition()
    {
        MusicClock clock = new MusicClock();
        clock.Schedule(50.0, 30.0, 50.0, 50.0); // 음원 30초 위치부터 시작

        clock.Tick(50.5, 50.5);
        Assert.AreEqual(30.5, clock.SongTime, 1e-9);
        Assert.AreEqual(30.49, clock.GetSongTimeAtRealtime(50.49), 1e-9);

        clock.Stop();
        Assert.IsFalse(clock.IsRunning);
        Assert.AreEqual(0.0, clock.SongTime);
    }
}
