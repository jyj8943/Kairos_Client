using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// BPM 구간 추가 · 수정 창 (기획서 3-6 ⑧-2, ⑧-3). Canvas_Editor 안 마지막 자식에 두고, Canvas_Editor가 Init()을 호출한다.
/// - 위치(읽기 전용) · BPM 입력 · 적용 범위 · 뒤 노트 경고 · 취소/확인. 확인을 눌러야 적용하고, 취소·ESC면 아무것도 바꾸지 않는다.
/// - 입력할 때마다 바로 검사한다 (BpmSectionRules). 오류면 안내 자리에 빨간 글씨, 입력창 테두리 빨강, 확인 버튼 비활성.
/// - 처음 선택은 BPM 입력창이고 바로 입력할 수 있게 포커스를 준다. Enter는 값이 올바르면 확인.
/// - ESC: 입력 중이면 UIManager가 입력만 끝내고(글자 유지), 다시 ESC면 OnCancelRequested()로 취소한다.
/// - 실제 적용은 EditorScene이 넘긴 onConfirm(tick, BPM)이 한다.
/// </summary>
public class Popup_BpmSection : UIPopup
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text positionText;
    [SerializeField] private TMP_Text positionHintText;
    [Tooltip("BPM 입력 (Input_BpmSectionValue)")]
    [SerializeField] private TMP_InputField bpmInput;
    [SerializeField] private Outline bpmInputOutline;
    [Tooltip("입력 옆 안내 (평소 회색 범위 안내, 오류면 빨간 오류 문구)")]
    [SerializeField] private TMP_Text bpmHintText;
    [SerializeField] private TMP_Text rangeText;
    [Tooltip("뒤 노트 경고 상자 (Img_Warning). 뒤에 노트가 없으면 숨긴다")]
    [SerializeField] private GameObject warningBox;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text deleteHintText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    [Header("색 (기획서 3-6)")]
    [SerializeField] private Color hintColor = new Color32(0x71, 0x71, 0x7A, 0xFF);
    [SerializeField] private Color errorColor = new Color32(0xDC, 0x26, 0x26, 0xFF);
    [SerializeField] private Color inputOutlineColor = new Color32(0x08, 0x91, 0xB2, 0xFF);

    private IReadOnlyList<BpmSection> sections;
    private int tick;
    private int totalTicks;
    private Action<int, float> onConfirm;
    private BpmSectionError currentError;
    private float currentBpm;
    private bool isAnswered = true;

    /// <summary>지금 입력 검사 결과 (확인용)</summary>
    public BpmSectionError CurrentError => currentError;

    /// <summary>수정 모드면 true (그 위치에 구간이 있음)</summary>
    public bool IsEditMode { get; private set; }

    /// <summary>창이 가리키는 tick (확인용)</summary>
    public int Tick => tick;

    /// <summary>BPM 입력창 (확인용)</summary>
    public TMP_InputField BpmInput => bpmInput;

    /// <summary>확인 버튼 (확인용)</summary>
    public Button ConfirmButton => confirmButton;

    /// <summary>안내 글자 (확인용)</summary>
    public TMP_Text BpmHintText => bpmHintText;

    /// <summary>
    /// 버튼·입력 리스너를 연결하고 닫힌 상태로 둔다.
    /// </summary>
    public void Init()
    {
        cancelButton.onClick.RemoveListener(OnClickCancel);
        cancelButton.onClick.AddListener(OnClickCancel);
        confirmButton.onClick.RemoveListener(OnClickConfirm);
        confirmButton.onClick.AddListener(OnClickConfirm);
        bpmInput.onValueChanged.RemoveListener(OnBpmChanged);
        bpmInput.onValueChanged.AddListener(OnBpmChanged);
        bpmInput.onSubmit.RemoveListener(OnBpmSubmitted);
        bpmInput.onSubmit.AddListener(OnBpmSubmitted);
        // ESC로 입력을 끝낼 때 글자를 남긴다 (CLAUDE.md 입력 규칙)
        bpmInput.restoreOriginalTextOnEscape = false;

        titleText.text = GameConstants.Editor.BpmSectionPopupTitle;
        positionHintText.text = GameConstants.Editor.BpmSectionPositionHint;
        deleteHintText.text = GameConstants.Editor.BpmSectionDeleteHint;

        onConfirm = null;
        isAnswered = true;
        HideImmediate();
    }

    /// <summary>
    /// 창을 채우고 연다. tick에 구간이 있으면 수정 모드(그 BPM), 없으면 추가 모드(그 위치의 지금 BPM)로 미리 넣는다.
    /// </summary>
    /// <param name="notesAfter">그 위치 뒤에서 시간이 바뀌는 노트 수 (경고용)</param>
    /// <param name="onConfirm">확인: (tick, 반올림한 BPM)</param>
    /// <returns>열었으면 true. 이미 열려 있으면 false</returns>
    public bool Show(IReadOnlyList<BpmSection> sections, int tick, int totalTicks, int notesAfter, Action<int, float> onConfirm)
    {
        if (IsOpen) return false;

        this.sections = sections;
        this.tick = tick;
        this.totalTicks = totalTicks;
        this.onConfirm = onConfirm;
        isAnswered = false;

        int index = BpmSectionRules.IndexOf(sections, tick);
        IsEditMode = index >= 0;
        float bpm = IsEditMode ? sections[index].Bpm : sections[BpmSectionRules.IndexAt(sections, tick)].Bpm;

        positionText.text = BpmSectionRules.FormatPosition(tick);
        rangeText.text = BpmSectionRules.FormatRange(sections, tick);
        warningBox.SetActive(notesAfter > 0);
        warningText.text = string.Format(GameConstants.Editor.BpmSectionNotesWarningFormat, notesAfter);

        bpmInput.SetTextWithoutNotify(SongData.FormatBpm(bpm));
        Validate(bpmInput.text);

        Open();
        // 바로 입력할 수 있게 포커스를 주고 글자를 모두 고른다
        bpmInput.ActivateInputField();
        bpmInput.Select();
        Debug.Log($"[Popup_BpmSection] 열림 ({(IsEditMode ? "수정" : "추가")}): {positionText.text}, BPM {bpmInput.text}, 뒤 노트 {notesAfter}개", this);
        return true;
    }

    /// <summary>ESC = 취소 (입력 중이면 UIManager가 먼저 입력만 끝낸다)</summary>
    public override void OnCancelRequested()
    {
        Answer(false);
    }

    /// <summary>입력 글자를 검사해 안내·테두리·확인 버튼을 바꾼다 (확인용으로도 부른다)</summary>
    public void Validate(string text)
    {
        BpmSectionError error = BpmSectionRules.TryParseBpm(text, out float bpm);
        if (error == BpmSectionError.None && sections != null)
            error = BpmSectionRules.ValidateAddOrSet(sections, tick, bpm, totalTicks);

        currentError = error;
        currentBpm = bpm;
        bool ok = error == BpmSectionError.None;
        bpmHintText.text = ok ? GameConstants.Editor.BpmSectionInputHint : BpmSectionRules.GetMessage(error);
        bpmHintText.color = ok ? hintColor : errorColor;
        if (bpmInputOutline != null) bpmInputOutline.effectColor = ok ? inputOutlineColor : errorColor;
        confirmButton.interactable = ok;
    }

    private void OnBpmChanged(string text)
    {
        Validate(text);
    }

    /// <summary>Enter: 값이 올바르면 확인</summary>
    private void OnBpmSubmitted(string text)
    {
        Validate(text);
        if (currentError == BpmSectionError.None) Answer(true);
    }

    private void OnClickCancel()
    {
        Answer(false);
    }

    private void OnClickConfirm()
    {
        Validate(bpmInput.text);
        if (currentError == BpmSectionError.None) Answer(true);
    }

    /// <summary>한 번만 답한다: 먼저 닫고, 확인이면 콜백을 부른다</summary>
    private void Answer(bool confirmed)
    {
        if (isAnswered || !IsOpen) return;

        isAnswered = true;
        Action<int, float> callback = onConfirm;
        onConfirm = null;
        int answeredTick = tick;
        float bpm = currentBpm;

        if (bpmInput.isFocused) bpmInput.DeactivateInputField();
        Close();
        Debug.Log($"[Popup_BpmSection] {(confirmed ? $"확인: BPM {SongData.FormatBpm(bpm)}" : "취소")}", this);
        if (confirmed) callback?.Invoke(answeredTick, bpm);
    }

    private void OnDestroy()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnClickCancel);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
        if (bpmInput != null)
        {
            bpmInput.onValueChanged.RemoveListener(OnBpmChanged);
            bpmInput.onSubmit.RemoveListener(OnBpmSubmitted);
        }
    }
}
