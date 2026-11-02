using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 선택이 비지 않도록 유지한다.
/// - 빈 곳 클릭 등으로 선택이 풀리면 마지막 선택을 다시 선택해, 바로 키보드로 이어서 조작할 수 있게 한다.
/// - 씬(메뉴 Canvas)마다 하나 둔다.
/// </summary>
public class UISelectionKeeper : MonoBehaviour
{
    [Tooltip("마지막 선택이 없거나 비활성일 때 선택할 기본 Selectable")]
    [SerializeField] private Selectable defaultSelectable;

    private GameObject lastSelected;

    /// <summary>
    /// 기본 Selectable을 바꾼다. 목록을 동적으로 만드는 화면에서 첫 항목을 지정할 때 쓴다.
    /// </summary>
    public void SetDefaultSelectable(Selectable selectable)
    {
        defaultSelectable = selectable;
    }

    /// <summary>
    /// 매 프레임 현재 선택을 기억하고, 선택이 비어 있으면 복구한다.
    /// </summary>
    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current != null)
        {
            lastSelected = current;
            return;
        }

        RestoreSelection(eventSystem);
    }

    /// <summary>
    /// 마지막 선택이 활성 상태면 다시 선택하고, 아니면 기본 Selectable을 선택한다.
    /// </summary>
    private void RestoreSelection(EventSystem eventSystem)
    {
        if (lastSelected != null && lastSelected.activeInHierarchy)
        {
            eventSystem.SetSelectedGameObject(lastSelected);
            return;
        }

        if (defaultSelectable != null && defaultSelectable.gameObject.activeInHierarchy)
            eventSystem.SetSelectedGameObject(defaultSelectable.gameObject);
    }
}
