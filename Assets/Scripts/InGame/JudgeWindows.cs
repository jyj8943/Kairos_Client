using System;
using UnityEngine;

/// <summary>
/// 판정 범위 (±ms). InGameScene 인스펙터에서 조정하고, 기본값은 기획서 잠정치(GameConstants.Play)다.
/// - |입력 − 노트| ≤ MISS 범위면 판정하고, 그보다 밖이면 빈 타격(판정 없음).
/// - 노트가 +GOOD 범위를 지나도록 안 누르면 MISS.
/// </summary>
[Serializable]
public class JudgeWindows
{
    /// <summary>경계값 비교 허용 오차 (ms). 부동소수 계산 때문에 40ms가 40.0000001ms로 나와도 PERFECT로 본다</summary>
    private const double BoundaryEpsilonMs = 0.0001;

    [Min(0f)] [SerializeField] private float perfectMs = GameConstants.Play.DefaultPerfectMs;
    [Min(0f)] [SerializeField] private float greatMs = GameConstants.Play.DefaultGreatMs;
    [Min(0f)] [SerializeField] private float goodMs = GameConstants.Play.DefaultGoodMs;
    [Tooltip("이 범위 안에서 누르면 MISS 판정, 밖이면 빈 타격")]
    [Min(0f)] [SerializeField] private float missMs = GameConstants.Play.DefaultMissMs;

    public float PerfectMs => perfectMs;
    public float GreatMs => greatMs;
    public float GoodMs => goodMs;
    public float MissMs => missMs;

    /// <summary>노트가 이만큼(ms) 지나도록 안 누르면 MISS (= GOOD 범위)</summary>
    public float LateMissMs => goodMs;

    public JudgeWindows() { }

    public JudgeWindows(float perfectMs, float greatMs, float goodMs, float missMs)
    {
        this.perfectMs = perfectMs;
        this.greatMs = greatMs;
        this.goodMs = goodMs;
        this.missMs = missMs;
    }

    /// <summary>
    /// 노트가 지난 시간(ms)이 +GOOD 범위를 넘었으면 true (놓친 MISS). 경계값은 TryJudge와 같은 허용 오차로 비교한다.
    /// </summary>
    public bool IsLateMiss(double elapsedMs)
    {
        return elapsedMs - BoundaryEpsilonMs > LateMissMs;
    }

    /// <summary>
    /// 입력과 노트의 차이(ms, 늦으면 +)로 판정한다. MISS 범위 밖이면 false (빈 타격).
    /// </summary>
    public bool TryJudge(double deltaMs, out JudgeType judge)
    {
        double abs = Math.Abs(deltaMs) - BoundaryEpsilonMs;

        if (abs <= perfectMs) judge = JudgeType.Perfect;
        else if (abs <= greatMs) judge = JudgeType.Great;
        else if (abs <= goodMs) judge = JudgeType.Good;
        else if (abs <= missMs) judge = JudgeType.Miss;
        else
        {
            judge = JudgeType.Miss;
            return false;
        }
        return true;
    }
}
