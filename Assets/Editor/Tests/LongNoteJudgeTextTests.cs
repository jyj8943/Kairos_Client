using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// 롱노트를 누르고 있는 동안의 판정 글자 (DJMAX RESPECT V·PLATiNA :: LAB 방식) EditMode 테스트.
/// - 유지 틱 결과의 StartType = 그 롱노트의 시작 판정 → 판정 글자 등급(JudgeTextType)
/// - 판정 글자는 모든 결과가 다시 재생, 판정 패널은 숏노트·롱노트 종합만 (CountsAsJudgement)
/// - 틱의 StartType이 바뀌어도 점수·콤보·판정 개수·FAST/SLOW·정확도·롱노트 끝 통계는 그대로
/// 60 BPM (1박 = 1초, 틱 0.25초 간격)으로 실제 NoteJudge 결과를 만든다.
/// </summary>
public class LongNoteJudgeTextTests
{
    private static readonly TimingMap Timing = TimingMap.Single(60f, 0);

    private static readonly JudgeWindows Windows = new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs,
        GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs);

    /// <summary>각 판정 범위 안쪽 오차 (ms)</summary>
    private static double PerfectMs => Windows.PerfectMs * 0.5;
    private static double GreatMs => (Windows.PerfectMs + Windows.GreatMs) * 0.5;
    private static double GoodMs => (Windows.GreatMs + Windows.GoodMs) * 0.5;

    private static PlayNote Short(int lane, double beat) => new PlayNote(lane, Timing.BeatToSeconds(beat));

    private static PlayNote Long(int lane, double startBeat, double endBeat) =>
        new PlayNote(lane, Timing.BeatToSeconds(startBeat), Timing.BeatToSeconds(endBeat), LongNoteTicks.GetTickTimes(Timing, startBeat, endBeat));

    private static NoteJudge NewJudge(params PlayNote[] notes) => new NoteJudge(new List<PlayNote>(notes), GameConstants.Editor.LaneCount, Windows);

    /// <summary>롱노트 하나를 startDeltaMs로 누르고, 끝까지 프레임(1/60초)마다 시간을 흘린 뒤 끝 + releaseDeltaMs에 뗀다. 모든 결과를 순서대로 돌려준다</summary>
    private static List<JudgeResult> PlayLong(NoteJudge judge, double startDeltaMs, double releaseDeltaMs)
    {
        List<JudgeResult> all = new List<JudgeResult>();
        List<JudgeResult> buffer = new List<JudgeResult>();
        PlayNote note = judge.Notes[0];
        double pressTime = note.Time + startDeltaMs / 1000.0;
        Assert.IsTrue(judge.TryHit(note.Lane, pressTime, out JudgeResult start));
        all.Add(start);

        double releaseTime = note.EndTime + releaseDeltaMs / 1000.0;
        for (double t = pressTime + 1.0 / 60.0; t < releaseTime; t += 1.0 / 60.0)
        {
            judge.CollectTimeouts(t, buffer);
            all.AddRange(buffer);
        }
        buffer.Clear();
        if (judge.TryRelease(note.Lane, releaseTime, buffer)) all.AddRange(buffer);
        return all;
    }

    /// <summary>1. GREAT로 시작한 롱노트: 모든 Tick의 StartType·판정 글자 등급 = GREAT (Type은 Perfect 그대로)</summary>
    [Test]
    public void Ticks_GreatStart()
    {
        List<JudgeResult> results = PlayLong(NewJudge(Long(0, 1, 3)), -GreatMs, 0);
        Assert.AreEqual(JudgeType.Great, results[0].Type, "시작 GREAT");
        List<JudgeResult> ticks = results.FindAll(r => r.Kind == JudgeKind.Tick);
        Assert.AreEqual(7, ticks.Count, "1→3박 틱 7개");
        foreach (JudgeResult tick in ticks)
        {
            Assert.AreEqual(JudgeType.Great, tick.StartType);
            Assert.AreEqual(JudgeType.Great, tick.JudgeTextType, "판정 글자는 시작 등급");
            Assert.AreEqual(JudgeType.Perfect, tick.Type, "Type은 그대로 (점수·집계에 쓰지 않음)");
        }
    }

    /// <summary>2. PERFECT로 시작한 롱노트: Tick StartType = PERFECT. GOOD 시작도 같은 규칙</summary>
    [TestCase(-1.0, JudgeType.Perfect)]
    [TestCase(1.0, JudgeType.Good)]
    public void Ticks_StartTypeFollowsStart(double sign, JudgeType expected)
    {
        double delta = expected == JudgeType.Perfect ? PerfectMs * sign : GoodMs * sign;
        List<JudgeResult> results = PlayLong(NewJudge(Long(0, 1, 3)), delta, 0);
        Assert.AreEqual(expected, results[0].Type);
        foreach (JudgeResult tick in results.FindAll(r => r.Kind == JudgeKind.Tick))
            Assert.AreEqual(expected, tick.StartType);
    }

    /// <summary>3. 늦게 눌러 이미 지난 틱(첫 틱 0.25초 뒤보다 늦게 눌러도 GOOD 범위 안)도 StartType이 시작 등급</summary>
    [Test]
    public void Ticks_PastTicksOnLatePress()
    {
        // 1/8박 간격 틱이 없으므로, 틱 0.25초 간격보다 짧은 롱노트 대신 BPM을 올려 첫 틱이 GOOD 범위 안에 오게 한다 (480 BPM: 1/4박 = 0.03125초)
        TimingMap fast = TimingMap.Single(480f, 0);
        PlayNote note = new PlayNote(0, fast.BeatToSeconds(4), fast.BeatToSeconds(8), LongNoteTicks.GetTickTimes(fast, 4, 8));
        NoteJudge judge = NewJudge(note);
        double press = note.Time + GoodMs / 1000.0; // 0.1초 늦게: 틱 3개(0.031·0.063·0.094초)가 이미 지남
        Assert.IsTrue(judge.TryHit(0, press, out JudgeResult start));
        Assert.AreEqual(JudgeType.Good, start.Type);

        List<JudgeResult> buffer = new List<JudgeResult>();
        judge.CollectTimeouts(press, buffer);
        List<JudgeResult> ticks = buffer.FindAll(r => r.Kind == JudgeKind.Tick);
        Assert.GreaterOrEqual(ticks.Count, 3, "누른 순간 이미 지난 틱을 한꺼번에 인정");
        foreach (JudgeResult tick in ticks)
            Assert.AreEqual(JudgeType.Good, tick.StartType);
    }

    /// <summary>
    /// 4. 같은 입력에서 틱 StartType을 예전 값(Perfect)으로 바꿔 넣어도 점수·콤보·최대 콤보·판정 개수·FAST/SLOW·LiveAccuracy·
    /// 롱노트 끝 통계가 같다 (틱 등급은 화면에만 쓰인다)
    /// </summary>
    [Test]
    public void Scoring_UnchangedByTickStartType()
    {
        NoteJudge judge = NewJudge(Long(0, 1, 3), Short(1, 1.5), Long(2, 4, 6), Short(3, 5));
        List<JudgeResult> all = new List<JudgeResult>();
        List<JudgeResult> buffer = new List<JudgeResult>();

        Assert.IsTrue(judge.TryHit(0, 1 - GreatMs / 1000.0, out JudgeResult r)); all.Add(r);           // 롱 GREAT FAST
        Assert.IsTrue(judge.TryHit(1, 1.5 + GoodMs / 1000.0, out r)); all.Add(r);                      // 숏 GOOD SLOW
        for (double t = 1.0; t < 3.05; t += 1.0 / 60.0) { judge.CollectTimeouts(t, buffer); all.AddRange(buffer); }
        buffer.Clear(); Assert.IsTrue(judge.TryRelease(0, 3.05, buffer)); all.AddRange(buffer);       // 롱 끝 늦게 → GREAT SLOW
        Assert.IsTrue(judge.TryHit(2, 4 + PerfectMs / 1000.0, out r)); all.Add(r);                     // 롱 PERFECT
        Assert.IsTrue(judge.TryHit(3, 5 + GreatMs / 1000.0, out r)); all.Add(r);                       // 숏 GREAT SLOW
        for (double t = 4.0; t < 6.2; t += 1.0 / 60.0) { judge.CollectTimeouts(t, buffer); all.AddRange(buffer); } // 롱 자동 GOOD

        ScoreCalculator now = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
        ScoreCalculator before = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
        LongReleaseStats statsNow = new LongReleaseStats(GameConstants.Play.LongReleaseMs, Windows.PerfectMs);
        LongReleaseStats statsBefore = new LongReleaseStats(GameConstants.Play.LongReleaseMs, Windows.PerfectMs);
        int tickCount = 0;
        foreach (JudgeResult res in all)
        {
            now.Apply(res);
            statsNow.Add(res);
            JudgeResult old = res.Kind == JudgeKind.Tick
                ? new JudgeResult(res.NoteIndex, res.Lane, JudgeType.Perfect, res.DeltaMs, res.IsHit, JudgeKind.Tick, JudgeType.Perfect, JudgeType.Perfect)
                : res;
            if (res.Kind == JudgeKind.Tick) tickCount++;
            before.Apply(old);
            statsBefore.Add(old);
        }

        Assert.Greater(tickCount, 0);
        Assert.AreEqual(judge.TotalCount, now.JudgedCount, "전부 판정");
        Assert.AreEqual(before.Score, now.Score);
        Assert.AreEqual(before.Combo, now.Combo);
        Assert.AreEqual(before.MaxCombo, now.MaxCombo);
        Assert.AreEqual(now.MaxPossibleCombo, now.MaxCombo, "틱 콤보 전부 인정");
        Assert.AreEqual(before.PerfectCount, now.PerfectCount);
        Assert.AreEqual(before.GreatCount, now.GreatCount);
        Assert.AreEqual(before.GoodCount, now.GoodCount);
        Assert.AreEqual(before.MissCount, now.MissCount);
        Assert.AreEqual(before.FastCount, now.FastCount);
        Assert.AreEqual(before.SlowCount, now.SlowCount);
        Assert.AreEqual(before.LiveAccuracy, now.LiveAccuracy, 1e-12);
        Assert.AreEqual(before.Accuracy, now.Accuracy, 1e-12);
        Assert.AreEqual(statsBefore.TotalCount, statsNow.TotalCount, "롱노트 끝 통계");
        Assert.AreEqual(2, statsNow.TotalCount, "롱노트 2개만 셈 (틱은 세지 않음)");
        Assert.AreEqual(statsBefore.LateCount, statsNow.LateCount);
        Assert.AreEqual(statsBefore.AutoGoodCount, statsNow.AutoGoodCount);
        Assert.AreEqual(statsBefore.AverageDeltaMs, statsNow.AverageDeltaMs, 1e-12);
        Assert.AreEqual(now.GreatCount + now.GoodCount, now.FastCount + now.SlowCount);
        Debug.Log($"[LongNoteJudgeTextTest] 점수 {now.Score}, 콤보 {now.MaxCombo}/{now.MaxPossibleCombo} (틱 {tickCount}), P/G/Go/M {now.PerfectCount}/{now.GreatCount}/{now.GoodCount}/{now.MissCount}, FAST/SLOW {now.FastCount}/{now.SlowCount}");
    }

    /// <summary>5. 판정 글자는 모든 결과(숏노트·롱노트 시작·틱·종합)가 띄우고, 판정 패널은 숏노트·종합만. 시작 MISS는 종합 MISS 하나</summary>
    [Test]
    public void JudgeText_AndPanelRules()
    {
        NoteJudge judge = NewJudge(Short(0, 1), Long(1, 1, 3), Long(2, 1, 3));
        Assert.IsTrue(judge.TryHit(0, 1, out JudgeResult shortHit));
        Assert.IsTrue(shortHit.CountsAsJudgement);
        Assert.AreEqual(JudgeType.Perfect, shortHit.JudgeTextType);

        Assert.IsTrue(judge.TryHit(1, 1 + GoodMs / 1000.0, out JudgeResult start));
        Assert.AreEqual(JudgeKind.LongStart, start.Kind);
        Assert.IsFalse(start.CountsAsJudgement, "롱노트 시작은 판정 패널을 바꾸지 않음");
        Assert.AreEqual(JudgeType.Good, start.JudgeTextType, "누르는 순간 시작 등급");

        // 시작 MISS(lane 2를 놓침)는 LongStart 없이 종합 MISS 하나
        List<JudgeResult> buffer = new List<JudgeResult>();
        judge.CollectTimeouts(1.5, buffer);
        List<JudgeResult> missFinal = buffer.FindAll(r => r.Lane == 2);
        Assert.AreEqual(1, missFinal.Count);
        Assert.AreEqual(JudgeKind.LongFinal, missFinal[0].Kind);
        Assert.AreEqual(JudgeType.Miss, missFinal[0].JudgeTextType);
        Assert.IsTrue(missFinal[0].CountsAsJudgement);

        JudgeResult tick = buffer.Find(r => r.Kind == JudgeKind.Tick && r.Lane == 1);
        Assert.AreEqual(JudgeKind.Tick, tick.Kind);
        Assert.IsFalse(tick.CountsAsJudgement, "틱은 판정 패널을 바꾸지 않음");
        Assert.AreEqual(JudgeType.Good, tick.JudgeTextType);

        buffer.Clear();
        Assert.IsTrue(judge.TryRelease(1, 3 - (GameConstants.Play.LongReleaseMs + 30) / 1000.0, buffer));
        JudgeResult final = buffer[buffer.Count - 1];
        Assert.AreEqual(JudgeKind.LongFinal, final.Kind);
        Assert.IsTrue(final.CountsAsJudgement);
        Assert.AreEqual(JudgeType.Miss, final.JudgeTextType, "일찍 떼면 MISS");
    }

    /// <summary>6. 틱은 FAST/SLOW를 바꾸지 않음: GetDisplay(틱) = None, 화면에 None을 넘기면 떠 있던 FAST/SLOW는 남은 시간 그대로</summary>
    [Test]
    public void Tick_DoesNotTouchFastSlow()
    {
        NoteJudge judge = NewJudge(Long(0, 1, 3));
        List<JudgeResult> results = PlayLong(judge, -GreatMs, 0);
        Assert.AreEqual(FastSlow.Fast, FastSlowRules.GetDisplay(results[0]), "시작 FAST");
        foreach (JudgeResult tick in results.FindAll(r => r.Kind == JudgeKind.Tick))
        {
            Assert.AreEqual(FastSlow.None, FastSlowRules.GetDisplay(tick));
            Assert.AreEqual(FastSlow.None, FastSlowRules.GetCount(tick));
        }

        // 화면: 시작 FAST를 띄우고 0.2초 뒤 틱(판정 글자만 다시, FAST/SLOW는 None) → FAST 시간 그대로
        GameObject root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            GameObject f = new GameObject("Txt_FastSlow", typeof(RectTransform), typeof(TextMeshProUGUI));
            f.transform.SetParent(root.transform, false);
            Txt_FastSlow fastSlow = f.AddComponent<Txt_FastSlow>();
            GameObject j = new GameObject("Txt_Judge", typeof(RectTransform), typeof(TextMeshProUGUI));
            j.transform.SetParent(root.transform, false);
            Txt_Judge judgeText = j.AddComponent<Txt_Judge>();
            fastSlow.ResetView();
            judgeText.ResetView();

            fastSlow.Show(GameConstants.Play.FastText, Color.cyan);
            judgeText.Show(GameConstants.Play.GreatText, Color.blue);
            fastSlow.Tick(0.2f);
            judgeText.Tick(0.2f);

            judgeText.Show(GameConstants.Play.GreatText, Color.blue); // 틱: 판정 글자만 처음부터
            Assert.AreEqual(0f, judgeText.Animation.Elapsed, 1e-6f);
            Assert.AreEqual(0.2f, fastSlow.Animation.Elapsed, 1e-6f, "FAST 시간 그대로");
            Assert.IsTrue(fastSlow.IsVisible);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
