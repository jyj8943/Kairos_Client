using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// FAST / SLOW 규칙(FastSlowRules)·집계(ScoreCalculator.FastCount·SlowCount)·플레이 중 정확도(LiveAccuracy) EditMode 테스트
/// (기획서 3-4 "판정 패널 규칙"). 결과는 실제 NoteJudge로 만든다 (60 BPM: 1박 = 1초).
/// </summary>
public class FastSlowRulesTests
{
    private static readonly TimingMap Timing = TimingMap.Single(60f, 0);

    private NoteJudge judge;
    private ScoreCalculator score;
    private readonly List<JudgeResult> buffer = new List<JudgeResult>();

    /// <summary>판정 범위 기본값 (테스트 시각은 이 값으로 계산한다)</summary>
    private static readonly JudgeWindows Windows = new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs,
        GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs);

    /// <summary>각 판정 범위 안쪽 오차 (ms): PERFECT 안, GREAT 안, GOOD 안, MISS 안(GOOD 밖)</summary>
    private static double PerfectMs => Windows.PerfectMs * 0.5;
    private static double GreatMs => (Windows.PerfectMs + Windows.GreatMs) * 0.5;
    private static double GoodMs => (Windows.GreatMs + Windows.GoodMs) * 0.5;
    private static double MissMs => (Windows.GoodMs + Windows.MissMs) * 0.5;

    /// <summary>롱노트 끝 범위 (±ms)</summary>
    private static double ReleaseMs => GameConstants.Play.LongReleaseMs;

    private static PlayNote Short(int lane, double beat) => new PlayNote(lane, Timing.BeatToSeconds(beat));

    private static PlayNote Long(int lane, double startBeat, double endBeat) =>
        new PlayNote(lane, Timing.BeatToSeconds(startBeat), Timing.BeatToSeconds(endBeat), LongNoteTicks.GetTickTimes(Timing, startBeat, endBeat));

    private void Setup(params PlayNote[] notes)
    {
        judge = new NoteJudge(new List<PlayNote>(notes), GameConstants.Editor.LaneCount, Windows);
        score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
    }

    /// <summary>누른다 (time 초 + deltaMs). 결과를 점수에 넣고 돌려준다</summary>
    private JudgeResult Press(int lane, double time, double deltaMs)
    {
        Assert.IsTrue(judge.TryHit(lane, time + deltaMs / 1000.0, out JudgeResult result), "판정이 나야 한다");
        score.Apply(result);
        return result;
    }

    /// <summary>뗀다. 틱은 점수에만 넣고 종합 판정(LongFinal)을 돌려준다</summary>
    private JudgeResult Release(int lane, double time, double deltaMs)
    {
        buffer.Clear();
        Assert.IsTrue(judge.TryRelease(lane, time + deltaMs / 1000.0, buffer));
        return ApplyAndFindFinal();
    }

    /// <summary>시간을 흘린다. 결과를 모두 점수에 넣고 마지막 종합·숏노트 판정을 돌려준다</summary>
    private JudgeResult Timeout(double songTime)
    {
        judge.CollectTimeouts(songTime, buffer);
        return ApplyAndFindFinal();
    }

    private JudgeResult ApplyAndFindFinal()
    {
        JudgeResult final = default;
        foreach (JudgeResult r in buffer)
        {
            score.Apply(r);
            if (r.Kind != JudgeKind.Tick) final = r;
        }
        return final;
    }

    private void AssertBalanced()
    {
        Assert.AreEqual(score.GreatCount + score.GoodCount, score.FastCount + score.SlowCount, "FAST + SLOW = GREAT + GOOD");
    }

    /// <summary>1. 숏노트: GREAT 일찍 → FAST, GOOD 늦게 → SLOW, PERFECT·MISS → 없음 (표시·집계 같음)</summary>
    [Test]
    public void Short_GreatEarlyFast_GoodLateSlow_PerfectMissNone()
    {
        Setup(Short(0, 1), Short(1, 1), Short(2, 1), Short(3, 1), Short(0, 3));

        JudgeResult great = Press(0, 1, -GreatMs);
        Assert.AreEqual(JudgeType.Great, great.Type);
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetDisplay(great));
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetCount(great));

        JudgeResult good = Press(1, 1, GoodMs);
        Assert.AreEqual(JudgeType.Good, good.Type);
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetDisplay(good));
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetCount(good));

        JudgeResult perfect = Press(2, 1, -PerfectMs);
        Assert.AreEqual(JudgeType.Perfect, perfect.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(perfect), "PERFECT는 일찍 쳐도 없음");

        JudgeResult missPressed = Press(3, 1, -MissMs);
        Assert.AreEqual(JudgeType.Miss, missPressed.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(missPressed), "MISS 범위에서 누름");

        JudgeResult missed = Timeout(3 + (Windows.GoodMs + 1) / 1000.0);
        Assert.AreEqual(JudgeType.Miss, missed.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(missed), "놓친 MISS");

        Assert.AreEqual(1, score.FastCount);
        Assert.AreEqual(1, score.SlowCount);
        AssertBalanced();
    }

    /// <summary>2. 롱노트 시작 GREAT 일찍 → 시작에 FAST 표시, 집계는 그대로 (종합 판정 때 센다)</summary>
    [Test]
    public void LongStart_GreatEarly_DisplaysFastButNotCounted()
    {
        Setup(Long(0, 1, 3));
        JudgeResult start = Press(0, 1, -GreatMs);
        Assert.AreEqual(JudgeKind.LongStart, start.Kind);
        Assert.AreEqual(JudgeType.Great, start.Type);
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetDisplay(start));
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(start));
        Assert.AreEqual(0, score.FastCount + score.SlowCount);
    }

    /// <summary>3. 롱노트 끝에서 일찍 뗌(범위 안) → 종합 FAST, 1개 셈</summary>
    [Test]
    public void LongFinal_ReleaseEarlyInRange_Fast()
    {
        Setup(Long(0, 1, 3));
        Press(0, 1, -GreatMs);
        JudgeResult final = Release(0, 3, -ReleaseMs * 0.5);
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.AreEqual(JudgeType.Great, final.Type, "등급은 시작 판정");
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetDisplay(final));
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetCount(final));
        Assert.AreEqual(1, score.FastCount);
        Assert.AreEqual(0, score.SlowCount);
        AssertBalanced();
    }

    /// <summary>4. 롱노트 끝에서 늦게 뗌(범위 안) → 종합 SLOW, 1개 셈</summary>
    [Test]
    public void LongFinal_ReleaseLateInRange_Slow()
    {
        Setup(Long(0, 1, 3));
        Press(0, 1, -GoodMs);
        JudgeResult final = Release(0, 3, ReleaseMs * 0.5);
        Assert.AreEqual(JudgeType.Good, final.Type);
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetCount(final));
        Assert.AreEqual(0, score.FastCount);
        Assert.AreEqual(1, score.SlowCount);
        AssertBalanced();
    }

    /// <summary>5. SLOW로 시작(늦게 눌러 GREAT)해 끝에서 일찍 뗌 → 종합 GREAT + FAST (시작 방향과 상관없음)</summary>
    [Test]
    public void LongFinal_SlowStartEarlyRelease_Fast()
    {
        Setup(Long(0, 1, 3));
        JudgeResult start = Press(0, 1, GreatMs);
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetDisplay(start), "시작은 SLOW 표시");
        JudgeResult final = Release(0, 3, -ReleaseMs * 0.25);
        Assert.AreEqual(JudgeType.Great, final.Type);
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetDisplay(final));
        Assert.AreEqual(1, score.FastCount);
        Assert.AreEqual(0, score.SlowCount);
    }

    /// <summary>6. 끝을 지나 계속 눌러 자동 GOOD → SLOW, 1개 셈 (DeltaMs가 + 범위보다 크다)</summary>
    [Test]
    public void LongFinal_AutoGoodAfterEnd_Slow()
    {
        Setup(Long(0, 1, 3));
        Press(0, 1, PerfectMs);
        JudgeResult final = Timeout(3 + (ReleaseMs + 5) / 1000.0);
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.IsFalse(final.IsHit, "시간이 지나 난 자동 GOOD");
        Assert.AreEqual(JudgeType.Good, final.Type);
        Assert.Greater(final.DeltaMs, ReleaseMs, "자동 GOOD의 DeltaMs = 끝에서 지난 시간 (양수)");
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetDisplay(final));
        Assert.AreEqual(FastSlow.Slow, FastSlowRules.GetCount(final));
        Assert.AreEqual(0, score.FastCount);
        Assert.AreEqual(1, score.SlowCount);
        AssertBalanced();
    }

    /// <summary>7. 일찍 떼 MISS, 시작 MISS(누름·놓침) → 없음, 세지 않음</summary>
    [Test]
    public void LongFinal_Misses_None()
    {
        Setup(Long(0, 1, 3), Long(1, 1, 3), Long(2, 1, 3));

        Press(0, 1, -GreatMs);
        JudgeResult earlyMiss = Release(0, 3, -(ReleaseMs + 50));
        Assert.AreEqual(JudgeType.Miss, earlyMiss.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(earlyMiss));
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(earlyMiss));

        JudgeResult startMissPressed = Press(1, 1, -MissMs);
        Assert.AreEqual(JudgeKind.LongFinal, startMissPressed.Kind, "시작 MISS는 바로 종합 MISS");
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(startMissPressed));

        JudgeResult startMissed = Timeout(1 + (Windows.GoodMs + 1) / 1000.0);
        Assert.AreEqual(JudgeType.Miss, startMissed.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(startMissed));

        Assert.AreEqual(3, score.MissCount);
        Assert.AreEqual(0, score.FastCount + score.SlowCount);
        AssertBalanced();
    }

    /// <summary>8. 시작·끝 PERFECT 롱노트 → 시작·종합 모두 없음 (일찍 뗐어도 PERFECT면 없음). 유지 틱도 없음</summary>
    [Test]
    public void Long_PerfectStartAndEnd_None()
    {
        Setup(Long(0, 1, 3));
        JudgeResult start = Press(0, 1, -PerfectMs);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(start));

        Timeout(2.0);
        bool sawTick = false;
        foreach (JudgeResult r in buffer)
        {
            if (r.Kind != JudgeKind.Tick) continue;
            sawTick = true;
            Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(r), "유지 틱");
            Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(r));
        }
        Assert.IsTrue(sawTick);

        JudgeResult final = Release(0, 3, -ReleaseMs * 0.5);
        Assert.AreEqual(JudgeType.Perfect, final.Type);
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(final));
        Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(final));
        Assert.AreEqual(0, score.FastCount + score.SlowCount);
    }

    /// <summary>9. 한 곡 분량 (숏노트·롱노트 섞어 200개, 정해 둔 난수로 누름·뗌·놓침): 매 판정 뒤 FAST + SLOW = GREAT + GOOD</summary>
    [Test]
    public void Simulation_Balanced()
    {
        System.Random random = new System.Random(20261113);
        List<PlayNote> notes = new List<PlayNote>();
        for (int i = 0; i < 200; i++)
        {
            int lane = i % GameConstants.Editor.LaneCount;
            double beat = 2 + i;
            notes.Add(i % 3 == 0 ? Long(lane, beat, beat + 0.75) : Short(lane, beat));
        }
        Setup(notes.ToArray());

        double[] pressOffsets = { -PerfectMs, PerfectMs, -GreatMs, GreatMs, -GoodMs, GoodMs, -MissMs };
        double[] releaseOffsets = { -ReleaseMs * 0.5, ReleaseMs * 0.5, -(ReleaseMs + 40), double.NaN }; // NaN = 계속 눌러 자동 GOOD

        foreach (PlayNote note in judge.Notes)
        {
            // 가끔 놓친다
            if (random.Next(10) == 0)
            {
                Timeout(note.Time + (Windows.GoodMs + 1) / 1000.0);
                AssertBalanced();
                continue;
            }

            JudgeResult start = Press(note.Lane, note.Time, pressOffsets[random.Next(pressOffsets.Length)]);
            AssertBalanced();
            if (start.Kind != JudgeKind.LongStart) continue;

            double release = releaseOffsets[random.Next(releaseOffsets.Length)];
            if (double.IsNaN(release)) Timeout(note.EndTime + (ReleaseMs + 5) / 1000.0);
            else Release(note.Lane, note.EndTime, release);
            AssertBalanced();
        }

        Assert.AreEqual(judge.TotalCount, score.JudgedCount, "전부 판정");
        Assert.Greater(score.FastCount, 0);
        Assert.Greater(score.SlowCount, 0);
        AssertBalanced();
        UnityEngine.Debug.Log($"[FastSlowTest] 시뮬레이션: P/G/Go/M {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, FAST/SLOW {score.FastCount}/{score.SlowCount}, 실시간 정확도 {score.LiveAccuracy:0.00}%");
    }

    /// <summary>10. LiveAccuracy: 판정 0개면 100%, 판정 뒤에는 비율 합 ÷ 판정한 수. 결과용 Accuracy는 전체 노트 수로 나눔 그대로</summary>
    [Test]
    public void LiveAccuracy()
    {
        ScoreCalculator calc = new ScoreCalculator(10);
        Assert.AreEqual(100.0, calc.LiveAccuracy, 1e-9, "판정 전");
        Assert.AreEqual(0.0, calc.Accuracy, 1e-9);

        calc.Add(JudgeType.Perfect);
        calc.Add(JudgeType.Great);
        calc.Add(JudgeType.Good);
        calc.Add(JudgeType.Miss);
        double weighted = GameConstants.Play.PerfectRatio + GameConstants.Play.GreatRatio + GameConstants.Play.GoodRatio + GameConstants.Play.MissRatio;
        Assert.AreEqual(4, calc.JudgedCount);
        Assert.AreEqual(weighted / 4, calc.LiveAccuracy, 1e-9, "(100 + 70 + 30 + 0) ÷ 4 = 50%");
        Assert.AreEqual(weighted / 10, calc.Accuracy, 1e-9, "결과 화면 정확도는 전체 10개로 나눔");
    }
}
