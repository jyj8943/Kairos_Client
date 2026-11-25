using System.Collections.Generic;

/// <summary>나만의 채보 탭 정지점 종류: 채보 줄 / "+ 새 채보 생성" 줄</summary>
public enum MyChartStopKind
{
    Chart,
    AddRow
}

/// <summary>나만의 채보 탭 정지점 하나 (순수 값)</summary>
public readonly struct MyChartStop
{
    /// <summary>채보 줄 / + 줄</summary>
    public readonly MyChartStopKind Kind;

    /// <summary>곡 묶음 순번 (곡 순서)</summary>
    public readonly int Group;

    /// <summary>묶음 안 채보 순번 (+ 줄은 -1)</summary>
    public readonly int ChartInGroup;

    /// <summary>목록 전체 채보 순번 (채보 줄 목록의 순번, + 줄은 -1)</summary>
    public readonly int ChartIndex;

    public MyChartStop(MyChartStopKind kind, int group, int chartInGroup, int chartIndex)
    {
        Kind = kind;
        Group = group;
        ChartInGroup = chartInGroup;
        ChartIndex = chartIndex;
    }

    public bool IsAddRow => Kind == MyChartStopKind.AddRow;
}

/// <summary>
/// 나만의 채보 탭 목록 이동 규칙 (기획서 3-3 「나만의 채보 탭 (11/17, 11/18 개정)」, 순수 클래스 · EditMode 테스트용).
/// - 정지점 = 곡 순서대로 묶음마다 [채보 줄들 + 그 곡의 + 줄]을 이은 한 줄 목록. 곡 머리 줄은 정지점이 아니다. 채보가 없는 곡은 + 줄 하나.
///   + 줄은 화면에는 지금 고른 곡에만 보이지만 목록 구조에는 늘 있다고 본다 (그 곡에 들어가면 보인다).
/// - ↑↓ (Vertical): 한 칸씩. ↓는 채보 줄 → 그 곡의 + 줄 → 다음 곡 첫 정지점, ↑는 거꾸로 (↑로 이전 곡의 + 줄에 간다).
/// - ←→ (Group): 이전·다음 곡 묶음의 첫 정지점.
/// - 양 끝에서 더 가면 그 자리에 멈춘다 (넘어가지 않는다). 곡이 없으면 -1.
/// - 정지점이 속한 곡이 "지금 고른 곡"이다.
/// </summary>
public class MyChartListNavigator
{
    private readonly List<MyChartStop> stops = new List<MyChartStop>();
    private readonly List<int> groupFirstStop = new List<int>();
    private readonly List<int> groupAddRowStop = new List<int>();
    private readonly List<int> chartStop = new List<int>();

    /// <summary>정지점 수</summary>
    public int Count => stops.Count;

    /// <summary>곡 묶음 수 (채보가 0개인 묶음 포함)</summary>
    public int GroupCount => groupFirstStop.Count;

    /// <summary>전체 채보 수</summary>
    public int ChartCount => chartStop.Count;

    /// <param name="groupSizes">곡 묶음마다 채보 수 (목록 순서대로)</param>
    public MyChartListNavigator(IReadOnlyList<int> groupSizes)
    {
        if (groupSizes == null) return;

        for (int g = 0; g < groupSizes.Count; g++)
        {
            groupFirstStop.Add(stops.Count);
            for (int i = 0; i < groupSizes[g]; i++)
            {
                chartStop.Add(stops.Count);
                stops.Add(new MyChartStop(MyChartStopKind.Chart, g, i, chartStop.Count - 1));
            }
            groupAddRowStop.Add(stops.Count);
            stops.Add(new MyChartStop(MyChartStopKind.AddRow, g, -1, -1));
        }
    }

    /// <summary>정지점 (범위 밖이면 Group -1인 값)</summary>
    public MyChartStop Get(int stop)
    {
        return stop >= 0 && stop < Count ? stops[stop] : new MyChartStop(MyChartStopKind.Chart, -1, -1, -1);
    }

    /// <summary>묶음의 첫 정지점 (채보가 있으면 첫 채보, 없으면 + 줄). 범위 밖이면 -1</summary>
    public int FirstStopOfGroup(int group)
    {
        return group >= 0 && group < GroupCount ? groupFirstStop[group] : -1;
    }

    /// <summary>묶음의 + 줄 정지점. 범위 밖이면 -1</summary>
    public int AddRowOfGroup(int group)
    {
        return group >= 0 && group < GroupCount ? groupAddRowStop[group] : -1;
    }

    /// <summary>채보 순번(목록 전체)의 정지점. 범위 밖이면 -1</summary>
    public int StopOfChart(int chartIndex)
    {
        return chartIndex >= 0 && chartIndex < ChartCount ? chartStop[chartIndex] : -1;
    }

    /// <summary>
    /// ↑(-1)·↓(+1): 옆 정지점. 끝이면 그대로. 곡이 없으면 -1, 범위 밖 정지점이면 0(첫 정지점).
    /// </summary>
    public int Vertical(int stop, int direction)
    {
        if (Count == 0) return -1;
        if (stop < 0 || stop >= Count) return 0;

        int next = stop + (direction > 0 ? 1 : direction < 0 ? -1 : 0);
        return next >= 0 && next < Count ? next : stop;
    }

    /// <summary>
    /// ←(-1)·→(+1): 이전·다음 곡 묶음의 첫 정지점. 끝이면 그대로. 곡이 없으면 -1, 범위 밖 정지점이면 0(첫 정지점).
    /// </summary>
    public int Group(int stop, int direction)
    {
        if (Count == 0) return -1;
        if (stop < 0 || stop >= Count) return 0;
        if (direction == 0) return stop;

        int group = stops[stop].Group + (direction > 0 ? 1 : -1);
        return group >= 0 && group < GroupCount ? groupFirstStop[group] : stop;
    }

    /// <summary>
    /// 곡 선택에 들어왔을 때 처음 고를 정지점 (곡 선택 진입 데이터, 순수 함수).
    /// - songId가 목록에 있고 chartId가 그 곡의 채보면 그 채보 줄
    /// - songId가 있고 chartId가 없으면(새 채보를 저장하지 않고 나감) 그 곡의 + 줄
    /// - songId가 있고 chartId가 그 곡에 없으면(파일을 못 읽음 등) 그 곡의 첫 정지점
    /// - songId가 없거나 목록에 없으면 첫 정지점. 곡이 없으면 -1
    /// </summary>
    /// <param name="groupSongIds">묶음 순서대로 곡 ID</param>
    /// <param name="chartIdsByGroup">묶음마다 채보 ID (목록 순서)</param>
    public int ResolveEntryStop(IReadOnlyList<string> groupSongIds, IReadOnlyList<IReadOnlyList<string>> chartIdsByGroup, string songId, string chartId)
    {
        if (Count == 0) return -1;

        int group = -1;
        if (!string.IsNullOrEmpty(songId) && groupSongIds != null)
        {
            for (int g = 0; g < groupSongIds.Count && g < GroupCount; g++)
            {
                if (groupSongIds[g] != songId) continue;
                group = g;
                break;
            }
        }
        if (group < 0) return 0;
        if (string.IsNullOrEmpty(chartId)) return AddRowOfGroup(group);

        IReadOnlyList<string> charts = chartIdsByGroup != null && group < chartIdsByGroup.Count ? chartIdsByGroup[group] : null;
        if (charts != null)
        {
            for (int i = 0; i < charts.Count; i++)
            {
                if (charts[i] == chartId) return groupFirstStop[group] + i;
            }
        }
        return FirstStopOfGroup(group);
    }
}
