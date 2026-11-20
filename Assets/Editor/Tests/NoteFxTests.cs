using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 노트 색 · 판정 이펙트 · 키 칸 안내 (11/20, 기획서 3-4 「노트 · 판정 이펙트」) EditMode 확인.
/// 1. 판정 이펙트 R2 타이밍 값 (u = 0 / 0.05 / 0.10 / 0.20 / 0.38)
/// 2. 이펙트를 터뜨릴 판정과 색 판정 (MISS 없음, 틱은 머리 판정, 꼬리는 꼬리 판정)
/// 3. 2박 롱노트 하나의 이펙트 횟수 (틱 7 + 머리 + 꼬리 = 9), 중간에 떼면 틱 이펙트가 멈춤
/// 4. 시작 키 글자 안내 (11/20 수정): 글자 · 마름모 알파 표 (메트로놈 · 중간 시작 · 다시 하기), 둘이 같이 보이는 순간 0
/// 5. JudgeSprites 세트 선택 (기어 Judge / 메뉴 JudgeMenu, 캐시 따로)
/// 6. 노트 광택 · 꼬리 크기 (1080 기준 px)
/// 7. 다시 하기 진입 데이터 (CloneForRetry)
/// </summary>
public class NoteFxTests
{
    private const float Eps = 1e-3f;

    // ───── 1. 이펙트 타이밍 ─────

    [Test]
    public void HitFx_At0_OrbFullAndOthersHidden()
    {
        HitFxFrame f = HitFxRules.Evaluate(0f);
        Assert.AreEqual(50f, f.HaloRadius, Eps);
        Assert.AreEqual(0.43f, f.HaloAlpha, Eps);
        Assert.AreEqual(25f, f.CoreRadius, Eps);
        Assert.AreEqual(1f, f.CoreAlpha, Eps);
        Assert.AreEqual(0f, f.RingAlpha, "원은 0.04초부터");
        Assert.AreEqual(0f, f.DotAlpha, "반짝이는 0.04초부터");
        Assert.AreEqual(0f, f.DiamondOuterAlpha, "마름모는 0.03초부터");
        Assert.AreEqual(256f, f.GlowSize, Eps);
        Assert.AreEqual(0.35f, f.GlowAlpha, Eps);
    }

    [Test]
    public void HitFx_At005()
    {
        HitFxFrame f = HitFxRules.Evaluate(0.05f);
        float orbE = HitFxRules.EaseOutCubic(0.5f); // 0.875
        Assert.AreEqual(0.875f, orbE, 1e-5f);
        Assert.AreEqual(50f - 30f * orbE, f.HaloRadius, Eps, "구슬 바깥 50 → 20 (이징)");
        Assert.AreEqual(0.30f, f.HaloAlpha, Eps, "0.43 → 0.17 반쯤");
        Assert.AreEqual(25f - 15f * orbE, f.CoreRadius, Eps);
        Assert.AreEqual(0.7f, f.CoreAlpha, Eps);

        float ringQ = 0.01f / 0.34f;
        Assert.AreEqual(38f + 24f * HitFxRules.EaseOutCubic(ringQ), f.RingRadius, Eps);
        Assert.AreEqual(0.82f * (1f - ringQ), f.RingAlpha, Eps);
        Assert.AreEqual(0.86f * (1f - ringQ), f.DotAlpha, Eps);

        float diaQ = 0.02f / 0.35f;
        Assert.AreEqual(30f - 13.5f * diaQ, f.DiamondHalfDiagonal, Eps, "반대각 30 → 16.5 (직선)");
        Assert.AreEqual(90f * HitFxRules.EaseOutCubic(diaQ), f.DiamondAngle, Eps);
        Assert.AreEqual(0.59f * (1f - Mathf.Pow(diaQ, 1.4f)), f.DiamondOuterAlpha, Eps);
        Assert.AreEqual(f.DiamondHalfDiagonal * 0.55f, f.DiamondInnerHalfDiagonal, Eps);
        Assert.AreEqual(1f - Mathf.Pow(diaQ, 1.4f), f.DiamondInnerAlpha, Eps);
        LogFrame(0.05f, f);
    }

    [Test]
    public void HitFx_At010_OrbEndValues()
    {
        HitFxFrame f = HitFxRules.Evaluate(0.10f);
        Assert.AreEqual(20f, f.HaloRadius, Eps);
        Assert.AreEqual(0.17f, f.HaloAlpha, Eps);
        Assert.AreEqual(10f, f.CoreRadius, Eps);
        Assert.AreEqual(0.4f, f.CoreAlpha, Eps);
        LogFrame(0.10f, f);

        HitFxFrame after = HitFxRules.Evaluate(0.1001f);
        Assert.AreEqual(0f, after.HaloAlpha, "0.10초 뒤 꺼짐");
        Assert.AreEqual(0f, after.CoreAlpha, "0.10초 뒤 꺼짐");
    }

    [Test]
    public void HitFx_At020()
    {
        HitFxFrame f = HitFxRules.Evaluate(0.20f);
        Assert.AreEqual(0f, f.HaloAlpha);
        float ringQ = 0.16f / 0.34f;
        Assert.AreEqual(38f + 24f * HitFxRules.EaseOutCubic(ringQ), f.RingRadius, Eps);
        Assert.AreEqual(0.82f * (1f - ringQ), f.RingAlpha, Eps);
        float diaQ = 0.17f / 0.35f;
        Assert.AreEqual(30f - 13.5f * diaQ, f.DiamondHalfDiagonal, Eps);
        Assert.AreEqual(90f * HitFxRules.EaseOutCubic(diaQ), f.DiamondAngle, Eps);
        LogFrame(0.20f, f);
    }

    [Test]
    public void HitFx_At038_Gone()
    {
        HitFxFrame end = HitFxRules.Evaluate(0.38f);
        Assert.AreEqual(0f, end.RingAlpha);
        Assert.AreEqual(0f, end.DiamondOuterAlpha);
        Assert.AreEqual(0f, end.DiamondInnerAlpha);
        Assert.AreEqual(0f, end.GlowAlpha);
        Assert.IsFalse(HitFxRules.IsAlive(0.38f));

        // 끝 바로 앞: 원 62 · 마름모 16.5 · 90°에 거의 닿고 알파는 0에 가깝다
        HitFxFrame last = HitFxRules.Evaluate(0.3799f);
        Assert.AreEqual(62f, last.RingRadius, 0.01f);
        Assert.AreEqual(16.5f, last.DiamondHalfDiagonal, 0.01f);
        Assert.AreEqual(90f, last.DiamondAngle, 0.01f);
        Assert.Less(last.RingAlpha, 0.001f);
        Debug.Log($"[NoteFxTest] u=0.38: 원 알파 {end.RingAlpha}, 마름모 알파 {end.DiamondOuterAlpha}/{end.DiamondInnerAlpha}, 번짐 {end.GlowAlpha} (모두 꺼짐). 직전 0.3799: 원 반지름 {last.RingRadius:0.00}, 마름모 반대각 {last.DiamondHalfDiagonal:0.00} · {last.DiamondAngle:0.0}°");
    }

    [Test]
    public void SpriteScale_DisplayPxToWorld()
    {
        // 1080 화면 · 카메라 높이 10유닛 → 1px = 10/1080. Fx_Diamond(128px, PPU 100) 표시 64px → 0.5 × 10/1080 × 100
        Texture2D texture = new Texture2D(128, 128);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
        try
        {
            float px = 10f / 1080f;
            Assert.AreEqual(64f * px / 1.28f, HitFxRules.SpriteScale(64f, sprite, px), 1e-6f);
            Assert.AreEqual(0f, HitFxRules.SpriteScale(64f, null, px));
        }
        finally
        {
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    private static void LogFrame(float u, HitFxFrame f)
    {
        Debug.Log($"[NoteFxTest] u={u:0.00}: 구슬 바깥 r{f.HaloRadius:0.00} a{f.HaloAlpha:0.000} · 안쪽 r{f.CoreRadius:0.00} a{f.CoreAlpha:0.000} · 원 r{f.RingRadius:0.00} a{f.RingAlpha:0.000} · 반짝이 a{f.DotAlpha:0.000} · " +
                  $"마름모 반대각 {f.DiamondHalfDiagonal:0.00} {f.DiamondAngle:0.0}° a{f.DiamondOuterAlpha:0.000} / 안쪽 {f.DiamondInnerHalfDiagonal:0.00} a{f.DiamondInnerAlpha:0.000} · 번짐 {f.GlowSize:0.0}px a{f.GlowAlpha:0.000}");
    }

    // ───── 2. 터뜨릴 판정 ─────

    [Test]
    public void FxJudge_ShortAndLong()
    {
        foreach (JudgeType type in new[] { JudgeType.Perfect, JudgeType.Great, JudgeType.Good })
        {
            Assert.IsTrue(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, type, 0, true), out JudgeType fx), type + " 숏노트");
            Assert.AreEqual(type, fx);
            Assert.IsTrue(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, type, 0, true, JudgeKind.LongStart, type, type), out fx), type + " 머리");
            Assert.AreEqual(type, fx);
        }
        Assert.IsFalse(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Miss, 150, false), out _), "숏노트 MISS는 이펙트 없음");

        // 틱: 머리 판정 색 (Type은 Perfect로 들어온다)
        Assert.IsTrue(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Perfect, 0, false, JudgeKind.Tick, JudgeType.Good, JudgeType.Good), out JudgeType tick));
        Assert.AreEqual(JudgeType.Good, tick, "틱은 머리 판정 색");

        // 꼬리: 꼬리 판정 색, 일찍 떼거나 시작 MISS면 없음
        Assert.IsTrue(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Great, 0, true, JudgeKind.LongFinal, JudgeType.Great, JudgeType.Great), out JudgeType tail));
        Assert.AreEqual(JudgeType.Great, tail);
        Assert.IsTrue(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Good, 200, false, JudgeKind.LongFinal, JudgeType.Perfect, JudgeType.Good), out tail));
        Assert.AreEqual(JudgeType.Good, tail, "늦게 떼 자동 GOOD → GOOD 색");
        Assert.IsFalse(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Miss, -500, true, JudgeKind.LongFinal, JudgeType.Perfect, JudgeType.Miss), out _), "일찍 뗀 꼬리 MISS");
        Assert.IsFalse(HitFxRules.TryGetFxJudge(new JudgeResult(0, 0, JudgeType.Miss, 0, false, JudgeKind.LongFinal, JudgeType.Miss, JudgeType.Miss), out _), "시작 MISS");
    }

    // ───── 3. 롱노트 이펙트 횟수 ─────

    private static readonly TimingMap Slow = TimingMap.Single(60f, 0);

    private static NoteJudge LongJudge(double startBeat, double endBeat)
    {
        PlayNote note = new PlayNote(0, Slow.BeatToSeconds(startBeat), Slow.BeatToSeconds(endBeat), LongNoteTicks.GetTickTimes(Slow, startBeat, endBeat));
        return new NoteJudge(new[] { note }, GameConstants.Editor.LaneCount,
            new JudgeWindows(GameConstants.Play.DefaultPerfectMs, GameConstants.Play.DefaultGreatMs, GameConstants.Play.DefaultGoodMs, GameConstants.Play.DefaultMissMs));
    }

    /// <summary>2박 롱노트를 60fps로 끝까지 누르고 끝에서 뗀다: 틱 7 + 머리 1 + 꼬리 1 = 이펙트 9번</summary>
    [Test]
    public void LongNote_TwoBeats_NineEffects()
    {
        Assert.AreEqual(9, HitFxRules.CountForHeldLongNote(0, 2));
        NoteJudge judge = LongJudge(1, 3);
        List<JudgeResult> results = new List<JudgeResult>();
        int fx = 0, ticks = 0;

        Assert.IsTrue(judge.TryHit(0, 1.0, out JudgeResult head));
        if (HitFxRules.TryGetFxJudge(head, out _)) fx++;
        for (double t = 1.0; t < 3.0; t += 1.0 / 60.0)
        {
            judge.CollectTimeouts(t, results);
            foreach (JudgeResult r in results)
            {
                if (r.Kind == JudgeKind.Tick) ticks++;
                if (HitFxRules.TryGetFxJudge(r, out _)) fx++;
            }
        }
        results.Clear();
        Assert.IsTrue(judge.TryRelease(0, 3.0, results));
        foreach (JudgeResult r in results)
        {
            if (r.Kind == JudgeKind.Tick) ticks++;
            if (HitFxRules.TryGetFxJudge(r, out _)) fx++;
        }
        Assert.AreEqual(7, ticks, "유지 틱 7");
        Assert.AreEqual(9, fx, "틱 7 + 머리 + 꼬리");
        Debug.Log($"[NoteFxTest] 2박 롱노트: 유지 틱 {ticks}, 이펙트 {fx}번 (틱 + 머리 + 꼬리)");
    }

    /// <summary>2박 롱노트를 1박에서 뗀다: 틱 3(0.25 · 0.5 · 0.75박) + 머리 = 4, 일찍 뗀 꼬리 MISS · 남은 틱은 이펙트 없음</summary>
    [Test]
    public void LongNote_ReleasedEarly_TickEffectsStop()
    {
        NoteJudge judge = LongJudge(1, 3);
        List<JudgeResult> results = new List<JudgeResult>();
        int fx = 0;
        Assert.IsTrue(judge.TryHit(0, 1.0, out JudgeResult head));
        if (HitFxRules.TryGetFxJudge(head, out _)) fx++;
        for (double t = 1.0; t < 1.9; t += 1.0 / 60.0)
        {
            judge.CollectTimeouts(t, results);
            foreach (JudgeResult r in results) if (HitFxRules.TryGetFxJudge(r, out _)) fx++;
        }
        results.Clear();
        Assert.IsTrue(judge.TryRelease(0, 1.9, results));
        foreach (JudgeResult r in results) if (HitFxRules.TryGetFxJudge(r, out _)) fx++;
        int afterRelease = 0;
        for (double t = 1.9; t < 4.0; t += 1.0 / 60.0)
        {
            judge.CollectTimeouts(t, results);
            foreach (JudgeResult r in results) if (HitFxRules.TryGetFxJudge(r, out _)) afterRelease++;
        }
        Assert.AreEqual(1 + 3, fx, "머리 + 뗀 시각까지의 틱 3 (꼬리 MISS는 없음)");
        Assert.AreEqual(0, afterRelease, "뗀 뒤로는 이펙트 없음");
        Debug.Log($"[NoteFxTest] 2박 롱노트 1.9초(0.9박)에 뗌: 이펙트 {fx}번, 뗀 뒤 {afterRelease}번");
    }

    // ───── 4. 시작 키 글자 안내 ─────

    /// <summary>120 BPM: −4박 = −2초, −1박 = −0.5초, −0.5박 = −0.25초, 0박 = 0초</summary>
    private static KeyLabelHintRules.BeatTimes Beats120 => KeyLabelHintRules.BeatTimes.From(TimingMap.Single(120f, 0));

    [TestCase(-4.5, 0f, 0f)]
    [TestCase(-4.0, 1f, 0f)]
    [TestCase(-2.0, 1f, 0f)]
    [TestCase(-1.0, 1f, 0f)]
    [TestCase(-0.75, 0.5f, 0f)]
    [TestCase(-0.5, 0f, 0f)]
    [TestCase(-0.25, 0f, 0.5f)]
    [TestCase(0.0, 0f, 1f)]
    [TestCase(1.0, 0f, 1f)]
    public void KeyHint_WithCountIn(double beat, float label, float diamond)
    {
        double t = TimingMap.Single(120f, 0).BeatToSeconds(beat);
        KeyHintAlpha a = KeyLabelHintRules.GetAlpha(true, t, Beats120, 99);
        Assert.AreEqual(label, a.Label, 1e-4f, beat + "박 글자");
        Assert.AreEqual(diamond, a.Diamond, 1e-4f, beat + "박 마름모");
        Assert.IsFalse(a.IsOverlapping, beat + "박 겹침 없음");
    }

    [TestCase(0.0, 1f, 0f)]
    [TestCase(0.5, 1f, 0f)]
    [TestCase(1.0, 1f, 0f)]
    [TestCase(1.125, 0.5f, 0f)]
    [TestCase(1.25, 0f, 0f)]
    [TestCase(1.375, 0f, 0.5f)]
    [TestCase(1.5, 0f, 1f)]
    [TestCase(2.0, 0f, 1f)]
    public void KeyHint_WithoutCountIn(double elapsed, float label, float diamond)
    {
        KeyHintAlpha a = KeyLabelHintRules.GetAlpha(false, 0, default, elapsed);
        Assert.AreEqual(label, a.Label, 1e-4f, elapsed + "초 글자");
        Assert.AreEqual(diamond, a.Diamond, 1e-4f, elapsed + "초 마름모");
    }

    [Test]
    public void KeyHint_Initial()
    {
        Assert.AreEqual(0f, KeyLabelHintRules.Initial(true).Diamond, "메트로놈 판 시작: 둘 다 숨김");
        Assert.AreEqual(0f, KeyLabelHintRules.Initial(true).Label);
        Assert.AreEqual(1f, KeyLabelHintRules.Initial(false).Label, "중간 시작: 글자만 (다시 하기도 같다)");
        Assert.AreEqual(0f, KeyLabelHintRules.Initial(false).Diamond);
    }

    /// <summary>어느 시점에도 글자 &gt; 0 이면서 마름모 &gt; 0인 순간이 없다 (1ms 간격으로 훑음)</summary>
    [Test]
    public void KeyHint_NeverOverlaps()
    {
        KeyLabelHintRules.BeatTimes beats = Beats120;
        int checkedCount = 0;
        for (double t = beats.Show - 1.0; t <= beats.End + 1.0; t += 0.001, checkedCount++)
            Assert.IsFalse(KeyLabelHintRules.WithCountIn(t, beats).IsOverlapping, $"메트로놈 {t:0.000}초");
        for (double e = -0.5; e <= 3.0; e += 0.001, checkedCount++)
            Assert.IsFalse(KeyLabelHintRules.WithoutCountIn(e).IsOverlapping, $"중간 시작 {e:0.000}초");
        Debug.Log($"[NoteFxTest] 키 글자 안내 겹침 검사 {checkedCount}개 시점: 0");
    }

    [Test]
    public void KeyHint_IsOver()
    {
        KeyLabelHintRules.BeatTimes b = Beats120;
        Assert.IsFalse(KeyLabelHintRules.IsOver(true, -0.1, b, 0));
        Assert.IsTrue(KeyLabelHintRules.IsOver(true, 0, b, 0));
        Assert.IsFalse(KeyLabelHintRules.IsOver(false, 0, b, 1.49));
        Assert.IsTrue(KeyLabelHintRules.IsOver(false, 0, b, 1.5));
    }

    // ───── 5. JudgeSprites 세트 ─────

    [Test]
    public void JudgeSprites_SetsAreSeparate()
    {
        Assert.AreEqual("Images/InGame/Judge/Judge_Miss", JudgeSprites.GetPath("Miss", JudgeSpriteSet.Gear));
        Assert.AreEqual("Images/InGame/JudgeMenu/Judge_Miss", JudgeSprites.GetPath("Miss", JudgeSpriteSet.Menu));
        Assert.AreSame(JudgeSprites.Get(JudgeType.Miss), JudgeSprites.Get(JudgeType.Miss, JudgeSpriteSet.Gear), "기본은 기어 세트");

        foreach (JudgeType judge in System.Enum.GetValues(typeof(JudgeType)))
        {
            Sprite gear = JudgeSprites.Get(judge, JudgeSpriteSet.Gear);
            Sprite menu = JudgeSprites.Get(judge, JudgeSpriteSet.Menu);
            Assert.IsNotNull(menu, judge + " 메뉴 이미지");
            Assert.AreNotSame(gear, menu, judge + " 세트마다 다른 이미지");
            Assert.AreSame(menu, JudgeSprites.Get(judge, JudgeSpriteSet.Menu), judge + " 메뉴 캐시");
            Assert.AreEqual(gear.rect.size, menu.rect.size, judge + " 크기는 같다");
        }
        foreach (FastSlow dir in new[] { FastSlow.Fast, FastSlow.Slow })
        {
            Sprite menu = JudgeSprites.Get(dir, JudgeSpriteSet.Menu);
            Assert.IsNotNull(menu, dir + " 메뉴 이미지");
            Assert.AreNotSame(JudgeSprites.Get(dir), menu);
        }
        Assert.IsNull(JudgeSprites.Get(FastSlow.None, JudgeSpriteSet.Menu));
    }

    // ───── 6. 노트 모양 ─────

    [Test]
    public void NoteShape_Shine()
    {
        float px = 10f / 1080f;
        Vector2 note = new Vector2(0.94f, 0.22f);
        Vector2 shine = NoteShape.ShineSize(note, px);
        Assert.AreEqual(0.94f - 6f * px, shine.x, 1e-6f, "폭 = 노트 폭 − 6px");
        Assert.AreEqual(4f * px, shine.y, 1e-6f, "두께 4px");
        float shineTop = NoteShape.ShineLocalY(note, px) + shine.y * 0.5f;
        Assert.AreEqual(0.11f - 2f * px, shineTop, 1e-6f, "광택 위 끝 = 노트 위 끝 − 2px");
    }

    /// <summary>11/20 정정: 몸통 폭 76px · 가운데 선 2px, 세로 = 머리 가운데 ~ 꼬리 (꼬리 따로 없음)</summary>
    [Test]
    public void LongNote_BodyWidthAndRange()
    {
        float px = 10f / 1080f;
        Assert.AreEqual(76f * px, NoteShape.LongNoteBodyWidth(px), 1e-6f, "몸통 폭 76px (x+16 ~ x+92)");
        Assert.AreEqual(2f * px, NoteShape.CenterLineWidth(px), 1e-6f, "가운데 선 2px (x+53 ~ x+55)");
        // 라인 폭 108: 몸통 왼쪽 = 가운데 54 − 38 = 16, 오른쪽 = 92 / 선 53 ~ 55
        Assert.AreEqual(16f, 54f - GameConstants.Play.LongNoteBodyWidthPx * 0.5f, 1e-6f);
        Assert.AreEqual(53f, 54f - GameConstants.Play.LongNoteCenterLineWidthPx * 0.5f, 1e-6f);

        NoteShape.LongNoteBodyRange(-3f, 1f, out float bottom, out float top);
        Assert.AreEqual(-3f, bottom, "아래 끝 = 머리 가운데");
        Assert.AreEqual(1f, top, "위 끝 = 꼬리 y");
        NoteShape.LongNoteBodyRange(-3f, -3.5f, out bottom, out top);
        Assert.AreEqual(0f, top - bottom, "꼬리가 머리보다 아래면 높이 0");
    }

    /// <summary>누르는 동안 머리 고정 · 위 끝만 내려옴, 판정 전 · 시작 MISS는 시간대로 지나감</summary>
    [Test]
    public void LongNote_HeldHeadFixed_MissedPassesThrough()
    {
        const float judgeLine = -3.33f;
        float head = -4f, tail = 0.5f;
        NoteShape.ResolveLongNoteY(LongNoteState.Held, judgeLine, ref head, ref tail);
        Assert.AreEqual(judgeLine, head, "누르는 동안 머리는 판정선");
        Assert.AreEqual(0.5f, tail, "꼬리는 그대로 내려옴");

        head = -2f; tail = -3.6f;
        NoteShape.ResolveLongNoteY(LongNoteState.Held, judgeLine, ref head, ref tail);
        Assert.AreEqual(judgeLine, tail, "늦게 떼는 중: 꼬리는 판정선 아래로 안 내려감");

        foreach (LongNoteState state in new[] { LongNoteState.Waiting, LongNoteState.Missed })
        {
            head = -4f; tail = -3.8f;
            NoteShape.ResolveLongNoteY(state, judgeLine, ref head, ref tail);
            Assert.AreEqual(-4f, head, state + " 머리는 판정선에 고정하지 않음");
            Assert.AreEqual(-3.8f, tail, state + " 꼬리 그대로");
        }
    }

    // ───── 7. 다시 하기 진입 데이터 ─────

    [Test]
    public void EntryData_CloneForRetry()
    {
        InGameEntryData entry = new InGameEntryData { songId = "s", chartId = "c" };
        Assert.IsFalse(entry.isRetry, "새 진입은 false");
        InGameEntryData retry = entry.CloneForRetry();
        Assert.IsTrue(retry.isRetry);
        Assert.IsFalse(entry.isRetry, "원본은 그대로");
        Assert.IsTrue(retry.Clone().isRetry, "복사본도 유지 (결과 → 다시 하기)");
        Assert.AreEqual("c", retry.chartId);
    }
}
