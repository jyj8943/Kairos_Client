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


    /// <summary>3: 곡 선택 공식 탭에서 NORMAL 선택 (11/25 난이도 탭) → 위 3px 줄 + 난이도 색 16% 칠, 레벨 숫자 44 난이도 색.
    /// 나머지 난이도는 줄 없음 · 7% 칠 · 레벨 34. 채보가 없으면 레벨 ―(흐린 글자) · 랭크/NOT PLAYED 없음 · 누를 수 없음. 기록 패널 칠 = NORMAL 16%</summary>
    [UnityTest]
    public IEnumerator D3_SongSelectNormalChosen_TabTintAndTopLine()
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
            Image topLine = Get<Image>(button, "topLine");
            TMP_Text level = Get<TMP_Text>(button, "levelText");
            log.Add($"{button.Difficulty}: 채보 {(button.Chart != null ? "있음" : "없음")}, 줄 {(topLine.gameObject.activeSelf ? "켜짐" : "꺼짐")}, 칠 #{ColorUtility.ToHtmlStringRGBA(button.TintColor)}, 레벨 '{level.text}' {level.fontSize}");

            if (button.Difficulty == GameConstants.Chart.DifficultyNormal)
            {
                Assert.IsTrue(button.IsChosen, "NORMAL 고름");
                Assert.IsTrue(topLine.gameObject.activeSelf, "NORMAL 위 3px 줄");
                AssertColor(new Color32(47, 127, 200, 255), topLine.color, "줄 = NORMAL 47,127,200");
                AssertColor(UITheme.WithAlpha(theme.difficultyNormal, 0.16f), button.TintColor, "고른 탭 16%");
                AssertColor(UITheme.WithAlpha(theme.difficultyNormal, 0.16f), songSelect.RecordTintColor, "기록 패널 16%");
                AssertColor(theme.difficultyNormal, level.color, "레벨 = 난이도 색");
                Assert.AreEqual(Btn_Difficulty.ChosenLevelSize, level.fontSize, "고름 44");
                continue;
            }

            Assert.IsFalse(button.IsChosen, $"{button.Difficulty}: 고르지 않음");
            Assert.IsFalse(topLine.gameObject.activeSelf, $"{button.Difficulty}: 줄 없음");
            Assert.AreEqual(Btn_Difficulty.NormalLevelSize, level.fontSize, $"{button.Difficulty}: 안 고름 34");

            ChartData official = ChartRepository.GetCharts(TestCharts.OfficialSongId, ChartType.Official).Find(c => c.difficulty == button.Difficulty);
            Assert.AreEqual(official != null, button.Chart != null, $"{button.Difficulty}: 채보 유무 = 공식 채보 파일");
            Color expected = official != null ? theme.GetDifficulty(button.Difficulty) : theme.textMuted;
            AssertColor(UITheme.WithAlpha(expected, 0.07f), button.TintColor, $"{button.Difficulty}: 7% 칠");
            AssertColor(expected, level.color, $"{button.Difficulty}: 레벨 색");
            Assert.AreEqual(official != null, button.Button.interactable, $"{button.Difficulty}: 누를 수 있음 = 채보 있음");
            Assert.AreEqual(official != null ? official.level.ToString() : GameConstants.SongSelect.EmptyValue, level.text, $"{button.Difficulty}: 레벨");
            if (official != null) continue;
            Assert.IsFalse(button.NotPlayedText.gameObject.activeSelf, $"{button.Difficulty}: 채보가 없으면 NOT PLAYED 없음");
            Assert.IsFalse(button.RankImage.gameObject.activeSelf, $"{button.Difficulty}: 채보가 없으면 랭크 없음");
        }
        Debug.Log($"[DifficultyColorTest] D3 곡 선택: {string.Join(" / ", log)}");
    }
}
