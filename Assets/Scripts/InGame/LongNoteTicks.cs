using System.Collections.Generic;

/// <summary>
/// 롱노트 유지 틱 위치 계산 (롱노트 판정 세부 규칙 1). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 틱은 채보의 박 기준으로 고정한다: 시작 박 + 1/4박, + 2/4박, + 3/4박 …
/// - 마지막 틱은 끝 박 − 1/4박 "이하"인 틱까지다. 끝이 1/4박 칸에 맞지 않아도 같은 규칙이다.
///   예) 0 → 2박: 0.25 ~ 1.75박 7틱 / 0 → 1/4박: 0틱 / 0 → 4/3박: 0.25 ~ 1.0박 4틱 / 0 → 1/3박: 0틱
/// - 박은 tick(1박 = TicksPerBeat)으로 반올림해 정수로 비교한다 (셋잇단 끝의 부동소수 오차를 피한다).
/// - 틱의 시간(초)은 TimingMap으로 박에서 계산한다 (BPM 변속이 있어도 같은 규칙).
/// </summary>
public static class LongNoteTicks
{
    /// <summary>틱 간격 (tick) = 1/4박</summary>
    private const int TickStep = GameConstants.Editor.TicksPerBeat / 4;

    /// <summary>
    /// 유지 틱 수. 시작 + 1/4박부터 1/4박마다, 끝 − 1/4박 이하인 것만 센다.
    /// </summary>
    public static int Count(double startBeat, double endBeat)
    {
        int startTick = BeatGridMath.BeatToTick((float)startBeat);
        int lastTick = BeatGridMath.BeatToTick((float)endBeat) - TickStep;
        if (lastTick < startTick + TickStep) return 0;
        return (lastTick - startTick) / TickStep;
    }

    /// <summary>
    /// 유지 틱의 박 위치 목록 (시간 순). 틱이 없으면 빈 배열.
    /// </summary>
    public static double[] GetTickBeats(double startBeat, double endBeat)
    {
        int count = Count(startBeat, endBeat);
        double[] beats = new double[count];
        int startTick = BeatGridMath.BeatToTick((float)startBeat);
        for (int i = 0; i < count; i++)
            beats[i] = (double)(startTick + TickStep * (i + 1)) / GameConstants.Editor.TicksPerBeat;
        return beats;
    }

    /// <summary>
    /// 유지 틱의 시간(초, 곡 시간) 목록. PlayNote를 만들 때 한 번만 부른다.
    /// </summary>
    public static double[] GetTickTimes(TimingMap timing, double startBeat, double endBeat)
    {
        double[] beats = GetTickBeats(startBeat, endBeat);
        for (int i = 0; i < beats.Length; i++) beats[i] = timing.BeatToSeconds(beats[i]);
        return beats;
    }

    /// <summary>노트 목록의 전체 틱 수 (최대 콤보 계산용)</summary>
    public static int TotalTicks(IReadOnlyList<PlayNote> notes)
    {
        int total = 0;
        for (int i = 0; i < notes.Count; i++) total += notes[i].TickCount;
        return total;
    }
}
