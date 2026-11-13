using UnityEngine;

/// <summary>
/// 인게임 콤보 애니메이션 계산 (시간 → 위치·알파). MonoBehaviour가 아니어서 EditMode로 테스트한다.
/// - 오름: 콤보가 오르면 숫자가 riseOffset만큼 아래에서 투명하게 시작해 fadeInSeconds 만에 불투명해지고,
///   riseSeconds 동안 ease-out quad로 제자리까지 올라온다. 연달아 오르면 매번 처음부터 다시 시작한다.
/// - 쉬면 숨기기: 마지막으로 오르고 idleHideSeconds 동안 안 오르면 그룹 전체가 idleSinkOffset만큼 내려가며
///   idleFadeOutSeconds에 걸쳐 사라진다. 다시 오르면 바로 보인다.
/// - 콤보 0이면 바로 숨긴다 (애니메이션 없음).
/// - 시간은 Tick으로만 흐른다 (부르지 않으면 상태가 바뀌지 않는다 = 일시정지).
/// </summary>
public class ComboMotion
{
    /// <summary>끝 시각 비교 여유 (초)</summary>
    private const float EndEpsilon = 1e-5f;

    /// <summary>오름 시작 위치: 제자리보다 이만큼 아래 (px)</summary>
    public float RiseOffset = 30f;

    /// <summary>숫자가 투명 → 불투명이 되는 시간 (초)</summary>
    public float FadeInSeconds = 0.05f;

    /// <summary>숫자가 제자리까지 올라오는 시간 (초, ease-out quad)</summary>
    public float RiseSeconds = 0.13f;

    /// <summary>한동안 안 오르면 콤보를 숨길지</summary>
    public bool HideWhenIdle = true;

    /// <summary>마지막으로 오르고 이만큼 안 오르면 숨기기 시작 (초)</summary>
    public float IdleHideSeconds = 0.67f;

    /// <summary>숨길 때 사라지는 시간 (초)</summary>
    public float IdleFadeOutSeconds = 0.07f;

    /// <summary>숨길 때 그룹이 내려가는 거리 (px)</summary>
    public float IdleSinkOffset = 8f;

    /// <summary>지금 콤보</summary>
    public int Combo { get; private set; }

    /// <summary>마지막으로 오르고 지난 시간 (초). 오름 애니메이션과 쉬는 시간을 함께 잰다</summary>
    public float Elapsed { get; private set; }

    /// <summary>숫자의 제자리 아래 거리 (px, 0이면 제자리)</summary>
    public float NumberOffset
    {
        get
        {
            if (Combo <= 0 || RiseSeconds <= 0f) return 0f;
            float t = Mathf.Clamp01(Elapsed / RiseSeconds);
            float remain = 1f - t;
            // ease-out quad: 진행 = 1 - (1 - t)², 남은 거리 = (1 - t)²
            return RiseOffset * remain * remain;
        }
    }

    /// <summary>숫자 알파 배율 (0~1, 텍스트 기본 알파에 곱한다)</summary>
    public float NumberAlpha
    {
        get
        {
            if (Combo <= 0) return 0f;
            if (FadeInSeconds <= 0f) return 1f;
            return Mathf.Clamp01(Elapsed / FadeInSeconds);
        }
    }

    /// <summary>쉬면 숨기기 진행 (0 = 보임, 1 = 다 사라짐)</summary>
    private float HideProgress
    {
        get
        {
            if (!HideWhenIdle || Elapsed < IdleHideSeconds) return 0f;
            // 끝 시각에는 정확히 1 (float 덧셈·나눗셈 오차로 알파가 0 바로 위에 남지 않게 조금 여유를 둔다)
            if (IdleFadeOutSeconds <= 0f || Elapsed >= IdleHideSeconds + IdleFadeOutSeconds - EndEpsilon) return 1f;
            return Mathf.Clamp01((Elapsed - IdleHideSeconds) / IdleFadeOutSeconds);
        }
    }

    /// <summary>그룹(COMBO 제목 + 숫자) 알파 (0~1)</summary>
    public float GroupAlpha => Combo <= 0 ? 0f : 1f - HideProgress;

    /// <summary>그룹의 제자리 아래 거리 (px, 쉬면 숨길 때만 0이 아니다)</summary>
    public float GroupOffset => Combo <= 0 ? 0f : IdleSinkOffset * HideProgress;

    /// <summary>그룹이 보이는지 (알파가 0보다 크다)</summary>
    public bool IsVisible => GroupAlpha > 0f;

    /// <summary>
    /// 콤보를 바꾼다. 커졌을 때만 오름 애니메이션·쉬는 시간을 처음부터 시작한다.
    /// 같은 값이면 아무것도 하지 않고, 0이면 바로 숨긴다.
    /// </summary>
    /// <returns>오름 애니메이션을 새로 시작했으면 true</returns>
    public bool SetCombo(int combo)
    {
        if (combo <= 0)
        {
            Combo = 0;
            Elapsed = 0f;
            return false;
        }

        if (combo == Combo) return false;

        bool rose = combo > Combo;
        Combo = combo;
        if (rose) Elapsed = 0f;
        return rose;
    }

    /// <summary>시간을 진행한다 (일시정지 중에는 부르지 않는다)</summary>
    public void Tick(float deltaTime)
    {
        if (Combo <= 0 || deltaTime <= 0f) return;

        // 다 숨긴 뒤로는 더 셀 필요가 없다 (float가 끝없이 커지지 않게)
        float end = HideWhenIdle ? IdleHideSeconds + IdleFadeOutSeconds : RiseSeconds;
        end = Mathf.Max(end, Mathf.Max(RiseSeconds, FadeInSeconds));
        if (Elapsed >= end) return;
        Elapsed = Mathf.Min(Elapsed + deltaTime, end);
    }

    /// <summary>처음 상태 (콤보 0, 숨김)</summary>
    public void Reset()
    {
        Combo = 0;
        Elapsed = 0f;
    }
}
