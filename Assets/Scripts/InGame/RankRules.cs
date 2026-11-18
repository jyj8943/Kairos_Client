/// <summary>
/// 랭크 규칙 한 곳 (기획서 2-1 5) 랭크 표, 11/18). 인게임(곡 끝 랭크)·결과·기록 저장이 모두 이 함수로 정한 랭크를 쓴다.
/// - AP: 모든 판정이 PERFECT (PERFECT 수 = 전체 판정 수, 판정이 1개 이상). 점수가 아니라 판정 수로 본다.
/// - S 950000 이상 · A 900000 · B 800000 · C 700000 · D 600000 이상, 그 미만 F (GameConstants.Play).
///   점수는 내림이라 AP가 아니면 1000000이 될 수 없다 (ScoreCalculator.Score). 그래도 AP가 아닌 1000000이 들어오면 S로 둔다.
/// </summary>
public static class RankRules
{
    /// <summary>
    /// 랭크를 정한다.
    /// </summary>
    /// <param name="perfectCount">PERFECT 수</param>
    /// <param name="totalJudged">전체 판정 수 (숏노트 + 롱노트 종합 판정)</param>
    /// <param name="score">점수 (0 ~ 1,000,000)</param>
    public static PlayRank GetRank(int perfectCount, int totalJudged, int score)
    {
        if (IsAllPerfect(perfectCount, totalJudged)) return PlayRank.AP;
        if (score >= GameConstants.Play.RankSScore) return PlayRank.S;
        if (score >= GameConstants.Play.RankAScore) return PlayRank.A;
        if (score >= GameConstants.Play.RankBScore) return PlayRank.B;
        if (score >= GameConstants.Play.RankCScore) return PlayRank.C;
        if (score >= GameConstants.Play.RankDScore) return PlayRank.D;
        return PlayRank.F;
    }

    /// <summary>모든 판정이 PERFECT면 true (판정이 1개 이상일 때만)</summary>
    public static bool IsAllPerfect(int perfectCount, int totalJudged)
    {
        return totalJudged > 0 && perfectCount == totalJudged;
    }
}
