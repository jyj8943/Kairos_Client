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
/// 일반 로딩 오버레이 (기획서 3-9 「일반 로딩」) 실제 흐름 확인. 이 테스트만 GameSceneManager.SkipLoadingOverlayForTests를 끄고 오버레이 경로를 탄다.
/// - G1 타이틀 → 곡 선택 → 에디터 → 곡 선택: 단계 순서(들어오기 0.5초 뒤 앞 씬 OnExit · 로드, 정리는 닫기 전, 닫기 시작은 들어오기 0초 + 1.5초 뒤,
///   OnEnter는 닫기 끝 뒤), 가는 곳 문구, 로딩 중 ESC · Enter · ↓ · Tab · E · Space를 눌러도 아무 일도 없음
/// - G2 에디터 → 테스트 플레이 → 나가기 → 에디터: 판 시작(OnEnter · 첫 카운트인 클릭)이 닫기 끝 뒤
/// - G3 레이아웃 · 무작위: 띠 y 864~1080, Canvas 안, 가장 긴 TIP · 가는 곳 넘침 없음, TIP 20번 분포 · 연속 중복 0, 일러스트 1장 · 비었을 때 대체 배경
/// 기록 · 마지막 선택은 바꾸지 않는다 (에디터는 새 채보로 열고 저장하지 않는다, 테스트 플레이는 기록하지 않는다).
/// </summary>
public class LoadingPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Key[] BlockedKeys = { Key.Escape, Key.Enter, Key.DownArrow, Key.Tab, Key.E, Key.Space };

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

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading) yield return null;
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), sceneName + " 불러오기 시작");
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName || GameManager.Scene.Current == null || !GameManager.Scene.Current.IsEntered)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, sceneName + " 대기 시간 초과");
            yield return null;
        }
        yield return null;
    }

    private static IEnumerator TapBlockedKeys()
    {
        foreach (Key key in BlockedKeys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }
    }

    /// <summary>
    /// 일반 로딩이 끝나 다음 씬 OnEnter까지 따라가며 입력 차단을 확인한다 (들어오기 중 · 다음 씬 Init 뒤 두 번).
    /// </summary>
    private static IEnumerator Follow(string fromScene, string toScene, StringBuilder log)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        Canvas_Loading overlay = GameManager.UI.Loading;
        Assert.IsTrue(GameManager.Scene.IsGeneralLoading, "일반 로딩 시작");
        Assert.IsTrue(GameManager.Scene.IsLoading, "로딩 중 = 씬 로딩 중 (ESC 무시)");
        Assert.AreEqual(LoadingRules.DestinationOf(toScene), overlay.DestinationText.text, "가는 곳 문구");

        while (overlay.OpenElapsed < 0.1f) yield return null;
        Assert.AreEqual(fromScene, GameManager.Scene.Current.gameObject.scene.name, "들어오기 중에는 앞 씬 그대로");
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        yield return TapBlockedKeys();
        Assert.AreEqual(selected, EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null, "앞 씬 선택 그대로");
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업 안 열림");
        Assert.AreEqual(fromScene, GameManager.Scene.Current != null ? GameManager.Scene.Current.gameObject.scene.name : fromScene, "다른 이동 없음");
        log.AppendLine($"  들어오기·직후 ESC·Enter·↓·Tab·E·Space ({overlay.OpenElapsed:0.00}초): 선택 그대로, 팝업 없음");

        SceneBase next = null;
        while (next == null || !next.IsInitDone)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, toScene + " Init 대기 시간 초과");
            next = GameManager.Scene.Current != null && GameManager.Scene.Current.gameObject.scene.name == toScene ? GameManager.Scene.Current : null;
            yield return null;
        }
        Assert.IsFalse(next.IsEntered, "닫기 전에는 OnEnter 안 함");
        yield return TapBlockedKeys();
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "다음 씬 Init 뒤에도 팝업 안 열림");
        Assert.IsFalse(next.IsEntered);
        log.AppendLine($"  {toScene} Init 뒤 ESC·Enter·↓·Tab·E·Space ({overlay.OpenElapsed:0.00}초): 팝업 없음, OnEnter 전");

        while (GameManager.Scene.IsGeneralLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "일반 로딩 끝 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(next.IsEntered, "나가기 끝 뒤 OnEnter");
        Assert.IsFalse(overlay.IsShown);
        Assert.AreEqual(toScene, SceneManager.GetActiveScene().name);
    }

    private static double At(IReadOnlyList<LoadingStep> steps, string prefix)
    {
        foreach (LoadingStep step in steps)
            if (step.Name.StartsWith(prefix)) return step.Seconds;
        Assert.Fail($"단계 '{prefix}'가 없습니다");
        return 0;
    }

    /// <summary>단계 시각 표를 만들고 순서 조건을 확인한다</summary>
    private static void CheckSteps(string title, string fromScene, string toScene, StringBuilder log)
    {
        IReadOnlyList<LoadingStep> steps = GameManager.Scene.LoadingSteps;
        log.AppendLine($"[{title}] 단계 | Enter부터 (초)");
        foreach (LoadingStep step in steps) log.AppendLine($"  {step.Name} | {step.Seconds:0.000}");
        foreach (LoadingFrameStats stats in GameManager.Scene.LoadingFrames)
            log.AppendLine($"  프레임 [{stats.Phase}] {stats.FrameCount}개, 최대 {stats.MaxMs:0.0}ms, 33ms 넘음 {stats.SlowCount}개");

        double in0 = At(steps, "들어오기 0초");
        double inEnd = At(steps, "들어오기 끝");
        double exit = At(steps, $"{fromScene} OnExit");
        double loadStart = At(steps, $"{toScene} 로드 시작");
        double init = At(steps, $"{toScene} Init 끝");
        double cleanup = At(steps, "정리 끝");
        double closeStart = At(steps, "닫기 시작");
        double closeEnd = At(steps, "닫기 끝");
        double enter = At(steps, $"{toScene} OnEnter");
        Assert.Greater(in0, 0.0, "들어오기 0초는 Enter 다음 프레임");
        Assert.GreaterOrEqual(inEnd - in0, GameConstants.Loading.InSeconds - 1e-3, "들어오기 0.5초");
        Assert.GreaterOrEqual(exit, inEnd, "앞 씬 OnExit는 들어오기 끝 뒤");
        Assert.GreaterOrEqual(loadStart, inEnd, "로드 시작은 들어오기 끝 뒤");
        Assert.GreaterOrEqual(init, loadStart);
        Assert.GreaterOrEqual(cleanup, init, "정리는 Init 뒤");
        Assert.GreaterOrEqual(closeStart, cleanup, "정리는 닫기 전에 끝남");
        Assert.GreaterOrEqual(closeStart - in0, GameConstants.Loading.MinSeconds - 1e-3, "닫기 시작은 들어오기 0초 + 1.5초 이후");
        Assert.GreaterOrEqual(closeEnd - closeStart, GameConstants.Loading.OutSeconds - 1e-3, "나가기 0.5초");
        Assert.GreaterOrEqual(enter, closeEnd, "OnEnter는 닫기 끝 뒤");
    }

    private static EditorEntryData NewChartEntry() => new EditorEntryData { songId = Song, chartName = "일반 로딩 확인", level = 1 };

    [UnityTest]
    public IEnumerator G1_Title_SongSelect_Editor_SongSelect()
    {
        StringBuilder log = new StringBuilder("[LoadingPlayTest] G1 일반 로딩 (오버레이는 실제 시간)\n");
        yield return LoadAndWait(GameConstants.Scenes.Title, null);

        Call(Object.FindFirstObjectByType<Canvas_Title>(), "OnClickPlay");
        yield return Follow(GameConstants.Scenes.Title, GameConstants.Scenes.SongSelect, log);
        CheckSteps("타이틀 → 곡 선택", GameConstants.Scenes.Title, GameConstants.Scenes.SongSelect, log);

        Assert.IsTrue(GameManager.Scene.LoadWithLoading(GameConstants.Scenes.Editor, NewChartEntry()), "곡 선택 → 에디터 (편집 · 새 채보와 같은 함수)");
        yield return Follow(GameConstants.Scenes.SongSelect, GameConstants.Scenes.Editor, log);
        CheckSteps("곡 선택 → 에디터", GameConstants.Scenes.SongSelect, GameConstants.Scenes.Editor, log);

        Call(Object.FindFirstObjectByType<EditorScene>(), "ExitToSongSelect");
        yield return Follow(GameConstants.Scenes.Editor, GameConstants.Scenes.SongSelect, log);
        CheckSteps("에디터 → 곡 선택", GameConstants.Scenes.Editor, GameConstants.Scenes.SongSelect, log);
        Debug.Log(log.ToString());
    }

    private static InGameEntryData TestPlayEntry()
    {
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "일반 로딩 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 64f, lane = 0, type = NoteData.TypeShort } }
        };
        return new InGameEntryData
        {
            songId = Song,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = 0f }
        };
    }

    [UnityTest]
    public IEnumerator G2_Editor_TestPlay_Exit_Editor()
    {
        StringBuilder log = new StringBuilder($"[LoadingPlayTest] G2 에디터 ↔ 테스트 플레이 (재생 배율 x{TestSpeed.CurrentRate})\n");
        yield return LoadAndWait(GameConstants.Scenes.Editor, NewChartEntry());

        // 에디터의 테스트 플레이 · 자동 테스트와 같은 함수 (EditorScene이 LoadWithLoading(InGame, 진입 데이터)를 부른다)
        Assert.IsTrue(GameManager.Scene.LoadWithLoading(GameConstants.Scenes.InGame, TestPlayEntry()));
        yield return Follow(GameConstants.Scenes.Editor, GameConstants.Scenes.InGame, log);
        InGameScene inGame = Object.FindFirstObjectByType<InGameScene>();
        Assert.IsTrue(Get<bool>(inGame, "isPlaying"), "나가기 끝 뒤 판 시작");
        IReadOnlyList<double> clicks = inGame.CountInClickSongTimes;
        Assert.Greater(clicks.Count, 0, "0박부터라 시작 메트로놈 있음");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (GameManager.Sound.SongTime < clicks[0])
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "첫 클릭 대기 시간 초과");
            yield return null;
        }
        GameManager.Scene.MarkLoading("첫 카운트인 클릭 (이 프레임에 지남)");
        CheckSteps("에디터 → 테스트 플레이", GameConstants.Scenes.Editor, GameConstants.Scenes.InGame, log);
        Assert.Greater(At(GameManager.Scene.LoadingSteps, "첫 카운트인 클릭"), At(GameManager.Scene.LoadingSteps, "닫기 끝"), "첫 클릭은 닫기 끝 뒤");

        Call(inGame, "ExitToReturnTarget");
        yield return Follow(GameConstants.Scenes.InGame, GameConstants.Scenes.Editor, log);
        CheckSteps("테스트 플레이 나가기 → 에디터", GameConstants.Scenes.InGame, GameConstants.Scenes.Editor, log);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator G3_Layout_Random_Fallback()
    {
        yield return LoadAndWait(GameConstants.Scenes.Title, null);
        Canvas_Loading overlay = GameManager.UI.Loading;
        RectTransform root = (RectTransform)overlay.transform;
        Assert.AreEqual(GameConstants.Loading.SortOrder, overlay.GetComponent<Canvas>().sortingOrder);
        Assert.Greater(overlay.GetComponent<Canvas>().sortingOrder, GameManager.UI.OverlayCanvas.sortingOrder, "Canvas_Overlay 팝업보다 위");
        Assert.IsFalse(overlay.IsShown, "Init 뒤 숨김");
        Assert.IsTrue(overlay.IsPrewarmed, "미리 그려 둠");
        Assert.AreEqual(1, overlay.IllustCount, "참고 일러스트 1장");

        overlay.Open(GameConstants.Scenes.SongSelect);
        float until = Time.realtimeSinceStartup + GameConstants.Loading.InSeconds + 0.15f;
        while (Time.realtimeSinceStartup < until) yield return null;
        StringBuilder sb = new StringBuilder("[LoadingPlayTest] G3\n");

        Rect band = TopLeft(overlay.BandRect, root);
        sb.AppendLine($"  띠: x {band.xMin:0}~{band.xMax:0}, y {band.yMin:0}~{band.yMax:0}");
        Assert.AreEqual(864f, band.yMin, 0.5f, "띠 위 y 864");
        Assert.AreEqual(1080f, band.yMax, 0.5f, "띠 아래 y 1080");
        Assert.AreEqual(1920f, band.width, 0.5f, "띠 화면 폭");
        Assert.IsFalse(overlay.IsFallbackBackground, "일러스트가 있으면 대체 배경 안 씀");
        Assert.IsTrue(overlay.Illust.gameObject.activeSelf);

        // 가장 긴 TIP · 가는 곳으로 넘침 · Canvas 안
        string longestTip = "", longestDestination = "";
        foreach (GameConstants.LoadingTip tip in GameConstants.Loading.Tips) if (overlay.TipText.GetPreferredValues(tip.Text).x > overlay.TipText.GetPreferredValues(longestTip).x) longestTip = tip.Text;
        foreach (string d in new[] { GameConstants.Loading.ToSongSelect, GameConstants.Loading.ToEditor, GameConstants.Loading.ToTitle, GameConstants.Loading.ToTestPlay })
            if (d.Length > longestDestination.Length) longestDestination = d;
        overlay.TipText.text = longestTip;
        overlay.DestinationText.text = longestDestination;
        LayoutRebuilderAll(overlay);
        overlay.TipText.ForceMeshUpdate();
        overlay.DestinationText.ForceMeshUpdate();
        Assert.IsFalse(overlay.TipText.isTextOverflowing, "가장 긴 TIP 넘침");
        Assert.IsFalse(overlay.DestinationText.isTextOverflowing, "가장 긴 가는 곳 넘침");
        sb.AppendLine($"  가장 긴 TIP '{longestTip}' 폭 {overlay.TipText.GetPreferredValues(longestTip).x:0} / 칸 {overlay.TipText.rectTransform.rect.width:0}, isTextOverflowing {overlay.TipText.isTextOverflowing}");
        sb.AppendLine($"  가장 긴 가는 곳 '{longestDestination}' isTextOverflowing {overlay.DestinationText.isTextOverflowing}");
        List<string> outside = new List<string>();
        foreach (RectTransform r in overlay.GetComponentsInChildren<RectTransform>(false))
        {
            if (r == root || r.name.StartsWith("TMP SubMesh") || r.name == "Img_Illust" || r.GetComponentInParent<MenuBackground>() != null) continue;
            Rect b = TopLeft(r, root);
            if (b.xMin < -0.5f || b.yMin < -0.5f || b.xMax > 1920.5f || b.yMax > 1080.5f) outside.Add(r.name);
        }
        Assert.IsEmpty(outside, "Canvas 밖: " + string.Join(", ", outside));
        Rect tipRow = TopLeft((RectTransform)overlay.TipCategoryText.transform.parent, root);
        Rect loading = TopLeft((RectTransform)overlay.Dots[0].transform.parent.parent, root);
        sb.AppendLine($"  TIP 줄 x {tipRow.xMin:0} y {tipRow.yMin:0} / 불러오는 중 오른쪽 끝 x {loading.xMax:0}, 세로 가운데 y {loading.center.y:0} / Canvas 밖 0개 (일러스트 · 대체 배경 제외)");
        Assert.AreEqual(80f, tipRow.xMin, 0.5f);
        Assert.AreEqual(920f, tipRow.yMin, 0.5f);
        Assert.AreEqual(1860f, loading.xMax, 0.5f);
        Assert.AreEqual(967f, loading.center.y, 0.5f);

        // TIP 무작위 20번: 연속 중복 0
        int[] hits = new int[GameConstants.Loading.Tips.Length];
        int previous = overlay.LastTipIndex, repeats = 0;
        for (int i = 0; i < 20; i++)
        {
            overlay.SetContent(GameConstants.Scenes.SongSelect);
            int tip = overlay.LastTipIndex;
            if (tip == previous) repeats++;
            Assert.AreEqual(GameConstants.Loading.Tips[tip].Category, overlay.TipCategoryText.text, "분류 글자도 같이");
            hits[tip]++;
            previous = tip;
        }
        Assert.AreEqual(0, repeats, "연속 중복 0");
        sb.AppendLine($"  TIP 20번 분포 (TIP 0~6): {string.Join(" / ", System.Array.ConvertAll(hits, h => h.ToString()))}, 연속 중복 {repeats}");

        // 일러스트가 비었을 때: 라벤더 그라데이션
        overlay.SetIllustrations(new Sprite[0]);
        overlay.SetContent(GameConstants.Scenes.Title);
        Assert.IsTrue(overlay.IsFallbackBackground, "비면 대체 배경");
        Assert.IsFalse(overlay.Illust.gameObject.activeSelf);
        overlay.SetIllustrations(Resources.LoadAll<Sprite>(GameConstants.Loading.IllustFolder));
        overlay.SetContent(GameConstants.Scenes.Title);
        Assert.IsFalse(overlay.IsFallbackBackground);
        Assert.AreEqual(0, overlay.LastIllustIndex, "1장이면 늘 그 1장");
        sb.AppendLine("  일러스트 1장: Img_Illust 보임 (번호 0) / 비었을 때: MenuBackground 대체 배경 보임, Img_Illust 숨김, 경고 로그 1번");

        yield return overlay.CloseRoutine();
        Assert.IsFalse(overlay.IsShown);
        Debug.Log(sb.ToString());
    }

    private static void LayoutRebuilderAll(Canvas_Loading overlay)
    {
        foreach (RectTransform r in overlay.GetComponentsInChildren<RectTransform>(true))
            if (r.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(r);
        Canvas.ForceUpdateCanvases();
    }

    private static Rect TopLeft(RectTransform r, RectTransform root)
    {
        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        Vector3 min = root.InverseTransformPoint(c[0]);
        Vector3 max = root.InverseTransformPoint(c[2]);
        Rect rr = root.rect;
        return Rect.MinMaxRect(min.x - rr.xMin, rr.yMax - max.y, max.x - rr.xMin, rr.yMax - min.y);
    }
}
