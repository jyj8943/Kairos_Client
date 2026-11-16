using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 난이도 표기 색 실제 씬 확인: 곡 선택 Btn_Difficulty(공식 탭)와 인게임 채보 뱃지.
/// Critical Velocity에는 공식 채보가 NORMAL 하나뿐이라 EASY·HARD 버튼은 채보 없음(회색)이다.
/// </summary>
public class DifficultyColorPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        // 마우스 포인터가 Game 뷰의 곡 목록 위에 있으면 hover(UISelectOnHover)로 다른 곡이 골라진다. 테스트 동안 마우스를 끈다
        if (UnityEngine.InputSystem.Mouse.current != null) UnityEngine.InputSystem.InputSystem.DisableDevice(UnityEngine.InputSystem.Mouse.current);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.Mouse && !device.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(device);
        }
        yield return new ExitPlayMode();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");

        deadline = TestSpeed.Deadline(20f);
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }

    private static void AssertColor(Color32 expected, Color actual, string message)
    {
        Assert.AreEqual(expected, (Color32)actual, message);
    }

    private static Material OutlinePreset()
    {
        Material preset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DifficultyColorsTests.OutlinePresetPath);
        Assert.IsNotNull(preset, "외곽선 프리셋 에셋");
        return preset;
    }


    /// <summary>3: 곡 선택 공식 탭에서 NORMAL 선택 → 배경 #FACC15, 이름·레벨 흰 글자 + 외곽선 프리셋. EASY·HARD는 채보가 없어 회색·원래 머티리얼.
    /// 곡 선택의 다른 글자(곡 목록 첫 항목 제목)는 원래 머티리얼 그대로</summary>
    [UnityTest]
    public IEnumerator D3_SongSelectNormalChosen_YellowWithDarkText()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect,
            new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.AreEqual(ChartType.Official, songSelect.CurrentTab);
        Assert.AreEqual(TestCharts.OfficialChartId, songSelect.ChosenChart.chartId);

        Btn_Difficulty[] buttons = Get<Btn_Difficulty[]>(songSelect, "difficultyButtons");
        Color unavailable = Get<Color>(buttons[0], "unavailableTextColor");
        Material preset = OutlinePreset();
        List<string> log = new List<string>();
        foreach (Btn_Difficulty button in buttons)
        {
            Image background = Get<Image>(button, "background");
            TMP_Text name = Get<TMP_Text>(button, "difficultyText");
            TMP_Text level = Get<TMP_Text>(button, "levelText");
            log.Add($"{button.Difficulty}: 채보 {(button.Chart != null ? "있음" : "없음")}, 배경 #{ColorUtility.ToHtmlStringRGB(background.color)}, 글자 #{ColorUtility.ToHtmlStringRGB(name.color)}/#{ColorUtility.ToHtmlStringRGB(level.color)}, 머티리얼 {name.fontSharedMaterial.name}");

            if (button.Difficulty == GameConstants.Chart.DifficultyNormal)
            {
                DifficultyColorPair pair = DifficultyColors.Get(button.Difficulty);
                AssertColor(new Color32(0xFA, 0xCC, 0x15, 0xFF), background.color, "NORMAL 배경 = #FACC15");
                AssertColor(pair.Background, background.color, "NORMAL 배경 = 상수");
                AssertColor(new Color32(0xFF, 0xFF, 0xFF, 0xFF), name.color, "NORMAL 이름 글자 = 흰색");
                AssertColor(new Color32(0xFF, 0xFF, 0xFF, 0xFF), level.color, "NORMAL 레벨 글자 = 흰색");
                Assert.AreSame(preset, name.fontSharedMaterial, "NORMAL 이름 = 외곽선 프리셋");
                Assert.AreSame(preset, level.fontSharedMaterial, "NORMAL 레벨 = 외곽선 프리셋");
                continue;
            }

            Assert.IsNull(button.Chart, $"{button.Difficulty}: 채보 없음");
            Assert.AreEqual(unavailable, name.color, $"{button.Difficulty}: 이름 회색");
            Assert.AreEqual(unavailable, level.color, $"{button.Difficulty}: 레벨 회색");
            Assert.AreNotEqual((Color32)DifficultyColors.Get(button.Difficulty).Background, (Color32)background.color, $"{button.Difficulty}: 난이도 색을 칠하지 않음");
            // 원래 머티리얼 = 버튼이 처음 가져온 머티리얼 (동적 글꼴이라 AssetDatabase로 읽은 객체와 인스턴스가 다를 수 있다)
            Assert.AreSame(Get<Material>(button, "normalNameMaterial"), name.fontSharedMaterial, $"{button.Difficulty}: 선택 안 됨 → 원래 머티리얼");
            Assert.AreSame(Get<Material>(button, "normalLevelMaterial"), level.fontSharedMaterial, $"{button.Difficulty}: 선택 안 됨 → 원래 머티리얼");
            Assert.AreNotSame(preset, name.fontSharedMaterial);
            Assert.AreEqual(0f, name.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth), 1e-6f, $"{button.Difficulty}: 외곽선 없음");
        }

        // NORMAL 선택을 풀면 원래 머티리얼로 돌아온다
        Btn_Difficulty normalButton = System.Array.Find(buttons, b => b.Difficulty == GameConstants.Chart.DifficultyNormal);
        normalButton.SetChosen(false);
        Assert.AreSame(Get<Material>(normalButton, "normalNameMaterial"), Get<TMP_Text>(normalButton, "difficultyText").fontSharedMaterial, "선택 풀림 → 이름 원래 머티리얼");
        Assert.AreSame(Get<Material>(normalButton, "normalLevelMaterial"), Get<TMP_Text>(normalButton, "levelText").fontSharedMaterial, "선택 풀림 → 레벨 원래 머티리얼");
        normalButton.SetChosen(true);

        // 곡 선택의 다른 글자는 외곽선 프리셋을 쓰지 않는다 (난이도 버튼 밖의 TMP 전부)
        int otherTexts = 0;
        foreach (TMP_Text text in songSelect.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Btn_Difficulty>(true) != null) continue;
            Assert.AreNotSame(preset, text.fontSharedMaterial, $"{text.name}: 외곽선 프리셋을 쓰지 않음");
            otherTexts++;
        }
        Assert.Greater(otherTexts, 0);
        log.Add($"다른 글자 {otherTexts}개는 프리셋 아님");
        Debug.Log($"[DifficultyColorTest] D3 곡 선택: {string.Join(" / ", log)}");
    }

    /// <summary>4: 인게임 뱃지 — 공식 Critical Velocity NORMAL은 #FACC15 + 외곽선 프리셋, 나만의 채보 테스트 플레이는 #8B5CF6 CUSTOM</summary>
    [UnityTest]
    public IEnumerator D4_InGameBadge_OfficialYellow_CustomPurple()
    {
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData official, out string error), error);
        yield return LoadAndWait(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal
        });
        Canvas_InGame canvas = Object.FindFirstObjectByType<Canvas_InGame>();
        Image badge = Get<Image>(canvas, "chartBadgeImage");
        TMP_Text badgeText = Get<TMP_Text>(canvas, "chartBadgeText");
        Assert.AreEqual(GameConstants.Chart.DifficultyNormal, badgeText.text);
        DifficultyColorPair normal = DifficultyColors.Get(GameConstants.Chart.DifficultyNormal);
        AssertColor(new Color32(0xFA, 0xCC, 0x15, 0xFF), badge.color, "공식 NORMAL 뱃지 = #FACC15");
        AssertColor(normal.Text, badgeText.color, "공식 NORMAL 뱃지 글자 = 흰색");
        Assert.AreSame(OutlinePreset(), badgeText.fontSharedMaterial, "뱃지 글자 = 외곽선 프리셋");
        string officialLog = $"공식 {badgeText.text}: #{ColorUtility.ToHtmlStringRGB(badge.color)} / 글자 #{ColorUtility.ToHtmlStringRGB(badgeText.color)}";

        ChartData custom = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = "뱃지 색 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 3,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 8f, lane = 0, type = NoteData.TypeShort } }
        };
        yield return LoadAndWait(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = custom, startBeat = 0f, autoPlay = true }
        });
        canvas = Object.FindFirstObjectByType<Canvas_InGame>();
        badge = Get<Image>(canvas, "chartBadgeImage");
        badgeText = Get<TMP_Text>(canvas, "chartBadgeText");
        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, badgeText.text);
        AssertColor(new Color32(0x8B, 0x5C, 0xF6, 0xFF), badge.color, "나만의 채보 테스트 플레이 뱃지 = #8B5CF6");
        AssertColor(DifficultyColors.Custom.Text, badgeText.color, "CUSTOM 뱃지 글자 = 흰색");

        Debug.Log($"[DifficultyColorTest] D4 인게임 뱃지: {officialLog} | 나만의 채보 {badgeText.text}: #{ColorUtility.ToHtmlStringRGB(badge.color)} / 글자 #{ColorUtility.ToHtmlStringRGB(badgeText.color)}");
    }
}
