using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 공식 채보 AP 콤보 검사 (11/27, 카테고리 OfficialCharts). 공식 채보가 추가·수정될 때만 따로 돌린다 (Slow 5분 규칙에 들지 않게 Slow에서 뺐다. 실행 약 7~8분).
/// - 대상: Assets/Resources/Data/OfficialCharts 아래 모든 공식 채보 (CLAUDE.md "공식 채보는 Critical Velocity NORMAL만"의 예외: 공식 채보 전체가 검사 대상이다).
/// - 일반 플레이 진입 데이터(InGameMode.Normal, 공식 채보 chartId)로 실제 인게임 씬에 들어가 "완벽한 플레이어" 입력을 넣는다.
///   기존 자동 플레이(RunAutoPlay)는 지금 곡 시간으로 입력해 1프레임 오차가 있고 일반 플레이의 곡 끝 연출을 끄므로 쓰지 않는다.
///   대신 InGameScene.TestPress / TestRelease(사용자 입력과 같은 경로)에 노트 시각 그대로(오차 0ms)를 넘긴다. 목표 시각보다 조금 먼저(InjectLeadSeconds) 넣는다.
/// - 판정 기준: ① 전부 PERFECT, 1,000,000점, 랭크 AP, 곡 끝 연출 ALL PERFECT ② 실제 최고 콤보 = 인게임 최대 콤보 = ChartData.maxCombo = 기대값
///   ③ 유지 틱 결과 수 = TotalTicks = 기대값 (롱노트마다 틱 결과 수 = 그 롱노트 틱 수) ④ 결과 화면 MAX COMBO "N / N".
/// - 기대값 표(Expected)는 채보 파일에서 계산한 값이다 (노트 + 유지 틱). 표에 없는 공식 채보가 생기면 실패해 표에 추가하라고 알린다.
/// - 결과 화면까지 가므로 PlayerData.json에 기록이 남을 수 있다. 실행 전 파일을 복사해 두고 되돌린다.
/// </summary>
[Explicit("공식 채보 AP 콤보 검사: 공식 채보가 추가·수정될 때만 category OfficialCharts로 골라 돌린다 (커밋 전 !Slow 실행에 끼지 않게)")]
public class OfficialChartApComboPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>테스트 카테고리. 돌릴 때 category "OfficialCharts"로 고른다 (클래스가 [Explicit]라 커밋 전 "!Slow" 실행에는 들지 않는다)</summary>
    public const string Category = "OfficialCharts";

    /// <summary>테스트 하나의 제한 시간 (ms). 기본 180초로는 채보 4개(한 판 약 75초)를 다 못 돈다</summary>
    private const int TimeoutMs = 600000;

    /// <summary>목표 시각보다 이만큼 먼저(곡 시간) 입력을 넣는다 (떼기 전에 마지막 틱 시각이 지나가지 않게, FullComboPlayTests와 같은 값)</summary>
    private const double InjectLeadSeconds = 0.05;

    /// <summary>공식 채보 Resources 폴더 (곡마다 하위 폴더)</summary>
    private const string OfficialRoot = "Assets/Resources/Data/OfficialCharts";

    /// <summary>
    /// 기대값 (채보 파일에서 계산, 11/27): 노트 수, 롱노트 수, 유지 틱 수, 최대 콤보 (= 노트 + 유지 틱).
    /// 공식 채보가 추가·수정되면 이 표를 같이 고친다.
    /// </summary>
    private static readonly Dictionary<string, int[]> Expected = new Dictionary<string, int[]>
    {
        { "critical_velocity_easy", new[] { 217, 43, 405, 622 } },
        { "critical_velocity_normal", new[] { 420, 98, 557, 977 } },
        { "critical_velocity_hard", new[] { 822, 153, 644, 1466 } },
        { "send_my_love_tonight_easy", new[] { 162, 38, 392, 554 } },
    };

    private InGameScene scene;
    private string playerDataPath;
    private byte[] playerDataBackup;

    /// <summary>한 판의 기록</summary>
    private class PlayRecord
    {
        public string ChartId;
        public int Perfect, Great, Good, Miss, TotalNotes, LongCount;
        public int MaxCombo, MaxPossibleCombo, ChartMaxCombo;
        public int TickResults, TotalTicks;
        public int Score;
        public PlayRank Rank;
        public ClearType Clear;
        public string ResultComboText;
        public int ClampedReleases;
        public float RealSeconds;
        public List<string> TickMismatches = new List<string>();
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        playerDataPath = Path.Combine(Application.persistentDataPath, "PlayerData.json");
        playerDataBackup = File.Exists(playerDataPath) ? File.ReadAllBytes(playerDataPath) : null;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (scene != null) scene.TestJudgeObserver = null;
        scene = null;
        yield return new ExitPlayMode();
        // 결과 화면이 남긴 기록·마지막 배속을 실행 전으로 되돌린다
        if (playerDataBackup != null) File.WriteAllBytes(playerDataPath, playerDataBackup);
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    /// <summary>OfficialCharts 아래 모든 공식 채보 (곡 폴더 순, 곡 안에서는 ChartRepository 순서)</summary>
    private static List<ChartData> LoadAllOfficialCharts()
    {
        List<ChartData> charts = new List<ChartData>();
        string[] folders = AssetDatabase.GetSubFolders(OfficialRoot);
        System.Array.Sort(folders, System.StringComparer.Ordinal);
        foreach (string folder in folders)
        {
            string songId = Path.GetFileName(folder);
            List<ChartData> songCharts = ChartRepository.GetCharts(songId, ChartType.Official);
            if (songCharts.Count == 0) Debug.Log($"[OfficialApCombo] {songId}: 공식 채보 없음 (빈 폴더), 건너뜀");
            charts.AddRange(songCharts);
        }
        return charts;
    }

    /// <summary>
    /// 1: 모든 공식 채보를 오차 0ms로 친다 (롱노트는 끝 시각에 정확히 뗌) → AP, 콤보·틱이 기대값과 같다.
    /// 한 채보가 실패해도 나머지를 다 돌리고 마지막에 표로 알린다.
    /// </summary>
    [UnityTest, Category(Category), Timeout(TimeoutMs)]
    public IEnumerator AP1_AllOfficialCharts_PerfectPlay()
    {
        List<ChartData> charts = LoadAllOfficialCharts();
        Assert.IsNotEmpty(charts, "공식 채보가 있어야 한다");

        List<PlayRecord> records = new List<PlayRecord>();
        List<string> failures = new List<string>();
        foreach (ChartData chart in charts)
        {
            PlayRecord record = new PlayRecord();
            yield return Play(chart, 0, record);
            records.Add(record);
            CheckRecord(record, failures);
        }

        string table = FormatTable(records, "AP1 공식 채보 전체 (롱노트 끝 +0ms 뗌)");
        Debug.Log(table);
        Assert.IsEmpty(failures, string.Join("\n", failures) + "\n" + table);
    }

    /// <summary>
    /// 2: Critical Velocity NORMAL에서 롱노트를 모두 끝 −110ms / +100ms에 뗀다 (끝 판정 범위 ±120ms 안) → AP 유지, 최대 콤보 그대로.
    /// −110ms가 롱노트 시작보다 앞이면 시작 시각에, +100ms가 같은 라인 다음 노트보다 뒤면 다음 노트 시각에 뗀다 (몇 개인지 기록).
    /// </summary>
    [UnityTest, Category(Category), Timeout(TimeoutMs)]
    public IEnumerator AP2_Normal_ReleaseOffsets_KeepAp()
    {
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);

        List<PlayRecord> records = new List<PlayRecord>();
        List<string> failures = new List<string>();
        foreach (double releaseMs in new[] { -110.0, 100.0 })
        {
            PlayRecord record = new PlayRecord();
            yield return Play(chart, releaseMs, record);
            record.ChartId += $" (뗌 {releaseMs:+0;-0}ms)";
            records.Add(record);
            CheckRecord(record, failures);
        }

        string table = FormatTable(records, "AP2 Critical Velocity NORMAL 롱노트 뗌 변형");
        Debug.Log(table);
        Assert.IsEmpty(failures, string.Join("\n", failures) + "\n" + table);
    }

    /// <summary>기준 1~4를 확인해 어긋난 것을 failures에 붙인다</summary>
    private static void CheckRecord(PlayRecord r, List<string> failures)
    {
        string baseId = r.ChartId.Split(' ')[0];
        void Check(bool ok, string message) { if (!ok) failures.Add($"{r.ChartId}: {message}"); }

        Check(Expected.TryGetValue(baseId, out int[] expected), "기대값 표(Expected)에 없는 공식 채보 — 채보 파일에서 노트·롱노트·유지 틱·최대 콤보를 계산해 표에 추가할 것");
        if (expected == null) expected = new[] { -1, -1, -1, -1 };

        // 1. AP
        Check(r.Perfect == r.TotalNotes && r.Great == 0 && r.Good == 0 && r.Miss == 0, $"전부 PERFECT 아님 (P/G/Go/M {r.Perfect}/{r.Great}/{r.Good}/{r.Miss}, 노트 {r.TotalNotes})");
        Check(r.Score == GameConstants.Play.MaxScore, $"점수 {r.Score}");
        Check(r.Rank == PlayRank.AP, $"랭크 {r.Rank}");
        Check(r.Clear == ClearType.AllPerfect, $"곡 끝 연출 {r.Clear}");
        Check(r.TotalNotes == expected[0], $"노트 수 {r.TotalNotes} ≠ 기대 {expected[0]}");
        Check(r.LongCount == expected[1], $"롱노트 수 {r.LongCount} ≠ 기대 {expected[1]}");
        // 2. 콤보
        Check(r.MaxCombo == r.MaxPossibleCombo && r.MaxPossibleCombo == r.ChartMaxCombo && r.ChartMaxCombo == expected[3],
            $"최대 콤보 실제 {r.MaxCombo} / 인게임 {r.MaxPossibleCombo} / ChartData {r.ChartMaxCombo} / 기대 {expected[3]}");
        // 3. 유지 틱
        Check(r.TickResults == r.TotalTicks && r.TotalTicks == expected[2], $"유지 틱 결과 {r.TickResults} / TotalTicks {r.TotalTicks} / 기대 {expected[2]}");
        Check(r.TickMismatches.Count == 0, $"틱 수가 다른 롱노트 {r.TickMismatches.Count}개: {string.Join(" / ", r.TickMismatches)}");
        // 4. 결과 화면
        string n = SongSelectRecordFormat.FormatCombo(expected[3]);
        Check(r.ResultComboText == $"{n} / {n}", $"결과 MAX COMBO \"{r.ResultComboText}\" ≠ \"{n} / {n}\"");
    }

    private static string FormatTable(List<PlayRecord> records, string title)
    {
        StringBuilder sb = new StringBuilder($"[OfficialApCombo] {title}\n");
        sb.Append("| 채보 | P/G/Go/M | 노트 | 롱노트 | MaxCombo | MaxPossible | ChartData.maxCombo | 틱 결과 | TotalTicks | 점수 | 랭크 | 연출 | 결과 MAX COMBO | 당긴 뗌 | 실제 초 |\n");
        foreach (PlayRecord r in records)
        {
            sb.Append($"| {r.ChartId} | {r.Perfect}/{r.Great}/{r.Good}/{r.Miss} | {r.TotalNotes} | {r.LongCount} | {r.MaxCombo} | {r.MaxPossibleCombo} | {r.ChartMaxCombo} | " +
                      $"{r.TickResults} | {r.TotalTicks} | {r.Score} | {r.Rank} | {r.Clear} | {r.ResultComboText} | {r.ClampedReleases} | {r.RealSeconds:0.0} |\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 일반 플레이로 채보 하나를 끝까지 친다. 누름은 노트 시각 그대로, 롱노트 뗌은 끝 + releaseMs. 결과 화면 MAX COMBO 글자까지 record에 적는다.
    /// </summary>
    private IEnumerator Play(ChartData chart, double releaseMs, PlayRecord record)
    {
        record.ChartId = chart.chartId;
        record.ChartMaxCombo = chart.maxCombo;
        float startRealtime = Time.realtimeSinceStartup;

        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, new InGameEntryData
        {
            songId = chart.songId,
            chartId = chart.chartId,
            chartType = ChartType.Official,
            mode = InGameMode.Normal,
            scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier
        }), $"{chart.chartId} 인게임 진입");

        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{chart.chartId} 인게임 시작 대기 시간 초과");
            scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        ScoreCalculator score = Get<ScoreCalculator>(scene, "score");
        TimingMap timing = Get<TimingMap>(scene, "timing");
        Canvas_InGame canvas = Get<Canvas_InGame>(scene, "canvas");

        // 롱노트마다 받은 틱 결과 수 (같은 틱이 두 번 나오거나 빠지면 그 롱노트의 수가 달라진다)
        int[] ticksByNote = new int[judge.TotalCount];
        int tickResults = 0;
        scene.TestJudgeObserver = result =>
        {
            if (result.Kind != JudgeKind.Tick) return;
            tickResults++;
            ticksByNote[result.NoteIndex]++;
        };

        // 입력 목록: (시각, 라인, 누름, 같은 시각 순서). 같은 시각이면 앞 롱노트 뗌 → 누름 → (시작 시각으로 당긴) 그 롱노트 뗌
        List<(double time, int lane, bool press, int order)> inputs = new List<(double, int, bool, int)>();
        double last = 0;
        for (int i = 0; i < judge.Notes.Count; i++)
        {
            PlayNote n = judge.Notes[i];
            inputs.Add((n.Time, n.Lane, true, 1));
            last = System.Math.Max(last, n.EndTime);
            if (!n.IsLong) continue;
            record.LongCount++;
            double release = n.EndTime + releaseMs / 1000.0;
            bool clamped = false;
            if (release < n.Time) { release = n.Time; clamped = true; }
            double nextStart = NextStartOnLane(judge, i);
            if (release > nextStart) { release = nextStart; clamped = true; }
            if (clamped) record.ClampedReleases++;
            inputs.Add((release, n.Lane, false, release <= n.Time ? 2 : 0));
        }
        inputs.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.order.CompareTo(b.order));

        deadline = TestSpeed.Deadline(last + 30.0);
        int next = 0;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{chart.chartId} 플레이 대기 시간 초과 (판정 {judge.JudgedCount}/{judge.TotalCount})");
            for (; next < inputs.Count && GameManager.Sound.SongTime >= inputs[next].time - InjectLeadSeconds; next++)
            {
                if (inputs[next].press) scene.TestPress(inputs[next].lane, inputs[next].time);
                else scene.TestRelease(inputs[next].lane, inputs[next].time);
            }
            yield return null;
        }
        // 모든 노트 판정 프레임의 연출 종류는 Update에서 정해진다
        yield return null;

        record.Perfect = score.PerfectCount;
        record.Great = score.GreatCount;
        record.Good = score.GoodCount;
        record.Miss = score.MissCount;
        record.TotalNotes = score.TotalNotes;
        record.MaxCombo = score.MaxCombo;
        record.MaxPossibleCombo = score.MaxPossibleCombo;
        record.TickResults = tickResults;
        record.TotalTicks = judge.TotalTicks;
        record.Score = score.Score;
        record.Rank = score.Rank;
        record.Clear = canvas.Banner.Type;
        for (int i = 0; i < judge.Notes.Count; i++)
        {
            PlayNote n = judge.Notes[i];
            if (ticksByNote[i] == n.TickCount) continue;
            double beat = timing.SecondsToBeat(n.Time), endBeat = timing.SecondsToBeat(n.EndTime);
            record.TickMismatches.Add($"노트 {i} 라인 {n.Lane} {beat:0.###}~{endBeat:0.###}박 BPM {timing.BpmAt(beat):0.##}: 틱 결과 {ticksByNote[i]} / 틱 {n.TickCount}");
        }
        scene.TestJudgeObserver = null;

        // 결과 화면 MAX COMBO 글자 (결과 연출은 테스트 실행 중 건너뛴다)
        deadline = TestSpeed.Deadline(20f);
        Canvas_Result result = null;
        while (result == null || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{chart.chartId} 결과 씬 대기 시간 초과");
            yield return null;
            result = Object.FindFirstObjectByType<Canvas_Result>();
        }
        yield return null;
        record.ResultComboText = Get<TMP_Text>(result, "maxComboText").text + " " + Get<TMP_Text>(result, "comboTotalText").text;
        record.RealSeconds = Time.realtimeSinceStartup - startRealtime;
        scene = null;
        Debug.Log($"[OfficialApCombo] {chart.chartId} 뗌 {releaseMs:+0;-0;0}ms: P/G/Go/M {record.Perfect}/{record.Great}/{record.Good}/{record.Miss}, " +
                  $"콤보 {record.MaxCombo}/{record.MaxPossibleCombo} (ChartData {record.ChartMaxCombo}), 틱 {record.TickResults}/{record.TotalTicks}, 점수 {record.Score}, " +
                  $"랭크 {record.Rank}, 연출 {record.Clear}, 결과 \"{record.ResultComboText}\", 당긴 뗌 {record.ClampedReleases}, 실제 {record.RealSeconds:0.0}초");
    }

    /// <summary>같은 라인에서 noteIndex 다음 노트의 시작 시각 (없으면 +∞)</summary>
    private static double NextStartOnLane(NoteJudge judge, int noteIndex)
    {
        int lane = judge.Notes[noteIndex].Lane;
        for (int i = noteIndex + 1; i < judge.Notes.Count; i++)
        {
            if (judge.Notes[i].Lane == lane) return judge.Notes[i].Time;
        }
        return double.PositiveInfinity;
    }
}
