using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 인게임 시작 메트로놈 실제 씬 확인 (기획서 3-4 「시작 메트로놈」, EnterPlayMode + Bootstrapper 방식).
/// Critical Velocity NORMAL 공식 채보만 쓴다. 판 시작 직후(노래 시작 전후)까지만 보고 끝낸다 (곡 시간 20초 안).
/// </summary>
public class CountInPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>같은 DSP 시각으로 볼 차이 (초)</summary>
    private const double DspTolerance = 1e-6;

    private InGameScene scene;

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

    private static ChartData LoadOfficial()
    {
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);
        return chart;
    }

    private static InGameEntryData NormalEntry()
    {
        return new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = 1f
        };
    }

    private static InGameEntryData TestPlayEntry(ChartData chart, float startBeat)
    {
        return new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Official,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = chart, startBeat = startBeat }
        };
    }

    private IEnumerator LoadInGame(InGameEntryData entry)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        yield return WaitInGameStarted(null);
    }

    /// <summary>새 InGameScene이 OnEnter를 마칠 때까지 기다린다 (previous와 다른 오브젝트)</summary>
    private IEnumerator WaitInGameStarted(InGameScene previous)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && scene != previous && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    /// <summary>예약한 클릭 4개: 각 DSP 시각 = 곡 시간 변환값, 모두 지금 이후, 예약 효과음 소스 4개</summary>
    private void AssertFourClicksScheduled(string label)
    {
        Assert.IsTrue(scene.UsesCountIn, $"{label}: 시작 메트로놈 씀");
        IReadOnlyList<double> songTimes = scene.CountInClickSongTimes;
        IReadOnlyList<double> dspTimes = scene.CountInClickDspTimes;
        Assert.AreEqual(4, songTimes.Count, $"{label}: 클릭 곡 시간 4개");
        Assert.AreEqual(4, dspTimes.Count, $"{label}: 예약한 클릭 4개");
        for (int i = 0; i < 4; i++)
            Assert.AreEqual(GameManager.Sound.GetDspTimeOfSongTime(songTimes[i]), dspTimes[i], DspTolerance, $"{label}: {i + 1}번째 클릭 DSP 시각");
        Assert.AreEqual(4, GameManager.Sound.ActiveScheduledSfxCount, $"{label}: 예약 중인 효과음 소스");
    }

    // ───── 항목 ─────

    /// <summary>5: 일반 플레이 시작 → 클릭 4개 예약, DSP 시각 = 곡 시간 변환값, 노래 시작(곡 시간 0) 전에 4개 모두 지나감</summary>
    [UnityTest]
    public IEnumerator C5_NormalPlay_SchedulesFourClicksBeforeMusic()
    {
        yield return LoadInGame(NormalEntry());
        AssertFourClicksScheduled("판 시작");

        double[] dsp = new double[4];
        for (int i = 0; i < 4; i++) dsp[i] = scene.CountInClickDspTimes[i];
        double musicStartDsp = GameManager.Sound.GetDspTimeOfSongTime(0);
        double initialSongTime = -scene.StartDelaySeconds;
        for (int i = 0; i < 4; i++) Assert.Less(dsp[i], musicStartDsp, $"{i + 1}번째 클릭이 노래 시작 전");

        // −1마디에는 마디선이 없다: 첫 선 = 0박
        TimingMap timing = Get<TimingMap>(scene, "timing");
        MeasureLineLayer lines = Get<MeasureLineLayer>(scene, "measureLineLayer");
        Assert.AreEqual(timing.BeatToSeconds(0), lines.GetTime(0), 1e-9, "첫 마디선 = 0박 (−1마디 선 없음)");

        float deadline = TestSpeed.Deadline(10f);
        while (GameManager.Sound.SongTime < 0.05)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "노래 시작 대기 시간 초과");
            yield return null;
        }
        double dspNow = AudioSettings.dspTime;
        for (int i = 0; i < 4; i++) Assert.Less(dsp[i], dspNow, $"노래 시작 뒤 {i + 1}번째 클릭이 이미 지나감");

        double oldDelay = CountInRules.GetNormalStartDelay(Get<NoteJudge>(scene, "judge").Notes[0].Time);
        Debug.Log($"[CountInTest] C5: 시작 지연 {oldDelay:0.###} → {scene.StartDelaySeconds:0.###}초,판 시작 곡 시간 ≈ {initialSongTime:0.###}초, " +
                  $"클릭 곡 시간 {scene.CountInClickSongTimes[0]:0.###}/{scene.CountInClickSongTimes[1]:0.###}/{scene.CountInClickSongTimes[2]:0.###}/{scene.CountInClickSongTimes[3]:0.###}초, " +
                  $"클릭 간격(DSP) {(dsp[1] - dsp[0]) * 1000:0.#}ms (배율 x{TestSpeed.CurrentRate}), 노래 시작 DSP − 마지막 클릭 {(musicStartDsp - dsp[3]) * 1000:0.#}ms");
    }

    /// <summary>6: 클릭 도중 F5 재시작 → 이전 예약은 취소되고 새 판의 4개만 남는다</summary>
    [UnityTest]
    public IEnumerator C6_RestartDuringClicks_KeepsOnlyNewFour()
    {
        yield return LoadInGame(NormalEntry());
        double firstClick = scene.CountInClickSongTimes[0];
        double lastClick = scene.CountInClickSongTimes[3];

        float deadline = TestSpeed.Deadline(10f);
        while (GameManager.Sound.SongTime <= firstClick)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "첫 클릭 대기 시간 초과");
            yield return null;
        }
        double restartSongTime = GameManager.Sound.SongTime;
        Assert.Less(restartSongTime, lastClick, "클릭 도중 (마지막 클릭 전)");

        InGameScene previous = scene;
        Assert.IsTrue(scene.TestPressRestartKey(), "F5 재시작");
        Assert.AreEqual(0, GameManager.Sound.ActiveScheduledSfxCount, "재시작 직후 이전 예약 취소");
        yield return null;
        yield return WaitInGameStarted(previous);

        AssertFourClicksScheduled("재시작 후");
        double dspNow = AudioSettings.dspTime;
        foreach (double dsp in scene.CountInClickDspTimes) Assert.Greater(dsp, dspNow, "새 클릭은 모두 앞으로 울림");

        Debug.Log($"[CountInTest] C6: 재시작 곡 시간 {restartSongTime:0.###}초 (클릭 {firstClick:0.###} ~ {lastClick:0.###}초 사이) → 새 예약 {scene.CountInClickDspTimes.Count}개, 예약 소스 {GameManager.Sound.ActiveScheduledSfxCount}개");
    }

    /// <summary>7: 곡 중간(12마디 = 48박) 테스트 플레이 → 클릭 0개, 시작 지연이 예전 규칙(TestPlayRules.GetMusicStart)과 같다, −1마디 선 없음</summary>
    [UnityTest]
    public IEnumerator C7_MidSongTestPlay_NoClicks()
    {
        const float startBeat = 48f;
        yield return LoadInGame(TestPlayEntry(LoadOfficial(), startBeat));

        Assert.IsFalse(scene.UsesCountIn, "곡 중간 테스트 플레이는 메트로놈 없음");
        Assert.AreEqual(0, scene.CountInClickDspTimes.Count, "예약한 클릭");
        Assert.AreEqual(0, GameManager.Sound.ActiveScheduledSfxCount, "예약 중인 효과음 소스");

        TimingMap timing = Get<TimingMap>(scene, "timing");
        TestPlayRules.MusicStart expected = TestPlayRules.GetMusicStart(timing.BeatToSeconds(startBeat));
        Assert.AreEqual(expected.DelaySeconds, scene.StartDelaySeconds, 1e-9, "시작 지연 = 예전 규칙");
        Assert.AreEqual(expected.StartPositionSeconds, Get<double>(scene, "musicStartPosition"), 1e-9, "음원 시작 위치 = 예전 규칙");

        MeasureLineLayer lines = Get<MeasureLineLayer>(scene, "measureLineLayer");
        Assert.AreEqual(timing.BeatToSeconds(startBeat), lines.GetTime(0), 1e-9, "첫 마디선 = 시작 박 (−1마디 선 없음)");

        Debug.Log($"[CountInTest] C7: 시작 {startBeat}박, 시작 지연 {scene.StartDelaySeconds:0.###}초, 음원 시작 위치 {expected.StartPositionSeconds:0.###}초, 클릭 0개");
    }
}
