using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// EditorKeySoundMath EditMode 테스트 (에디터 키음 예약 계산). 롱노트는 시작과 끝에서 각각 키음을 낸다.
/// </summary>
public class EditorKeySoundMathTests
{
    private const int T = GameConstants.Editor.TicksPerBeat;

    /// <summary>(tick, lane) 순으로 정렬된 노트: 0박 라인0, 1박 라인0·라인2(같은 박 두 라인), 2.5~3박 롱노트 라인1, 4박 라인3</summary>
    private static List<EditorNote> CreateNotes()
    {
        return new List<EditorNote>
        {
            EditorNote.Short(0, 0),
            EditorNote.Short(0, T),
            EditorNote.Short(2, T),
            new EditorNote(1, T * 5 / 2, T * 3),
            EditorNote.Short(3, T * 4)
        };
    }

    /// <summary>노트 5개(롱노트 1개 포함) → 키음 순간 6개: 0, 1박(2개), 2.5박(롱 시작), 3박(롱 끝), 4박</summary>
    private static List<EditorKeySoundMath.KeySoundEvent> CreateEvents()
    {
        List<EditorKeySoundMath.KeySoundEvent> events = new List<EditorKeySoundMath.KeySoundEvent>();
        EditorKeySoundMath.BuildEvents(CreateNotes(), events);
        return events;
    }

    /// <summary>롱노트는 시작·끝 두 번, 숏노트는 한 번. tick 순으로 정렬된다</summary>
    [Test]
    public void BuildEvents_LongNoteHasStartAndEnd()
    {
        List<EditorKeySoundMath.KeySoundEvent> events = CreateEvents();

        Assert.AreEqual(6, events.Count);
        CollectionAssert.AreEqual(new[] { 0, T, T, T * 5 / 2, T * 3, T * 4 }, events.ConvertAll(e => e.Tick));
        CollectionAssert.AreEqual(new[] { 0, 0, 2, 1, 1, 3 }, events.ConvertAll(e => e.Lane));
        CollectionAssert.AreEqual(new[] { false, false, false, false, true, false }, events.ConvertAll(e => e.IsEnd));
    }

    /// <summary>긴 롱노트의 끝이 뒤 노트들보다 늦으면 끝 순간은 tick 순서 자리로 들어간다</summary>
    [Test]
    public void BuildEvents_LongEndIsSortedByTick()
    {
        List<EditorNote> notes = new List<EditorNote>
        {
            new EditorNote(0, 0, T * 4),
            EditorNote.Short(1, T),
            EditorNote.Short(2, T * 2),
            EditorNote.Short(1, T * 4)
        };
        List<EditorKeySoundMath.KeySoundEvent> events = new List<EditorKeySoundMath.KeySoundEvent>();
        EditorKeySoundMath.BuildEvents(notes, events);

        CollectionAssert.AreEqual(new[] { 0, T, T * 2, T * 4, T * 4 }, events.ConvertAll(e => e.Tick));
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 1 }, events.ConvertAll(e => e.Lane), "같은 tick이면 라인 순");
        Assert.IsTrue(events[3].IsEnd);
    }

    /// <summary>현재 위치 이후 첫 키음: 박 위에 정확히 있으면 포함, 박 중간이면 다음 순간</summary>
    [Test]
    public void FirstAtOrAfter()
    {
        List<EditorKeySoundMath.KeySoundEvent> events = CreateEvents();

        Assert.AreEqual(0, EditorKeySoundMath.FindFirstAtOrAfter(events, 0));
        Assert.AreEqual(1, EditorKeySoundMath.FindFirstAtOrAfter(events, EditorKeySoundMath.BeatToFirstTick(1f)), "1박 위 → 1박 첫 노트(같은 박 두 개 중 앞)");
        Assert.AreEqual(3, EditorKeySoundMath.FindFirstAtOrAfter(events, EditorKeySoundMath.BeatToFirstTick(1.01f)), "1박 조금 뒤 → 2.5박 롱노트 시작");
        Assert.AreEqual(4, EditorKeySoundMath.FindFirstAtOrAfter(events, EditorKeySoundMath.BeatToFirstTick(2.75f)), "롱노트 도중 → 롱노트 끝(3박)");
        Assert.AreEqual(6, EditorKeySoundMath.FindFirstAtOrAfter(events, EditorKeySoundMath.BeatToFirstTick(4.5f)), "뒤에 없음 → Count");
        Assert.AreEqual(0, EditorKeySoundMath.FindFirstAtOrAfter(new List<EditorKeySoundMath.KeySoundEvent>(), 10), "빈 목록");
    }

    /// <summary>예약 범위 안 목록: 같은 박 여러 라인은 노트마다, 롱노트는 시작과 끝에서 각각</summary>
    [Test]
    public void CollectUntil_WindowAndNextIndex()
    {
        List<EditorKeySoundMath.KeySoundEvent> events = CreateEvents();
        List<int> result = new List<int>();

        int next = EditorKeySoundMath.CollectUntil(events, 1, T, result);
        CollectionAssert.AreEqual(new[] { 1, 2 }, result, "1박의 두 라인 노트 모두");
        Assert.AreEqual(3, next);

        next = EditorKeySoundMath.CollectUntil(events, next, T * 5 / 2, result);
        CollectionAssert.AreEqual(new[] { 3 }, result, "롱노트 시작(2.5박)");
        Assert.AreEqual(4, next);

        next = EditorKeySoundMath.CollectUntil(events, next, T * 3, result);
        CollectionAssert.AreEqual(new[] { 4 }, result, "롱노트 끝(3박)도 키음");
        Assert.IsTrue(events[4].IsEnd);
        Assert.AreEqual(5, next);

        next = EditorKeySoundMath.CollectUntil(events, next, T * 3, result);
        Assert.IsEmpty(result, "범위 밖이면 비어 있고 인덱스는 그대로");
        Assert.AreEqual(5, next);

        next = EditorKeySoundMath.CollectUntil(events, next, T * 100, result);
        CollectionAssert.AreEqual(new[] { 5 }, result);
        Assert.AreEqual(6, next);
    }

    /// <summary>재생 구간의 키음 수 (양 끝 포함, 같은 박 여러 라인 포함, 롱노트 끝 포함)</summary>
    [Test]
    public void CountInRange()
    {
        List<EditorKeySoundMath.KeySoundEvent> events = CreateEvents();

        Assert.AreEqual(6, EditorKeySoundMath.CountInRange(events, 0, T * 4));
        Assert.AreEqual(2, EditorKeySoundMath.CountInRange(events, T, T), "같은 박 두 라인");
        Assert.AreEqual(4, EditorKeySoundMath.CountInRange(events, T, T * 3), "1박 두 개 + 롱 시작 + 롱 끝");
        Assert.AreEqual(1, EditorKeySoundMath.CountInRange(events, T * 11 / 4, T * 3), "롱노트 도중부터면 끝만");
        Assert.AreEqual(0, EditorKeySoundMath.CountInRange(events, T * 4 + 1, T * 10));
        Assert.AreEqual(0, EditorKeySoundMath.CountInRange(events, T * 3, T), "거꾸로 된 구간은 0");
    }
}
