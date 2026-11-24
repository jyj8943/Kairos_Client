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
        float deadline = TestSpeed.Deadline(20f);
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

    private string NoteCountText => canvas.NotesValue + "/" + canvas.LongNotesValue;
    private string StatusText => Get<TMP_Text>(canvas, "saveStatusText").text;

    // ───── 에디터 항목 ─────

    /// <summary>E1: 노트 도구로 2박 드래그 → 롱노트 1개 (시작·끝 틱), 롱노트 수 표시 +1</summary>
    [UnityTest]
    public IEnumerator E1_DragLongNote()
    {
        canvas.SetTool(EditorTool.Note);
        string before = NoteCountText;
        Drag(0, 4f, 6f);
        yield return null;
        string after = NoteCountText;
        Debug.Log($"[LongNoteEditorTest] E1: 롱노트 {session.Data.LongCount}개, 표시 \"{before}\" → \"{after}\"");

        Assert.AreEqual(1, session.Data.LongCount);
        Assert.IsTrue(TryFind(0, T * 4, out EditorNote note));
        Assert.AreEqual(T * 6, note.EndTick);
        Assert.AreEqual("0/0", before, "노트 / 롱노트 카드");
        Assert.AreEqual("1/1", after);
    }

    /// <summary>
    /// E2: 노트 도구 드래그 경계. 클릭 · 12px 미만 한 칸 · 1/4박 미만 12px 이상 → 숏노트, 1/4박 이상 + 12px 이상 → 롱노트.
    /// 드래그 미리 보기도 롱노트가 되는 경우에만 보인다.
    /// </summary>
    [UnityTest]
    public IEnumerator E2_DragBoundary()
    {
        canvas.SetTool(EditorTool.Note);
        grid.SetGridHeight(2f);
        grid.SetCurrentBeat(3f);
        yield return null;
        float beatPx = grid.BeatToLocalY(5f) - grid.BeatToLocalY(4f);
        float half = 0.125f; // 1/4 스냅의 반올림 경계 (4박 ↔ 4.25박)

        var cases = new (string name, int lane, int snap, float from, float to, bool expectLong)[]
        {
            ("클릭", 0, 4, 4f, 4f, false),
            ("1/4박 + 12px 이상", 1, 4, 4f, 4.25f, true),
            ("한 칸, 12px 미만", 2, 4, 4f + half - 4f / beatPx, 4f + half + 4f / beatPx, false),
            ("12px 이상, 1/4박 미만", 3, 8, 4f, 4.125f, false),
        };

        System.Text.StringBuilder log = new System.Text.StringBuilder($"[LongNoteEditorTest] E2 드래그 경계 (1박 = {beatPx:0.0}px)\n| 경우 | 끈 거리(px) | 미리 보기 | 결과 |\n|---|---|---|---|\n");
        GameObject target = placer.gameObject;
        foreach (var c in cases)
        {
            grid.SetSnap(c.snap);
            float px = (c.to - c.from) * beatPx;
            ExecuteEvents.Execute(target, Pointer(ScreenPoint(c.lane, c.from), PointerEventData.InputButton.Left), ExecuteEvents.pointerDownHandler);
            Assert.IsFalse(IsDragPreviewShown, c.name + ": 누르기만 하면 미리 보기 없음");
            ExecuteEvents.Execute(target, Pointer(ScreenPoint(c.lane, c.to), PointerEventData.InputButton.Left), ExecuteEvents.dragHandler);
            bool previewShown = IsDragPreviewShown;
            ExecuteEvents.Execute(target, Pointer(ScreenPoint(c.lane, c.to), PointerEventData.InputButton.Left), ExecuteEvents.pointerUpHandler);

            bool found = TryFind(c.lane, T * 4, out EditorNote note);
            log.Append($"| {c.name} | {px:0.0} | {previewShown} | {(!found ? "없음" : note.IsLong ? $"롱 {note.Tick}~{note.EndTick}" : $"숏 {note.Tick}")} |\n");
            Assert.IsTrue(found, c.name);
            Assert.AreEqual(c.expectLong, note.IsLong, c.name);
            Assert.AreEqual(c.expectLong, previewShown, c.name + ": 미리 보기 = 놓을 결과");
            if (c.expectLong) Assert.AreEqual(T * 4 + 12, note.EndTick);
        }
        Debug.Log(log.ToString());
        Assert.Less((cases[2].to - cases[2].from) * beatPx, GameConstants.Editor.LongNoteDragMinPx, "12px 미만 경우 확인");
        Assert.GreaterOrEqual((cases[3].to - cases[3].from) * beatPx, GameConstants.Editor.LongNoteDragMinPx, "12px 이상 경우 확인");
        Assert.AreEqual(4, session.Data.Notes.Count);
    }

    private bool IsDragPreviewShown => noteLayer.Preview != null && noteLayer.Preview.gameObject.activeSelf;

    /// <summary>E3: 기존 롱노트와 겹치게 드래그 / 롱노트 구간 안 숏노트 클릭 → 둘 다 거부, 상태 표시줄에 겹침 안내</summary>
    [UnityTest]
    public IEnumerator E3_OverlapRejected()
    {
        canvas.SetTool(EditorTool.Note);
        Drag(0, 4f, 6f);
        Drag(0, 5f, 7f);
        yield return null;
        string status1 = StatusText;
        int countAfterLong = session.Data.Notes.Count;

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
        canvas.SetTool(EditorTool.Note);
        Drag(3, 4f, 8f);
        yield return null;
        int before = session.Data.LongCount;
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
        canvas.SetTool(EditorTool.Note);
        Drag(0, 4f, 6f);
        grid.SetSnap(3);
        Drag(1, 8f, 8f + 4f / 3f);
        Drag(2, 10f + 1f / 3f, 11f + 2f / 3f);
        grid.SetSnap(4);
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
        canvas.SetTool(EditorTool.Note);
        Drag(2, 6f, 8f);
        yield return null;

        EditorPlayback playback = Object.FindFirstObjectByType<EditorPlayback>();
        playback.SetMetronome(false);
        playback.SetKeySound(true);
        grid.SetCurrentBeat(5f);
        playback.Play();

        double endTime = session.Timing.BeatToSeconds(8f);
        float deadline = TestSpeed.Deadline(10f);
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

    /// <summary>E7: 롱노트 머리 가운데 = 시작 틱 y (숏노트와 같은 자리), 꼬리 끝 막대 가운데 = 끝 틱 y (높이 6, 폭 = 몸통 폭), 몸통 = 시작 y ~ 끝 y</summary>
    [UnityTest]
    public IEnumerator E7_HeadTailPosition()
    {
        canvas.SetTool(EditorTool.Note);
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
            float barHalf = GameConstants.Editor.EditorLongTailBarHeightPx * 0.5f;
            Debug.Log($"[LongNoteEditorTest] E7 라인{note.Lane} {note.Tick / (float)T}~{note.EndTick / (float)T}박: 머리 {head.x:0.00}~{head.y:0.00} / 시작 숏노트 {startY - half:0.00}~{startY + half:0.00}, " +
                      $"꼬리 막대 {tail.x:0.00}~{tail.y:0.00} / 끝 y {endY:0.00} ± {barHalf}, 몸통 {view.RectTransform.anchoredPosition.y:0.00} + {view.RectTransform.rect.height:0.00}");

            Assert.AreEqual(startY - half, head.x, 0.01, "머리 아래 = 시작 숏노트 아래");
            Assert.AreEqual(startY + half, head.y, 0.01, "머리 위 = 시작 숏노트 위");
            Assert.AreEqual(endY - barHalf, tail.x, 0.01, "막대 아래");
            Assert.AreEqual(endY + barHalf, tail.y, 0.01, "막대 위");
            Assert.AreEqual(startY, view.RectTransform.anchoredPosition.y, 0.01, "몸통 아래 = 머리 가운데");
            Assert.AreEqual(endY - startY, view.RectTransform.rect.height, 0.01, "몸통 위 = 끝 틱");
            Assert.AreEqual(view.RectTransform.rect.width, view.TailRect.rect.width, 0.01, "막대 폭 = 몸통 폭");
        }
    }

    /// <summary>
    /// E8: 라인 0~3 숏노트 · 롱노트 색이 인게임 라인 색과 같다 (바깥 라벤더, 안쪽 하늘). 광택 · 가운데 선 · 몸통 알파 · 몸통 폭 · 9-slice 배율도 본다.
    /// </summary>
    [UnityTest]
    public IEnumerator E8_NoteLooksLikeInGame()
    {
        canvas.SetTool(EditorTool.Note);
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++) Click(lane, 4f);
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++) Drag(lane, 5f, 7f);
        yield return null;
        grid.SetCurrentBeat(4f);
        yield return null;

        UITheme theme = UITheme.Current;
        System.Text.StringBuilder log = new System.Text.StringBuilder("[LongNoteEditorTest] E8 라인별 색\n| 라인 | 숏노트 | 롱노트 머리 | 몸통 | 꼬리 막대 |\n|---|---|---|---|---|\n");
        foreach (Img_ShortNote view in noteLayer.GetComponentsInChildren<Img_ShortNote>(false))
        {
            int lane = Mathf.FloorToInt(view.RectTransform.anchoredPosition.x / grid.LaneWidth);
            Assert.AreEqual(theme.NoteColor(lane), view.Body.color, "숏노트 라인 색 " + lane);
            Assert.AreEqual(theme.noteShine, view.Shine.color, "광택 흰색");
            Assert.AreEqual(GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier, view.Body.pixelsPerUnitMultiplier);
            Vector2 size = view.RectTransform.sizeDelta;
            Assert.AreEqual(size.x - 6f, view.Shine.rectTransform.rect.width, 0.01, "광택 폭 = 노트 폭 − 6");
            Assert.AreEqual(4f, view.Shine.rectTransform.rect.height, 0.01, "광택 두께 4");
            Assert.AreEqual(size.y * 0.5f - 2f, view.Shine.rectTransform.anchoredPosition.y + 2f, 0.01, "광택 위 끝 = 노트 위 끝 − 2");
        }

        float expectedBody = grid.LaneWidth * GameConstants.Editor.EditorLongBodyWidthRatio;
        string[] rows = new string[GameConstants.Editor.LaneCount];
        foreach (EditorNote note in session.Data.Notes)
        {
            if (!note.IsLong) continue;
            Img_LongNote view = FindView(note);
            Assert.IsNotNull(view);
            Color line = theme.NoteColor(note.Lane);
            Assert.AreEqual(line, view.HeadColor, "머리 라인 색");
            Assert.AreEqual(line, view.TailColor, "꼬리 막대 라인 색 100%");
            Assert.AreEqual(line, view.BodyColor, "몸통 라인 색 (알파는 효과)");
            view.BodyAlpha.GetEdgeAlphas(out float bottom, out float top);
            Assert.AreEqual(0.70f, bottom, 1e-4f, "몸통 아래 70%");
            Assert.AreEqual(0.45f, top, 1e-4f, "몸통 위 45%");
            Assert.AreEqual(expectedBody, view.RectTransform.rect.width, 0.01, "몸통 폭 = W × 0.70");
            Assert.AreEqual(2f, view.CenterLine.rectTransform.rect.width, 0.01, "가운데 선 2px");
            Assert.AreEqual(view.RectTransform.rect.height, view.CenterLine.rectTransform.rect.height, 0.01, "가운데 선 = 몸통 길이");
            Assert.AreEqual(theme.longNoteCenterLine, view.CenterLine.color);
            Assert.IsFalse(view.BodyFrame.enabled, "미리 보기 테두리는 꺼짐");
            Assert.IsNull(view.GetComponent<Outline>(), "몸통 Outline 없음");
            Assert.AreEqual(theme.noteShine, view.HeadShine.color, "머리 광택 흰색");
            rows[note.Lane] = $"머리 {Hex(view.HeadColor)} | 몸통 {Hex(view.BodyColor)} 위 {top:0.00}/아래 {bottom:0.00} 폭 {view.RectTransform.rect.width:0.0} | 막대 {Hex(view.TailColor)} {view.TailRect.rect.width:0.0}×{view.TailRect.rect.height:0.0}";
        }
        for (int lane = 0; lane < rows.Length; lane++) log.Append($"| {lane} | {Hex(theme.NoteColor(lane))} | {rows[lane]} |\n");
        Debug.Log(log.ToString());
    }

    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

    /// <summary>
    /// E9: 마우스 미리 보기. 빈 칸 = 놓을 자리(점선), 숏노트 · 롱노트 머리 · 몸통 · 꼬리 막대 위 = 흰 테두리(롱노트는 전체).
    /// 흰 테두리 노트 = 같은 자리 우클릭으로 지워지는 노트, 흰 테두리와 점선이 같이 보이는 프레임 0.
    /// BPM 구간 도구 · 드래그 중 · 팝업 · 재생 중에는 둘 다 숨김.
    /// </summary>
    [UnityTest]
    public IEnumerator E9_MouseHover()
    {
        canvas.SetTool(EditorTool.Note);
        Click(1, 4f);
        Drag(2, 4f, 6f);
        yield return null;
        grid.SetCurrentBeat(3f);
        yield return null;

        float barTopBeat = 6f;
        var cases = new (string name, int lane, float beat, float yOffset, bool expectNote, int expectLane)[]
        {
            ("빈 칸", 0, 8f, 0f, false, -1),
            ("숏노트", 1, 4f, 0f, true, 1),
            ("롱노트 머리", 2, 4f, -5f, true, 2),
            ("롱노트 몸통", 2, 5f, 0f, true, 2),
            ("롱노트 꼬리 막대", 2, barTopBeat, 2f, true, 2),
        };

        int bothVisibleFrames = 0;
        System.Text.StringBuilder log = new System.Text.StringBuilder("[LongNoteEditorTest] E9 마우스 미리 보기\n| 커서 | 상태 | 점선 | 흰 테두리 | 테두리 Rect | 같은 자리 우클릭 노트 |\n|---|---|---|---|---|---|\n");
        foreach (var c in cases)
        {
            Vector2 screen = ScreenPoint(c.lane, c.beat) + new Vector2(0f, c.yOffset * ScreenPerLocal());
            placer.PointerScreenOverride = screen;
            yield return null;
            if (noteLayer.IsNoteHoverVisible && noteLayer.IsPlacePreviewVisible) bothVisibleFrames++;

            bool deletable = placer.TryFindNoteAtScreen(screen, out EditorNote deleteTarget);
            bool hovering = noteLayer.TryGetHoverNote(out EditorNote hoverNote);
            Rect hoverRect = noteLayer.NoteHover.rectTransform.rect;
            Vector2 hoverPos = noteLayer.NoteHover.rectTransform.anchoredPosition;
            log.Append($"| {c.name} | {placer.CursorState} | {noteLayer.IsPlacePreviewVisible} | {noteLayer.IsNoteHoverVisible} | " +
                       (hovering ? $"x {hoverPos.x:0.0} y {hoverPos.y:0.0} {hoverRect.width:0.0}×{hoverRect.height:0.0}" : "-") +
                       $" | {(deletable ? $"라인{deleteTarget.Lane} tick {deleteTarget.Tick}" : "없음")} |\n");

            Assert.AreEqual(c.expectNote, noteLayer.IsNoteHoverVisible, c.name + " 흰 테두리");
            Assert.AreEqual(!c.expectNote, noteLayer.IsPlacePreviewVisible, c.name + " 점선");
            Assert.AreEqual(deletable, hovering, c.name + " 흰 테두리 유무 = 우클릭 대상 유무");
            if (!c.expectNote) continue;

            Assert.AreEqual(c.expectLane, hoverNote.Lane);
            Assert.AreEqual(deleteTarget.Tick, hoverNote.Tick, c.name + " 흰 테두리 노트 = 우클릭 대상");
            Rect expected = EditorNoteLayer.HoverRect(noteLayer.GetNoteRect(hoverNote));
            Assert.AreEqual(expected.position.x, hoverPos.x, 0.01);
            Assert.AreEqual(expected.position.y, hoverPos.y, 0.01);
            Assert.AreEqual(expected.height, hoverRect.height, 0.01, c.name + " 테두리가 노트 전체를 감쌈");
        }

        // 스크롤로 노트가 커서 밑으로 와도 갱신된다 (커서 고정)
        grid.SetCurrentBeat(7f);
        yield return null;
        Vector2 fixedPoint = ScreenPoint(1, 8f);
        placer.PointerScreenOverride = fixedPoint;
        yield return null;
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Place, placer.CursorState, "스크롤 전: 8박 빈 칸");
        grid.SetCurrentBeat(3f);
        yield return null;
        log.Append($"커서 고정, 현재 위치 7박 → 3박 스크롤: 상태 {placer.CursorState} (4박 숏노트가 커서 밑으로)\n");
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.NoteHover, placer.CursorState, "스크롤 뒤 커서 밑 노트를 다시 찾음");
        Assert.IsTrue(noteLayer.TryGetHoverNote(out EditorNote scrolled) && scrolled.Lane == 1 && scrolled.Tick == T * 4);

        // 우클릭으로 지운 노트 = 흰 테두리 노트 (롱노트 몸통)
        Vector2 bodyPoint = ScreenPoint(2, 5f);
        placer.PointerScreenOverride = bodyPoint;
        yield return null;
        Assert.IsTrue(noteLayer.TryGetHoverNote(out EditorNote before));
        ExecuteEvents.Execute(placer.gameObject, Pointer(bodyPoint, PointerEventData.InputButton.Right), ExecuteEvents.pointerClickHandler);
        Assert.IsFalse(session.Data.TryFindAt(before.Lane, before.Tick, out _), "흰 테두리 롱노트가 지워짐");
        Assert.IsTrue(TryFind(1, T * 4, out _), "다른 노트는 남음");
        session.Undo();
        yield return null;

        // 숨기는 경우
        placer.PointerScreenOverride = ScreenPoint(1, 4f);
        canvas.SetTool(EditorTool.BpmSection);
        yield return null;
        log.Append($"BPM 구간 도구: {placer.CursorState}\n");
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Hidden, placer.CursorState, "BPM 구간 도구");
        Assert.IsFalse(noteLayer.IsNoteHoverVisible || noteLayer.IsPlacePreviewVisible);
        canvas.SetTool(EditorTool.Note);
        yield return null;
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.NoteHover, placer.CursorState);

        ExecuteEvents.Execute(placer.gameObject, Pointer(ScreenPoint(0, 8f), PointerEventData.InputButton.Left), ExecuteEvents.pointerDownHandler);
        yield return null;
        log.Append($"드래그 중: {placer.CursorState}\n");
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Hidden, placer.CursorState, "드래그 중");
        Assert.IsFalse(noteLayer.IsNoteHoverVisible || noteLayer.IsPlacePreviewVisible);
        ExecuteEvents.Execute(placer.gameObject, Pointer(ScreenPoint(0, 8f), PointerEventData.InputButton.Left), ExecuteEvents.pointerUpHandler);
        session.Undo();
        yield return null;

        Assert.IsTrue(GameManager.UI.Confirm("테스트", "확인", () => { }));
        yield return null;
        log.Append($"팝업 열림: {placer.CursorState}\n");
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Hidden, placer.CursorState, "팝업 열림");
        Assert.IsFalse(noteLayer.IsNoteHoverVisible || noteLayer.IsPlacePreviewVisible);
        GameManager.UI.CloseAllPopups();
        yield return null;

        EditorPlayback playback = Object.FindFirstObjectByType<EditorPlayback>();
        playback.Play();
        yield return null;
        log.Append($"재생 중: {placer.CursorState}\n");
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Hidden, placer.CursorState, "재생 중");
        Assert.IsFalse(noteLayer.IsNoteHoverVisible || noteLayer.IsPlacePreviewVisible);
        playback.Stop(false);
        yield return null;

        placer.PointerScreenOverride = new Vector2(-1000f, -1000f);
        yield return null;
        Assert.AreEqual(EditorNotePlacer.CursorPreviewState.Hidden, placer.CursorState, "그리드 밖");
        placer.PointerScreenOverride = null;

        log.Append($"흰 테두리 · 점선 동시 프레임 {bothVisibleFrames}");
        Debug.Log(log.ToString());
        Assert.AreEqual(0, bothVisibleFrames);
    }

    /// <summary>
    /// E10: 롱노트 드래그 미리 보기 모양 · 알파 (몸통 W × 0.70, 하늘 22% + 하늘 테두리 2px, 머리 · 꼬리 막대 하늘 55%).
    /// 누르기만 했거나 12px 미만으로 끌면 미리 보기가 없고, 1/4박 · 12px 이상부터 보인다. 막대는 머리 뒤.
    /// </summary>
    [UnityTest]
    public IEnumerator E10_DragPreview()
    {
        canvas.SetTool(EditorTool.Note);
        grid.SetCurrentBeat(3f);
        yield return null;
        GameObject target = placer.gameObject;
        ExecuteEvents.Execute(target, Pointer(ScreenPoint(3, 4f), PointerEventData.InputButton.Left), ExecuteEvents.pointerDownHandler);
        yield return null;

        UITheme theme = UITheme.Current;
        Assert.IsFalse(IsDragPreviewShown, "누르기만 하면 미리 보기 없음");
        float tenPx = 10f / (grid.BeatToLocalY(5f) - grid.BeatToLocalY(4f));
        ExecuteEvents.Execute(target, Pointer(ScreenPoint(3, 4f + tenPx), PointerEventData.InputButton.Left), ExecuteEvents.dragHandler);
        yield return null;
        Assert.IsFalse(IsDragPreviewShown, "10px만 끌면 미리 보기 없음");

        ExecuteEvents.Execute(target, Pointer(ScreenPoint(3, 6f), PointerEventData.InputButton.Left), ExecuteEvents.dragHandler);
        yield return null;
        Img_LongNote preview = noteLayer.Preview;
        Assert.IsTrue(IsDragPreviewShown, "1/4박 · 12px 이상이면 보임");
        Assert.Greater(preview.HeadRect.GetSiblingIndex(), preview.TailRect.GetSiblingIndex(), "머리가 막대보다 앞");

        preview.BodyAlpha.GetEdgeAlphas(out float bottom, out float top);
        Color part = UITheme.WithAlpha(theme.editorPreview, 0.55f);
        string info = $"몸통 폭 {preview.RectTransform.rect.width:0.0} (W×0.70 = {grid.LaneWidth * 0.7f:0.0}), 높이 {preview.RectTransform.rect.height:0.0}, " +
                      $"채움 {Hex(preview.BodyColor)} 알파 위 {top:0.00}/아래 {bottom:0.00}, 테두리 {Hex(preview.BodyFrame.color)} {preview.BodyFrame.Thickness}px, " +
                      $"머리 {Hex(preview.HeadColor)}, 막대 {Hex(preview.TailColor)}, 가운데 선 {(preview.CenterLine.enabled ? "보임" : "없음")}";
        Debug.Log("[LongNoteEditorTest] E10 드래그 미리 보기: " + info);

        Assert.AreEqual(grid.LaneWidth * 0.7f, preview.RectTransform.rect.width, 0.01);
        Assert.AreEqual(grid.BeatToLocalY(6f) - grid.BeatToLocalY(4f), preview.RectTransform.rect.height, 0.01, "머리 가운데 ~ 끝");
        Assert.AreEqual(theme.editorPreview, preview.BodyColor);
        Assert.AreEqual(0.22f, bottom, 1e-4f);
        Assert.AreEqual(0.22f, top, 1e-4f);
        Assert.IsTrue(preview.BodyFrame.enabled);
        Assert.AreEqual(2f, preview.BodyFrame.Thickness);
        Assert.AreEqual(theme.editorPreview, preview.BodyFrame.color);
        Assert.AreEqual(part, preview.HeadColor, "머리 하늘 55%");
        Assert.AreEqual(part, preview.TailColor, "막대 하늘 55%");
        Assert.IsFalse(noteLayer.IsNoteHoverVisible, "드래그 중 흰 테두리 없음");

        PointerEventData up = Pointer(ScreenPoint(3, 6f), PointerEventData.InputButton.Left);
        ExecuteEvents.Execute(target, up, ExecuteEvents.pointerUpHandler);
        yield return null;
        Img_LongNote placed = FindView(new EditorNote(3, T * 4, T * 6));
        Assert.IsNotNull(placed);
        Assert.AreEqual(theme.NoteColor(3), placed.HeadColor, "놓은 뒤 라인 색");
    }

    /// <summary>그리드 로컬 1px의 화면 px</summary>
    private float ScreenPerLocal()
    {
        return Mathf.Abs(ScreenPoint(0, 1f).y - ScreenPoint(0, 0f).y) / Mathf.Abs(grid.BeatToLocalY(1f) - grid.BeatToLocalY(0f));
    }

    private Img_LongNote FindView(EditorNote note)
    {
        float x = (note.Lane + 0.5f) * grid.LaneWidth;
        float bottom = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick));
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
