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

/// <summary>
/// 인게임 배경 · 배경 밝기 · 배속/밝기 카드 · 판정 색·글꼴 (기획서 3-4 「배경 · BGA (11/19)」) 실제 씬 확인. 곡 시작 직후 몇 초 안에 끝난다.
/// B1. Critical Velocity NORMAL 시작 → 배경 소스 = 곡 일러스트(블러 아님), 덮개 알파 0, 카드 "x1.0 · 100% · 막대 4칸", 곡 정보 3줄 "BPM 150.07"(채보 첫 BPM 구간),
///     배속 2(올림)·1(내림) → 카드 값만 바뀜, Txt_SpeedChange 없음, 판정 패널 판정 이름 = 판정 이미지(크기 = 원본 × 배율), 개수 숫자는 TMP
/// B2. 테스트 플레이: PgDn 두 번(한 번은 실제 키 입력) → 50% · 막대 2칸 · 덮개 0.5 · 저장값 50, F5 재시작 뒤에도 50%, 일시정지 팝업이 열린 동안 PgDn 무시
/// B3. 와이어프레임 맞춤 (11/19): 기어 바탕·키 칸 색 알파 1, 판정 글자 = 이미지(TMP 없음),
///     Txt_Combo = NEXON Lv2 Bold 72, 배속·밝기 카드 요소들의 세로 중심이 같음 (한 줄)
/// PlayerData.json의 마지막 선택은 테스트 앞뒤로 되돌린다.
/// </summary>
public class InGameBackgroundPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string KartFontName = "NEXON Kart Gothic ExtraBold SDF";

    private LastSelectionSaveData savedSelection;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
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
        yield return new ExitPlayMode();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    /// <summary>전역 매니저를 기다리고 마지막 선택을 저장해 둔 뒤 배경 밝기를 brightness로 맞춘다</summary>
    private IEnumerator Prepare(int brightness)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
        GameManager.PlayerData.SetLastBackgroundBrightness(brightness);
    }

    /// <summary>인게임을 불러와 플레이가 시작될 때까지 기다린다 (before가 있으면 그것과 다른 새 씬)</summary>
    private static IEnumerator WaitPlaying(InGameScene before, System.Action<InGameScene> done)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            if (!GameManager.Scene.IsLoading && SceneManager.GetActiveScene().name == GameConstants.Scenes.InGame)
            {
                InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
                if (scene != null && scene != before && Get<bool>(scene, "isPlaying"))
                {
                    done(scene);
                    yield break;
                }
            }
            yield return null;
        }
    }

    /// <summary>B3: 불투명 기어 · 판정 글자 빛(Glow만) · 콤보 글꼴 · 카드 한 줄</summary>
    [UnityTest]
    public IEnumerator B3_Wireframe_OpaqueGear_JudgeGlow_ComboFont_CardRow()
    {
        yield return Prepare(100);
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = 1f
        }));
        InGameScene scene = null;
        yield return WaitPlaying(null, s => scene = s);
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        // 기어 바탕 · 키 칸: 불투명 (배경이 레인 안으로 비치지 않음)
        GearView gear = Get<GearView>(scene, "gear");
        SpriteRenderer lane = Get<SpriteRenderer>(gear, "laneBackground");
        Assert.AreEqual(1f, lane.color.a, 1e-4f, "기어 바탕 알파 1");
        for (int i = 0; i < 4; i++) Assert.AreEqual(1f, gear.GetKeyCellColor(i).a, 1e-4f, $"키 칸 {i} 알파 1");

        // 기어 판정 글자는 이미지 (11/19): TMP 글자 없음
        Assert.IsNotNull(canvas.Judge.ImageComponent, "Img_Judge Image");
        Assert.IsNull(canvas.Judge.GetComponent<TMP_Text>(), "판정 글자에 TMP 없음");
        Assert.IsNull(canvas.FastSlowImage.GetComponent<TMP_Text>(), "FAST/SLOW에 TMP 없음");

        // 콤보 숫자: NEXON Lv2 Bold 72
        TMP_Text combo = canvas.Combo.GetComponent<TMP_Text>();
        Assert.AreEqual("NEXON Lv2 Gothic Bold SDF", combo.font.name, "Txt_Combo 글꼴");
        Assert.AreEqual(72f, combo.fontSize, "Txt_Combo 크기");

        // 카드: 모든 요소의 세로 중심이 같다
        RectTransform cardRect = (RectTransform)canvas.SpeedBrightnessCard.transform;
        float? centerY = null;
        foreach (RectTransform item in canvas.SpeedBrightnessCard.RowItems)
        {
            Assert.IsNotNull(item, "카드 한 줄 요소");
            float y = cardRect.InverseTransformPoint(item.TransformPoint(item.rect.center)).y;
            if (centerY == null) centerY = y;
            Assert.AreEqual(centerY.Value, y, 0.5f, item.name + " 세로 중심");
        }
        Assert.AreEqual(-cardRect.rect.height / 2f, centerY.Value, 0.5f, "카드 세로 가운데");

        Debug.Log($"[InGameBackgroundTest] B3: 기어 알파 {lane.color.a}, 판정 글자 이미지, 콤보 {combo.font.name} {combo.fontSize}, 카드 중심 {centerY:0.#}");
    }

    /// <summary>B1: 배경 소스·덮개·카드·BPM·배속 키·Txt_SpeedChange 없음·판정 글꼴·PERFECT 그라데이션</summary>
    [UnityTest]
    public IEnumerator B1_OfficialStart_BackgroundCardBpmJudgeFont()
    {
        yield return Prepare(100);
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = 1f
        }));
        InGameScene scene = null;
        yield return WaitPlaying(null, s => scene = s);
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");
        SongData song = GameManager.Songs.Find(TestCharts.OfficialSongId);

        // 배경: 곡 일러스트 그대로 (블러·라벤더 덮기 없음), 덮개 알파 0
        Canvas_InGameBackground background = scene.Background;
        Assert.IsNotNull(background, "Canvas_InGameBackground 연결");
        Assert.AreEqual(InGameBackgroundSource.Illustration, background.SourceKind);
        Assert.AreSame(song.Background.texture, background.SourceImage.texture, "배경 소스 = SongData.Background 텍스처 (흐린 RenderTexture 아님)");
        Assert.AreEqual(0f, background.DimImage.color.a, 1e-4f, "100% → 덮개 알파 0");
        Assert.IsNull(Object.FindFirstObjectByType<Canvas_InGame>().GetComponentInChildren<MenuBackground>(true), "인게임에 MenuBackground 없음 (곡 로딩 오버레이의 배경은 Managers_DontDestroy 쪽)");
        Assert.IsNull(Object.FindFirstObjectByType<Canvas_InGameBackground>().GetComponentInChildren<MenuBackground>(true), "인게임 배경 Canvas에도 없음");

        // 카드 · BPM
        Img_SpeedBrightnessCard card = canvas.SpeedBrightnessCard;
        Assert.IsNotNull(card, "배속·밝기 카드");
        Assert.AreEqual("x1.0", card.SpeedText);
        Assert.AreEqual("100%", card.BrightnessText);
        Assert.AreEqual(4, card.FilledBarCount, "100% → 막대 4칸");
        CollectionAssert.AreEqual(new[] { "1", "2", "PgDn", "PgUp" }, card.KeyTexts, "키 이름은 바인딩에서");
        // 공식 채보는 BPM 구간이 있다 (0박 150.07 → 150박 150.68 → 280박 151.2). 시작 위치(곡 시간 앞부분)는 첫 구간
        Assert.AreEqual("BPM 150.07", canvas.BpmLine, "곡 정보 3줄 = 지금 위치 BPM");

        // 배속 2 → 1: 카드 값만 바뀐다. 배속 변경 글자는 없다
        scene.TestTapSpeedKey(true);
        string afterUp = card.SpeedText;
        scene.TestTapSpeedKey(false);
        Assert.AreEqual("x1.1", afterUp, "2 → x1.1");
        Assert.AreEqual("x1.0", card.SpeedText, "1 → x1.0");
        Assert.IsNull(GameObject.Find("Txt_SpeedChange"), "Txt_SpeedChange 오브젝트 없음");

        // 판정 패널 판정 이름 = 기어 판정 글자와 같은 이미지 (11/19), 크기 = 원본 × 배율, 개수 숫자는 TMP 그대로
        Img_JudgePanel panel = canvas.JudgePanel;
        foreach (JudgeType judgeType in System.Enum.GetValues(typeof(JudgeType)))
        {
            UnityEngine.UI.Image title = panel.GetJudgeTitle(judgeType);
            Assert.IsNotNull(title, judgeType + " 이름 이미지");
            Assert.AreSame(JudgeSprites.Get(judgeType), title.sprite, judgeType + " 이미지");
            Assert.AreEqual(JudgeSprites.DisplaySize(title.sprite, GameConstants.Play.PanelJudgeImageScale), title.rectTransform.sizeDelta, judgeType + " 크기 = 원본 × 판정 배율");
            Assert.IsTrue(title.preserveAspect && !title.raycastTarget, judgeType + " Preserve Aspect 켬 · Raycast 끔");
            Assert.AreEqual(Color.white, title.color, judgeType + " 색 흰색 (색은 이미지 안)");
        }
        foreach (FastSlow dir in new[] { FastSlow.Fast, FastSlow.Slow })
        {
            UnityEngine.UI.Image title = panel.GetFastSlowTitle(dir);
            Assert.AreSame(JudgeSprites.Get(dir), title.sprite, dir + " 이미지");
            Assert.AreEqual(JudgeSprites.DisplaySize(title.sprite, GameConstants.Play.PanelFastSlowImageScale), title.rectTransform.sizeDelta, dir + " 크기 = 원본 × FAST·SLOW 배율");
        }
        Assert.AreNotEqual(KartFontName, Get<TMP_Text>(canvas, "scoreText").font.name, "점수 숫자는 NEXON Lv2 그대로");

        ScoreCalculator sample = new ScoreCalculator(10);
        sample.Add(JudgeType.Great);
        sample.Add(JudgeType.Miss);
        canvas.RefreshJudgePanel(sample);
        StringAssert.Contains(">1<", Get<TMP_Text>(panel, "greatCount").text, "개수 TMP는 그대로 바뀜");
        StringAssert.Contains(">1<", Get<TMP_Text>(panel, "missCount").text);

        Debug.Log($"[InGameBackgroundTest] B1: 배경 {background.SourceImage.texture.name}, 덮개 {background.DimImage.color.a:0.##}, 카드 {card.SpeedText} · {card.BrightnessText} · 막대 {card.FilledBarCount}칸, " +
                  $"키 {string.Join("/", card.KeyTexts)}, {canvas.BpmLine}, PERFECT 이름 {panel.GetJudgeTitle(JudgeType.Perfect).rectTransform.sizeDelta}");
    }

    /// <summary>B2: PgDn 두 번 → 50% · 막대 2칸 · 덮개 0.5 · 저장, F5 재시작 뒤 50%, 일시정지 중 무시</summary>
    [UnityTest]
    public IEnumerator B2_TestPlay_PageDownTwice_RestartKeeps_PausedIgnores()
    {
        yield return Prepare(100);
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < 4; i++) notes.Add(new NoteData { beat = 16f + i, lane = i, type = NoteData.TypeShort });
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData
            {
                songId = TestCharts.OfficialSongId,
                startBeat = 0f,
                chart = new ChartData
                {
                    version = GameConstants.Chart.CurrentVersion,
                    songId = TestCharts.OfficialSongId,
                    type = ChartData.TypeCustom,
                    chartName = "밝기 확인",
                    keyCount = GameConstants.Editor.LaneCount,
                    level = 1,
                    timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
                    notes = notes
                }
            }
        }));
        InGameScene scene = null;
        yield return WaitPlaying(null, s => scene = s);
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        // 한 번은 실제 키 입력 (입력 에셋 Gameplay/BackgroundDarker = Page Down → InGameScene 구독), 한 번은 같은 경로의 테스트 함수
        Keyboard keyboard = Keyboard.current;
        Assert.IsNotNull(keyboard, "키보드 장치");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
        Assert.AreEqual(75, scene.BackgroundBrightness, "실제 PgDn → 75%");
        scene.TestTapBrightnessKey(false);

        Assert.AreEqual(50, scene.BackgroundBrightness);
        Assert.AreEqual("50%", canvas.SpeedBrightnessCard.BrightnessText);
        Assert.AreEqual(2, canvas.SpeedBrightnessCard.FilledBarCount, "50% → 막대 2칸");
        Assert.AreEqual(0.5f, scene.Background.DimImage.color.a, 1e-4f, "덮개 알파 0.5");
        Assert.AreEqual(50, GameManager.PlayerData.LastBackgroundBrightness, "테스트 플레이도 저장");

        // F5 재시작 → 저장값으로 다시 시작
        InGameScene before = scene;
        Assert.IsTrue(scene.TestPressRestartKey(), "F5 재시작");
        yield return WaitPlaying(before, s => scene = s);
        canvas = Get<Canvas_InGame>(scene, "canvas");
        Assert.AreEqual(50, scene.BackgroundBrightness, "재시작 뒤 50%");
        Assert.AreEqual(0.5f, scene.Background.DimImage.color.a, 1e-4f, "재시작 뒤 덮개 0.5");
        Assert.AreEqual("50%", canvas.SpeedBrightnessCard.BrightnessText);

        // 일시정지 팝업이 열린 동안 PgDn 무시
        scene.OnBack();
        yield return null;
        Assert.IsTrue(GameManager.UI.HasOpenPopup, "일시정지 팝업 열림");
        scene.TestTapBrightnessKey(false);
        Assert.AreEqual(50, scene.BackgroundBrightness, "팝업 중 PgDn 무시");
        Assert.AreEqual(50, GameManager.PlayerData.LastBackgroundBrightness, "저장값 그대로");

        Debug.Log($"[InGameBackgroundTest] B2: PgDn 두 번 → {canvas.SpeedBrightnessCard.BrightnessText} · 막대 {canvas.SpeedBrightnessCard.FilledBarCount}칸 · 덮개 {scene.Background.DimImage.color.a:0.##}, 재시작 뒤 그대로, 일시정지 중 무시");
    }
}
