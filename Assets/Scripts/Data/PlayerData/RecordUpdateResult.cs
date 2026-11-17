/// <summary>
/// 한 판의 결과를 기록에 넣은 결과 (PlayerRecordBook.Submit / PlayerDataManager.SubmitResult가 돌려준다).
/// </summary>
public class RecordUpdateResult
{
    /// <summary>기록에 넣었으면 true. 테스트 플레이·자동 플레이·chartId 없음이면 false (skipReason에 이유)</summary>
    public bool isRecorded;

    /// <summary>기록하지 않은 이유 (기록했으면 빈 문자열)</summary>
    public string skipReason = string.Empty;

    /// <summary>신기록: 처음 플레이했거나 bestScore가 올랐으면 true (= isNewBestScore)</summary>
    public bool isNewRecord;

    /// <summary>
    /// 항목별 신기록 (기획서 3-5 NEW RECORD, 11/17): 점수 · 정확도 · 최대 콤보를 각각 이전 최고와 비교해 더 높을 때만 true.
    /// 처음 플레이(이전 기록 없음)는 셋 다 true. 기록하지 않는 판(테스트·자동 플레이)은 모두 false.
    /// </summary>
    public bool isNewBestScore;
    public bool isNewBestAccuracy;
    public bool isNewMaxCombo;

    /// <summary>이 판 전에 기록이 있었으면 true (처음 플레이면 false)</summary>
    public bool hadPreviousRecord;

    /// <summary>이 판 전의 최고 점수·최고 정확도·최대 콤보 (기록이 없었으면 0)</summary>
    public int previousBestScore;
    public double previousBestAccuracy;
    public int previousMaxCombo;

    /// <summary>이 판을 넣은 뒤의 최고 점수·플레이 횟수</summary>
    public int bestScore;
    public int playCount;
}
