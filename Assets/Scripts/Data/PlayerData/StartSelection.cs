/// <summary>
/// 곡 선택에 들어올 때 처음 고를 곡·채보·탭 (공식 탭: PlayerDataRules.ResolveOfficialStart의 결과, 값 묶음).
/// </summary>
public readonly struct StartSelection
{
    /// <summary>고를 곡 ID. 곡이 하나도 없으면 null</summary>
    public readonly string SongId;

    /// <summary>고를 채보 ID. null이면 그 곡의 첫 채보 (공식 탭은 가장 낮은 난이도)</summary>
    public readonly string ChartId;

    /// <summary>시작할 탭</summary>
    public readonly ChartType ChartType;

    /// <summary>마지막 선택을 그대로 썼으면 true, 공식 첫 곡으로 돌아갔으면 false</summary>
    public readonly bool FromLastSelection;

    public StartSelection(string songId, string chartId, ChartType chartType, bool fromLastSelection)
    {
        SongId = songId;
        ChartId = chartId;
        ChartType = chartType;
        FromLastSelection = fromLastSelection;
    }
}
