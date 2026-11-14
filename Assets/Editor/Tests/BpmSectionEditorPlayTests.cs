using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 에디터 BPM 구간 실제 씬 확인 (EnterPlayMode + Bootstrapper, Critical Velocity 새 채보).
/// 9. BPM 구간 도구 클릭 → 창 → 확인이면 추가, 취소면 그대로, 잘못된 값이면 확인 버튼 비활성
/// 10. 구간선 오른쪽 클릭: 뒤 노트가 있으면 확인 팝업, 없으면 바로 삭제. 곡 시작 구간은 아무 일도 없음
/// 11. 화면 개편 뒤 Missing 참조가 없고 지운 오브젝트가 없음, 목록·타임라인 눈금·현재 BPM이 구간을 따름
/// 12. 테스트 플레이로 구간이 전부 넘어가고(인게임 판정 시간), 돌아오면 구간·실행 취소 기록이 남음
/// </summary>
public class BpmSectionEditorPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const int T = GameConstants.Editor.TicksPerBeat;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private EditorScene editorScene;
    private ChartEditSession session;
    private ChartGridView grid;
    private EditorNotePlacer placer;
    private Canvas_Editor canvas;
    private GraphicRaycaster raycaster;
    private Camera eventCamera;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return OpenEditor();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return new ExitPlayMode();
    }

    private IEnumerator OpenEditor()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, new EditorEntryData { songId = Song, chartName = "BPM 구간 확인", level = 1 }));
        yield return WaitEditor();
    }

    private IEnumerator WaitEditor()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        yield return null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 시작 대기 시간 초과");
            editorScene = Object.FindFirstObjectByType<EditorScene>();
            if (editorScene != null && !GameManager.Scene.IsLoading && Get<bool>(editorScene, "isReady")) break;
            yield return null;
        }
        session = Object.FindFirstObjectByType<ChartEditSession>();
        grid = Object.FindFirstObjectByType<ChartGridView>();
        placer = Object.FindFirstObjectByType<EditorNotePlacer>();
        canvas = Object.FindFirstObjectByType<Canvas_Editor>();
        Canvas root = grid.GridRect.GetComponentInParent<Canvas>().rootCanvas;
        raycaster = root.GetComponent<GraphicRaycaster>();
        eventCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        grid.SetSnap(4);
        grid.SetGridHeight(1f);
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private Vector2 ScreenPoint(float beat)
    {
        Rect rect = grid.GridRect.rect;
        Vector3 local = new Vector3(rect.xMin + 1.5f * grid.LaneWidth, rect.yMin + grid.BeatToLocalY(beat), 0f);
        return RectTransformUtility.WorldToScreenPoint(eventCamera, grid.GridRect.TransformPoint(local));
    }

    private void Click(float beat, PointerEventData.InputButton button)
    {
        PointerEventData data = new PointerEventData(EventSystem.current) { position = ScreenPoint(beat), button = button };
        RaycastResult raycast = new RaycastResult { module = raycaster };
        data.pointerPressRaycast = raycast;
        data.pointerCurrentRaycast = raycast;
        ExecuteEvents.Execute(placer.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(placer.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(placer.gameObject, data, ExecuteEvents.pointerClickHandler);
    }

    /// <summary>9. BPM 구간 도구 클릭 → 창 → 확인이면 추가 / 취소면 그대로 / 잘못된 값이면 확인 버튼 비활성</summary>
    [UnityTest]
    public IEnumerator P9_ClickPopupConfirmCancel()
    {
        canvas.SetTool(EditorTool.BpmSection);
        Assert.AreEqual(EditorTool.BpmSection, canvas.CurrentTool);
        grid.SetCurrentBeat(14f);
        yield return null;

        Click(16f, PointerEventData.InputButton.Left);
        yield return null;
        Popup_BpmSection popup = canvas.BpmSectionPopup;
        Assert.IsTrue(popup.IsOpen, "창이 열림");
        Assert.IsTrue(GameManager.UI.HasOpenPopup);
        Assert.IsFalse(popup.IsEditMode, "빈 곳 → 추가 모드");
        Assert.AreEqual(16 * T, popup.Tick);
        Assert.AreEqual(0, session.Data.Notes.Count, "BPM 구간 도구는 노트를 찍지 않는다");
        Assert.AreEqual(SongData.FormatBpm(session.Bpm), popup.BpmInput.text, "그 위치의 지금 BPM을 미리 넣는다");
        Assert.AreEqual(BpmSectionError.SameAsPrevious, popup.CurrentError, "지금 BPM 그대로면 이전과 같음");
        Assert.IsFalse(popup.ConfirmButton.interactable);

        popup.BpmInput.text = "500";
        Assert.AreEqual(BpmSectionError.OutOfRange, popup.CurrentError);
        Assert.IsFalse(popup.ConfirmButton.interactable, "잘못된 값이면 확인 버튼 비활성");
        Assert.AreEqual(GameConstants.Editor.BpmSectionOutOfRangeMessage, popup.BpmHintText.text);
        Assert.AreEqual((Color)new Color32(0xDC, 0x26, 0x26, 0xFF), popup.BpmHintText.color, "빨간 글씨");

        popup.BpmInput.text = "200";
        Assert.AreEqual(BpmSectionError.None, popup.CurrentError);
        Assert.IsTrue(popup.ConfirmButton.interactable);
        Assert.AreEqual(GameConstants.Editor.BpmSectionInputHint, popup.BpmHintText.text);
        popup.ConfirmButton.onClick.Invoke();
        yield return null;

        Assert.IsFalse(popup.IsOpen);
        Assert.AreEqual(2, session.BpmSections.Count, "확인 → 추가");
        Assert.AreEqual(16 * T, session.BpmSections[1].Tick);
        Assert.AreEqual(200f, session.BpmSections[1].Bpm);
        Assert.IsTrue(session.IsDirty);

        // 취소: 아무것도 생기지 않음 (ESC와 같은 OnCancelRequested)
        Click(24f, PointerEventData.InputButton.Left);
        yield return null;
        Assert.IsTrue(popup.IsOpen);
        popup.BpmInput.text = "175";
        popup.OnCancelRequested();
        yield return null;
        Assert.AreEqual(2, session.BpmSections.Count, "취소 → 추가 안 됨");

        // 구간선 위 클릭 → 수정 모드
        Click(16f, PointerEventData.InputButton.Left);
        yield return null;
        Assert.IsTrue(popup.IsEditMode);
        Assert.AreEqual("200", popup.BpmInput.text);
        popup.BpmInput.text = "180";
        popup.ConfirmButton.onClick.Invoke();
        yield return null;
        Assert.AreEqual(180f, session.BpmSections[1].Bpm);

        // Ctrl+Z와 같은 경로로 되돌리기
        Assert.AreEqual(GameConstants.Editor.HistorySetBpmSection, session.Undo());
        Assert.AreEqual(200f, session.BpmSections[1].Bpm);
        Debug.Log("[BpmSectionEditorPlayTest] P9: 클릭 → 창 → 확인 추가 · 취소 · 수정 · 잘못된 값 확인 비활성");
    }

    /// <summary>10. 구간선 오른쪽 클릭: 뒤 노트가 있으면 확인 팝업, 없으면 바로 삭제. 곡 시작 구간은 아무 일도 없음</summary>
    [UnityTest]
    public IEnumerator P10_RightClickRemove()
    {
        Assert.IsTrue(session.AddBpmSection(16 * T, 200f, out _));
        Assert.IsTrue(session.AddBpmSection(32 * T, session.Bpm, out _));
        canvas.SetTool(EditorTool.BpmSection);
        grid.SetCurrentBeat(30f);
        yield return null;

        // 뒤에 노트 없음 → 바로 삭제
        Click(32f, PointerEventData.InputButton.Right);
        yield return null;
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "뒤 노트가 없으면 확인 없이");
        Assert.AreEqual(2, session.BpmSections.Count);

        // 뒤에 노트 있음 → 확인 팝업 → 삭제
        Assert.IsTrue(session.AddBpmSection(32 * T, session.Bpm, out _));
        session.AddNote(0, 20 * T);
        session.AddNote(1, 40 * T);
        grid.SetCurrentBeat(14f);
        yield return null;
        Click(16f, PointerEventData.InputButton.Right);
        yield return null;
        Popup_Confirm confirm = GameManager.UI.Get<Popup_Confirm>();
        Assert.IsTrue(confirm.IsOpen, "뒤 노트가 있으면 확인 팝업");
        string message = Get<TMP_Text>(confirm, "messageText").text;
        StringAssert.Contains("뒤에 있는 노트 2개", message);
        StringAssert.Contains("같은 BPM이 된 다음 구간도 함께 삭제됩니다.", message, "뒤 구간이 같이 지워지는 경우 한 줄 더");
        Get<Button>(confirm, "confirmButton").onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, session.BpmSections.Count, "16박 구간 + 같은 BPM이 된 32박 구간");
        Assert.AreEqual(2, session.Data.Notes.Count, "노트는 그대로");

        // 곡 시작 구간 → 아무 일도 없음
        grid.SetCurrentBeat(0f);
        yield return null;
        Click(0f, PointerEventData.InputButton.Right);
        yield return null;
        Assert.IsFalse(GameManager.UI.HasOpenPopup);
        Assert.AreEqual(1, session.BpmSections.Count);

        // 한 번에 되돌리기
        session.Undo();
        Assert.AreEqual(3, session.BpmSections.Count);
        Debug.Log($"[BpmSectionEditorPlayTest] P10: 확인 문구 \"{message.Replace("\n", " / ")}\"");
    }

    /// <summary>11. Missing 참조 없음, 지운 오브젝트 없음, 목록·눈금·현재 BPM·라벨이 구간을 따름, 단축키 대상 도구</summary>
    [UnityTest]
    public IEnumerator P11_LayoutAndReferences()
    {
        int missing = 0;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null) { missing++; continue; }
        }
        Assert.AreEqual(0, missing, "Missing 스크립트 없음");

        Transform canvasT = canvas.transform;
        Assert.IsNull(canvasT.Find("LeftTools/Toggle_Select"));
        Assert.IsNull(canvasT.Find("LeftTools/Toggle_Eraser"));
        Assert.IsNull(canvasT.Find("RightPanel/PlaybackSpeed"));
        Assert.IsNull(canvasT.Find("RightPanel/SongLength"));
        Assert.IsNotNull(canvasT.Find("LeftTools/Toggle_BpmSection"));
        Assert.IsNotNull(canvasT.Find("LeftTools/Img_NoteCountBox/Txt_NoteCount"));

        session.AddNote(0, 8 * T);
        session.AddLongNote(1, 10 * T, 12 * T, out _);
        Assert.AreEqual("노트 2\n<size=80%><color=#52525B>롱노트 1</color></size>", Get<TMP_Text>(canvas, "noteCountText").text);

        Assert.IsTrue(session.AddBpmSection(64 * T, 200f, out _));
        Assert.IsTrue(session.AddBpmSection(98 * T, 175f, out _));
        yield return null;
        ScrollView_BpmSections list = canvas.BpmSectionList;
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual("마디 1 · 1박", list.GetItem(0).PositionText.text);
        Assert.AreEqual("마디 25 · 3박", list.GetItem(2).PositionText.text);
        Assert.AreEqual(0, list.CurrentIndex, "현재 위치(0박)가 속한 구간 강조");
        Assert.AreEqual("다음: 마디 17 → 200", Get<TMP_Text>(canvas, "nextBpmText").text);
        Assert.IsTrue(Get<Button>(canvas, "resetBpmButton").interactable);

        // 목록 줄 클릭 → 그 구간이 현재 위치로
        list.GetItem(1).GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual(64f, grid.CurrentBeat, 1e-4f);
        Assert.AreEqual(1, list.CurrentIndex);
        Assert.AreEqual("200", Get<TMP_Text>(canvas, "currentBpmText").text);
        Assert.IsTrue(list.GetItem(1).IsHighlighted);
        Assert.AreEqual("다음: 마디 25 3박 → 175", Get<TMP_Text>(canvas, "nextBpmText").text);

        Slider_Timeline timeline = Object.FindFirstObjectByType<Slider_Timeline>();
        Assert.AreEqual(2, timeline.MarkerCount, "곡 시작 구간은 눈금 없음");
        Assert.AreEqual("200", timeline.GetMarker(0).GetComponentInChildren<TMP_Text>().text);

        EditorBpmSectionLayer layer = Object.FindFirstObjectByType<EditorBpmSectionLayer>();
        Assert.GreaterOrEqual(layer.ActiveCount, 1, "보이는 구간선");
        Assert.AreEqual("♩ 200 BPM", layer.GetLabelText(1));
        Assert.AreEqual($"{SongData.FormatBpm(session.SongDefaultBpm)} → 200", layer.GetChangeText(1));
        Assert.AreEqual(GameConstants.Editor.BpmSectionStartLabel, layer.GetChangeText(0));

        // 되돌리기 버튼 → 확인 팝업 → 곡 기본 BPM 1개, 버튼 비활성
        Get<Button>(canvas, "resetBpmButton").onClick.Invoke();
        yield return null;
        Popup_Confirm confirm = GameManager.UI.Get<Popup_Confirm>();
        Assert.IsTrue(confirm.IsOpen);
        StringAssert.Contains($"곡의 기본 BPM({SongData.FormatBpm(session.SongDefaultBpm)})", Get<TMP_Text>(confirm, "messageText").text);
        Get<Button>(confirm, "confirmButton").onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, session.BpmSections.Count);
        Assert.IsFalse(Get<Button>(canvas, "resetBpmButton").interactable);
        Assert.AreEqual(0, timeline.MarkerCount);

        // 도구 단축키 경로 (1 · 2 · 3)
        canvas.SetTool(EditorTool.LongNote);
        Assert.AreEqual(EditorTool.LongNote, canvas.CurrentTool);
        Toggle bpmToggle = Get<Toggle>(canvas, "bpmSectionToggle");
        Assert.AreEqual((Color)new Color32(0xFF, 0xFF, 0xFF, 0xFF), bpmToggle.targetGraphic.color, "선택 안 된 도구: 흰 바탕");
        canvas.SetTool(EditorTool.BpmSection);
        Assert.AreEqual((Color)new Color32(0xFF, 0xF7, 0xED, 0xFF), bpmToggle.targetGraphic.color, "선택된 도구: 연한 주황 바탕");
        Assert.AreEqual((Color)new Color32(0xC2, 0x41, 0x0C, 0xFF), bpmToggle.GetComponentInChildren<TMP_Text>().color);
        Debug.Log("[BpmSectionEditorPlayTest] P11: Missing 0, 목록·눈금·현재 BPM·라벨·되돌리기·도구 색 확인");
    }

    /// <summary>12. 테스트 플레이로 구간이 전부 넘어가고(인게임 판정 시간), ESC로 돌아오면 구간·실행 취소 기록이 남음</summary>
    [UnityTest]
    public IEnumerator P12_TestPlayRoundTrip()
    {
        Assert.IsTrue(session.AddBpmSection(8 * T, 300f, out _));
        session.AddNote(0, 10 * T);
        int undoCount = session.UndoCount;
        double editorNoteTime = session.Timing.BeatToSeconds(10);

        MethodInfo start = typeof(EditorScene).GetMethod("OnTestPlayRequested", Private);
        start.Invoke(editorScene, null);
        float deadline = Time.realtimeSinceStartup + 20f;
        InGameScene inGame;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            inGame = Object.FindFirstObjectByType<InGameScene>();
            if (inGame != null && !GameManager.Scene.IsLoading && Get<bool>(inGame, "isPlaying")) break;
            yield return null;
        }

        ChartData chart = Get<ChartData>(inGame, "chart");
        Assert.AreEqual(2, chart.timingPoints.Count, "구간이 전부 넘어감");
        TimingMap timing = Get<TimingMap>(inGame, "timing");
        double inGameNoteTime = timing.BeatToSeconds(10);
        NoteJudge judge = Get<NoteJudge>(inGame, "judge");
        Assert.AreEqual(1, judge.Notes.Count);
        Assert.AreEqual(timing.BeatToSeconds(10), judge.Notes[0].Time, 1e-6, "노트 판정 시간 = 구간별 BPM으로 계산한 시간");
        Assert.AreEqual(editorNoteTime + (timing.OffsetMs - session.OffsetMs) / 1000.0, inGameNoteTime, 1e-6, "에디터와 같은 박 → 시간 (사용자 오프셋 차이만)");

        MethodInfo exit = typeof(InGameScene).GetMethod("ExitToReturnTarget", Private);
        exit.Invoke(inGame, null);
        yield return WaitEditor();

        Assert.AreEqual(2, session.BpmSections.Count, "돌아와도 구간 유지");
        Assert.AreEqual(300f, session.BpmSections[1].Bpm);
        Assert.AreEqual(undoCount, session.UndoCount, "실행 취소 기록 유지");
        Assert.AreEqual(GameConstants.Editor.HistoryAddShortNote, session.Undo());
        Assert.AreEqual(GameConstants.Editor.HistoryAddBpmSection, session.Undo());
        Assert.AreEqual(1, session.BpmSections.Count);
        Debug.Log($"[BpmSectionEditorPlayTest] P12: 인게임 timingPoints {chart.timingPoints.Count}개, 노트 시간 {inGameNoteTime:0.000}초 (에디터 {editorNoteTime:0.000}초), 돌아와서 실행 취소 {undoCount}개 유지");
    }
}
