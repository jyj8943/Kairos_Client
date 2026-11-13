using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 롱노트 인게임 동작 자동 확인 (롱노트 판정 세부 규칙 11/10, 기획서 2-1). 실제 InGameScene을 테스트 플레이로 돌린다.
/// - EditMode 테스트에서 EnterPlayMode로 들어가 Bootstrapper가 실제 게임과 같게 전역 매니저를 만든다.
/// - 채보는 파일을 만들지 않고 코드로 만든 ChartData를 InGameEntryData.testPlay로 넘긴다 (곡: Critical Velocity, 150.61 BPM).
/// - 입력은 InGameScene.TestPress / TestRelease (사용자 입력과 같은 OnUserLanePressed / OnUserLaneReleased 경로)로 넣는다.
///   곡 시간이 목표 시각 바로 앞(InjectLeadSeconds 전)에 오면 목표 시각을 입력 시각으로 넘긴다.
/// - 판정 결과는 InGameScene.TestJudgeObserver로 받아 기록한다.
/// </summary>
public class LongNotePlayTests
{
    private const string Song = "critical_velocity";
    private const int T = GameConstants.Editor.TicksPerBeat;

    /// <summary>목표 시각보다 이만큼 먼저(곡 시간) 입력을 넣는다 (프레임이 늦어 목표 시각을 지나치지 않게)</summary>
    private const double InjectLeadSeconds = 0.05;

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private InGameScene scene;
    private NoteJudge judge;
    private ScoreCalculator score;
    private readonly List<JudgeResult> results = new List<JudgeResult>();

    // ───── 준비 ─────

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        results.Clear();
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (scene != null) scene.TestJudgeObserver = null;
        scene = null;
        yield return new ExitPlayMode();
    }

    private static NoteData Short(float beat, int lane) => new NoteData { beat = beat, lane = lane, type = NoteData.TypeShort };
    private static NoteData Long(float beat, float endBeat, int lane) => new NoteData { beat = beat, endBeat = endBeat, lane = lane, type = NoteData.TypeLong };

    private static ChartData Chart(params NoteData[] notes)
    {
        return new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            chartId = null,
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "롱노트 자동 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData>(notes)
        };
    }

    /// <summary>테스트 플레이로 인게임에 들어가 재생이 시작될 때까지 기다린다.</summary>
    private IEnumerator StartPlay(ChartData chart, bool autoPlay = false, string songId = Song)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 초기화 대기 시간 초과");
            yield return null;
        }

        InGameEntryData entry = new InGameEntryData
        {
            songId = songId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = songId, chart = chart, startBeat = 0f, autoPlay = autoPlay }
        };
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry), "인게임 씬 로드 요청 실패");

        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            scene = UnityEngine.Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && Get<bool>(scene, "isPlaying")) break;
            yield return null;
        }

        judge = Get<NoteJudge>(scene, "judge");
        score = Get<ScoreCalculator>(scene, "score");
        scene.TestJudgeObserver = r => results.Add(r);
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, Private).SetValue(target, value);
    }

    private static double SongTime => GameManager.Sound.SongTime;

    /// <summary>곡 시간이 time이 될 때까지 기다린다 (일시정지 중에는 곡 시간이 멈춘다).</summary>
    private static IEnumerator WaitSongTime(double time, float timeoutSeconds = 30f)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (SongTime < time)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"곡 시간 {time:0.000}초 대기 시간 초과 (지금 {SongTime:0.000}초)");
            yield return null;
        }
    }

    /// <summary>목표 시각 바로 앞까지 기다렸다가 목표 시각으로 누른다.</summary>
    private IEnumerator PressAt(int lane, double time)
    {
        yield return WaitSongTime(time - InjectLeadSeconds);
        Assert.Less(SongTime, time + 0.03, $"누를 시각 {time:0.000}초를 이미 지났습니다 (지금 {SongTime:0.000}초)");
        scene.TestPress(lane, time);
    }

    private IEnumerator ReleaseAt(int lane, double time)
    {
        yield return WaitSongTime(time - InjectLeadSeconds);
        Assert.Less(SongTime, time + 0.03, $"뗄 시각 {time:0.000}초를 이미 지났습니다 (지금 {SongTime:0.000}초)");
        scene.TestRelease(lane, time);
    }

    /// <summary>노트가 판정될 때까지 기다린다.</summary>
    private IEnumerator WaitJudged(int noteIndex, float timeoutSeconds = 15f)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!judge.IsJudged(noteIndex))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"노트 {noteIndex} 판정 대기 시간 초과");
            yield return null;
        }
    }

    /// <summary>라인의 첫 롱노트 번호</summary>
    private int LongIndex(int lane, int nth = 0)
    {
        for (int i = 0; i < judge.TotalCount; i++)
        {
            if (judge.Notes[i].Lane == lane && judge.Notes[i].IsLong && nth-- == 0) return i;
        }
        Assert.Fail($"라인 {lane}에 롱노트가 없습니다");
        return -1;
    }

    private int ShortIndex(int lane, int nth = 0)
    {
        for (int i = 0; i < judge.TotalCount; i++)
        {
            if (judge.Notes[i].Lane == lane && !judge.Notes[i].IsLong && nth-- == 0) return i;
        }
        Assert.Fail($"라인 {lane}에 숏노트가 없습니다");
        return -1;
    }

    private int CountTicks(int noteIndex) => results.FindAll(r => r.NoteIndex == noteIndex && r.Kind == JudgeKind.Tick).Count;
    private JudgeResult Final(int noteIndex) => results.Find(r => r.NoteIndex == noteIndex && (r.Kind == JudgeKind.LongFinal || r.Kind == JudgeKind.Short));
    private bool HasStart(int noteIndex, out JudgeResult start)
    {
        start = results.Find(r => r.NoteIndex == noteIndex && r.Kind == JudgeKind.LongStart);
        return results.Exists(r => r.NoteIndex == noteIndex && r.Kind == JudgeKind.LongStart);
    }

    private int EndKeySounds => Get<int>(scene, "longEndKeySoundCount");

    private string Describe(string label, int noteIndex)
    {
        JudgeResult final = Final(noteIndex);
        string start = HasStart(noteIndex, out JudgeResult s) ? $"{s.Type}({s.DeltaMs:+0.0;-0.0}ms)" : "없음";
        return $"[LongNoteTest] {label}: 시작 {start}, 끝 {final.EndType}({final.DeltaMs:+0.0;-0.0}ms), 종합 {final.Type}, 틱 {CountTicks(noteIndex)}/{judge.Notes[noteIndex].TickCount}, " +
               $"콤보 {score.Combo} (최대 {score.MaxCombo}), 끝 키음 {EndKeySounds}, 점수 {score.Score}";
    }

    // ───── 인게임 항목 ─────

    /// <summary>P1·P11: 정확히 누르고 떼기 → 종합 PERFECT, 틱 전부, 콤보 1 + 틱. 누르는 동안 머리(아래 끝 + 숏노트 높이 ÷ 2)가 판정선, 위 끝은 내려온다</summary>
    [UnityTest]
    public IEnumerator P1_P11_ExactPressRelease()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);

        // P11: 누르는 동안 막대 위치 두 번 샘플
        yield return WaitSongTime(note.Time + 0.2);
        LongNoteView view = Get<LongNoteView[]>(Get<NoteLayer>(scene, "noteLayer"), "longViewByNote")[n];
        GearView gear = Get<GearView>(scene, "gear");
        Vector2 noteSize = Get<NoteLayer>(scene, "noteLayer").NoteSize;
        float bottom1 = view.BottomY, top1 = view.TopY;
        yield return WaitSongTime(note.Time + 0.5);
        float bottom2 = view.BottomY, top2 = view.TopY;
        Debug.Log($"[LongNoteTest] P11 표시: 판정선 {gear.JudgeLineY:0.000}, 아래 끝 {bottom1:0.000} → {bottom2:0.000} (머리 중심 {bottom1 + noteSize.y / 2:0.000} → {bottom2 + noteSize.y / 2:0.000}), 위 끝 {top1:0.000} → {top2:0.000}");

        yield return ReleaseAt(0, note.EndTime);
        yield return WaitJudged(n);
        Debug.Log(Describe("P1 정확히 누르고 떼기", n));

        JudgeResult final = Final(n);
        Assert.AreEqual(JudgeType.Perfect, final.Type, "종합");
        Assert.AreEqual(note.TickCount, CountTicks(n), "틱 전부 인정");
        Assert.AreEqual(7, note.TickCount);
        Assert.AreEqual(1 + note.TickCount, score.Combo, "콤보 = 1 + 틱");
        Assert.AreEqual(1, EndKeySounds, "끝 키음");

        Assert.AreEqual(gear.JudgeLineY, bottom1 + noteSize.y / 2, 0.01, "P11: 누르는 동안 머리 중심이 판정선");
        Assert.AreEqual(gear.JudgeLineY, bottom2 + noteSize.y / 2, 0.01, "P11: 머리는 판정선에 고정");
        Assert.Less(top2, top1, "P11: 위 끝은 계속 내려온다");
    }

    /// <summary>P2: 끝 ±120ms 안에서 떼면 시작 판정 그대로 ① 시작 +60 / 끝 +0 → GREAT (끝이 올려 주지 않음) ② 시작 +0 / 끝 −100 → PERFECT (끝이 깎지 않음)</summary>
    [UnityTest]
    public IEnumerator P2_ReleaseKeepsStartJudge()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0), Long(10f, 12f, 1)));
        int a = LongIndex(0), b = LongIndex(1);

        yield return PressAt(0, judge.Notes[a].Time + 0.060);
        yield return ReleaseAt(0, judge.Notes[a].EndTime);
        yield return PressAt(1, judge.Notes[b].Time);
        yield return ReleaseAt(1, judge.Notes[b].EndTime - 0.100);
        yield return WaitJudged(b);

        Debug.Log(Describe("P2① 시작 +60 / 끝 +0", a));
        Debug.Log(Describe("P2② 시작 +0 / 끝 −100", b));
        Assert.AreEqual(JudgeType.Great, Final(a).StartType);
        Assert.AreEqual(JudgeType.Great, Final(a).EndType, "끝 판정 = 시작 판정");
        Assert.AreEqual(JudgeType.Great, Final(a).Type, "①");
        Assert.AreEqual(JudgeType.Perfect, Final(b).StartType);
        Assert.AreEqual(JudgeType.Perfect, Final(b).EndType, "끝 판정 = 시작 판정");
        Assert.AreEqual(JudgeType.Perfect, Final(b).Type, "②");
    }

    /// <summary>P3: 끝 −130ms에 뗌 → 끝 MISS·종합 MISS, 그 순간 콤보 0, 남은 틱 미인정</summary>
    [UnityTest]
    public IEnumerator P3_EarlyRelease()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);
        yield return ReleaseAt(0, note.EndTime - 0.130);
        yield return WaitJudged(n);
        int maxComboAtRelease = score.MaxCombo;
        yield return WaitSongTime(note.EndTime + 0.3);
        Debug.Log(Describe("P3 끝 −130ms에 뗌", n));

        int expectedTicks = 0;
        for (int i = 0; i < note.TickCount; i++) if (note.GetTickTime(i) <= note.EndTime - 0.130) expectedTicks++;

        Assert.AreEqual(JudgeType.Miss, Final(n).EndType);
        Assert.AreEqual(JudgeType.Miss, Final(n).Type);
        Assert.AreEqual(0, score.Combo, "종합 MISS 순간 콤보 0");
        Assert.AreEqual(expectedTicks, CountTicks(n), "뗀 시각까지의 틱만");
        Assert.AreEqual(6, expectedTicks);
        Assert.AreEqual(maxComboAtRelease, score.MaxCombo, "뗀 뒤 틱이 더 들어오지 않는다");
        Assert.AreEqual(1, EndKeySounds, "뗄 때 끝 키음");
    }

    /// <summary>P4: 떼지 않음 → 끝 +120ms 지나 자동 끝 GOOD, 종합 GOOD, 그 순간 끝 키음</summary>
    [UnityTest]
    public IEnumerator P4_HoldPastEnd()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);
        yield return WaitSongTime(note.EndTime + 0.110);
        bool judgedBefore = judge.IsJudged(n);
        int soundsBefore = EndKeySounds;
        yield return WaitJudged(n);
        double judgedAt = SongTime;
        Debug.Log(Describe("P4 끝까지 누름", n) + $", +110ms에 판정 {(judgedBefore ? "있음" : "없음")}, 판정된 프레임 곡 시간 끝 +{(judgedAt - note.EndTime) * 1000:0.0}ms");

        Assert.IsFalse(judgedBefore, "끝 +120ms 전에는 판정 없음");
        Assert.AreEqual(0, soundsBefore);
        Assert.AreEqual(JudgeType.Good, Final(n).EndType);
        Assert.AreEqual(JudgeType.Good, Final(n).Type);
        Assert.IsFalse(Final(n).IsHit, "시간이 지나 낸 판정");
        Assert.Greater(Final(n).DeltaMs, 120.0);
        Assert.AreEqual(1, EndKeySounds, "자동 GOOD 순간 끝 키음");
        Assert.AreEqual(note.TickCount, CountTicks(n));
    }

    /// <summary>P5: 시작 +110ms에 누름 (첫 틱 +99.6ms 이미 지남) → 시작 GOOD, 지난 틱도 인정, 최종 콤보 1 + 전체 틱</summary>
    [UnityTest]
    public IEnumerator P5_LatePressCountsPassedTick()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time + 0.110);
        yield return ReleaseAt(0, note.EndTime);
        yield return WaitJudged(n);
        Debug.Log(Describe("P5 시작 +110ms", n) + $", 첫 틱 시작 +{(note.GetTickTime(0) - note.Time) * 1000:0.0}ms");

        Assert.IsTrue(HasStart(n, out JudgeResult start));
        Assert.AreEqual(JudgeType.Good, start.Type);
        Assert.Less(note.GetTickTime(0), note.Time + 0.110, "첫 틱은 누르기 전에 지났다");
        Assert.AreEqual(note.TickCount, CountTicks(n), "지난 첫 틱 포함 전부");
        Assert.AreEqual(1 + note.TickCount, score.Combo);
        Assert.AreEqual(JudgeType.Good, Final(n).Type);
    }

    /// <summary>P6·P11: 시작 놓침 → +120ms 지나 바로 종합 MISS, Missed(회색), 몸통 도중 누름은 빈 타격, 콤보 0</summary>
    [UnityTest]
    public IEnumerator P6_StartMissed()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return WaitSongTime(note.Time + 0.110);
        bool judgedBefore = judge.IsJudged(n);
        yield return WaitJudged(n);
        double judgedAt = SongTime;
        int countAfterMiss = results.Count;

        yield return PressAt(0, note.Time + 0.4);
        yield return null;
        LongNoteView view = Get<LongNoteView[]>(Get<NoteLayer>(scene, "noteLayer"), "longViewByNote")[n];
        Color bodyColor = view != null ? view.transform.Find("Body").GetComponent<SpriteRenderer>().color : Color.clear;
        Debug.Log(Describe("P6 시작 놓침", n) + $", 판정된 프레임 시작 +{(judgedAt - note.Time) * 1000:0.0}ms, 상태 {judge.GetLongState(n)}, 몸통 색 {bodyColor}, 도중 누름 뒤 결과 수 {results.Count - countAfterMiss}");

        Assert.IsFalse(judgedBefore, "+120ms 전에는 판정 없음");
        Assert.AreEqual(JudgeKind.LongFinal, Final(n).Kind);
        Assert.AreEqual(JudgeType.Miss, Final(n).Type);
        Assert.IsFalse(HasStart(n, out _));
        Assert.AreEqual(LongNoteState.Missed, judge.GetLongState(n));
        Assert.AreEqual(countAfterMiss, results.Count, "도중 누름은 빈 타격");
        Assert.IsFalse(judge.TryGetHeld(0, out _));
        Assert.AreEqual(0, score.Combo);
        Assert.AreEqual(0, CountTicks(n));
        Assert.IsNotNull(view, "회색으로 계속 내려가는 중");
        Assert.AreEqual((Color)GameConstants.Play.LongNoteMissedColor, bodyColor, "P11: 시작 MISS 색");
        Assert.AreEqual(0, EndKeySounds, "시작 MISS는 끝 키음 없음");
    }

    /// <summary>P7: 누르는 중 일시정지 → 눌린 채 재개 → 유지 이어짐, 끝 +0 뗌 → PERFECT, 일시정지 동안 틱 안 늘어남</summary>
    [UnityTest]
    public IEnumerator P7_PauseHeldResumePressed()
    {
        yield return StartPlay(Chart(Long(6f, 9f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);
        yield return WaitSongTime(note.Time + 0.3);
        scene.OnBack();
        Assert.IsTrue(Get<bool>(scene, "isPaused"), "일시정지");
        int ticksAtPause = CountTicks(n);
        double pausedAt = SongTime;
        yield return new WaitForSecondsRealtime(1f);
        int ticksAfterWait = CountTicks(n);
        double songTimeAfterWait = SongTime;

        Set(scene, "isLaneHeldForResume", (Func<int, bool>)(lane => true));
        yield return Resume();
        bool heldAfterResume = judge.TryGetHeld(0, out _);

        yield return ReleaseAt(0, note.EndTime);
        yield return WaitJudged(n);
        Debug.Log(Describe("P7 눌린 채 재개", n) + $", 일시정지 곡 시간 {pausedAt:0.000} → 1초 뒤 {songTimeAfterWait:0.000}, 틱 {ticksAtPause} → {ticksAfterWait}, 재개 뒤 누르는 중 {heldAfterResume}");

        Assert.AreEqual(ticksAtPause, ticksAfterWait, "일시정지 동안 틱 안 늘어남");
        Assert.AreEqual(pausedAt, songTimeAfterWait, 1e-6, "곡 시간 멈춤");
        Assert.IsTrue(heldAfterResume, "재개 뒤 유지 이어짐");
        Assert.AreEqual(JudgeType.Perfect, Final(n).Type);
        Assert.AreEqual(note.TickCount, CountTicks(n));
    }

    /// <summary>P8: 누르는 중 일시정지 → 일시정지 중 뗌(무시) → 뗀 채 재개 → 재개 순간 끝 판정 (끝 MISS → 종합 MISS)</summary>
    [UnityTest]
    public IEnumerator P8_PauseHeldResumeReleased()
    {
        yield return StartPlay(Chart(Long(6f, 9f, 0)));
        int n = LongIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);
        yield return WaitSongTime(note.Time + 0.3);
        scene.OnBack();
        double pausedAt = SongTime;
        scene.TestRelease(0, pausedAt);
        yield return null;
        bool heldAfterPausedRelease = judge.TryGetHeld(0, out _);

        Set(scene, "isLaneHeldForResume", (Func<int, bool>)(lane => false));
        yield return Resume();
        Debug.Log(Describe("P8 뗀 채 재개", n) + $", 일시정지 곡 시간 {pausedAt:0.000}, 일시정지 중 뗀 뒤 누르는 중 {heldAfterPausedRelease}");

        Assert.IsTrue(heldAfterPausedRelease, "일시정지 중 뗌은 무시");
        Assert.IsTrue(judge.IsJudged(n), "재개 순간 끝 판정");
        Assert.AreEqual(JudgeType.Miss, Final(n).EndType);
        Assert.AreEqual(JudgeType.Miss, Final(n).Type);
        Assert.AreEqual((pausedAt - note.EndTime) * 1000.0, Final(n).DeltaMs, 1.0, "재개 순간(멈춘 곡 시간)을 뗀 시각으로");
        Assert.AreEqual(0, score.Combo);
        Assert.AreEqual(1, EndKeySounds);
    }

    /// <summary>계속 버튼과 같은 경로로 재개하고 카운트다운이 끝날 때까지 기다린다.</summary>
    private IEnumerator Resume()
    {
        scene.GetType().GetMethod("OnClickResume", Private).Invoke(scene, null);
        float deadline = Time.realtimeSinceStartup + GameConstants.Pause.CountdownSeconds + 3f;
        while (Get<bool>(scene, "isPaused"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "재개 대기 시간 초과");
            yield return null;
        }
    }

    /// <summary>P9: 롱노트를 누른 채 같은 라인 누름 → 판정 없음 (다음 노트 미판정)</summary>
    [UnityTest]
    public IEnumerator P9_SameLaneWhileHeld()
    {
        yield return StartPlay(Chart(Long(6f, 8f, 0), Short(8.25f, 0)));
        int n = LongIndex(0), next = ShortIndex(0);
        PlayNote note = judge.Notes[n];

        yield return PressAt(0, note.Time);
        yield return PressAt(0, note.EndTime - 0.04);
        int countAfterPress = results.FindAll(r => r.NoteIndex == next).Count;
        double deltaToNext = (note.EndTime - 0.04 - judge.Notes[next].Time) * 1000.0;
        yield return ReleaseAt(0, note.EndTime);
        yield return PressAt(0, judge.Notes[next].Time);
        yield return WaitJudged(next);
        Debug.Log(Describe("P9 누르는 중 같은 라인", n) + $", 누른 동안 같은 라인 누름(다음 노트까지 {deltaToNext:0.0}ms) 뒤 다음 노트 결과 {countAfterPress}개, 다음 노트 {Final(next).Type}");

        Assert.AreEqual(0, countAfterPress, "누르는 동안 같은 라인 다음 노트는 판정하지 않는다");
        Assert.AreEqual(JudgeType.Perfect, Final(n).Type);
        Assert.AreEqual(JudgeType.Perfect, Final(next).Type, "뗀 뒤 다음 노트는 판정");
    }

    /// <summary>P10: 숏·롱 섞인 채보 자동 플레이 → 1,000,000점, AP, FC, 최대 콤보 = 노트 + 틱, 끝 키음 = 롱노트 수, 결과 씬</summary>
    [UnityTest]
    public IEnumerator P10_AutoPlayMixed()
    {
        ChartData chart = Chart(
            Short(6f, 0), Short(6.5f, 1), Short(7f, 2),
            Long(7f, 9f, 3), Long(8f, 8.25f, 0), Long(9f, 10f + 1f / 3f, 1),
            Short(9.5f, 2), Short(10f, 0),
            Long(11f, 12f, 0), Long(11f, 12f, 3), Short(11.5f, 1), Short(12.5f, 2));
        int longCount = chart.notes.FindAll(x => x.type == NoteData.TypeLong).Count;

        yield return StartPlay(chart, true);
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            yield return null;
        }
        yield return null;

        int possible = judge.TotalCount + judge.TotalTicks;
        Debug.Log($"[LongNoteTest] P10 자동 플레이: 노트 {judge.TotalCount}(롱 {longCount}), 틱 {judge.TotalTicks}, 점수 {score.Score}, P/G/Go/M {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, " +
                  $"최대 콤보 {score.MaxCombo}/{score.MaxPossibleCombo}, AP {score.IsAllPerfect}, FC {score.IsFullCombo}, 끝 키음 {EndKeySounds}");

        Assert.AreEqual(1000000, score.Score);
        Assert.IsTrue(score.IsAllPerfect);
        Assert.IsTrue(score.IsFullCombo);
        Assert.AreEqual(possible, score.MaxCombo);
        Assert.AreEqual(possible, score.MaxPossibleCombo);
        Assert.AreEqual(longCount, EndKeySounds);

        // 결과 씬으로 넘어가는지 (모든 노트 판정 뒤 3초)
        deadline = Time.realtimeSinceStartup + 8f;
        while (UnityEngine.Object.FindFirstObjectByType<ResultScene>() == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "결과 씬 대기 시간 초과");
            yield return null;
        }
        scene = null;
    }

    /// <summary>P12: 숏노트만 있는 D Minor Descent HARD 자동 플레이 (롱노트 작업 전과 같은지)</summary>
    [UnityTest, Category("Slow")]
    public IEnumerator P12_ShortNoteRegression()
    {
        Assert.IsTrue(ChartRepository.TryLoad("d_minor_descent", "d_minor_descent_hard", ChartType.Official, out ChartData chart, out string error), error);
        yield return StartPlay(chart, true, "d_minor_descent");

        float deadline = Time.realtimeSinceStartup + 200f;
        while (!judge.AllJudged)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "자동 플레이 대기 시간 초과");
            yield return null;
        }
        Debug.Log($"[LongNoteTest] P12 D Minor Descent HARD: 노트 {judge.TotalCount}, 점수 {score.Score}, 최대 콤보 {score.MaxCombo}, AP {score.IsAllPerfect}, 노트 풀 추가 생성 {Get<NoteLayer>(scene, "noteLayer").GrowCount}");

        Assert.AreEqual(315, judge.TotalCount);
        Assert.AreEqual(315, score.MaxCombo);
        Assert.AreEqual(1000000, score.Score);
        Assert.IsTrue(score.IsAllPerfect);
    }

    /// <summary>
    /// P13 (기록용, 세부 규칙 7번 미정): 롱노트 끝 뒤 같은 라인 숏노트 +1/4·+1/2·+1박.
    /// ① 라인 1: 롱노트를 끝 +0에 떼고 숏노트를 +0에 누름  ② 라인 2: 롱노트 시작을 놓치고 숏노트를 −60ms에 누름.
    /// 통과·실패 없이 판정 결과만 기록한다.
    /// </summary>
    [UnityTest]
    public IEnumerator P13_RecordNoteAfterLongEnd()
    {
        float[] gaps = { 0.25f, 0.5f, 1f };
        List<NoteData> notes = new List<NoteData>();
        for (int i = 0; i < gaps.Length; i++)
        {
            float start = 6f + i * 4f;
            notes.Add(Long(start, start + 2f, 1));
            notes.Add(Short(start + 2f + gaps[i], 1));
            notes.Add(Long(start, start + 2f, 2));
            notes.Add(Short(start + 2f + gaps[i], 2));
        }
        yield return StartPlay(Chart(notes.ToArray()));

        // 입력을 시간 순으로 넣는다
        List<(double time, int lane, bool press)> inputs = new List<(double, int, bool)>();
        for (int i = 0; i < gaps.Length; i++)
        {
            int l1 = LongIndex(1, i), s1 = ShortIndex(1, i), s2 = ShortIndex(2, i);
            inputs.Add((judge.Notes[l1].Time, 1, true));
            inputs.Add((judge.Notes[l1].EndTime, 1, false));
            inputs.Add((judge.Notes[s1].Time, 1, true));
            inputs.Add((judge.Notes[s2].Time - 0.060, 2, true));
        }
        inputs.Sort((x, y) => x.time.CompareTo(y.time));
        foreach (var input in inputs)
        {
            if (input.press) yield return PressAt(input.lane, input.time);
            else yield return ReleaseAt(input.lane, input.time);
        }
        yield return WaitJudged(ShortIndex(2, gaps.Length - 1));
        yield return WaitSongTime(judge.Notes[ShortIndex(2, gaps.Length - 1)].Time + 0.2);

        StringBuilder table = new StringBuilder("[LongNoteTest] P13 기록 (간격 | ① 롱노트 종합 · 숏노트 | ② 롱노트 종합 · 숏노트)\n");
        for (int i = 0; i < gaps.Length; i++)
        {
            int l1 = LongIndex(1, i), s1 = ShortIndex(1, i), l2 = LongIndex(2, i), s2 = ShortIndex(2, i);
            string Fmt(int idx) => judge.IsJudged(idx) ? $"{Final(idx).Type}({Final(idx).DeltaMs:+0.0;-0.0}ms{(Final(idx).IsHit ? "" : ", 놓침")})" : "판정 없음";
            double gapMs = (judge.Notes[s1].Time - judge.Notes[l1].EndTime) * 1000.0;
            table.Append($"  +{gaps[i]}박({gapMs:0.0}ms) | ① {Fmt(l1)} · {Fmt(s1)} | ② {Fmt(l2)} · {Fmt(s2)}\n");
        }
        Debug.Log(table.ToString());
        Assert.Pass(table.ToString());
    }
}
