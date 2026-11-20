using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 곡 로딩 오버레이 (기획서 3-8 「곡 로딩」) 실제 흐름 확인. 이 테스트만 GameSceneManager.SkipLoadingOverlayForTests를 끄고 오버레이 경로를 탄다.
/// - L1 곡 선택 Enter → 곡 로딩 → 인게임: 단계 순서(열기 연출 1.1초가 끝난 뒤 곡 선택 OnExit · 로드, 정리는 닫기 전, 닫기 시작은 열기 끝 + 2초 뒤, OnEnter와 첫 클릭은 닫기 끝 뒤),
///   로딩 중 ESC · Enter · ↓를 눌러도 아무 일도 없음 (곡 선택 선택 그대로, 팝업 · 일시정지 없음, 시작 한 번)
/// - L2 결과 다시 하기 → 같은 곡 로딩 (결과 위에 덮고 0.35초 뒤 결과 OnExit)
/// - L3 카드 네 상태(공식 / 나만의 채보 / AP 기록 / 기록 없음) + 긴 제목 · 긴 채보 이름: 보이는 오브젝트, Canvas 안, 넘침, 곡 선택 앨범과 같은 자리
/// 일반 플레이로 들어가지만 마지막 배속은 지금 저장된 값 그대로 쓰고, 결과는 자동 플레이로 표시해 기록을 남기지 않는다.
/// </summary>
public class SongLoadingPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private bool savedSkip;
    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedSkip = GameSceneManager.SkipLoadingOverlayForTests;
        GameSceneManager.SkipLoadingOverlayForTests = false;
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
        GameSceneManager.SkipLoadingOverlayForTests = savedSkip;
        yield return new ExitPlayMode();
    }

    private static T Get<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (T)info.GetValue(target);
    }

    private static void Call(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method, Private);
        Assert.IsNotNull(info, $"메서드 {method}가 없습니다");
        info.Invoke(target, null);
    }

    private static float LastSpeed => PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName || GameManager.Scene.Current == null || !GameManager.Scene.Current.IsEntered)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
    }

    private static IEnumerator Tap(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private static IEnumerator TapBlockedKeys()
    {
        yield return Tap(Key.DownArrow);
        yield return Tap(Key.Enter);
        yield return Tap(Key.Escape);
    }

    /// <summary>
    /// 곡 로딩이 끝나 인게임 OnEnter까지 기다리며 입력 차단을 확인하고, 첫 카운트인 클릭 · 노래 0박 시각을 단계 기록에 더한다.
    /// </summary>
    private IEnumerator FollowSongLoading(string fromScene, GameObject fromSelected, StringBuilder log)
    {
        int starts = InGameLauncher.StartCount;
        float deadline = Time.realtimeSinceStartup + 20f;
        Canvas_SongLoading overlay = GameManager.UI.SongLoading;
        Assert.IsTrue(GameManager.Scene.IsSongLoading, "곡 로딩 시작");
        Assert.IsTrue(GameManager.Scene.IsLoading, "로딩 중 = 씬 로딩 중 (ESC 무시)");
        Assert.IsTrue(overlay.IsShown);

        // 열기 연출 중 (앞 씬이 아직 있음): ↓ · Enter · ESC
        while (overlay.OpenElapsed < 0.1f) yield return null;
        Assert.AreEqual(fromScene, GameManager.Scene.Current.gameObject.scene.name, "열기 연출 중에는 앞 씬 그대로");
        yield return TapBlockedKeys();
        Assert.AreEqual(fromSelected, EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null, $"{fromScene} 선택 그대로 (방향키 무시)");
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업 안 열림");
        Assert.AreEqual(starts, InGameLauncher.StartCount, "Enter로 다시 시작하지 않음");
        log.AppendLine($"  열기 중 ↓·Enter·ESC ({overlay.OpenElapsed:0.00}초): 선택 그대로, 팝업 없음, 시작 횟수 그대로");

        // 인게임 Init 뒤 · OnEnter 전: ESC · Enter · ↓
        InGameScene inGame = null;
        while (inGame == null || !inGame.IsInitDone)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 Init 대기 시간 초과");
            inGame = Object.FindFirstObjectByType<InGameScene>();
            yield return null;
        }
        Assert.IsFalse(inGame.IsEntered, "닫기 전에는 OnEnter 안 함");
        Assert.IsFalse(Get<bool>(inGame, "isPlaying"), "판 시작 전");
        yield return TapBlockedKeys();
        Assert.IsFalse(Get<bool>(inGame, "isPaused"), "ESC로 일시정지 안 됨");
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "팝업 안 열림");
        log.AppendLine($"  인게임 Init 뒤 ↓·Enter·ESC ({overlay.OpenElapsed:0.00}초): 일시정지 · 팝업 없음, 판 시작 전");

        while (GameManager.Scene.IsSongLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "곡 로딩 끝 대기 시간 초과");
            if (!overlay.IsClosing) Assert.IsFalse(Get<bool>(inGame, "isPlaying"), "닫기 끝 전에는 판 시작 안 함");
            yield return null;
        }
        Assert.IsTrue(inGame.IsEntered, "닫기 끝 뒤 OnEnter");
        Assert.IsFalse(overlay.IsShown, "오버레이 숨김");
        Assert.AreEqual(starts, InGameLauncher.StartCount);

        IReadOnlyList<double> clicks = inGame.CountInClickSongTimes;
        Assert.Greater(clicks.Count, 0, "시작 메트로놈 클릭 있음");
        bool clicked = false;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "노래 0박 대기 시간 초과");
            double songTime = GameManager.Sound.SongTime;
            if (!clicked && songTime >= clicks[0]) { GameManager.Scene.MarkLoading("첫 카운트인 클릭 (이 프레임에 지남)"); clicked = true; }
            if (songTime >= 0) { GameManager.Scene.MarkLoading("노래 0박"); break; }
            yield return null;
        }
    }

    /// <summary>이름이 prefix로 시작하는 첫 단계의 시각 (단계 이름 뒤에 걸린 ms가 붙어 있다)</summary>
    private static double At(IReadOnlyList<LoadingStep> steps, string prefix)
    {
        foreach (LoadingStep step in steps)
            if (step.Name.StartsWith(prefix)) return step.Seconds;
        Assert.Fail($"단계 '{prefix}'가 없습니다");
        return 0;
    }

    /// <summary>단계 시각 표를 만들고 순서 조건을 확인한다</summary>
    private static void CheckSteps(string fromScene, StringBuilder log)
    {
        IReadOnlyList<LoadingStep> steps = GameManager.Scene.LoadingSteps;
        log.AppendLine("  단계 | Enter부터 (초)");
        foreach (LoadingStep step in steps) log.AppendLine($"  {step.Name} | {step.Seconds:0.000}");
        double openZero = At(steps, "열기 연출 0초");
        double openEnd = At(steps, "열기 끝");
        double exit = At(steps, $"{fromScene} OnExit");
        double loadStart = At(steps, $"{GameConstants.Scenes.InGame} 로드 시작");
        double init = At(steps, $"{GameConstants.Scenes.InGame} Init 끝");
        double cleanup = At(steps, "정리 끝");
        double closeStart = At(steps, "닫기 시작");
        double closeEnd = At(steps, "닫기 끝");
        double enter = At(steps, $"{GameConstants.Scenes.InGame} OnEnter");
        double click = At(steps, "첫 카운트인 클릭");
        double zero = At(steps, "노래 0박");
        Assert.Greater(openZero, 0.0, "열기 연출 0초는 Enter 다음 프레임");
        Assert.GreaterOrEqual(openEnd - openZero, GameConstants.SongLoading.OpenSeconds - 1e-3, "열기 연출 1.1초");
        Assert.GreaterOrEqual(exit, openEnd, "앞 씬 OnExit는 열기 끝 뒤");
        Assert.GreaterOrEqual(loadStart, openEnd, "인게임 로드 시작은 열기 끝 뒤");
        Assert.GreaterOrEqual(loadStart, exit, "인게임 로드는 OnExit 뒤");
        Assert.GreaterOrEqual(init, loadStart);
        Assert.GreaterOrEqual(cleanup, init, "정리(UnloadUnusedAssets · GC)는 Init 뒤");
        Assert.GreaterOrEqual(closeStart, cleanup, "정리는 닫기 전에 끝남");
        Assert.GreaterOrEqual(closeStart - openEnd, GameConstants.SongLoading.MinLoadSeconds - 1e-3, "닫기 시작은 열기 끝 + 2초 이후");
        Assert.GreaterOrEqual(closeStart, init, "닫기 시작은 인게임 Init 뒤");
        foreach (LoadingFrameStats stats in GameManager.Scene.LoadingFrames)
            log.AppendLine($"  프레임 [{stats.Phase}] {stats.FrameCount}개, 최대 {stats.MaxMs:0.0}ms, 33ms 넘음 {stats.SlowCount}개");
        Assert.GreaterOrEqual(closeEnd - closeStart, GameConstants.SongLoading.CloseSeconds - 0.02, "닫기 0.64초");
        Assert.GreaterOrEqual(enter, closeEnd, "OnEnter는 닫기 끝 뒤");
        Assert.Greater(click, closeEnd, "첫 클릭은 닫기 끝 뒤");
        Assert.Greater(zero, click, "노래 0박은 첫 클릭 뒤");
    }

    [UnityTest]
    public IEnumerator L1_SongSelectEnter_SongLoading_InGame()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
        });
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        Call(songSelect, "StartGame");
        StringBuilder log = new StringBuilder($"[SongLoadingPlayTest] L1 곡 선택 Enter → 곡 로딩 → 인게임 (재생 배율 x{TestSpeed.CurrentRate}, 오버레이는 실제 시간)\n");
        yield return FollowSongLoading(GameConstants.Scenes.SongSelect, selected, log);
        CheckSteps(GameConstants.Scenes.SongSelect, log);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator L2_ResultRetry_SongLoading_InGame()
    {
        ScoreCalculator score = new ScoreCalculator(1);
        score.Add(JudgeType.Perfect);
        ResultData result = ResultData.FromScore(score);
        result.songId = Song;
        result.songTitle = "Critical Velocity";
        result.chartType = ChartType.Official;
        result.difficulty = GameConstants.Chart.DifficultyNormal;
        result.level = 7;
        result.isAutoPlay = true; // 기록을 남기지 않게
        result.retryEntry = InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, LastSpeed);
        result.returnTarget = new ResultReturnTarget { sceneName = GameConstants.Scenes.SongSelect, sceneData = new SongSelectEntryData { songId = Song } };
        yield return LoadAndWait(GameConstants.Scenes.Result, result);
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        Call(Object.FindFirstObjectByType<ResultScene>(), "Retry");
        StringBuilder log = new StringBuilder($"[SongLoadingPlayTest] L2 결과 다시 하기 → 곡 로딩 → 인게임 (재생 배율 x{TestSpeed.CurrentRate})\n");
        yield return FollowSongLoading(GameConstants.Scenes.Result, selected, log);
        CheckSteps(GameConstants.Scenes.Result, log);
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator L3_CardStates_LayoutOverflowAlbum()
    {
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, new SongSelectEntryData
        {
            songId = Song, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId, scrollMultiplier = LastSpeed
        });
        Canvas_SongLoading overlay = GameManager.UI.SongLoading;
        RectTransform root = (RectTransform)overlay.transform;
        Assert.AreEqual(GameConstants.SongLoading.SortOrder, overlay.GetComponent<Canvas>().sortingOrder);
        Assert.Greater(overlay.GetComponent<Canvas>().sortingOrder, GameManager.UI.OverlayCanvas.sortingOrder, "Canvas_Overlay 팝업보다 위");
        Assert.IsFalse(overlay.IsShown, "Init 뒤 숨김");

        InGameEntryData entry = InGameLauncher.CreateNormalEntry(Song, TestCharts.OfficialChartId, ChartType.Official, 5.5f);
        overlay.Open(entry);
        float until = Time.realtimeSinceStartup + GameConstants.SongLoading.OpenSeconds + 0.1f;
        while (Time.realtimeSinceStartup < until) yield return null;

        // 곡 선택 큰 앨범과 같은 자리 · 크기
        RectTransform cover = (RectTransform)Object.FindFirstObjectByType<Canvas_SongSelect>().transform.Find("Img_Cover");
        Rect coverRect = TopLeft(cover, (RectTransform)cover.root);
        Rect albumRect = TopLeft(overlay.AlbumRect, root);
        StringBuilder sb = new StringBuilder("[SongLoadingPlayTest] L3\n");
        sb.AppendLine($"  앨범 비교: 곡 선택 Img_Cover x {coverRect.x:0.0} y {coverRect.y:0.0} {coverRect.width:0.0}×{coverRect.height:0.0} / 곡 로딩 Img_Album x {albumRect.x:0.0} y {albumRect.y:0.0} {albumRect.width:0.0}×{albumRect.height:0.0}");
        Assert.AreEqual(coverRect.x, albumRect.x, 0.5f, "앨범 x");
        Assert.AreEqual(coverRect.y, albumRect.y, 0.5f, "앨범 y");
        Assert.AreEqual(coverRect.width, albumRect.width, 0.5f);
        Assert.AreEqual(coverRect.height, albumRect.height, 0.5f);

        SongData song = GameManager.Songs.Find(Song);
        ChartData official = new ChartData { chartId = "x", type = ChartData.TypeOfficial, difficulty = "NORMAL", level = 7 };
        ChartData custom = new ChartData { chartId = "x", type = ChartData.TypeCustom, chartName = "내가 만든 첫 채보", level = 6 };
        ChartRecordData a = new ChartRecordData { chartId = "x", bestScore = 948210, bestRank = "A", maxCombo = 812 };
        ChartRecordData ap = new ChartRecordData { chartId = "x", bestScore = 1000000, bestRank = "AP", maxCombo = 329, isAllPerfect = true, isFullCombo = true };
        InGameEntryData customEntry = InGameLauncher.CreateNormalEntry(Song, "x", ChartType.Custom, 5.5f);

        sb.AppendLine("  상태 | 뱃지 | CHART 줄 | 점수 | 랭크 이미지 | 빈 칸 | 콤보 | FC | Canvas 안 | isTextOverflowing (제목/채보 이름/말풍선) | 줄임표 잘림 (제목/채보 이름)");
        Report(sb, overlay, root, "01 공식", SongLoadingInfoRules.Build(entry, song, official, a), song);
        Report(sb, overlay, root, "02 나만의 채보", SongLoadingInfoRules.Build(customEntry, song, custom, a), song);
        Report(sb, overlay, root, "03 AP 기록", SongLoadingInfoRules.Build(entry, song, official, ap), song);
        Report(sb, overlay, root, "04 기록 없음", SongLoadingInfoRules.Build(entry, song, official, null), song);

        SongLoadingInfo longInfo = SongLoadingInfoRules.Build(customEntry, song,
            new ChartData { chartId = "x", type = ChartData.TypeCustom, chartName = "아주아주 긴 나만의 채보 이름이 판 끝까지 이어지는 경우를 확인하기 위한 채보 이름입니다 그리고 조금 더 길게 이어 붙인 부분까지", level = 12 }, ap);
        longInfo.Title = "Critical Velocity (Extended Hyper Midnight Drive Remix for the Endless Night)";
        Report(sb, overlay, root, "긴 제목 · 채보 이름", longInfo, song);
        sb.AppendLine($"  긴 글자: 제목 원래 폭 {Preferred(overlay.TitleText):0} > 칸 {overlay.TitleText.rectTransform.rect.width:0}, 보이는 글자 끝 '{VisibleTail(overlay.TitleText)}' · " +
                      $"채보 이름 원래 폭 {Preferred(overlay.ChartNameText):0} > 칸 {overlay.ChartNameText.rectTransform.rect.width:0}, 보이는 글자 끝 '{VisibleTail(overlay.ChartNameText)}'");
        Assert.Greater(Preferred(overlay.TitleText), overlay.TitleText.rectTransform.rect.width, "긴 제목은 칸보다 김");
        Assert.Greater(Preferred(overlay.ChartNameText), overlay.ChartNameText.rectTransform.rect.width, "긴 채보 이름은 칸보다 김");
        Assert.AreEqual('\u2026', VisibleTail(overlay.TitleText), "긴 제목 끝 줄임표");
        Assert.AreEqual('\u2026', VisibleTail(overlay.ChartNameText), "긴 채보 이름 끝 줄임표");
        Assert.AreEqual(TextOverflowModes.Ellipsis, overlay.TitleText.overflowMode);
        Assert.AreEqual(TextOverflowModes.Ellipsis, overlay.ChartNameText.overflowMode);

        Assert.IsNull(overlay.SdImage.sprite, "SD 일러스트 아직 없음");
        Assert.AreEqual(0f, overlay.SdImage.color.a, "스프라이트 없으면 투명");
        yield return overlay.CloseRoutine();
        Assert.IsFalse(overlay.IsShown);
        Assert.IsFalse(overlay.GetComponent<Canvas>().enabled, "닫으면 Canvas 끔");
        Debug.Log(sb.ToString());
    }

    private static void Report(StringBuilder sb, Canvas_SongLoading overlay, RectTransform root, string state, SongLoadingInfo info, SongData song)
    {
        overlay.SetContent(info, song);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in overlay.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();

        bool inside = true;
        List<string> outside = new List<string>();
        foreach (RectTransform r in overlay.GetComponentsInChildren<RectTransform>(false))
        {
            if (r == root || r.name.StartsWith("TMP SubMesh")) continue;
            Rect b = TopLeft(r, root);
            if (b.xMin < -0.5f || b.yMin < -0.5f || b.xMax > root.rect.width + 0.5f || b.yMax > root.rect.height + 0.5f)
            {
                // 배경(화면 덮기 일러스트 · 시계 원 문양)은 화면보다 클 수 있다
                if (r.GetComponentInParent<MenuBackground>() != null || r.name == "Img_ClockPattern") continue;
                inside = false;
                outside.Add(r.name);
            }
        }
        Assert.IsTrue(inside, $"{state}: Canvas 밖 {string.Join(", ", outside)}");
        Assert.IsFalse(overlay.BubbleText.isTextOverflowing, "말풍선 넘침");
        if (!state.StartsWith("긴"))
        {
            Assert.IsFalse(overlay.TitleText.isTextOverflowing || overlay.TitleText.isTextTruncated, state + " 제목 넘침");
            Assert.IsFalse(overlay.ChartNameText.isTextOverflowing || overlay.ChartNameText.isTextTruncated, state + " 채보 이름 넘침");
        }

        Img_Rank rank = overlay.BestRank;
        string rankText = rank.gameObject.activeSelf ? $"{rank.Sprite.name} {rank.Size.x:0}×{rank.Size.y:0}" : "없음";
        string fc = overlay.FullCombo.IsShown ? $"Clear_FC {overlay.FullCombo.Size.x:0}×{overlay.FullCombo.Size.y:0}" : "없음";
        sb.AppendLine($"  {state} | {overlay.DifficultyText.text} | {(overlay.ChartRow.activeSelf ? "보임 '" + Shorten(overlay.ChartNameText.text) + "'" : "숨김")} | {overlay.BestScoreText.text} | {rankText} | " +
                      $"{(overlay.BestRankEmpty.gameObject.activeSelf ? "84×54 보임" : "숨김")} | {overlay.MaxComboText.text} | {fc} | {(inside ? "예" : "아니오")} | " +
                      $"{overlay.TitleText.isTextOverflowing}/{overlay.ChartNameText.isTextOverflowing}/{overlay.BubbleText.isTextOverflowing} | {overlay.TitleText.isTextTruncated}/{overlay.ChartNameText.isTextTruncated}");

        if (state.StartsWith("03"))
        {
            Assert.AreEqual("Rank_AP", rank.Sprite.name);
            Assert.AreEqual(Canvas_SongLoading.ApRankHeight, rank.Size.y, 0.5f, "AP 높이 54");
            Assert.IsTrue(overlay.FullCombo.IsShown, "AP면 FC");
            Assert.AreEqual(50f, overlay.FullCombo.Size.x, 0.5f, "FC 폭 50");
        }
        if (state.StartsWith("04"))
        {
            Assert.IsFalse(rank.gameObject.activeSelf);
            Assert.IsTrue(overlay.BestRankEmpty.gameObject.activeSelf, "빈 칸");
            Assert.AreEqual(GameConstants.SongSelect.EmptyValue, overlay.BestScoreText.text);
        }
        if (state.StartsWith("01")) Assert.AreEqual(56f, rank.Size.y, 0.5f, "랭크 높이 56");
        if (state.StartsWith("02")) Assert.IsTrue(overlay.ChartRow.activeSelf);
        if (state.StartsWith("01")) Assert.IsFalse(overlay.ChartRow.activeSelf);
    }

    private static float Preferred(TMP_Text text) => text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;

    /// <summary>화면에 그려진 마지막 글자 (줄임표면 '\u2026')</summary>
    private static char VisibleTail(TMP_Text text)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        for (int i = info.characterCount - 1; i >= 0; i--)
            if (info.characterInfo[i].isVisible) return info.characterInfo[i].character;
        return '\0';
    }

    private static string Shorten(string text) => text.Length > 12 ? text.Substring(0, 12) + "…" : text;

    /// <summary>Canvas 왼쪽 위 기준 (x, y는 위에서 아래로) 사각형 (Canvas 단위 = 1920×1080 기준 px)</summary>
    private static Rect TopLeft(RectTransform r, RectTransform root)
    {
        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        Vector3 min = root.InverseTransformPoint(c[0]);
        Vector3 max = root.InverseTransformPoint(c[2]);
        Rect rr = root.rect;
        return Rect.MinMaxRect(min.x - rr.xMin, rr.yMax - max.y, max.x - rr.xMin, rr.yMax - min.y);
    }
}
