/// <summary>
/// 개발용 인게임 설정 (손맛 비교 확인용). 인게임 F1 패널(Canvas_InGameDebug, Unity 에디터 전용)만 값을 바꾼다.
/// - static이라 같은 플레이 세션 동안 재시작·다시 하기·다른 곡으로 들어와도 유지되고, 게임을 다시 켜면 기본값(켬)으로 돌아간다.
/// - 인게임 코드가 읽으므로 빌드에도 들어가지만, 빌드에는 값을 바꾸는 패널이 없어 항상 켜짐이다.
/// - 플레이어용 설정이 아니다 (설정 창에 넣지 않는다).
/// </summary>
public static class DevPlaySettings
{
    /// <summary>라인 키를 누를 때 판정선 위 빛기둥(GearView 라인 빛남)을 켜면 true</summary>
    public static bool LaneGlowEnabled = true;

    /// <summary>라인 키를 누를 때(자동 플레이 포함) 키음(NoteSFX)을 내면 true</summary>
    public static bool KeySoundEnabled = true;

    /// <summary>
    /// 게임(플레이 모드)을 시작할 때마다 기본값으로 되돌린다.
    /// 이 프로젝트는 플레이를 시작할 때 도메인을 다시 불러오지 않으므로(Enter Play Mode Options), 이게 없으면 지난 플레이의 값이 남는다.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetToDefaults()
    {
        LaneGlowEnabled = true;
        KeySoundEnabled = true;
    }
}
