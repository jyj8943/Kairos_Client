using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 결과 화면 규칙 EditMode 확인 (기획서 3-5 결과 11/17, 2-4 기록).
/// 1. 항목별 신기록 (PlayerRecordBook): 처음 → 셋 다 / 점수만 / 정확도만(점수는 낮음) / 콤보만 / 같은 값은 아님 / 테스트 플레이는 모두 false·저장 안 함
/// 2. 점수 차이 형식: +12345, -2400, ±0, 이전 기록 없음·테스트 플레이면 표시 안 함
/// 3. FAST/SLOW 막대 비율: 30:10 → 0.75, 0:0 → 빈 막대
/// </summary>
public class ResultRulesTests
{
    private static readonly DateTime Now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private static InGameEntryData Normal()
    {
        return new InGameEntryData { songId = "song", chartId = "chart", chartType = ChartType.Official, mode = InGameMode.Normal };
    }

    private static ResultData Result(int score, double accuracy, int maxCombo)
    {
        return new ResultData { score = score, rank = PlayRank.A, accuracy = accuracy, maxCombo = maxCombo };
    }

    // ───── 1. 항목별 신기록 ─────

    [Test]
    public void NewRecord_FirstPlay_AllThree()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());
        RecordUpdateResult first = book.Submit(Normal(), Result(900000, 90.0, 500), Now);
        Assert.IsTrue(first.isRecorded);
        Assert.IsFalse(first.hadPreviousRecord);
        Assert.IsTrue(first.isNewBestScore && first.isNewBestAccuracy && first.isNewMaxCombo, "처음 플레이는 셋 다 신기록");
        Assert.IsTrue(first.isNewRecord);
    }

    [Test]
    public void NewRecord_EachItemSeparately()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());
        book.Submit(Normal(), Result(900000, 90.0, 500), Now);

        RecordUpdateResult scoreOnly = book.Submit(Normal(), Result(910000, 89.0, 400), Now);
        AssertFlags(scoreOnly, true, false, false, "점수만 오름");
        Assert.AreEqual(900000, scoreOnly.previousBestScore, "차이 표시용 이전 최고 점수");

        RecordUpdateResult accuracyOnly = book.Submit(Normal(), Result(800000, 92.5, 400), Now);
        AssertFlags(accuracyOnly, false, true, false, "정확도만 오름 (점수는 낮음)");
        Assert.AreEqual(90.0, accuracyOnly.previousBestAccuracy, 1e-9);

        RecordUpdateResult comboOnly = book.Submit(Normal(), Result(700000, 80.0, 600), Now);
        AssertFlags(comboOnly, false, false, true, "최대 콤보만 오름");
        Assert.AreEqual(500, comboOnly.previousMaxCombo);

        RecordUpdateResult same = book.Submit(Normal(), Result(910000, 92.5, 600), Now);
        AssertFlags(same, false, false, false, "같은 값은 신기록이 아님");

        // 저장은 항목별로 따로 (2-4): 최고 점수 910000 · 최고 정확도 92.5 · 최대 콤보 600, 랭크는 최고 점수 판의 것
        ChartRecordData record = book.Find("chart");
        Assert.AreEqual(910000, record.bestScore);
        Assert.AreEqual(92.5, record.bestAccuracy, 1e-9);
        Assert.AreEqual(600, record.maxCombo);
    }

    [Test]
    public void NewRecord_TestPlay_AllFalse_NotSaved()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());
        InGameEntryData test = new InGameEntryData { songId = "song", chartId = "chart", chartType = ChartType.Custom, mode = InGameMode.TestPlay };
        ResultData result = Result(1000000, 100, 999);
        result.isTestPlay = true;

        RecordUpdateResult update = book.Submit(test, result, Now);
        Assert.IsFalse(update.isRecorded, "테스트 플레이는 저장하지 않음");
        AssertFlags(update, false, false, false, "테스트 플레이는 신기록 없음");
        Assert.IsNull(book.Find("chart"));

        ResultData auto = Result(1000000, 100, 999);
        auto.isAutoPlay = true;
        AssertFlags(book.Submit(Normal(), auto, Now), false, false, false, "자동 플레이도 신기록 없음");
    }

    private static void AssertFlags(RecordUpdateResult update, bool score, bool accuracy, bool combo, string message)
    {
        Assert.AreEqual(score, update.isNewBestScore, message + " (점수)");
        Assert.AreEqual(accuracy, update.isNewBestAccuracy, message + " (정확도)");
        Assert.AreEqual(combo, update.isNewMaxCombo, message + " (최대 콤보)");
    }

    // ───── 2. 점수 차이 ─────

    [Test]
    public void ScoreDiff_Format()
    {
        Assert.AreEqual("+12345", ResultFormat.FormatScoreDiff(true, false, 945947, 933602));
        Assert.AreEqual("-2400", ResultFormat.FormatScoreDiff(true, false, 931202, 933602), "쉼표 없음, 글꼴에 U+2212가 없어 -");
        Assert.AreEqual("±0", ResultFormat.FormatScoreDiff(true, false, 933602, 933602));
        Assert.AreEqual("+1000000", ResultFormat.FormatScoreDiff(true, false, 1000000, 0));
        Assert.IsNull(ResultFormat.FormatScoreDiff(false, false, 945936, 0), "이전 기록 없음 → 표시 안 함");
        Assert.IsNull(ResultFormat.FormatScoreDiff(true, true, 945936, 933602), "테스트 플레이 → 표시 안 함");
    }

    [Test]
    public void NewRecordBadge_HiddenOnTestPlay()
    {
        Assert.IsTrue(ResultFormat.ShowNewRecord(true, false));
        Assert.IsFalse(ResultFormat.ShowNewRecord(true, true));
        Assert.IsFalse(ResultFormat.ShowNewRecord(false, false));
        Assert.AreEqual("702", ResultFormat.FormatCount(702), "앞에 0을 채우지 않음");
        Assert.AreEqual("0", ResultFormat.FormatCount(0));
    }

    // ───── 3. FAST/SLOW 막대 ─────

    [Test]
    public void FastSlowRatio()
    {
        Assert.IsTrue(ResultFormat.TryGetFastRatio(30, 10, out float ratio));
        Assert.AreEqual(0.75f, ratio, 1e-6f);
        Assert.IsTrue(ResultFormat.TryGetFastRatio(0, 5, out ratio));
        Assert.AreEqual(0f, ratio, 1e-6f);
        Assert.IsTrue(ResultFormat.TryGetFastRatio(7, 0, out ratio));
        Assert.AreEqual(1f, ratio, 1e-6f);
        Assert.IsFalse(ResultFormat.TryGetFastRatio(0, 0, out ratio), "둘 다 0 → 빈 막대");
    }
}
