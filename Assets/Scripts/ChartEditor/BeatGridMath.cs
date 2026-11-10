using UnityEngine;

/// <summary>
/// 박(beat) 기준 그리드 계산 모음. 상태가 없는 순수 함수만 둔다.
/// - 에디터 그리드 표시뿐 아니라 노트 배치·인게임에서도 다시 쓴다.
/// - 위로 갈수록 뒤 시간이다. y는 현재 위치 선을 0으로 한 값이다.
/// - 박 ↔ 시간은 TimingMap이 맡는다. 그리드 높이는 시간 기준(BeatToY·YToBeat)이고, tick·스냅·마디 계산은 박 기준이다.
/// </summary>
public static class BeatGridMath
{
    /// <summary>스냅 위치 비교에 쓰는 허용 오차 (박 × 분할 수 단위)</summary>
    private const float SnapEpsilon = 0.0001f;

    /// <summary>
    /// BPM 하나일 때 그리드에서 1박의 높이 (px) = 1초 높이(PixelsPerSecond) × 60 ÷ BPM × 그리드 높이 배율.
    /// 그리드 배치는 시간 기준(BeatToY)이라 여기서는 스냅 선 간격 안내·선 풀 크기 계산에만 쓴다.
    /// </summary>
    public static float GetBeatHeight(float bpm, float heightScale)
    {
        return GameConstants.Editor.PixelsPerSecond * 60f / bpm * heightScale;
    }

    /// <summary>
    /// beat의 y 위치 (현재 위치 선 = 0). 시간 기준이라 BPM 변속이 있어도 인게임 낙하(시간 기준)와 노트 간격이 같다.
    /// y = (BeatToSeconds(beat) − BeatToSeconds(currentBeat)) × PixelsPerSecond × 그리드 높이 배율
    /// (BPM이 하나면 (beat − currentBeat) × GetBeatHeight(bpm, heightScale)와 같다)
    /// </summary>
    public static float BeatToY(TimingMap timing, float beat, float currentBeat, float heightScale)
    {
        double seconds = timing.BeatToSeconds(beat) - timing.BeatToSeconds(currentBeat);
        return (float)(seconds * GameConstants.Editor.PixelsPerSecond * heightScale);
    }

    /// <summary>
    /// y 위치(현재 위치 선 = 0)의 beat. BeatToY의 반대 변환이다.
    /// beat = SecondsToBeat(BeatToSeconds(currentBeat) + y ÷ (PixelsPerSecond × 그리드 높이 배율))
    /// </summary>
    public static float YToBeat(TimingMap timing, float y, float currentBeat, float heightScale)
    {
        double seconds = timing.BeatToSeconds(currentBeat) + y / ((double)GameConstants.Editor.PixelsPerSecond * heightScale);
        return (float)timing.SecondsToBeat(seconds);
    }

    /// <summary>
    /// 지금 스냅의 선 간격 (px) = 1박 높이 ÷ 스냅 칸 수.
    /// </summary>
    /// <param name="bpm">선 간격을 볼 위치의 BPM (TimingMap.BpmAt(현재 박))</param>
    public static float GetSnapLineSpacing(float bpm, float heightScale, int snapDivision)
    {
        return GetBeatHeight(bpm, heightScale) / snapDivision;
    }

    /// <summary>
    /// 선 간격이 MinSnapLineSpacingPixels보다 좁으면 true ("선 간격이 좁습니다" 안내를 보여줄지).
    /// </summary>
    /// <param name="bpm">선 간격을 볼 위치의 BPM (TimingMap.BpmAt(현재 박))</param>
    public static bool IsSnapLineSpacingTooSmall(float bpm, float heightScale, int snapDivision)
    {
        return GetSnapLineSpacing(bpm, heightScale, snapDivision) < GameConstants.Editor.MinSnapLineSpacingPixels;
    }

    /// <summary>
    /// beat를 가장 가까운 스냅 위치로 반올림한다. 1/n은 1박을 n칸으로 나눈다.
    /// </summary>
    public static float SnapBeat(float beat, int snapDivision)
    {
        return Mathf.Round(beat * snapDivision) / snapDivision;
    }

    /// <summary>
    /// tick을 박으로 바꾼다 (beat = tick ÷ TicksPerBeat).
    /// </summary>
    public static float TickToBeat(int tick)
    {
        return (float)tick / GameConstants.Editor.TicksPerBeat;
    }

    /// <summary>
    /// 박을 가장 가까운 tick으로 바꾼다. 채보를 불러올 때 쓴다.
    /// </summary>
    public static int BeatToTick(float beat)
    {
        return Mathf.RoundToInt(beat * GameConstants.Editor.TicksPerBeat);
    }

    /// <summary>
    /// beat를 스냅 n의 가장 가까운 선으로 반올림한 tick. (스냅 한 칸 = TicksPerBeat ÷ n 틱)
    /// </summary>
    public static int SnapBeatToTick(float beat, int snapDivision)
    {
        int ticksPerSnap = GameConstants.Editor.TicksPerBeat / snapDivision;
        return Mathf.RoundToInt(beat * snapDivision) * ticksPerSnap;
    }

    /// <summary>
    /// 스냅 한 칸씩 이동한 beat. 스냅 위치에 있지 않으면 진행 방향의 다음 스냅 위치로 간다.
    /// </summary>
    /// <param name="steps">양수면 뒤 시간(위), 음수면 앞 시간(아래)</param>
    public static float StepBeat(float beat, int snapDivision, int steps)
    {
        if (steps == 0) return beat;

        float index = beat * snapDivision;
        float next = steps > 0
            ? Mathf.Floor(index + SnapEpsilon) + steps
            : Mathf.Ceil(index - SnapEpsilon) + steps;
        return next / snapDivision;
    }

    /// <summary>
    /// 1박 안의 위치 k/n을 약분한 분모. k가 0(박 위)이면 1이다.
    /// </summary>
    public static int ReduceDenominator(int k, int n)
    {
        return n / GreatestCommonDivisor(k, n);
    }

    /// <summary>
    /// 선 종류를 판정한다.
    /// </summary>
    /// <param name="lineIndex">0박부터 센 스냅 칸 번호 (beat × n). 정수로 받아 부동소수 오차를 피한다</param>
    /// <param name="snapDivision">박자 스냅 n</param>
    public static GridLineType GetLineType(int lineIndex, int snapDivision)
    {
        int k = lineIndex % snapDivision;
        int denominator = ReduceDenominator(k, snapDivision);

        if (denominator == 1)
        {
            int beat = lineIndex / snapDivision;
            return beat % GameConstants.Editor.BeatsPerMeasure == 0 ? GridLineType.Measure : GridLineType.Beat;
        }

        return GetLineTypeByDenominator(denominator);
    }

    /// <summary>
    /// 약분한 분모의 선 종류. 분모 1은 마디 여부를 알 수 없어 박선으로 본다.
    /// </summary>
    public static GridLineType GetLineTypeByDenominator(int denominator)
    {
        switch (denominator)
        {
            case 1: return GridLineType.Beat;
            case 2: return GridLineType.Half;
            case 4: return GridLineType.Quarter;
            case 8: return GridLineType.Eighth;
            case 16: return GridLineType.Sixteenth;
            case 3:
            case 6: return GridLineType.Third;
            case 12: return GridLineType.Twelfth;
            default:
                Debug.LogWarning($"[BeatGridMath] 정해지지 않은 분모입니다: {denominator}");
                return GridLineType.Sixteenth;
        }
    }

    /// <summary>
    /// beat가 속한 마디 번호 (1부터).
    /// </summary>
    public static int BeatToMeasure(float beat)
    {
        return Mathf.FloorToInt(beat / GameConstants.Editor.BeatsPerMeasure + SnapEpsilon) + 1;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Mathf.Abs(a);
        b = Mathf.Abs(b);
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return a;
    }
}
