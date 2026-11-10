using System;
using System.Collections.Generic;

/// <summary>
/// 편집 중인 채보의 노트를 관리한다. 노트 데이터는 이 클래스 한곳에서만 바꾼다.
/// - 노트 목록은 (tick, lane) 순으로 정렬해 두고, 찾기·구간 검색은 이진 탐색으로 한다.
/// - 노트가 바뀌면 OnChanged를 보낸다. 표시(EditorNoteLayer)와 노트 수 표시는 이 이벤트를 구독한다.
/// - 롱노트는 [시작 tick, 끝 tick] 구간을 차지한다 (양 끝 포함). 같은 라인에서 다른 노트와 구간이 겹치면 넣지 않는다.
///   끝이 다른 노트 위치와 같은 tick이어도 겹침이다. 롱노트 최소 길이는 GameConstants.Editor.MinLongNoteTicks(1/4박).
/// </summary>
public class ChartEditData
{
    private readonly List<EditorNote> notes = new List<EditorNote>();
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<EditorNote> readOnlyNotes;

    /// <summary>
    /// 롱노트 길이(끝 − 시작 tick)의 상한. 어떤 tick을 덮는 노트는 시작 tick이 (tick − 이 값) 이상이라,
    /// 구간 검색을 그 앞부터만 하면 된다. 지울 때는 줄이지 않는다 (상한으로만 쓰므로 맞다). ReplaceAll에서 다시 센다.
    /// </summary>
    private int maxLongLength;

    /// <summary>노트 전체를 (tick, lane) 순으로 읽는다. 바깥에서 목록을 바꿀 수 없다</summary>
    public IReadOnlyList<EditorNote> Notes => readOnlyNotes;

    /// <summary>노트가 추가·삭제되면 호출된다</summary>
    public event Action OnChanged;

    /// <summary>곡 끝 tick. 노트는 0 ~ TotalTicks(포함)에만 둘 수 있다</summary>
    public int TotalTicks { get; private set; }

    /// <summary>라인 수</summary>
    public int LaneCount { get; }

    public int ShortCount { get; private set; }
    public int LongCount { get; private set; }

    public ChartEditData(int totalTicks, int laneCount)
    {
        TotalTicks = Math.Max(0, totalTicks);
        LaneCount = laneCount;
        readOnlyNotes = notes.AsReadOnly();
    }

    /// <summary>
    /// 숏노트를 추가한다. 범위 밖이거나, 같은 라인에서 그 tick에 노트가 있거나 롱노트 구간 안이면 false.
    /// </summary>
    public bool TryAdd(int lane, int tick)
    {
        return TryAdd(lane, tick, out _);
    }

    /// <summary>
    /// 숏노트를 추가한다. 실패하면 error에 이유가 들어간다.
    /// </summary>
    public bool TryAdd(int lane, int tick, out NoteEditError error)
    {
        if (!IsInRange(lane, tick)) { error = NoteEditError.OutOfRange; return false; }
        if (FindOverlapIndex(lane, tick, tick) >= 0) { error = NoteEditError.Overlap; return false; }

        int index = FindIndex(lane, tick, out _);
        notes.Insert(index, EditorNote.Short(lane, tick));
        ShortCount++;
        error = NoteEditError.None;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 롱노트를 추가한다. startTick·endTick은 어느 쪽이 커도 되고, 작은 쪽이 시작이 된다.
    /// - 범위(0 ~ 곡 끝) 밖이면 OutOfRange
    /// - 길이(끝 − 시작)가 MinLongNoteTicks(1/4박)보다 짧으면 TooShort
    /// - 같은 라인에서 [시작, 끝] 구간이 다른 노트(숏노트 위치, 다른 롱노트 구간)와 겹치면 Overlap (끝 tick이 같아도 겹침)
    /// </summary>
    public bool TryAddLong(int lane, int startTick, int endTick, out NoteEditError error)
    {
        int start = Math.Min(startTick, endTick);
        int end = Math.Max(startTick, endTick);

        if (!IsInRange(lane, start) || end > TotalTicks) { error = NoteEditError.OutOfRange; return false; }
        if (end - start < GameConstants.Editor.MinLongNoteTicks) { error = NoteEditError.TooShort; return false; }
        if (FindOverlapIndex(lane, start, end) >= 0) { error = NoteEditError.Overlap; return false; }

        int index = FindIndex(lane, start, out _);
        notes.Insert(index, new EditorNote(lane, start, end));
        LongCount++;
        maxLongLength = Math.Max(maxLongLength, end - start);
        error = NoteEditError.None;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 같은 라인에서 tick을 덮는 노트를 찾는다 (숏노트는 그 tick, 롱노트는 시작·몸통·끝 어느 tick이든).
    /// </summary>
    /// <returns>찾았으면 true</returns>
    public bool TryFindAt(int lane, int tick, out EditorNote note)
    {
        int index = FindOverlapIndex(lane, tick, tick);
        note = index >= 0 ? notes[index] : default;
        return index >= 0;
    }

    /// <summary>
    /// 같은 라인에서 tick을 덮는 노트를 지운다. 롱노트는 시작·몸통·끝 어느 tick을 가리켜도 지운다. 없으면 false.
    /// </summary>
    public bool TryRemove(int lane, int tick)
    {
        int index = FindOverlapIndex(lane, tick, tick);
        if (index < 0) return false;

        if (notes[index].IsLong) LongCount--;
        else ShortCount--;

        notes.RemoveAt(index);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 노트 전체를 한 번에 바꾼다 (채보 불러오기용). (tick, lane) 순으로 정렬하고 OnChanged는 한 번만 보낸다.
    /// 범위 밖이거나, 1/4박보다 짧은 롱노트이거나, 같은 라인에서 앞 노트와 구간이 겹치는 노트는 넣지 않는다.
    /// </summary>
    /// <returns>넣지 못한 노트 수</returns>
    public int ReplaceAll(IEnumerable<EditorNote> newNotes)
    {
        notes.Clear();
        ShortCount = 0;
        LongCount = 0;
        maxLongLength = 0;

        int skipped = 0;
        if (newNotes != null)
        {
            foreach (EditorNote note in newNotes)
            {
                if (!IsInRange(note.Lane, note.Tick) || note.EndTick < note.Tick || note.EndTick > TotalTicks
                    || (note.IsLong && note.EndTick - note.Tick < GameConstants.Editor.MinLongNoteTicks))
                {
                    skipped++;
                    continue;
                }
                notes.Add(note);
            }
        }

        notes.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Lane.CompareTo(b.Lane));

        // 정렬 순서대로 보며, 같은 라인에서 앞에 남긴 노트의 끝 tick 이하에서 시작하면 겹친 노트라 뺀다
        int[] laneEnd = new int[LaneCount];
        for (int lane = 0; lane < LaneCount; lane++) laneEnd[lane] = -1;

        int write = 0;
        for (int read = 0; read < notes.Count; read++)
        {
            EditorNote note = notes[read];
            if (note.Tick <= laneEnd[note.Lane])
            {
                skipped++;
                continue;
            }

            laneEnd[note.Lane] = note.EndTick;
            notes[write++] = note;
            if (note.IsLong)
            {
                LongCount++;
                maxLongLength = Math.Max(maxLongLength, note.EndTick - note.Tick);
            }
            else
            {
                ShortCount++;
            }
        }
        notes.RemoveRange(write, notes.Count - write);

        OnChanged?.Invoke();
        return skipped;
    }

    /// <summary>
    /// 곡 끝 tick을 바꾼다 (곡 BPM·offset을 바꿨을 때). 노트는 지우지 않는다.
    /// </summary>
    /// <returns>새 곡 끝을 넘는 노트 수 (경고용)</returns>
    public int SetTotalTicks(int totalTicks)
    {
        TotalTicks = Math.Max(0, totalTicks);

        int outside = 0;
        foreach (EditorNote note in notes)
        {
            if (note.Tick > TotalTicks || note.EndTick > TotalTicks) outside++;
        }
        return outside;
    }

    /// <summary>같은 라인·같은 tick에서 시작하는 노트가 있으면 true (롱노트 몸통은 보지 않는다. 덮는 노트는 TryFindAt)</summary>
    public bool Contains(int lane, int tick)
    {
        FindIndex(lane, tick, out bool found);
        return found;
    }

    /// <summary>
    /// 시작 tick ~ 끝 tick(둘 다 포함) 구간에 걸치는 노트를 시작 tick 순으로 results에 채운다.
    /// 구간 앞에서 시작해 구간 안으로 들어오는 롱노트도 포함한다 (노트 끝 tick ≥ startTick).
    /// 스크롤할 때마다 새 리스트를 만들지 않도록 결과 리스트를 받아 채운다.
    /// </summary>
    public void GetRange(int startTick, int endTick, List<EditorNote> results)
    {
        results.Clear();
        if (endTick < startTick) return;

        for (int i = LowerBound(startTick - maxLongLength); i < notes.Count && notes[i].Tick <= endTick; i++)
        {
            if (notes[i].EndTick >= startTick) results.Add(notes[i]);
        }
    }

    /// <summary>
    /// 채보 JSON에 넣을 노트 목록으로 바꾼다 (beat = tick ÷ TicksPerBeat).
    /// </summary>
    public List<NoteData> ToNoteDataList()
    {
        List<NoteData> list = new List<NoteData>(notes.Count);
        foreach (EditorNote note in notes)
        {
            list.Add(new NoteData
            {
                beat = BeatGridMath.TickToBeat(note.Tick),
                lane = note.Lane,
                type = note.IsLong ? NoteData.TypeLong : NoteData.TypeShort,
                endBeat = note.IsLong ? BeatGridMath.TickToBeat(note.EndTick) : 0f
            });
        }
        return list;
    }

    private bool IsInRange(int lane, int tick)
    {
        return lane >= 0 && lane < LaneCount && tick >= 0 && tick <= TotalTicks;
    }

    /// <summary>
    /// 같은 라인에서 [startTick, endTick] 구간과 겹치는 노트의 인덱스. 없으면 −1.
    /// 노트 구간은 [Tick, EndTick] (숏노트는 한 점)이고, 양 끝이 같은 tick이어도 겹침으로 본다.
    /// </summary>
    private int FindOverlapIndex(int lane, int startTick, int endTick)
    {
        for (int i = LowerBound(startTick - maxLongLength); i < notes.Count && notes[i].Tick <= endTick; i++)
        {
            EditorNote note = notes[i];
            if (note.Lane == lane && note.EndTick >= startTick) return i;
        }
        return -1;
    }

    /// <summary>
    /// (tick, lane) 순서에서 노트가 있어야 할 자리. 같은 노트가 있으면 found = true.
    /// </summary>
    private int FindIndex(int lane, int tick, out bool found)
    {
        int low = 0;
        int high = notes.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            EditorNote note = notes[mid];
            if (note.Tick < tick || (note.Tick == tick && note.Lane < lane)) low = mid + 1;
            else high = mid;
        }

        found = low < notes.Count && notes[low].Tick == tick && notes[low].Lane == lane;
        return low;
    }

    /// <summary>tick 이상인 첫 노트의 자리</summary>
    private int LowerBound(int tick)
    {
        int low = 0;
        int high = notes.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (notes[mid].Tick < tick) low = mid + 1;
            else high = mid;
        }
        return low;
    }
}
