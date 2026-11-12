using System;

/// <summary>
/// PlayerData.json에 저장하는 설정 값 (기획서 2-4). 불러온 뒤 PlayerDataRules.Sanitize로 범위를 정리한다.
/// </summary>
[Serializable]
public class SettingsSaveData
{
    /// <summary>메인·음악·효과음 음량 (0~100)</summary>
    public int masterVolume = GameConstants.Settings.DefaultVolume;
    public int musicVolume = GameConstants.Settings.DefaultVolume;
    public int sfxVolume = GameConstants.Settings.DefaultVolume;

    /// <summary>판정 오프셋 (ms, -200 ~ +200, 5ms 단위)</summary>
    public int offsetMs;

    /// <summary>해상도 (GameConstants.Settings.Resolutions 중 하나)</summary>
    public int resolutionWidth = GameConstants.Settings.DefaultResolution.x;
    public int resolutionHeight = GameConstants.Settings.DefaultResolution.y;

    /// <summary>창 모드 (FullScreenMode 이름, GameConstants.Settings.WindowModes 중 하나)</summary>
    public string windowMode = GameConstants.Settings.DefaultWindowMode.ToString();
}
