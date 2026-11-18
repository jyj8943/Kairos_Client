using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>팝업 선택 표시 방식 (기획서 3-7 「팝업 공통 톤」 선택 표시)</summary>
public enum PopupSelectMode
{
    /// <summary>버튼: 바깥에 흰 링 3 + 포인트색 링 3 (위험 버튼은 빨강 링)</summary>
    Ring,

    /// <summary>메뉴·항목 줄: 왼쪽 막대 4 + 포인트색 18% 바탕 (위험 항목은 빨강 막대·글자 + 빨강 16% 바탕)</summary>
    Row
}

/// <summary>
/// 팝업 안 선택 표시 (기획서 3-7 「팝업 공통 톤 (11/18)」). 설정 창·확인 팝업·일시정지의 버튼·메뉴·항목 줄에 붙이는 공용 컴포넌트.
/// - EventSystem 선택(ISelectHandler·IDeselectHandler)을 따른다. 마우스를 올리면 선택(UISelectOnHover), 팝업이 닫히면 선택 복구(UIPopup)가 그대로 표시로 이어진다.
/// - Ring: 버튼 바깥의 두 링(Img_FocusRing 포인트색 · 그 안 Img_FocusRingInner 흰색)을 선택될 때만 켠다. 버튼 바탕색은 건드리지 않는다 (UIThemePaint.PopupButton이 칠한다).
/// - Row: 바탕(보통 버튼 자신의 Image)을 기본 버튼 톤(흰색 75%) ↔ 선택 바탕으로 칠하고, 왼쪽 막대(Img_SelectBar)를 선택될 때만 켠다.
///   글자: 한글 기본 글자 · 영문 흐린 글자, 선택되면 영문은 포인트색. 위험 항목(SetDanger)은 선택될 때 막대·한글·영문이 빨강, 바탕 빨강 16%.
/// - 색은 모두 UITheme에서 읽는다. 처음 상태는 화면 Init에서 Sync()로 맞춘다.
/// - UISelectHighlight(타이틀 메뉴, 어두운 배경용)와 따로 둔다: 팝업은 밝은 유리 패널 위라 색 규칙이 다르다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIPopupSelect : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    /// <summary>줄 모양 (순수 값)</summary>
    public readonly struct RowLook
    {
        public readonly Color Background;
        public readonly bool ShowBar;
        public readonly Color Bar;
        public readonly Color MainText;
        public readonly Color SubText;

        public RowLook(Color background, bool showBar, Color bar, Color mainText, Color subText)
        {
            Background = background;
            ShowBar = showBar;
            Bar = bar;
            MainText = mainText;
            SubText = subText;
        }
    }

    [SerializeField] private PopupSelectMode mode = PopupSelectMode.Ring;

    [Header("Ring (버튼)")]
    [Tooltip("바깥 링 (Img_FocusRing, 포인트색 · 위험이면 빨강)")]
    [SerializeField] private Graphic ringOuter;
    [Tooltip("안쪽 링 (Img_FocusRing/Img_FocusRingInner, 흰색)")]
    [SerializeField] private Graphic ringInner;

    [Header("Row (메뉴 · 항목 줄)")]
    [Tooltip("줄 바탕 (보통 버튼 자신의 Image)")]
    [SerializeField] private Graphic background;
    [Tooltip("왼쪽 막대 (Img_SelectBar). 선택될 때만 켠다")]
    [SerializeField] private Graphic bar;
    [Tooltip("한글 이름 (선택 안 됨: 기본 글자 / 위험 선택: 빨강). 비워 두면 칠하지 않는다")]
    [SerializeField] private TMP_Text mainLabel;
    [Tooltip("영문 작은 글씨 (선택 안 됨: 흐린 글자 / 선택: 포인트색 · 위험이면 빨강). 비워 두면 칠하지 않는다")]
    [SerializeField] private TMP_Text subLabel;

    private bool isDanger;

    /// <summary>지금 선택 표시 상태 (확인용)</summary>
    public bool IsHighlighted { get; private set; }

    /// <summary>위험 표시 (확인용)</summary>
    public bool IsDanger => isDanger;

    /// <summary>표시 방식 (확인용)</summary>
    public PopupSelectMode Mode => mode;

    /// <summary>바깥 링 (확인용)</summary>
    public Graphic RingOuter => ringOuter;

    /// <summary>왼쪽 막대 (확인용)</summary>
    public Graphic Bar => bar;

    /// <summary>줄 바탕 (확인용)</summary>
    public Graphic Background => background;

    /// <summary>
    /// 줄 모양을 계산한다 (순수 함수).
    /// </summary>
    public static RowLook GetRowLook(UITheme theme, bool selected, bool danger)
    {
        if (!selected) return new RowLook(theme.popupButton, false, theme.accent, theme.textPrimary, theme.textMuted);

        Color point = theme.AccentOrDanger(danger);
        Color back = danger ? theme.DangerSelectedBackground : theme.SelectedBackground;
        return new RowLook(back, true, point, danger ? theme.danger : theme.textPrimary, point);
    }

    /// <summary>위험 표시를 바꾸고 지금 선택 상태로 다시 칠한다</summary>
    public void SetDanger(bool danger)
    {
        isDanger = danger;
        SetHighlighted(IsHighlighted);
    }

    /// <summary>선택 표시 상태를 정하고 바로 칠한다</summary>
    public void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        UITheme theme = UITheme.Current;

        if (mode == PopupSelectMode.Ring)
        {
            if (ringOuter != null)
            {
                ringOuter.color = theme.AccentOrDanger(isDanger);
                ringOuter.gameObject.SetActive(highlighted);
            }
            if (ringInner != null) ringInner.color = theme.popupRingInner;
            return;
        }

        RowLook look = GetRowLook(theme, highlighted, isDanger);
        if (background != null) background.color = look.Background;
        if (bar != null)
        {
            bar.color = look.Bar;
            bar.gameObject.SetActive(look.ShowBar);
        }
        if (mainLabel != null) mainLabel.color = look.MainText;
        if (subLabel != null) subLabel.color = look.SubText;
    }

    /// <summary>EventSystem의 지금 선택에 맞춘다 (이 오브젝트가 선택돼 있으면 표시). 화면 Init·열 때 부른다</summary>
    public void Sync()
    {
        EventSystem eventSystem = EventSystem.current;
        SetHighlighted(eventSystem != null && eventSystem.currentSelectedGameObject == gameObject);
    }

    /// <summary>EventSystem이 이 오브젝트를 선택했다</summary>
    public void OnSelect(BaseEventData eventData)
    {
        SetHighlighted(true);
    }

    /// <summary>EventSystem 선택이 다른 곳으로 옮겨 갔다</summary>
    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlighted(false);
    }
}
