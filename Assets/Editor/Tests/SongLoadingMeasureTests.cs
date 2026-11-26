using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 곡 로딩 프레임 측정 (사용자가 요청할 때만, Explicit). 한 플레이 세션에서 곡 선택 → 인게임 3회 + 결과 다시 하기 1회를 하고,
/// 회마다 단계별 시각과 단계별 프레임 통계(최대 ms · 33ms 넘은 프레임 수)를 콘솔에 남긴다. 1회째가 앱을 켜고 처음 들어간 판이다.
/// 기록은 남기지 않는다 (마지막 배속은 지금 저장된 값 그대로, 결과는 자동 플레이 표시).
/// </summary>
public class SongLoadingMeasureTests
{
    private const string Song = TestCharts.OfficialSongId;

    private bool savedSkip;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkip = GameSceneManager.SkipLoadingOverlayForTests;
        GameSceneManager.SkipLoadingOverlayForTests = false;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameSceneManager.SkipLoadingOverlayForTests = savedSkip;
        yield return new ExitPlayMode();
    }

    private static float LastSpeed => PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading) yield return null;
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data));
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName || GameManager.Scene.Current == null || !GameManager.Scene.Current.IsEntered)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, sceneName + " 대기 시간 초과");
            yield return null;
        }
        // 들어간 뒤 화면이 자리 잡을 시간
        float until = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    private static IEnumerator WaitSongLoading(StringBuilder sb, string title)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (GameManager.Scene.IsSongLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "곡 로딩 대기 시간 초과");
            yield return null;
        }
        sb.AppendLine($"[{title}]");
        sb.AppendLine("  단계: " + GameManager.Scene.DescribeLoading());
        sb.AppendLine("  프레임: " + GameManager.Scene.DescribeLoadingFrames());
        float until = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    [UnityTest, Explicit("사용자가 요청할 때만 (곡 로딩 프레임 측정)")]
    public IEnumerator Measure_SongSelect3_Retry1()
    {
        StringBuilder sb = new StringBuilder("[SongLoadingMeasure] 곡 로딩 측정 (에디터 플레이 모드)\n");
        for (int i = 1; i <= 3; i++)
        {
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
            {
                songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
            });
            Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
            typeof(Canvas_SongSelect).GetMethod("StartGame", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(songSelect, null);
            yield return WaitSongLoading(sb, $"곡 선택 → 인게임 {i}회째{(i == 1 ? " (앱 켜고 처음)" : "")}");
        }

        ScoreCalculator score = new ScoreCalculator(1);
        score.Add(JudgeType.Perfect);
        ResultData result = ResultData.FromScore(score);
        result.songId = Song;
        result.songTitle = "Critical Velocity";
        result.chartType = ChartType.Official;
        result.difficulty = GameConstants.Chart.DifficultyNormal;
        result.level = 7;
        result.isAutoPlay = true;
        result.retryEntry = InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, LastSpeed);
        result.returnTarget = new ResultReturnTarget { sceneName = GameConstants.Scenes.SongSelect, sceneData = new SongSelectEntryData { songId = Song } };
        yield return LoadAndWait(GameConstants.Scenes.Result, result);
        typeof(ResultScene).GetMethod("Retry", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Object.FindFirstObjectByType<ResultScene>(), null);
        yield return WaitSongLoading(sb, "결과 다시 하기");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 곡 3개 × 공식 채보 / 나만의 채보 곡 로딩 확인 (사용자가 요청할 때만, Explicit). 곡 선택 Enter → 곡 로딩 → 인게임마다
    /// 단계 시각 표(전체 ≥ 3.55초, 로드 시작 ≥ 0.95초, OnEnter · 첫 클릭은 걷힘 끝 뒤)와 바늘 표 · 프레임 통계를 남긴다.
    /// 공식 채보가 없는 곡은 건너뛰고 그렇다고 적는다. 나만의 채보는 그 곡의 첫 채보를 쓴다.
    /// </summary>
    [UnityTest, Explicit("사용자가 요청할 때만 (곡 3개 × 공식 / 나만의 채보 곡 로딩 확인)")]
    public IEnumerator Verify_ThreeSongs_OfficialCustom()
    {
        StringBuilder sb = new StringBuilder("[SongLoadingMeasure] 곡 3개 × 공식 / 나만의 채보\n");
        foreach (SongData song in GameManager.Songs.Songs)
        {
            if (song == null) continue;
            foreach (ChartType type in new[] { ChartType.Official, ChartType.Custom })
            {
                System.Collections.Generic.List<ChartData> charts = ChartRepository.GetCharts(song.SongId, type);
                // 공식 채보는 Critical Velocity NORMAL을 먼저 (테스트 기준 채보)
                ChartData chart = null;
                foreach (ChartData c in charts) if (c.chartId == TestCharts.OfficialChartId) chart = c;
                if (chart == null && charts.Count > 0) chart = charts[0];
                string title = $"{song.SongId} · {type}";
                if (chart == null)
                {
                    sb.AppendLine($"[{title}] 채보 없음 → 건너뜀");
                    continue;
                }
                yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
                {
                    songId = song.SongId, chartType = type, chartId = chart.chartId, scrollMultiplier = LastSpeed
                });
                Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
                typeof(Canvas_SongSelect).GetMethod("StartGame", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(songSelect, null);
                Canvas_SongLoading overlay = GameManager.UI.SongLoading;
                StringBuilder log = new StringBuilder($"[{title} '{chart.chartName}' {chart.difficulty} Lv{chart.level}] 뱃지 {overlay.Info.BadgeText} {overlay.Info.LevelText} CHART줄 {overlay.Info.ShowChartName} '{overlay.Info.ChartText}'\n");
                yield return SongLoadingPlayTests.FollowSongLoading(GameConstants.Scenes.SongSelect, null, log, false);
                SongLoadingPlayTests.CheckSteps(GameConstants.Scenes.SongSelect, log);
                sb.Append(log);
            }
        }
        Debug.Log(sb.ToString());
    }
}
