using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 구간 추가 · 수정 창 (기획서 3-6 ⑧ · ⑨, 11/19 라벤더 톤). Canvas_Editor 안 마지막 자식에 두고, Canvas_Editor가 Init()을 호출한다.
/// - 3-7 팝업 공용 톤 (PopupBackdrop 블러 + 덮개, 패널 rgba(250,248,253) 86%, 모서리 20), 가로 640, 강조색은 호박(UITheme.editorAmber).
/// - 위에서부터: ♩ 아이콘(호박 14% 바탕) + "BPM 구간 추가"/"BPM 구간 수정" + BPM SECTION + 호박 짧은 선
///   → 위치 줄 ("마디 2 · 3박" + 오른쪽 시간) → 범위 안내 + BPM 입력칸(오른쪽 "BPM") → "이전 구간 150.61 · 다음 구간 180"(값 호박)
///   → ⑨ 안내 자리 → 취소 / 추가(수정) 버튼 → 키 안내 줄 (Enter 추가 · Esc 취소).
/// - ⑨ 안내 자리 (우선순위: 입력 오류 > 뒤 노트 경고 > 평소):
///   평소 = 회색 글자만 / 뒤에 노트 있음 = 호박 12% 박스 + ! + 경고 / 입력 오류 = 빨강 10% 박스 + ! + 오류 문구, 입력칸 빨간 2px 테두리, 오른쪽 버튼 꺼짐(회색).
/// - 입력칸: 평소 흐린 테두리, 입력 중 하늘 2px 테두리 + 하늘 18% 링, 오류면 빨간 2px 테두리 (입력 중 여부는 열려 있을 때만 매 프레임 본다).
/// - 확인(추가·수정)을 눌러야 적용하고, 취소·ESC면 아무것도 바꾸지 않는다. 입력할 때마다 바로 검사한다 (BpmSectionRules).
/// - 처음 선택은 BPM 입력창이고 바로 입력할 수 있게 포커스를 준다. Enter는 값이 올바르면 확인.
/// - ESC: 입력 중이면 UIManager가 입력만 끝내고(글자 유지), 다시 ESC면 OnCancelRequested()로 취소한다.
/// - 실제 적용은 EditorScene이 넘긴 onConfirm(tick, BPM)이 한다. 문구는 모두 GameConstants.Editor, 색은 UITheme.
/// </summary>
public class Popup_BpmSection : UIPopup
{
    /// <summary>⑨ 안내 자리 상태</summary>
    public enum NoticeKind
    {
        /// <summary>회색 평소 안내</summary>
        Normal,
        /// <summary>뒤에 노트가 있음 (호박 박스)</summary>
        NotesWarning,
        /// <summary>입력 오류 (빨강 박스)</summary>
        Error
    }

    [Header("제목")]
    [Tooltip("♩ 아이콘 바탕 (Img_Icon, 호박 14%)")]
    [SerializeField] private Image iconBackground;
    [Tooltip("♩ 아이콘 글자 (Img_Icon/Txt_Icon, 호박)")]
    [SerializeField] private TMP_Text iconText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text titleEnText;
    [Tooltip("제목 아래 호박 짧은 선 (Img_TitleLine)")]
    [SerializeField] private Image titleLine;

    [Header("위치 · 입력")]
    [SerializeField] private TMP_Text positionText;
    [Tooltip("위치 줄 오른쪽 시간 (Txt_PositionTime)")]
    [SerializeField] private TMP_Text positionTimeText;
    [Tooltip("입력칸 위 범위 안내 (Txt_RangeHint)")]
    [SerializeField] private TMP_Text rangeHintText;
    [Tooltip("BPM 입력 (Input_BpmSectionValue)")]
    [SerializeField] private TMP_InputField bpmInput;
    [Tooltip("입력칸 테두리 (Input_BpmSectionValue/Img_InputFrame, 2px)")]
    [SerializeField] private Image inputFrame;
    [Tooltip("입력 중 하늘 18% 링 (Img_InputRing, 입력칸 뒤)")]
    [SerializeField] private Image inputRing;
    [Tooltip("입력칸 오른쪽 \"BPM\" (Txt_Unit)")]
    [SerializeField] private TMP_Text unitText;
    [Tooltip("이전 · 다음 구간 BPM (Txt_Neighbor, 값은 호박)")]
    [SerializeField] private TMP_Text neighborText;

    [Header("⑨ 안내 자리")]
    [Tooltip("안내 박스 바탕 (Img_Notice). 평소에는 투명")]
    [SerializeField] private Image noticeBox;
    [Tooltip("안내 박스 왼쪽 ! (Txt_NoticeMark). 평소에는 숨김")]
    [SerializeField] private TMP_Text noticeMarkText;
    [SerializeField] private TMP_Text noticeText;

    [Header("버튼 · 키 안내")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private KeyGuideBar keyGuideBar;

    private const string GuideConfirmId = "confirm";

    private IReadOnlyList<BpmSection> sections;
    private TimingMap timing;
    private int tick;
    private int totalTicks;
    private int notesAfter;
    private Action<int, float> onConfirm;
    private BpmSectionError currentError;
    private float currentBpm;
    private bool isAnswered = true;
    private bool shownFocus;

    /// <summary>지금 입력 검사 결과 (확인용)</summary>
    public BpmSectionError CurrentError => currentError;

    /// <summary>수정 모드면 true (그 위치에 구간이 있음)</summary>
    public bool IsEditMode { get; private set; }

    /// <summary>창이 가리키는 tick (확인용)</summary>
    public int Tick => tick;

    /// <summary>BPM 입력창 (확인용)</summary>
    public TMP_InputField BpmInput => bpmInput;

    /// <summary>확인(추가·수정) 버튼 (확인용)</summary>
    public Button ConfirmButton => confirmButton;

    /// <summary>확인 버튼 글자 (확인용: "추가" / "수정")</summary>
    public string ConfirmLabel => confirmLabel != null ? confirmLabel.text : string.Empty;

    /// <summary>제목 글자 (확인용)</summary>
    public string TitleLabel => titleText != null ? titleText.text : string.Empty;

    /// <summary>⑨ 안내 글자 (확인용)</summary>
    public TMP_Text NoticeText => noticeText;

    /// <summary>⑨ 안내 상태 (확인용)</summary>
    public NoticeKind Notice { get; private set; }

    /// <summary>⑨ 안내 박스 (확인용)</summary>
    public Image NoticeBox => noticeBox;

    /// <summary>입력칸 테두리 (확인용)</summary>
    public Image InputFrame => inputFrame;

    /// <summary>이전 · 다음 구간 글자 (확인용)</summary>
    public string NeighborLabel => neighborText != null ? neighborText.text : string.Empty;

    /// <summary>
    /// 버튼·입력 리스너를 연결하고 고정 글자·색을 채운 뒤 닫힌 상태로 둔다.
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

        UITheme theme = UITheme.Current;
        if (iconBackground != null) iconBackground.color = UITheme.WithAlpha(theme.editorAmber, theme.editorAmberSoftAlpha);
        if (iconText != null)
        {
            iconText.text = GameConstants.Editor.BpmSectionIcon;
            iconText.color = theme.editorAmber;
        }
        if (titleEnText != null)
        {
            titleEnText.text = GameConstants.Editor.BpmSectionPopupTitleEn;
            titleEnText.color = theme.editorAmber;
        }
        if (titleLine != null) titleLine.color = theme.editorAmber;
        if (rangeHintText != null) rangeHintText.text = GameConstants.Editor.BpmSectionRangeHint;
        if (unitText != null) unitText.text = GameConstants.Editor.BpmSectionUnit;
        if (inputRing != null) inputRing.color = UITheme.WithAlpha(theme.accent, theme.selectedAlpha);
        if (cancelLabel != null) cancelLabel.text = GameConstants.Editor.BpmSectionCancel;
        if (noticeMarkText != null) noticeMarkText.text = GameConstants.Editor.BpmSectionNoticeMark;

        onConfirm = null;
        isAnswered = true;
        HideImmediate();
    }

    /// <summary>
    /// 창을 채우고 연다. tick에 구간이 있으면 수정 모드(그 BPM), 없으면 추가 모드(그 위치의 지금 BPM)로 미리 넣는다.
    /// </summary>
    /// <param name="timing">위치 줄 시간 표시용 박 ↔ 시간 (null이면 시간을 비운다)</param>
    /// <param name="notesAfter">그 위치 뒤에서 시간이 바뀌는 노트 수 (경고용)</param>
    /// <param name="onConfirm">확인: (tick, 반올림한 BPM)</param>
    /// <returns>열었으면 true. 이미 열려 있으면 false</returns>
    public bool Show(IReadOnlyList<BpmSection> sections, TimingMap timing, int tick, int totalTicks, int notesAfter, Action<int, float> onConfirm)
    {
        if (IsOpen) return false;

        this.sections = sections;
        this.timing = timing;
        this.tick = tick;
        this.totalTicks = totalTicks;
        this.notesAfter = notesAfter;
        this.onConfirm = onConfirm;
        isAnswered = false;

        int index = BpmSectionRules.IndexOf(sections, tick);
        IsEditMode = index >= 0;
        int ownIndex = IsEditMode ? index : BpmSectionRules.IndexAt(sections, tick);
        float bpm = sections[ownIndex].Bpm;

        titleText.text = IsEditMode ? GameConstants.Editor.BpmSectionPopupTitleEdit : GameConstants.Editor.BpmSectionPopupTitleAdd;
        confirmLabel.text = IsEditMode ? GameConstants.Editor.BpmSectionConfirmEdit : GameConstants.Editor.BpmSectionConfirmAdd;
        positionText.text = BpmSectionRules.FormatPosition(tick);
        if (positionTimeText != null)
            positionTimeText.text = timing != null ? TimeFormat.ToMinSecMillis((float)timing.BeatToSeconds(BeatGridMath.TickToBeat(tick))) : string.Empty;
        if (neighborText != null) neighborText.text = FormatNeighbors(sections, tick, IsEditMode, UITheme.Current.editorAmber);
        InitKeyGuide();

        bpmInput.SetTextWithoutNotify(SongData.FormatBpm(bpm));
        Validate(bpmInput.text);
        Open();

        // 바로 입력할 수 있게 포커스를 주고 글자를 모두 고른다
        bpmInput.ActivateInputField();
        bpmInput.Select();
        shownFocus = !bpmInput.isFocused;
        RefreshInputFrame();
        Debug.Log($"[Popup_BpmSection] 열림 ({(IsEditMode ? "수정" : "추가")}): {positionText.text}, BPM {bpmInput.text}, 뒤 노트 {notesAfter}개", this);
        return true;
    }

    /// <summary>
    /// "이전 구간 150.61 · 다음 구간 180" (순수 함수, 값은 호박 리치 텍스트). 이전 = 이 위치보다 앞 구간, 다음 = 뒤 구간. 없으면 "없음".
    /// </summary>
    public static string FormatNeighbors(IReadOnlyList<BpmSection> sections, int tick, bool isEditMode, Color valueColor)
    {
        int own = isEditMode ? BpmSectionRules.IndexOf(sections, tick) : BpmSectionRules.IndexAt(sections, tick);
        // 추가 모드면 tick이 들어갈 자리의 앞 구간이 own, 뒤 구간이 own + 1
        int previous = isEditMode ? own - 1 : own;
        int next = own + 1;
        string hex = ColorUtility.ToHtmlStringRGB(valueColor);
        string prevText = previous >= 0 && previous < sections.Count ? $"<color=#{hex}>{SongData.FormatBpm(sections[previous].Bpm)}</color>" : GameConstants.Editor.BpmSectionNeighborNone;
        string nextText = next >= 0 && next < sections.Count ? $"<color=#{hex}>{SongData.FormatBpm(sections[next].Bpm)}</color>" : GameConstants.Editor.BpmSectionNeighborNone;
        return string.Format(GameConstants.Editor.BpmSectionNeighborFormat, prevText, nextText);
    }

    private void InitKeyGuide()
    {
        if (keyGuideBar == null) return;
        keyGuideBar.Init(new[]
        {
            new KeyGuideEntry(IsEditMode ? GameConstants.Editor.BpmSectionConfirmEdit : GameConstants.Editor.BpmSectionConfirmAdd,
                KeyGuideKey.Fixed(GameConstants.Editor.KeyEnter)).WithId(GuideConfirmId),
            new KeyGuideEntry(GameConstants.Editor.BpmSectionCancel, KeyGuideKey.Fixed(GameConstants.Editor.KeyEsc))
        });
    }

    /// <summary>ESC = 취소 (입력 중이면 UIManager가 먼저 입력만 끝낸다)</summary>
    public override void OnCancelRequested()
    {
        Answer(false);
    }

    /// <summary>입력 글자를 검사해 ⑨ 안내·입력칸 테두리·확인 버튼을 바꾼다 (확인용으로도 부른다)</summary>
    public void Validate(string text)
    {
        BpmSectionError error = BpmSectionRules.TryParseBpm(text, out float bpm);
        if (error == BpmSectionError.None && sections != null)
            error = BpmSectionRules.ValidateAddOrSet(sections, tick, bpm, totalTicks);

        currentError = error;
        currentBpm = bpm;
        bool ok = error == BpmSectionError.None;

        if (!ok) SetNotice(NoticeKind.Error, BpmSectionRules.GetMessage(error));
        else if (notesAfter > 0) SetNotice(NoticeKind.NotesWarning, string.Format(GameConstants.Editor.BpmSectionNotesWarningFormat, notesAfter));
        else SetNotice(NoticeKind.Normal, IsEditMode ? GameConstants.Editor.BpmSectionNoticeEdit : GameConstants.Editor.BpmSectionNoticeAdd);

        confirmButton.interactable = ok;
        RefreshConfirmButton(ok);
        RefreshInputFrame();
    }

    /// <summary>⑨ 안내 자리를 상태에 맞게 칠한다</summary>
    private void SetNotice(NoticeKind kind, string message)
    {
        UITheme theme = UITheme.Current;
        Notice = kind;
        noticeText.text = message;
        switch (kind)
        {
            case NoticeKind.Error:
                noticeBox.color = UITheme.WithAlpha(theme.danger, theme.editorErrorBoxAlpha);
                noticeText.color = theme.danger;
                break;
            case NoticeKind.NotesWarning:
                noticeBox.color = UITheme.WithAlpha(theme.editorAmber, theme.editorWarningBoxAlpha);
                noticeText.color = theme.editorAmber;
                break;
            default:
                noticeBox.color = Color.clear;
                noticeText.color = theme.textMuted;
                break;
        }
        if (noticeMarkText != null)
        {
            noticeMarkText.gameObject.SetActive(kind != NoticeKind.Normal);
            noticeMarkText.color = noticeText.color;
        }
    }

    /// <summary>오른쪽 버튼: 올바르면 하늘 채움 + 흰 글자, 오류면 회색으로 꺼짐</summary>
    private void RefreshConfirmButton(bool ok)
    {
        UITheme theme = UITheme.Current;
        if (confirmButton.targetGraphic != null) confirmButton.targetGraphic.color = ok ? theme.accent : theme.divider;
        if (confirmLabel != null) confirmLabel.color = ok ? theme.onAccentText : theme.textMuted;
    }

    /// <summary>입력칸 테두리: 오류 빨강 2px, 입력 중 하늘 2px + 하늘 18% 링, 평소 흐린 선</summary>
    private void RefreshInputFrame()
    {
        UITheme theme = UITheme.Current;
        bool focused = bpmInput != null && bpmInput.isFocused;
        shownFocus = focused;
        bool error = currentError != BpmSectionError.None;
        if (inputFrame != null) inputFrame.color = error ? theme.danger : focused ? theme.accent : theme.divider;
        if (inputRing != null) inputRing.enabled = focused && !error;
    }

    /// <summary>입력 중 여부가 바뀌면 테두리를 다시 칠한다 (열려 있을 때만)</summary>
    private void Update()
    {
        if (!IsOpen || bpmInput == null) return;
        if (bpmInput.isFocused != shownFocus) RefreshInputFrame();
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
