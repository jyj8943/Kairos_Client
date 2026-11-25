using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

/// <summary>
/// 곡 미리 듣기 (11/25) 규칙 EditMode 확인.
/// 1. 구간(SongPreviewRules): 기본값 · 곡 끝에서 자르기 · 시작이 곡 길이를 넘으면 기본값 · 값 검사 경고 · F1 저장 값
/// 2. 시간표(SongPreviewSequencer): 페이드 인/아웃 · 반복 · 같은 곡 유지 · 곡 바꾸기 0.25초 · 멈추기 · GC 할당 0
/// 3. 시작 시점(SongPreviewSelector): 들어가기 전 기억 · 0.3초 지연과 취소 · 같은 곡 유지 · 떠난 뒤 시작 안 함
/// </summary>
public class SongPreviewTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created) Object.DestroyImmediate(item);
        created.Clear();
    }

    private AudioClip Clip(string name, float seconds)
    {
        AudioClip clip = AudioClip.Create(name, (int)(seconds * 8000), 1, 8000, false);
        created.Add(clip);
        return clip;
    }

    private SongData Song(string id, float seconds, int startMs = 0, int lengthMs = 0)
    {
        SongData song = ScriptableObject.CreateInstance<SongData>();
        created.Add(song);
        Set(song, "songId", id);
        Set(song, "clip", Clip(id, seconds));
        Set(song, "previewStartMs", startMs);
        Set(song, "previewLengthMs", lengthMs);
        return song;
    }

    private static void Set(SongData song, string field, object value)
    {
        typeof(SongData).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(song, value);
    }

    // ───── 1. 구간 ─────

    [Test]
    public void Range_DefaultsClampAndFallback()
    {
        Assert.AreEqual(35.0, SongPreviewRules.GetStart(0, 100), 1e-9, "0 → 길이 × 0.35");
        Assert.AreEqual(15.0, SongPreviewRules.GetLength(0, 0, 100), 1e-9, "0 → 15초");
        Assert.AreEqual(12.5, SongPreviewRules.GetStart(12500, 100), 1e-9);
        Assert.AreEqual(8.0, SongPreviewRules.GetLength(12500, 8000, 100), 1e-9);
        Assert.AreEqual(5.0, SongPreviewRules.GetLength(95000, 15000, 100), 1e-9, "곡 끝에서 자름");
        Assert.AreEqual(35.0, SongPreviewRules.GetStart(100000, 100), 1e-9, "시작 = 곡 길이 → 기본값");
        Assert.AreEqual(35.0, SongPreviewRules.GetStart(250000, 100), 1e-9, "시작 > 곡 길이 → 기본값");
        Assert.AreEqual(15.0, SongPreviewRules.GetLength(250000, 0, 100), 1e-9);
        Assert.AreEqual(35.0, SongPreviewRules.GetStart(-5, 100), 1e-9);
        Assert.AreEqual(15.0, SongPreviewRules.GetLength(0, -5, 100), 1e-9);
        Assert.AreEqual(6.5, SongPreviewRules.GetLength(0, 0, 10), 1e-9, "짧은 곡도 끝에서 자름");
        Assert.AreEqual(0.0, SongPreviewRules.GetStart(0, 0));
        Assert.AreEqual(0.0, SongPreviewRules.GetLength(0, 0, 0), "음원 없음");
    }

    [Test]
    public void Range_SongData_AndF1Write()
    {
        SongData song = Song("a", 40f);
        Assert.AreEqual(14.0, song.PreviewStart, 1e-3);
        Assert.AreEqual(15.0, song.PreviewLength, 1e-3);
        Canvas_SongTuning.WritePreviewStart(song, 12.3456);
        Assert.AreEqual(12346, song.PreviewStartMs);
        Assert.AreEqual(12.346, song.PreviewStart, 1e-6);
        Canvas_SongTuning.WritePreviewStart(song, 0);
        Assert.AreEqual(1, song.PreviewStartMs, "0은 기본값이라는 뜻이라 맨 앞은 1ms");
    }

    [Test]
    public void Validate_Warnings()
    {
        List<string> warnings = new List<string>();
        SongPreviewRules.Validate(0, 0, 100, warnings);
        SongPreviewRules.Validate(85000, 15000, 100, warnings);
        Assert.IsEmpty(warnings, "기본값 · 딱 맞는 구간은 경고 없음");
        SongPreviewRules.Validate(-1, 0, 100, warnings);
        SongPreviewRules.Validate(0, -1, 100, warnings);
        SongPreviewRules.Validate(90000, 15000, 100, warnings);
        Assert.AreEqual(3, warnings.Count, string.Join(" / ", warnings));
    }

    [Test]
    public void ProjectSongs_UseDefaults_NoWarnings()
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:SongData"))
        {
            SongData song = UnityEditor.AssetDatabase.LoadAssetAtPath<SongData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            List<string> warnings = new List<string>();
            SongPreviewRules.Validate(song.PreviewStartMs, song.PreviewLengthMs, song.Length, warnings);
            Assert.IsEmpty(warnings, song.SongId);
            Debug.Log($"[SongPreviewTest] {song.SongId}: 길이 {song.Length:0.000}초, 미리 듣기 {song.PreviewStart:0.000}초부터 {song.PreviewLength:0.000}초");
        }
    }

    // ───── 2. 시간표 ─────

    [Test]
    public void Sequencer_FadeTable_AndLoop()
    {
        SongPreviewSequencer preview = new SongPreviewSequencer();
        AudioClip a = Clip("a", 40f);
        Assert.AreEqual(SongPreviewCommand.Start, preview.Play(a, 14, 15), "쉬는 중이면 바로 시작");
        Assert.AreEqual(0f, preview.Volume);
        StringBuilder log = new StringBuilder("[SongPreviewTest] 페이드 시간표 | 시각 | 음량\n");
        float At(double time)
        {
            Assert.AreEqual(SongPreviewCommand.None, preview.Tick(time - preview.Elapsed));
            log.Append($"  {time:0.00} | {preview.Volume:0.00}\n");
            return preview.Volume;
        }
        Assert.AreEqual(0f, At(0), 1e-5f);
        Assert.AreEqual(0.5f, At(0.25), 1e-5f);
        Assert.AreEqual(1f, At(0.5), 1e-5f);
        Assert.AreEqual(1f, At(7), 1e-5f);
        Assert.AreEqual(1f, At(14), 1e-5f, "끝 −1초");
        Assert.AreEqual(0.5f, At(14.5), 1e-5f, "끝 −0.5초");
        Assert.AreEqual(0.01f, At(14.99), 1e-4f, "끝 직전");
        Assert.AreEqual(SongPreviewCommand.Start, preview.Tick(0.02), "끝 → 시작 지점으로");
        Assert.AreEqual(1, preview.LoopCount);
        Assert.AreEqual(0f, preview.Volume);
        Assert.AreEqual(14.0, preview.Start);
        log.Append($"  반복 시작 (위치 {preview.Start:0.0}초) | {preview.Volume:0.00}\n");
        Assert.AreEqual(0.5f, At(0.25), 1e-5f, "다시 페이드 인");
        Debug.Log(log.ToString());
    }

    [Test]
    public void Sequencer_SameSongKept_OtherSongSwitches()
    {
        SongPreviewSequencer preview = new SongPreviewSequencer();
        AudioClip a = Clip("a", 40f);
        AudioClip b = Clip("b", 40f);
        preview.Play(a, 14, 15);
        preview.Tick(2);
        Assert.AreEqual(SongPreviewCommand.None, preview.Play(a, 14, 15));
        Assert.AreEqual(2.0, preview.Elapsed, 1e-9, "같은 곡이면 이어서");
        Assert.IsFalse(preview.IsFadingOut);

        Assert.AreEqual(SongPreviewCommand.None, preview.Play(b, 5, 15));
        preview.Tick(0.125);
        Assert.AreEqual(0.5f, preview.Volume, 1e-5f, "0.25초 페이드 아웃");
        Assert.AreSame(a, preview.Clip);
        Assert.AreEqual(SongPreviewCommand.Start, preview.Tick(0.126));
        Assert.AreSame(b, preview.Clip);
        Assert.AreEqual(5.0, preview.Start);
        Assert.AreEqual(0f, preview.Volume);

        // 줄이는 중에 또 다른 곡: 마지막 곡만, 시간은 늘어나지 않는다
        preview.Tick(1);
        preview.Play(a, 14, 15);
        preview.Tick(0.2);
        preview.Play(a, 14, 15);
        Assert.AreEqual(SongPreviewCommand.Start, preview.Tick(0.06));
        Assert.AreSame(a, preview.Clip);
    }

    [Test]
    public void Sequencer_Duck_ThenSwitchImmediately_OrRestore()
    {
        SongPreviewSequencer preview = new SongPreviewSequencer();
        AudioClip a = Clip("a", 40f);
        AudioClip b = Clip("b", 40f);
        preview.SetDucked(true);
        preview.Play(a, 14, 15);
        preview.Tick(2);
        Assert.AreEqual(1f, preview.Volume, 1e-5f, "쉬는 중의 Duck은 남지 않는다");

        preview.SetDucked(true);
        preview.Tick(0.125);
        Assert.AreEqual(0.5f, preview.Volume, 1e-5f, "고른 순간부터 0.25초");
        preview.SetDucked(false);
        preview.Tick(0.125);
        Assert.AreEqual(1f, preview.Volume, 1e-5f, "돌아오면 되살림");
        Assert.AreEqual(2.25, preview.Elapsed, 1e-9, "위치는 이어짐");

        preview.SetDucked(true);
        preview.Tick(0.3);
        Assert.AreEqual(0f, preview.Volume);
        Assert.IsTrue(preview.IsActive, "멈추지 않고 조용히 흐름");
        Assert.AreEqual(SongPreviewCommand.Start, preview.Play(b, 5, 15), "다 줄었으면 바로 시작");
        Assert.AreSame(b, preview.Clip);
        preview.Tick(1);
        Assert.AreEqual(1f, preview.Volume, 1e-5f);

        preview.SetDucked(true);
        preview.Tick(0.2);
        Assert.AreEqual(SongPreviewCommand.None, preview.Play(a, 14, 15), "덜 줄었으면 남은 만큼만 더 줄인다");
        Assert.AreEqual(SongPreviewCommand.Start, preview.Tick(0.051));
    }

    [Test]
    public void Sequencer_Stop_FadeOrImmediate_DropsPending()
    {
        SongPreviewSequencer preview = new SongPreviewSequencer();
        AudioClip a = Clip("a", 40f);
        AudioClip b = Clip("b", 40f);
        preview.Play(a, 14, 15);
        preview.Tick(1);
        Assert.AreEqual(SongPreviewCommand.None, preview.Stop(0.3));
        preview.Tick(0.15);
        Assert.AreEqual(0.5f, preview.Volume, 1e-5f);
        Assert.AreEqual(SongPreviewCommand.Stop, preview.Tick(0.16));
        Assert.IsFalse(preview.IsActive);
        Assert.IsNull(preview.Clip);

        preview.Play(a, 14, 15);
        preview.Tick(1);
        preview.Play(b, 5, 15);
        preview.Stop(0.3);
        Assert.AreEqual(SongPreviewCommand.Stop, preview.Tick(0.31), "기다리던 다음 곡은 버린다");

        preview.Play(a, 14, 15);
        Assert.AreEqual(SongPreviewCommand.Stop, preview.Stop(0), "0이면 바로");
        Assert.AreEqual(SongPreviewCommand.None, preview.Stop(0));
        Assert.AreEqual(SongPreviewCommand.None, preview.Play(null, 0, 15));
        Assert.AreEqual(SongPreviewCommand.None, preview.Tick(1));
    }

    [Test]
    public void Sequencer_Tick_NoAllocation()
    {
        SongPreviewSequencer preview = new SongPreviewSequencer();
        preview.Play(Clip("a", 40f), 14, 2);
        preview.Tick(0.016);
        Assert.That(() =>
        {
            for (int i = 0; i < 300; i++) preview.Tick(0.016);
        }, Is.Not.AllocatingGCMemory());
        Assert.Greater(preview.LoopCount, 1);
    }

    // ───── 3. 시작 시점 ─────

    [Test]
    public void Selector_BeforeEnter_Remembers_EnterStartsImmediately()
    {
        SongPreviewSelector selector = new SongPreviewSelector();
        SongData a = Song("a", 40f);
        SongData b = Song("b", 40f);
        selector.Reset();
        selector.Select(a);
        selector.Select(b);
        Assert.IsNull(selector.Pending, "들어가기 전에는 0.3초 예약을 걸지 않는다");
        Assert.IsNull(selector.Tick(1f));
        Assert.IsNull(selector.Current);
        Assert.AreSame(b, selector.Enter(), "마지막에 고른 곡을 바로");
        Assert.IsNull(selector.Enter(), "한 번만");
    }

    [Test]
    public void Selector_Delay_CancelledByNextSong_OnlyLastStarts()
    {
        SongPreviewSelector selector = new SongPreviewSelector();
        SongData a = Song("a", 40f), b = Song("b", 40f), c = Song("c", 40f), d = Song("d", 40f);
        selector.Select(a);
        selector.Enter();

        // 0.1초 간격으로 세 번
        Assert.AreEqual(SongPreviewSelectResult.Duck, selector.Select(b), "고른 순간 앞 곡을 줄임");
        Assert.IsNull(selector.Tick(0.1f));
        selector.Select(c);
        Assert.IsNull(selector.Tick(0.1f));
        selector.Select(d);
        Assert.IsNull(selector.Tick(0.1f));
        Assert.IsNull(selector.Tick(0.15f), "마지막 곡부터 다시 0.3초");
        Assert.AreSame(a, selector.Current, "아직 앞 곡");
        Assert.AreSame(d, selector.Tick(0.06f));
        Assert.IsNull(selector.Tick(1f), "한 번만");
    }

    [Test]
    public void Selector_SameSong_KeepsPlaying_AndReturnCancelsPending()
    {
        SongPreviewSelector selector = new SongPreviewSelector();
        SongData a = Song("a", 40f), b = Song("b", 40f);
        selector.Select(a);
        selector.Enter();
        Assert.AreEqual(SongPreviewSelectResult.None, selector.Select(a));
        Assert.IsNull(selector.Pending, "같은 곡(난이도 · 탭 · 채보 줄)은 다시 시작하지 않음");
        selector.Select(b);
        selector.Tick(0.2f);
        Assert.AreEqual(SongPreviewSelectResult.Restore, selector.Select(a));
        Assert.IsNull(selector.Pending, "훑다가 돌아오면 앞 요청 취소");
        Assert.IsNull(selector.Tick(1f));
        Assert.AreSame(a, selector.Current);
    }

    [Test]
    public void Selector_Leave_StartsNothing()
    {
        SongPreviewSelector selector = new SongPreviewSelector();
        SongData a = Song("a", 40f), b = Song("b", 40f);
        selector.Select(a);
        selector.Enter();
        selector.Select(b);
        selector.Leave();
        Assert.IsNull(selector.Tick(1f));
        selector.Select(a);
        Assert.IsNull(selector.Tick(1f));
        Assert.IsFalse(selector.IsActive);
    }
}
