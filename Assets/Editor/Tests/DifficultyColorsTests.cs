using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 난이도 표기 색(DifficultyColors) EditMode 테스트: 이름 → 배경·글자색, 배경과 글자의 명암 대비 (WCAG 2 상대 휘도).
/// </summary>
public class DifficultyColorsTests
{
    private static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

    /// <summary>1: EASY 초록 · NORMAL 노랑(어두운 글자) · HARD 빨강, 대소문자·앞뒤 공백 무시, 모르는 이름은 CUSTOM 보라</summary>
    [TestCase("EASY", 0x15803D, 0xFFFFFF)]
    [TestCase("NORMAL", 0xF5B800, 0x1A1A1A)]
    [TestCase("HARD", 0xDC2626, 0xFFFFFF)]
    [TestCase("normal", 0xF5B800, 0x1A1A1A)]
    [TestCase(" Hard ", 0xDC2626, 0xFFFFFF)]
    [TestCase("EXPERT", 0x7C3AED, 0xFFFFFF)]
    [TestCase("", 0x7C3AED, 0xFFFFFF)]
    [TestCase(null, 0x7C3AED, 0xFFFFFF)]
    public void Get_ReturnsPair(string difficulty, int background, int text)
    {
        DifficultyColorPair pair = DifficultyColors.Get(difficulty);
        Assert.AreEqual(Hex(background), pair.Background, "배경");
        Assert.AreEqual(Hex(text), pair.Text, "글자");
    }

    /// <summary>공식 난이도 이름 목록(GameConstants.Chart.Difficulties)은 모두 CUSTOM이 아닌 자기 색을 가진다</summary>
    [Test]
    public void EveryOfficialDifficulty_HasOwnColor()
    {
        foreach (string difficulty in GameConstants.Chart.Difficulties)
            Assert.AreNotEqual(DifficultyColors.Custom.Background, DifficultyColors.Get(difficulty).Background, difficulty);
    }

    /// <summary>2: 각 배경과 짝 글자색의 명암 대비가 4.5:1 이상</summary>
    [TestCase("EASY")]
    [TestCase("NORMAL")]
    [TestCase("HARD")]
    [TestCase("CUSTOM")]
    public void Contrast_AtLeast4_5(string difficulty)
    {
        DifficultyColorPair pair = DifficultyColors.Get(difficulty);
        double ratio = ContrastRatio(pair.Background, pair.Text);
        TestContext.WriteLine($"{difficulty}: 명암 대비 {ratio:0.00}:1");
        Assert.GreaterOrEqual(ratio, 4.5, $"{difficulty} 명암 대비 {ratio:0.00}:1");
    }

    /// <summary>WCAG 2 명암 대비 = (밝은 휘도 + 0.05) ÷ (어두운 휘도 + 0.05)</summary>
    public static double ContrastRatio(Color32 a, Color32 b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
    }

    /// <summary>WCAG 2 상대 휘도 = 0.2126 R + 0.7152 G + 0.0722 B (sRGB → 선형)</summary>
    public static double RelativeLuminance(Color32 c)
    {
        return 0.2126 * Linear(c.r) + 0.7152 * Linear(c.g) + 0.0722 * Linear(c.b);
    }

    private static double Linear(byte channel)
    {
        double v = channel / 255.0;
        return v <= 0.03928 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
