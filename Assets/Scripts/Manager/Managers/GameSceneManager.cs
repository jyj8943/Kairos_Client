using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환과 현재 씬(SceneBase) 관리.
/// - 씬 간 데이터는 Load(sceneName, data)로 넘기고, 다음 씬 Init에서 TakeSceneData&lt;T&gt;()로 받는다.
/// </summary>
public class GameSceneManager : MonoSingleton<GameSceneManager>, IGameModule
{
    /// <summary>현재 씬의 진입점. 씬의 SceneBase가 Start에서 등록한다.</summary>
    public SceneBase Current { get; private set; }

    /// <summary>Load 호출부터 다음 씬의 SceneBase가 등록될 때까지 true</summary>
    public bool IsLoading { get; private set; }

    private object sceneData;

    public void Init()
    {
        Current = null;
        IsLoading = false;
        sceneData = null;
    }

    /// <summary>
    /// 씬의 SceneBase가 자신을 현재 씬으로 등록한다. 등록되면 로딩이 끝난 것으로 본다.
    /// </summary>
    public void SetCurrent(SceneBase scene)
    {
        Current = scene;
        IsLoading = false;
    }

    /// <summary>
    /// 씬을 비동기로 불러온다. 현재 씬의 OnExit()을 먼저 호출하고, data는 다음 씬에 넘긴다.
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

        if (Current != null) Current.OnExit();
        Current = null;

        sceneData = data;
        IsLoading = true;
        SceneManager.LoadSceneAsync(sceneName);
        return true;
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
