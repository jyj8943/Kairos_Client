using UnityEngine;

/// <summary>
/// MonoBehaviour 싱글톤 베이스.
/// - 전역 매니저는 Managers_DontDestroy 프리팹 안에 두고 Bootstrapper가 생성한다.
/// - 씬 전용 싱글톤은 isDontDestroy를 끄고 씬에 배치한다.
/// </summary>
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    [SerializeField] private bool isDontDestroy = false;

    private static T instance;
    private static bool isQuitting;

    /// <summary>
    /// 인스턴스를 새로 만들지 않고 존재 여부만 확인한다.
    /// 씬 정리(OnDisable/OnDestroy) 중 구독 해제할 때는 Instance 대신 이걸 쓴다.
    /// </summary>
    public static bool HasInstance => instance != null;

    public static T Instance
    {
        get
        {
            if (instance != null) return instance;

            // 게임 종료 중이거나 에디터(플레이 중 아님)에서는 새로 만들지 않는다
            if (isQuitting || !Application.isPlaying) return null;

            instance = FindFirstObjectByType<T>();

            if (instance == null)
            {
                Debug.LogWarning($"[MonoSingleton] {typeof(T).Name}이(가) 없어 새로 생성합니다. " +
                                 "인스펙터 값이 비어 있을 수 있으니 Managers_DontDestroy 프리팹에 추가하세요.");
                instance = new GameObject(typeof(T).Name).AddComponent<T>();
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        // instance == this: Instance에서 FindFirstObjectByType으로 먼저 잡힌 경우
        if (instance == null || instance == this)
        {
            instance = (T)this;
            isQuitting = false; // 도메인 리로드를 끈 상태로 플레이를 다시 시작할 때 초기화

            if (isDontDestroy) DontDestroyOnLoad(transform.root.gameObject);
            return;
        }

        // 이미 인스턴스가 있으면 중복 정리 (기존 MonoSingleton 로직 유지)
        Transform root = transform.root;

        bool cleanUpRoot = isDontDestroy
                           && root != transform
                           && root != instance.transform.root;

        if (cleanUpRoot) transform.SetParent(null);

        Destroy(gameObject);

        if (cleanUpRoot && root.childCount == 0)
            Destroy(root.gameObject);
    }

    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}