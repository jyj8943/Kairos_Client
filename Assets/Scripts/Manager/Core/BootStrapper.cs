using UnityEngine;

/// <summary>
/// 어느 씬에서 플레이를 시작해도 첫 씬이 로드되기 전에
/// 전역 매니저 프리팹(Managers_DontDestroy)을 만들고 매니저를 정해진 순서로 초기화한다.
/// </summary>
public static class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameManager.IsInitialized = false;

        GameObject prefab = Resources.Load<GameObject>(GameConstants.ResourcePaths.ManagersPrefab);

        if (prefab == null)
        {
            Debug.LogError($"[Bootstrapper] Resources/{GameConstants.ResourcePaths.ManagersPrefab} 프리팹이 없습니다. " +
                           "매니저가 자동 생성되며 인스펙터 값이 비어 있을 수 있습니다.");
        }
        else
        {
            GameObject root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);
        }

        InitManagers();
    }

    private static void InitManagers()
    {
        Init(GameManager.Settings); // 설정 값 (다른 매니저·씬이 읽기 전에)
        Init(GameManager.Songs);  // 곡 목록 검사 (씬이 곡을 찾기 전에)
        Init(GameManager.Sound);  // 노래·효과음 준비
        Init(GameManager.UI);     // 오버레이 Canvas·공용 팝업·ESC (씬이 Canvas를 등록하기 전에)
        Init(GameManager.Scene);  // 씬 전환 준비 (마지막)

        GameManager.IsInitialized = true;
        Debug.Log("[Bootstrapper] 전역 매니저 초기화 완료");
    }

    private static void Init(IGameModule module)
    {
        module?.Init();
    }
}