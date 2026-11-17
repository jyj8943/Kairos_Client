using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 선택된 버튼을 강조한다 (기획서 3-2 타이틀 메뉴). 여러 버튼에 붙이는 공용 컴포넌트.
/// - 선택: 하늘색 반투명 바탕 + 왼쪽 포인트색 막대 + 한글 흰색 Bold + 영문 포인트색
/// - 선택 안 됨: 바탕·막대 없이 글자만 (한글 흰색 약 70%, 영문 흰색 약 45%)
/// - EventSystem 선택(ISelectHandler·IDeselectHandler)을 따른다. 마우스를 올리면 선택(UISelectOnHover), 팝업이 닫히면 선택 복구(UIPopup·UISelectionKeeper)가 그대로 강조로 이어진다.
/// - 바탕은 버튼 자신의 Image 색으로 칠한다 (바탕을 숨길 때도 알파만 0으로 두어 클릭은 그대로 받는다). Button의 Color Tint는 그 위에 곱해진다.
/// - 색·불투명도는 GameConstants.SelectHighlight에 둔다. 처음 상태는 화면 Init()에서 Sync()로 맞춘다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UISelectHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    /// <summary>
    /// 선택 여부에 따른 모양 (순수 값). 바탕·막대 표시 여부와 색, 한글 굵기.
    /// </summary>
    public readonly struct Look
    {
        public readonly bool ShowBackground;
        public readonly bool ShowBar;
        public readonly Color BackgroundColor;
        public readonly Color MainTextColor;
        public readonly Color SubTextColor;
        public readonly FontStyles MainFontStyle;

        public Look(bool showBackground, bool showBar, Color backgroundColor, Color mainTextColor, Color subTextColor, FontStyles mainFontStyle)
        {
            ShowBackground = showBackground;
            ShowBar = showBar;
            BackgroundColor = backgroundColor;
            MainTextColor = mainTextColor;
            SubTextColor = subTextColor;
            MainFontStyle = mainFontStyle;
        }
    }

    [Tooltip("바탕 Graphic (보통 버튼 자신의 Image)")]
    [SerializeField] private Graphic background;

    [Tooltip("왼쪽 포인트색 막대 (Img_SelectBar). 선택될 때만 켠다")]
    [SerializeField] private Graphic accentBar;

    [Tooltip("한글 이름 (예: Txt_Play)")]
    [SerializeField] private TMP_Text mainLabel;

    [Tooltip("영문 작은 글씨 (예: Txt_PlayEn). 없으면 비워 둔다")]
    [SerializeField] private TMP_Text subLabel;

    /// <summary>지금 강조 상태 (확인용)</summary>
    public bool IsHighlighted { get; private set; }

    /// <summary>
    /// 선택 여부에 따른 모양을 계산한다 (GameConstants.SelectHighlight 값).
    /// </summary>
    public static Look GetLook(bool selected)
    {
        Color accent = GameConstants.SelectHighlight.AccentColor;
        if (selected)
        {
            Color background = accent;
            background.a = GameConstants.SelectHighlight.SelectedBackgroundAlpha;
            return new Look(true, true, background, Color.white, accent, FontStyles.Bold);
        }

        Color hidden = accent;
        hidden.a = 0f;
        return new Look(false, false, hidden,
            new Color(1f, 1f, 1f, GameConstants.SelectHighlight.NormalMainTextAlpha),
            new Color(1f, 1f, 1f, GameConstants.SelectHighlight.NormalSubTextAlpha),
            FontStyles.Normal);
    }

    /// <summary>
    /// 강조 상태를 정하고 바탕·막대·글자에 바로 적용한다.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        Look look = GetLook(highlighted);

        if (background != null) background.color = look.BackgroundColor;
        if (accentBar != null) accentBar.gameObject.SetActive(look.ShowBar);
        if (mainLabel != null)
        {
            mainLabel.color = look.MainTextColor;
            mainLabel.fontStyle = look.MainFontStyle;
        }
        if (subLabel != null) subLabel.color = look.SubTextColor;
    }

    /// <summary>
    /// EventSystem의 지금 선택에 맞춘다 (이 오브젝트가 선택돼 있으면 강조). 화면 Init()에서 부른다.
    /// </summary>
    public void Sync()
    {
        EventSystem eventSystem = EventSystem.current;
        SetHighlighted(eventSystem != null && eventSystem.currentSelectedGameObject == gameObject);
    }

    /// <summary>EventSystem이 이 버튼을 선택했다.</summary>
    public void OnSelect(BaseEventData eventData)
    {
        SetHighlighted(true);
    }

    /// <summary>EventSystem 선택이 다른 곳으로 옮겨 갔다.</summary>
    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlighted(false);
    }
}
