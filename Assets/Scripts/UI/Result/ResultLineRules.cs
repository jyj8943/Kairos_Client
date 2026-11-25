/// <summary>
/// 결과 화면 카이로스 대화창의 랭크별 멘트를 고르는 규칙 (11/25). 문구는 GameConstants.ResultLines.
/// AP / S ~ A / B ~ D / F 네 가지. FULL COMBO(AP 아님)는 따로 없고 랭크 멘트 그대로다.
/// </summary>
public static class ResultLineRules
{
    /// <summary>랭크에 맞는 멘트</summary>
    public static string Pick(PlayRank rank)
    {
        switch (rank)
        {
            case PlayRank.AP: return GameConstants.ResultLines.AllPerfect;
            case PlayRank.S:
            case PlayRank.A: return GameConstants.ResultLines.High;
            case PlayRank.B:
            case PlayRank.C:
            case PlayRank.D: return GameConstants.ResultLines.Middle;
            default: return GameConstants.ResultLines.Failed;
        }
    }
}
