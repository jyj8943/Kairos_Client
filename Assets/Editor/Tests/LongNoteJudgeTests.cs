using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 롱노트 판정·유지 틱·콤보·점수 EditMode 테스트 (기획서 2-1, 롱노트 판정 세부 규칙 11/10).
/// </summary>
public class LongNoteJudgeTests
{
    private static JudgeWindows DefaultWindows()
    {
        return new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs,
            GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs);
    }

    /// <summary>롱노트 하나 (박으로 지정, TimingMap으로 시간·틱 계산)</summary>
    private static PlayNote Long(TimingMap timing, int lane, double startBeat, double endBeat)
    {
        return new PlayNote(lane, timing.BeatToSeconds(startBeat), timing.BeatToSeconds(endBeat),
            LongNoteTicks.GetTickTimes(timing, startBeat, endBeat));
    }

    /// <summary>60 BPM, offset 0: 1박 = 1초, 틱 간격 0.25초</summary>
    private static readonly TimingMap Slow = TimingMap.Single(60f, 0);

    // ───── 틱 위치 ─────

    /// <summary>세부 규칙 1 표: 0→2박 7틱, 0→1/4박 0틱, 0→4/3박 4틱, 0→1/3박 0틱</summary>
    [TestCase(0.0, 2.0, 7)]
    [TestCase(0.0, 0.25, 0)]
    [TestCase(0.0, 4.0 / 3.0, 4)]
    [TestCase(0.0, 1.0 / 3.0, 0)]
    [TestCase(10.0 + 1.0 / 3.0, 11.0 + 2.0 / 3.0, 4)]
    [TestCase(3.0, 3.5, 1)]
    public void Ticks_Count(double start, double end, int expected)
    {
        Assert.AreEqual(expected, LongNoteTicks.Count(start, end));
        Assert.AreEqual(expected, LongNoteTicks.GetTickBeats(start, end).Length);
    }

    /// <summary>틱 위치는 시작 + 1/4박씩, 마지막은 끝 − 1/4박 이하 (0→4/3박: 0.25, 0.5, 0.75, 1.0)</summary>
    [Test]
    public void Ticks_BeatsAndTimes()
    {
        CollectionAssert.AreEqual(new[] { 0.25, 0.5, 0.75, 1.0 }, LongNoteTicks.GetTickBeats(0, 4.0 / 3.0));
        CollectionAssert.AreEqual(new[] { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75 }, LongNoteTicks.GetTickBeats(0, 2));

        // 시간은 TimingMap으로: offset 120ms, 0~16박 150 BPM, 16박부터 200 BPM → 16.25박 틱 = 6.52 + 0.075초
        TimingMap twoBpm = new TimingMap(new List<TimingPointData>
        {
            new TimingPointData { beat = 0f, bpm = 150f },
            new TimingPointData { beat = 16f, bpm = 200f }
        }, 120);
        double[] times = LongNoteTicks.GetTickTimes(twoBpm, 15.5, 16.75);
        Assert.AreEqual(4, times.Length, "15.75, 16.0, 16.25, 16.5박");
        Assert.AreEqual(0.12 + 15.75 * 0.4, times[0], 1e-9);
        Assert.AreEqual(6.52, times[1], 1e-9);
        Assert.AreEqual(6.52 + 0.075, times[2], 1e-9);
    }

    // ───── 종합 판정 ─────

    /// <summary>시작 판정별 누른 오프셋 (초): P 0ms · G +60ms · GOOD +100ms · MISS +140ms (MISS 범위에서 누름)</summary>
    private static double StartOffset(JudgeType start)
    {
        switch (start)
        {
            case JudgeType.Perfect: return 0;
            case JudgeType.Great: return 0.06;
            case JudgeType.Good: return 0.1;
            default: return 0.14;
        }
    }

    /// <summary>
    /// 종합 판정 표 (DJMAX RESPECT 방식): (시작 판정, 뗀 오차 ms) → 종합 판정.
    /// 끝 ±120ms 안에서 떼면 끝 판정 = 시작 판정이라 종합 = 시작 판정 그대로, −120ms보다 일찍 떼면 끝 MISS → 종합 MISS.
    /// 시작 MISS면 뗀 오차와 상관없이 MISS (누르고 있는 롱노트가 없다).
    /// </summary>
    [TestCase(JudgeType.Perfect, 0.0, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, -60.0, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, -100.0, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, -120.0, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, 100.0, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, -130.0, JudgeType.Miss)]
    [TestCase(JudgeType.Great, 0.0, JudgeType.Great)]
    [TestCase(JudgeType.Great, -60.0, JudgeType.Great)]
    [TestCase(JudgeType.Great, -100.0, JudgeType.Great)]
    [TestCase(JudgeType.Great, 100.0, JudgeType.Great)]
    [TestCase(JudgeType.Great, -130.0, JudgeType.Miss)]
    [TestCase(JudgeType.Good, 0.0, JudgeType.Good)]
    [TestCase(JudgeType.Good, -60.0, JudgeType.Good)]
    [TestCase(JudgeType.Good, -100.0, JudgeType.Good)]
    [TestCase(JudgeType.Good, 100.0, JudgeType.Good)]
    [TestCase(JudgeType.Good, -130.0, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, 0.0, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, -60.0, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, -130.0, JudgeType.Miss)]
    public void FinalJudge_Table(JudgeType start, double releaseMs, JudgeType expected)
    {
        PlayNote note = Long(Slow, 0, 1, 3);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();
        double releaseTime = note.EndTime + releaseMs / 1000.0;

        Assert.IsTrue(judge.TryHit(0, note.Time + StartOffset(start), out JudgeResult hit));
        if (start == JudgeType.Miss)
        {
            Assert.AreEqual(JudgeKind.LongFinal, hit.Kind, "시작 MISS는 바로 종합 MISS");
            Assert.AreEqual(JudgeType.Miss, hit.Type);
            Assert.AreEqual(JudgeType.Miss, hit.EndType);
            Assert.AreEqual(LongNoteState.Missed, judge.GetLongState(hit.NoteIndex));
            Assert.IsFalse(judge.TryRelease(0, releaseTime, results), "누르고 있는 롱노트가 없다");
            Assert.AreEqual(expected, hit.Type);
            return;
        }

        Assert.AreEqual(JudgeKind.LongStart, hit.Kind);
        Assert.AreEqual(start, hit.Type);
        Assert.IsTrue(judge.TryGetHeld(0, out _));

        Assert.IsTrue(judge.TryRelease(0, releaseTime, results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.AreEqual(start, final.StartType);
        Assert.AreEqual(expected == JudgeType.Miss ? JudgeType.Miss : start, final.EndType, "범위 안이면 끝 판정 = 시작 판정, 일찍 떼면 Miss");
        Assert.AreEqual(expected, final.Type);
        Assert.AreEqual(releaseMs, final.DeltaMs, 1e-6, "DeltaMs = 뗀 시각 − 끝 시각");
        Assert.IsTrue(final.IsHit);
        Assert.IsTrue(judge.AllJudged);
        Assert.IsFalse(judge.TryGetHeld(0, out _));
    }

    /// <summary>뗀 오차 경계 (GREAT 시작): −120ms·+80ms·+120ms는 끝 판정 = 시작 판정(GREAT), −120.1ms보다 일찍이면 끝 MISS</summary>
    [TestCase(-0.120, JudgeType.Great)]
    [TestCase(-0.1201, JudgeType.Miss)]
    [TestCase(-0.121, JudgeType.Miss)]
    [TestCase(-0.5, JudgeType.Miss)]
    [TestCase(-0.04, JudgeType.Great)]
    [TestCase(0.08, JudgeType.Great)]
    [TestCase(0.12, JudgeType.Great)]
    public void EarlyRelease_Boundary(double releaseOffset, JudgeType expectedEnd)
    {
        PlayNote note = Long(Slow, 1, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(1, note.Time + StartOffset(JudgeType.Great), out JudgeResult hit));
        Assert.AreEqual(JudgeType.Great, hit.Type);
        Assert.IsTrue(judge.TryRelease(1, note.EndTime + releaseOffset, results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(expectedEnd, final.EndType);
        Assert.AreEqual(expectedEnd, final.Type, "범위 안이면 종합 = 시작 판정, 일찍 떼면 MISS");
    }

    /// <summary>GREAT 시작 + 끝에서 정확히 뗌 → GREAT (끝이 올려 주지 않는다)</summary>
    [Test]
    public void GreatStart_ExactRelease_StaysGreat()
    {
        PlayNote note = Long(Slow, 2, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(2, note.Time + StartOffset(JudgeType.Great), out _));
        Assert.IsTrue(judge.TryRelease(2, note.EndTime, results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(JudgeType.Great, final.EndType);
        Assert.AreEqual(JudgeType.Great, final.Type);
    }

    /// <summary>GOOD 시작 + 끝 −100ms에 뗌 → GOOD</summary>
    [Test]
    public void GoodStart_EarlyInRange_StaysGood()
    {
        PlayNote note = Long(Slow, 3, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(3, note.Time + StartOffset(JudgeType.Good), out _));
        Assert.IsTrue(judge.TryRelease(3, note.EndTime - 0.1, results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(JudgeType.Good, final.EndType);
        Assert.AreEqual(JudgeType.Good, final.Type);
    }

    /// <summary>
    /// 끝 + 1/4박(216 BPM ≈ 69ms) 뒤 같은 라인 숏노트: 끝에서 떼고 숏노트를 정시에 누르면
    /// 롱노트 = 시작 판정, 숏노트 = PERFECT로 따로 판정된다 (끝 범위 ±120ms가 다음 노트를 먹지 않는다).
    /// </summary>
    [TestCase(JudgeType.Perfect)]
    [TestCase(JudgeType.Great)]
    public void ShortNoteRightAfterLongEnd_JudgedSeparately(JudgeType start)
    {
        TimingMap fast = TimingMap.Single(216f, 0);
        PlayNote longNote = Long(fast, 0, 4, 6);
        PlayNote shortNote = new PlayNote(0, fast.BeatToSeconds(6.25));
        Assert.AreEqual(69.44, (shortNote.Time - longNote.EndTime) * 1000.0, 0.01, "1/4박 ≈ 69ms");

        NoteJudge judge = new NoteJudge(new[] { longNote, shortNote }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, longNote.Time + StartOffset(start), out JudgeResult hit));
        Assert.AreEqual(start, hit.Type);

        judge.CollectTimeouts(longNote.EndTime, results);
        results.Clear();
        Assert.IsTrue(judge.TryRelease(0, longNote.EndTime, results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.AreEqual(start, final.Type, "롱노트 = 시작 판정");

        judge.CollectTimeouts(shortNote.Time, results);
        Assert.IsEmpty(results, "숏노트는 아직 MISS가 아니다");
        Assert.IsTrue(judge.TryHit(0, shortNote.Time, out JudgeResult next));
        Assert.AreEqual(JudgeKind.Short, next.Kind);
        Assert.AreEqual(JudgeType.Perfect, next.Type, "숏노트 = PERFECT");
        Assert.IsTrue(judge.AllJudged);
    }

    // ───── 끝 판정 통계 ─────

    /// <summary>LongReleaseStats 구간: 일찍 MISS / −120~−40 / −40~+40 / +40~+120 / 자동 GOOD, 평균은 자동 GOOD·시작 MISS 제외</summary>
    [Test]
    public void LongReleaseStats_Buckets()
    {
        LongReleaseStats stats = new LongReleaseStats(GameConstants.Play.LongReleaseMs, GameConstants.Play.DefaultPerfectMs);

        stats.Add(-500.0);   // 일찍 MISS
        stats.Add(-120.1);   // 일찍 MISS
        stats.Add(-120.0);   // −120~−40 (경계 포함)
        stats.Add(-40.1);    // −120~−40
        stats.Add(-40.0);    // −40~+40 (경계 포함)
        stats.Add(0.0);      // −40~+40
        stats.Add(40.0);     // −40~+40 (경계 포함)
        stats.Add(40.1);     // +40~+120
        stats.Add(120.0);    // +40~+120 (경계 포함)
        stats.Add(120.1);    // 자동 GOOD (평균 제외)
        stats.Add(150.0);    // 자동 GOOD (평균 제외)

        // JudgeResult로: 시작 MISS는 따로 세고, 롱노트 종합 판정이 아닌 것은 무시
        stats.Add(new JudgeResult(0, 0, JudgeType.Miss, 140, true, JudgeKind.LongFinal, JudgeType.Miss, JudgeType.Miss));
        stats.Add(new JudgeResult(0, 0, JudgeType.Perfect, 0, true));
        stats.Add(new JudgeResult(0, 0, JudgeType.Perfect, 0, true, JudgeKind.LongStart, JudgeType.Perfect, JudgeType.Perfect));
        stats.Add(new JudgeResult(0, 0, JudgeType.Perfect, 5, false, JudgeKind.Tick, JudgeType.Perfect, JudgeType.Perfect));
        stats.Add(new JudgeResult(0, 0, JudgeType.Great, 10, true, JudgeKind.LongFinal, JudgeType.Great, JudgeType.Great)); // −40~+40

        Assert.AreEqual(2, stats.EarlyMissCount);
        Assert.AreEqual(2, stats.EarlyCount);
        Assert.AreEqual(4, stats.OnTimeCount);
        Assert.AreEqual(2, stats.LateCount);
        Assert.AreEqual(2, stats.AutoGoodCount);
        Assert.AreEqual(1, stats.StartMissCount);
        Assert.AreEqual(10, stats.ReleasedCount);
        Assert.AreEqual(13, stats.TotalCount);

        double sum = -500.0 - 120.1 - 120.0 - 40.1 - 40.0 + 0.0 + 40.0 + 40.1 + 120.0 + 10.0;
        Assert.AreEqual(sum / 10, stats.AverageDeltaMs, 1e-9);

        stats.Reset();
        Assert.AreEqual(0, stats.TotalCount);
        Assert.AreEqual(0, stats.AverageDeltaMs);
    }

    /// <summary>LongReleaseStats를 NoteJudge 결과로 채우면 일찍 뗌·정시·자동 GOOD이 맞는 구간에 들어간다</summary>
    [Test]
    public void LongReleaseStats_FromJudge()
    {
        PlayNote a = Long(Slow, 0, 0, 2);
        PlayNote b = Long(Slow, 1, 0, 2);
        PlayNote c = Long(Slow, 2, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { a, b, c }, 4, DefaultWindows());
        LongReleaseStats stats = new LongReleaseStats(GameConstants.Play.LongReleaseMs, GameConstants.Play.DefaultPerfectMs);
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, 0, out _));
        Assert.IsTrue(judge.TryHit(1, 0, out _));
        Assert.IsTrue(judge.TryHit(2, 0, out _));

        judge.TryRelease(0, 1.0, results);         // 끝 −1000ms → 일찍 MISS
        judge.TryRelease(1, 2.0 + 0.010, results); // 끝 +10ms → −40~+40
        foreach (JudgeResult r in results) stats.Add(r);
        judge.CollectTimeouts(2.0 + 0.2, results); // 끝 +200ms까지 누름 → 자동 GOOD
        foreach (JudgeResult r in results) stats.Add(r);

        Assert.AreEqual(1, stats.EarlyMissCount);
        Assert.AreEqual(1, stats.OnTimeCount);
        Assert.AreEqual(1, stats.AutoGoodCount);
        Assert.AreEqual(3, stats.TotalCount);
        Assert.AreEqual((-1000.0 + 10.0) / 2, stats.AverageDeltaMs, 1e-6);
    }

    /// <summary>늦게 뗌: 끝 + 120ms까지는 판정 없음, 그 뒤 처음 보는 프레임에 끝 GOOD 종합 판정 (모든 틱 인정)</summary>
    [Test]
    public void LateRelease_AutoGoodBoundary()
    {
        PlayNote note = Long(Slow, 2, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(2, note.Time, out _));

        judge.CollectTimeouts(note.EndTime + 0.12, results);
        Assert.AreEqual(7, results.Count, "틱 7개만, 아직 끝 판정 없음");
        Assert.IsTrue(results.TrueForAll(r => r.Kind == JudgeKind.Tick));
        Assert.IsTrue(judge.TryGetHeld(2, out _));

        judge.CollectTimeouts(note.EndTime + 0.1201, results);
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(JudgeKind.LongFinal, results[0].Kind);
        Assert.AreEqual(JudgeType.Good, results[0].EndType);
        Assert.AreEqual(JudgeType.Good, results[0].Type, "시작 PERFECT + 끝 GOOD → GOOD");
        Assert.IsFalse(results[0].IsHit, "시간이 지나 낸 판정");
        Assert.IsTrue(judge.AllJudged);

        Assert.IsFalse(judge.TryRelease(2, note.EndTime + 0.2, results), "끝난 뒤 떼도 아무 일 없음");
    }

    /// <summary>늦게 눌러 이미 지난 틱은 그때 한꺼번에 인정한다 (216 BPM: 1/4박 ≈ 69ms라 GOOD +100ms면 첫 틱이 지나 있다)</summary>
    [Test]
    public void LatePress_PassedTicksCounted()
    {
        TimingMap fast = TimingMap.Single(216f, 0);
        PlayNote note = Long(fast, 0, 4, 6);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();
        ScoreCalculator score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);

        Assert.IsTrue(judge.TryHit(0, note.Time + 0.1, out JudgeResult hit));
        Assert.AreEqual(JudgeType.Good, hit.Type);
        score.Apply(hit);

        judge.CollectTimeouts(note.Time + 0.1, results);
        Assert.AreEqual(1, results.Count, "누른 순간 이미 지난 첫 틱(+69ms)을 인정");
        Assert.AreEqual(JudgeKind.Tick, results[0].Kind);
        foreach (JudgeResult r in results) score.Apply(r);
        Assert.AreEqual(2, score.Combo, "시작 +1, 틱 +1");

        results.Clear();
        Assert.IsTrue(judge.TryRelease(0, note.EndTime, results));
        Assert.AreEqual(6 + 1, results.Count, "남은 틱 6개 + 종합 판정");
        foreach (JudgeResult r in results) score.Apply(r);
        Assert.AreEqual(score.MaxPossibleCombo, score.MaxCombo, "남은 틱도 모두 인정 → 최대 콤보");
        Assert.AreEqual(7, judge.TotalTicks);
    }

    /// <summary>일찍 떼면 뗀 시각까지 지난 틱만 인정하고, 남은 틱은 인정하지 않는다. 종합 MISS로 콤보 0</summary>
    [Test]
    public void EarlyRelease_RemainingTicksNotCounted()
    {
        PlayNote note = Long(Slow, 3, 0, 2); // 틱 0.25 ~ 1.75초
        NoteJudge judge = new NoteJudge(new[] { note, new PlayNote(0, 5.0) }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();
        ScoreCalculator score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);

        Assert.IsTrue(judge.TryHit(3, 0, out JudgeResult hit));
        score.Apply(hit);
        judge.CollectTimeouts(0.3, results);
        foreach (JudgeResult r in results) score.Apply(r);
        Assert.AreEqual(1, results.Count);

        results.Clear();
        Assert.IsTrue(judge.TryRelease(3, 0.8, results));
        Assert.AreEqual(3, results.Count, "0.5·0.75초 틱 2개 + 종합 판정");
        foreach (JudgeResult r in results) score.Apply(r);
        Assert.AreEqual(JudgeType.Miss, results[2].Type);
        Assert.AreEqual(0, score.Combo, "종합 MISS 순간 콤보 0");
        Assert.AreEqual(4, score.MaxCombo, "시작 + 틱 3개");

        judge.CollectTimeouts(3.0, results);
        Assert.IsEmpty(results, "남은 틱은 나오지 않는다");
        Assert.AreEqual(1, score.MissCount);
        Assert.AreEqual(1, score.JudgedCount, "롱노트는 판정 1개");
    }

    /// <summary>시작 MISS(안 누르고 지나감): 바로 종합 MISS, 몸통이 내려오는 동안 그 라인 누름은 빈 타격, 끝 뒤 다음 노트는 판정</summary>
    [Test]
    public void StartMiss_ThenPressIsEmptyHit()
    {
        PlayNote note = Long(Slow, 1, 1, 3);
        NoteJudge judge = new NoteJudge(new[] { note, new PlayNote(1, 3.2) }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.AreEqual(0, judge.CollectTimeouts(note.Time + 0.12, results));
        Assert.AreEqual(1, judge.CollectTimeouts(note.Time + 0.121, results));
        Assert.AreEqual(JudgeKind.LongFinal, results[0].Kind);
        Assert.AreEqual(JudgeType.Miss, results[0].Type);
        Assert.AreEqual(LongNoteState.Missed, judge.GetLongState(results[0].NoteIndex));

        Assert.IsFalse(judge.TryHit(1, 2.0, out _), "몸통 도중 누름은 빈 타격");
        Assert.IsFalse(judge.TryGetHeld(1, out _));
        Assert.AreEqual(0, judge.CollectTimeouts(2.9, results), "틱도 나오지 않는다");

        Assert.IsTrue(judge.TryHit(1, 3.2, out JudgeResult next), "끝 뒤 다음 노트는 판정");
        Assert.AreEqual(JudgeKind.Short, next.Kind);
        Assert.AreEqual(JudgeType.Perfect, next.Type);
    }

    /// <summary>누르는 동안 같은 라인의 다음 노트는 판정하지 않는다 (다른 라인은 그대로)</summary>
    [Test]
    public void WhileHeld_SameLaneNextNoteNotJudged()
    {
        PlayNote note = Long(Slow, 0, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note, new PlayNote(0, 2.1), new PlayNote(2, 1.0) }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, 0, out _));
        Assert.IsFalse(judge.TryHit(0, 2.1, out _), "누르고 있는 라인은 다음 노트를 판정하지 않는다");
        Assert.IsTrue(judge.TryHit(2, 1.0, out JudgeResult other), "다른 라인은 판정");
        Assert.AreEqual(JudgeKind.Short, other.Kind);

        Assert.IsTrue(judge.TryRelease(0, 2.0, results));
        Assert.AreEqual(JudgeType.Perfect, results[results.Count - 1].Type);
        Assert.IsTrue(judge.TryHit(0, 2.1, out JudgeResult next), "뗀 뒤에는 다음 노트 판정");
        Assert.AreEqual(JudgeType.Perfect, next.Type);
    }

    /// <summary>일시정지 재개: 눌려 있으면 그대로 이어 가고, 안 눌려 있으면 재개 순간에 뗀 것으로 끝 판정</summary>
    [Test]
    public void ResolveHeldOnResume()
    {
        PlayNote a = Long(Slow, 0, 0, 4);
        PlayNote b = Long(Slow, 1, 0, 4);
        NoteJudge judge = new NoteJudge(new[] { a, b }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, 0, out _));
        Assert.IsTrue(judge.TryHit(1, 0, out _));

        // 1.1초에 멈췄다가 재개: 라인 0은 눌려 있음, 라인 1은 뗌
        int released = judge.ResolveHeldOnResume(1.1, lane => lane == 0, results);
        Assert.AreEqual(1, released);
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(1, final.Lane);
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.AreEqual(JudgeType.Miss, final.Type, "끝(4초)보다 한참 일찍 뗀 것 → 끝 MISS");
        Assert.AreEqual(4, results.FindAll(r => r.Kind == JudgeKind.Tick).Count, "1.1초까지 지난 틱 4개(0.25~1.0)는 인정");
        Assert.IsTrue(judge.TryGetHeld(0, out _), "눌려 있던 라인은 계속 유지");
        Assert.IsFalse(judge.TryGetHeld(1, out _));

        results.Clear();
        Assert.AreEqual(0, judge.ResolveHeldOnResume(2.0, lane => true, results), "모두 눌려 있으면 아무 일 없음");
        Assert.IsEmpty(results);
    }

    /// <summary>롱노트만 있는 채보를 끝까지 잘 치면 1,000,000점 · 정확도 100% · AP · 최대 콤보 = 노트 수 + 틱 수</summary>
    [Test]
    public void LongOnlyChart_PerfectScore()
    {
        TimingMap timing = TimingMap.Single(150f, 18);
        List<PlayNote> source = new List<PlayNote>
        {
            Long(timing, 0, 0, 2),
            Long(timing, 1, 1, 1.25),
            Long(timing, 2, 2, 4.0 / 3.0 + 2),
            Long(timing, 3, 4, 8)
        };
        NoteJudge judge = new NoteJudge(source, 4, DefaultWindows());
        ScoreCalculator score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.AreEqual(4, judge.TotalCount);
        Assert.AreEqual(7 + 0 + 4 + 15, judge.TotalTicks);

        // 자동 플레이처럼: 시작 시각에 누르고 끝 시각에 뗀다 (시간 순으로 처리)
        List<(double time, int lane, bool press)> events = new List<(double, int, bool)>();
        foreach (PlayNote n in judge.Notes)
        {
            events.Add((n.Time, n.Lane, true));
            events.Add((n.EndTime, n.Lane, false));
        }
        events.Sort((x, y) => x.time.CompareTo(y.time));

        foreach (var e in events)
        {
            judge.CollectTimeouts(e.time, results);
            foreach (JudgeResult r in results) score.Apply(r);
            results.Clear();
            if (e.press)
            {
                Assert.IsTrue(judge.TryHit(e.lane, e.time, out JudgeResult hit));
                score.Apply(hit);
            }
            else
            {
                Assert.IsTrue(judge.TryRelease(e.lane, e.time, results));
                foreach (JudgeResult r in results) score.Apply(r);
                results.Clear();
            }
        }

        Assert.IsTrue(judge.AllJudged);
        Assert.AreEqual(1000000, score.Score);
        Assert.AreEqual(100.0, score.Accuracy, 1e-9);
        Assert.AreEqual(4, score.PerfectCount);
        Assert.AreEqual(4, score.JudgedCount, "롱노트는 판정 1개씩");
        Assert.IsTrue(score.IsAllPerfect);
        Assert.IsTrue(score.IsFullCombo);
        Assert.AreEqual(score.MaxPossibleCombo, score.MaxCombo);
        Assert.AreEqual(30, score.MaxPossibleCombo);
    }

    /// <summary>점수: 롱노트는 숏노트와 같은 무게 (숏 PERFECT 1 + 롱 GOOD 1 → (100 + 30) ÷ 2 = 65%)</summary>
    [Test]
    public void Score_LongCountsAsOneJudge()
    {
        ScoreCalculator score = new ScoreCalculator(2, 3);
        score.Add(JudgeType.Perfect);
        score.AddLongStart();
        score.AddTick();
        score.AddTick();
        score.AddTick();
        score.AddLongFinal(JudgeType.Good);

        Assert.AreEqual(650000, score.Score);
        Assert.AreEqual(65.0, score.Accuracy, 1e-9);
        Assert.AreEqual(5, score.Combo, "숏 1 + 시작 1 + 틱 3, 종합 판정은 콤보를 더하지 않는다");
        Assert.AreEqual(5, score.MaxPossibleCombo);
        Assert.AreEqual(2, score.JudgedCount);
        Assert.IsTrue(score.IsFullCombo);
        Assert.IsFalse(score.IsAllPerfect);
    }
}
