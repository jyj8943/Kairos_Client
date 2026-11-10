using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 판정(JudgeWindows·NoteJudge)·점수(ScoreCalculator) EditMode 테스트 (기획서 2-1).
/// </summary>
public class InGameJudgeTests
{
    private static JudgeWindows DefaultWindows()
    {
        return new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs,
            GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs);
    }

    /// <summary>1. 경계값: ±40 PERFECT, ±41 GREAT, ±80 GREAT, ±120 GOOD, ±121 MISS, ±160 MISS, ±161 빈 타격</summary>
    [TestCase(0, true, JudgeType.Perfect)]
    [TestCase(40, true, JudgeType.Perfect)]
    [TestCase(41, true, JudgeType.Great)]
    [TestCase(80, true, JudgeType.Great)]
    [TestCase(81, true, JudgeType.Good)]
    [TestCase(120, true, JudgeType.Good)]
    [TestCase(121, true, JudgeType.Miss)]
    [TestCase(160, true, JudgeType.Miss)]
    [TestCase(161, false, JudgeType.Miss)]
    public void JudgeWindows_Boundaries(int ms, bool judged, JudgeType expected)
    {
        JudgeWindows windows = DefaultWindows();
        foreach (int delta in new[] { ms, -ms })
        {
            Assert.AreEqual(judged, windows.TryJudge(delta, out JudgeType type), $"{delta}ms 판정 여부");
            if (judged) Assert.AreEqual(expected, type, $"{delta}ms");
        }
    }

    /// <summary>NoteJudge가 실제 초 단위 입력에서도 경계값을 같게 판정한다 (노트 시간이 소수여도)</summary>
    [TestCase(0.04, JudgeType.Perfect)]
    [TestCase(-0.04, JudgeType.Perfect)]
    [TestCase(0.041, JudgeType.Great)]
    [TestCase(-0.12, JudgeType.Good)]
    [TestCase(0.121, JudgeType.Miss)]
    [TestCase(-0.16, JudgeType.Miss)]
    public void NoteJudge_HitBoundaries(double offset, JudgeType expected)
    {
        double noteTime = 12.3456789;
        NoteJudge judge = new NoteJudge(new[] { new PlayNote(0, noteTime) }, 4, DefaultWindows());

        Assert.IsTrue(judge.TryHit(0, noteTime + offset, out JudgeResult result));
        Assert.AreEqual(expected, result.Type);
        Assert.IsTrue(judge.AllJudged);
    }

    /// <summary>MISS 범위 밖은 빈 타격 (판정 없음), 다른 라인 노트는 건드리지 않는다</summary>
    [Test]
    public void NoteJudge_EmptyHitAndLanesIndependent()
    {
        NoteJudge judge = new NoteJudge(new[] { new PlayNote(0, 1.0), new PlayNote(1, 1.0), new PlayNote(0, 2.0) }, 4, DefaultWindows());

        Assert.IsFalse(judge.TryHit(0, 1.0 - 0.161, out _), "-161ms는 빈 타격");
        Assert.IsFalse(judge.TryHit(2, 1.0, out _), "노트 없는 라인은 빈 타격");
        Assert.AreEqual(0, judge.JudgedCount);

        Assert.IsTrue(judge.TryHit(0, 1.0, out JudgeResult first));
        Assert.AreEqual(0, first.Lane);
        Assert.IsFalse(judge.IsJudged(1), "라인 1 노트는 그대로");

        // 라인 0의 다음 노트(2.0초)가 대상이 된다
        Assert.IsTrue(judge.TryPeekNext(0, out int next));
        Assert.AreEqual(2.0, judge.Notes[next].Time);
    }

    /// <summary>+120ms까지는 놓친 것으로 보지 않고, 넘으면 MISS</summary>
    [Test]
    public void NoteJudge_LateMissAfterGoodWindow()
    {
        NoteJudge judge = new NoteJudge(new[] { new PlayNote(0, 1.0), new PlayNote(3, 1.5) }, 4, DefaultWindows());
        List<JudgeResult> misses = new List<JudgeResult>();

        Assert.AreEqual(0, judge.CollectTimeouts(1.12, misses));
        Assert.AreEqual(1, judge.CollectTimeouts(1.121, misses));
        Assert.AreEqual(JudgeType.Miss, misses[0].Type);
        Assert.IsFalse(misses[0].IsHit);
        Assert.AreEqual(2, judge.CollectTimeouts(10.0, new List<JudgeResult>()) + 1, "남은 노트 하나만 더 MISS");
        Assert.IsTrue(judge.AllJudged);
    }

    /// <summary>2. 전부 PERFECT면 노트 수와 상관없이 1,000,000, 전부 MISS면 0</summary>
    [TestCase(1)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(1234)]
    public void Score_AllPerfectIsMaxAndAllMissIsZero(int noteCount)
    {
        ScoreCalculator perfect = new ScoreCalculator(noteCount);
        ScoreCalculator miss = new ScoreCalculator(noteCount);
        for (int i = 0; i < noteCount; i++)
        {
            perfect.Add(JudgeType.Perfect);
            miss.Add(JudgeType.Miss);
        }

        Assert.AreEqual(1000000, perfect.Score);
        Assert.AreEqual(noteCount, perfect.MaxCombo);
        Assert.IsTrue(perfect.IsAllPerfect);
        Assert.IsTrue(perfect.IsFullCombo);
        Assert.AreEqual(100.0, perfect.Accuracy, 1e-9);

        Assert.AreEqual(0, miss.Score);
        Assert.AreEqual(0, miss.MaxCombo);
        Assert.AreEqual(PlayRank.F, miss.Rank);
        Assert.IsFalse(miss.IsFullCombo);
    }

    /// <summary>판정이 섞이면 비율 합 ÷ 노트 수, 반올림은 마지막에 한 번 (7개 중 GREAT 1개 = 957,142.857… → 957,143)</summary>
    [Test]
    public void Score_MixedJudgesAndCombo()
    {
        ScoreCalculator score = new ScoreCalculator(7);
        foreach (JudgeType judge in new[] { JudgeType.Perfect, JudgeType.Perfect, JudgeType.Great, JudgeType.Perfect, JudgeType.Perfect, JudgeType.Perfect, JudgeType.Perfect })
            score.Add(judge);

        Assert.AreEqual(957143, score.Score);
        Assert.AreEqual(PlayRank.S, score.Rank);
        Assert.AreEqual(7, score.MaxCombo);
        Assert.IsTrue(score.IsFullCombo);
        Assert.IsFalse(score.IsAllPerfect);
        Assert.AreEqual(670.0 / 7.0, score.Accuracy, 1e-9);

        // MISS가 나오면 콤보가 0이 되고, 최대 콤보는 남는다
        ScoreCalculator combo = new ScoreCalculator(5);
        foreach (JudgeType judge in new[] { JudgeType.Perfect, JudgeType.Good, JudgeType.Miss, JudgeType.Great, JudgeType.Perfect })
            combo.Add(judge);
        Assert.AreEqual(2, combo.Combo);
        Assert.AreEqual(2, combo.MaxCombo);
        Assert.IsFalse(combo.IsFullCombo);
        Assert.AreEqual((100 + 30 + 0 + 70 + 100) / 5.0, combo.Accuracy, 1e-9);
        Assert.AreEqual(600000, combo.Score);
    }

    /// <summary>3. 랭크 경계값</summary>
    [TestCase(1000000, PlayRank.S)]
    [TestCase(950000, PlayRank.S)]
    [TestCase(949999, PlayRank.A)]
    [TestCase(900000, PlayRank.A)]
    [TestCase(899999, PlayRank.B)]
    [TestCase(800000, PlayRank.B)]
    [TestCase(799999, PlayRank.C)]
    [TestCase(700000, PlayRank.C)]
    [TestCase(699999, PlayRank.D)]
    [TestCase(600000, PlayRank.D)]
    [TestCase(599999, PlayRank.F)]
    [TestCase(0, PlayRank.F)]
    public void Rank_Boundaries(int score, PlayRank expected)
    {
        Assert.AreEqual(expected, ScoreCalculator.GetRank(score));
    }
}
