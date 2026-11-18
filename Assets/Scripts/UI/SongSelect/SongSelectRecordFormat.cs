using System;
using System.Globalization;

/// <summary>
/// 최고 기록 카드·목록 랭크 칸에 쓸 값 묶음 (SongSelectRecordFormat.Build의 결과).
/// 랭크·FC는 이미지라 글자 대신 값으로 넘긴다 (Img_Rank · Img_FullCombo).
/// </summary>
public readonly struct RecordTexts
{
    public readonly bool HasRecord;
    public readonly string Score;
    public readonly string Rate;
    public readonly string Combo;

    /// <summary>최고 랭크 (HasRecord일 때만 뜻이 있다)</summary>
    public readonly PlayRank Rank;

    /// <summary>FULL COMBO 기록이 있으면 true (AP 기록이면 항상 true)</summary>
    public readonly bool IsFullCombo;

    public RecordTexts(bool hasRecord, string score, string rate, string combo, PlayRank rank, bool isFullCombo)
    {
        HasRecord = hasRecord;
        Score = score;
        Rate = rate;
        Combo = combo;
        Rank = rank;
        IsFullCombo = isFullCombo;
    }
}

/// <summary>
/// 곡 선택 최고 기록 표시 형식 (기획서 3-3 ⑥, 순수 함수 · EditMode 테스트용).
/// - 점수·콤보는 쉼표 없이 (1000000, 1104)
/// - RATE는 최고 정확도(bestAccuracy, 점수와 상관없이 가장 높았던 값)를 소수 둘째 자리 %로. 반올림하면 99.996%가 100.00%로 보이므로 버린다
/// - 랭크 AP · S~F (bestRank, 모르는 이름은 F)와 FC 여부는 값으로 넘기고 이미지로 그린다 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」).
///   FC = FULL COMBO 기록이 있거나 ALL PERFECT 기록이 있으면 (AP면 FC도 보인다)
/// - 기록이 없으면 점수·RATE·COMBO 칸 "—" (GameConstants.SongSelect.EmptyValue), 랭크는 빈 자리, FC 없음
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

    /// <summary>
    /// 기록 하나를 카드 값으로 바꾼다. null이면 점수·RATE·COMBO "—", 랭크 없음(빈 자리), FC 없음.
    /// </summary>
    public static RecordTexts Build(ChartRecordData record)
    {
        string empty = GameConstants.SongSelect.EmptyValue;
        if (record == null) return new RecordTexts(false, empty, empty, empty, PlayRank.F, false);

        return new RecordTexts(
            true,
            FormatScore(record.bestScore),
            FormatRate(record.bestAccuracy),
            FormatCombo(record.maxCombo),
            PlayerDataRules.ParseRank(record.bestRank),
            record.isFullCombo || record.isAllPerfect);
    }
}
