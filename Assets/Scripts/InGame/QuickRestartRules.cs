/// <summary>
/// 인게임 F5 즉시 다시 하기(Gameplay/Restart)를 받을지 정하는 규칙 (기획서 3-9). Unity API를 쓰지 않아 EditMode 테스트로 검사한다.
/// - 받는 때: 플레이 중, 재개 카운트다운 중, 모든 노트 판정 뒤 결과로 가기 전 대기 중, 일시정지 메뉴(Popup_Pause)가 맨 위 팝업일 때.
/// - 받지 않는 때: 시작 전(채보 로딩 실패 포함), 씬 로딩 중, 결과·다른 씬으로 떠나는 중, 확인 팝업·설정 창이 맨 위일 때.
/// </summary>
public static class QuickRestartRules
{
    /// <param name="isReady">판 준비가 끝났다 (채보를 불러왔다)</param>
    /// <param name="isPlaying">OnEnter가 지나 플레이를 시작했다 (일시정지·카운트다운·결과 대기 중에도 true)</param>
    /// <param name="isLeaving">결과로 가거나 재시작·나가기로 씬을 떠나는 중이다</param>
    /// <param name="isSceneLoading">씬 로딩 중이다</param>
    /// <param name="hasOpenPopup">열린 팝업이 있다</param>
    /// <param name="isPauseTopPopup">맨 위 팝업이 일시정지 메뉴다</param>
    public static bool CanQuickRestart(bool isReady, bool isPlaying, bool isLeaving, bool isSceneLoading, bool hasOpenPopup, bool isPauseTopPopup)
    {
        if (!isReady || !isPlaying || isLeaving || isSceneLoading) return false;
        return !hasOpenPopup || isPauseTopPopup;
    }
}
