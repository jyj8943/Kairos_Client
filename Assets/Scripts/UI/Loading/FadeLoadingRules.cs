/// <summary>
/// 암전 로딩 (기획서 3-10 「암전 로딩」, Canvas_FadeLoading) 시간 계산. 상태가 없는 순수 함수라 EditMode 테스트로 검사한다.
/// - 열기 0.5초: 덮개 알파 = t² (ease-in), 곡 음악 계수 = 1 − t² (같은 곡선으로 지금 값 → 0). t = 열기 경과 ÷ 0.5.
/// - 닫기 0.45초: 덮개 알파 = 1 − (1 − (1 − t)²) (ease-out으로 1 → 0). t = 닫기 경과 ÷ 0.45.
/// </summary>
public static class FadeLoadingRules
{
    /// <summary>열기 경과(초)의 덮개 알파 (0 → 1, t²)</summary>
    public static float OpenAlpha(float elapsed)
    {
        return UIEasing.InQuad(UIEasing.Progress(elapsed, 0f, GameConstants.FadeLoading.OpenSeconds));
    }

    /// <summary>열기 경과(초)의 곡 음악 계수 (1 → 0, 1 − t²)</summary>
    public static float OpenMusicFactor(float elapsed)
    {
        return 1f - OpenAlpha(elapsed);
    }

    /// <summary>닫기 경과(초)의 덮개 알파 (1 → 0, ease-out 1 − (1 − t)²로 걷힘)</summary>
    public static float CloseAlpha(float elapsed)
    {
        return 1f - UIEasing.OutQuad(UIEasing.Progress(elapsed, 0f, GameConstants.FadeLoading.CloseSeconds));
    }
}
