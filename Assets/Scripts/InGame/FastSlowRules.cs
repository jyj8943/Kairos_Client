/// <summary>FAST / SLOW 방향</summary>
public enum FastSlow
{
    /// <summary>표시·집계하지 않음 (PERFECT, MISS, 유지 틱)</summary>
    None,

    /// <summary>일찍 쳤다 (롱노트 끝은 일찍 뗐다)</summary>
    Fast,

    /// <summary>늦게 쳤다 (롱노트 끝은 늦게 뗐다, 끝을 지나 계속 눌러 자동 GOOD 포함)</summary>
    Slow
}

/// <summary>
/// FAST / SLOW 규칙 (기획서 3-4 "판정 패널 규칙"). MonoBehaviour가 아닌 정적 클래스라 EditMode 테스트로 검사한다.
/// - GREAT · GOOD에서만 방향을 정한다. PERFECT · MISS(일찍 떼 MISS·시작 MISS 포함)·유지 틱은 없음.
/// - 방향은 DeltaMs 부호로만 정한다 (판정 범위 값은 쓰지 않는다): 0보다 작으면 FAST, 그 외 SLOW.
///   - 숏노트·롱노트 시작: 누른 시각 − 노트 시각
///   - 롱노트 종합(LongFinal): 뗀 시각 − 끝 시각. 등급은 시작 판정을 따르고 방향만 뗀 타이밍으로 정한다
///     (예: 늦게 눌러 GREAT SLOW로 시작해 끝에서 조금 일찍 떼면 GREAT FAST, DJMAX와 같음).
///   - 끝을 지나 계속 눌러 자동 GOOD: NoteJudge가 DeltaMs = 지난 시간(> +LongReleaseMs)으로 넣으므로 항상 SLOW.
/// - 표시: 숏노트·롱노트 시작·롱노트 종합. 집계: 숏노트·롱노트 종합만 (롱노트 하나를 한 번만 센다)
///   → 항상 FAST + SLOW = GREAT + GOOD.
/// </summary>
public static class FastSlowRules
{
    /// <summary>
    /// 판정 글자 위에 띄울 FAST / SLOW (숏노트, 롱노트 시작, 롱노트 종합). 유지 틱은 None.
    /// </summary>
    public static FastSlow GetDisplay(in JudgeResult result)
    {
        switch (result.Kind)
        {
            case JudgeKind.Short:
            case JudgeKind.LongStart:
            case JudgeKind.LongFinal:
                return Direction(result.Type, result.DeltaMs);
            default:
                return FastSlow.None;
        }
    }

    /// <summary>
    /// 판정 패널 FAST / SLOW 개수에 셀 방향 (숏노트, 롱노트 종합만). 롱노트 시작·유지 틱은 None.
    /// </summary>
    public static FastSlow GetCount(in JudgeResult result)
    {
        switch (result.Kind)
        {
            case JudgeKind.Short:
            case JudgeKind.LongFinal:
                return Direction(result.Type, result.DeltaMs);
            default:
                return FastSlow.None;
        }
    }

    /// <summary>GREAT · GOOD이면 DeltaMs 부호로 방향을 정한다. 그 밖의 판정은 None</summary>
    private static FastSlow Direction(JudgeType type, double deltaMs)
    {
        if (type != JudgeType.Great && type != JudgeType.Good) return FastSlow.None;
        return deltaMs < 0 ? FastSlow.Fast : FastSlow.Slow;
    }
}
