/// <summary>
/// 곡 선택·결과에서 채보를 들고 들어가는 한 경로 (기획서 3-1 · 3-9): 인게임(곡 선택 시작 · 결과 다시 하기)과 에디터(곡 선택 나만의 채보 탭의 편집 · 새 채보)가 같이 쓴다.
/// - 인게임 일반 진입(곡 선택 시작 · 결과 다시 하기)은 곡 로딩 오버레이(3-9, GameSceneManager.LoadWithSongLoading)를 거친다 (UsesSongLoading).
///   결과에서 다시 하는 테스트 플레이 판과 에디터 진입(편집 · 새 채보, 일반 로딩 3-10은 아직)은 지금처럼 바로 그 씬으로 간다.
/// - 테스트(Unity 에디터): SkipSongLoadingForTests가 켜져 있으면 오버레이를 건너뛴다 (기존 테스트가 인게임에 들어갈 때마다 3초씩 늘지 않게. 곡 로딩 테스트만 끈다).
/// - 마지막 선택 (2-4 lastSelection, 11/18 탭마다 한 칸): 일반 플레이로 인게임에 들어가면 마지막 배속과 플레이한 탭의 칸을, 에디터로 들어가면 나만의 채보 칸
///   ({ songId, chartId }, 새 채보면 chartId 없음)을 저장한다. 테스트 플레이·자동 플레이는 저장하지 않는다.
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
        bool started = UsesSongLoading(entry)
            ? GameManager.Scene.LoadWithSongLoading(GameConstants.Scenes.InGame, entry)
            : GameManager.Scene.Load(GameConstants.Scenes.InGame, entry);
        if (!started) return false;

        LastStartedEntry = entry;
        StartCount++;
        if (ShouldSaveLastPlayed(entry) && PlayerDataManager.HasInstance)
            GameManager.PlayerData.SetLastPlayed(entry.scrollMultiplier, entry.songId, entry.chartId, entry.chartType);
        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터): true면 곡 로딩 오버레이 없이 바로 인게임을 불러온다. TestSpeedRunCallback이 테스트 실행 동안 켜고,
    /// 곡 로딩 테스트만 잠깐 끈다.
    /// </summary>
    public static bool SkipSongLoadingForTests;
#endif

    /// <summary>곡 로딩 오버레이를 거칠지: 일반 플레이(테스트 플레이 아님)만. 테스트 실행 중 SkipSongLoadingForTests면 건너뛴다</summary>
    public static bool UsesSongLoading(InGameEntryData entry)
    {
#if UNITY_EDITOR
        if (SkipSongLoadingForTests) return false;
#endif
        return entry != null && entry.mode == InGameMode.Normal && entry.testPlay == null;
    }

    /// <summary>인게임에 들어갈 때 마지막 선택(플레이한 탭 칸 · 배속)을 저장할지 (순수 함수): 일반 플레이만, 테스트 플레이는 저장하지 않는다</summary>
    public static bool ShouldSaveLastPlayed(InGameEntryData entry)
    {
        return entry != null && entry.mode == InGameMode.Normal && entry.testPlay == null;
    }

    /// <summary>
    /// 에디터 씬을 불러온다 (곡 선택 나만의 채보 탭: 채보 줄 E 편집 · 새 채보 팝업 만들기). 마지막 선택의 나만의 채보 칸을 { songId, chartId }로 저장한다 (새 채보는 chartId 없음 = 그 곡의 + 줄).
    /// </summary>
    /// <returns>불러오기를 시작했으면 true (이미 씬 이동 중이면 false)</returns>
    public static bool StartEditor(EditorEntryData entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.songId)) return false;
        if (!GameManager.Scene.Load(GameConstants.Scenes.Editor, entry)) return false;

        LastEditorEntry = entry;
        EditorStartCount++;
        if (PlayerDataManager.HasInstance) GameManager.PlayerData.SetLastCustom(entry.songId, entry.chartId);
        return true;
    }

    /// <summary>마지막으로 인게임을 시작한 진입 데이터 (확인용)</summary>
    public static InGameEntryData LastStartedEntry { get; private set; }

    /// <summary>이 경로로 인게임을 시작한 횟수 (확인용, 테스트가 곡 선택·결과가 같은 경로를 쓰는지 본다)</summary>
    public static int StartCount { get; private set; }

    /// <summary>마지막으로 에디터를 연 진입 데이터 (확인용)</summary>
    public static EditorEntryData LastEditorEntry { get; private set; }

    /// <summary>이 경로로 에디터를 연 횟수 (확인용)</summary>
    public static int EditorStartCount { get; private set; }
}
