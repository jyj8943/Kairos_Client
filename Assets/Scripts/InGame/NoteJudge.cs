using System;
using System.Collections.Generic;

/// <summary>
/// 인게임 노트 하나. 시간은 곡 시간(초)이다.
/// - 숏노트: EndTime = Time, 틱 없음.
/// - 롱노트: EndTime > Time, 유지 틱 시간 목록(TickTimes, 시간 순)을 들고 있다 (LongNoteTicks로 만든다).
/// </summary>
public readonly struct PlayNote
{
    private static readonly double[] NoTicks = new double[0];

    public readonly int Lane;
    public readonly double Time;

    /// <summary>롱노트 끝 시간 (숏노트는 Time과 같다)</summary>
    public readonly double EndTime;

    private readonly double[] tickTimes;

    /// <summary>숏노트</summary>
    public PlayNote(int lane, double time)
    {
        Lane = lane;
        Time = time;
        EndTime = time;
        tickTimes = NoTicks;
    }

    /// <summary>롱노트</summary>
    /// <param name="tickTimes">유지 틱 시간 (시간 순). null이면 틱 없음</param>
    public PlayNote(int lane, double time, double endTime, double[] tickTimes)
    {
        Lane = lane;
        Time = time;
        EndTime = endTime;
        this.tickTimes = tickTimes ?? NoTicks;
    }

    public bool IsLong => EndTime > Time;

    /// <summary>유지 틱 수</summary>
    public int TickCount => tickTimes != null ? tickTimes.Length : 0;

    /// <summary>i번째 유지 틱 시간 (초)</summary>
    public double GetTickTime(int index) => tickTimes[index];
}

/// <summary>판정 결과의 종류</summary>
public enum JudgeKind
{
    /// <summary>숏노트 판정 (점수·판정 개수·콤보)</summary>
    Short,

    /// <summary>롱노트 시작 판정 (PERFECT·GREAT·GOOD). 콤보만 +1, 점수·판정 개수에는 넣지 않는다</summary>
    LongStart,

    /// <summary>롱노트 유지 틱 (콤보만 +1)</summary>
    Tick,

    /// <summary>롱노트 종합 판정 (점수·판정 개수, MISS면 콤보 0). 시작 MISS도 이것으로 바로 낸다</summary>
    LongFinal
}

/// <summary>롱노트의 지금 상태 (표시용)</summary>
public enum LongNoteState
{
    /// <summary>아직 시작 판정 전</summary>
    Waiting,

    /// <summary>시작 판정 뒤 누르고 있는 중</summary>
    Held,

    /// <summary>시작 MISS (종합 MISS). 몸통을 회색으로 끝까지 내려보낸다</summary>
    Missed,

    /// <summary>끝 판정까지 끝남</summary>
    Done
}

/// <summary>
/// 판정 결과 하나.
/// </summary>
public readonly struct JudgeResult
{
    /// <summary>판정한 노트 번호 (NoteJudge.Notes 기준)</summary>
    public readonly int NoteIndex;
    public readonly int Lane;

    /// <summary>판정. 롱노트 종합 판정이면 시작·끝 중 낮은 쪽, 틱이면 Perfect(점수·집계에 쓰지 않음. 화면 등급은 StartType)</summary>
    public readonly JudgeType Type;

    /// <summary>입력 − 노트 (ms, 늦으면 +). 롱노트 종합 판정은 뗀 시각 − 끝 시각, 놓친 MISS는 판정한 시각 기준</summary>
    public readonly double DeltaMs;

    /// <summary>키를 눌러(떼어) 낸 판정이면 true, 시간이 지나 낸 판정(놓친 MISS, 늦게 떼 자동 GOOD, 틱)이면 false</summary>
    public readonly bool IsHit;

    public readonly JudgeKind Kind;

    /// <summary>롱노트의 시작 판정: 종합 판정·유지 틱이면 그 롱노트의 시작 판정 (틱은 판정 글자를 이 등급으로 띄운다), 그 밖에는 Type과 같다</summary>
    public readonly JudgeType StartType;

    /// <summary>
    /// 롱노트 종합 판정의 끝 판정. 끝 ±LongReleaseMs 안에서 떼면 시작 판정과 같고, 일찍 떼면 Miss, 끝 + LongReleaseMs를 지나도록 누르면 Good.
    /// 시작 MISS면 Miss. 롱노트 종합 판정이 아니면 Type과 같다
    /// </summary>
    public readonly JudgeType EndType;

    public JudgeResult(int noteIndex, int lane, JudgeType type, double deltaMs, bool isHit)
        : this(noteIndex, lane, type, deltaMs, isHit, JudgeKind.Short, type, type)
    {
    }

    public JudgeResult(int noteIndex, int lane, JudgeType type, double deltaMs, bool isHit, JudgeKind kind, JudgeType startType, JudgeType endType)
    {
        NoteIndex = noteIndex;
        Lane = lane;
        Type = type;
        DeltaMs = deltaMs;
        IsHit = isHit;
        Kind = kind;
        StartType = startType;
        EndType = endType;
    }

    /// <summary>
    /// 판정 개수·점수에 들어가는 판정이면 true (숏노트, 롱노트 종합). 판정 패널을 이때만 바꾼다.
    /// 롱노트 시작·유지 틱은 콤보만 올린다.
    /// </summary>
    public bool CountsAsJudgement => Kind == JudgeKind.Short || Kind == JudgeKind.LongFinal;

    /// <summary>
    /// 판정 글자로 띄울 등급 (DJMAX RESPECT V·PLATiNA :: LAB 방식). 모든 결과가 판정 글자를 처음부터 다시 재생한다.
    /// - 숏노트·롱노트 시작: 그 판정 / 롱노트 종합: 최종 판정 (일찍 떼면 MISS)
    /// - 유지 틱: 그 롱노트의 시작 판정 (GREAT로 시작하면 누르는 동안 계속 GREAT)
    /// </summary>
    public JudgeType JudgeTextType => Kind == JudgeKind.Tick ? StartType : Type;
}

/// <summary>
/// 숏노트·롱노트 판정 (기획서 2-1, 롱노트 판정 세부 규칙. 끝 판정만 DJMAX RESPECT 방식으로 기획서와 일부러 다르게 간다). MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 노트는 시간 순(같으면 라인 순)으로 정렬해 들고, 라인마다 아직 판정되지 않은 가장 이른 노트 하나만 판정한다. 여러 라인은 따로 판정한다.
/// - 롱노트
///   - 시작 판정은 숏노트와 같다. PERFECT·GREAT·GOOD이면 그 라인은 "누르고 있는 롱노트" 상태가 되고, 그동안 그 라인의 다음 노트는 판정하지 않는다.
///   - 시작 MISS(MISS 범위에서 누름, 또는 +GOOD 범위를 지나도록 안 누름)면 바로 종합 MISS. 몸통이 끝까지 내려가는 동안(끝 시간 전) 그 라인 누름은 빈 타격이다.
///   - 끝 판정 (뗀 시각 − 끝 시각, GameConstants.Play.LongReleaseMs = ±120ms, 경계 포함):
///     범위 안에서 떼면 끝 판정 = 시작 판정 (끝이 판정을 깎지도 올리지도 않는다).
///     범위보다 일찍 떼면 끝 MISS (종합 MISS, 콤보 0, 남은 틱 인정 안 함). 끝 + 범위를 지나도록 누르고 있으면 그 순간 끝 GOOD (모든 틱 인정).
///   - 종합 판정 = 시작과 끝 중 낮은 쪽 (범위 안이면 결국 시작 판정). 한 번 떼면 끝이다 (다시 누르기 불가).
///   - 유지 틱: 누르고 있는 동안 지난 틱마다 Tick 결과 (StartType = 시작 판정). 늦게 눌러 이미 지난 틱도 인정하고(다음 CollectTimeouts·TryRelease에서),
///     일찍 떼서 남은 틱과 종합 MISS 뒤 남은 틱은 인정하지 않는다.
/// - 판정 중에는 메모리를 새로 잡지 않는다 (매 프레임 GC 할당 없음).
/// </summary>
public class NoteJudge
{
    private readonly PlayNote[] notes;
    private readonly int[][] laneNotes;
    private readonly int[] laneCursor;
    private readonly bool[] judged;
    private readonly LongNoteState[] longStates;
    private readonly JudgeWindows windows;

    // 라인별 누르고 있는 롱노트 (없으면 -1)와 그 시작 판정, 다음에 인정할 틱 번호
    private readonly int[] heldNote;
    private readonly JudgeType[] heldStartType;
    private readonly int[] heldTickCursor;

    /// <summary>라인별: 시작 MISS 롱노트의 끝 시간. 이 시각 전의 누름은 빈 타격이다</summary>
    private readonly double[] laneBlockedUntil;

    /// <summary>판정할 노트 (시간 순)</summary>
    public IReadOnlyList<PlayNote> Notes => notes;

    /// <summary>판정 개수 (숏노트 수 + 롱노트 수)</summary>
    public int TotalCount => notes.Length;

    /// <summary>전체 유지 틱 수</summary>
    public int TotalTicks { get; }

    /// <summary>판정(숏노트, 롱노트 종합)이 끝난 노트 수</summary>
    public int JudgedCount { get; private set; }
    public bool AllJudged => JudgedCount >= notes.Length;

    /// <param name="sourceNotes">판정할 노트. 순서는 상관없다 (안에서 정렬한다)</param>
    public NoteJudge(IEnumerable<PlayNote> sourceNotes, int laneCount, JudgeWindows windows)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));

        List<PlayNote> sorted = new List<PlayNote>(sourceNotes);
        sorted.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Lane.CompareTo(b.Lane));
        notes = sorted.ToArray();
        judged = new bool[notes.Length];
        longStates = new LongNoteState[notes.Length];
        TotalTicks = LongNoteTicks.TotalTicks(notes);

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

        heldNote = new int[laneCount];
        for (int lane = 0; lane < laneCount; lane++) heldNote[lane] = -1;
        heldStartType = new JudgeType[laneCount];
        heldTickCursor = new int[laneCount];
        laneBlockedUntil = new double[laneCount];
        for (int lane = 0; lane < laneCount; lane++) laneBlockedUntil[lane] = double.NegativeInfinity;
    }

    /// <summary>판정(숏노트, 롱노트 종합)이 끝났으면 true</summary>
    public bool IsJudged(int noteIndex)
    {
        return judged[noteIndex];
    }

    /// <summary>롱노트의 지금 상태 (숏노트는 판정 전 Waiting, 뒤 Done)</summary>
    public LongNoteState GetLongState(int noteIndex)
    {
        return longStates[noteIndex];
    }

    /// <summary>라인에서 누르고 있는 롱노트. 없으면 false</summary>
    public bool TryGetHeld(int lane, out int noteIndex)
    {
        noteIndex = lane >= 0 && lane < heldNote.Length ? heldNote[lane] : -1;
        return noteIndex >= 0;
    }

    /// <summary>
    /// 라인의 다음 판정 대상 노트 (시작 판정 전인 노트). 없으면 false.
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
    /// 키 누름을 판정한다. 라인의 다음 노트와 차이가 MISS 범위 밖이면 빈 타격으로 false.
    /// 롱노트를 누르고 있는 라인, 시작 MISS 롱노트의 몸통이 내려오는 중인 라인도 빈 타격이다.
    /// 롱노트 시작이 PERFECT·GREAT·GOOD이면 LongStart, MISS면 바로 LongFinal(MISS)을 돌려준다.
    /// </summary>
    /// <param name="inputTime">입력 시각 (곡 시간, 초)</param>
    public bool TryHit(int lane, double inputTime, out JudgeResult result)
    {
        result = default;
        if (lane < 0 || lane >= laneNotes.Length) return false;
        if (heldNote[lane] >= 0 || inputTime < laneBlockedUntil[lane]) return false;
        if (!TryPeekNext(lane, out int noteIndex)) return false;

        PlayNote note = notes[noteIndex];
        double deltaMs = (inputTime - note.Time) * 1000.0;
        if (!windows.TryJudge(deltaMs, out JudgeType type)) return false;

        laneCursor[lane]++;
        if (!note.IsLong)
        {
            MarkJudged(noteIndex, LongNoteState.Done);
            result = new JudgeResult(noteIndex, lane, type, deltaMs, true);
            return true;
        }

        if (type == JudgeType.Miss)
        {
            result = MissLongAtStart(lane, noteIndex, deltaMs, true);
            return true;
        }

        heldNote[lane] = noteIndex;
        heldStartType[lane] = type;
        heldTickCursor[lane] = 0;
        longStates[noteIndex] = LongNoteState.Held;
        result = new JudgeResult(noteIndex, lane, type, deltaMs, true, JudgeKind.LongStart, type, type);
        return true;
    }

    /// <summary>
    /// 키를 뗐다. 그 라인에서 누르고 있는 롱노트가 있으면 뗀 시각까지 지난 틱(Tick)과 종합 판정(LongFinal)을 results 뒤에 붙인다.
    /// results는 비우지 않는다 (CollectTimeouts 결과와 같이 쓸 수 있게).
    /// </summary>
    /// <param name="releaseTime">뗀 시각 (곡 시간, 초)</param>
    /// <returns>누르고 있던 롱노트가 있어 끝 판정을 냈으면 true</returns>
    public bool TryRelease(int lane, double releaseTime, List<JudgeResult> results)
    {
        if (lane < 0 || lane >= heldNote.Length || heldNote[lane] < 0) return false;

        int noteIndex = heldNote[lane];
        CollectTicks(lane, releaseTime, results);

        double deltaMs = (releaseTime - notes[noteIndex].EndTime) * 1000.0;
        JudgeType endType;
        if (deltaMs < -LongReleaseMs - BoundaryEpsilonMs) endType = JudgeType.Miss;     // 범위보다 일찍 뗌
        else if (deltaMs - BoundaryEpsilonMs > LongReleaseMs) endType = JudgeType.Good; // 범위를 지나 뗀 것이 자동 GOOD 처리 전에 들어옴
        else endType = heldStartType[lane];                                             // 범위 안: 시작 판정 그대로

        results.Add(FinishLong(lane, noteIndex, endType, deltaMs, true));
        return true;
    }

    /// <summary>
    /// 시간이 지나 나는 판정을 results에 담는다 (results는 먼저 비운다). 매 프레임 부른다.
    /// - 누르고 있는 롱노트: 지난 틱마다 Tick, 끝 + LongReleaseMs가 지나면 끝 GOOD 종합 판정
    /// - +GOOD 범위를 지나도록 안 누른 숏노트는 MISS, 롱노트는 시작 MISS로 종합 MISS
    /// </summary>
    /// <returns>이번에 담은 결과 수</returns>
    public int CollectTimeouts(double songTime, List<JudgeResult> results)
    {
        results.Clear();

        for (int lane = 0; lane < laneNotes.Length; lane++)
        {
            int held = heldNote[lane];
            if (held >= 0)
            {
                CollectTicks(lane, songTime, results);
                double elapsedMs = (songTime - notes[held].EndTime) * 1000.0;
                if (elapsedMs - BoundaryEpsilonMs > LongReleaseMs) results.Add(FinishLong(lane, held, JudgeType.Good, elapsedMs, false));
            }

            while (TryPeekNext(lane, out int noteIndex) && windows.IsLateMiss((songTime - notes[noteIndex].Time) * 1000.0))
            {
                double deltaMs = (songTime - notes[noteIndex].Time) * 1000.0;
                laneCursor[lane]++;
                if (notes[noteIndex].IsLong)
                {
                    results.Add(MissLongAtStart(lane, noteIndex, deltaMs, false));
                }
                else
                {
                    MarkJudged(noteIndex, LongNoteState.Done);
                    results.Add(new JudgeResult(noteIndex, lane, JudgeType.Miss, deltaMs, false));
                }
            }
        }
        return results.Count;
    }

    /// <summary>
    /// 일시정지에서 재개하는 순간(카운트다운이 끝나 음악을 다시 시작할 때) 누르고 있던 롱노트를 확인한다 (세부 규칙 5).
    /// 그 라인 키가 눌려 있지 않으면 그 순간(songTime)에 뗀 것으로 보고 TryRelease한다. 결과는 results 뒤에 붙인다.
    /// </summary>
    /// <param name="isPressed">라인 키가 지금 눌려 있는지 (InGameInput.IsLanePressed)</param>
    /// <returns>뗀 것으로 처리한 롱노트 수</returns>
    public int ResolveHeldOnResume(double songTime, Func<int, bool> isPressed, List<JudgeResult> results)
    {
        int released = 0;
        for (int lane = 0; lane < heldNote.Length; lane++)
        {
            if (heldNote[lane] < 0 || isPressed(lane)) continue;
            if (TryRelease(lane, songTime, results)) released++;
        }
        return released;
    }

    /// <summary>경계값 비교 허용 오차 (JudgeWindows와 같은 값)</summary>
    private const double BoundaryEpsilonMs = 0.0001;

    /// <summary>롱노트를 뗄 때 시작 판정을 그대로 인정하는 범위 ±ms (GameConstants.Play.LongReleaseMs)</summary>
    private const double LongReleaseMs = GameConstants.Play.LongReleaseMs;

    /// <summary>누르고 있는 롱노트에서 time까지 지난 틱을 Tick 결과로 붙인다.</summary>
    private void CollectTicks(int lane, double time, List<JudgeResult> results)
    {
        int noteIndex = heldNote[lane];
        PlayNote note = notes[noteIndex];
        while (heldTickCursor[lane] < note.TickCount && note.GetTickTime(heldTickCursor[lane]) <= time)
        {
            double deltaMs = (time - note.GetTickTime(heldTickCursor[lane])) * 1000.0;
            heldTickCursor[lane]++;
            // 틱 Type은 Perfect 그대로 (점수·집계에 쓰지 않음), StartType에 시작 판정을 담아 판정 글자 등급으로 쓴다
            results.Add(new JudgeResult(noteIndex, lane, JudgeType.Perfect, deltaMs, false, JudgeKind.Tick, heldStartType[lane], JudgeType.Perfect));
        }
    }

    /// <summary>누르고 있던 롱노트를 끝내고 종합 판정(시작·끝 중 낮은 쪽)을 만든다.</summary>
    private JudgeResult FinishLong(int lane, int noteIndex, JudgeType endType, double deltaMs, bool isHit)
    {
        JudgeType startType = heldStartType[lane];
        JudgeType finalType = (JudgeType)Math.Max((int)startType, (int)endType);

        heldNote[lane] = -1;
        MarkJudged(noteIndex, LongNoteState.Done);
        return new JudgeResult(noteIndex, lane, finalType, deltaMs, isHit, JudgeKind.LongFinal, startType, endType);
    }

    /// <summary>시작 MISS: 바로 종합 MISS로 끝내고, 몸통이 내려가는 동안(끝 시간 전) 그 라인 누름을 빈 타격으로 막는다.</summary>
    private JudgeResult MissLongAtStart(int lane, int noteIndex, double deltaMs, bool isHit)
    {
        laneBlockedUntil[lane] = Math.Max(laneBlockedUntil[lane], notes[noteIndex].EndTime);
        MarkJudged(noteIndex, LongNoteState.Missed);
        return new JudgeResult(noteIndex, lane, JudgeType.Miss, deltaMs, isHit, JudgeKind.LongFinal, JudgeType.Miss, JudgeType.Miss);
    }

    private void MarkJudged(int noteIndex, LongNoteState state)
    {
        judged[noteIndex] = true;
        longStates[noteIndex] = state;
        JudgedCount++;
    }
}
