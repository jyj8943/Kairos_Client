using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 에디터 BPM 구간 EditMode 테스트 (기획서 3-6 BPM 구간 규칙).
/// - BpmSectionRules: 범위·반올림·이전/다음과 같은 BPM·같은 위치·곡 시작 삭제 불가, 뒤 노트 수, 위치 문구
/// - ChartEditSession: 저장·불러오기 왕복, 추가·수정·삭제·되돌리기 Undo/Redo, 삭제 병합, 노트 tick 유지·시간 변화, 메트로놈 박 시간
/// 확인용 여러 구간 채보는 여기서 만든다 (Resources 공식 채보에 넣지 않는다).
/// </summary>
public class BpmSectionTests
{
    private const string SongId = "__test_bpm__";
    private const int T = GameConstants.Editor.TicksPerBeat;

    private GameObject owner;
    private ChartEditSession session;

    [SetUp]
    public void SetUp()
    {
        DeleteTestFolder();
        owner = new GameObject("ChartEditSession_Bpm");
        session = owner.AddComponent<ChartEditSession>();
        // 150 BPM · 120초 = 300박
        session.Init(new EditorEntryData { songId = SongId, chartName = "BPM 구간", level = 1 }, SongId, 150f, 0, 120f);
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

    private static List<BpmSection> Sections(params float[] tickBpm)
    {
        List<BpmSection> list = new List<BpmSection>();
        for (int i = 0; i < tickBpm.Length; i += 2) list.Add(new BpmSection((int)tickBpm[i], tickBpm[i + 1]));
        return list;
    }

    private void AssertSections(params float[] tickBpm)
    {
        List<string> expected = new List<string>();
        for (int i = 0; i < tickBpm.Length; i += 2) expected.Add($"{(int)tickBpm[i]}:{tickBpm[i + 1]:0.##}");
        List<string> actual = new List<string>();
        foreach (BpmSection s in session.BpmSections) actual.Add($"{s.Tick}:{s.Bpm:0.##}");
        CollectionAssert.AreEqual(expected, actual);
    }

    // ───── 1. BpmSectionRules ─────

    /// <summary>1-a. 범위 30 ~ 400, 숫자 아님, 소수 둘째 자리 반올림</summary>
    [Test]
    public void Rules_RangeAndRounding()
    {
        Assert.AreEqual(BpmSectionError.OutOfRange, BpmSectionRules.TryParseBpm("29.99", out _));
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.TryParseBpm("30", out float min));
        Assert.AreEqual(30f, min);
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.TryParseBpm("400", out _));
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.TryParseBpm("400.004", out float near), "반올림하면 400");
        Assert.AreEqual(400f, near);
        Assert.AreEqual(BpmSectionError.OutOfRange, BpmSectionRules.TryParseBpm("400.01", out _));
        Assert.AreEqual(BpmSectionError.OutOfRange, BpmSectionRules.TryParseBpm("abc", out _), "숫자 아님");
        Assert.AreEqual(BpmSectionError.OutOfRange, BpmSectionRules.TryParseBpm("", out _));
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.TryParseBpm("123.456", out float rounded));
        Assert.AreEqual(123.46f, rounded, 1e-4f, "소수 둘째 자리로 반올림");
        Assert.AreEqual(GameConstants.Editor.BpmSectionOutOfRangeMessage, BpmSectionRules.GetMessage(BpmSectionError.OutOfRange));
        Assert.AreEqual("30 ~ 400 사이의 숫자를 입력해 주세요.", BpmSectionRules.GetMessage(BpmSectionError.OutOfRange));
    }

    /// <summary>1-b. 이전·다음과 같은 BPM(소수 둘째 자리 기준), 같은 위치, 곡 시작 삭제 불가</summary>
    [Test]
    public void Rules_SameBpmDuplicateAndStart()
    {
        List<BpmSection> s = Sections(0, 150, 32 * T, 200);
        Assert.AreEqual(BpmSectionError.SameAsPrevious, BpmSectionRules.ValidateAdd(s, 16 * T, 150f, 0));
        Assert.AreEqual(BpmSectionError.SameAsPrevious, BpmSectionRules.ValidateAdd(s, 16 * T, 150.004f, 0), "소수 둘째 자리 기준으로 같음");
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.ValidateAdd(s, 16 * T, 150.01f, 0));
        Assert.AreEqual(BpmSectionError.SameAsNext, BpmSectionRules.ValidateAdd(s, 16 * T, 200f, 0));
        Assert.AreEqual(BpmSectionError.DuplicatePosition, BpmSectionRules.ValidateAdd(s, 32 * T, 180f, 0));
        Assert.AreEqual(BpmSectionError.OutOfRange, BpmSectionRules.ValidateAdd(s, 16 * T, 401f, 0));
        Assert.AreEqual(BpmSectionError.SameAsPrevious, BpmSectionRules.ValidateSet(s, 32 * T, 150f));
        Assert.AreEqual(BpmSectionError.SameAsNext, BpmSectionRules.ValidateSet(s, 0, 200f), "곡 시작 구간 수정도 다음과 같으면 불가");
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.ValidateSet(s, 0, 160f), "곡 시작 구간은 고칠 수 있다");
        Assert.AreEqual(BpmSectionError.CannotRemoveStart, BpmSectionRules.ValidateRemove(s, 0));
        Assert.AreEqual(BpmSectionError.None, BpmSectionRules.ValidateRemove(s, 32 * T));
        Assert.AreEqual(BpmSectionError.NotFound, BpmSectionRules.ValidateRemove(s, 8 * T));
        Assert.AreEqual("이전 BPM 구간과 같은 BPM을 입력할 수 없습니다.", BpmSectionRules.GetMessage(BpmSectionError.SameAsPrevious));
        Assert.AreEqual("다음 BPM 구간과 같은 BPM을 입력할 수 없습니다.", BpmSectionRules.GetMessage(BpmSectionError.SameAsNext));

        // 세션: 곡 시작 구간은 지울 수 없다
        Assert.IsFalse(session.RemoveBpmSection(0, out BpmSectionError error, out _));
        Assert.AreEqual(BpmSectionError.CannotRemoveStart, error);
        Assert.IsFalse(session.CanUndo, "실패는 기록하지 않는다");
    }

    // ───── 2. 저장·불러오기 왕복 ─────

    /// <summary>2. 추가 → 저장 → 불러오기 왕복에서 timingPoints가 그대로 (셋잇단 위치 포함)</summary>
    [Test]
    public void SaveAndLoad_KeepsTimingPoints()
    {
        Assert.IsTrue(session.AddBpmSection(16 * T, 200f, out _));
        Assert.IsTrue(session.AddBpmSection(98 * T + T / 2, 175.25f, out _), "98.5박");
        Assert.IsTrue(session.AddBpmSection(40 * T + 16, 123.46f, out _), "40 + 1/3박");
        session.AddNote(0, 20 * T);
        Assert.IsTrue(session.TrySave(out string saveError), saveError);

        Assert.IsTrue(ChartRepository.TryLoad(SongId, session.ChartId, ChartType.Custom, out ChartData loaded, out string loadError), loadError);
        Assert.AreEqual(4, loaded.timingPoints.Count);
        Assert.AreEqual(16f, loaded.timingPoints[1].beat, 1e-5f);
        Assert.AreEqual(200f, loaded.timingPoints[1].bpm, 1e-4f);

        GameObject other = new GameObject("ChartEditSession_Bpm_Load");
        try
        {
            ChartEditSession reopened = other.AddComponent<ChartEditSession>();
            reopened.Init(new EditorEntryData { songId = SongId, chartName = "x", level = 1, chartId = session.ChartId }, SongId, 150f, 0, 120f);
            reopened.ApplyChartData(loaded);
            Assert.AreEqual(session.BpmSections.Count, reopened.BpmSections.Count);
            for (int i = 0; i < session.BpmSections.Count; i++)
            {
                Assert.AreEqual(session.BpmSections[i].Tick, reopened.BpmSections[i].Tick, $"{i}번 구간 tick");
                Assert.AreEqual(session.BpmSections[i].Bpm, reopened.BpmSections[i].Bpm, 1e-4f, $"{i}번 구간 BPM");
            }
            Assert.AreEqual(session.Timing.BeatToSeconds(120), reopened.Timing.BeatToSeconds(120), 1e-9, "시간 변환도 같다");
            Assert.IsFalse(reopened.IsDirty);
            Assert.IsFalse(reopened.CanUndo, "불러오면 기록은 비어 있다");
        }
        finally
        {
            Object.DestroyImmediate(other);
        }
    }

    // ───── 3. 추가·수정·삭제·되돌리기 Undo/Redo ─────

    /// <summary>3. 추가·수정·삭제·곡 기본 BPM으로 되돌리기를 각각 Undo/Redo</summary>
    [Test]
    public void Edits_UndoRedo()
    {
        int timingEvents = 0;
        session.OnTimingChanged += () => timingEvents++;

        Assert.IsTrue(session.AddBpmSection(16 * T, 200f, out _));
        AssertSections(0, 150, 16 * T, 200);
        Assert.AreEqual(GameConstants.Editor.HistoryAddBpmSection, session.Undo());
        AssertSections(0, 150);
        Assert.AreEqual(GameConstants.Editor.HistoryAddBpmSection, session.Redo());
        AssertSections(0, 150, 16 * T, 200);

        Assert.IsTrue(session.SetBpmSection(16 * T, 180f, out _));
        Assert.IsTrue(session.SetBpmSection(0, 140f, out _), "곡 시작 구간 수정");
        AssertSections(0, 140, 16 * T, 180);
        Assert.AreEqual(GameConstants.Editor.HistorySetBpmSection, session.Undo());
        AssertSections(0, 150, 16 * T, 180);
        session.Undo();
        AssertSections(0, 150, 16 * T, 200);
        session.Redo();
        session.Redo();
        AssertSections(0, 140, 16 * T, 180);

        Assert.IsTrue(session.RemoveBpmSection(16 * T, out _, out int merged));
        Assert.AreEqual(-1, merged);
        AssertSections(0, 140);
        Assert.AreEqual(GameConstants.Editor.HistoryRemoveBpmSection, session.Undo());
        AssertSections(0, 140, 16 * T, 180);

        Assert.IsTrue(session.ResetBpmToSongDefault());
        AssertSections(0, 150);
        Assert.IsTrue(session.IsSongDefaultBpm);
        Assert.IsFalse(session.ResetBpmToSongDefault(), "이미 곡 기본 BPM 1개");
        Assert.AreEqual(GameConstants.Editor.HistoryResetBpm, session.Undo());
        AssertSections(0, 140, 16 * T, 180);
        Assert.IsFalse(session.IsSongDefaultBpm);
        session.Redo();
        AssertSections(0, 150);

        Assert.Greater(timingEvents, 10, "구간이 바뀔 때마다(실행 취소 포함) OnTimingChanged");

        // 실패한 편집은 기록하지 않는다
        int undoCount = session.UndoCount;
        Assert.IsFalse(session.AddBpmSection(16 * T, 150f, out BpmSectionError error));
        Assert.AreEqual(BpmSectionError.SameAsPrevious, error);
        Assert.AreEqual(undoCount, session.UndoCount);

        // 끝까지 되돌리면 처음 상태라 저장할 것이 없다
        while (session.Undo() != null) { }
        Assert.IsFalse(session.IsDirty);
        AssertSections(0, 150);
    }

    // ───── 4. 삭제 병합 ─────

    /// <summary>4. 삭제로 앞뒤 BPM이 같아지면 뒤 구간도 지워지고, Undo 한 번에 둘 다 복구</summary>
    [Test]
    public void Remove_MergesSameNeighbors()
    {
        session.AddBpmSection(16 * T, 200f, out _);
        session.AddBpmSection(32 * T, 150f, out _);
        session.AddBpmSection(48 * T, 175f, out _);
        Assert.AreEqual(32 * T, BpmSectionRules.GetMergedNextTick(session.BpmSections, 16 * T));
        int undoBefore = session.UndoCount;

        Assert.IsTrue(session.RemoveBpmSection(16 * T, out _, out int merged));
        Assert.AreEqual(32 * T, merged, "같은 BPM(150)이 된 다음 구간도 함께");
        AssertSections(0, 150, 48 * T, 175);
        Assert.AreEqual(undoBefore + 1, session.UndoCount, "기록 하나로 묶임");

        Assert.AreEqual(GameConstants.Editor.HistoryRemoveBpmSection, session.Undo());
        AssertSections(0, 150, 16 * T, 200, 32 * T, 150, 48 * T, 175);
        session.Redo();
        AssertSections(0, 150, 48 * T, 175);
    }

    // ───── 5. 노트 tick 유지, 시간 변화 ─────

    /// <summary>5. 구간을 넣어도 노트 tick은 그대로, 노트 시간(TimingMap)은 바뀐다. 곡 끝 박도 다시 계산</summary>
    [Test]
    public void AddSection_KeepsTicksChangesTime()
    {
        session.AddNote(1, 10 * T);
        session.AddLongNote(2, 4 * T, 12 * T, out _);
        double before = session.Timing.BeatToSeconds(10);
        Assert.AreEqual(4.0, before, 1e-9, "150 BPM: 10박 = 4초");
        float totalBefore = session.TotalBeats;

        Assert.IsTrue(session.AddBpmSection(8 * T, 300f, out _));
        Assert.IsTrue(session.Data.TryFindAt(1, 10 * T, out EditorNote shortNote));
        Assert.AreEqual(10 * T, shortNote.Tick, "tick 그대로");
        Assert.IsTrue(session.Data.TryFindAt(2, 4 * T, out EditorNote longNote));
        Assert.AreEqual(12 * T, longNote.EndTick, "롱노트 끝 tick도 그대로");
        Assert.AreEqual(8 * 0.4 + 2 * 0.2, session.Timing.BeatToSeconds(10), 1e-9, "8박까지 150, 뒤는 300 BPM");
        Assert.AreEqual(4 * 0.4, session.Timing.BeatToSeconds(4), 1e-9, "구간 앞 노트 시간은 그대로");
        Assert.Greater(session.TotalBeats, totalBefore, "BPM이 오르면 같은 곡 길이에 박이 더 많다");
        Assert.AreEqual(Mathf.FloorToInt(session.TotalBeats * T), session.Data.TotalTicks);

        // 테스트 플레이 채보에도 구간이 전부 들어간다
        ChartData chart = session.BuildChartData();
        Assert.AreEqual(2, chart.timingPoints.Count);
        Assert.AreEqual(session.Timing.BeatToSeconds(10), TimingMap.FromChart(chart, 0).BeatToSeconds(10), 1e-9, "인게임 TimingMap.FromChart와 같다");
    }

    // ───── 6. 뒤 노트 수 ─────

    /// <summary>6. "뒤 노트 N개": 구간 위치보다 뒤에서 시작하는 노트 (롱노트는 시작 기준, 같은 tick은 시간이 안 바뀌어 제외)</summary>
    [Test]
    public void CountNotesAfter()
    {
        session.AddNote(0, 0);
        session.AddNote(1, 8 * T);              // 구간과 같은 위치 → 제외
        session.AddLongNote(2, 6 * T, 12 * T, out _); // 구간 앞에서 시작 → 제외
        session.AddLongNote(3, 9 * T, 11 * T, out _); // 뒤에서 시작 → 포함
        session.AddNote(0, 20 * T);             // 포함
        Assert.AreEqual(2, BpmSectionRules.CountNotesAfter(session.Data.Notes, 8 * T));
        Assert.AreEqual(5, BpmSectionRules.CountNotesAfter(session.Data.Notes, -1));
        Assert.AreEqual(0, BpmSectionRules.CountNotesAfter(session.Data.Notes, 20 * T));
    }

    // ───── 7. 위치 문구 ─────

    /// <summary>7. 마디·박 문구 (0박 → 마디 1 · 1박, 66박 → 마디 17 · 3박, 98.5박 → 마디 25 · 3.5박), 적용 범위</summary>
    [Test]
    public void Formats()
    {
        Assert.AreEqual("마디 1 · 1박", BpmSectionRules.FormatPosition(0f));
        Assert.AreEqual("마디 17 · 3박", BpmSectionRules.FormatPosition(66f));
        Assert.AreEqual("마디 25 · 3.5박", BpmSectionRules.FormatPosition(98.5f));
        Assert.AreEqual("마디 17 · 1박", BpmSectionRules.FormatPosition(64 * T));
        Assert.AreEqual("마디 11 · 1.33박", BpmSectionRules.FormatPosition(40 * T + 16), "셋잇단은 소수 둘째 자리까지");

        List<BpmSection> s = Sections(0, 150, 64 * T, 200, 98 * T, 175);
        Assert.AreEqual("마디 17 ~ 마디 25 3박 (다음 구간 전까지)", BpmSectionRules.FormatRange(s, 64 * T));
        Assert.AreEqual("마디 25 3박 ~ 곡 끝", BpmSectionRules.FormatRange(s, 98 * T));
        Assert.AreEqual("마디 9 ~ 마디 17 (다음 구간 전까지)", BpmSectionRules.FormatRange(s, 32 * T), "추가 모드(아직 없는 위치)");
        Assert.AreEqual("다음: 마디 17 → 200", string.Format(GameConstants.Editor.NextBpmFormat, BpmSectionRules.FormatShortPosition(64 * T), SongData.FormatBpm(200f)));
    }

    // ───── 8. 메트로놈 ─────

    /// <summary>8. 메트로놈 박 시간(EditorPlayback: session.Timing.BeatToSeconds(정수 박))이 구간별 BPM을 따른다</summary>
    [Test]
    public void Metronome_FollowsSections()
    {
        session.AddBpmSection(16 * T, 200f, out _);
        TimingMap timing = session.Timing;
        Assert.AreEqual(60.0 / 150.0, timing.BeatToSeconds(16) - timing.BeatToSeconds(15), 1e-9, "구간 앞: 150 BPM 간격");
        Assert.AreEqual(60.0 / 200.0, timing.BeatToSeconds(17) - timing.BeatToSeconds(16), 1e-9, "구간 뒤: 200 BPM 간격");
        Assert.AreEqual(16, EditorPlaybackMath.GetFirstClickBeat(15.5f + 0.5f), "재생 시작 박부터 정수 박마다 친다");
        Assert.IsTrue(EditorPlaybackMath.IsMeasureStart(16), "마디는 4박마다 (구간과 상관없음)");
    }

    // ───── F1 곡 조정 ─────

    /// <summary>F1 곡 조정은 곡 시작 구간 BPM과 offset만 바꾼다 (다른 구간 그대로)</summary>
    [Test]
    public void Tuning_ChangesOnlyStartSection()
    {
        session.AddBpmSection(16 * T, 200f, out _);
        session.SetTiming(160f, 40);
        AssertSections(0, 160, 16 * T, 200);
        Assert.AreEqual(40, session.Timing.OffsetMs);
        Assert.AreEqual(0.04 + 16 * 60.0 / 160.0, session.Timing.BeatToSeconds(16), 1e-9);
        session.Undo();
        AssertSections(0, 150, 16 * T, 200);
        Assert.AreEqual(40, session.OffsetMs, "offset은 기록하지 않는다");
    }

    /// <summary>채보 timingPoints 불러오기: 정렬, 0 이하 BPM·같은 위치 제외, 첫 구간은 0박</summary>
    [Test]
    public void FromTimingPoints_Sanitizes()
    {
        List<TimingPointData> points = new List<TimingPointData>
        {
            new TimingPointData { beat = 16f, bpm = 200f },
            new TimingPointData { beat = 2f, bpm = 140f },
            new TimingPointData { beat = 16f, bpm = 210f },
            new TimingPointData { beat = 20f, bpm = 0f },
        };
        List<BpmSection> sections = BpmSectionRules.FromTimingPoints(points, 150f, out int skipped);
        Assert.AreEqual(2, skipped);
        Assert.AreEqual(0, sections[0].Tick, "첫 구간은 0박으로 본다 (TimingMap과 같음)");
        Assert.AreEqual(140f, sections[0].Bpm);
        Assert.AreEqual(16 * T, sections[1].Tick);
        Assert.AreEqual(1, BpmSectionRules.FromTimingPoints(null, 150f, out _).Count, "없으면 곡 기본 BPM 1개");
    }
}
