using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에디터 키음 예약 계산. 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 노트 목록은 ChartEditData.Notes처럼 (tick, lane) 순으로 정렬돼 있다고 본다.
/// - 같은 tick에 여러 라인 노트가 있으면 노트마다 따로 센다 (인게임에서 동시에 누를 때와 같게 노트마다 한 번씩 소리를 낸다).
/// - 롱노트는 시작 tick에서 한 번만 센다.
/// </summary>
public static class EditorKeySoundMath
{
    /// <summary>
    /// tick 이상인 첫 노트의 인덱스. 없으면 notes.Count.
    /// </summary>
    public static int FindFirstNoteAtOrAfter(IReadOnlyList<EditorNote> notes, int tick)
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

    /// <summary>
    /// 박 위치(소수)를 "이 위치 이후 첫 노트"를 찾을 tick으로 바꾼다. 박 위에 정확히 있는 노트는 포함한다 (올림).
    /// </summary>
    public static int BeatToFirstTick(float beat)
    {
        return Mathf.CeilToInt(beat * GameConstants.Editor.TicksPerBeat - 0.001f);
    }

    /// <summary>
    /// startIndex부터 차례로 보며 tick이 endTick 이하인 노트의 인덱스를 result에 넣는다 (예약 범위 안 노트 목록).
    /// </summary>
    /// <returns>다음에 볼 노트 인덱스 (범위 밖 첫 노트, 없으면 notes.Count)</returns>
    public static int CollectNotesUntil(IReadOnlyList<EditorNote> notes, int startIndex, int endTick, List<int> result)
    {
        result.Clear();
        int index = Mathf.Max(0, startIndex);
        while (index < notes.Count && notes[index].Tick <= endTick)
        {
            result.Add(index);
            index++;
        }
        return index;
    }

    /// <summary>
    /// tick이 fromTick 이상 toTick 이하인 노트 수 (재생 구간의 노트 수, 같은 tick 여러 라인 포함).
    /// </summary>
    public static int CountNotesInRange(IReadOnlyList<EditorNote> notes, int fromTick, int toTick)
    {
        if (toTick < fromTick) return 0;
        return FindFirstNoteAtOrAfter(notes, toTick + 1) - FindFirstNoteAtOrAfter(notes, fromTick);
    }
}
