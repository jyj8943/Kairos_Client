/// <summary>
/// 한 판의 결과를 기록에 넣은 결과 (PlayerRecordBook.Submit / PlayerDataManager.SubmitResult가 돌려준다).
/// </summary>
public class RecordUpdateResult
{
    /// <summary>기록에 넣었으면 true. 테스트 플레이·자동 플레이·chartId 없음이면 false (skipReason에 이유)</summary>
    public bool isRecorded;

    /// <summary>기록하지 않은 이유 (기록했으면 빈 문자열)</summary>
    public string skipReason = string.Empty;

    /// <summary>신기록: 처음 플레이했거나 bestScore가 올랐으면 true</summary>
    public bool isNewRecord;

    /// <summary>이 판 전에 기록이 있었으면 true (처음 플레이면 false)</summary>
    public bool hadPreviousRecord;

    /// <summary>이 판 전의 최고 점수 (기록이 없었으면 0)</summary>
    public int previousBestScore;

    /// <summary>이 판을 넣은 뒤의 최고 점수·플레이 횟수</summary>
    public int bestScore;
    public int playCount;
}
