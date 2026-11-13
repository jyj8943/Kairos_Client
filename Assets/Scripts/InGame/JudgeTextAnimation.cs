using UnityEngine;

/// <summary>한 순간의 글자 모습 (JudgeTextAnimation.Evaluate 결과)</summary>
public struct JudgeTextFrame
{
    /// <summary>크기 (localScale 배율, 1 = 원래 크기)</summary>
    public float Scale;

    /// <summary>투명도 배율 (0~1, 글자 색 알파에 곱한다)</summary>
    public float Alpha;

    /// <summary>보이는지 (퇴장이 끝나면 false)</summary>
    public bool Visible;
}

/// <summary>
/// 인게임 판정 글자·FAST/SLOW 글자 애니메이션 계산 (경과 시간 → 크기·투명도·보임). MonoBehaviour가 아니어서 EditMode로 테스트한다.
/// DJMAX RESPECT V 기준 기본값 (판정 글자):
/// - 등장: 크기 StartScale → 1 (ScaleInSeconds, ease-out quad), 투명도 0 → 1 (FadeInSeconds, 선형)
/// - 유지: 등장 시작부터 HoldSeconds까지
/// - 퇴장: 크기 그대로 투명도 1 → 0 (FadeOutSeconds), 끝나면 숨김
/// FAST/SLOW 글자는 같은 클래스를 StartScale 1 · 등장 시간 0(바로 불투명) · 유지 0.47초 · 퇴장 0.15초로 쓴다.
/// - Restart: 진행 중인 애니메이션을 버리고 처음부터 (판정이 날 때마다).
/// - 시간은 Tick으로만 흐른다 (부르지 않으면 멈춤 = 일시정지·카운트다운, 콤보 ComboMotion과 같은 방식).
/// </summary>
public class JudgeTextAnimation
{
    /// <summary>끝 시각 비교 여유 (초). float 합이 끝에 조금 못 미쳐 알파가 0 바로 위에 남지 않게 (ComboMotion과 같은 값)</summary>
    private const float EndEpsilon = 1e-5f;

    /// <summary>등장 시작 크기</summary>
    public float StartScale = 0.7f;

    /// <summary>크기가 1이 되는 시간 (초, ease-out quad). 0이면 바로 1</summary>
    public float ScaleInSeconds = 0.08f;

    /// <summary>투명도가 1이 되는 시간 (초, 선형). 0이면 바로 불투명</summary>
    public float FadeInSeconds = 0.04f;

    /// <summary>등장 시작부터 퇴장 시작까지 (초)</summary>
    public float HoldSeconds = 0.82f;

    /// <summary>퇴장(투명도 1 → 0) 시간 (초)</summary>
    public float FadeOutSeconds = 0.13f;

    /// <summary>등장 시작부터 지난 시간 (초)</summary>
    public float Elapsed { get; private set; }

    /// <summary>재생 중인지 (Restart 뒤 퇴장이 끝나기 전)</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>등장부터 퇴장 끝까지 (초)</summary>
    public float TotalSeconds => Mathf.Max(HoldSeconds, 0f) + Mathf.Max(FadeOutSeconds, 0f);

    /// <summary>지금 모습</summary>
    public JudgeTextFrame Current => IsPlaying ? Evaluate(Elapsed) : Hidden;

    private static readonly JudgeTextFrame Hidden = new JudgeTextFrame { Scale = 1f, Alpha = 0f, Visible = false };

    /// <summary>처음부터 다시 재생한다 (진행 중이던 것은 버린다)</summary>
    public void Restart()
    {
        Elapsed = 0f;
        IsPlaying = true;
    }

    /// <summary>바로 숨긴다 (Init·재시작)</summary>
    public void Stop()
    {
        Elapsed = 0f;
        IsPlaying = false;
    }

    /// <summary>시간을 진행한다 (일시정지·카운트다운 중에는 부르지 않는다). 퇴장이 끝나면 재생을 멈춘다</summary>
    public void Tick(float deltaTime)
    {
        if (!IsPlaying || deltaTime <= 0f) return;

        float end = TotalSeconds;
        Elapsed = Mathf.Min(Elapsed + deltaTime, end);
        if (Elapsed >= end - EndEpsilon) IsPlaying = false;
    }

    /// <summary>
    /// 등장 시작부터 t초 뒤의 모습. 퇴장이 끝났으면(t ≥ HoldSeconds + FadeOutSeconds) 숨김.
    /// </summary>
    public JudgeTextFrame Evaluate(float t)
    {
        if (t < 0f) t = 0f;
        if (t >= TotalSeconds - EndEpsilon) return Hidden;

        float scale = 1f;
        if (ScaleInSeconds > 0f && t < ScaleInSeconds)
        {
            float x = t / ScaleInSeconds;
            float eased = 1f - (1f - x) * (1f - x); // ease-out quad
            scale = Mathf.LerpUnclamped(StartScale, 1f, eased);
        }

        float alpha = FadeInSeconds > 0f ? Mathf.Clamp01(t / FadeInSeconds) : 1f;
        if (t > HoldSeconds && FadeOutSeconds > 0f)
            alpha = Mathf.Min(alpha, 1f - Mathf.Clamp01((t - HoldSeconds) / FadeOutSeconds));

        return new JudgeTextFrame { Scale = scale, Alpha = alpha, Visible = true };
    }
}
