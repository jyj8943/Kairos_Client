using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 인게임 콤보 애니메이션 실제 씬 확인 (ScrollSpeedPlayTests와 같은 방식: EnterPlayMode + Bootstrapper, 코드로 만든 채보, 자동 플레이).
/// - 플레이 중 콤보가 오르면 보이고, 일시정지·재개 카운트다운 중에는 애니메이션·쉬는 시간이 멈춘다 (콤보가 사라지지 않는다).
/// - 모든 노트를 판정한 뒤 결과 대기 중에도 Tick이 흘러 쉬면 숨기기로 사라진다.
/// - 재시작하면 처음 상태 (콤보 0, 숨김).
/// </summary>
public class ComboPlayTests
{
    private const string Song = "critical_velocity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private InGameScene scene;
    private NoteJudge judge;
    private ScoreCalculator score;
    private Txt_Combo combo;
    private Canvas_InGame canvas;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        scene = null;
        yield return new ExitPlayMode();
    }

    private static NoteData Short(float beat, int lane) => new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };

    /// <summary>6박부터 1/2박마다 숏노트 16개 (약 3.2초)</summary>
    private static ChartData Chart()
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 16; i++) notes.Add(Short(6f + i * 0.5f, i % 4));
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "콤보 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    private static InGameEntryData Entry()
    {
        return new InGameEntryData
        {
            songId = Song,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = Chart(), startBeat = 0f, autoPlay = true }
        };
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private void Call(string method, params object[] args)
    {
        MethodInfo info = typeof(InGameScene).GetMethod(method, Private);
        Assert.IsNotNull(info, $"함수 {method}가 없습니다");
        info.Invoke(scene, args);
    }

    private IEnumerator LoadInGame()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, Entry()));
        yield return WaitStarted();
    }

    private IEnumerator WaitStarted()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        judge = Get<NoteJudge>(scene, "judge");
        score = Get<ScoreCalculator>(scene, "score");
        canvas = Get<Canvas_InGame>(scene, "canvas");
        combo = canvas.Combo;
    }

    private IEnumerator WaitCombo(int target)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (score.Combo < target)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"콤보 {target} 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    /// <summary>C1: 오르면 보임 → 일시정지 1.5초 동안 그대로 → 재개 카운트다운 동안도 그대로 → 결과 대기 중 쉬면 숨김</summary>
    [UnityTest]
    public IEnumerator C1_PauseFreezesAndIdleHidesAfterAllJudged()
    {
        yield return LoadInGame();
        Assert.AreEqual(0, combo.Motion.Combo, "시작: 콤보 0");
        Assert.AreEqual(0f, combo.Group.alpha, 1e-4f, "시작: 숨김");

        yield return WaitCombo(4);
        Assert.AreEqual(score.Combo, combo.Motion.Combo, "화면 콤보 = 점수 콤보");
        Assert.AreEqual(1f, combo.Group.alpha, 1e-4f, "오르면 보임");

        // 오름 애니메이션 도중에 멈추게 몇 프레임 흘린다
        yield return WaitRealtime(0.03f);
        Assert.Greater(combo.Motion.Elapsed, 0f, "Tick이 흐른다");
        Call("RequestPause", "콤보 테스트");
        Assert.IsTrue(Get<bool>(scene, "isPaused"), "일시정지됨");
        float pausedElapsed = combo.Motion.Elapsed;
        float pausedJudgeElapsed = canvas.Judge.Animation.Elapsed;
        int pausedCombo = combo.Motion.Combo;
        yield return WaitRealtime(1.5f);
        Assert.AreEqual(pausedElapsed, combo.Motion.Elapsed, 1e-6f, "일시정지 중 시간이 흐르지 않음");
        Assert.AreEqual(pausedJudgeElapsed, canvas.Judge.Animation.Elapsed, 1e-6f, "판정 글자 애니메이션도 멈춤");
        Assert.IsTrue(canvas.Judge.IsVisible, "일시정지 1.5초 동안 판정 글자가 사라지지 않음");
        Assert.AreEqual(pausedCombo, combo.Motion.Combo);
        Assert.AreEqual(1f, combo.Group.alpha, 1e-4f, "일시정지 중 콤보가 사라지지 않음");

        Call("OnClickResume");
        yield return WaitRealtime(GameConstants.Pause.CountdownSeconds - 0.5f);
        Assert.IsTrue(Get<bool>(scene, "isPaused"), "아직 카운트다운 중");
        Assert.AreEqual(pausedElapsed, combo.Motion.Elapsed, 1e-6f, "카운트다운 중에도 멈춤");
        Assert.AreEqual(pausedJudgeElapsed, canvas.Judge.Animation.Elapsed, 1e-6f, "카운트다운 중 판정 글자도 멈춤");
        Assert.AreEqual(1f, combo.Group.alpha, 1e-4f);

        float deadline = Time.realtimeSinceStartup + 20f;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            yield return null;
        }
        Assert.AreEqual(judge.TotalCount, combo.Motion.Combo, "마지막 콤보 표시");
        Assert.AreEqual(1f, combo.Group.alpha, 1e-4f, "마지막 판정 직후에는 보임");

        // 결과로 가기 전 3초 동안 Tick이 계속 흐른다 → 0.74초 뒤 숨김
        yield return WaitRealtime(1.0f);
        Assert.IsFalse(Get<bool>(scene, "isFinished"), "아직 결과 대기 중");
        Assert.AreEqual(0f, combo.Group.alpha, 1e-4f, "결과 대기 중 쉬면 숨김");
        Debug.Log($"[ComboPlayTest] C1: 일시정지 때 콤보 {pausedCombo} · 경과 {pausedElapsed:0.000}초 유지, 마지막 콤보 {combo.Motion.Combo}, 결과 대기 1초 뒤 그룹 알파 {combo.Group.alpha:0.00}");
    }

    /// <summary>C2: 재시작하면 처음 상태 (콤보 0, 숨김, 숫자 제자리)</summary>
    [UnityTest]
    public IEnumerator C2_RestartResetsView()
    {
        yield return LoadInGame();
        yield return WaitCombo(3);
        Assert.Greater(combo.Motion.Combo, 0);

        Call("Restart");
        scene = null;
        yield return null;
        yield return WaitStarted();
        Assert.AreEqual(0, combo.Motion.Combo, "재시작: 콤보 0");
        Assert.AreEqual(0f, combo.Group.alpha, 1e-4f, "재시작: 숨김");
        Assert.AreEqual(0f, ((RectTransform)combo.transform).anchoredPosition.y, 1e-3f, "재시작: 숫자 제자리");
        Debug.Log("[ComboPlayTest] C2: 재시작 뒤 콤보 0 · 숨김");
    }
}
