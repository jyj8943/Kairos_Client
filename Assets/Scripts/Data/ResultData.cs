/// <summary>
/// 결과 화면에서 버튼으로 돌아갈 곳. 인게임이 정해서 넘기고, 결과 씬은 이 값대로 이동만 한다
/// (결과 씬이 에디터·곡 선택 씬을 직접 알지 않게).
/// </summary>
public class ResultReturnTarget
{
    /// <summary>이동할 씬 이름 (GameConstants.Scenes)</summary>
    public string sceneName;

    /// <summary>그 씬에 넘길 데이터 (없으면 null)</summary>
    public object sceneData;

    /// <summary>돌아가기 버튼 문구</summary>
    public string buttonLabel;
}

/// <summary>
/// 인게임 → 결과 씬으로 넘기는 한 판의 결과 (기획서 3-5).
/// </summary>
public class ResultData
{
    public int score;
    public PlayRank rank;
    public int perfectCount;
    public int greatCount;
    public int goodCount;
    public int missCount;
    public int maxCombo;
    public int totalNotes;

    /// <summary>끝까지 잘 쳤을 때의 최대 콤보 = 노트 수 + 롱노트 전체 유지 틱 수</summary>
    public int maxPossibleCombo;

    /// <summary>정확도 (%)</summary>
    public double accuracy;

    public bool isFullCombo;
    public bool isAllPerfect;

    public string songTitle;
    public string chartName;
    public int level;

    public bool isTestPlay;
    /// <summary>자동 플레이로 친 결과면 true (자동 테스트)</summary>
    public bool isAutoPlay;

    /// <summary>다시 하기 때 인게임에 넘길 데이터</summary>
    public InGameEntryData retryEntry;

    /// <summary>돌아가기 버튼으로 갈 곳</summary>
    public ResultReturnTarget returnTarget;

    /// <summary>
    /// 기록 갱신 결과 (결과 씬이 들어올 때 PlayerDataManager.SubmitResult로 채운다).
    /// 신기록 = 처음 플레이했거나 최고 점수가 올랐을 때. 기록하지 않는 판(테스트·자동 플레이)이면 false.
    /// </summary>
    public bool isNewRecord;

    /// <summary>이 판 전에 기록이 있었으면 true, 그때의 최고 점수 (없었으면 0)</summary>
    public bool hadPreviousRecord;
    public int previousBestScore;

    /// <summary>
    /// 점수 계산 결과를 옮겨 담는다.
    /// </summary>
    public static ResultData FromScore(ScoreCalculator score)
    {
        return new ResultData
        {
            score = score.Score,
            rank = score.Rank,
            perfectCount = score.PerfectCount,
            greatCount = score.GreatCount,
            goodCount = score.GoodCount,
            missCount = score.MissCount,
            maxCombo = score.MaxCombo,
            totalNotes = score.TotalNotes,
            maxPossibleCombo = score.MaxPossibleCombo,
            accuracy = score.Accuracy,
            isFullCombo = score.IsFullCombo,
            isAllPerfect = score.IsAllPerfect
        };
    }
}
