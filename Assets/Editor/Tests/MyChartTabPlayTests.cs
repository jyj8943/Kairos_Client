using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 에디터 진입을 곡 선택 나만의 채보 탭으로 합침 (기획서 3-3 · 3-7 ④ · 3-1, 11/18) PlayMode 확인 (EnterPlayMode 방식, 짧게).
/// - M1 타이틀 에디터 메뉴 → 곡 선택 나만의 채보 탭 (첫 정지점), 배속은 저장된 마지막 배속
/// - M2 채보 줄에서 E → 에디터 (그 채보) → 나가기 → 같은 채보가 골라진 곡 선택
/// - M3 + 줄 Enter → 새 채보 팝업 (빈 이름 오류 → 이름 고치면 사라짐) → 만들기 → 에디터 → 저장하지 않고 나가기 → 그 곡의 + 줄이 골라짐
/// - M4 팝업이 열려 있으면 E · Tab · 1 · 2가 동작하지 않음
/// - M5 (실제 키 상태) + 줄에서 Enter 누름 → 팝업, 같은 Enter의 결정은 무시(에디터로 안 감) → 뗌 → 이름을 지우고 Enter → 오류 → 이름 넣고 Enter → 에디터
///   TMP 입력칸은 Enter를 IMGUI 키 이벤트로 받아 키 상태 시뮬레이션으로는 onSubmit이 생기지 않으므로, TMP가 부르는 onSubmit을 직접 불러 대신한다.
/// 테스트용 나만의 채보는 공식 곡(Critical Velocity)에 my_test_mytab_* 이름으로 만들고 끝나면 지운다. 기록(PlayerData.json)은 남기지 않는다.
/// </summary>
public class MyChartTabPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string TestChartId = "my_test_mytab_edit";

    private string chartPath;

    [SetUp]
    public void SetUp()
    {
        chartPath = null;
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(chartPath) && File.Exists(chartPath)) File.Delete(chartPath);
    }

    [UnityTest]
    public IEnumerator M1_TitleEditor_OpensMyChartTab()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return LoadAndWait(GameConstants.Scenes.Title, null);
        float saved = GameManager.PlayerData.LastScrollMultiplier;

        Canvas_Title title = Object.FindFirstObjectByType<Canvas_Title>();
        typeof(Canvas_Title).GetMethod("OnClickEditor", Private).Invoke(title, null);
        yield return WaitScene(GameConstants.Scenes.SongSelect);

        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.AreEqual(ChartType.Custom, songSelect.CurrentTab, "에디터 메뉴 → 나만의 채보 탭");
        Assert.AreEqual(0, StopIndex(songSelect), "첫 정지점");
        Assert.AreEqual(saved, songSelect.ScrollMultiplier, 1e-4f, "진입 데이터가 저장된 배속을 덮어쓰지 않음");
        Assert.AreEqual(songSelect.SongItems.Count, songSelect.SongGroupHeaders.Count, "모든 곡이 묶음으로 나온다");
        StringAssert.StartsWith("곡 01 / ", songSelect.MyChartCountText.text);
        Assert.IsTrue(songSelect.NewChartRow.gameObject.activeSelf, "고른 곡 묶음에 + 줄");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator M2_EditKey_OpensEditor_ExitReturnsWithSameChart()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return WaitManagers();
        SaveTestChart();
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = TestCharts.OfficialSongId, chartType = ChartType.Custom, chartId = TestChartId
        });

        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.AreEqual(TestChartId, songSelect.ChosenChart.chartId, "진입 데이터의 채보가 골라짐");
        Assert.IsTrue(songSelect.KeyGuide.Describe().Exists(line => line.EndsWith(GameConstants.KeyGuide.Edit)), "채보 줄이면 E 편집 안내");

        int starts = InGameLauncher.EditorStartCount;
        Assert.IsTrue(songSelect.EditChosenChart(), "E → 에디터");
        Assert.AreEqual(starts + 1, InGameLauncher.EditorStartCount, "인게임과 같은 진입 함수");
        Assert.AreEqual(TestChartId, InGameLauncher.LastEditorEntry.chartId);
        yield return WaitScene(GameConstants.Scenes.Editor);

        yield return ExitEditor();
        songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.AreEqual(ChartType.Custom, songSelect.CurrentTab);
        Assert.AreEqual(TestChartId, songSelect.ChosenChart != null ? songSelect.ChosenChart.chartId : null, "나가면 편집하던 채보가 골라짐");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator M3_AddRowEnter_Popup_Create_Editor_ExitReturnsToAddRow()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return WaitManagers();
        SaveTestChart();
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = TestCharts.OfficialSongId, chartType = ChartType.Custom, chartId = TestChartId
        });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();

        // ↓로 그 곡의 + 줄까지 (채보 줄 → … → + 줄)
        int group = songSelect.ChosenStop.Group;
        int addRow = songSelect.MyChartNavigator.AddRowOfGroup(group);
        for (int guard = 0; StopIndex(songSelect) != addRow && guard < 20; guard++)
        {
            typeof(Canvas_SongSelect).GetMethod("MoveMyChartVertical", Private).Invoke(songSelect, new object[] { 1 });
            yield return null;
        }
        Assert.IsTrue(songSelect.IsNewChartRowChosen, "+ 줄을 고름");
        Assert.IsTrue(songSelect.NewChartRow.IsChosen && songSelect.NewChartRow.Hint.activeSelf, "고른 + 줄: 높이 60 · 힌트");
        Assert.IsTrue(songSelect.IsVisibleInList((RectTransform)songSelect.NewChartRow.transform), "+ 줄이 보이게 스크롤");
        Assert.IsTrue(songSelect.IsNewChartCardShown, "왼쪽 점선 새 채보 카드");
        Assert.AreEqual(GameConstants.SongSelect.NewChartRecordAlpha, songSelect.RecordCardAlpha, 1e-4f, "최고 기록 카드 흐리게");
        Assert.AreEqual(GameConstants.SongSelect.NewChartStartLabel, songSelect.StartLabel);
        Assert.IsFalse(songSelect.KeyGuide.Describe().Exists(line => line.EndsWith(GameConstants.KeyGuide.Edit)), "+ 줄에서는 E 편집 안내 없음");

        // Enter → 새 채보 팝업
        typeof(Canvas_SongSelect).GetMethod("OnStartKey", Private).Invoke(songSelect, new object[] { default(InputAction.CallbackContext) });
        yield return null;
        Popup_NewChart popup = songSelect.NewChartPopup;
        Assert.AreEqual(popup, GameManager.UI.TopPopup, "+ 줄 Enter → 새 채보 팝업");
        Assert.AreEqual(TestCharts.OfficialSongId, popup.Song.SongId);
        Assert.AreEqual(GameConstants.SongSelect.NewChartDefaultLevel, popup.Level);

        popup.ChartNameInput.text = "   ";
        popup.Create();
        Assert.IsTrue(popup.HasError, "공백만 → 오류");
        Assert.AreEqual(GameConstants.SongSelect.NewChartNameEmpty, popup.ErrorText.text);
        popup.ChartNameInput.text = "팝업 테스트 채보";
        Assert.IsFalse(popup.HasError, "이름을 고치면 오류가 사라짐");

        int starts = InGameLauncher.EditorStartCount;
        popup.Create();
        Assert.AreEqual(starts + 1, InGameLauncher.EditorStartCount, "만들기 → 인게임과 같은 진입 함수로 에디터");
        Assert.IsNull(InGameLauncher.LastEditorEntry.chartId, "새 채보");
        Assert.AreEqual("팝업 테스트 채보", InGameLauncher.LastEditorEntry.chartName);
        yield return WaitScene(GameConstants.Scenes.Editor);

        yield return ExitEditor();
        songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.IsTrue(songSelect.IsNewChartRowChosen, "저장하지 않고 나가면 그 곡의 + 줄");
        Assert.AreEqual(TestCharts.OfficialSongId, songSelect.ChosenSongId);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator M4_PopupOpen_BlocksEditTabSpeed()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return WaitManagers();
        SaveTestChart();
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Custom });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        Assert.IsTrue(songSelect.IsNewChartRowChosen, "chartId 없음 → 그 곡의 + 줄");

        typeof(Canvas_SongSelect).GetMethod("OnStartKey", Private).Invoke(songSelect, new object[] { default(InputAction.CallbackContext) });
        yield return null;
        Assert.IsTrue(GameManager.UI.HasOpenPopup);

        float speed = songSelect.ScrollMultiplier;
        object context = default(InputAction.CallbackContext);
        typeof(Canvas_SongSelect).GetMethod("OnSwitchTab", Private).Invoke(songSelect, new[] { context });
        typeof(Canvas_SongSelect).GetMethod("OnSpeedUpStarted", Private).Invoke(songSelect, new[] { context });
        typeof(Canvas_SongSelect).GetMethod("OnSpeedUpCanceled", Private).Invoke(songSelect, new[] { context });
        typeof(Canvas_SongSelect).GetMethod("OnSpeedDownStarted", Private).Invoke(songSelect, new[] { context });
        typeof(Canvas_SongSelect).GetMethod("OnSpeedDownCanceled", Private).Invoke(songSelect, new[] { context });
        int starts = InGameLauncher.EditorStartCount;
        typeof(Canvas_SongSelect).GetMethod("OnEditKey", Private).Invoke(songSelect, new[] { context });
        yield return null;

        Assert.AreEqual(ChartType.Custom, songSelect.CurrentTab, "Tab 무시");
        Assert.AreEqual(speed, songSelect.ScrollMultiplier, 1e-4f, "1 · 2 무시");
        Assert.AreEqual(starts, InGameLauncher.EditorStartCount, "E 무시");
        Assert.AreEqual(songSelect.NewChartPopup, GameManager.UI.TopPopup, "팝업 그대로");

        // ESC → 닫고 + 줄을 다시 고른다
        GameManager.UI.TopPopup.OnCancelRequested();
        yield return null;
        Assert.IsFalse(GameManager.UI.HasOpenPopup);
        Assert.IsTrue(songSelect.IsNewChartRowChosen);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator M5_EnterThatOpenedPopup_DoesNotCreate_UntilReleased()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSettings.EditorInputBehaviorInPlayMode savedEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSettings.BackgroundBehavior savedBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Custom });
            Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
            Assert.IsTrue(songSelect.IsNewChartRowChosen, "+ 줄");
            int starts = InGameLauncher.EditorStartCount;

            // Enter 누름 (SongSelect/Start) → 팝업. 같은 Enter로 TMP가 onSubmit을 불러도 만들지 않는다
            Press(Key.Enter);
            yield return null;
            Popup_NewChart popup = songSelect.NewChartPopup;
            Assert.AreEqual(popup, GameManager.UI.TopPopup, "Enter → 새 채보 팝업");
            Assert.IsTrue(popup.IsWaitingOpenKeyRelease, "연 Enter를 아직 떼지 않음");
            popup.ChartNameInput.onSubmit.Invoke(popup.ChartNameInput.text);
            yield return null;
            yield return null;
            Assert.AreEqual(starts, InGameLauncher.EditorStartCount, "팝업을 연 Enter로는 만들지 않는다");
            Assert.IsTrue(popup.IsOpen, "팝업이 떠 있다");
            Assert.AreEqual(popup.ChartNameInput.gameObject, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "입력칸 선택 유지");

            // 뗌 → 이제 Enter로 결정
            ReleaseAll();
            yield return null;
            Assert.IsFalse(popup.IsWaitingOpenKeyRelease, "Enter를 떼면 결정 가능");

            popup.ChartNameInput.text = "";
            popup.ChartNameInput.onSubmit.Invoke("");
            yield return null;
            Assert.IsTrue(popup.HasError, "이름을 지우고 Enter → 오류");
            Assert.AreEqual(starts, InGameLauncher.EditorStartCount, "오류면 넘어가지 않는다");

            popup.ChartNameInput.text = "Enter 테스트";
            Press(Key.Enter);
            popup.ChartNameInput.onSubmit.Invoke(popup.ChartNameInput.text);
            ReleaseAll();
            Assert.AreEqual(starts + 1, InGameLauncher.EditorStartCount, "이름을 넣고 Enter → 에디터");
            yield return WaitScene(GameConstants.Scenes.Editor);
        }
        finally
        {
            ReleaseAll();
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditor;
            InputSystem.settings.backgroundBehavior = savedBackground;
        }
        yield return new ExitPlayMode();
    }

    // ───── 도우미 ─────

    private static void Press(Key key)
    {
        Assert.IsNotNull(Keyboard.current, "키보드 장치가 없습니다");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
    }

    private static void ReleaseAll()
    {
        if (Keyboard.current == null) return;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
    }

    private static int StopIndex(Canvas_SongSelect songSelect)
    {
        return (int)typeof(Canvas_SongSelect).GetField("chosenStop", Private).GetValue(songSelect);
    }

    /// <summary>테스트용 나만의 채보 (공식 곡, 노트 하나)를 저장한다. 끝나면 TearDown이 지운다</summary>
    private void SaveTestChart()
    {
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            chartId = TestChartId,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = "탭 합치기 테스트",
            keyCount = GameConstants.Editor.LaneCount,
            level = 3,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 8f, lane = 1, type = NoteData.TypeShort } }
        };
        Assert.IsTrue(ChartRepository.TrySave(chart, out string error), error);
        chartPath = ChartRepository.GetChartPath(chart.songId, chart.chartId, ChartType.Custom);
    }

    /// <summary>에디터에서 나가기 (저장하지 않은 변경이 있으면 확인 팝업의 나가기)</summary>
    private static IEnumerator ExitEditor()
    {
        EditorScene editor = Object.FindFirstObjectByType<EditorScene>();
        Assert.IsNotNull(editor);
        typeof(EditorScene).GetMethod("OnExitRequested", Private).Invoke(editor, null);
        yield return null;
        if (GameManager.UI.TopPopup is Popup_Confirm confirm) confirm.ConfirmButton.onClick.Invoke();
        yield return WaitScene(GameConstants.Scenes.SongSelect);
    }

    private static IEnumerator WaitManagers()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        yield return WaitManagers();
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        yield return WaitScene(sceneName);
    }

    private static IEnumerator WaitScene(string sceneName)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }
}
