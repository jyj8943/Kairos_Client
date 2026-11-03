/// <summary>
/// 편집 중인 노트 하나. 위치는 정수 tick(1박 = GameConstants.Editor.TicksPerBeat)으로 다룬다.
/// </summary>
public readonly struct EditorNote
{
    public readonly int Lane;
    public readonly int Tick;

    /// <summary>롱노트 끝 tick. 숏노트는 Tick과 같다</summary>
    public readonly int EndTick;

    public bool IsLong => EndTick > Tick;

    public EditorNote(int lane, int tick, int endTick)
    {
        Lane = lane;
        Tick = tick;
        EndTick = endTick;
    }

    /// <summary>숏노트를 만든다</summary>
    public static EditorNote Short(int lane, int tick)
    {
        return new EditorNote(lane, tick, tick);
    }
}
