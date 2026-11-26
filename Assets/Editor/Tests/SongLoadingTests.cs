using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 곡 로딩 · 시계 (기획서 3-8 「곡 로딩」, 11/26 와이어프레임 21_SongLoading_Clock) EditMode 확인.
/// 1. 열기 · 끝 연출 시간표 (README 연출표)
/// 2. 바늘: fillAmount = min(EaseInOut((t − 0.8) / 1.6), 로딩 진행), 줄어들지 않고 로딩을 기다리면 그 자리에 멈춤 · 끝 점 위치 · 로딩 진행 계산
/// 3. 글자 값: 공식 / 나만의 채보 (배속 · 기록 없음)
/// 4. 곡 로딩을 거치는 진입: 일반 플레이만 (테스트 플레이 · 테스트 실행 중 건너뛰기 제외)
/// </summary>
public class SongLoadingTests
{
    /// <summary>암전이 다 덮인 뒤에는 검은 화면만 남고, 걷히는 동안 아래 그림(배경 · 앨범 · 시계 · 글자 · SD)이 비치지 않는다 (11/26 잔상 수정)</summary>
    [Test]
    public void End_AfterBlackout_OnlyBlackRemainsWhileRevealing()
    {
        float revealStart = GameConstants.SongLoading.RevealStart;
        float[] times = { GameConstants.SongLoading.BlackedOutAt, revealStart, revealStart + GameConstants.SongLoading.RevealSeconds * 0.5f };
        foreach (float e in times)
        {
            SongLoadingFrame f = SongLoadingClockTimeline.Evaluate(5f, e, revealStart);
            Assert.AreEqual(1f, f.BlackoutAlpha, 1e-5f, $"e={e}: 검은 화면 1");
            Assert.AreEqual(0f, f.BackgroundAlpha, $"e={e}: 곡 배경 0");
            Assert.AreEqual(0f, f.AlbumAlpha, $"e={e}: 앨범 0");
            Assert.AreEqual(0f, f.GlowAlpha, $"e={e}: 빛 0");
            Assert.AreEqual(0f, f.ClockAlpha, $"e={e}: 시계 0");
            Assert.AreEqual(0f, f.SparkAlpha, $"e={e}: 반짝 0");
            Assert.AreEqual(0f, f.InfoAlpha, $"e={e}: 글자 0");
            Assert.AreEqual(0f, f.SdAlpha, $"e={e}: SD 0");
        }
        // 다 덮이기 전(암전 중)에는 아래 그림이 아직 보인다
        SongLoadingFrame before = SongLoadingClockTimeline.Evaluate(5f, GameConstants.SongLoading.BlackedOutAt - 0.05f, revealStart);
        Assert.Greater(before.AlbumAlpha, 0f, "암전 중 앨범은 아직 보임");
        Assert.Less(before.BlackoutAlpha, 1f);
    }

    private const float Eps = 1e-4f;

    [Test]
    public void Timeline_Open()
    {
        SongLoadingFrame zero = SongLoadingClockTimeline.EvaluateOpen(0f);
        Assert.AreEqual(0f, zero.BackgroundAlpha, Eps);
        Assert.AreEqual(0f, zero.AlbumAlpha, Eps);
        Assert.AreEqual(0.9f, zero.AlbumScale, Eps, "앨범 0.9배에서");
        Assert.AreEqual(0f, zero.FlashAlpha, Eps, "번쩍은 처음 0");
        Assert.AreEqual(0f, zero.ClockAlpha, Eps);
        Assert.AreEqual(1.15f, zero.ClockScale, Eps, "링 1.15배에서");
        Assert.AreEqual(-10f, zero.InfoOffsetY, Eps, "글자 아래 10px");
        Assert.AreEqual(60f, zero.SdOffsetY, Eps, "SD 위 60px");
        Assert.AreEqual(1f, zero.RootAlpha, Eps);
        Assert.AreEqual(0f, zero.BlackoutAlpha, Eps);

        Assert.AreEqual(0.5f, SongLoadingClockTimeline.EvaluateOpen(0.125f).BackgroundAlpha, Eps, "배경 0~0.25 선형");
        Assert.AreEqual(1f, SongLoadingClockTimeline.EvaluateOpen(0.25f).BackgroundAlpha, Eps);
        Assert.AreEqual(0.25f, SongLoadingClockTimeline.EvaluateOpen(0.1f + 1e-5f).FlashAlpha, 1e-3f, "번쩍 0.1초에 0.25");
        Assert.AreEqual(0.125f, SongLoadingClockTimeline.EvaluateOpen(0.17f).FlashAlpha, Eps, "0.1~0.24 가운데 0.125");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(0.24f).FlashAlpha, Eps);
        Assert.AreEqual(UIEasing.OutCubic(0.5f), SongLoadingClockTimeline.EvaluateOpen(0.275f).AlbumAlpha, Eps, "앨범 OutCubic 0.1~0.45");
        Assert.AreEqual(1f, SongLoadingClockTimeline.EvaluateOpen(0.45f).AlbumScale, Eps);
        Assert.AreEqual(0.35f, SongLoadingClockTimeline.EvaluateOpen(0.45f).GlowAlpha, Eps, "빛 가운데 0.35 (sin)");
        Assert.AreEqual(1.275f, SongLoadingClockTimeline.EvaluateOpen(0.45f).GlowScale, Eps, "빛 크기 1.05 → 1.5 가운데");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(0.8f).GlowAlpha, Eps, "빛 0.8초에 0");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(0.3f).ClockAlpha, Eps);
        Assert.AreEqual(1f, SongLoadingClockTimeline.EvaluateOpen(0.7f).ClockScale, Eps, "링 0.7초에 제자리");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(0.5f).InfoAlpha, Eps);
        Assert.AreEqual(1f, SongLoadingClockTimeline.EvaluateOpen(0.8f).InfoAlpha, Eps, "글자 0.5~0.8");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(0.8f).InfoOffsetY, Eps);
        Assert.AreEqual(1f, SongLoadingClockTimeline.EvaluateOpen(0.85f).SdAlpha, Eps, "SD 알파 0.6~0.85");
        Assert.Greater(SongLoadingClockTimeline.EvaluateOpen(0.85f).SdOffsetY, 0f, "SD는 0.95초까지 내려옴");
        Assert.AreEqual(0f, SongLoadingClockTimeline.EvaluateOpen(GameConstants.SongLoading.OpenSeconds).SdOffsetY, Eps, "0.95초에 앉음");
        Assert.AreEqual(0.95f, GameConstants.SongLoading.OpenSeconds, Eps);

        // 앨범 · 링 · 글자 · SD 알파는 열기 동안 줄지 않는다
        float album = 0f, clock = 0f, info = 0f, sd = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            SongLoadingFrame f = SongLoadingClockTimeline.EvaluateOpen(t);
            Assert.GreaterOrEqual(f.AlbumAlpha, album - Eps);
            Assert.GreaterOrEqual(f.ClockAlpha, clock - Eps);
            Assert.GreaterOrEqual(f.InfoAlpha, info - Eps);
            Assert.GreaterOrEqual(f.SdAlpha, sd - Eps);
            album = f.AlbumAlpha; clock = f.ClockAlpha; info = f.InfoAlpha; sd = f.SdAlpha;
        }
    }

    [Test]
    public void Timeline_End()
    {
        const float t0 = 2.4f;
        float reveal = GameConstants.SongLoading.RevealStart;
        SongLoadingFrame e0 = SongLoadingClockTimeline.Evaluate(t0, 0f, reveal);
        Assert.AreEqual(0f, e0.SparkAlpha, Eps);
        Assert.AreEqual(1f, e0.InfoAlpha, Eps);
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 0.15f, 0.15f, reveal).SparkAlpha, Eps, "반짝 0.15초에 1 (sin)");
        Assert.AreEqual(0f, SongLoadingClockTimeline.Evaluate(t0 + 0.3f, 0.3f, reveal).SparkAlpha, Eps);
        Assert.AreEqual(0.5f, SongLoadingClockTimeline.Evaluate(t0 + 0.125f, 0.125f, reveal).InfoAlpha, Eps, "글자 1 → 0, 0.25초");
        Assert.AreEqual(0f, SongLoadingClockTimeline.Evaluate(t0 + 0.25f, 0.25f, reveal).SdAlpha, Eps);
        Assert.AreEqual(0f, SongLoadingClockTimeline.Evaluate(t0 + 0.3f, 0.3f, reveal).BlackoutAlpha, Eps, "암전은 0.3초부터");
        Assert.AreEqual(0.5f, SongLoadingClockTimeline.Evaluate(t0 + 0.475f, 0.475f, reveal).BlackoutAlpha, Eps, "InOutCubic 가운데 0.5");
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 0.65f, 0.65f, reveal).BlackoutAlpha, Eps, "0.65초에 검은 화면");
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 0.8f, 0.8f, reveal).RootAlpha, Eps, "0.8초까지 유지");
        Assert.AreEqual(0.5f, SongLoadingClockTimeline.Evaluate(t0 + 0.975f, 0.975f, reveal).RootAlpha, Eps, "걷힘 0.8~1.15");
        Assert.AreEqual(0f, SongLoadingClockTimeline.Evaluate(t0 + 1.15f, 1.15f, reveal).RootAlpha, Eps);
        SongLoadingFrame end = SongLoadingClockTimeline.Evaluate(t0 + 0.5f, 0.5f, reveal);
        Assert.AreEqual(1f, end.ClockScale, Eps, "링은 커지지 않는다");
        Assert.AreEqual(1f, end.AlbumScale, Eps, "앨범은 커지지 않는다");

        // 정리가 길면 검은 화면을 늘린다 (걷힘 시작 = 늦은 쪽), 0.8보다 이르게 걷히지 않는다
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 1.0f, 1.0f, 1.0f).RootAlpha, Eps, "걷힘 1.0초 시작이면 1.0초까지 유지");
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 0.9f, 0.9f, float.MaxValue).RootAlpha, Eps, "걷힘 허락 전에는 계속 검은 화면");
        Assert.AreEqual(1f, SongLoadingClockTimeline.Evaluate(t0 + 0.79f, 0.79f, 0.5f).RootAlpha, Eps, "0.8 전에는 걷히지 않음");

        Assert.AreEqual(3.55f, GameConstants.SongLoading.MinTotalSeconds, Eps, "가장 짧을 때 0.8 + 1.6 + 1.15");
        Assert.AreEqual(0.65f, GameConstants.SongLoading.BlackedOutAt, Eps);
        Assert.AreEqual(0.8f, GameConstants.SongLoading.RevealStart, Eps);
    }

    [Test]
    public void Hand_FillTable_WaitsAndNeverShrinks()
    {
        StringBuilder sb = new StringBuilder("[SongLoadingTest] 바늘 fillAmount (로딩 진행 1일 때)\n t | fillAmount\n");
        float[] times = { 0.8f, 1.2f, 1.6f, 2.0f, 2.4f };
        float[] expected = { 0f, UIEasing.InOutCubic(0.25f), 0.5f, UIEasing.InOutCubic(0.75f), 1f };
        for (int i = 0; i < times.Length; i++)
        {
            float fill = SongLoadingClockTimeline.HandFill(times[i], 1f, 0f);
            sb.AppendLine($" {times[i]:0.0} | {fill:0.0000}");
            Assert.AreEqual(expected[i], fill, Eps, $"{times[i]}초");
        }
        Assert.AreEqual(0f, SongLoadingClockTimeline.HandFill(0.5f, 1f, 0f), Eps, "0.8초 전에는 0");
        Assert.Less(SongLoadingClockTimeline.HandFill(2.399f, 1f, 0f), 1f, "2.4초 전에는 12시에 닿지 않음 (float 반올림)");

        // 로딩이 늦을 때: 진행 0.9에서 멈추고(12시 직전), 진행이 내려가도 줄지 않으며, 1이 되면 바로 12시
        float f = 0f;
        for (float t = 0.8f; t <= 4f; t += 1f / 60f) f = SongLoadingClockTimeline.HandFill(t, 0.9f, f);
        Assert.AreEqual(0.9f, f, Eps, "로딩을 기다리면 0.9(12시 직전)에서 멈춤");
        float before = f;
        f = SongLoadingClockTimeline.HandFill(4.1f, 0.3f, f);
        Assert.AreEqual(before, f, Eps, "줄어들지 않는다");
        f = SongLoadingClockTimeline.HandFill(4.2f, 1f, f);
        Assert.AreEqual(1f, f, Eps, "진행 1이면 12시");
        sb.AppendLine($" 로딩 늦음: 진행 0.9 → {before:0.0000}에서 멈춤, 진행 0.3을 받아도 {before:0.0000}, 진행 1 → {f:0.0000}");

        float prev = 0f;
        float[] progresses = { 0f, 0.2f, 0.1f, 0.5f, 0.45f, 0.9f, 0.8f, 1f };
        for (int i = 0; i < progresses.Length; i++)
        {
            float next = SongLoadingClockTimeline.HandFill(1f + i * 0.3f, progresses[i], prev);
            Assert.GreaterOrEqual(next, prev, "진행이 오르내려도 바늘은 줄지 않는다");
            prev = next;
        }

        Assert.AreEqual(0f, SongLoadingClockTimeline.LoadProgress(false, 0.5f, false), Eps, "로드 시작 전 0");
        Assert.AreEqual(0.45f, SongLoadingClockTimeline.LoadProgress(true, 0.45f, false), Eps, "progress 0.45 ÷ 0.9 × 0.9");
        Assert.AreEqual(0.9f, SongLoadingClockTimeline.LoadProgress(true, 1f, false), Eps, "로드 끝 · Init 전 0.9");
        Assert.AreEqual(1f, SongLoadingClockTimeline.LoadProgress(true, 0.9f, true), Eps, "Init 끝 1");

        // 끝 점: 반지름 300 원 위, 12시에서 시계 방향
        AssertNear(new Vector2(0f, 300f), SongLoadingClockTimeline.DotPosition(0f, 300f), "0 = 12시");
        AssertNear(new Vector2(300f, 0f), SongLoadingClockTimeline.DotPosition(0.25f, 300f), "0.25 = 3시");
        AssertNear(new Vector2(0f, -300f), SongLoadingClockTimeline.DotPosition(0.5f, 300f), "0.5 = 6시");
        AssertNear(new Vector2(-300f, 0f), SongLoadingClockTimeline.DotPosition(0.75f, 300f), "0.75 = 9시");
        Debug.Log(sb.ToString());
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, string message)
    {
        Assert.AreEqual(expected.x, actual.x, 0.01f, message);
        Assert.AreEqual(expected.y, actual.y, 0.01f, message);
    }

    private static ChartData Chart(string type, string difficulty, string name, int level)
    {
        return new ChartData { chartId = "test_chart", songId = TestCharts.OfficialSongId, type = type, difficulty = difficulty, chartName = name, level = level };
    }

    [Test]
    public void Info_OfficialCustom()
    {
        InGameEntryData entry = new InGameEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, mode = InGameMode.Normal, scrollMultiplier = 5.5f };
        SongLoadingInfo official = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeOfficial, "NORMAL", null, 7));
        Assert.AreEqual("NORMAL", official.BadgeText);
        Assert.IsFalse(official.IsCustom);
        Assert.AreEqual("Lv. 7", official.LevelText);
        Assert.IsFalse(official.ShowChartName, "공식 채보는 CHART 줄 숨김");
        Assert.AreEqual(string.Empty, official.ChartText);

        entry.chartType = ChartType.Custom;
        SongLoadingInfo custom = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeCustom, null, "내가 만든 질주 채보", 9));
        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, custom.BadgeText);
        Assert.IsTrue(custom.IsCustom);
        Assert.IsTrue(custom.ShowChartName);
        Assert.AreEqual("CHART  내가 만든 질주 채보", custom.ChartText, "CHART 뒤 두 칸");
        Assert.AreEqual("Lv. 9", custom.LevelText);
        Debug.Log($"[SongLoadingTest] 글자 값: 공식 {official.BadgeText} {official.LevelText} CHART {official.ShowChartName} · 나만의 {custom.BadgeText} {custom.LevelText} '{custom.ChartText}'");
    }

    [Test]
    public void Launcher_UsesSongLoadingOnlyForNormalPlay()
    {
        bool saved = GameSceneManager.SkipLoadingOverlayForTests;
        try
        {
            GameSceneManager.SkipLoadingOverlayForTests = false;
            InGameEntryData normal = InGameLauncher.CreateNormalEntry(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, 1f);
            Assert.IsTrue(InGameLauncher.UsesSongLoading(normal), "곡 선택 시작 · 결과 다시 하기 (일반 플레이) → 곡 로딩");
            InGameEntryData testPlay = new InGameEntryData { songId = TestCharts.OfficialSongId, mode = InGameMode.TestPlay, testPlay = new TestPlayData() };
            Assert.IsFalse(InGameLauncher.UsesSongLoading(testPlay), "테스트 플레이 → 바로");
            Assert.IsFalse(InGameLauncher.UsesSongLoading(null));
            GameSceneManager.SkipLoadingOverlayForTests = true;
            Assert.IsFalse(InGameLauncher.UsesSongLoading(normal), "테스트 실행 중 건너뛰기");
        }
        finally
        {
            GameSceneManager.SkipLoadingOverlayForTests = saved;
        }
    }
}
