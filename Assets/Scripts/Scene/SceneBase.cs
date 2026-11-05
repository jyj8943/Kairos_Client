using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 진입점 베이스. 씬마다 루트에 하나 두고, 씬 안 오브젝트의 Init()을 정해진 순서로 호출한다.
/// - 진행 순서: 전역 매니저 초기화 대기 → GameSceneManager에 등록 → 씬 Canvas를 UIManager에 등록 → Init() → OnEnter()
/// - 떠날 때: GameSceneManager가 Exit()을 부른다 → OnExit() → 씬 Canvas 해제·열린 팝업 닫기
/// - ESC는 UIManager가 받고, 팝업이 없으면 현재 씬의 OnBack()을 부른다.
/// - 싱글톤이 아니다. 현재 씬은 GameManager.Scene.Current로 접근한다.
/// </summary>
public abstract class SceneBase : MonoBehaviour
{
    /// <summary>이 씬에서 UIManager에 등록한 루트 Canvas</summary>
    private readonly List<Canvas> registeredCanvases = new List<Canvas>();

    private IEnumerator Start()
    {
        WarnIfDuplicated();

        // 안전장치: Bootstrapper가 BeforeSceneLoad에서 끝내지만, 혹시 늦어지면 기다린다
        while (!GameManager.IsInitialized)
            yield return null;

        GameManager.Scene.SetCurrent(this);
        RegisterCanvases();

        yield return Init();

        OnEnter();
    }

    /// <summary>
    /// 씬별 초기화. 씬 안 오브젝트의 Init()을 순서대로 호출한다.
    /// 채보·음원 로딩처럼 기다려야 하는 작업도 넣을 수 있도록 코루틴으로 둔다.
    /// </summary>
    protected abstract IEnumerator Init();

    /// <summary>
    /// Init()이 모두 끝난 뒤 호출된다. BGM 재생, 입력 허용처럼 초기화 후에 시작할 것을 둔다.
    /// </summary>
    protected virtual void OnEnter() { }

    /// <summary>
    /// 씬을 떠나기 직전 호출된다 (Exit 안에서). 이벤트 구독 해제, 사운드 정지 등을 둔다.
    /// </summary>
    public virtual void OnExit() { }

    /// <summary>
    /// 열린 팝업이 없을 때 ESC가 오면 UIManager가 부른다. 씬마다 뒤로가기 동작을 쓴다 (기본은 아무것도 하지 않음).
    /// </summary>
    public virtual void OnBack() { }

    /// <summary>
    /// 씬을 떠날 때 GameSceneManager가 부른다: OnExit() 뒤에 씬 Canvas를 해제하고 열린 팝업을 닫는다.
    /// </summary>
    public void Exit()
    {
        OnExit();

        if (UIManager.HasInstance) GameManager.UI.OnSceneExit(registeredCanvases);
        registeredCanvases.Clear();
    }

    /// <summary>
    /// 이 씬의 루트 Canvas(다른 Canvas 아래에 있지 않은 것)를 UIManager에 Scene 층으로 등록한다.
    /// </summary>
    private void RegisterCanvases()
    {
        registeredCanvases.Clear();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas && canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>(true) != null)
                    continue;

                GameManager.UI.Register(canvas, UILayer.Scene);
                registeredCanvases.Add(canvas);
            }
        }
    }

    /// <summary>
    /// 같은 씬에 SceneBase가 두 개 이상이면 경고한다.
    /// </summary>
    private void WarnIfDuplicated()
    {
        int count = 0;
        foreach (SceneBase scene in FindObjectsByType<SceneBase>(FindObjectsSortMode.None))
        {
            if (scene.gameObject.scene == gameObject.scene) count++;
        }

        if (count > 1)
            Debug.LogWarning($"[SceneBase] {gameObject.scene.name} 씬에 SceneBase가 {count}개 있습니다. 씬마다 하나만 둡니다.", this);
    }
}
