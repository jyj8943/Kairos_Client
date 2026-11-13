using System.Collections.Generic;

/// <summary>
/// BPM 구간 편집 기록 하나 (추가·수정·삭제·곡 기본 BPM으로 되돌리기). 바꾸기 전 · 바꾼 뒤 구간 목록을 값으로 들고 있다.
/// - Apply는 바꾼 뒤 목록으로, Revert는 바꾸기 전 목록으로 세션의 구간을 통째로 바꾼다 (기록을 남기지 않는 내부 경로).
/// - 삭제로 뒤 구간까지 지워지면 EditHistory 묶음 기록으로 두 기록을 하나로 묶는다.
/// </summary>
public class BpmSectionEditAction : IEditAction
{
    private readonly BpmSection[] before;
    private readonly BpmSection[] after;

    public string Description { get; }

    /// <summary>바꾸기 전 구간 목록 (확인용)</summary>
    public IReadOnlyList<BpmSection> Before => before;

    /// <summary>바꾼 뒤 구간 목록 (확인용)</summary>
    public IReadOnlyList<BpmSection> After => after;

    public BpmSectionEditAction(string description, IReadOnlyList<BpmSection> before, IReadOnlyList<BpmSection> after)
    {
        Description = description;
        this.before = Copy(before);
        this.after = Copy(after);
    }

    public bool Apply(ChartEditSession session)
    {
        return session.ApplyBpmSections(after);
    }

    public bool Revert(ChartEditSession session)
    {
        return session.ApplyBpmSections(before);
    }

    private static BpmSection[] Copy(IReadOnlyList<BpmSection> sections)
    {
        BpmSection[] copy = new BpmSection[sections.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = sections[i];
        return copy;
    }
}
