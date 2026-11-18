using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 곡 선택 화면 개편 규칙 EditMode 확인 (기획서 3-3 곡 선택 화면 개편 · 나만의 채보 탭 11/17, 2-4 마지막 선택).
/// 1. 최고 기록 카드 형식: 점수·콤보 쉼표 없음, RATE 소수 둘째 자리(버림), 기록 없음 —, 랭크(AP·S~F)·FC 값 (AP면 FC도, 11/18)
/// 2. 마지막 선택 (11/18 탭마다 한 칸): 플레이한 탭 칸만 바뀜(테스트 플레이 제외), 에디터 E · 새 채보 · 저장 → 나만의 채보 칸, 예전 JSON(한 벌) → 맞는 칸, 없는 ID → 빈 값
/// 3. 나만의 채보 목록 이동 (11/18): 정지점 = 채보 줄 + 곡마다 + 줄, ↓ 채보 → + 줄 → 다음 곡, ↑로 이전 곡 + 줄, ←→ 묶음 첫 정지점, 양 끝 멈춤, 곡 0개
/// 4. 곡 선택 진입: chartId → 그 채보 / 없으면 그 곡 + 줄 / songId 없음 → 첫 정지점, 진입 데이터가 배속을 덮어쓰지 않음, 위쪽 숫자·NO CHART
/// 5. 새 채보 검사: 빈 이름·공백만 → 오류, 레벨 범위 밖 → 범위 안
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
    public void RecordFormat_NoRecord_AllDash_NoFullCombo()
    {
        RecordTexts texts = SongSelectRecordFormat.Build(null);
        Assert.IsFalse(texts.HasRecord);
        Assert.AreEqual("―", Empty, "기록 없음 표시는 긴 줄표 (글꼴에 U+2014가 없어 U+2015)");
        Assert.AreEqual(Empty, texts.Score);
        Assert.AreEqual(Empty, texts.Rate);
        Assert.AreEqual(Empty, texts.Combo);
        Assert.IsFalse(texts.IsFullCombo);
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
        Assert.AreEqual(PlayRank.A, texts.Rank);
        Assert.IsTrue(texts.IsFullCombo);

        record.bestRank = "AP";
        record.isFullCombo = false;
        record.isAllPerfect = true;
        texts = SongSelectRecordFormat.Build(record);
        Assert.AreEqual(PlayRank.AP, texts.Rank);
        Assert.IsTrue(texts.IsFullCombo, "AP 기록이면 FC도 보인다");

        record.isAllPerfect = false;
        Assert.IsFalse(SongSelectRecordFormat.Build(record).IsFullCombo, "FC 아님");

        record.bestRank = "???";
        Assert.AreEqual(PlayRank.F, SongSelectRecordFormat.Build(record).Rank, "모르는 랭크 이름은 F");
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
    public void LastSelection_PlayPerTab_OnlyThatSlotChanges()
    {
        PlayerData data = new PlayerData();
        // 공식 플레이 → 공식 칸만
        Assert.IsTrue(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "song_b", "song_b_normal", ChartType.Official));
        Assert.IsFalse(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "song_b", "song_b_normal", ChartType.Official), "같은 값이면 저장할 것 없음");
        Assert.AreEqual("song_b_normal", data.lastSelection.official.chartId);
        Assert.IsTrue(data.lastSelection.custom.IsEmpty, "나만의 채보 칸은 그대로");

        // 나만의 채보 플레이 → 나만의 채보 칸만
        Assert.IsTrue(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "song_a", "my_1", ChartType.Custom));
        Assert.AreEqual("my_1", data.lastSelection.custom.chartId);
        Assert.AreEqual("song_b_normal", data.lastSelection.official.chartId, "공식 칸은 그대로");
        Assert.IsFalse(PlayerDataRules.TrySetLastPlayed(data.lastSelection, "", "c", ChartType.Official), "빈 ID는 저장하지 않음");

        // 테스트 플레이는 저장하지 않는다 (InGameLauncher.Start가 이 규칙으로 SetLastPlayed를 부른다)
        Assert.IsTrue(InGameLauncher.ShouldSaveLastPlayed(InGameLauncher.CreateNormalEntry("song_a", "my_1", ChartType.Custom, 2f)), "일반 플레이 (나만의 채보 포함)");
        Assert.IsFalse(InGameLauncher.ShouldSaveLastPlayed(new InGameEntryData { mode = InGameMode.TestPlay, testPlay = new TestPlayData() }), "테스트 플레이");
        Assert.IsFalse(InGameLauncher.ShouldSaveLastPlayed(null));

        // 저장 → 불러오기 → 탭마다 시작
        Assert.IsTrue(PlayerDataRules.TrySetLastScrollMultiplier(data.lastSelection, 5.5f));
        PlayerData loaded = PlayerDataRules.Sanitize(PlayerDataStore.Parse(JsonUtility.ToJson(data)));
        Assert.AreEqual(5.5f, loaded.lastSelection.scrollMultiplier, 1e-4f, "배속은 하나");
        StartSelection official = PlayerDataRules.ResolveOfficialStart(loaded.lastSelection, SongIds, ChartExists);
        Assert.IsTrue(official.FromLastSelection);
        Assert.AreEqual("song_b", official.SongId);
        Assert.AreEqual("song_b_normal", official.ChartId);
        TabSelectionSaveData custom = PlayerDataRules.ResolveTab(loaded.lastSelection.custom, ChartType.Custom, SongIds, ChartExists);
        Assert.AreEqual("song_a", custom.songId);
        Assert.AreEqual("my_1", custom.chartId);
    }

    [Test]
    public void LastSelection_Editor_CustomSlot_EditNewSave()
    {
        LastSelectionSaveData s = new LastSelectionSaveData();
        PlayerDataRules.TrySetLastPlayed(s, "song_b", "song_b_normal", ChartType.Official);

        // E로 에디터 → { songId, chartId }
        Assert.IsTrue(PlayerDataRules.TrySetTab(s, ChartType.Custom, "song_a", "my_1"));
        Assert.AreEqual("my_1", s.custom.chartId);

        // 새 채보로 에디터 → { songId, 빈 chartId } (그 곡의 + 줄)
        Assert.IsTrue(PlayerDataRules.TrySetTab(s, ChartType.Custom, "song_b", null));
        Assert.AreEqual("song_b", s.custom.songId);
        Assert.AreEqual(string.Empty, s.custom.chartId);
        TabSelectionSaveData addRow = PlayerDataRules.ResolveTab(s.custom, ChartType.Custom, SongIds, ChartExists);
        Assert.AreEqual("song_b", addRow.songId, "곡만 있음 = + 줄 (곡만 확인)");
        Assert.AreEqual(string.Empty, addRow.chartId);

        // 새 채보 저장 → chartId 갱신
        Assert.IsTrue(PlayerDataRules.TrySetTab(s, ChartType.Custom, "song_b", "my_new"));
        Assert.AreEqual("my_new", s.custom.chartId);
        Assert.AreEqual("song_b_normal", s.official.chartId, "공식 칸은 그대로");
        Assert.IsFalse(PlayerDataRules.TrySetTab(s, ChartType.Official, "song_b", null), "공식 칸은 채보 ID가 있어야 한다");

        // 정리: 나만의 채보 칸 곡만 있음(+ 줄)은 그대로, 곡 없이 채보만 있으면 비움
        s.custom = new TabSelectionSaveData { songId = "song_a", chartId = null };
        s.official = new TabSelectionSaveData { songId = null, chartId = "orphan" };
        PlayerDataRules.SanitizeLastSelection(s);
        Assert.AreEqual("song_a", s.custom.songId);
        Assert.AreEqual(string.Empty, s.custom.chartId);
        Assert.AreEqual(string.Empty, s.official.chartId);
    }

    [Test]
    public void LastSelection_OldJson_MovesToTabSlot_MissingIdsEmpty()
    {
        // 11/18 이전 형식 (곡·채보·탭 한 벌) → chartType에 맞는 칸, 다른 칸은 빈 값
        const string oldCustom = "{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":4.0,\"songId\":\"song_a\",\"chartId\":\"my_1\",\"chartType\":\"Custom\"},\"records\":[]}";
        PlayerData custom = PlayerDataRules.Sanitize(PlayerDataStore.Parse(oldCustom));
        Assert.AreEqual("song_a", custom.lastSelection.custom.songId);
        Assert.AreEqual("my_1", custom.lastSelection.custom.chartId);
        Assert.IsTrue(custom.lastSelection.official.IsEmpty, "다른 칸은 빈 값");
        Assert.AreEqual(4f, custom.lastSelection.scrollMultiplier, 1e-4f);

        const string oldOfficial = "{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":2.0,\"songId\":\"song_b\",\"chartId\":\"song_b_normal\",\"chartType\":\"Official\"}}";
        PlayerData official = PlayerDataRules.Sanitize(PlayerDataStore.Parse(oldOfficial));
        Assert.AreEqual("song_b_normal", official.lastSelection.official.chartId);
        Assert.IsTrue(official.lastSelection.custom.IsEmpty);

        // 새 형식 JSON에는 예전 칸(chartType)을 쓰지 않는다
        string json = JsonUtility.ToJson(official);
        StringAssert.Contains("\"official\"", json);
        StringAssert.DoesNotContain("chartType", json);

        // 더 예전 형식 (배속만 / lastSelection 없음)
        PlayerData speedOnly = PlayerDataRules.Sanitize(PlayerDataStore.Parse("{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":3.5}}"));
        Assert.AreEqual(3.5f, speedOnly.lastSelection.scrollMultiplier, 1e-4f);
        Assert.IsTrue(speedOnly.lastSelection.official.IsEmpty && speedOnly.lastSelection.custom.IsEmpty);
        Assert.IsNotNull(PlayerDataRules.Sanitize(PlayerDataStore.Parse("{\"version\":1,\"records\":[]}")).lastSelection);

        // 없는 ID → 빈 값 (나만의 채보는 첫 정지점, 공식은 첫 곡)
        Assert.IsTrue(PlayerDataRules.ResolveTab(new TabSelectionSaveData { songId = "song_a", chartId = "my_deleted" }, ChartType.Custom, SongIds, ChartExists).IsEmpty, "삭제된 나만의 채보");
        Assert.IsTrue(PlayerDataRules.ResolveTab(new TabSelectionSaveData { songId = "song_x", chartId = "" }, ChartType.Custom, SongIds, ChartExists).IsEmpty, "목록에 없는 곡");
        StartSelection fallback = PlayerDataRules.ResolveOfficialStart(new LastSelectionSaveData { official = new TabSelectionSaveData { songId = "song_x", chartId = "c" } }, SongIds, ChartExists);
        Assert.IsFalse(fallback.FromLastSelection);
        Assert.AreEqual("song_a", fallback.SongId, "공식 첫 곡");
        Assert.IsNull(fallback.ChartId, "가장 낮은 난이도");
        Assert.IsNull(PlayerDataRules.ResolveOfficialStart(null, new string[0], ChartExists).SongId, "곡이 없으면 null");
    }

    // ───── 3. 나만의 채보 목록 이동 (11/18: 정지점 = 채보 줄 + 곡마다 + 줄) ─────

    [Test]
    public void Navigator_StopList_ChartsThenAddRowPerSong()
    {
        // 곡 A(채보 2) · 곡 B(채보 0) · 곡 C(채보 1) → 정지점 A1 A2 A+ | B+ | C1 C+
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 0, 1 });
        Assert.AreEqual(6, nav.Count);
        Assert.AreEqual(3, nav.GroupCount);
        Assert.AreEqual(3, nav.ChartCount);
        Assert.AreEqual(MyChartStopKind.Chart, nav.Get(1).Kind);
        Assert.AreEqual(1, nav.Get(1).ChartInGroup);
        Assert.AreEqual(MyChartStopKind.AddRow, nav.Get(2).Kind, "곡 A 묶음 끝 = + 줄");
        Assert.AreEqual(1, nav.Get(3).Group, "채보 없는 곡 B = + 줄 하나");
        Assert.IsTrue(nav.Get(3).IsAddRow);
        Assert.AreEqual(2, nav.Get(4).ChartIndex, "곡 C 첫 채보 = 전체 채보 순번 2");
        Assert.AreEqual(4, nav.StopOfChart(2));
        Assert.AreEqual(3, nav.FirstStopOfGroup(1));
        Assert.AreEqual(5, nav.AddRowOfGroup(2));
        Assert.AreEqual(-1, nav.Get(9).Group, "범위 밖");
    }

    [Test]
    public void Navigator_UpDown_ChartToAddRowToNextSong_AndBack()
    {
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 0, 1 });
        Assert.AreEqual(2, nav.Vertical(1, 1), "↓ 채보 → 그 곡의 + 줄");
        Assert.AreEqual(3, nav.Vertical(2, 1), "↓ + 줄 → 다음 곡 첫 정지점 (채보 없는 곡은 + 줄)");
        Assert.AreEqual(4, nav.Vertical(3, 1), "채보 없는 곡 → 채보 있는 곡의 첫 채보");
        Assert.AreEqual(3, nav.Vertical(4, -1), "↑ 이전 곡의 + 줄");
        Assert.AreEqual(2, nav.Vertical(3, -1), "↑ 채보 없는 곡 → 이전 곡의 + 줄");
        Assert.AreEqual(1, nav.Vertical(2, -1), "↑ + 줄 → 그 곡 마지막 채보");
        Assert.AreEqual(5, nav.Vertical(5, 1), "아래 끝에서 멈춤");
        Assert.AreEqual(0, nav.Vertical(0, -1), "위 끝에서 멈춤 (돌아가지 않음)");
        Assert.AreEqual(0, nav.Vertical(-5, 1), "범위 밖이면 첫 정지점");
    }

    [Test]
    public void Navigator_LeftRight_JumpsToGroupFirstStop_StopsAtEnds()
    {
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 0, 1 });
        Assert.AreEqual(3, nav.Group(1, 1), "곡 A 두 번째 채보 → 곡 B 첫 정지점(+ 줄)");
        Assert.AreEqual(4, nav.Group(3, 1), "곡 B → 곡 C 첫 채보");
        Assert.AreEqual(3, nav.Group(5, -1), "곡 C + 줄 → 곡 B 첫 정지점");
        Assert.AreEqual(2, nav.Group(2, -1), "첫 곡에서 ← → 멈춤 (그 자리)");
        Assert.AreEqual(5, nav.Group(5, 1), "마지막 곡에서 → 멈춤");
        Assert.AreEqual(0, nav.Group(3, -1), "곡 B → 곡 A 첫 채보");
    }

    [Test]
    public void Navigator_NoSongs()
    {
        MyChartListNavigator none = new MyChartListNavigator(new int[0]);
        Assert.AreEqual(0, none.Count);
        Assert.AreEqual(-1, none.Vertical(0, 1));
        Assert.AreEqual(-1, none.Group(0, 1));
        Assert.AreEqual(-1, new MyChartListNavigator(null).Vertical(-1, 1));
        Assert.AreEqual(-1, none.ResolveEntryStop(new string[0], null, "a", null));
    }

    // ───── 4. 곡 선택 진입 (나만의 채보 탭) ─────

    [Test]
    public void EntryStop_ChartOrSongAddRowOrFirst()
    {
        MyChartListNavigator nav = new MyChartListNavigator(new[] { 2, 0, 1 });
        string[] songs = { "a", "b", "c" };
        List<IReadOnlyList<string>> charts = new List<IReadOnlyList<string>> { new[] { "a2", "a1" }, new string[0], new[] { "c1" } };

        Assert.AreEqual(1, nav.ResolveEntryStop(songs, charts, "a", "a1"), "chartId 있음 → 그 채보 줄");
        Assert.AreEqual(4, nav.ResolveEntryStop(songs, charts, "c", "c1"));
        Assert.AreEqual(2, nav.ResolveEntryStop(songs, charts, "a", null), "chartId 없음 → 그 곡의 + 줄");
        Assert.AreEqual(3, nav.ResolveEntryStop(songs, charts, "b", ""), "채보 없는 곡 → + 줄");
        Assert.AreEqual(0, nav.ResolveEntryStop(songs, charts, null, null), "songId 없음 → 첫 정지점");
        Assert.AreEqual(0, nav.ResolveEntryStop(songs, charts, "zzz", "a1"), "없는 곡 → 첫 정지점");
        Assert.AreEqual(4, nav.ResolveEntryStop(songs, charts, "c", "gone"), "그 곡에 없는 채보 → 그 곡 첫 정지점");
    }

    [Test]
    public void EntrySpeed_DoesNotOverwriteSavedSpeed()
    {
        Assert.AreEqual(4.2f, Canvas_SongSelect.ResolveStartSpeed(null, 4.2f), 1e-4f, "진입 데이터 없음 → 저장된 배속");
        Assert.AreEqual(4.2f, Canvas_SongSelect.ResolveStartSpeed(new SongSelectEntryData { chartType = ChartType.Custom }, 4.2f), 1e-4f,
            "배속을 넘기지 않은 진입 데이터(에디터·타이틀) → 저장된 배속 (기본값 1.0으로 덮어쓰지 않음)");
        Assert.AreEqual(5.5f, Canvas_SongSelect.ResolveStartSpeed(new SongSelectEntryData { scrollMultiplier = 5.5f }, 4.2f), 1e-4f, "결과에서 넘긴 배속");
        Assert.AreEqual("곡 02 / 08 · 채보 5", Canvas_SongSelect.FormatMyChartCount(1, 8, 5));
        Assert.AreEqual("NO CHART", SongGroupHeader.FormatCount(0));
        Assert.AreEqual("1 CHART", SongGroupHeader.FormatCount(1));
        Assert.AreEqual("3 CHARTS", SongGroupHeader.FormatCount(3));

        SongSelectEntryData fromEditor = EditorScene.CreateSongSelectEntry("s", null);
        Assert.AreEqual(ChartType.Custom, fromEditor.chartType, "에디터에서 나가면 나만의 채보 탭");
        Assert.IsNull(fromEditor.chartId, "새 채보를 저장하지 않았으면 + 줄");
        Assert.AreEqual(SongSelectEntryData.NoScrollMultiplier, fromEditor.scrollMultiplier);
    }

    // ───── 5. 새 채보 검사 (팝업) ─────

    [Test]
    public void NewChart_EmptyNameIsError_LevelClamped()
    {
        Assert.IsNull(NewChartRules.CreateEntry("s", "", 7), "빈 이름 → 오류");
        Assert.IsNull(NewChartRules.CreateEntry("s", "   ", 7), "공백만 → 오류");
        Assert.IsNull(NewChartRules.CreateEntry("s", null, 7));
        Assert.IsNull(NewChartRules.CreateEntry(null, "이름", 7), "곡이 없으면 만들지 않음");

        EditorEntryData data = NewChartRules.CreateEntry("s", "  보스 패턴  ", 7);
        Assert.AreEqual("보스 패턴", data.chartName, "앞뒤 공백 제거");
        Assert.AreEqual("s", data.songId);
        Assert.IsNull(data.chartId, "새 채보");
        Assert.AreEqual(7, data.level);

        Assert.AreEqual(GameConstants.Editor.MinLevel, NewChartRules.CreateEntry("s", "x", 0).level, "범위 밖 → 최소");
        Assert.AreEqual(GameConstants.Editor.MaxLevel, NewChartRules.CreateEntry("s", "x", 99).level, "범위 밖 → 최대");
        Assert.AreEqual(1, NewChartRules.ClampLevel(-3));
        Assert.AreEqual(15, NewChartRules.ClampLevel(16));
        Assert.AreEqual("새 채보 3", NewChartRules.DefaultName(2));
    }
}
