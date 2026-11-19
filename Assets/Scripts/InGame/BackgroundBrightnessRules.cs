using UnityEngine;

/// <summary>
/// 인게임 배경 밝기 규칙 (순수 함수, 기획서 3-4 「배경 · BGA (11/19)」 · 2-4 「마지막 선택」).
/// - 단계는 GameConstants.Play.BackgroundBrightnessSteps (0 · 25 · 50 · 75 · 100%). PgUp·PgDn 한 번에 한 단계, 끝값에서 더 누르면 그대로.
/// - 배경 위 검은 덮개 알파 = 1 − 밝기 (100% → 0, 0% → 1 = 검은 화면).
/// - 배속·밝기 카드의 단계 막대: 25%마다 한 칸 (0% → 0칸 … 100% → 4칸).
/// - 저장값 정리: 단계에 없는 값은 가장 가까운 단계 (같은 거리면 어두운 쪽).
/// </summary>
public static class BackgroundBrightnessRules
{
    /// <summary>밝기 기본값 (%)</summary>
    public static int Default => GameConstants.Play.DefaultBackgroundBrightness;

    /// <summary>
    /// 밝기를 delta 단계만큼 옮긴다 (+ 밝게, − 어둡게). 끝값에서는 멈춘다. current가 단계에 없으면 가까운 단계에서 시작한다.
    /// </summary>
    public static int Step(int current, int delta)
    {
        int[] steps = GameConstants.Play.BackgroundBrightnessSteps;
        int index = Mathf.Clamp(IndexOfNearest(current) + delta, 0, steps.Length - 1);
        return steps[index];
    }

    /// <summary>단계에 없는 값을 가장 가까운 단계로 (같은 거리면 어두운 쪽)</summary>
    public static int Sanitize(int value)
    {
        return GameConstants.Play.BackgroundBrightnessSteps[IndexOfNearest(value)];
    }

    /// <summary>검은 덮개 알파 = 1 − 밝기 (0 ~ 1)</summary>
    public static float DimAlpha(int brightness)
    {
        return 1f - Mathf.Clamp(brightness, 0, 100) / 100f;
    }

    /// <summary>단계 막대에서 켜는 칸 수 (25%마다 한 칸, 0 ~ BrightnessBarCount)</summary>
    public static int FilledBars(int brightness)
    {
        int bars = GameConstants.Play.BrightnessBarCount;
        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp(brightness, 0, 100) * bars / 100f + 0.0001f), 0, bars);
    }

    /// <summary>value와 가장 가까운 단계의 번호</summary>
    private static int IndexOfNearest(int value)
    {
        int[] steps = GameConstants.Play.BackgroundBrightnessSteps;
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < steps.Length; i++)
        {
            int distance = System.Math.Abs(steps[i] - value);
            if (distance >= bestDistance) continue;
            best = i;
            bestDistance = distance;
        }
        return best;
    }
}
