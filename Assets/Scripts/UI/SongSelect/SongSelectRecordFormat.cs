using System;
using System.Globalization;

/// <summary>FC/AP 도장 종류 (ALL PERFECT면 AP만)</summary>
public enum RecordStamp
{
    None,
    FullCombo,
    AllPerfect
}

/// <summary>
/// 최고 기록 카드·목록 랭크 칸에 쓸 글자 묶음 (SongSelectRecordFormat.Build의 결과).
/// </summary>
public readonly struct RecordTexts
{
    public readonly bool HasRecord;
    public readonly string Score;
    public readonly string Rate;
    public readonly string Combo;
    public readonly string Rank;

    /// <summary>랭크가 S면 true (하늘색으로 칠한다)</summary>
    public readonly bool IsRankS;

    public readonly RecordStamp Stamp;

    public RecordTexts(bool hasRecord, string score, string rate, string combo, string rank, bool isRankS, RecordStamp stamp)
    {
        HasRecord = hasRecord;
        Score = score;
        Rate = rate;
        Combo = combo;
        Rank = rank;
        IsRankS = isRankS;
        Stamp = stamp;
    }
}

/// <summary>
/// 곡 선택 최고 기록 표시 형식 (기획서 3-3 ⑥, 순수 함수 · EditMode 테스트용).
/// - 점수·콤보는 쉼표 없이 (1000000, 1104)
/// - RATE는 최고 정확도(bestAccuracy, 점수와 상관없이 가장 높았던 값)를 소수 둘째 자리 %로. 반올림하면 99.996%가 100.00%로 보이므로 버린다
/// - 랭크 S~F, S는 하늘색. 도장: FULL COMBO → FC, ALL PERFECT → AP (AP면 AP만)
/// - 기록이 없으면 각 칸 "—" (GameConstants.SongSelect.EmptyValue), 도장 없음
/// </summary>
public static class SongSelectRecordFormat
{
    /// <summary>점수: 쉼표 없는 정수</summary>
    public static string FormatScore(int score)
    {
        return score.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>콤보: 쉼표 없는 정수</summary>
    public static string FormatCombo(int combo)
    {
        return combo.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>RATE: 소수 둘째 자리까지 버림 + "%" (예: 95.3449 → "95.34%")</summary>
    public static string FormatRate(double accuracy)
    {
        if (double.IsNaN(accuracy) || double.IsInfinity(accuracy)) accuracy = 0;
        accuracy = Math.Max(0, Math.Min(100, accuracy));
        // 95.34 × 100 = 9533.999…처럼 부동소수 오차로 한 단계 내려가지 않게 아주 작은 값을 더한 뒤 버린다
        double truncated = Math.Floor(accuracy * 100 + 1e-6) / 100;
        return string.Format(CultureInfo.InvariantCulture, GameConstants.SongSelect.RateFormat, truncated);
    }

    /// <summary>도장 규칙: AP면 AP만, 아니면 FC, 둘 다 아니면 없음</summary>
    public static RecordStamp GetStamp(bool isFullCombo, bool isAllPerfect)
    {
        if (isAllPerfect) return RecordStamp.AllPerfect;
        return isFullCombo ? RecordStamp.FullCombo : RecordStamp.None;
    }

    /// <summary>도장 글자 (없으면 빈 문자열)</summary>
    public static string GetStampText(RecordStamp stamp)
    {
        switch (stamp)
        {
            case RecordStamp.AllPerfect: return GameConstants.SongSelect.StampAllPerfect;
            case RecordStamp.FullCombo: return GameConstants.SongSelect.StampFullCombo;
            default: return string.Empty;
        }
    }

    /// <summary>
    /// 기록 하나를 카드 글자로 바꾼다. null이면 모든 칸 "—", 도장 없음.
    /// </summary>
    public static RecordTexts Build(ChartRecordData record)
    {
        string empty = GameConstants.SongSelect.EmptyValue;
        if (record == null) return new RecordTexts(false, empty, empty, empty, empty, false, RecordStamp.None);

        PlayRank rank = PlayerDataRules.ParseRank(record.bestRank);
        return new RecordTexts(
            true,
            FormatScore(record.bestScore),
            FormatRate(record.bestAccuracy),
            FormatCombo(record.maxCombo),
            rank.ToString(),
            rank == PlayRank.S,
            GetStamp(record.isFullCombo, record.isAllPerfect));
    }
}
