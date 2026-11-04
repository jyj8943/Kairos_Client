/// <summary>
/// 채보 종류. 코드에서는 이 enum을 쓰고, JSON에 쓸 때만 문자열("official" / "custom")로 바꾼다 (ChartData.ToTypeString).
/// </summary>
public enum ChartType
{
    /// <summary>공식 채보 (빌드 안 StreamingAssets, 읽기 전용)</summary>
    Official,

    /// <summary>나만의 채보 (persistentDataPath/MyCharts)</summary>
    Custom
}
