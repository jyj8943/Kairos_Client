using UnityEngine;

/// <summary>
/// 인게임 판정 글자 위 FAST / SLOW (Txt_FastSlow). DJMAX RESPECT V 기준:
/// 새 FAST/SLOW가 나면 글자·색을 바꾸고 바로 불투명하게 (크기·투명도 등장 없음) → 0.47초 유지 → 0.15초에 걸쳐 사라짐 → 숨김.
/// 판정 글자와 따로 자기 시간만큼 남는다 (다음 판정이 PERFECT이거나 FAST/SLOW 대상이 아니어도 지우지 않음).
/// 계산은 JudgeTextAnimation, 적용은 AnimatedJudgeText.
/// </summary>
public class Txt_FastSlow : AnimatedJudgeText
{
    [Tooltip("나타난 뒤 사라지기 시작할 때까지 (초)")]
    [SerializeField] private float holdSeconds = 0.47f;
    [Tooltip("사라지는 시간 (초)")]
    [SerializeField] private float fadeOutSeconds = 0.15f;

    protected override void ApplySettings(JudgeTextAnimation target)
    {
        target.StartScale = 1f;
        target.ScaleInSeconds = 0f;
        target.FadeInSeconds = 0f;
        target.HoldSeconds = holdSeconds;
        target.FadeOutSeconds = fadeOutSeconds;
    }
}
