using UnityEngine;

/// <summary>
/// 설정 창(Popup_Settings)이 보여주는 값 묶음 (임시).
/// - 음량은 SoundManager, 해상도·창 모드는 Screen에 바로 적용하고, 열 때 그 값으로 다시 맞춘다.
/// - 판정 오프셋은 여기 두지 않고 GameManager.Settings.OffsetMs를 바로 읽고 쓴다.
/// TODO(플레이 데이터 저장): 설정 저장·불러오기와 함께 정리한다 (기획서 2-4).
/// </summary>
public class SettingsViewState
{
    /// <summary>열려 있던 탭 (이 실행 동안만 기억)</summary>
    public SettingsTab tab = SettingsTab.Sound;

    public int masterVolume = GameConstants.Settings.DefaultVolume;
    public int musicVolume = GameConstants.Settings.DefaultVolume;
    public int sfxVolume = GameConstants.Settings.DefaultVolume;

    /// <summary>고른 해상도 (해상도 드롭다운 항목). 열 때 지금 화면 크기로 다시 맞춘다</summary>
    public Vector2Int resolution = GameConstants.Settings.DefaultResolution;

    /// <summary>고른 창 모드 (창 모드 드롭다운 항목). 열 때 지금 화면 모드로 다시 맞춘다</summary>
    public FullScreenMode windowMode = FullScreenMode.ExclusiveFullScreen;
}

/// <summary>설정 창의 탭</summary>
public enum SettingsTab
{
    Sound,
    Game,
    Screen
}
