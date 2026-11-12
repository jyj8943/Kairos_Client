using UnityEngine;

/// <summary>
/// 설정 창(Popup_Settings)이 보여주는 값 묶음 (임시).
/// - 음량은 SoundManager, 해상도·창 모드는 Screen에 바로 적용하고, 열 때 그 값으로 다시 맞춘다. 게임 탭 값(오프셋·기본 배속)은 아직 들고만 있다.
/// TODO(설정 저장): 설정 데이터 클래스(settings.json 저장·불러오기, 기획서 2-4)로 바꾸고, 시작할 때 그 값을 읽어 적용한다.
/// </summary>
public class SettingsViewState
{
    /// <summary>열려 있던 탭 (이 실행 동안만 기억)</summary>
    public SettingsTab tab = SettingsTab.Sound;

    public int masterVolume = GameConstants.Settings.DefaultVolume;
    public int musicVolume = GameConstants.Settings.DefaultVolume;
    public int sfxVolume = GameConstants.Settings.DefaultVolume;

    /// <summary>판정 오프셋 (ms, -200 ~ +200, 5ms 단위)</summary>
    public int offsetMs;

    /// <summary>기본 배속 단계 (배속 × 10, ScrollSpeedRules)</summary>
    public int defaultSpeedSteps = ScrollSpeedRules.ToSteps(GameConstants.Play.DefaultScrollMultiplier);

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
