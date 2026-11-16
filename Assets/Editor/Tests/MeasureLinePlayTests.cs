using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 인게임 마디선 실제 씬 확인 (ScrollSpeedPlayTests와 같은 방식: EnterPlayMode + Bootstrapper).
/// Critical Velocity NORMAL 공식 채보를 테스트 플레이(자동 플레이, 0박부터)로 넘겨 확인한다 (공식 채보 파일은 고치지 않는다).
/// </summary>
public class MeasureLinePlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>같은 시각으로 볼 노트·선 시각 차이 (초)</summary>
    private const double SameTimeTolerance = 1e-6;

    /// <summary>한 판에서 맞춰 볼 선 수</summary>
    private const int CheckLineCount = 3;

    private InGameScene scene;
    private NoteJudge judge;
    private NoteLayer noteLayer;
    private MeasureLineLayer lineLayer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        scene = null;
        yield return new ExitPlayMode();
    }

    // ───── 준비 ─────

    private static ChartData LoadOfficial()
    {
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);
        return chart;
    }

    private static InGameEntryData AutoPlayEntry(ChartData chart, float multiplier)
    {
        return new InGameEntryData
        {
            songId = TestCharts.OfficialSongId,
            chartType = ChartType.Official,
            mode = InGameMode.TestPlay,
            scrollMultiplier = multiplier,
            testPlay = new TestPlayData { songId = TestCharts.OfficialSongId, chart = chart, startBeat = 0f, autoPlay = true }
        };
    }

    private IEnumerator LoadInGame(InGameEntryData entry)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        yield return WaitInGameStarted();
    }

    private IEnumerator WaitInGameStarted()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = UnityEngine.Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        judge = Get<NoteJudge>(scene, "judge");
        noteLayer = Get<NoteLayer>(scene, "noteLayer");
        lineLayer = Get<MeasureLineLayer>(scene, "measureLineLayer");
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    /// <summary>마디선 번호 → 같은 시각의 숏노트 번호 (없으면 짝을 만들지 않는다)</summary>
    private Dictionary<int, int> PairLinesWithShortNotes()
    {
        Dictionary<int, int> pairs = new Dictionary<int, int>();
        IReadOnlyList<PlayNote> notes = judge.Notes;
        for (int line = 0; line < lineLayer.LineCount; line++)
        {
            for (int n = 0; n < notes.Count; n++)
            {
                if (notes[n].IsLong || Math.Abs(notes[n].Time - lineLayer.GetTime(line)) > SameTimeTolerance) continue;
                pairs[line] = n;
                break;
            }
        }
        return pairs;
    }

    /// <summary>
    /// 매 프레임 화면에 함께 있는 (선, 같은 시각 숏노트)의 y를 비교한다. 서로 다른 선 CheckLineCount개를 맞춰 보면 끝낸다.
    /// </summary>
    /// <returns>최대 y 오차</returns>
    private IEnumerator CompareLinesWithNotes(List<string> log, Action<double> done)
    {
        Dictionary<int, int> pairs = PairLinesWithShortNotes();
        Assert.GreaterOrEqual(pairs.Count, CheckLineCount, "같은 시각 숏노트가 있는 마디선");

        HashSet<int> checkedLines = new HashSet<int>();
        double maxError = 0;
        float deadline = TestSpeed.Deadline(60f);
        while (checkedLines.Count < CheckLineCount)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "마디선·노트 비교 대기 시간 초과");
            yield return null;

            NoteView[] views = Get<NoteView[]>(noteLayer, "viewByNote");
            foreach (KeyValuePair<int, int> pair in pairs)
            {
                SpriteRenderer line = lineLayer.GetActiveLine(pair.Key);
                NoteView view = views[pair.Value];
                if (line == null || view == null || !view.gameObject.activeSelf || !line.gameObject.activeSelf) continue;

                double error = Math.Abs(line.transform.localPosition.y - view.transform.localPosition.y);
                maxError = Math.Max(maxError, error);
                if (checkedLines.Add(pair.Key))
                    log.Add($"선 {pair.Key}({lineLayer.GetTime(pair.Key):0.###}초) y {line.transform.localPosition.y:0.####} / 노트 {pair.Value} y {view.transform.localPosition.y:0.####}");
            }
        }
        done(maxError);
    }

    // ───── 항목 ─────

    /// <summary>4: 자동 플레이 도중 마디선 y = 같은 시각 숏노트 y (배속 x1.0, x3.0 각각)</summary>
    [UnityTest, Category("Slow")]
    public IEnumerator M4_LineMatchesNoteY([Values(1f, 3f)] float multiplier)
    {
        ChartData chart = LoadOfficial();
        yield return LoadInGame(AutoPlayEntry(chart, multiplier));
        Assert.AreEqual(multiplier, noteLayer.ScrollMultiplier, 1e-4f);

        List<string> log = new List<string>();
        double maxError = -1;
        yield return CompareLinesWithNotes(log, e => maxError = e);

        Debug.Log($"[MeasureLineTest] M4 x{multiplier:0.0}: 마디선 {lineLayer.LineCount}개, 풀 {lineLayer.PooledCount}개, 화면 {lineLayer.ActiveCount}개, " +
                  $"굵기 {lineLayer.LineThickness:0.#####}유닛, 폭 {lineLayer.LineWidth:0.###}유닛, 최대 y 오차 {maxError:0.000000} | {string.Join(" / ", log)}");
        Assert.Less(maxError, 1e-4, "선과 같은 시각 숏노트의 y");
        Assert.AreEqual(0, lineLayer.GrowCount, "플레이 중 마디선 새로 만들지 않음");
    }

    /// <summary>5: F5 재시작 후에도 선 개수·시각·풀이 처음과 같고 노트와 겹치며, 플레이 중 풀 밖으로 새 오브젝트가 생기지 않는다</summary>
    [UnityTest, Category("Slow")]
    public IEnumerator M5_RestartKeepsLinesAndPool()
    {
        ChartData chart = LoadOfficial();
        yield return LoadInGame(AutoPlayEntry(chart, 1f));

        int lineCount = lineLayer.LineCount;
        int pooled = lineLayer.PooledCount;
        double[] times = new double[lineCount];
        for (int i = 0; i < lineCount; i++) times[i] = lineLayer.GetTime(i);

        List<string> firstLog = new List<string>();
        double firstError = -1;
        yield return CompareLinesWithNotes(firstLog, e => firstError = e);
        int firstChildren = lineLayer.transform.childCount;
        Assert.AreEqual(0, lineLayer.GrowCount);
        Assert.AreEqual(pooled, firstChildren, "MeasureLines 자식 수 = 풀 크기");

        Assert.IsTrue(scene.TestPressRestartKey(), "F5 재시작");
        yield return null;
        yield return WaitInGameStarted();

        Assert.AreEqual(lineCount, lineLayer.LineCount, "재시작 후 선 개수");
        Assert.AreEqual(pooled, lineLayer.PooledCount, "재시작 후 풀 크기");
        for (int i = 0; i < lineCount; i++) Assert.AreEqual(times[i], lineLayer.GetTime(i), 1e-9, $"재시작 후 {i}번 선 시각");

        List<string> secondLog = new List<string>();
        double secondError = -1;
        yield return CompareLinesWithNotes(secondLog, e => secondError = e);

        Debug.Log($"[MeasureLineTest] M5: 마디선 {lineCount}개, 풀 {pooled}개 (자식 {firstChildren} → {lineLayer.transform.childCount}), GrowCount {lineLayer.GrowCount}, " +
                  $"y 오차 처음 {firstError:0.000000} / 재시작 후 {secondError:0.000000}");
        Assert.Less(firstError, 1e-4);
        Assert.Less(secondError, 1e-4);
        Assert.AreEqual(0, lineLayer.GrowCount, "재시작 후 플레이 중 마디선 새로 만들지 않음");
        Assert.AreEqual(pooled, lineLayer.transform.childCount, "재시작 후 MeasureLines 자식 수 = 풀 크기");
    }
}
