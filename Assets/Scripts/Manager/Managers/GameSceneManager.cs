using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>오버레이 로딩(곡 로딩 · 일반 로딩 · 암전 로딩) 한 단계의 시각 (확인용 로그·테스트)</summary>
public readonly struct LoadingStep
{
    public readonly string Name;
    /// <summary>Enter(로딩 시작)부터 지난 unscaled 프레임 시간 (초, 오버레이 연출과 같은 시계)</summary>
    public readonly double Seconds;

    public LoadingStep(string name, double seconds)
    {
        Name = name;
        Seconds = seconds;
    }
}

/// <summary>오버레이 로딩 중 한 단계(열기 · 불러오기 · 닫기)의 프레임 통계 (확인용)</summary>
public class LoadingFrameStats
{
    public string Phase;
    public int FrameCount;
    public double MaxMs;
    /// <summary>33ms를 넘은 프레임 수</summary>
    public int SlowCount;
}

/// <summary>
/// 씬 전환과 현재 씬(SceneBase) 관리.
/// - 씬 간 데이터는 Load(sceneName, data)로 넘기고, 다음 씬 Init에서 TakeSceneData&lt;T&gt;()로 받는다.
/// - 오버레이 로딩 (곡 로딩 3-8 · 일반 로딩 3-9 · 암전 로딩 3-10, 같은 루틴 OverlayLoadingRoutine, 오버레이 규약 ILoadingOverlay):
///   1) 입력 차단 (IsLoading, EventSystem 방향키·Enter 전달 끄기) → 2) 화면 데이터 채우기 (오버레이 Open, Enter 프레임) →
///   3) 다음 프레임부터 열기 연출 (이 동안은 화면 말고 아무것도 하지 않는다) →
///   4) 열기가 끝나 화면을 다 덮은 뒤 지금 씬 OnExit → 다음 씬 비동기 로드(Single) → 다음 씬 Init (판 시작은 아직) →
///      정리 (Resources.UnloadUnusedAssets · GC.Collect, 닫기 · 시작 메트로놈 중에 GC가 터지지 않게) →
///   5) 최소 시간 대기 → 6) 닫기 연출 → 7) 다음 씬 OnEnter (SceneBase.Enter).
///   - 곡 로딩 (LoadWithSongLoading, 11/26 시계): 열기 0.95초 뒤 로드. 로딩 진행(씬 로드 progress ÷ 0.9 × 0.9, Init 끝 = 1)을 오버레이에 알리고,
///     "로딩 진행 = 1"과 "바늘 2.4초"가 모두 되어 바늘이 12시에 닿으면(T) 반짝 · 암전 → 검은 화면(T+0.65 ~)에서 정리 → 걷힘(T+0.8 이후, 정리가 길면 그만큼 늦게) →
///     다 걷힌 뒤 OnEnter. 최소 시간 대기는 없다 (가장 짧을 때 3.55초, GameConstants.SongLoading.MinTotalSeconds).
///   - 일반 로딩 (LoadWithLoading): 들어오기 0.5초, 최소 시간 = 들어오기 0초부터 1.5초 (GameConstants.Loading.MinSeconds).
///   - 암전 로딩 (LoadWithFadeLoading, 인게임 → 결과 · 인게임 재시작): 열기 0.5초, 최소 시간 = 열기 끝부터 0.2초 (재시작은 0.4초, 인자로 이번 한 번만),
///     닫기 0.45초 (GameConstants.FadeLoading).
///   - 최소 시간과 그 기준(열기 0초 / 열기 끝)은 오버레이가 정한다 (ILoadingOverlay.MinSeconds · MinFromOpenEnd).
///   - 단계 시각은 Enter 기준이고, 기준 시각(예: 마지막 판정)을 함께 넘기면 그 기준으로 다시 잰 값도 로그에 적는다 (LoadingOriginName · LoadingOriginOffset).
///   - 다음 씬이 Init 중에 Load를 부르면 (예: 에디터가 채보를 못 읽어 곡 선택으로) 같은 오버레이 아래에서 그 씬으로 이어서 간다.
///   - 단계 시각(LoadingSteps)과 단계별 프레임 통계(LoadingFrames)를 두 로딩 모두 로그로 남긴다.
/// - 일반 Load는 지금 동작 그대로다 (오버레이를 건너뛸 때의 인게임 재시작 등).
/// - 테스트(Unity 에디터): SkipLoadingOverlayForTests가 켜져 있으면 오버레이를 건너뛰고 바로 Load한다.
/// </summary>
public class GameSceneManager : MonoSingleton<GameSceneManager>, IGameModule
{
    /// <summary>현재 씬의 진입점. 씬의 SceneBase가 Start에서 등록한다.</summary>
    public SceneBase Current { get; private set; }

    /// <summary>Load 호출부터 다음 씬의 SceneBase가 등록될 때까지 true (오버레이 로딩은 다음 씬 OnEnter까지)</summary>
    public bool IsLoading { get; private set; }

    private object sceneData;

    /// <summary>오버레이 로딩(곡 로딩 · 일반 로딩) 중이면 true. 이 동안 IsLoading도 true</summary>
    public bool IsOverlayLoading { get; private set; }

    /// <summary>곡 로딩 중이면 true (LoadWithSongLoading 호출부터 다음 씬 OnEnter까지)</summary>
    public bool IsSongLoading { get; private set; }

    /// <summary>일반 로딩 중이면 true (LoadWithLoading 호출부터 다음 씬 OnEnter까지)</summary>
    public bool IsGeneralLoading { get; private set; }

    /// <summary>암전 로딩 중이면 true (LoadWithFadeLoading 호출부터 다음 씬 OnEnter까지)</summary>
    public bool IsFadeLoading { get; private set; }

    /// <summary>마지막 오버레이 로딩의 기준 시각 이름 (예: "마지막 판정", 없으면 null, 확인용)</summary>
    public string LoadingOriginName { get; private set; }

    /// <summary>기준 시각부터 Enter까지 지난 unscaled 시간 (초, 기준이 없으면 0). 단계 시각 + 이 값 = 기준 시각부터 지난 시간</summary>
    public double LoadingOriginOffset { get; private set; }

    private double pendingOriginRealtime = double.NaN;
    private string pendingOriginName;

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터): true면 곡 로딩 · 일반 로딩 오버레이 없이 바로 불러온다 (씬 이동마다 1.5초 이상 늘지 않게).
    /// TestSpeedRunCallback이 테스트 실행 동안 켜고, 곡 로딩 · 일반 로딩 테스트만 잠깐 끈다.
    /// </summary>
    public static bool SkipLoadingOverlayForTests;

    /// <summary>
    /// 테스트 전용 (Unity 에디터): 0보다 크면 오버레이 로딩에서 다음 씬 Init 끝 뒤 이만큼(초, unscaled) 더 기다린다 (불러오기가 오래 걸린 경우 확인용).
    /// 쓰는 테스트가 끝나면 0으로 되돌린다.
    /// </summary>
    public static double TestExtraLoadSeconds;
#endif

    /// <summary>오버레이를 건너뛸지 (테스트 실행 중)</summary>
    public static bool IsOverlaySkipped
    {
        get
        {
#if UNITY_EDITOR
            return SkipLoadingOverlayForTests;
#else
            return false;
#endif
        }
    }

    /// <summary>마지막 오버레이 로딩의 단계별 시각 (확인용)</summary>
    public IReadOnlyList<LoadingStep> LoadingSteps => loadingSteps;

    private readonly List<LoadingStep> loadingSteps = new List<LoadingStep>();
    private double loadingStart;
    private SceneBase deferredScene;
    private string redirectScene;
    private object redirectData;
    private bool isWaitingInit;

    private string lastFramePhase;
    private ILoadingOverlay phaseOverlay;
    private readonly List<LoadingFrameStats> frameStats = new List<LoadingFrameStats>();
    private readonly List<string> slowFrames = new List<string>();

    /// <summary>마지막 오버레이 로딩의 단계별 프레임 통계 (확인용)</summary>
    public IReadOnlyList<LoadingFrameStats> LoadingFrames => frameStats;

    /// <summary>마지막 오버레이 로딩에서 33ms를 넘은 프레임 (단계 · Enter부터 시각 · ms, 확인용)</summary>
    public IReadOnlyList<string> LoadingSlowFrames => slowFrames;

    /// <summary>33ms를 넘으면 끊긴 프레임으로 센다</summary>
    public const double SlowFrameMs = 33.0;

    /// <summary>
    /// 오버레이 로딩 중 프레임마다 앞 프레임 길이(unscaledDeltaTime)를 그 프레임의 단계에 넣는다.
    /// </summary>
    private void Update()
    {
        if (lastFramePhase != null)
        {
            double ms = Time.unscaledDeltaTime * 1000.0;
            LoadingFrameStats stats = frameStats.Find(x => x.Phase == lastFramePhase);
            if (stats == null)
            {
                stats = new LoadingFrameStats { Phase = lastFramePhase };
                frameStats.Add(stats);
            }
            stats.FrameCount++;
            if (ms > stats.MaxMs) stats.MaxMs = ms;
            if (ms > SlowFrameMs)
            {
                stats.SlowCount++;
                slowFrames.Add($"{lastFramePhase} {Time.unscaledTimeAsDouble - loadingStart - Time.unscaledDeltaTime:0.000}초 {ms:0.0}ms");
            }
        }
        lastFramePhase = PhaseOf(phaseOverlay);
    }

    /// <summary>지금 프레임의 로딩 단계 (오버레이 로딩 중이 아니면 null)</summary>
    private string PhaseOf(ILoadingOverlay overlay)
    {
        if (!IsOverlayLoading || overlay == null || !overlay.IsShown) return null;
        if (overlay.IsClosing) return overlay.ClosingPhase;
        if (!overlay.HasOpenStarted) return "Open 준비 (Enter 프레임)";
        if (overlay.IsOpening) return overlay.OpeningPhase;
        return "열기 뒤 (불러오기 · 대기)";
    }

    /// <summary>단계별 프레임 통계를 한 줄로</summary>
    public string DescribeLoadingFrames()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (LoadingFrameStats stats in frameStats)
            sb.Append($"[{stats.Phase}] 프레임 {stats.FrameCount}개, 최대 {stats.MaxMs:0.0}ms, 33ms 넘음 {stats.SlowCount}개 / ");
        if (slowFrames.Count > 0) sb.Append("끊긴 프레임: ").Append(string.Join(", ", slowFrames));
        return sb.ToString();
    }

    public void Init()
    {
        Current = null;
        IsLoading = false;
        IsOverlayLoading = false;
        IsSongLoading = false;
        IsGeneralLoading = false;
        IsFadeLoading = false;
        deferredScene = null;
        redirectScene = null;
        redirectData = null;
        isWaitingInit = false;
        sceneData = null;
    }

    /// <summary>
    /// 씬의 SceneBase가 자신을 현재 씬으로 등록한다. 등록되면 로딩이 끝난 것으로 본다.
    /// </summary>
    public void SetCurrent(SceneBase scene)
    {
        Current = scene;
        // 오버레이 로딩은 오버레이가 걷히고 OnEnter를 부를 때까지 로딩 중으로 둔다
        if (!IsOverlayLoading) IsLoading = false;
    }

    /// <summary>
    /// SceneBase가 Init 뒤 부른다. 오버레이 로딩 중이면 OnEnter를 미루고(오버레이가 걷힌 뒤 Enter) true, 아니면 false (바로 OnEnter).
    /// </summary>
    public bool DeferEnter(SceneBase scene)
    {
        if (!IsOverlayLoading || scene == null) return false;
        deferredScene = scene;
        MarkLoading($"{scene.gameObject.scene.name} Init 끝 ({scene.InitMilliseconds:0.0}ms)");
        return true;
    }

    /// <summary>
    /// 씬을 비동기로 불러온다. 현재 씬의 Exit()(OnExit + UI 해제)을 먼저 호출하고, data는 다음 씬에 넘긴다.
    /// 오버레이 로딩으로 들어온 씬이 Init 중에 부르면 같은 오버레이 아래에서 그 씬으로 이어서 간다.
    /// </summary>
    /// <returns>로드를 시작했으면 true. 이미 로드 중이거나 씬이 없으면 false</returns>
    public bool Load(string sceneName, object data = null)
    {
        if (IsOverlayLoading && isWaitingInit && redirectScene == null && IsLoadable(sceneName))
        {
            redirectScene = sceneName;
            redirectData = data;
            MarkLoading($"Init 중 다른 씬 요청: {sceneName}");
            return true;
        }

        if (IsLoading)
        {
            Debug.LogWarning($"[GameSceneManager] 이미 씬을 불러오는 중이라 '{sceneName}' 로드를 무시합니다.");
            return false;
        }

        if (!IsLoadable(sceneName))
        {
            Debug.LogWarning($"[GameSceneManager] '{sceneName}' 씬이 없거나 Build Profiles에 등록되지 않았습니다.");
            return false;
        }

        if (Current != null) Current.Exit();
        Current = null;

        sceneData = data;
        IsLoading = true;
        SceneManager.LoadSceneAsync(sceneName);
        return true;
    }

    private static bool IsLoadable(string sceneName) => !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

    /// <summary>
    /// 곡 로딩 오버레이를 거쳐 씬을 불러온다 (곡 선택 시작 · 결과 다시 하기 → 인게임). 오버레이가 없으면 일반 Load로 간다.
    /// </summary>
    /// <returns>로드를 시작했으면 true. 이미 로드 중이거나 씬이 없으면 false</returns>
    public bool LoadWithSongLoading(string sceneName, InGameEntryData entry)
    {
        Canvas_SongLoading overlay = UIManager.HasInstance ? GameManager.UI.SongLoading : null;
        if (overlay == null || IsOverlaySkipped)
        {
            if (overlay == null) Debug.LogWarning("[GameSceneManager] 곡 로딩 오버레이(Canvas_SongLoading)가 없어 바로 불러옵니다.");
            return Load(sceneName, entry);
        }
        if (!CanStartOverlay(sceneName)) return false;

        IsSongLoading = true;
        StartOverlayLoading(sceneName, entry, overlay, () => overlay.Open(entry));
        return true;
    }

    /// <summary>
    /// 일반 로딩 오버레이를 거쳐 씬을 불러온다 (타이틀 ↔ 곡 선택, 곡 선택 → 에디터, 에디터 → 곡 선택 · 테스트 플레이, 결과 → 곡 선택, 인게임 나가기).
    /// 오버레이가 없거나 테스트 실행 중 건너뛰기면 일반 Load로 간다.
    /// </summary>
    /// <returns>로드를 시작했으면 true. 이미 로드 중이거나 씬이 없으면 false</returns>
    public bool LoadWithLoading(string sceneName, object data = null)
    {
        Canvas_Loading overlay = UIManager.HasInstance ? GameManager.UI.Loading : null;
        if (overlay == null || IsOverlaySkipped)
        {
            if (overlay == null) Debug.LogWarning("[GameSceneManager] 일반 로딩 오버레이(Canvas_Loading)가 없어 바로 불러옵니다.");
            return Load(sceneName, data);
        }
        if (!CanStartOverlay(sceneName)) return false;

        IsGeneralLoading = true;
        StartOverlayLoading(sceneName, data, overlay, () => overlay.Open(sceneName));
        return true;
    }

    /// <summary>
    /// 암전 로딩 오버레이를 거쳐 씬을 불러온다 (인게임 → 결과, 인게임 재시작). 오버레이가 없거나 테스트 실행 중 건너뛰기면 false를 돌려주고 아무것도 하지 않는다
    /// (부르는 쪽이 지금처럼 바로 Load한다).
    /// </summary>
    /// <param name="originRealtime">단계 로그의 기준 시각 (Time.unscaledTimeAsDouble, 예: 마지막 판정). NaN이면 기준 없음</param>
    /// <param name="originName">기준 시각 이름 (로그용)</param>
    /// <param name="minSeconds">이번 한 번의 최소 시간 (열기 끝부터 초). 결과로는 기본값 MinSeconds 0.2, 재시작은 RestartMinSeconds 0.4</param>
    /// <returns>암전 로딩을 시작했으면 true</returns>
    public bool LoadWithFadeLoading(string sceneName, object data, double originRealtime = double.NaN, string originName = null,
        double minSeconds = GameConstants.FadeLoading.MinSeconds)
    {
        Canvas_FadeLoading overlay = UIManager.HasInstance ? GameManager.UI.FadeLoading : null;
        if (overlay == null || IsOverlaySkipped)
        {
            if (overlay == null) Debug.LogWarning("[GameSceneManager] 암전 로딩 오버레이(Canvas_FadeLoading)가 없습니다.");
            return false;
        }
        if (!CanStartOverlay(sceneName)) return false;

        IsFadeLoading = true;
        pendingOriginRealtime = originRealtime;
        pendingOriginName = originName;
        StartOverlayLoading(sceneName, data, overlay, () => overlay.OpenFade(minSeconds));
        return true;
    }

    private bool CanStartOverlay(string sceneName)
    {
        if (IsLoading)
        {
            Debug.LogWarning($"[GameSceneManager] 이미 씬을 불러오는 중이라 '{sceneName}' 로딩을 무시합니다.");
            return false;
        }
        if (!IsLoadable(sceneName))
        {
            Debug.LogWarning($"[GameSceneManager] '{sceneName}' 씬이 없거나 Build Profiles에 등록되지 않았습니다.");
            return false;
        }
        return true;
    }

    private void StartOverlayLoading(string sceneName, object data, ILoadingOverlay overlay, System.Action open)
    {
        sceneData = data;
        IsLoading = true;
        IsOverlayLoading = true;
        deferredScene = null;
        redirectScene = null;
        redirectData = null;
        isWaitingInit = false;
        StartCoroutine(OverlayLoadingRoutine(sceneName, overlay, open));
    }

    /// <summary>
    /// 곡 로딩 · 일반 로딩 · 암전 로딩 공용 흐름 (클래스 설명의 1~7). 최소 시간과 기준은 오버레이가 정한다
    /// (MinFromOpenEnd면 열기 끝부터 — 암전 로딩, 아니면 열기 0초부터 — 일반 로딩). 곡 로딩은 5~6 대신 바늘 · 암전 · 걷힘 (클래스 설명).
    /// </summary>
    private IEnumerator OverlayLoadingRoutine(string sceneName, ILoadingOverlay overlay, System.Action open)
    {
        loadingSteps.Clear();
        frameStats.Clear();
        slowFrames.Clear();
        lastFramePhase = null;
        phaseOverlay = overlay;
        loadingStart = Time.unscaledTimeAsDouble;
        bool hasOrigin = !double.IsNaN(pendingOriginRealtime);
        LoadingOriginName = hasOrigin ? pendingOriginName : null;
        LoadingOriginOffset = hasOrigin ? loadingStart - pendingOriginRealtime : 0;
        pendingOriginRealtime = double.NaN;
        pendingOriginName = null;
        MarkLoading("Enter");

        // 1. 방향키·Enter가 뒤 화면의 선택을 옮기지 않게 EventSystem 전달을 잠시 끈다 (UI 맵은 그대로)
        EventSystem eventSystem = EventSystem.current;
        bool sentNavigation = eventSystem != null && eventSystem.sendNavigationEvents;
        if (eventSystem != null) eventSystem.sendNavigationEvents = false;

        // 2. 화면 데이터를 먼저 채운다 (Enter 프레임). 3. 열기 연출 0초는 다음 프레임부터
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        open();
        MarkLoading($"오버레이 Open 준비 ({watch.Elapsed.TotalMilliseconds:0.0}ms)");
        while (!overlay.HasOpenStarted) yield return null;
        // 오버레이가 센 0초 그대로 남긴다 (이 루틴이 한 프레임 늦게 볼 수 있어서)
        double openStartTime = Time.unscaledTimeAsDouble - overlay.OpenElapsed;
        MarkLoadingAt(overlay.OpenStartStep, openStartTime);

        // 열기 연출 동안은 화면 말고는 아무것도 하지 않는다
        while (overlay.IsOpening) yield return null;
        MarkLoading(overlay.OpenEndStep);
        double openEndTime = Time.unscaledTimeAsDouble;

        Canvas_SongLoading songOverlay = overlay as Canvas_SongLoading;

        // 4. 화면을 다 덮은 뒤: 앞 씬 OnExit → 다음 씬 비동기 로드(Single, 앞 씬은 함께 내려간다) → 다음 씬 Init
        string target = sceneName;
        while (true)
        {
            if (Current != null)
            {
                string leaving = Current.gameObject.scene.name;
                watch.Restart();
                Current.Exit();
                MarkLoading($"{leaving} OnExit ({watch.Elapsed.TotalMilliseconds:0.0}ms)");
            }
            Current = null;
            deferredScene = null;

            watch.Restart();
            isWaitingInit = true;
            AsyncOperation operation = SceneManager.LoadSceneAsync(target);
            MarkLoading($"{target} 로드 시작 (호출 {watch.Elapsed.TotalMilliseconds:0.0}ms)");
            while (operation != null && !operation.isDone)
            {
                if (songOverlay != null) songOverlay.SetLoadProgress(SongLoadingClockTimeline.LoadProgress(true, operation.progress, false));
                yield return null;
            }
            if (songOverlay != null) songOverlay.SetLoadProgress(SongLoadingClockTimeline.LoadProgress(true, 1f, false));
            MarkLoading($"{target} 로드 끝");
            while (deferredScene == null) yield return null;
            isWaitingInit = false;

            // 다음 씬이 Init 중에 다른 씬을 요청했으면 같은 오버레이 아래에서 이어서 간다
            if (redirectScene == null) break;
            target = redirectScene;
            sceneData = redirectData;
            redirectScene = null;
            redirectData = null;
        }

#if UNITY_EDITOR
        // 테스트 전용: 불러오기가 오래 걸린 경우를 흉내 낸다 (Init 끝 뒤 이만큼 더 기다린다)
        if (TestExtraLoadSeconds > 0)
        {
            double delayStart = Time.unscaledTimeAsDouble;
            while (Time.unscaledTimeAsDouble - delayStart < TestExtraLoadSeconds) yield return null;
            MarkLoading($"테스트 지연 끝 ({TestExtraLoadSeconds:0.###}초)");
        }
#endif

        if (songOverlay != null)
        {
            // 곡 로딩: 진행 1 → 바늘 12시(T) → 암전 끝 → 검은 화면에서 정리 → 걷힘 (최소 시간 대기 없음)
            songOverlay.SetLoadProgress(1f);
            MarkLoading("로딩 진행 1");
            while (!songOverlay.HasReachedTwelve) yield return null;
            MarkLoadingAt($"바늘 12시 T (열기 시작 +{songOverlay.TwelveAt:0.000}초)", openStartTime + songOverlay.TwelveAt);
            while (!songOverlay.IsBlackedOut) yield return null;
            MarkLoading("암전 끝");
            yield return CleanupRoutine(watch);
            // 정리(GC)가 든 긴 프레임에서 걷히기 시작하면 첫 화면이 그 프레임 길이만큼 건너뛴다. 한 프레임 넘긴 뒤 걷는다
            yield return null;
            songOverlay.ReleaseBlackout();
            yield return songOverlay.CloseRoutine();
            MarkLoading($"걷힘 끝 (걷힘 시작 T+{songOverlay.RevealStart:0.000}초)");
        }
        else
        {
            // 닫기 연출 · 시작 메트로놈 중에 GC가 터지지 않게 정리는 닫기 전에 끝낸다
            yield return CleanupRoutine(watch);
            // 정리(GC)가 든 긴 프레임에서 닫기를 시작하면 닫기 첫 화면이 그 프레임 길이만큼 건너뛴다 (암전 로딩은 최소 0.2초가 이미 지나 있다).
            // 한 프레임 넘긴 뒤 닫기를 시작한다 (일반 로딩은 아래 최소 시간 대기가 길어 시각이 바뀌지 않는다)
            yield return null;

            // 5. 최소 시간: 암전 로딩은 열기 끝부터, 일반 로딩은 열기 0초부터 (오버레이가 정한다)
            double minBase = overlay.MinFromOpenEnd ? openEndTime : openStartTime;
            while (Time.unscaledTimeAsDouble - minBase < overlay.MinSeconds) yield return null;

            // 6. 닫기 연출
            MarkLoading("닫기 시작");
            yield return overlay.CloseRoutine();
            MarkLoading("닫기 끝");
        }

        if (eventSystem != null) eventSystem.sendNavigationEvents = sentNavigation;
        EventSystem next = EventSystem.current;
        if (next != null && next != eventSystem) next.sendNavigationEvents = true;

        // 7. 닫기 끝(곡 로딩은 걷힘 끝) → 다음 씬 OnEnter (인게임이면 판 시작 · 시작 메트로놈)
        SceneBase scene = deferredScene;
        deferredScene = null;
        IsSongLoading = false;
        IsGeneralLoading = false;
        IsFadeLoading = false;
        IsOverlayLoading = false;
        IsLoading = false;
        scene.Enter();
        MarkLoading($"{target} OnEnter");
        phaseOverlay = null;
        lastFramePhase = null;
        Debug.Log($"[GameSceneManager] {overlay.LogName} 끝: {DescribeLoading()}", this);
        Debug.Log($"[GameSceneManager] {overlay.LogName} 프레임: {DescribeLoadingFrames()}", this);
    }

    /// <summary>
    /// 정리: Resources.UnloadUnusedAssets · GC.Collect (닫기 · 시작 메트로놈 중에 GC가 터지지 않게. 곡 로딩은 검은 화면 동안).
    /// </summary>
    private IEnumerator CleanupRoutine(System.Diagnostics.Stopwatch watch)
    {
        watch.Restart();
        AsyncOperation unload = Resources.UnloadUnusedAssets();
        while (unload != null && !unload.isDone) yield return null;
        double unloadMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        System.GC.Collect();
        double gcMs = watch.Elapsed.TotalMilliseconds;
        // 프레임 시각은 이 프레임이 시작한 때라 GC.Collect 시간을 더해 실제로 끝난 때로 남긴다
        MarkLoadingAt($"정리 끝 (UnloadUnusedAssets {unloadMs:0.0}ms, GC.Collect {gcMs:0.0}ms)", Time.unscaledTimeAsDouble + gcMs / 1000.0);
    }

    /// <summary>로딩 단계를 지금 시각으로 남긴다</summary>
    public void MarkLoading(string step)
    {
        loadingSteps.Add(new LoadingStep(step, Time.unscaledTimeAsDouble - loadingStart));
    }

    /// <summary>로딩 단계를 주어진 시각(Time.unscaledTimeAsDouble 기준)으로 남긴다</summary>
    private void MarkLoadingAt(string step, double realtime)
    {
        loadingSteps.Add(new LoadingStep(step, realtime - loadingStart));
    }

    /// <summary>로딩 단계 시각을 한 줄로 (기준 시각이 있으면 "기준 +x초"도 같이)</summary>
    public string DescribeLoading()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (LoadingOriginName != null) sb.Append('(').Append(LoadingOriginName).Append(" 기준 +").Append(LoadingOriginOffset.ToString("0.000")).Append("초에 Enter) ");
        for (int i = 0; i < loadingSteps.Count; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append(loadingSteps[i].Name).Append(' ').Append(loadingSteps[i].Seconds.ToString("0.000")).Append('초');
            if (LoadingOriginName != null) sb.Append(" [").Append((loadingSteps[i].Seconds + LoadingOriginOffset).ToString("0.000")).Append(']');
        }
        return sb.ToString();
    }

    /// <summary>
    /// 이전 씬이 넘긴 데이터를 꺼내고 비운다. 다음 씬 Init에서 한 번만 꺼내 쓴다.
    /// </summary>
    /// <returns>데이터가 없거나 타입이 다르면 null</returns>
    public T TakeSceneData<T>() where T : class
    {
        T data = sceneData as T;

        if (sceneData != null && data == null)
            Debug.LogWarning($"[GameSceneManager] 씬 데이터 타입이 {sceneData.GetType().Name}이라 {typeof(T).Name}(으)로 꺼낼 수 없습니다.");

        sceneData = null;
        return data;
    }
}
