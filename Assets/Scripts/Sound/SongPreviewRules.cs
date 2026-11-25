using System;
using System.Collections.Generic;

/// <summary>
/// 곡 미리 듣기 구간 · 음량 규칙 (11/25). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다. 값은 GameConstants.SongPreview.
/// - 시작: 0(또는 음수, 곡 길이 이상)이면 곡 길이 × DefaultStartRatio
/// - 길이: 0(또는 음수)이면 DefaultLengthSeconds. 곡 끝을 넘으면 끝까지로 자른다
/// - 음량 계수: 시작 FadeIn초 동안 0 → 1, 끝나기 FadeOutAtEnd초 전부터 1 → 0 (둘 중 작은 값)
/// </summary>
public static class SongPreviewRules
{
    /// <summary>미리 듣기 시작 (초). 곡 길이가 0이면 0</summary>
    public static double GetStart(int startMs, double songLength)
    {
        if (songLength <= 0) return 0;

        double start = startMs / 1000.0;
        return start <= 0 || start >= songLength ? songLength * GameConstants.SongPreview.DefaultStartRatio : start;
    }

    /// <summary>미리 듣기 길이 (초). 곡 끝에서 자른다. 곡 길이가 0이면 0</summary>
    public static double GetLength(int startMs, int lengthMs, double songLength)
    {
        if (songLength <= 0) return 0;

        double length = lengthMs > 0 ? lengthMs / 1000.0 : GameConstants.SongPreview.DefaultLengthSeconds;
        return Math.Min(length, songLength - GetStart(startMs, songLength));
    }

    /// <summary>구간 안 시각(초)의 음량 계수 (0 ~ 1)</summary>
    public static float GetVolume(double elapsed, double length)
    {
        double fadeIn = elapsed / GameConstants.SongPreview.FadeIn;
        double fadeOut = (length - elapsed) / GameConstants.SongPreview.FadeOutAtEnd;
        return (float)Math.Max(0, Math.Min(1, Math.Min(fadeIn, fadeOut)));
    }

    /// <summary>
    /// SongData 값 검사: 시작 &lt; 0, 길이 &lt; 0, 시작 + 길이 &gt; 곡 길이면 경고를 더한다 (0은 기본값으로 바꿔 본다). 음원이 없으면 길이 비교는 하지 않는다.
    /// </summary>
    public static void Validate(int startMs, int lengthMs, double songLength, List<string> warnings)
    {
        if (startMs < 0) warnings.Add($"미리 듣기 시작이 0보다 작습니다: {startMs}ms");
        if (lengthMs < 0) warnings.Add($"미리 듣기 길이가 0보다 작습니다: {lengthMs}ms");
        if (songLength <= 0) return;

        double start = startMs > 0 ? startMs / 1000.0 : songLength * GameConstants.SongPreview.DefaultStartRatio;
        double length = lengthMs > 0 ? lengthMs / 1000.0 : GameConstants.SongPreview.DefaultLengthSeconds;
        if (start + length > songLength)
            warnings.Add($"미리 듣기 구간이 곡 길이를 넘습니다: 시작 {start:0.###}초 + 길이 {length:0.###}초 > 곡 {songLength:0.###}초");
    }
}
