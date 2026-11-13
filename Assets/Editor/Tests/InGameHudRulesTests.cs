using NUnit.Framework;

/// <summary>
/// 인게임 곡 정보 패널 표시 규칙·곡 진행 바 비율 (InGameHudRules) EditMode 테스트 (기획서 3-4 "왼쪽 곡 정보 패널" 표).
/// </summary>
public class InGameHudRulesTests
{
    /// <summary>공식 채보: 난이도 뱃지(난이도 순번), 채보 이름 숨김, Lv. 표시</summary>
    [Test]
    public void Official_DifficultyBadgeAndNoChartName()
    {
        for (int i = 0; i < GameConstants.Chart.Difficulties.Length; i++)
        {
            string difficulty = GameConstants.Chart.Difficulties[i];
            SongInfoDisplay info = InGameHudRules.BuildSongInfo(ChartType.Official, difficulty, "HARD 채보", 9, false, false);
            Assert.AreEqual(difficulty, info.BadgeText);
            Assert.AreEqual(i, info.DifficultyIndex, "뱃지 색은 난이도 순번으로 고른다");
            Assert.IsFalse(info.IsCustom);
            Assert.IsFalse(info.ShowChartName, "공식 채보는 채보 이름을 숨긴다");
            Assert.AreEqual("Lv.9", info.LevelText);
        }
    }

    /// <summary>공식 채보 난이도 이름은 대소문자와 상관없이 같은 뱃지, 모르는 이름은 순번 -1 (CUSTOM 색)</summary>
    [Test]
    public void Official_CaseInsensitiveAndUnknown()
    {
        SongInfoDisplay lower = InGameHudRules.BuildSongInfo(ChartType.Official, "normal", null, 5, false, false);
        Assert.AreEqual("NORMAL", lower.BadgeText);
        Assert.AreEqual(System.Array.IndexOf(GameConstants.Chart.Difficulties, "NORMAL"), lower.DifficultyIndex);

        SongInfoDisplay unknown = InGameHudRules.BuildSongInfo(ChartType.Official, "EXTREME", null, 5, false, false);
        Assert.AreEqual("EXTREME", unknown.BadgeText);
        Assert.AreEqual(-1, unknown.DifficultyIndex);
    }

    /// <summary>나만의 채보: CUSTOM 뱃지 + 채보 이름 + Lv. (이름이 없으면 채보 이름 숨김)</summary>
    [Test]
    public void Custom_BadgeAndChartName()
    {
        SongInfoDisplay info = InGameHudRules.BuildSongInfo(ChartType.Custom, "HARD", "롱노트 연습", 3, false, false);
        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, info.BadgeText);
        Assert.AreEqual("CUSTOM", info.BadgeText);
        Assert.IsTrue(info.IsCustom);
        Assert.AreEqual(-1, info.DifficultyIndex, "난이도 이름이 있어도 CUSTOM 색");
        Assert.IsTrue(info.ShowChartName);
        Assert.AreEqual("롱노트 연습", info.ChartName);
        Assert.AreEqual("Lv.3", info.LevelText);

        SongInfoDisplay noName = InGameHudRules.BuildSongInfo(ChartType.Custom, null, "", 1, false, false);
        Assert.IsFalse(noName.ShowChartName);
        Assert.AreEqual(string.Empty, noName.ChartName);
    }

    /// <summary>테스트 플레이 표시: 보통 플레이는 꺼짐, 테스트 플레이는 켜짐, 자동 플레이면 "자동 플레이"를 함께 씀 (공식·나만의 채보 같음)</summary>
    [Test]
    public void TestPlayBadge()
    {
        foreach (ChartType type in new[] { ChartType.Official, ChartType.Custom })
        {
            Assert.IsFalse(InGameHudRules.BuildSongInfo(type, "EASY", "a", 1, false, false).ShowTestPlay, $"{type} 보통 플레이");

            SongInfoDisplay test = InGameHudRules.BuildSongInfo(type, "EASY", "a", 1, true, false);
            Assert.IsTrue(test.ShowTestPlay);
            Assert.AreEqual(GameConstants.Play.TestPlayLabel, test.TestPlayText);

            SongInfoDisplay auto = InGameHudRules.BuildSongInfo(type, "EASY", "a", 1, true, true);
            Assert.IsTrue(auto.ShowTestPlay);
            Assert.AreEqual(GameConstants.Play.AutoTestPlayLabel, auto.TestPlayText);
            StringAssert.Contains("자동 플레이", auto.TestPlayText);
        }
    }

    /// <summary>진행 바 비율: 곡 시간 ÷ 곡 길이, 0 미만·1 초과는 자르고 곡 길이 0 이하면 0</summary>
    [Test]
    public void ProgressRatio_Clamps()
    {
        Assert.AreEqual(0.25f, InGameHudRules.ProgressRatio(30, 120), 1e-6f);
        Assert.AreEqual(0f, InGameHudRules.ProgressRatio(-1.5, 120), "시작 전 대기 (곡 시간 음수)");
        Assert.AreEqual(1f, InGameHudRules.ProgressRatio(130, 120), "곡 끝 뒤");
        Assert.AreEqual(1f, InGameHudRules.ProgressRatio(120, 120));
        Assert.AreEqual(0f, InGameHudRules.ProgressRatio(10, 0), "곡 길이 0");
        Assert.AreEqual(0f, InGameHudRules.ProgressRatio(10, -5));
        Assert.AreEqual(0f, InGameHudRules.ProgressRatio(double.NaN, 120));
    }
}
