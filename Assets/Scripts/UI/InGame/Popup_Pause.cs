using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 일시정지 메뉴 (기획서 3-4). InGameScene.unity의 Canvas_InGame 아래에 두고, InGameScene이 Init()을 호출한다.
/// - 버튼은 위에서부터 계속 · 재시작 · 설정 · 나가기. 처음 선택은 계속, ↑↓로 이동, Enter 실행.
/// - ESC(팝업이 열려 있을 때 UIManager가 부름)는 "계속"과 같다. 바깥 클릭은 무시한다.
/// - 버튼을 누르면 InGameScene이 넘긴 콜백만 부른다. 돌아갈 곳(곡 선택·에디터)은 이 팝업이 알지 않는다.
/// - 아래쪽 키 안내 줄(KeyGuideBar): ↑↓ 이동 · Enter 선택 · F5 다시 하기 · ESC 계속. F5(즉시 다시 하기)는 InGameScene이 받는다.
/// - 설정 버튼은 공용 설정 창(GameManager.UI.OpenSettings)을 이 메뉴 위에 연다. 설정을 닫으면 이 메뉴로 돌아온다 (인게임은 멈춘 그대로).
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
    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private Action onResume;
    private Action onRestart;
    private Action onExit;

    /// <summary>
    /// 문구와 버튼 콜백, ↑↓ 이동을 연결하고 닫힌 상태로 둔다.
    /// </summary>
    /// <param name="onResume">계속 (ESC도 같다)</param>
    /// <param name="onRestart">재시작</param>
    /// <param name="onExit">나가기</param>
    /// <param name="quickRestartAction">F5 즉시 다시 하기 액션 (Gameplay/Restart, 키 안내 줄에 키 이름을 보여줄 때만 쓴다)</param>
    public void Init(Action onResume, Action onRestart, Action onExit, InputActionReference quickRestartAction)
    {
        this.onResume = onResume;
        this.onRestart = onRestart;
        this.onExit = onExit;

        titleText.text = GameConstants.Pause.Title;
        resumeText.text = GameConstants.Pause.ResumeLabel;
        restartText.text = GameConstants.Pause.RestartLabel;
        settingsText.text = GameConstants.Pause.SettingsLabel;
        exitText.text = GameConstants.Pause.ExitLabel;
        InitKeyGuide(quickRestartAction);

        resumeButton.onClick.RemoveListener(OnClickResume);
        resumeButton.onClick.AddListener(OnClickResume);
        restartButton.onClick.RemoveListener(OnClickRestart);
        restartButton.onClick.AddListener(OnClickRestart);
        exitButton.onClick.RemoveListener(OnClickExit);
        exitButton.onClick.AddListener(OnClickExit);
        settingsButton.onClick.RemoveListener(OnClickSettings);
        settingsButton.onClick.AddListener(OnClickSettings);
        settingsButton.interactable = true;

        // ↑↓: 계속 → 재시작 → 설정 → 나가기
        resumeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restartButton };
        restartButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = resumeButton, selectOnDown = settingsButton };
        settingsButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = restartButton, selectOnDown = exitButton };
        exitButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = settingsButton };

        HideImmediate();
    }

    /// <summary>키 안내 줄: ↑↓ 이동 · Enter 선택 · F5 다시 하기 · ESC 계속</summary>
    private void InitKeyGuide(InputActionReference quickRestartAction)
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Popup_Pause] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Move, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.Select, KeyGuideKey.Fixed(GameConstants.KeyGuide.EnterKey)),
            new KeyGuideEntry(GameConstants.KeyGuide.Retry, KeyGuideKey.FromAction(quickRestartAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Resume, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

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
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnClickSettings);
    }

    /// <summary>설정: 공용 설정 창을 이 메뉴 위에 연다 (맨 위 팝업일 때만)</summary>
    private void OnClickSettings()
    {
        if (!IsOpen || GameManager.UI.TopPopup != this) return;
        GameManager.UI.OpenSettings();
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
