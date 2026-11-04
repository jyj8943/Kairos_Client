using System;
using System.Collections.Generic;

/// <summary>
/// 인게임 노트 하나 (숏노트). 시간은 음원 시작 기준 초다.
/// </summary>
public readonly struct PlayNote
{
    public readonly int Lane;
    public readonly double Time;

    public PlayNote(int lane, double time)
    {
        Lane = lane;
        Time = time;
    }
}

/// <summary>
/// 판정 결과 하나.
/// </summary>
public readonly struct JudgeResult
{
    /// <summary>판정한 노트 번호 (NoteJudge.Notes 기준)</summary>
    public readonly int NoteIndex;
    public readonly int Lane;
    public readonly JudgeType Type;

    /// <summary>입력 − 노트 (ms, 늦으면 +). 놓친 MISS는 판정한 시각 기준</summary>
    public readonly double DeltaMs;

    /// <summary>키를 눌러 낸 판정이면 true, 놓쳐서 낸 MISS면 false</summary>
    public readonly bool IsHit;

    public JudgeResult(int noteIndex, int lane, JudgeType type, double deltaMs, bool isHit)
    {
        NoteIndex = noteIndex;
        Lane = lane;
        Type = type;
        DeltaMs = deltaMs;
        IsHit = isHit;
    }
}

/// <summary>
/// 숏노트 판정. MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 노트는 시간 순(같으면 라인 순)으로 정렬해 들고, 라인마다 아직 판정되지 않은 가장 이른 노트 하나만 판정한다.
/// - 여러 라인은 따로 판정한다.
/// - 판정 중에는 메모리를 새로 잡지 않는다 (매 프레임 GC 할당 없음).
/// </summary>
public class NoteJudge
{
    private readonly PlayNote[] notes;
    private readonly int[][] laneNotes;
    private readonly int[] laneCursor;
    private readonly bool[] judged;
    private readonly JudgeWindows windows;

    /// <summary>판정할 노트 (시간 순)</summary>
    public IReadOnlyList<PlayNote> Notes => notes;

    public int TotalCount => notes.Length;
    public int JudgedCount { get; private set; }
    public bool AllJudged => JudgedCount >= notes.Length;

    /// <param name="sourceNotes">판정할 숏노트. 순서는 상관없다 (안에서 정렬한다)</param>
    public NoteJudge(IEnumerable<PlayNote> sourceNotes, int laneCount, JudgeWindows windows)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));

        List<PlayNote> sorted = new List<PlayNote>(sourceNotes);
        sorted.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Lane.CompareTo(b.Lane));
        notes = sorted.ToArray();
        judged = new bool[notes.Length];

        List<int>[] perLane = new List<int>[laneCount];
        for (int lane = 0; lane < laneCount; lane++) perLane[lane] = new List<int>();
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i].Lane < 0 || notes[i].Lane >= laneCount)
                throw new ArgumentOutOfRangeException(nameof(sourceNotes), $"노트 {i}의 lane({notes[i].Lane})이 범위 밖입니다.");
            perLane[notes[i].Lane].Add(i);
        }

        laneNotes = new int[laneCount][];
        for (int lane = 0; lane < laneCount; lane++) laneNotes[lane] = perLane[lane].ToArray();
        laneCursor = new int[laneCount];
    }

    /// <summary>노트가 판정됐으면 true</summary>
    public bool IsJudged(int noteIndex)
    {
        return judged[noteIndex];
    }

    /// <summary>
    /// 라인의 다음 판정 대상 노트. 없으면 false.
    /// </summary>
    public bool TryPeekNext(int lane, out int noteIndex)
    {
        int cursor = laneCursor[lane];
        if (cursor >= laneNotes[lane].Length)
        {
            noteIndex = -1;
            return false;
        }

        noteIndex = laneNotes[lane][cursor];
        return true;
    }

    /// <summary>
    /// 키 입력을 판정한다. 라인의 다음 노트와 차이가 MISS 범위 밖이면 빈 타격으로 false.
    /// </summary>
    /// <param name="inputTime">입력 시각 (곡 시간, 초)</param>
    public bool TryHit(int lane, double inputTime, out JudgeResult result)
    {
        result = default;
        if (lane < 0 || lane >= laneNotes.Length) return false;
        if (!TryPeekNext(lane, out int noteIndex)) return false;

        double deltaMs = (inputTime - notes[noteIndex].Time) * 1000.0;
        if (!windows.TryJudge(deltaMs, out JudgeType type)) return false;

        MarkJudged(lane, noteIndex);
        result = new JudgeResult(noteIndex, lane, type, deltaMs, true);
        return true;
    }

    /// <summary>
    /// +GOOD 범위를 지나도록 안 누른 노트를 MISS로 처리해 results에 담는다 (results는 먼저 비운다).
    /// </summary>
    /// <returns>이번에 MISS 처리한 수</returns>
    public int CollectMisses(double songTime, List<JudgeResult> results)
    {
        results.Clear();

        for (int lane = 0; lane < laneNotes.Length; lane++)
        {
            while (TryPeekNext(lane, out int noteIndex) && windows.IsLateMiss((songTime - notes[noteIndex].Time) * 1000.0))
            {
                MarkJudged(lane, noteIndex);
                results.Add(new JudgeResult(noteIndex, lane, JudgeType.Miss, (songTime - notes[noteIndex].Time) * 1000.0, false));
            }
        }
        return results.Count;
    }

    private void MarkJudged(int lane, int noteIndex)
    {
        judged[noteIndex] = true;
        laneCursor[lane]++;
        JudgedCount++;
    }
}
