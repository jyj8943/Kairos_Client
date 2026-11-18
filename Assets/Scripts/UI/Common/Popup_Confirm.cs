using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 어디서든 쓰는 공용 확인 팝업 (기획서 3-7 ②). UIManager 오버레이 Canvas 아래에 두고, UIManager가 Init()을 호출한다.
/// - 호출하는 쪽은 GameManager.UI.Confirm(문구, 확인 버튼 이름, 확인 콜백, 취소 콜백, 위험 표시) 한 줄만 쓴다.
/// - 모양 (11/18 팝업 공통 톤): 폭 560, 위에서부터 "!" 원(44) → 문구(21 굵게, 1~3줄) → 보조 문구(15, 흐린 글자, 없으면 숨김) → 취소 · 확인 버튼 → 키 안내 줄.
///   패널(Img_Panel)은 VerticalLayoutGroup + ContentSizeFitter라 높이가 문구 줄 수에 맞춰 늘어난다.
/// - 위험 표시(isDanger, 나가기·삭제): 확인 버튼 = 위험 빨강 채움, 선택 링 = 빨강, "!" 원 = 빨강. 아니면 모두 포인트색(하늘색).
/// - 문구에 GameConstants.Confirm.SubSeparator가 있으면 그 뒤를 보조 문구로 쓴다.
/// - 처음 선택은 Btn_Cancel (Enter를 연타해도 실수로 진행되지 않게). ←→로 두 버튼을 오가고, Enter는 선택한 버튼, ESC는 취소.
/// - 바깥 클릭은 무시한다 (closeOnOutsideClick 꺼 둠).
/// - 버튼을 누르면 먼저 팝업을 닫고 콜백을 한 번만 부른다 (연타로 두 번 불리지 않는다).
/// - 이미 열려 있을 때 새 요청이 오면 무시하고 경고 로그를 남긴다.
/// </summary>
public class Popup_Confirm : UIPopup
{
    [SerializeField] private TMP_Text messageText;
    [Tooltip("보조 문구 (Txt_SubMessage, 15 흐린 글자). 없으면 숨긴다")]
    [SerializeField] private TMP_Text subMessageText;
    [Tooltip("\"!\" 원 (Img_Icon, 포인트색 18% · 위험이면 빨강 16%)")]
    [SerializeField] private Image iconImage;
    [Tooltip("\"!\" 글자 (Img_Icon/Txt_Icon, 포인트색 · 위험이면 빨강)")]
    [SerializeField] private TMP_Text iconText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelText;
    [Tooltip("취소 버튼 테두리 (Btn_Cancel/Img_Border, 구분선 색)")]
    [SerializeField] private Image cancelBorder;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmText;
    [Tooltip("확인 버튼 테두리 (Btn_Confirm/Img_Border, 채움 버튼이라 숨김)")]
    [SerializeField] private Image confirmBorder;
    [Tooltip("높이를 문구에 맞출 패널 (Img_Panel)")]
    [SerializeField] private RectTransform panelRect;
    [Tooltip("아래 키 안내 줄 (←→ 선택 · Enter 결정 · ESC 취소)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private Action onConfirm;
    private Action onCancel;
    private bool isAnswered;
    private bool isDanger;

    /// <summary>지금 위험 표시 (확인용)</summary>
    public bool IsDanger => isDanger;

    /// <summary>"!" 원 (확인용)</summary>
    public Image IconImage => iconImage;

    /// <summary>"!" 글자 (확인용)</summary>
    public TMP_Text IconText => iconText;

    /// <summary>확인 버튼 (확인용)</summary>
    public Button ConfirmButton => confirmButton;

    /// <summary>취소 버튼 (확인용)</summary>
    public Button CancelButton => cancelButton;

    /// <summary>문구 (확인용)</summary>
    public TMP_Text MessageText => messageText;

    /// <summary>보조 문구 (확인용)</summary>
    public TMP_Text SubMessageText => subMessageText;

    /// <summary>패널 (확인용)</summary>
    public RectTransform Panel => panelRect;

    /// <summary>
    /// 버튼 리스너와 ←→ 이동을 연결하고 닫힌 상태로 둔다.
    /// </summary>
    public void Init()
    {
        InitPopupTone();
        if (iconText != null) iconText.text = GameConstants.Confirm.IconText;
        if (keyGuideBar != null)
        {
            keyGuideBar.Init(new List<KeyGuideEntry>
            {
                new KeyGuideEntry(GameConstants.KeyGuide.ConfirmSelect, KeyGuideKey.Fixed(GameConstants.KeyGuide.LeftRightKeys)),
                new KeyGuideEntry(GameConstants.KeyGuide.ConfirmDecide, KeyGuideKey.Fixed(GameConstants.KeyGuide.EnterKey)),
                new KeyGuideEntry(GameConstants.KeyGuide.ConfirmCancel, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
            });
        }
        ApplyDanger(false);

        cancelButton.onClick.RemoveListener(OnClickCancel);
        cancelButton.onClick.AddListener(OnClickCancel);
        confirmButton.onClick.RemoveListener(OnClickConfirm);
        confirmButton.onClick.AddListener(OnClickConfirm);

        cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = confirmButton };
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancelButton };

        onConfirm = null;
        onCancel = null;
        isAnswered = true;
        HideImmediate();
    }

    /// <summary>
    /// 문구와 버튼 이름을 채우고 연다.
    /// </summary>
    /// <param name="isDanger">true면 확인 버튼·선택 링·"!" 원을 위험 빨강으로 (나가기·삭제)</param>
    /// <returns>열었으면 true. 이미 열려 있으면 false (새 요청은 무시)</returns>
    public bool Show(string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel, bool isDanger = false)
    {
        if (IsOpen)
        {
            Debug.LogWarning($"[Popup_Confirm] 이미 열려 있어 새 요청을 무시합니다: \"{message}\"", this);
            return false;
        }

        SplitMessage(message, out string body, out string sub);
        messageText.text = body;
        if (subMessageText != null)
        {
            subMessageText.text = sub;
            subMessageText.gameObject.SetActive(sub.Length > 0);
        }
        confirmText.text = confirmLabel;
        cancelText.text = cancelLabel;
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        isAnswered = false;
        ApplyDanger(isDanger);

        Open();
        // 문구 줄 수에 맞춰 패널 높이를 바로 맞춘다 (ContentSizeFitter)
        if (panelRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Debug.Log($"[Popup_Confirm] 열림: \"{message.Replace("\n", " ")}\" (확인 버튼: {confirmLabel}{(isDanger ? ", 위험" : "")})", this);
        return true;
    }

    /// <summary>
    /// 문구를 본문과 보조 문구로 나눈다 (순수 함수). SubSeparator가 없으면 전부 본문, 보조 문구는 빈 문자열.
    /// </summary>
    public static void SplitMessage(string message, out string body, out string sub)
    {
        message = message ?? string.Empty;
        int index = message.IndexOf(GameConstants.Confirm.SubSeparator);
        if (index < 0)
        {
            body = message;
            sub = string.Empty;
            return;
        }
        body = message.Substring(0, index).TrimEnd();
        sub = message.Substring(index + 1).Trim();
    }

    /// <summary>
    /// 위험 표시에 맞춰 칠한다: 취소 = 기본 버튼, 확인 = 강조(하늘색) 또는 위험(빨강), "!" 원·선택 링도 같은 색.
    /// </summary>
    private void ApplyDanger(bool danger)
    {
        isDanger = danger;
        UITheme theme = UITheme.Current;
        UIThemePaint.PopupButton(cancelButton.targetGraphic, cancelBorder, cancelText, PopupButtonStyle.Normal);
        UIThemePaint.PopupButton(confirmButton.targetGraphic, confirmBorder, confirmText, danger ? PopupButtonStyle.Danger : PopupButtonStyle.Accent);

        if (iconImage != null) iconImage.color = danger ? theme.DangerSelectedBackground : theme.SelectedBackground;
        if (iconText != null) iconText.color = theme.AccentOrDanger(danger);
        if (messageText != null) messageText.color = theme.textPrimary;
        if (subMessageText != null) subMessageText.color = theme.textMuted;

        UIPopupSelect cancelSelect = cancelButton.GetComponent<UIPopupSelect>();
        if (cancelSelect != null) cancelSelect.SetDanger(false);
        UIPopupSelect confirmSelect = confirmButton.GetComponent<UIPopupSelect>();
        if (confirmSelect != null) confirmSelect.SetDanger(danger);
    }

    /// <summary>ESC = 취소</summary>
    public override void OnCancelRequested()
    {
        Answer(false);
    }

    private void OnDestroy()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnClickCancel);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
    }

    private void OnClickCancel()
    {
        Answer(false);
    }

    private void OnClickConfirm()
    {
        Answer(true);
    }

    /// <summary>
    /// 한 번만 답한다: 먼저 닫고 콜백을 부른다.
    /// </summary>
    private void Answer(bool confirmed)
    {
        if (isAnswered || !IsOpen) return;

        isAnswered = true;
        Action callback = confirmed ? onConfirm : onCancel;
        onConfirm = null;
        onCancel = null;

        Close();
        Debug.Log($"[Popup_Confirm] {(confirmed ? "확인" : "취소")}", this);
        callback?.Invoke();
    }
}
