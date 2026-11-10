using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Play 종료 뒤 입력 구독이 남지 않는지 확인 (도메인 리로드를 끈 Enter Play Mode 설정).
/// 인게임에 들어가 있는 상태(OnExit가 불리지 않음)에서 Play를 끝내고 다시 Play → 곡 선택에서 실제 키 이벤트로 2를 누른다.
/// 남은 InGameScene 배속 키 구독이 있으면 InGameScene.TestSpeedKeyCallbackCount가 늘어난다.
/// 곡 선택 배속이 1.0 → 1.1로 바뀌는지로 키 이벤트가 액션에 닿았는지도 함께 확인한다.
/// </summary>
public class InputSubscriptionLeakTests
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

    /// <summary>R1: 인게임에서 Play 종료 → 다시 Play → 곡 선택에서 2 → InGameScene 콜백 0번</summary>
    [UnityTest]
    public IEnumerator R1_NoInGameCallbackAfterExitFromInGame()
    {
        List<string> log = new List<string>();

        // EnterPlayMode / ExitPlayMode는 테스트 메서드에서 직접 yield해야 테스트 러너가 처리한다 (중첩 코루틴 안에서는 무시된다)
        yield return new EnterPlayMode();
        PrepareInput();
        yield return EnterInGame();
        yield return new ExitPlayMode();

        yield return new EnterPlayMode();
        PrepareInput();
        yield return PressSpeedUpInSongSelect(log);
        yield return new ExitPlayMode();

        Debug.Log($"[InputLeakTest] R1: {string.Join(" | ", log)}");
    }

    /// <summary>R2: 위를 3번 반복해도 0번 (구독이 쌓이지 않는다)</summary>
    [UnityTest]
    public IEnumerator R2_RepeatedThreeTimes()
    {
        List<string> log = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            log.Add($"{i + 1}회차");
            yield return new EnterPlayMode();
            PrepareInput();
            yield return EnterInGame();
            yield return new ExitPlayMode();

            yield return new EnterPlayMode();
            PrepareInput();
            yield return PressSpeedUpInSongSelect(log);
            yield return new ExitPlayMode();
        }
        Debug.Log($"[InputLeakTest] R2: {string.Join(" | ", log)}");
    }

    /// <summary>인게임에 들어가 배속 키를 구독한 상태가 될 때까지 (이 뒤 OnExit 없이 Play를 끝낸다).</summary>
    private static IEnumerator EnterInGame()
    {
        yield return LoadInGame();
        InGameScene inGame = Object.FindFirstObjectByType<InGameScene>();
        Assert.IsTrue(Get<bool>(inGame, "isSpeedInputEnabled"), "인게임이 배속 키를 구독했다");
    }

    /// <summary>곡 선택에서 실제 키 이벤트로 2를 누르고, 곡 선택 배속 변화와 InGameScene 콜백 수를 확인한다.</summary>
    private static IEnumerator PressSpeedUpInSongSelect(List<string> log)
    {
        yield return LoadSongSelect();
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        float before = songSelect.ScrollMultiplier;

        InGameScene.TestSpeedKeyCallbackCount = 0;
        yield return TapKey(Key.Digit2);
        float after = songSelect.ScrollMultiplier;
        int callbacks = InGameScene.TestSpeedKeyCallbackCount;
        log.Add($"곡 선택 배속 x{before:0.0} → x{after:0.0}, InGameScene 콜백 {callbacks}번");

        Assert.AreEqual(before + 0.1f, after, 1e-4f, "키 이벤트가 곡 선택 배속 액션에 닿았다");
        Assert.AreEqual(0, callbacks, "Play 종료 뒤 남은 InGameScene 구독이 없다");
    }

    private static void PrepareInput()
    {
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    /// <summary>실제 키보드 상태 이벤트로 눌렀다 뗀다.</summary>
    private static IEnumerator TapKey(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        Assert.IsNotNull(keyboard, "키보드 장치가 없습니다");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private static IEnumerator WaitManagers()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator LoadInGame()
    {
        yield return WaitManagers();
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = "critical_velocity",
            type = ChartData.TypeCustom,
            chartName = "구독 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 8f, lane = 0, type = NoteData.TypeShort } }
        };
        InGameEntryData entry = new InGameEntryData
        {
            songId = chart.songId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = chart.songId, chart = chart, startBeat = 0f }
        };
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));

        float deadline = Time.realtimeSinceStartup + 20f;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
    }

    private static IEnumerator LoadSongSelect()
    {
        yield return WaitManagers();
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.SongSelect));
        yield return null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Object.FindFirstObjectByType<Canvas_SongSelect>() == null || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "곡 선택 대기 시간 초과");
            yield return null;
        }
        yield return null;
    }

    private static TValue Get<TValue>(object target, string field)
    {
        return (TValue)target.GetType().GetField(field, Private).GetValue(target);
    }
}
