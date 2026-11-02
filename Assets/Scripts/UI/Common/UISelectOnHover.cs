using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스를 올리면 이 오브젝트를 EventSystem의 선택으로 만든다.
/// - 키보드 선택과 마우스 강조가 따로 보이지 않도록, 강조를 항상 하나로 맞춘다.
/// - Selectable(Button 등)이 있는 오브젝트에 붙인다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UISelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    /// <summary>
    /// 포인터가 들어오면 선택한다. 상호작용 불가이거나 EventSystem이 없으면 무시한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !selectable.IsInteractable()) return;

        if (eventSystem.currentSelectedGameObject != gameObject)
            eventSystem.SetSelectedGameObject(gameObject);
    }
}
