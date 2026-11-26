using System.Collections;

/// <summary>
/// 씬 이동을 덮는 로딩 오버레이 규약 (곡 로딩 Canvas_SongLoading · 일반 로딩 Canvas_Loading · 암전 로딩 Canvas_FadeLoading).
/// GameSceneManager의 오버레이 로딩 루틴이 이 규약만 보고 흐름을 진행한다:
/// 화면 데이터 채우기(각 오버레이의 Open) → HasOpenStarted(다음 프레임부터 0초) → IsOpening이 끝나면 화면을 다 덮음 →
/// (씬 교체 · Init · 정리 · 최소 시간) → CloseRoutine() → 다음 씬 OnEnter.
/// 곡 로딩(Canvas_SongLoading)은 최소 시간 대신 로딩 진행 · 바늘 12시 · 암전 중 정리 · 걷힘으로 이어 간다 (GameSceneManager).
/// </summary>
public interface ILoadingOverlay
{
    /// <summary>보이는 중이면 true (Open ~ 닫기 끝)</summary>
    bool IsShown { get; }

    /// <summary>열기(들어오기) 연출 0초를 센 뒤면 true (Open한 다음 프레임부터)</summary>
    bool HasOpenStarted { get; }

    /// <summary>열기(들어오기) 연출 중이면 true. 끝나면 화면을 다 덮은 상태</summary>
    bool IsOpening { get; }

    /// <summary>닫기(나가기) 연출 중이면 true</summary>
    bool IsClosing { get; }

    /// <summary>열기 연출 0초부터 지난 시간 (초, unscaled)</summary>
    float OpenElapsed { get; }

    /// <summary>닫기(나가기) 연출을 하고 숨긴다. 끝날 때까지 기다릴 수 있다</summary>
    IEnumerator CloseRoutine();

    /// <summary>최소 표시 시간 (초). 불러오기가 빨리 끝나도 닫기는 이만큼 지난 뒤에 시작한다</summary>
    double MinSeconds { get; }

    /// <summary>최소 시간을 열기 끝부터 세면 true (암전 로딩), 열기 0초부터 세면 false (일반 로딩). 곡 로딩은 최소 시간 대신 바늘 · 로딩 진행을 기다린다</summary>
    bool MinFromOpenEnd { get; }

    /// <summary>로그 이름 ("곡 로딩" / "일반 로딩" / "암전 로딩")</summary>
    string LogName { get; }

    /// <summary>단계 로그 이름: 열기 0초 · 열기 끝</summary>
    string OpenStartStep { get; }
    string OpenEndStep { get; }

    /// <summary>프레임 통계 단계 이름: 열기 중 · 닫기 중</summary>
    string OpeningPhase { get; }
    string ClosingPhase { get; }
}
