using System;

/// <summary>
/// 배속 계산 모음 (기획서 2-1 6). 곡 선택·인게임·에디터가 같이 쓴다. Unity API를 쓰지 않아 EditMode 테스트로 검사할 수 있다.
/// - 배속은 1.0 ~ 8.0배, 0.1 단위. 0.1씩 더하면 부동소수 오차가 쌓이므로 배속 × ScrollMultiplierScale(10)의 정수 "단계"로 계산한다.
/// - 1.0배 기준: 노트 중심이 화면 위 끝 → 판정선까지 BaseVisibleSeconds(2.69초). 배속 s면 보이는 시간 = 2.69 ÷ s.
///   낙하 속도(유닛/초) = (화면 위 끝 y − 판정선 y) ÷ 2.69 × s. 기어 높이·해상도가 바뀌어도 보이는 시간이 같다.
/// - 판정은 배속과 상관없다 (곡 시간 기준).
/// </summary>
public static class ScrollSpeedRules
{
    /// <summary>최저 배속의 단계 (1.0 → 10)</summary>
    public static int MinSteps => ToStepsUnclamped(GameConstants.Play.MinScrollMultiplier);

    /// <summary>최고 배속의 단계 (8.0 → 80)</summary>
    public static int MaxSteps => ToStepsUnclamped(GameConstants.Play.MaxScrollMultiplier);

    /// <summary>
    /// 배속을 단계(정수, 배속 × 10)로 바꾼다. 범위 밖이면 범위 안으로 맞춘다.
    /// </summary>
    public static int ToSteps(float multiplier)
    {
        return Clamp(ToStepsUnclamped(multiplier), MinSteps, MaxSteps);
    }

    /// <summary>단계를 배속으로 바꾼다 (예: 56 → 5.6).</summary>
    public static float FromSteps(int steps)
    {
        return (float)steps / GameConstants.Play.ScrollMultiplierScale;
    }

    /// <summary>
    /// 단계를 delta만큼(+1 = 0.1배) 바꾼다. 범위 끝에서는 멈춘다.
    /// </summary>
    public static int Step(int steps, int delta)
    {
        return Clamp(steps + delta, MinSteps, MaxSteps);
    }

    /// <summary>
    /// 배속을 delta 단계만큼 바꾼 배속. 단계로 바꿔 계산하므로 오차가 쌓이지 않는다.
    /// </summary>
    public static float StepMultiplier(float multiplier, int delta)
    {
        return FromSteps(Step(ToSteps(multiplier), delta));
    }

    /// <summary>배속 s에서 노트가 화면 위 끝 → 판정선까지 보이는 시간 (초) = 2.69 ÷ s</summary>
    public static float VisibleSeconds(float multiplier)
    {
        return GameConstants.Play.BaseVisibleSeconds / multiplier;
    }

    /// <summary>
    /// 낙하 속도 (유닛/초) = 화면 위 끝 ~ 판정선 거리 ÷ 2.69초 × 배속.
    /// </summary>
    /// <param name="visibleDistance">화면 위 끝 y − 판정선 y (월드 유닛)</param>
    public static float ScrollSpeed(float visibleDistance, float multiplier)
    {
        return visibleDistance / GameConstants.Play.BaseVisibleSeconds * multiplier;
    }

    private static int ToStepsUnclamped(float multiplier)
    {
        return (int)Math.Round(multiplier * GameConstants.Play.ScrollMultiplierScale, MidpointRounding.AwayFromZero);
    }

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : (value > max ? max : value);
    }
}
