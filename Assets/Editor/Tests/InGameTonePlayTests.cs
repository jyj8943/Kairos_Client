using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 인게임 톤 개편 (기획서 3-4 「인게임 톤 개편 (11/18)」) 실제 씬 확인. 곡이 시작되면 바로 확인하고 끝낸다 (곡 시간 몇 초).
/// T1. Critical Velocity NORMAL 일반 플레이 시작 → 배경 RawImage에 흐린 일러스트 텍스처, Img_Album = 곡 앨범, 난이도 뱃지 = #2F7FC8, RATE 0.00% · SCORE 0
/// T2. 나만의 채보 테스트 플레이 → CUSTOM 뱃지(#8B7FD1) + 채보 이름 + 테스트 플레이 뱃지.
///     F5 재시작 뒤에는 같은 곡이라 배경을 다시 흐리게 처리하지 않는다 (남겨 둔 결과를 다시 씀).
///     재시작은 테스트 플레이로 확인한다 (일반 플레이 재시작은 PlayerData에 마지막 배속을 저장하므로)
/// T3. 라인 키(A)를 누르면 키 칸이 #3E9BE0, 떼면 평소 색 (입력 에셋 → InGameInput → GearView 실제 경로)
/// T4. 판정선 이미지 (11/18): 스프라이트 JudgeLine, 색 흰색(덧칠 없음), Sliced, 폭 = 기어 폭, 두께 = JudgeLineThicknessPx, 세로 중심 = y 900 (이전 JudgeLineY 그대로)
/// </summary>
public class InGameTonePlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const float Tolerance = 1.5f / 255f;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return new ExitPlayMode();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static void AssertHex(string hex, Color actual, string message)
    {
        Assert.IsTrue(ColorUtility.TryParseHtmlString("#" + hex, out Color expected));
        Assert.AreEqual(expected.r, actual.r, Tolerance, message + " (r)");
        Assert.AreEqual(expected.g, actual.g, Tolerance, message + " (g)");
        Assert.AreEqual(expected.b, actual.b, Tolerance, message + " (b)");
    }

    /// <summary>인게임을 불러와 플레이가 시작될 때까지 기다린다</summary>
    private static IEnumerator Load(InGameEntryData entry, System.Action<InGameScene> done)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        InGameScene scene = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            if (!GameManager.Scene.IsLoading && SceneManager.GetActiveScene().name == GameConstants.Scenes.InGame)
            {
                scene = Object.FindFirstObjectByType<InGameScene>();
                if (scene != null && Get<bool>(scene, "isPlaying")) break;
            }
            yield return null;
        }
        done(scene);
    }

    /// <summary>음표 몇 개짜리 나만의 채보 (Critical Velocity 곡)</summary>
    private static ChartData CustomChart(string chartName)
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 4; i++) notes.Add(new NoteData { beat = 16f + i, lane = 3 - i % 2, type = NoteData.TypeShort });
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = chartName,
            keyCount = GameConstants.Editor.LaneCount,
            level = 5,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    private static InGameEntryData TestPlayEntry(string chartName, bool autoPlay)
    {
        return new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = CustomChart(chartName), startBeat = 0f, autoPlay = autoPlay }
        };
    }

    /// <summary>T1: 공식 NORMAL 시작 → 배경 텍스처 · 앨범 · NORMAL 뱃지 #2F7FC8 · RATE 0.00% · SCORE 0</summary>
    [UnityTest]
    public IEnumerator T1_OfficialStart_BackgroundAlbumBadgeRateScore()
    {
        InGameScene scene = null;
        yield return Load(new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = 1f
        }, s => scene = s);

        SongData song = GameManager.Songs.Find(TestCharts.OfficialSongId);
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        Assert.IsNotNull(scene.Background, "배경 연결");
        RawImage background = scene.Background.Blur.FrontImage;
        Assert.IsNotNull(background.texture, "배경 RawImage에 흐린 일러스트 텍스처");
        Assert.IsTrue(background.enabled && background.color.a > 0.99f, "배경 보임");
        Assert.AreSame(song.Background.texture, scene.Background.Blur.Source, "곡 일러스트(SongData.Background)가 입력");

        Assert.IsNotNull(song.Cover, "곡 앨범 에셋");
        Assert.AreSame(song.Cover, canvas.AlbumSprite, "Img_Album = 곡 앨범");
        AssertHex("2F7FC8", canvas.ChartBadgeColor, "NORMAL 뱃지 = 메뉴용 #2F7FC8");
        Assert.AreEqual("0.00%", canvas.JudgePanel.RateText, "판정 전 RATE 0.00%");
        Assert.AreEqual("0", canvas.ScoreText, "SCORE 0 (쉼표 없음)");

        Debug.Log($"[InGameToneTest] T1: 배경 {background.texture.width}×{background.texture.height} (블러 {scene.Background.Blur.ProcessCount}회, 다시 씀 {scene.Background.Blur.ReusedCount}회), " +
                  $"앨범 {canvas.AlbumSprite.name}, 뱃지 #{ColorUtility.ToHtmlStringRGB(canvas.ChartBadgeColor)}, RATE {canvas.JudgePanel.RateText}, SCORE {canvas.ScoreText}");
    }

    /// <summary>T2: 나만의 채보 테스트 플레이 → CUSTOM 뱃지 #8B7FD1 · 채보 이름 · 테스트 플레이 뱃지</summary>
    [UnityTest]
    public IEnumerator T2_CustomTestPlay_CustomBadgeNameAndTestPlay()
    {
        InGameScene scene = null;
        yield return Load(TestPlayEntry("톤 확인용 채보", false), s => scene = s);
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        Assert.AreEqual(GameConstants.Play.ChartBadgeCustomText, Get<TMP_Text>(canvas, "chartBadgeText").text);
        AssertHex("8B7FD1", canvas.ChartBadgeColor, "CUSTOM 뱃지 = #8B7FD1");
        TMP_Text chartName = Get<TMP_Text>(canvas, "chartNameText");
        Assert.IsTrue(chartName.gameObject.activeInHierarchy, "채보 이름 보임");
        Assert.AreEqual("톤 확인용 채보", chartName.text);
        GameObject testPlay = Get<GameObject>(canvas, "testPlayBadge");
        Assert.IsTrue(testPlay.activeInHierarchy, "테스트 플레이 뱃지 보임");
        Assert.AreEqual(GameConstants.Play.TestPlayLabel, Get<TMP_Text>(canvas, "testPlayText").text);
        AssertHex("3E9BE0", testPlay.GetComponent<Image>().color, "테스트 플레이 뱃지 = 포인트색");

        string badgeLog = $"뱃지 {Get<TMP_Text>(canvas, "chartBadgeText").text} #{ColorUtility.ToHtmlStringRGB(canvas.ChartBadgeColor)}, 이름 '{chartName.text}', 테스트 플레이 뱃지 켜짐";

        // F5 재시작: 씬을 다시 불러오지만 같은 곡이라 블러를 다시 하지 않는다
        Texture firstResult = scene.Background.Blur.FrontImage.texture;
        Assert.IsNotNull(firstResult, "배경 텍스처");
        InGameScene before = scene;
        Assert.IsTrue(scene.TestPressRestartKey(), "F5 재시작");
        float deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "재시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && scene != before && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        UIBlurBackground blur = scene.Background.Blur;
        Assert.AreEqual(0, blur.ProcessCount, "재시작 뒤 블러 다시 안 함");
        Assert.AreEqual(1, blur.ReusedCount, "남겨 둔 결과를 다시 씀");
        Assert.AreSame(firstResult, blur.FrontImage.texture, "같은 결과 텍스처");

        Debug.Log($"[InGameToneTest] T2: {badgeLog} → 재시작 뒤 블러 {blur.ProcessCount}회 · 다시 씀 {blur.ReusedCount}회");
    }

    /// <summary>T3: 라인 키 A를 누르면 키 칸이 #3E9BE0, 떼면 평소 색</summary>
    [UnityTest]
    public IEnumerator T3_KeyPress_KeyCellAccentThenNormal()
    {
        InGameScene scene = null;
        yield return Load(TestPlayEntry("키 칸 확인", false), s => scene = s);
        GearView gear = Get<GearView>(scene, "gear");
        Keyboard keyboard = Keyboard.current;
        Assert.IsNotNull(keyboard, "키보드 장치");

        Color normal = UITheme.Current.keyCell;
        Assert.AreEqual(normal, gear.GetKeyCellColor(0), "누르기 전 평소 색");

        // 첫 노트(16박 ≈ 6.4초) 전이라 빈 타격 (판정 없음)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        InputSystem.Update();
        yield return null;
        Assert.IsTrue(gear.IsKeyCellPressed(0), "A → 라인 0 눌림");
        AssertHex("3E9BE0", gear.GetKeyCellColor(0), "누를 때 키 칸 = #3E9BE0");
        Color pressed = gear.GetKeyCellColor(0);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
        Assert.IsFalse(gear.IsKeyCellPressed(0), "떼면 눌림 풀림");
        Assert.AreEqual(normal, gear.GetKeyCellColor(0), "떼면 평소 색");

        Debug.Log($"[InGameToneTest] T3: 평소 #{ColorUtility.ToHtmlStringRGBA(normal)} → 누름 #{ColorUtility.ToHtmlStringRGBA(pressed)} → 뗌 #{ColorUtility.ToHtmlStringRGBA(gear.GetKeyCellColor(0))}");
    }

    /// <summary>T4: 판정선 = JudgeLine 이미지 그대로 (흰색, Sliced), 폭 = 기어 폭, 두께 24px, 세로 중심 y 900</summary>
    [UnityTest]
    public IEnumerator T4_JudgeLineImage_WhiteSlicedGearWidth_CenterUnchanged()
    {
        InGameScene scene = null;
        yield return Load(TestPlayEntry("판정선 확인", false), s => scene = s);
        GearView gear = Get<GearView>(scene, "gear");
        SpriteRenderer line = gear.JudgeLineRenderer;
        Assert.IsNotNull(line, "판정선 SpriteRenderer");

        Assert.IsNotNull(line.sprite);
        Assert.AreEqual("JudgeLine", line.sprite.name, "판정선 이미지");
        Assert.AreEqual(Color.white, line.color, "색 덧칠 없음 (흰색)");
        Assert.AreEqual(SpriteDrawMode.Sliced, line.drawMode);
        Assert.AreEqual(Vector3.one, line.transform.localScale, "크기는 scale이 아니라 size");
        float px = gear.UnitsPerReferencePx;
        Assert.AreEqual(gear.LaneWidth * GameConstants.Editor.LaneCount, line.size.x, 1e-4f, "폭 = 기어 폭");
        Assert.AreEqual(GameConstants.Play.JudgeLineThicknessPx * px, line.size.y, 1e-4f, "두께 = JudgeLineThicknessPx");
        Assert.AreEqual(5, line.sortingOrder, "그리는 순서 그대로 (노트 3 위, 키 칸 6 아래)");

        // 세로 중심 = 화면 위에서 900px (이전 JudgeLineY 그대로), 렌더러 중심도 같은 높이
        float expectedY = gear.ScreenTopY - 900f * px;
        Assert.AreEqual(expectedY, gear.JudgeLineY, 1e-3f, "JudgeLineY = y 900");
        Assert.AreEqual(gear.transform.position.y + gear.JudgeLineY, line.bounds.center.y, 1e-3f, "그림 세로 중심 = JudgeLineY");

        float topPx = (gear.ScreenTopY - (line.bounds.max.y - gear.transform.position.y)) / px;
        float bottomPx = (gear.ScreenTopY - (line.bounds.min.y - gear.transform.position.y)) / px;
        float keyTopPx = (gear.ScreenTopY - gear.KeyCellTopY) / px;
        Debug.Log($"[InGameToneTest] T4: 판정선 y {topPx:0.0}~{bottomPx:0.0}px (두께 {line.size.y / px:0.0}px = {line.size.y:0.####}유닛), 폭 {line.size.x:0.###}유닛, 키 칸 위 끝 {keyTopPx:0.0}px");
    }
}
