using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 결과 등장 연출 시간표 (ResultIntroTimeline, 기획서 3-5 · README 2절) · 「테스트 후 결과 보기」 저장 EditMode 확인.
/// </summary>
public class ResultIntroTests
{
    private const float Eps = 1e-4f;

    private static float E(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    [Test]
    public void Stages_MatchReadmeTable()
    {
        float[] expected = { 0f, 0.06f, 0.14f, 0.22f, 0.45f, 0.50f, 1.25f, 1.25f, 1.45f, 1.57f, 1.69f, 1.81f, 1.95f, 2.30f, 2.70f, 2.80f, 3.20f, 3.30f, 3.45f, 3.90f };
        Assert.AreEqual(expected.Length, ResultIntroTimeline.Stages.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], ResultIntroTimeline.Stages[i].Start, Eps, ResultIntroTimeline.Stages[i].Name);
        Assert.AreEqual(3.9f, GameConstants.ResultIntro.EndSeconds, Eps);
    }

    [Test]
    public void At0_AllValuesHidden()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(0f);
        Assert.AreEqual(0f, f.Album.Alpha);
        Assert.AreEqual(0.85f, f.Album.Scale, Eps);
        Assert.AreEqual(0f, f.Title.Alpha);
        Assert.AreEqual(-30f, f.Title.Offset.x, Eps);
        Assert.AreEqual(0f, f.Score.Alpha);
        Assert.AreEqual(-16f, f.Score.Offset.y, Eps);
        Assert.AreEqual(0f, f.Judge0.Alpha);
        Assert.AreEqual(0f, f.FastSlow.Alpha);
        Assert.AreEqual(0f, f.Rate.Alpha);
        Assert.AreEqual(0f, f.Combo.Alpha);
        Assert.AreEqual(0f, f.NewScore.Alpha);
        Assert.AreEqual(0f, f.FullCombo.Alpha);
        Assert.AreEqual(0f, f.Rank.Alpha);
        Assert.AreEqual(0f, f.ScoreCount);
        Assert.IsFalse(f.IsFinished);
    }

    [Test]
    public void At03_SongInfoAndScoreStart()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(0.3f);
        Assert.AreEqual(1f, f.Album.Alpha, Eps, "앨범 0.25초 끝");
        Assert.AreEqual(E(0.24f / 0.25f), f.Title.Alpha, Eps);
        Assert.AreEqual(-30f * (1f - E(0.24f / 0.25f)), f.Title.Offset.x, Eps);
        Assert.AreEqual(E(0.08f / 0.25f), f.ChartLine.Alpha, Eps);
        Assert.AreEqual(0f, f.Score.Alpha, "SCORE는 0.45초부터");
        Log(0.3f, f);
    }

    [Test]
    public void At10_ScoreCounting()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(1.0f);
        Assert.AreEqual(1f, f.Score.Alpha, Eps);
        Assert.AreEqual(0f, f.Score.Offset.y, Eps);
        Assert.AreEqual(E(0.5f / 0.75f), f.ScoreCount, Eps, "점수 숫자 0.50 ~ 1.25");
        Assert.AreEqual(ResultIntroTimeline.CountInt(945936, f.ScoreCount), Mathf.RoundToInt(945936 * E(0.5f / 0.75f)));
        Assert.AreEqual(0f, f.NewScore.Alpha, "NEW RECORD는 1.25부터");
        Assert.AreEqual(0f, f.Best.Alpha);
        Log(1.0f, f);
    }

    [Test]
    public void At15_PopAndJudgeRows()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(1.5f);
        Assert.AreEqual(1f, f.ScoreCount, Eps, "점수 끝");
        Assert.AreEqual(1f, f.NewScore.Scale, Eps, "톡 0.2초 끝 → 1");
        Assert.AreEqual(1f, f.NewScore.Alpha, Eps);
        Assert.AreEqual(E(0.05f / 0.2f), f.Judge0.Alpha, Eps, "PERFECT 1.45 시작");
        Assert.AreEqual(0f, f.Judge1.Alpha, "GREAT 1.57 시작");
        Assert.AreEqual(E(0.05f / 0.35f), f.JudgeCount0, Eps);
        // 톡: 1.35초(앞 절반 끝) = 1.15, 1.30초 = 0.6 → 1.15 사이
        Assert.AreEqual(1.15f, ResultIntroTimeline.Pop(1.35f, 1.25f, 0.2f).Scale, Eps);
        Assert.AreEqual(1f, ResultIntroTimeline.Pop(1.35f, 1.25f, 0.2f).Alpha, Eps, "알파는 앞 절반에 1");
        Assert.AreEqual(0.6f, ResultIntroTimeline.Pop(1.25f, 1.25f, 0.2f).Scale, Eps);
        Log(1.5f, f);
    }

    [Test]
    public void At25_FastSlowAndRate()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(2.5f);
        Assert.AreEqual(1f, f.Judge3.Alpha, Eps);
        Assert.AreEqual(1f, f.FastSlow.Alpha, Eps);
        Assert.AreEqual(1f, f.FastSlowProgress, Eps, "1.95 + 0.35 = 2.30 끝");
        Assert.AreEqual(1f, f.Rate.Alpha, Eps, "RATE 2.30 + 0.20");
        Assert.AreEqual(E(0.2f / 0.4f), f.RateValue, Eps, "RATE 값 0.40초");
        Assert.AreEqual(0f, f.Combo.Alpha);
        Assert.AreEqual(0f, f.NewRate.Alpha, "2.70부터");
        Log(2.5f, f);
    }

    [Test]
    public void At35_RankStamp()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(3.5f);
        Assert.AreEqual(1f, f.ComboValue, Eps, "콤보 2.80 + 0.40 = 3.20 끝");
        Assert.AreEqual(1f, f.FullCombo.Scale, Eps, "FC 3.20 + 0.22 끝");
        float q = 0.05f / 0.35f;
        Assert.AreEqual(Mathf.Lerp(2f, 0.88f, E(q / 0.55f)), f.Rank.Scale, Eps, "도장 크기");
        Assert.AreEqual(E(q * 3f), f.Rank.Alpha, Eps, "알파 처음 1/3");
        Assert.AreEqual(0f, f.RankFlash, "번쩍임은 0.55 지점 ± 0.2");
        Log(3.5f, f);

        float impact = 3.45f + 0.35f * 0.55f;
        Assert.AreEqual(0.88f, ResultIntroTimeline.Stamp(impact).Scale, Eps, "0.55 지점 0.88");
        Assert.AreEqual(0.8f, ResultIntroTimeline.RankFlash(impact), Eps, "찍히는 순간 +80%");
        Assert.AreEqual(1f, ResultIntroTimeline.Stamp(3.8f).Scale, Eps, "끝 1.0");
        Assert.AreEqual(0f, ResultIntroTimeline.RankFlash(3.8f), "끝에는 번쩍임 없음");
    }

    [Test]
    public void At39_Finished()
    {
        ResultIntroFrame f = ResultIntroTimeline.Evaluate(3.9f);
        Assert.IsTrue(f.IsFinished);
        Assert.AreEqual(1f, f.Rank.Scale);
        Assert.AreEqual(0f, f.RankFlash);
        Assert.AreEqual(1f, f.ScoreCount);
        Assert.AreEqual(1f, f.NewCombo.Scale);
        Assert.IsTrue(ResultIntroTimeline.Finished.IsFinished);
        Assert.AreEqual(945936, ResultIntroTimeline.CountInt(945936, 1f));
        Assert.AreEqual(93.74, ResultIntroTimeline.CountDouble(93.74, 1f), 1e-9);
        Log(3.9f, f);
    }

    /// <summary>NEW RECORD · FC가 없어도 시간표는 같다 (화면이 그 요소만 숨긴다): 뒤 단계 값이 단계 유무와 상관없다</summary>
    [Test]
    public void MissingStages_DoNotShiftTimes()
    {
        for (float t = 0f; t <= 3.9f; t += 0.05f)
        {
            ResultIntroFrame f = ResultIntroTimeline.Evaluate(t);
            Assert.AreEqual(ResultIntroTimeline.Stamp(t).Scale, f.Rank.Scale, Eps, "랭크는 언제나 3.45 시작");
            Assert.AreEqual(ResultIntroTimeline.Slide(t, 2.8f, 0.2f, new Vector2(0f, -12f)).Alpha, f.Combo.Alpha, Eps, "MAX COMBO는 언제나 2.80 시작");
        }
    }

    [Test]
    public void ShowTestResult_Save_Legacy_Clone()
    {
        Assert.IsTrue(new LastSelectionSaveData().editorShowTestResult, "처음 true");
        PlayerData data = new PlayerData();
        Assert.IsTrue(PlayerDataRules.TrySetEditorShowTestResult(data.lastSelection, false));
        Assert.IsFalse(PlayerDataRules.TrySetEditorShowTestResult(data.lastSelection, false), "같은 값이면 저장 안 함");
        PlayerData loaded = PlayerDataRules.Sanitize(PlayerDataStore.Parse(JsonUtility.ToJson(data)));
        Assert.IsFalse(loaded.lastSelection.editorShowTestResult, "저장 → 불러오기");
        Assert.IsFalse(loaded.lastSelection.Clone().editorShowTestResult, "복사본");
        LastSelectionSaveData copy = new LastSelectionSaveData();
        copy.CopyFrom(loaded.lastSelection);
        Assert.IsFalse(copy.editorShowTestResult, "CopyFrom");

        const string legacy = "{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":2.5,\"editorTestPlayMultiplier\":1.5,\"official\":{\"songId\":\"\",\"chartId\":\"\"},\"custom\":{\"songId\":\"\",\"chartId\":\"\"}}}";
        PlayerData old = PlayerDataRules.Sanitize(PlayerDataStore.Parse(legacy));
        Assert.IsTrue(old.lastSelection.editorShowTestResult, "예전 JSON → true");
        Assert.IsTrue(new TestPlayData().showResult, "테스트 플레이 데이터 처음 true");
    }

    private static void Log(float t, ResultIntroFrame f)
    {
        Debug.Log($"[ResultIntroTest] t={t:0.0}: 앨범 a{f.Album.Alpha:0.00} s{f.Album.Scale:0.00} · 제목 a{f.Title.Alpha:0.00} x{f.Title.Offset.x:0.0} · SCORE a{f.Score.Alpha:0.00} y{f.Score.Offset.y:0.0} 숫자 {f.ScoreCount:0.000} · " +
                  $"NEW 점수 a{f.NewScore.Alpha:0.00} s{f.NewScore.Scale:0.00} · BEST a{f.Best.Alpha:0.00} · 판정 a{f.Judge0.Alpha:0.00}/{f.Judge1.Alpha:0.00}/{f.Judge2.Alpha:0.00}/{f.Judge3.Alpha:0.00} 개수 {f.JudgeCount0:0.00} · " +
                  $"F/S a{f.FastSlow.Alpha:0.00} 막대 {f.FastSlowProgress:0.00} · RATE a{f.Rate.Alpha:0.00} y{f.Rate.Offset.y:0.0} 값 {f.RateValue:0.000} · COMBO a{f.Combo.Alpha:0.00} 값 {f.ComboValue:0.000} · " +
                  $"FC a{f.FullCombo.Alpha:0.00} s{f.FullCombo.Scale:0.00} · 랭크 a{f.Rank.Alpha:0.00} s{f.Rank.Scale:0.00} 번쩍 {f.RankFlash:0.00}");
    }
}
