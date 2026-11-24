using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 결과 등장 연출 · 건너뛰기 · 「테스트 후 결과 보기」 (기획서 3-5 · 3-6, 11/20) 실제 씬 확인.
/// 이 테스트만 ResultScene.SkipResultIntroForTests를 끈다 (등장 연출이 돈다). 결과는 테스트 안에서 만든 ResultData(Critical Velocity NORMAL 표시)로 들어간다.
/// 기록은 남지 않는다 (다시 하기 데이터가 테스트 플레이라 기록하지 않음).
/// - I1 암전 로딩으로 들어가 걷히는 동안 값 요소 숨김 → OnEnter부터 단계 시작 시각 · t별 값 · 끝 상태 최종 글자 (NEW RECORD 3개 · FC 있음)
/// - I2 NEW RECORD · FC 없는 판: 같은 단계 시작 시각이 I1과 같음 (밀리지 않음)
/// - I3 연출 중 Enter · Esc · F5 · Enter 버튼 · 다시 하기 버튼 → 건너뛰기만 (씬 그대로) → 다음 입력에 이동 · 다시 하기. 같은 프레임 Enter + Esc는 한 번만
/// - I4 테스트 플레이 결과: Init 직후 끝 상태, 첫 Enter에 돌아가기
/// - I5 스위치: 꺼짐이면 테스트 플레이 · 자동 테스트 끝 → Result 로드 0회로 에디터, 켜짐이면 결과(연출 없음) → Enter로 에디터. 저장값 유지 · 설명 줄 배치
/// </summary>
public class ResultIntroPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private bool savedSkipIntro;
    private bool savedSkipOverlay;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;
    private LastSelectionSaveData savedSelection;
    private int resultLoads;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkipIntro = ResultScene.SkipResultIntroForTests;
        savedSkipOverlay = GameSceneManager.SkipLoadingOverlayForTests;
        ResultScene.SkipResultIntroForTests = false;
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        SceneManager.sceneLoaded += CountResult;
        resultLoads = 0;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded -= CountResult;
        if (savedSelection != null && PlayerDataManager.HasInstance)
        {
            GameManager.PlayerData.LastSelection.CopyFrom(savedSelection);
            GameManager.PlayerData.Save();
        }
        savedSelection = null;
        ResultScene.SkipResultIntroForTests = savedSkipIntro;
        GameSceneManager.SkipLoadingOverlayForTests = savedSkipOverlay;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
        yield return new ExitPlayMode();
    }

    private void CountResult(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameConstants.Scenes.Result) resultLoads++;
    }

    private static T Get<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (T)info.GetValue(target);
    }

    private static ChartData SmallChart()
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 2; i++) notes.Add(new NoteData { beat = 4f + i, lane = i, type = NoteData.TypeShort });
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion, songId = Song, type = ChartData.TypeCustom, chartName = "결과 연출 확인용",
            keyCount = GameConstants.Editor.LaneCount, level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } }, notes = notes
        };
    }

    private static InGameEntryData TestPlayEntry(bool autoPlay, bool showResult)
    {
        return new InGameEntryData
        {
            songId = Song, chartType = ChartType.Custom, mode = InGameMode.TestPlay, scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = SmallChart(), startBeat = 0f, autoPlay = autoPlay, showResult = showResult }
        };
    }

    /// <summary>Critical Velocity NORMAL 모양의 결과 (P 709 · G 88 · Go 15 · M 0, FAST 41 · SLOW 62)</summary>
    private static ResultData CvNormalResult(bool isTestPlay)
    {
        ScoreCalculator score = new ScoreCalculator(812);
        for (int i = 0; i < 709; i++) score.Add(JudgeType.Perfect);
        for (int i = 0; i < 88; i++) score.Add(JudgeType.Great);
        for (int i = 0; i < 15; i++) score.Add(JudgeType.Good);
        ResultData r = ResultData.FromScore(score);
        r.songId = Song;
        r.songTitle = "Critical Velocity";
        r.artist = "Kairos";
        r.chartType = ChartType.Official;
        r.difficulty = "NORMAL";
        r.level = 7;
        r.scrollMultiplier = 5.5f;
        r.fastCount = 41;
        r.slowCount = 62;
        r.isTestPlay = isTestPlay;
        r.retryEntry = TestPlayEntry(true, true); // 기록하지 않음 · 다시 하기는 작은 채보
        r.returnTarget = new ResultReturnTarget
        {
            sceneName = GameConstants.Scenes.SongSelect,
            sceneData = new SongSelectEntryData { songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = 1f },
            buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
        };
        return r;
    }

    private static IEnumerator WaitManagers()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
    }

    /// <summary>결과 씬을 바로(오버레이 없이) 불러와 OnEnter까지 기다린다</summary>
    private static IEnumerator LoadResult(ResultData data, System.Action<ResultScene> done)
    {
        yield return WaitManagers();
        GameSceneManager.SkipLoadingOverlayForTests = true;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Result, data));
        float deadline = Time.realtimeSinceStartup + 20f;
        ResultScene scene;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 대기 시간 초과");
            scene = Object.FindFirstObjectByType<ResultScene>();
            if (scene != null && !GameManager.Scene.IsLoading && scene.IsEntered) break;
            yield return null;
        }
        done(scene);
    }

    private static IEnumerator Tap(params Key[] keys)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    /// <summary>값 요소 CanvasGroup 알파들 (패널 안, CanvasGroup이 붙은 것)</summary>
    private static List<float> ValueAlphas(Canvas_Result canvas)
    {
        List<float> list = new List<float>();
        Transform panel = canvas.transform.Find("Img_ResultPanel");
        foreach (CanvasGroup g in panel.GetComponentsInChildren<CanvasGroup>(true))
        {
            // 흐르는 제목 글자의 CanvasGroup은 연출이 아니라 마퀴가 쓴다 (연출 알파는 그 칸에 있다)
            if (g.GetComponent<UIMarqueeText>() == null && g.GetComponentInParent<UIMarqueeText>(true) != null) continue;
            list.Add(g.alpha);
        }
        return list;
    }

    private static string Snapshot(Canvas_Result c)
    {
        ResultIntroFrame f = c.IntroFrame;
        TMP_Text score = Get<TMP_Text>(c, "scoreText");
        TMP_Text rate = Get<TMP_Text>(c, "rateText");
        TMP_Text combo = Get<TMP_Text>(c, "maxComboText");
        TMP_Text[] counts = c.JudgeCountTexts;
        return $"t={c.IntroTime:0.000} | 앨범 a{f.Album.Alpha:0.00} s{f.Album.Scale:0.00} | 제목 a{f.Title.Alpha:0.00} x{f.Title.Offset.x:0.0} | SCORE a{f.Score.Alpha:0.00} y{f.Score.Offset.y:0.0} \"{score.text}\" | " +
               $"NEW a{f.NewScore.Alpha:0.00} s{f.NewScore.Scale:0.00} | PERFECT a{f.Judge0.Alpha:0.00} \"{counts[0].text}\" MISS a{f.Judge3.Alpha:0.00} | F/S a{f.FastSlow.Alpha:0.00} 막대 {f.FastSlowProgress:0.00} \"{counts[4].text}/{counts[5].text}\" | " +
               $"RATE a{f.Rate.Alpha:0.00} \"{rate.text}\" | COMBO a{f.Combo.Alpha:0.00} \"{combo.text}\" | FC a{f.FullCombo.Alpha:0.00} s{f.FullCombo.Scale:0.00} | 랭크 a{f.Rank.Alpha:0.00} s{f.Rank.Scale:0.00} 번쩍 {f.RankFlash:0.00}";
    }

    /// <summary>암전 로딩으로 결과에 들어가 등장 연출을 끝까지 본다. 단계 시작 시각을 돌려준다</summary>
    private IEnumerator PlayThroughFade(bool withBadges, Dictionary<string, float> stageTimes, List<string> snapshots)
    {
        yield return WaitManagers();
        GameSceneManager.SkipLoadingOverlayForTests = false;
        ResultData data = CvNormalResult(false);
        data.isFullCombo = withBadges;
        Assert.IsTrue(GameManager.Scene.LoadWithFadeLoading(GameConstants.Scenes.Result, data));
        float deadline = Time.realtimeSinceStartup + 20f;
        bool badgesSet = false;
        int hiddenFrames = 0;
        float maxHiddenAlpha = 0f;
        ResultScene scene = null;
        while (GameManager.Scene.IsFadeLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "암전 로딩 대기 시간 초과");
            scene = Object.FindFirstObjectByType<ResultScene>();
            if (scene != null && scene.Canvas != null && scene.Canvas.IsIntroPending)
            {
                Canvas_Result c = scene.Canvas;
                if (withBadges && !badgesSet)
                {
                    // NEW RECORD는 기록을 남겨야 뜨므로 이 테스트에서는 직접 켠다 (시간표는 같다)
                    foreach (string f in new[] { "newScoreBadge", "newRateBadge", "newComboBadge" }) Get<GameObject>(c, f).SetActive(true);
                    badgesSet = true;
                }
                hiddenFrames++;
                foreach (float a in ValueAlphas(c)) maxHiddenAlpha = Mathf.Max(maxHiddenAlpha, a);
                Assert.IsFalse(c.IntroFrame.IsFinished, "걷히는 동안 끝 상태가 아님");
            }
            yield return null;
        }
        scene = Object.FindFirstObjectByType<ResultScene>();
        Assert.IsNotNull(scene);
        Assert.Greater(hiddenFrames, 5, "암전이 걷히는 동안 결과가 이미 있다");
        Assert.AreEqual(0f, maxHiddenAlpha, "걷히는 동안 값 요소는 숨김");
        Canvas_Result canvas = scene.Canvas;
        Assert.IsTrue(canvas.IsIntroPlaying || canvas.IntroTime < 0.1f, "OnEnter에서 연출 시작");

        float[] marks = { 0.3f, 1.0f, 1.5f, 2.5f, 3.5f };
        int next = 0;
        while (canvas.IsIntroPlaying)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "등장 연출 대기 시간 초과");
            if (next < marks.Length && canvas.IntroTime >= marks[next])
            {
                snapshots.Add(Snapshot(canvas));
                next++;
            }
            yield return null;
        }
        snapshots.Add(Snapshot(canvas) + " (끝)");
        Assert.IsTrue(canvas.IntroFrame.IsFinished);
        foreach (KeyValuePair<string, float> pair in canvas.IntroStageTimes) stageTimes[pair.Key] = pair.Value;

        // 끝 상태 글자 = 지금 결과 화면 형식 그대로
        Assert.AreEqual(SongSelectRecordFormat.FormatScore(data.score), Get<TMP_Text>(canvas, "scoreText").text);
        Assert.AreEqual(SongSelectRecordFormat.FormatRate(data.accuracy), Get<TMP_Text>(canvas, "rateText").text);
        Assert.AreEqual(SongSelectRecordFormat.FormatCombo(data.maxCombo), Get<TMP_Text>(canvas, "maxComboText").text);
        Assert.AreEqual(ResultFormat.FormatCount(709), canvas.JudgeCountTexts[0].text);
        Assert.AreEqual(ResultFormat.FormatCount(41), canvas.JudgeCountTexts[4].text);
        foreach (float a in ValueAlphas(canvas)) Assert.AreEqual(1f, a, 1e-5f, "끝: 모두 보임");
        Assert.AreEqual(Vector3.one, canvas.RankImage.transform.localScale, "랭크 크기 1");
        Assert.IsFalse(Get<Image>(canvas, "rankFlashImage").enabled, "번쩍임 없음");
    }

    [UnityTest]
    public IEnumerator I1_I2_IntroTimeline_HiddenDuringFade_StagesFixed()
    {
        Dictionary<string, float> withBadges = new Dictionary<string, float>();
        Dictionary<string, float> without = new Dictionary<string, float>();
        List<string> snaps = new List<string>();
        yield return PlayThroughFade(true, withBadges, snaps);
        yield return PlayThroughFade(false, without, new List<string>());

        StringBuilder table = new StringBuilder("| 단계 | 표 | 실제 (NEW · FC 있음) | 실제 (없음) |\n");
        foreach ((string name, float start) in ResultIntroTimeline.Stages)
        {
            Assert.IsTrue(withBadges.ContainsKey(name), name + " 단계 지남");
            Assert.IsTrue(without.ContainsKey(name), name + " 단계 지남 (없는 판)");
            Assert.GreaterOrEqual(withBadges[name], start - 1e-4f, name);
            Assert.Less(withBadges[name] - start, 0.05f, name + " 표 시각에서 한 프레임 안");
            Assert.AreEqual(withBadges[name], without[name], 0.05f, name + " 단계 유무와 상관없이 같은 시각");
            table.Append($"| {name} | {start:0.00} | {withBadges[name]:0.000} | {without[name]:0.000} |\n");
        }
        Debug.Log("[ResultIntroPlayTest] 단계 시작 시각 (OnEnter = 0)\n" + table);
        Debug.Log("[ResultIntroPlayTest] t별 값\n" + string.Join("\n", snaps));
    }

    [UnityTest]
    public IEnumerator I3_SkipOnlyOnce_ThenMove()
    {
        string[] kinds = { "Enter", "Esc", "F5", "Enter 버튼", "다시 하기 버튼", "Enter+Esc 같은 프레임" };
        List<string> log = new List<string>();
        foreach (string kind in kinds)
        {
            ResultScene scene = null;
            yield return LoadResult(CvNormalResult(false), s => scene = s);
            Canvas_Result canvas = scene.Canvas;
            Assert.IsTrue(canvas.IsIntroPlaying, kind + ": 연출 중");
            float wait = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < wait) yield return null;
            int retry0 = canvas.TestRetryCount, return0 = canvas.TestReturnCount;
            float skippedAt = canvas.IntroTime;

            yield return SendInput(kind, canvas);
            Assert.IsFalse(canvas.IsIntroPlaying, kind + ": 건너뜀");
            Assert.IsTrue(canvas.IntroFrame.IsFinished, kind + ": 끝 상태");
            Assert.AreEqual(SongSelectRecordFormat.FormatScore(Get<ResultData>(scene, "result").score), Get<TMP_Text>(canvas, "scoreText").text, kind + ": 최종 점수");
            Assert.AreEqual(retry0, canvas.TestRetryCount, kind + ": 다시 하기 안 함");
            Assert.AreEqual(return0, canvas.TestReturnCount, kind + ": 돌아가기 안 함");
            Assert.IsFalse(GameManager.Scene.IsLoading, kind + ": 씬 그대로");
            Assert.AreEqual(GameConstants.Scenes.Result, SceneManager.GetActiveScene().name);

            // 다음 입력: 이동 · 다시 하기
            string second = kind.StartsWith("Enter+Esc") ? "Enter" : kind;
            yield return SendInput(second, canvas);
            bool moved = canvas.TestRetryCount == retry0 + 1 || canvas.TestReturnCount == return0 + 1;
            Assert.IsTrue(moved, kind + ": 다음 입력에 이동 / 다시 하기");
            Assert.AreEqual(1, (canvas.TestRetryCount - retry0) + (canvas.TestReturnCount - return0), kind + ": 한 번만");
            log.Add($"{kind}: {skippedAt:0.00}초에 건너뜀 → 다음 입력에 {(canvas.TestRetryCount > retry0 ? "다시 하기" : "돌아가기")} 1번");

            float deadline = Time.realtimeSinceStartup + 20f;
            while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name == GameConstants.Scenes.Result)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "이동 대기 시간 초과");
                yield return null;
            }
            yield return WaitManagers();
        }
        Debug.Log("[ResultIntroPlayTest] 건너뛰기: " + string.Join(" / ", log));
    }

    private static IEnumerator SendInput(string kind, Canvas_Result canvas)
    {
        switch (kind)
        {
            case "Enter": yield return Tap(Key.Enter); break;
            case "Esc": yield return Tap(Key.Escape); break;
            case "F5": yield return Tap(Key.F5); break;
            case "Enter 버튼": Get<Button>(canvas, "returnButton").onClick.Invoke(); yield return null; break;
            case "다시 하기 버튼": Get<Button>(canvas, "retryButton").onClick.Invoke(); yield return null; break;
            default: yield return Tap(Key.Enter, Key.Escape); break;
        }
    }

    [UnityTest]
    public IEnumerator I4_TestPlayResult_FinishedAtInit_FirstEnterReturns()
    {
        ResultScene scene = null;
        yield return LoadResult(CvNormalResult(true), s => scene = s);
        Canvas_Result canvas = scene.Canvas;
        Assert.IsFalse(canvas.IsIntroPlaying, "테스트 플레이는 연출 없음");
        Assert.IsTrue(canvas.IntroFrame.IsFinished, "Init 직후 끝 상태");
        foreach (float a in ValueAlphas(canvas)) Assert.AreEqual(1f, a, 1e-5f);
        int return0 = canvas.TestReturnCount;
        string label = canvas.ReturnLabel;
        Assert.AreEqual(GameConstants.KeyGuide.ToEditor, label, "테스트 플레이 → 에디터로");
        yield return Tap(Key.Enter);
        Assert.AreEqual(return0 + 1, canvas.TestReturnCount, "첫 Enter에 돌아가기");
        Debug.Log($"[ResultIntroPlayTest] 테스트 플레이 결과: Init 직후 끝 상태 True, 첫 Enter 돌아가기 {canvas.TestReturnCount - return0}번, 버튼 \"{label}\"");
    }

    /// <summary>I6: 긴 곡 제목은 등장 연출 중 처음 위치에 멈춰 있다가, 연출이 끝나거나 건너뛴 뒤 처음 멈춤(1.5초)부터 흐른다 (UIMarqueeText)</summary>
    [UnityTest]
    public IEnumerator I6_LongTitle_ScrollsAfterIntro()
    {
        yield return WaitManagers();
        ResultData result = CvNormalResult(false);
        result.songTitle = "Send My Love Tonight (Extended Hyper Midnight Drive Remix for the Endless Night in the City)";
        ResultScene scene = null;
        yield return LoadResult(result, s => scene = s);
        Canvas_Result canvas = scene.Canvas;
        UIMarqueeText marquee = canvas.SongTitleMarquee;
        float maxOffset = 0f;
        Assert.IsTrue(canvas.IsIntroPlaying || canvas.IsIntroPending, "등장 연출 중");
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && (canvas.IsIntroPlaying || canvas.IsIntroPending))
        {
            Assert.IsFalse(marquee.IsPlaying, "연출 중에는 멈춤");
            maxOffset = Mathf.Max(maxOffset, marquee.CurrentOffset);
            yield return null;
        }
        Assert.AreEqual(0f, maxOffset, "연출 중 위치 0");
        float introAt = canvas.IntroTime;
        canvas.SkipIntro();
        yield return null;
        Assert.IsTrue(marquee.IsScrolling, "건너뛴 뒤 흐른다");
        float startedAt = Time.unscaledTime - marquee.Elapsed;
        Assert.Less(marquee.Elapsed, 0.2f, "처음 멈춤부터");
        while (marquee.CurrentOffset <= 0f && Time.unscaledTime - startedAt < 3f) yield return null;
        float movedAt = Time.unscaledTime - startedAt;
        Debug.Log($"[ResultIntroPlayTest] I6: 연출 {introAt:0.00}초까지 위치 {maxOffset:0.0}, 건너뛴 뒤 {movedAt:0.00}초에 이동 시작, 칸 {marquee.CellWidth:0} · 글자 {marquee.TextWidth:0} · 이동 거리 {marquee.OverflowWidth:0}");
        Assert.AreEqual(GameConstants.Marquee.StartDelay, movedAt, 0.2f, "StartDelay 뒤 이동");
        Assert.AreEqual(950f, marquee.CellWidth, 0.5f, "제목 칸 폭 그대로");
    }

    /// <summary>스위치 꺼짐/켜짐 × 테스트 플레이/자동 테스트: 곡 끝 흐름, 저장값 유지, 설명 줄 배치</summary>
    [UnityTest]
    public IEnumerator I5_ShowTestResultSwitch_FlowAndSave()
    {
        yield return WaitManagers();
        GameSceneManager.SkipLoadingOverlayForTests = false;
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
        List<string> log = new List<string>();

        // 저장 · 설명 줄: 에디터를 열어 끄고, 나갔다 다시 들어와 확인
        yield return OpenEditor();
        Canvas_Editor editor = Object.FindFirstObjectByType<Canvas_Editor>();
        editor.ShowTestResultToggle.isOn = true;
        Assert.AreEqual(GameConstants.Editor.ShowTestResultHintOn, editor.ShowTestResultHint.text);
        CheckHint(editor, log, "켜짐");
        editor.ShowTestResultToggle.isOn = false;
        Assert.AreEqual(GameConstants.Editor.ShowTestResultHintOff, editor.ShowTestResultHint.text);
        CheckHint(editor, log, "꺼짐");
        Assert.IsFalse(GameManager.PlayerData.LastEditorShowTestResult, "끄면 저장");
        yield return OpenEditor();
        editor = Object.FindFirstObjectByType<Canvas_Editor>();
        Assert.IsFalse(editor.ShowTestResultToggle.isOn, "다시 들어와도 꺼짐");
        Assert.IsFalse(editor.ShowTestResult);
        log.Add("저장값: 끄고 다시 들어와도 꺼짐");

        foreach (bool autoPlay in new[] { false, true })
        {
            foreach (bool show in new[] { false, true })
            {
                resultLoads = 0;
                yield return RunTestPlay(autoPlay, show);
                string flow;
                if (!show)
                {
                    Assert.AreEqual(0, resultLoads, "꺼짐: Result 로드 0회");
                    Assert.AreEqual(GameConstants.Scenes.Editor, SceneManager.GetActiveScene().name, "꺼짐: 바로 에디터");
                    flow = $"암전 로딩 → 에디터 (Result 로드 {resultLoads}회, 로딩 {GameManager.Scene.LoadingOriginName}: {GameManager.Scene.DescribeLoading()})";
                }
                else
                {
                    Assert.AreEqual(1, resultLoads, "켜짐: 결과");
                    ResultScene result = Object.FindFirstObjectByType<ResultScene>();
                    Assert.IsTrue(result.Canvas.IntroFrame.IsFinished, "켜짐: 결과 연출 없음");
                    yield return Tap(Key.Enter);
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != GameConstants.Scenes.Editor || !GameManager.Scene.Current.IsEntered)
                    {
                        Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 대기 시간 초과");
                        yield return null;
                    }
                    flow = "암전 로딩 → 결과(연출 없음, Result 로드 1회) → Enter → 에디터";
                }
                log.Add($"{(autoPlay ? "자동 테스트" : "테스트 플레이")} · 스위치 {(show ? "켜짐" : "꺼짐")}: {flow}");
            }
        }
        Debug.Log("[ResultIntroPlayTest] 스위치\n" + string.Join("\n", log));
    }

    private static void CheckHint(Canvas_Editor editor, List<string> log, string state)
    {
        TMP_Text hint = editor.ShowTestResultHint;
        hint.ForceMeshUpdate();
        Assert.IsFalse(hint.isTextOverflowing, state + " 설명 넘치지 않음");
        RectTransform panel = (RectTransform)hint.transform.parent;
        Vector3[] p = new Vector3[4];
        Vector3[] h = new Vector3[4];
        panel.GetWorldCorners(p);
        hint.rectTransform.GetWorldCorners(h);
        bool inside = h[0].x >= p[0].x - 0.01f && h[0].y >= p[0].y - 0.01f && h[2].x <= p[2].x + 0.01f && h[2].y <= p[2].y + 0.01f;
        Assert.IsTrue(inside, state + " 설명이 왼쪽 패널 안");
        float preferred = hint.GetPreferredValues(hint.text).x;
        log.Add($"설명({state}) \"{hint.text}\": 넘침 {hint.isTextOverflowing}, 글자 폭 {preferred:0.0} / 칸 {hint.rectTransform.rect.width:0}, 패널 안 {inside} (패널 {panel.name})");
    }

    private IEnumerator OpenEditor()
    {
        yield return WaitManagers();
        EditorScene before = Object.FindFirstObjectByType<EditorScene>();
        GameSceneManager.SkipLoadingOverlayForTests = true;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, new EditorEntryData { songId = Song, chartName = "결과 스위치 확인", level = 1 }));
        float deadline = Time.realtimeSinceStartup + 20f;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 시작 대기 시간 초과");
            EditorScene scene = Object.FindFirstObjectByType<EditorScene>();
            if (scene != null && scene != before && !GameManager.Scene.IsLoading && scene.IsEntered) break;
            yield return null;
        }
        GameSceneManager.SkipLoadingOverlayForTests = false;
    }

    /// <summary>테스트 플레이를 끝까지 (자동 플레이 또는 노트 시각에 누름) 하고 다음 씬 OnEnter까지 기다린다</summary>
    private IEnumerator RunTestPlay(bool autoPlay, bool showResult)
    {
        yield return WaitManagers();
        GameSceneManager.SkipLoadingOverlayForTests = true;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, TestPlayEntry(autoPlay, showResult)));
        float deadline = Time.realtimeSinceStartup + 30f;
        InGameScene scene = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        GameSceneManager.SkipLoadingOverlayForTests = false; // 곡 끝은 실제 암전 로딩으로
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        if (!autoPlay)
        {
            foreach (PlayNote note in judge.Notes)
            {
                while (GameManager.Sound.SongTime < note.Time)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, "노트 대기 시간 초과");
                    yield return null;
                }
                scene.TestPress(note.Lane, note.Time);
            }
        }
        while (!GameManager.Scene.IsFadeLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "암전 시작 대기 시간 초과");
            yield return null;
        }
        while (GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "암전 로딩 대기 시간 초과");
            yield return null;
        }
        yield return null;
    }
}
