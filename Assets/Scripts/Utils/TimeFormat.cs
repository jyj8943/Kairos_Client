using UnityEngine;

/// <summary>
/// 시간 표시 형식 모음. 화면마다 형식이 달라지지 않게 여기서만 만든다.
/// </summary>
public static class TimeFormat
{
    /// <summary>
    /// m:ss 형식 (예: 2:19). 초 아래는 버린다. 음수는 0으로 본다.
    /// </summary>
    public static string ToMinSec(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    /// <summary>
    /// m:ss.fff 형식 (예: 0:12.345). 밀리초 아래는 버린다. 음수는 0으로 본다.
    /// </summary>
    public static string ToMinSecMillis(float seconds)
    {
        int totalMillis = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 1000f);
        int totalSeconds = totalMillis / 1000;
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}.{totalMillis % 1000:000}";
    }
}
