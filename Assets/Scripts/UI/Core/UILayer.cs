/// <summary>
/// UI 화면의 층. UIManager가 등록·ESC 처리·씬 정리에 쓴다.
/// </summary>
public enum UILayer
{
    /// <summary>씬마다 있는 기본 화면 (Canvas_Title, Canvas_Editor 등)</summary>
    Scene,

    /// <summary>화면 위에 열고 닫는 창 (Popup_Confirm 등). 열리면 팝업 스택에 올라간다</summary>
    Popup,

    /// <summary>모든 것 위에 그리는 표시 (지금은 없음. 로딩 화면 등)</summary>
    Overlay
}
