using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 UI. TitleScene이 Init()을 호출한다.
/// - 버튼 OnClick은 인스펙터가 아니라 Init()에서 코드로 연결한다.
/// - 플레이·설정·종료 버튼 동작은 해당 씬과 창을 만들 때 추가한다.
/// </summary>
public class Canvas_Title : MonoBehaviour
{
    [SerializeField] private Button editorButton;

    [Tooltip("씬 이동을 시작하면 한 번에 막을 메뉴 버튼 (Btn_Play, Btn_Editor, Btn_Settings, Btn_Quit)")]
    [SerializeField] private Button[] menuButtons;

    /// <summary>
    /// 버튼 리스너를 연결한다.
    /// </summary>
    public void Init()
    {
        editorButton.onClick.RemoveListener(OnClickEditor);
        editorButton.onClick.AddListener(OnClickEditor);
    }

    private void OnDestroy()
    {
        if (editorButton != null) editorButton.onClick.RemoveListener(OnClickEditor);
    }

    /// <summary>
    /// 에디터 곡 고르기 씬으로 이동한다. 로드를 시작했으면 메뉴 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void OnClickEditor()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect))
            SetMenuInteractable(false);
    }

    private void SetMenuInteractable(bool interactable)
    {
        foreach (Button button in menuButtons)
        {
            if (button != null) button.interactable = interactable;
        }
    }
}
