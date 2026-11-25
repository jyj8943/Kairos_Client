using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;

/// <summary>
/// 긴 제목 전광판(UIMarqueeText)의 화면 연결 확인 (EnterPlayMode). 시간표 · 판단은 MarqueeTests(EditMode), 결과 화면은 ResultIntroPlayTests I6.
/// - M1 곡 선택: 큰 제목 · 목록 고른 칸 · 나만의 채보 줄, 곡을 바꾸면 처음 상태, 한 바퀴 시간, 칸 크기가 바뀌면 다시 잼
/// - M2 인게임 · 일시정지 · 곡 로딩 · 에디터: 긴 제목은 흐르고 짧은 제목은 그대로
/// 마지막 선택(PlayerData)은 테스트 앞뒤로 되돌린다.
/// </summary>
public class MarqueePlayTests
{
    private const string LongSongId = "send_my_love_tonight";
    private const string LongTitle = "Send My Love Tonight";
    private const string VeryLongTitle = "Send My Love Tonight (Extended Hyper Midnight Drive Remix for the Endless Night in the City)";
    private const string ShortTitle = "Critical Velocity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private LastSelectionSaveData savedSelection;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        if (UnityEngine.InputSystem.Mouse.current != null) UnityEngine.InputSystem.InputSystem.DisableDevice(UnityEngine.InputSystem.Mouse.current);
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (savedSelection != null && PlayerDataManager.HasInstance)
        {
            GameManager.PlayerData.LastSelection.CopyFrom(savedSelection);
            GameManager.PlayerData.Save();
        }
        savedSelection = null;
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
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), sceneName);
        float deadline = TestSpeed.Deadline(20f);
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }

    private static UIMarqueeText MarqueeOf(TMP_Text text)
    {
        UIMarqueeText marquee = text.GetComponentInParent<UIMarqueeText>(true);
        Assert.IsNotNull(marquee, text.name + "에 UIMarqueeText가 없습니다");
        Assert.AreSame(text, marquee.Text);
        Assert.AreEqual("TitleViewport", marquee.name);
        return marquee;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static void Row(StringBuilder log, UIMarqueeText m)
    {
        log.Append($"| {PathOf(m.transform).Replace("Managers_DontDestroy/UIManager/", "")} | {m.CellWidth:0.0} | {m.Text.text} | {m.TextWidth:0.0} | {m.IsScrolling} | {m.OverflowWidth:0.0} |\n");
    }

    /// <summary>글자를 넣고 두 프레임 뒤(레이아웃이 끝난 뒤) 상태를 확인한다.</summary>
    private static IEnumerator Check(StringBuilder log, UIMarqueeText m, string title, bool? expectScroll = null, bool fixedCell = true)
    {
        TextAlignmentOptions alignment = m.Text.alignment;
        float cell = m.CellWidth;
        m.SetText(title);
        yield return null;
        yield return null;
        Row(log, m);
        Assert.AreEqual(m.TextWidth - m.CellWidth > 0.5f, m.IsScrolling, PathOf(m.transform) + ": 넘칠 때만 흐른다");
        if (expectScroll.HasValue) Assert.AreEqual(expectScroll.Value, m.IsScrolling, $"{PathOf(m.transform)} '{title}'");
        Assert.AreEqual(alignment, m.Text.alignment, "정렬 그대로");
        Assert.AreEqual(0f, m.CurrentOffset, "처음 멈춤 중");
        if (fixedCell && cell > 0f) Assert.AreEqual(cell, m.CellWidth, 0.5f, "칸 폭 그대로");
        if (!m.IsScrolling) Assert.AreEqual(m.CellWidth, m.Text.rectTransform.rect.width, 0.5f, "넘치지 않으면 글자 칸 = 칸");
    }

    [UnityTest]
    public IEnumerator M1_SongSelect()
    {
        GameManager.PlayerData.LastSelection.official.songId = TestCharts.OfficialSongId;
        GameManager.PlayerData.LastSelection.official.chartId = TestCharts.OfficialChartId;
        yield return LoadAndWait(GameConstants.Scenes.SongSelect, null);
        Canvas_SongSelect songSelect = Object.FindFirstObjectByType<Canvas_SongSelect>();
        UIMarqueeText big = MarqueeOf(Get<TMP_Text>(songSelect, "songTitleText"));
        StringBuilder log = new StringBuilder("[MarqueePlayTest] M1 곡 선택\n| 칸 | 칸 폭 | 글자 | 글자 폭 | IsScrolling | 이동 거리 |\n|---|---|---|---|---|---|\n");

        Btn_SongItem shortItem = null;
        Btn_SongItem longItem = null;
        foreach (Btn_SongItem item in songSelect.SongItems)
        {
            if (item.Song.SongId == TestCharts.OfficialSongId) shortItem = item;
            if (item.Song.SongId == LongSongId) longItem = item;
        }
        Row(log, big);
        Assert.AreEqual(ShortTitle, big.Text.text);
        Assert.IsFalse(big.IsScrolling, "짧은 제목은 그대로");
        Assert.IsTrue(shortItem.TitleMarquee.IsPlaying, "고른 칸");
        Assert.IsFalse(longItem.TitleMarquee.IsPlaying, "고르지 않은 칸");
        Assert.AreEqual(TextOverflowModes.Ellipsis, longItem.TitleMarquee.Text.overflowMode, "고르지 않은 칸은 …");
        Assert.AreEqual(shortItem.TitleMarquee.Text.preferredHeight, ((RectTransform)shortItem.TitleMarquee.transform).rect.height, 0.5f, "칸 높이 = 글자 선호 높이 (레이아웃 그대로)");

        // 11/25 개편으로 제목 칸이 548로 넓어져 이 제목이 들어간다 → 흐름을 보려고 칸을 예전 폭(460)으로 줄인다
        Assert.AreEqual(548f, big.CellWidth, 0.5f, "곡 정보 칸 폭");
        RectTransform bigCell = (RectTransform)big.transform;
        bigCell.sizeDelta = new Vector2(460f, bigCell.sizeDelta.y);

        // 긴 제목 곡으로 바꿈
        EventSystem.current.SetSelectedGameObject(longItem.gameObject);
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(LongTitle, big.Text.text);
        Row(log, big);
        Row(log, longItem.TitleMarquee);
        Row(log, shortItem.TitleMarquee);
        Assert.IsTrue(big.IsScrolling, "긴 제목은 흐른다");
        Assert.AreEqual(0f, big.CurrentOffset, "바꾼 직후 처음 상태");
        Assert.Less(big.Elapsed, 0.5f);
        Assert.IsTrue(longItem.TitleMarquee.IsPlaying);
        Assert.IsFalse(shortItem.TitleMarquee.IsPlaying, "고른 칸이 바뀌면 이전 칸은 멈춤");
        Assert.AreEqual(TextOverflowModes.Ellipsis, shortItem.TitleMarquee.Text.overflowMode);

        // 한 바퀴 (실제 시간)
        float overflow = big.OverflowWidth;
        float cycle = MarqueeTimeline.CycleSeconds(overflow);
        log.Append($"한 바퀴: 멈춤 1.5 → 이동 {MarqueeTimeline.MoveSeconds(overflow):0.00} → 멈춤 1.0 → 되돌림 0.5 = {cycle:0.00}초. 잰 값(t:위치): ");
        float start = Time.unscaledTime - big.Elapsed;
        float nextLog = 0f;
        float maxError = 0f;
        float maxOffset = 0f;
        while (Time.unscaledTime - start < cycle - 0.05f)
        {
            float t = Time.unscaledTime - start;
            MarqueeTimeline.Evaluate(t, overflow, out float expected, out _);
            if (t < cycle - GameConstants.Marquee.ReturnFade * 2f) maxError = Mathf.Max(maxError, Mathf.Abs(expected - big.CurrentOffset));
            Assert.AreEqual(-big.CurrentOffset, big.Text.rectTransform.anchoredPosition.x, 0.01f);
            maxOffset = Mathf.Max(maxOffset, big.CurrentOffset);
            if (t >= nextLog)
            {
                log.Append($"{t:0.00}:{big.CurrentOffset:0.0} ");
                nextLog += 0.5f;
            }
            yield return null;
        }
        log.Append($"| 실제 시각 시간표와 차이 최대 {maxError:0.00}px, 최대 위치 {maxOffset:0.0} / {overflow:0.0}\n");
        Assert.Less(maxError, 6f, "시간표와 0.1초 안");
        Assert.AreEqual(overflow, maxOffset, 0.01f, "끝 글자까지");

        // 흐르는 도중 곡을 바꾸면 처음 상태
        float moveUntil = Time.unscaledTime + 3f;
        while (big.CurrentOffset <= 0f && Time.unscaledTime < moveUntil) yield return null;
        Assert.Greater(big.CurrentOffset, 0f, "흐르는 중");
        Assert.That(() =>
        {
            for (int i = 0; i < 60; i++) big.Tick(0.016f);
        }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), "실제 화면에서 60프레임 GC 할당 0");
        EventSystem.current.SetSelectedGameObject(shortItem.gameObject);
        yield return null;
        yield return null;
        Assert.AreEqual(ShortTitle, big.Text.text);
        Assert.AreEqual(0f, big.CurrentOffset, "곡을 바꾸면 처음 상태");
        Assert.AreEqual(0f, longItem.TitleMarquee.CurrentOffset, "푼 칸도 처음 위치");
        Assert.AreEqual(TextOverflowModes.Ellipsis, longItem.TitleMarquee.Text.overflowMode);

        // 목록 고른 칸 · 나만의 채보 줄에 아주 긴 글자
        yield return Check(log, shortItem.TitleMarquee, VeryLongTitle, true);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SongSelect/Btn_MyChartItem.prefab");
        Btn_MyChartItem row = Object.Instantiate(prefab, songSelect.transform).GetComponent<Btn_MyChartItem>();
        ((RectTransform)row.transform).sizeDelta = new Vector2(480f, 60f);
        row.Init(new ChartData { chartId = "x", chartName = VeryLongTitle, level = 1 }, null);
        yield return null;
        yield return null;
        Assert.IsFalse(row.ChartNameMarquee.IsScrolling, "고르지 않은 줄");
        Assert.AreEqual(TextOverflowModes.Ellipsis, row.ChartNameMarquee.Text.overflowMode);
        row.SetChosen(true);
        yield return Check(log, row.ChartNameMarquee, VeryLongTitle, true);
        yield return Check(log, row.ChartNameMarquee, "연습", false);
        Object.Destroy(row.gameObject);

        // 칸 크기가 바뀌면 다시 잰다
        RectTransform cell = (RectTransform)big.transform;
        Vector2 size = cell.sizeDelta;
        cell.sizeDelta = new Vector2(200f, size.y);
        yield return null;
        yield return null;
        log.Append($"칸 폭 {size.x:0} → 200: 넘침 {big.OverflowWidth:0.0}, 흐름 {big.IsScrolling}\n");
        Assert.IsTrue(big.IsScrolling, "칸이 줄면 흐른다");
        cell.sizeDelta = size;
        yield return null;
        yield return null;
        Assert.IsFalse(big.IsScrolling, "되돌리면 멈춘다");

        // 꺼졌다 켜지면 처음부터
        cell.sizeDelta = new Vector2(200f, size.y);
        yield return Wait(1.8f);
        Assert.Greater(big.CurrentOffset, 0f);
        big.gameObject.SetActive(false);
        Assert.AreEqual(0f, big.CurrentOffset);
        big.gameObject.SetActive(true);
        yield return null;
        Assert.AreEqual(0f, big.CurrentOffset, "켜지면 처음 상태부터");
        Debug.Log(log.ToString());
    }

    [UnityTest]
    public IEnumerator M2_InGame_Pause_SongLoading_Editor()
    {
        StringBuilder log = new StringBuilder("[MarqueePlayTest] M2\n| 칸 | 칸 폭 | 글자 | 글자 폭 | IsScrolling | 이동 거리 |\n|---|---|---|---|---|---|\n");
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion, songId = TestCharts.OfficialSongId, type = ChartData.TypeCustom, chartName = "제목 흐름 확인",
            keyCount = GameConstants.Editor.LaneCount, level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150f } },
            notes = new List<NoteData> { new NoteData { beat = 40f, lane = 0, type = NoteData.TypeShort } }
        };
        yield return LoadAndWait(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId, chartType = ChartType.Custom, mode = InGameMode.TestPlay, scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = chart, startBeat = 0f }
        });
        Canvas_InGame inGame = Object.FindFirstObjectByType<Canvas_InGame>();
        UIMarqueeText title = MarqueeOf(Get<TMP_Text>(inGame, "songTitleText"));
        Assert.AreEqual(ShortTitle, title.Text.text);

        // 테스트 플레이 뱃지가 있으면 제목 칸만 줄어들고, 뱃지 · 제목이 줄 안에 들어온다
        RectTransform rowRect = (RectTransform)title.transform.parent;
        yield return Check(log, title, ShortTitle, null, false);
        yield return Check(log, title, VeryLongTitle, true, false);
        RectTransform badge = Get<GameObject>(inGame, "testPlayBadge").GetComponent<RectTransform>();
        Assert.LessOrEqual(((RectTransform)title.transform).rect.width + badge.rect.width, rowRect.rect.width + 0.5f, "제목 칸 + 뱃지 ≤ 줄");
        Assert.AreEqual(40f, ((RectTransform)title.transform).rect.height, 0.5f, "제목 칸 높이 그대로");
        badge.gameObject.SetActive(false);
        yield return null;
        yield return null;
        Assert.AreEqual(400f, title.CellWidth, 0.5f, "뱃지가 없으면 줄 전체");
        yield return Check(log, title, LongTitle);

        // 일시정지 (unscaled라 멈춘 동안에도 흐른다)
        Popup_Pause pause = Object.FindFirstObjectByType<Popup_Pause>(FindObjectsInactive.Include);
        UIMarqueeText pauseTitle = MarqueeOf(pause.SongTitleText);
        InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
        typeof(InGameScene).GetMethod("OnBack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(scene, null);
        yield return null;
        yield return null;
        Assert.IsTrue(pause.gameObject.activeInHierarchy, "일시정지");
        yield return Check(log, pauseTitle, ShortTitle, false);
        yield return Check(log, pauseTitle, LongTitle);
        yield return Check(log, pauseTitle, VeryLongTitle, true);
        yield return Wait(1.8f);
        Assert.Greater(pauseTitle.CurrentOffset, 0f, "일시정지 중에도 흐른다");

        // 곡 로딩: 열 때마다 처음 상태부터
        Canvas_SongLoading loading = Object.FindFirstObjectByType<Canvas_SongLoading>(FindObjectsInactive.Include);
        InGameEntryData entry = InGameLauncher.CreateNormalEntry(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, 1f);
        loading.Open(entry);
        yield return null;
        yield return Check(log, loading.TitleMarquee, ShortTitle, false);
        yield return Check(log, loading.TitleMarquee, LongTitle);
        yield return Check(log, loading.TitleMarquee, VeryLongTitle, true);
        loading.ChartRow.SetActive(true);
        yield return Check(log, loading.ChartNameMarquee, "연습", false);
        yield return Check(log, loading.ChartNameMarquee, VeryLongTitle + VeryLongTitle, true);
        yield return Wait(1.8f);
        Assert.Greater(loading.ChartNameMarquee.CurrentOffset, 0f);
        loading.Open(entry);
        Assert.AreEqual(0f, loading.TitleMarquee.CurrentOffset, "다시 열면 처음 상태");
        Assert.AreEqual(0f, loading.ChartNameMarquee.CurrentOffset);
        yield return loading.CloseRoutine();

        yield return LoadAndWait(GameConstants.Scenes.Editor, new EditorEntryData { songId = TestCharts.OfficialSongId, chartName = "제목 흐름 확인", level = 1 });
        Canvas_Editor editor = Object.FindFirstObjectByType<Canvas_Editor>();
        UIMarqueeText editorTitle = MarqueeOf(Get<TMP_Text>(editor, "songTitleText"));
        yield return Check(log, editorTitle, ShortTitle, false);
        yield return Check(log, editorTitle, LongTitle);
        yield return Check(log, editorTitle, VeryLongTitle);
        Debug.Log(log.ToString());
    }
}
