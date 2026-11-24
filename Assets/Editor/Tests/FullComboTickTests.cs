using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// FULL COMBO인데 최대 콤보가 "전체 − 1"로 나온 버그 회귀 테스트 (EditMode).
/// - 원인: 롱노트를 끝 −120ms ~ 끝 −1/4박(150 BPM에서 −100ms) 사이에 떼면 끝 판정은 MISS가 아닌데 아직 오지 않은 마지막 유지 틱이 콤보에서 빠졌다.
/// - 규칙: 끝 판정이 MISS가 아니면 남은 유지 틱은 모두 인정한다 → FULL COMBO면 항상 최대 콤보 = 가능한 최대 콤보.
/// - 실제 게임과 같은 순서(프레임마다 입력 처리 → CollectTimeouts)로 NoteJudge·ScoreCalculator를 돌린다.
/// </summary>
public class FullComboTickTests
{
    /// <summary>입력이 프레임의 CollectTimeouts보다 먼저 오는지, 뒤에 오는지 (한 프레임 늦게 전달된 입력)</summary>
    private enum InputOrder { BeforeTimeouts, AfterTimeouts }

    private const double Frame60 = 1.0 / 60.0;

    private static JudgeWindows DefaultWindows()
    {
        return new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs,
            GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs);
    }

    private static PlayNote Long(TimingMap timing, int lane, double startBeat, double endBeat)
    {
        return new PlayNote(lane, timing.BeatToSeconds(startBeat), timing.BeatToSeconds(endBeat),
            LongNoteTicks.GetTickTimes(timing, startBeat, endBeat));
    }

    /// <summary>
    /// 노트를 모두 제때 누르고 롱노트는 끝 + releaseMs에 떼는 플레이를 끝까지 돌린다.
    /// </summary>
    /// <param name="ticksByNote">노트별로 나온 틱 수를 받을 배열 (없으면 null)</param>
    private static ScoreCalculator Play(IEnumerable<PlayNote> notes, double releaseMs, double frame = Frame60,
        InputOrder order = InputOrder.BeforeTimeouts, int[] ticksByNote = null)
    {
        NoteJudge judge = new NoteJudge(notes, GameConstants.Editor.LaneCount, DefaultWindows());
        ScoreCalculator score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);

        List<(double time, int lane, bool press)> inputs = new List<(double, int, bool)>();
        double last = 0;
        foreach (PlayNote n in judge.Notes)
        {
            inputs.Add((n.Time, n.Lane, true));
            if (n.IsLong) inputs.Add((n.EndTime + releaseMs / 1000.0, n.Lane, false));
            last = System.Math.Max(last, n.EndTime);
        }
        // 같은 시각이면 뗌 먼저 (같은 라인에서 이어지는 노트)
        inputs.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.press.CompareTo(b.press));

        List<JudgeResult> results = new List<JudgeResult>();
        int next = 0;
        double songTime = -1;
        while (songTime < last + 1)
        {
            songTime += frame;
            if (order == InputOrder.BeforeTimeouts) next = Feed(judge, score, inputs, next, songTime, results, ticksByNote);
            judge.CollectTimeouts(songTime, results);
            Apply(score, results, ticksByNote);
            if (order == InputOrder.AfterTimeouts) next = Feed(judge, score, inputs, next, songTime, results, ticksByNote);
        }
        Assert.IsTrue(judge.AllJudged, "모든 노트 판정");
        return score;
    }

    /// <summary>until까지의 입력을 시간 순으로 넣고 다음 입력 번호를 돌려준다.</summary>
    private static int Feed(NoteJudge judge, ScoreCalculator score, List<(double time, int lane, bool press)> inputs, int next, double until,
        List<JudgeResult> results, int[] ticksByNote)
    {
        for (; next < inputs.Count && inputs[next].time <= until; next++)
        {
            (double time, int lane, bool press) input = inputs[next];
            if (!input.press) judge.TryRelease(input.lane, input.time, results);
            else if (judge.TryHit(input.lane, input.time, out JudgeResult hit)) results.Add(hit);
            Apply(score, results, ticksByNote);
        }
        return next;
    }

    private static void Apply(ScoreCalculator score, List<JudgeResult> results, int[] ticksByNote)
    {
        foreach (JudgeResult r in results)
        {
            score.Apply(r);
            if (r.Kind == JudgeKind.Tick && ticksByNote != null) ticksByNote[r.NoteIndex]++;
        }
        results.Clear();
    }

    private static string Describe(string label, ScoreCalculator s)
    {
        return $"[FullComboTick] {label}: MISS {s.MissCount}, 최대 콤보 {s.MaxCombo}/{s.MaxPossibleCombo}, FC {s.IsFullCombo}, AP {s.IsAllPerfect}, 점수 {s.Score}, 정확도 {s.Accuracy:0.00}%";
    }

    /// <summary>FC면 최대 콤보 = 가능한 최대 콤보, 결과로 넘기는 값(결과 화면 MAX COMBO·기록 저장)도 같다</summary>
    private static void AssertFullCombo(ScoreCalculator s)
    {
        Assert.IsTrue(s.IsFullCombo, "FULL COMBO");
        Assert.AreEqual(s.MaxPossibleCombo, s.MaxCombo, "FC면 최대 콤보 = 가능한 최대 콤보");
        ResultData result = ResultData.FromScore(s);
        Assert.AreEqual(s.MaxCombo, result.maxCombo, "결과 화면 MAX COMBO N");
        Assert.AreEqual(s.MaxPossibleCombo, result.maxPossibleCombo, "결과 화면 MAX COMBO 전체");
    }

    // ───── 원인 1: 롱노트를 일찍 떼기 ─────

    /// <summary>150 BPM 4박 롱노트 하나(틱 15개, 전체 콤보 16)를 떼는 시점별. 점수·정확도는 종합 판정으로만 정해진다</summary>
    [TestCase(-130.0, 1, 15, 0)]
    [TestCase(-120.0, 0, 16, 1000000)]
    [TestCase(-110.0, 0, 16, 1000000)]
    [TestCase(-101.0, 0, 16, 1000000)]
    [TestCase(-90.0, 0, 16, 1000000)]
    [TestCase(0.0, 0, 16, 1000000)]
    [TestCase(100.0, 0, 16, 1000000)]
    [TestCase(130.0, 0, 16, 300000)]
    public void ReleaseTiming_SingleLong(double releaseMs, int expectedMiss, int expectedMaxCombo, int expectedScore)
    {
        ScoreCalculator s = Play(new[] { Long(TimingMap.Single(150f, 0), 0, 4, 8) }, releaseMs);
        Debug.Log(Describe($"뗌 {releaseMs:+0;-0;0}ms", s));

        Assert.AreEqual(16, s.MaxPossibleCombo);
        Assert.AreEqual(expectedMiss, s.MissCount);
        Assert.AreEqual(expectedScore, s.Score, "범위 안이면 시작 판정(PERFECT) 그대로, 끝 + 120ms를 지나면 자동 GOOD, 일찍 떼면 0");
        Assert.AreEqual(expectedMaxCombo, s.MaxCombo, "시작 1 + 틱 15. 끝 MISS만 남은 틱을 인정하지 않아 15");
        if (expectedMiss == 0)
        {
            AssertFullCombo(s);
            return;
        }
        Assert.IsFalse(s.IsFullCombo);
        Assert.AreEqual(0, s.Combo, "끝 MISS면 콤보 0");
    }

    /// <summary>빠른 BPM(300, 1/4박 = 50ms)에서는 −110ms에 떼면 남은 틱이 두 개다 → 둘 다 인정</summary>
    [Test]
    public void EarlyRelease_FastBpm_TwoTicksLeft()
    {
        ScoreCalculator s = Play(new[] { Long(TimingMap.Single(300f, 0), 0, 4, 8) }, -110);
        Debug.Log(Describe("300 BPM 뗌 −110ms", s));
        AssertFullCombo(s);
    }

    /// <summary>남은 틱은 종합 판정보다 먼저, 시작 판정 등급으로 나온다 (DeltaMs는 뗀 시각 − 틱 시각이라 음수)</summary>
    [Test]
    public void EarlyRelease_RemainingTickBeforeFinal()
    {
        PlayNote note = Long(TimingMap.Single(150f, 0), 0, 4, 8);
        NoteJudge judge = new NoteJudge(new[] { note }, GameConstants.Editor.LaneCount, DefaultWindows());
        List<JudgeResult> results = new List<JudgeResult>();
        Assert.IsTrue(judge.TryHit(0, note.Time + 0.06, out JudgeResult hit));
        Assert.AreEqual(JudgeType.Great, hit.Type);
        judge.CollectTimeouts(note.EndTime - 0.115, results);
        Assert.AreEqual(14, results.Count, "끝 −115ms까지 지난 틱");

        results.Clear();
        Assert.IsTrue(judge.TryRelease(0, note.EndTime - 0.11, results));
        Assert.AreEqual(2, results.Count, "남은 틱 1 + 종합");
        Assert.AreEqual(JudgeKind.Tick, results[0].Kind);
        Assert.AreEqual(JudgeType.Great, results[0].StartType);
        Assert.AreEqual(-10.0, results[0].DeltaMs, 1e-6);
        Assert.AreEqual(JudgeKind.LongFinal, results[1].Kind);
        Assert.AreEqual(JudgeType.Great, results[1].Type);

        judge.CollectTimeouts(note.EndTime + 1, results);
        Assert.IsEmpty(results, "끝난 뒤 틱이 다시 나오지 않는다");
    }

    // ───── 원인 2: BPM 구간 경계 ─────

    /// <summary>BPM 경계(150 · 280박)에 걸치거나 닿은 롱노트: 예상 틱 수 = 실제 판정된 틱 수. 공식 채보와 같은 3구간</summary>
    [TestCase(149.5, 151.0, 5)]
    [TestCase(148.0, 151.0, 11)]
    [TestCase(279.25, 280.5, 4)]
    [TestCase(280.0, 281.0, 3)]
    public void BpmBoundary_TickCount(double start, double end, int expected)
    {
        TimingMap timing = new TimingMap(new List<TimingPointData>
        {
            new TimingPointData { beat = 0f, bpm = 150.07f },
            new TimingPointData { beat = 150f, bpm = 150.68f },
            new TimingPointData { beat = 280f, bpm = 151.2f }
        }, 0);
        StringBuilder log = new StringBuilder($"[FullComboTick] BPM 경계 {start} ~ {end}박: 예상 틱 {expected}");
        foreach (double releaseMs in new[] { 0.0, 100.0, -90.0, -110.0 })
        {
            int[] ticks = new int[1];
            ScoreCalculator s = Play(new[] { Long(timing, 0, start, end) }, releaseMs, Frame60, InputOrder.BeforeTimeouts, ticks);
            log.Append($" | 뗌 {releaseMs:+0;-0;0}ms 틱 {ticks[0]}, 콤보 {s.MaxCombo}/{s.MaxPossibleCombo}");
            if (releaseMs < -100) Debug.Log(log.ToString());
            Assert.AreEqual(expected, s.TotalTicks, "예상 틱 수");
            Assert.AreEqual(expected, ticks[0], $"뗌 {releaseMs}ms 실제 틱 수");
            AssertFullCombo(s);
        }
    }

    // ───── 원인 3: 같은 프레임의 순서 ─────

    /// <summary>
    /// 마지막 틱·끝 판정·입력이 같은 프레임에 겹칠 때: 입력이 CollectTimeouts보다 먼저/뒤에 와도, 프레임이 끊겨도(최대 200ms) 틱이 빠지거나 두 번 세어지지 않는다.
    /// 누름이 200ms 프레임만큼 늦게 전달되면 이미 놓친 MISS라 (지금 규칙) FC가 아니므로 그 조합은 뺀다.
    /// </summary>
    [TestCase(0.0)]
    [TestCase(-90.0)]
    [TestCase(-100.0)]
    [TestCase(-110.0)]
    public void SameFrame_OrderAndHitch(double releaseMs)
    {
        TimingMap timing = TimingMap.Single(150f, 0);
        List<string> wrong = new List<string>();
        foreach (InputOrder order in new[] { InputOrder.BeforeTimeouts, InputOrder.AfterTimeouts })
        {
            foreach (double frame in new[] { 1.0 / 240.0, Frame60, 0.05, 0.1, 0.2 })
            {
                if (order == InputOrder.AfterTimeouts && frame * 1000.0 > GameConstants.Play.DefaultGoodMs) continue;
                ScoreCalculator s = Play(new[] { Long(timing, 0, 4, 8), Long(timing, 1, 4.5, 7.75) }, releaseMs, frame, order);
                if (s.MissCount != 0 || s.MaxCombo != s.MaxPossibleCombo)
                    wrong.Add($"{order} 프레임 {frame * 1000:0}ms: MISS {s.MissCount}, 콤보 {s.MaxCombo}/{s.MaxPossibleCombo}");
            }
        }
        Debug.Log($"[FullComboTick] 같은 프레임 순서, 뗌 {releaseMs:+0;-0;0}ms: 9가지 중 어긋남 {wrong.Count} {string.Join(" / ", wrong)}");
        Assert.IsEmpty(wrong);
    }

    // ───── 원인 4: 그 밖의 경로 ─────

    /// <summary>자동 GOOD(떼지 않음), 일시정지 재개 순간 끝 −110ms에 뗀 것으로 처리 → 둘 다 틱 전부 인정</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void OtherPaths_AutoGoodAndResume(bool resume)
    {
        PlayNote note = Long(TimingMap.Single(150f, 0), 0, 4, 8);
        NoteJudge judge = new NoteJudge(new[] { note }, GameConstants.Editor.LaneCount, DefaultWindows());
        ScoreCalculator s = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
        List<JudgeResult> results = new List<JudgeResult>();
        Assert.IsTrue(judge.TryHit(0, note.Time, out JudgeResult hit));
        s.Apply(hit);
        if (resume) Assert.AreEqual(1, judge.ResolveHeldOnResume(note.EndTime - 0.11, lane => false, results));
        else judge.CollectTimeouts(note.EndTime + 0.5, results);
        Apply(s, results, null);
        Debug.Log(Describe(resume ? "재개 순간 끝 −110ms에 뗌" : "자동 GOOD", s));
        AssertFullCombo(s);
    }

    // ───── 공식 채보 전체 ─────

    /// <summary>공식 NORMAL 전체: 롱노트를 정시 / 모두 끝 −110ms에 떼도 최대 콤보 = 가능한 최대 콤보, 1,000,000점</summary>
    [TestCase(0.0)]
    [TestCase(-110.0)]
    public void OfficialChart_Full(double releaseMs)
    {
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);
        TimingMap timing = TimingMap.FromChart(chart, 0);
        List<PlayNote> notes = new List<PlayNote>();
        foreach (NoteData n in chart.notes)
            notes.Add(n.type == NoteData.TypeLong ? Long(timing, n.lane, n.beat, n.endBeat) : new PlayNote(n.lane, timing.BeatToSeconds(n.beat)));

        ScoreCalculator s = Play(notes, releaseMs);
        Debug.Log(Describe($"공식 NORMAL 롱노트 뗌 {releaseMs:+0;-0;0}ms", s));
        Assert.AreEqual(1000000, s.Score);
        AssertFullCombo(s);
    }
}
