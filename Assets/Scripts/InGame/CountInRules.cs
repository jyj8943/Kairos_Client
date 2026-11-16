/// <summary>
/// 인게임 시작 메트로놈 계산 (기획서 3-4 「시작 메트로놈」). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 채보를 첫 마디(0박)부터 플레이할 때만 쓴다 (UsesCountIn): 일반 플레이는 항상, 테스트 플레이는 시작 박이 0박일 때만 (tick으로 비교).
/// - 판은 −1마디부터: 곡 시간이 (−4박 시각 − CountInMarginSeconds)부터 흐른다. 노래 파일은 지금처럼 곡 시간 0초에 시작한다.
/// - 클릭은 −4, −3, −2, −1박 (CountInBeats개). 그다음 0박에 노래 첫 박이 온다.
/// - 박 시각은 TimingMap.BeatToSeconds로 구한다. 음수 박은 첫 BPM으로 이어서 계산한다 (0박 근처에 BPM 구간이 있어도 영향 없음).
///   판정 오프셋(사용자 설정)이 들어간 TimingMap을 넘기지 말고, 곡 offset만으로 만든 TimingMap을 넘긴다 (클릭은 노래에 맞춘다).
/// - 이 계산이 지금 시작 지연보다 짧으면 지금 시작 지연을 그대로 쓴다 (준비 시간이 줄지 않는다).
/// </summary>
public static class CountInRules
{
    /// <summary>
    /// 시작 메트로놈을 쓰는지. 일반 플레이(테스트 플레이 데이터 없음)는 항상, 테스트 플레이는 시작 박이 0박일 때만.
    /// </summary>
    public static bool UsesCountIn(InGameEntryData entry)
    {
        if (entry == null || entry.testPlay == null) return true;
        return UsesCountIn(true, entry.testPlay.startBeat);
    }

    /// <summary>
    /// 시작 메트로놈을 쓰는지 (테스트 플레이 여부와 시작 박). 시작 박은 tick으로 바꿔 0인지 본다 (부동소수 오차 없음).
    /// </summary>
    public static bool UsesCountIn(bool isTestPlay, float startBeat)
    {
        return !isTestPlay || BeatGridMath.BeatToTick(startBeat) == 0;
    }

    /// <summary>
    /// 클릭 곡 시간 목록 (초, 오름차순): −CountInBeats박 ~ −1박.
    /// </summary>
    /// <param name="songTiming">곡 offset만으로 만든 박 ↔ 시간 변환 (판정 오프셋 제외)</param>
    public static double[] GetClickTimes(TimingMap songTiming)
    {
        int count = GameConstants.Play.CountInBeats;
        double[] times = new double[count];
        for (int i = 0; i < count; i++) times[i] = songTiming.BeatToSeconds(i - count);
        return times;
    }

    /// <summary>
    /// 판 시작 곡 시간 = 첫 클릭(−CountInBeats박) 시각 − CountInMarginSeconds.
    /// </summary>
    public static double GetInitialSongTime(TimingMap songTiming)
    {
        return songTiming.BeatToSeconds(-GameConstants.Play.CountInBeats) - GameConstants.Play.CountInMarginSeconds;
    }

    /// <summary>
    /// 시작 메트로놈을 쓸 때의 시작 지연 (노래는 음원 0초부터): max(지금 시작 지연, −판 시작 곡 시간).
    /// </summary>
    /// <param name="baseDelaySeconds">메트로놈이 없을 때의 시작 지연 (GetNormalStartDelay 또는 테스트 플레이 지연)</param>
    public static double GetStartDelay(TimingMap songTiming, double baseDelaySeconds)
    {
        return System.Math.Max(baseDelaySeconds, -GetInitialSongTime(songTiming));
    }

    /// <summary>
    /// 메트로놈이 없을 때 일반 플레이의 시작 지연 (예전 규칙): max(MinStartDelaySeconds, LeadInSeconds − 첫 노트 시각).
    /// </summary>
    public static double GetNormalStartDelay(double firstNoteTimeSeconds)
    {
        return System.Math.Max(GameConstants.Play.MinStartDelaySeconds, GameConstants.Play.LeadInSeconds - firstNoteTimeSeconds);
    }

    /// <summary>
    /// 테스트 플레이(0박 시작)의 준비 시간: max(TestPlayLeadInSeconds, 시작 시각 − 판 시작 곡 시간).
    /// TestPlayRules.GetMusicStart(시작 시각, 이 값)으로 노래 시작 위치·지연을 정한다.
    /// </summary>
    /// <param name="startTimeSeconds">시작 박(0박)의 시각 (판정 오프셋이 들어간 시각)</param>
    public static double GetTestPlayLeadIn(TimingMap songTiming, double startTimeSeconds)
    {
        return System.Math.Max(GameConstants.Play.TestPlayLeadInSeconds, startTimeSeconds - GetInitialSongTime(songTiming));
    }
}
