using NUnit.Framework;

/// <summary>
/// 인게임 시작 메트로놈 계산(CountInRules) EditMode 테스트 (기획서 3-4 「시작 메트로놈」).
/// </summary>
public class CountInRulesTests
{
    private const double SecondsTolerance = 1e-6;

    private static InGameEntryData TestPlayEntry(float startBeat)
    {
        return new InGameEntryData
        {
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { startBeat = startBeat }
        };
    }

    /// <summary>1: 일반 플레이 → 씀, 테스트 플레이 0박 → 씀, 0.25박·12마디(48박) → 안 씀</summary>
    [Test]
    public void UsesCountIn_OnlyWhenStartingFromBeatZero()
    {
        Assert.IsTrue(CountInRules.UsesCountIn(new InGameEntryData { mode = InGameMode.Normal }), "일반 플레이");
        Assert.IsTrue(CountInRules.UsesCountIn(TestPlayEntry(0f)), "테스트 플레이 0박");
        Assert.IsFalse(CountInRules.UsesCountIn(TestPlayEntry(0.25f)), "테스트 플레이 0.25박");
        Assert.IsFalse(CountInRules.UsesCountIn(TestPlayEntry(48f)), "테스트 플레이 12마디(48박)");
    }

    /// <summary>2: BPM 150.61·offset 18ms → 클릭 = 0박 시각 − 4·3·2·1 × (60 ÷ 150.61), 판 시작 = 첫 클릭 − 0.3초</summary>
    [Test]
    public void ClickTimes_AreFourBeatsBeforeBeatZero()
    {
        TimingMap timing = TimingMap.Single(150.61f, 18);
        double beat0 = 0.018;
        double beatSeconds = 60.0 / 150.61;

        double[] clicks = CountInRules.GetClickTimes(timing);
        Assert.AreEqual(GameConstants.Play.CountInBeats, clicks.Length);
        for (int i = 0; i < clicks.Length; i++)
            Assert.AreEqual(beat0 - (4 - i) * beatSeconds, clicks[i], SecondsTolerance, $"{i + 1}번째 클릭 (−{4 - i}박)");

        Assert.AreEqual(clicks[0] - GameConstants.Play.CountInMarginSeconds, CountInRules.GetInitialSongTime(timing), SecondsTolerance, "판 시작 곡 시간");
        Assert.AreEqual(0.3, GameConstants.Play.CountInMarginSeconds, 1e-12, "여유 0.3초");
    }

    /// <summary>
    /// 3: 느린 BPM(60)·첫 노트 0박 → 4박이 모두 들어가고 시작 지연이 예전보다 줄지 않음.
    /// 0박 바로 뒤에 BPM 구간이 있어도 음수 박은 첫 BPM(60)으로 계산. 테스트 플레이(0박)도 같은 판 시작 곡 시간.
    /// </summary>
    [Test]
    public void SlowBpm_FitsAllClicks_AndNegativeBeatsUseFirstBpm()
    {
        TimingMap timing = new TimingMap(new[]
        {
            new TimingPointData { beat = 0f, bpm = 60f },
            new TimingPointData { beat = 0.5f, bpm = 240f }
        }, 0);

        double[] clicks = CountInRules.GetClickTimes(timing);
        CollectionAssert.AreEqual(new[] { -4.0, -3.0, -2.0, -1.0 }, clicks, "음수 박은 첫 BPM 60으로 1초 간격");

        double firstNoteTime = timing.BeatToSeconds(0);
        double oldDelay = CountInRules.GetNormalStartDelay(firstNoteTime);
        double delay = CountInRules.GetStartDelay(timing, oldDelay);
        Assert.AreEqual(4.3, delay, SecondsTolerance, "시작 지연 = 4박 + 여유 0.3초");
        Assert.GreaterOrEqual(delay, oldDelay, "예전 시작 지연보다 줄지 않음");
        Assert.LessOrEqual(-delay, clicks[0] - GameConstants.Play.CountInMarginSeconds + SecondsTolerance, "판 시작 곡 시간이 첫 클릭 앞");

        // 빠른 BPM + 첫 노트가 늦어 예전 지연이 더 길면 예전 값을 쓴다
        TimingMap fast = TimingMap.Single(300f, 0);
        Assert.AreEqual(5.0, CountInRules.GetStartDelay(fast, 5.0), SecondsTolerance, "예전 지연이 더 길면 그대로");

        // 테스트 플레이 0박: TestPlayRules로 정한 판 시작 곡 시간 = 같은 계산
        double startTime = timing.BeatToSeconds(0);
        TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(startTime, CountInRules.GetTestPlayLeadIn(timing, startTime));
        Assert.AreEqual(0.0, start.StartPositionSeconds, SecondsTolerance, "노래는 음원 0초부터");
        Assert.AreEqual(CountInRules.GetInitialSongTime(timing), start.InitialSongTime, SecondsTolerance, "테스트 플레이 판 시작 곡 시간");
        Assert.LessOrEqual(start.InitialSongTime, startTime - GameConstants.Play.TestPlayLeadInSeconds + SecondsTolerance, "테스트 플레이 준비 시간이 줄지 않음");
    }
}
