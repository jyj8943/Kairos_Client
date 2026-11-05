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

    /// <summary>배속 (곡 선택 화면에서 정함). 노트 낙하 속도 = 기본 낙하 속도 × 배속</summary>
    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>테스트 플레이면 에디터가 넘긴 채보·시작 위치. 있으면 파일을 읽지 않는다</summary>
    public TestPlayData testPlay;

    /// <summary>다시 하기용 복사본. 테스트 플레이 데이터는 인게임이 바꾸지 않으므로 같은 것을 넘긴다</summary>
    public InGameEntryData Clone()
    {
        return new InGameEntryData
        {
            songId = songId,
            chartId = chartId,
            chartType = chartType,
            mode = mode,
            scrollMultiplier = scrollMultiplier,
            testPlay = testPlay
        };
    }
}
