using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ◀ 값 ▶ 줄처럼 좌우 키로 값을 바꾸는 Selectable에 붙이는 공용 컴포넌트.
/// - EventSystem의 Navigate(←→)를 받아 Stepped(-1 / +1)를 알린다. ↑↓는 같은 오브젝트의 Selectable 기본 이동이 그대로 처리한다
///   (그 Selectable의 Navigation에서 selectOnLeft / selectOnRight를 비워 둬야 좌우로 선택이 옮겨 가지 않는다).
/// - 새 입력 처리를 만들지 않고 EventSystem(UI 맵) Navigate를 그대로 쓴다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UINavigateStepper : MonoBehaviour, IMoveHandler
{
    private Selectable selectable;

    /// <summary>←면 -1, →면 +1</summary>
    public event Action<int> Stepped;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnMove(AxisEventData eventData)
    {
        if (selectable != null && !selectable.IsInteractable()) return;

        if (eventData.moveDir == MoveDirection.Left) Stepped?.Invoke(-1);
        else if (eventData.moveDir == MoveDirection.Right) Stepped?.Invoke(1);
    }
}
