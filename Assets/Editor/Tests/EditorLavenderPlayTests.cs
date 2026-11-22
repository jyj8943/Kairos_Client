using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 에디터 라벤더 톤 개편 (기획서 3-6, 11/19) 실제 씬 확인 (EnterPlayMode + Bootstrapper, Critical Velocity 새 채보).
/// L1. 테스트 플레이 배속: 저장값(2.5)으로 시작 → ▶ 한 번 → 2.6 저장 → 에디터를 다시 열면 2.6, 인게임 배속은 그대로
/// L2. "● 저장 안 됨": 처음 숨김 → 노트를 놓으면 보임 → 실행 취소로 저장 시점에 돌아오면 숨김
///     BPM 입력 창 ⑨ 안내: 평소(회색) / 뒤 노트 경고(호박 박스) / 오류 두 가지(같은 BPM · 범위 밖, 빨강 박스 + 추가 꺼짐)
/// PlayerData.json의 마지막 선택은 테스트 앞뒤로 되돌린다.
/// </summary>
public class EditorLavenderPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const int T = GameConstants.Editor.TicksPerBeat;

    private LastSelectionSaveData savedSelection;
    private Canvas_Editor canvas;
    private ChartEditSession session;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (savedSelection != null && PlayerDataManager.HasInstance)
        {
            GameManager.PlayerData.LastSelection.CopyFrom(savedSelection);
            GameManager.PlayerData.Save();
        }
        savedSelection = null;
        yield return new ExitPlayMode();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private IEnumerator OpenEditor(string chartName)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        if (savedSelection == null) savedSelection = GameManager.PlayerData.LastSelection.Clone();
        EditorScene before = Object.FindFirstObjectByType<EditorScene>();
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, new EditorEntryData { songId = TestCharts.OfficialSongId, chartName = chartName, level = 1 }));
        yield return null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 시작 대기 시간 초과");
            EditorScene scene = Object.FindFirstObjectByType<EditorScene>();
            if (scene != null && scene != before && !GameManager.Scene.IsLoading && Get<bool>(scene, "isReady")) break;
            yield return null;
        }
        canvas = Object.FindFirstObjectByType<Canvas_Editor>();
        session = Object.FindFirstObjectByType<ChartEditSession>();
    }

    [UnityTest]
    public IEnumerator L1_TestPlaySpeed_SavedAndRestored_InGameSpeedUntouched()
    {
        yield return WaitManagers();
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
        GameManager.PlayerData.SetLastEditorTestPlayMultiplier(2.5f);
        float inGameSpeed = GameManager.PlayerData.LastScrollMultiplier;

        yield return OpenEditor("배속 저장 확인");
        Assert.AreEqual(2.5f, canvas.TestPlayScrollMultiplier, 1e-4f, "저장값으로 시작");
        Assert.AreEqual("x2.5", Get<TMPro.TMP_Text>(canvas, "speedText").text);

        Get<Button>(canvas, "speedUpButton").onClick.Invoke();
        Assert.AreEqual(2.6f, canvas.TestPlayScrollMultiplier, 1e-4f);
        Assert.AreEqual(2.6f, GameManager.PlayerData.LastEditorTestPlayMultiplier, 1e-4f, "바꿀 때 저장");
        Assert.AreEqual(inGameSpeed, GameManager.PlayerData.LastScrollMultiplier, 1e-4f, "인게임 배속은 그대로");

        yield return OpenEditor("배속 저장 확인 2");
        Assert.AreEqual(2.6f, canvas.TestPlayScrollMultiplier, 1e-4f, "나갔다 들어오면 그 값");
        Debug.Log($"[EditorLavenderPlayTest] L1: 시작 x2.5 → ▶ x2.6 저장 → 다시 열기 x{canvas.TestPlayScrollMultiplier:0.0}, 인게임 배속 x{GameManager.PlayerData.LastScrollMultiplier:0.0} 그대로");
    }

    [UnityTest]
    public IEnumerator L2_UnsavedStatus_And_BpmPopupNotices()
    {
        yield return OpenEditor("저장 안 됨 확인");

        // "● 저장 안 됨": 변경이 있을 때만
        Assert.IsFalse(canvas.IsUnsavedShown, "처음(새 채보, 변경 없음)은 숨김");
        Assert.IsTrue(session.AddNote(0, 8 * T));
        Assert.IsTrue(canvas.IsUnsavedShown, "변경이 있으면 보임");
        Assert.AreEqual(UITheme.Current.editorAmber, Get<TMPro.TMP_Text>(canvas, "saveStatusText").color, "호박 글자");
        Assert.IsNotNull(session.Undo(), "실행 취소");
        Assert.IsFalse(canvas.IsUnsavedShown, "저장 시점으로 돌아오면 숨김");

        Popup_BpmSection popup = canvas.BpmSectionPopup;
        int total = session.Data.TotalTicks;

        // 평소: 뒤에 노트 없음 → 회색 안내, 추가 켜짐
        Assert.IsTrue(popup.Show(session.BpmSections, session.Timing, 16 * T, total, 0, (t, b) => { }));
        popup.BpmInput.text = "180";
        Assert.AreEqual(Popup_BpmSection.NoticeKind.Normal, popup.Notice);
        Assert.AreEqual(GameConstants.Editor.BpmSectionNoticeAdd, popup.NoticeText.text);
        Assert.AreEqual(Color.clear, popup.NoticeBox.color, "평소엔 박스 없음");
        Assert.IsTrue(popup.ConfirmButton.interactable);
        Assert.AreEqual(GameConstants.Editor.BpmSectionConfirmAdd, popup.ConfirmLabel);
        StringAssert.Contains("이전 구간", popup.NeighborLabel);
        popup.OnCancelRequested();
        yield return null;
        yield return new WaitUntilOrTimeout(() => !popup.IsOpen, 3f);

        // 뒤 노트 경고: 호박 박스 + 경고 문구, 추가 켜짐
        Assert.IsTrue(popup.Show(session.BpmSections, session.Timing, 16 * T, total, 3, (t, b) => { }));
        popup.BpmInput.text = "180";
        Assert.AreEqual(Popup_BpmSection.NoticeKind.NotesWarning, popup.Notice);
        Assert.AreEqual(string.Format(GameConstants.Editor.BpmSectionNotesWarningFormat, 3), popup.NoticeText.text);
        Assert.AreEqual(UITheme.Current.editorAmber.r, popup.NoticeBox.color.r, 1e-3f, "호박 박스");
        Assert.AreEqual(UITheme.Current.editorWarningBoxAlpha, popup.NoticeBox.color.a, 1e-3f, "호박 12%");
        Assert.IsTrue(popup.ConfirmButton.interactable);

        // 오류 1: 이전 구간과 같은 BPM → 빨강 박스 + 추가 꺼짐
        popup.BpmInput.text = SongData.FormatBpm(session.Bpm);
        Assert.AreEqual(Popup_BpmSection.NoticeKind.Error, popup.Notice);
        Assert.AreEqual(GameConstants.Editor.BpmSectionSameAsPreviousMessage, popup.NoticeText.text);
        Assert.AreEqual(UITheme.Current.danger.r, popup.NoticeBox.color.r, 1e-3f, "빨강 박스");
        Assert.AreEqual(UITheme.Current.editorErrorBoxAlpha, popup.NoticeBox.color.a, 1e-3f, "빨강 10%");
        Assert.AreEqual(UITheme.Current.danger, popup.InputFrame.color, "입력칸 빨간 테두리");
        Assert.IsFalse(popup.ConfirmButton.interactable, "추가 꺼짐");

        // 오류 2: 범위 밖
        popup.BpmInput.text = "500";
        Assert.AreEqual(Popup_BpmSection.NoticeKind.Error, popup.Notice);
        Assert.AreEqual(GameConstants.Editor.BpmSectionOutOfRangeMessage, popup.NoticeText.text);
        Assert.IsFalse(popup.ConfirmButton.interactable);
        popup.OnCancelRequested();
        yield return null;
        Debug.Log("[EditorLavenderPlayTest] L2: 저장 안 됨 숨김 → 보임 → 숨김, 입력 창 평소 / 노트 경고 / 같은 BPM 오류 / 범위 밖 오류 확인");
    }

    /// <summary>
    /// L3: 와이어프레임 맞춤 (11/19) — 바뀐 오브젝트가 Canvas 안, 글자 넘침 없음, 제목 한 줄. 표는 로그로 남긴다.
    /// BPM 구간 2개(곡 시작 + 마디 2 · 3박 180) 상태로 열고, 입력 창 4가지 상태의 보이는 오브젝트 · 버튼 상태도 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator L3_WireframeLayout_AndPopupStates()
    {
        yield return OpenEditor("새 채보 6");
        Assert.IsTrue(session.AddBpmSection(6 * T, 180f, out _));
        Assert.IsTrue(session.AddNote(0, 8 * T));
        yield return null;
        Canvas.ForceUpdateCanvases();

        RectTransform root = (RectTransform)canvas.transform;
        var log = new System.Text.StringBuilder("[EditorLavenderPlayTest] L3 배치 (Canvas " + root.rect.size + ")\n");
        string[] paths =
        {
            "TopBar/TitleRow", "TopBar/TitleRow/Txt_Title", "TopBar/TitleRow/Txt_TitleEn", "TopBar/Img_TitleLine", "TopBar/Img_TopDivider", "TopBar/Img_AlbumMask",
            "TopBar/Txt_SongTitle", "TopBar/ChartRow", "TopBar/Btn_Save", "TopBar/Btn_AutoTestPlay", "TopBar/Btn_TestPlay", "TopBar/Btn_Exit",
            "LeftTools/NoteCards", "LeftTools/Toggle_ShortNote", "LeftTools/Toggle_BpmSection", "LeftTools/Txt_ToolHint", "LeftTools/TestPlaySpeed",
            "RightPanel/Bpm/Txt_CurrentBpmTitle", "RightPanel/Bpm/Img_CurrentBpmBox", "RightPanel/Bpm/Img_CurrentBpmBox/Txt_NextBpm",
            "RightPanel/BpmSections/Txt_BpmSectionsTitle", "RightPanel/BpmSections/ScrollView_BpmSections", "RightPanel/Btn_ResetBpm",
            "RightPanel/Toggle_Metronome", "RightPanel/Toggle_KeySound", "GridArea/Txt_CurrentLineArrow", "BottomTimeline/Slider_Timeline",
            "BottomTimeline/Txt_TimeInfo", "BottomTimeline/Txt_TimeTotal", "KeyGuideBar"
        };
        Vector3[] rc = new Vector3[4];
        root.GetWorldCorners(rc);
        foreach (string p in paths)
        {
            RectTransform rt = (RectTransform)canvas.transform.Find(p);
            Assert.IsNotNull(rt, p);
            log.AppendLine(Describe(root, rt));
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            Assert.IsTrue(c[0].x >= rc[0].x - 0.5f && c[2].x <= rc[2].x + 0.5f && c[0].y >= rc[0].y - 0.5f && c[2].y <= rc[2].y + 0.5f, p + " Canvas 안");
            TMPro.TMP_Text t = rt.GetComponent<TMPro.TMP_Text>();
            if (t != null)
            {
                t.ForceMeshUpdate();
                Assert.IsFalse(t.isTextOverflowing, p + " 글자 넘침");
            }
        }
        TMPro.TMP_Text title = canvas.transform.Find("TopBar/TitleRow/Txt_Title").GetComponent<TMPro.TMP_Text>();
        TMPro.TMP_Text titleEn = canvas.transform.Find("TopBar/TitleRow/Txt_TitleEn").GetComponent<TMPro.TMP_Text>();
        Assert.AreEqual(1, title.textInfo.lineCount, "에디터 제목 한 줄");
        Assert.AreEqual(1, titleEn.textInfo.lineCount, "EDITOR 한 줄");
        Assert.Less(title.rectTransform.position.x, titleEn.rectTransform.position.x, "EDITOR는 제목 오른쪽 (가로 배치)");
        Assert.IsNull(canvas.transform.Find("LeftTools/Txt_SpeedHint"), "배속 안내 문구 삭제");
        Assert.IsNull(canvas.transform.Find("TopBar/Img_AutoTestShadow"), "자동 테스트 뒤 판 삭제");
        Assert.IsNull(canvas.transform.Find("TopBar/Img_TestPlayShadow"), "테스트 플레이 뒤 판 삭제");
        StringAssert.Contains($"{session.BpmSections.Count}개", Get<TMPro.TMP_Text>(canvas, "bpmSectionsTitleText").text, "BPM 구간 개수 (곡 구간 + 추가 1개)");
        Assert.IsTrue(Get<Button>(canvas, "resetBpmButton").interactable, "곡 BPM 구간과 다름 → 되돌리기 켜짐");
        TMPro.TMP_Text resetLabel = Get<TMPro.TMP_Text>(canvas, "resetBpmLabel");
        resetLabel.ForceMeshUpdate();
        Assert.AreEqual(GameConstants.Editor.ResetBpmButtonLabel, resetLabel.text, "되돌리기 버튼 글자");
        Assert.IsFalse(resetLabel.isTextOverflowing, "되돌리기 버튼 글자 넘침");
        log.AppendLine($"되돌리기 버튼 '{resetLabel.text}' 넘침 {resetLabel.isTextOverflowing}, 선호 폭 {resetLabel.preferredWidth:0.#} / 칸 {resetLabel.rectTransform.rect.width:0.#}");
        log.AppendLine("제목 lineCount " + title.textInfo.lineCount + " / " + titleEn.textInfo.lineCount + ", 구간 제목 '" + Get<TMPro.TMP_Text>(canvas, "bpmSectionsTitleText").text + "'");

        // 입력 창 4가지 상태
        Popup_BpmSection popup = canvas.BpmSectionPopup;
        Transform panel = popup.transform.Find("Img_Panel");
        TMPro.TMP_Text popupTitle = panel.Find("TitleRow/Txt_Title").GetComponent<TMPro.TMP_Text>();
        int total = session.Data.TotalTicks;
        (string name, int tick, int notesAfter, string input, Popup_BpmSection.NoticeKind kind, bool confirm)[] cases =
        {
            ("추가", 10 * T, 0, "200", Popup_BpmSection.NoticeKind.Normal, true),
            ("수정+경고", 6 * T, 12, "200", Popup_BpmSection.NoticeKind.NotesWarning, true),
            ("같은 BPM 오류", 4 * T, 0, SongData.FormatBpm(session.Bpm), Popup_BpmSection.NoticeKind.Error, false),
            ("범위 밖 오류", 4 * T, 0, "520", Popup_BpmSection.NoticeKind.Error, false)
        };
        foreach (var c in cases)
        {
            Assert.IsTrue(popup.Show(session.BpmSections, session.Timing, c.tick, total, c.notesAfter, (t, b) => { }), c.name + " 열림");
            popup.BpmInput.text = c.input;
            Canvas.ForceUpdateCanvases();
            popupTitle.ForceMeshUpdate();
            Assert.AreEqual(c.kind, popup.Notice, c.name);
            Assert.AreEqual(c.confirm, popup.ConfirmButton.interactable, c.name + " 확인 버튼");
            Assert.AreEqual(1, popupTitle.textInfo.lineCount, c.name + " 제목 한 줄");
            bool iconOn = panel.Find("Img_Notice/Img_NoticeIcon").gameObject.activeInHierarchy;
            Assert.AreEqual(c.kind != Popup_BpmSection.NoticeKind.Normal, iconOn, c.name + " ! 아이콘");
            popup.NoticeText.ForceMeshUpdate();
            Assert.IsFalse(popup.NoticeText.isTextOverflowing, c.name + " 안내 글자 넘침");
            log.AppendLine($"팝업 {c.name}: 제목 '{popup.TitleLabel}' · 버튼 '{popup.ConfirmLabel}' {(popup.ConfirmButton.interactable ? "켜짐" : "꺼짐")} · 안내 {popup.Notice} '{popup.NoticeText.text}' · ! {(iconOn ? "보임" : "숨김")} · 박스 알파 {popup.NoticeBox.color.a:0.00} · 입력 테두리 #{ColorUtility.ToHtmlStringRGB(popup.InputFrame.color)} · 이웃 '{popup.NeighborLabel}'");
            if (c.name == "추가")
            {
                foreach (string p in new[] { "TitleRow", "Img_TitleLine", "Img_PositionCard", "Txt_RangeHint", "Input_BpmSectionValue", "Txt_Neighbor", "Img_Notice", "Btn_Cancel", "Btn_Confirm", "KeyGuideBar" })
                    log.AppendLine("  " + Describe(root, (RectTransform)panel.Find(p)));
                log.AppendLine("  " + Describe(root, (RectTransform)panel));
            }
            popup.OnCancelRequested();
            yield return new WaitUntilOrTimeout(() => !popup.IsOpen, 3f);
            yield return null;
        }
        Debug.Log(log.ToString());
    }

    /// <summary>"이름 x a~b y c~d (w×h)" (1920×1080 화면 좌표, 위에서 아래)</summary>
    private static string Describe(RectTransform root, RectTransform rt)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector3 tl = root.InverseTransformPoint(c[1]);
        Vector3 br = root.InverseTransformPoint(c[3]);
        float x0 = tl.x + root.rect.width / 2f, y0 = root.rect.height / 2f - tl.y, x1 = br.x + root.rect.width / 2f, y1 = root.rect.height / 2f - br.y;
        TMPro.TMP_Text t = rt.GetComponent<TMPro.TMP_Text>();
        return $"{rt.name}: x {x0:0}~{x1:0} y {y0:0}~{y1:0} ({x1 - x0:0}×{y1 - y0:0})" + (t != null ? $" 넘침={t.isTextOverflowing} 줄={t.textInfo.lineCount}" : "");
    }

    private static IEnumerator WaitManagers()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
    }

    /// <summary>조건이 될 때까지 기다린다 (실제 초 timeout)</summary>
    private sealed class WaitUntilOrTimeout : CustomYieldInstruction
    {
        private readonly System.Func<bool> condition;
        private readonly float until;

        public WaitUntilOrTimeout(System.Func<bool> condition, float seconds)
        {
            this.condition = condition;
            until = Time.realtimeSinceStartup + seconds;
        }

        public override bool keepWaiting => !condition() && Time.realtimeSinceStartup < until;
    }
}
