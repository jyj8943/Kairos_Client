using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 일시정지 메뉴 (기획서 3-4). InGameScene.unity의 Canvas_InGame 아래에 두고, InGameScene이 Init()을 호출한다.
/// - 버튼은 위에서부터 계속 · 재시작 · 설정 · 나가기. 처음 선택은 계속, ↑↓로 이동, Enter 실행.
/// - ESC(팝업이 열려 있을 때 UIManager가 부름)는 "계속"과 같다. 바깥 클릭은 무시한다.
/// - 버튼을 누르면 InGameScene이 넘긴 콜백만 부른다. 돌아갈 곳(곡 선택·에디터)은 이 팝업이 알지 않는다.
/// - 설정 버튼은 설정 창이 아직 없어 눌리지 않게 둔다.
/// </summary>
public class Popup_Pause : UIPopup
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private TMP_Text resumeText;
    [SerializeField] private Button restartButton;
    [SerializeField] private TMP_Text restartText;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Text settingsText;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text exitText;
    [Tooltip("패널 아래 조작 안내 (Txt_Hint)")]
    [SerializeField] private TMP_Text hintText;

    private Action onResume;
    private Action onRestart;
    private Action onExit;

    /// <summary>
    /// 문구와 버튼 콜백, ↑↓ 이동을 연결하고 닫힌 상태로 둔다.
    /// </summary>
    /// <param name="onResume">계속 (ESC도 같다)</param>
    /// <param name="onRestart">재시작</param>
    /// <param name="onExit">나가기</param>
    public void Init(Action onResume, Action onRestart, Action onExit)
    {
        this.onResume = onResume;
        this.onRestart = onRestart;
        this.onExit = onExit;

        titleText.text = GameConstants.Pause.Title;
        resumeText.text = GameConstants.Pause.ResumeLabel;
        restartText.text = GameConstants.Pause.RestartLabel;
        settingsText.text = GameConstants.Pause.SettingsLabel;
        exitText.text = GameConstants.Pause.ExitLabel;
        if (hintText != null) hintText.text = GameConstants.Pause.HintText;

        resumeButton.onClick.RemoveListener(OnClickResume);
        resumeButton.onClick.AddListener(OnClickResume);
        restartButton.onClick.RemoveListener(OnClickRestart);
        restartButton.onClick.AddListener(OnClickRestart);
        exitButton.onClick.RemoveListener(OnClickExit);
        exitButton.onClick.AddListener(OnClickExit);

        // TODO: 설정 창을 만들면 settingsButton을 켜고 설정 창 열기를 연결한다
        settingsButton.interactable = false;

        // 설정 버튼은 꺼져 있어 ↑↓ 이동에서 건너뛴다 (재시작 ↔ 나가기)
        resumeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restartButton };
        restartButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = resumeButton, selectOnDown = exitButton };
        settingsButton.navigation = new Navigation { mode = Navigation.Mode.None };
        exitButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = restartButton };

        HideImmediate();
    }

    /// <summary>ESC = 계속</summary>
    public override void OnCancelRequested()
    {
        OnClickResume();
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnClickResume);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnClickRestart);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnClickExit);
    }

    private void OnClickResume()
    {
        if (!IsOpen) return;
        onResume?.Invoke();
    }

    private void OnClickRestart()
    {
        if (!IsOpen) return;
        onRestart?.Invoke();
    }

    private void OnClickExit()
    {
        if (!IsOpen) return;
        onExit?.Invoke();
    }
}
