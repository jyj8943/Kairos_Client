using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 롱노트 에디터 동작 자동 확인. 실제 EditorScene을 Critical Velocity 새 채보로 열고,
/// GridArea(EditorNotePlacer)에 EventSystem 포인터 이벤트(ExecuteEvents: 누름·드래그·놓음·클릭)를 보내 실제 UI 경로로 확인한다.
/// - 화면 좌표는 ChartGridView의 박 → y 변환(BeatToLocalY)으로 계산한다.
/// - 저장 테스트(E5)는 저장 경로를 바꿀 수 없어(ChartRepository가 persistentDataPath/MyCharts 고정) 실제 곡 폴더에 저장하고,
///   만든 파일만 TearDown에서 지운다. 저장 전후 폴더의 다른 파일은 건드리지 않았는지 확인한다.
/// </summary>
public class LongNoteEditorSceneTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const int T = GameConstants.Editor.TicksPerBeat;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private EditorScene editorScene;
    private ChartEditSession session;
    private ChartGridView grid;
    private EditorNoteLayer noteLayer;
    private EditorNotePlacer placer;
    private Canvas_Editor canvas;
    private GraphicRaycaster raycaster;
    private Camera eventCamera;

    /// <summary>E5에서 저장한 채보 (TearDown에서 지운다)</summary>
    private string savedChartId;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedChartId = null;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return OpenEditor(null);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!string.IsNullOrEmpty(savedChartId))
        {
            string path = ChartRepository.GetChartPath(Song, savedChartId, ChartType.Custom);
            if (File.Exists(path)) File.Delete(path);
            Debug.Log($"[LongNoteEditorTest] 테스트로 저장한 채보를 지웠습니다: {path}");
        }
        yield return new ExitPlayMode();
    }

    /// <summary>에디터 씬을 연다. chartId가 없으면 새 채보.</summary>
    private IEnumerator OpenEditor(string chartId)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }

        EditorEntryData entry = new EditorEntryData { songId = Song, chartName = "롱노트 에디터 확인", level = 1, chartId = chartId };
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, entry));
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
        noteLayer = Object.FindFirstObjectByType<EditorNoteLayer>();
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

    // ───── 포인터 이벤트 ─────

    /// <summary>라인 lane, 박 beat 위치의 화면 좌표</summary>
    private Vector2 ScreenPoint(int lane, float beat)
    {
        Rect rect = grid.GridRect.rect;
        Vector3 local = new Vector3(rect.xMin + (lane + 0.5f) * grid.LaneWidth, rect.yMin + grid.BeatToLocalY(beat), 0f);
        return RectTransformUtility.WorldToScreenPoint(eventCamera, grid.GridRect.TransformPoint(local));
    }

    private PointerEventData Pointer(Vector2 position, PointerEventData.InputButton button)
    {
        PointerEventData data = new PointerEventData(EventSystem.current) { position = position, button = button };
        RaycastResult raycast = new RaycastResult { module = raycaster };
        data.pointerPressRaycast = raycast;
        data.pointerCurrentRaycast = raycast;
        return data;
    }

    /// <summary>왼쪽 버튼으로 from 박에서 눌러 to 박까지 끌고 놓는다 (같은 라인).</summary>
    private void Drag(int lane, float fromBeat, float toBeat)
    {
        GameObject target = placer.gameObject;
        ExecuteEvents.Execute(target, Pointer(ScreenPoint(lane, fromBeat), PointerEventData.InputButton.Left), ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, Pointer(ScreenPoint(lane, (fromBeat + toBeat) * 0.5f), PointerEventData.InputButton.Left), ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(target, Pointer(ScreenPoint(lane, toBeat), PointerEventData.InputButton.Left), ExecuteEvents.dragHandler);
        PointerEventData up = Pointer(ScreenPoint(lane, toBeat), PointerEventData.InputButton.Left);
        ExecuteEvents.Execute(target, up, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, up, ExecuteEvents.pointerClickHandler);
    }

    private void Click(int lane, float beat) => Drag(lane, beat, beat);

    private void RightClick(int lane, float beat)
    {
        ExecuteEvents.Execute(placer.gameObject, Pointer(ScreenPoint(lane, beat), PointerEventData.InputButton.Right), ExecuteEvents.pointerClickHandler);
    }

    private bool TryFind(int lane, int tick, out EditorNote note)
    {
        foreach (EditorNote n in session.Data.Notes)
        {
            if (n.Lane == lane && n.Tick == tick)
            {
                note = n;
                return true;
            }
        }
        note = default;
        return false;
    }

    private string NoteCountText => Get<TMP_Text>(canvas, "noteCountText").text;
    private string StatusText => Get<TMP_Text>(canvas, "saveStatusText").text;

    // ───── 에디터 항목 ─────

    /// <summary>E1: 롱노트 도구로 2박 드래그 → 롱노트 1개 (시작·끝 틱), 롱노트 수 표시 +1</summary>
    [UnityTest]
    public IEnumerator E1_LongToolDrag()
    {
        canvas.SetTool(EditorTool.LongNote);
        string before = NoteCountText;
        Drag(0, 4f, 6f);
        yield return null;
        string after = NoteCountText;
        Debug.Log($"[LongNoteEditorTest] E1: 롱노트 {session.Data.LongCount}개, 표시 \"{before}\" → \"{after}\"");

        Assert.AreEqual(1, session.Data.LongCount);
        Assert.IsTrue(TryFind(0, T * 4, out EditorNote note));
        Assert.AreEqual(T * 6, note.EndTick);
        Assert.AreEqual(string.Format(GameConstants.Editor.NoteCountFormat, 0, 0), before);
        Assert.AreEqual(string.Format(GameConstants.Editor.NoteCountFormat, 1, 1), after);
    }

    /// <summary>E2: 숏노트 도구로 1/4박 미만 드래그 → 숏노트, 1박 드래그 → 롱노트</summary>
    [UnityTest]
    public IEnumerator E2_ShortToolDrag()
    {
        canvas.SetTool(EditorTool.ShortNote);
        Drag(1, 4f, 4.1f);
        Drag(2, 4f, 5f);
        yield return null;
        bool shortOk = TryFind(1, T * 4, out EditorNote shortNote);
        bool longOk = TryFind(2, T * 4, out EditorNote longNote);
        Debug.Log($"[LongNoteEditorTest] E2: 4→4.1박 드래그 → {(shortOk ? (shortNote.IsLong ? "롱" : "숏") : "없음")}, 4→5박 드래그 → {(longOk ? (longNote.IsLong ? $"롱 {longNote.Tick}~{longNote.EndTick}" : "숏") : "없음")}");

        Assert.IsTrue(shortOk);
        Assert.IsFalse(shortNote.IsLong, "1/4박 미만 드래그는 숏노트");
        Assert.IsTrue(longOk);
        Assert.IsTrue(longNote.IsLong, "1박 드래그는 롱노트");
        Assert.AreEqual(T * 5, longNote.EndTick);
    }

    /// <summary>E3: 기존 롱노트와 겹치게 드래그 / 롱노트 구간 안 숏노트 클릭 → 둘 다 거부, 상태 표시줄에 겹침 안내</summary>
    [UnityTest]
    public IEnumerator E3_OverlapRejected()
    {
        canvas.SetTool(EditorTool.LongNote);
        Drag(0, 4f, 6f);
        Drag(0, 5f, 7f);
        yield return null;
        string status1 = StatusText;
        int countAfterLong = session.Data.Notes.Count;

        canvas.SetTool(EditorTool.ShortNote);
        Get<TMP_Text>(canvas, "saveStatusText").text = string.Empty;
        Click(0, 5f);
        yield return null;
        string status2 = StatusText;
        Debug.Log($"[LongNoteEditorTest] E3: 겹치는 롱노트 드래그 뒤 노트 {countAfterLong}개 · 상태 \"{status1}\", 롱노트 안 숏노트 클릭 뒤 노트 {session.Data.Notes.Count}개 · 상태 \"{status2}\"");

        Assert.AreEqual(1, countAfterLong, "겹치는 롱노트 거부");
        Assert.AreEqual(GameConstants.Editor.NoteOverlapMessage, status1);
        Assert.AreEqual(1, session.Data.Notes.Count, "롱노트 안 숏노트 거부");
        Assert.AreEqual(GameConstants.Editor.NoteOverlapMessage, status2);
    }

    /// <summary>E4: 롱노트 몸통 가운데 우클릭 → 삭제</summary>
    [UnityTest]
    public IEnumerator E4_RightClickBody()
    {
        canvas.SetTool(EditorTool.LongNote);
        Drag(3, 4f, 8f);
        yield return null;
        int before = session.Data.LongCount;
        canvas.SetTool(EditorTool.ShortNote);
        RightClick(3, 6.1f);
        yield return null;
        Debug.Log($"[LongNoteEditorTest] E4: 롱노트 4~8박, 6.1박 우클릭 → 롱노트 {before} → {session.Data.LongCount}");

        Assert.AreEqual(1, before);
        Assert.AreEqual(0, session.Data.LongCount);
        Assert.AreEqual(0, session.Data.Notes.Count);
    }

    /// <summary>E5: 저장 → 다시 열기 → 롱노트 시작·끝·라인 같음 (셋잇단 끝 4/3박 포함)</summary>
    [UnityTest]
    public IEnumerator E5_SaveAndReopen()
    {
        canvas.SetTool(EditorTool.LongNote);
        Drag(0, 4f, 6f);
        grid.SetSnap(3);
        Drag(1, 8f, 8f + 4f / 3f);
        Drag(2, 10f + 1f / 3f, 11f + 2f / 3f);
        grid.SetSnap(4);
        canvas.SetTool(EditorTool.ShortNote);
        Click(3, 5f);
        yield return null;

        List<EditorNote> expected = new List<EditorNote>(session.Data.Notes);
        Assert.AreEqual(3, session.Data.LongCount, "롱노트 3개를 놓았다");

        string folder = ChartRepository.GetSongFolder(Song, ChartType.Custom);
        HashSet<string> filesBefore = new HashSet<string>(Directory.Exists(folder) ? Directory.GetFiles(folder) : new string[0]);

        editorScene.GetType().GetMethod("OnSaveRequested", Private).Invoke(editorScene, null);
        savedChartId = session.ChartId;
        Assert.IsFalse(session.IsDirty, "저장됨");
        HashSet<string> filesAfter = new HashSet<string>(Directory.GetFiles(folder));
        filesAfter.ExceptWith(filesBefore);
        Assert.AreEqual(1, filesAfter.Count, "새 파일 하나만 생겼다");

        yield return OpenEditor(savedChartId);
        List<EditorNote> actual = new List<EditorNote>(session.Data.Notes);

        string Fmt(List<EditorNote> list)
        {
            List<string> parts = new List<string>();
            foreach (EditorNote n in list) parts.Add(n.IsLong ? $"L{n.Lane}:{n.Tick}~{n.EndTick}" : $"S{n.Lane}:{n.Tick}");
            return string.Join(", ", parts);
        }
        Debug.Log($"[LongNoteEditorTest] E5: 저장 전 [{Fmt(expected)}] / 다시 연 뒤 [{Fmt(actual)}], chartId {savedChartId}");

        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].Lane, actual[i].Lane, $"노트 {i} 라인");
            Assert.AreEqual(expected[i].Tick, actual[i].Tick, $"노트 {i} 시작");
            Assert.AreEqual(expected[i].EndTick, actual[i].EndTick, $"노트 {i} 끝");
        }
        Assert.IsTrue(TryFind(1, T * 8, out EditorNote triplet));
        Assert.AreEqual(T * 8 + 64, triplet.EndTick, "셋잇단 끝 4/3박 = 64틱");
    }

    /// <summary>E6: 롱노트 앞에서 재생 → 시작·끝에 키음 예약이 각각 1번, 예약 시각이 TimingMap 시간과 1ms 안</summary>
    [UnityTest]
    public IEnumerator E6_KeySoundAtStartAndEnd()
    {
        canvas.SetTool(EditorTool.LongNote);
        Drag(2, 6f, 8f);
        yield return null;

        EditorPlayback playback = Object.FindFirstObjectByType<EditorPlayback>();
        playback.SetMetronome(false);
        playback.SetKeySound(true);
        grid.SetCurrentBeat(5f);
        playback.Play();

        double endTime = session.Timing.BeatToSeconds(8f);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (GameManager.Sound.SongTime < endTime + 0.3)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "재생 대기 시간 초과");
            yield return null;
        }

        List<int> ticks = new List<int>(Get<List<int>>(playback, "keyTicks"));
        List<double> dsps = new List<double>(Get<List<double>>(playback, "keyDsps"));
        playback.Stop(false);

        double maxErrorMs = 0;
        for (int i = 0; i < ticks.Count; i++)
        {
            double expectedDsp = GameManager.Sound.GetDspTimeOfSongTime(session.Timing.BeatToSeconds(BeatGridMath.TickToBeat(ticks[i])));
            maxErrorMs = System.Math.Max(maxErrorMs, System.Math.Abs(dsps[i] - expectedDsp) * 1000.0);
        }
        Debug.Log($"[LongNoteEditorTest] E6: 예약한 키음 tick [{string.Join(", ", ticks)}], 예약 시각 − TimingMap 시각 최대 {maxErrorMs:0.000}ms");

        Assert.AreEqual(1, ticks.FindAll(t => t == T * 6).Count, "시작 키음 1번");
        Assert.AreEqual(1, ticks.FindAll(t => t == T * 8).Count, "끝 키음 1번");
        Assert.AreEqual(2, ticks.Count);
        Assert.Less(maxErrorMs, 1.0);
    }

    /// <summary>E7: 롱노트 머리·꼬리 아래·위 y = 시작·끝 틱 숏노트 아래·위 y</summary>
    [UnityTest]
    public IEnumerator E7_HeadTailPosition()
    {
        canvas.SetTool(EditorTool.LongNote);
        Drag(1, 4f, 6f);
        Drag(2, 7f, 7.25f);
        yield return null;
        grid.SetCurrentBeat(4f);
        yield return null;

        float half = noteLayer.NoteHeight * 0.5f;
        foreach (EditorNote note in session.Data.Notes)
        {
            Img_LongNote view = FindView(note);
            Assert.IsNotNull(view, $"롱노트 표시 {note.Tick}");
            float startY = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick));
            float endY = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.EndTick));
            Vector2 head = Edges(view.HeadRect, view.RectTransform);
            Vector2 tail = Edges(view.TailRect, view.RectTransform);
            Debug.Log($"[LongNoteEditorTest] E7 라인{note.Lane} {note.Tick / (float)T}~{note.EndTick / (float)T}박: 머리 {head.x:0.00}~{head.y:0.00} / 시작 숏노트 {startY - half:0.00}~{startY + half:0.00}, " +
                      $"꼬리 {tail.x:0.00}~{tail.y:0.00} / 끝 숏노트 {endY - half:0.00}~{endY + half:0.00}");

            Assert.AreEqual(startY - half, head.x, 0.01);
            Assert.AreEqual(startY + half, head.y, 0.01);
            Assert.AreEqual(endY - half, tail.x, 0.01);
            Assert.AreEqual(endY + half, tail.y, 0.01);
        }
    }

    private Img_LongNote FindView(EditorNote note)
    {
        float x = (note.Lane + 0.5f) * grid.LaneWidth;
        float bottom = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)) - noteLayer.NoteHeight * 0.5f;
        foreach (Img_LongNote view in noteLayer.GetComponentsInChildren<Img_LongNote>(false))
        {
            RectTransform rect = view.RectTransform;
            if (view.name.EndsWith("_Preview")) continue;
            if (Mathf.Abs(rect.anchoredPosition.x - x) < 0.01f && Mathf.Abs(rect.anchoredPosition.y - bottom) < 0.5f) return view;
        }
        return null;
    }

    /// <summary>자식 RectTransform의 아래·위 y를 막대(피벗 아래)와 같은 좌표(그리드 아래에서부터)로 바꾼다.</summary>
    private static Vector2 Edges(RectTransform child, RectTransform bar)
    {
        Vector3[] corners = new Vector3[4];
        child.GetWorldCorners(corners);
        return new Vector2(bar.anchoredPosition.y + bar.InverseTransformPoint(corners[0]).y, bar.anchoredPosition.y + bar.InverseTransformPoint(corners[1]).y);
    }
}
