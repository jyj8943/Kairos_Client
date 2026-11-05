#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 개발용 패널 (Unity 에디터 전용, 손맛 비교 확인용). InGameScene.unity의 "Canvas_InGameDebug"에 붙이고, InGameScene이 Init()을 호출한다.
/// - 파일 전체가 #if UNITY_EDITOR 안에 있고, 오브젝트는 EditorOnly 태그라 빌드에서 오브젝트째 빠진다 (에디터 F1 패널과 같다).
/// - F1(ChartEditor/ToggleTuning, 에디터 F1 패널과 같은 액션)로 여닫는다. 팝업이 열려 있으면 F1을 받지 않는다.
/// - 빛기둥·키음 토글은 DevPlaySettings 값을 바꾼다. 판정·점수·입력은 그대로이고 표시와 소리만 끈다.
/// - 게임을 멈추지 않는다: UIPopup이 아니라 팝업 스택에 들어가지 않으므로 라인 키·ESC 일시정지가 그대로 동작한다.
/// - 토글은 마우스로 누른다. 누른 뒤 EventSystem 선택을 비워 Enter·Space가 토글을 다시 누르지 않게 하고, 내비게이션은 끈다.
/// </summary>
public class Canvas_InGameDebug : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [Tooltip("빛기둥 (라인 빛남) 켜기·끄기")]
    [SerializeField] private Toggle laneGlowToggle;
    [Tooltip("키음 (NoteSFX) 켜기·끄기")]
    [SerializeField] private Toggle keySoundToggle;
    [Tooltip("지금 상태 한 줄 (Txt_DebugStatus)")]
    [SerializeField] private TMP_Text statusText;

    [Header("입력")]
    [Tooltip("입력 에셋의 ChartEditor/ToggleTuning (F1). 에디터 F1 패널과 같은 액션")]
    [SerializeField] private InputActionReference toggleAction;

    private GearView gear;
    private bool isInputEnabled;

    /// <summary>패널이 열려 있으면 true</summary>
    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>
    /// 기어(빛기둥을 바로 끌 때 쓴다)를 받고, 토글을 지금 DevPlaySettings 값으로 맞춘다. 패널은 닫은 상태로 시작한다.
    /// </summary>
    public void Init(GearView gear)
    {
        this.gear = gear;

        laneGlowToggle.onValueChanged.RemoveListener(OnLaneGlowToggled);
        keySoundToggle.onValueChanged.RemoveListener(OnKeySoundToggled);
        laneGlowToggle.SetIsOnWithoutNotify(DevPlaySettings.LaneGlowEnabled);
        keySoundToggle.SetIsOnWithoutNotify(DevPlaySettings.KeySoundEnabled);
        laneGlowToggle.onValueChanged.AddListener(OnLaneGlowToggled);
        keySoundToggle.onValueChanged.AddListener(OnKeySoundToggled);

        // 키보드 선택 이동으로 토글이 선택되지 않게 한다
        Navigation none = new Navigation { mode = Navigation.Mode.None };
        laneGlowToggle.navigation = none;
        keySoundToggle.navigation = none;

        panel.SetActive(false);
        RefreshStatus();
    }

    /// <summary>F1 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.</summary>
    public void EnableInput()
    {
        if (isInputEnabled || toggleAction == null) return;

        toggleAction.action.performed += OnToggle;
        toggleAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>F1 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.</summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        toggleAction.action.performed -= OnToggle;
        isInputEnabled = false;
    }

    private void OnDestroy()
    {
        DisableInput();
        if (laneGlowToggle != null) laneGlowToggle.onValueChanged.RemoveListener(OnLaneGlowToggled);
        if (keySoundToggle != null) keySoundToggle.onValueChanged.RemoveListener(OnKeySoundToggled);
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        // 팝업(일시정지·확인)이 열려 있으면 F1을 받지 않는다
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;

        panel.SetActive(!panel.activeSelf);
        RefreshStatus();
        Debug.Log($"[Canvas_InGameDebug] F1: 패널 {(panel.activeSelf ? "열림" : "닫힘")} ({statusText.text})", this);
    }

    private void OnLaneGlowToggled(bool on)
    {
        DevPlaySettings.LaneGlowEnabled = on;

        // 끄면 지금 켜져 있던 빛남도 바로 끈다
        if (!on && gear != null)
        {
            for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
                gear.SetPressed(lane, false);
        }

        AfterToggle();
    }

    private void OnKeySoundToggled(bool on)
    {
        DevPlaySettings.KeySoundEnabled = on;
        AfterToggle();
    }

    /// <summary>
    /// 토글을 누른 뒤: 선택을 비워 Enter·Space가 토글을 다시 누르지 않게 하고 상태를 다시 쓴다.
    /// </summary>
    private void AfterToggle()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        RefreshStatus();
        Debug.Log($"[Canvas_InGameDebug] {statusText.text}", this);
    }

    private void RefreshStatus()
    {
        statusText.text = string.Format(GameConstants.Play.DebugStatusFormat,
            DevPlaySettings.LaneGlowEnabled ? GameConstants.Play.DebugOnLabel : GameConstants.Play.DebugOffLabel,
            DevPlaySettings.KeySoundEnabled ? GameConstants.Play.DebugOnLabel : GameConstants.Play.DebugOffLabel);
    }
}
#endif
