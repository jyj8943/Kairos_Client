using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>BPM 표기 방식 (기획서 2-2 「BPM 표기」)</summary>
public enum SongBpmDisplay
{
    /// <summary>대표: 고른 채보의 BPM 구간 값 중 프롬프트 BPM과 가장 가까운 값 하나</summary>
    Main,

    /// <summary>범위: 고른 채보의 BPM 구간 최소 ~ 최대</summary>
    Range
}

/// <summary>
/// BPM 표기 (기획서 2-2, 11/25). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 구간이 1개면 방식과 상관없이 그 값 하나
/// - Range: "최소 ~ 최대" (~는 연한 색 UITheme.textMuted 리치 텍스트). 표기한 두 숫자가 같으면 하나만
/// - Main: 구간 값 중 promptBpm과 가장 가까운 값 하나 (같은 거리면 앞 구간)
/// - 숫자는 SongData.FormatBpm (끝자리 0 제거, 소수 최대 둘째 자리). 구간이 없으면 "―"
/// - 나만의 채보는 곡의 방식을 따르고 값은 그 채보의 구간으로 계산한다. 채보가 없으면 곡의 구간을 쓴다
/// - 표시 전용이다. 타이밍 계산에는 쓰지 않는다
/// </summary>
public static class BpmDisplayRules
{
    /// <summary>채보(없으면 곡)의 구간을 곡의 방식대로 표기한다</summary>
    public static string Format(SongData song, ChartData chart)
    {
        if (song == null) return GameConstants.SongSelect.EmptyValue;

        IReadOnlyList<TimingPointData> points = chart != null && chart.timingPoints != null && chart.timingPoints.Count > 0
            ? chart.timingPoints
            : song.TimingPoints;
        return Format(points, song.BpmDisplay, song.PromptBpm);
    }

    /// <summary>구간 목록 · 방식 · 프롬프트 BPM으로 표기한다</summary>
    public static string Format(IReadOnlyList<TimingPointData> points, SongBpmDisplay display, float promptBpm)
    {
        if (!TryGetRange(points, out float min, out float max)) return GameConstants.SongSelect.EmptyValue;

        if (display == SongBpmDisplay.Main) return SongData.FormatBpm(GetMain(points, promptBpm));

        string minText = SongData.FormatBpm(min);
        string maxText = SongData.FormatBpm(max);
        return minText == maxText
            ? minText
            : string.Format(GameConstants.SongSelect.BpmRangeFormat, minText, maxText, ColorUtility.ToHtmlStringRGB(UITheme.Current.textMuted));
    }

    /// <summary>
    /// 구간 값 중 promptBpm과 가장 가까운 값 (같은 거리면 앞 구간). promptBpm이 0 이하면 첫 구간 값, 쓸 구간이 없으면 0.
    /// 거리는 decimal로 비교한다 (150.2 · 150.8처럼 같은 거리가 float 오차로 갈리지 않게).
    /// </summary>
    public static float GetMain(IReadOnlyList<TimingPointData> points, float promptBpm)
    {
        float best = 0f;
        decimal bestDistance = 0m;
        bool found = false;
        if (points == null) return best;

        for (int i = 0; i < points.Count; i++)
        {
            if (!IsUsable(points[i])) continue;

            decimal distance = promptBpm > 0f ? Math.Abs((decimal)points[i].bpm - (decimal)promptBpm) : 0m;
            if (found && distance >= bestDistance) continue;
            best = points[i].bpm;
            bestDistance = distance;
            found = true;
        }
        return best;
    }

    /// <summary>구간의 최소 · 최대. 쓸 구간이 없으면 false</summary>
    private static bool TryGetRange(IReadOnlyList<TimingPointData> points, out float min, out float max)
    {
        min = float.MaxValue;
        max = 0f;
        if (points == null) return false;

        for (int i = 0; i < points.Count; i++)
        {
            if (!IsUsable(points[i])) continue;
            min = Mathf.Min(min, points[i].bpm);
            max = Mathf.Max(max, points[i].bpm);
        }
        return max > 0f;
    }

    private static bool IsUsable(TimingPointData point)
    {
        return point != null && point.bpm > 0f;
    }
}
