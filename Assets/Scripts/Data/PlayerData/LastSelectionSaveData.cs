using System;

/// <summary>
/// PlayerData.json에 저장하는 곡 선택의 마지막 선택 상태 (기획서 2-4 「마지막 선택」 11/18: 탭마다 따로 저장).
/// 설정 창에서 바꾸는 값이 아니라서 settings와 따로 둔다.
/// - 배속(하나): 일반 플레이로 인게임에 들어갈 때·떠날 때 저장한다.
/// - 배경 밝기(하나, 11/19): 인게임에서 PgUp·PgDn으로 바꿀 때 저장한다 (일반·테스트 플레이 같은 값).
/// - official: 마지막으로 플레이한 공식 곡·난이도 (일반 플레이로 인게임에 들어갈 때). 타이틀 "플레이"가 이 칸으로 들어간다.
/// - custom: 마지막으로 플레이하거나 에디터로 연 나만의 채보 (일반 플레이 · E 편집 · 새 채보 생성 · 에디터에서 새 채보를 처음 저장할 때).
///   chartId가 비어 있고 songId만 있으면 "그 곡의 + 새 채보 생성 줄" (새 채보를 저장하지 않고 나감). 타이틀 "에디터"가 이 칸으로 들어간다.
/// - 테스트 플레이·자동 플레이는 저장하지 않는다. 한 칸을 바꿀 때 다른 칸은 건드리지 않는다.
/// 예전 형식(songId · chartId · chartType 한 벌)은 PlayerDataStore가 읽을 때 chartType에 맞는 칸으로 옮긴다 (LegacyLastSelectionSaveData).
/// 불러온 뒤 PlayerDataRules.Sanitize로 정리한다.
/// </summary>
[Serializable]
public class LastSelectionSaveData
{
    /// <summary>마지막 배속 (1.0 ~ 8.0, 0.1 단위)</summary>
    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>
    /// 마지막 인게임 배경 밝기 (%, 0 · 25 · 50 · 75 · 100, 기획서 2-4 11/19). 인게임에서 PgUp·PgDn으로 바꿀 때 저장하고 판 시작 때 읽는다.
    /// 예전 JSON(이 필드 없음)은 기본값 100이 남는다. 단계에 없는 값은 Sanitize가 가장 가까운 단계로 바꾼다.
    /// </summary>
    public int backgroundBrightness = GameConstants.Play.DefaultBackgroundBrightness;

    /// <summary>
    /// 마지막 에디터 테스트 플레이 배속 (1.0 ~ 8.0, 0.1 단위, 기획서 2-4 11/19). 에디터 왼쪽 패널에서 바꿀 때 저장하고, 에디터에 들어올 때 읽는다.
    /// 인게임 배속(scrollMultiplier)과 따로다. 예전 JSON(이 필드 없음)은 기본값 1.0이 남는다.
    /// </summary>
    public float editorTestPlayMultiplier = GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>
    /// 에디터 「테스트 후 결과 보기」 스위치 (11/20, 기획서 2-4 · 3-6). 에디터 왼쪽 패널에서 바꿀 때 저장하고, 에디터에 들어올 때 읽는다.
    /// 예전 JSON(이 필드 없음)은 기본값 true가 남는다.
    /// </summary>
    public bool editorShowTestResult = GameConstants.Editor.ShowTestResultDefaultOn;

    /// <summary>공식 채보 탭의 마지막 선택</summary>
    public TabSelectionSaveData official = new TabSelectionSaveData();

    /// <summary>나만의 채보 탭의 마지막 선택</summary>
    public TabSelectionSaveData custom = new TabSelectionSaveData();

    /// <summary>탭의 칸 (null이면 만들어 둔다)</summary>
    public TabSelectionSaveData Get(ChartType type)
    {
        if (type == ChartType.Custom) return custom ??= new TabSelectionSaveData();
        return official ??= new TabSelectionSaveData();
    }

    /// <summary>복사본 (테스트가 바꾼 뒤 되돌릴 때)</summary>
    public LastSelectionSaveData Clone()
    {
        return new LastSelectionSaveData
        {
            scrollMultiplier = scrollMultiplier,
            backgroundBrightness = backgroundBrightness,
            editorTestPlayMultiplier = editorTestPlayMultiplier,
            editorShowTestResult = editorShowTestResult,
            official = Get(ChartType.Official).Clone(),
            custom = Get(ChartType.Custom).Clone()
        };
    }

    /// <summary>다른 값을 그대로 옮겨 온다 (같은 객체를 쓰는 곳이 있어 바꿔 끼우지 않고 값만 바꾼다)</summary>
    public void CopyFrom(LastSelectionSaveData other)
    {
        if (other == null) return;
        scrollMultiplier = other.scrollMultiplier;
        backgroundBrightness = other.backgroundBrightness;
        editorTestPlayMultiplier = other.editorTestPlayMultiplier;
        editorShowTestResult = other.editorShowTestResult;
        official = other.Get(ChartType.Official).Clone();
        custom = other.Get(ChartType.Custom).Clone();
    }
}

/// <summary>탭 하나의 마지막 선택 (곡 · 채보). 비어 있으면 빈 문자열</summary>
[Serializable]
public class TabSelectionSaveData
{
    public string songId = string.Empty;

    /// <summary>나만의 채보 칸에서 비어 있고 songId만 있으면 그 곡의 + 줄</summary>
    public string chartId = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(songId);

    public TabSelectionSaveData Clone()
    {
        return new TabSelectionSaveData { songId = songId, chartId = chartId };
    }
}

/// <summary>
/// 11/18 이전 lastSelection 형식 (곡·채보·탭 한 벌). 새 형식에 없는 칸이라 PlayerData에서 따로 읽어 새 칸으로 옮긴다 (PlayerDataRules.MigrateLegacyLastSelection).
/// </summary>
[Serializable]
public class LegacyLastSelectionSaveData
{
    public string songId = string.Empty;
    public string chartId = string.Empty;
    public string chartType = string.Empty;
}

/// <summary>예전 lastSelection만 읽기 위한 JSON 틀</summary>
[Serializable]
public class LegacyPlayerDataFile
{
    public LegacyLastSelectionSaveData lastSelection;
}
