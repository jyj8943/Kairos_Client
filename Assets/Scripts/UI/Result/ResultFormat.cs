using System.Globalization;

/// <summary>
/// 결과 화면 표시 규칙 (기획서 3-5, 순수 함수 · EditMode 테스트용).
/// - 점수 차이: 이번 점수 − 이전 최고 점수. "+12345" / "-2400" / "±0" (쉼표 없음). 이전 기록이 없거나 테스트 플레이면 표시하지 않음(null).
///   게임 글꼴에 U+2212(−)가 없어 빼기는 "-"를 쓴다.
/// - FAST/SLOW 막대: FAST 비율 = FAST ÷ (FAST + SLOW). 둘 다 0이면 빈 막대.
/// - 점수·콤보·RATE 숫자는 곡 선택과 같은 SongSelectRecordFormat 함수를 쓴다.
/// </summary>
public static class ResultFormat
{
    /// <summary>점수 차이 글자. 표시하지 않으면 null</summary>
    public static string FormatScoreDiff(bool hadPreviousRecord, bool isTestPlay, int score, int previousBestScore)
    {
        if (!hadPreviousRecord || isTestPlay) return null;

        long diff = (long)score - previousBestScore;
        if (diff == 0) return GameConstants.Result.DiffZero;
        string number = System.Math.Abs(diff).ToString(CultureInfo.InvariantCulture);
        return (diff > 0 ? GameConstants.Result.DiffPlus : GameConstants.Result.DiffMinus) + number;
    }

    /// <summary>
    /// FAST/SLOW 막대에서 FAST가 차지하는 비율 (0 ~ 1). 둘 다 0이면 false (빈 막대).
    /// </summary>
    public static bool TryGetFastRatio(int fastCount, int slowCount, out float fastRatio)
    {
        fastRatio = 0f;
        int total = System.Math.Max(0, fastCount) + System.Math.Max(0, slowCount);
        if (total == 0) return false;
        fastRatio = System.Math.Max(0, fastCount) / (float)total;
        return true;
    }

    /// <summary>NEW RECORD를 띄울지 (테스트 플레이는 기록을 저장하지 않으므로 띄우지 않는다)</summary>
    public static bool ShowNewRecord(bool isNewRecord, bool isTestPlay)
    {
        return isNewRecord && !isTestPlay;
    }

    /// <summary>판정 개수: 앞에 0을 채우지 않는다</summary>
    public static string FormatCount(int count)
    {
        return System.Math.Max(0, count).ToString(CultureInfo.InvariantCulture);
    }
}
