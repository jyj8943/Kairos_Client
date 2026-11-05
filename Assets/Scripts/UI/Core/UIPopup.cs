using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 팝업(Popup_)의 기반 클래스. UIBase를 상속한다.
/// - 열면 UIManager 팝업 스택 맨 위에 올라가고, 같은 Canvas 안에서 맨 앞에 그린다 (SetAsLastSibling).
/// - 뒤 화면을 덮는 어두운 배경(클릭을 막는 Image)은 팝업 프리팹 안에 둔다 (dimBackground).
/// - 키보드 선택: 열 때 EventSystem의 지금 선택을 기억하고 firstSelected로 옮긴다. 닫으면 기억한 선택으로 되돌린다
///   (그 오브젝트가 사라졌거나 꺼졌으면 그대로 둔다).
/// - ESC는 UIManager가 받아 맨 위 팝업의 OnCancelRequested()를 부른다. 기본은 Close()이고 팝업마다 바꿀 수 있다.
/// - 바깥(어두운 배경) 클릭: closeOnOutsideClick이 켜져 있으면 OnCancelRequested(), 아니면 무시한다.
///   배경 Image의 클릭이 부모인 이 오브젝트까지 올라오므로 여기서 받는다.
/// </summary>
public abstract class UIPopup : UIBase, IPointerClickHandler
{
    [Header("팝업")]
    [Tooltip("바깥(어두운 배경)을 클릭하면 OnCancelRequested()를 부른다")]
    [SerializeField] private bool closeOnOutsideClick;
    [Tooltip("열 때 처음 선택할 Selectable")]
    [SerializeField] private Selectable firstSelected;
    [Tooltip("뒤 화면을 덮는 어두운 배경 (Img_Dim). 바깥 클릭 판정에 쓴다")]
    [SerializeField] private GameObject dimBackground;

    private GameObject previousSelected;

    public override UILayer Layer => UILayer.Popup;

    /// <summary>
    /// 열고 팝업 스택에 올린다. 지금 선택을 기억하고 firstSelected를 선택한다.
    /// </summary>
    public override void Open()
    {
        if (IsOpen) return;

        EventSystem eventSystem = EventSystem.current;
        previousSelected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        transform.SetAsLastSibling();
        base.Open();
        GameManager.UI.PushPopup(this);

        if (eventSystem != null && firstSelected != null)
            eventSystem.SetSelectedGameObject(firstSelected.gameObject);
    }

    /// <summary>
    /// 닫고 팝업 스택에서 뺀다. 기억한 선택이 남아 있으면 되돌린다.
    /// </summary>
    public override void Close()
    {
        if (!IsOpen) return;

        base.Close();
        if (UIManager.HasInstance) GameManager.UI.RemovePopup(this);

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && previousSelected != null && previousSelected.activeInHierarchy)
            eventSystem.SetSelectedGameObject(previousSelected);
        previousSelected = null;
    }

    /// <summary>
    /// ESC가 왔을 때 할 일. 기본은 닫기. 팝업마다 바꾼다 (Popup_Confirm: 취소, Popup_Pause: 계속).
    /// </summary>
    public virtual void OnCancelRequested()
    {
        Close();
    }

    /// <summary>
    /// 어두운 배경을 클릭했을 때만 반응한다 (패널 안 클릭은 무시).
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!closeOnOutsideClick || !IsOpen || dimBackground == null) return;

        if (eventData.pointerCurrentRaycast.gameObject == dimBackground)
            OnCancelRequested();
    }
}
