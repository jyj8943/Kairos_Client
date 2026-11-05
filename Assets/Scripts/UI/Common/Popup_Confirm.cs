using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 어디서든 쓰는 공용 확인 팝업 (기획서 3-8). UIManager 오버레이 Canvas 아래에 두고, UIManager가 Init()을 호출한다.
/// - 호출하는 쪽은 GameManager.UI.Confirm(문구, 확인 버튼 이름, 확인 콜백, 취소 콜백) 한 줄만 쓴다.
/// - 처음 선택은 Btn_Cancel (Enter를 연타해도 실수로 진행되지 않게). ←→로 두 버튼을 오가고, Enter는 선택한 버튼, ESC는 취소.
/// - 바깥 클릭은 무시한다 (closeOnOutsideClick 꺼 둠).
/// - 버튼을 누르면 먼저 팝업을 닫고 콜백을 한 번만 부른다 (연타로 두 번 불리지 않는다).
/// - 이미 열려 있을 때 새 요청이 오면 무시하고 경고 로그를 남긴다.
/// </summary>
public class Popup_Confirm : UIPopup
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmText;

    private Action onConfirm;
    private Action onCancel;
    private bool isAnswered;

    /// <summary>
    /// 버튼 리스너와 ←→ 이동을 연결하고 닫힌 상태로 둔다.
    /// </summary>
    public void Init()
    {
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
    /// <returns>열었으면 true. 이미 열려 있으면 false (새 요청은 무시)</returns>
    public bool Show(string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
    {
        if (IsOpen)
        {
            Debug.LogWarning($"[Popup_Confirm] 이미 열려 있어 새 요청을 무시합니다: \"{message}\"", this);
            return false;
        }

        messageText.text = message;
        confirmText.text = confirmLabel;
        cancelText.text = cancelLabel;
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        isAnswered = false;

        Open();
        Debug.Log($"[Popup_Confirm] 열림: \"{message.Replace("\n", " ")}\" (확인 버튼: {confirmLabel})", this);
        return true;
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
