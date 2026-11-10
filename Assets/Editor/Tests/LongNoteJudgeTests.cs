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

    /// <summary>
    /// 종합 판정 표 16칸 = 시작·끝 중 낮은 쪽. 시작 MISS면 끝과 상관없이 MISS.
    /// 시작 오프셋: P 0ms · G +60ms · GOOD +100ms · MISS +140ms (MISS 범위에서 누름)
    /// 끝 오프셋: P 0ms · G −60ms · GOOD −100ms · MISS −130ms (GOOD 범위보다 일찍 뗌)
    /// </summary>
    [TestCase(JudgeType.Perfect, JudgeType.Perfect, JudgeType.Perfect)]
    [TestCase(JudgeType.Perfect, JudgeType.Great, JudgeType.Great)]
    [TestCase(JudgeType.Perfect, JudgeType.Good, JudgeType.Good)]
    [TestCase(JudgeType.Perfect, JudgeType.Miss, JudgeType.Miss)]
    [TestCase(JudgeType.Great, JudgeType.Perfect, JudgeType.Great)]
    [TestCase(JudgeType.Great, JudgeType.Great, JudgeType.Great)]
    [TestCase(JudgeType.Great, JudgeType.Good, JudgeType.Good)]
    [TestCase(JudgeType.Great, JudgeType.Miss, JudgeType.Miss)]
    [TestCase(JudgeType.Good, JudgeType.Perfect, JudgeType.Good)]
    [TestCase(JudgeType.Good, JudgeType.Great, JudgeType.Good)]
    [TestCase(JudgeType.Good, JudgeType.Good, JudgeType.Good)]
    [TestCase(JudgeType.Good, JudgeType.Miss, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, JudgeType.Perfect, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, JudgeType.Great, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, JudgeType.Good, JudgeType.Miss)]
    [TestCase(JudgeType.Miss, JudgeType.Miss, JudgeType.Miss)]
    public void FinalJudge_Table(JudgeType start, JudgeType end, JudgeType expected)
    {
        double[] startOffset = { 0, 0.06, 0.1, 0.14 };
        double[] endOffset = { 0, -0.06, -0.1, -0.13 };

        PlayNote note = Long(Slow, 0, 1, 3);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, note.Time + startOffset[(int)start], out JudgeResult hit));
        if (start == JudgeType.Miss)
        {
            Assert.AreEqual(JudgeKind.LongFinal, hit.Kind, "시작 MISS는 바로 종합 MISS");
            Assert.AreEqual(JudgeType.Miss, hit.Type);
            Assert.AreEqual(LongNoteState.Missed, judge.GetLongState(hit.NoteIndex));
            Assert.IsFalse(judge.TryRelease(0, note.EndTime + endOffset[(int)end], results), "누르고 있는 롱노트가 없다");
            Assert.AreEqual(expected, hit.Type);
            return;
        }

        Assert.AreEqual(JudgeKind.LongStart, hit.Kind);
        Assert.AreEqual(start, hit.Type);
        Assert.IsTrue(judge.TryGetHeld(0, out _));

        Assert.IsTrue(judge.TryRelease(0, note.EndTime + endOffset[(int)end], results));
        JudgeResult final = results[results.Count - 1];
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.AreEqual(start, final.StartType);
        Assert.AreEqual(end, final.EndType);
        Assert.AreEqual(expected, final.Type);
        Assert.IsTrue(judge.AllJudged);
        Assert.IsFalse(judge.TryGetHeld(0, out _));
    }

    /// <summary>일찍 뗌 경계: 끝 −120ms는 끝 GOOD, −121ms는 끝 MISS</summary>
    [TestCase(-0.120, JudgeType.Good)]
    [TestCase(-0.1201, JudgeType.Miss)]
    [TestCase(-0.121, JudgeType.Miss)]
    [TestCase(-0.5, JudgeType.Miss)]
    [TestCase(-0.04, JudgeType.Perfect)]
    [TestCase(0.08, JudgeType.Great)]
    [TestCase(0.12, JudgeType.Good)]
    public void EarlyRelease_Boundary(double releaseOffset, JudgeType expectedEnd)
    {
        PlayNote note = Long(Slow, 1, 0, 2);
        NoteJudge judge = new NoteJudge(new[] { note }, 4, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(1, note.Time, out _));
        Assert.IsTrue(judge.TryRelease(1, note.EndTime + releaseOffset, results));
        Assert.AreEqual(expectedEnd, results[results.Count - 1].EndType);
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
