using UnityEngine;

/// <summary>
/// 박(beat) 기준 그리드 계산 모음. 상태가 없는 순수 함수만 둔다.
/// - 에디터 그리드 표시뿐 아니라 노트 배치·인게임에서도 다시 쓴다.
/// - 위로 갈수록 뒤 시간이다. y는 현재 위치 선을 0으로 한 값이다.
/// </summary>
public static class BeatGridMath
{
    /// <summary>스냅 위치 비교에 쓰는 허용 오차 (박 × 분할 수 단위)</summary>
    private const float SnapEpsilon = 0.0001f;

    /// <summary>
    /// 음원 시간(초)을 박으로 바꾼다. 0박 = 음원 시작 + offsetMs. (BPM 변속이 없다고 본다)
    /// 박 = (초 − offsetMs ÷ 1000) × BPM ÷ 60. offset 이전 시간은 음수 박이 된다.
    /// </summary>
    public static float SecondsToBeat(float seconds, float bpm, int offsetMs)
    {
        return (seconds - offsetMs / 1000f) * bpm / 60f;
    }

    /// <summary>
    /// 박을 음원 시간(초)으로 바꾼다. SecondsToBeat의 반대 변환이다.
    /// 초 = 박 × 60 ÷ BPM + offsetMs ÷ 1000
    /// </summary>
    public static float BeatToSeconds(float beat, float bpm, int offsetMs)
    {
        return beat * 60f / bpm + offsetMs / 1000f;
    }

    /// <summary>
    /// beat의 y 위치. (beat − 현재 beat) × 1박당 픽셀 × 그리드 높이 배율
    /// </summary>
    public static float BeatToY(float beat, float currentBeat, float pixelsPerBeat, float heightScale)
    {
        return (beat - currentBeat) * pixelsPerBeat * heightScale;
    }

    /// <summary>
    /// y 위치의 beat. BeatToY의 반대 변환이다.
    /// </summary>
    public static float YToBeat(float y, float currentBeat, float pixelsPerBeat, float heightScale)
    {
        return currentBeat + y / (pixelsPerBeat * heightScale);
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
