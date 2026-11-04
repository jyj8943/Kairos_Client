/// <summary>
/// 판정 종류 (기획서 2-1, 4단계).
/// </summary>
public enum JudgeType
{
    Perfect,
    Great,
    Good,
    Miss
}

/// <summary>
/// 랭크 (기획서 2-1). 점수 기준은 GameConstants.Play.
/// </summary>
public enum PlayRank
{
    S,
    A,
    B,
    C,
    D,
    F
}

/// <summary>
/// 인게임 시작 방식.
/// </summary>
public enum InGameMode
{
    /// <summary>곡 선택에서 시작한 보통 플레이</summary>
    Normal,

    /// <summary>에디터 테스트 플레이 (TODO: 아직 Normal과 같게 동작한다)</summary>
    TestPlay
}
