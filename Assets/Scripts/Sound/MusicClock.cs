using System;

/// <summary>
/// 노래 재생 시간(곡 시간) 계산. MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 곡 시간 = dspTime − 노래 시작 DSP 시각 + 시작 위치 (초). 시작 전(준비 시간)에는 음수다.
/// - dspTime은 오디오 버퍼 단위로 띄엄띄엄 바뀌므로, 같은 값이 이어지는 동안은 realtime으로 앞으로 보간하고
///   dspTime이 바뀌면 그 값 쪽으로 다시 맞춘다. 보정 때문에 곡 시간이 뒤로 가지는 않는다.
/// - 일시정지한 시간만큼 시작 DSP 시각을 뒤로 미뤄 재개 후 시간이 밀리지 않게 한다.
/// - 시각은 모두 바깥에서 넣는다 (SoundManager: AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble).
/// </summary>
public class MusicClock
{
    /// <summary>보간 값과 dspTime 값의 차이가 이보다 크면 바로 맞춘다 (초)</summary>
    public const double HardResyncSeconds = 0.05;

    /// <summary>차이가 작을 때 한 번 갱신마다 줄이는 비율 (0~1)</summary>
    public const double SoftCorrectionRate = 0.2;

    private double startDsp;
    private double startPosition;
    private double lastDsp;
    private double syncSongTime;
    private double syncRealtime;
    private double lastTickRealtime;
    private double pauseDsp;

    /// <summary>노래가 예약됐거나 재생 중이면 true (일시정지 중에도 true)</summary>
    public bool IsRunning { get; private set; }

    /// <summary>일시정지 중이면 true</summary>
    public bool IsPaused { get; private set; }

    /// <summary>마지막 Tick 시점의 곡 시간 (초). 시작 전에는 음수</summary>
    public double SongTime { get; private set; }

    /// <summary>
    /// 노래를 예약한다. startDsp에 곡의 startPosition 위치가 재생된다고 본다.
    /// </summary>
    public void Schedule(double startDsp, double startPosition, double dspNow, double realtimeNow)
    {
        this.startDsp = startDsp;
        this.startPosition = startPosition;
        IsRunning = true;
        IsPaused = false;

        lastDsp = dspNow;
        syncSongTime = DspToSongTime(dspNow);
        syncRealtime = realtimeNow;
        lastTickRealtime = realtimeNow;
        SongTime = syncSongTime;
    }

    /// <summary>
    /// 매 프레임 호출한다. dspTime이 바뀌었으면 그 값 쪽으로 맞추고, 아니면 realtime으로 보간한다.
    /// </summary>
    public void Tick(double dspNow, double realtimeNow)
    {
        if (!IsRunning || IsPaused)
        {
            lastTickRealtime = realtimeNow;
            return;
        }

        // 지난 기준점에서 흐른 realtime으로 예상한 값
        double predicted = syncSongTime + (realtimeNow - syncRealtime);

        if (dspNow != lastDsp)
        {
            lastDsp = dspNow;
            double dspSongTime = DspToSongTime(dspNow);
            double error = dspSongTime - predicted;

            // 차이가 크면 바로 맞추고, 작으면 조금씩 맞춰 화면이 튀지 않게 한다
            syncSongTime = Math.Abs(error) > HardResyncSeconds ? dspSongTime : predicted + error * SoftCorrectionRate;
            syncRealtime = realtimeNow;
            predicted = syncSongTime;
        }

        // 곡 시간은 뒤로 가지 않는다
        SongTime = Math.Max(SongTime, predicted);
        lastTickRealtime = realtimeNow;
    }

    /// <summary>
    /// 마지막 Tick 시점에서 realtime 차이만큼 앞뒤로 옮긴 곡 시간. 입력 이벤트 시각을 곡 시간으로 바꿀 때 쓴다.
    /// 재생 중이 아니면 SongTime을 그대로 돌려준다.
    /// </summary>
    public double GetSongTimeAtRealtime(double realtime)
    {
        if (!IsRunning || IsPaused) return SongTime;
        return SongTime + (realtime - lastTickRealtime);
    }

    /// <summary>
    /// 일시정지한다. 곡 시간은 지금 값에서 멈춘다.
    /// </summary>
    public void Pause(double dspNow, double realtimeNow)
    {
        if (!IsRunning || IsPaused) return;

        Tick(dspNow, realtimeNow);
        IsPaused = true;
        pauseDsp = dspNow;
    }

    /// <summary>
    /// 재개한다. 일시정지한 시간만큼 시작 DSP 시각을 뒤로 미룬다.
    /// </summary>
    public void Resume(double dspNow, double realtimeNow)
    {
        if (!IsRunning || !IsPaused) return;

        startDsp += dspNow - pauseDsp;
        IsPaused = false;

        lastDsp = dspNow;
        syncSongTime = SongTime;
        syncRealtime = realtimeNow;
        lastTickRealtime = realtimeNow;
    }

    /// <summary>
    /// 멈추고 곡 시간을 0으로 되돌린다.
    /// </summary>
    public void Stop()
    {
        IsRunning = false;
        IsPaused = false;
        SongTime = 0;
    }

    private double DspToSongTime(double dsp)
    {
        return dsp - startDsp + startPosition;
    }
}
