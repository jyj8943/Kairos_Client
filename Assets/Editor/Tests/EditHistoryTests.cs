using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 에디터 실행 취소 · 다시 실행 EditMode 테스트.
/// - ChartEditSession: 노트 추가·삭제 기록, 실패한 편집, 수정 여부(저장 시점), 불러오기, 테스트 플레이 왕복
/// - EditHistory: 최대 개수, 묶음 기록, 다시 실행 목록 비우기
/// </summary>
public class EditHistoryTests
{
    private const string SongId = "__test_undo__";
    private const int T = GameConstants.Editor.TicksPerBeat;

    private GameObject owner;
    private ChartEditSession session;

    [SetUp]
    public void SetUp()
    {
        DeleteTestFolder();
        owner = new GameObject("ChartEditSession_Undo");
        session = owner.AddComponent<ChartEditSession>();
        // 150 BPM · 60초 = 150박 (tick 7200)
        session.Init(new EditorEntryData { songId = SongId, chartName = "실행 취소", level = 1 }, SongId, 150f, 0, 60f);
    }

    [TearDown]
    public void TearDown()
    {
        if (owner != null) Object.DestroyImmediate(owner);
        DeleteTestFolder();
    }

    private static void DeleteTestFolder()
    {
        string folder = ChartRepository.GetSongFolder(SongId, ChartType.Custom);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    private bool Has(int lane, int tick, int endTick)
    {
        return session.Data.TryFindAt(lane, tick, out EditorNote note) && note.Tick == tick && note.EndTick == endTick;
    }

    /// <summary>1. 숏노트 추가 → Undo → 없음 → Redo → 다시 있음. 노트 수 갱신 경로(OnChanged)도 탄다</summary>
    [Test]
    public void ShortNote_UndoRedo()
    {
        int changed = 0;
        session.Data.OnChanged += () => changed++;

        Assert.IsTrue(session.AddNote(1, 2 * T));
        Assert.IsTrue(session.CanUndo);
        Assert.IsFalse(session.CanRedo);

        Assert.AreEqual(GameConstants.Editor.HistoryAddShortNote, session.Undo());
        Assert.AreEqual(0, session.Data.Notes.Count);
        Assert.AreEqual(0, session.Data.ShortCount, "노트 수도 줄어듦");
        Assert.IsFalse(session.CanUndo);
        Assert.IsTrue(session.CanRedo);

        Assert.AreEqual(GameConstants.Editor.HistoryAddShortNote, session.Redo());
        Assert.IsTrue(Has(1, 2 * T, 2 * T));
        Assert.AreEqual(1, session.Data.ShortCount);
        Assert.AreEqual(3, changed, "추가·Undo·Redo 모두 OnChanged (노트 표시·노트 수 갱신)");

        Assert.IsNull(session.Redo(), "다시 실행할 것이 없으면 null");
    }

    /// <summary>2. 롱노트 추가(끝 → 시작 방향 드래그) → Undo → 없음 → Redo → 시작·끝 tick 그대로</summary>
    [Test]
    public void LongNote_UndoRedo_KeepsTicks()
    {
        Assert.IsTrue(session.AddLongNote(2, 6 * T, 4 * T + 16, out _));
        Assert.AreEqual(GameConstants.Editor.HistoryAddLongNote, session.Undo());
        Assert.AreEqual(0, session.Data.Notes.Count);
        Assert.AreEqual(0, session.Data.LongCount);

        Assert.AreEqual(GameConstants.Editor.HistoryAddLongNote, session.Redo());
        Assert.IsTrue(Has(2, 4 * T + 16, 6 * T), "작은 쪽이 시작, 끝 tick 그대로");
        Assert.AreEqual(1, session.Data.LongCount);
    }

    /// <summary>3. 노트 삭제 → Undo → 같은 레인·tick으로 복구 (롱노트는 몸통을 눌러 지워도 시작·끝 그대로)</summary>
    [Test]
    public void Remove_Undo_Restores()
    {
        session.AddNote(0, T);
        session.AddLongNote(3, 8 * T, 10 * T, out _);

        Assert.IsTrue(session.RemoveNote(0, T));
        Assert.IsTrue(session.RemoveNote(3, 9 * T), "롱노트 몸통 가운데를 지움");
        Assert.AreEqual(0, session.Data.Notes.Count);

        Assert.AreEqual(GameConstants.Editor.HistoryRemoveLongNote, session.Undo());
        Assert.IsTrue(Has(3, 8 * T, 10 * T), "롱노트 시작·끝 tick 그대로 복구");
        Assert.AreEqual(GameConstants.Editor.HistoryRemoveShortNote, session.Undo());
        Assert.IsTrue(Has(0, T, T));

        Assert.AreEqual(GameConstants.Editor.HistoryRemoveShortNote, session.Redo());
        Assert.IsFalse(session.Data.TryFindAt(0, T, out _));
        Assert.IsTrue(Has(3, 8 * T, 10 * T), "다시 실행은 하나씩");
    }

    /// <summary>4. 추가 3번 → Undo 2번 → 새 편집 → Redo 불가</summary>
    [Test]
    public void NewEdit_ClearsRedo()
    {
        session.AddNote(0, 0);
        session.AddNote(1, T);
        session.AddNote(2, 2 * T);
        session.Undo();
        session.Undo();
        Assert.AreEqual(1, session.UndoCount);
        Assert.AreEqual(2, session.RedoCount);

        session.AddNote(3, 3 * T);
        Assert.IsFalse(session.CanRedo);
        Assert.IsNull(session.Redo());
        Assert.AreEqual(2, session.UndoCount);
        Assert.AreEqual(2, session.Data.Notes.Count);
    }

    /// <summary>5. 실패한 편집(겹침·너무 짧음·없는 노트 삭제)은 기록되지 않음</summary>
    [Test]
    public void FailedEdit_NotRecorded()
    {
        session.AddLongNote(0, 4 * T, 6 * T, out _);
        Assert.AreEqual(1, session.UndoCount);

        Assert.IsFalse(session.AddNote(0, 5 * T, out NoteEditError overlap));
        Assert.AreEqual(NoteEditError.Overlap, overlap);
        Assert.IsFalse(session.AddLongNote(0, 3 * T, 4 * T, out _), "끝이 다른 노트 시작과 겹침");
        Assert.IsFalse(session.AddLongNote(1, 0, 4, out NoteEditError tooShort));
        Assert.AreEqual(NoteEditError.TooShort, tooShort);
        Assert.IsFalse(session.RemoveNote(2, 0), "없는 노트 삭제");
        Assert.IsFalse(session.AddNote(0, -1), "범위 밖");

        Assert.AreEqual(1, session.UndoCount, "실패는 기록하지 않는다");
        session.Undo();
        Assert.AreEqual(0, session.Data.Notes.Count);
        Assert.IsFalse(session.CanUndo);
    }

    /// <summary>6. 최대 개수(200)를 넘으면 가장 오래된 기록부터 버린다</summary>
    [Test]
    public void Capacity_DropsOldest()
    {
        int max = GameConstants.Editor.MaxUndoCount;
        Assert.AreEqual(200, max);
        for (int i = 0; i <= max; i++) Assert.IsTrue(session.AddNote(i % 4, (i / 4) * 12));
        Assert.AreEqual(max, session.UndoCount, "201번째에서 하나 버림");

        int undone = 0;
        while (session.Undo() != null) undone++;
        Assert.AreEqual(max, undone);
        Assert.AreEqual(1, session.Data.Notes.Count, "가장 오래된 첫 노트는 되돌릴 수 없다");
        Assert.IsTrue(Has(0, 0, 0), "남은 것은 첫 번째 편집");
        Assert.IsTrue(session.IsDirty, "기록을 버렸으므로 처음(빈 채보) 상태가 아니다");

        // 순수 클래스: 작은 최대 개수
        EditHistory small = new EditHistory(3);
        List<string> log = new List<string>();
        for (int i = 0; i < 5; i++) small.Record(new FakeAction("e" + i, log));
        Assert.AreEqual(3, small.UndoCount);
        Assert.AreEqual("e4", small.Undo(null).Description);
        Assert.AreEqual("e3", small.Undo(null).Description);
        Assert.AreEqual("e2", small.Undo(null).Description);
        Assert.IsNull(small.Undo(null), "e0·e1은 버려졌다");
    }

    /// <summary>7. 저장 → 편집 → Undo → IsDirty false / 다시 Redo → true. 확인 팝업은 이 값을 그대로 쓴다</summary>
    [Test]
    public void SavePoint_TracksDirty()
    {
        List<bool> dirtyEvents = new List<bool>();
        session.OnDirtyChanged += dirtyEvents.Add;

        session.AddNote(0, 0);
        Assert.IsTrue(session.TrySave(out string error), error);
        Assert.IsFalse(session.IsDirty);

        session.AddNote(1, T);
        Assert.IsTrue(session.IsDirty);
        session.Undo();
        Assert.IsFalse(session.IsDirty, "저장 시점으로 돌아옴");
        session.Redo();
        Assert.IsTrue(session.IsDirty, "저장 시점에서 벗어남");
        session.Undo();
        session.Undo();
        Assert.IsTrue(session.IsDirty, "저장 시점보다 앞(빈 채보)도 수정 상태");
        session.Redo();
        Assert.IsFalse(session.IsDirty);
        CollectionAssert.AreEqual(new[] { true, false, true, false, true, false, true, false }, dirtyEvents, "바뀔 때마다 알림");

        // 저장 시점에서 갈라진 뒤에는 돌아올 수 없다
        session.Undo();
        session.AddNote(2, 2 * T);
        session.Undo();
        Assert.IsTrue(session.IsDirty, "저장한 노트 목록과 다르다 (첫 노트가 없음)");
    }

    /// <summary>8. ApplyChartData 뒤에는 기록이 비어 있고 수정 여부 false</summary>
    [Test]
    public void ApplyChartData_ClearsHistory()
    {
        session.AddNote(0, 0);
        session.AddNote(1, T);
        session.Undo();
        Assert.IsTrue(session.CanUndo);
        Assert.IsTrue(session.CanRedo);

        session.ApplyChartData(session.BuildChartData());
        Assert.IsFalse(session.CanUndo);
        Assert.IsFalse(session.CanRedo);
        Assert.AreEqual(0, session.UndoCount);
        Assert.IsFalse(session.IsDirty);
        Assert.IsNull(session.Undo());
        Assert.AreEqual(1, session.Data.Notes.Count, "불러온 노트는 그대로");

        // 새 채보 시작(Init)도 기록을 비운다
        session.AddNote(2, 0);
        session.Init(new EditorEntryData { songId = SongId, chartName = "새 채보", level = 1 }, SongId, 150f, 0, 60f);
        Assert.IsFalse(session.CanUndo);
    }

    /// <summary>9. 묶음 기록: 두 편집을 묶어 Undo 한 번에 둘 다 되돌아가고, Redo 한 번에 둘 다 다시 적용</summary>
    [Test]
    public void Group_UndoRedoTogether()
    {
        session.AddNote(0, 0);
        session.BeginEditGroup("묶음 테스트");
        session.AddNote(1, T);
        session.AddLongNote(2, 2 * T, 3 * T, out _);
        Assert.IsTrue(session.IsDirty);
        Assert.IsFalse(session.CanUndo, "묶는 중에는 실행 취소하지 않는다");
        session.EndEditGroup();
        Assert.AreEqual(2, session.UndoCount, "숏노트 1 + 묶음 1");

        Assert.AreEqual("묶음 테스트", session.Undo());
        Assert.AreEqual(1, session.Data.Notes.Count);
        Assert.IsTrue(Has(0, 0, 0));

        Assert.AreEqual("묶음 테스트", session.Redo());
        Assert.IsTrue(Has(1, T, T));
        Assert.IsTrue(Has(2, 2 * T, 3 * T));

        // 순수 클래스: 되돌리기는 거꾸로, 빈 묶음은 기록하지 않음
        EditHistory history = new EditHistory();
        List<string> log = new List<string>();
        history.BeginGroup("g");
        history.Record(new FakeAction("a", log));
        history.Record(new FakeAction("b", log));
        history.EndGroup();
        history.BeginGroup("empty");
        history.EndGroup();
        Assert.AreEqual(1, history.UndoCount);
        history.Undo(null);
        history.Redo(null);
        CollectionAssert.AreEqual(new[] { "revert b", "revert a", "apply a", "apply b" }, log);
    }

    /// <summary>10. 테스트 플레이 데이터로 넘겼다가 돌아와 받은 기록으로 Undo·Redo가 동작하고 저장 시점도 이어진다</summary>
    [Test]
    public void TestPlayRoundTrip_KeepsHistory()
    {
        session.AddNote(0, 0);
        Assert.IsTrue(session.TrySave(out string error), error);
        session.AddNote(1, T);
        session.AddLongNote(2, 2 * T, 4 * T, out _);
        session.Undo();

        TestPlayData data = session.CreateTestPlayData(0f, 4, 1f, EditorTool.ShortNote, false, out string playError);
        Assert.IsNotNull(data, playError);
        Assert.IsNotNull(data.editorState.history, "씬 데이터에 기록을 담는다");

        // 인게임 → 에디터: 진입 데이터로 돌려받아 새 세션(씬을 다시 연 것)에 되돌린다
        EditorRestoreState restore = data.CreateEditorEntry(1f).restore;
        Object.DestroyImmediate(owner);
        owner = new GameObject("ChartEditSession_Undo_Return");
        session = owner.AddComponent<ChartEditSession>();
        session.Init(new EditorEntryData { songId = SongId, chartName = "실행 취소", level = 1, chartId = restore.chart.chartId }, SongId, 150f, 0, 60f);
        session.ApplyChartData(restore.chart);
        session.RestoreEditState(restore.isDirty, restore.history);

        Assert.IsTrue(session.IsDirty);
        Assert.AreEqual(2, session.UndoCount);
        Assert.AreEqual(1, session.RedoCount);
        Assert.AreEqual(GameConstants.Editor.HistoryAddLongNote, session.Redo(), "되돌렸던 롱노트 다시 실행");
        Assert.IsTrue(Has(2, 2 * T, 4 * T));
        session.Undo();
        Assert.AreEqual(GameConstants.Editor.HistoryAddShortNote, session.Undo());
        Assert.AreEqual(1, session.Data.Notes.Count);
        Assert.IsFalse(session.IsDirty, "저장 시점(노트 1개)으로 돌아옴");
    }

    /// <summary>F1 곡 조정의 곡 시작 BPM 변경도 기록된다: Undo로 BPM이 돌아오고, 끝까지 되돌리면 저장할 것이 없다</summary>
    [Test]
    public void TuneStartBpm_IsUndoable()
    {
        session.AddNote(0, 0);
        session.SetTiming(160f, 0);
        Assert.AreEqual(160f, session.Bpm);
        Assert.AreEqual(GameConstants.Editor.HistoryTuneStartBpm, session.Undo());
        Assert.AreEqual(150f, session.Bpm, "곡 시작 BPM이 돌아옴");
        session.Undo();
        Assert.IsFalse(session.IsDirty, "처음(빈 새 채보) 상태");

        session.SetTiming(150f, 30);
        Assert.AreEqual(30, session.OffsetMs);
        Assert.IsFalse(session.CanUndo, "offset만 바꾸면 기록하지 않는다 (곡 정보 값)");
        Assert.IsFalse(session.IsDirty);
    }

    /// <summary>순서를 기록만 하는 가짜 편집 (EditHistory 순수 클래스 확인용)</summary>
    private class FakeAction : IEditAction
    {
        private readonly List<string> log;
        public string Description { get; }

        public FakeAction(string description, List<string> log)
        {
            Description = description;
            this.log = log;
        }

        public bool Apply(ChartEditSession target) { log.Add("apply " + Description); return true; }
        public bool Revert(ChartEditSession target) { log.Add("revert " + Description); return true; }
    }
}
