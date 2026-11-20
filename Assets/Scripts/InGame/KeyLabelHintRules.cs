/// <summary>
/// 시작할 때 키 칸 위 키 글자 안내의 알파 (11/20, 기획서 3-4 「노트 · 판정 이펙트」 키 칸). 상태가 없는 순수 함수라 EditMode 테스트로 검사한다.
/// - 메트로놈이 있을 때(CountInRules.UsesCountIn): −4박부터 보이고, −1박 ~ 0박의 1박 동안 1 → 0, 0박부터는 없다.
///   박 시각은 카운트인 클릭과 같은 곡 시간(곡 offset만으로 만든 TimingMap)이다. −4박 전(판 시작 여유 0.3초)에는 보이지 않는다.
/// - 메트로놈이 없을 때(중간부터 테스트 플레이): 판 시작 후 1.5초 보이고, 마지막 0.5초 동안 1 → 0.
/// - 다시 하기(F5 · 일시정지 재시작 · 결과의 다시 하기)로 들어온 판이면 보여 주지 않는다 (InGameEntryData.isRetry).
///   일시정지 후 이어 하기는 판 시작이 아니므로 이 규칙을 다시 시작하지 않는다 (곡 시간이 그대로 이어진다).
/// </summary>
public static class KeyLabelHintRules
{
    /// <summary>
    /// 지금 키 글자 알파 (0 ~ 1).
    /// </summary>
    /// <param name="usesCountIn">시작 메트로놈이 있는지</param>
    /// <param name="isRetry">다시 하기로 들어온 판인지</param>
    /// <param name="songTime">지금 곡 시간 (초, 메트로놈이 있을 때 쓴다)</param>
    /// <param name="showStartTime">−4박 시각 (곡 시간)</param>
    /// <param name="fadeStartTime">−1박 시각</param>
    /// <param name="fadeEndTime">0박 시각</param>
    /// <param name="elapsedSeconds">판 시작 후 지난 곡 시간 (초, 메트로놈이 없을 때 쓴다)</param>
    public static float GetAlpha(bool usesCountIn, bool isRetry, double songTime, double showStartTime, double fadeStartTime, double fadeEndTime, double elapsedSeconds)
    {
        if (isRetry) return 0f;
        return usesCountIn
            ? WithCountIn(songTime, showStartTime, fadeStartTime, fadeEndTime)
            : WithoutCountIn(elapsedSeconds);
    }

    /// <summary>메트로놈이 있을 때: [−4박, −1박) 1, [−1박, 0박) 1 → 0 (직선), 그 밖 0</summary>
    public static float WithCountIn(double songTime, double showStartTime, double fadeStartTime, double fadeEndTime)
    {
        if (songTime < showStartTime || songTime >= fadeEndTime) return 0f;
        if (songTime < fadeStartTime) return 1f;
        double length = fadeEndTime - fadeStartTime;
        if (length <= 0) return 0f;
        return (float)(1.0 - (songTime - fadeStartTime) / length);
    }

    /// <summary>메트로놈이 없을 때: [0, 1.0초) 1, [1.0, 1.5초) 1 → 0 (직선), 1.5초부터 0</summary>
    public static float WithoutCountIn(double elapsedSeconds)
    {
        double show = GameConstants.Play.KeyLabelHintSeconds;
        double fade = GameConstants.Play.KeyLabelHintFadeSeconds;
        if (elapsedSeconds < 0 || elapsedSeconds >= show) return 0f;
        double fadeStart = show - fade;
        if (elapsedSeconds < fadeStart) return 1f;
        return (float)(1.0 - (elapsedSeconds - fadeStart) / fade);
    }

    /// <summary>안내가 끝났는지 (더 계산하지 않아도 되는지)</summary>
    public static bool IsOver(bool usesCountIn, bool isRetry, double songTime, double fadeEndTime, double elapsedSeconds)
    {
        if (isRetry) return true;
        return usesCountIn ? songTime >= fadeEndTime : elapsedSeconds >= GameConstants.Play.KeyLabelHintSeconds;
    }
}
