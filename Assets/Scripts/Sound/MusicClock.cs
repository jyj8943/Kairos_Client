using System;

/// <summary>
/// 노래 재생 시간(곡 시간) 계산. MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 곡 시간 = (dspTime − 노래 시작 DSP 시각) × 재생 배율 + 시작 위치 (초). 시작 전(준비 시간)에는 음수다.
///   재생 배율(Rate)은 보통 1.0이고 에디터 테스트에서만 바꾼다 (SoundManager.SetPlaybackRateForTests). 1.0이면 배율이 없던 때와 숫자가 같다.
/// - dspTime은 오디오 버퍼 단위로 띄엄띄엄 바뀌므로, 같은 값이 이어지는 동안은 realtime으로 앞으로 보간하고
///   dspTime이 바뀌면 그 값 쪽으로 다시 맞춘다. 보정 때문에 곡 시간이 뒤로 가지는 않는다.
/// - 재개하면 멈춘 곡 시간에서 이어지도록 시작 DSP 시각을 다시 정해, 멈춘 시간만큼 밀리지 않게 한다.
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

    /// <summary>노래가 예약됐거나 재생 중이면 true (일시정지 중에도 true)</summary>
    public bool IsRunning { get; private set; }

    /// <summary>일시정지 중이면 true</summary>
    public bool IsPaused { get; private set; }

    /// <summary>마지막 Tick 시점의 곡 시간 (초). 시작 전에는 음수</summary>
    public double SongTime { get; private set; }

    /// <summary>재생 배율 (실제 1초에 흐르는 곡 시간, 기본 1.0). 0보다 커야 한다</summary>
    public double Rate { get; private set; } = 1.0;

    /// <summary>
    /// 재생 배율을 바꾼다. 재생 중이면 지금 곡 시간이 튀지 않도록 시작 DSP 시각을 다시 정한다
    /// (곡 시간 = (dsp − 시작 DSP) × 배율 + 시작 위치가 dspNow에서 같은 값이 되게).
    /// </summary>
    /// <param name="rate">새 배율 (0 이하면 바꾸지 않는다)</param>
    public void SetRate(double rate, double dspNow, double realtimeNow)
    {
        if (!(rate > 0) || rate == Rate) return;

        if (IsRunning && !IsPaused)
        {
            double songTimeAtDsp = DspToSongTime(dspNow);
            Rate = rate;
            startDsp = dspNow - (songTimeAtDsp - startPosition) / rate;

            // 보간 기준점을 지금 표시 값으로 옮겨 앞으로는 새 배율로 늘어나게 한다
            syncSongTime = SongTime;
            syncRealtime = realtimeNow;
            lastTickRealtime = realtimeNow;
            return;
        }
        Rate = rate;
    }

    /// <summary>
    /// 노래를 예약한다. startDsp에 곡의 startPosition 위치가 재생된다고 본다.
    /// </summary>
    public void Schedule(double startDsp, double startPosition, double dspNow, double realtimeNow)
    {
        this.startDsp = startDsp;
        this.startPosition = startPosition;
        IsRunning = true;
        IsPaused = false;
        IsHolding = false;

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
        double predicted = syncSongTime + (realtimeNow - syncRealtime) * Rate;

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
        if (IsHolding && predicted >= SongTime) IsHolding = false;
        SongTime = Math.Max(SongTime, predicted);
        lastTickRealtime = realtimeNow;
    }

    /// <summary>
    /// 마지막 Tick 시점에서 realtime 차이(× 재생 배율)만큼 앞뒤로 옮긴 곡 시간. 입력 이벤트 시각을 곡 시간으로 바꿀 때 쓴다.
    /// 재생 중이 아니면 SongTime을 그대로 돌려준다.
    /// </summary>
    public double GetSongTimeAtRealtime(double realtime)
    {
        if (!IsRunning || IsPaused) return SongTime;

        // 재개 직후 붙잡아 두는 동안은 표시용 SongTime이 아니라 실제로 흐르는 시간으로 바꾼다 (음원이 아직 그 위치에 오지 않았다)
        if (IsHolding) return syncSongTime + (realtime - syncRealtime) * Rate;
        return SongTime + (realtime - lastTickRealtime) * Rate;
    }

    /// <summary>
    /// 일시정지한다. 곡 시간은 마지막 Tick 값(이번 프레임에 노트·판정이 쓴 값)에서 멈춘다.
    /// 여기서 Tick을 더 하지 않는다: 멈추는 순간 시간이 앞으로 가면 그 사이 노트가 처리되지 않은 채 남는다.
    /// </summary>
    public void Pause(double dspNow, double realtimeNow)
    {
        if (!IsRunning || IsPaused) return;

        IsPaused = true;
    }

    /// <summary>
    /// 재개할 때 음원을 다시 재생할 위치 (초). 노래 시작 전(준비 시간)에 멈췄으면 시작 위치, 재생 중에 멈췄으면 멈춘 곡 시간.
    /// </summary>
    public double ResumePosition => Math.Max(SongTime, startPosition);

    /// <summary>
    /// 노래 시작 전(준비 시간)에 멈췄으면 true
    /// </summary>
    public bool IsPausedBeforeStart => IsPaused && SongTime < startPosition;

    /// <summary>
    /// 재개 직후 곡 시간을 멈춘 값에 붙잡아 두는 중이면 true (minLeadSeconds 때문에 생긴 여유 동안)
    /// </summary>
    public bool IsHolding { get; private set; }

    /// <summary>
    /// 재개한다. 멈춘 곡 시간에서 이어지도록 시작 DSP 시각을 다시 정한다 (멈춘 시간만큼 밀리지 않는다).
    /// minLeadSeconds(실제 초)를 주면 ResumePosition을 재생할 DSP 시각이 지금보다 최소 그만큼 뒤가 되도록 미룬다
    /// (음원을 PlayScheduled로 다시 예약할 때 시작이 늦지 않게). 그 여유 동안 곡 시간은 멈춘 값에 머물고 뒤로 가지 않는다.
    /// </summary>
    /// <param name="minLeadSeconds">재생 예약까지 둘 최소 여유 (초). 0이면 지금 바로 이어진다</param>
    public void Resume(double dspNow, double realtimeNow, double minLeadSeconds = 0)
    {
        if (!IsRunning || !IsPaused) return;

        // 지금 DSP 시각에 곡 시간이 멈춘 값(SongTime)이 되도록 한다 (배율 1.0이면 dspNow − SongTime + startPosition)
        startDsp = dspNow - SongTime / Rate + startPosition / Rate;

        // 다시 재생할 위치까지 남은 실제 시간이 여유보다 짧으면 그 차이만큼 더 미룬다 (extra는 실제 초)
        double lead = (ResumePosition - SongTime) / Rate;
        double extra = Math.Max(0, minLeadSeconds - lead);
        startDsp += extra;
        IsPaused = false;
        IsHolding = extra > 0;

        lastDsp = dspNow;
        syncSongTime = SongTime - extra * Rate;
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
        IsHolding = false;
        SongTime = 0;
    }

    private double DspToSongTime(double dsp)
    {
        return (dsp - startDsp) * Rate + startPosition;
    }

    /// <summary>
    /// 곡 시간을 DSP 시각으로 바꾼다 (DspToSongTime의 반대). 효과음을 박에 맞춰 예약할 때 쓴다.
    /// 일시정지 중에는 재개 시각을 알 수 없으므로 의미가 없다.
    /// </summary>
    public double SongTimeToDsp(double songTime)
    {
        return (songTime - startPosition) / Rate + startDsp;
    }
}
