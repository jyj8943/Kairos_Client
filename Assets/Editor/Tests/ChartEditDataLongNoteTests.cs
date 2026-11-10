using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// ChartEditData 롱노트 규칙 EditMode 테스트: 최소 길이 1/4박, 같은 라인 겹침 금지(끝 tick이 같아도 겹침), 어느 부분이든 삭제, 구간 검색.
/// </summary>
public class ChartEditDataLongNoteTests
{
    private const int T = GameConstants.Editor.TicksPerBeat;
    private const int Min = GameConstants.Editor.MinLongNoteTicks;

    private static ChartEditData CreateData()
    {
        return new ChartEditData(T * 100, GameConstants.Editor.LaneCount);
    }

    /// <summary>최소 길이: 1/4박(12틱)은 되고 11틱은 TooShort. 길이 0도 TooShort</summary>
    [Test]
    public void MinLength_QuarterBeat()
    {
        ChartEditData data = CreateData();
        Assert.AreEqual(12, Min);

        Assert.IsFalse(data.TryAddLong(0, 0, Min - 1, out NoteEditError error));
        Assert.AreEqual(NoteEditError.TooShort, error);
        Assert.IsFalse(data.TryAddLong(0, T, T, out error));
        Assert.AreEqual(NoteEditError.TooShort, error);

        Assert.IsTrue(data.TryAddLong(0, 0, Min, out error));
        Assert.AreEqual(NoteEditError.None, error);
        Assert.AreEqual(1, data.LongCount);
        Assert.AreEqual(0, data.ShortCount);
    }

    /// <summary>끝이 시작보다 앞이어도 작은 쪽이 시작이 된다 (위아래 어느 쪽으로 드래그해도)</summary>
    [Test]
    public void ReversedRange_SmallerIsStart()
    {
        ChartEditData data = CreateData();
        Assert.IsTrue(data.TryAddLong(2, T * 4, T * 2, out _));

        EditorNote note = data.Notes[0];
        Assert.AreEqual(T * 2, note.Tick);
        Assert.AreEqual(T * 4, note.EndTick);
        Assert.IsTrue(note.IsLong);
    }

    /// <summary>같은 라인의 숏노트·롱노트와 겹치면 Overlap. 끝·시작 tick이 다른 노트 위치와 같아도 겹침, 다른 라인은 상관없음</summary>
    [Test]
    public void Overlap_SameLaneOnly()
    {
        ChartEditData data = CreateData();
        Assert.IsTrue(data.TryAdd(0, T * 2));
        Assert.IsTrue(data.TryAddLong(0, T * 4, T * 6, out _));

        NoteEditError error;
        Assert.IsFalse(data.TryAddLong(0, T, T * 3, out error), "숏노트를 몸통에 품음");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T, T * 2, out error), "끝이 숏노트 위치와 같은 tick");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T * 2, T * 3, out error), "시작이 숏노트 위치와 같은 tick");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T * 6, T * 7, out error), "시작이 앞 롱노트 끝과 같은 tick");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T * 3, T * 4, out error), "끝이 뒤 롱노트 시작과 같은 tick");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T * 3, T * 8, out error), "다른 롱노트를 통째로 품음");
        Assert.AreEqual(NoteEditError.Overlap, error);
        Assert.IsFalse(data.TryAddLong(0, T * 4 + Min, T * 5, out error), "다른 롱노트 안");
        Assert.AreEqual(NoteEditError.Overlap, error);

        Assert.IsTrue(data.TryAddLong(0, T * 6 + 1, T * 7, out _), "앞 롱노트 끝 바로 다음 tick부터는 된다");
        Assert.IsTrue(data.TryAddLong(1, T, T * 8, out _), "다른 라인은 겹쳐도 된다");
        Assert.AreEqual(1, data.ShortCount);
        Assert.AreEqual(3, data.LongCount);
    }

    /// <summary>숏노트 TryAdd도 롱노트 구간(시작·몸통·끝) 안이면 Overlap</summary>
    [Test]
    public void ShortNote_InsideLong_Rejected()
    {
        ChartEditData data = CreateData();
        Assert.IsTrue(data.TryAddLong(1, T * 2, T * 4, out _));

        foreach (int tick in new[] { T * 2, T * 3, T * 4 })
        {
            Assert.IsFalse(data.TryAdd(1, tick, out NoteEditError error), $"tick {tick}");
            Assert.AreEqual(NoteEditError.Overlap, error);
        }
        Assert.IsFalse(data.TryAdd(1, T * 3), "out 없는 TryAdd도 같은 규칙");

        Assert.IsTrue(data.TryAdd(1, T * 4 + 1), "끝 바로 다음 tick");
        Assert.IsTrue(data.TryAdd(2, T * 3), "다른 라인");
    }

    /// <summary>범위 밖(곡 끝을 넘음, 음수, 라인 밖)은 OutOfRange</summary>
    [Test]
    public void OutOfRange()
    {
        ChartEditData data = CreateData();
        Assert.IsFalse(data.TryAddLong(0, T * 99, T * 101, out NoteEditError error));
        Assert.AreEqual(NoteEditError.OutOfRange, error);
        Assert.IsFalse(data.TryAddLong(0, -T, T, out error));
        Assert.AreEqual(NoteEditError.OutOfRange, error);
        Assert.IsFalse(data.TryAddLong(4, 0, T, out error));
        Assert.AreEqual(NoteEditError.OutOfRange, error);
        Assert.IsTrue(data.TryAddLong(0, T * 99, T * 100, out _), "곡 끝 tick까지는 된다");
    }

    /// <summary>시작·몸통·끝 어느 tick을 가리켜도 그 롱노트를 찾고 지운다</summary>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Remove_AnyPartOfLong(int part)
    {
        ChartEditData data = CreateData();
        Assert.IsTrue(data.TryAddLong(3, T * 2, T * 4, out _));
        Assert.IsTrue(data.TryAdd(3, T * 5));
        int tick = part == 0 ? T * 2 : part == 1 ? T * 3 + 7 : T * 4;

        Assert.IsTrue(data.TryFindAt(3, tick, out EditorNote found));
        Assert.AreEqual(T * 2, found.Tick);
        Assert.AreEqual(T * 4, found.EndTick);

        int changed = 0;
        data.OnChanged += () => changed++;
        Assert.IsTrue(data.TryRemove(3, tick));
        Assert.AreEqual(1, changed);
        Assert.AreEqual(0, data.LongCount);
        Assert.AreEqual(1, data.ShortCount, "옆 숏노트는 그대로");
        Assert.IsFalse(data.TryFindAt(3, tick, out _));
        Assert.IsFalse(data.TryRemove(3, T * 4 + 1), "아무것도 없는 자리");
        Assert.IsFalse(data.TryRemove(2, T * 3), "다른 라인");
    }

    /// <summary>구간 검색: 구간 앞에서 시작해 구간 안으로 들어오는 롱노트도 포함, 구간 전에 끝난 것은 제외</summary>
    [Test]
    public void GetRange_IncludesLongStartingBefore()
    {
        ChartEditData data = CreateData();
        Assert.IsTrue(data.TryAddLong(0, 0, T * 20, out _));
        Assert.IsTrue(data.TryAddLong(1, T, T * 2, out _));
        Assert.IsTrue(data.TryAdd(2, T * 3));
        Assert.IsTrue(data.TryAdd(3, T * 12));
        Assert.IsTrue(data.TryAddLong(3, T * 30, T * 31, out _));

        List<EditorNote> result = new List<EditorNote>();
        data.GetRange(T * 10, T * 15, result);
        Assert.AreEqual(2, result.Count, "0~20박 롱노트와 12박 숏노트");
        Assert.AreEqual(0, result[0].Tick);
        Assert.AreEqual(T * 12, result[1].Tick);

        data.GetRange(T * 2, T * 2, result);
        Assert.AreEqual(2, result.Count, "끝이 구간 시작과 같은 롱노트도 포함");

        data.GetRange(T * 21, T * 29, result);
        Assert.IsEmpty(result);
    }

    /// <summary>불러오기(ReplaceAll): 겹치는 노트·1/4박보다 짧은 롱노트는 넣지 않고, 수를 센다</summary>
    [Test]
    public void ReplaceAll_SkipsOverlapAndTooShort()
    {
        ChartEditData data = CreateData();
        int skipped = data.ReplaceAll(new[]
        {
            new EditorNote(0, T * 2, T * 4),
            EditorNote.Short(0, T * 3),
            EditorNote.Short(0, T * 4),
            new EditorNote(1, 0, Min - 1),
            EditorNote.Short(1, T),
            EditorNote.Short(1, T),
            new EditorNote(2, T * 4 / 3, T * 8 / 3),
        });

        Assert.AreEqual(4, skipped, "몸통 안 숏노트, 끝과 같은 tick 숏노트, 짧은 롱노트, 같은 자리 숏노트");
        Assert.AreEqual(2, data.LongCount);
        Assert.AreEqual(1, data.ShortCount);
    }
}
