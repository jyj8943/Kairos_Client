using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 UI. TitleScene이 Init()을 호출한다.
/// - 버튼 OnClick은 인스펙터가 아니라 Init()에서 코드로 연결한다.
/// - 플레이 → 곡 선택, 에디터 → 에디터 곡 고르기, 종료 → 확인 팝업 → 게임 종료 (Unity 에디터에서는 플레이 모드 종료).
/// - 설정 버튼 동작은 설정 창을 만들 때 추가한다.
/// - 키보드 이동(↑↓, Enter)은 EventSystem이 맡는다.
/// </summary>
public class Canvas_Title : UIBase
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button editorButton;
    [SerializeField] private Button quitButton;

    [Tooltip("씬 이동을 시작하면 한 번에 막을 메뉴 버튼 (Btn_Play, Btn_Editor, Btn_Settings, Btn_Quit)")]
    [SerializeField] private Button[] menuButtons;

    /// <summary>
    /// 버튼 리스너를 연결한다.
    /// </summary>
    public void Init()
    {
        RemoveListeners();
        playButton.onClick.AddListener(OnClickPlay);
        editorButton.onClick.AddListener(OnClickEditor);
        quitButton.onClick.AddListener(OnClickQuit);
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    private void RemoveListeners()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnClickPlay);
        if (editorButton != null) editorButton.onClick.RemoveListener(OnClickEditor);
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
        foreach (Button button in menuButtons)
        {
            if (button != null) button.interactable = interactable;
        }
    }
}
