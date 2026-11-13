/// <summary>
/// 에디터로 돌아올 때 되돌릴 편집 상태 (테스트 플레이 전 상태).
/// 에디터 → 인게임 → (결과) → 에디터로 씬 데이터에 실어 보낸다. 싱글톤에 남겨 두지 않는다.
/// </summary>
public class EditorRestoreState
{
    /// <summary>편집 중이던 채보 (노트·채보 이름·레벨·chartId·BPM). 저장하지 않았어도 메모리 그대로다</summary>
    public ChartData chart;

    /// <summary>테스트 플레이 전 수정 여부</summary>
    public bool isDirty;

    /// <summary>테스트 플레이 전 실행 취소 · 다시 실행 기록 (저장 시점 포함). 돌아오면 그대로 이어 쓴다</summary>
    public EditHistory history;

    /// <summary>현재 위치 선의 박</summary>
    public float currentBeat;

    /// <summary>박자 스냅 n (1/n)</summary>
    public int snapDivision;

    /// <summary>그리드 높이 배율</summary>
    public float gridHeightScale;

    /// <summary>왼쪽 도구</summary>
    public EditorTool tool;

    /// <summary>도구 패널 "테스트 플레이 배속" (테스트 플레이 중 1 / 2로 바꿨으면 그 값)</summary>
    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;
}
