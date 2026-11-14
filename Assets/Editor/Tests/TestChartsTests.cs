using NUnit.Framework;

/// <summary>
/// 테스트용 공식 채보(TestCharts)가 실제로 불러와지는지 확인한다. 공식 채보가 바뀌거나 지워지면 다른 PlayTests보다 여기서 먼저 실패한다.
/// </summary>
public class TestChartsTests
{
    [Test]
    public void OfficialChart_LoadsFromChartRepository()
    {
        bool loaded = ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error);

        Assert.IsTrue(loaded, $"테스트용 공식 채보를 불러오지 못했습니다 ({error}). 공식 채보가 바뀌었으면 TestCharts와 CLAUDE.md를 같이 고친다.");
        Assert.AreEqual(TestCharts.OfficialSongId, chart.songId);
        Assert.AreEqual(TestCharts.OfficialChartId, chart.chartId);
        Assert.IsNotEmpty(chart.notes, "노트가 있어야 한다");
        Assert.IsTrue(chart.notes.Exists(n => n.type == NoteData.TypeShort), "숏노트가 있어야 한다 (P12가 숏노트만 남겨 쓴다)");
        Assert.IsTrue(chart.notes.Exists(n => n.type == NoteData.TypeLong), "롱노트가 있어야 한다");
    }

    [Test]
    public void OfficialChart_IsInOfficialList()
    {
        Assert.IsTrue(ChartRepository.GetCharts(TestCharts.OfficialSongId, ChartType.Official).Exists(c => c.chartId == TestCharts.OfficialChartId),
            "곡 선택 공식 탭 목록에도 있어야 한다");
    }
}
