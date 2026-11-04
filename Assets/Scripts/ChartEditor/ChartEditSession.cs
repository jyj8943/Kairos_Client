using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 편집 중인 채보의 주인. EditorScene.unity 루트의 "ChartEditSession" 오브젝트에 붙이고, EditorScene이 Init()을 호출한다.
/// - 채보 정보(songId·chartId·이름·레벨·BPM), 노트(ChartEditData), 수정 여부(IsDirty)를 들고 있다.
/// - 노트 편집은 반드시 AddNote / RemoveNote를 거친다. 나중에 실행 취소 기록을 이 두 함수에 넣는다.
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

    public string SongId { get; private set; }

    /// <summary>새 채보면 null</summary>
    public string ChartId { get; private set; }

    public string ChartName { get; private set; }
    public int Level { get; private set; }
    public float Bpm { get; private set; }

    /// <summary>음원 시작부터 0박까지의 시간 (ms). 곡 정보(SongData)의 값</summary>
    public int OffsetMs { get; private set; }

    /// <summary>곡 길이 (초). 곡 음원 길이</summary>
    public float SongLengthSeconds { get; private set; }

    /// <summary>곡 끝 위치 (박) = 곡 길이를 offset·BPM으로 바꾼 값. 0 이상</summary>
    public float TotalBeats { get; private set; }

    /// <summary>편집 중인 노트. 표시용으로 읽기만 하고, 바꿀 때는 AddNote / RemoveNote를 쓴다</summary>
    public ChartEditData Data { get; private set; }

    /// <summary>마지막 저장(또는 시작) 뒤로 노트가 바뀌었으면 true</summary>
    public bool IsDirty { get; private set; }

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
        Bpm = bpm;
        OffsetMs = offsetMs;
        SongLengthSeconds = songLengthSeconds;
        TotalBeats = Mathf.Max(0f, BeatGridMath.SecondsToBeat(songLengthSeconds, bpm, offsetMs));

        if (Data != null) Data.OnChanged -= RefreshDebugNotes;
        if (Data != null) Data.OnChanged -= RefreshDebugView;
        int totalTicks = Mathf.FloorToInt(TotalBeats * GameConstants.Editor.TicksPerBeat);
        Data = new ChartEditData(totalTicks, GameConstants.Editor.LaneCount);
        Data.OnChanged += RefreshDebugView;
        Data.OnChanged += RefreshDebugNotes;

        IsDirty = false;
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
        // TODO: 실행 취소 기록
        if (!Data.TryAdd(lane, tick)) return false;

        SetDirty(true);
        return true;
    }

    /// <summary>
    /// 같은 자리의 노트를 지운다. 성공하면 수정 상태가 된다.
    /// </summary>
    /// <returns>지웠으면 true. 그 자리에 노트가 없으면 false</returns>
    public bool RemoveNote(int lane, int tick)
    {
        // TODO: 실행 취소 기록
        if (!Data.TryRemove(lane, tick)) return false;

        SetDirty(true);
        return true;
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
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = Bpm } },
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

        if (chart.timingPoints != null && chart.timingPoints.Count > 0)
        {
            Bpm = chart.timingPoints[0].bpm;
            if (chart.timingPoints.Count > 1)
                Debug.LogWarning($"[ChartEditSession] BPM 변속은 아직 지원하지 않아 첫 BPM({Bpm})만 씁니다: chartId={ChartId}", this);
        }

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
            Debug.LogWarning($"[ChartEditSession] 곡 범위 밖이거나 겹쳐서 넣지 못한 노트가 {skipped}개 있습니다: chartId={ChartId}", this);

        IsDirty = false;
        RefreshDebugView();
        OnDirtyChanged?.Invoke(false);
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
    public TestPlayData CreateTestPlayData(float startBeat, int snapDivision, float gridHeightScale, EditorTool tool, out string error)
    {
        if (Data.Notes.Count == 0)
        {
            error = GameConstants.Editor.TestPlayNoNotesMessage;
            return null;
        }

        int startTick = BeatGridMath.BeatToTick(startBeat);
        if (Data.Notes[Data.Notes.Count - 1].Tick < startTick)
        {
            error = GameConstants.Editor.TestPlayNoNotesAfterMessage;
            return null;
        }

        ChartData chart = BuildChartData();
        error = string.Empty;
        return new TestPlayData
        {
            songId = SongId,
            chart = chart,
            startBeat = startBeat,
            editorState = new EditorRestoreState
            {
                chart = chart,
                isDirty = IsDirty,
                currentBeat = startBeat,
                snapDivision = snapDivision,
                gridHeightScale = gridHeightScale,
                tool = tool
            }
        };
    }

    /// <summary>
    /// 테스트 플레이에서 돌아와 상태를 되돌릴 때 수정 여부를 되돌린다 (ApplyChartData가 false로 만들기 때문).
    /// </summary>
    public void RestoreDirty(bool dirty)
    {
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
