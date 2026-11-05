using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 UI 화면 스크립트(Canvas_, Popup_)의 기반 클래스.
/// - 초기화는 각 화면의 Init()에서 한다 (Init 인자는 화면마다 달라 여기서 정하지 않는다). Open/Close를 초기화 수단으로 쓰지 않는다.
/// - Open()/Close(): 열고 닫는다. CanvasGroup이 있으면 페이드, panel이 있으면 0.9 → 1.0 크기 연출을 짧게(unscaled 시간) 한다.
///   CanvasGroup이 없으면 오브젝트를 켜고 끄기만 한다.
/// - Awake를 두지 않는다 (자식 화면 스크립트가 자기 Awake를 그대로 쓸 수 있게). 컴포넌트는 처음 쓸 때 가져온다.
/// </summary>
public abstract class UIBase : MonoBehaviour
{
    [Header("열고 닫기 연출 (선택)")]
    [Tooltip("페이드할 CanvasGroup. 비어 있으면 같은 오브젝트에서 찾고, 없으면 켜고 끄기만 한다")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("크기 연출을 할 패널 (선택)")]
    [SerializeField] private RectTransform panel;

    private Coroutine transition;
    private bool isComponentCached;
    private bool isOpen;

    /// <summary>이 화면의 층. 씬 화면은 Scene, 팝업은 Popup</summary>
    public virtual UILayer Layer => UILayer.Scene;

    /// <summary>열려 있으면 true. 씬 화면은 오브젝트가 켜져 있으면 열린 것으로 본다</summary>
    public bool IsOpen => isOpen || (Layer == UILayer.Scene && gameObject.activeInHierarchy);

    /// <summary>
    /// 연다. 이미 열려 있으면 아무것도 하지 않는다.
    /// </summary>
    public virtual void Open()
    {
        if (isOpen) return;

        isOpen = true;
        CacheComponents();
        gameObject.SetActive(true);
        StartTransition(true);
        OnOpened();
    }

    /// <summary>
    /// 닫는다. 상태는 바로 닫힘이 되고, 연출이 끝나면 오브젝트를 끈다.
    /// </summary>
    public virtual void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        CacheComponents();
        StartTransition(false);
        OnClosed();
    }

    /// <summary>열린 직후 (연출 시작 시점) 할 일</summary>
    protected virtual void OnOpened() { }

    /// <summary>닫힌 직후 (연출 시작 시점) 할 일</summary>
    protected virtual void OnClosed() { }

    /// <summary>
    /// 연출 없이 바로 닫힌 상태로 둔다 (처음 상태 맞추기, 씬 정리용). OnClosed는 부르지 않는다.
    /// </summary>
    public void HideImmediate()
    {
        isOpen = false;
        CacheComponents();
        if (transition != null) StopCoroutine(transition);
        transition = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void CacheComponents()
    {
        if (isComponentCached) return;

        isComponentCached = true;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void StartTransition(bool opening)
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;

        if (canvasGroup == null || !gameObject.activeInHierarchy)
        {
            if (!opening) gameObject.SetActive(false);
            return;
        }

        transition = StartCoroutine(Transition(opening));
    }

    /// <summary>
    /// 페이드와 크기 연출. 닫을 때는 시작하자마자 입력을 막고, 끝나면 오브젝트를 끈다.
    /// </summary>
    private IEnumerator Transition(bool opening)
    {
        float duration = GameConstants.UI.TransitionSeconds;
        float startScale = GameConstants.UI.TransitionStartScale;

        canvasGroup.interactable = opening;
        canvasGroup.blocksRaycasts = true;

        float from = opening ? 0f : canvasGroup.alpha;
        float to = opening ? 1f : 0f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            if (panel != null)
            {
                float scale = opening ? Mathf.Lerp(startScale, 1f, t) : Mathf.Lerp(1f, startScale, t);
                panel.localScale = new Vector3(scale, scale, 1f);
            }
            yield return null;
        }

        canvasGroup.alpha = to;
        if (panel != null) panel.localScale = Vector3.one;
        transition = null;

        if (!opening)
        {
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}
