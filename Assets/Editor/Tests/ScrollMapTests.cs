using NUnit.Framework;

/// <summary>
/// ScrollMap(곡 시간 → 스크롤 거리) EditMode 테스트. 배속 구간이 없는 지금은 스크롤 거리 = 곡 시간이다.
/// </summary>
public class ScrollMapTests
{
    /// <summary>PositionAt(t) == t (음수 시간·곡 끝 포함)</summary>
    [TestCase(-2.0)]
    [TestCase(0.0)]
    [TestCase(0.12)]
    [TestCase(7.967222214)]
    [TestCase(142.2)]
    public void PositionAt_EqualsSongTime(double songTime)
    {
        Assert.AreEqual(songTime, new ScrollMap().PositionAt(songTime));
    }
}
