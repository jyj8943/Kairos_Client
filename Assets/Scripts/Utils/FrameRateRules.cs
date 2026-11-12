using UnityEngine;

/// <summary>
/// 게임 프레임 설정 (한 곳). Bootstrapper가 매니저 Init 전에 한 번, 설정 창이 해상도·창 모드를 적용한 뒤에 다시 부른다 (모니터·모드가 바뀔 수 있으므로).
/// - 기본은 VSync (모니터 주사율에 맞춘다). 주사율을 못 읽어도 VSync를 쓴다.
/// - 모니터 주사율이 60Hz 미만이면 VSync를 끄고 60프레임을 목표로 한다 (GameConstants.Display.MinTargetFrameRate).
/// - 최소 프레임은 보장할 수 없고 상한만 정할 수 있다. VSync가 켜져 있으면 targetFrameRate는 무시된다.
/// </summary>
public static class FrameRateRules
{
    /// <summary>정한 프레임 설정</summary>
    public readonly struct Setting
    {
        public readonly int VSyncCount;
        public readonly int TargetFrameRate;

        public Setting(int vSyncCount, int targetFrameRate)
        {
            VSyncCount = vSyncCount;
            TargetFrameRate = targetFrameRate;
        }
    }

    /// <summary>
    /// 주사율로 설정을 정한다: 60Hz 이상(또는 못 읽음: 0 이하·NaN) → VSync 1, targetFrameRate -1 / 60Hz 미만 → VSync 0, targetFrameRate 60.
    /// </summary>
    public static Setting Decide(double refreshRateHz)
    {
        bool unreadable = double.IsNaN(refreshRateHz) || double.IsInfinity(refreshRateHz) || refreshRateHz <= 0;
        if (unreadable || refreshRateHz >= GameConstants.Display.MinTargetFrameRate) return new Setting(1, -1);
        return new Setting(0, GameConstants.Display.MinTargetFrameRate);
    }

    /// <summary>
    /// 지금 모니터 주사율(Screen.currentResolution.refreshRateRatio)로 VSync·targetFrameRate를 설정하고 로그를 남긴다.
    /// 최소 프레임은 보장할 수 없고 상한만 정할 수 있다. VSync가 켜져 있으면 targetFrameRate는 무시된다.
    /// </summary>
    /// <param name="reason">로그에 남길 부른 곳</param>
    public static void Apply(string reason)
    {
        double refreshRate = Screen.currentResolution.refreshRateRatio.value;
        Setting setting = Decide(refreshRate);
        QualitySettings.vSyncCount = setting.VSyncCount;
        Application.targetFrameRate = setting.TargetFrameRate;

        Debug.Log($"[FrameRateRules] {reason}: 주사율 {refreshRate:0.###}Hz → vSyncCount {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}");
    }
}
