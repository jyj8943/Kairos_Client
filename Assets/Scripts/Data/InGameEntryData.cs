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

    /// <summary>
    /// 다시 하기(F5 · 일시정지 재시작 · 결과의 다시 하기)로 만든 진입이면 true (11/20). 다시 하기도 시작 키 글자 안내를 보여 준다 (11/20 변경, 테스트 · 로그 확인용 구분 값).
    /// 곡 선택·에디터에서 새로 들어오면 false. 싱글톤에 남기지 않고 이 진입 데이터로만 넘긴다.
    /// </summary>
    public bool isRetry;

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
            testPlay = testPlay,
            isRetry = isRetry
        };
    }

    /// <summary>다시 하기용 복사본 (isRetry = true). 인게임 재시작 · 결과의 다시 하기 데이터를 만들 때 쓴다</summary>
    public InGameEntryData CloneForRetry()
    {
        InGameEntryData clone = Clone();
        clone.isRetry = true;
        return clone;
    }
}
