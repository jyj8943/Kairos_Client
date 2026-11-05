using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// EditorKeySoundMath EditMode 테스트 (에디터 키음 예약 계산).
/// </summary>
public class EditorKeySoundMathTests
{
    private const int T = GameConstants.Editor.TicksPerBeat;

    /// <summary>(tick, lane) 순으로 정렬된 노트: 0박 라인0, 1박 라인0·라인2(같은 박 두 라인), 2.5박 롱노트, 4박 라인3</summary>
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

    /// <summary>현재 위치 이후 첫 노트: 박 위에 정확히 있으면 포함, 박 중간이면 다음 노트</summary>
    [Test]
    public void FirstNoteAtOrAfter()
    {
        List<EditorNote> notes = CreateNotes();

        Assert.AreEqual(0, EditorKeySoundMath.FindFirstNoteAtOrAfter(notes, 0));
        Assert.AreEqual(1, EditorKeySoundMath.FindFirstNoteAtOrAfter(notes, EditorKeySoundMath.BeatToFirstTick(1f)), "1박 위 → 1박 첫 노트(같은 박 두 개 중 앞)");
        Assert.AreEqual(3, EditorKeySoundMath.FindFirstNoteAtOrAfter(notes, EditorKeySoundMath.BeatToFirstTick(1.01f)), "1박 조금 뒤 → 2.5박 롱노트");
        Assert.AreEqual(5, EditorKeySoundMath.FindFirstNoteAtOrAfter(notes, EditorKeySoundMath.BeatToFirstTick(4.5f)), "뒤에 노트 없음 → Count");
        Assert.AreEqual(0, EditorKeySoundMath.FindFirstNoteAtOrAfter(new List<EditorNote>(), 10), "빈 목록");
    }

    /// <summary>예약 범위 안 노트 목록: 같은 박 여러 라인은 노트마다, 롱노트는 시작 tick 한 번</summary>
    [Test]
    public void CollectNotesUntil_WindowAndNextIndex()
    {
        List<EditorNote> notes = CreateNotes();
        List<int> result = new List<int>();

        int next = EditorKeySoundMath.CollectNotesUntil(notes, 1, T, result);
        CollectionAssert.AreEqual(new[] { 1, 2 }, result, "1박의 두 라인 노트 모두");
        Assert.AreEqual(3, next);

        next = EditorKeySoundMath.CollectNotesUntil(notes, next, T * 3, result);
        CollectionAssert.AreEqual(new[] { 3 }, result, "롱노트는 시작 tick(2.5박)에서 한 번. 끝 tick(3박)은 세지 않는다");
        Assert.AreEqual(4, next);

        next = EditorKeySoundMath.CollectNotesUntil(notes, next, T * 3, result);
        Assert.IsEmpty(result, "범위 밖이면 비어 있고 인덱스는 그대로");
        Assert.AreEqual(4, next);

        next = EditorKeySoundMath.CollectNotesUntil(notes, next, T * 100, result);
        CollectionAssert.AreEqual(new[] { 4 }, result);
        Assert.AreEqual(5, next);
    }

    /// <summary>재생 구간의 노트 수 (양 끝 포함, 같은 박 여러 라인 포함)</summary>
    [Test]
    public void CountNotesInRange()
    {
        List<EditorNote> notes = CreateNotes();

        Assert.AreEqual(5, EditorKeySoundMath.CountNotesInRange(notes, 0, T * 4));
        Assert.AreEqual(2, EditorKeySoundMath.CountNotesInRange(notes, T, T), "같은 박 두 라인");
        Assert.AreEqual(3, EditorKeySoundMath.CountNotesInRange(notes, T, T * 3));
        Assert.AreEqual(0, EditorKeySoundMath.CountNotesInRange(notes, T * 4 + 1, T * 10));
        Assert.AreEqual(0, EditorKeySoundMath.CountNotesInRange(notes, T * 3, T), "거꾸로 된 구간은 0");
    }
}
