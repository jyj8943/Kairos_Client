using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Enter(Submit) 때 Pressed 색을 잠깐 확실히 보여준다.
/// - 기본 Button은 Submit 때 Pressed로 페이드하자마자 되돌아가서 진한 색이 거의 보이지 않는다.
/// - 색은 Selectable의 ColorBlock 값을 그대로 쓴다. Color Tint 전환일 때만 동작한다.
/// - 마우스 클릭은 Selectable 기본 Pressed 동작을 그대로 쓰므로 여기서 처리하지 않는다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIPressFeedback : MonoBehaviour, ISubmitHandler
{
    [Tooltip("Enter 때 Pressed 색을 유지하는 시간(초)")]
    [SerializeField] private float holdDuration = 0.12f;

    private Selectable selectable;
    private float holdRemaining;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    private void OnDisable()
    {
        holdRemaining = 0f;
    }

    /// <summary>
    /// Enter를 받으면 Pressed 색을 바로 적용하고 유지 시간을 잰다.
    /// </summary>
    public void OnSubmit(BaseEventData eventData)
    {
        if (!selectable.IsInteractable()) return;
        if (selectable.transition != Selectable.Transition.ColorTint || selectable.targetGraphic == null) return;

        holdRemaining = holdDuration;
        ApplyColor(selectable.colors.pressedColor, 0f);
    }

    /// <summary>
    /// 유지 시간 동안 Pressed 색을 덮어쓰고, 끝나면 현재 상태 색으로 페이드한다.
    /// Button이 Submit 뒤 스스로 되돌리는 전환보다 늦게 적용되도록 LateUpdate에서 처리한다.
    /// </summary>
    private void LateUpdate()
    {
        if (holdRemaining <= 0f) return;

        holdRemaining -= Time.unscaledDeltaTime;
        if (holdRemaining > 0f)
        {
            ApplyColor(selectable.colors.pressedColor, 0f);
            return;
        }

        ApplyColor(GetCurrentStateColor(), selectable.colors.fadeDuration);
    }

    /// <summary>
    /// 지금 상태에 맞는 ColorBlock 색을 고른다. (비활성 → 선택됨 → 기본 순)
    /// </summary>
    private Color GetCurrentStateColor()
    {
        ColorBlock colors = selectable.colors;
        if (!selectable.IsInteractable()) return colors.disabledColor;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
            return colors.selectedColor;

        return colors.normalColor;
    }

    private void ApplyColor(Color color, float duration)
    {
        selectable.targetGraphic.CrossFadeColor(color * selectable.colors.colorMultiplier, duration, true, true);
    }
}
