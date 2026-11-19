using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>곡 로딩 한 단계의 시각 (확인용 로그·테스트)</summary>
public readonly struct SongLoadingStep
{
    public readonly string Name;
    /// <summary>Enter(곡 로딩 시작)부터 지난 unscaled 프레임 시간 (초, 오버레이 연출과 같은 시계)</summary>
    public readonly double Seconds;

    public SongLoadingStep(string name, double seconds)
    {
        Name = name;
        Seconds = seconds;
    }
}

/// <summary>
/// 씬 전환과 현재 씬(SceneBase) 관리.
/// - 씬 간 데이터는 Load(sceneName, data)로 넘기고, 다음 씬 Init에서 TakeSceneData&lt;T&gt;()로 받는다.
/// - 곡 로딩 (기획서 3-9): LoadWithSongLoading(sceneName, entry)는 곡 로딩 오버레이(Canvas_SongLoading)를 지금 씬 위에 열고,
///   0.35초(화면을 다 덮은 뒤)에 지금 씬 Exit + 다음 씬 비동기 로드 → 다음 씬 Init (판 시작은 아직) →
///   로드 끝과 열기 시작부터 1.5초를 둘 다 채우면 닫기 연출 → 다 걷힌 뒤 다음 씬 OnEnter (SceneBase.Enter).
///   그동안 IsLoading이 true라 ESC(UIManager)·씬 이동·화면 단축키를 막고, EventSystem 방향키·Enter(Navigate · Submit) 전달도 잠시 끈다.
///   일반 Load는 지금 동작 그대로다.
/// </summary>
public class GameSceneManager : MonoSingleton<GameSceneManager>, IGameModule
{
    /// <summary>현재 씬의 진입점. 씬의 SceneBase가 Start에서 등록한다.</summary>
    public SceneBase Current { get; private set; }

    /// <summary>Load 호출부터 다음 씬의 SceneBase가 등록될 때까지 true</summary>
    public bool IsLoading { get; private set; }

    private object sceneData;

    /// <summary>곡 로딩 중이면 true (LoadWithSongLoading 호출부터 다음 씬 OnEnter까지). 이 동안 IsLoading도 true</summary>
    public bool IsSongLoading { get; private set; }

    /// <summary>마지막 곡 로딩의 단계별 시각 (확인용)</summary>
    public IReadOnlyList<SongLoadingStep> SongLoadingSteps => songLoadingSteps;

    private readonly List<SongLoadingStep> songLoadingSteps = new List<SongLoadingStep>();
    private double songLoadingStart;
    private SceneBase deferredScene;

    public void Init()
    {
        Current = null;
        IsLoading = false;
        IsSongLoading = false;
        deferredScene = null;
        sceneData = null;
    }

    /// <summary>
    /// 씬의 SceneBase가 자신을 현재 씬으로 등록한다. 등록되면 로딩이 끝난 것으로 본다.
    /// </summary>
    public void SetCurrent(SceneBase scene)
    {
        Current = scene;
        // 곡 로딩은 오버레이가 걷히고 OnEnter를 부를 때까지 로딩 중으로 둔다
        if (!IsSongLoading) IsLoading = false;
    }

    /// <summary>
    /// SceneBase가 Init 뒤 부른다. 곡 로딩 중이면 OnEnter를 미루고(오버레이가 걷힌 뒤 Enter) true, 아니면 false (바로 OnEnter).
    /// </summary>
    public bool DeferEnter(SceneBase scene)
    {
        if (!IsSongLoading || scene == null) return false;
        deferredScene = scene;
        MarkSongLoading($"{scene.gameObject.scene.name} Init 끝");
        return true;
    }

    /// <summary>
    /// 씬을 비동기로 불러온다. 현재 씬의 Exit()(OnExit + UI 해제)을 먼저 호출하고, data는 다음 씬에 넘긴다.
    /// </summary>
    /// <returns>로드를 시작했으면 true. 이미 로드 중이거나 씬이 없으면 false</returns>
    public bool Load(string sceneName, object data = null)
    {
        if (IsLoading)
        {
            Debug.LogWarning($"[GameSceneManager] 이미 씬을 불러오는 중이라 '{sceneName}' 로드를 무시합니다.");
            return false;
        }

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
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

    /// <summary>
    /// 곡 로딩 오버레이를 거쳐 씬을 불러온다 (곡 선택 시작 · 결과 다시 하기 → 인게임). 순서는 클래스 설명 참고.
    /// 오버레이(Canvas_SongLoading)가 없으면 일반 Load로 간다.
    /// </summary>
    /// <returns>로드를 시작했으면 true. 이미 로드 중이거나 씬이 없으면 false</returns>
    public bool LoadWithSongLoading(string sceneName, InGameEntryData entry)
    {
        if (IsLoading)
        {
            Debug.LogWarning($"[GameSceneManager] 이미 씬을 불러오는 중이라 '{sceneName}' 곡 로딩을 무시합니다.");
            return false;
        }

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[GameSceneManager] '{sceneName}' 씬이 없거나 Build Profiles에 등록되지 않았습니다.");
            return false;
        }

        Canvas_SongLoading overlay = UIManager.HasInstance ? GameManager.UI.SongLoading : null;
        if (overlay == null)
        {
            Debug.LogWarning("[GameSceneManager] 곡 로딩 오버레이(Canvas_SongLoading)가 없어 바로 불러옵니다.");
            return Load(sceneName, entry);
        }

        sceneData = entry;
        IsLoading = true;
        IsSongLoading = true;
        deferredScene = null;
        StartCoroutine(SongLoadingRoutine(sceneName, entry, overlay));
        return true;
    }

    private IEnumerator SongLoadingRoutine(string sceneName, InGameEntryData entry, Canvas_SongLoading overlay)
    {
        songLoadingSteps.Clear();
        songLoadingStart = Time.unscaledTimeAsDouble;
        MarkSongLoading("Enter (오버레이 열기 시작)");

        // 방향키·Enter가 뒤 화면(곡 선택·결과)의 선택을 옮기지 않게 EventSystem 전달을 잠시 끈다 (UI 맵은 그대로)
        EventSystem eventSystem = EventSystem.current;
        bool sentNavigation = eventSystem != null && eventSystem.sendNavigationEvents;
        if (eventSystem != null) eventSystem.sendNavigationEvents = false;

        overlay.Open(entry);
        while (overlay.OpenElapsed < GameConstants.SongLoading.CoverSeconds) yield return null;
        MarkSongLoading("덮기 완료");

        // 덮은 뒤에야 지금 씬을 떠난다 (덮기 전에 지우면 빈 화면이 한 프레임 보일 수 있다)
        if (Current != null)
        {
            string leaving = Current.gameObject.scene.name;
            Current.Exit();
            MarkSongLoading($"{leaving} OnExit");
        }
        Current = null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        MarkSongLoading($"{sceneName} 로드 시작");
        while (operation != null && !operation.isDone) yield return null;
        MarkSongLoading($"{sceneName} 로드 끝");

        // 다음 씬 Init 끝(DeferEnter)과 최소 표시 시간(열기 시작부터 1.5초, 열기 연출 1.1초 포함)을 둘 다 기다린다
        while (deferredScene == null || overlay.OpenElapsed < GameConstants.SongLoading.MinSeconds || overlay.IsOpening)
            yield return null;

        MarkSongLoading("닫기 시작");
        yield return overlay.CloseRoutine();
        MarkSongLoading("닫기 끝");

        if (eventSystem != null) eventSystem.sendNavigationEvents = sentNavigation;
        EventSystem next = EventSystem.current;
        if (next != null && next != eventSystem) next.sendNavigationEvents = true;

        SceneBase scene = deferredScene;
        deferredScene = null;
        IsSongLoading = false;
        IsLoading = false;
        scene.Enter();
        MarkSongLoading($"{sceneName} OnEnter");
        Debug.Log($"[GameSceneManager] 곡 로딩 끝: {DescribeSongLoading()}", this);
    }

    /// <summary>곡 로딩 단계를 지금 시각으로 남긴다</summary>
    public void MarkSongLoading(string step)
    {
        songLoadingSteps.Add(new SongLoadingStep(step, Time.unscaledTimeAsDouble - songLoadingStart));
    }

    /// <summary>곡 로딩 단계 시각을 한 줄로</summary>
    public string DescribeSongLoading()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < songLoadingSteps.Count; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append(songLoadingSteps[i].Name).Append(' ').Append(songLoadingSteps[i].Seconds.ToString("0.000")).Append('초');
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
