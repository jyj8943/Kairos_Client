/// <summary>
/// 인게임 씬으로 넘기는 데이터.
/// GameManager.Scene.Load(GameConstants.Scenes.InGame, data)로 넘기고 InGameScene Init에서 TakeSceneData로 받는다.
/// </summary>
public class InGameEntryData
{
    public string songId;
    public string chartId;
    public ChartType chartType = ChartType.Custom;
    public InGameMode mode = InGameMode.Normal;

    /// <summary>다시 하기용 복사본</summary>
    public InGameEntryData Clone()
    {
        return new InGameEntryData { songId = songId, chartId = chartId, chartType = chartType, mode = mode };
    }
}
