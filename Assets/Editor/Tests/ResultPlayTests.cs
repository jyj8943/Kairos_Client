using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 결과 화면 실제 씬 확인 (기획서 3-5 결과 11/17, EnterPlayMode 방식). 결과 씬에 ResultData를 직접 넣는다.
/// 다시 하기·돌아가기 데이터는 테스트 플레이(기록을 남기지 않음)로 만든다. 표시 확인은 들어온 뒤 Canvas_Result.Init을 원하는 결과로 다시 부른다
/// (결과 씬은 들어올 때 기록을 갱신하며 신기록 여부를 덮어쓰므로).
/// - R1 나만의 채보 결과 → CUSTOM 뱃지 + 채보 이름, 공식 결과 → 난이도 뱃지(메뉴용 색), 채보 이름 숨김
/// - R2 정확도만 신기록인 결과 → RATE 옆에만 NEW RECORD
/// - R3 F5 → 곡 선택 시작과 같은 경로(InGameLauncher.Start)로 인게임, 다시 들어와 Enter → 곡 선택, 테스트 플레이 결과 Enter → 에디터
/// </summary>
public class ResultPlayTests
{
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
    public IEnumerator R1_CustomShowsCustomBadgeAndName_OfficialShowsDifficultyBadge()
    {
        yield return new EnterPlayMode();
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult(true));
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();
        UITheme theme = UITheme.Current;

        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, canvas.ChartBadgeLabel, "나만의 채보 → CUSTOM 뱃지");
        Assert.AreEqual((Color32)theme.difficultyCustom, (Color32)canvas.ChartBadgeColor, "CUSTOM #8B7FD1");
        Assert.IsTrue(canvas.IsChartNameShown, "채보 이름 표시");
        Assert.AreEqual("결과 확인용 채보", canvas.ChartNameLabel);
        Assert.IsNotNull(Object.FindFirstObjectByType<MenuBackground>().Blur.FrontImage.texture, "배경 일러스트");

        ResultData official = CreateResult(false);
        official.chartType = ChartType.Official;
        official.difficulty = GameConstants.Chart.DifficultyNormal;
        canvas.Init(official, null, null);
        Assert.AreEqual(GameConstants.Chart.DifficultyNormal, canvas.ChartBadgeLabel, "공식 → 난이도 뱃지");
        Assert.AreEqual((Color32)theme.difficultyNormal, (Color32)canvas.ChartBadgeColor, "메뉴용 NORMAL #2F7FC8");
        Assert.IsFalse(canvas.IsChartNameShown, "공식 채보는 채보 이름 숨김");
        Assert.AreEqual(0.75f, canvas.FastRatio, 1e-5f, "FAST 30 : SLOW 10");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator R2_AccuracyOnlyNewRecord_BadgeOnlyNextToRate()
    {
        yield return new EnterPlayMode();
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult(false));
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();

        ResultData result = CreateResult(false);
        result.hadPreviousRecord = true;
        result.previousBestScore = 960000;
        result.isNewBestScore = false;
        result.isNewBestAccuracy = true;
        result.isNewMaxCombo = false;
        canvas.Init(result, null, null);
        Assert.IsFalse(canvas.IsNewScoreShown, "점수 옆 NEW RECORD 없음");
        Assert.IsTrue(canvas.IsNewRateShown, "RATE 옆에만 NEW RECORD");
        Assert.IsFalse(canvas.IsNewComboShown, "MAX COMBO 옆 NEW RECORD 없음");

        result.isTestPlay = true;
        canvas.Init(result, null, null);
        Assert.IsFalse(canvas.IsNewRateShown, "테스트 플레이는 NEW RECORD를 띄우지 않음");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator R3_F5UsesLauncher_EnterReturnsToSongSelect_TestPlayEnterToEditor()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        try
        {
            // F5 → 곡 선택 시작과 같은 경로
            ResultData result = CreateResult(false);
            yield return LoadAndWait(GameConstants.Scenes.Result, result);
            Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();
            CollectionAssert.AreEqual(new[] { "F5 다시 하기", "Enter ESC 곡 선택으로" }, canvas.KeyGuide.Describe());
            Assert.AreEqual(GameConstants.KeyGuide.ToSongSelect, canvas.ReturnLabel);
            int starts = InGameLauncher.StartCount;
            yield return Tap(Key.F5);
            Assert.AreEqual(starts + 1, InGameLauncher.StartCount, "다시 하기 = InGameLauncher.Start (곡 선택 시작과 같은 함수)");
            Assert.AreSame(result.retryEntry.testPlay.chart, InGameLauncher.LastStartedEntry.testPlay.chart, "같은 채보로 다시 하기");
            Assert.AreNotSame(result.retryEntry, InGameLauncher.LastStartedEntry, "다시 하기 데이터의 복사본으로 시작");
            yield return WaitScene(GameConstants.Scenes.InGame);

            // Enter → 곡 선택
            yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult(false));
            yield return Tap(Key.Enter);
            yield return WaitScene(GameConstants.Scenes.SongSelect);

            // 테스트 플레이 결과 Enter → 에디터
            ResultData testPlay = CreateResult(true);
            yield return LoadAndWait(GameConstants.Scenes.Result, testPlay);
            canvas = Object.FindFirstObjectByType<Canvas_Result>();
            Assert.AreEqual(GameConstants.KeyGuide.ToEditor, canvas.ReturnLabel, "테스트 플레이 → 에디터로");
            CollectionAssert.AreEqual(new[] { "F5 다시 하기", "Enter ESC 에디터로" }, canvas.KeyGuide.Describe());
            yield return Tap(Key.Enter);
            yield return WaitScene(GameConstants.Scenes.Editor);
        }
        finally
        {
            ReleaseAll();
        }
        yield return new ExitPlayMode();
    }

    // ───── 결과 데이터 ─────

    /// <summary>
    /// 결과 데이터. 다시 하기는 테스트 플레이(기록 안 남김). testPlayResult면 테스트 플레이 결과(돌아가기 = 에디터), 아니면 돌아가기 = 곡 선택.
    /// </summary>
    private static ResultData CreateResult(bool testPlayResult)
    {
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = "결과 확인용 채보",
            difficulty = string.Empty,
            level = 3,
            timingPoints = new System.Collections.Generic.List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new System.Collections.Generic.List<NoteData> { new NoteData { beat = 4f, lane = 0, type = NoteData.TypeShort } }
        };
        TestPlayData testPlay = new TestPlayData { songId = chart.songId, chart = chart, startBeat = 0f };
        InGameEntryData retry = new InGameEntryData
        {
            songId = chart.songId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = testPlay
        };

        SongData song = null;
        foreach (SongData candidate in GameManager.Songs.Songs) if (candidate.SongId == TestCharts.OfficialSongId) song = candidate;

        ScoreCalculator score = new ScoreCalculator(1);
        score.Add(JudgeType.Perfect);
        ResultData data = ResultData.FromScore(score);
        data.songId = chart.songId;
        data.songTitle = song != null ? song.Title : "Critical Velocity";
        data.artist = song != null ? song.Artist : string.Empty;
        data.cover = song != null ? song.Cover : null;
        data.background = song != null ? song.Background : null;
        data.chartType = ChartType.Custom;
        data.chartName = chart.chartName;
        data.level = chart.level;
        data.scrollMultiplier = 5.5f;
        data.fastCount = 30;
        data.slowCount = 10;
        data.isTestPlay = testPlayResult;
        data.retryEntry = retry;
        data.returnTarget = testPlayResult
            ? new ResultReturnTarget { sceneName = GameConstants.Scenes.Editor, sceneData = testPlay.CreateEditorEntry(1f), buttonLabel = GameConstants.Play.ReturnToEditorLabel }
            : new ResultReturnTarget
            {
                sceneName = GameConstants.Scenes.SongSelect,
                sceneData = new SongSelectEntryData { songId = TestCharts.OfficialSongId },
                buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
            };
        return data;
    }

    // ───── 입력 · 씬 ─────

    private static void PrepareInput()
    {
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    private static IEnumerator Tap(Key key)
    {
        Assert.IsNotNull(Keyboard.current, "키보드 장치가 없습니다");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        ReleaseAll();
        yield return null;
    }

    private static void ReleaseAll()
    {
        if (Keyboard.current == null) return;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
    }

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        yield return WaitScene(sceneName);
    }

    private static IEnumerator WaitScene(string sceneName)
    {
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
