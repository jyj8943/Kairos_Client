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
/// 랭크 (기획서 2-1 5), 11/18 AP 추가). 정하는 규칙은 RankRules.GetRank 한 곳, 점수 기준은 GameConstants.Play.
/// - AP는 모든 판정이 PERFECT일 때 (점수가 아니라 판정 수로 본다).
/// - 기록 JSON·씬에는 숫자가 아니라 이름 문자열("AP", "S" …)로 저장한다. 순서를 바꿔도 저장 값이 밀리지 않는다.
/// </summary>
public enum PlayRank
{
    AP,
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
