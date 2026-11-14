using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 UI. TitleScene이 Init()을 호출한다.
/// - 버튼 OnClick은 인스펙터가 아니라 Init()에서 코드로 연결한다.
/// - 플레이 → 곡 선택, 에디터 → 에디터 곡 고르기, 설정 → 공용 설정 창(GameManager.UI.OpenSettings), 종료 → 확인 팝업 → 게임 종료 (Unity 에디터에서는 플레이 모드 종료).
/// - 키보드 이동(↑↓, Enter)은 EventSystem이 맡는다. ESC(TitleScene.OnBack)는 종료 확인 팝업을 연다 (RequestQuit).
/// - 아래쪽 키 안내 줄(KeyGuideBar): ↑↓ 이동 · Enter 선택 · ESC 종료.
/// </summary>
public class Canvas_Title : UIBase
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button editorButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Tooltip("씬 이동을 시작하면 한 번에 막을 메뉴 버튼 (Btn_Play, Btn_Editor, Btn_Settings, Btn_Quit)")]
    [SerializeField] private Button[] menuButtons;

    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private bool isLeaving;

    /// <summary>
    /// 버튼 리스너와 키 안내 줄을 연결한다.
    /// </summary>
    public void Init()
    {
        isLeaving = false;
        RemoveListeners();
        playButton.onClick.AddListener(OnClickPlay);
        editorButton.onClick.AddListener(OnClickEditor);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnClickSettings);
        else Debug.LogWarning("[Canvas_Title] settingsButton이 연결되지 않았습니다.", this);
        quitButton.onClick.AddListener(OnClickQuit);
        InitKeyGuide();
    }

    /// <summary>키 안내 줄: ↑↓ 이동 · Enter 선택 · ESC 종료 (모두 UI 맵 키라 고정 글자)</summary>
    private void InitKeyGuide()
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Canvas_Title] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Move, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.Select, KeyGuideKey.Fixed(GameConstants.KeyGuide.EnterKey)),
            new KeyGuideEntry(GameConstants.KeyGuide.Quit, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>
    /// ESC (TitleScene.OnBack): 종료 확인 팝업을 연다. 씬을 떠나는 중이면 무시한다.
    /// 다른 팝업이 열려 있으면 UIManager가 그 팝업을 먼저 처리하므로 여기까지 오지 않는다.
    /// </summary>
    public void RequestQuit()
    {
        if (isLeaving) return;
        OnClickQuit();
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    private void RemoveListeners()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnClickPlay);
        if (editorButton != null) editorButton.onClick.RemoveListener(OnClickEditor);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnClickSettings);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnClickQuit);
    }

    /// <summary>
    /// 곡 선택 씬으로 이동한다. 로드를 시작했으면 메뉴 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void OnClickPlay()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.SongSelect))
            SetMenuInteractable(false);
    }

    /// <summary>
    /// 설정 창을 연다. 씬을 옮기지 않으므로 메뉴 버튼은 그대로 둔다 (설정 창이 열린 동안은 팝업이 뒤 화면 조작을 막는다).
    /// </summary>
    private void OnClickSettings()
    {
        GameManager.UI.OpenSettings();
    }

    /// <summary>
    /// 에디터 곡 고르기 씬으로 이동한다. 로드를 시작했으면 메뉴 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void OnClickEditor()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect))
            SetMenuInteractable(false);
    }

    /// <summary>
    /// 확인 팝업을 띄우고, 확인하면 게임을 끝낸다.
    /// </summary>
    private void OnClickQuit()
    {
        GameManager.UI.Confirm(GameConstants.Confirm.QuitMessage, GameConstants.Confirm.QuitLabel, Quit);
    }

    /// <summary>
    /// 게임을 끝낸다. Unity 에디터에서는 플레이 모드를 끝낸다.
    /// </summary>
    private void Quit()
    {
        Debug.Log("[Canvas_Title] 게임 종료");
        SetMenuInteractable(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetMenuInteractable(bool interactable)
    {
        isLeaving = !interactable;
        foreach (Button button in menuButtons)
        {
            if (button != null) button.interactable = interactable;
        }
    }
}
