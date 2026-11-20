using UnityEngine;

/// <summary>
/// UI 연출 공용 easing 함수 (곡 로딩 · 일반 로딩 등). x는 0~1 진행도.
/// </summary>
public static class UIEasing
{
    public static float OutCubic(float x) { float y = 1f - x; return 1f - y * y * y; }

    public static float InCubic(float x) => x * x * x;

    /// <summary>easeInQuad: t² (암전 로딩 열기)</summary>
    public static float InQuad(float x) => x * x;

    /// <summary>easeOutQuad: 1 − (1 − t)² (암전 로딩 닫기)</summary>
    public static float OutQuad(float x) { float y = 1f - x; return 1f - y * y; }

    public static float InOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    /// <summary>easeOutBack (튀어 오름 1.70158)</summary>
    public static float OutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    /// <summary>구간 진행도 (start부터 duration 동안 0 → 1, 밖은 0 또는 1)</summary>
    public static float Progress(float t, float start, float duration)
    {
        if (t <= start) return 0f;
        if (duration <= 0f) return 1f;
        return Mathf.Clamp01((t - start) / duration);
    }
}
