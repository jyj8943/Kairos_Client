using System.Collections;
using System.Collections.Generic;
using System.IO;
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
/// 곡 선택 개편 (11/25) 실제 씬 배치 확인 (EnterPlayMode 방식). 스크린샷 대신 코드 검사 표를 로그로 남긴다.
/// - L1 공식(기록 없음 / 있음) · 나만의 채보 · 새 채보 생성 세 상태의 카드 자리, Canvas 안, 글자 넘침, 세로 가운데(1px), 기록 유무에 따른 x 위치, BPM 표기 · TIME 위치, 키 안내 · 1 2 배속, 목록 숫자
/// - L2 곡 선택 최대 콤보 = 인게임 MaxPossibleCombo (Critical Velocity NORMAL)
/// 기록·마지막 선택·곡 BPM 방식은 메모리에서만 바꾸고 되돌린다. 나만의 채보는 테스트 채보 하나를 저장했다가 지운다.
/// </summary>
public class SongSelectLayoutPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string TestChartId = "my_test_songselect_layout";
    private const float Tolerance = 1f;

    private InputSettings.EditorInputBehaviorInPlayMode savedEditor;
    private InputSettings.BackgroundBehavior savedBackground;

    [SetUp]
    public void SetUp()
    {
        savedEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
    }

    [TearDown]
    public void TearDown()
    {
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditor;
        InputSystem.settings.backgroundBehavior = savedBackground;
        string path = ChartRepository.GetChartPath(TestCharts.OfficialSongId, TestChartId, ChartType.Custom);
        if (File.Exists(path)) File.Delete(path);
    }

    [UnityTest]
    public IEnumerator L1_Layout_ThreeStates()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        if (Mouse.current != null) InputSystem.DisableDevice(Mouse.current);
        yield return WaitManagers();
        SaveTestChart();

        PlayerRecordBook book = (PlayerRecordBook)typeof(PlayerDataManager).GetField("book", Private).GetValue(GameManager.PlayerData);
        Dictionary<string, ChartRecordData> byId = (Dictionary<string, ChartRecordData>)typeof(PlayerRecordBook).GetField("byChartId", Private).GetValue(book);
        byId.TryGetValue(TestCharts.OfficialChartId, out ChartRecordData savedRecord);
        SongData song = GameManager.Songs.Find(TestCharts.OfficialSongId);
        FieldInfo displayField = typeof(SongData).GetField("bpmDisplay", Private);
        object savedDisplay = displayField.GetValue(song);
        StringBuilder log = new StringBuilder("[SongSelectLayoutTest] L1\n");
        try
        {
            SongSelectEntryData official = new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId };

            // ── 공식 · 기록 없음
            byId.Remove(TestCharts.OfficialChartId);
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, official);
            Canvas_SongSelect view = Object.FindFirstObjectByType<Canvas_SongSelect>();
            Assert.AreEqual(GameConstants.SongSelect.NotPlayed, view.ScoreText);
            Rect tabs = CardRect(view, "DifficultyList", "RecordPanel");
            Dictionary<string, float> emptyX = ValueXs(view);
            CheckAll(view, "공식 · 기록 없음", log);

            // ── 공식 · AP 기록
            byId[TestCharts.OfficialChartId] = new ChartRecordData
            {
                chartId = TestCharts.OfficialChartId, songId = TestCharts.OfficialSongId, chartType = ChartType.Official.ToString(),
                bestScore = GameConstants.Play.MaxScore, bestRank = PlayRank.AP.ToString(), bestAccuracy = 100,
                maxCombo = view.ChosenChart.maxCombo, isFullCombo = true, isAllPerfect = true, playCount = 1
            };
            yield return LoadAndWait(GameConstants.Scenes.SongSelect, official);
            view = Object.FindFirstObjectByType<Canvas_SongSelect>();
            Assert.AreEqual("1000000", view.ScoreText);
            Dictionary<string, float> recordX = ValueXs(view);
            log.Append("| x 위치 | 기록 없음 | 기록 있음 |\n|---|---|---|\n");
            foreach (string key in emptyX.Keys)
            {
                log.Append($"| {key} | {emptyX[key]:0.0} | {recordX[key]:0.0} |\n");
                Assert.AreEqual(emptyX[key], recordX[key], 0.1f, $"{key}: 기록이 있든 없든 같은 x");
            }
            CheckAll(view, "공식 · AP 기록", log);

            // 목록: 고른 곡의 고른 난이도만 밑줄 + 난이도 색, 채보 없는 난이도는 빈칸
            foreach (Btn_SongItem item in view.SongItems)
            {
                foreach (Btn_SongItem.DifficultyColumn column in item.DifficultyColumns)
                {
                    ChartData chart = ChartRepository.GetCharts(item.Song.SongId, ChartType.Official).Find(c => c.difficulty == column.difficulty);
                    bool marked = item.IsChosen && column.difficulty == GameConstants.Chart.DifficultyNormal;
                    Assert.AreEqual(chart != null ? chart.level.ToString() : string.Empty, column.levelText.text, "채보 없는 난이도는 빈칸");
                    Assert.AreEqual(marked, column.underline.gameObject.activeSelf, $"{item.Song.SongId} {column.difficulty}: 밑줄");
                    Assert.AreEqual(item.IsChosen ? 24f : 20f, column.levelText.fontSize, "고른 곡 24, 나머지 20");
                    if (marked) Assert.AreEqual((Color32)UITheme.Current.difficultyNormal, (Color32)column.levelText.color, "난이도 색");
                }
            }

            // BPM: Main / Range, TIME 위치 고정
            RectTransform time = Find(view, "Txt_Time");
            float timeY = Center(time).y;
            List<string> bpmRows = new List<string>();
            foreach (Btn_SongItem item in view.SongItems)
                bpmRows.Add($"| {item.Song.Title} | {item.Song.BpmDisplay} | {item.Song.PromptBpm} | {BpmDisplayRules.Format(item.Song, null)} |");
            string main = view.BpmText;
            displayField.SetValue(song, SongBpmDisplay.Range);
            typeof(Canvas_SongSelect).GetMethod("RefreshBpm", Private).Invoke(view, null);
            Canvas.ForceUpdateCanvases();
            string range = view.BpmText;
            StringAssert.Contains(">~</color>", range, "범위 표기");
            Assert.AreEqual(timeY, Center(time).y, 0.01f, "범위 표기여도 TIME 위치 고정");
            CheckAll(view, "공식 · BPM 범위", log);
            displayField.SetValue(song, savedDisplay);
            typeof(Canvas_SongSelect).GetMethod("RefreshBpm", Private).Invoke(view, null);
            Assert.AreEqual(main, view.BpmText);
            log.Append($"| 곡 | 방식 | promptBpm | 표기 |\n|---|---|---|---|\n{string.Join("\n", bpmRows)}\n| {song.Title} NORMAL 채보 | Main | {song.PromptBpm} | {main} |\n| {song.Title} NORMAL 채보 | Range | {song.PromptBpm} | {range} |\n");

            // 키 안내에 배속이 없고 1 · 2 배속은 그대로
            List<string> guide = view.KeyGuide.Describe();
            Assert.IsFalse(guide.Exists(line => line.Contains(GameConstants.SongSelect.SpeedLabelKo)), string.Join(" / ", guide));
            float before = view.ScrollMultiplier;
            yield return Tap(Key.Digit2);
            float up = view.ScrollMultiplier;
            yield return Tap(Key.Digit1);
            float down = view.ScrollMultiplier;
            Assert.AreEqual(before + 0.1f, up, 1e-3f, "2 = 배속 올림");
            Assert.AreEqual(before, down, 1e-3f, "1 = 배속 내림");
            log.Append($"키 안내: {string.Join(" / ", guide)}\n배속: {before:0.0} → 2 키 {up:0.0} → 1 키 {down:0.0}\n");

            // ── 나만의 채보 · 채보 줄
            yield return LoadAndWait(GameConstants.Scenes.SongSelect,
                new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Custom, chartId = TestChartId });
            view = Object.FindFirstObjectByType<Canvas_SongSelect>();
            Assert.AreEqual(TestChartId, view.ChosenChart.chartId);
            Assert.AreEqual(GameConstants.SongSelect.MyChartHeader, view.DifficultyHeader);
            Assert.IsFalse(view.IsDifficultyKeysShown);
            Rect header = CardRect(view, "MyChartHeader", "RecordPanel");
            Assert.AreEqual(30f, Find(view, "DifficultyRow").rect.height, 0.01f, "난이도 줄 30 고정");
            Assert.AreEqual((Color32)UITheme.WithAlpha(UITheme.Current.difficultyCustom, 0.16f), (Color32)view.RecordTintColor, "기록 패널 CUSTOM 보라 16%");
            UIMarqueeText name = Find(view, "MyChartHeader").GetComponentInChildren<UIMarqueeText>();
            log.Append($"나만의 채보 BPM: {view.BpmText} (구간 148.2 / 156, Main), 이름 칸 {name.CellWidth:0} / 글자 {name.TextWidth:0} IsScrolling={name.IsScrolling}, {Find(view, "Txt_MyChartModified").GetComponent<TMP_Text>().text}\n");
            Assert.IsTrue(name.IsScrolling, "긴 채보 이름은 흐른다");
            Assert.AreEqual("148.2", view.BpmText, "그 채보의 구간 중 150.61과 가까운 148.2");
            CheckAll(view, "나만의 채보", log);

            // ── 새 채보 생성
            int addRow = view.MyChartNavigator.AddRowOfGroup(view.ChosenStop.Group);
            typeof(Canvas_SongSelect).GetMethod("ChooseStop", Private).Invoke(view, new object[] { addRow, "테스트" });
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(view.IsNewChartCardShown);
            Assert.IsFalse(view.IsRecordPanelShown, "기록 패널 숨김");
            Assert.AreEqual("MY CHART · 새 채보 생성", view.DifficultyHeader);
            StringAssert.IsMatch(@"^곡 \d\d / \d\d · 새 채보 생성$", view.MyChartCountText.text);
            Assert.AreEqual("새 채보 생성", view.StartLabel);
            Rect newCard = CardRect(view, "Img_NewChartCard", "Img_NewChartCard");
            CheckAll(view, "새 채보 생성", log);

            log.Append("| 상태 | 위끝 | 아래끝 | x | 폭 |\n|---|---|---|---|---|\n");
            foreach ((string label, Rect rect) in new[] { ("공식", tabs), ("나만의 채보", header), ("새 채보 생성", newCard) })
            {
                log.Append($"| {label} | {rect.yMin:0.0} | {rect.yMax:0.0} | {rect.xMin:0.0} | {rect.width:0.0} |\n");
                Assert.AreEqual(80f, rect.xMin, 0.1f);
                Assert.AreEqual(1028f, rect.width, 0.1f);
                Assert.AreEqual(680f, rect.yMin, 0.1f);
                Assert.AreEqual(916f, rect.yMax, 0.1f);
            }
            Debug.Log(log.ToString());
        }
        finally
        {
            displayField.SetValue(song, savedDisplay);
            if (savedRecord != null) byId[TestCharts.OfficialChartId] = savedRecord;
            else byId.Remove(TestCharts.OfficialChartId);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            foreach (InputDevice device in InputSystem.devices)
                if (device is Mouse && !device.enabled) InputSystem.EnableDevice(device);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator L2_MaxCombo_EqualsInGame()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return LoadAndWait(GameConstants.Scenes.SongSelect,
            new SongSelectEntryData { songId = TestCharts.OfficialSongId, chartType = ChartType.Official, chartId = TestCharts.OfficialChartId });
        Canvas_SongSelect view = Object.FindFirstObjectByType<Canvas_SongSelect>();
        int shown = view.ChosenChart.maxCombo;
        StringAssert.Contains($"/ {shown}", view.ComboText);

        yield return LoadAndWait(GameConstants.Scenes.InGame,
            InGameLauncher.CreateNormalEntry(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, 4f));
        InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
        ScoreCalculator score = (ScoreCalculator)typeof(InGameScene).GetField("score", Private).GetValue(scene);
        Assert.AreEqual(score.MaxPossibleCombo, shown, "곡 선택 최대 콤보 = 인게임");
        Debug.Log($"[SongSelectLayoutTest] L2 곡 선택 {shown} = 인게임 {score.MaxPossibleCombo} (노트 {score.TotalNotes} + 틱 {score.TotalTicks})");
        yield return new ExitPlayMode();
    }

    // ───── 검사 ─────

    /// <summary>Canvas 안 · 글자 넘침 · 세로 가운데를 모두 검사하고 표로 남긴다</summary>
    private static void CheckAll(Canvas_SongSelect view, string state, StringBuilder log)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform canvas = (RectTransform)view.transform;
        int rects = 0;
        int texts = 0;
        foreach (RectTransform rect in view.GetComponentsInChildren<RectTransform>(false))
        {
            if (IsExcluded(rect)) continue;
            rects++;
            Rect r = InCanvas(rect);
            Assert.IsTrue(r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= canvas.rect.width + 0.5f && r.yMax <= canvas.rect.height + 0.5f, $"{state} {rect.name}: Canvas 밖 {r}");

            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null || rect.parent.GetComponent<UIMarqueeText>() != null) continue;
            texts++;
            Assert.IsFalse(text.isTextOverflowing, $"{state} {rect.name} '{text.text}': 넘침");
            Assert.LessOrEqual(text.GetPreferredValues(text.text).x, rect.rect.width + 0.6f, $"{state} {rect.name} '{text.text}': 폭 넘침");
        }
        log.Append($"[{state}] RectTransform {rects}개 모두 Canvas 안, 글자 {texts}개 넘침 없음\n");

        // 탭 · 채보 머리 안 요소끼리 세로 가운데
        foreach (RectTransform area in view.GetComponentsInChildren<RectTransform>(false))
        {
            if (area.name != "TabArea") continue;
            float center = Center(area).y;
            foreach (RectTransform child in area)
            {
                if (!child.gameObject.activeSelf || child.name == "TitleViewport") continue;
                Assert.AreEqual(0.5f, child.pivot.y);
                Assert.AreEqual(0f, child.anchoredPosition.y, 0.001f, $"{child.name} anchoredPosition.y");
                float y = InkCenterY(child);
                log.Append($"  {area.parent.name} {child.name}: y {y:0.00} (줄 가운데 {center:0.00})\n");
                Assert.AreEqual(center, y, Tolerance, $"{state} {area.parent.name}/{child.name}: 세로 가운데");
            }
        }

        if (!view.IsRecordPanelShown) return;
        RectTransform panel = Find(view, "RecordPanel");
        RectTransform score = FindIn(panel, "Txt_BestScore");
        RectTransform rank = view.RecordRank.HasRank ? (RectTransform)view.RecordRank.transform : (RectTransform)view.RecordRank.EmptyMark.transform;
        float scoreY = InkCenterY(score);
        float rankY = Center(rank).y;
        float bestTop = InCanvas(FindIn(panel, "Txt_BestScoreLabel")).yMin;
        float rankTop = InCanvas(FindIn(panel, "Txt_RankLabel")).yMin;
        float bestInk = InkCenterY(FindIn(panel, "Txt_BestScoreLabel"));
        float rankInk = InkCenterY(FindIn(panel, "Txt_RankLabel"));
        log.Append($"  패널 값 줄: 점수 y {scoreY:0.00}, 랭크 y {rankY:0.00} / 라벨 줄: BEST SCORE 위끝 {bestTop:0.00} 잉크 {bestInk:0.00}, RANK 위끝 {rankTop:0.00} 잉크 {rankInk:0.00}\n");
        Assert.AreEqual(scoreY, rankY, Tolerance, $"{state}: 점수와 랭크 세로 가운데");
        Assert.AreEqual(bestTop, rankTop, 0.01f, $"{state}: 라벨 위끝");
        Assert.AreEqual(bestInk, rankInk, Tolerance);

        // 배속 줄 세 요소
        RectTransform speed = Find(view, "SpeedRow");
        float rowY = Center(speed).y;
        foreach (string name in new[] { "SpeedLabels", "Txt_Speed", "SpeedKeys" })
        {
            float y = InkCenterY(FindIn(speed, name));
            log.Append($"  배속 줄 {name}: y {y:0.00} (줄 가운데 {rowY:0.00})\n");
            Assert.AreEqual(rowY, y, Tolerance, $"{state} 배속 줄 {name}");
        }
    }

    /// <summary>배경(화면보다 큼) · 스크롤 안 · 전광판 글자 · 팝업 · 잘라 쓰는 바탕은 Canvas 안 검사에서 뺀다</summary>
    private static bool IsExcluded(RectTransform rect)
    {
        if (rect.GetComponentInParent<MenuBackground>() != null || rect.GetComponentInParent<Popup_NewChart>() != null) return true;
        for (Transform t = rect; t != null; t = t.parent)
            if (t.name == "Content" || t.name == "Viewport") return true;
        return rect.parent != null && rect.parent.GetComponent<UIMarqueeText>() != null;
    }

    /// <summary>왼쪽 카드 자리: 위쪽 오브젝트의 위끝 ~ 아래쪽 오브젝트의 아래끝 (왼쪽 위 원점, y는 아래로)</summary>
    private static Rect CardRect(Canvas_SongSelect view, string top, string bottom)
    {
        Canvas.ForceUpdateCanvases();
        Rect a = InCanvas(Find(view, top));
        Rect b = InCanvas(Find(view, bottom));
        Assert.AreEqual(a.xMin, b.xMin, 0.1f);
        Assert.AreEqual(a.width, b.width, 0.1f);
        return Rect.MinMaxRect(a.xMin, a.yMin, a.xMax, b.yMax);
    }

    /// <summary>RATE · MAX COMBO · 랭크 칸 · 구분선 x</summary>
    private static Dictionary<string, float> ValueXs(Canvas_SongSelect view)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform panel = Find(view, "RecordPanel");
        Dictionary<string, float> xs = new Dictionary<string, float>();
        foreach (string name in new[] { "Txt_RateLabel", "Txt_ComboLabel", "Img_RankDivider", "Txt_RankLabel", "RankCell" }) xs[name] = InCanvas(FindIn(panel, name)).xMin;
        foreach (string name in new[] { "Txt_Rate", "Txt_Combo" }) xs[name + " 오른쪽 끝"] = InCanvas(FindIn(panel, name)).xMax;
        xs["랭크/빈칸 가운데"] = Center(view.RecordRank.HasRank ? (RectTransform)view.RecordRank.transform : (RectTransform)view.RecordRank.EmptyMark.transform).x;
        return xs;
    }

    /// <summary>화면 좌표 (왼쪽 위 원점, y는 아래로) 사각형</summary>
    private static Rect InCanvas(RectTransform rect)
    {
        RectTransform canvas = (RectTransform)rect.GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = ToTopLeft(canvas, corners[1]);
        Vector2 max = ToTopLeft(canvas, corners[3]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Vector2 ToTopLeft(RectTransform canvas, Vector3 world)
    {
        Vector3 local = canvas.InverseTransformPoint(world);
        return new Vector2(local.x - canvas.rect.xMin, canvas.rect.yMax - local.y);
    }

    private static Vector2 Center(RectTransform rect) => InCanvas(rect).center;

    /// <summary>눈에 보이는 잉크의 가운데 y: 글자는 글자 bounds, 글자 묶음은 자식 글자를 합친 것, 나머지는 사각형</summary>
    private static float InkCenterY(RectTransform rect)
    {
        RectTransform canvas = (RectTransform)rect.GetComponentInParent<Canvas>().rootCanvas.transform;
        TMP_Text[] texts = rect.GetComponents<Component>().Length > 0 && (rect.GetComponent<TMP_Text>() != null || rect.name == "NameGroup" || rect.name == "SpeedLabels")
            ? rect.GetComponentsInChildren<TMP_Text>(false)
            : new TMP_Text[0];
        if (texts.Length == 0) return Center(rect).y;

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (TMP_Text text in texts)
        {
            text.ForceMeshUpdate();
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo c = text.textInfo.characterInfo[i];
                if (!c.isVisible) continue;
                // SDF 여백을 뺀 실제 글자 모양 (글리프 높이)
                float scale = c.scale * c.textElement.scale * c.textElement.glyph.scale;
                float top = c.baseLine + c.textElement.glyph.metrics.horizontalBearingY * scale;
                float bottom = top - c.textElement.glyph.metrics.height * scale;
                min = Mathf.Min(min, ToTopLeft(canvas, text.rectTransform.TransformPoint(0f, top, 0f)).y);
                max = Mathf.Max(max, ToTopLeft(canvas, text.rectTransform.TransformPoint(0f, bottom, 0f)).y);
            }
        }
        return (min + max) * 0.5f;
    }

    private static RectTransform Find(Canvas_SongSelect view, string name) => FindIn((RectTransform)view.transform, name);

    private static RectTransform FindIn(RectTransform root, string name)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            if (rect.name == name) return rect;
        Assert.Fail($"{name}이(가) 없습니다");
        return null;
    }

    // ───── 도우미 ─────

    private static IEnumerator Tap(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private static void SaveTestChart()
    {
        ChartData chart = new ChartData
        {
            chartId = TestChartId, songId = TestCharts.OfficialSongId, type = ChartData.TypeCustom, level = 12,
            chartName = "긴 채보 이름이 전광판으로 흐르는지 확인하는 테스트 채보 이름 · 레벨과 마지막 수정은 그대로 보인다 · 끝",
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 148.2f }, new TimingPointData { beat = 16f, bpm = 156f } },
            notes = new List<NoteData> { new NoteData { beat = 8f, lane = 1, type = NoteData.TypeShort } }
        };
        Assert.IsTrue(ChartRepository.TrySave(chart, out string error), error);
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
        float deadline = TestSpeed.Deadline(20f);
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
        yield return null;
    }
}
