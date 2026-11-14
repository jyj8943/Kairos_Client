using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 조작 키 흐름 (기획서 3-9): 실제 키보드 상태 이벤트로 키를 눌러 화면이 넘어가는지 본다 (EnterPlayMode 방식, 기존 PlayTests와 같음).
/// - K1 곡 선택 2 꾹 누르기 → 지연 뒤 계속 오름, 떼면 멈춤
/// - K2 결과 Enter → 곡 선택 (Enter 한 번에 돌아가기 1번, 다시 하기 0번. 버튼이 선택돼 있어도 같음)
/// - K3 결과 F5 → 인게임, K4 결과 ESC → 곡 선택
/// - K5 타이틀 ESC → 종료 확인 팝업 (ESC 한 번 더 → 닫힘)
/// - K6 인게임 F5 즉시 다시 하기: 플레이 중·일시정지 중은 됨, 확인 팝업이 맨 위면 안 됨. 시작·다시 하기 뒤 판정 패널 정확도 0.00%
/// 인게임·결과는 테스트 플레이 데이터로 들어가 기록(PlayerData.json)을 남기지 않는다.
/// </summary>
public class KeyControlPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

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
    public IEnumerator K1_SongSelectHold2_RepeatsAfterDelay_StopsOnRelease()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        CollectionAssert.AreEqual(new[] { "↑↓ 곡", "←→ 난이도", "Tab 탭", "1 2 배속", "Enter 시작", "ESC 타이틀" }, songSelect.KeyGuide.Describe());

        // 바닥(1.0)에서 시작하도록 1을 꾹 누른다 (저장된 마지막 배속에서 시작하므로 최고 8.0 → 1.0도 넉넉히: 0.4 + 69 × 0.08 ≈ 6초)
        float start = songSelect.ScrollMultiplier;
        Press(Key.Digit1);
        float deadline = Time.realtimeSinceStartup + 8f;
        while (songSelect.ScrollMultiplier > 1f + 1e-4f)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"1을 꾹 누르면 최저 배속까지 내려간다 (x{start:0.0} → x{songSelect.ScrollMultiplier:0.0})");
            yield return null;
        }
        ReleaseAll();
        yield return null;
        Assert.AreEqual(1f, songSelect.ScrollMultiplier, 1e-4f);

        Press(Key.Digit2);
        yield return null;
        Assert.AreEqual(1.1f, songSelect.ScrollMultiplier, 1e-4f, "누른 순간 한 번");
        yield return WaitRealtime((float)GameConstants.Play.SpeedKeyRepeatDelaySeconds * 0.5f);
        Assert.AreEqual(1.1f, songSelect.ScrollMultiplier, 1e-4f, "지연 시간 전에는 그대로");
        yield return WaitRealtime((float)GameConstants.Play.SpeedKeyRepeatDelaySeconds * 0.5f + (float)GameConstants.Play.SpeedKeyRepeatIntervalSeconds * 4.5f);
        float held = songSelect.ScrollMultiplier;
        Assert.GreaterOrEqual(held, 1.5f - 1e-4f, $"지연 뒤 계속 오른다 (x{held:0.0})");
        Assert.IsTrue(songSelect.TestIsSpeedKeyHeld(true));

        ReleaseAll();
        yield return null;
        float released = songSelect.ScrollMultiplier;
        yield return WaitRealtime(0.4f);
        Assert.AreEqual(released, songSelect.ScrollMultiplier, 1e-4f, "떼면 멈춘다");
        Assert.IsFalse(songSelect.TestIsSpeedKeyHeld(true));
        Debug.Log($"[KeyControlPlayTest] K1: 꾹 누른 뒤 x{held:0.0}, 뗀 뒤 x{released:0.0}");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K2_ResultEnter_ReturnsOnce_EvenIfButtonSelected()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult());
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();
        Assert.IsNull(EventSystem.current.currentSelectedGameObject, "결과 화면에 들어올 때 버튼을 미리 선택하지 않는다");
        CollectionAssert.AreEqual(new[] { "F5 다시 하기", "Enter 곡 선택", "ESC 곡 선택" }, canvas.KeyGuide.Describe());

        // 다른 코드가 다시 하기 버튼을 선택해 둔 경우: 다음 프레임에 풀리므로 Enter는 돌아가기 하나만
        GameObject retryButton = ((UnityEngine.UI.Button)typeof(Canvas_Result).GetField("retryButton", Private).GetValue(canvas)).gameObject;
        EventSystem.current.SetSelectedGameObject(retryButton);
        yield return null;
        yield return null;
        Assert.IsNull(EventSystem.current.currentSelectedGameObject, "선택된 버튼은 풀어 둔다");

        yield return Tap(Key.Enter);
        Assert.AreEqual(1, canvas.TestReturnCount, "Enter 한 번에 돌아가기 한 번");
        Assert.AreEqual(0, canvas.TestRetryCount, "Submit으로 다시 하기가 같이 실행되지 않는다");
        yield return WaitScene(GameConstants.Scenes.SongSelect);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K3_ResultF5_Retries()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult());
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();

        yield return Tap(Key.F5);
        Assert.AreEqual(1, canvas.TestRetryCount);
        Assert.AreEqual(0, canvas.TestReturnCount);
        yield return WaitInGamePlaying();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K4_ResultEsc_ReturnsToSongSelect()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.Result, CreateResult());
        Canvas_Result canvas = Object.FindFirstObjectByType<Canvas_Result>();

        yield return Tap(Key.Escape);
        Assert.AreEqual(1, canvas.TestReturnCount);
        yield return WaitScene(GameConstants.Scenes.SongSelect);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K5_TitleEsc_OpensQuitConfirm()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.Title, null);
        Canvas_Title title = Object.FindFirstObjectByType<Canvas_Title>();
        CollectionAssert.AreEqual(new[] { "↑↓ 이동", "Enter 선택", "ESC 종료" }, title.KeyGuide.Describe());
        Assert.IsFalse(GameManager.UI.HasOpenPopup);

        yield return Tap(Key.Escape);
        Assert.IsTrue(GameManager.UI.TopPopup is Popup_Confirm, "ESC → 종료 확인 팝업");

        yield return Tap(Key.Escape);
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업이 열려 있으면 ESC는 팝업 취소");
        Assert.AreEqual(GameConstants.Scenes.Title, SceneManager.GetActiveScene().name, "게임은 끝나지 않는다");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator K6_InGameF5_PlayingAndPaused_ButNotUnderConfirm()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        yield return LoadAndWait(GameConstants.Scenes.InGame, CreateTestPlayEntry());
        yield return WaitInGamePlaying();

        // 플레이 중: 실제 F5 키
        InGameScene first = Object.FindFirstObjectByType<InGameScene>();
        Assert.AreEqual("0.00%", AccuracyText(first), "테스트 플레이 시작: 판정 전 정확도 0%");
        yield return Tap(Key.F5);
        yield return WaitNewInGame(first);
        Assert.AreEqual("0.00%", AccuracyText(Object.FindFirstObjectByType<InGameScene>()), "F5 다시 하기 뒤에도 0%");

        // 일시정지 메뉴가 맨 위: 다시 하기 됨
        InGameScene second = Object.FindFirstObjectByType<InGameScene>();
        second.OnBack();
        yield return null;
        Assert.IsTrue(GameManager.UI.TopPopup is Popup_Pause, "ESC → 일시정지 메뉴");
        Popup_Pause pause = (Popup_Pause)GameManager.UI.TopPopup;
        CollectionAssert.AreEqual(new[] { "↑↓ 이동", "Enter 선택", "F5 다시 하기", "ESC 계속" }, pause.KeyGuide.Describe());

        // 확인 팝업(재시작 확인)이 맨 위면 F5를 받지 않는다
        typeof(InGameScene).GetMethod("OnClickRestart", Private).Invoke(second, null);
        yield return null;
        Assert.IsTrue(GameManager.UI.TopPopup is Popup_Confirm);
        Assert.IsFalse(second.TestPressRestartKey(), "확인 팝업이 맨 위면 F5 무시");
        GameManager.UI.TopPopup.OnCancelRequested();
        yield return null;
        Assert.IsTrue(GameManager.UI.TopPopup is Popup_Pause, "취소하면 일시정지 메뉴로");

        yield return Tap(Key.F5);
        yield return WaitNewInGame(second);
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "다시 시작하면 팝업이 닫혀 있다");
        Assert.AreEqual("0.00%", AccuracyText(Object.FindFirstObjectByType<InGameScene>()), "일시정지 중 F5 뒤에도 0%");
        yield return new ExitPlayMode();
    }

    /// <summary>판정 패널 정확도 글자 (InGameScene → Canvas_InGame → Img_JudgePanel)</summary>
    private static string AccuracyText(InGameScene scene)
    {
        object canvas = typeof(InGameScene).GetField("canvas", Private).GetValue(scene);
        object panel = canvas.GetType().GetField("judgePanel", Private).GetValue(canvas);
        return ((TMPro.TMP_Text)panel.GetType().GetField("accuracy", Private).GetValue(panel)).text;
    }

    // ───── 준비 ─────

    private static void PrepareInput()
    {
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    private static ChartData CreateChart()
    {
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = "조작 키 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 64f, lane = 0, type = NoteData.TypeShort } }
        };
    }

    /// <summary>테스트 플레이 진입 데이터 (파일을 읽지 않고, 기록·마지막 배속을 저장하지 않는다)</summary>
    private static InGameEntryData CreateTestPlayEntry()
    {
        ChartData chart = CreateChart();
        return new InGameEntryData
        {
            songId = chart.songId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = chart.songId, chart = chart, startBeat = 0f }
        };
    }

    /// <summary>결과 데이터: 다시 하기는 테스트 플레이(기록 안 남김), 돌아가기는 곡 선택</summary>
    private static ResultData CreateResult()
    {
        ScoreCalculator score = new ScoreCalculator(1);
        score.Add(JudgeType.Perfect);
        ResultData data = ResultData.FromScore(score);
        data.songTitle = "Critical Velocity";
        data.chartName = "조작 키 확인";
        data.level = 1;
        data.isTestPlay = false;
        data.retryEntry = CreateTestPlayEntry();
        data.returnTarget = new ResultReturnTarget
        {
            sceneName = GameConstants.Scenes.SongSelect,
            sceneData = new SongSelectEntryData { songId = TestCharts.OfficialSongId },
            buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
        };
        return data;
    }

    // ───── 키 ─────

    private static Keyboard Board
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            Assert.IsNotNull(keyboard, "키보드 장치가 없습니다");
            return keyboard;
        }
    }

    private static void Press(Key key)
    {
        InputSystem.QueueStateEvent(Board, new KeyboardState(key));
        InputSystem.Update();
    }

    private static void ReleaseAll()
    {
        InputSystem.QueueStateEvent(Board, new KeyboardState());
        InputSystem.Update();
    }

    private static IEnumerator Tap(Key key)
    {
        Press(key);
        yield return null;
        ReleaseAll();
        yield return null;
    }

    private static IEnumerator Hold(Key key, float seconds)
    {
        Press(key);
        yield return WaitRealtime(seconds);
        ReleaseAll();
        yield return null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    // ───── 씬 ─────

    private static IEnumerator WaitManagers()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
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
        yield return WaitScene(sceneName);
    }

    private static IEnumerator WaitScene(string sceneName)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }

    private static IEnumerator WaitInGamePlaying()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && (bool)typeof(InGameScene).GetField("isPlaying", Private).GetValue(scene)) break;
            yield return null;
        }
    }

    /// <summary>F5로 인게임을 다시 불러와 새 InGameScene이 플레이를 시작할 때까지</summary>
    private static IEnumerator WaitNewInGame(InGameScene previous)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 다시 시작 대기 시간 초과");
            InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && scene != previous && !GameManager.Scene.IsLoading && (bool)typeof(InGameScene).GetField("isPlaying", Private).GetValue(scene)) break;
            yield return null;
        }
    }
}
