/// <summary>
/// 한 판 동안의 롱노트 끝 판정 통계 (플레이테스트용). LongFinal 결과만 받아 구간별로 센다. 숫자만 더하고 문자열·메모리를 새로 잡지 않는다.
/// 구간 (뗀 시각 − 끝 시각 = deltaMs, 경계는 BoundaryEpsilonMs로 안쪽 구간에 포함):
/// - 일찍 MISS: deltaMs &lt; −LongReleaseMs
/// - 일찍: −LongReleaseMs ~ −PerfectMs 미만
/// - 정시: −PerfectMs ~ +PerfectMs
/// - 늦게: +PerfectMs 초과 ~ +LongReleaseMs
/// - 자동 GOOD: +LongReleaseMs 초과 (끝 + 범위까지 누르고 있음. 자동 GOOD 처리 직전에 뗀 것도 여기에 센다)
/// 시작 MISS 롱노트는 끝 판정이 없으므로 따로 세고 구간·평균에서 뺀다. 평균 deltaMs는 자동 GOOD·시작 MISS를 뺀 나머지로 낸다.
/// </summary>
public class LongReleaseStats
{
    /// <summary>경계값 비교 허용 오차 (JudgeWindows·NoteJudge와 같은 값)</summary>
    private const double BoundaryEpsilonMs = 0.0001;

    private readonly double releaseMs;
    private readonly double perfectMs;

    /// <summary>범위보다 일찍 떼 끝 MISS</summary>
    public int EarlyMissCount { get; private set; }

    /// <summary>−LongReleaseMs ~ −PerfectMs 미만에서 뗌</summary>
    public int EarlyCount { get; private set; }

    /// <summary>−PerfectMs ~ +PerfectMs에서 뗌</summary>
    public int OnTimeCount { get; private set; }

    /// <summary>+PerfectMs 초과 ~ +LongReleaseMs에서 뗌</summary>
    public int LateCount { get; private set; }

    /// <summary>끝 + LongReleaseMs를 지나도록 누르고 있음 (끝 GOOD)</summary>
    public int AutoGoodCount { get; private set; }

    /// <summary>시작 MISS (끝 판정 없음, 구간·평균에서 뺀다)</summary>
    public int StartMissCount { get; private set; }

    /// <summary>평균에 넣은 개수 (일찍 MISS + 일찍 + 정시 + 늦게)</summary>
    public int ReleasedCount => EarlyMissCount + EarlyCount + OnTimeCount + LateCount;

    /// <summary>평균에 넣은 deltaMs 합</summary>
    public double ReleasedDeltaSumMs { get; private set; }

    /// <summary>평균 deltaMs (자동 GOOD·시작 MISS 제외, 없으면 0)</summary>
    public double AverageDeltaMs => ReleasedCount > 0 ? ReleasedDeltaSumMs / ReleasedCount : 0;

    /// <summary>모은 롱노트 수 (시작 MISS 포함)</summary>
    public int TotalCount => ReleasedCount + AutoGoodCount + StartMissCount;

    /// <param name="releaseMs">롱노트 끝 범위 ±ms (GameConstants.Play.LongReleaseMs)</param>
    /// <param name="perfectMs">정시 구간 ±ms (PERFECT 범위)</param>
    public LongReleaseStats(double releaseMs, double perfectMs)
    {
        this.releaseMs = releaseMs;
        this.perfectMs = perfectMs;
    }

    public void Reset()
    {
        EarlyMissCount = 0;
        EarlyCount = 0;
        OnTimeCount = 0;
        LateCount = 0;
        AutoGoodCount = 0;
        StartMissCount = 0;
        ReleasedDeltaSumMs = 0;
    }

    /// <summary>판정 결과 하나를 더한다. 롱노트 종합 판정(LongFinal)이 아니면 무시한다.</summary>
    public void Add(JudgeResult result)
    {
        if (result.Kind != JudgeKind.LongFinal) return;

        if (result.StartType == JudgeType.Miss)
        {
            StartMissCount++;
            return;
        }
        Add(result.DeltaMs);
    }

    /// <summary>끝 판정 하나를 뗀 시각 − 끝 시각(ms)으로 더한다 (시작 MISS가 아닌 롱노트).</summary>
    public void Add(double deltaMs)
    {
        if (deltaMs - BoundaryEpsilonMs > releaseMs)
        {
            AutoGoodCount++;
            return;
        }

        if (deltaMs < -releaseMs - BoundaryEpsilonMs) EarlyMissCount++;
        else if (deltaMs < -perfectMs - BoundaryEpsilonMs) EarlyCount++;
        else if (deltaMs - BoundaryEpsilonMs > perfectMs) LateCount++;
        else OnTimeCount++;

        ReleasedDeltaSumMs += deltaMs;
    }
}
