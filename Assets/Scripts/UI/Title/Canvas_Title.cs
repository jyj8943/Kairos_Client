using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 UI. TitleScene이 Init()을 호출한다.
/// - 버튼 OnClick은 인스펙터가 아니라 Init()에서 코드로 연결한다.
/// - 플레이 → 곡 선택, 에디터 → 곡 선택 나만의 채보 탭 (기획서 3-1, 11/18), 설정 → 공용 설정 창(GameManager.UI.OpenSettings), 종료 → 확인 팝업 → 게임 종료 (Unity 에디터에서는 플레이 모드 종료).
/// - 키보드 이동(↑↓, Enter)은 EventSystem이 맡는다. ESC(TitleScene.OnBack)는 종료 확인 팝업을 연다 (RequestQuit).
/// - 아래쪽 키 안내 줄(KeyGuideBar): ↑↓ 이동 · Enter 선택 · ESC 종료.
/// - 메뉴 버튼은 한글 + 영문 작은 글씨(GameConstants.Title), 선택된 버튼만 UISelectHighlight로 강조한다 (기획서 3-2, 11/17 개편).
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

    [Tooltip("로고 \"KAIROS\" (Txt_Logo). 글자 간격은 GameConstants.Title.LogoCharacterSpacing")]
    [SerializeField] private TMP_Text logoText;

    [Tooltip("메뉴 영문 작은 글씨 (Txt_PlayEn, Txt_EditorEn, Txt_SettingsEn, Txt_QuitEn)")]
    [SerializeField] private TMP_Text playEnText;
    [SerializeField] private TMP_Text editorEnText;
    [SerializeField] private TMP_Text settingsEnText;
    [SerializeField] private TMP_Text quitEnText;

    [Tooltip("메뉴 버튼 선택 강조 (Btn_Play, Btn_Editor, Btn_Settings, Btn_Quit의 UISelectHighlight)")]
    [SerializeField] private UISelectHighlight[] menuHighlights;

    private bool isLeaving;

    /// <summary>
    /// 버튼 리스너와 키 안내 줄을 연결하고, 메뉴 글자와 선택 강조를 맞춘다.
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
        InitMenuTexts();
        SyncHighlights();
    }

    /// <summary>로고 글자 간격과 메뉴 영문 글자 (GameConstants.Title)</summary>
    private void InitMenuTexts()
    {
        if (logoText != null) logoText.characterSpacing = GameConstants.Title.LogoCharacterSpacing;
        SetEnText(playEnText, GameConstants.Title.PlayEn);
        SetEnText(editorEnText, GameConstants.Title.EditorEn);
        SetEnText(settingsEnText, GameConstants.Title.SettingsEn);
        SetEnText(quitEnText, GameConstants.Title.QuitEn);
    }

    private void SetEnText(TMP_Text text, string value)
    {
        if (text == null)
        {
            Debug.LogWarning($"[Canvas_Title] 영문 메뉴 글자({value})가 연결되지 않았습니다.", this);
            return;
        }
        text.text = value;
        text.characterSpacing = GameConstants.Title.MenuEnCharacterSpacing;
    }

    /// <summary>
    /// 선택 강조를 EventSystem의 지금 선택에 맞춘다. 처음 선택(Btn_Play)은 EventSystem First Selected가 정하고,
    /// 그 뒤 선택이 바뀌면 각 UISelectHighlight가 스스로 따라간다.
    /// </summary>
    private void SyncHighlights()
    {
        if (menuHighlights == null) return;
        foreach (UISelectHighlight highlight in menuHighlights)
        {
            if (highlight != null) highlight.Sync();
        }
    }

    /// <summary>메뉴 버튼 선택 강조 (확인용)</summary>
    public IReadOnlyList<UISelectHighlight> MenuHighlights => menuHighlights;

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
    /// 곡 선택을 공식 채보 탭으로 연다 (마지막 선택의 공식 칸: 마지막으로 플레이한 공식 곡·난이도, 기획서 2-4 11/18). 로드를 시작했으면 메뉴 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void OnClickPlay()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.SongSelect, new SongSelectEntryData { chartType = ChartType.Official }))
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
    /// 곡 선택을 나만의 채보 탭으로 연다 (마지막 선택의 나만의 채보 칸: 채보 · 없으면 그 곡의 + 줄 · 칸이 비었으면 첫 줄, 기획서 2-4 11/18). 로드를 시작했으면 메뉴 버튼을 막아 중복 실행을 막는다.
    /// </summary>
    private void OnClickEditor()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.SongSelect, new SongSelectEntryData { chartType = ChartType.Custom }))
            SetMenuInteractable(false);
    }

    /// <summary>
    /// 확인 팝업을 띄우고, 확인하면 게임을 끝낸다.
    /// </summary>
    private void OnClickQuit()
    {
        GameManager.UI.Confirm(GameConstants.Confirm.QuitMessage, GameConstants.Confirm.QuitLabel, Quit, null, GameConstants.Confirm.QuitIsDanger);
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
