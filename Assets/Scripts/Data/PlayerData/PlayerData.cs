using System;
using System.Collections.Generic;

/// <summary>
/// 유저 플레이 데이터 파일(persistentDataPath/PlayerData.json)의 내용 (기획서 2-4). 설정과 공식·나만의 채보 기록을 한 파일에 둔다.
/// - JsonUtility로 읽고 쓰므로 공개 필드와 List만 쓴다 (Dictionary·프로퍼티를 두지 않는다). enum은 이름 문자열로 저장한다.
/// - 형식이 바뀌면 version을 올리고 불러올 때 변환한다 (PlayerDataRules.Sanitize). 나중에 profile 묶음을 추가할 예정이다.
/// </summary>
[Serializable]
public class PlayerData
{
    /// <summary>파일 형식 버전 (GameConstants.PlayerData.CurrentVersion)</summary>
    public int version = GameConstants.PlayerData.CurrentVersion;

    /// <summary>마지막으로 저장한 시각 (UTC, yyyy-MM-ddTHH:mm:ssZ)</summary>
    public string savedAt = string.Empty;

    /// <summary>설정 값</summary>
    public SettingsSaveData settings = new SettingsSaveData();

    /// <summary>곡 선택의 마지막 선택 상태 (지금은 배속만). 이 칸이 없는 예전 파일은 기본값(1.0배)으로 읽는다</summary>
    public LastSelectionSaveData lastSelection = new LastSelectionSaveData();

    /// <summary>채보별 기록 (chartId마다 하나)</summary>
    public List<ChartRecordData> records = new List<ChartRecordData>();
}
