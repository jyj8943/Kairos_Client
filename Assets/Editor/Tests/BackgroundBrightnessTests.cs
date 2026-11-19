using NUnit.Framework;

/// <summary>
/// 인게임 배경 밝기 규칙 EditMode 확인 (기획서 3-4 「배경 · BGA (11/19)」, 2-4 「마지막 선택」).
/// 1. 밝기 단계: PgUp·PgDn 5단계, 끝값에서 멈춤, 덮개 알파 = 1 − 밝기
/// 2. 저장: 바꾼 값 저장 → 불러오기, 예전 JSON(필드 없음) → 100, 33 → 25
/// 3. 단계 막대 칸 수: 0 → 0칸, 25 → 1칸 … 100 → 4칸
/// + 카드 키 이름 줄이기 (Page Up → PgUp)
/// </summary>
public class BackgroundBrightnessTests
{
    [Test]
    public void Step_FiveSteps_StopsAtEnds_DimAlpha()
    {
        CollectionAssert.AreEqual(new[] { 0, 25, 50, 75, 100 }, GameConstants.Play.BackgroundBrightnessSteps);
        Assert.AreEqual(100, BackgroundBrightnessRules.Default);

        int value = 100;
        int[] down = new int[5];
        for (int i = 0; i < 5; i++) down[i] = value = BackgroundBrightnessRules.Step(value, -1);
        CollectionAssert.AreEqual(new[] { 75, 50, 25, 0, 0 }, down, "PgDn 한 번에 한 단계, 0%에서 멈춤");

        int[] up = new int[5];
        for (int i = 0; i < 5; i++) up[i] = value = BackgroundBrightnessRules.Step(value, 1);
        CollectionAssert.AreEqual(new[] { 25, 50, 75, 100, 100 }, up, "PgUp 한 번에 한 단계, 100%에서 멈춤");

        Assert.AreEqual(0f, BackgroundBrightnessRules.DimAlpha(100), 1e-6f);
        Assert.AreEqual(0.25f, BackgroundBrightnessRules.DimAlpha(75), 1e-6f);
        Assert.AreEqual(0.5f, BackgroundBrightnessRules.DimAlpha(50), 1e-6f);
        Assert.AreEqual(0.75f, BackgroundBrightnessRules.DimAlpha(25), 1e-6f);
        Assert.AreEqual(1f, BackgroundBrightnessRules.DimAlpha(0), 1e-6f, "0%는 검은 화면");
    }

    [Test]
    public void Save_RoundTrip_LegacyJson_Nearest()
    {
        PlayerData data = new PlayerData();
        Assert.IsTrue(PlayerDataRules.TrySetLastBackgroundBrightness(data.lastSelection, 50));
        Assert.IsFalse(PlayerDataRules.TrySetLastBackgroundBrightness(data.lastSelection, 50), "같은 값이면 저장하지 않음");
        PlayerData loaded = PlayerDataRules.Sanitize(PlayerDataStore.Parse(UnityEngine.JsonUtility.ToJson(data)));
        Assert.AreEqual(50, loaded.lastSelection.backgroundBrightness, "저장 → 불러오기");
        Assert.AreEqual(50, loaded.lastSelection.Clone().backgroundBrightness, "복사본도 같은 값");

        // 예전 JSON: lastSelection에 backgroundBrightness가 없다
        const string legacy = "{\"version\":1,\"lastSelection\":{\"scrollMultiplier\":2.5,\"official\":{\"songId\":\"\",\"chartId\":\"\"},\"custom\":{\"songId\":\"\",\"chartId\":\"\"}}}";
        PlayerData old = PlayerDataRules.Sanitize(PlayerDataStore.Parse(legacy));
        Assert.AreEqual(100, old.lastSelection.backgroundBrightness, "예전 JSON → 100");

        LastSelectionSaveData odd = new LastSelectionSaveData { backgroundBrightness = 33 };
        PlayerDataRules.SanitizeLastSelection(odd);
        Assert.AreEqual(25, odd.backgroundBrightness, "33 → 가장 가까운 25");
        Assert.AreEqual(100, BackgroundBrightnessRules.Sanitize(140), "범위 밖 → 끝 단계");
        Assert.AreEqual(0, BackgroundBrightnessRules.Sanitize(-10));
        Assert.AreEqual(75, BackgroundBrightnessRules.Sanitize(70));
    }

    [Test]
    public void FilledBars_PerQuarter()
    {
        Assert.AreEqual(4, GameConstants.Play.BrightnessBarCount);
        Assert.AreEqual(0, BackgroundBrightnessRules.FilledBars(0), "0%면 빈 막대");
        Assert.AreEqual(1, BackgroundBrightnessRules.FilledBars(25));
        Assert.AreEqual(2, BackgroundBrightnessRules.FilledBars(50));
        Assert.AreEqual(3, BackgroundBrightnessRules.FilledBars(75));
        Assert.AreEqual(4, BackgroundBrightnessRules.FilledBars(100));
    }

    [Test]
    public void ShortKeyName_PageKeys()
    {
        Assert.AreEqual("PgUp", Img_SpeedBrightnessCard.ShortKeyName("Page Up"));
        Assert.AreEqual("PgDn", Img_SpeedBrightnessCard.ShortKeyName("Page Down"));
        Assert.AreEqual("1", Img_SpeedBrightnessCard.ShortKeyName("1"));
        Assert.AreEqual(string.Empty, Img_SpeedBrightnessCard.ShortKeyName(null));
    }
}
