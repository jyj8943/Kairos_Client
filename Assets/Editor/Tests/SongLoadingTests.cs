using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 곡 로딩 (기획서 3-8 「곡 로딩」) EditMode 확인.
/// 1. 열기 · 닫기 시간표 (README 4절 · 5절)
/// 2. 카드 값: 공식 / 나만의 채보 / AP 기록 / 기록 없음
/// 3. 곡 로딩을 거치는 진입: 일반 플레이만 (테스트 플레이 · 테스트 실행 중 건너뛰기 제외)
/// </summary>
public class SongLoadingTests
{
    [Test]
    public void Timeline_OpenAndClose()
    {
        SongLoadingFrame start = SongLoadingTimeline.EvaluateOpen(0f);
        Assert.AreEqual(0f, start.BackgroundAlpha, 1e-4f);
        Assert.AreEqual(0f, start.PanelAlpha, 1e-4f);
        Assert.AreEqual(60f, start.PanelOffsetX, 1e-4f, "판 오른쪽 60px에서 시작");
        Assert.AreEqual(-70f, start.SdOffsetY, 1e-4f, "SD 아래 70px에서 시작");
        Assert.AreEqual(-16f, start.BubbleOffsetX, 1e-4f, "말풍선 왼쪽 16px에서 시작");

        Assert.AreEqual(0.5f, SongLoadingTimeline.EvaluateOpen(0.175f).BackgroundAlpha, 1e-4f, "easeInOutCubic 가운데 0.5");
        SongLoadingFrame cover = SongLoadingTimeline.EvaluateOpen(GameConstants.SongLoading.CoverSeconds);
        Assert.AreEqual(1f, cover.BackgroundAlpha, 1e-4f, "0.35초에 다 덮음");
        Assert.AreEqual(1f, cover.AlbumAlpha, 1e-4f);
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.25f).ScreenNameAlpha, 1e-4f);
        Assert.AreEqual(1f, SongLoadingTimeline.EvaluateOpen(0.55f).ScreenNameAlpha, 1e-4f, "화면 이름 0.25~0.55");
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.3f).PanelAlpha, 1e-4f);
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.75f).PanelOffsetX, 1e-4f, "판 0.75초에 제자리");
        Assert.Greater(SongLoadingTimeline.EvaluateOpen(0.85f).SdOffsetY, 0f, "SD easeOutBack: 제자리보다 살짝 위로 튐");
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.95f).SdOffsetY, 1e-4f, "SD 0.95초에 제자리");
        Assert.AreEqual(1f, SongLoadingTimeline.EvaluateOpen(0.75f).SdAlpha, 1e-4f, "SD 알파는 앞 절반 (0.55~0.75)");
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.7f).LoadingAlpha, 1e-4f);
        Assert.AreEqual(1f, SongLoadingTimeline.EvaluateOpen(1.0f).LoadingAlpha, 1e-4f, "불러오는 중 0.7~1.0");
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateOpen(0.85f).BubbleAlpha, 1e-4f);
        SongLoadingFrame open = SongLoadingTimeline.EvaluateOpen(GameConstants.SongLoading.OpenSeconds);
        Assert.AreEqual(1f, open.BubbleAlpha, 1e-4f, "1.1초 말풍선 다 나옴");
        Assert.AreEqual(0f, open.BubbleOffsetX, 1e-4f);

        SongLoadingFrame c0 = SongLoadingTimeline.EvaluateClose(0f);
        Assert.AreEqual(1f, c0.BackgroundAlpha, 1e-4f);
        Assert.AreEqual(1f, c0.PanelAlpha, 1e-4f);
        SongLoadingFrame c3 = SongLoadingTimeline.EvaluateClose(0.3f);
        Assert.AreEqual(-90f, c3.AlbumOffsetX, 1e-4f, "앨범 왼쪽 90");
        Assert.AreEqual(70f, c3.PanelOffsetX, 1e-4f, "판 오른쪽 70");
        Assert.AreEqual(-90f, c3.SdOffsetY, 1e-4f, "SD 아래 90");
        Assert.AreEqual(-90f, c3.BubbleOffsetY, 1e-4f, "말풍선도 함께 아래 90");
        Assert.AreEqual(0f, c3.AlbumAlpha + c3.PanelAlpha + c3.SdAlpha + c3.BubbleAlpha + c3.ScreenNameAlpha + c3.LoadingAlpha, 1e-4f, "0.3초에 앞쪽 모두 0");
        Assert.AreEqual(1f, SongLoadingTimeline.EvaluateClose(0.22f).BackgroundAlpha, 1e-4f, "배경은 0.22초부터");
        Assert.AreEqual(0.5f, SongLoadingTimeline.EvaluateClose(0.43f).BackgroundAlpha, 1e-4f, "0.22~0.64 가운데 0.5");
        Assert.AreEqual(0f, SongLoadingTimeline.EvaluateClose(GameConstants.SongLoading.CloseSeconds).BackgroundAlpha, 1e-4f, "0.64초 끝");
        Assert.AreEqual(0.125f, 1f - SongLoadingTimeline.EvaluateClose(0.15f).PanelAlpha, 1e-4f, "easeInCubic(0.5) = 0.125");

        StringBuilder sb = new StringBuilder("[SongLoadingTest] 열기: 시간 | 배경·앨범 | 화면 이름 | 판 알파·x | SD 알파·y | 불러오는 중 | 말풍선 알파·x\n");
        foreach (float t in new[] { 0f, 0.175f, 0.35f, 0.55f, 0.75f, 0.85f, 0.95f, 1.1f })
        {
            SongLoadingFrame f = SongLoadingTimeline.EvaluateOpen(t);
            sb.AppendLine($"{t:0.000} | {f.BackgroundAlpha:0.00} | {f.ScreenNameAlpha:0.00} | {f.PanelAlpha:0.00}·{f.PanelOffsetX:+0.0;-0.0;0} | {f.SdAlpha:0.00}·{f.SdOffsetY:+0.0;-0.0;0} | {f.LoadingAlpha:0.00} | {f.BubbleAlpha:0.00}·{f.BubbleOffsetX:+0.0;-0.0;0}");
        }
        sb.AppendLine("닫기: 시간 | 앨범 알파·x | 판 알파·x | SD·말풍선 y | 배경");
        foreach (float t in new[] { 0f, 0.15f, 0.22f, 0.3f, 0.43f, 0.64f })
        {
            SongLoadingFrame f = SongLoadingTimeline.EvaluateClose(t);
            sb.AppendLine($"{t:0.000} | {f.AlbumAlpha:0.00}·{f.AlbumOffsetX:+0.0;-0.0;0} | {f.PanelAlpha:0.00}·{f.PanelOffsetX:+0.0;-0.0;0} | {f.SdOffsetY:+0.0;-0.0;0} | {f.BackgroundAlpha:0.00}");
        }
        Debug.Log(sb.ToString());
    }

    private static ChartData Chart(string type, string difficulty, string name, int level)
    {
        return new ChartData { chartId = "test_chart", songId = TestCharts.OfficialSongId, type = type, difficulty = difficulty, chartName = name, level = level };
    }

    [Test]
    public void Info_OfficialCustomApNoRecord()
    {
        InGameEntryData entry = new InGameEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, mode = InGameMode.Normal, scrollMultiplier = 5.5f };

        ChartRecordData a = new ChartRecordData { chartId = "test_chart", bestScore = 948210, bestRank = "A", maxCombo = 812 };
        SongLoadingInfo official = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeOfficial, "NORMAL", null, 7), a);
        Assert.AreEqual("NORMAL", official.BadgeText);
        Assert.IsFalse(official.IsCustom);
        Assert.AreEqual("Lv. 7", official.LevelText);
        Assert.AreEqual("x5.5", official.SpeedText);
        Assert.IsFalse(official.ShowChartName, "공식 채보는 CHART 줄 숨김");
        Assert.AreEqual("948210", official.Record.Score, "쉼표 없음");
        Assert.AreEqual("812", official.Record.Combo);
        Assert.AreEqual(PlayRank.A, official.Record.Rank);
        Assert.IsFalse(official.Record.IsFullCombo);

        entry.chartType = ChartType.Custom;
        SongLoadingInfo custom = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeCustom, null, "내가 만든 첫 채보", 6), a);
        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, custom.BadgeText);
        Assert.IsTrue(custom.IsCustom);
        Assert.IsTrue(custom.ShowChartName);
        Assert.AreEqual("내가 만든 첫 채보", custom.ChartName);

        ChartRecordData ap = new ChartRecordData { chartId = "test_chart", bestScore = 1000000, bestRank = "AP", maxCombo = 329, isAllPerfect = true, isFullCombo = true };
        SongLoadingInfo apInfo = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeOfficial, "NORMAL", null, 7), ap);
        Assert.AreEqual(PlayRank.AP, apInfo.Record.Rank);
        Assert.IsTrue(apInfo.Record.IsFullCombo, "AP면 FC도");

        SongLoadingInfo none = SongLoadingInfoRules.Build(entry, null, Chart(ChartData.TypeOfficial, "NORMAL", null, 7), null);
        Assert.IsFalse(none.Record.HasRecord);
        Assert.AreEqual(GameConstants.SongSelect.EmptyValue, none.Record.Score, "기록 없음 —");
        Assert.AreEqual(GameConstants.SongSelect.EmptyValue, none.Record.Combo);
        Debug.Log($"[SongLoadingTest] 카드 값: 공식 {official.BadgeText} {official.LevelText} {official.SpeedText} CHART {official.ShowChartName} 기록 {official.Record.Score}/{official.Record.Rank}/{official.Record.Combo} · " +
                  $"나만의 {custom.BadgeText} '{custom.ChartName}' · AP {apInfo.Record.Rank} FC {apInfo.Record.IsFullCombo} · 기록 없음 {none.Record.Score}/{none.Record.Combo}");
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
