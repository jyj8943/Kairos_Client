using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 에디터 곡 고르기 화면 UI. EditorSongSelectScene이 Init()을 호출한다.
/// - 곡 목록은 받은 순서(SongDatabase 순서)대로 Btn_SongItem 프리팹을 생성해 채운다.
/// - 곡 아이템으로 곡을 고르고, 새 채보 정보(이름·레벨)를 입력해 편집 씬으로 넘어간다.
/// - ESC는 InputManager 없이 입력 에셋의 UI/Cancel 액션으로 받는다.
/// </summary>
public class Canvas_EditorSongSelect : MonoBehaviour
{
    [Header("곡 목록")]
    [SerializeField] private Btn_SongItem songItemPrefab;
    [SerializeField] private RectTransform songListContent;
    [SerializeField] private UISelectionKeeper selectionKeeper;

    [Header("내 채보")]
    [SerializeField] private TMP_Text myChartTitleText;
    [SerializeField] private TMP_Text myChartEmptyText;

    [Header("새 채보 만들기")]
    [SerializeField] private TMP_InputField chartNameInput;
    [SerializeField] private TMP_InputField levelInput;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button createButton;

    [Header("나가기")]
    [SerializeField] private Button exitButton;
    [Tooltip("입력 에셋의 UI/Cancel (ESC)")]
    [SerializeField] private InputActionReference cancelAction;

    private readonly List<Btn_SongItem> songItems = new List<Btn_SongItem>();
    private Btn_SongItem chosenItem;
    private SongData chosenSong;
    private bool isInputEnabled;

    /// <summary>
    /// 곡 목록을 만들고, 내비게이션과 버튼 리스너를 연결한 뒤 첫 곡을 고른 상태로 시작한다.
    /// </summary>
    public void Init(IReadOnlyList<SongData> songs)
    {
        errorText.text = string.Empty;

        CreateSongItems(songs);
        ConnectNavigation();

        exitButton.onClick.RemoveListener(OnClickExit);
        exitButton.onClick.AddListener(OnClickExit);
        createButton.onClick.RemoveListener(OnClickCreate);
        createButton.onClick.AddListener(OnClickCreate);

        if (songItems.Count == 0) return;

        Btn_SongItem firstItem = songItems[0];
        OnSongChosen(firstItem.Song);
        selectionKeeper.SetDefaultSelectable(firstItem.Button);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstItem.gameObject);
    }

    /// <summary>
    /// ESC 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (cancelAction == null)
        {
            Debug.LogWarning("[Canvas_EditorSongSelect] cancelAction이 연결되지 않아 ESC로 나갈 수 없습니다.", this);
            return;
        }

        cancelAction.action.performed += OnCancel;
        cancelAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// ESC 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        cancelAction.action.performed -= OnCancel;
        isInputEnabled = false;
    }

    private void OnDestroy()
    {
        DisableInput();

        if (exitButton != null) exitButton.onClick.RemoveListener(OnClickExit);
        if (createButton != null) createButton.onClick.RemoveListener(OnClickCreate);

        foreach (Btn_SongItem item in songItems)
        {
            if (item != null) item.Button.onClick.RemoveListener(FocusChartNameInput);
        }
    }

    /// <summary>
    /// Content의 기존 자식을 비우고 곡마다 아이템을 생성한다.
    /// </summary>
    private void CreateSongItems(IReadOnlyList<SongData> songs)
    {
        for (int i = songListContent.childCount - 1; i >= 0; i--)
            Destroy(songListContent.GetChild(i).gameObject);

        songItems.Clear();

        foreach (SongData song in songs)
        {
            if (song == null) continue;

            Btn_SongItem item = Instantiate(songItemPrefab, songListContent);
            item.name = songItemPrefab.name;
            item.Init(song, OnSongChosen);
            item.Button.onClick.AddListener(FocusChartNameInput);
            songItems.Add(item);
        }
    }

    /// <summary>
    /// 곡 아이템끼리 위아래(순환 없음), 곡 → 채보 이름 입력창, 입력창 ↔ 레벨 ↔ 만들기 버튼을 연결한다.
    /// </summary>
    private void ConnectNavigation()
    {
        for (int i = 0; i < songItems.Count; i++)
        {
            Navigation navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? songItems[i - 1].Button : null,
                selectOnDown = i < songItems.Count - 1 ? songItems[i + 1].Button : null,
                selectOnRight = chartNameInput
            };
            songItems[i].Button.navigation = navigation;
        }

        SetNavigation(chartNameInput, left: null, right: levelInput, up: null, down: createButton);
        SetNavigation(levelInput, left: chartNameInput, right: null, up: null, down: createButton);
        SetNavigation(createButton, left: chartNameInput, right: null, up: levelInput, down: null);
    }

    private static void SetNavigation(Selectable target, Selectable left, Selectable right, Selectable up, Selectable down)
    {
        target.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = left,
            selectOnRight = right,
            selectOnUp = up,
            selectOnDown = down
        };
    }

    /// <summary>
    /// 곡 아이템이 선택되면 호출된다. 고른 곡 표시와 내 채보 패널 제목을 바꾼다.
    /// </summary>
    private void OnSongChosen(SongData song)
    {
        Btn_SongItem item = songItems.Find(view => view.Song == song);
        if (item == null || item == chosenItem) return;

        if (chosenItem != null) chosenItem.SetChosen(false);
        item.SetChosen(true);
        chosenItem = item;
        chosenSong = song;

        // 채보 이름 입력창에서 ←를 누르면 지금 고른 곡으로 돌아간다
        Navigation navigation = chartNameInput.navigation;
        navigation.selectOnLeft = item.Button;
        chartNameInput.navigation = navigation;

        RefreshMyChartPanel();
    }

    /// <summary>
    /// 내 채보 패널을 고른 곡 기준으로 갱신한다.
    /// TODO: 채보 JSON 저장 기능을 만든 뒤 chosenSong.SongId의 내 채보 목록을 연결한다.
    /// </summary>
    private void RefreshMyChartPanel()
    {
        myChartTitleText.text = $"{chosenSong.Title} - 내 채보";
        myChartEmptyText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 곡 아이템에서 Enter·클릭하면 채보 이름 입력창으로 포커스를 옮긴다.
    /// </summary>
    private void FocusChartNameInput()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(chartNameInput.gameObject);
    }

    /// <summary>
    /// 입력값을 검사하고, 통과하면 편집 씬으로 새 채보 정보를 넘긴다.
    /// </summary>
    private void OnClickCreate()
    {
        errorText.text = string.Empty;

        if (chosenSong == null)
        {
            errorText.text = "곡을 먼저 고르세요.";
            return;
        }

        string chartName = chartNameInput.text.Trim();
        if (string.IsNullOrEmpty(chartName))
        {
            errorText.text = "채보 이름을 입력하세요.";
            return;
        }

        int minLevel = GameConstants.Editor.MinLevel;
        int maxLevel = GameConstants.Editor.MaxLevel;
        if (!int.TryParse(levelInput.text, out int level) || level < minLevel || level > maxLevel)
        {
            errorText.text = $"레벨은 {minLevel}~{maxLevel} 사이 숫자로 입력하세요.";
            return;
        }

        EditorEntryData data = new EditorEntryData
        {
            songId = chosenSong.SongId,
            chartName = chartName,
            level = level,
            chartId = null
        };

        if (GameManager.Scene.Load(GameConstants.Scenes.Editor, data))
            SetInteractable(false);
    }

    private void OnClickExit()
    {
        ExitToTitle();
    }

    /// <summary>
    /// ESC: 입력창에 글자를 입력하는 중이면 입력만 끝내고, 아니면 타이틀로 나간다.
    /// </summary>
    private void OnCancel(InputAction.CallbackContext context)
    {
        TMP_InputField editingInput = GetEditingInput();
        if (editingInput != null)
        {
            // 입력한 글자는 그대로 두고 편집만 끝낸다 (선택은 입력창에 남아 방향키로 이어서 이동 가능)
            editingInput.DeactivateInputField();
            return;
        }

        ExitToTitle();
    }

    private TMP_InputField GetEditingInput()
    {
        if (chartNameInput.isFocused) return chartNameInput;
        if (levelInput.isFocused) return levelInput;
        return null;
    }

    private void ExitToTitle()
    {
        if (GameManager.Scene.Load(GameConstants.Scenes.Title))
            SetInteractable(false);
    }

    /// <summary>
    /// 씬 이동을 시작하면 버튼·입력창을 막아 중복 실행을 막는다.
    /// </summary>
    private void SetInteractable(bool interactable)
    {
        foreach (Btn_SongItem item in songItems)
            item.Button.interactable = interactable;

        chartNameInput.interactable = interactable;
        levelInput.interactable = interactable;
        createButton.interactable = interactable;
        exitButton.interactable = interactable;
    }
}
