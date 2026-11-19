using System.Collections;

/// <summary>
/// 씬 이동을 덮는 로딩 오버레이 규약 (곡 로딩 Canvas_SongLoading · 일반 로딩 Canvas_Loading).
/// GameSceneManager의 오버레이 로딩 루틴이 이 규약만 보고 흐름을 진행한다:
/// 화면 데이터 채우기(각 오버레이의 Open) → HasOpenStarted(다음 프레임부터 0초) → IsOpening이 끝나면 화면을 다 덮음 →
/// (씬 교체 · Init · 정리 · 최소 시간) → CloseRoutine() → 다음 씬 OnEnter.
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

    /// <summary>로그 이름 ("곡 로딩" / "일반 로딩")</summary>
    string LogName { get; }

    /// <summary>단계 로그 이름: 열기 0초 · 열기 끝</summary>
    string OpenStartStep { get; }
    string OpenEndStep { get; }

    /// <summary>프레임 통계 단계 이름: 열기 중 · 닫기 중</summary>
    string OpeningPhase { get; }
    string ClosingPhase { get; }
}
