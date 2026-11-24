/// <summary>
/// 에디터 왼쪽 도구 패널의 도구 (단축키 1 · 2).
/// 값은 메모리 안에서만 넘긴다 (TestPlayData·EditorRestoreState는 파일로 저장하지 않는다). 그래도 숫자를 정해 둔다.
/// </summary>
public enum EditorTool
{
    /// <summary>노트 도구: 클릭 = 숏노트, 끌기 = 롱노트, 오른쪽 클릭 = 지우기 (11/24 숏노트 · 롱노트 도구를 합침)</summary>
    Note = 0,

    /// <summary>BPM 구간 도구: 그리드 왼쪽 클릭 = 추가·수정 창, 구간선 오른쪽 클릭 = 삭제 (노트를 찍지 않는다)</summary>
    BpmSection = 1
}
