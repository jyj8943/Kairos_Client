using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 난이도 표기 색(DifficultyColors) EditMode 테스트: 이름 → 배경·흰 글자, 난이도 색 위 글자 외곽선 프리셋 머티리얼.
/// </summary>
public class DifficultyColorsTests
{
    /// <summary>난이도 색 위 글자에 쓰는 외곽선 프리셋 (글꼴 머티리얼 폴더)</summary>
    public const string OutlinePresetPath = "Assets/Fonts/NEXON_Lv2_Gothic/TTF/NEXON Lv2 Gothic SDF Outline Difficulty.mat";
    public const string FontAssetPath = "Assets/Fonts/NEXON_Lv2_Gothic/TTF/NEXON Lv2 Gothic SDF.asset";

    private static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

    /// <summary>1: EASY·NORMAL·HARD·CUSTOM(모르는 이름 포함) → 밝은 배경 + 흰 글자, 대소문자·앞뒤 공백 무시</summary>
    [TestCase("EASY", 0x22C55E)]
    [TestCase("NORMAL", 0xFACC15)]
    [TestCase("HARD", 0xEF4444)]
    [TestCase("CUSTOM", 0x8B5CF6)]
    [TestCase("normal", 0xFACC15)]
    [TestCase(" Hard ", 0xEF4444)]
    [TestCase("EXPERT", 0x8B5CF6)]
    [TestCase(null, 0x8B5CF6)]
    public void Get_ReturnsBackgroundAndWhiteText(string difficulty, int background)
    {
        DifficultyColorPair pair = DifficultyColors.Get(difficulty);
        Assert.AreEqual(Hex(background), pair.Background, "배경");
        Assert.AreEqual(Hex(0xFFFFFF), pair.Text, "글자 = 흰색");
    }

    /// <summary>공식 난이도 이름 목록(GameConstants.Chart.Difficulties)은 모두 CUSTOM이 아닌 자기 색을 가진다</summary>
    [Test]
    public void EveryOfficialDifficulty_HasOwnColor()
    {
        foreach (string difficulty in GameConstants.Chart.Difficulties)
            Assert.AreNotEqual(DifficultyColors.Custom.Background, DifficultyColors.Get(difficulty).Background, difficulty);
    }

    /// <summary>2: 외곽선 프리셋은 글꼴 기본 머티리얼과 다른 에셋이고, 외곽선 굵기 > 0, 외곽선 색 = 상수. 기본 머티리얼에는 외곽선이 없다</summary>
    [Test]
    public void OutlinePreset_IsSeparateAssetWithConstants()
    {
        Material preset = AssetDatabase.LoadAssetAtPath<Material>(OutlinePresetPath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Assert.IsNotNull(preset, "외곽선 프리셋 에셋");
        Assert.IsNotNull(font, "글꼴 에셋");

        Assert.AreNotSame(font.material, preset, "글꼴 기본 머티리얼과 다른 에셋");
        Assert.AreNotEqual(AssetDatabase.GetAssetPath(font.material), AssetDatabase.GetAssetPath(preset), "다른 파일");
        // 동적 글꼴이라 플레이 모드 앞뒤로 아틀라스 객체가 바뀔 수 있어 인스턴스가 아니라 에셋(파일·이름)으로 비교한다
        Texture presetAtlas = preset.GetTexture(ShaderUtilities.ID_MainTex);
        Assert.IsNotNull(presetAtlas, "프리셋 아틀라스");
        Assert.AreEqual(FontAssetPath, AssetDatabase.GetAssetPath(presetAtlas), "같은 글꼴의 아틀라스");
        Assert.AreEqual(font.atlasTexture.name, presetAtlas.name, "같은 글꼴의 아틀라스 이름");

        Assert.Greater(preset.GetFloat(ShaderUtilities.ID_OutlineWidth), 0f, "외곽선 굵기 > 0");
        Assert.AreEqual(GameConstants.DifficultyColor.OutlineWidth, preset.GetFloat(ShaderUtilities.ID_OutlineWidth), 1e-4f, "외곽선 굵기 = 상수");
        Assert.AreEqual(GameConstants.DifficultyColor.OutlineColor, preset.GetColor(ShaderUtilities.ID_OutlineColor), "외곽선 색 = 상수");
        Assert.AreEqual(0f, font.material.GetFloat(ShaderUtilities.ID_OutlineWidth), 1e-6f, "글꼴 기본 머티리얼은 외곽선 없음");
    }
}
