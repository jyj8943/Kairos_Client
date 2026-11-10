using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인게임 라인 키 입력 (입력 에셋의 Gameplay/Lane0~Lane3 = A · S · ; · '). InGameScene.unity의 "InGameInput"에 붙이고,
/// InGameScene이 Init()을 호출하고 OnEnter에서 EnableInput, OnExit에서 DisableInput을 부른다.
/// - 키를 누른 순간의 입력 시각(context.time)을 곡 시간으로 바꿔 넘긴다. 프레임 단위로 판정하지 않는다.
/// - 키를 뗀 순간도 같은 방식(입력 시각 → 곡 시간)으로 넘긴다 (롱노트 끝 판정용).
/// - 키를 누르는 동안과 뗐을 때를 따로 알려준다 (라인 빛남용).
/// - 지금 키가 눌려 있는지 IsLanePressed로 물을 수 있다 (일시정지 재개 때 누르고 있던 롱노트 확인용).
/// </summary>
public class InGameInput : MonoBehaviour
{
    [Tooltip("입력 에셋의 Gameplay/Lane0 ~ Lane3 (A · S · ; · ' 순서)")]
    [SerializeField] private InputActionReference[] laneActions;

    private Action<int, double> onLanePressed;
    private Action<int, double> onLaneReleased;
    private Action<int, bool> onLaneHeld;
    private Action<InputAction.CallbackContext>[] startedHandlers;
    private Action<InputAction.CallbackContext>[] canceledHandlers;
    private bool isInputEnabled;

    /// <summary>
    /// 입력을 넘길 곳을 받고, 라인마다 콜백을 한 번만 만들어 둔다 (구독할 때 새로 만들지 않게).
    /// </summary>
    /// <param name="onLanePressed">(라인, 누른 곡 시간)</param>
    /// <param name="onLaneReleased">(라인, 뗀 곡 시간)</param>
    /// <param name="onLaneHeld">(라인, 누르는 중이면 true)</param>
    public void Init(Action<int, double> onLanePressed, Action<int, double> onLaneReleased, Action<int, bool> onLaneHeld)
    {
        this.onLanePressed = onLanePressed;
        this.onLaneReleased = onLaneReleased;
        this.onLaneHeld = onLaneHeld;

        int count = laneActions != null ? laneActions.Length : 0;
        startedHandlers = new Action<InputAction.CallbackContext>[count];
        canceledHandlers = new Action<InputAction.CallbackContext>[count];
        for (int lane = 0; lane < count; lane++)
        {
            int captured = lane;
            startedHandlers[lane] = context => OnStarted(captured, context);
            canceledHandlers[lane] = context => OnCanceled(captured, context);
        }
    }

    /// <summary>
    /// 라인 키 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled || startedHandlers == null) return;

        if (laneActions == null || laneActions.Length != GameConstants.Editor.LaneCount)
        {
            Debug.LogError($"[InGameInput] laneActions는 {GameConstants.Editor.LaneCount}개여야 합니다.", this);
            return;
        }

        for (int lane = 0; lane < laneActions.Length; lane++)
        {
            InputAction action = laneActions[lane].action;
            action.started += startedHandlers[lane];
            action.canceled += canceledHandlers[lane];
            action.Enable();
        }
        isInputEnabled = true;
    }

    /// <summary>
    /// 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        for (int lane = 0; lane < laneActions.Length; lane++)
        {
            InputAction action = laneActions[lane].action;
            action.started -= startedHandlers[lane];
            action.canceled -= canceledHandlers[lane];
            onLaneHeld?.Invoke(lane, false);
        }
        isInputEnabled = false;
    }

    private void OnDestroy()
    {
        DisableInput();
    }

    /// <summary>
    /// 라인 키가 지금 눌려 있으면 true (입력을 받고 있지 않으면 false).
    /// </summary>
    public bool IsLanePressed(int lane)
    {
        if (!isInputEnabled || laneActions == null || lane < 0 || lane >= laneActions.Length) return false;
        InputAction action = laneActions[lane].action;
        return action != null && action.IsPressed();
    }

    private void OnStarted(int lane, InputAction.CallbackContext context)
    {
        // 팝업이 열려 있으면 라인 키를 받지 않는다
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;

        // 입력 시각(realtimeSinceStartup 기준)을 곡 시간으로 바꾼다
        double inputSongTime = GameManager.Sound.GetSongTimeAtRealtime(context.time);
        onLaneHeld?.Invoke(lane, true);
        onLanePressed?.Invoke(lane, inputSongTime);
    }

    private void OnCanceled(int lane, InputAction.CallbackContext context)
    {
        onLaneHeld?.Invoke(lane, false);

        // 뗀 시각도 누를 때와 같은 방식으로 곡 시간으로 바꾼다. 팝업·일시정지 중인지는 받는 쪽(InGameScene)이 판단한다
        if (!SoundManager.HasInstance) return;
        double releaseSongTime = GameManager.Sound.GetSongTimeAtRealtime(context.time);
        onLaneReleased?.Invoke(lane, releaseSongTime);
    }
}
