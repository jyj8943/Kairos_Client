/// <summary>
/// 점수·콤보·정확도·랭크 계산 (기획서 2-1). MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 점수 = 1,000,000 × (판정 비율 합) ÷ 전체 노트 수. 정수로 계산하고 반올림은 마지막에 한 번만 해서
///   전부 PERFECT면 노트 수와 상관없이 정확히 1,000,000이 된다.
/// - 콤보: PERFECT·GREAT·GOOD이면 +1, MISS면 0. 최대 콤보를 기록한다.
/// - 롱노트 (기획서 2-1 4, 롱노트 판정 세부 규칙 2)
///   - 판정 1개로 센다: 전체 판정 수 = 숏노트 + 롱노트. 점수·판정 개수에는 종합 판정(AddLongFinal)만 넣는다.
///   - 시작 PERFECT·GREAT·GOOD은 콤보만 +1 (AddLongStart), 유지 틱도 콤보만 +1 (AddTick).
///   - 종합 판정이 MISS면 그 순간 콤보 0, MISS가 아니면 콤보는 그대로 (시작에서 이미 +1 했다).
///   - 끝까지 잘 누르면 최대 콤보 = 노트 수 + 전체 틱 수 (MaxPossibleCombo).
/// - FAST / SLOW 개수: Apply로 넣은 결과 중 FastSlowRules.GetCount가 방향을 돌려준 것(숏노트·롱노트 종합의 GREAT·GOOD)만 센다
///   → Apply만 쓰면 항상 FastCount + SlowCount = GreatCount + GoodCount.
/// - 정확도: 결과 화면용 Accuracy(전체 노트 수로 나눔)와 플레이 중 표시용 LiveAccuracy(지금까지 판정한 수로 나눔)를 따로 둔다.
/// </summary>
public class ScoreCalculator
{
    public int TotalNotes { get; }
    public int PerfectCount { get; private set; }
    public int GreatCount { get; private set; }
    public int GoodCount { get; private set; }
    public int MissCount { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }

    /// <summary>GREAT · GOOD 중 일찍 친(뗀) 개수 (기획서 3-4 판정 패널)</summary>
    public int FastCount { get; private set; }

    /// <summary>GREAT · GOOD 중 늦게 친(뗀) 개수 (끝을 지나 계속 눌러 자동 GOOD 포함)</summary>
    public int SlowCount { get; private set; }

    /// <summary>전체 유지 틱 수</summary>
    public int TotalTicks { get; }

    /// <summary>끝까지 잘 쳤을 때의 최대 콤보 = 노트 수 + 전체 틱 수 (확인용)</summary>
    public int MaxPossibleCombo => TotalNotes + TotalTicks;

    /// <summary>지금까지 판정한 노트 수</summary>
    public int JudgedCount => PerfectCount + GreatCount + GoodCount + MissCount;

    /// <param name="totalNotes">전체 판정 수 (숏노트 + 롱노트)</param>
    /// <param name="totalTicks">전체 유지 틱 수 (콤보에만 쓴다)</param>
    public ScoreCalculator(int totalNotes, int totalTicks = 0)
    {
        TotalNotes = totalNotes < 0 ? 0 : totalNotes;
        TotalTicks = totalTicks < 0 ? 0 : totalTicks;
    }

    /// <summary>
    /// 숏노트 판정 하나를 더한다 (점수·판정 개수, 콤보 +1 또는 MISS면 0).
    /// </summary>
    public void Add(JudgeType judge)
    {
        CountJudge(judge);
        if (judge == JudgeType.Miss) Combo = 0;
        else AddCombo();
    }

    /// <summary>
    /// 롱노트 시작 판정 (PERFECT·GREAT·GOOD): 콤보만 +1. 점수·판정 개수는 종합 판정 때 센다.
    /// 시작 MISS는 이것을 부르지 않고 바로 AddLongFinal(Miss)를 부른다.
    /// </summary>
    public void AddLongStart()
    {
        AddCombo();
    }

    /// <summary>롱노트 유지 틱: 콤보만 +1 (판정이 아니다)</summary>
    public void AddTick()
    {
        AddCombo();
    }

    /// <summary>
    /// 롱노트 종합 판정: 점수·판정 개수에 1개로 넣는다. MISS면 콤보 0, 아니면 콤보는 그대로.
    /// </summary>
    public void AddLongFinal(JudgeType judge)
    {
        CountJudge(judge);
        if (judge == JudgeType.Miss) Combo = 0;
    }

    /// <summary>판정 결과의 종류에 맞는 함수를 부르고 FAST / SLOW를 센다 (NoteJudge 결과를 그대로 넣을 때).</summary>
    public void Apply(JudgeResult result)
    {
        switch (result.Kind)
        {
            case JudgeKind.LongStart: AddLongStart(); break;
            case JudgeKind.Tick: AddTick(); break;
            case JudgeKind.LongFinal: AddLongFinal(result.Type); break;
            default: Add(result.Type); break;
        }

        switch (FastSlowRules.GetCount(result))
        {
            case FastSlow.Fast: FastCount++; break;
            case FastSlow.Slow: SlowCount++; break;
        }
    }

    private void AddCombo()
    {
        Combo++;
        if (Combo > MaxCombo) MaxCombo = Combo;
    }

    private void CountJudge(JudgeType judge)
    {
        switch (judge)
        {
            case JudgeType.Perfect: PerfectCount++; break;
            case JudgeType.Great: GreatCount++; break;
            case JudgeType.Good: GoodCount++; break;
            default: MissCount++; break;
        }
    }

    /// <summary>판정 비율 합 (PERFECT×100 + GREAT×70 + GOOD×30)</summary>
    private long WeightedSum =>
        (long)PerfectCount * GameConstants.Play.PerfectRatio
        + (long)GreatCount * GameConstants.Play.GreatRatio
        + (long)GoodCount * GameConstants.Play.GoodRatio
        + (long)MissCount * GameConstants.Play.MissRatio;

    /// <summary>
    /// 점수 (0 ~ 1,000,000). 1,000,000 × 비율 합 ÷ (전체 노트 수 × 100)을 마지막에 한 번 반올림한다 (0.5는 올림).
    /// </summary>
    public int Score
    {
        get
        {
            if (TotalNotes == 0) return 0;

            long denominator = (long)TotalNotes * GameConstants.Play.RatioBase;
            long numerator = WeightedSum * GameConstants.Play.MaxScore;
            return (int)((numerator * 2 + denominator) / (denominator * 2));
        }
    }

    /// <summary>정확도 (%) = (PERFECT×100 + GREAT×70 + GOOD×30) ÷ 전체 노트 수</summary>
    public double Accuracy => TotalNotes == 0 ? 0 : (double)WeightedSum / TotalNotes;

    /// <summary>
    /// 플레이 중 정확도 (%) = 비율 합 ÷ 지금까지 판정한 수 (기획서 3-4: 전체로 나누면 초반에 0%에서 거의 오르지 않는다).
    /// 아직 판정이 없으면 100.
    /// </summary>
    public double LiveAccuracy => JudgedCount == 0 ? 100.0 : (double)WeightedSum / JudgedCount;

    public PlayRank Rank => GetRank(Score);

    /// <summary>MISS가 하나도 없으면 true (노트가 있을 때만, 틱은 판정이 아니라 보지 않는다)</summary>
    public bool IsFullCombo => TotalNotes > 0 && MissCount == 0 && JudgedCount == TotalNotes;

    /// <summary>전부 PERFECT면 true (노트가 있을 때만)</summary>
    public bool IsAllPerfect => TotalNotes > 0 && PerfectCount == TotalNotes;

    /// <summary>
    /// 점수의 랭크. S 950,000 · A 900,000 · B 800,000 · C 700,000 · D 600,000 이상, 그 미만 F.
    /// </summary>
    public static PlayRank GetRank(int score)
    {
        if (score >= GameConstants.Play.RankSScore) return PlayRank.S;
        if (score >= GameConstants.Play.RankAScore) return PlayRank.A;
        if (score >= GameConstants.Play.RankBScore) return PlayRank.B;
        if (score >= GameConstants.Play.RankCScore) return PlayRank.C;
        if (score >= GameConstants.Play.RankDScore) return PlayRank.D;
        return PlayRank.F;
    }
}
