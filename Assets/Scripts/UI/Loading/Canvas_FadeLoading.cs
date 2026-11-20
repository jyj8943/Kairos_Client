using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 암전 로딩 오버레이 (기획서 3-10 「암전 로딩」 · 🔧 연출 상세, 와이어프레임 15_InGame_ResultFade). 인게임 → 결과 씬 이동만 덮는다.
/// - Managers_DontDestroy/UIManager 아래에 있어 씬이 바뀌어도 살아 있다. 정렬 순서는 다른 로딩보다 위(GameConstants.FadeLoading.SortOrder).
/// - 자식: 전체 화면 검은 Image Img_Fade 하나 (맨 앞, raycastTarget 켬 → 뒤 화면 마우스를 막는다). 색은 UITheme.fadeColor.
/// - Bootstrapper가 전역 매니저 Init 때 Init()을 한 번 부르고 숨겨 둔다.
/// - 흐름은 GameSceneManager.LoadWithFadeLoading(오버레이 로딩 공용 루틴)이 정한다:
///   OpenFade() → 다음 프레임부터 열기 0.5초 (알파 0 → 1 t², 곡 음악 계수 1 → 0 같은 곡선) → (지금 씬 OnExit · 결과 씬 로드 · Init · 정리 ·
///   열기 끝부터 0.2초) → CloseRoutine() 닫기 0.45초 (알파 1 → 0 ease-out) → 결과 OnEnter.
/// - 음악: SoundManager.MusicFadeMultiplier만 바꾼다 (설정 음량은 그대로). 노래는 DontDestroy 쪽 SoundManager가 재생하므로
///   인게임 OnExit의 StopMusic이 멈추고 계수를 1로 되돌린다. 닫을 때도 1로 되돌려 결과 · 다음 곡에 0이 남지 않게 한다.
///   열 때 노래가 재생 중이 아니면(곡이 이미 끝남) 계수를 건드리지 않는다.
/// - 시간은 unscaled 프레임 시각 (음악이 줄어도 흐른다).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class Canvas_FadeLoading : UIBase, ILoadingOverlay
{
    [Tooltip("전체 화면 검은 덮개 (맨 앞, raycastTarget 켬)")]
    [SerializeField] private Image fade;

    private Canvas canvas;
    private bool isShown;
    private bool isClosing;
    private bool openStarted;
    private bool fadesMusic;
    private double openStart;
    private Coroutine openRoutine;

    public override UILayer Layer => UILayer.Overlay;

    // 오버레이 규약 (ILoadingOverlay)
    public string LogName => "암전 로딩";
    public string OpenStartStep => "열기 시작";
    public string OpenEndStep => "열기 끝";
    public string OpeningPhase => "열기 0~0.5초";
    public string ClosingPhase => "닫기";
    public double MinSeconds => GameConstants.FadeLoading.MinSeconds;
    public bool MinFromOpenEnd => true;

    public bool IsShown => isShown;
    public bool HasOpenStarted => isShown && openStarted;
    public bool IsOpening => isShown && !isClosing && (!openStarted || OpenElapsed < GameConstants.FadeLoading.OpenSeconds);
    public bool IsClosing => isClosing;
    public float OpenElapsed => isShown && openStarted ? (float)(Time.unscaledTimeAsDouble - openStart) : 0f;

    /// <summary>지금 덮개 알파 (확인용)</summary>
    public float FadeAlpha => fade != null ? fade.color.a : 0f;

    /// <summary>검은 덮개 (확인용)</summary>
    public Image Fade => fade;

    /// <summary>이번 열기에서 곡 음악을 줄이는지 (열 때 노래가 재생 중이었는지, 확인용)</summary>
    public bool FadesMusic => fadesMusic;

    private void CacheComponents()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
    }

    /// <summary>
    /// 덮개 색 · 마우스 막기를 맞추고 숨긴다. Bootstrapper가 전역 매니저 Init 때 한 번 부른다.
    /// </summary>
    public void Init()
    {
        CacheComponents();
        if (fade != null) fade.raycastTarget = true;
        Hide();
    }

    /// <summary>
    /// 보이기 시작한다 (Enter 프레임, 알파 0). 열기 0초는 그다음 프레임부터 센다.
    /// </summary>
    public void OpenFade()
    {
        CacheComponents();
        isShown = true;
        isClosing = false;
        openStarted = false;
        fadesMusic = SoundManager.HasInstance && GameManager.Sound.IsMusicSourcePlaying;
        canvas.enabled = true;
        if (fade != null) fade.raycastTarget = true;
        SetAlpha(0f);
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(OpenRoutine());
    }

    /// <summary>열기 0.5초: 알파 0 → 1, 곡 음악 계수 1 → 0 (같은 t² 곡선)</summary>
    private IEnumerator OpenRoutine()
    {
        yield return null;
        openStart = Time.unscaledTimeAsDouble;
        openStarted = true;
        while (isShown && !isClosing && OpenElapsed < GameConstants.FadeLoading.OpenSeconds)
        {
            ApplyOpen(OpenElapsed);
            yield return null;
        }
        if (isShown && !isClosing) ApplyOpen(GameConstants.FadeLoading.OpenSeconds);
        openRoutine = null;
    }

    private void ApplyOpen(float elapsed)
    {
        SetAlpha(FadeLoadingRules.OpenAlpha(elapsed));
        if (fadesMusic && SoundManager.HasInstance) GameManager.Sound.MusicFadeMultiplier = FadeLoadingRules.OpenMusicFactor(elapsed);
    }

    /// <summary>닫기 0.45초: 알파 1 → 0 (ease-out) 뒤 숨긴다. 곡 음악 계수는 1로 되돌린다</summary>
    public IEnumerator CloseRoutine()
    {
        if (!isShown) yield break;
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = null;
        isClosing = true;
        RestoreMusic();
        double closeStart = Time.unscaledTimeAsDouble;
        float elapsed = 0f;
        while (elapsed < GameConstants.FadeLoading.CloseSeconds)
        {
            SetAlpha(FadeLoadingRules.CloseAlpha(elapsed));
            yield return null;
            elapsed = (float)(Time.unscaledTimeAsDouble - closeStart);
        }
        Hide();
    }

    private void RestoreMusic()
    {
        if (fadesMusic && SoundManager.HasInstance) GameManager.Sound.MusicFadeMultiplier = 1f;
        fadesMusic = false;
    }

    private void Hide()
    {
        CacheComponents();
        isShown = false;
        isClosing = false;
        openStarted = false;
        RestoreMusic();
        SetAlpha(0f);
        if (fade != null) fade.raycastTarget = false;
        canvas.enabled = false;
    }

    private void SetAlpha(float alpha)
    {
        if (fade == null) return;
        fade.color = UITheme.WithAlpha(UITheme.Current.fadeColor, alpha);
    }
}
