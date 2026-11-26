using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 곡 로딩 · 시계 오버레이 (기획서 3-8 「곡 로딩」, 11/26 와이어프레임 21_SongLoading_Clock) 실제 흐름 확인.
/// 이 테스트만 GameSceneManager.SkipLoadingOverlayForTests를 끄고 오버레이 경로를 탄다.
/// - L1 곡 선택 Enter → 곡 로딩 → 인게임: 단계 순서 (열기 끝 0.95 뒤 OnExit · 로드, 바늘 12시 T ≥ 2.4초, 암전 끝 → 정리 → 걷힘 끝 ≥ T+1.15, 전체 ≥ 3.55초,
///   OnEnter와 첫 클릭은 걷힘 끝 뒤), 바늘 fillAmount 표 (줄지 않음), 로딩 중 ESC · Enter · ↓ 무시
/// - L2 결과 다시 하기 → 같은 곡 로딩 (결과 위에 덮음)
/// - L3 배치 (README 배치표) · Canvas 안 · 넘침 · 긴 제목 · 긴 채보 이름 흐름 · BadgeRow 가운데
/// - L4 로딩이 늦을 때 바늘이 12시 직전에서 멈추고 줄어들지 않음 (오버레이만 직접 연다)
/// 일반 플레이로 들어가지만 마지막 배속은 지금 저장된 값 그대로 쓰고, 결과는 자동 플레이로 표시해 기록을 남기지 않는다.
/// </summary>
public class SongLoadingPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private bool savedSkip;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkip = GameSceneManager.SkipLoadingOverlayForTests;
        GameSceneManager.SkipLoadingOverlayForTests = false;
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
        GameSceneManager.SkipLoadingOverlayForTests = savedSkip;
        yield return new ExitPlayMode();
    }

    private static T Get<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (T)info.GetValue(target);
    }

    private static void Call(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method, Private);
        Assert.IsNotNull(info, $"메서드 {method}가 없습니다");
        info.Invoke(target, null);
    }

    private static float LastSpeed => PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName || GameManager.Scene.Current == null || !GameManager.Scene.Current.IsEntered)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
    }

    private static IEnumerator Tap(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private static IEnumerator TapBlockedKeys()
    {
        yield return Tap(Key.DownArrow);
        yield return Tap(Key.Enter);
        yield return Tap(Key.Escape);
    }

    /// <summary>
    /// 곡 로딩이 끝나 인게임 OnEnter까지 기다리며 입력 차단 · 바늘을 확인하고, 첫 카운트인 클릭 · 노래 0박 시각을 단계 기록에 더한다.
    /// </summary>
    public static IEnumerator FollowSongLoading(string fromScene, GameObject fromSelected, StringBuilder log, bool tapKeys = true)
    {
        int starts = InGameLauncher.StartCount;
        float deadline = Time.realtimeSinceStartup + 20f;
        Canvas_SongLoading overlay = GameManager.UI.SongLoading;
        Assert.IsTrue(GameManager.Scene.IsSongLoading, "곡 로딩 시작");
        Assert.IsTrue(GameManager.Scene.IsLoading, "로딩 중 = 씬 로딩 중 (ESC 무시)");
        Assert.IsTrue(overlay.IsShown);

        // 바늘 표: 0.8 / 1.2 / 1.6 / 2.0 / 2.4초를 처음 지난 프레임의 값
        float[] marks = { 0.8f, 1.2f, 1.6f, 2.0f, 2.4f };
        int next = 0;
        float lastFill = 0f;
        StringBuilder hand = new StringBuilder("  바늘 | 기준 t | 그 프레임 t | fillAmount | EaseInOut 시간값 | 로딩 진행\n");

        // 열기 연출 중 (앞 씬이 아직 있음): ↓ · Enter · ESC
        while (overlay.OpenElapsed < 0.1f) yield return null;
        Assert.AreEqual(fromScene, GameManager.Scene.Current.gameObject.scene.name, "열기 연출 중에는 앞 씬 그대로");
        if (tapKeys)
        {
            yield return TapBlockedKeys();
            Assert.AreEqual(fromSelected, EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null, $"{fromScene} 선택 그대로 (방향키 무시)");
            Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업 안 열림");
            Assert.AreEqual(starts, InGameLauncher.StartCount, "Enter로 다시 시작하지 않음");
            log.AppendLine($"  열기 중 ↓·Enter·ESC ({overlay.OpenElapsed:0.00}초): 선택 그대로, 팝업 없음, 시작 횟수 그대로");
        }

        InGameScene inGame = null;
        bool tappedAfterInit = false;
        while (GameManager.Scene.IsSongLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "곡 로딩 끝 대기 시간 초과");
            if (overlay.IsShown)
            {
                float t = overlay.OpenElapsed;
                Assert.GreaterOrEqual(overlay.Fill, lastFill - 1e-6f, "바늘은 줄어들지 않는다");
                Assert.LessOrEqual(overlay.Fill, SongLoadingClockTimeline.HandTime(t) + 1e-4f, "바늘은 시간 진행보다 앞서지 않는다");
                Assert.LessOrEqual(overlay.Fill, overlay.LoadProgress + 1e-4f, "바늘은 로딩 진행보다 앞서지 않는다");
                lastFill = overlay.Fill;
                while (next < marks.Length && t >= marks[next])
                {
                    hand.AppendLine($"  | {marks[next]:0.0} | {t:0.000} | {overlay.Fill:0.0000} | {SongLoadingClockTimeline.HandTime(t):0.0000} | {overlay.LoadProgress:0.000}");
                    next++;
                }
            }
            if (inGame == null) inGame = Object.FindFirstObjectByType<InGameScene>();
            if (inGame != null)
            {
                Assert.IsFalse(Get<bool>(inGame, "isPlaying"), "걷힘 끝 전에는 판 시작 안 함");
                if (tapKeys && inGame.IsInitDone && !tappedAfterInit && !overlay.IsClosing)
                {
                    // 인게임 Init 뒤 · OnEnter 전: ESC · Enter · ↓
                    tappedAfterInit = true;
                    Assert.IsFalse(inGame.IsEntered, "걷히기 전에는 OnEnter 안 함");
                    yield return TapBlockedKeys();
                    Assert.IsFalse(Get<bool>(inGame, "isPaused"), "ESC로 일시정지 안 됨");
                    Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업 안 열림");
                    log.AppendLine($"  인게임 Init 뒤 ↓·Enter·ESC ({overlay.OpenElapsed:0.00}초): 일시정지 · 팝업 없음, 판 시작 전");
                    continue;
                }
            }
            yield return null;
        }
        log.Append(hand);
        Assert.AreEqual(marks.Length, next, "바늘 표 5칸");
        Assert.IsNotNull(inGame);
        Assert.IsTrue(inGame.IsEntered, "걷힘 끝 뒤 OnEnter");
        Assert.IsFalse(overlay.IsShown, "오버레이 숨김");
        Assert.IsFalse(overlay.GetComponent<Canvas>().enabled, "Canvas 끔");
        Assert.AreEqual(starts, InGameLauncher.StartCount);

        IReadOnlyList<double> clicks = inGame.CountInClickSongTimes;
        Assert.Greater(clicks.Count, 0, "시작 메트로놈 클릭 있음");
        bool clicked = false;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "노래 0박 대기 시간 초과");
            double songTime = GameManager.Sound.SongTime;
            if (!clicked && songTime >= clicks[0]) { GameManager.Scene.MarkLoading("첫 카운트인 클릭 (이 프레임에 지남)"); clicked = true; }
            if (songTime >= 0) { GameManager.Scene.MarkLoading("노래 0박"); break; }
            yield return null;
        }
    }

    /// <summary>이름이 prefix로 시작하는 첫 단계의 시각 (단계 이름 뒤에 걸린 ms가 붙어 있다)</summary>
    private static double At(IReadOnlyList<LoadingStep> steps, string prefix)
    {
        foreach (LoadingStep step in steps)
            if (step.Name.StartsWith(prefix)) return step.Seconds;
        Assert.Fail($"단계 '{prefix}'가 없습니다");
        return 0;
    }

    /// <summary>단계 시각 표를 만들고 순서 · 시간 조건을 확인한다</summary>
    public static void CheckSteps(string fromScene, StringBuilder log)
    {
        IReadOnlyList<LoadingStep> steps = GameManager.Scene.LoadingSteps;
        double open = At(steps, "열기 시작");
        log.AppendLine("  단계 | Enter부터 (초) | 열기 시작부터 (초)");
        foreach (LoadingStep step in steps) log.AppendLine($"  {step.Name} | {step.Seconds:0.000} | {step.Seconds - open:0.000}");
        double openEnd = At(steps, "열기 끝");
        double exit = At(steps, $"{fromScene} OnExit");
        double loadStart = At(steps, $"{GameConstants.Scenes.InGame} 로드 시작");
        double loadEnd = At(steps, $"{GameConstants.Scenes.InGame} 로드 끝");
        double init = At(steps, $"{GameConstants.Scenes.InGame} Init 끝");
        double progress = At(steps, "로딩 진행 1");
        double twelve = At(steps, "바늘 12시 T");
        double black = At(steps, "암전 끝");
        double cleanup = At(steps, "정리 끝");
        double reveal = At(steps, "걷힘 끝");
        double enter = At(steps, $"{GameConstants.Scenes.InGame} OnEnter");
        double click = At(steps, "첫 카운트인 클릭");
        double zero = At(steps, "노래 0박");
        const double frame = 1e-3;
        Assert.Greater(open, 0.0, "열기 0초는 Enter 다음 프레임");
        Assert.GreaterOrEqual(openEnd - open, GameConstants.SongLoading.OpenSeconds - frame, "열기 0.95초");
        Assert.GreaterOrEqual(exit - open, GameConstants.SongLoading.OpenSeconds - frame, "앞 씬 OnExit는 열기 끝 뒤");
        Assert.GreaterOrEqual(loadStart - open, GameConstants.SongLoading.OpenSeconds - frame, "로드 시작 ≥ 0.95초");
        Assert.GreaterOrEqual(loadStart, exit, "인게임 로드는 OnExit 뒤");
        Assert.GreaterOrEqual(loadEnd, loadStart);
        Assert.GreaterOrEqual(init, loadEnd);
        Assert.GreaterOrEqual(progress, init, "진행 1은 Init 뒤");
        Assert.GreaterOrEqual(twelve - open, GameConstants.SongLoading.HandStart + GameConstants.SongLoading.HandSeconds - frame, "바늘 12시 ≥ 2.4초");
        Assert.GreaterOrEqual(twelve, init - 0.05, "바늘 12시는 Init 끝(진행 1) 뒤 (같은 프레임 허용)");
        Assert.GreaterOrEqual(black - twelve, GameConstants.SongLoading.BlackedOutAt - frame, "암전 끝 ≥ T+0.65");
        Assert.GreaterOrEqual(cleanup, black, "정리는 검은 화면에서");
        Assert.GreaterOrEqual(reveal - twelve, GameConstants.SongLoading.RevealStart + GameConstants.SongLoading.RevealSeconds - frame, "걷힘 끝 ≥ T+1.15");
        Assert.GreaterOrEqual(reveal - open, GameConstants.SongLoading.MinTotalSeconds - frame, "전체 ≥ 3.55초");
        Assert.GreaterOrEqual(enter, reveal, "OnEnter는 걷힘 끝 뒤");
        Assert.Greater(click, reveal, "첫 클릭은 걷힘 끝 뒤");
        Assert.Greater(zero, click, "노래 0박은 첫 클릭 뒤");
        foreach (LoadingFrameStats stats in GameManager.Scene.LoadingFrames)
            log.AppendLine($"  프레임 [{stats.Phase}] {stats.FrameCount}개, 최대 {stats.MaxMs:0.0}ms, 33ms 넘음 {stats.SlowCount}개");
        log.AppendLine($"  전체(열기 시작 → 걷힘 끝) {reveal - open:0.000}초, 로드 시작 {loadStart - open:0.000}초, T {twelve - open:0.000}초, 정리 끝 T+{cleanup - twelve:0.000}초");
    }

    [UnityTest]
    public IEnumerator L1_SongSelectEnter_SongLoading_InGame()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
        });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        Call(songSelect, "StartGame");
        StringBuilder log = new StringBuilder($"[SongLoadingPlayTest] L1 곡 선택 Enter → 곡 로딩 → 인게임 (재생 배율 x{TestSpeed.CurrentRate}, 오버레이는 실제 시간)\n");
        yield return FollowSongLoading(GameConstants.Scenes.SongSelect, selected, log);
        CheckSteps(GameConstants.Scenes.SongSelect, log);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator L2_ResultRetry_SongLoading_InGame()
    {
        ScoreCalculator score = new ScoreCalculator(1);
        score.Add(JudgeType.Perfect);
        ResultData result = ResultData.FromScore(score);
        result.songId = Song;
        result.songTitle = "Critical Velocity";
        result.chartType = ChartType.Official;
        result.difficulty = GameConstants.Chart.DifficultyNormal;
        result.level = 7;
        result.isAutoPlay = true; // 기록을 남기지 않게
        result.retryEntry = InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, LastSpeed);
        result.returnTarget = new ResultReturnTarget { sceneName = GameConstants.Scenes.SongSelect, sceneData = new SongSelectEntryData { songId = Song } };
        yield return LoadAndWait(GameConstants.Scenes.Result, result);
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        Call(Object.FindFirstObjectByType<ResultScene>(), "Retry");
        StringBuilder log = new StringBuilder($"[SongLoadingPlayTest] L2 결과 다시 하기 → 곡 로딩 → 인게임 (재생 배율 x{TestSpeed.CurrentRate})\n");
        yield return FollowSongLoading(GameConstants.Scenes.Result, selected, log);
        CheckSteps(GameConstants.Scenes.Result, log);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator L3_Layout_Overflow_Marquee()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
        });
        Canvas_SongLoading overlay = GameManager.UI.SongLoading;
        RectTransform root = (RectTransform)overlay.transform;
        Assert.AreEqual(GameConstants.SongLoading.SortOrder, overlay.GetComponent<Canvas>().sortingOrder);
        Assert.Greater(overlay.GetComponent<Canvas>().sortingOrder, GameManager.UI.OverlayCanvas.sortingOrder, "Canvas_Overlay 팝업보다 위");
        Assert.IsFalse(overlay.IsShown, "Init 뒤 숨김");
        Assert.IsTrue(overlay.IsPrewarmed, "미리 그려 두기 끝");

        InGameEntryData entry = InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, 5.5f);
        overlay.Open(entry);
        float until = Time.realtimeSinceStartup + GameConstants.SongLoading.OpenSeconds + 0.1f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Canvas.ForceUpdateCanvases();

        // README 배치표 (1920×1080, 왼쪽 위 기준)
        StringBuilder sb = new StringBuilder("[SongLoadingPlayTest] L3 배치\n  오브젝트 | x | y | 폭 | 높이 | 기대\n");
        ExpectRect(sb, root, "Clock", 620, 60, 680, 680);
        ExpectRect(sb, root, "Img_Album", 776, 216, 368, 368);
        ExpectRect(sb, root, "Img_SD", 1427, 719, 326, 326);
        ExpectRect(sb, root, "Img_SdLine", 1560, 947, 270, 2);
        Rect title = ExpectTop(sb, root, "TitleViewport", 748);
        Assert.AreEqual(1100f, title.width, 0.5f, "제목 칸 폭 1100");
        Assert.AreEqual(960f, title.center.x, 0.5f, "제목 칸 가운데");
        Rect artist = ExpectTop(sb, root, "Txt_Artist", 820);
        Assert.AreEqual(960f, artist.center.x, 0.5f);
        ExpectTop(sb, root, "BadgeRow", 866);
        Rect chart = ExpectTop(sb, root, "ChartViewport", 910);
        Assert.AreEqual(1100f, chart.width, 0.5f, "채보 이름 칸 폭 1100");
        Rect loading = ExpectTop(sb, root, "Txt_Loading", 986);
        Assert.AreEqual(1650f, loading.x, 0.5f, "LOADING 왼쪽 x 1650");
        Rect clock = TopLeft((RectTransform)Find(root, "Clock"), root);
        Assert.AreEqual(960f, clock.center.x, 0.5f, "시계 중심 x 960");
        Assert.AreEqual(400f, clock.center.y, 0.5f, "시계 중심 y 400");
        Rect spark = TopLeft((RectTransform)Find(root, "Img_ClockSpark"), root);
        Assert.AreEqual(960f, spark.center.x, 0.5f);
        Assert.AreEqual(100f, spark.center.y, 0.5f, "반짝 12시 (960, 100)");
        Rect glow = TopLeft((RectTransform)Find(root, "Img_Glow"), root);
        Assert.AreEqual(400f, glow.center.y, 0.5f, "빛 중심 y 400");
        Assert.IsTrue(IsRadialTopClockwise(overlay), "호는 Filled Radial 360 Top Clockwise");

        sb.AppendLine("  상태 | 뱃지 | Lv | CHART 줄 | Canvas 밖 | 넘침 Artist/Badge/Level/Loading | 제목 흐름 | 채보 흐름 | BadgeRow 가운데 x");
        SongData song = GameManager.Songs.Find(Song);
        ChartData official = new ChartData { chartId = "x", type = ChartData.TypeOfficial, difficulty = "NORMAL", level = 7 };
        ChartData custom = new ChartData { chartId = "x", type = ChartData.TypeCustom, chartName = "내가 만든 질주 채보", level = 9 };
        InGameEntryData customEntry = InGameLauncher.CreateNormalEntry(Song, "x", ChartType.Custom, 5.5f);
        yield return Report(sb, overlay, root, "공식", SongLoadingInfoRules.Build(entry, song, official), song, false);
        yield return Report(sb, overlay, root, "나만의 채보", SongLoadingInfoRules.Build(customEntry, song, custom), song, false);
        SongLoadingInfo longInfo = SongLoadingInfoRules.Build(customEntry, song,
            new ChartData { chartId = "x", type = ChartData.TypeCustom, chartName = "아주아주 긴 나만의 채보 이름이 칸 끝까지 이어지는 경우를 확인하기 위한 채보 이름입니다 그리고 조금 더 길게 이어 붙인 부분까지", level = 12 });
        longInfo.Title = "Critical Velocity (Extended Hyper Midnight Drive Remix for the Endless Night)";
        yield return Report(sb, overlay, root, "긴 제목 · 채보 이름", longInfo, song, true);
        foreach (string level in new[] { "Lv. 1", "Lv. 15" })
        {
            SongLoadingInfo info = SongLoadingInfoRules.Build(entry, song, official);
            info.LevelText = level;
            yield return Report(sb, overlay, root, level, info, song, false);
        }

        yield return overlay.CloseRoutine();
        Assert.IsFalse(overlay.IsShown);
        Assert.IsFalse(overlay.GetComponent<Canvas>().enabled, "닫으면 Canvas 끔");
        Debug.Log(sb.ToString());
    }

    private static bool IsRadialTopClockwise(Canvas_SongLoading overlay)
    {
        UnityEngine.UI.Image arc = overlay.ClockArc;
        return arc.type == UnityEngine.UI.Image.Type.Filled && arc.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360
            && arc.fillOrigin == (int)UnityEngine.UI.Image.Origin360.Top && arc.fillClockwise;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        Assert.Fail($"{name}가 없습니다");
        return null;
    }

    private static void ExpectRect(StringBuilder sb, RectTransform root, string name, float x, float y, float w, float h)
    {
        Rect r = TopLeft((RectTransform)Find(root, name), root);
        sb.AppendLine($"  {name} | {r.x:0.0} | {r.y:0.0} | {r.width:0.0} | {r.height:0.0} | ({x}, {y}) {w}×{h}");
        Assert.AreEqual(x, r.x, 0.5f, name + " x");
        Assert.AreEqual(y, r.y, 0.5f, name + " y");
        Assert.AreEqual(w, r.width, 0.5f, name + " 폭");
        Assert.AreEqual(h, r.height, 0.5f, name + " 높이");
    }

    private static Rect ExpectTop(StringBuilder sb, RectTransform root, string name, float top)
    {
        Rect r = TopLeft((RectTransform)Find(root, name), root);
        sb.AppendLine($"  {name} | {r.x:0.0} | {r.y:0.0} | {r.width:0.0} | {r.height:0.0} | 위 y {top}");
        Assert.AreEqual(top, r.y, 0.5f, name + " 위 y");
        return r;
    }

    private static IEnumerator Report(StringBuilder sb, Canvas_SongLoading overlay, RectTransform root, string state, SongLoadingInfo info, SongData song, bool expectScroll)
    {
        overlay.SetContent(info, song);
        yield return null;
        overlay.TitleMarquee.Tick(0f);
        if (overlay.ChartMarquee.isActiveAndEnabled) overlay.ChartMarquee.Tick(0f);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in overlay.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate();

        List<string> outside = new List<string>();
        foreach (RectTransform r in overlay.GetComponentsInChildren<RectTransform>(false))
        {
            if (r == root || r.name.StartsWith("TMP SubMesh")) continue;
            // 흐르는 글자는 칸(RectMask2D)보다 넓다 · 곡 배경은 화면 덮기라 화면보다 클 수 있다
            if (r.GetComponent<TMP_Text>() != null && r.parent.GetComponent<UIMarqueeText>() != null) continue;
            if (r.name == "Img_SongArt") continue;
            Rect b = TopLeft(r, root);
            if (b.xMin < -0.5f || b.yMin < -0.5f || b.xMax > root.rect.width + 0.5f || b.yMax > root.rect.height + 0.5f) outside.Add(r.name);
        }
        Assert.IsEmpty(outside, $"{state}: Canvas 밖 {string.Join(", ", outside)}");
        Assert.IsFalse(overlay.ArtistText.isTextOverflowing, state + " 아티스트 넘침");
        Assert.IsFalse(overlay.BadgeText.isTextOverflowing, state + " 뱃지 넘침");
        Assert.IsFalse(overlay.LevelText.isTextOverflowing, state + " Lv 넘침");
        Assert.IsFalse(overlay.LoadingText.isTextOverflowing, "LOADING 넘침");
        Assert.AreEqual(expectScroll, overlay.TitleMarquee.IsScrolling, state + " 제목 흐름");
        bool chartShown = overlay.ChartMarquee.gameObject.activeSelf;
        Assert.AreEqual(info.ShowChartName, chartShown, state + " CHART 줄");
        if (chartShown) Assert.AreEqual(expectScroll, overlay.ChartMarquee.IsScrolling, state + " 채보 이름 흐름");
        if (!expectScroll)
        {
            Assert.IsFalse(overlay.TitleText.isTextOverflowing, state + " 제목 넘침");
            Assert.AreEqual(TextAlignmentOptions.Center, overlay.TitleText.alignment, state + " 제목 가운데 정렬");
        }

        // BadgeRow 묶음(뱃지 + 14px + Lv) 가운데
        RectTransform first = overlay.Badge.rectTransform;
        RectTransform last = overlay.LevelText.rectTransform;
        Rect a = TopLeft(first, root);
        Rect c = TopLeft(last, root);
        float levelRight = c.x + c.width;
        float center = (a.x + levelRight) * 0.5f;
        Assert.AreEqual(960f, center, 1f, state + " BadgeRow 가운데");
        Assert.AreEqual(14f, c.x - (a.x + a.width), 0.5f, "뱃지와 Lv 사이 14px");
        Assert.AreEqual(96f, a.width, 0.5f, "뱃지 96×28");
        Assert.AreEqual(28f, a.height, 0.5f);
        Color expected = info.IsCustom ? UITheme.Current.difficultyCustom : UITheme.Current.GetDifficulty(info.BadgeText);
        Assert.AreEqual(expected, overlay.Badge.color, "뱃지 색 = 곡 선택 난이도 색");

        sb.AppendLine($"  {state} | {overlay.BadgeText.text} | {overlay.LevelText.text} | {(chartShown ? "보임" : "숨김")} | {(outside.Count == 0 ? "없음" : string.Join(",", outside))} | " +
                      $"{overlay.ArtistText.isTextOverflowing}/{overlay.BadgeText.isTextOverflowing}/{overlay.LevelText.isTextOverflowing}/{overlay.LoadingText.isTextOverflowing} | " +
                      $"{overlay.TitleMarquee.IsScrolling} ({overlay.TitleMarquee.TextWidth:0}/{overlay.TitleMarquee.CellWidth:0}) | {(chartShown ? overlay.ChartMarquee.IsScrolling + $" ({overlay.ChartMarquee.TextWidth:0}/{overlay.ChartMarquee.CellWidth:0})" : "-")} | {center:0.0}");
    }

    [UnityTest]
    public IEnumerator L4_SlowLoading_HandWaitsBeforeTwelve()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
        });
        Canvas_SongLoading overlay = GameManager.UI.SongLoading;
        overlay.Open(InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, 1f));
        overlay.SetLoadProgress(0.9f);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (overlay.OpenElapsed < 2.6f) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
        float stopped = overlay.Fill;
        Assert.AreEqual(0.9f, stopped, 1e-4f, "로딩 진행 0.9에서 멈춤 (12시 직전)");
        // 0.6초 더 기다려도 · 진행이 더 낮게 와도 그대로
        overlay.SetLoadProgress(0.3f);
        float min = 1f, max = 0f;
        while (overlay.OpenElapsed < 3.2f)
        {
            min = Mathf.Min(min, overlay.Fill);
            max = Mathf.Max(max, overlay.Fill);
            Assert.IsFalse(overlay.HasReachedTwelve, "진행 1 전에는 12시에 닿지 않는다");
            yield return null;
        }
        Assert.AreEqual(stopped, min, 1e-6f, "줄어들지 않는다");
        Assert.AreEqual(stopped, max, 1e-6f, "다시 돌지 않는다");
        Assert.AreEqual(0f, overlay.Frame.SparkAlpha, "반짝 전");

        overlay.SetLoadProgress(1f);
        yield return null;
        yield return null;
        Assert.IsTrue(overlay.HasReachedTwelve, "진행 1이 되면 바로 12시");
        float twelve = overlay.TwelveAt;
        yield return overlay.CloseRoutine();
        Assert.IsFalse(overlay.IsShown);
        Debug.Log($"[SongLoadingPlayTest] L4 로딩이 늦을 때: 2.6~3.2초 fillAmount {min:0.0000}~{max:0.0000} (진행 0.9, 0.3을 받아도 그대로), 진행 1을 준 뒤 T = {twelve:0.000}초");
    }

    /// <summary>Canvas 왼쪽 위 기준 (x, y는 위에서 아래로) 사각형 (Canvas 단위 = 1920×1080 기준 px)</summary>
    public static Rect TopLeft(RectTransform r, RectTransform root)
    {
        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        Vector3 min = root.InverseTransformPoint(c[0]);
        Vector3 max = root.InverseTransformPoint(c[2]);
        Rect rr = root.rect;
        return Rect.MinMaxRect(min.x - rr.xMin, rr.yMax - max.y, max.x - rr.xMin, rr.yMax - min.y);
    }
}
