/// <summary>
/// 노트 추가가 실패한 이유 (ChartEditData.TryAdd / TryAddLong).
/// </summary>
public enum NoteEditError
{
    /// <summary>실패하지 않음</summary>
    None,

    /// <summary>라인이나 tick이 곡 범위(0 ~ 곡 끝) 밖</summary>
    OutOfRange,

    /// <summary>롱노트가 최소 길이(1/4박)보다 짧음</summary>
    TooShort,

    /// <summary>같은 라인의 다른 노트와 겹침</summary>
    Overlap
}
