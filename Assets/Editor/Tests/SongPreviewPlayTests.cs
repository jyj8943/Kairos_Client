using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 곡 미리 듣기 (11/25) 실제 씬 확인 (EnterPlayMode). 미리 듣기 시간은 재생 배율과 상관없는 실제 시간이다.
/// - P1 들어오자마자 시작 · 세 곡 시작 위치 · 빠르게 훑기 · 난이도/탭/채보 줄 연속 · 음량 0/50/100 · 팝업
/// - P2 페이드 · 반복 실제 시각 / P3 타이틀 → 곡 선택 → 곡 로딩 → 인게임 / P4 결과 → 곡 선택 → 타이틀 / P5 에디터 → 곡 선택 (P3~P5만 로딩 오버레이를 켠다)
/// 마지막 선택 · 음량은 끝나면 되돌린다.
/// </summary>
public class SongPreviewPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private bool savedSkip;
    private LastSelectionSaveData savedSelection;
    private float savedMaster, savedMusic;

    private static SoundManager Sound => GameManager.Sound;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkip = GameSceneManager.SkipLoadingOverlayForTests;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        if (Mouse.current != null) InputSystem.DisableDevice(Mouse.current);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading) yield return null;
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
        savedMaster = Sound.MasterVolume;
        savedMusic = Sound.MusicVolume;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameSceneManager.SkipLoadingOverlayForTests = savedSkip;
        if (PlayerDataManager.HasInstance)
        {
            Sound.MasterVolume = savedMaster;
            Sound.MusicVolume = savedMusic;
            GameManager.PlayerData.LastSelection.CopyFrom(savedSelection);
            GameManager.PlayerData.Save();
        }
        foreach (InputDevice device in InputSystem.devices)
            if (device is Mouse && !device.enabled) InputSystem.EnableDevice(device);
        yield return new ExitPlayMode();
    }

    private static SongSelectEntryData Entry() => new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId };

    private static IEnumerator Load(string sceneName, object data)
    {
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), sceneName);
        yield return WaitEntered(sceneName);
    }

    private static IEnumerator WaitEntered(string sceneName)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName || GameManager.Scene.Current == null || !GameManager.Scene.Current.IsEntered)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, sceneName + " 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }

    private static void Call(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, Private);
        Assert.IsNotNull(info, method);
        info.Invoke(target, args);
    }

    private static void Select(Btn_SongItem item) => EventSystem.current.SetSelectedGameObject(item.gameObject);

    [UnityTest]
    public IEnumerator P1_StartPositions_Scan_SameSongKept_Volume()
    {
        StringBuilder log = new StringBuilder("[SongPreviewPlayTest] P1\n");
        yield return Load(GameConstants.Scenes.SongSelect, Entry());
        Canvas_SongSelect view = Object.FindFirstObjectByType<Canvas_SongSelect>();
        SongData first = GameManager.Songs.Find(TestCharts.OfficialSongId);
        Assert.AreSame(first.Clip, Sound.PreviewClip, "들어오자마자 마지막 선택 곡");
        Assert.AreEqual(first.PreviewStart, Sound.PreviewPosition, 0.2, "구간 시작");

        // 세 곡 각각: 고른 뒤 0.3초에 시작 (앞 곡은 고른 순간부터 0.25초에 줄어 있음), 위치 = 길이 × 0.35
        log.Append("| 곡 | 길이 | PreviewStart | 시작 위치 | 고른 뒤 시작까지 |\n|---|---|---|---|---|\n");
        List<Btn_SongItem> order = new List<Btn_SongItem>();
        foreach (Btn_SongItem item in view.SongItems) if (item.Song != first) order.Add(item);
        foreach (Btn_SongItem item in view.SongItems) if (item.Song == first) order.Add(item);
        yield return Wait(0.6f);
        foreach (Btn_SongItem item in order)
        {
            AudioClip before = Sound.PreviewClip;
            float selectedAt = Time.unscaledTime;
            Select(item);
            float fadeBeforeStart = 1f;
            while (Sound.PreviewClip == before)
            {
                fadeBeforeStart = Sound.PreviewFade;
                Assert.Less(Time.unscaledTime - selectedAt, 2f, item.Song.SongId + " 시작 안 함");
                yield return null;
            }
            float delay = Time.unscaledTime - selectedAt;
            double position = Sound.PreviewPosition;
            log.Append($"| {item.Song.SongId} | {item.Song.Length:0.000} | {item.Song.PreviewStart:0.000} | {position:0.000} | {delay:0.000} |\n");
            Assert.AreSame(item.Song.Clip, Sound.PreviewClip);
            Assert.AreEqual(item.Song.Length * 0.35, item.Song.PreviewStart, 1e-3);
            Assert.AreEqual(item.Song.PreviewStart, position, 0.15);
            Assert.That(delay, Is.InRange(0.29f, 0.42f), "고른 뒤 0.3초");
            Assert.Less(fadeBeforeStart, 0.05f, "시작 전에 앞 곡이 다 줄어 있음");
            yield return Wait(0.6f);
        }

        // 다른 곡을 골랐다가 0.15초 뒤 원래 곡으로: 끊기지 않고 되살아남
        double backFrom = Sound.PreviewPosition;
        float backAt = Time.unscaledTime;
        AudioClip kept = Sound.PreviewClip;
        Select(order[0]);
        yield return Wait(0.15f);
        float ducked = Sound.PreviewFade;
        Select(order[order.Count - 1]);
        yield return Wait(0.5f);
        Assert.AreSame(kept, Sound.PreviewClip);
        Assert.Less(ducked, 0.7f, "고른 순간부터 줄어듦");
        Assert.AreEqual(1f, Sound.PreviewFade, 1e-3f, "되살아남");
        Assert.AreEqual(backFrom + (Time.unscaledTime - backAt), Sound.PreviewPosition, 0.2, "위치 이어짐");
        log.Append($"0.15초 만에 원래 곡으로: 줄어든 음량 {ducked:0.00} → {Sound.PreviewFade:0.00}, 위치 이어짐\n");

        // 0.1초 간격으로 3번: 마지막 곡만 재생
        AudioClip current = Sound.PreviewClip;
        HashSet<AudioClip> heard = new HashSet<AudioClip>();
        Btn_SongItem b = order[0], c = order[1];
        foreach (Btn_SongItem item in new[] { b, c, b })
        {
            Select(item);
            float until = Time.unscaledTime + 0.1f;
            while (Time.unscaledTime < until) { heard.Add(Sound.PreviewClip); yield return null; }
        }
        float end = Time.unscaledTime + 0.9f;
        while (Time.unscaledTime < end) { if (Sound.PreviewClip != null) heard.Add(Sound.PreviewClip); yield return null; }
        Assert.IsFalse(heard.Contains(c.Song.Clip), "지나간 곡은 소리 나지 않음");
        Assert.AreSame(b.Song.Clip, Sound.PreviewClip, "마지막 곡");
        Assert.AreEqual(2, heard.Count, "처음 곡과 마지막 곡만");
        log.Append($"0.1초 간격 3번: 들린 곡 {heard.Count}개 (처음 {current.name}, 마지막 {b.Song.Clip.name})\n");

        // 같은 곡: 난이도 · 탭 · 나만의 채보 줄 · + 줄 · 팝업 → 끊기지 않음
        Btn_SongItem firstItem = order[order.Count - 1];
        Select(firstItem);
        yield return Wait(1.2f);
        Assert.AreSame(first.Clip, Sound.PreviewClip);
        Call(view, "SetChosenStop", view.MyChartNavigator.FirstStopOfGroup(IndexOf(view, firstItem)), false);
        double startPosition = Sound.PreviewPosition;
        float startTime = Time.unscaledTime;
        double previous = startPosition;
        int jumps = 0;
        List<string> actions = new List<string>();
        System.Action[] steps =
        {
            () => Call(view, "MoveChart", -1),
            () => Call(view, "MoveChart", 1),
            () => Call(view, "OnSwitchTab", default(InputAction.CallbackContext)),
            () => Call(view, "MoveMyChartVertical", 1),
            () => Call(view, "ChooseStop", view.MyChartNavigator.AddRowOfGroup(view.ChosenStop.Group), "테스트"),
            () => Call(view, "OnStartKey", default(InputAction.CallbackContext)),
            () => GameManager.UI.TopPopup.OnCancelRequested(),
            () => GameManager.UI.Confirm("미리 듣기 확인", "확인", () => { }, () => { }, false),
            () => GameManager.UI.TopPopup.OnCancelRequested(),
            () => Call(view, "OnSwitchTab", default(InputAction.CallbackContext))
        };
        foreach (System.Action step in steps)
        {
            step();
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                double position = Sound.PreviewPosition;
                if (position < previous || position - previous > 0.3) jumps++;
                previous = position;
            }
            Assert.AreEqual(firstItem.Song.SongId, view.ChosenSongId, "같은 곡");
        }
        double expected = startPosition + (Time.unscaledTime - startTime);
        log.Append($"같은 곡 조작 {steps.Length}가지: 튄 프레임 {jumps}, 위치 {startPosition:0.000} → {previous:0.000} (실제 시간으로 예상 {expected:0.000})\n");
        Assert.AreEqual(0, jumps, "재생 위치가 끊기지 않음");
        Assert.AreEqual(expected, previous, 0.2);

        // 음악 음량 0 / 50 / 100 (메인 100)
        Sound.MasterVolume = 1f;
        foreach (float volume in new[] { 0f, 0.5f, 1f })
        {
            Sound.MusicVolume = volume;
            log.Append($"음악 음량 {volume * 100:0}: 미리 듣기 {Sound.PreviewSourceVolume:0.00} (페이드 {Sound.PreviewFade:0.00})\n");
            Assert.AreEqual(volume * Sound.PreviewFade, Sound.PreviewSourceVolume, 1e-4f, "바로 반영");
        }
        Assert.IsFalse(Sound.IsMusicSourcePlaying, "인게임 노래 소스는 쓰지 않음");
        Debug.Log(log.ToString());
    }

    private static int IndexOf(Canvas_SongSelect view, Btn_SongItem item)
    {
        for (int i = 0; i < view.SongItems.Count; i++) if (view.SongItems[i] == item) return i;
        return -1;
    }

    [UnityTest]
    public IEnumerator P2_FadeAndLoop_RealTime()
    {
        AudioClip clip = GameManager.Songs.Find(TestCharts.OfficialSongId).Clip;
        const double start = 40.0, length = 3.0;
        Sound.PlayPreview(clip, start, length);
        float startedAt = Time.unscaledTime;
        StringBuilder log = new StringBuilder("[SongPreviewPlayTest] P2 | 시각 | 페이드 | 위치\n");
        log.Append($"  0.00 | {Sound.PreviewFade:0.00} | {Sound.PreviewPosition:0.000}\n");
        Assert.AreEqual(start, Sound.PreviewPosition, 0.05);
        foreach (float at in new[] { 0.25f, 0.5f, 2f, 2.5f, 2.95f })
        {
            while (Time.unscaledTime - startedAt < at) yield return null;
            float t = Time.unscaledTime - startedAt;
            log.Append($"  {t:0.00} | {Sound.PreviewFade:0.00} | {Sound.PreviewPosition:0.000}\n");
            Assert.AreEqual(SongPreviewRules.GetVolume(t, length), Sound.PreviewFade, 0.12f, $"{at}초");
            Assert.AreEqual(start + t, Sound.PreviewPosition, 0.15);
        }
        while (Sound.PreviewLoopCount == 0) yield return null;
        log.Append($"  {Time.unscaledTime - startedAt:0.00} 반복 | {Sound.PreviewFade:0.00} | {Sound.PreviewPosition:0.000}\n");
        Assert.AreEqual(start, Sound.PreviewPosition, 0.1, "시작 지점으로");
        Assert.Less(Sound.PreviewFade, 0.2f);
        Sound.StopPreview(0.3f);
        yield return Wait(0.4f);
        Assert.IsFalse(Sound.IsPreviewPlaying || Sound.IsPreviewSourcePlaying);
        Debug.Log(log.ToString());
    }

    /// <summary>로딩이 끝날 때까지 프레임마다 본다</summary>
    private class LoadingWatch
    {
        public int CoveredPreviewFrames;
        public int PreviewFrames;
        public int MusicFrames;
        public int OverlapFrames;
        public float PreviewStoppedAt = -1f;
        public int EnterFrame = -1;
        public int PreviewFrame = -1;
        public double PreviewPosition;
    }

    private static IEnumerator Watch(string toScene, LoadingWatch watch)
    {
        float startedAt = Time.unscaledTime;
        float deadline = Time.realtimeSinceStartup + 20f;
        bool loadingSeen = false;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, toScene + " 대기 시간 초과");
            bool loading = GameManager.Scene.IsLoading;
            loadingSeen |= loading;
            bool preview = Sound.IsPreviewSourcePlaying;
            bool music = Sound.IsMusicSourcePlaying;
            if (loading)
            {
                if (preview) watch.PreviewFrames++;
                if (music) watch.MusicFrames++;
                if (!preview && watch.PreviewStoppedAt < 0f) watch.PreviewStoppedAt = Time.unscaledTime - startedAt;
                // 화면을 다 덮은 뒤 (열기 끝 ~ 닫기 끝)
                bool covered = false;
                foreach (LoadingStep step in GameManager.Scene.LoadingSteps) covered |= step.Name.Contains("로드 시작");
                if (covered && preview) watch.CoveredPreviewFrames++;
            }
            if (preview && music) watch.OverlapFrames++;
            SceneBase current = GameManager.Scene.Current;
            if (loadingSeen && !loading && current != null && current.IsEntered && current.gameObject.scene.name == toScene)
            {
                if (watch.EnterFrame < 0) watch.EnterFrame = Time.frameCount;
                if (preview && watch.PreviewFrame < 0)
                {
                    watch.PreviewFrame = Time.frameCount;
                    watch.PreviewPosition = Sound.PreviewPosition;
                }
                if (watch.PreviewFrame >= 0 || Time.frameCount > watch.EnterFrame + 3) yield break;
            }
            yield return null;
        }
    }

    private static void CheckEntered(string title, LoadingWatch watch, StringBuilder log)
    {
        double closeEnd = -1, enter = -1, start = -1;
        foreach (LoadingStep step in GameManager.Scene.LoadingSteps)
        {
            if (step.Name == "닫기 끝") closeEnd = step.Seconds;
            else if (step.Name.EndsWith("SongSelectScene OnEnter")) enter = step.Seconds;
            else if (step.Name.StartsWith("미리 듣기 시작")) start = step.Seconds;
        }
        SongData song = Object.FindFirstObjectByType<Canvas_SongSelect>().CurrentSong;
        log.Append($"| {title} | {closeEnd:0.000} | {enter:0.000} | {start:0.000} | {watch.PreviewFrame - watch.EnterFrame} | {watch.PreviewPosition:0.000} / {song.PreviewStart:0.000} | {watch.PreviewFrames} | {watch.MusicFrames} |\n");
        Assert.GreaterOrEqual(start, closeEnd, "닫기 끝보다 앞서지 않음");
        Assert.AreEqual(enter, start, 1e-6, "OnEnter와 같은 프레임");
        Assert.That(watch.PreviewFrame - watch.EnterFrame, Is.InRange(0, 1));
        Assert.AreEqual(song.PreviewStart, watch.PreviewPosition, 0.15, "구간 시작");
        Assert.AreEqual(0, watch.CoveredPreviewFrames, "로딩 화면이 덮고 있는 동안 미리 듣기 0프레임");
    }

    private const string EnterHeader = "| 경로 | 닫기 끝 | OnEnter | 미리 듣기 시작 | 프레임 차 | 시작 위치 / PreviewStart | 로딩 중 미리 듣기 프레임 | 로딩 중 노래 프레임 |\n|---|---|---|---|---|---|---|---|\n";

    [UnityTest]
    public IEnumerator P3_Title_SongSelect_SongLoading_InGame()
    {
        StringBuilder log = new StringBuilder("[SongPreviewPlayTest] P3\n" + EnterHeader);
        yield return Load(GameConstants.Scenes.Title, null);
        GameManager.PlayerData.LastSelection.official.songId = TestCharts.OfficialSongId;
        GameManager.PlayerData.LastSelection.official.chartId = TestCharts.OfficialChartId;
        GameSceneManager.SkipLoadingOverlayForTests = false;
        Assert.IsFalse(Sound.IsPreviewSourcePlaying || Sound.IsMusicSourcePlaying, "타이틀에는 소리 없음");

        Call(Object.FindFirstObjectByType<Canvas_Title>(), "OnClickPlay");
        LoadingWatch enter = new LoadingWatch();
        yield return Watch(GameConstants.Scenes.SongSelect, enter);
        CheckEntered("타이틀 → 곡 선택", enter, log);
        Assert.AreEqual(0, enter.PreviewFrames);
        Assert.AreEqual(0, enter.MusicFrames);

        // 곡 로딩 → 인게임
        yield return Wait(0.6f);
        Call(Object.FindFirstObjectByType<Canvas_SongSelect>(), "OnStartKey", default(InputAction.CallbackContext));
        Assert.IsTrue(GameManager.Scene.IsSongLoading, "곡 로딩 시작");
        LoadingWatch leave = new LoadingWatch();
        yield return Watch(GameConstants.Scenes.InGame, leave);
        log.Append($"곡 선택 → 곡 로딩 → 인게임: 미리 듣기 멈춤 {leave.PreviewStoppedAt:0.000}초, 로드 시작 뒤 미리 듣기 {leave.CoveredPreviewFrames}프레임, 인게임 노래와 겹침 {leave.OverlapFrames}프레임\n");
        Assert.That(leave.PreviewStoppedAt, Is.InRange(0.2f, 0.5f), "열기 연출(1.1초) 중 0.3초 만에 멈춤");
        Assert.AreEqual(0, leave.CoveredPreviewFrames);
        Assert.AreEqual(0, leave.OverlapFrames);
        Assert.IsFalse(Sound.IsPreviewPlaying);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator P4_Result_SongSelect_Title()
    {
        StringBuilder log = new StringBuilder("[SongPreviewPlayTest] P4\n" + EnterHeader);
        ResultData result = ResultData.FromScore(new ScoreCalculator(1));
        result.songId = TestCharts.OfficialSongId;
        result.songTitle = "Critical Velocity";
        result.chartType = ChartType.Official;
        result.difficulty = GameConstants.Chart.DifficultyNormal;
        result.level = 7;
        result.isTestPlay = true;
        yield return Load(GameConstants.Scenes.Result, result);
        Assert.IsFalse(Sound.IsPreviewSourcePlaying || Sound.IsMusicSourcePlaying, "결과에는 소리 없음");

        GameSceneManager.SkipLoadingOverlayForTests = false;
        Assert.IsTrue(GameManager.Scene.LoadWithLoading(GameConstants.Scenes.SongSelect, Entry()));
        LoadingWatch enter = new LoadingWatch();
        yield return Watch(GameConstants.Scenes.SongSelect, enter);
        CheckEntered("결과 → 곡 선택", enter, log);
        Assert.AreEqual(0, enter.PreviewFrames);

        yield return Wait(0.6f);
        Object.FindFirstObjectByType<Canvas_SongSelect>().ExitToTitle();
        LoadingWatch leave = new LoadingWatch();
        yield return Watch(GameConstants.Scenes.Title, leave);
        log.Append($"곡 선택 → 일반 로딩 → 타이틀: 미리 듣기 멈춤 {leave.PreviewStoppedAt:0.000}초, 로드 시작 뒤 {leave.CoveredPreviewFrames}프레임\n");
        Assert.That(leave.PreviewStoppedAt, Is.InRange(0.2f, 0.5f));
        Assert.AreEqual(0, leave.CoveredPreviewFrames);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator P5_Editor_SongSelect()
    {
        StringBuilder log = new StringBuilder("[SongPreviewPlayTest] P5\n" + EnterHeader);
        yield return Load(GameConstants.Scenes.Editor, new EditorEntryData { songId = TestCharts.OfficialSongId, chartName = "미리 듣기 확인", level = 1 });
        EditorScene editor = Object.FindFirstObjectByType<EditorScene>();
        while (!(bool)typeof(EditorScene).GetField("isReady", Private).GetValue(editor)) yield return null;
        // 에디터에서 곡을 재생하다가 나간다
        ((EditorPlayback)typeof(EditorScene).GetField("playback", Private).GetValue(editor)).Play();
        yield return Wait(0.4f);
        Assert.IsTrue(Sound.IsMusicSourcePlaying, "에디터 재생 중");

        GameSceneManager.SkipLoadingOverlayForTests = false;
        Call(editor, "ExitToSongSelect");
        Assert.IsTrue(GameManager.Scene.IsGeneralLoading);
        LoadingWatch enter = new LoadingWatch();
        yield return Watch(GameConstants.Scenes.SongSelect, enter);
        CheckEntered("에디터(재생 중) → 곡 선택", enter, log);
        Assert.AreEqual(0, enter.PreviewFrames);
        Assert.AreEqual(0, enter.MusicFrames, "로딩 동안 에디터 노래가 남지 않음");
        Debug.Log(log.ToString());
    }
}
