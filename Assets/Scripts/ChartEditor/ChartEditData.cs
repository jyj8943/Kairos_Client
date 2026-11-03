using System;
using System.Collections.Generic;

/// <summary>
/// 편집 중인 채보의 노트를 관리한다. 노트 데이터는 이 클래스 한곳에서만 바꾼다.
/// - 노트 목록은 (tick, lane) 순으로 정렬해 두고, 찾기·구간 검색은 이진 탐색으로 한다.
/// - 노트가 바뀌면 OnChanged를 보낸다. 표시(EditorNoteLayer)와 노트 수 표시는 이 이벤트를 구독한다.
/// </summary>
public class ChartEditData
{
    private readonly List<EditorNote> notes = new List<EditorNote>();

    /// <summary>노트가 추가·삭제되면 호출된다</summary>
    public event Action OnChanged;

    /// <summary>곡 끝 tick. 노트는 0 ~ TotalTicks(포함)에만 둘 수 있다</summary>
    public int TotalTicks { get; }

    /// <summary>라인 수</summary>
    public int LaneCount { get; }

    public int ShortCount { get; private set; }
    public int LongCount { get; private set; }

    public ChartEditData(int totalTicks, int laneCount)
    {
        TotalTicks = Math.Max(0, totalTicks);
        LaneCount = laneCount;
    }

    /// <summary>
    /// 숏노트를 추가한다. 범위 밖이거나 같은 라인·같은 tick에 이미 노트가 있으면 false.
    /// </summary>
    public bool TryAdd(int lane, int tick)
    {
        if (!IsInRange(lane, tick)) return false;

        int index = FindIndex(lane, tick, out bool found);
        if (found) return false;

        notes.Insert(index, EditorNote.Short(lane, tick));
        ShortCount++;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 같은 라인·같은 tick의 노트를 지운다. 없으면 false.
    /// </summary>
    public bool TryRemove(int lane, int tick)
    {
        int index = FindIndex(lane, tick, out bool found);
        if (!found) return false;

        if (notes[index].IsLong) LongCount--;
        else ShortCount--;

        notes.RemoveAt(index);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>같은 라인·같은 tick에 노트가 있으면 true</summary>
    public bool Contains(int lane, int tick)
    {
        FindIndex(lane, tick, out bool found);
        return found;
    }

    /// <summary>
    /// 시작 tick ~ 끝 tick(둘 다 포함)에 있는 노트를 tick 순으로 results에 채운다.
    /// 스크롤할 때마다 새 리스트를 만들지 않도록 결과 리스트를 받아 채운다.
    /// </summary>
    public void GetRange(int startTick, int endTick, List<EditorNote> results)
    {
        results.Clear();
        if (endTick < startTick) return;

        for (int i = LowerBound(startTick); i < notes.Count && notes[i].Tick <= endTick; i++)
            results.Add(notes[i]);
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
