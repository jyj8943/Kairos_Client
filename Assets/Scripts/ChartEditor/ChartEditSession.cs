using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 편집 중인 채보의 주인. EditorScene.unity 루트의 "ChartEditSession" 오브젝트에 붙이고, EditorScene이 Init()을 호출한다.
/// - 채보 정보(songId·chartId·이름·레벨·BPM), 노트(ChartEditData), 수정 여부(IsDirty)를 들고 있다.
/// - 노트 편집은 반드시 AddNote / AddLongNote / RemoveNote를 거친다. 성공한 편집만 실행 취소 기록(EditHistory)에 남는다.
/// - Undo / Redo도 같은 검사를 거치는 내부 경로(ApplyAddNote / ApplyRemoveNote)로 노트를 바꾸고 기록은 남기지 않는다.
/// - 수정 여부(IsDirty)는 기록의 저장 시점과 비교한다: 실행 취소·다시 실행으로 저장 시점에 돌아오면 false.
/// - BPM 구간(채보의 timingPoints)을 tick 순 목록으로 들고, 바뀔 때마다 TimingMap과 곡 끝(UpdateLength)을 다시 계산한 뒤 OnTimingChanged를 보낸다.
///   구간 편집(AddBpmSection / SetBpmSection / RemoveBpmSection / ResetBpmToSongDefault)도 실행 취소 기록에 남는다.
///   노트는 박(tick) 위치 그대로 두고 시간만 바뀐다 (기획서 3-6 A안).
/// - 표시 쪽(EditorNoteLayer, Canvas_Editor)은 Data를 읽기만 한다.
/// - 싱글톤이 아니다. EditorScene이 참조를 들고 필요한 곳에 넘겨준다.
/// </summary>
public class ChartEditSession : MonoBehaviour
{
    [Header("테스트 기본값 (씬을 직접 실행해 채보 정보가 없을 때. 곡은 곡 목록의 첫 곡)")]
    [SerializeField] private string testChartName = "테스트 채보";
    [SerializeField] private int testLevel = 1;

    // 확인용: 플레이 중 상태를 인스펙터에 보여 주기만 한다. 세션은 이 값을 읽지 않고 바뀔 때마다 덮어쓴다.
    [Header("확인용 (플레이 중 표시만, 바꿔도 동작에 영향 없음)")]
    [SerializeField] private string debugChartId;
    [SerializeField] private string debugChartName;
    [SerializeField] private int debugShortCount;
    [SerializeField] private int debugLongCount;
    [SerializeField] private bool debugIsDirty;

    // 필드는 빌드에도 두어 직렬화 구조를 에디터와 같게 맞추고, 채우는 코드만 UNITY_EDITOR 안에 둔다 (빌드에서는 비어 있음)
    [Header("확인용: 편집 중인 노트 목록. 여기서 값을 바꿔도 동작에 영향 없음")]
    [Tooltip("노트 전체 수와 표시 중인 수")]
    [SerializeField] private string debugNoteSummary;
    [Tooltip("확인용: 편집 중인 노트 목록 (tick·lane 순, 최대 500개). 여기서 값을 바꿔도 동작에 영향 없음")]
    [SerializeField] private List<DebugNoteRow> debugNotes = new List<DebugNoteRow>();

    /// <summary>수정 여부가 바뀌면 호출된다 (나중에 상단 바 "저장 안 됨" 표시용)</summary>
    public event Action<bool> OnDirtyChanged;

    /// <summary>BPM 구간·offset이 바뀌어 Timing·곡 끝이 다시 계산되면 호출된다 (그리드·목록·타임라인 갱신용)</summary>
    public event Action OnTimingChanged;

    public string SongId { get; private set; }

    /// <summary>새 채보면 null</summary>
    public string ChartId { get; private set; }

    public string ChartName { get; private set; }
    public int Level { get; private set; }

    /// <summary>곡 시작 구간(0박)의 BPM</summary>
    public float Bpm => bpmSections[0].Bpm;

    /// <summary>곡 기본 BPM (SongData의 BPM). 새 채보와 "곡 기본 BPM으로 되돌리기"에 쓴다</summary>
    public float SongDefaultBpm { get; private set; }

    /// <summary>BPM 구간 목록 (tick 순, 첫 구간은 0 tick). 읽기만 하고, 바꿀 때는 구간 편집 함수를 쓴다</summary>
    public IReadOnlyList<BpmSection> BpmSections => bpmSections;

    /// <summary>곡 기본 BPM 1개 상태면 true ("곡 기본 BPM으로 되돌리기" 비활성)</summary>
    public bool IsSongDefaultBpm => bpmSections.Count == 1 && BpmSectionRules.SameBpm(bpmSections[0].Bpm, SongDefaultBpm);

    private List<BpmSection> bpmSections = new List<BpmSection> { new BpmSection(0, 120f) };

    /// <summary>음원 시작부터 0박까지의 시간 (ms). 곡 정보(SongData)의 값</summary>
    public int OffsetMs { get; private set; }

    /// <summary>
    /// 박 ↔ 시간 변환 (편집 중인 BPM 구간 전체·offset). 구간이나 offset이 바뀔 때마다 다시 만든다.
    /// </summary>
    public TimingMap Timing { get; private set; }

    /// <summary>곡 길이 (초). 곡 음원 길이</summary>
    public float SongLengthSeconds { get; private set; }

    /// <summary>곡 끝 위치 (박) = 곡 길이를 offset·BPM으로 바꾼 값. 0 이상</summary>
    public float TotalBeats { get; private set; }

    /// <summary>편집 중인 노트. 표시용으로 읽기만 하고, 바꿀 때는 AddNote / RemoveNote를 쓴다</summary>
    public ChartEditData Data { get; private set; }

    /// <summary>마지막 저장(또는 시작) 뒤로 노트가 바뀌었으면 true</summary>
    public bool IsDirty { get; private set; }

    /// <summary>실행 취소 · 다시 실행 기록. 채보를 새로 시작하거나 불러오면 비운다</summary>
    private EditHistory history = new EditHistory();

    /// <summary>실행 취소할 편집이 있으면 true</summary>
    public bool CanUndo => history.CanUndo;

    /// <summary>다시 실행할 편집이 있으면 true</summary>
    public bool CanRedo => history.CanRedo;

    /// <summary>실행 취소 기록 수 (확인용)</summary>
    public int UndoCount => history.UndoCount;

    /// <summary>다시 실행 기록 수 (확인용)</summary>
    public int RedoCount => history.RedoCount;

    /// <summary>
    /// 곡 정보(BPM·offset·음원 길이)로 세션을 시작한다. EditorScene이 호출한다.
    /// </summary>
    /// <param name="entry">곡 고르기 씬이 넘긴 채보 정보. null이면 테스트 기본 채보 이름·레벨을 쓴다</param>
    public void Init(EditorEntryData entry, SongData song)
    {
        Init(entry, song.SongId, song.Bpm, song.OffsetMs, song.Length);
    }

    /// <summary>
    /// 채보 정보를 채우고 빈 노트 데이터를 만든다. 수정 여부는 false로 시작한다.
    /// 곡 끝 tick은 곡 길이를 offset·BPM으로 바꾼 박에서 구하고, 곡 끝을 넘지 않게 내림한다.
    /// (EditMode 테스트처럼 음원 없이 시작할 때도 이 함수를 쓴다)
    /// </summary>
    /// <param name="entry">곡 고르기 씬이 넘긴 채보 정보. null이면 테스트 기본 채보 이름·레벨을 쓴다</param>
    public void Init(EditorEntryData entry, string songId, float bpm, int offsetMs, float songLengthSeconds)
    {
        if (entry == null)
        {
            Debug.LogWarning("[ChartEditSession] 채보 정보가 없어 테스트 기본 채보 이름·레벨로 시작합니다. (EditorScene을 직접 실행한 경우)", this);
            entry = new EditorEntryData { songId = songId, chartName = testChartName, level = testLevel, chartId = null };
        }

        SongId = entry.songId;
        ChartId = entry.chartId;
        ChartName = entry.chartName;
        Level = entry.level;
        SongDefaultBpm = bpm;
        bpmSections = new List<BpmSection> { new BpmSection(0, bpm) };
        OffsetMs = offsetMs;
        Timing = BuildTiming();
        SongLengthSeconds = songLengthSeconds;
        TotalBeats = Mathf.Max(0f, (float)Timing.SecondsToBeat(songLengthSeconds));

        if (Data != null) Data.OnChanged -= RefreshDebugNotes;
        if (Data != null) Data.OnChanged -= RefreshDebugView;
        int totalTicks = Mathf.FloorToInt(TotalBeats * GameConstants.Editor.TicksPerBeat);
        Data = new ChartEditData(totalTicks, GameConstants.Editor.LaneCount);
        Data.OnChanged += RefreshDebugView;
        Data.OnChanged += RefreshDebugNotes;

        IsDirty = false;
        history = new EditHistory();
        RefreshDebugView();
        RefreshDebugNotes();

        Debug.Log($"[ChartEditSession] 시작: songId={SongId}, chartName={ChartName}, level={Level}, " +
                  $"chartId={ChartId ?? "(새 채보)"}, bpm={Bpm}, offsetMs={OffsetMs}, 곡 길이={SongLengthSeconds:0.###}초, " +
                  $"곡 끝 {TotalBeats:0.###}박 (tick {totalTicks})");
        // 내 채보 열기(chartId 있음)는 EditorScene이 파일을 읽어 ApplyChartData로 채운다
    }

    private void OnDestroy()
    {
        if (Data == null) return;

        Data.OnChanged -= RefreshDebugView;
        Data.OnChanged -= RefreshDebugNotes;
    }

    /// <summary>
    /// 숏노트를 추가한다. 성공하면 수정 상태가 된다.
    /// </summary>
    /// <returns>추가했으면 true. 범위 밖이거나 같은 자리에 이미 있으면 false</returns>
    public bool AddNote(int lane, int tick)
    {
        return AddNote(lane, tick, out _);
    }

    /// <summary>
    /// 숏노트를 추가한다. 실패하면 error에 이유가 들어간다 (상태 표시줄 안내용).
    /// </summary>
    public bool AddNote(int lane, int tick, out NoteEditError error)
    {
        if (!Data.TryAdd(lane, tick, out error)) return false;

        RecordEdit(new NoteEditAction(EditorNote.Short(lane, tick), true));
        return true;
    }

    /// <summary>
    /// 롱노트를 추가한다 (시작·끝은 어느 쪽이 커도 된다). 성공하면 수정 상태가 된다.
    /// </summary>
    /// <param name="error">실패 이유 (너무 짧음, 겹침, 범위 밖)</param>
    public bool AddLongNote(int lane, int startTick, int endTick, out NoteEditError error)
    {
        if (!Data.TryAddLong(lane, startTick, endTick, out error)) return false;

        // 드래그로 만든 롱노트 하나 = 기록 하나 (작은 쪽이 시작)
        RecordEdit(new NoteEditAction(new EditorNote(lane, Mathf.Min(startTick, endTick), Mathf.Max(startTick, endTick)), true));
        return true;
    }

    /// <summary>
    /// 노트 추가 실패 이유를 상태 표시줄 문구로 바꾼다. 안내할 필요가 없으면(범위 밖 등) null.
    /// </summary>
    public static string GetErrorMessage(NoteEditError error)
    {
        switch (error)
        {
            case NoteEditError.TooShort: return GameConstants.Editor.LongNoteTooShortMessage;
            case NoteEditError.Overlap: return GameConstants.Editor.NoteOverlapMessage;
            default: return null;
        }
    }

    /// <summary>
    /// 그 자리를 덮는 노트를 지운다 (롱노트는 시작·몸통·끝 어느 tick이든). 성공하면 수정 상태가 된다.
    /// </summary>
    /// <returns>지웠으면 true. 그 자리에 노트가 없으면 false</returns>
    public bool RemoveNote(int lane, int tick)
    {
        // 지우기 전에 어떤 노트인지 값으로 기억해 둔다 (롱노트는 시작·끝 tick)
        if (!Data.TryFindAt(lane, tick, out EditorNote removed)) return false;
        if (!Data.TryRemove(lane, tick)) return false;

        RecordEdit(new NoteEditAction(removed, false));
        return true;
    }

    /// <summary>
    /// 마지막 편집을 되돌린다 (Ctrl+Z). 저장 시점으로 돌아오면 수정 여부가 false가 된다.
    /// </summary>
    /// <returns>되돌린 편집의 설명 (예: "숏노트 추가"). 되돌릴 것이 없거나 실패하면 null</returns>
    public string Undo()
    {
        IEditAction action = history.Undo(this);
        SetDirty(!history.IsAtSavedState);
        return action != null ? action.Description : null;
    }

    /// <summary>
    /// 마지막으로 되돌린 편집을 다시 적용한다 (Ctrl+Y · Ctrl+Shift+Z).
    /// </summary>
    /// <returns>다시 적용한 편집의 설명. 없거나 실패하면 null</returns>
    public string Redo()
    {
        IEditAction action = history.Redo(this);
        SetDirty(!history.IsAtSavedState);
        return action != null ? action.Description : null;
    }

    /// <summary>
    /// 여러 편집을 실행 취소 한 번으로 묶기 시작한다 (다음 작업의 BPM 구간 삭제 등). EndEditGroup으로 끝낸다.
    /// </summary>
    public void BeginEditGroup(string description)
    {
        history.BeginGroup(description);
    }

    /// <summary>편집 묶음을 끝낸다. 묶은 편집이 없으면 기록하지 않는다</summary>
    public void EndEditGroup()
    {
        history.EndGroup();
    }

    /// <summary>
    /// 기록을 남기지 않고 노트를 넣는다 (실행 취소·다시 실행 전용). 검사는 AddNote / AddLongNote와 같다.
    /// </summary>
    internal bool ApplyAddNote(EditorNote note)
    {
        return note.IsLong
            ? Data.TryAddLong(note.Lane, note.Tick, note.EndTick, out _)
            : Data.TryAdd(note.Lane, note.Tick);
    }

    /// <summary>
    /// 기록을 남기지 않고 노트를 지운다 (실행 취소·다시 실행 전용). 그 자리 노트가 기록과 같은 노트(시작·끝 tick)일 때만 지운다.
    /// </summary>
    internal bool ApplyRemoveNote(EditorNote note)
    {
        if (!Data.TryFindAt(note.Lane, note.Tick, out EditorNote found)) return false;
        if (found.Tick != note.Tick || found.EndTick != note.EndTick) return false;

        return Data.TryRemove(note.Lane, note.Tick);
    }

    /// <summary>성공한 편집을 기록하고 수정 상태로 만든다 (새 편집은 항상 저장 시점에서 벗어난다)</summary>
    private void RecordEdit(IEditAction action)
    {
        history.Record(action);
        SetDirty(true);
    }

    /// <summary>
    /// 세션의 채보 정보와 노트를 채보 파일 형식으로 바꾼다 (beat = tick ÷ TicksPerBeat).
    /// 새 채보(chartId 없음)면 새 ID를 만들어 세션에 기록한다.
    /// </summary>
    /// <returns>바꾼 채보. 새 ID를 만들지 못하면 null</returns>
    public ChartData ToChartData()
    {
        if (string.IsNullOrEmpty(ChartId))
        {
            string newId = ChartRepository.CreateNewCustomChartId(SongId);
            if (newId == null) return null;

            ChartId = newId;
            RefreshDebugView();
        }

        return BuildChartData();
    }

    /// <summary>
    /// 세션의 채보 정보와 노트를 그대로 채보 형식으로 만든다. 새 ID를 만들지 않는다 (새 채보면 chartId가 null).
    /// </summary>
    public ChartData BuildChartData()
    {
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            chartId = ChartId,
            songId = SongId,
            type = ChartData.TypeCustom,
            chartName = ChartName,
            difficulty = string.Empty,
            author = string.Empty,
            keyCount = Data.LaneCount,
            level = Level,
            timingPoints = BpmSectionRules.ToTimingPoints(bpmSections),
            notes = Data.ToNoteDataList()
        };
    }

    /// <summary>
    /// 불러온 채보로 세션을 채운다. 노트는 tick = beat × TicksPerBeat를 반올림해 한 번에 바꾸고, 수정 여부는 false가 된다.
    /// 채보의 songId가 세션의 곡과 다르면 적용하지 않는다.
    /// </summary>
    public void ApplyChartData(ChartData chart)
    {
        if (chart == null)
        {
            Debug.LogError("[ChartEditSession] 적용할 채보가 없습니다.", this);
            return;
        }

        if (chart.songId != SongId)
        {
            Debug.LogError($"[ChartEditSession] 다른 곡의 채보라 적용하지 않습니다: 세션 songId={SongId}, 채보 songId={chart.songId}", this);
            return;
        }

        ChartId = chart.chartId;
        ChartName = chart.chartName;
        Level = chart.level;

        // BPM 구간을 전부 불러온다. 곡 끝 박은 편집에 쓰는 구간(채보 BPM)으로 다시 계산한다 (곡 BPM과 다를 수 있다)
        bpmSections = BpmSectionRules.FromTimingPoints(chart.timingPoints, SongDefaultBpm, out int skippedPoints);
        if (skippedPoints > 0)
            Debug.LogWarning($"[ChartEditSession] BPM이 0 이하이거나 같은 위치에 겹친 BPM 구간 {skippedPoints}개를 뺐습니다: chartId={ChartId}", this);
        Timing = BuildTiming();
        UpdateLength();

        List<EditorNote> notes = new List<EditorNote>();
        int roundedCount = 0;
        if (chart.notes != null)
        {
            foreach (NoteData note in chart.notes)
            {
                int tick = ToTick(note.beat, ref roundedCount);
                int endTick = note.type == NoteData.TypeLong ? ToTick(note.endBeat, ref roundedCount) : tick;
                notes.Add(new EditorNote(note.lane, tick, endTick));
            }
        }

        if (roundedCount > 0)
            Debug.LogWarning($"[ChartEditSession] 반올림 오차가 {GameConstants.Chart.MaxTickRoundingError}틱을 넘는 위치가 {roundedCount}개 있습니다: chartId={ChartId}", this);

        int skipped = Data.ReplaceAll(notes);
        if (skipped > 0)
            Debug.LogWarning($"[ChartEditSession] 곡 범위 밖이거나, 겹치거나, 너무 짧은 롱노트라 넣지 못한 노트가 {skipped}개 있습니다: chartId={ChartId}", this);

        // 불러온 채보가 새 시작점이다 (기록을 비우고 저장 시점으로 본다)
        history.Clear();
        IsDirty = false;
        RefreshDebugView();
        OnDirtyChanged?.Invoke(false);
        OnTimingChanged?.Invoke();
    }

    /// <summary>
    /// 채보를 나만의 채보로 저장한다. 성공하면 수정 여부가 false가 된다.
    /// (버튼·Ctrl+S 연결은 아직 하지 않았다)
    /// </summary>
    /// <param name="error">실패 이유. 성공하면 빈 문자열</param>
    public bool TrySave(out string error)
    {
        ChartData chart = ToChartData();
        if (chart == null)
        {
            error = "새 채보 ID를 만들지 못했습니다.";
            return false;
        }

        if (!ChartRepository.TrySave(chart, out error)) return false;

        history.MarkSaved();
        SetDirty(false);
        return true;
    }

    /// <summary>
    /// 테스트 플레이로 넘길 데이터를 만든다. 저장하지 않고, 수정 여부도 그대로 둔다.
    /// 노트가 없거나 시작 박 뒤(같은 박 포함)에 노트가 없으면 null과 이유를 돌려준다.
    /// </summary>
    /// <param name="startBeat">시작 위치 (그리드의 현재 위치 선 박)</param>
    /// <param name="snapDivision">돌아왔을 때 되돌릴 박자 스냅</param>
    /// <param name="gridHeightScale">돌아왔을 때 되돌릴 그리드 높이 배율</param>
    /// <param name="tool">돌아왔을 때 되돌릴 도구</param>
    /// <param name="autoPlay">자동 플레이로 시작하면 true (자동 테스트)</param>
    public TestPlayData CreateTestPlayData(float startBeat, int snapDivision, float gridHeightScale, EditorTool tool, bool autoPlay, out string error)
    {
        if (Data.Notes.Count == 0)
        {
            error = GameConstants.Editor.TestPlayNoNotesMessage;
            return null;
        }

        // 인게임과 같은 규칙(TestPlayRules.FilterNotesFrom)으로 칠 노트가 남는지 본다.
        // 시작 박 앞의 노트와, 시작 위치가 도중인 롱노트(시작이 시작 박 앞)는 빠진다
        ChartData chart = BuildChartData();
        if (TestPlayRules.CountNotesFrom(chart.notes, startBeat) == 0)
        {
            error = GameConstants.Editor.TestPlayNoNotesAfterMessage;
            return null;
        }

        error = string.Empty;
        return new TestPlayData
        {
            songId = SongId,
            chart = chart,
            startBeat = startBeat,
            autoPlay = autoPlay,
            editorState = new EditorRestoreState
            {
                chart = chart,
                isDirty = IsDirty,
                history = history,
                currentBeat = startBeat,
                snapDivision = snapDivision,
                gridHeightScale = gridHeightScale,
                tool = tool
            }
        };
    }

    /// <summary>
    /// 곡 시작 구간 BPM과 offset을 바꾼다 (Unity 에디터 전용 F1 곡 조정 도구). 다른 구간은 그대로 둔다.
    /// - offset은 곡 정보(SongData) 값이라 채보 수정이 아니다 (수정 여부·기록에 넣지 않는다).
    /// - 곡 시작 BPM이 바뀌면 채보의 timingPoints가 바뀌므로 구간 편집과 같이 기록한다 (Ctrl+Z로 되돌릴 수 있다).
    ///   개발 도구라 30 ~ 400 · 다음 구간과 같은 BPM 규칙은 보지 않고 경고만 남긴다.
    /// 노트는 박 단위라 그대로 두고 곡 끝 박만 다시 계산한다.
    /// </summary>
    public void SetTiming(float bpm, int offsetMs)
    {
        if (bpm <= 0f)
        {
            Debug.LogWarning($"[ChartEditSession] BPM은 0보다 커야 합니다: {bpm}", this);
            return;
        }

        bool bpmChanged = !Mathf.Approximately(Bpm, bpm);
        OffsetMs = offsetMs;
        if (!bpmChanged)
        {
            RebuildTiming();
            return;
        }

        if (bpmSections.Count > 1 && BpmSectionRules.SameBpm(bpmSections[1].Bpm, bpm))
            Debug.LogWarning($"[ChartEditSession] 곡 시작 BPM이 다음 구간({BpmSectionRules.FormatPosition(bpmSections[1].Tick)})과 같아졌습니다: {bpm}", this);

        List<BpmSection> after = new List<BpmSection>(bpmSections);
        after[0] = new BpmSection(0, bpm);
        ChangeBpmSections(GameConstants.Editor.HistoryTuneStartBpm, after);
    }

    /// <summary>
    /// tick 위치에 BPM 구간을 추가한다 (BPM은 소수 둘째 자리로 반올림). 성공하면 기록하고 수정 상태가 된다.
    /// </summary>
    /// <param name="error">실패 이유 (범위 밖, 이전·다음과 같은 BPM, 같은 위치)</param>
    public bool AddBpmSection(int tick, float bpm, out BpmSectionError error)
    {
        error = BpmSectionRules.ValidateAdd(bpmSections, tick, bpm, Data != null ? Data.TotalTicks : 0);
        if (error != BpmSectionError.None) return false;

        return ChangeBpmSections(GameConstants.Editor.HistoryAddBpmSection, BpmSectionRules.WithSection(bpmSections, tick, bpm));
    }

    /// <summary>
    /// tick 위치 구간의 BPM을 바꾼다 (곡 시작 구간도 가능). 같은 BPM이면 바꾸지 않고 true.
    /// </summary>
    public bool SetBpmSection(int tick, float bpm, out BpmSectionError error)
    {
        error = BpmSectionRules.ValidateSet(bpmSections, tick, bpm);
        if (error != BpmSectionError.None) return false;

        int index = BpmSectionRules.IndexOf(bpmSections, tick);
        if (BpmSectionRules.SameBpm(bpmSections[index].Bpm, bpm)) return true;

        return ChangeBpmSections(GameConstants.Editor.HistorySetBpmSection, BpmSectionRules.WithSection(bpmSections, tick, bpm));
    }

    /// <summary>
    /// tick 위치 구간을 지운다 (곡 시작 구간은 불가). 지운 뒤 앞뒤 구간 BPM이 같아지면 뒤 구간도 지우고, 둘을 기록 하나로 묶는다.
    /// </summary>
    /// <param name="removedNextTick">함께 지운 뒤 구간의 tick. 없으면 −1</param>
    public bool RemoveBpmSection(int tick, out BpmSectionError error, out int removedNextTick)
    {
        removedNextTick = -1;
        error = BpmSectionRules.ValidateRemove(bpmSections, tick);
        if (error != BpmSectionError.None) return false;

        int mergedTick = BpmSectionRules.GetMergedNextTick(bpmSections, tick);
        if (mergedTick < 0)
            return ChangeBpmSections(GameConstants.Editor.HistoryRemoveBpmSection, BpmSectionRules.WithoutSection(bpmSections, tick));

        history.BeginGroup(GameConstants.Editor.HistoryRemoveBpmSection);
        bool removed = ChangeBpmSections(GameConstants.Editor.HistoryRemoveBpmSection, BpmSectionRules.WithoutSection(bpmSections, tick))
                       && ChangeBpmSections(GameConstants.Editor.HistoryRemoveMergedBpmSection, BpmSectionRules.WithoutSection(bpmSections, mergedTick));
        history.EndGroup();
        if (removed) removedNextTick = mergedTick;
        return removed;
    }

    /// <summary>
    /// 모든 구간을 지우고 곡 기본 BPM 하나로 되돌린다 (기록 하나). 이미 곡 기본 BPM 1개면 false.
    /// </summary>
    public bool ResetBpmToSongDefault()
    {
        if (IsSongDefaultBpm) return false;
        return ChangeBpmSections(GameConstants.Editor.HistoryResetBpm, new List<BpmSection> { new BpmSection(0, SongDefaultBpm) });
    }

    /// <summary>
    /// 곡 기본 BPM을 바꾼다 (F1 곡 조정에서 SongData에 저장했을 때). 구간은 그대로 둔다.
    /// </summary>
    public void SetSongDefaultBpm(float bpm)
    {
        if (bpm > 0f) SongDefaultBpm = bpm;
    }

    /// <summary>
    /// 기록을 남기지 않고 구간 목록을 통째로 바꾼다 (실행 취소·다시 실행 전용). 첫 구간이 0 tick이 아니거나 비었으면 false.
    /// </summary>
    internal bool ApplyBpmSections(IReadOnlyList<BpmSection> sections)
    {
        if (sections == null || sections.Count == 0 || sections[0].Tick != 0)
        {
            Debug.LogWarning("[ChartEditSession] 곡 시작(0박) 구간이 없는 BPM 구간 목록은 적용하지 않습니다.", this);
            return false;
        }

        bpmSections = new List<BpmSection>(sections);
        RebuildTiming();
        return true;
    }

    /// <summary>구간 목록을 바꾸고 기록한다. 바뀐 것이 없으면 기록하지 않는다</summary>
    private bool ChangeBpmSections(string description, List<BpmSection> after)
    {
        List<BpmSection> before = new List<BpmSection>(bpmSections);
        if (!ApplyBpmSections(after)) return false;

        RecordEdit(new BpmSectionEditAction(description, before, after));
        return true;
    }

    /// <summary>지금 구간·offset으로 Timing과 곡 끝을 다시 만들고 알린다</summary>
    private void RebuildTiming()
    {
        Timing = BuildTiming();
        UpdateLength();
        RefreshDebugView();
        OnTimingChanged?.Invoke();
    }

    private TimingMap BuildTiming()
    {
        return new TimingMap(BpmSectionRules.ToTimingPoints(bpmSections), OffsetMs);
    }

    /// <summary>
    /// 곡 길이를 지금 박 ↔ 시간 변환(Timing)으로 박·tick으로 바꿔 곡 끝을 다시 정한다.
    /// </summary>
    private void UpdateLength()
    {
        TotalBeats = Mathf.Max(0f, (float)Timing.SecondsToBeat(SongLengthSeconds));
        if (Data == null) return;

        int outside = Data.SetTotalTicks(Mathf.FloorToInt(TotalBeats * GameConstants.Editor.TicksPerBeat));
        if (outside > 0)
            Debug.LogWarning($"[ChartEditSession] 곡 끝을 넘는 노트가 {outside}개 있습니다 (지우지 않았습니다): 곡 끝 {TotalBeats:0.###}박", this);
    }

    /// <summary>
    /// 테스트 플레이에서 돌아와 상태를 되돌릴 때 수정 여부를 되돌린다 (ApplyChartData가 false로 만들기 때문).
    /// </summary>
    public void RestoreDirty(bool dirty)
    {
        SetDirty(dirty);
    }

    /// <summary>
    /// 테스트 플레이에서 돌아와 실행 취소 기록과 수정 여부를 되돌린다 (ApplyChartData 뒤에 부른다. ApplyChartData가 기록을 비우기 때문).
    /// 기록이 없으면(예전 데이터) 수정 여부만 되돌린다. 기록의 저장 시점과 수정 여부가 다르면 수정 여부를 따른다.
    /// </summary>
    public void RestoreEditState(bool dirty, EditHistory restoredHistory)
    {
        if (restoredHistory != null)
        {
            history = restoredHistory;
            if (dirty && history.IsAtSavedState) history.MarkSavedUnreachable();
            else if (!dirty && !history.IsAtSavedState) history.MarkSaved();
        }
        SetDirty(dirty);
    }

    /// <summary>
    /// beat를 tick으로 반올림한다. 반올림 오차가 허용값을 넘으면 count를 늘린다.
    /// </summary>
    private static int ToTick(float beat, ref int roundedCount)
    {
        float exact = beat * GameConstants.Editor.TicksPerBeat;
        int tick = BeatGridMath.BeatToTick(beat);
        if (Mathf.Abs(exact - tick) > GameConstants.Chart.MaxTickRoundingError) roundedCount++;
        return tick;
    }

    private void SetDirty(bool dirty)
    {
        if (IsDirty == dirty) return;

        IsDirty = dirty;
        RefreshDebugView();
        OnDirtyChanged?.Invoke(dirty);
    }

    /// <summary>
    /// 인스펙터 확인용 값을 지금 상태로 덮어쓴다.
    /// </summary>
    private void RefreshDebugView()
    {
        debugChartId = ChartId ?? "(새 채보)";
        debugChartName = ChartName;
        debugShortCount = Data != null ? Data.ShortCount : 0;
        debugLongCount = Data != null ? Data.LongCount : 0;
        debugIsDirty = IsDirty;
    }

    /// <summary>
    /// 인스펙터 확인용 노트 목록을 지금 노트 전체로 다시 채운다 (tick·lane 순, 최대 DebugNoteLimit개).
    /// 에디터에서만 채우고 빌드에서는 아무것도 하지 않는다.
    /// </summary>
    private void RefreshDebugNotes()
    {
#if UNITY_EDITOR
        debugNotes.Clear();
        if (Data == null)
        {
            debugNoteSummary = string.Empty;
            return;
        }

        IReadOnlyList<EditorNote> notes = Data.Notes;
        int count = Mathf.Min(notes.Count, DebugNoteLimit);
        for (int i = 0; i < count; i++)
            debugNotes.Add(ToDebugRow(notes[i]));

        debugNoteSummary = notes.Count > DebugNoteLimit
            ? $"총 {notes.Count}개 중 {DebugNoteLimit}개 표시"
            : $"총 {notes.Count}개";
#endif
    }

#if UNITY_EDITOR
    /// <summary>인스펙터에 채우는 노트 수 상한. 노트가 많으면 인스펙터가 느려지므로 제한한다</summary>
    private const int DebugNoteLimit = 500;

    /// <summary>확인용 라인 키 이름 (A · S · ; · ' 순서)</summary>
    private static readonly string[] DebugLaneKeyNames = { "A", "S", ";", "'" };

    /// <summary>
    /// 노트 하나를 확인용 행으로 바꾼다. position은 "마디 M · B박 · T/48" 형식이다 (마디·박은 1부터).
    /// </summary>
    private static DebugNoteRow ToDebugRow(EditorNote note)
    {
        int ticksPerBeat = GameConstants.Editor.TicksPerBeat;
        int beatIndex = note.Tick / ticksPerBeat;
        int measure = beatIndex / GameConstants.Editor.BeatsPerMeasure + 1;
        int beatInMeasure = beatIndex % GameConstants.Editor.BeatsPerMeasure + 1;
        int tickInBeat = note.Tick % ticksPerBeat;

        return new DebugNoteRow
        {
            lane = note.Lane,
            keyName = note.Lane >= 0 && note.Lane < DebugLaneKeyNames.Length ? DebugLaneKeyNames[note.Lane] : "?",
            tick = note.Tick,
            beat = BeatGridMath.TickToBeat(note.Tick),
            position = $"마디 {measure} · {beatInMeasure}박 · {tickInBeat}/{ticksPerBeat}",
            type = note.IsLong ? NoteData.TypeLong : NoteData.TypeShort,
            endTick = note.EndTick
        };
    }
#endif
}
