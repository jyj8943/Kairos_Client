using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 곡 끝 FULL COMBO · ALL PERFECT 연출 실제 씬 확인 (기획서 3-4 「FC/AP 연출 (11/19)」, 재생 배율 TestSpeed.PlaybackRate).
/// 노트 8개짜리 채보를 테스트 안에서 만들어 전부 PERFECT로 친다.
/// - B1 보통 플레이: ALL PERFECT 연출이 결과 대기 타이머(판정 뒤 곡 시간)로 흐르고, 1.0초에 바늘 12시 · 개수 전체,
///   연출 중 ESC는 무시(일시정지 팝업 안 열림), 3초에 결과로. (InGameScene.TestNormalChart로 파일 대신 이 채보를 쓴다. chartId가 비어 기록은 남지 않는다)
/// - B2 테스트 플레이: 전부 PERFECT여도 연출 없이 3초 뒤 결과로
/// </summary>
public class ClearBannerPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const int NoteCount = 8;

    private InGameScene scene;
    private Canvas_InGame canvas;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InGameScene.TestNormalChart = null;
        scene = null;
        yield return new ExitPlayMode();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static ChartData Chart()
    {
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < NoteCount; i++) notes.Add(new NoteData { beat = 6f + i * 0.5f, lane = i % 4, type = NoteData.TypeShort });
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "곡 끝 연출 확인용",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = notes
        };
    }

    private IEnumerator Load(InGameEntryData entry)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        canvas = Get<Canvas_InGame>(scene, "canvas");
    }

    /// <summary>모든 노트를 노트 시각에 친다 (전부 PERFECT)</summary>
    private IEnumerator HitAll(float deadline)
    {
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        for (int i = 0; i < judge.Notes.Count; i++)
        {
            PlayNote note = judge.Notes[i];
            while (GameManager.Sound.SongTime < note.Time)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "노트 대기 시간 초과");
                yield return null;
            }
            Assert.IsFalse(Get<bool>(scene, "isPaused"), "포커스를 잃어 일시정지되면 안 된다");
            scene.TestPress(note.Lane, note.Time);
        }
        yield return null;
        Assert.IsTrue(judge.AllJudged);
    }

    /// <summary>결과로 넘어갈 때까지 기다려 "모든 노트 판정 뒤 곡 시간"을 돌려준다</summary>
    private IEnumerator WaitFinish(float deadline)
    {
        while (!Get<bool>(scene, "isFinished"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 대기 시간 초과");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator B1_NormalPlay_AllPerfectBanner_EscIgnored_ResultAt3s()
    {
        InGameScene.TestNormalChart = Chart();
        float speed = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;
        yield return Load(new InGameEntryData { songId = Song, chartType = ChartType.Custom, mode = InGameMode.Normal, scrollMultiplier = speed });
        ClearBanner banner = canvas.Banner;
        Assert.AreEqual(ClearType.None, banner.Type, "시작: 연출 없음");
        Assert.IsFalse(banner.IsPlaying);

        float deadline = TestSpeed.Deadline(20f);
        yield return HitAll(deadline);
        Assert.AreEqual(ClearType.AllPerfect, banner.Type, "보통 플레이 + 전부 PERFECT → ALL PERFECT");
        Assert.IsTrue(banner.IsPlaying);
        double judgedAt = Get<double>(scene, "allJudgedSongTime");

        // 1.0초: 바늘 12시 · 개수 전체 (곡 시간으로 1.0초가 지난 첫 프레임)
        while (GameManager.Sound.SongTime < judgedAt + 1.0)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "1.0초 대기 시간 초과");
            yield return null;
        }
        yield return null;
        float atNoon = banner.Elapsed;
        Assert.That(atNoon, Is.InRange(1.0f, 1.2f), "곡 시간으로 1.0초 뒤");
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, banner.MinuteHand.rectTransform.localEulerAngles.z), 1e-3f, "분침 12시");
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, banner.HourHand.rectTransform.localEulerAngles.z), 1e-3f, "시침 12시");
        banner.CountText.ForceMeshUpdate();
        Assert.AreEqual(NoteCount.ToString(), banner.CountText.GetParsedText(), "개수 = 전체");
        Assert.AreEqual("/ " + NoteCount, banner.CountTotal.text);
        Assert.AreEqual(1f, canvas.Combo.Group.alpha, 1e-4f, "연출 중 콤보는 쉬어도 숨지 않음");

        // 연출 중 ESC → 무시 (일시정지 팝업 안 열림)
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("곡 끝 연출 중 ESC 무시"));
        scene.OnBack();
        yield return null;
        Assert.IsFalse(Get<bool>(scene, "isPaused"), "ESC로 일시정지되지 않음");
        Assert.IsFalse(GameManager.UI.HasOpenPopup, "일시정지 팝업 안 열림");

        // 2.7초: 사라지는 중 (콤보 40%로 가는 중)
        while (GameManager.Sound.SongTime < judgedAt + 2.7)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "2.7초 대기 시간 초과");
            yield return null;
        }
        yield return null;
        ClearBannerFrame fading = banner.Frame;
        Assert.IsTrue(fading.Visible, "2.7초 아직 보임");
        Assert.Less(fading.RootAlpha, 0.6f, "사라지는 중");
        float fadingCombo = canvas.Combo.Group.alpha;
        Assert.That(fadingCombo, Is.InRange(0.4f, 0.75f), "콤보 숫자 40%로 가는 중");
        Assert.IsFalse(Get<bool>(scene, "isFinished"), "3초 전에는 결과 전");

        // 3초에 결과로 (타이머는 하나: 결과 대기 = 연출)
        yield return WaitFinish(deadline);
        double finishedAfter = Get<double>(scene, "finishedAfterJudged");
        Assert.GreaterOrEqual(finishedAfter, GameConstants.Play.ResultDelaySeconds - 1e-6, "3초 뒤 결과로");
        Assert.Less(finishedAfter, GameConstants.Play.ResultDelaySeconds + 0.1, "3초에 바로 결과로");
        Debug.Log($"[ClearBannerPlayTest] B1 보통 플레이 AP: 연출 AllPerfect, 1.0초 확인 시각 {atNoon:0.000}초(분침·시침 0°, 개수 {NoteCount}/{NoteCount}), ESC 무시, " +
                  $"2.7초 전체 알파 {fading.RootAlpha:0.00} · 콤보 알파 {fadingCombo:0.00}, 결과 진입 = 판정 뒤 곡 시간 {finishedAfter:0.000}초 (재생 배율 x{TestSpeed.CurrentRate})");
    }

    [UnityTest]
    public IEnumerator B2_TestPlay_AllPerfect_NoBanner_ResultAt3s()
    {
        ChartData chart = Chart();
        yield return Load(new InGameEntryData
        {
            songId = Song, chartType = ChartType.Custom, mode = InGameMode.TestPlay, scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = 0f, autoPlay = false }
        });
        float deadline = TestSpeed.Deadline(20f);
        yield return HitAll(deadline);
        ScoreCalculator score = Get<ScoreCalculator>(scene, "score");
        Assert.IsTrue(score.IsAllPerfect, "전부 PERFECT");
        Assert.AreEqual(ClearType.None, canvas.Banner.Type, "테스트 플레이는 연출 없음");
        Assert.IsFalse(canvas.Banner.IsPlaying);
        Assert.AreEqual(0f, canvas.Banner.GetComponent<CanvasGroup>().alpha, "숨김");
        double judgedAt = Get<double>(scene, "allJudgedSongTime");

        yield return WaitFinish(deadline);
        double finishedAfter = Get<double>(scene, "finishedAfterJudged");
        Assert.GreaterOrEqual(finishedAfter, GameConstants.Play.ResultDelaySeconds - 1e-6);
        Assert.Less(finishedAfter, GameConstants.Play.ResultDelaySeconds + 0.1);
        Debug.Log($"[ClearBannerPlayTest] B2 테스트 플레이 AP: 연출 None, 결과 진입 = 판정 뒤 곡 시간 {finishedAfter:0.000}초");
    }
}
