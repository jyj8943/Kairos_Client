using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 UI (기획서 3-5 최소 형태). ResultScene이 Init()을 호출한다.
/// - 랭크(크게), 점수, 신기록(Txt_NewRecord, 점수 옆), FULL COMBO / ALL PERFECT, 판정별 개수, 최대 콤보, 정확도, 곡 정보, 테스트 플레이 표시.
/// - 신기록: 처음 플레이면 "NEW RECORD", 기록을 넘었으면 이전 최고 점수도 함께. 신기록이 아니면(테스트·자동 플레이 포함) 숨긴다.
/// - 버튼: 다시 하기(R), 돌아가기(Enter — 처음에 선택해 두므로 Enter로 누른다). 문구는 넘겨받은 값을 쓴다.
/// - R은 입력 에셋의 Result/Retry로 받는다.
/// </summary>
public class Canvas_Result : UIBase
{
    [Header("결과")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("신기록 표시 (Txt_NewRecord, 점수 옆). 신기록이 아니면 숨긴다")]
    [SerializeField] private TMP_Text newRecordText;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private TMP_Text judgesText;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private TMP_Text testPlayText;

    [Header("버튼")]
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryLabelText;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text returnLabelText;
    [SerializeField] private UISelectionKeeper selectionKeeper;

    [Header("입력")]
    [Tooltip("입력 에셋의 Result/Retry (R)")]
    [SerializeField] private InputActionReference retryAction;

    private Action onRetry;
    private Action onReturn;
    private bool isInputEnabled;
    private bool isLeaving;

    /// <summary>
    /// 결과를 표시하고 버튼을 연결한다. 돌아가기 버튼을 선택한 상태로 시작한다.
    /// </summary>
    public void Init(ResultData result, Action onRetry, Action onReturn)
    {
        this.onRetry = onRetry;
        this.onReturn = onReturn;
        isLeaving = false;

        rankText.text = result.rank.ToString();
        scoreText.text = result.score.ToString("N0", CultureInfo.InvariantCulture);
        ShowNewRecord(result);

        if (result.isAllPerfect) badgeText.text = GameConstants.Play.AllPerfectText;
        else if (result.isFullCombo) badgeText.text = GameConstants.Play.FullComboText;
        else badgeText.text = string.Empty;

        judgesText.text =
            $"{GameConstants.Play.PerfectText}  {result.perfectCount}\n" +
            $"{GameConstants.Play.GreatText}  {result.greatCount}\n" +
            $"{GameConstants.Play.GoodText}  {result.goodCount}\n" +
            $"{GameConstants.Play.MissText}  {result.missCount}";
        maxComboText.text = $"MAX COMBO  {result.maxCombo} / {result.maxPossibleCombo}";
        accuracyText.text = $"정확도  {result.accuracy.ToString("0.00", CultureInfo.InvariantCulture)}%";
        songInfoText.text = string.Format(GameConstants.Editor.ChartInfoFormat, result.songTitle, result.chartName, result.level);

        testPlayText.text = result.isAutoPlay ? GameConstants.Play.AutoTestPlayLabel : GameConstants.Play.TestPlayLabel;
        testPlayText.gameObject.SetActive(result.isTestPlay);

        retryLabelText.text = GameConstants.Play.RetryLabel + GameConstants.Play.RetryKeyHint;
        string returnLabel = result.returnTarget != null ? result.returnTarget.buttonLabel : GameConstants.Play.ReturnToSongSelectLabel;
        returnLabelText.text = returnLabel + GameConstants.Play.ReturnKeyHint;

        retryButton.onClick.RemoveListener(OnClickRetry);
        retryButton.onClick.AddListener(OnClickRetry);
        returnButton.onClick.RemoveListener(OnClickReturn);
        returnButton.onClick.AddListener(OnClickReturn);
        SetInteractable(true);

        if (selectionKeeper != null) selectionKeeper.SetDefaultSelectable(returnButton);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
    }

    /// <summary>신기록 표시: 처음 플레이면 "NEW RECORD", 기록을 넘었으면 이전 최고 점수와 함께. 아니면 숨긴다</summary>
    private void ShowNewRecord(ResultData result)
    {
        if (newRecordText == null) return;

        newRecordText.gameObject.SetActive(result.isNewRecord);
        if (!result.isNewRecord) return;

        newRecordText.text = result.hadPreviousRecord
            ? string.Format(CultureInfo.InvariantCulture, GameConstants.Play.NewRecordPreviousFormat, result.previousBestScore)
            : GameConstants.Play.NewRecordText;
    }

    /// <summary>
    /// R 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (retryAction == null)
        {
            Debug.LogWarning("[Canvas_Result] retryAction이 연결되지 않아 R로 다시 할 수 없습니다.", this);
            return;
        }

        retryAction.action.performed += OnRetryKey;
        retryAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// R 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        retryAction.action.performed -= OnRetryKey;
        isInputEnabled = false;
    }

    /// <summary>씬 이동을 시작하면 버튼을 막아 중복 실행을 막는다.</summary>
    public void SetInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        returnButton.interactable = interactable;
        isLeaving = !interactable;
    }

    private void OnDestroy()
    {
        DisableInput();
        if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        if (returnButton != null) returnButton.onClick.RemoveListener(OnClickReturn);
    }

    private void OnRetryKey(InputAction.CallbackContext context)
    {
        // 팝업이 열려 있으면 R을 받지 않는다
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;
        OnClickRetry();
    }

    private void OnClickRetry()
    {
        if (!isLeaving) onRetry?.Invoke();
    }

    private void OnClickReturn()
    {
        if (!isLeaving) onReturn?.Invoke();
    }
}
