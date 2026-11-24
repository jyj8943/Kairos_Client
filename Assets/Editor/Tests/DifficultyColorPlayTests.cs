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
/// 난이도 표기 색 실제 씬 확인: 곡 선택 Btn_Difficulty(공식 탭 난이도 카드, 메뉴용 UITheme 색).
/// 인게임 곡 정보 카드 뱃지(같은 메뉴용 색)는 InGameTonePlayTests가 본다.
/// 고르는 채보는 Critical Velocity NORMAL. 나머지 난이도는 공식 채보가 있으면 고르지 않은 모양, 없으면 채보 없음(회색)이어야 한다.
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


    /// <summary>3: 곡 선택 공식 탭에서 NORMAL 선택 (라벤더 톤, 메뉴용 UITheme 색) → 포인트색 테두리 + 선택 바탕(포인트색 18%),
    /// 이름 뱃지 = 메뉴용 NORMAL 색 #2F7FC8 + 흰 글자, 레벨 기본 글자색. 나머지 난이도는 테두리 없음·패널 바탕이고, 채보가 있으면 난이도 색 뱃지·레벨, 없으면 레벨 ―(흐린 글자)·누를 수 없음.
    /// </summary>
    [UnityTest]
    public IEnumerator D3_SongSelectNormalChosen_BorderAndDifficultyColorName()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect,
            new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.AreEqual(ChartType.Official, songSelect.CurrentTab);
        Assert.AreEqual(TestCharts.OfficialChartId, songSelect.ChosenChart.chartId);

        UITheme theme = UITheme.Current;
        Btn_Difficulty[] buttons = Get<Btn_Difficulty[]>(songSelect, "difficultyButtons");
        List<string> log = new List<string>();
        foreach (Btn_Difficulty button in buttons)
        {
            Image background = Get<Image>(button, "background");
            Image border = Get<Image>(button, "chosenBorder");
            Image badge = Get<Image>(button, "difficultyBadge");
            TMP_Text name = Get<TMP_Text>(button, "difficultyText");
            TMP_Text level = Get<TMP_Text>(button, "levelText");
            log.Add($"{button.Difficulty}: 채보 {(button.Chart != null ? "있음" : "없음")}, 테두리 {(border.gameObject.activeSelf ? "켜짐" : "꺼짐")}, 뱃지 #{ColorUtility.ToHtmlStringRGBA(badge.color)}, 레벨 #{ColorUtility.ToHtmlStringRGB(level.color)}");
            AssertColor(Color.white, name.color, $"{button.Difficulty}: 뱃지 위 이름 = 흰색");

            if (button.Difficulty == GameConstants.Chart.DifficultyNormal)
            {
                Assert.IsTrue(button.IsChosen, "NORMAL 고름");
                Assert.IsTrue(border.gameObject.activeSelf, "NORMAL 포인트색 테두리");
                AssertColor(theme.accent, border.color, "테두리 = 포인트색 #3E9BE0");
                AssertColor(theme.SelectedBackground, background.color, "고른 카드 바탕 = 포인트색 18%");
                AssertColor(new Color32(0x2F, 0x7F, 0xC8, 0xFF), badge.color, "NORMAL 뱃지 = 메뉴용 #2F7FC8");
                AssertColor(theme.textPrimary, level.color, "레벨 = 기본 글자색");
                continue;
            }

            Assert.IsFalse(button.IsChosen, $"{button.Difficulty}: 고르지 않음");
            Assert.IsFalse(border.gameObject.activeSelf, $"{button.Difficulty}: 테두리 없음");
            AssertColor(theme.panel, background.color, $"{button.Difficulty}: 패널 바탕");

            ChartData official = ChartRepository.GetCharts(TestCharts.OfficialSongId, ChartType.Official).Find(c => c.difficulty == button.Difficulty);
            Assert.AreEqual(official != null, button.Chart != null, $"{button.Difficulty}: 채보 유무 = 공식 채보 파일");
            if (official != null)
            {
                Assert.AreEqual(official.chartId, button.Chart.chartId, $"{button.Difficulty}: 채보");
                AssertColor(theme.GetDifficulty(button.Difficulty), badge.color, $"{button.Difficulty}: 뱃지 난이도 색");
                AssertColor(theme.textPrimary, level.color, $"{button.Difficulty}: 레벨 기본 글자색");
                Assert.AreEqual(string.Format(GameConstants.SongSelect.LevelFormat, official.level), level.text, $"{button.Difficulty}: 레벨");
                Assert.IsTrue(button.Button.interactable, $"{button.Difficulty}: 누를 수 있음");
                continue;
            }

            AssertColor(theme.textMuted, level.color, $"{button.Difficulty}: 레벨 흐린 글자");
            Assert.AreEqual(GameConstants.SongSelect.EmptyValue, level.text, $"{button.Difficulty}: 레벨 ―");
            Assert.IsFalse(button.Button.interactable, $"{button.Difficulty}: 누를 수 없음");
        }

        // NORMAL 선택을 풀면 테두리가 꺼지고 뱃지는 난이도 색 그대로
        Btn_Difficulty normalButton = System.Array.Find(buttons, b => b.Difficulty == GameConstants.Chart.DifficultyNormal);
        normalButton.SetChosen(false);
        Assert.IsFalse(Get<Image>(normalButton, "chosenBorder").gameObject.activeSelf, "선택 풀림 → 테두리 꺼짐");
        AssertColor(theme.GetDifficulty(normalButton.Difficulty), normalButton.BadgeColor, "선택 풀림 → 뱃지 난이도 색 유지");
        normalButton.SetChosen(true);
        Debug.Log($"[DifficultyColorTest] D3 곡 선택: {string.Join(" / ", log)}");
    }
}
