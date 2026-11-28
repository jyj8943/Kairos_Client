using NUnit.Framework;

/// <summary>
/// 암전 로딩 (기획서 3-10, FadeLoadingRules) EditMode 확인: 열기 t² · 음악 1 − t² · 닫기 ease-out, 시작 시각 = 3.0 − 0.5.
/// </summary>
public class FadeLoadingTests
{
    [TestCase(0f, 0f)]
    [TestCase(0.1f, 0.04f)]
    [TestCase(0.25f, 0.25f)]
    [TestCase(0.5f, 1f)]
    [TestCase(0.8f, 1f)]
    public void OpenAlpha_EaseIn(float elapsed, float expected)
    {
        Assert.AreEqual(expected, FadeLoadingRules.OpenAlpha(elapsed), 1e-5f);
        Assert.AreEqual(1f - expected, FadeLoadingRules.OpenMusicFactor(elapsed), 1e-5f, "음악 계수는 같은 곡선으로 1 → 0");
    }

    [Test]
    public void CloseAlpha_EaseOut()
    {
        float Close(float e) { float t = e / 0.45f; return 1f - (1f - (1f - t) * (1f - t)); }
        Assert.AreEqual(1f, FadeLoadingRules.CloseAlpha(0f), 1e-5f);
        Assert.AreEqual(Close(0.1f), FadeLoadingRules.CloseAlpha(0.1f), 1e-5f);
        Assert.AreEqual(Close(0.25f), FadeLoadingRules.CloseAlpha(0.25f), 1e-5f);
        Assert.AreEqual(0f, FadeLoadingRules.CloseAlpha(0.45f), 1e-5f);
        Assert.Less(FadeLoadingRules.CloseAlpha(0.1f), 1f - 0.1f / 0.45f, "ease-out: 처음에 빨리 걷힌다");
    }

    [Test]
    public void Constants()
    {
        Assert.AreEqual(0.5f, GameConstants.FadeLoading.OpenSeconds);
        Assert.AreEqual(0.2f, GameConstants.FadeLoading.MinSeconds);
        Assert.AreEqual(0.4f, GameConstants.FadeLoading.RestartMinSeconds, "재시작 암전 최소 0.4초 (덮기 끝부터)");
        Assert.AreEqual(1.35f, GameConstants.FadeLoading.OpenSeconds + GameConstants.FadeLoading.RestartMinSeconds + GameConstants.FadeLoading.CloseSeconds, 1e-5f,
            "재시작 암전 가장 짧을 때 약 1.35초");
        Assert.AreEqual(0.45f, GameConstants.FadeLoading.CloseSeconds);
        Assert.AreEqual(2.5, GameConstants.Play.FadeStartSeconds, 1e-9, "3.0 − 0.5");
        Assert.AreEqual(GameConstants.Play.ResultDelaySeconds, GameConstants.Play.FadeStartSeconds + GameConstants.FadeLoading.OpenSeconds, 1e-9, "곡 끝 → 결과는 3초 그대로");
        Assert.Greater(GameConstants.FadeLoading.SortOrder, GameConstants.Loading.SortOrder);
        Assert.Greater(GameConstants.FadeLoading.SortOrder, GameConstants.SongLoading.SortOrder);
        Assert.AreEqual(0.25f, UIEasing.InQuad(0.5f), 1e-6f);
        Assert.AreEqual(0.75f, UIEasing.OutQuad(0.5f), 1e-6f);
    }
}
