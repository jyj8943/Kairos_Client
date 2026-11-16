using UnityEngine;

/// <summary>난이도 표기 색 한 쌍 (배경과 그 위 글자)</summary>
public readonly struct DifficultyColorPair
{
    public readonly Color32 Background;
    public readonly Color32 Text;

    public DifficultyColorPair(Color32 background, Color32 text)
    {
        Background = background;
        Text = text;
    }
}

/// <summary>
/// 난이도 이름 → 표기 색 (곡 선택 Btn_Difficulty와 인게임 Canvas_InGame 뱃지가 같이 쓴다).
/// - 이름(GameConstants.Chart.DifficultyEasy 등)으로 찾고 대소문자는 가리지 않는다. 배열 순번을 쓰지 않는다.
/// - 모르는 이름·빈 이름은 CUSTOM 색이다. 색 값은 GameConstants.DifficultyColor 한 곳에만 둔다.
/// </summary>
public static class DifficultyColors
{
    /// <summary>나만의 채보·테스트 플레이(CUSTOM) 색</summary>
    public static DifficultyColorPair Custom =>
        new DifficultyColorPair(GameConstants.DifficultyColor.CustomBackground, GameConstants.DifficultyColor.CustomText);

    /// <summary>
    /// 난이도 이름의 배경·글자색. 모르는 이름이면 Custom.
    /// </summary>
    public static DifficultyColorPair Get(string difficulty)
    {
        string name = string.IsNullOrEmpty(difficulty) ? string.Empty : difficulty.Trim().ToUpperInvariant();
        switch (name)
        {
            case GameConstants.Chart.DifficultyEasy:
                return new DifficultyColorPair(GameConstants.DifficultyColor.EasyBackground, GameConstants.DifficultyColor.EasyText);
            case GameConstants.Chart.DifficultyNormal:
                return new DifficultyColorPair(GameConstants.DifficultyColor.NormalBackground, GameConstants.DifficultyColor.NormalText);
            case GameConstants.Chart.DifficultyHard:
                return new DifficultyColorPair(GameConstants.DifficultyColor.HardBackground, GameConstants.DifficultyColor.HardText);
            default:
                return Custom;
        }
    }
}
