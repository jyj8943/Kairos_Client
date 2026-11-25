/// <summary>
/// 같은 라인 노트끼리 겹치는지 정하는 규칙 (DJMAX · PLATiNA::LAB 방식, 11/25). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// 에디터 편집(ChartEditData), 채보 불러오기 정리, 저장 · 불러오기 검사(ChartRepository)가 모두 이 규칙을 쓴다.
/// - 숏노트는 한 점(시작 = 끝)이다. 같은 tick에는 다른 노트가 시작할 수 없다.
/// - 롱노트는 [시작, 끝) 구간을 차지한다. 끝 tick은 비어 있어 같은 라인의 다음 노트(숏노트 · 롱노트)가 끝 tick에서 시작할 수 있다 (떼고 바로 누르기).
/// - 라인은 보지 않는다 (같은 라인 노트끼리만 부른다). 롱노트 최소 길이는 GameConstants.Editor.MinLongNoteTicks로 따로 검사한다.
/// </summary>
public static class NoteOverlapRules
{
    /// <summary>
    /// 같은 라인의 노트 a와 b가 겹치면 true. 숏노트(또는 tick 하나)는 끝 tick에 시작 tick을 그대로 넘긴다.
    /// 시작이 같으면 항상 겹치고, 아니면 뒤 노트의 시작이 앞 노트의 끝보다 앞일 때 겹친다.
    /// </summary>
    public static bool Overlaps(int startA, int endA, int startB, int endB)
    {
        if (startA == startB) return true;
        return startA < startB ? startB < endA : startA < endB;
    }
}
