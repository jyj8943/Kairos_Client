/// <summary>
/// 인게임에 들어가는 한 경로 (곡 선택 시작 · 결과 다시 하기가 같이 쓴다, 기획서 3-1 · 3-10).
/// - 일반 플레이로 들어가면 마지막 배속과 마지막으로 플레이한 곡·채보·탭을 저장한다 (2-4 lastSelection). 테스트 플레이·자동 플레이는 저장하지 않는다.
/// - 곡 로딩 화면(3-10)을 넣을 때는 Start 안에서 인게임 대신 곡 로딩을 거치게 바꾸면 곡 선택·결과가 함께 따라간다.
/// - 인게임 안의 재시작(일시정지 메뉴)·F5 즉시 다시 하기는 이 경로를 쓰지 않는다 (같은 씬 안에서 바로 다시 시작, 곡 로딩 없음).
/// </summary>
public static class InGameLauncher
{
    /// <summary>
    /// 인게임 진입 데이터를 만든다 (곡 선택에서 시작하는 일반 플레이).
    /// </summary>
    public static InGameEntryData CreateNormalEntry(string songId, string chartId, ChartType chartType, float scrollMultiplier)
    {
        return new InGameEntryData
        {
            songId = songId,
            chartId = chartId,
            chartType = chartType,
            mode = InGameMode.Normal,
            scrollMultiplier = scrollMultiplier
        };
    }

    /// <summary>
    /// 인게임 씬을 불러온다. 불러오기를 시작했으면 true (이미 씬 이동 중이면 false).
    /// </summary>
    public static bool Start(InGameEntryData entry)
    {
        if (entry == null) return false;
        if (!GameManager.Scene.Load(GameConstants.Scenes.InGame, entry)) return false;

        LastStartedEntry = entry;
        StartCount++;
        if (entry.mode == InGameMode.Normal && entry.testPlay == null && PlayerDataManager.HasInstance)
            GameManager.PlayerData.SetLastPlayed(entry.scrollMultiplier, entry.songId, entry.chartId, entry.chartType);
        return true;
    }

    /// <summary>마지막으로 인게임을 시작한 진입 데이터 (확인용)</summary>
    public static InGameEntryData LastStartedEntry { get; private set; }

    /// <summary>이 경로로 인게임을 시작한 횟수 (확인용, 테스트가 곡 선택·결과가 같은 경로를 쓰는지 본다)</summary>
    public static int StartCount { get; private set; }
}
