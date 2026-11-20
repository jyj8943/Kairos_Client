using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 노트 색 · 판정 이펙트 · 키 칸 (11/20, 기획서 3-4 「노트 · 판정 이펙트」) 실제 씬 확인. 테스트 안에서 만든 작은 채보, 곡 시간 10초 안.
/// F1. 0박부터 테스트 플레이(메트로놈 있음):
///     - 그리는 순서표, 라인별 숏노트 · 롱노트 머리 · 몸통 · 꼬리 색, 광택 크기 · 위치, 키 칸 마름모 (평소 · 누름)
///     - 키 글자 안내: −2박 1, −0.5박 약 0.5, 0박 뒤 0
///     - PERFECT · GREAT · GOOD · MISS 숏노트 4개 + 2박 롱노트(머리 PERFECT, 끝에서 뗌): 이펙트 1 + 1 + 1 + 0 + 9 = 12번, 판정별 색 C
///     - 이펙트 풀: 판 도중 생성 0, 라인별 최대 동시 사용 수
/// F2. 8박부터 테스트 플레이(메트로놈 없음): 키 글자 0.5초 1, 1.25초 약 0.5, 1.5초 0.
///     일시정지 → 이어 하기 뒤에도 0. F5 다시 하기 → isRetry, 키 글자 0.
/// </summary>
public class NoteFxPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const double InjectLeadSeconds = 0.05;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private InGameScene scene;
    private readonly List<JudgeResult> results = new List<JudgeResult>();

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
            songId = Song,
            type = ChartData.TypeCustom,
            chartName = "노트 이펙트 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData>(notes)
        };
    }

    private IEnumerator StartPlay(ChartData chart, float startBeat)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 초기화 대기 시간 초과");
            yield return null;
        }
        InGameEntryData entry = new InGameEntryData
        {
            songId = Song,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            scrollMultiplier = 1f,
            testPlay = new TestPlayData { songId = Song, chart = chart, startBeat = startBeat, autoPlay = false }
        };
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.InGame, entry));
        yield return WaitPlaying(deadline);
    }

    private IEnumerator WaitPlaying(float deadline)
    {
        scene = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            if (!GameManager.Scene.IsLoading)
            {
                scene = Object.FindFirstObjectByType<InGameScene>();
                if (scene != null && Get<bool>(scene, "isPlaying")) break;
            }
            yield return null;
        }
        scene.TestJudgeObserver = r => results.Add(r);
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static double SongTime => GameManager.Sound.SongTime;

    private static IEnumerator WaitSongTime(double time, float timeoutSeconds = 30f)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (SongTime < time)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"곡 시간 {time:0.000}초 대기 시간 초과 (지금 {SongTime:0.000}초)");
            yield return null;
        }
    }

    private IEnumerator PressAt(int lane, double time)
    {
        yield return WaitSongTime(time - InjectLeadSeconds);
        scene.TestPress(lane, time);
    }

    private IEnumerator ReleaseAt(int lane, double time)
    {
        yield return WaitSongTime(time - InjectLeadSeconds);
        scene.TestRelease(lane, time);
    }

    private static void AssertColor(Color expected, Color actual, string message, float tolerance = 1.5f / 255f)
    {
        Assert.AreEqual(expected.r, actual.r, tolerance, message + " (r)");
        Assert.AreEqual(expected.g, actual.g, tolerance, message + " (g)");
        Assert.AreEqual(expected.b, actual.b, tolerance, message + " (b)");
        Assert.AreEqual(expected.a, actual.a, tolerance, message + " (a)");
    }

    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

    /// <summary>F1: 0박 시작 — 정렬 · 노트 색 · 키 칸 · 키 글자(메트로놈) · 판정 이펙트 · 롱노트 틱 이펙트 · 풀</summary>
    [UnityTest]
    public IEnumerator F1_CountInPlay_ColorsFxKeyHint()
    {
        yield return StartPlay(Chart(
            Short(2f, 0), Short(2.5f, 1), Short(3f, 2), Short(3.5f, 3),
            Long(5f, 7f, 1)), 0f);
        UITheme theme = UITheme.Current;
        GearView gear = scene.Gear;
        HitFxPool pool = scene.HitFx;
        Assert.IsNotNull(pool, "HitFx 풀 연결");
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        TimingMap timing = Get<TimingMap>(scene, "timing");
        Assert.IsTrue(scene.UsesCountIn, "0박 시작은 메트로놈");
        int created = pool.CreatedCount;
        int childCount = pool.GetComponentsInChildren<Transform>(true).Length;
        Assert.AreEqual(GameConstants.Editor.LaneCount * GameConstants.Play.HitFxPoolPerLane, created, "라인마다 6개 미리 만듦");
        StringBuilder log = new StringBuilder();

        // ── 그리는 순서표 ──
        Dictionary<string, int> orders = new Dictionary<string, int>();
        foreach (Renderer r in gear.GetComponentsInChildren<Renderer>(true))
        {
            string key = r.name;
            if (r.GetComponentInParent<HitFx>(true) != null) key = "HitFx";
            else if (r.GetComponentInParent<NoteView>(true) != null) key = "Note/" + r.name;
            else if (r.GetComponentInParent<LongNoteView>(true) != null) key = "LongNote/" + r.name;
            if (r.GetComponentInParent<HitFx>(true) != null)
            {
                UnityEngine.Rendering.SortingGroup group = r.GetComponentInParent<UnityEngine.Rendering.SortingGroup>(true);
                orders["HitFx(SortingGroup)"] = group.sortingOrder;
                continue;
            }
            key = System.Text.RegularExpressions.Regex.Replace(key, "_[0-9]+$", "_n");
            if (key.StartsWith("Label")) key = "KeyLabel";
            if (!orders.ContainsKey(key)) orders[key] = r.sortingOrder;
            else Assert.AreEqual(orders[key], r.sortingOrder, key + " 같은 종류는 같은 순서");
        }
        Assert.AreEqual(GameConstants.InGameSort.JudgeLine, orders["JudgeLine"], "판정선 3");
        Assert.AreEqual(GameConstants.InGameSort.NoteGlow, orders["Note/Glow"]);
        Assert.AreEqual(GameConstants.InGameSort.Note, orders["Note/Note"]);
        Assert.AreEqual(GameConstants.InGameSort.NoteShine, orders["Note/Shine"]);
        Assert.AreEqual(GameConstants.InGameSort.LongNoteBody, orders["LongNote/Body"]);
        Assert.AreEqual(GameConstants.InGameSort.LongNoteCenterLine, orders["LongNote/CenterLine"]);
        Assert.AreEqual(GameConstants.InGameSort.Note, orders["LongNote/Head"]);
        Assert.AreEqual(GameConstants.InGameSort.Note, orders["LongNote/Tail"]);
        Assert.AreEqual(GameConstants.InGameSort.HitFx, orders["HitFx(SortingGroup)"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyCell, orders["KeyCell_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyCellFrame, orders["Frame"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyDiamond, orders["KeyDiamondLine_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyDiamond, orders["KeyDiamond_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyLabelPlate, orders["KeyLabelPlate_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyLabel, orders["KeyLabel"]);
        Assert.Greater(orders["HitFx(SortingGroup)"], orders["Note/Shine"], "이펙트는 노트 위");
        Assert.Less(orders["HitFx(SortingGroup)"], orders["KeyCell_n"], "이펙트는 키 칸 아래");
        foreach (KeyValuePair<string, int> pair in orders) log.Append(pair.Key).Append('=').Append(pair.Value).Append(", ");
        Debug.Log("[NoteFxPlayTest] 정렬 순서: " + log);
        log.Clear();

        // ── 키 칸 마름모 (평소 · 누름) ──
        float px = gear.UnitsPerReferencePx;
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            SpriteRenderer line = gear.GetKeyDiamondLine(lane);
            SpriteRenderer dot = gear.GetKeyDiamond(lane);
            Assert.AreEqual("Key_DiamondLine", line.sprite.name);
            Assert.AreEqual("Key_Diamond", dot.sprite.name);
            AssertColor(theme.NoteColor(lane), line.color, lane + " 마름모 테두리 라인 색");
            AssertColor(theme.NoteColor(lane), dot.color, lane + " 가운데 점 라인 색");
            Assert.IsTrue(line.enabled);
            float lineHalf = line.bounds.extents.y / px * 32f / 38f; // 이미지 76px 중 반대각 32px
            Assert.AreEqual(16f, lineHalf, 0.2f, lane + " 반대각 16px");
            Assert.AreEqual(6f, dot.bounds.extents.y / px * 32f / 38f, 0.2f, lane + " 가운데 점 반대각 6px");
            float centerYPx = (gear.ScreenTopY - (line.bounds.center.y - gear.transform.position.y)) / px;
            Assert.AreEqual(1020f, centerYPx, 0.5f, lane + " y 1020");
            Assert.AreEqual(gear.GetLaneX(lane), line.bounds.center.x - gear.transform.position.x, 1e-3f, lane + " 라인 가운데");

            gear.SetPressed(lane, true);
            Assert.IsFalse(line.enabled, "누르면 테두리를 끄고");
            AssertColor(Color.white, dot.color, lane + " 누름 흰색");
            Assert.AreEqual(16f, dot.bounds.extents.y / px * 32f / 38f, 0.2f, lane + " 누름 반대각 16px");
            AssertColor(theme.keyCellPressed, gear.GetKeyCellColor(lane), lane + " 칸 채움 #3E9BE0");
            gear.SetPressed(lane, false);
            Assert.IsTrue(line.enabled);
        }

        // ── 키 글자 안내 (메트로놈 −4박 ~ 0박) ──
        TimingMap songTiming = TimingMap.FromChart(Get<ChartData>(scene, "chart"), Get<SongData>(scene, "song").OffsetMs);
        double beatSeconds = songTiming.BeatToSeconds(1) - songTiming.BeatToSeconds(0);
        yield return WaitSongTime(songTiming.BeatToSeconds(-2));
        float alphaM2 = gear.KeyLabelHintAlpha;
        Assert.AreEqual(1f, alphaM2, 1e-4f, "−2박 1");
        Assert.IsTrue(gear.GetKeyLabel(0).enabled && gear.GetKeyLabelPlate(0).enabled, "판과 글자가 보인다");
        AssertColor(theme.keyLabelPlate, gear.GetKeyLabelPlate(0).color, "판 #17141F 88%");
        Assert.AreEqual(GearView.KeyLabelFontSize(px), gear.GetKeyLabel(0).fontSize, 1e-4f, "글자 34px");
        yield return WaitSongTime(songTiming.BeatToSeconds(-0.5));
        float alphaM05 = gear.KeyLabelHintAlpha;
        double hintSongTime = Get<double>(scene, "keyHintFadeStartTime");
        Assert.AreEqual(0.5f, alphaM05, 0.15f, "−0.5박 약 0.5 (프레임 차이)");
        yield return WaitSongTime(songTiming.BeatToSeconds(0) + 0.01);
        Assert.AreEqual(0f, gear.KeyLabelHintAlpha, "0박 뒤 0");
        Assert.IsFalse(gear.GetKeyLabel(0).enabled, "글자 끔");
        Debug.Log($"[NoteFxPlayTest] F1 키 글자: −2박 {alphaM2:0.00}, −0.5박 {alphaM05:0.00}, 0박 뒤 {gear.KeyLabelHintAlpha:0.00} (1박 {beatSeconds:0.000}초, −1박 {hintSongTime:0.000}초)");

        // ── 노트 색 · 광택 (화면에 있는 숏노트 4개) ──
        yield return WaitSongTime(timing.BeatToSeconds(1.2));
        NoteLayer layer = Get<NoteLayer>(scene, "noteLayer");
        int checkedShort = 0;
        foreach (NoteView view in layer.GetComponentsInChildren<NoteView>(false))
        {
            int lane = Mathf.RoundToInt(view.transform.localPosition.x / gear.LaneWidth + (GameConstants.Editor.LaneCount - 1) * 0.5f);
            AssertColor(theme.NoteColor(lane), view.BodyColor, "숏노트 라인 " + lane);
            AssertColor(theme.NoteGlowColor(lane), view.Glow.color, "빛 라인 " + lane);
            AssertColor(theme.noteShine, view.Shine.color, "광택 흰색 75%");
            Vector2 noteSize = view.GetComponent<SpriteRenderer>().size;
            Assert.AreEqual(noteSize.x - 6f * px, view.Shine.size.x, 1e-4f);
            Assert.AreEqual(4f * px, view.Shine.size.y, 1e-4f);
            float shineTopPx = (noteSize.y * 0.5f - (view.Shine.transform.localPosition.y + view.Shine.size.y * 0.5f)) / px;
            Assert.AreEqual(2f, shineTopPx, 0.01f, "광택 위 끝 = 노트 위 끝 − 2px");
            log.Append($"| 숏노트 | {lane} | {Hex(view.BodyColor)} | 광택 {view.Shine.size.x / px:0.0}×{view.Shine.size.y / px:0.0}px, 위에서 {shineTopPx:0.0}px | 빛 {Hex(view.Glow.color)} |\n");
            checkedShort++;
        }
        Assert.AreEqual(4, checkedShort, "숏노트 4개");

        // ── PERFECT · GREAT · GOOD · MISS 숏노트 ──
        IReadOnlyList<PlayNote> notes = judge.Notes;
        yield return PressAt(0, notes[0].Time);
        HitFx perfectFx = pool.GetFx(0, 0);
        yield return PressAt(1, notes[1].Time + 0.06);
        yield return PressAt(2, notes[2].Time + 0.10);
        yield return WaitSongTime(notes[3].Time + 0.3);  // 라인 3은 안 눌러 MISS
        Assert.AreEqual(1, pool.PlayCountByJudge[(int)JudgeType.Perfect], "PERFECT 1");
        Assert.AreEqual(1, pool.PlayCountByJudge[(int)JudgeType.Great], "GREAT 1");
        Assert.AreEqual(1, pool.PlayCountByJudge[(int)JudgeType.Good], "GOOD 1");
        Assert.AreEqual(0, pool.PlayCountByJudge[(int)JudgeType.Miss], "MISS 0");
        Assert.AreEqual(0, pool.GetPlayCount(3), "MISS 라인 이펙트 없음");
        AssertColor(theme.hitFxPerfect, perfectFx.Color, "PERFECT C");
        AssertColor(theme.hitFxGreat, pool.GetFx(1, 0).Color, "GREAT C");
        AssertColor(theme.hitFxGood, pool.GetFx(2, 0).Color, "GOOD C");
        Assert.IsFalse(perfectFx.IsPlaying, "0.38초 뒤 꺼짐");
        float fxCenterPx = (gear.ScreenTopY - (perfectFx.transform.position.y - gear.transform.position.y)) / px;
        Assert.AreEqual(906f, fxCenterPx, 0.5f, "이펙트 y 906");

        // ── 2박 롱노트: 머리 PERFECT, 끝에서 뗌 ──
        PlayNote longNote = notes[4];
        Assert.IsTrue(longNote.IsLong);
        yield return WaitSongTime(longNote.Time - 0.3);
        LongNoteView longView = null;
        foreach (LongNoteView v in layer.GetComponentsInChildren<LongNoteView>(false)) longView = v;
        Assert.IsNotNull(longView, "롱노트가 화면에 있다");
        Color inner = theme.NoteColor(1);
        AssertColor(inner, longView.BodyColor, "몸통 라인 색");
        AssertColor(inner, longView.HeadColor, "머리 라인 색");
        AssertColor(inner, longView.TailColor, "꼬리 라인 색");
        AssertColor(theme.longNoteCenterLine, longView.CenterLine.color, "가운데 선 흰색 78%");
        Assert.AreEqual(2f * px, longView.CenterLine.size.x, 1e-4f, "가운데 선 폭 2px");
        Assert.AreEqual(longView.Body.size.y, longView.CenterLine.size.y, 1e-4f, "가운데 선 = 몸통 길이");
        Assert.AreEqual(new Vector2(76f, 10f) * px, longView.Tail.size, "꼬리 76 × 10");
        Assert.AreEqual(0.85f, longView.AlphaAt(0f), 1e-4f, "몸통 아래 85%");
        Assert.AreEqual(0.55f, longView.AlphaAt(1f), 1e-4f, "몸통 위 55%");
        log.Append($"| 롱노트 | 1 | 몸통 {Hex(longView.BodyColor)} (위 55% → 아래 85%) · 머리 {Hex(longView.HeadColor)} · 꼬리 {Hex(longView.TailColor)} ({longView.Tail.size.x / px:0}×{longView.Tail.size.y / px:0}px) | 머리 광택 {longView.HeadShine.size.x / px:0.0}×{longView.HeadShine.size.y / px:0.0}px | 가운데 선 {Hex(longView.CenterLine.color)} 폭 {longView.CenterLine.size.x / px:0.0}px |\n");

        int before = pool.PlayCount;
        int tickResults = 0;
        System.Action<JudgeResult> countTicks = r => { if (r.Kind == JudgeKind.Tick) tickResults++; };
        scene.TestJudgeObserver += countTicks;
        yield return PressAt(1, longNote.Time);
        yield return ReleaseAt(1, longNote.EndTime);
        yield return null;
        scene.TestJudgeObserver -= countTicks;
        int longFx = pool.PlayCount - before;
        Assert.AreEqual(7, tickResults, "유지 틱 7");
        Assert.AreEqual(9, longFx, "롱노트 이펙트 = 틱 7 + 머리 + 꼬리");
        Assert.AreEqual(10, pool.PlayCountByJudge[(int)JudgeType.Perfect], "PERFECT 색 10 (숏노트 1 + 롱노트 9)");

        yield return WaitSongTime(longNote.EndTime + 0.5);
        Assert.AreEqual(created, pool.CreatedCount, "판 도중 생성 0");
        Assert.AreEqual(childCount, pool.GetComponentsInChildren<Transform>(true).Length, "자식 수 그대로");
        StringBuilder maxLog = new StringBuilder();
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
            maxLog.Append($"라인 {lane}: 터짐 {pool.GetPlayCount(lane)} · 최대 동시 {pool.GetMaxActive(lane)} / ");
        Assert.LessOrEqual(pool.GetMaxActive(1), GameConstants.Play.HitFxPoolPerLane);
        Debug.Log($"[NoteFxPlayTest] F1 노트 색 표\n{log}");
        Debug.Log($"[NoteFxPlayTest] F1 이펙트: 합계 {pool.PlayCount} (P {pool.PlayCountByJudge[0]} · G {pool.PlayCountByJudge[1]} · Go {pool.PlayCountByJudge[2]} · M {pool.PlayCountByJudge[3]}), " +
                  $"롱노트 틱 {tickResults} → 이펙트 {longFx}, 풀 {created}개 (판 도중 생성 {pool.CreatedCount - created}), {maxLog}, " +
                  $"색 C: P {Hex(theme.hitFxPerfect)} G {Hex(theme.hitFxGreat)} Go {Hex(theme.hitFxGood)}");
    }

    /// <summary>F2: 8박부터(메트로놈 없음) 키 글자 1.5초 → 일시정지·이어 하기 뒤 0 → F5 다시 하기 0</summary>
    [UnityTest]
    public IEnumerator F2_MidStart_KeyHint_ResumeAndRetryHidden()
    {
        yield return StartPlay(Chart(Short(16f, 0), Short(24f, 1)), 8f);
        GearView gear = scene.Gear;
        Assert.IsFalse(scene.UsesCountIn, "중간 시작은 메트로놈 없음");
        Assert.IsFalse(scene.IsRetryEntry);

        yield return null; // 첫 Update에서 시작 시각을 잡는다
        double start = Get<double>(scene, "keyHintStartSongTime");
        yield return WaitSongTime(start + 0.5);
        float a05 = gear.KeyLabelHintAlpha;
        Assert.AreEqual(1f, a05, 1e-4f, "0.5초 1");
        yield return WaitSongTime(start + 1.25);
        float a125 = gear.KeyLabelHintAlpha;
        Assert.AreEqual(0.5f, a125, 0.15f, "1.25초 약 0.5");
        yield return WaitSongTime(start + 1.5);
        yield return null;
        float a15 = gear.KeyLabelHintAlpha;
        Assert.AreEqual(0f, a15, "1.5초 0");

        // 일시정지 → 이어 하기: 판 시작이 아니므로 다시 보이지 않는다
        MethodInfo pause = typeof(InGameScene).GetMethod("RequestPause", Private);
        MethodInfo resume = typeof(InGameScene).GetMethod("OnClickResume", Private);
        pause.Invoke(scene, new object[] { "테스트" });
        yield return null;
        resume.Invoke(scene, null);
        float deadline = Time.realtimeSinceStartup + 6f;
        float maxAfterResume = 0f;
        while (Get<bool>(scene, "isPaused"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "이어 하기 대기 시간 초과");
            maxAfterResume = Mathf.Max(maxAfterResume, gear.KeyLabelHintAlpha);
            yield return null;
        }
        double resumed = SongTime;
        while (SongTime < resumed + 0.5)
        {
            maxAfterResume = Mathf.Max(maxAfterResume, gear.KeyLabelHintAlpha);
            yield return null;
        }
        Assert.AreEqual(0f, maxAfterResume, "이어 하기 뒤 표시 0");

        // F5 다시 하기
        Assert.IsTrue(scene.TestPressRestartKey(), "F5 받음");
        yield return WaitPlaying(TestSpeed.Deadline(20f));
        Assert.IsTrue(scene.IsRetryEntry, "다시 하기 진입 데이터");
        double retryStart = SongTime;
        float maxRetry = 0f;
        while (SongTime < retryStart + 1.6)
        {
            maxRetry = Mathf.Max(maxRetry, scene.Gear.KeyLabelHintAlpha);
            yield return null;
        }
        Assert.AreEqual(0f, maxRetry, "F5 다시 하기 표시 0");
        Debug.Log($"[NoteFxPlayTest] F2 키 글자(중간 시작): 0.5초 {a05:0.00}, 1.25초 {a125:0.00}, 1.5초 {a15:0.00} / 이어 하기 뒤 최대 {maxAfterResume:0.00} / F5 다시 하기 최대 {maxRetry:0.00}");
    }
}
