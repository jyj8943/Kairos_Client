using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 재생 배율(TestSpeed) 실제 씬 확인: Critical Velocity NORMAL을 테스트 플레이 자동 플레이로 끝까지 돌린다 (공식 채보 파일은 고치지 않는다).
/// 배율마다 모든 판정 PERFECT, 최대 콤보·점수가 배율 1.0과 같은지 본다. 배율은 테스트 안에서 바꾸고 TestSpeedRunCallback이 되돌린다.
/// </summary>
public class PlaybackRatePlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>배율별 결과 (같은 실행 안에서 1.0 결과와 비교한다): 판정 수, PERFECT 수, 최대 콤보, 점수</summary>
    private static readonly Dictionary<double, int[]> results = new Dictionary<double, int[]>();

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

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    /// <summary>4: 배율 1.0·2.0·3.0으로 끝까지 자동 플레이 → 전부 PERFECT, 최대 콤보 = 가능한 최대 콤보, 1,000,000점, 1.0과 같은 결과</summary>
    [UnityTest, Category("Slow")]
    public IEnumerator R4_OfficialAutoPlay_SameResultAtRate([Values(1.0, 2.0, 3.0)] double rate)
    {
        SoundManager.SetPlaybackRateForTests(rate);
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);

        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.AreEqual(rate, GameManager.Sound.PlaybackRate, 1e-9, "매니저 배율");

        InGameEntryData entry = new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Official,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = chart, startBeat = 0f, autoPlay = true }
        };
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));

        deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        ScoreCalculator score = Get<ScoreCalculator>(scene, "score");

        float startRealtime = Time.realtimeSinceStartup;
        double startSongTime = GameManager.Sound.SongTime;
        double lastNoteEnd = 0;
        foreach (PlayNote note in judge.Notes) lastNoteEnd = System.Math.Max(lastNoteEnd, note.EndTime);

        deadline = TestSpeed.Deadline(lastNoteEnd + 30.0);
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            yield return null;
        }
        float playRealtime = Time.realtimeSinceStartup - startRealtime;
        double playSongTime = GameManager.Sound.SongTime - startSongTime;
        double maxSyncError = Get<double>(scene, "maxSyncErrorSeconds");

        int[] result = { judge.JudgedCount, score.PerfectCount, score.MaxCombo, score.Score };
        results[rate] = result;
        Debug.Log($"[PlaybackRateTest] x{rate:0.0}: 판정 {result[0]}/{judge.TotalCount}, PERFECT {result[1]}, GREAT {score.GreatCount}, GOOD {score.GoodCount}, MISS {score.MissCount}, " +
                  $"최대 콤보 {result[2]}/{score.MaxPossibleCombo}, 점수 {result[3]}, 실제 {playRealtime:0.0}초 동안 곡 시간 {playSongTime:0.0}초 (비 {playSongTime / playRealtime:0.00}), " +
                  $"음원 위치와 곡 시간 최대 차이 {maxSyncError * 1000:0}ms");

        Assert.AreEqual(judge.TotalCount, score.PerfectCount, "모든 판정 PERFECT");
        Assert.AreEqual(score.MaxPossibleCombo, score.MaxCombo, "최대 콤보");
        Assert.AreEqual(GameConstants.Play.MaxScore, score.Score, "점수");
        if (results.TryGetValue(1.0, out int[] normal)) CollectionAssert.AreEqual(normal, result, "배율 1.0과 같은 결과");
    }
}
