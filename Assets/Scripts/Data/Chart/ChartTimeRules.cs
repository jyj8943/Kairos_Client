using System;
using System.Globalization;

/// <summary>
/// 채보 마지막 수정 시각 (11/25). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 저장 글자: ISO 8601 UTC ("2026-11-25T08:30:00Z", ChartData.updatedAt). 저장 창구(ChartRepository.TrySave)가 쓴다.
/// - 값이 없거나 읽을 수 없으면 채보 파일의 수정 시각(UTC)으로 대신한다 (예전 채보).
/// - 화면 표시는 지역 시간 "MM/dd HH:mm" (GameConstants.SongSelect.ModifiedTimeFormat).
/// </summary>
public static class ChartTimeRules
{
    private const string UtcFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>UTC 시각 → 저장 글자</summary>
    public static string ToText(DateTime utc)
    {
        return utc.ToString(UtcFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>저장 글자 → UTC 시각. 비었거나 형식이 틀리면 false</summary>
    public static bool TryParse(string text, out DateTime utc)
    {
        utc = default;
        return !string.IsNullOrEmpty(text)
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }

    /// <summary>updatedAt 값이 있으면 그 시각, 없으면 파일 시각 (파일도 없으면 null)</summary>
    public static DateTime? Resolve(string updatedAt, DateTime? fileTimeUtc)
    {
        return TryParse(updatedAt, out DateTime utc) ? utc : fileTimeUtc;
    }

    /// <summary>화면 표시 글자 (지역 시간). 시각이 없으면 "―"</summary>
    public static string ToDisplay(DateTime? utc)
    {
        return utc.HasValue
            ? utc.Value.ToLocalTime().ToString(GameConstants.SongSelect.ModifiedTimeFormat, CultureInfo.InvariantCulture)
            : GameConstants.SongSelect.EmptyValue;
    }
}
