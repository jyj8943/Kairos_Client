using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 유저 플레이 데이터(PlayerData.json) EditMode 테스트. 임시 폴더에 파일을 만들어 PlayerDataStore·PlayerDataRules·PlayerRecordBook을 시험한다.
/// </summary>
public class PlayerDataTests
{
    private string folder;
    private string filePath;

    private static readonly DateTime T1 = new DateTime(2026, 11, 12, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new DateTime(2026, 11, 12, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T3 = new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "KairosPlayerDataTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        filePath = Path.Combine(folder, GameConstants.PlayerData.FileName);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    // ───── 만들기 ─────

    private static InGameEntryData Entry(string chartId = "song_001_hard", ChartType type = ChartType.Official)
    {
        return new InGameEntryData { songId = "song_001", chartId = chartId, chartType = type, mode = InGameMode.Normal };
    }

    private static ResultData Result(int score, PlayRank rank, int maxCombo, double accuracy, bool fc = false, bool ap = false)
    {
        return new ResultData { score = score, rank = rank, maxCombo = maxCombo, accuracy = accuracy, isFullCombo = fc, isAllPerfect = ap };
    }

    private static PlayerData Sample()
    {
        return new PlayerData
        {
            savedAt = "2026-11-12T01:00:00Z",
            settings = new SettingsSaveData
            {
                masterVolume = 80, musicVolume = 55, sfxVolume = 30, offsetMs = -35,
                resolutionWidth = 1600, resolutionHeight = 900, windowMode = FullScreenMode.Windowed.ToString()
            },
            records = new List<ChartRecordData>
            {
                new ChartRecordData
                {
                    chartId = "song_001_hard", songId = "song_001", chartType = "Official", bestScore = 912345, bestRank = "A",
                    bestAccuracy = 93.25, maxCombo = 321, isFullCombo = true, isAllPerfect = false, playCount = 4,
                    firstPlayedAt = "2026-11-10T09:00:00Z", lastPlayedAt = "2026-11-12T01:00:00Z"
                },
                new ChartRecordData
                {
                    chartId = "my_20261110_143229_b5ra", songId = "d_minor_descent", chartType = "Custom", bestScore = 1000000, bestRank = "S",
                    bestAccuracy = 100, maxCombo = 111, isFullCombo = true, isAllPerfect = true, playCount = 1,
                    firstPlayedAt = "2026-11-11T00:00:00Z", lastPlayedAt = "2026-11-11T00:00:00Z"
                }
            }
        };
    }

    // ───── 저장 → 불러오기 ─────

    [Test]
    public void SaveThenLoad_RoundTripIsSame()
    {
        PlayerData original = Sample();
        PlayerDataStore store = new PlayerDataStore(filePath);

        Assert.IsTrue(store.Save(original), store.LastError);
        Assert.IsTrue(File.Exists(filePath));
        Assert.IsFalse(File.Exists(store.TempPath), "임시 파일이 남지 않는다");

        Assert.AreEqual(PlayerDataStore.LoadSource.Main, store.Load(out PlayerData loaded));
        Assert.AreEqual(JsonUtility.ToJson(original), JsonUtility.ToJson(PlayerDataRules.Sanitize(loaded)));

        // enum은 이름 문자열로 저장된다
        string json = File.ReadAllText(filePath);
        StringAssert.Contains("\"windowMode\": \"Windowed\"", json);
        StringAssert.Contains("\"chartType\": \"Custom\"", json);
        StringAssert.Contains("\"bestRank\": \"S\"", json);
    }

    [Test]
    public void Save_MovesPreviousFileToBackup()
    {
        PlayerDataStore store = new PlayerDataStore(filePath);
        PlayerData first = Sample();
        Assert.IsTrue(store.Save(first));

        PlayerData second = Sample();
        second.settings.masterVolume = 10;
        Assert.IsTrue(store.Save(second));

        Assert.IsTrue(File.Exists(store.BackupPath));
        PlayerData backup = JsonUtility.FromJson<PlayerData>(File.ReadAllText(store.BackupPath));
        Assert.AreEqual(80, backup.settings.masterVolume, ".bak은 직전 파일");
        store.Load(out PlayerData main);
        Assert.AreEqual(10, main.settings.masterVolume);
    }

    [Test]
    public void Load_NoFile_ReturnsNoFile()
    {
        PlayerDataStore store = new PlayerDataStore(filePath);
        Assert.AreEqual(PlayerDataStore.LoadSource.NoFile, store.Load(out PlayerData data));
        Assert.IsNull(data);
    }

    // ───── 기록 갱신 규칙 ─────

    [Test]
    public void Submit_KeepsBestPerItem_CountsPlays_AndFlagsNewRecord()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());

        // 1판: 처음 → 신기록
        RecordUpdateResult first = book.Submit(Entry(), Result(900000, PlayRank.A, 50, 90.0), T1);
        Assert.IsTrue(first.isRecorded);
        Assert.IsTrue(first.isNewRecord, "처음 플레이는 신기록");
        Assert.IsFalse(first.hadPreviousRecord);
        Assert.AreEqual(1, first.playCount);

        // 2판: 점수만 높음 → 신기록, 콤보·정확도는 그대로
        RecordUpdateResult second = book.Submit(Entry(), Result(950000, PlayRank.S, 30, 85.0), T2);
        Assert.IsTrue(second.isNewRecord);
        Assert.IsTrue(second.hadPreviousRecord);
        Assert.AreEqual(900000, second.previousBestScore);

        // 3판: 콤보만 높고 FC → 신기록 아님, 콤보·정확도·FC만 갱신
        RecordUpdateResult third = book.Submit(Entry(), Result(800000, PlayRank.B, 120, 95.5, fc: true), T3);
        Assert.IsFalse(third.isNewRecord, "점수가 오르지 않으면 신기록이 아니다");
        Assert.AreEqual(950000, third.previousBestScore);

        ChartRecordData r = book.Find("song_001_hard");
        Assert.AreEqual(950000, r.bestScore);
        Assert.AreEqual("S", r.bestRank, "bestRank는 bestScore를 낸 판의 랭크");
        Assert.AreEqual(120, r.maxCombo);
        Assert.AreEqual(95.5, r.bestAccuracy, 1e-9);
        Assert.IsTrue(r.isFullCombo);
        Assert.IsFalse(r.isAllPerfect);
        Assert.AreEqual(3, r.playCount);
        Assert.AreEqual("2026-11-12T01:00:00Z", r.firstPlayedAt);
        Assert.AreEqual("2026-11-12T03:00:00Z", r.lastPlayedAt);
        Assert.AreEqual("Official", r.chartType);
        Assert.AreEqual(1, book.Records.Count);

        // 4판: FC 아님 → FC는 한 번 true면 계속 true
        book.Submit(Entry(), Result(700000, PlayRank.C, 10, 50.0), T3);
        Assert.IsTrue(book.Find("song_001_hard").isFullCombo);
        Assert.AreEqual(4, book.Find("song_001_hard").playCount);
    }

    [Test]
    public void Submit_TestPlayAutoPlayAndEmptyChartId_AreNotRecorded()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());

        InGameEntryData test = Entry("my_x", ChartType.Custom);
        test.mode = InGameMode.TestPlay;
        test.testPlay = new TestPlayData();
        ResultData testResult = Result(1000000, PlayRank.S, 10, 100);
        testResult.isTestPlay = true;
        RecordUpdateResult a = book.Submit(test, testResult, T1);

        ResultData autoResult = Result(1000000, PlayRank.S, 10, 100);
        autoResult.isAutoPlay = true;
        RecordUpdateResult b = book.Submit(Entry(), autoResult, T1);

        RecordUpdateResult c = book.Submit(Entry(""), Result(1000000, PlayRank.S, 10, 100), T1);

        Assert.IsFalse(a.isRecorded); Assert.AreEqual("테스트 플레이", a.skipReason);
        Assert.IsFalse(b.isRecorded); Assert.AreEqual("자동 플레이", b.skipReason);
        Assert.IsFalse(c.isRecorded); Assert.AreEqual("chartId 없음", c.skipReason);
        Assert.IsFalse(a.isNewRecord || b.isNewRecord || c.isNewRecord);
        Assert.AreEqual(0, book.Records.Count);
    }

    // ───── 깨진 파일 ─────

    [Test]
    public void Load_BrokenMain_RecoversFromBackup()
    {
        PlayerDataStore store = new PlayerDataStore(filePath);
        File.WriteAllText(filePath, "{ \"version\": 1, \"settings\": { \"masterVolume\": ");
        File.WriteAllText(store.BackupPath, JsonUtility.ToJson(Sample(), true));

        Assert.AreEqual(PlayerDataStore.LoadSource.Backup, store.Load(out PlayerData data, new DateTime(2026, 11, 12, 10, 20, 30)));
        Assert.AreEqual(80, data.settings.masterVolume);
        Assert.AreEqual(2, data.records.Count);
        // 깨진 본 파일은 broken 이름으로 옮겨 다음 저장 때 .bak을 덮지 않게 한다
        Assert.IsFalse(File.Exists(filePath));
        Assert.IsTrue(File.Exists(Path.Combine(folder, "PlayerData_broken_20261112_102030.json")));
        Assert.IsTrue(File.Exists(store.BackupPath));
    }

    [Test]
    public void Load_BothBroken_StartsDefault_AndLeavesBrokenFile()
    {
        PlayerDataStore store = new PlayerDataStore(filePath);
        File.WriteAllText(filePath, "not json at all");
        File.WriteAllText(store.BackupPath, "");

        Assert.AreEqual(PlayerDataStore.LoadSource.Broken, store.Load(out PlayerData data, new DateTime(2026, 11, 12, 10, 20, 30)));
        Assert.IsNull(data);
        string broken = Path.Combine(folder, "PlayerData_broken_20261112_102030.json");
        Assert.IsTrue(File.Exists(broken), "깨진 파일을 남긴다");
        Assert.AreEqual(store.LastBrokenPath, broken);
        Assert.AreEqual("not json at all", File.ReadAllText(broken));
        Assert.IsFalse(File.Exists(filePath));

        // 기본값으로 시작 (Sanitize가 null을 기본값으로)
        PlayerData fallback = PlayerDataRules.Sanitize(null);
        Assert.AreEqual(GameConstants.Settings.DefaultVolume, fallback.settings.masterVolume);
        Assert.AreEqual(0, fallback.records.Count);
    }

    // ───── 값 정리 ─────

    [Test]
    public void Sanitize_UnknownEnumsAndOutOfRange_BecomeDefaultsOrClamped()
    {
        PlayerData data = new PlayerData
        {
            version = 1,
            settings = new SettingsSaveData
            {
                masterVolume = 150, musicVolume = -5, sfxVolume = 50, offsetMs = 203,
                resolutionWidth = 1000, resolutionHeight = 500, windowMode = "SuperFullScreen"
            },
            records = new List<ChartRecordData>
            {
                new ChartRecordData { chartId = "a", chartType = "Weird", bestScore = 2000000, bestRank = "SSS", bestAccuracy = 130, maxCombo = -3, playCount = -1 },
                new ChartRecordData { chartId = "", bestScore = 1 },
                null
            }
        };

        List<string> log = new List<string>();
        PlayerDataRules.Sanitize(data, log);

        Assert.AreEqual(100, data.settings.masterVolume);
        Assert.AreEqual(0, data.settings.musicVolume);
        Assert.AreEqual(50, data.settings.sfxVolume);
        Assert.AreEqual(200, data.settings.offsetMs, "203 → 5ms 단위 205 → 범위 200");
        Assert.AreEqual(GameConstants.Settings.DefaultResolution.x, data.settings.resolutionWidth);
        Assert.AreEqual(GameConstants.Settings.DefaultResolution.y, data.settings.resolutionHeight);
        Assert.AreEqual(GameConstants.Settings.DefaultWindowMode.ToString(), data.settings.windowMode);

        Assert.AreEqual(1, data.records.Count, "chartId 없는 기록·null은 버린다");
        ChartRecordData r = data.records[0];
        Assert.AreEqual("Custom", r.chartType);
        Assert.AreEqual(GameConstants.Play.MaxScore, r.bestScore);
        Assert.AreEqual("F", r.bestRank);
        Assert.AreEqual(100, r.bestAccuracy, 1e-9);
        Assert.AreEqual(0, r.maxCombo);
        Assert.AreEqual(0, r.playCount);
        Assert.Greater(log.Count, 0);

        // 숫자로 쓴 enum은 받지 않는다 (순서가 바뀌면 뜻이 달라지므로)
        Assert.AreEqual(GameConstants.Settings.DefaultWindowMode, PlayerDataRules.ParseWindowMode("3"));
        Assert.AreEqual(PlayRank.F, PlayerDataRules.ParseRank("0"));
        // 목록에 없는 창 모드(MaximizedWindow)도 기본값
        Assert.AreEqual(GameConstants.Settings.DefaultWindowMode, PlayerDataRules.ParseWindowMode(FullScreenMode.MaximizedWindow.ToString()));
        Assert.AreEqual(FullScreenMode.Windowed, PlayerDataRules.ParseWindowMode("Windowed"));
        // 오프셋 5ms 단위
        Assert.AreEqual(5, SettingsManager.ClampOffset(7));
        Assert.AreEqual(-200, SettingsManager.ClampOffset(-999));
    }

    [Test]
    public void Sanitize_MergesDuplicateChartIds()
    {
        List<ChartRecordData> records = new List<ChartRecordData>
        {
            new ChartRecordData { chartId = "c", bestScore = 800000, bestRank = "B", bestAccuracy = 97, maxCombo = 10, isFullCombo = true, playCount = 2,
                firstPlayedAt = "2026-11-10T00:00:00Z", lastPlayedAt = "2026-11-10T05:00:00Z" },
            new ChartRecordData { chartId = "c", bestScore = 900000, bestRank = "A", bestAccuracy = 90, maxCombo = 99, isAllPerfect = true, playCount = 3,
                firstPlayedAt = "2026-11-09T00:00:00Z", lastPlayedAt = "2026-11-11T00:00:00Z" }
        };

        List<ChartRecordData> merged = PlayerDataRules.MergeRecords(records);

        Assert.AreEqual(1, merged.Count);
        ChartRecordData r = merged[0];
        Assert.AreEqual(900000, r.bestScore);
        Assert.AreEqual("A", r.bestRank);
        Assert.AreEqual(97, r.bestAccuracy, 1e-9);
        Assert.AreEqual(99, r.maxCombo);
        Assert.IsTrue(r.isFullCombo);
        Assert.IsTrue(r.isAllPerfect);
        Assert.AreEqual(5, r.playCount);
        Assert.AreEqual("2026-11-09T00:00:00Z", r.firstPlayedAt);
        Assert.AreEqual("2026-11-11T00:00:00Z", r.lastPlayedAt);
    }

    /// <summary>
    /// 회귀: Sanitize가 records를 새 List로 바꾸면 PlayerRecordBook이 들고 있는 목록과 저장하는 목록이 어긋나
    /// 첫 저장 뒤의 기록 추가·삭제가 파일에 들어가지 않았다. 같은 List 안에서 정리해야 한다.
    /// </summary>
    [Test]
    public void Sanitize_KeepsRecordListIdentity_SoBookChangesAreSaved()
    {
        PlayerData data = Sample();
        List<ChartRecordData> list = data.records;
        PlayerRecordBook book = new PlayerRecordBook(data.records);

        PlayerDataRules.Sanitize(data);
        Assert.AreSame(list, data.records, "Sanitize 뒤에도 같은 List");

        book.Remove("song_001_hard");
        book.Submit(Entry("song_002_easy"), Result(500000, PlayRank.F, 5, 40), T1);

        PlayerDataStore store = new PlayerDataStore(filePath);
        Assert.IsTrue(store.Save(data));
        store.Load(out PlayerData loaded);
        PlayerRecordBook reloaded = new PlayerRecordBook(PlayerDataRules.Sanitize(loaded).records);
        Assert.IsNull(reloaded.Find("song_001_hard"), "지운 기록이 파일에서도 없다");
        Assert.IsNotNull(reloaded.Find("song_002_easy"), "새 기록이 파일에 들어간다");
        Assert.IsNotNull(reloaded.Find("my_20261110_143229_b5ra"));
    }

    // ───── 기록 초기화 ─────

    [Test]
    public void Remove_DeletesThatChartRecordOnly_AndSurvivesSaveLoad()
    {
        PlayerData data = Sample();
        PlayerRecordBook book = new PlayerRecordBook(data.records);

        Assert.IsTrue(book.Remove("my_20261110_143229_b5ra"));
        Assert.IsNull(book.Find("my_20261110_143229_b5ra"));
        Assert.IsNotNull(book.Find("song_001_hard"), "다른 채보 기록은 그대로");
        Assert.IsFalse(book.Remove("my_20261110_143229_b5ra"), "없는 기록은 false");

        PlayerDataStore store = new PlayerDataStore(filePath);
        Assert.IsTrue(store.Save(data));
        store.Load(out PlayerData loaded);
        PlayerRecordBook reloaded = new PlayerRecordBook(PlayerDataRules.Sanitize(loaded).records);
        Assert.IsNull(reloaded.Find("my_20261110_143229_b5ra"));
        Assert.AreEqual(1, reloaded.Records.Count);
    }
}
