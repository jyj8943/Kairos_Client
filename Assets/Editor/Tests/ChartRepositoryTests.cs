using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ChartData·ChartRepository·ChartEditSession 변환 EditMode 테스트.
/// - 파일은 테스트용 곡 폴더(MyCharts/__test__, __test_other__)에만 쓰고, 테스트마다 지운다.
/// </summary>
public class ChartRepositoryTests
{
    private const string TestSongId = "__test__";
    private const string OtherTestSongId = "__test_other__";

    [SetUp]
    public void SetUp()
    {
        DeleteTestFolders();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTestFolders();
    }

    /// <summary>1. 저장 → 불러오기 하면 모든 필드와 노트가 같고, 노트는 beat·lane 순으로 정렬된다</summary>
    [Test]
    public void SaveThenLoad_AllFieldsAndNotesEqual()
    {
        ChartData saved = CreateChart("my_test_roundtrip");
        saved.chartName = "테스트 채보 #1 (한글·특수문자)";
        saved.level = 7;
        saved.timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } };
        saved.notes = new List<NoteData>
        {
            Short(2f, 3),
            Short(0.25f, 1),
            Short(0.25f, 0),
            new NoteData { beat = 4f, lane = 2, type = NoteData.TypeLong, endBeat = 5.5f }
        };

        Assert.IsTrue(ChartRepository.TrySave(saved, out string saveError), saveError);
        Assert.IsTrue(ChartRepository.TryLoad(TestSongId, saved.chartId, ChartType.Custom, out ChartData loaded, out string loadError), loadError);

        Assert.AreEqual(saved.version, loaded.version);
        Assert.AreEqual(saved.chartId, loaded.chartId);
        Assert.AreEqual(saved.songId, loaded.songId);
        Assert.AreEqual(saved.type, loaded.type);
        Assert.AreEqual(saved.chartName, loaded.chartName);
        Assert.AreEqual(saved.difficulty, loaded.difficulty);
        Assert.AreEqual(saved.author, loaded.author);
        Assert.AreEqual(saved.keyCount, loaded.keyCount);
        Assert.AreEqual(saved.level, loaded.level);
        Assert.AreEqual(1, loaded.timingPoints.Count);
        Assert.AreEqual(0f, loaded.timingPoints[0].beat);
        Assert.AreEqual(150.61f, loaded.timingPoints[0].bpm);

        // beat, lane 순으로 정렬되어 돌아온다
        float[] beats = { 0.25f, 0.25f, 2f, 4f };
        int[] lanes = { 0, 1, 3, 2 };
        Assert.AreEqual(beats.Length, loaded.notes.Count);
        for (int i = 0; i < beats.Length; i++)
        {
            Assert.AreEqual(beats[i], loaded.notes[i].beat, $"노트 {i} beat");
            Assert.AreEqual(lanes[i], loaded.notes[i].lane, $"노트 {i} lane");
        }
        Assert.AreEqual(NoteData.TypeLong, loaded.notes[3].type);
        Assert.AreEqual(5.5f, loaded.notes[3].endBeat);
    }

    /// <summary>2. 같은 chartId로 다시 저장하면 덮어쓰고 .tmp 파일이 남지 않는다</summary>
    [Test]
    public void SaveSameId_Overwrites_NoTempFileLeft()
    {
        ChartData chart = CreateChart("my_test_overwrite");
        chart.notes.Add(Short(1f, 0));
        Assert.IsTrue(ChartRepository.TrySave(chart, out string firstError), firstError);

        chart.chartName = "덮어쓴 이름";
        chart.notes.Add(Short(2f, 1));
        Assert.IsTrue(ChartRepository.TrySave(chart, out string secondError), secondError);

        Assert.IsTrue(ChartRepository.TryLoad(TestSongId, chart.chartId, ChartType.Custom, out ChartData loaded, out string loadError), loadError);
        Assert.AreEqual("덮어쓴 이름", loaded.chartName);
        Assert.AreEqual(2, loaded.notes.Count);

        string folder = ChartRepository.GetSongFolder(TestSongId, ChartType.Custom);
        Assert.AreEqual(1, Directory.GetFiles(folder).Length, "채보 파일 하나만 있어야 한다");
        Assert.IsEmpty(Directory.GetFiles(folder, "*" + GameConstants.Chart.TempExtension));
    }

    /// <summary>3. 깨진 JSON은 목록에서 빠지고 .broken으로 바뀐다. 다른 채보는 정상으로 읽힌다</summary>
    [Test]
    public void BrokenJson_SkippedAndRenamed_OtherChartsLoaded()
    {
        ChartData good = CreateChart("my_test_good");
        Assert.IsTrue(ChartRepository.TrySave(good, out string error), error);

        string brokenPath = ChartRepository.GetChartPath(TestSongId, "my_test_broken", ChartType.Custom);
        File.WriteAllText(brokenPath, "{ \"chartId\": \"my_test_broken\", \"notes\": [ 이건 JSON이 아니다");

        List<ChartData> charts = ChartRepository.GetCharts(TestSongId, ChartType.Custom);

        Assert.AreEqual(1, charts.Count);
        Assert.AreEqual("my_test_good", charts[0].chartId);
        Assert.IsFalse(File.Exists(brokenPath), "깨진 파일은 원래 이름으로 남지 않는다");
        Assert.IsTrue(File.Exists(brokenPath + GameConstants.Chart.BrokenExtension), ".broken 백업이 있어야 한다");

        // 같은 이름으로 또 깨지면 시간을 붙여 따로 백업한다
        File.WriteAllText(brokenPath, "not json");
        ChartRepository.GetCharts(TestSongId, ChartType.Custom);
        string folder = ChartRepository.GetSongFolder(TestSongId, ChartType.Custom);
        Assert.AreEqual(2, Directory.GetFiles(folder, "*" + GameConstants.Chart.BrokenExtension).Length);
    }

    /// <summary>4. 폴더의 songId와 파일의 songId가 다르면 건너뛰고 파일은 그대로 둔다</summary>
    [Test]
    public void SongIdMismatch_Skipped_FileKept()
    {
        ChartData other = CreateChart("my_test_other");
        other.songId = OtherTestSongId;
        Assert.IsTrue(ChartRepository.TrySave(other, out string error), error);

        // 다른 곡 파일을 __test__ 폴더로 옮겨 둔다
        string wrongPath = ChartRepository.GetChartPath(TestSongId, other.chartId, ChartType.Custom);
        Directory.CreateDirectory(Path.GetDirectoryName(wrongPath));
        File.Move(ChartRepository.GetChartPath(OtherTestSongId, other.chartId, ChartType.Custom), wrongPath);

        List<ChartData> charts = ChartRepository.GetCharts(TestSongId, ChartType.Custom);

        Assert.IsEmpty(charts);
        Assert.IsTrue(File.Exists(wrongPath), "파일은 그대로 있어야 한다");
        Assert.IsFalse(ChartRepository.TryLoad(TestSongId, other.chartId, ChartType.Custom, out _, out _));
    }

    /// <summary>5. 없는 곡 폴더는 빈 목록, 없는 chartId는 TryLoad false</summary>
    [Test]
    public void MissingFolderAndMissingChart()
    {
        Assert.IsEmpty(ChartRepository.GetCharts(TestSongId, ChartType.Custom));
        Assert.IsEmpty(ChartRepository.GetCharts(TestSongId, ChartType.Official));

        Assert.IsFalse(ChartRepository.TryLoad(TestSongId, "my_test_none", ChartType.Custom, out ChartData chart, out string error));
        Assert.IsNull(chart);
        Assert.IsNotEmpty(error);
    }

    /// <summary>형식이 맞지 않는 채보는 저장·불러오기가 실패하고, 읽을 때 파일은 그대로 둔다</summary>
    [Test]
    public void InvalidChart_Rejected()
    {
        ChartData badKey = CreateChart("my_test_badkey");
        badKey.keyCount = 6;
        Assert.IsFalse(ChartRepository.TrySave(badKey, out _));

        ChartData noTiming = CreateChart("my_test_notiming");
        noTiming.timingPoints.Clear();
        Assert.IsFalse(ChartRepository.TrySave(noTiming, out _));

        ChartData badLane = CreateChart("my_test_badlane");
        badLane.notes.Add(Short(1f, 4));
        Assert.IsFalse(ChartRepository.TrySave(badLane, out _));

        ChartData unsafeId = CreateChart("../escape");
        Assert.IsFalse(ChartRepository.TrySave(unsafeId, out _));

        // 파일로 직접 쓴 지원하지 않는 버전 → 읽기 실패, 파일 유지
        ChartData oldVersion = CreateChart("my_test_version");
        oldVersion.version = 99;
        string path = ChartRepository.GetChartPath(TestSongId, oldVersion.chartId, ChartType.Custom);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(oldVersion, true));

        Assert.IsFalse(ChartRepository.TryLoad(TestSongId, oldVersion.chartId, ChartType.Custom, out _, out string error));
        StringAssert.Contains("버전", error);
        Assert.IsTrue(File.Exists(path));
    }

    /// <summary>6. 새 채보 ID는 my_yyyyMMdd_HHmmss_xxxx 형식이고 연속으로 만들어도 겹치지 않는다</summary>
    [Test]
    public void CreateNewCustomChartId_FormatAndUnique()
    {
        Regex format = new Regex(@"^my_\d{8}_\d{6}_[a-z0-9]{4}$");
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            string id = ChartRepository.CreateNewCustomChartId(TestSongId);
            Assert.IsTrue(format.IsMatch(id), $"형식이 맞지 않음: {id}");
            Assert.IsTrue(ids.Add(id), $"겹침: {id}");
        }
    }

    /// <summary>7. 셋잇단(16틱)·16분(12틱)·64분(3틱) 위치 노트가 ToChartData → ApplyChartData 뒤에도 같은 tick이다 (파일을 거쳐도 같다)</summary>
    [Test]
    public void Session_ToChartDataAndApply_KeepsTicks()
    {
        int[] ticks = { 0, 16, 32, 12, 36, 3, 45, 48 + 16, 48 * 10 + 3, 48 * 100 + 45 };
        GameObject sourceObject = new GameObject("ChartEditSession_Source");
        GameObject targetObject = new GameObject("ChartEditSession_Target");
        try
        {
            ChartEditSession source = CreateSession(sourceObject, null);
            for (int i = 0; i < ticks.Length; i++)
                Assert.IsTrue(source.AddNote(i % GameConstants.Editor.LaneCount, ticks[i]), $"tick {ticks[i]} 추가");

            ChartData chart = source.ToChartData();
            Assert.IsNotNull(chart);
            Assert.AreEqual(chart.chartId, source.ChartId, "새 ID가 세션에 기록된다");
            Assert.AreEqual(1, chart.timingPoints.Count);
            Assert.AreEqual(150f, chart.timingPoints[0].bpm);

            // 메모리에서 바로 적용
            source.ApplyChartData(chart);
            AssertTicks(source, ticks);
            Assert.IsFalse(source.IsDirty);

            // 파일 저장 → 새 세션이 chartId로 불러오기
            Assert.IsTrue(source.TrySave(out string saveError), saveError);
            ChartEditSession target = CreateSession(targetObject, source.ChartId);
            AssertTicks(target, ticks);
            Assert.AreEqual(source.ChartName, target.ChartName);
            Assert.AreEqual(source.Level, target.Level);
            Assert.IsFalse(target.IsDirty);
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    /// <summary>스냅 칸 위의 노트(셋잇단 1/3박 등 float 오차 포함)는 경고가 없고, 칸에서 벗어난 노트만 경고한다</summary>
    [Test]
    public void ApplyChartData_WarnsOnlyForOffGridBeat()
    {
        GameObject owner = new GameObject("ChartEditSession_Rounding");
        try
        {
            ChartEditSession session = CreateSession(owner, null);

            ChartData onGrid = CreateChart("my_test_ongrid");
            onGrid.notes.Add(Short(1f / 3f, 0));
            onGrid.notes.Add(Short(10f + 1f / 12f, 1));
            onGrid.notes.Add(Short(100f + 15f / 16f, 2));
            int roundingWarnings = 0;
            Application.LogCallback counter = (message, stackTrace, type) =>
            {
                if (type == LogType.Warning && message.Contains("반올림 오차")) roundingWarnings++;
            };
            Application.logMessageReceived += counter;
            try
            {
                session.ApplyChartData(onGrid);
                Assert.AreEqual(0, roundingWarnings, "스냅 칸 위의 노트는 경고하지 않는다");

                ChartData offGrid = CreateChart("my_test_offgrid");
                offGrid.notes.Add(Short(0.1f, 0)); // 4.8틱 → 5틱으로 반올림 (오차 0.2틱)
                session.ApplyChartData(offGrid);
                Assert.AreEqual(1, roundingWarnings, "스냅 칸에서 벗어난 노트는 경고한다");
                Assert.AreEqual(5, session.Data.Notes[0].Tick);
            }
            finally
            {
                Application.logMessageReceived -= counter;
            }
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    private static ChartEditSession CreateSession(GameObject owner, string chartId)
    {
        ChartEditSession session = owner.AddComponent<ChartEditSession>();
        EditorEntryData entry = new EditorEntryData { songId = TestSongId, chartName = "세션 테스트", level = 3, chartId = chartId };
        session.Init(entry, 150f, 144f);
        return session;
    }

    private static void AssertTicks(ChartEditSession session, int[] expectedTicks)
    {
        List<int> actual = new List<int>();
        foreach (EditorNote note in session.Data.Notes) actual.Add(note.Tick);

        List<int> expected = new List<int>(expectedTicks);
        expected.Sort();
        CollectionAssert.AreEqual(expected, actual);
    }

    private static ChartData CreateChart(string chartId)
    {
        return new ChartData
        {
            chartId = chartId,
            songId = TestSongId,
            type = ChartData.TypeCustom,
            chartName = "테스트",
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150f } },
            notes = new List<NoteData>()
        };
    }

    private static NoteData Short(float beat, int lane)
    {
        return new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };
    }

    private static void DeleteTestFolders()
    {
        foreach (string songId in new[] { TestSongId, OtherTestSongId })
        {
            string folder = ChartRepository.GetSongFolder(songId, ChartType.Custom);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
