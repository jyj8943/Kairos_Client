using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 곡 선택 화면 개편 규칙 EditMode 확인 (기획서 3-3 곡 선택 화면 개편 · 나만의 채보 탭 11/17, 2-4 마지막 선택).
/// 1. 최고 기록 카드 형식: 점수·콤보 쉼표 없음, RATE 소수 둘째 자리(버림), 기록 없음 —, FC/AP 도장 (AP면 AP만), S 하늘색
/// 2. 마지막 선택: 저장 → 불러오기, 없는 채보 ID면 공식 첫 곡, 예전 JSON(필드 없음) 읽기, 곡·채보 한쪽만 있으면 비움
/// 3. 나만의 채보 목록 이동: ↑↓가 곡 경계를 넘음, ←→가 이전·다음 곡 첫 채보, 양 끝에서 멈춤, 채보 0개
/// </summary>
public class SongSelectRulesTests
{
    private const string Empty = GameConstants.SongSelect.EmptyValue;

    // ───── 1. 기록 카드 형식 ─────

    [Test]
    public void RecordFormat_ScoreAndCombo_NoComma()
    {
        Assert.AreEqual("1000000", SongSelectRecordFormat.FormatScore(1000000));
        Assert.AreEqual("948210", SongSelectRecordFormat.FormatScore(948210));
        Assert.AreEqual("0", SongSelectRecordFormat.FormatScore(0));
        Assert.AreEqual("1104", SongSelectRecordFormat.FormatCombo(1104));
    }

    [Test]
    public void RecordFormat_Rate_TwoDecimals_Truncated()
    {
        Assert.AreEqual("95.34%", SongSelectRecordFormat.FormatRate(95.34));
        Assert.AreEqual("95.34%", SongSelectRecordFormat.FormatRate(95.3449));
        Assert.AreEqual("99.99%", SongSelectRecordFormat.FormatRate(99.996), "반올림하면 100.00%로 보이므로 버린다");
        Assert.AreEqual("100.00%", SongSelectRecordFormat.FormatRate(100));
        Assert.AreEqual("0.00%", SongSelectRecordFormat.FormatRate(0));
        Assert.AreEqual("7.10%", SongSelectRecordFormat.FormatRate(7.1));
        Assert.AreEqual("0.00%", SongSelectRecordFormat.FormatRate(double.NaN));
    }

    [Test]
    public void RecordFormat_NoRecord_AllDash_NoStamp()
    {
        RecordTexts texts = SongSelectRecordFormat.Build(null);
        Assert.IsFalse(texts.HasRecord);
        Assert.AreEqual("―", Empty, "기록 없음 표시는 긴 줄표 (글꼴에 U+2014가 없어 U+2015)");
        Assert.AreEqual(Empty, texts.Score);
        Assert.AreEqual(Empty, texts.Rate);
        Assert.AreEqual(Empty, texts.Combo);
        Assert.AreEqual(Empty, texts.Rank);
        Assert.IsFalse(texts.IsRankS);
        Assert.AreEqual(RecordStamp.None, texts.Stamp);
    }

    [Test]
    public void RecordFormat_Stamp_ApOnlyWhenAllPerfect()
    {
        Assert.AreEqual(RecordStamp.AllPerfect, SongSelectRecordFormat.GetStamp(true, true), "AP면 AP만");
        Assert.AreEqual(RecordStamp.AllPerfect, SongSelectRecordFormat.GetStamp(false, true));
        Assert.AreEqual(RecordStamp.FullCombo, SongSelectRecordFormat.GetStamp(true, false));
        Assert.AreEqual(RecordStamp.None, SongSelectRecordFormat.GetStamp(false, false));
        Assert.AreEqual("AP", SongSelectRecordFormat.GetStampText(RecordStamp.AllPerfect));
        Assert.AreEqual("FC", SongSelectRecordFormat.GetStampText(RecordStamp.FullCombo));
        Assert.AreEqual(string.Empty, SongSelectRecordFormat.GetStampText(RecordStamp.None));
    }

    [Test]
    public void RecordFormat_Build_FromRecord()
    {
        ChartRecordData record = new ChartRecordData
        {
            chartId = "c", bestScore = 948210, bestRank = "A", bestAccuracy = 95.3449, maxCombo = 1104, isFullCombo = true
        };
        RecordTexts texts = SongSelectRecordFormat.Build(record);
        Assert.IsTrue(texts.HasRecord);
        Assert.AreEqual("948210", texts.Score);
        Assert.AreEqual("95.34%", texts.Rate);
        Assert.AreEqual("1104", texts.Combo);
        Assert.AreEqual("A", texts.Rank);
        Assert.IsFalse(texts.IsRankS);
        Assert.AreEqual(RecordStamp.FullCombo, texts.Stamp);

        record.bestRank = "S";
        record.isAllPerfect = true;
        texts = SongSelectRecordFormat.Build(record);
        Assert.IsTrue(texts.IsRankS, "S는 하늘색");
        Assert.AreEqual(RecordStamp.AllPerfect, texts.Stamp);

        record.bestRank = "???";
        Assert.AreEqual("F", SongSelectRecordFormat.Build(record).Rank, "모르는 랭크 이름은 F");
    }

    [Test]
    public void RecordBook_BestAccuracy_IsHighestRegardlessOfScore()
    {
        // 최고 정확도는 점수와 상관없이 가장 높았던 값 (기획서 2-1 8, 11/17): 점수가 낮은 판의 정확도가 더 높아도 갱신된다
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());
        InGameEntryData entry = new InGameEntryData { songId = "s", chartId = "c", chartType = ChartType.Official, mode = InGameMode.Normal };
        DateTime now = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);

        book.Submit(entry, new ResultData { score = 900000, rank = PlayRank.A, accuracy = 90.0 }, now);
        book.Submit(entry, new ResultData { score = 800000, rank = PlayRank.B, accuracy = 92.5 }, now);
        ChartRecordData record = book.Find("c");
        Assert.AreEqual(900000, record.bestScore, "점수는 높은 판");
        Assert.AreEqual(92.5, record.bestAccuracy, 1e-9, "정확도는 점수와 상관없이 가장 높은 값");

        book.Submit(entry, new ResultData { score = 950000, rank = PlayRank.S, accuracy = 91.0 }, now);
        Assert.AreEqual(950000, record.bestScore);
        Assert.AreEqual(92.5, record.bestAccuracy, 1e-9, "점수가 올라도 정확도는 더 높았던 값 유지");
    }

    // ───── 2. 마지막 선택 ─────

    private static readonly string[] SongIds = { "song_a", "song_b" };

    private static bool ChartExists(string songId, string chartId, ChartType type)
    {
        if (type == ChartType.Official) return songId == "song_b" && chartId == "song_b_normal";
        return songId == "song_a" && chartId == "my_1";
    }

    [Test]
    public void LastSelection_SaveThenLoad_StartsThere()
    {
        PlayerData data = new PlayerData();
        Assert.IsTrue(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "song_b", "song_b_normal", ChartType.Official));
        Assert.IsFalse(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "song_b", "song_b_normal", ChartType.Official), "같은 값이면 저장할 것 없음");
        Assert.IsTrue(PlayerDataRules.TrySetLastScrollMultiplier(data.lastSelection, 5.5f));

        PlayerData loaded = PlayerDataRules.Sanitize(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data)));
        Assert.AreEqual("song_b", loaded.lastSelection.songId);
        Assert.AreEqual("song_b_normal", loaded.lastSelection.chartId);
        Assert.AreEqual("Official", loaded.lastSelection.chartType);
        Assert.AreEqual(5.5f, loaded.lastSelection.scrollMultiplier, 1e-4f);

        StartSelection start = PlayerDataRules.ResolveStartSelection(loaded.lastSelection, SongIds, ChartExists);
        Assert.IsTrue(start.FromLastSelection);
        Assert.AreEqual("song_b", start.SongId);
        Assert.AreEqual("song_b_normal", start.ChartId);
        Assert.AreEqual(ChartType.Official, start.ChartType);

        // 나만의 채보 → 탭도 그쪽으로
        PlayerDataRules.TrySetLastPlayed(loaded.lastSelection, "song_a", "my_1", ChartType.Custom);
        start = PlayerDataRules.ResolveStartSelection(loaded.lastSelection, SongIds, ChartExists);
        Assert.AreEqual(ChartType.Custom, start.ChartType);
        Assert.AreEqual("my_1", start.ChartId);
    }

    [Test]
    public void LastSelection_MissingChart_FallsBackToFirstOfficialSong()
    {
        LastSelectionSaveData deleted = new LastSelectionSaveData { songId = "song_a", chartId = "my_deleted", chartType = "Custom" };
        StartSelection start = PlayerDataRules.ResolveStartSelection(deleted, SongIds, ChartExists);
        Assert.IsFalse(start.FromLastSelection);
        Assert.AreEqual("song_a", start.SongId, "공식 탭 첫 곡");
        Assert.IsNull(start.ChartId, "첫 채보 (가장 낮은 난이도)");
        Assert.AreEqual(ChartType.Official, start.ChartType);

        LastSelectionSaveData unknownSong = new LastSelectionSaveData { songId = "song_x", chartId = "song_b_normal", chartType = "Official" };
        Assert.IsFalse(PlayerDataRules.ResolveStartSelection(unknownSong, SongIds, ChartExists).FromLastSelection, "목록에 없는 곡");

        Assert.IsFalse(PlayerDataRules.ResolveStartSelection(new LastSelectionSaveData(), SongIds, ChartExists).FromLastSelection, "빈 값");
        Assert.IsNull(PlayerDataRules.ResolveStartSelection(null, new string[0], ChartExists).SongId, "곡이 없으면 null");
    }

    [Test]
    public void LastSelection_OldJsonWithoutNewFields_LoadsDefaults()
    {
        // 11/17 이전 형식: lastSelection에 배속만 있다
        const string oldJson = "{\"version\":1,\"savedAt\":\"\",\"settings\":{},\"lastSelection\":{\"scrollMultiplier\":3.5},\"records\":[]}";
        List<string> log = new List<string>();
        PlayerData data = PlayerDataRules.Sanitize(JsonUtility.FromJson<PlayerData>(oldJson), log);

        Assert.AreEqual(3.5f, data.lastSelection.scrollMultiplier, 1e-4f, "배속은 그대로");
        Assert.AreEqual(string.Empty, data.lastSelection.songId);
        Assert.AreEqual(string.Empty, data.lastSelection.chartId);
        Assert.AreEqual("Official", data.lastSelection.chartType);
        Assert.IsFalse(PlayerDataRules.ResolveStartSelection(data.lastSelection, SongIds, ChartExists).FromLastSelection);

        // lastSelection 칸 자체가 없는 더 예전 형식
        PlayerData older = PlayerDataRules.Sanitize(JsonUtility.FromJson<PlayerData>("{\"version\":1,\"records\":[]}"));
        Assert.IsNotNull(older.lastSelection);
        Assert.AreEqual(string.Empty, older.lastSelection.songId);
    }

    [Test]
    public void LastSelection_Sanitize_HalfEmptyOrUnknownType()
    {
        LastSelectionSaveData onlySong = new LastSelectionSaveData { songId = "song_a", chartId = null, chartType = null };
        PlayerDataRules.SanitizeLastSelection(onlySong);
        Assert.AreEqual(string.Empty, onlySong.songId, "곡만 있고 채보가 없으면 둘 다 비움");
        Assert.AreEqual(string.Empty, onlySong.chartId);
        Assert.AreEqual("Official", onlySong.chartType, "모르는 탭 이름은 Official");

        LastSelectionSaveData badType = new LastSelectionSaveData { songId = "song_a", chartId = "my_1", chartType = "2" };
        PlayerDataRules.SanitizeLastSelection(badType);
        Assert.AreEqual("Official", badType.chartType, "숫자 문자열은 받지 않는다");
        Assert.AreEqual("my_1", badType.chartId);

        Assert.IsFalse(PlayerDataRules.TrySetLastPlayed(new LastSelectionSaveData(), "", "c", ChartType.Official), "빈 ID는 저장하지 않음");
    }

    // ───── 3. 나만의 채보 목록 이동 ─────

    [Test]
    public void Navigator_UpDown_CrossesSongBoundary_StopsAtEnds()
    {
        // 곡 A(채보 2) · 곡 B(채보 2) · 곡 C(채보 1) → 순번 0 1 | 2 3 | 4
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 2, 1 });
        Assert.AreEqual(5, nav.Count);
        Assert.AreEqual(2, nav.Vertical(1, 1), "곡 A 마지막 → 곡 B 첫 채보");
        Assert.AreEqual(1, nav.Vertical(2, -1), "곡 B 첫 → 곡 A 마지막");
        Assert.AreEqual(4, nav.Vertical(3, 1));
        Assert.AreEqual(4, nav.Vertical(4, 1), "아래 끝에서 멈춤");
        Assert.AreEqual(0, nav.Vertical(0, -1), "위 끝에서 멈춤 (돌아가지 않음)");
    }

    [Test]
    public void Navigator_LeftRight_JumpsToGroupFirstChart_StopsAtEnds()
    {
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 2, 1 });
        Assert.AreEqual(2, nav.Group(1, 1), "곡 A 두 번째 → 곡 B 첫 채보");
        Assert.AreEqual(2, nav.Group(0, 1));
        Assert.AreEqual(4, nav.Group(3, 1), "곡 B 두 번째 → 곡 C 첫 채보");
        Assert.AreEqual(4, nav.Group(4, 1), "마지막 곡에서 → 멈춤");
        Assert.AreEqual(0, nav.Group(3, -1), "곡 B 두 번째 → 곡 A 첫 채보");
        Assert.AreEqual(2, nav.Group(4, -1));
        Assert.AreEqual(1, nav.Group(1, -1), "첫 곡에서 ← → 멈춤 (그 자리)");
        Assert.AreEqual(0, nav.GetGroup(1));
        Assert.AreEqual(2, nav.GetGroup(4));
    }

    [Test]
    public void Navigator_NoCharts_AndEmptyGroups()
    {
        MyChartListNavigator none = new MyChartListNavigator(new int[0]);
        Assert.AreEqual(0, none.Count);
        Assert.AreEqual(-1, none.Vertical(0, 1));
        Assert.AreEqual(-1, none.Group(0, 1));
        Assert.AreEqual(-1, new MyChartListNavigator(null).Vertical(-1, 1));

        // 채보 0개인 묶음은 건너뛴다
        MyChartListNavigator gap = new MyChartListNavigator(new[] { 1, 0, 2 });
        Assert.AreEqual(1, gap.Group(0, 1));
        Assert.AreEqual(0, gap.Group(2, -1));
        Assert.AreEqual(0, gap.Vertical(-5, 1), "범위 밖 순번이면 첫 채보");
    }
}
