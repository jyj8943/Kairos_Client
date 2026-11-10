/// <summary>
/// 설정 창(Popup_Settings)이 보여주는 값 묶음 (임시). 지금은 팝업 안에서만 들고 있고 아무 데도 적용·저장하지 않는다.
/// TODO(설정 기능): 설정 데이터 클래스(settings.json 저장·불러오기, 기획서 2-4)로 바꾸고, 열 때 그 값을 읽어 채운다.
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

    /// <summary>GameConstants.Settings.Resolutions의 번호</summary>
    public int resolutionIndex;

    /// <summary>GameConstants.Settings.WindowModes의 번호</summary>
    public int windowModeIndex;
}

/// <summary>설정 창의 탭</summary>
public enum SettingsTab
{
    Sound,
    Game,
    Screen
}
