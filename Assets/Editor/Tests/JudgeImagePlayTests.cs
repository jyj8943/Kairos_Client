using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 기어 판정 글자 이미지 (11/19) 실제 씬 확인. 테스트 플레이(첫 노트 16박)로 들어가 첫 노트 전에 Canvas_InGame.ShowJudge를 불러 본다.
/// J1. 판정마다 Img_Judge 이미지·크기(원본 × 0.5)가 맞음, PERFECT·MISS에서 FAST/SLOW 안 뜸, GREAT·GOOD에서 FAST/SLOW 이미지 맞음,
///     애니메이션: 표시 직후 크기 0.7 → 0.08초 뒤 1, 유지 뒤 알파가 줄어 Image 꺼짐.
/// </summary>
public class JudgeImagePlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

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

    [UnityTest]
    public IEnumerator J1_JudgeImages_FastSlowRule_Animation()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }

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
                    chartName = "판정 이미지 확인",
                    keyCount = GameConstants.Editor.LaneCount,
                    level = 1,
                    timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
                    notes = notes
                }
            }
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
        Img_Judge judge = canvas.Judge;
        Img_FastSlow fastSlow = canvas.FastSlowImage;
        Image judgeImage = judge.ImageComponent;
        Image fastSlowImage = fastSlow.ImageComponent;
        Assert.IsFalse(judgeImage.raycastTarget, "판정 이미지 Raycast Target 끔");
        Assert.IsTrue(judgeImage.preserveAspect, "Preserve Aspect 켬");

        // 3. 판정마다 이미지 · PERFECT·MISS는 FAST/SLOW 없음 (FastSlowRules가 None을 넘김) · GREAT·GOOD은 FAST/SLOW 이미지
        (JudgeType judgeType, FastSlow dir)[] cases =
        {
            (JudgeType.Perfect, FastSlow.None), (JudgeType.Great, FastSlow.Fast), (JudgeType.Good, FastSlow.Slow), (JudgeType.Miss, FastSlow.None)
        };
        foreach ((JudgeType judgeType, FastSlow dir) in cases)
        {
            fastSlow.ResetView();
            canvas.ShowJudge(judgeType, dir, 0, 0);
            Sprite expected = JudgeSprites.Get(judgeType);
            Assert.AreSame(expected, judgeImage.sprite, judgeType + " 이미지");
            Assert.AreEqual(JudgeSprites.DisplaySize(expected), judgeImage.rectTransform.sizeDelta, judgeType + " 크기 = 원본 × 0.5");
            if (dir == FastSlow.None)
            {
                Assert.IsFalse(fastSlowImage.enabled, judgeType + "에는 FAST/SLOW 없음");
            }
            else
            {
                Assert.IsTrue(fastSlowImage.enabled, judgeType + " " + dir + " 보임");
                Assert.AreSame(JudgeSprites.Get(dir), fastSlowImage.sprite, dir + " 이미지");
                Assert.AreEqual(JudgeSprites.DisplaySize(fastSlowImage.sprite), fastSlowImage.rectTransform.sizeDelta, dir + " 크기");
            }
        }

        // 4. 애니메이션: 표시 직후 0.7 → 0.08초 뒤 1, 유지 뒤 사라져 Image 꺼짐
        canvas.ShowJudge(JudgeType.Great, FastSlow.None, 0, 0);
        Assert.AreEqual(0.7f, judgeImage.rectTransform.localScale.x, 1e-4f, "표시 직후 0.7배");
        Assert.AreEqual(0f, judgeImage.color.a, 1e-4f, "표시 직후 투명");
        while (judge.Animation.Elapsed < 0.08f)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "등장 대기 시간 초과");
            yield return null;
        }
        Assert.AreEqual(1f, judgeImage.rectTransform.localScale.x, 1e-4f, "0.08초 뒤 1배");
        Assert.AreEqual(1f, judgeImage.color.a, 1e-4f, "불투명");

        float lastAlpha = 1f;
        bool faded = false;
        while (judge.IsVisible)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "퇴장 대기 시간 초과");
            if (judge.Animation.Elapsed > judge.Animation.HoldSeconds && judgeImage.color.a < lastAlpha) faded = true;
            lastAlpha = judgeImage.color.a;
            yield return null;
        }
        Assert.IsTrue(faded, "유지 뒤 알파가 줄어듦");
        Assert.IsFalse(judgeImage.enabled, "퇴장 뒤 Image 꺼짐");
        Assert.AreEqual(0, Get<ScoreCalculator>(scene, "score").JudgedCount, "첫 노트 판정 전에 끝남");
        Debug.Log($"[JudgeImagePlayTest] J1: PERFECT {JudgeSprites.DisplaySize(JudgeSprites.Get(JudgeType.Perfect))}, 애니메이션 0.7 → 1 → 퇴장 (경과 {judge.Animation.Elapsed:0.00}초)");
    }
}
