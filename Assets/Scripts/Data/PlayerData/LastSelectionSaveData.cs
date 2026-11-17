using System;

/// <summary>
/// PlayerData.json에 저장하는 곡 선택의 마지막 선택 상태 (기획서 2-4 마지막 선택, 3-3 저장 데이터).
/// 설정 창에서 바꾸는 값이 아니라서 settings와 따로 둔다.
/// - 배속: 일반 플레이로 인게임에 들어갈 때·떠날 때 저장한다.
/// - 마지막으로 플레이한 곡·채보·탭(11/17): 일반 플레이로 인게임에 들어갈 때 저장한다 (테스트 플레이는 저장하지 않는다).
/// 이 칸이 없는 예전 파일은 기본값(빈 ID → 공식 탭 첫 곡)으로 읽는다. 불러온 뒤 PlayerDataRules.Sanitize로 정리한다.
/// </summary>
[Serializable]
public class LastSelectionSaveData
{
    /// <summary>마지막 배속 (1.0 ~ 8.0, 0.1 단위)</summary>
    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>마지막으로 플레이한 곡 ID (없으면 빈 문자열)</summary>
    public string songId = string.Empty;

    /// <summary>마지막으로 플레이한 채보 ID (없으면 빈 문자열)</summary>
    public string chartId = string.Empty;

    /// <summary>"Official" / "Custom" (ChartType 이름). 곡 선택에 들어올 때 이 탭으로 시작한다</summary>
    public string chartType = ChartType.Official.ToString();
}
