using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 암전 로딩 (기획서 3-10 「암전 로딩」, 인게임 → 결과) 실제 흐름 확인. 이 테스트만 SkipLoadingOverlayForTests를 끄고,
/// 실제 시간(2.5초 · 0.5초 · 0.2초 · 0.45초)을 보므로 재생 배율 1.0([NormalSpeed])으로 돈다. 채보는 테스트 안에서 만든 노트 4개.
/// - A1 일반(MISS 1, 연출 없음) · A2 FC(GREAT 1) · A3 AP · A4 테스트 플레이: 마지막 판정 기준 단계 순서·시각,
///   열기 끝부터 최소 0.2초, 결과 OnEnter는 닫기 끝 뒤, 열기 동안 화면 갱신(노트 Refresh · ClearBanner 시간), 음악 계수 1 → 0 뒤 결과에서 1.
/// - A1은 열기 중 ESC · F5 · A(레인) · 1 · 2 · PgUp · PgDn을 눌러도 아무 일이 없는지 본다.
/// - A3은 결과에서 다시 하기(곡 로딩)로 다음 판에 들어가 음량이 정상인지 본다.
/// 기록은 남지 않는다 (chartId가 비어 있고, 테스트 플레이는 기록하지 않는다).
/// </summary>
public class FadeLoadingPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Key[] BlockedKeys = { Key.Escape, Key.F5, Key.A, Key.Digit1, Key.Digit2, Key.PageUp, Key.PageDown };

    private bool savedSkip;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;
    private static readonly List<string> Report = new List<string>();

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
        InGameScene.TestNormalChart = null;
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

    private static ChartData Chart()
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 4; i++) notes.Add(new NoteData { beat = 4f + i * 0.5f, lane = i, type = NoteData.TypeShort });
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "암전 로딩 확인용",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    private enum Case { Normal, FullCombo, AllPerfect, TestPlay }

    /// <summary>인게임을 바로(오버레이 없이) 불러와 판이 시작될 때까지 기다린다</summary>
    private static IEnumerator LoadInGame(InGameEntryData entry, System.Action<InGameScene> done)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading) yield return null;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        InGameScene scene = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        done(scene);
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

    private struct Sample
    {
        public float Elapsed, Alpha, Music;
    }

    private static string Nearest(List<Sample> samples, float t)
    {
        Sample best = samples[0];
        foreach (Sample s in samples) if (Mathf.Abs(s.Elapsed - t) < Mathf.Abs(best.Elapsed - t)) best = s;
        return $"{best.Elapsed:0.000}초 알파 {best.Alpha:0.000} · 음악 {best.Music:0.000}";
    }

    private IEnumerator RunCase(Case kind, bool tapKeys, bool retryAfter)
    {
        UITheme theme = UITheme.Current;
        float savedMusic = GameManager.Sound.MusicVolume;
        float savedMaster = GameManager.Sound.MasterVolume;
        ChartData chart = Chart();
        InGameEntryData entry;
        if (kind == Case.TestPlay)
        {
            entry = new InGameEntryData
            {
                songId = Song, chartType = ChartType.Custom, mode = InGameMode.TestPlay, scrollMultiplier = 1f,
                testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = 0f, autoPlay = false }
            };
        }
        else
        {
            InGameScene.TestNormalChart = chart;
            float speed = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;
            entry = new InGameEntryData { songId = Song, chartType = ChartType.Custom, mode = InGameMode.Normal, scrollMultiplier = speed };
        }

        InGameScene scene = null;
        yield return LoadInGame(entry, s => scene = s);
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        NoteLayer layer = Get<NoteLayer>(scene, "noteLayer");
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");
        GearView gear = scene.Gear;
        Canvas_FadeLoading fade = GameManager.UI.FadeLoading;
        Assert.IsNotNull(fade, "암전 로딩 오버레이");
        Assert.IsFalse(fade.IsShown, "판 중에는 숨김");

        // 노트 치기: 일반은 마지막을 안 쳐서 MISS, FC는 하나를 +60ms(GREAT)
        float deadline = Time.realtimeSinceStartup + 30f;
        for (int i = 0; i < judge.Notes.Count; i++)
        {
            if (kind == Case.Normal && i == judge.Notes.Count - 1) break;
            PlayNote note = judge.Notes[i];
            double at = note.Time + (kind == Case.FullCombo && i == 1 ? 0.06 : 0);
            while (GameManager.Sound.SongTime < at)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "노트 대기 시간 초과");
                yield return null;
            }
            scene.TestPress(note.Lane, at);
        }
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "판정 대기 시간 초과");
            yield return null;
        }
        yield return null;
        double origin = scene.AllJudgedRealtime;
        ClearBanner banner = canvas.Banner;
        ClearType expectedType = kind == Case.AllPerfect ? ClearType.AllPerfect : kind == Case.FullCombo ? ClearType.FullCombo : ClearType.None;
        Assert.AreEqual(expectedType, banner.Type, kind + " 곡 끝 연출");

        // 2.5초 전에는 아무것도 없다
        while (!GameManager.Scene.IsFadeLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "암전 시작 대기 시간 초과");
            Assert.IsFalse(fade.IsShown);
            yield return null;
        }
        double fadeCalled = Time.unscaledTimeAsDouble - origin;
        Assert.AreEqual(2.5, fadeCalled, 0.05, "모든 노트 판정 뒤 2.5초에 암전 시작");
        Assert.IsTrue(scene.IsFadingToResult);
        Assert.AreEqual(GameConstants.FadeLoading.MinSeconds, fade.MinSeconds, 1e-6, "결과로 가는 암전은 최소 0.2초 그대로 (재시작 0.4초가 남지 않음)");
        float speedBefore = layer.ScrollMultiplier;
        int brightnessBefore = scene.BackgroundBrightness;
        bool musicWasPlaying = GameManager.Sound.IsMusicSourcePlaying;

        // 열기 동안: 알파 · 음악 계수 · 화면 갱신 · 입력 막힘
        List<Sample> open = new List<Sample>();
        double lastRefresh = layer.LastRefreshSongTime;
        int refreshAdvanced = 0;
        float bannerStart = banner.Elapsed;
        float bannerEnd = bannerStart;
        bool keysTapped = false;
        bool anyReaction = false;
        while (SceneManager.GetActiveScene().name == GameConstants.Scenes.InGame && scene != null)
        {
            if (fade.HasOpenStarted && fade.IsOpening)
            {
                open.Add(new Sample { Elapsed = fade.OpenElapsed, Alpha = fade.FadeAlpha, Music = GameManager.Sound.MusicFadeMultiplier });
                if (layer.LastRefreshSongTime > lastRefresh) refreshAdvanced++;
                lastRefresh = layer.LastRefreshSongTime;
                bannerEnd = banner.Elapsed;
                if (tapKeys && !keysTapped && fade.OpenElapsed > 0.1f)
                {
                    keysTapped = true;
                    foreach (Key key in BlockedKeys) yield return Tap(key);
                    anyReaction = GameManager.UI.HasOpenPopup || gear.IsKeyCellPressed(0) || !Mathf.Approximately(layer.ScrollMultiplier, speedBefore)
                                  || scene.BackgroundBrightness != brightnessBefore || !GameManager.Scene.IsFadeLoading;
                    continue;
                }
            }
            yield return null;
        }
        Assert.Greater(open.Count, 5, "열기 프레임 샘플");
        foreach (Sample s in open)
        {
            Assert.AreEqual(FadeLoadingRules.OpenAlpha(s.Elapsed), s.Alpha, 0.12f, $"열기 {s.Elapsed:0.000}초 알파 ≈ t²");
            if (musicWasPlaying) Assert.AreEqual(1f - s.Alpha, s.Music, 0.12f, "음악 계수 = 같은 곡선");
        }
        Assert.Greater(refreshAdvanced, open.Count / 2, "열기 동안 인게임 화면(노트 Refresh)이 계속 갱신됨");
        if (expectedType != ClearType.None) Assert.Greater(bannerEnd, bannerStart, "열기 동안 ClearBanner 시간이 흐름");
        if (tapKeys)
        {
            Assert.IsTrue(keysTapped, "키를 눌렀다");
            Assert.IsFalse(anyReaction, "열기 중 ESC · F5 · 레인 · 1/2 · PgUp/PgDn에 반응 없음");
        }

        // 닫기 동안 알파
        List<Sample> close = new List<Sample>();
        double closeStart = -1;
        while (GameManager.Scene.IsFadeLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 대기 시간 초과");
            if (fade.IsClosing)
            {
                if (closeStart < 0) closeStart = Time.unscaledTimeAsDouble;
                close.Add(new Sample { Elapsed = (float)(Time.unscaledTimeAsDouble - closeStart), Alpha = fade.FadeAlpha, Music = GameManager.Sound.MusicFadeMultiplier });
            }
            yield return null;
        }
        Assert.IsFalse(fade.IsShown, "닫기 끝에 숨김");
        Assert.AreEqual(GameConstants.Scenes.Result, SceneManager.GetActiveScene().name);
        Assert.IsTrue(GameManager.Scene.Current != null && GameManager.Scene.Current.IsEntered, "결과 OnEnter");
        foreach (Sample s in close) Assert.AreEqual(FadeLoadingRules.CloseAlpha(s.Elapsed), s.Alpha, 0.12f, $"닫기 {s.Elapsed:0.000}초 알파");
        Assert.IsTrue(close.Exists(c => c.Alpha > 0.3f && c.Alpha < 0.9f), "닫기 중간 모습이 실제로 보인다 (긴 프레임에 건너뛰지 않음)");
        Assert.Greater(close.Count, 10, "닫기 프레임 수");

        // 단계 순서 · 시각 (마지막 판정 기준)
        IReadOnlyList<LoadingStep> steps = GameManager.Scene.LoadingSteps;
        double offset = GameManager.Scene.LoadingOriginOffset;
        Assert.AreEqual("마지막 판정", GameManager.Scene.LoadingOriginName);
        double At(string prefix)
        {
            foreach (LoadingStep st in steps) if (st.Name.StartsWith(prefix)) return st.Seconds + offset;
            Assert.Fail("단계 없음: " + prefix);
            return 0;
        }
        double openStart = At("열기 시작"), openEnd = At("열기 끝"), exit = At(GameConstants.Scenes.InGame + " OnExit"),
            load = At(GameConstants.Scenes.Result + " 로드 시작"), init = At(GameConstants.Scenes.Result + " Init 끝"),
            closeBegin = At("닫기 시작"), closeEnd = At("닫기 끝"), enter = At(GameConstants.Scenes.Result + " OnEnter");
        Assert.AreEqual(2.5, openStart, 0.06, "열기 시작 2.5초");
        Assert.AreEqual(3.0, openEnd, 0.06, "열기 끝 3.0초");
        Assert.LessOrEqual(openEnd, exit + 1e-6);
        Assert.LessOrEqual(exit, load + 1e-6);
        Assert.LessOrEqual(load, init + 1e-6);
        Assert.GreaterOrEqual(closeBegin - openEnd, GameConstants.FadeLoading.MinSeconds - 1e-6, "열기 끝부터 최소 0.2초");
        Assert.GreaterOrEqual(closeBegin, init - 1e-6, "닫기는 Init 뒤");
        Assert.AreEqual(GameConstants.FadeLoading.CloseSeconds, closeEnd - closeBegin, 0.06, "닫기 0.45초");
        Assert.GreaterOrEqual(enter, closeEnd - 1e-6, "OnEnter는 닫기 끝 뒤");

        // 음량: 설정값 · 계수 · 실제 AudioSource
        Assert.AreEqual(savedMusic, GameManager.Sound.MusicVolume, 1e-6f, "설정 음악 음량 그대로");
        Assert.AreEqual(savedMaster, GameManager.Sound.MasterVolume, 1e-6f, "설정 메인 음량 그대로");
        Assert.AreEqual(1f, GameManager.Sound.MusicFadeMultiplier, 1e-6f, "결과에서 계수 1");
        Assert.AreEqual(savedMaster * savedMusic, GameManager.Sound.MusicSourceVolume, 1e-5f, "AudioSource 음량 = 메인 × 음악");
        Assert.IsFalse(GameManager.Sound.IsMusicSourcePlaying, "곡 음악은 멈춤");

        string bannerText = expectedType != ClearType.None
            ? $" · ClearBanner 0.000초 시작 ~ {ClearBannerTimeline.TotalSeconds:0.000}초 끝 (열기 중 {bannerStart:0.000} → {bannerEnd:0.000}초까지 흐름)" : "";
        LoadingFrameStats openFrames = null;
        foreach (LoadingFrameStats f in GameManager.Scene.LoadingFrames) if (f.Phase == fade.OpeningPhase) openFrames = f;
        string line = $"| {kind} | {fadeCalled:0.000} | {openStart:0.000} | {openEnd:0.000} / OnExit {exit:0.000} / 로드 {load:0.000} | {init:0.000} | {closeBegin:0.000} (열기 끝 +{closeBegin - openEnd:0.000}) | {closeEnd:0.000} | {enter:0.000} |{bannerText}" +
                      $" · 열기 프레임 {(openFrames != null ? openFrames.FrameCount : 0)}개 최대 {(openFrames != null ? openFrames.MaxMs : 0):0.0}ms · Refresh 갱신 {refreshAdvanced}/{open.Count}" +
                      $" · 음악 재생 중이었음 {musicWasPlaying} · 결과 음량 설정 {GameManager.Sound.MusicVolume:0.00} 계수 {GameManager.Sound.MusicFadeMultiplier:0.00} 소스 {GameManager.Sound.MusicSourceVolume:0.00}";
        Report.Add(line);
        Debug.Log("[FadeLoadingPlayTest] " + line);
        Debug.Log($"[FadeLoadingPlayTest] {kind} 열기 샘플: 0.1 → {Nearest(open, 0.1f)} / 0.25 → {Nearest(open, 0.25f)} / 0.5 → {Nearest(open, 0.5f)} | " +
                  $"닫기 샘플: 0.1 → {Nearest(close, 0.1f)} / 0.25 → {Nearest(close, 0.25f)} / 0.45 → {Nearest(close, 0.45f)}");
        Debug.Log($"[FadeLoadingPlayTest] {kind} 단계: {GameManager.Scene.DescribeLoading()}");

        if (!retryAfter) yield break;

        // 다시 하기 (곡 로딩) → 다음 판 음량 정상
        ResultScene result = Object.FindFirstObjectByType<ResultScene>();
        typeof(ResultScene).GetMethod("Retry", Private).Invoke(result, null);
        float retryDeadline = Time.realtimeSinceStartup + 20f;
        InGameScene next = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, retryDeadline, "다시 하기 대기 시간 초과");
            next = Object.FindFirstObjectByType<InGameScene>();
            if (next != null && !GameManager.Scene.IsLoading && Get<bool>(next, "isPlaying") && GameManager.Sound.IsMusicSourcePlaying) break;
            yield return null;
        }
        Assert.AreEqual(1f, GameManager.Sound.MusicFadeMultiplier, 1e-6f, "다음 판 계수 1");
        Assert.AreEqual(savedMaster * savedMusic, GameManager.Sound.MusicSourceVolume, 1e-5f, "다음 판 AudioSource 음량 정상");
        Debug.Log($"[FadeLoadingPlayTest] 다시 하기 뒤 다음 판: 음악 재생 {GameManager.Sound.IsMusicSourcePlaying}, 계수 {GameManager.Sound.MusicFadeMultiplier:0.00}, 소스 음량 {GameManager.Sound.MusicSourceVolume:0.00} (설정 메인 {savedMaster:0.00} × 음악 {savedMusic:0.00})");
    }

    [UnityTest, NormalSpeed]
    public IEnumerator A1_Normal_Flow_InputBlocked()
    {
        yield return RunCase(Case.Normal, true, false);
    }

    [UnityTest, NormalSpeed]
    public IEnumerator A2_FullCombo_Flow()
    {
        yield return RunCase(Case.FullCombo, false, false);
    }

    [UnityTest, NormalSpeed]
    public IEnumerator A3_AllPerfect_Flow_RetryVolume()
    {
        yield return RunCase(Case.AllPerfect, false, true);
    }

    [UnityTest, NormalSpeed]
    public IEnumerator A4_TestPlay_Flow()
    {
        yield return RunCase(Case.TestPlay, false, false);
    }
}
