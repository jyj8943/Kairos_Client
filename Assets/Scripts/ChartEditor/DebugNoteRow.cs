using System;

/// <summary>
/// ChartEditSession 인스펙터에 노트 하나를 보여 주기 위한 확인용 행. 값을 바꿔도 동작에 영향이 없다.
/// </summary>
[Serializable]
public struct DebugNoteRow
{
    /// <summary>라인 번호 (0부터)</summary>
    public int lane;

    /// <summary>라인 키 ("A" / "S" / ";" / "'")</summary>
    public string keyName;

    /// <summary>위치 (1박 = 48틱)</summary>
    public int tick;

    /// <summary>위치 (박) = tick ÷ 48</summary>
    public float beat;

    /// <summary>사람이 읽는 위치. 예: "마디 2 · 3박 · 12/48"</summary>
    public string position;

    /// <summary>"short" / "long"</summary>
    public string type;

    /// <summary>롱노트 끝 tick. 숏노트는 tick과 같다</summary>
    public int endTick;
}
