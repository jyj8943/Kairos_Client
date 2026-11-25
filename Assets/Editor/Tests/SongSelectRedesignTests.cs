using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 곡 선택 개편 (11/25) 규칙 EditMode 확인.
/// 1. BPM 표기(BpmDisplayRules): 구간 1개 / Range / Main(promptBpm 최근접 · 같은 거리면 앞 구간) / 끝자리 0 제거
/// 2. 최대 콤보(ChartData.maxCombo): 숏노트만 / 롱노트 유지 틱 포함 / 인게임(NoteJudge + ScoreCalculator)과 같은 값 (BPM 변속 포함)
/// 3. 마지막 수정 시각: 저장하면 updatedAt 기록 / 없으면 파일 시각 / 표시 형식
/// 4. 글자 형식: MAX COMBO 칸, + 줄 위쪽 숫자
/// 파일은 테스트용 곡 폴더(MyCharts/__test_redesign__)에만 쓰고 지운다.
/// </summary>
public class SongSelectRedesignTests
{
    private const string TestSongId = "__test_redesign__";

    [SetUp]
    public void SetUp()
    {
        DeleteFolder();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteFolder();
    }

    private static void DeleteFolder()
    {
        string folder = ChartRepository.GetSongFolder(TestSongId, ChartType.Custom);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    private static List<TimingPointData> Points(params float[] bpms)
    {
        List<TimingPointData> list = new List<TimingPointData>();
        for (int i = 0; i < bpms.Length; i++) list.Add(new TimingPointData { beat = i * 4f, bpm = bpms[i] });
        return list;
    }

    private static NoteData Short(float beat, int lane) => new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };

    private static NoteData Long(float beat, float endBeat, int lane) => new NoteData { beat = beat, endBeat = endBeat, lane = lane, type = NoteData.TypeLong };

    private static ChartData Chart(string chartId, List<TimingPointData> points, params NoteData[] notes)
    {
        return new ChartData
        {
            chartId = chartId, songId = TestSongId, type = ChartData.TypeCustom, chartName = "개편 테스트", level = 5,
            timingPoints = points, notes = new List<NoteData>(notes)
        };
    }

    private static ChartData SaveAndLoad(ChartData chart)
    {
        Assert.IsTrue(ChartRepository.TrySave(chart, out string saveError), saveError);
        Assert.IsTrue(ChartRepository.TryLoad(TestSongId, chart.chartId, ChartType.Custom, out ChartData loaded, out string loadError), loadError);
        return loaded;
    }

    // ───── 1. BPM 표기 ─────

    [Test]
    public void Bpm_OneSection_SingleValue_AnyDisplay()
    {
        Assert.AreEqual("150.61", BpmDisplayRules.Format(Points(150.61f), SongBpmDisplay.Main, 216f));
        Assert.AreEqual("150.61", BpmDisplayRules.Format(Points(150.61f), SongBpmDisplay.Range, 216f));
        Assert.AreEqual(GameConstants.SongSelect.EmptyValue, BpmDisplayRules.Format(new List<TimingPointData>(), SongBpmDisplay.Main, 150f), "구간 없음");
    }

    [Test]
    public void Bpm_Range_MinToMax_MutedTilde()
    {
        string hex = ColorUtility.ToHtmlStringRGB(UITheme.Current.textMuted);
        Assert.AreEqual($"148.2 <color=#{hex}>~</color> 156", BpmDisplayRules.Format(Points(150f, 156f, 148.2f), SongBpmDisplay.Range, 150f));
        Assert.AreEqual("150", BpmDisplayRules.Format(Points(150f, 150.001f), SongBpmDisplay.Range, 150f), "표기한 숫자가 같으면 하나만");
    }

    [Test]
    public void Bpm_Main_NearestToPrompt_TieTakesEarlier()
    {
        Assert.AreEqual("150.68", BpmDisplayRules.Format(Points(150.07f, 150.68f, 151.2f), SongBpmDisplay.Main, 150.61f));
        Assert.AreEqual("150.07", BpmDisplayRules.Format(Points(150.07f, 150.68f, 151.2f), SongBpmDisplay.Main, 150f));
        Assert.AreEqual("151", BpmDisplayRules.Format(Points(151f, 149f), SongBpmDisplay.Main, 150f), "같은 거리 → 앞 구간");
        Assert.AreEqual("149", BpmDisplayRules.Format(Points(149f, 151f), SongBpmDisplay.Main, 150f), "같은 거리 → 앞 구간");
        Assert.AreEqual("150.8", BpmDisplayRules.Format(Points(150.8f, 150.2f), SongBpmDisplay.Main, 150.5f), "소수도 같은 거리");
        Assert.AreEqual("120", BpmDisplayRules.Format(Points(120f, 180f), SongBpmDisplay.Main, 0f), "promptBpm 없음 → 첫 구간");
    }

    [Test]
    public void Bpm_TrailingZerosRemoved_MaxTwoDecimals()
    {
        Assert.AreEqual("156", BpmDisplayRules.Format(Points(156.00f), SongBpmDisplay.Main, 156f));
        Assert.AreEqual("148.2", BpmDisplayRules.Format(Points(148.20f), SongBpmDisplay.Main, 0f));
        Assert.AreEqual("150.07", BpmDisplayRules.Format(Points(150.068f), SongBpmDisplay.Main, 0f));
    }

    // ───── 2. 최대 콤보 ─────

    /// <summary>인게임(InGameScene)과 같은 순서로 만든 점수 계산기의 MaxPossibleCombo</summary>
    private static int InGameMaxCombo(ChartData chart)
    {
        TimingMap timing = TimingMap.FromChart(chart, 137);
        NoteJudge judge = new NoteJudge(PlayNoteBuilder.Build(chart.notes, timing, out _), GameConstants.Editor.LaneCount, new JudgeWindows());
        return new ScoreCalculator(judge.TotalCount, judge.TotalTicks).MaxPossibleCombo;
    }

    [Test]
    public void MaxCombo_ShortOnly_EqualsNoteCount()
    {
        ChartData loaded = SaveAndLoad(Chart("my_short", Points(150f), Short(0f, 0), Short(1f, 1), Short(1f, 2)));
        Assert.AreEqual(3, loaded.maxCombo);
        Assert.AreEqual(InGameMaxCombo(loaded), loaded.maxCombo);
    }

    [Test]
    public void MaxCombo_LongNote_IncludesHoldTicks()
    {
        // 0 → 2박: 0.25 ~ 1.75박 7틱, 4 → 4.25박: 0틱
        ChartData loaded = SaveAndLoad(Chart("my_long", Points(150f, 75f), Long(0f, 2f, 0), Long(4f, 4.25f, 1), Short(5f, 2)));
        Assert.AreEqual(3 + 7, loaded.maxCombo);
        Assert.AreEqual(InGameMaxCombo(loaded), loaded.maxCombo, "BPM 변속이 있어도 인게임과 같음");
        StringAssert.DoesNotContain("maxCombo", JsonUtility.ToJson(loaded), "파일에는 저장하지 않는다");
    }

    [Test]
    public void MaxCombo_OfficialNormal_EqualsInGame()
    {
        ChartData chart = ChartRepository.GetCharts(TestCharts.OfficialSongId, ChartType.Official).Find(c => c.chartId == TestCharts.OfficialChartId);
        Assert.IsNotNull(chart);
        Assert.Greater(chart.maxCombo, chart.notes.Count - 1);
        Assert.AreEqual(InGameMaxCombo(chart), chart.maxCombo);
        Debug.Log($"[SongSelectRedesignTest] {chart.chartId}: 노트 {chart.notes.Count}, 최대 콤보 {chart.maxCombo}, 인게임 {InGameMaxCombo(chart)}");
    }

    // ───── 3. 마지막 수정 시각 ─────

    [Test]
    public void UpdatedAt_WrittenOnSave()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-2);
        ChartData loaded = SaveAndLoad(Chart("my_time", Points(150f), Short(0f, 0)));
        Assert.IsTrue(ChartTimeRules.TryParse(loaded.updatedAt, out DateTime utc), loaded.updatedAt);
        Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        StringAssert.EndsWith("Z", loaded.updatedAt);
        Assert.That(utc, Is.InRange(before, DateTime.UtcNow.AddSeconds(2)));

        // 파일 시각이 달라져도 저장한 값이 기준
        File.SetLastWriteTimeUtc(ChartRepository.GetChartPath(TestSongId, loaded.chartId, ChartType.Custom), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.AreEqual(utc, ChartRepository.GetUpdatedAtUtc(loaded));
    }

    [Test]
    public void UpdatedAt_Missing_UsesFileTime()
    {
        ChartData loaded = SaveAndLoad(Chart("my_old", Points(150f), Short(0f, 0)));
        string path = ChartRepository.GetChartPath(TestSongId, loaded.chartId, ChartType.Custom);

        // 예전 채보: updatedAt 줄이 없는 파일
        List<string> lines = new List<string>(File.ReadAllLines(path));
        int index = lines.FindIndex(line => line.Contains("\"updatedAt\""));
        Assert.GreaterOrEqual(index, 0, "저장한 파일에 updatedAt이 있다");
        string before = lines[index - 1];
        if (!lines[index].TrimEnd().EndsWith(",")) lines[index - 1] = before.TrimEnd().TrimEnd(',');
        lines.RemoveAt(index);
        File.WriteAllText(path, string.Join("\n", lines));
        DateTime fileTime = new DateTime(2026, 11, 15, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, fileTime);

        Assert.IsTrue(ChartRepository.TryLoad(TestSongId, loaded.chartId, ChartType.Custom, out ChartData old, out string error), error);
        Assert.IsTrue(string.IsNullOrEmpty(old.updatedAt));
        Assert.AreEqual(fileTime, ChartRepository.GetUpdatedAtUtc(old));
        Assert.AreEqual(fileTime.ToLocalTime().ToString("MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), ChartTimeRules.ToDisplay(ChartRepository.GetUpdatedAtUtc(old)));
        Assert.AreEqual(fileTime, ChartTimeRules.Resolve("이상한 값", fileTime), "읽을 수 없는 값도 파일 시각");
        Assert.AreEqual(GameConstants.SongSelect.EmptyValue, ChartTimeRules.ToDisplay(null));
    }

    // ───── 4. 글자 형식 ─────

    [Test]
    public void ComboAndCountFormats()
    {
        Assert.AreEqual("812 <size=16><color=#7A7390>/ 1320</color></size>", SongSelectRecordFormat.FormatComboOfMax("812", 1320, "7A7390"));
        Assert.AreEqual("곡 01 / 08 · 새 채보 생성", Canvas_SongSelect.FormatNewChartCount(0, 8));
        Assert.AreEqual("MY CHART · 새 채보 생성", GameConstants.SongSelect.NewChartHeader);
        foreach (string text in new[] { GameConstants.SongSelect.NewChartRowLabel, GameConstants.SongSelect.NewChartCardTitle, GameConstants.SongSelect.NewChartStartLabel, GameConstants.SongSelect.NewChartTitle })
            Assert.AreEqual("새 채보 생성", text);
    }
}
