using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 랭크 · FC 이미지 실제 씬 확인 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」 · 3-5 결과, EnterPlayMode 방식). 곡을 재생하지 않는다.
/// - K1 곡 선택: 기록 없음 → 기록 카드 Img_RankEmpty 보임 · Img_Rank 숨김 · FC 숨김 · 목록 "—".
///   AP 기록 → 기록 카드 Rank_AP (94 칸에 맞춤) + FC(폭 30), 목록 Rank_AP (높이 26)
/// - K2 결과: AP → Rank_AP (높이 150) + FC(폭 78) + 곡 끝 연출 ALL PERFECT / S·FC → Rank_S 150×150 + FC + FULL COMBO / FC 아님 → FC 숨김
/// 기록·마지막 선택은 메모리에서만 바꾸고 끝나면 되돌린다. 혹시 그동안 PlayerData.json이 저장됐으면 처음 내용으로 되돌려 쓴다.
/// 결과는 테스트 플레이 결과로 들어가 기록을 남기지 않는다.
/// </summary>
public class RankDisplayPlayTests
{
    private const float SizeTolerance = 0.05f;

    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;

    [SetUp]
    public void SetUp()
    {
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
    }

    [TearDown]
    public void TearDown()
    {
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
    }

    [UnityTest]
    public IEnumerator K1_SongSelect_NoRecordShowsEmptySlot_ApRecordShowsApAndFullCombo()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        if (Mouse.current != null) InputSystem.DisableDevice(Mouse.current);
        yield return WaitManagers();

        // 메모리 기록·마지막 선택을 바꾸기 전 상태 (끝나면 되돌린다)
        string filePath = GameManager.PlayerData.FilePath;
        byte[] savedFile = File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;
        PlayerRecordBook book = RecordBook();
        List<ChartRecordData> records = book.Records;
        int savedIndex = records.FindIndex(r => r.chartId == TestCharts.OfficialChartId);
        ChartRecordData savedRecord = book.Find(TestCharts.OfficialChartId);
        LastSelectionSaveData last = GameManager.PlayerData.LastSelection;
        LastSelectionSaveData savedLast = last.Clone();
        ChartRecordData apRecord = new ChartRecordData
        {
            chartId = TestCharts.OfficialChartId, songId = TestCharts.OfficialSongId, chartType = ChartType.Official.ToString(),
            bestScore = GameConstants.Play.MaxScore, bestRank = PlayRank.AP.ToString(), bestAccuracy = 100, maxCombo = 1104,
            isFullCombo = true, isAllPerfect = true, playCount = 1
        };

        try
        {
            last.official.songId = TestCharts.OfficialSongId;
            last.official.chartId = TestCharts.OfficialChartId;

            // 기록 없음
            book.Remove(TestCharts.OfficialChartId);
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
            Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
            Assert.AreEqual(TestCharts.OfficialChartId, songSelect.ChosenChart.chartId);
            Img_Rank cardRank = songSelect.RecordRank;
            Assert.IsFalse(cardRank.gameObject.activeSelf, "기록 없음 → Img_Rank 숨김");
            Assert.IsNotNull(cardRank.EmptyMark, "빈 자리 연결");
            Assert.AreEqual("Img_RankEmpty", cardRank.EmptyMark.name);
            Assert.IsTrue(cardRank.EmptyMark.activeInHierarchy, "기록 없음 → Img_RankEmpty 보임");
            Assert.IsFalse(songSelect.RecordFullCombo.IsShown, "기록 없음 → FC 없음");
            Img_Rank listRank = NormalColumnRank(songSelect);
            Assert.IsFalse(listRank.gameObject.activeSelf, "목록: 기록 없음 → 랭크 이미지 숨김");
            Assert.IsNull(listRank.EmptyMark, "목록: 기록 없음 → 빈칸 (— 없음)");
            Assert.AreEqual(GameConstants.SongSelect.NotPlayed, songSelect.ScoreText, "기록 없음 → NOT PLAYED");
            StringAssert.StartsWith(GameConstants.SongSelect.EmptyValue, songSelect.ComboText, "MAX COMBO ― / 최대 콤보");
            StringAssert.Contains($"/ {songSelect.ChosenChart.maxCombo}", songSelect.ComboText);

            // AP 기록
            PutRecord(book, apRecord, records.Count);
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
            songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
            cardRank = songSelect.RecordRank;
            Assert.IsTrue(cardRank.gameObject.activeInHierarchy, "AP 기록 → Img_Rank 보임");
            Assert.IsFalse(cardRank.EmptyMark.activeSelf, "AP 기록 → 빈 자리 숨김");
            Assert.AreEqual("Rank_AP", cardRank.Sprite.name);
            Assert.AreEqual(116f, cardRank.Size.x, SizeTolerance, "AP는 랭크 칸 폭 116에 맞춤");
            Assert.LessOrEqual(cardRank.Size.y, 84f + SizeTolerance);
            Assert.IsTrue(songSelect.RecordFullCombo.IsShown, "AP면 FC도 보임");
            Assert.AreEqual(30f, songSelect.RecordFullCombo.Size.x, SizeTolerance, "FC 폭 30");
            listRank = NormalColumnRank(songSelect);
            Assert.IsTrue(listRank.gameObject.activeInHierarchy, "목록 AP 랭크 보임");
            Assert.AreEqual("Rank_AP", listRank.Sprite.name);
            Assert.AreEqual(18f, listRank.Size.y, SizeTolerance, "목록 랭크 높이 18");

            Debug.Log($"[RankDisplayPlayTest] K1 기록 카드 AP {cardRank.Size.x:0.0}×{cardRank.Size.y:0.0}, FC {songSelect.RecordFullCombo.Size.x:0.0}×{songSelect.RecordFullCombo.Size.y:0.0}, 목록 AP {listRank.Size.x:0.0}×{listRank.Size.y:0.0}");
        }
        finally
        {
            book.Remove(TestCharts.OfficialChartId);
            if (savedRecord != null) PutRecord(book, savedRecord, Mathf.Min(savedIndex, records.Count));
            last.CopyFrom(savedLast);
            if (savedFile != null && File.Exists(filePath) && !SameBytes(savedFile, File.ReadAllBytes(filePath)))
            {
                File.WriteAllBytes(filePath, savedFile);
                Debug.Log("[RankDisplayPlayTest] 테스트 중 저장된 PlayerData.json을 처음 내용으로 되돌림");
            }
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Mouse && !device.enabled) InputSystem.EnableDevice(device);
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K2_Result_ApShowsApAndFullCombo_SFullCombo_NotFullComboHidesFc()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult(Judges(4, 0, 0, 0)));
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();

        // AP: 모두 PERFECT → Rank_AP (높이 150, 가로로 김) + FC, 곡 끝 연출은 ALL PERFECT
        ScoreCalculator ap = Judges(4, 0, 0, 0);
        canvas.Init(CreateResult(ap), null, null);
        Assert.AreEqual(PlayRank.AP, ap.Rank);
        Assert.AreEqual("Rank_AP", canvas.RankImage.Sprite.name);
        Assert.AreEqual(150f, canvas.RankImage.Size.y, SizeTolerance, "높이 150");
        Assert.Greater(canvas.RankImage.Size.x, 200f, "AP는 가로로 김");
        Assert.LessOrEqual(canvas.RankImage.Size.x, 240f + SizeTolerance, "랭크 칸 240 안");
        Assert.IsTrue(canvas.FullComboImage.IsShown, "AP면 FC도");
        Assert.AreEqual(78f, canvas.FullComboImage.Size.x, SizeTolerance, "FC 폭 78");
        Assert.AreEqual(ClearType.AllPerfect, ClearBannerRules.Select(false, false, ap.PerfectCount, ap.MissCount, ap.JudgedCount, ap.TotalNotes), "곡 끝 연출 ALL PERFECT");
        string apLog = $"AP {canvas.RankImage.Size.x:0.0}×{canvas.RankImage.Size.y:0.0}, FC {canvas.FullComboImage.Size.x:0.0}×{canvas.FullComboImage.Size.y:0.0}";

        // S · FC: 7개 중 GREAT 1개 = 957142 → Rank_S 150×150 + FC, 곡 끝 연출 FULL COMBO
        ScoreCalculator s = Judges(6, 1, 0, 0);
        canvas.Init(CreateResult(s), null, null);
        Assert.AreEqual(PlayRank.S, s.Rank);
        Assert.AreEqual("Rank_S", canvas.RankImage.Sprite.name);
        Assert.AreEqual(150f, canvas.RankImage.Size.x, SizeTolerance, "S는 정사각형");
        Assert.AreEqual(150f, canvas.RankImage.Size.y, SizeTolerance);
        Assert.IsTrue(canvas.FullComboImage.IsShown, "S · FC → FC 보임");
        Assert.AreEqual(ClearType.FullCombo, ClearBannerRules.Select(false, false, s.PerfectCount, s.MissCount, s.JudgedCount, s.TotalNotes));

        // FC 아님 (MISS 1개) → FC 숨김
        ScoreCalculator miss = Judges(3, 0, 0, 1);
        canvas.Init(CreateResult(miss), null, null);
        Assert.AreEqual("Rank_" + miss.Rank, canvas.RankImage.Sprite.name);
        Assert.IsFalse(canvas.FullComboImage.IsShown, "FC 아님 → FC 숨김");

        Debug.Log($"[RankDisplayPlayTest] K2 결과 {apLog}, S {canvas.RankImage.Size.x:0.0}, MISS → {miss.Rank} FC 숨김");
        yield return new ExitPlayMode();
    }

    // ───── 도우미 ─────

    /// <summary>PlayerDataManager가 들고 있는 기록 목록 (메모리에서만 바꾸려고 꺼낸다)</summary>
    private static PlayerRecordBook RecordBook()
    {
        FieldInfo field = typeof(PlayerDataManager).GetField("book", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "PlayerDataManager.book");
        return (PlayerRecordBook)field.GetValue(GameManager.PlayerData);
    }

    /// <summary>기록 목록과 찾기용 Dictionary에 기록 하나를 넣는다 (메모리만)</summary>
    private static void PutRecord(PlayerRecordBook book, ChartRecordData record, int index)
    {
        book.Records.Insert(index, record);
        FieldInfo field = typeof(PlayerRecordBook).GetField("byChartId", BindingFlags.NonPublic | BindingFlags.Instance);
        ((Dictionary<string, ChartRecordData>)field.GetValue(book))[record.chartId] = record;
    }

    private static Img_Rank NormalColumnRank(Canvas_SongSelect songSelect)
    {
        foreach (Btn_SongItem item in songSelect.SongItems)
        {
            if (item.Song.SongId != TestCharts.OfficialSongId) continue;
            foreach (Btn_SongItem.DifficultyColumn column in item.DifficultyColumns)
            {
                if (column.difficulty == GameConstants.Chart.DifficultyNormal) return column.rank;
            }
        }
        Assert.Fail("Critical Velocity NORMAL 칸이 없습니다");
        return null;
    }

    private static ScoreCalculator Judges(int perfect, int great, int good, int miss)
    {
        ScoreCalculator score = new ScoreCalculator(perfect + great + good + miss);
        for (int i = 0; i < perfect; i++) score.Add(JudgeType.Perfect);
        for (int i = 0; i < great; i++) score.Add(JudgeType.Great);
        for (int i = 0; i < good; i++) score.Add(JudgeType.Good);
        for (int i = 0; i < miss; i++) score.Add(JudgeType.Miss);
        return score;
    }

    /// <summary>테스트 플레이 결과 (기록을 남기지 않음). 돌아가기·다시 하기는 이 테스트에서 누르지 않는다</summary>
    private static ResultData CreateResult(ScoreCalculator score)
    {
        SongData song = GameManager.Songs.Find(TestCharts.OfficialSongId);
        ResultData data = ResultData.FromScore(score);
        data.songId = TestCharts.OfficialSongId;
        data.songTitle = song != null ? song.Title : "Critical Velocity";
        data.artist = song != null ? song.Artist : string.Empty;
        data.cover = song != null ? song.Cover : null;
        data.background = song != null ? song.Background : null;
        data.chartType = ChartType.Official;
        data.difficulty = GameConstants.Chart.DifficultyNormal;
        data.level = 7;
        data.isTestPlay = true;
        data.returnTarget = new ResultReturnTarget
        {
            sceneName = GameConstants.Scenes.SongSelect,
            sceneData = new SongSelectEntryData { songId = TestCharts.OfficialSongId },
            buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
        };
        return data;
    }

    private static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static IEnumerator WaitManagers()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        yield return WaitManagers();
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        float deadline = TestSpeed.Deadline(20f);
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }
}
