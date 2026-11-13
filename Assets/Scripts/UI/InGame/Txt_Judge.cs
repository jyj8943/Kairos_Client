using UnityEngine;

/// <summary>
/// 인게임 판정 글자 (Txt_Judge, 판정선 위). DJMAX RESPECT V 기준:
/// 판정이 날 때마다 처음부터 — 크기 0.7 → 1.0 (0.08초, ease-out quad) · 투명도 0 → 1 (0.04초) → 등장부터 0.82초 유지 → 0.13초에 걸쳐 사라짐 → 숨김.
/// 판정 색(PERFECT·GREAT·GOOD·MISS)은 Canvas_InGame이 Show로 넘긴다. 계산은 JudgeTextAnimation, 적용은 AnimatedJudgeText.
/// </summary>
public class Txt_Judge : AnimatedJudgeText
{
    [Header("등장")]
    [Tooltip("등장 시작 크기 (1 = 원래 크기)")]
    [SerializeField] private float startScale = 0.7f;
    [Tooltip("크기가 1이 되는 시간 (초, ease-out quad)")]
    [SerializeField] private float scaleInSeconds = 0.08f;
    [Tooltip("투명 → 불투명 시간 (초, 선형)")]
    [SerializeField] private float fadeInSeconds = 0.04f;

    [Header("유지 · 퇴장")]
    [Tooltip("등장 시작부터 사라지기 시작할 때까지 (초)")]
    [SerializeField] private float holdSeconds = 0.82f;
    [Tooltip("사라지는 시간 (초, 크기는 그대로)")]
    [SerializeField] private float fadeOutSeconds = 0.13f;

    protected override void ApplySettings(JudgeTextAnimation target)
    {
        target.StartScale = startScale;
        target.ScaleInSeconds = scaleInSeconds;
        target.FadeInSeconds = fadeInSeconds;
        target.HoldSeconds = holdSeconds;
        target.FadeOutSeconds = fadeOutSeconds;
    }
}
