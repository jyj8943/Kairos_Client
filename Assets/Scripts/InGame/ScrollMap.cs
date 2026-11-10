/// <summary>
/// 곡 시간을 "그 시각까지 내려온 스크롤 거리"로 바꾼다. 노트 높이는 두 시각의 스크롤 거리 차이로 구한다 (NoteLayer).
/// - 스크롤 거리 단위는 "1배속으로 흐른 초"다. 화면 거리는 여기에 낙하 속도(기본 낙하 속도 × 플레이어 배속)를 곱한다.
/// - 일반 클래스이고 Unity API를 쓰지 않아 EditMode 테스트로 검사할 수 있다.
/// </summary>
public sealed class ScrollMap
{
    /// <summary>
    /// songTime까지 내려온 스크롤 거리. 지금은 배속 구간이 없어 곡 시간과 같다.
    /// 배속 변속을 넣으면 구간별로 누적한다 (예: 10초부터 2배면 PositionAt(12) = 10 + 2 × 2 = 14).
    /// </summary>
    public double PositionAt(double songTime)
    {
        return songTime;
    }
}
