/// <summary>
/// 테스트가 쓰는 곡·공식 채보 ID 한 곳 모음 (CLAUDE.md 작업 확인 규칙).
/// - 공식 채보로 테스트할 때는 Critical Velocity EASY만 쓴다. 지금 프로젝트에 있는 공식 채보는 이것 하나뿐이다
///   (Assets/Resources/Data/OfficialCharts/critical_velocity/EASY.json, 노트 413 · 롱노트 84, Lv.3, BPM 150.61).
/// - 공식 채보가 추가되거나 지워지면 이 값과 CLAUDE.md를 같이 고친다. TestChartsTests가 먼저 실패해 알려 준다.
/// - 특정 노트 구성(숏노트만, BPM 여러 구간 등)이 필요하면 공식 채보를 고치지 말고 테스트 안에서 채보를 만든다.
/// </summary>
public static class TestCharts
{
    /// <summary>테스트용 곡 (Critical Velocity). 나만의 채보·테스트 플레이 채보도 이 곡에 만든다</summary>
    public const string OfficialSongId = "critical_velocity";

    /// <summary>테스트용 공식 채보 (Critical Velocity EASY)</summary>
    public const string OfficialChartId = "critical_velocity_easy";
}
