using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 테스트 플레이 계산(TestPlayRules)과 세션의 테스트 플레이 데이터 만들기 EditMode 테스트.
/// </summary>
public class TestPlayRulesTests
{
    private static NoteData Short(float beat, int lane)
    {
        return new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };
    }

    /// <summary>시작 박 이상인 노트만 남긴다 (같은 박 포함, 셋잇단 위치도 tick으로 비교)</summary>
    [Test]
    public void FilterNotesFrom_KeepsNotesAtOrAfterStart()
    {
        List<NoteData> notes = new List<NoteData>
        {
            Short(0f, 0), Short(7.75f, 1), Short(8f, 2), Short(8f + 1f / 3f, 3), Short(12f, 0)
        };

        List<NoteData> filtered = TestPlayRules.FilterNotesFrom(notes, 8f);
        Assert.AreEqual(3, filtered.Count);
        Assert.AreEqual(8f, filtered[0].beat);
        Assert.AreEqual(12f, filtered[2].beat);
        Assert.AreEqual(5, notes.Count, "원래 목록은 바꾸지 않는다");

        // 셋잇단 위치에서 시작해도 그 노트는 포함한다 (부동소수 오차가 있어도)
        List<NoteData> fromTriplet = TestPlayRules.FilterNotesFrom(notes, 8f + 1f / 3f);
        Assert.AreEqual(2, fromTriplet.Count);

        Assert.IsEmpty(TestPlayRules.FilterNotesFrom(notes, 12.25f));
        Assert.AreEqual(5, TestPlayRules.FilterNotesFrom(notes, 0f).Count);
        Assert.IsEmpty(TestPlayRules.FilterNotesFrom(null, 0f));
    }

    /// <summary>시작 시간 10초: 음원 8초부터, 최소 지연만 두고 재생 → 곡 시간이 약 8초부터 흐른다</summary>
    [Test]
    public void MusicStart_AfterLeadIn()
    {
        TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(10.0);
        Assert.AreEqual(8.0, start.StartPositionSeconds, 1e-9);
        Assert.AreEqual(GameConstants.Play.ScheduleLatencySeconds, start.DelaySeconds, 1e-9);
        Assert.AreEqual(8.0 - GameConstants.Play.ScheduleLatencySeconds, start.InitialSongTime, 1e-9);
    }

    /// <summary>시작 시간 0.5초: 음원 0초부터, 1.5초 기다린 뒤 재생 → 곡 시간이 −1.5초부터 흐른다</summary>
    [Test]
    public void MusicStart_BeforeLeadIn()
    {
        TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(0.5);
        Assert.AreEqual(0.0, start.StartPositionSeconds, 1e-9);
        Assert.AreEqual(1.5, start.DelaySeconds, 1e-9);
        Assert.AreEqual(-1.5, start.InitialSongTime, 1e-9);
    }

    /// <summary>시작 시간이 준비 시간과 같거나 0초여도 음수 위치가 나오지 않는다</summary>
    [TestCase(2.0, 0.0)]
    [TestCase(0.0, 0.0)]
    public void MusicStart_EdgeCases(double startTime, double expectedPosition)
    {
        TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(startTime);
        Assert.AreEqual(expectedPosition, start.StartPositionSeconds, 1e-9);
        Assert.GreaterOrEqual(start.DelaySeconds, GameConstants.Play.ScheduleLatencySeconds - 1e-9);
        Assert.LessOrEqual(start.InitialSongTime, startTime - GameConstants.Play.TestPlayLeadInSeconds + 1e-9);
    }

    /// <summary>세션: 저장하지 않고 수정 여부 그대로, 새 채보에 ID를 만들지 않고, 뒤에 노트가 없으면 null</summary>
    [Test]
    public void Session_CreateTestPlayData()
    {
        GameObject owner = new GameObject("ChartEditSession_TestPlay");
        try
        {
            ChartEditSession session = owner.AddComponent<ChartEditSession>();
            session.Init(new EditorEntryData { songId = "__test__", chartName = "테스트", level = 2 }, "__test__", 150f, 0, 60f);

            Assert.IsNull(session.CreateTestPlayData(0f, 4, 1f, EditorTool.ShortNote, false, out string emptyError), "노트가 없으면 null");
            Assert.IsNotEmpty(emptyError);

            session.AddNote(0, 0);
            session.AddNote(1, 48 * 8);
            Assert.IsTrue(session.IsDirty);

            TestPlayData data = session.CreateTestPlayData(8f, 6, 1.5f, EditorTool.BpmSection, true, out string error);
            Assert.IsNotNull(data, error);
            Assert.IsNull(session.ChartId, "새 채보에 ID를 만들지 않는다");
            Assert.IsNull(data.chart.chartId);
            Assert.IsTrue(session.IsDirty, "수정 여부 그대로");
            Assert.AreEqual(2, data.chart.notes.Count, "채보에는 모든 노트가 들어 있다 (필터는 인게임이 한다)");
            Assert.AreEqual(8f, data.startBeat);
            Assert.IsTrue(data.autoPlay, "자동 테스트 값이 그대로 들어간다");
            Assert.IsTrue(data.editorState.isDirty);
            Assert.AreEqual(6, data.editorState.snapDivision);
            Assert.AreEqual(1.5f, data.editorState.gridHeightScale);
            Assert.AreEqual(EditorTool.BpmSection, data.editorState.tool);

            Assert.IsNull(session.CreateTestPlayData(8.25f, 4, 1f, EditorTool.ShortNote, false, out string afterError), "뒤에 노트가 없으면 null");
            Assert.AreEqual(GameConstants.Editor.TestPlayNoNotesAfterMessage, afterError);

            // 돌아와서 되돌리기: ApplyChartData는 수정 여부를 false로 만들고, RestoreDirty로 되돌린다
            session.ApplyChartData(data.editorState.chart);
            Assert.IsFalse(session.IsDirty);
            session.RestoreDirty(data.editorState.isDirty);
            Assert.IsTrue(session.IsDirty);
            Assert.AreEqual(2, session.Data.Notes.Count);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
    /// <summary>
    /// 롱노트: 시작 위치가 롱노트 도중이면(시작이 시작 박 앞) 그 롱노트는 뺀다. 시작 박에서 시작하는 롱노트는 넣는다.
    /// CountNotesFrom은 FilterNotesFrom이 남기는 수와 같다.
    /// </summary>
    [Test]
    public void FilterNotesFrom_ExcludesLongInProgress()
    {
        List<NoteData> notes = new List<NoteData>
        {
            new NoteData { beat = 6f, endBeat = 10f, lane = 0, type = NoteData.TypeLong },
            new NoteData { beat = 8f, endBeat = 9f, lane = 1, type = NoteData.TypeLong },
            Short(8f, 2),
            new NoteData { beat = 7f + 2f / 3f, endBeat = 8f + 1f / 3f, lane = 3, type = NoteData.TypeLong }
        };

        List<NoteData> filtered = TestPlayRules.FilterNotesFrom(notes, 8f);
        Assert.AreEqual(2, filtered.Count, "도중인 롱노트 두 개(6~10박, 7.67~8.33박)는 빠진다");
        Assert.AreSame(notes[1], filtered[0], "8박에서 시작하는 롱노트는 넣는다");
        Assert.AreSame(notes[2], filtered[1]);
        Assert.AreEqual(filtered.Count, TestPlayRules.CountNotesFrom(notes, 8f));
        Assert.AreEqual(4, TestPlayRules.CountNotesFrom(notes, 0f));
        Assert.AreEqual(0, TestPlayRules.CountNotesFrom(notes, 8.25f));
        Assert.AreEqual(0, TestPlayRules.CountNotesFrom(null, 0f));
    }

    /// <summary>세션: 시작 위치 뒤에 도중인 롱노트만 있으면 테스트 플레이를 만들지 않는다 (칠 노트가 없다)</summary>
    [Test]
    public void Session_CreateTestPlayData_LongInProgressOnly()
    {
        GameObject owner = new GameObject("ChartEditSession_TestPlayLong");
        try
        {
            ChartEditSession session = owner.AddComponent<ChartEditSession>();
            session.Init(new EditorEntryData { songId = "__test__", chartName = "테스트", level = 2 }, "__test__", 150f, 0, 60f);
            Assert.IsTrue(session.AddLongNote(0, 48 * 4, 48 * 12, out _));

            Assert.IsNull(session.CreateTestPlayData(8f, 4, 1f, EditorTool.LongNote, false, out string error), "롱노트 도중에서 시작");
            Assert.AreEqual(GameConstants.Editor.TestPlayNoNotesAfterMessage, error);

            TestPlayData atStart = session.CreateTestPlayData(4f, 4, 1f, EditorTool.LongNote, false, out error);
            Assert.IsNotNull(atStart, error);
            Assert.AreEqual(1, TestPlayRules.CountNotesFrom(atStart.chart.notes, atStart.startBeat), "롱노트 시작 박에서 시작하면 넣는다");

            Assert.IsTrue(session.AddNote(1, 48 * 10, out _));
            TestPlayData middle = session.CreateTestPlayData(8f, 4, 1f, EditorTool.LongNote, false, out error);
            Assert.IsNotNull(middle, error);
            Assert.AreEqual(2, middle.chart.notes.Count, "채보에는 모든 노트가 들어 있다 (돌아올 때 되돌리기용)");
            List<NoteData> playable = TestPlayRules.FilterNotesFrom(middle.chart.notes, middle.startBeat);
            Assert.AreEqual(1, playable.Count, "인게임에서는 도중인 롱노트를 빼고 숏노트 하나만 친다");
            Assert.AreEqual(NoteData.TypeShort, playable[0].type);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
