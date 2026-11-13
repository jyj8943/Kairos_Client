using System;
using UnityEngine;

/// <summary>모든 노트 판정 뒤 띄울 연출</summary>
public enum ClearKind
{
    /// <summary>연출 없음 (MISS 있음, 자동 플레이)</summary>
    None,

    /// <summary>FULL COMBO (MISS 0개)</summary>
    FullCombo,

    /// <summary>ALL PERFECT (전부 PERFECT)</summary>
    AllPerfect
}

/// <summary>
/// FULL COMBO / ALL PERFECT 연출을 띄울지 정한다 (순수 함수, EditMode 테스트용).
/// </summary>
public static class ClearEffectRules
{
    /// <summary>
    /// ALL PERFECT면 AllPerfect, 아니고 FULL COMBO면 FullCombo, 둘 다 아니면 None.
    /// 자동 플레이는 항상 None (에디터 테스트 플레이는 띄운다).
    /// </summary>
    public static ClearKind Select(bool isAutoPlay, bool isAllPerfect, bool isFullCombo)
    {
        if (isAutoPlay) return ClearKind.None;
        if (isAllPerfect) return ClearKind.AllPerfect;
        if (isFullCombo) return ClearKind.FullCombo;
        return ClearKind.None;
    }
}

/// <summary>한 순간의 FULL COMBO / ALL PERFECT 연출 모습 (ClearEffectAnimation.Evaluate 결과)</summary>
public struct ClearEffectFrame
{
    /// <summary>연출 전체가 보이는지 (첫 등장 전·끝난 뒤 false)</summary>
    public bool Visible;

    /// <summary>ClearEffect 전체 알파 (CanvasGroup, 마지막에 1 → 0)</summary>
    public float GroupAlpha;

    /// <summary>검은 띠: 가로 크기 배율(세로 1 고정)과 알파</summary>
    public float BandScaleX;
    public float BandAlpha;

    /// <summary>메인 글자: 크기 배율(등장 1.4 → 1, 이후 맥동 포함)과 알파</summary>
    public float TitleScale;
    public float TitleAlpha;

    /// <summary>화면 전체 흰색 번쩍임 알파</summary>
    public float FlashAlpha;

    /// <summary>잔상: 크기 배율과 알파 (구간 밖이면 알파 0)</summary>
    public float AfterimageScale;
    public float AfterimageAlpha;

    /// <summary>빛 훑기 진행 (0 = 글자 왼쪽 밖, 1 = 오른쪽 밖). 구간 밖이면 보이지 않음</summary>
    public float ShineProgress;
    public bool ShineVisible;
}

/// <summary>
/// FULL COMBO / ALL PERFECT 연출 계산 (모든 노트 판정 뒤 경과 시간 → 레이어별 크기·알파·위치). MonoBehaviour가 아니어서 EditMode로 테스트한다.
/// 기본값 (초, 모든 노트 판정 순간부터):
/// - 0.30부터 0.20: 띠 가로 0 → 1, 알파 0 → 0.55 (ease-out quad)
/// - 0.40부터 0.25: 글자 크기 1.4 → 1.0, 알파 0 → 1 (ease-out back, 튕김 1.2) / 0.40부터 0.12: 화면 번쩍임 0.35 → 0 (선형)
/// - 0.65부터 0.30: 잔상 크기 1.0 → 1.25, 알파 0.8 → 0 (ease-out quad)
/// - 0.90부터 0.35: 빛 훑기 왼쪽 밖 → 오른쪽 밖 (ease-in-out quad)
/// - 0.65 ~ 2.40: 글자 크기 1.0 ↔ 1.02 맥동 (주기 1.2초, 사인). 2.40 뒤로는 그때 크기를 그대로 둔다 (튀지 않게)
/// - 2.40부터 0.40: 전체 알파 1 → 0 (선형), 2.80 이후 숨김
/// Inspector(ClearEffect)에서 [Serializable]로 바로 조정한다. 연출 총 길이(TotalSeconds)는 결과로 가는 시간(ResultDelaySeconds)보다 길면 안 된다.
/// </summary>
[Serializable]
public class ClearEffectAnimation
{
    /// <summary>끝 시각 비교 여유 (초)</summary>
    private const float EndEpsilon = 1e-5f;

    [Header("띠 (Img_ClearBand)")]
    public float bandStart = 0.30f;
    public float bandDuration = 0.20f;
    [Tooltip("띠 최종 알파")]
    public float bandAlpha = 0.55f;

    [Header("글자 (Img_ClearTitle)")]
    public float titleStart = 0.40f;
    public float titleDuration = 0.25f;
    [Tooltip("등장 시작 크기")]
    public float titleStartScale = 1.4f;
    [Tooltip("ease-out back 튕김 정도 (클수록 1.0 아래로 더 내려갔다 돌아옴)")]
    public float titleOvershoot = 1.2f;

    [Header("화면 번쩍임 (Img_ScreenFlash)")]
    public float flashStart = 0.40f;
    public float flashDuration = 0.12f;
    public float flashAlpha = 0.35f;

    [Header("잔상 (Img_ClearAfterimage)")]
    public float afterimageStart = 0.65f;
    public float afterimageDuration = 0.30f;
    public float afterimageEndScale = 1.25f;
    public float afterimageStartAlpha = 0.8f;

    [Header("빛 훑기 (Img_ClearShine)")]
    public float shineStart = 0.90f;
    public float shineDuration = 0.35f;

    [Header("글자 맥동")]
    public bool pulseEnabled = true;
    public float pulseStart = 0.65f;
    public float pulseEnd = 2.40f;
    [Tooltip("최대 크기 (1.0 ↔ 이 값)")]
    public float pulseScale = 1.02f;
    public float pulsePeriod = 1.2f;

    [Header("퇴장 (ClearEffect 전체)")]
    public float fadeOutStart = 2.40f;
    public float fadeOutDuration = 0.40f;

    /// <summary>연출이 끝나 숨기는 시각 (초) = 전체 퇴장 끝</summary>
    public float TotalSeconds => fadeOutStart + Mathf.Max(fadeOutDuration, 0f);

    /// <summary>처음 무엇이라도 보이기 시작하는 시각 (초)</summary>
    public float FirstVisibleSeconds => Mathf.Min(Mathf.Min(bandStart, titleStart), Mathf.Min(flashStart, afterimageStart));

    /// <summary>숨김 상태 (Init·연출 없음·끝난 뒤)</summary>
    public static readonly ClearEffectFrame Hidden = new ClearEffectFrame
    {
        Visible = false, GroupAlpha = 0f, BandScaleX = 0f, BandAlpha = 0f, TitleScale = 1f, TitleAlpha = 0f,
        FlashAlpha = 0f, AfterimageScale = 1f, AfterimageAlpha = 0f, ShineProgress = 0f, ShineVisible = false
    };

    /// <summary>모든 노트 판정 순간부터 t초 뒤의 모습</summary>
    public ClearEffectFrame Evaluate(float t)
    {
        if (t < FirstVisibleSeconds || t >= TotalSeconds - EndEpsilon) return Hidden;

        ClearEffectFrame f = Hidden;
        f.Visible = true;

        // 전체 퇴장
        f.GroupAlpha = t < fadeOutStart ? 1f : 1f - Progress(t, fadeOutStart, fadeOutDuration);

        // 띠
        float band = EaseOutQuad(Progress(t, bandStart, bandDuration));
        f.BandScaleX = band;
        f.BandAlpha = bandAlpha * band;

        // 글자 등장 (구간 전에는 알파 0, 크기는 시작 크기)
        float titleX = Progress(t, titleStart, titleDuration);
        float titleEase = EaseOutBack(titleX, titleOvershoot);
        f.TitleScale = Mathf.LerpUnclamped(titleStartScale, 1f, titleEase);
        f.TitleAlpha = t < titleStart ? 0f : Mathf.Clamp01(titleEase);
        if (pulseEnabled) f.TitleScale *= PulseScale(t);

        // 화면 번쩍임 (구간 안에서만)
        if (t >= flashStart && t < flashStart + flashDuration)
            f.FlashAlpha = flashAlpha * (1f - Progress(t, flashStart, flashDuration));

        // 잔상 (구간 안에서만)
        if (t >= afterimageStart && t < afterimageStart + afterimageDuration)
        {
            float x = EaseOutQuad(Progress(t, afterimageStart, afterimageDuration));
            f.AfterimageScale = Mathf.Lerp(1f, afterimageEndScale, x);
            f.AfterimageAlpha = afterimageStartAlpha * (1f - x);
        }

        // 빛 훑기 (구간 안에서만)
        if (t >= shineStart && t < shineStart + shineDuration)
        {
            f.ShineVisible = true;
            f.ShineProgress = EaseInOutQuad(Progress(t, shineStart, shineDuration));
        }

        return f;
    }

    /// <summary>맥동 크기 배율: pulseStart에서 1.0으로 시작해 사인으로 1.0 ↔ pulseScale. pulseEnd 뒤로는 그때 값을 유지</summary>
    private float PulseScale(float t)
    {
        if (t < pulseStart || pulsePeriod <= 0f) return 1f;
        float u = Mathf.Min(t, pulseEnd) - pulseStart;
        float wave = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * u / pulsePeriod)); // 0 → 1 → 0
        return 1f + (pulseScale - 1f) * wave;
    }

    /// <summary>구간 진행 (0~1). 길이가 0 이하면 시작 뒤 바로 1</summary>
    private static float Progress(float t, float start, float duration)
    {
        if (t <= start) return 0f;
        if (duration <= 0f) return 1f;
        return Mathf.Clamp01((t - start) / duration);
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseInOutQuad(float x) => x < 0.5f ? 2f * x * x : 1f - 2f * (1f - x) * (1f - x);

    /// <summary>ease-out back: 끝에서 1을 살짝 넘었다 돌아온다 (s = 튕김 정도)</summary>
    private static float EaseOutBack(float x, float s)
    {
        float c3 = s + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + s * y * y;
    }
}
