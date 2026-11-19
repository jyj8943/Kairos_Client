using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 기어 판정 글자·FAST/SLOW 이미지 (JudgeSprites, 11/19) EditMode 확인.
/// 1. 6장 모두 불러와짐, 같은 판정을 두 번 부르면 같은 객체 (캐시)
/// 2. 화면 크기 = 원본 × 0.5 (469×179 → 234.5×89.5)
/// </summary>
public class JudgeSpritesTests
{
    [Test]
    public void AllSixLoad_AndCached()
    {
        foreach (JudgeType judge in System.Enum.GetValues(typeof(JudgeType)))
        {
            Sprite sprite = JudgeSprites.Get(judge);
            Assert.IsNotNull(sprite, judge + " 이미지");
            Assert.AreSame(sprite, JudgeSprites.Get(judge), judge + " 두 번째는 캐시");
        }
        foreach (FastSlow fastSlow in new[] { FastSlow.Fast, FastSlow.Slow })
        {
            Sprite sprite = JudgeSprites.Get(fastSlow);
            Assert.IsNotNull(sprite, fastSlow + " 이미지");
            Assert.AreSame(sprite, JudgeSprites.Get(fastSlow), fastSlow + " 두 번째는 캐시");
        }
        Assert.IsNull(JudgeSprites.Get(FastSlow.None), "None은 이미지 없음");
        Assert.AreEqual(new Vector2(469f, 179f), JudgeSprites.Get(JudgeType.Perfect).rect.size, "PERFECT 원본 크기");
    }

    [Test]
    public void DisplaySize_HalfOfSource()
    {
        Assert.AreEqual(0.5f, GameConstants.Play.JudgeSpriteDisplayScale);
        Vector2 size = JudgeSprites.DisplaySize(new Vector2(469f, 179f));
        Assert.AreEqual(234.5f, size.x, 1e-4f);
        Assert.AreEqual(89.5f, size.y, 1e-4f);
        Assert.AreEqual(Vector2.zero, JudgeSprites.DisplaySize((Sprite)null));
        Assert.AreEqual(new Vector2(65f, 37.5f), JudgeSprites.DisplaySize(JudgeSprites.Get(FastSlow.Fast)), "FAST 130×75 → 65×37.5");
    }
}
