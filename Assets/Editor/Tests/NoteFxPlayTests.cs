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
/// F3. 롱노트 모양 상태별 (11/20 정정): 내려오는 중 · 누르는 동안 · 떼기 직전 · 시작 MISS.
/// F2. 8박부터 테스트 플레이(메트로놈 없음): 키 글자 0.5초 1, 1.25초 약 0.5, 1.5초 0.
///     일시정지 → 이어 하기 뒤에는 0. F5 다시 하기 → isRetry여도 새 판이라 같은 안내 (글자 1 → 1.5초에 마름모 1).
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
        Assert.IsFalse(orders.ContainsKey("LongNote/Tail"), "꼬리 오브젝트 없음 (11/20 정정)");
        Assert.Less(orders["JudgeLine"], orders["LongNote/Body"], "판정선 < 몸통");
        Assert.Less(orders["LongNote/CenterLine"], orders["LongNote/Head"], "가운데 선 < 머리");
        Assert.AreEqual(GameConstants.InGameSort.HitFx, orders["HitFx(SortingGroup)"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyCell, orders["KeyCell_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyCellFrame, orders["Frame"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyDiamond, orders["KeyDiamondLine_n"]);
        Assert.AreEqual(GameConstants.InGameSort.KeyDiamond, orders["KeyDiamond_n"]);
        Assert.IsFalse(orders.ContainsKey("KeyLabelPlate_n"), "글자 뒤 판 없음 (11/20 수정)");
        Assert.AreEqual(GameConstants.InGameSort.KeyLabel, orders["KeyLabel"]);
        Assert.Greater(orders["HitFx(SortingGroup)"], orders["Note/Shine"], "이펙트는 노트 위");
        Assert.Less(orders["HitFx(SortingGroup)"], orders["KeyCell_n"], "이펙트는 키 칸 아래");
        foreach (KeyValuePair<string, int> pair in orders) log.Append(pair.Key).Append('=').Append(pair.Value).Append(", ");
        Debug.Log("[NoteFxPlayTest] 정렬 순서: " + log);
        log.Clear();

        // ── 시작 키 글자 안내 (메트로놈): 글자 → 마름모, 같이 보이는 프레임 0 ──
        float px = gear.UnitsPerReferencePx;
        TimingMap songTiming = TimingMap.FromChart(Get<ChartData>(scene, "chart"), Get<SongData>(scene, "song").OffsetMs);
        double[] sampleBeats = { -4, -2, -1, -0.75, -0.5, -0.25, 0, 1 };
        StringBuilder hintLog = new StringBuilder();
        int overlapFrames = 0, frames = 0, next = 0;
        string beforeShow = $"판 시작 곡 시간 {SongTime:0.000}초 (−4박 {songTiming.BeatToSeconds(-4):0.000}초): {gear.KeyHint}";
        bool pressChecked = false;
        while (next < sampleBeats.Length)
        {
            double now = SongTime;
            KeyHintAlpha hint = gear.KeyHint;
            frames++;
            if (hint.IsOverlapping) overlapFrames++;
            // 레인 · 마름모 실제 렌더러도 같이 본다
            if (hint.Label > 0f && (gear.GetKeyDiamond(0).enabled || gear.GetKeyDiamondLine(0).enabled)) overlapFrames++;
            if (now >= songTiming.BeatToSeconds(sampleBeats[next]))
            {
                KeyHintAlpha expected = KeyLabelHintRules.WithCountIn(now, KeyLabelHintRules.BeatTimes.From(songTiming));
                Assert.AreEqual(expected.Label, hint.Label, 0.12f, sampleBeats[next] + "박 글자");
                Assert.AreEqual(expected.Diamond, hint.Diamond, 0.12f, sampleBeats[next] + "박 마름모");
                hintLog.Append($"{sampleBeats[next]:+0.##;-0.##;0}박 {hint} / ");
                next++;
            }
            // 글자 구간에 키를 누르면: 칸 채움은 켜지고 흰 마름모는 보이지 않는다
            if (!pressChecked && hint.Label >= 1f && now >= songTiming.BeatToSeconds(-2))
            {
                pressChecked = true;
                gear.SetPressed(0, true);
                AssertColor(theme.keyCellPressed, gear.GetKeyCellColor(0), "글자 구간 누름: 칸 채움 #3E9BE0");
                SpriteRenderer white = gear.GetKeyDiamond(0);
                Assert.IsFalse(white.enabled, "글자 구간 누름: 흰 마름모 숨김");
                Assert.AreEqual(0f, white.color.a, 1e-5f, "흰 마름모 알파 0");
                hintLog.Append($"[글자 구간 누름: 칸 {Hex(gear.GetKeyCellColor(0))}, 흰 마름모 알파 {white.color.a:0.00} 켜짐 {white.enabled}] / ");
                gear.SetPressed(0, false);
            }
            yield return null;
        }
        Assert.AreEqual(0, overlapFrames, "글자 · 마름모가 같이 보인 프레임 0");
        Assert.IsTrue(pressChecked);
        Assert.AreEqual(GearView.KeyLabelFontSize(px), gear.GetKeyLabel(0).fontSize, 1e-4f, "글자 34px");
        Assert.IsFalse(gear.GetKeyLabel(0).enabled, "0박 뒤 글자 끔");
        Debug.Log($"[NoteFxPlayTest] F1 키 글자 안내(메트로놈): {beforeShow} | {hintLog}프레임 {frames}개 중 겹침 {overlapFrames}");

        // ── 키 칸 마름모 (안내가 끝난 뒤 평소 · 누름) ──
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            SpriteRenderer line = gear.GetKeyDiamondLine(lane);
            SpriteRenderer dot = gear.GetKeyDiamond(lane);
            Assert.AreEqual("Key_DiamondLine", line.sprite.name);
            Assert.AreEqual("Key_Diamond", dot.sprite.name);
            AssertColor(theme.NoteColor(lane), line.color, lane + " 마름모 테두리 라인 색");
            AssertColor(theme.NoteColor(lane), dot.color, lane + " 가운데 점 라인 색");
            Assert.IsTrue(line.enabled && dot.enabled);
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

        // ── 노트 색 · 광택 (화면에 있는 숏노트 4개) ──
        yield return WaitSongTime(timing.BeatToSeconds(1.2));
        NoteLayer layer = Get<NoteLayer>(scene, "noteLayer");
        int checkedShort = 0;
        foreach (NoteView view in layer.GetComponentsInChildren<NoteView>(false))
        {
            int lane = Mathf.RoundToInt(view.transform.localPosition.x / gear.LaneWidth + (GameConstants.Editor.LaneCount - 1) * 0.5f);
            AssertColor(theme.NoteColor(lane), view.BodyColor, "숏노트 라인 " + lane);
            AssertColor(theme.NoteGlowColor(lane), view.Glow.color, "빛 라인 " + lane);
            Assert.AreEqual(theme.noteGlowEnabled, view.Glow.enabled, "빛 켜짐 = UITheme.noteGlowEnabled, 라인 " + lane);
            AssertColor(theme.noteShine, view.Shine.color, "광택 흰색 75%");
            Vector2 noteSize = view.GetComponent<SpriteRenderer>().size;
            Assert.AreEqual(noteSize.x - 6f * px, view.Shine.size.x, 1e-4f);
            Assert.AreEqual(4f * px, view.Shine.size.y, 1e-4f);
            float shineTopPx = (noteSize.y * 0.5f - (view.Shine.transform.localPosition.y + view.Shine.size.y * 0.5f)) / px;
            Assert.AreEqual(2f, shineTopPx, 0.01f, "광택 위 끝 = 노트 위 끝 − 2px");
            log.Append($"| 숏노트 | {lane} | {Hex(view.BodyColor)} | 광택 {view.Shine.size.x / px:0.0}×{view.Shine.size.y / px:0.0}px, 위에서 {shineTopPx:0.0}px | 빛 {(view.Glow.enabled ? Hex(view.Glow.color) : "꺼짐")} |\n");
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
        float judgeLinePx = (gear.ScreenTopY - gear.JudgeLineY) / px;
        Assert.AreEqual(judgeLinePx, fxCenterPx, 0.5f, "이펙트 y = 판정선 높이");

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
        AssertColor(theme.longNoteCenterLine, longView.CenterLine.color, "가운데 선 흰색 78%");
        Assert.AreEqual(2f * px, longView.CenterLine.size.x, 1e-4f, "가운데 선 폭 2px");
        Assert.AreEqual(longView.Body.size.y, longView.CenterLine.size.y, 1e-4f, "가운데 선 = 몸통 길이");
        Assert.AreEqual(76f * px, longView.Body.size.x, 1e-4f, "몸통 폭 76");
        Assert.AreEqual("Square", longView.Body.sprite.name, "몸통 = Square 평평한 띠");
        Assert.AreEqual(0.70f, longView.AlphaAt(0f), 1e-4f, "몸통 아래 70%");
        Assert.AreEqual(0.45f, longView.AlphaAt(1f), 1e-4f, "몸통 위 45%");
        log.Append($"| 롱노트 | 1 | 몸통 {Hex(longView.BodyColor)} 폭 {longView.Body.size.x / px:0}px (위 45% → 아래 70%) · 머리 {Hex(longView.HeadColor)} | 머리 광택 {longView.HeadShine.size.x / px:0.0}×{longView.HeadShine.size.y / px:0.0}px | 가운데 선 {Hex(longView.CenterLine.color)} 폭 {longView.CenterLine.size.x / px:0.0}px |\n");

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

    /// <summary>1080 기준 px로 바꾼 y (화면 위에서)</summary>
    private static float PxY(GearView gear, float worldY) => (gear.ScreenTopY - (worldY - gear.transform.position.y)) / gear.UnitsPerReferencePx;

    /// <summary>라인 왼쪽 끝 기준 x (px)</summary>
    private static float PxX(GearView gear, int lane, float worldX) =>
        (worldX - (gear.transform.position.x + gear.GetLaneX(lane) - gear.LaneWidth * 0.5f)) / gear.UnitsPerReferencePx;

    private static LongNoteView FindLong(NoteLayer layer, GearView gear, int lane)
    {
        foreach (LongNoteView v in layer.GetComponentsInChildren<LongNoteView>(false))
            if (Mathf.Abs(v.transform.localPosition.x - gear.GetLaneX(lane)) < 1e-3f) return v;
        return null;
    }

    private struct LongShot
    {
        public string Name;
        public bool Visible, HeadVisible, ShineOn, LineOn, Missed;
        public float BodyLeft, BodyRight, Bottom, Top, LineLength, LineLeft, LineRight, HeadY, AlphaTop, AlphaBottom;
        public Color Color;
    }

    private static LongShot Shot(string name, LongNoteView v, GearView gear, int lane)
    {
        LongShot s = new LongShot { Name = name, Visible = v != null && v.gameObject.activeInHierarchy };
        if (!s.Visible) return s;
        float px = gear.UnitsPerReferencePx;
        Bounds body = v.Body.bounds;
        Bounds line = v.CenterLine.bounds;
        s.BodyLeft = PxX(gear, lane, body.min.x);
        s.BodyRight = PxX(gear, lane, body.max.x);
        s.Bottom = PxY(gear, body.min.y);
        s.Top = PxY(gear, body.max.y);
        s.LineLeft = PxX(gear, lane, line.min.x);
        s.LineRight = PxX(gear, lane, line.max.x);
        s.LineLength = v.CenterLine.size.y / px;
        s.HeadY = PxY(gear, v.Head.transform.position.y);
        s.HeadVisible = v.Head.enabled && v.Head.gameObject.activeInHierarchy;
        s.ShineOn = v.HeadShine.enabled;
        s.LineOn = v.CenterLine.enabled;
        s.Missed = v.IsMissed;
        s.AlphaTop = v.AlphaAt(1f);
        s.AlphaBottom = v.AlphaAt(0f);
        s.Color = v.BodyColor;
        return s;
    }

    private static string Row(LongShot s) => !s.Visible ? $"| {s.Name} | (사라짐) |" :
        $"| {s.Name} | {s.BodyRight - s.BodyLeft:0.0} (x+{s.BodyLeft:0.0}~{s.BodyRight:0.0}) | {s.Bottom:0.0} | {s.Top:0.0} | {s.LineLength:0.0} (x+{s.LineLeft:0.0}~{s.LineRight:0.0}) | {s.HeadY:0.0} | {(s.HeadVisible ? "보임" : "숨김")} | " +
        $"{s.AlphaTop:0.00} / {s.AlphaBottom:0.00} | {(s.Missed ? "회색 " : "라인 색 ")}{Hex(s.Color)} | 광택 {(s.ShineOn ? "켜짐" : "꺼짐")} · 선 {(s.LineOn ? "켜짐" : "꺼짐")} |";

    /// <summary>
    /// F3: 롱노트 모양 상태별 (11/20 정정). 라인 1에 3박 롱노트(누르고 끝에서 뗌), 라인 2에 2박 롱노트(안 눌러 시작 MISS).
    /// ① 내려오는 중 ② 누르는 동안(틱 3번 뒤 · 5번 뒤) ③ 떼기 직전 ④ 시작 MISS로 지나가는 중.
    /// </summary>
    [UnityTest]
    public IEnumerator F3_LongNoteShape_ByState()
    {
        yield return StartPlay(Chart(Long(4f, 7f, 1), Long(9f, 11f, 2)), 0f);
        GearView gear = scene.Gear;
        HitFxPool pool = scene.HitFx;
        NoteLayer layer = Get<NoteLayer>(scene, "noteLayer");
        NoteJudge judge = Get<NoteJudge>(scene, "judge");
        TimingMap timing = Get<TimingMap>(scene, "timing");
        PlayNote held = judge.Notes[0];
        PlayNote missed = judge.Notes[1];
        double quarter = timing.BeatToSeconds(4.25) - timing.BeatToSeconds(4);
        float judgeLinePx = PxY(gear, gear.transform.position.y + gear.JudgeLineY);
        List<LongShot> shots = new List<LongShot>();

        // ① 내려오는 중
        yield return WaitSongTime(held.Time - 0.3);
        LongShot s1 = Shot("① 내려오는 중", FindLong(layer, gear, 1), gear, 1);
        shots.Add(s1);
        Assert.IsTrue(s1.Visible);
        Assert.AreEqual(76f, s1.BodyRight - s1.BodyLeft, 0.05f, "몸통 폭 76");
        Assert.AreEqual(16f, s1.BodyLeft, 0.05f, "x+16");
        Assert.AreEqual(53f, s1.LineLeft, 0.05f, "선 x+53");
        Assert.AreEqual(55f, s1.LineRight, 0.05f, "선 x+55");
        Assert.AreEqual(s1.HeadY, s1.Bottom, 0.05f, "아래 끝 = 머리 가운데");
        Assert.AreEqual(s1.Bottom - s1.Top, s1.LineLength, 0.05f, "가운데 선 = 몸통 길이");
        Assert.Less(s1.HeadY, judgeLinePx, "아직 판정선 위");

        // ② 누르는 동안: 머리 판정선 고정, 위 끝만 내려옴
        yield return PressAt(1, held.Time);
        yield return WaitSongTime(held.Time + quarter * 3 + 0.02);
        LongShot s2 = Shot("② 누르는 동안 (틱 3번 뒤)", FindLong(layer, gear, 1), gear, 1);
        shots.Add(s2);
        HitFx tickFx = null;
        for (int i = 0; i < GameConstants.Play.HitFxPoolPerLane; i++) if (pool.GetFx(1, i).IsPlaying) tickFx = pool.GetFx(1, i);
        Assert.IsNotNull(tickFx, "누르는 동안 틱 이펙트가 보인다");
        float fxY = PxY(gear, tickFx.transform.position.y);
        float fxX = PxX(gear, 1, tickFx.transform.position.x);
        Assert.AreEqual(PxY(gear, gear.transform.position.y + gear.JudgeLineY), fxY, 0.05f, "틱 이펙트 y = 판정선 높이");
        Assert.AreEqual(54f, fxX, 0.05f, "라인 가운데");

        yield return WaitSongTime(held.Time + quarter * 5 + 0.02);
        LongShot s2b = Shot("② 누르는 동안 (틱 5번 뒤)", FindLong(layer, gear, 1), gear, 1);
        shots.Add(s2b);
        foreach (LongShot s in new[] { s2, s2b })
        {
            Assert.IsTrue(s.Visible && s.HeadVisible, "누르는 동안 머리 보임");
            Assert.AreEqual(judgeLinePx, s.HeadY, 0.05f, "머리 = 판정선");
            Assert.AreEqual(judgeLinePx, s.Bottom, 0.05f, "몸통 아래 끝 = 판정선");
        }
        Assert.Greater(s2b.Top, s2.Top, "위 끝이 시간에 따라 내려온다 (y 증가)");

        // ③ 떼기 직전 → 뗌
        yield return WaitSongTime(held.EndTime - 0.08);
        LongShot s3 = Shot("③ 떼기 직전", FindLong(layer, gear, 1), gear, 1);
        shots.Add(s3);
        Assert.AreEqual(judgeLinePx, s3.HeadY, 0.05f);
        Assert.AreEqual(judgeLinePx, s3.Bottom, 0.05f);
        Assert.Greater(s3.Top, s2b.Top, "더 내려옴");
        yield return ReleaseAt(1, held.EndTime);
        yield return null;
        Assert.IsNull(FindLong(layer, gear, 1), "떼면 머리 · 몸통 사라짐");
        int fxTotal = pool.GetPlayCount(1);
        int ticks = LongNoteTicks.Count(4, 7);

        // ④ 시작 MISS: 안 눌러 판정선을 그대로 지나감
        yield return WaitSongTime(missed.Time + 0.25);
        LongNoteView missedView = FindLong(layer, gear, 2);
        LongShot s4 = Shot("④ 시작 MISS 지나가는 중", missedView, gear, 2);
        shots.Add(s4);
        Assert.IsTrue(s4.Visible && s4.Missed, "시작 MISS 회색");
        AssertColor(UITheme.Current.longNoteMissed, missedView.HeadColor, "머리 회색");
        Assert.IsFalse(s4.ShineOn || s4.LineOn, "광택 · 선 끔");
        Assert.Greater(s4.HeadY, judgeLinePx + 5f, "머리가 판정선에 고정되지 않고 지나감");
        Assert.AreEqual(s4.HeadY, s4.Bottom, 0.05f);
        Assert.AreEqual(0, pool.GetPlayCount(2), "시작 MISS 이펙트 없음");

        StringBuilder table = new StringBuilder("| 상태 | 몸통 폭 | 아래 끝 y | 위 끝 y | 가운데 선 길이 (x) | 머리 y | 머리 | 위/아래 알파 | 색 | 광택 · 선 |\n");
        foreach (LongShot s in shots) table.Append(Row(s)).Append('\n');
        Debug.Log($"[NoteFxPlayTest] F3 롱노트 상태별 (판정선 {judgeLinePx:0.0}px)\n{table}" +
                  $"3박 롱노트 유지 틱 {ticks}, 라인 1 이펙트 {fxTotal}번 (틱 + 머리 + 꼬리 = {ticks + 2}), 틱 이펙트 위치 x+{fxX:0.0} y {fxY:0.0}");
        Assert.AreEqual(ticks + 2, fxTotal, "틱 + 머리 + 꼬리");
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
        double[] samples = { 0, 0.5, 1.0, 1.125, 1.25, 1.375, 1.5, 2.0 };
        StringBuilder midLog = new StringBuilder();
        int midOverlap = 0, midFrames = 0, si = 0;
        while (si < samples.Length)
        {
            double elapsed = SongTime - start;
            KeyHintAlpha hint = gear.KeyHint;
            midFrames++;
            if (hint.IsOverlapping || (hint.Label > 0f && gear.GetKeyDiamondLine(0).enabled)) midOverlap++;
            if (elapsed >= samples[si])
            {
                KeyHintAlpha expected = KeyLabelHintRules.WithoutCountIn(elapsed);
                Assert.AreEqual(expected.Label, hint.Label, 0.12f, samples[si] + "초 글자");
                Assert.AreEqual(expected.Diamond, hint.Diamond, 0.12f, samples[si] + "초 마름모");
                midLog.Append($"{samples[si]:0.###}초({elapsed:0.000}) {hint} / ");
                si++;
            }
            yield return null;
        }
        Assert.AreEqual(0, midOverlap, "중간 시작: 겹친 프레임 0");
        Assert.AreEqual(KeyHintAlpha.DiamondOnly.Diamond, gear.KeyHint.Diamond, "1.5초 뒤 마름모만");

        // 일시정지 → 이어 하기: 판 시작이 아니므로 다시 보이지 않는다
        MethodInfo pause = typeof(InGameScene).GetMethod("RequestPause", Private);
        MethodInfo resume = typeof(InGameScene).GetMethod("OnClickResume", Private);
        pause.Invoke(scene, new object[] { "테스트" });
        yield return null;
        resume.Invoke(scene, null);
        float deadline = Time.realtimeSinceStartup + 6f;
        float maxAfterResume = 0f;
        float minDiamondAfterResume = 1f;
        while (Get<bool>(scene, "isPaused"))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "이어 하기 대기 시간 초과");
            maxAfterResume = Mathf.Max(maxAfterResume, gear.KeyHint.Label);
            minDiamondAfterResume = Mathf.Min(minDiamondAfterResume, gear.KeyHint.Diamond);
            yield return null;
        }
        double resumed = SongTime;
        KeyHintAlpha resumeStart = gear.KeyHint;
        while (SongTime < resumed + 1.0)
        {
            maxAfterResume = Mathf.Max(maxAfterResume, gear.KeyHint.Label);
            minDiamondAfterResume = Mathf.Min(minDiamondAfterResume, gear.KeyHint.Diamond);
            yield return null;
        }
        KeyHintAlpha resume1s = gear.KeyHint;
        Assert.AreEqual(0f, maxAfterResume, "이어 하기 뒤 글자 0");
        Assert.AreEqual(1f, minDiamondAfterResume, "이어 하기 뒤 마름모 1");

        // F5 다시 하기
        Assert.IsTrue(scene.TestPressRestartKey(), "F5 받음");
        yield return WaitPlaying(TestSpeed.Deadline(20f));
        Assert.IsTrue(scene.IsRetryEntry, "다시 하기 진입 데이터");
        double retryStart = SongTime;
        KeyHintAlpha retryStartHint = scene.Gear.KeyHint;
        float maxRetry = 0f;
        int retryOverlap = 0;
        while (SongTime < retryStart + 1.6)
        {
            maxRetry = Mathf.Max(maxRetry, scene.Gear.KeyHint.Label);
            if (scene.Gear.KeyHint.IsOverlapping) retryOverlap++;
            yield return null;
        }
        KeyHintAlpha retryEnd = scene.Gear.KeyHint;
        Assert.AreEqual(1f, maxRetry, 1e-3f, "F5 다시 하기도 키 글자 안내");
        Assert.AreEqual(0, retryOverlap, "F5 다시 하기 겹침 없음");
        Assert.AreEqual(0f, retryEnd.Label, "1.6초 뒤 글자 0");
        Assert.AreEqual(1f, retryEnd.Diamond, "1.6초 뒤 마름모 1");
        Debug.Log($"[NoteFxPlayTest] F2 키 글자 안내(중간 시작): {midLog}프레임 {midFrames}개 중 겹침 {midOverlap} | 이어 하기 직후 {resumeStart} · 1초 뒤 {resume1s} | " +
                  $"F5 다시 하기 직후 {retryStartHint} · 1.6초 동안 글자 최대 {maxRetry:0.00} · 겹침 {retryOverlap} · 끝 {retryEnd}");
    }
}
