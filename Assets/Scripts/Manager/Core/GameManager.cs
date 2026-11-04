/// <summary>
/// 전역 매니저 접근 창구. 씬에 붙이지 않는 static 클래스.
/// 사용 예: GameManager.Sound.PlaySfx(...), GameManager.Scene.Load(...)
/// </summary>
public static class GameManager
{
    /// <summary>Bootstrapper가 모든 매니저의 Init()을 마치면 true</summary>
    public static bool IsInitialized { get; internal set; }
    
    public static GameSceneManager Scene => GameSceneManager.Instance;

    /// <summary>제공 곡 목록 (곡 찾기·검사)</summary>
    public static SongManager Songs => SongManager.Instance;

    /// <summary>노래·효과음 재생과 곡 시간 (DSP 기준)</summary>
    public static SoundManager Sound => SoundManager.Instance;
}

/// <summary>
/// Bootstrapper가 순서대로 초기화하는 전역 매니저가 구현한다.
/// </summary>
public interface IGameModule
{
    void Init();
}