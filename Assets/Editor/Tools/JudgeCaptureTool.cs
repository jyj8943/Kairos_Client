using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 판정 글자 비교용 캡처 도구 (사용자 요청 때만, [Explicit]라 일반 테스트 실행에는 끼지 않는다).
/// - Game 뷰를 1920×1080 고정 해상도로 맞추고 인게임 Critical Velocity NORMAL(배경 밝기 100%)에 들어가,
///   곡 시작 전(첫 노트가 오기 전) 구간에서 Canvas_InGame.ShowJudge로 판정 글자를 띄우고 0.2초 뒤 한 프레임을 찍는다.
/// - 노트는 NoteView·LongNoteView 렌더러만 매 프레임 꺼서 화면에 안 보이게 한다 (게임 코드·씬은 바꾸지 않음).
/// - 저장: SaveFolder에 judge_*.png 6장(기어 판정 영역 x 704~1216, y 640~830) · panel.png(x 40~700, y 300~960) · full.png, 같은 이름은 덮어쓴다.
///   찍을 때 판정 글자 이미지 값(sprite·크기)은 같은 폴더 capture_info.txt에 적는다.
/// - 재생 배율 1.0 ([NormalSpeed]): 첫 노트가 판정되기 전에 끝나야 해서.
/// </summary>
public class JudgeCaptureTool
{
    /// <summary>캡처 저장 폴더</summary>
    private static readonly string SaveFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Videos", "Captures", "_kairos_judge_1119");

    private const int RefWidth = 1920;
    private const int RefHeight = 1080;
    private const float ShowSeconds = 0.2f;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private LastSelectionSaveData savedSelection;
    private int previousSizeIndex = -1;
    private readonly StringBuilder info = new StringBuilder();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousSizeIndex = GameViewSizeUtil.SetFixedResolution(RefWidth, RefHeight);
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
        if (previousSizeIndex >= 0) GameViewSizeUtil.SetSelectedIndex(previousSizeIndex);
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo fieldInfo = target.GetType().GetField(field, Private);
        Assert.IsNotNull(fieldInfo, $"필드 {field}가 없습니다");
        return (TValue)fieldInfo.GetValue(target);
    }

    /// <summary>판정 글자 8장을 찍어 SaveFolder에 저장한다</summary>
    [UnityTest, Explicit("사용자가 요청할 때만 (판정 글자 비교 캡처)"), Category("Capture"), NormalSpeed]
    public IEnumerator Capture_JudgeTexts()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
        savedSelection = GameManager.PlayerData.LastSelection.Clone();
        GameManager.PlayerData.SetLastBackgroundBrightness(100);

        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartId = TestCharts.OfficialChartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = 1f
        }));

        InGameScene scene = null;
        while (scene == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            if (!GameManager.Scene.IsLoading && SceneManager.GetActiveScene().name == GameConstants.Scenes.InGame)
            {
                InGameScene found = Object.FindFirstObjectByType<InGameScene>();
                if (found != null && Get<bool>(found, "isPlaying")) scene = found;
            }
            yield return null;
        }

        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");
        NoteLayer noteLayer = Get<NoteLayer>(scene, "noteLayer");
        Directory.CreateDirectory(SaveFolder);
        info.Clear();
        info.AppendLine($"Game 뷰 해상도: {Screen.width}×{Screen.height}");

        // 판정 패널 값 (테스트 표시용, 점수 계산과 무관)
        Img_JudgePanel panel = canvas.JudgePanel;
        string countFormat = (string)typeof(Img_JudgePanel).GetField("CountFormat", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        string rateFormat = (string)typeof(Img_JudgePanel).GetField("RateFormat", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        string[] countFields = { "perfectCount", "greatCount", "goodCount", "missCount", "fastCount", "slowCount" };
        int[] counts = { 812, 64, 12, 3, 40, 36 };
        const int score = 961204;

        (string file, JudgeType judge, FastSlow fastSlow)[] shots =
        {
            ("judge_perfect.png", JudgeType.Perfect, FastSlow.None),
            ("judge_great_fast.png", JudgeType.Great, FastSlow.Fast),
            ("judge_great_slow.png", JudgeType.Great, FastSlow.Slow),
            ("judge_good_fast.png", JudgeType.Good, FastSlow.Fast),
            ("judge_good_slow.png", JudgeType.Good, FastSlow.Slow),
            ("judge_miss.png", JudgeType.Miss, FastSlow.None),
            ("full.png", JudgeType.Great, FastSlow.Fast)
        };

        foreach ((string file, JudgeType judge, FastSlow fastSlow) in shots)
        {
            canvas.FastSlowImage.ResetView();
            canvas.ShowJudge(judge, fastSlow, 0, score);
            for (int i = 0; i < countFields.Length; i++) Get<TMP_Text>(panel, countFields[i]).SetText(countFormat, counts[i]);
            Get<TMP_Text>(panel, "rate").SetText(rateFormat, 96.12f);

            float until = Time.realtimeSinceStartup + ShowSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                HideNotes(noteLayer);
                yield return null;
            }
            HideNotes(noteLayer);
            Assert.IsTrue(Get<bool>(scene, "isPlaying"), "플레이 중");
            Assert.AreEqual(0, Get<ScoreCalculator>(scene, "score").JudgedCount, "첫 노트 판정 전에 찍어야 한다");

            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (file == "full.png")
                {
                    Save(shot, file, 0, 0, RefWidth, RefHeight);
                    Save(shot, "panel.png", 40, 300, 660, 660);
                }
                else
                {
                    Save(shot, file, 704, 640, 512, 190);
                    Record(file, canvas);
                }
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        File.WriteAllText(Path.Combine(SaveFolder, "capture_info.txt"), info.ToString(), new UTF8Encoding(false));
        Debug.Log($"[JudgeCapture] 저장 폴더 {SaveFolder}\n{info}");
    }

    /// <summary>노트(숏·롱) 렌더러를 끈다 (마디선은 그대로)</summary>
    private static void HideNotes(NoteLayer layer)
    {
        if (layer == null) return;
        foreach (NoteView view in layer.GetComponentsInChildren<NoteView>(true))
            foreach (Renderer r in view.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (LongNoteView view in layer.GetComponentsInChildren<LongNoteView>(true))
            foreach (Renderer r in view.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    /// <summary>1920×1080 화면 좌표(위에서 아래) 사각형을 잘라 PNG로 저장한다. 해상도가 다르면 비율대로 자른다</summary>
    private void Save(Texture2D shot, string file, int x, int y, int width, int height)
    {
        float sx = shot.width / (float)RefWidth;
        float sy = shot.height / (float)RefHeight;
        int px = Mathf.RoundToInt(x * sx);
        int pw = Mathf.Min(Mathf.RoundToInt(width * sx), shot.width - px);
        int ph = Mathf.RoundToInt(height * sy);
        int py = Mathf.Max(0, shot.height - Mathf.RoundToInt(y * sy) - ph);
        Texture2D crop = new Texture2D(pw, ph, TextureFormat.RGB24, false);
        crop.SetPixels(shot.GetPixels(px, py, pw, ph));
        crop.Apply();
        string path = Path.Combine(SaveFolder, file);
        File.WriteAllBytes(path, crop.EncodeToPNG());
        info.AppendLine($"{path} {pw}×{ph}");
        Object.Destroy(crop);
    }

    /// <summary>찍을 때 판정 글자 이미지 값을 적는다 (11/19 TMP 글자 → 이미지)</summary>
    private void Record(string file, Canvas_InGame canvas)
    {
        foreach (AnimatedJudgeImage animated in new AnimatedJudgeImage[] { canvas.Judge, canvas.FastSlowImage })
        {
            if (!animated.IsVisible) continue;
            UnityEngine.UI.Image image = animated.ImageComponent;
            RectTransform rt = image.rectTransform;
            info.AppendLine($"  [{file}] {image.name}: sprite={image.sprite.name} ({image.sprite.rect.width}×{image.sprite.rect.height}), sizeDelta={rt.sizeDelta}, scale={rt.localScale.x:0.##}, alpha={image.color.a:0.##}");
        }
    }
}

/// <summary>Game 뷰 해상도 고르기 (UnityEditor 내부 API를 리플렉션으로 쓴다, 캡처 도구 전용)</summary>
internal static class GameViewSizeUtil
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static System.Type GameViewType => typeof(Editor).Assembly.GetType("UnityEditor.GameView");

    private static object CurrentGroup()
    {
        System.Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        object instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", All).GetValue(null);
        return sizesType.GetProperty("currentGroup", All).GetValue(instance);
    }

    /// <summary>width×height 고정 해상도를 고르고(없으면 추가) 이전 선택 번호를 돌려준다</summary>
    public static int SetFixedResolution(int width, int height)
    {
        object group = CurrentGroup();
        System.Type groupType = group.GetType();
        int total = (int)groupType.GetMethod("GetTotalCount", All).Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            object size = groupType.GetMethod("GetGameViewSize", All).Invoke(group, new object[] { i });
            System.Type sizeType = size.GetType();
            if ((int)sizeType.GetProperty("width", All).GetValue(size) == width && (int)sizeType.GetProperty("height", All).GetValue(size) == height
                && sizeType.GetProperty("sizeType", All).GetValue(size).ToString() == "FixedResolution")
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            System.Type gvsType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            System.Type kindType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            object kind = System.Enum.Parse(kindType, "FixedResolution");
            object newSize = System.Activator.CreateInstance(gvsType, kind, width, height, $"Kairos Capture {width}x{height}");
            groupType.GetMethod("AddCustomSize", All).Invoke(group, new[] { newSize });
            index = total;
        }

        EditorWindow gameView = EditorWindow.GetWindow(GameViewType, false, null, false);
        PropertyInfo selected = GameViewType.GetProperty("selectedSizeIndex", All);
        int previous = (int)selected.GetValue(gameView);
        SetSelectedIndex(index);
        return previous;
    }

    /// <summary>Game 뷰 해상도 선택 번호를 바꾼다</summary>
    public static void SetSelectedIndex(int index)
    {
        EditorWindow gameView = EditorWindow.GetWindow(GameViewType, false, null, false);
        GameViewType.GetMethod("SizeSelectionCallback", All)?.Invoke(gameView, new object[] { index, null });
        gameView.Repaint();
    }
}
