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
    [Header("테스트 기본값 (씬을 직접 실행해 채보 정보가 없을 때)")]
    [SerializeField] private string testSongId = "test_song";
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

    /// <summary>편집 중인 노트. 표시용으로 읽기만 하고, 바꿀 때는 AddNote / RemoveNote를 쓴다</summary>
    public ChartEditData Data { get; private set; }

    /// <summary>마지막 저장(또는 시작) 뒤로 노트가 바뀌었으면 true</summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// 채보 정보를 채우고 빈 노트 데이터를 만든다. 수정 여부는 false로 시작한다.
    /// </summary>
    /// <param name="entry">곡 고르기 씬이 넘긴 채보 정보. null이면 테스트 기본값을 쓴다</param>
    public void Init(EditorEntryData entry, float bpm, float songLengthSeconds)
    {
        if (entry == null)
        {
            Debug.LogWarning("[ChartEditSession] 채보 정보가 없어 테스트 기본값으로 시작합니다. (EditorScene을 직접 실행한 경우)", this);
            entry = new EditorEntryData { songId = testSongId, chartName = testChartName, level = testLevel, chartId = null };
        }

        SongId = entry.songId;
        ChartId = entry.chartId;
        ChartName = entry.chartName;
        Level = entry.level;
        Bpm = bpm;

        if (Data != null) Data.OnChanged -= RefreshDebugNotes;
        if (Data != null) Data.OnChanged -= RefreshDebugView;
        int totalTicks = BeatGridMath.BeatToTick(BeatGridMath.SecondsToBeats(songLengthSeconds, bpm));
        Data = new ChartEditData(totalTicks, GameConstants.Editor.LaneCount);
        Data.OnChanged += RefreshDebugView;
        Data.OnChanged += RefreshDebugNotes;

        IsDirty = false;
        RefreshDebugView();
        RefreshDebugNotes();

        Debug.Log($"[ChartEditSession] 시작: songId={SongId}, chartName={ChartName}, level={Level}, " +
                  $"chartId={ChartId ?? "(새 채보)"}, bpm={Bpm}, 곡 끝 tick={totalTicks}");

        // TODO: Load(chartId) — chartId가 있으면 JSON을 불러와 ChartEditData에 넣는다 (beat → BeatGridMath.BeatToTick)
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

    // TODO: Save() — ChartData로 바꿔 JSON 저장, 저장 후 SetDirty(false)
    // TODO: CreateTestPlayData() / Restore() — 테스트 플레이로 넘길 데이터 만들기와 돌아왔을 때 복원

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
