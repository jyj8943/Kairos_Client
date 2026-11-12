/// <summary>
/// 한 판 동안의 프레임 시간 통계 (평균 fps, 최저 fps = 가장 긴 프레임 기준). 매 프레임 숫자만 더하고 문자열은 만들지 않는다.
/// </summary>
public class FrameTimeStats
{
    /// <summary>모은 프레임 수</summary>
    public int Count { get; private set; }

    /// <summary>모은 프레임 시간 합 (초)</summary>
    public double TotalSeconds { get; private set; }

    /// <summary>가장 긴 프레임 시간 (초)</summary>
    public double MaxSeconds { get; private set; }

    /// <summary>평균 fps = 프레임 수 ÷ 시간 합 (없으면 0)</summary>
    public double AverageFps => TotalSeconds > 0 ? Count / TotalSeconds : 0;

    /// <summary>최저 fps = 1 ÷ 가장 긴 프레임 (없으면 0)</summary>
    public double MinFps => MaxSeconds > 0 ? 1.0 / MaxSeconds : 0;

    public void Reset()
    {
        Count = 0;
        TotalSeconds = 0;
        MaxSeconds = 0;
    }

    /// <summary>프레임 하나를 더한다 (0 이하는 무시)</summary>
    public void Add(double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;
        Count++;
        TotalSeconds += deltaSeconds;
        if (deltaSeconds > MaxSeconds) MaxSeconds = deltaSeconds;
    }
}
