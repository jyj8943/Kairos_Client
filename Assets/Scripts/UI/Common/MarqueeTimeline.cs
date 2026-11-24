using UnityEngine;

/// <summary>
/// 긴 제목이 흐르는 시간표 (UIMarqueeText, 순수 함수). 한 바퀴 = 처음 멈춤 → 왼쪽 이동 → 끝 멈춤 → 사라짐 → 처음 위치에서 나타남.
/// 시간 값은 GameConstants.Marquee 한 곳에 있다.
/// </summary>
public static class MarqueeTimeline
{
    /// <summary>왼쪽으로 흐르는 시간 (초)</summary>
    public static float MoveSeconds(float overflow) => Mathf.Max(0f, overflow) / GameConstants.Marquee.Speed;

    /// <summary>한 바퀴 시간 (초)</summary>
    public static float CycleSeconds(float overflow)
    {
        return GameConstants.Marquee.StartDelay + MoveSeconds(overflow) + GameConstants.Marquee.EndDelay + GameConstants.Marquee.ReturnFade * 2f;
    }

    /// <summary>
    /// 한 바퀴 안의 시각 time(초)에서 왼쪽으로 밀린 거리와 알파. time은 0 ~ CycleSeconds.
    /// </summary>
    /// <param name="overflow">글자 폭 − 칸 폭 (px). 0 이하면 움직이지 않는다</param>
    public static void Evaluate(float time, float overflow, out float offset, out float alpha)
    {
        offset = 0f;
        alpha = 1f;
        if (overflow <= 0f) return;

        float t = time - GameConstants.Marquee.StartDelay;
        if (t <= 0f) return;

        float move = MoveSeconds(overflow);
        if (t < move)
        {
            offset = t * GameConstants.Marquee.Speed;
            return;
        }

        t -= move + GameConstants.Marquee.EndDelay;
        if (t <= 0f)
        {
            offset = overflow;
            return;
        }

        float fade = GameConstants.Marquee.ReturnFade;
        if (t < fade)
        {
            offset = overflow;
            alpha = 1f - t / fade;
            return;
        }

        alpha = Mathf.Clamp01((t - fade) / fade);
    }
}
