/// <summary>
/// 그리드 선 종류. 1박 안의 위치 k/n을 약분한 분모로 정한다.
/// </summary>
public enum GridLineType
{
    /// <summary>마디 시작 (분모 1, beat가 마디 박 수의 배수)</summary>
    Measure,
    /// <summary>그 외 박 (분모 1)</summary>
    Beat,
    /// <summary>분모 2</summary>
    Half,
    /// <summary>분모 4</summary>
    Quarter,
    /// <summary>분모 8</summary>
    Eighth,
    /// <summary>분모 16</summary>
    Sixteenth,
    /// <summary>분모 3, 6</summary>
    Third,
    /// <summary>분모 12</summary>
    Twelfth
}
