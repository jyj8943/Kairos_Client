using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 인게임 재시작 암전 (11/28, F5 · 일시정지 → 다시 하기) 실제 흐름 확인. 이 테스트만 SkipLoadingOverlayForTests를 끈다.
/// 암전 시간(덮기 0.5 · 최소 0.4 · 걷기 0.45초)은 unscaled라 재생 배율과 상관없이 실제 시간으로 잰다. 채보는 테스트 안에서 만든 노트 4개.
/// - R1 재생 중 F5: 단계 순서 · 최소 0.4초 · 덮기 동안 알파 / 음악 계수 1 → 0 · 판 멈춤(노트 Refresh · 판정) · 키 무시(F5 연타 포함, 재시작 1회) · 새 판 상태.
/// - R2 일시정지 → 다시 하기 확인: 확인 팝업은 닫히고 일시정지 팝업은 덮는 동안 그대로, 음악은 멈춘 채, 새 판에서 팝업 없음.
/// - R3 테스트 플레이 + 불러오기 지연(TestExtraLoadSeconds 0.8초): 걷기는 불러오기(지연 · 정리) 끝 뒤, 같은 시작 위치.
/// 기록은 남지 않는다 (chartId가 비어 있고, 결과 씬을 거치지 않는다). 일반 플레이 배속은 저장된 마지막 배속 그대로 쓴다 (PlayerData가 바뀌지 않게).
/// </summary>
public class RestartFadePlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Key[] BlockedKeys = { Key.A, Key.F5, Key.Escape, Key.Digit1, Key.Digit2, Key.PageUp, Key.PageDown };

    private bool savedSkip;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkip = GameSceneManager.SkipLoadingOverlayForTests;
        GameSceneManager.SkipLoadingOverlayForTests = false;
        GameSceneManager.TestExtraLoadSeconds = 0;
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
        GameSceneManager.TestExtraLoadSeconds = 0;
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
        for (int i = 0; i < 4; i++) notes.Add(new NoteData { beat = 4f + i * 2f, lane = i, type = NoteData.TypeShort });
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "재시작 암전 확인용",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    private static InGameEntryData NormalEntry()
    {
        InGameScene.TestNormalChart = Chart();
        float speed = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;
        return new InGameEntryData { songId = Song, chartType = ChartType.Custom, mode = InGameMode.Normal, scrollMultiplier = speed };
    }

    private static InGameEntryData TestPlayEntry()
    {
        return new InGameEntryData
        {
            songId = Song, chartType = ChartType.Custom, mode = InGameMode.TestPlay, scrollMultiplier = 1.5f,
            testPlay = new TestPlayData { songId = Song, chart = Chart(), startBeat = 0f, autoPlay = false }
        };
    }

    /// <summary>인게임을 바로(오버레이 없이) 불러와 판이 시작될 때까지 기다린다</summary>
    private static IEnumerator LoadInGame(InGameEntryData entry, System.Action<InGameScene> done)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading) yield return null;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        InGameScene scene;
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
        if (samples.Count == 0) return "샘플 없음";
        Sample best = samples[0];
        foreach (Sample s in samples) if (Mathf.Abs(s.Elapsed - t) < Mathf.Abs(best.Elapsed - t)) best = s;
        return $"{best.Elapsed:0.000}초 알파 {best.Alpha:0.000} · 음악 {best.Music:0.000}";
    }

    private enum Case { F5Playing, PauseConfirm, TestPlaySlowLoad }

    private IEnumerator RunCase(Case kind)
    {
        float savedMusic = GameManager.Sound.MusicVolume;
        float savedMaster = GameManager.Sound.MasterVolume;
        bool isTestPlay = kind == Case.TestPlaySlowLoad;
        InGameScene scene = null;
        yield return LoadInGame(isTestPlay ? TestPlayEntry() : NormalEntry(), s => scene = s);
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        NoteLayer layer = Get<NoteLayer>(scene, "noteLayer");
        ScoreCalculator score = Get<ScoreCalculator>(scene, "score");
        Canvas_FadeLoading fade = GameManager.UI.FadeLoading;
        Assert.IsNotNull(fade, "암전 로딩 오버레이");
        Assert.IsFalse(fade.IsShown, "판 중에는 숨김");
        float speedBefore = layer.ScrollMultiplier;
        float startBeatBefore = isTestPlay ? Get<InGameEntryData>(scene, "entry").testPlay.startBeat : 0f;

        // 첫 노트를 쳐서 점수 · 콤보를 0이 아니게 만든다 (노래 재생 중)
        float deadline = Time.realtimeSinceStartup + 30f;
        PlayNote first = judge.Notes[0];
        while (GameManager.Sound.SongTime < first.Time)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "첫 노트 대기 시간 초과");
            yield return null;
        }
        scene.TestPress(first.Lane, first.Time);
        yield return null;
        Assert.AreEqual(1, score.Combo, "재시작 전 콤보 1");
        Assert.IsTrue(GameManager.Sound.IsMusicSourcePlaying, "재시작 전 노래 재생 중");

        Popup_Pause pause = null;
        if (kind == Case.PauseConfirm)
        {
            scene.OnBack();
            yield return null;
            pause = GameManager.UI.TopPopup as Popup_Pause;
            Assert.IsNotNull(pause, "ESC → 일시정지 메뉴");
            Assert.IsTrue(GameManager.Sound.IsMusicPaused, "일시정지 중 노래 멈춤");
            typeof(InGameScene).GetMethod("OnClickRestart", Private).Invoke(scene, null);
            yield return null;
            Popup_Confirm confirm = GameManager.UI.TopPopup as Popup_Confirm;
            Assert.IsNotNull(confirm, "다시 하기 확인 팝업");
            confirm.ConfirmButton.onClick.Invoke();
            Assert.IsFalse(confirm.IsOpen, "확인 팝업은 닫힘");
            Assert.IsTrue(pause.IsOpen, "일시정지 팝업은 덮는 동안 그대로");
        }
        else
        {
            if (kind == Case.TestPlaySlowLoad) GameSceneManager.TestExtraLoadSeconds = 0.8;
            Assert.IsTrue(scene.TestPressRestartKey(), "F5 재시작");
        }

        Assert.IsTrue(GameManager.Scene.IsFadeLoading, "재시작은 암전 로딩");
        Assert.IsTrue(Get<bool>(scene, "isFinished"), "판 멈춤 (isFinished)");
        Assert.IsFalse(Get<bool>(scene, "isPlaying"), "판 멈춤 (isPlaying)");
        Assert.AreEqual(0, GameManager.Sound.ActiveScheduledSfxCount, "아직 안 울린 시작 메트로놈 클릭은 바로 취소");
        bool musicWasPlaying = kind != Case.PauseConfirm;
        Assert.AreEqual(musicWasPlaying, fade.FadesMusic, "재생 중이면 음악을 줄이고, 일시정지 중이면 멈춘 채");
        Assert.AreEqual(GameConstants.FadeLoading.RestartMinSeconds, fade.MinSeconds, 1e-6, "이번 암전 최소 시간 0.4초");

        // 덮기 · 검은 화면 · 걷기 동안: 알파 · 음악 계수 · 판 멈춤 · 키 무시
        List<Sample> open = new List<Sample>();
        double frozenRefresh = layer.LastRefreshSongTime;
        int judgedBefore = judge.JudgedCount;
        int refreshMoved = 0, judgedMoved = 0, restartAccepted = 0, pauseClosedDuringCover = 0, musicPlayingWhilePaused = 0;
        bool keysTappedOpen = false, keysTappedBlack = false, anyReaction = false;
        InGameScene oldScene = scene;
        InGameScene next = null;
        while (GameManager.Scene.IsFadeLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "재시작 암전 대기 시간 초과");
            bool oldAlive = oldScene != null && SceneManager.GetActiveScene().name == GameConstants.Scenes.InGame && oldScene.gameObject.scene.isLoaded;
            if (fade.HasOpenStarted && fade.IsOpening)
            {
                open.Add(new Sample { Elapsed = fade.OpenElapsed, Alpha = fade.FadeAlpha, Music = GameManager.Sound.MusicFadeMultiplier });
                if (oldAlive && layer.LastRefreshSongTime != frozenRefresh) refreshMoved++;
                if (oldAlive && judge.JudgedCount != judgedBefore) judgedMoved++;
                if (pause != null && !pause.IsOpen) pauseClosedDuringCover++;
                if (!musicWasPlaying && GameManager.Sound.IsMusicSourcePlaying && !GameManager.Sound.IsMusicPaused) musicPlayingWhilePaused++;
            }
            // F5 연타 (테스트 경로): 옛 판이 살아 있는 동안 매 프레임
            if (oldAlive && oldScene.TestPressRestartKey()) restartAccepted++;
            // 실제 키: 덮기 중간에 한 번, 검은 화면에서 한 번
            if (!keysTappedOpen && fade.HasOpenStarted && fade.IsOpening && fade.OpenElapsed > 0.1f)
            {
                keysTappedOpen = true;
                foreach (Key key in BlockedKeys) yield return Tap(key);
                anyReaction |= oldAlive && (oldScene.Gear.IsKeyCellPressed(0) || !Mathf.Approximately(layer.ScrollMultiplier, speedBefore));
                anyReaction |= GameManager.UI.HasOpenPopup && !(GameManager.UI.TopPopup is Popup_Pause && pause != null);
                continue;
            }
            if (!keysTappedBlack && fade.IsShown && fade.HasOpenStarted && !fade.IsOpening && !fade.IsClosing)
            {
                keysTappedBlack = true;
                foreach (Key key in BlockedKeys) yield return Tap(key);
                continue;
            }
            yield return null;
        }
        Assert.AreEqual(0, judgedMoved, "덮기 동안 판정 멈춤 (놓침 MISS 없음)");
        Assert.IsTrue(keysTappedOpen, "덮기 중 키를 눌렀다");
        Assert.IsFalse(anyReaction, "덮기 중 레인 · F5 · ESC · 1/2 · PgUp/PgDn에 반응 없음");
        Assert.AreEqual(0, restartAccepted, "F5 연타해도 재시작은 한 번");
        Assert.AreEqual(0, refreshMoved, "덮기 동안 노트 움직임 멈춤");
        Assert.AreEqual(0, pauseClosedDuringCover, "일시정지 팝업은 덮는 동안 그대로");
        Assert.AreEqual(0, musicPlayingWhilePaused, "일시정지 중 재시작은 음악이 멈춘 채");
        Assert.Greater(open.Count, 5, "덮기 프레임 샘플");
        foreach (Sample s in open)
        {
            Assert.AreEqual(FadeLoadingRules.OpenAlpha(s.Elapsed), s.Alpha, 0.12f, $"덮기 {s.Elapsed:0.000}초 알파 ≈ t²");
            Assert.AreEqual(musicWasPlaying ? 1f - s.Alpha : 1f, s.Music, 0.12f, $"덮기 {s.Elapsed:0.000}초 음악 계수");
        }

        // 새 판
        double enterDsp = AudioSettings.dspTime;
        next = Object.FindFirstObjectByType<InGameScene>();
        Assert.IsNotNull(next, "새 인게임");
        Assert.AreNotSame(oldScene, next, "씬을 다시 불러옴");
        Assert.IsTrue(next.IsEntered, "새 인게임 OnEnter");
        Assert.IsFalse(fade.IsShown, "걷힌 뒤 숨김");
        Assert.AreEqual(GameConstants.FadeLoading.MinSeconds, fade.MinSeconds, 1e-6, "숨기면 최소 시간이 기본 0.2초로 돌아감 (다음 암전에 남지 않음)");
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "새 판에서 팝업 없음");
        ScoreCalculator newScore = Get<ScoreCalculator>(next, "score");
        Assert.AreEqual(0, newScore.JudgedCount, "판정 개수 0");
        Assert.AreEqual(0, newScore.Combo, "콤보 0");
        Assert.AreEqual(0, newScore.PerfectCount + newScore.GreatCount + newScore.GoodCount + newScore.MissCount, "판정별 개수 0");
        NoteLayer newLayer = Get<NoteLayer>(next, "noteLayer");
        Assert.AreEqual(speedBefore, newLayer.ScrollMultiplier, 1e-6f, "배속 유지");
        if (isTestPlay) Assert.AreEqual(startBeatBefore, Get<InGameEntryData>(next, "entry").testPlay.startBeat, 1e-6f, "테스트 플레이 같은 시작 위치");
        Assert.IsTrue(next.UsesCountIn, "시작 메트로놈 씀");
        Assert.AreEqual(4, next.CountInClickDspTimes.Count, "새 판 클릭 4개 예약");
        foreach (double dsp in next.CountInClickDspTimes) Assert.Greater(dsp, enterDsp - 1e-3, "첫 카운트인 클릭은 걷기 끝 뒤");
        Assert.AreEqual(next.CountInClickDspTimes.Count, GameManager.Sound.ActiveScheduledSfxCount, "예약은 새 판 클릭만 (이전 판 클릭 없음)");
        Assert.IsFalse(Get<bool>(next, "isKeyHintOver"), "시작 키 글자 안내 진행 중");
        Assert.AreEqual(savedMusic, GameManager.Sound.MusicVolume, 1e-6f, "설정 음악 음량 그대로");
        Assert.AreEqual(savedMaster, GameManager.Sound.MasterVolume, 1e-6f, "설정 메인 음량 그대로");
        Assert.AreEqual(1f, GameManager.Sound.MusicFadeMultiplier, 1e-6f, "새 판 음악 계수 1");
        Assert.AreEqual(savedMaster * savedMusic, GameManager.Sound.MusicSourceVolume, 1e-5f, "AudioSource 음량 = 메인 × 음악");
        Assert.IsFalse(GameManager.Sound.IsMusicPaused, "새 판 노래는 멈춤 상태가 아님");

        // 단계 순서 · 시각 (다시 하기 기준)
        IReadOnlyList<LoadingStep> steps = GameManager.Scene.LoadingSteps;
        double offset = GameManager.Scene.LoadingOriginOffset;
        Assert.AreEqual("다시 하기", GameManager.Scene.LoadingOriginName);
        int loadStarts = 0;
        foreach (LoadingStep st in steps) if (st.Name.StartsWith(GameConstants.Scenes.InGame + " 로드 시작")) loadStarts++;
        Assert.AreEqual(1, loadStarts, "재시작(로드)은 한 번");
        double At(string prefix)
        {
            foreach (LoadingStep st in steps) if (st.Name.StartsWith(prefix)) return st.Seconds + offset;
            Assert.Fail("단계 없음: " + prefix);
            return 0;
        }
        string inGame = GameConstants.Scenes.InGame;
        double openStart = At("열기 시작"), openEnd = At("열기 끝"), exit = At(inGame + " OnExit"),
            loadStart = At(inGame + " 로드 시작"), loadEnd = At(inGame + " 로드 끝"), init = At(inGame + " Init 끝"),
            cleanup = At("정리 끝"), closeBegin = At("닫기 시작"), closeEnd = At("닫기 끝"), enter = At(inGame + " OnEnter");
        double delayEnd = isTestPlay ? At("테스트 지연 끝") : init;
        Assert.LessOrEqual(openStart, openEnd);
        Assert.AreEqual(GameConstants.FadeLoading.OpenSeconds, openEnd - openStart, 0.06, "덮기 0.5초");
        Assert.LessOrEqual(openEnd, exit + 1e-6, "OnExit는 덮기 끝 뒤");
        Assert.LessOrEqual(openEnd, loadStart + 1e-6, "로드 시작은 덮기 끝 뒤");
        Assert.LessOrEqual(loadStart, loadEnd + 1e-6);
        Assert.LessOrEqual(loadEnd, init + 1e-6);
        Assert.LessOrEqual(init, cleanup + 1e-6);
        Assert.GreaterOrEqual(closeBegin - openEnd, GameConstants.FadeLoading.RestartMinSeconds - 1e-6, "덮기 끝부터 최소 0.4초");
        Assert.GreaterOrEqual(closeBegin, cleanup - 1e-6, "걷기는 정리 끝 뒤");
        Assert.GreaterOrEqual(closeBegin, delayEnd - 1e-6, "걷기는 불러오기(지연 포함) 끝 뒤");
        if (isTestPlay) Assert.GreaterOrEqual(closeBegin - openEnd, 0.8 - 1e-6, "지연 0.8초면 0.4초보다 늦게 걷힘");
        Assert.AreEqual(GameConstants.FadeLoading.CloseSeconds, closeEnd - closeBegin, 0.06, "걷기 0.45초");
        Assert.GreaterOrEqual(enter, closeEnd - 1e-6, "OnEnter는 걷기 끝 뒤");

        // 첫 카운트인 클릭 (DSP → 다시 하기 기준 시각으로 바꿔 적는다)
        double firstClickAt = enter + (next.CountInClickDspTimes[0] - enterDsp);
        Debug.Log($"[RestartFadePlayTest] {kind} 단계 (다시 하기 기준 초): 덮기 시작 {openStart:0.000} / 덮기 끝 {openEnd:0.000} / OnExit {exit:0.000} / " +
                  $"로드 시작 {loadStart:0.000} · 끝 {loadEnd:0.000} / Init 끝 {init:0.000}{(isTestPlay ? $" / 테스트 지연 끝 {delayEnd:0.000}" : "")} / 정리 끝 {cleanup:0.000} / " +
                  $"걷기 시작 {closeBegin:0.000} (덮기 끝 +{closeBegin - openEnd:0.000}) · 끝 {closeEnd:0.000} / OnEnter {enter:0.000} / 첫 카운트인 클릭 ≈ {firstClickAt:0.000} | " +
                  $"덮기 샘플 0 → {Nearest(open, 0f)} / 0.25 → {Nearest(open, 0.25f)} / 0.5 → {Nearest(open, 0.5f)} | " +
                  $"검은 화면 키 {keysTappedBlack} · 새 판 배속 {newLayer.ScrollMultiplier:0.0} · 클릭 예약 {GameManager.Sound.ActiveScheduledSfxCount} · 음악 계수 {GameManager.Sound.MusicFadeMultiplier:0.00} · 소스 음량 {GameManager.Sound.MusicSourceVolume:0.00}");
        Debug.Log($"[RestartFadePlayTest] {kind} 전체: {GameManager.Scene.DescribeLoading()}");
    }

    [UnityTest]
    public IEnumerator R1_F5WhilePlaying_FadeRestart()
    {
        yield return RunCase(Case.F5Playing);
    }

    [UnityTest]
    public IEnumerator R2_PauseRestartConfirm_FadeRestart()
    {
        yield return RunCase(Case.PauseConfirm);
    }

    [UnityTest]
    public IEnumerator R3_TestPlay_SlowLoad_RevealAfterLoad()
    {
        yield return RunCase(Case.TestPlaySlowLoad);
    }
}
