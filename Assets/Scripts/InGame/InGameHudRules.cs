using System;

/// <summary>
/// 인게임 곡 정보 패널에 무엇을 보일지 (기획서 3-4 "왼쪽 곡 정보 패널: 공식 채보 / 나만의 채보" 표).
/// </summary>
public struct SongInfoDisplay
{
    /// <summary>뱃지 문구 (공식: 난이도 이름, 나만의 채보: CUSTOM)</summary>
    public string BadgeText;

    /// <summary>뱃지 색을 고를 난이도 순번 (GameConstants.Chart.Difficulties 안의 위치). 나만의 채보·모르는 난이도는 -1</summary>
    public int DifficultyIndex;

    /// <summary>나만의 채보면 true (CUSTOM 색)</summary>
    public bool IsCustom;

    /// <summary>채보 이름을 보일지 (나만의 채보이고 이름이 있을 때만)</summary>
    public bool ShowChartName;

    /// <summary>채보 이름 (ShowChartName일 때만 쓴다)</summary>
    public string ChartName;

    /// <summary>"Lv.3" (항상 보인다)</summary>
    public string LevelText;

    /// <summary>패널 오른쪽 위 테스트 플레이 표시를 켤지</summary>
    public bool ShowTestPlay;

    /// <summary>테스트 플레이 표시 문구 (자동 플레이면 "자동 플레이"를 함께 쓴다)</summary>
    public string TestPlayText;
}

/// <summary>
/// 인게임 화면 표시 규칙 (MonoBehaviour와 떨어진 계산만, EditMode 테스트용).
/// - 곡 정보 패널: 공식 채보 → 난이도 뱃지 + Lv. (채보 이름 숨김), 나만의 채보 → CUSTOM 뱃지 + 채보 이름 + Lv.
///   테스트 플레이·자동 플레이도 같은 규칙이고, 테스트 플레이일 때만 테스트 플레이 표시를 켠다.
/// - 곡 진행 바 비율 = 곡 시간 ÷ 곡 길이 (0~1로 자른다).
/// </summary>
public static class InGameHudRules
{
    /// <summary>
    /// 곡 정보 패널 표시를 정한다.
    /// </summary>
    /// <param name="chartType">채보 종류 (ChartData.type)</param>
    /// <param name="difficulty">난이도 이름 (ChartData.difficulty, 공식 채보만 쓴다)</param>
    /// <param name="chartName">채보 이름 (나만의 채보만 보인다)</param>
    /// <param name="level">레벨</param>
    /// <param name="isTestPlay">테스트 플레이면 true</param>
    /// <param name="isAutoPlay">자동 플레이면 true (테스트 플레이 표시에 "자동 플레이"를 함께 쓴다)</param>
    public static SongInfoDisplay BuildSongInfo(ChartType chartType, string difficulty, string chartName, int level, bool isTestPlay, bool isAutoPlay)
    {
        SongInfoDisplay display = new SongInfoDisplay
        {
            LevelText = string.Format(GameConstants.Play.InGameLevelFormat, level),
            ShowTestPlay = isTestPlay || isAutoPlay,
            TestPlayText = isAutoPlay ? GameConstants.Play.AutoTestPlayLabel : GameConstants.Play.TestPlayLabel
        };

        if (chartType == ChartType.Official)
        {
            string name = string.IsNullOrEmpty(difficulty) ? string.Empty : difficulty.ToUpperInvariant();
            display.BadgeText = name;
            display.DifficultyIndex = Array.IndexOf(GameConstants.Chart.Difficulties, name);
            display.IsCustom = false;
            display.ShowChartName = false;
            display.ChartName = string.Empty;
        }
        else
        {
            display.BadgeText = GameConstants.Play.ChartBadgeCustomText;
            display.DifficultyIndex = -1;
            display.IsCustom = true;
            display.ShowChartName = !string.IsNullOrEmpty(chartName);
            display.ChartName = chartName ?? string.Empty;
        }

        return display;
    }

    /// <summary>
    /// 곡 진행 바 비율 = 곡 시간 ÷ 곡 길이. 0 미만(시작 전 대기)은 0, 1 초과(곡 끝 뒤)는 1. 곡 길이가 0 이하면 0.
    /// </summary>
    public static float ProgressRatio(double songTime, double songLength)
    {
        if (songLength <= 0 || double.IsNaN(songTime)) return 0f;
        double ratio = songTime / songLength;
        if (ratio < 0) return 0f;
        if (ratio > 1) return 1f;
        return (float)ratio;
    }
}
