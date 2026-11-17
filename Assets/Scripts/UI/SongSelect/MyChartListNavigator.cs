using System.Collections.Generic;

/// <summary>
/// 나만의 채보 탭 목록 이동 규칙 (기획서 3-3 나만의 채보 탭 11/17, 순수 클래스 · EditMode 테스트용).
/// - 목록은 곡별 묶음(곡 순서) 안에 채보가 놓인 한 줄 목록이다. 채보 위치는 목록 전체의 순번(0부터)으로 다룬다.
/// - ↑↓ (Vertical): 채보 하나씩. 곡이 달라도 바로 다음·이전 채보로 간다.
/// - ←→ (Group): 이전 곡 / 다음 곡 묶음의 첫 채보로.
/// - 양 끝에서 더 가면 그 자리에 멈춘다 (돌아가지 않는다). 채보가 없으면 -1.
/// </summary>
public class MyChartListNavigator
{
    private readonly List<int> groupStarts = new List<int>();
    private readonly List<int> groupOfIndex = new List<int>();

    /// <summary>전체 채보 수</summary>
    public int Count => groupOfIndex.Count;

    /// <summary>묶음 수 (채보가 0개인 묶음 포함)</summary>
    public int GroupCount => groupStarts.Count;

    /// <param name="groupSizes">곡 묶음마다 채보 수 (목록 순서대로)</param>
    public MyChartListNavigator(IReadOnlyList<int> groupSizes)
    {
        if (groupSizes == null) return;

        for (int g = 0; g < groupSizes.Count; g++)
        {
            groupStarts.Add(groupOfIndex.Count);
            for (int i = 0; i < groupSizes[g]; i++) groupOfIndex.Add(g);
        }
    }

    /// <summary>채보 순번이 속한 묶음 (범위 밖이면 -1)</summary>
    public int GetGroup(int index)
    {
        return index >= 0 && index < Count ? groupOfIndex[index] : -1;
    }

    /// <summary>
    /// ↑(-1)·↓(+1): 옆 채보 순번. 끝이면 그대로. 채보가 없으면 -1, 범위 밖 순번이면 0(첫 채보).
    /// </summary>
    public int Vertical(int index, int direction)
    {
        if (Count == 0) return -1;
        if (index < 0 || index >= Count) return 0;

        int next = index + (direction > 0 ? 1 : direction < 0 ? -1 : 0);
        return next >= 0 && next < Count ? next : index;
    }

    /// <summary>
    /// ←(-1)·→(+1): 이전·다음 곡 묶음의 첫 채보 순번 (채보가 0개인 묶음은 건너뛴다). 끝이면 그대로.
    /// 채보가 없으면 -1, 범위 밖 순번이면 0(첫 채보).
    /// </summary>
    public int Group(int index, int direction)
    {
        if (Count == 0) return -1;
        if (index < 0 || index >= Count) return 0;
        if (direction == 0) return index;

        int step = direction > 0 ? 1 : -1;
        for (int g = groupOfIndex[index] + step; g >= 0 && g < groupStarts.Count; g += step)
        {
            int start = groupStarts[g];
            bool hasChart = start < Count && groupOfIndex[start] == g;
            if (hasChart) return start;
        }
        return index;
    }
}
