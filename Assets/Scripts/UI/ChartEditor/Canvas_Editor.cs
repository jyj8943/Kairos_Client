using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 채보 편집 화면 UI. EditorScene이 그리드 Init 뒤에 Init()을 호출한다.
/// - 오른쪽 패널(박자 스냅·그리드 높이·노트 수)과 타임라인 마디 표시를 그리드·노트 상태에 맞춰 갱신한다.
/// - 왼쪽 도구 토글 상태를 CurrentTool로 알려준다 (도구 동작은 각 처리 쪽에서 이 값을 읽는다).
/// - 마우스 휠은 입력 에셋의 ChartEditor 맵으로 받는다. 포인터가 그리드 위에 있을 때만 처리한다.
///   (휠: 현재 beat를 스냅 한 칸씩 이동, Ctrl+휠: 그리드 높이 배율 변경)
/// - 폴더 이름을 Editor로 두면 Unity 특수 폴더가 되므로 UI/ChartEditor/에 둔다.
/// </summary>
public class Canvas_Editor : MonoBehaviour
{
    /// <summary>
    /// 음원 재생을 연결하기 전까지 Txt_TimeInfo에 그대로 둘 시간 표시.
    /// TODO: 음원 재생 연결 시 현재 시간 / 곡 길이로 교체
    /// </summary>
    private const string TimeTextPlaceholder = "0:00.000 / 0:00";

    [Header("그리드")]
    [Tooltip("휠 입력을 받을 영역 (Img_Grid)")]
    [SerializeField] private RectTransform gridRect;

    [Header("왼쪽 도구")]
    [SerializeField] private Toggle shortNoteToggle;
    [SerializeField] private Toggle longNoteToggle;
    [SerializeField] private Toggle selectToggle;
    [SerializeField] private Toggle eraserToggle;

    [Header("오른쪽 패널")]
    [SerializeField] private TMP_Dropdown beatSnapDropdown;
    [SerializeField] private TMP_Text gridHeightText;
    [SerializeField] private TMP_Text noteCountText;

    [Header("타임라인")]
    [SerializeField] private TMP_Text timeInfoText;

    [Header("입력")]
    [Tooltip("입력 에셋의 ChartEditor/Scroll (마우스 휠 y)")]
    [SerializeField] private InputActionReference scrollAction;
    [Tooltip("입력 에셋의 ChartEditor/ZoomModifier (Ctrl)")]
    [SerializeField] private InputActionReference zoomModifierAction;
    [Tooltip("입력 에셋의 ChartEditor/Point (포인터 위치)")]
    [SerializeField] private InputActionReference pointAction;

    private Canvas canvas;
    private ChartGridView grid;
    private ChartEditData data;
    private bool isInputEnabled;

    /// <summary>
    /// 지금 켜진 도구. 토글이 모두 꺼져 있으면 숏노트로 본다 (ToggleGroup이 하나는 켜 두므로 보통 생기지 않음).
    /// </summary>
    public EditorTool CurrentTool
    {
        get
        {
            if (longNoteToggle != null && longNoteToggle.isOn) return EditorTool.LongNote;
            if (selectToggle != null && selectToggle.isOn) return EditorTool.Select;
            if (eraserToggle != null && eraserToggle.isOn) return EditorTool.Eraser;
            return EditorTool.ShortNote;
        }
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    /// <summary>
    /// 그리드·노트 상태로 박자 스냅 드롭다운과 표시 텍스트를 맞추고, 드롭다운·노트 변경을 구독한다.
    /// 노트는 ChartEditSession.Data를 읽기만 한다.
    /// </summary>
    public void Init(ChartGridView grid, ChartEditSession session)
    {
        this.grid = grid;

        if (data != null) data.OnChanged -= RefreshNoteCountText;
        data = session.Data;
        data.OnChanged += RefreshNoteCountText;

        InitBeatSnapDropdown();
        RefreshGridHeightText();
        RefreshTimeInfoText();
        RefreshNoteCountText();
    }

    /// <summary>
    /// 휠 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (scrollAction == null || zoomModifierAction == null || pointAction == null)
        {
            Debug.LogWarning("[Canvas_Editor] 입력 액션이 연결되지 않아 휠 조작을 받을 수 없습니다.", this);
            return;
        }

        scrollAction.action.performed += OnScroll;
        scrollAction.action.Enable();
        zoomModifierAction.action.Enable();
        pointAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// 휠 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        scrollAction.action.performed -= OnScroll;
        isInputEnabled = false;
    }

    private void OnDestroy()
    {
        DisableInput();

        if (beatSnapDropdown != null) beatSnapDropdown.onValueChanged.RemoveListener(OnBeatSnapChanged);
        if (data != null) data.OnChanged -= RefreshNoteCountText;
    }

    /// <summary>
    /// 드롭다운 선택지를 GameConstants의 스냅 목록(표시 이름)으로 채우고 그리드의 현재 스냅을 고른다.
    /// </summary>
    private void InitBeatSnapDropdown()
    {
        GameConstants.Editor.SnapOption[] snaps = GameConstants.Editor.SnapOptions;
        List<string> options = new List<string>(snaps.Length);
        int selected = 0;
        for (int i = 0; i < snaps.Length; i++)
        {
            options.Add(snaps[i].Label);
            if (snaps[i].Division == grid.SnapDivision) selected = i;
        }

        beatSnapDropdown.onValueChanged.RemoveListener(OnBeatSnapChanged);
        beatSnapDropdown.ClearOptions();
        beatSnapDropdown.AddOptions(options);
        beatSnapDropdown.SetValueWithoutNotify(selected);
        beatSnapDropdown.RefreshShownValue();
        beatSnapDropdown.onValueChanged.AddListener(OnBeatSnapChanged);
    }

    private void OnBeatSnapChanged(int index)
    {
        GameConstants.Editor.SnapOption[] snaps = GameConstants.Editor.SnapOptions;
        if (index < 0 || index >= snaps.Length) return;

        grid.SetSnap(snaps[index].Division);
    }

    /// <summary>
    /// 휠 한 번에 한 칸. Windows는 한 칸에 120씩 들어오므로 값의 부호만 쓴다.
    /// 휠을 올리면 뒤 시간(위)으로 간다.
    /// </summary>
    private void OnScroll(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        if (Mathf.Approximately(value, 0f) || grid == null || !IsPointerOverGrid()) return;

        int direction = value > 0f ? 1 : -1;

        if (zoomModifierAction.action.IsPressed())
        {
            grid.SetGridHeight(grid.GridHeightScale + direction * GameConstants.Editor.GridHeightStep);
            RefreshGridHeightText();
        }
        else
        {
            grid.SetCurrentBeat(BeatGridMath.StepBeat(grid.CurrentBeat, grid.SnapDivision, direction));
            RefreshTimeInfoText();
        }
    }

    private bool IsPointerOverGrid()
    {
        Vector2 pointer = pointAction.action.ReadValue<Vector2>();
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(gridRect, pointer, eventCamera);
    }

    /// <summary>
    /// "x1.5 (Ctrl+휠)" 형식으로 그리드 높이 배율을 표시한다.
    /// </summary>
    private void RefreshGridHeightText()
    {
        string scale = grid.GridHeightScale.ToString("0.0#", CultureInfo.InvariantCulture);
        gridHeightText.text = $"x{scale} (Ctrl+휠)";
    }

    /// <summary>
    /// "N (롱 M)" 형식으로 노트 수를 표시한다. N은 숏노트와 롱노트를 합한 수다.
    /// </summary>
    private void RefreshNoteCountText()
    {
        noteCountText.text = $"{data.ShortCount + data.LongCount} (롱 {data.LongCount})";
    }

    /// <summary>
    /// 타임라인 정보의 마디 부분만 갱신한다. 시간 부분은 음원 재생을 연결할 때 바꾼다.
    /// </summary>
    private void RefreshTimeInfoText()
    {
        timeInfoText.text = $"{TimeTextPlaceholder} · 마디 {grid.CurrentMeasure}";
    }
}
