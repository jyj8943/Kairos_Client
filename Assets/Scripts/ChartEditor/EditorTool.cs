/// <summary>
/// 에디터 왼쪽 도구 패널의 도구 (단축키 1 · 2 · 3).
/// 값은 메모리 안에서만 넘긴다 (TestPlayData·EditorRestoreState는 파일로 저장하지 않는다). 그래도 숫자를 정해 둔다.
/// </summary>
public enum EditorTool
{
    ShortNote = 0,
    LongNote = 1,

    /// <summary>BPM 구간 도구: 그리드 왼쪽 클릭 = 추가·수정 창, 구간선 오른쪽 클릭 = 삭제 (노트를 찍지 않는다)</summary>
    BpmSection = 2
}
