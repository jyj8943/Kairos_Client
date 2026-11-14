using System;
using System.Collections.Generic;
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
/// - 버튼: 다시 하기, 돌아가기 (곡 선택으로 / 테스트 플레이면 에디터로). 문구는 넘겨받은 값을 쓴다. 마우스 클릭으로 누른다.
/// - 키 (기획서 3-9): F5 다시 하기(Result/Retry), Enter 돌아가기(Result/Return), ESC 돌아가기(UIManager → ResultScene.OnBack).
///   Enter가 선택된 버튼에 따라 바뀌지 않게 버튼은 키보드 선택을 받지 않는다 (Navigation None, 처음 선택 없음, 마우스를 올려도 선택하지 않음).
///   그래서 Enter를 누르면 EventSystem Submit은 아무 버튼에도 가지 않고 Result/Return만 실행된다.
/// - 아래쪽 키 안내 줄(KeyGuideBar): F5 다시 하기 · Enter 곡 선택 · ESC 곡 선택 (테스트 플레이면 "에디터로").
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

    [Header("입력")]
    [Tooltip("입력 에셋의 Result/Retry (F5)")]
    [SerializeField] private InputActionReference retryAction;
    [Tooltip("입력 에셋의 Result/Return (Enter)")]
    [SerializeField] private InputActionReference returnAction;

    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private Action onRetry;
    private Action onReturn;
    private bool isInputEnabled;
    private bool isLeaving;

    /// <summary>
    /// 결과를 표시하고 버튼·키 안내 줄을 연결한다. 버튼은 키보드 선택을 받지 않는다 (Enter는 항상 Result/Return).
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

        retryLabelText.text = GameConstants.Play.RetryLabel;
        returnLabelText.text = result.returnTarget != null ? result.returnTarget.buttonLabel : GameConstants.Play.ReturnToSongSelectLabel;

        retryButton.onClick.RemoveListener(OnClickRetry);
        retryButton.onClick.AddListener(OnClickRetry);
        returnButton.onClick.RemoveListener(OnClickReturn);
        returnButton.onClick.AddListener(OnClickReturn);
        SetInteractable(true);

        // 클릭해도 선택되지 않게 (선택된 버튼이 있으면 Enter가 Submit으로 그 버튼도 누른다)
        retryButton.navigation = new Navigation { mode = Navigation.Mode.None };
        returnButton.navigation = new Navigation { mode = Navigation.Mode.None };

        InitKeyGuide(result.isTestPlay);
    }

    /// <summary>키 안내 줄: F5 다시 하기 · Enter 곡 선택 · ESC 곡 선택 (테스트 플레이면 돌아가는 곳 이름이 "에디터로")</summary>
    private void InitKeyGuide(bool isTestPlay)
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Canvas_Result] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        string returnLabel = isTestPlay ? GameConstants.KeyGuide.ToEditor : GameConstants.KeyGuide.ToSongSelect;
        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Retry, KeyGuideKey.FromAction(retryAction)),
            new KeyGuideEntry(returnLabel, KeyGuideKey.FromAction(returnAction, GameConstants.KeyGuide.EnterKey)).WithId(GameConstants.KeyGuide.ReturnId),
            new KeyGuideEntry(returnLabel, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey)).WithId(GameConstants.KeyGuide.BackId)
        });
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

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
    /// F5·Enter 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (retryAction == null || returnAction == null)
        {
            Debug.LogWarning("[Canvas_Result] retryAction·returnAction이 연결되지 않아 키로 다시 하기·돌아가기를 할 수 없습니다.", this);
            return;
        }

        retryAction.action.performed += OnRetryKey;
        returnAction.action.performed += OnReturnKey;
        retryAction.action.Enable();
        returnAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// F5·Enter 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        retryAction.action.performed -= OnRetryKey;
        returnAction.action.performed -= OnReturnKey;
        isInputEnabled = false;
    }

    /// <summary>ESC (ResultScene.OnBack): 돌아가기 버튼과 같다</summary>
    public void RequestReturn()
    {
        OnClickReturn();
    }

    /// <summary>씬 이동을 시작하면 버튼을 막아 중복 실행을 막는다.</summary>
    public void SetInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        returnButton.interactable = interactable;
        isLeaving = !interactable;
    }

    /// <summary>
    /// 다른 코드가 버튼을 선택해 두었어도 다음 프레임에 풀어, Enter가 Submit으로 그 버튼까지 누르지 않게 한다
    /// (Enter는 항상 Result/Return 하나만 실행).
    /// </summary>
    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && (selected == retryButton.gameObject || selected == returnButton.gameObject))
            eventSystem.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        DisableInput();
        if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        if (returnButton != null) returnButton.onClick.RemoveListener(OnClickReturn);
    }

    private void OnRetryKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickRetry();
    }

    private void OnReturnKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickReturn();
    }

    /// <summary>팝업이 열려 있으면 키를 받지 않는다</summary>
    private static bool IsShortcutBlocked => UIManager.HasInstance && GameManager.UI.HasOpenPopup;

#if UNITY_EDITOR
    /// <summary>테스트 전용 (Unity 에디터): 다시 하기·돌아가기가 실행된 횟수 (Enter 한 번에 한 번만 실행되는지 확인)</summary>
    public int TestRetryCount { get; private set; }
    public int TestReturnCount { get; private set; }
#endif

    private void OnClickRetry()
    {
        if (isLeaving) return;
#if UNITY_EDITOR
        TestRetryCount++;
#endif
        onRetry?.Invoke();
    }

    private void OnClickReturn()
    {
        if (isLeaving) return;
#if UNITY_EDITOR
        TestReturnCount++;
#endif
        onReturn?.Invoke();
    }
}
