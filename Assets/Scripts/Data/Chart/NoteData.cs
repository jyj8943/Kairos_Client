using System;

/// <summary>
/// 채보 JSON의 노트 하나 (기획서 2-2). 위치는 시간(ms)이 아니라 박(beat)으로 저장한다.
/// </summary>
[Serializable]
public class NoteData
{
    /// <summary>노트 type 값</summary>
    public const string TypeShort = "short";
    public const string TypeLong = "long";

    /// <summary>노트 위치 (박 단위, 소수 가능)</summary>
    public float beat;

    /// <summary>라인 번호 (0부터)</summary>
    public int lane;

    /// <summary>"short" / "long"</summary>
    public string type;

    /// <summary>롱노트 끝 위치 (롱노트만)</summary>
    public float endBeat;
}
