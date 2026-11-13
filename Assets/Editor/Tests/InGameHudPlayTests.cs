using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 인게임 배치 개정(기획서 3-4, 11/13) 실제 씬 확인 (ComboPlayTests와 같은 방식: EnterPlayMode + Bootstrapper, 코드로 만든 채보, 자동 플레이).
/// - 나만의 채보 테스트 플레이: CUSTOM 뱃지·채보 이름·Lv.·테스트 플레이(자동 플레이) 표시
/// - 곡 진행 바가 오르고, 일시정지 중에는 멈춘다
/// - 자동 플레이가 노트를 칠 때 키 칸이 밝아지고, 일시정지하면 꺼진다
/// - 일찍 쳐 GREAT면 판정 글자 위 FAST와 판정 패널 GREAT·FAST 개수가 오르고, 다음 PERFECT에서 FAST가 지워진다
/// </summary>
public class InGameHudPlayTests
{
    private const string Song = "critical_velocity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private InGameScene scene;
    private Canvas_InGame canvas;
    private GearView gear;

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

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static InGameEntryData Entry(bool autoPlay = true)
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 24; i++) notes.Add(new NoteData { beat = 6f + i * 0.5f, lane = i % 4, type = NoteData.TypeShort });
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "배치 확인용 나만의 채보",
            keyCount = GameConstants.Editor.LaneCount,
            level = 7,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
        return new InGameEntryData
        {
            songId = Song,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = 0f, autoPlay = autoPlay }
        };
    }

    private IEnumerator Load(bool autoPlay = true)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, Entry(autoPlay)));
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        canvas = Get<Canvas_InGame>(scene, "canvas");
        gear = Get<GearView>(scene, "gear");
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    /// <summary>H1: 곡 정보 패널(나만의 채보·자동 테스트 플레이), 진행 바, 키 칸 눌림, 일시정지 중 멈춤</summary>
    [UnityTest]
    public IEnumerator H1_SongInfoProgressAndKeyCells()
    {
        yield return Load();

        Assert.AreEqual("CUSTOM", Get<TMP_Text>(canvas, "chartBadgeText").text);
        TMP_Text chartName = Get<TMP_Text>(canvas, "chartNameText");
        Assert.IsTrue(chartName.gameObject.activeInHierarchy, "나만의 채보 이름 보임");
        Assert.AreEqual("배치 확인용 나만의 채보", chartName.text);
        Assert.AreEqual("Lv.7", Get<TMP_Text>(canvas, "levelText").text);
        Assert.IsTrue(Get<GameObject>(canvas, "testPlayBadge").activeInHierarchy, "테스트 플레이 표시 켜짐");
        Assert.AreEqual(GameConstants.Play.AutoTestPlayLabel, Get<TMP_Text>(canvas, "testPlayText").text);
        StringAssert.StartsWith("배속 x1.0", Get<TMP_Text>(canvas, "speedText").text);

        // 첫 노트(약 2.4초) 뒤까지 기다리며 키 칸이 밝아지는지 본다
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        bool sawPressed = false;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (judge.JudgedCount < 8)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++) sawPressed |= gear.IsKeyCellPressed(lane);
            yield return null;
        }
        Assert.IsTrue(sawPressed, "자동 플레이가 칠 때 키 칸이 밝아짐");
        float progress = canvas.Progress;
        Assert.Greater(progress, 0f, "진행 바가 오름");
        Assert.Less(progress, 1f);

        MethodInfo pause = typeof(InGameScene).GetMethod("RequestPause", Private);
        pause.Invoke(scene, new object[] { "배치 테스트" });
        Assert.IsTrue(Get<bool>(scene, "isPaused"));
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
            Assert.IsFalse(gear.IsKeyCellPressed(lane), $"일시정지하면 키 칸 꺼짐 (라인 {lane})");
        float pausedProgress = canvas.Progress;
        yield return WaitRealtime(1f);
        Assert.AreEqual(pausedProgress, canvas.Progress, 1e-6f, "일시정지 중 진행 바 멈춤");

        Debug.Log($"[InGameHudPlayTest] H1: 뱃지 CUSTOM, 채보 이름 보임, 진행 바 {progress:0.000} → 일시정지 1초 뒤 {canvas.Progress:0.000}, 키 칸 눌림 봄 {sawPressed}");
    }

    /// <summary>H2: 일찍 쳐 GREAT → FAST 표시·판정 패널 GREAT 1·FAST 1·정확도, 다음 PERFECT → FAST 지움·PERFECT 1</summary>
    [UnityTest]
    public IEnumerator H2_FastSlowAndJudgePanel()
    {
        yield return Load(false);
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        Img_JudgePanel panel = Get<Img_JudgePanel>(canvas, "judgePanel");
        Assert.AreEqual(string.Empty, canvas.FastSlowShown, "시작: FAST/SLOW 없음");
        Assert.AreEqual("100.00%", Get<TMP_Text>(panel, "accuracy").text, "판정 전 정확도 100%");

        double greatEarly = (GameConstants.Play.DefaultPerfectMs + GameConstants.Play.DefaultGreatMs) * 0.5 / 1000.0;
        PlayNote first = judge.Notes[0];
        PlayNote second = judge.Notes[1];
        float deadline = Time.realtimeSinceStartup + 15f;
        while (GameManager.Sound.SongTime < first.Time - greatEarly)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "첫 노트 대기 시간 초과");
            yield return null;
        }
        Assert.IsFalse(Get<bool>(scene, "isPaused"), "에디터 포커스를 잃어 일시정지되면 안 된다");
        scene.TestPress(first.Lane, first.Time - greatEarly);
        Assert.AreEqual(GameConstants.Play.FastText, canvas.FastSlowShown, "GREAT 일찍 → FAST");
        StringAssert.Contains(">1<", Get<TMP_Text>(panel, "greatCount").text);
        StringAssert.Contains(">1<", Get<TMP_Text>(panel, "fastCount").text);
        StringAssert.Contains(">0<", Get<TMP_Text>(panel, "slowCount").text);
        Assert.AreEqual("70.00%", Get<TMP_Text>(panel, "accuracy").text, "GREAT 1개 → 70%");

        while (GameManager.Sound.SongTime < second.Time)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "둘째 노트 대기 시간 초과");
            yield return null;
        }
        scene.TestPress(second.Lane, second.Time);
        Assert.AreEqual(string.Empty, canvas.FastSlowShown, "다음 PERFECT에서 FAST 지움");
        StringAssert.Contains(">1<", Get<TMP_Text>(panel, "perfectCount").text);
        Assert.AreEqual("85.00%", Get<TMP_Text>(panel, "accuracy").text, "(100 + 70) ÷ 2");
        Debug.Log($"[InGameHudPlayTest] H2: GREAT FAST → 패널 {Get<TMP_Text>(panel, "greatCount").text} / {Get<TMP_Text>(panel, "fastCount").text}, PERFECT 뒤 FAST/SLOW '{canvas.FastSlowShown}', 정확도 {Get<TMP_Text>(panel, "accuracy").text}");
    }
}
