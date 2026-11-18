using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 랭크 · FC 규칙 EditMode 확인 (기획서 2-1 5) 랭크 표 · 2-4 저장 데이터 · 3-3 「랭크·FC 표시 (11/18 개정)」).
/// 1. 랭크: 전부 PERFECT → AP, 999999·950000 → S, 949999·900000 → A, 899999 → B, PERFECT 아닌 판정이 하나라도 있으면 AP 아님, 노트 0개
/// 2. 점수 내림: 소수가 .5 이상이어도 올리지 않는다 (999999.6 → 999999, 957142.857 → 957142)
/// 3. FC: MISS 0 → true, AP → FC도 true
/// 4. 기록: bestRank "AP" 저장 → 불러오기, 예전 JSON(S~F) 그대로 읽기, 모르는 값 → F, 예전 ALL PERFECT 기록(1000000 · "S") → AP
/// 5. RankSprites: 모든 PlayRank 값에 스프라이트, FC 스프라이트 (S~F 정사각형, AP·FC 가로로 김)
/// </summary>
public class RankRulesTests
{
    // ───── 1. 랭크 ─────

    [Test]
    public void Rank_ScoreBoundaries_WhenNotAllPerfect()
    {
        // PERFECT 9 / 판정 10 (AP 아님): 점수만으로 정한다
        Assert.AreEqual(PlayRank.S, RankRules.GetRank(9, 10, 999999), "999999 → S");
        Assert.AreEqual(PlayRank.S, RankRules.GetRank(9, 10, 950000), "950000 → S");
        Assert.AreEqual(PlayRank.A, RankRules.GetRank(9, 10, 949999), "949999 → A");
        Assert.AreEqual(PlayRank.A, RankRules.GetRank(9, 10, 900000), "900000 → A");
        Assert.AreEqual(PlayRank.B, RankRules.GetRank(9, 10, 899999), "899999 → B");
        Assert.AreEqual(PlayRank.B, RankRules.GetRank(0, 10, 800000));
        Assert.AreEqual(PlayRank.C, RankRules.GetRank(0, 10, 799999));
        Assert.AreEqual(PlayRank.C, RankRules.GetRank(0, 10, 700000));
        Assert.AreEqual(PlayRank.D, RankRules.GetRank(0, 10, 699999));
        Assert.AreEqual(PlayRank.D, RankRules.GetRank(0, 10, 600000));
        Assert.AreEqual(PlayRank.F, RankRules.GetRank(0, 10, 599999));
        Assert.AreEqual(PlayRank.F, RankRules.GetRank(0, 10, 0));
    }

    [Test]
    public void Rank_AllPerfect_IsAP_ByJudgeCountNotScore()
    {
        Assert.AreEqual(PlayRank.AP, RankRules.GetRank(10, 10, 1000000), "전부 PERFECT (1000000) → AP");
        Assert.AreEqual(PlayRank.S, RankRules.GetRank(9, 10, 1000000), "PERFECT 아닌 판정이 있으면 점수가 1000000이어도 AP 아님");
        Assert.AreEqual(PlayRank.F, RankRules.GetRank(0, 0, 0), "노트 0개는 AP 아님");
        Assert.IsFalse(RankRules.IsAllPerfect(0, 0));

        ScoreCalculator all = new ScoreCalculator(5);
        for (int i = 0; i < 5; i++) all.Add(JudgeType.Perfect);
        Assert.AreEqual(1000000, all.Score);
        Assert.AreEqual(PlayRank.AP, all.Rank);

        ScoreCalculator oneGreat = new ScoreCalculator(5);
        for (int i = 0; i < 4; i++) oneGreat.Add(JudgeType.Perfect);
        oneGreat.Add(JudgeType.Great);
        Assert.AreNotEqual(PlayRank.AP, oneGreat.Rank, "GREAT 하나면 AP 아님");

        ScoreCalculator unfinished = new ScoreCalculator(5);
        for (int i = 0; i < 4; i++) unfinished.Add(JudgeType.Perfect);
        Assert.AreNotEqual(PlayRank.AP, unfinished.Rank, "판정하지 않은 노트가 남으면 AP 아님");
        Assert.AreEqual(PlayRank.F, new ScoreCalculator(0).Rank, "노트 0개");
    }

    // ───── 2. 점수 내림 ─────

    [Test]
    public void Score_IsFloored_NotRounded()
    {
        // 1,000,000 × (100·749999 + 70) ÷ (750000 × 100) = 999,999.6 → 반올림이면 1000000, 내림이면 999999
        const int notes = 750000;
        ScoreCalculator score = new ScoreCalculator(notes);
        for (int i = 0; i < notes - 1; i++) score.Add(JudgeType.Perfect);
        score.Add(JudgeType.Great);
        Assert.AreEqual(999999, score.Score, "999999.6 → 999999");
        Assert.AreEqual(PlayRank.S, score.Rank, "AP처럼 보이지 않는다");
        Assert.IsTrue(score.IsFullCombo);
        Assert.IsFalse(score.IsAllPerfect);

        // 7개 중 GOOD 1개 = 900,000 → 정확히 나누어떨어짐 / GREAT 1개 = 957,142.857… → 957,142
        ScoreCalculator seven = new ScoreCalculator(7);
        for (int i = 0; i < 6; i++) seven.Add(JudgeType.Perfect);
        seven.Add(JudgeType.Great);
        Assert.AreEqual(957142, seven.Score);
    }

    // ───── 3. FC ─────

    [Test]
    public void FullCombo_NoMiss_AndAllPerfectIsAlsoFullCombo()
    {
        ScoreCalculator noMiss = new ScoreCalculator(3);
        noMiss.Add(JudgeType.Perfect);
        noMiss.Add(JudgeType.Good);
        noMiss.Add(JudgeType.Great);
        Assert.IsTrue(noMiss.IsFullCombo, "MISS 0 → FC");
        Assert.IsFalse(noMiss.IsAllPerfect);

        ScoreCalculator ap = new ScoreCalculator(3);
        for (int i = 0; i < 3; i++) ap.Add(JudgeType.Perfect);
        Assert.IsTrue(ap.IsAllPerfect);
        Assert.IsTrue(ap.IsFullCombo, "AP면 FC도 true");

        ScoreCalculator miss = new ScoreCalculator(3);
        miss.Add(JudgeType.Perfect);
        miss.Add(JudgeType.Miss);
        miss.Add(JudgeType.Perfect);
        Assert.IsFalse(miss.IsFullCombo, "MISS 1 → FC 아님");

        // 곡 끝 연출: AP면 ALL PERFECT만, 아니고 FC면 FULL COMBO
        Assert.AreEqual(ClearKind.AllPerfect, ClearEffectRules.Select(false, ap.IsAllPerfect, ap.IsFullCombo));
        Assert.AreEqual(ClearKind.FullCombo, ClearEffectRules.Select(false, noMiss.IsAllPerfect, noMiss.IsFullCombo));
        Assert.AreEqual(ClearKind.None, ClearEffectRules.Select(false, miss.IsAllPerfect, miss.IsFullCombo));
    }

    // ───── 4. 기록 ─────

    [Test]
    public void Record_ApRank_SaveAndLoad()
    {
        PlayerRecordBook book = new PlayerRecordBook(new List<ChartRecordData>());
        InGameEntryData entry = new InGameEntryData { songId = "s", chartId = "c", chartType = ChartType.Official, mode = InGameMode.Normal };
        ResultData ap = new ResultData { score = 1000000, rank = PlayRank.AP, maxCombo = 10, accuracy = 100, isFullCombo = true, isAllPerfect = true };
        book.Submit(entry, ap, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc));
        Assert.AreEqual("AP", book.Find("c").bestRank, "기록에 이름 문자열 \"AP\"로 저장");

        string folder = Path.Combine(Path.GetTempPath(), "KairosRankRulesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, GameConstants.PlayerData.FileName);
            PlayerDataStore store = new PlayerDataStore(path);
            PlayerData data = PlayerDataRules.Sanitize(new PlayerData());
            data.records.Add(book.Find("c"));
            Assert.IsTrue(store.Save(data), store.LastError);
            StringAssert.Contains("\"bestRank\": \"AP\"", File.ReadAllText(path));

            Assert.AreEqual(PlayerDataStore.LoadSource.Main, store.Load(out PlayerData loaded));
            ChartRecordData record = PlayerDataRules.Sanitize(loaded).records[0];
            Assert.AreEqual("AP", record.bestRank);
            Assert.AreEqual(PlayRank.AP, PlayerDataRules.ParseRank(record.bestRank));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Record_OldRankNames_UnknownIsF_OldAllPerfectBecomesAP()
    {
        foreach (string name in new[] { "S", "A", "B", "C", "D", "F" })
        {
            ChartRecordData old = new ChartRecordData { chartId = "c", bestScore = 700000, bestRank = name };
            PlayerDataRules.SanitizeRecord(old);
            Assert.AreEqual(name, old.bestRank, $"예전 기록 {name} 그대로");
        }

        ChartRecordData unknown = new ChartRecordData { chartId = "c", bestRank = "SSS" };
        PlayerDataRules.SanitizeRecord(unknown);
        Assert.AreEqual("F", unknown.bestRank, "모르는 값 → F");
        Assert.AreEqual(PlayRank.F, PlayerDataRules.ParseRank("0"), "숫자 문자열은 받지 않는다 (enum 순서가 바뀌어도 안전)");

        // AP 랭크가 생기기 전 ALL PERFECT 기록 (반올림 시절, 1000000점 · "S") → AP
        ChartRecordData oldAp = new ChartRecordData { chartId = "c", bestScore = 1000000, bestRank = "S", isFullCombo = true, isAllPerfect = true };
        PlayerDataRules.SanitizeRecord(oldAp);
        Assert.AreEqual("AP", oldAp.bestRank);

        // ALL PERFECT 기록이 있어도 최고 점수가 1000000이 아니면 (값이 어긋난 기록) 그대로
        ChartRecordData odd = new ChartRecordData { chartId = "c", bestScore = 900000, bestRank = "A", isAllPerfect = true };
        PlayerDataRules.SanitizeRecord(odd);
        Assert.AreEqual("A", odd.bestRank);
    }

    // ───── 5. 이미지 ─────

    [Test]
    public void RankSprites_EveryRankAndFullCombo()
    {
        foreach (PlayRank rank in (PlayRank[])Enum.GetValues(typeof(PlayRank)))
        {
            Sprite sprite = RankSprites.Get(rank);
            Assert.IsNotNull(sprite, $"{rank} 스프라이트");
            float aspect = RankSprites.AspectOf(sprite);
            if (rank == PlayRank.AP) Assert.Greater(aspect, 1.4f, "AP는 가로로 김");
            else Assert.AreEqual(1f, aspect, 1e-3f, $"{rank}는 정사각형");
        }

        Sprite fc = RankSprites.FullCombo;
        Assert.IsNotNull(fc, "FC 스프라이트");
        Assert.Greater(RankSprites.AspectOf(fc), 1.4f, "FC는 가로로 김");

        // 크기 맞추기: 높이 62 → S 62×62, AP는 94 칸에 맞게 줄임
        Vector2 s = Img_Rank.FitSize(1f, 62f, 94f);
        Assert.AreEqual(62f, s.x, 1e-3f);
        Assert.AreEqual(62f, s.y, 1e-3f);
        Vector2 ap = Img_Rank.FitSize(737f / 477f, 62f, 94f);
        Assert.AreEqual(94f, ap.x, 1e-3f, "칸 폭을 넘지 않는다");
        Assert.Less(ap.y, 62f);
        Vector2 result = Img_Rank.FitSize(737f / 477f, 150f, 240f);
        Assert.AreEqual(150f, result.y, 1e-3f, "결과 AP는 높이 150 그대로");
    }
}
