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
