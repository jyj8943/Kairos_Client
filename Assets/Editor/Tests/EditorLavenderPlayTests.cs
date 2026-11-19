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
