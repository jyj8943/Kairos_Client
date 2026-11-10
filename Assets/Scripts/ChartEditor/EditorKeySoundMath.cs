using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에디터 키음 예약 계산. 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 노트 목록(ChartEditData.Notes, (tick, lane) 순)을 키음을 낼 순간의 목록(KeySoundEvent)으로 바꿔 tick 순으로 예약한다.
/// - 같은 tick에 여러 라인 노트가 있으면 노트마다 따로 센다 (인게임에서 동시에 누를 때와 같게 노트마다 한 번씩 소리를 낸다).
/// - 롱노트는 시작 tick과 끝 tick에서 각각 한 번씩 센다 (롱노트 판정 세부 규칙 6: 시작 판정·끝 판정 때 각각 키음).
/// </summary>
public static class EditorKeySoundMath
{
    /// <summary>키음을 낼 순간 하나 (노트 시작, 또는 롱노트 끝)</summary>
    public readonly struct KeySoundEvent
    {
        public readonly int Tick;
        public readonly int Lane;

        /// <summary>롱노트 끝이면 true, 노트 시작이면 false</summary>
        public readonly bool IsEnd;

        public KeySoundEvent(int tick, int lane, bool isEnd)
        {
            Tick = tick;
            Lane = lane;
            IsEnd = isEnd;
        }
    }

    /// <summary>(tick, lane, 시작 먼저) 순 비교. 정렬할 때마다 대리자를 새로 만들지 않도록 한 번만 만든다</summary>
    private static readonly System.Comparison<KeySoundEvent> EventOrder = (a, b) =>
    {
        if (a.Tick != b.Tick) return a.Tick.CompareTo(b.Tick);
        if (a.Lane != b.Lane) return a.Lane.CompareTo(b.Lane);
        return a.IsEnd.CompareTo(b.IsEnd);
    };

    /// <summary>
    /// 노트 목록을 키음 순간 목록으로 바꿔 result에 (tick, lane) 순으로 채운다.
    /// 숏노트는 tick 하나, 롱노트는 시작 tick과 끝 tick 두 개가 들어간다. 재생을 시작할 때와 노트가 바뀔 때만 부른다.
    /// </summary>
    public static void BuildEvents(IReadOnlyList<EditorNote> notes, List<KeySoundEvent> result)
    {
        result.Clear();
        for (int i = 0; i < notes.Count; i++)
        {
            EditorNote note = notes[i];
            result.Add(new KeySoundEvent(note.Tick, note.Lane, false));
            if (note.IsLong) result.Add(new KeySoundEvent(note.EndTick, note.Lane, true));
        }
        result.Sort(EventOrder);
    }

    /// <summary>
    /// tick 이상인 첫 키음 순간의 인덱스. 없으면 events.Count.
    /// </summary>
    public static int FindFirstAtOrAfter(IReadOnlyList<KeySoundEvent> events, int tick)
    {
        int low = 0;
        int high = events.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (events[mid].Tick < tick) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    /// <summary>
    /// 박 위치(소수)를 "이 위치 이후 첫 노트"를 찾을 tick으로 바꾼다. 박 위에 정확히 있는 노트는 포함한다 (올림).
    /// </summary>
    public static int BeatToFirstTick(float beat)
    {
        return Mathf.CeilToInt(beat * GameConstants.Editor.TicksPerBeat - 0.001f);
    }

    /// <summary>
    /// startIndex부터 차례로 보며 tick이 endTick 이하인 키음 순간의 인덱스를 result에 넣는다 (예약 범위 안 목록).
    /// </summary>
    /// <returns>다음에 볼 인덱스 (범위 밖 첫 순간, 없으면 events.Count)</returns>
    public static int CollectUntil(IReadOnlyList<KeySoundEvent> events, int startIndex, int endTick, List<int> result)
    {
        result.Clear();
        int index = Mathf.Max(0, startIndex);
        while (index < events.Count && events[index].Tick <= endTick)
        {
            result.Add(index);
            index++;
        }
        return index;
    }

    /// <summary>
    /// tick이 fromTick 이상 toTick 이하인 키음 순간 수 (재생 구간에 울려야 하는 키음 수, 롱노트 끝 포함).
    /// </summary>
    public static int CountInRange(IReadOnlyList<KeySoundEvent> events, int fromTick, int toTick)
    {
        if (toTick < fromTick) return 0;
        return FindFirstAtOrAfter(events, toTick + 1) - FindFirstAtOrAfter(events, fromTick);
    }
}
