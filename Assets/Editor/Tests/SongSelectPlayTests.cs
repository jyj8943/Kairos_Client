using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 곡 선택 화면 개편 실제 씬 확인 (기획서 3-3 곡 선택 화면 개편 11/17, EnterPlayMode 방식).
/// - S1 마지막 선택이 Critical Velocity NORMAL이면 들어오자마자 그 곡·난이도 선택, 배경 RawImage에 텍스처가 있음 (처음은 페이드 없이)
/// - S2 곡을 바꾸면 크로스페이드가 끝난 뒤 앞 RawImage만 보임, Enter → 인게임으로 넘어감 (곡 로딩 확인까지만)
/// 마지막 선택은 메모리에서만 바꾸고 끝나면 되돌린다. S2는 Enter로 일반 플레이에 들어가 PlayerData.json에 마지막 선택을 저장하므로
/// 끝나면 원래 값으로 되돌려 다시 저장한다.
/// </summary>
public class SongSelectPlayTests
{
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;
    private LastSelectionSaveData savedSelection;

    [SetUp]
    public void SetUp()
    {
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
    }

    [TearDown]
    public void TearDown()
    {
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
    }

    /// <summary>마우스 포인터가 Game 뷰의 목록 위에 있으면 hover(UISelectOnHover)로 다른 곡이 골라진다. 테스트 동안 마우스를 끈다</summary>
    private static void DisableMouse()
    {
        if (Mouse.current != null) InputSystem.DisableDevice(Mouse.current);
    }

    private static void EnableMouse()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Mouse && !device.enabled) InputSystem.EnableDevice(device);
        }
    }

    [UnityTest]
    public IEnumerator S1_LastSelectionOfficialNormal_ChosenOnEnter_BackgroundShown()
    {
        yield return new EnterPlayMode();
        DisableMouse();
        yield return WaitManagers();
        SetLastSelection(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official);
        try
        {
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
            Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();

            Assert.AreEqual(ChartType.Official, songSelect.CurrentTab);
            Assert.AreEqual(TestCharts.OfficialSongId, songSelect.ChosenSongId, "마지막으로 플레이한 곡");
            Assert.AreEqual(TestCharts.OfficialChartId, songSelect.ChosenChart.chartId, "마지막으로 플레이한 난이도");

            UIBlurBackground blur = songSelect.BlurBackground;
            Assert.IsNotNull(blur.FrontImage.texture, "배경 RawImage에 텍스처");
            Assert.IsTrue(blur.FrontImage.texture is RenderTexture, "블러 결과 RenderTexture");
            SongData song = GameManager.Songs.Songs[IndexOfSong(TestCharts.OfficialSongId)];
            Assert.AreSame(song.Background.texture, blur.Source, "고른 곡의 1920×1080 일러스트");
            Assert.AreEqual(1f, blur.FrontImage.color.a, 1e-4f, "처음은 페이드 없이 바로");
            Assert.IsFalse(blur.IsFading);
            Debug.Log($"[SongSelectPlayTest] S1 시작 선택 {songSelect.ChosenSongId}/{songSelect.ChosenChart.chartId}, 블러 {blur.LastProcessMilliseconds:0.00}ms, 결과 {blur.ResultSize.x}×{blur.ResultSize.y}");
        }
        finally
        {
            RestoreLastSelection(false);
            EnableMouse();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator S2_SongChange_CrossfadeEndsWithFrontOnly_EnterLoadsInGame()
    {
        yield return new EnterPlayMode();
        PrepareInput();
        DisableMouse();
        yield return WaitManagers();
        SetLastSelection(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official);
        try
        {
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
            Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
            UIBlurBackground blur = songSelect.BlurBackground;
            Assert.Greater(songSelect.SongItems.Count, 1, "곡이 2곡 이상 있어야 곡을 바꿀 수 있다");

            // 다른 곡으로 (↑↓ 중 갈 수 있는 쪽) → 크로스페이드
            int index = IndexOfItem(songSelect, TestCharts.OfficialSongId);
            Key away = index < songSelect.SongItems.Count - 1 ? Key.DownArrow : Key.UpArrow;
            Key back = away == Key.DownArrow ? Key.UpArrow : Key.DownArrow;
            int processed = blur.ProcessCount;
            yield return Tap(away);
            Assert.AreNotEqual(TestCharts.OfficialSongId, songSelect.ChosenSongId, "곡이 바뀜");
            Assert.AreEqual(processed + 1, blur.ProcessCount, "곡을 바꿀 때 한 번 처리");
            Assert.IsTrue(blur.IsFading || blur.BackImage.enabled, "크로스페이드 시작");
            yield return WaitFadeEnd(blur);
            AssertFrontOnly(blur, "다른 곡");

            // 원래 곡으로 돌아와 Enter (공식 채보는 Critical Velocity NORMAL만 쓴다)
            yield return Tap(back);
            Assert.AreEqual(TestCharts.OfficialSongId, songSelect.ChosenSongId);
            Assert.AreEqual(TestCharts.OfficialChartId, songSelect.ChosenChart.chartId, "Critical Velocity는 NORMAL만 있어 그것이 골라짐");
            yield return WaitFadeEnd(blur);
            AssertFrontOnly(blur, "원래 곡");
            Debug.Log($"[SongSelectPlayTest] S2 곡 바꿀 때 블러 {blur.LastProcessMilliseconds:0.00}ms, 처리 {blur.ProcessCount}번, RenderTexture {blur.CreatedTextureCount}장");

            yield return Tap(Key.Enter);
            yield return WaitScene(GameConstants.Scenes.InGame);
            Assert.IsNotNull(Object.FindFirstObjectByType<InGameScene>(), "인게임으로 넘어감");
            Assert.AreEqual(TestCharts.OfficialChartId, GameManager.PlayerData.LastSelection.chartId, "일반 플레이로 들어가면 마지막 선택 저장");
        }
        finally
        {
            ReleaseAll();
            RestoreLastSelection(true);
            EnableMouse();
        }
        yield return new ExitPlayMode();
    }

    // ───── 마지막 선택 (메모리에서 바꾸고 되돌림) ─────

    private void SetLastSelection(string songId, string chartId, ChartType type)
    {
        LastSelectionSaveData last = GameManager.PlayerData.LastSelection;
        savedSelection = new LastSelectionSaveData { scrollMultiplier = last.scrollMultiplier, songId = last.songId, chartId = last.chartId, chartType = last.chartType };
        last.songId = songId;
        last.chartId = chartId;
        last.chartType = type.ToString();
    }

    /// <param name="save">true면 되돌린 값을 파일에 다시 저장한다 (테스트 중에 저장됐을 때)</param>
    private void RestoreLastSelection(bool save)
    {
        if (savedSelection == null || !PlayerDataManager.HasInstance) return;
        LastSelectionSaveData last = GameManager.PlayerData.LastSelection;
        last.scrollMultiplier = savedSelection.scrollMultiplier;
        last.songId = savedSelection.songId;
        last.chartId = savedSelection.chartId;
        last.chartType = savedSelection.chartType;
        if (save) GameManager.PlayerData.Save();
        savedSelection = null;
    }

    // ───── 확인 도우미 ─────

    private static int IndexOfSong(string songId)
    {
        for (int i = 0; i < GameManager.Songs.Songs.Count; i++)
        {
            if (GameManager.Songs.Songs[i].SongId == songId) return i;
        }
        Assert.Fail($"곡이 없습니다: {songId}");
        return -1;
    }

    private static int IndexOfItem(Canvas_SongSelect songSelect, string songId)
    {
        for (int i = 0; i < songSelect.SongItems.Count; i++)
        {
            if (songSelect.SongItems[i].Song.SongId == songId) return i;
        }
        Assert.Fail($"곡 항목이 없습니다: {songId}");
        return -1;
    }

    private static IEnumerator WaitFadeEnd(UIBlurBackground blur)
    {
        float deadline = Time.realtimeSinceStartup + GameConstants.SongSelect.BackgroundFadeSeconds + TestSpeed.MinDeadlineSeconds;
        while (blur.IsFading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "크로스페이드가 끝나지 않음");
            yield return null;
        }
        yield return null;
    }

    private static void AssertFrontOnly(UIBlurBackground blur, string label)
    {
        Assert.IsTrue(blur.FrontImage.enabled, $"{label}: 앞 RawImage 켜짐");
        Assert.AreEqual(1f, blur.FrontImage.color.a, 1e-4f, $"{label}: 앞 RawImage 불투명");
        Assert.IsFalse(blur.BackImage.enabled && blur.BackImage.color.a > 0f, $"{label}: 뒤 RawImage 안 보임");
    }

    // ───── 입력 ─────

    private static void PrepareInput()
    {
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    private static Keyboard Board
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            Assert.IsNotNull(keyboard, "키보드 장치가 없습니다");
            return keyboard;
        }
    }

    private static void Press(Key key)
    {
        InputSystem.QueueStateEvent(Board, new KeyboardState(key));
        InputSystem.Update();
    }

    private static void ReleaseAll()
    {
        if (Keyboard.current == null) return;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
    }

    private static IEnumerator Tap(Key key)
    {
        Press(key);
        yield return null;
        ReleaseAll();
        yield return null;
    }

    // ───── 씬 ─────

    private static IEnumerator WaitManagers()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
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
