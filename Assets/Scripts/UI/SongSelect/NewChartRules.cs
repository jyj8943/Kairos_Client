using UnityEngine;

/// <summary>
/// 새 채보 팝업(Popup_NewChart, 기획서 3-7 ④) 입력 검사 (순수 함수 · EditMode 테스트용).
/// 예전 에디터 곡 고르기의 만들기 검사(이름 앞뒤 공백 제거, 레벨 범위)를 옮겨 왔다.
/// </summary>
public static class NewChartRules
{
    /// <summary>
    /// 채보 이름을 검사한다: 앞뒤 공백을 지운 이름이 비어 있으면(공백만 포함) false.
    /// </summary>
    /// <param name="trimmed">앞뒤 공백을 지운 이름 (비었으면 빈 문자열)</param>
    public static bool IsValidName(string name, out string trimmed)
    {
        trimmed = name == null ? string.Empty : name.Trim();
        return trimmed.Length > 0;
    }

    /// <summary>레벨을 GameConstants.Editor.MinLevel ~ MaxLevel 안으로 자른다</summary>
    public static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, GameConstants.Editor.MinLevel, GameConstants.Editor.MaxLevel);
    }

    /// <summary>
    /// 만들기 검사: 이름이 비면 null (오류 문구를 띄운다), 아니면 에디터로 넘길 데이터 (새 채보라 chartId는 null, 레벨은 범위 안으로).
    /// </summary>
    public static EditorEntryData CreateEntry(string songId, string chartName, int level)
    {
        if (string.IsNullOrEmpty(songId) || !IsValidName(chartName, out string trimmed)) return null;

        return new EditorEntryData
        {
            songId = songId,
            chartName = trimmed,
            level = ClampLevel(level),
            chartId = null
        };
    }

    /// <summary>처음 이름 (순수 함수): "새 채보 {그 곡의 채보 수 + 1}"</summary>
    public static string DefaultName(int existingChartCount)
    {
        return string.Format(GameConstants.SongSelect.NewChartDefaultNameFormat, Mathf.Max(0, existingChartCount) + 1);
    }
}
