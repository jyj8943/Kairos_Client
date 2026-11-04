using System;

/// <summary>
/// 채보 JSON의 BPM 구간 하나 (기획서 2-2). 지금은 채보마다 1개, 변속을 넣으면 여러 개가 된다.
/// </summary>
[Serializable]
public class TimingPointData
{
    /// <summary>이 BPM이 시작되는 위치 (박)</summary>
    public float beat;

    /// <summary>BPM (0보다 커야 한다)</summary>
    public float bpm;
}
