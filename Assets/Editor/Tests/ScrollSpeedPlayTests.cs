using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 배속 변경 실제 씬 확인 (LongNotePlayTests와 같은 방식: EnterPlayMode + Bootstrapper, 코드로 만든 채보, TestPress·TestRelease).
/// 배속 키는 InGameScene.TestTapSpeedKey(키 콜백과 같은 PressSpeedKey 경로, 팝업 확인 포함)로 누른다.
/// </summary>
public class ScrollSpeedPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const double InjectLeadSeconds = 0.05;

    private InGameScene scene;
    private NoteJudge judge;
    private ScoreCalculator score;
    private NoteLayer layer;
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

    // ───── 준비 ─────

    private static NoteData Short(float beat, int lane) => new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };
    private static NoteData Long(float beat, float endBeat, int lane) => new NoteData { beat = beat, endBeat = endBeat, lane = lane, type = NoteData.TypeLong };

    private static ChartData Chart(List<NoteData> notes)
    {
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "배속 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    /// <summary>6박부터 1/2박마다 숏노트, 사이사이 롱노트 (약 8초)</summary>
    private static ChartData DenseChart()
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 40; i++) notes.Add(Short(6f + i * 0.5f, i % 3));
        for (int i = 0; i < 5; i++) notes.Add(Long(6f + i * 4f, 8f + i * 4f, 3));
        return Chart(notes);
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

    private IEnumerator LoadInGame(InGameEntryData entry)
    {
        yield return WaitManagers();
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        yield return WaitInGameStarted();
    }

    private IEnumerator WaitInGameStarted()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = UnityEngine.Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        judge = Get<NoteJudge>(scene, "judge");
        score = Get<ScoreCalculator>(scene, "score");
        layer = Get<NoteLayer>(scene, "noteLayer");
        gear = Get<GearView>(scene, "gear");
    }

    private static InGameEntryData TestPlayEntry(ChartData chart, bool autoPlay, float multiplier = 1f, EditorRestoreState restore = null)
    {
        return new InGameEntryData
        {
            songId = Song,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = multiplier,
            testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = 0f, autoPlay = autoPlay, editorState = restore }
        };
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static double SongTime => GameManager.Sound.SongTime;

    private static IEnumerator WaitSongTime(double time, float timeoutSeconds = 30f)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (SongTime < time)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"곡 시간 {time:0.000}초 대기 시간 초과");
            yield return null;
        }
    }

    /// <summary>배속 키를 한 번씩 눌러 목표 배속까지 바꾼다 (같은 프레임).</summary>
    private void TapTo(float target)
    {
        int guard = 0;
        while (Mathf.Abs(layer.ScrollMultiplier - target) > 0.001f && guard++ < 100)
            scene.TestTapSpeedKey(target > layer.ScrollMultiplier);
    }

    private float ExpectedSpeed(float multiplier) => ScrollSpeedRules.ScrollSpeed(gear.ScreenTopY - gear.JudgeLineY, multiplier);

    // ───── 항목 ─────

    /// <summary>S1·S2: 자동 플레이 중 1초마다 배속을 바꿔도 전부 PERFECT, 1,000,000점, GrowCount 0. 바꾼 다음 프레임 노트 y가 새 속도 공식과 같다</summary>
    [UnityTest]
    public IEnumerator S1_S2_ChangeDuringAutoPlay()
    {
        yield return LoadInGame(TestPlayEntry(DenseChart(), true));
        float[] targets = { 8f, 4f, 5.5f };
        double firstNote = judge.Notes[0].Time;
        double maxError = 0;
        int checkedNotes = 0;
        List<string> logs = new List<string>();

        for (int i = 0; i < targets.Length; i++)
        {
            yield return WaitSongTime(firstNote + i * 1.0);
            TapTo(targets[i]);
            Assert.AreEqual(targets[i], layer.ScrollMultiplier, 1e-4f, "배속 바뀜");
            yield return null; // 다음 프레임 Refresh

            // S2: 화면의 숏노트 y = 판정선 + (노트 시간 − Refresh 곡 시간) × 새 속도
            double refreshTime = layer.LastRefreshSongTime;
            NoteView[] views = Get<NoteView[]>(layer, "viewByNote");
            for (int n = 0; n < views.Length; n++)
            {
                if (views[n] == null || !views[n].gameObject.activeSelf) continue;
                double expected = gear.JudgeLineY + (judge.Notes[n].Time - refreshTime) * layer.ScrollSpeed;
                maxError = Math.Max(maxError, Math.Abs(views[n].transform.localPosition.y - expected));
                checkedNotes++;
            }
            logs.Add($"x{targets[i]:0.0}: 낙하 속도 {layer.ScrollSpeed:0.###} (공식 {ExpectedSpeed(targets[i]):0.###}), 보이는 시간 {(gear.ScreenTopY - gear.JudgeLineY) / layer.ScrollSpeed:0.###}초, 화면 노트 {layer.ActiveCount}개");
            Assert.AreEqual(ExpectedSpeed(targets[i]), layer.ScrollSpeed, 1e-4f);
        }

        float deadline = Time.realtimeSinceStartup + 20f;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            yield return null;
        }
        Debug.Log($"[ScrollSpeedTest] S1·S2: {string.Join(" / ", logs)} | 결과 {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, 점수 {score.Score}, " +
                  $"최대 콤보 {score.MaxCombo}/{score.MaxPossibleCombo}, GrowCount {layer.GrowCount}, 노트 풀 {layer.PooledCount}개 | S2 노트 {checkedNotes}개 y 오차 최대 {maxError:0.000000}");

        Assert.AreEqual(judge.TotalCount, score.PerfectCount, "전부 PERFECT");
        Assert.AreEqual(1000000, score.Score);
        Assert.AreEqual(0, layer.GrowCount, "플레이 중 Instantiate 없음");
        Assert.Greater(checkedNotes, 0);
        Assert.Less(maxError, 0.01, "S2: 바꾼 다음 프레임 노트 y");
    }

    /// <summary>S3: 누르고 있는 롱노트가 있을 때 배속을 바꿔도 머리는 판정선, 끝 판정 정상</summary>
    [UnityTest]
    public IEnumerator S3_ChangeWhileHoldingLong()
    {
        yield return LoadInGame(TestPlayEntry(Chart(new List<NoteData> { Long(6f, 9f, 1) }), false));
        PlayNote note = judge.Notes[0];
        LongNoteView view = null;
        float half = layer.NoteSize.y / 2f;

        yield return WaitSongTime(note.Time - InjectLeadSeconds);
        scene.TestPress(1, note.Time);
        List<string> samples = new List<string>();
        float maxHeadError = 0f;
        foreach (float target in new[] { 6f, 2f, 7.5f })
        {
            yield return WaitSongTime(SongTime + 0.25);
            TapTo(target);
            yield return null;
            view = Get<LongNoteView[]>(layer, "longViewByNote")[0];
            float headCenter = view.BottomY + half;
            maxHeadError = Mathf.Max(maxHeadError, Mathf.Abs(headCenter - gear.JudgeLineY));
            samples.Add($"x{target:0.0} 머리 중심 {headCenter:0.0000}, 위 끝 {view.TopY:0.000}");
        }

        yield return WaitSongTime(note.EndTime - InjectLeadSeconds);
        scene.TestRelease(1, note.EndTime);
        yield return null;
        bool done = judge.IsJudged(0);
        Debug.Log($"[ScrollSpeedTest] S3: 판정선 {gear.JudgeLineY:0.000}, {string.Join(" / ", samples)}, 끝 판정 {(done ? "남" : "안 남")}, 종합 {score.PerfectCount}P/{score.MissCount}M, 콤보 {score.Combo}");

        Assert.Less(maxHeadError, 0.01f, "누르는 동안 머리는 판정선");
        Assert.IsTrue(done);
        Assert.AreEqual(1, score.PerfectCount, "종합 PERFECT");
        Assert.AreEqual(1 + note.TickCount, score.Combo);
    }

    /// <summary>S4: 일시정지 팝업이 열린 동안 1 / 2는 무시, 재개 카운트다운 중(팝업 닫힘)에는 받는다</summary>
    [UnityTest]
    public IEnumerator S4_IgnoredWhilePopupOpen()
    {
        yield return LoadInGame(TestPlayEntry(DenseChart(), false));
        yield return WaitSongTime(1.0);
        scene.OnBack();
        bool popupOpen = GameManager.UI.HasOpenPopup;
        float before = layer.ScrollMultiplier;
        for (int i = 0; i < 5; i++) scene.TestTapSpeedKey(true);
        float afterPopupTaps = layer.ScrollMultiplier;

        scene.GetType().GetMethod("OnClickResume", Private).Invoke(scene, null);
        yield return null;
        bool countingDown = Get<bool>(scene, "isPaused") && !GameManager.UI.HasOpenPopup;
        scene.TestTapSpeedKey(true);
        float afterCountdownTap = layer.ScrollMultiplier;
        Debug.Log($"[ScrollSpeedTest] S4: 팝업 열림 {popupOpen}, 팝업 중 2를 5번 → x{before:0.0} → x{afterPopupTaps:0.0}, 카운트다운 중 {countingDown} 2를 1번 → x{afterCountdownTap:0.0}");

        Assert.IsTrue(popupOpen);
        Assert.AreEqual(before, afterPopupTaps, "팝업이 열린 동안 안 바뀜");
        Assert.IsTrue(countingDown);
        Assert.AreEqual(before + 0.1f, afterCountdownTap, 1e-4f, "카운트다운 중에는 받는다");
    }

    /// <summary>S5: 인게임에서 바꾼 배속이 재시작, 결과 → 다시 하기, 결과 → 곡 선택, 테스트 플레이 → 에디터 복귀에 이어진다</summary>
    [UnityTest]
    public IEnumerator S5_SpeedCarriesOver()
    {
        List<string> log = new List<string>();

        // ① 보통 플레이 (테스트용 공식 채보 Critical Velocity EASY): 3.0으로 바꿈 → 재시작
        InGameEntryData normal = new InGameEntryData { songId = Song, chartId = TestCharts.OfficialChartId, chartType = ChartType.Official, mode = InGameMode.Normal };
        yield return LoadInGame(normal);
        Img_JudgePanel panel = Get<Img_JudgePanel>(Get<Canvas_InGame>(scene, "canvas"), "judgePanel");
        Assert.AreEqual("0.00%", Get<TMPro.TMP_Text>(panel, "accuracy").text, "일반 플레이 시작: 판정 전 정확도 0%");
        TapTo(3f);
        scene.GetType().GetMethod("Restart", Private).Invoke(scene, null);
        yield return null;
        yield return WaitInGameStarted();
        float afterRestart = layer.ScrollMultiplier;
        log.Add($"재시작 x{afterRestart:0.0}");

        // ② 4.2로 바꾸고 결과로 → 다시 하기 데이터·곡 선택 데이터.
        // 자동 플레이로 켜서(기록·마지막 배속을 저장하지 않게) 곡이 끝날 때와 같은 Finish를 바로 부른다.
        // 테스트용 공식 채보(Critical Velocity EASY)는 곡 전체를 기다리면 약 2.5분이 걸린다.
        TapTo(4.2f);
        Set(scene, "isAutoPlay", true);
        scene.GetType().GetMethod("Finish", Private).Invoke(scene, null);
        ResultScene resultScene = null;
        float deadline = Time.realtimeSinceStartup + 60f;
        while ((resultScene = UnityEngine.Object.FindFirstObjectByType<ResultScene>()) == null || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 씬 대기 시간 초과");
            yield return null;
        }
        while (Get<ResultData>(resultScene, "result") == null) yield return null;
        ResultData result = Get<ResultData>(resultScene, "result");
        float retryMultiplier = result.retryEntry.scrollMultiplier;
        float returnMultiplier = ((SongSelectEntryData)result.returnTarget.sceneData).scrollMultiplier;
        log.Add($"결과 다시 하기 x{retryMultiplier:0.0}, 곡 선택으로 x{returnMultiplier:0.0}");

        // 다시 하기 → 인게임 배속
        resultScene.GetType().GetMethod("Retry", Private).Invoke(resultScene, null);
        yield return null;
        yield return WaitInGameStarted();
        float afterRetry = layer.ScrollMultiplier;
        log.Add($"다시 하기 뒤 인게임 x{afterRetry:0.0}");

        // 곡 선택으로 돌아가기 (결과 화면 데이터와 같은 경로) → 곡 선택 배속
        Assert.IsTrue(GameManager.Scene.Load(result.returnTarget.sceneName, result.returnTarget.sceneData));
        Canvas_SongSelect songSelect = null;
        deadline = Time.realtimeSinceStartup + 20f;
        while ((songSelect = UnityEngine.Object.FindFirstObjectByType<Canvas_SongSelect>()) == null || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "곡 선택 대기 시간 초과");
            yield return null;
        }
        yield return null;
        float songSelectMultiplier = songSelect.ScrollMultiplier;
        log.Add($"곡 선택 x{songSelectMultiplier:0.0}");

        // ③ 테스트 플레이 (편집 상태 x1.0) → 6.6으로 바꿈 → 나가기(에디터) → 에디터 테스트 플레이 배속 칸
        EditorRestoreState restore = new EditorRestoreState { chart = DenseChart(), currentBeat = 0f, snapDivision = 4, gridHeightScale = 1f, tool = EditorTool.ShortNote };
        yield return LoadInGame(TestPlayEntry(restore.chart, false, 1f, restore));
        TapTo(6.6f);
        scene.GetType().GetMethod("OnClickExit", Private).Invoke(scene, null);
        Canvas_Editor editorCanvas = null;
        deadline = Time.realtimeSinceStartup + 20f;
        while ((editorCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas_Editor>()) == null || GameManager.Scene.IsLoading
               || !Get<bool>(UnityEngine.Object.FindFirstObjectByType<EditorScene>(), "isReady"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 대기 시간 초과");
            yield return null;
        }
        float editorMultiplier = editorCanvas.TestPlayScrollMultiplier;
        string editorText = Get<TMPro.TMP_Text>(editorCanvas, "speedText").text;
        log.Add($"에디터 복귀 x{editorMultiplier:0.0} (\"{editorText}\")");
        Debug.Log($"[ScrollSpeedTest] S5: {string.Join(" → ", log)}");

        Assert.AreEqual(3f, afterRestart, 1e-4f, "재시작");
        Assert.AreEqual(4.2f, retryMultiplier, 1e-4f, "결과 다시 하기 데이터");
        Assert.AreEqual(4.2f, returnMultiplier, 1e-4f, "결과 → 곡 선택 데이터");
        Assert.AreEqual(4.2f, afterRetry, 1e-4f, "다시 하기 뒤 인게임");
        Assert.AreEqual(4.2f, songSelectMultiplier, 1e-4f, "곡 선택");
        Assert.AreEqual(6.6f, editorMultiplier, 1e-4f, "테스트 플레이 → 에디터");
        Assert.AreEqual("x6.6", editorText);
    }

    /// <summary>S6: 에디터 배속 도구 x6.0 → 테스트 플레이 낙하 속도가 6.0배 공식과 같다</summary>
    [UnityTest]
    public IEnumerator S6_EditorSpeedTool()
    {
        yield return WaitManagers();
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, new EditorEntryData { songId = Song, chartName = "배속 확인", level = 1 }));
        EditorScene editor = null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((editor = UnityEngine.Object.FindFirstObjectByType<EditorScene>()) == null || GameManager.Scene.IsLoading || !Get<bool>(editor, "isReady"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 대기 시간 초과");
            yield return null;
        }

        Canvas_Editor canvas = UnityEngine.Object.FindFirstObjectByType<Canvas_Editor>();
        ChartEditSession session = UnityEngine.Object.FindFirstObjectByType<ChartEditSession>();
        float startValue = canvas.TestPlayScrollMultiplier;
        UnityEngine.UI.Button up = Get<UnityEngine.UI.Button>(canvas, "speedUpButton");
        UnityEngine.UI.Button down = Get<UnityEngine.UI.Button>(canvas, "speedDownButton");
        bool downAtStart = down.interactable;
        for (int i = 0; i < 50; i++) up.onClick.Invoke();
        string text = Get<TMPro.TMP_Text>(canvas, "speedText").text;
        session.AddNote(0, GameConstants.Editor.TicksPerBeat * 6, out _);
        session.AddNote(1, GameConstants.Editor.TicksPerBeat * 7, out _);

        editor.GetType().GetMethod("OnAutoTestPlayRequested", Private).Invoke(editor, null);
        yield return null;
        yield return WaitInGameStarted();

        float expected = ExpectedSpeed(6f);
        Debug.Log($"[ScrollSpeedTest] S6: 에디터 시작 x{startValue:0.0} (− 버튼 {(downAtStart ? "켜짐" : "꺼짐")}), + 50번 → \"{text}\", 인게임 배속 x{layer.ScrollMultiplier:0.0}, " +
                  $"낙하 속도 {layer.ScrollSpeed:0.####} (6.0배 공식 {expected:0.####}, 1.0배 {layer.BaseScrollSpeed:0.####} × 6 = {layer.BaseScrollSpeed * 6f:0.####})");

        Assert.AreEqual(1f, startValue, "에디터는 1.0에서 시작");
        Assert.IsFalse(downAtStart, "1.0에서는 − 버튼 꺼짐");
        Assert.AreEqual("x6.0", text);
        Assert.AreEqual(6f, layer.ScrollMultiplier, 1e-4f);
        Assert.AreEqual(expected, layer.ScrollSpeed, 1e-4f);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, Private).SetValue(target, value);
    }
}
