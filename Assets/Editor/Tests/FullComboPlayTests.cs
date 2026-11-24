using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 롱노트를 살짝 일찍(끝 −110ms) 떼고도 FULL COMBO일 때 연출 개수 캡슐과 결과 화면 MAX COMBO가 "N / N"인지 실제 씬에서 확인한다.
/// - 작은 채보(BPM 경계에 걸친 롱노트 포함)를 테스트 안에서 만들어 보통 플레이로 친다 (InGameScene.TestNormalChart, chartId가 비어 기록은 남지 않는다).
/// - 첫 노트를 +60ms(GREAT)로 쳐서 ALL PERFECT가 아니라 FULL COMBO가 되게 한다.
/// - 입력은 InGameScene.TestPress / TestRelease (사용자 입력과 같은 경로)로 목표 시각보다 조금 먼저 넣고 목표 시각을 입력 시각으로 넘긴다.
/// </summary>
public class FullComboPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>목표 시각보다 이만큼 먼저(곡 시간) 입력을 넣는다 (떼기 전에 마지막 틱 시각이 지나가지 않게)</summary>
    private const double InjectLeadSeconds = 0.05;

    private InGameScene scene;
    private ScoreCalculator score;
    private string capsuleText;
    private string resultText;

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

    private static NoteData Short(float beat, int lane) => new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };
    private static NoteData Long(float beat, float endBeat, int lane) => new NoteData { beat = beat, endBeat = endBeat, lane = lane, type = NoteData.TypeLong };

    private static ChartData SmallChart()
    {
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "FC 콤보 확인용",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData>
            {
                new TimingPointData { beat = 0f, bpm = 150.07f },
                new TimingPointData { beat = 8f, bpm = 150.68f }
            },
            notes = new List<NoteData> { Short(6f, 0), Long(6.5f, 8.5f, 1), Long(7.5f, 9f, 2), Short(9f, 3), Long(10f, 10.5f, 0), Long(10f, 12f, 3) }
        };
    }

    /// <summary>
    /// 보통 플레이로 채보를 끝까지 친다: 첫 노트만 +60ms, 롱노트는 끝 + releaseMs에 뗀다. FC 연출 글자와 결과 화면 글자를 읽어 둔다.
    /// </summary>
    private IEnumerator PlayToResult(ChartData chart, double releaseMs)
    {
        InGameScene.TestNormalChart = chart;
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        float speed = PlayerDataManager.HasInstance ? GameManager.PlayerData.LastScrollMultiplier : 1f;
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame,
            new InGameEntryData { songId = Song, chartType = ChartType.Custom, mode = InGameMode.Normal, scrollMultiplier = speed }));
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        score = Get<ScoreCalculator>(scene, "score");
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        List<(double time, int lane, bool press)> inputs = new List<(double, int, bool)>();
        double last = 0;
        for (int i = 0; i < judge.Notes.Count; i++)
        {
            PlayNote n = judge.Notes[i];
            inputs.Add((n.Time + (i == 0 ? 0.06 : 0), n.Lane, true));
            inputs.Add((n.EndTime + (n.IsLong ? releaseMs / 1000.0 : 0.07), n.Lane, false));
            last = System.Math.Max(last, n.EndTime);
        }
        inputs.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.press.CompareTo(b.press));

        deadline = TestSpeed.Deadline(last + 20.0);
        int next = 0;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "플레이 대기 시간 초과");
            for (; next < inputs.Count && GameManager.Sound.SongTime >= inputs[next].time - InjectLeadSeconds; next++)
            {
                if (inputs[next].press) scene.TestPress(inputs[next].lane, inputs[next].time);
                else scene.TestRelease(inputs[next].lane, inputs[next].time);
            }
            yield return null;
        }

        // 개수가 다 올라간 뒤(1.0초)의 캡슐 글자
        double judgedAt = GameManager.Sound.SongTime;
        while (GameManager.Sound.SongTime < judgedAt + 1.3)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "연출 대기 시간 초과");
            yield return null;
        }
        Assert.AreEqual(ClearType.FullCombo, canvas.Banner.Type, $"FULL COMBO 연출 (MISS {score.MissCount})");
        canvas.Banner.CountText.ForceMeshUpdate();
        capsuleText = canvas.Banner.CountText.GetParsedText() + " " + canvas.Banner.CountTotal.text;

        deadline = TestSpeed.Deadline(20f);
        Canvas_Result result = null;
        while (result == null || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 씬 대기 시간 초과");
            yield return null;
            result = Object.FindFirstObjectByType<Canvas_Result>();
        }
        yield return null;
        resultText = Get<TMP_Text>(result, "maxComboText").text + " " + Get<TMP_Text>(result, "comboTotalText").text;
        Debug.Log($"[FullComboPlay] 노트 {judge.TotalCount}, 롱노트 뗌 {releaseMs:+0;-0;0}ms: P/G/Go/M {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, " +
                  $"최대 콤보 {score.MaxCombo}/{score.MaxPossibleCombo}, 점수 {score.Score}, 정확도 {score.Accuracy:0.00}%, 연출 캡슐 \"{capsuleText}\", 결과 MAX COMBO \"{resultText}\"");
    }

    private void AssertAllSame()
    {
        Assert.AreEqual(0, score.MissCount);
        Assert.AreEqual(1, score.GreatCount, "첫 노트만 GREAT");
        Assert.AreEqual(score.MaxPossibleCombo, score.MaxCombo);
        string total = score.MaxPossibleCombo.ToString();
        Assert.AreEqual($"{total} / {total}", capsuleText, "FC 연출 캡슐");
        string formatted = SongSelectRecordFormat.FormatCombo(score.MaxPossibleCombo);
        Assert.AreEqual($"{formatted} / {formatted}", resultText, "결과 MAX COMBO");
    }

    /// <summary>롱노트를 모두 끝 −110ms에 떼고 FC → 연출 캡슐·결과 MAX COMBO가 N / N</summary>
    [UnityTest]
    public IEnumerator EarlyRelease_FullCombo_ShowsFullCount()
    {
        yield return PlayToResult(SmallChart(), -110);
        AssertAllSame();
    }
}
