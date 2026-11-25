using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 곡 정보(SongData)의 BPM 구간과 새 커스텀 채보 시작 구간 확인 (EditMode).
/// - 구간 목록이 없는 곡은 bpm 하나(0박), 있는 곡은 목록이 기준이다.
/// - 새 채보는 곡 BPM 구간 전체로 시작하고, "곡 BPM 구간으로 되돌리기"도 곡 구간 전체로 돌아간다.
/// - SongManager 검사: 첫 구간 0박, beat 오름차순, 같은 위치 중복 없음, BPM 30 ~ 400. 이웃 구간 같은 BPM은 경고.
/// - Song_CriticalVelocity 에셋 구간이 공식 NORMAL 채보 timingPoints와 같다.
/// 테스트용 SongData는 ScriptableObject.CreateInstance로 만든다.
/// </summary>
public class SongTimingPointsTests
{
    private const string SongId = "__song_timing_test__";
    private const string CriticalVelocityAssetPath = "Assets/Data/Songs/Song_CriticalVelocity.asset";
    private const int T = GameConstants.Editor.TicksPerBeat;

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object obj in created)
            if (obj != null) Object.DestroyImmediate(obj);
        created.Clear();
    }

    /// <summary>테스트용 곡을 만든다. beatBpm은 (beat, bpm) 쌍. 비우면 구간 목록 없는 곡</summary>
    private SongData CreateSong(float bpm, params float[] beatBpm)
    {
        SongData song = ScriptableObject.CreateInstance<SongData>();
        created.Add(song);
        SerializedObject serialized = new SerializedObject(song);
        serialized.FindProperty("songId").stringValue = SongId;
        serialized.FindProperty("bpm").floatValue = bpm;
        SerializedProperty points = serialized.FindProperty("timingPoints");
        points.arraySize = beatBpm.Length / 2;
        for (int i = 0; i < points.arraySize; i++)
        {
            SerializedProperty point = points.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("beat").floatValue = beatBpm[i * 2];
            point.FindPropertyRelative("bpm").floatValue = beatBpm[i * 2 + 1];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return song;
    }

    private ChartEditSession CreateSession(SongData song, float lengthSeconds)
    {
        GameObject owner = new GameObject("ChartEditSession_SongTiming");
        created.Add(owner);
        ChartEditSession session = owner.AddComponent<ChartEditSession>();
        session.Init(new EditorEntryData { songId = song.SongId, chartName = "곡 BPM 구간", level = 1 }, song.SongId, song.TimingPoints, song.OffsetMs, lengthSeconds);
        return session;
    }

    private static List<TimingPointData> Points(params float[] beatBpm)
    {
        List<TimingPointData> list = new List<TimingPointData>();
        for (int i = 0; i < beatBpm.Length; i += 2) list.Add(new TimingPointData { beat = beatBpm[i], bpm = beatBpm[i + 1] });
        return list;
    }

    private static List<BpmSection> Sections(params float[] tickBpm)
    {
        List<BpmSection> list = new List<BpmSection>();
        for (int i = 0; i < tickBpm.Length; i += 2) list.Add(new BpmSection((int)tickBpm[i], tickBpm[i + 1]));
        return list;
    }

    [Test]
    public void NoTimingPoints_UsesSingleBpm_AndNewChartStartsWithOneSection()
    {
        SongData song = CreateSong(216f);
        Assert.IsFalse(song.HasTimingPoints);
        Assert.AreEqual(1, song.TimingPoints.Count);
        Assert.AreEqual(0f, song.TimingPoints[0].beat);
        Assert.AreEqual(216f, song.TimingPoints[0].bpm);
        Assert.AreEqual(216f, song.StartBpm);
        Assert.AreEqual(216f, song.PromptBpm, "promptBpm 0 → 시작 BPM 기준");
        Assert.AreEqual("216", BpmDisplayRules.Format(song, null));

        GameObject owner = new GameObject("ChartEditSession_SongTiming");
        created.Add(owner);
        ChartEditSession session = owner.AddComponent<ChartEditSession>();
        session.Init(new EditorEntryData { songId = SongId, chartName = "새 채보", level = 1 }, song);
        Assert.AreEqual(1, session.BpmSections.Count);
        Assert.AreEqual(0, session.BpmSections[0].Tick);
        Assert.AreEqual(216f, session.BpmSections[0].Bpm);
        Assert.IsTrue(session.IsSongBpmSections);
        Assert.AreEqual(1, session.BuildChartData().timingPoints.Count, "저장하면 1구간");
    }

    [Test]
    public void ThreeSections_NewChartStartsWithAll_AndResetKeepsAll()
    {
        SongData song = CreateSong(150.07f, 0f, 150.07f, 150f, 150.68f, 280f, 151.2f);
        Assert.IsTrue(song.HasTimingPoints);
        Assert.AreEqual(3, song.TimingPoints.Count);
        Assert.AreEqual(150.07f, song.StartBpm);

        ChartEditSession session = CreateSession(song, 200f);
        Assert.AreEqual(3, session.BpmSections.Count, "새 채보가 곡 구간 3개로 시작");
        Assert.AreEqual(150 * T, session.BpmSections[1].Tick);
        Assert.AreEqual(280 * T, session.BpmSections[2].Tick);
        Assert.AreEqual(151.2f, session.BpmSections[2].Bpm);
        Assert.IsTrue(session.IsSongBpmSections);
        Assert.IsFalse(session.ResetBpmToSongSections(), "이미 곡 구간과 같음");

        // 구간을 바꾸고 되돌리면 곡 구간 3개 전체로 돌아온다
        Assert.IsTrue(session.SetBpmSection(150 * T, 170f, out _));
        Assert.IsTrue(session.AddBpmSection(32 * T, 200f, out _));
        Assert.IsFalse(session.IsSongBpmSections);
        Assert.IsTrue(session.ResetBpmToSongSections());
        Assert.AreEqual(3, session.BpmSections.Count, "되돌리기 후에도 3구간");
        Assert.IsTrue(session.IsSongBpmSections);
        Assert.AreEqual(150.68f, session.BpmSections[1].Bpm);
        Assert.AreEqual(GameConstants.Editor.HistoryResetBpm, session.Undo());
        Assert.AreEqual(4, session.BpmSections.Count);

        // 채보 구간을 쓸 수 없으면(모두 BPM 0) 곡 구간 전체를 쓴다
        ChartData chart = session.BuildChartData();
        chart.timingPoints = Points(0f, 0f);
        session.ApplyChartData(chart);
        Assert.IsTrue(session.IsSongBpmSections, "쓸 채보 구간이 없으면 곡 구간");
    }

    [Test]
    public void SameSections_ComparesCountTickAndBpm()
    {
        List<BpmSection> song = Sections(0, 150.07f, 150 * T, 150.68f, 280 * T, 151.2f);
        Assert.IsTrue(BpmSectionRules.SameSections(song, Sections(0, 150.07f, 150 * T, 150.68f, 280 * T, 151.2f)));
        Assert.IsTrue(BpmSectionRules.SameSections(song, Sections(0, 150.0700073f, 150 * T, 150.68f, 280 * T, 151.2f)), "소수 둘째 자리 기준");
        Assert.IsFalse(BpmSectionRules.SameSections(song, Sections(0, 150.07f, 150 * T, 150.69f, 280 * T, 151.2f)), "BPM 하나 다름");
        Assert.IsFalse(BpmSectionRules.SameSections(song, Sections(0, 150.07f, 151 * T, 150.68f, 280 * T, 151.2f)), "tick 하나 다름");
        Assert.IsFalse(BpmSectionRules.SameSections(song, Sections(0, 150.07f, 150 * T, 150.68f)), "구간 수 다름");
        Assert.IsFalse(BpmSectionRules.SameSections(song, Sections(0, 150.61f)), "예전 커스텀 채보(150.61 1구간)는 다름 → 경고");
    }

    [Test]
    public void ValidateTimingPoints_FindsErrors()
    {
        AssertValid(Points(0f, 150.07f, 150f, 150.68f, 280f, 151.2f), true, 0, "정상 3구간");
        AssertValid(Points(4f, 150f), false, 0, "첫 구간이 0박이 아님");
        AssertValid(Points(0f, 150f, 16f, 180f, 16f, 200f), false, 0, "같은 beat 중복");
        AssertValid(Points(0f, 150f, 32f, 180f, 16f, 200f), false, 0, "내림차순");
        AssertValid(Points(0f, 29.99f), false, 0, "30 미만");
        AssertValid(Points(0f, 150f, 16f, 400.01f), false, 0, "400 초과");
        AssertValid(new List<TimingPointData>(), false, 0, "비어 있음");
        AssertValid(Points(0f, 150f, 16f, 150f), true, 1, "이웃 구간 같은 BPM은 경고만");
    }

    private static void AssertValid(List<TimingPointData> points, bool valid, int warningCount, string label)
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        Assert.AreEqual(valid, SongManager.ValidateTimingPoints(points, errors, warnings), $"{label}: {string.Join(", ", errors)}");
        Assert.AreEqual(valid, errors.Count == 0, label);
        Assert.AreEqual(warningCount, warnings.Count, $"{label} 경고 수");
    }

    [Test]
    public void CriticalVelocityAsset_MatchesOfficialNormalTimingPoints()
    {
        SongData song = AssetDatabase.LoadAssetAtPath<SongData>(CriticalVelocityAssetPath);
        Assert.IsNotNull(song, CriticalVelocityAssetPath);
        Assert.AreEqual(TestCharts.OfficialSongId, song.SongId);
        Assert.IsTrue(ChartRepository.TryLoad(TestCharts.OfficialSongId, TestCharts.OfficialChartId, ChartType.Official, out ChartData chart, out string error), error);

        List<BpmSection> songSections = BpmSectionRules.FromTimingPoints(song.TimingPoints, 0f, out int songSkipped);
        List<BpmSection> chartSections = BpmSectionRules.FromTimingPoints(chart.timingPoints, 0f, out int chartSkipped);
        Assert.AreEqual(0, songSkipped);
        Assert.AreEqual(0, chartSkipped);
        Assert.AreEqual(3, songSections.Count);
        Assert.IsTrue(BpmSectionRules.SameSections(songSections, chartSections),
            $"곡 [{BpmSectionRules.FormatSections(songSections)}] / 채보 [{BpmSectionRules.FormatSections(chartSections)}]");
        for (int i = 0; i < chart.timingPoints.Count; i++)
            Assert.AreEqual(chart.timingPoints[i].bpm, song.TimingPoints[i].bpm, $"{i}번 구간 float 값 그대로");

        Assert.IsTrue(SongManager.ValidateTimingPoints(song.TimingPoints, new List<string>(), new List<string>()));
        Assert.AreEqual(150.61f, song.PromptBpm, "promptBpm 150.61 (예전 displayBpm에서 옮김)");
        Assert.AreEqual(SongBpmDisplay.Main, song.BpmDisplay, "기본은 대표");
        Assert.AreEqual("150.68", BpmDisplayRules.Format(song, chart), "대표 = 150.61과 가장 가까운 실제 구간 값");
        Assert.AreEqual(song.TimingPoints[0].bpm, song.StartBpm);
        Assert.AreEqual(169, song.OffsetMs);
    }
}
