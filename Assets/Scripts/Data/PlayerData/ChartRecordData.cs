using System;

/// <summary>
/// 채보 하나의 기록 (기획서 2-1(8)). 공식·나만의 채보 모두 chartId 기준으로 같은 방식으로 저장한다.
/// enum(ChartType, PlayRank)은 이름 문자열로 저장해 enum 순서가 바뀌어도 읽을 수 있게 한다.
/// </summary>
[Serializable]
public class ChartRecordData
{
    public string chartId = string.Empty;
    public string songId = string.Empty;

    /// <summary>"Official" / "Custom" (ChartType 이름)</summary>
    public string chartType = ChartType.Official.ToString();

    /// <summary>최고 점수 (0 ~ 1,000,000)</summary>
    public int bestScore;

    /// <summary>최고 점수를 낸 판의 랭크 (PlayRank 이름)</summary>
    public string bestRank = PlayRank.F.ToString();

    /// <summary>최고 정확도 (%, 0 ~ 100)</summary>
    public double bestAccuracy;

    /// <summary>최대 콤보 (모든 판 중 가장 큰 값)</summary>
    public int maxCombo;

    /// <summary>한 번이라도 FULL COMBO / ALL PERFECT를 냈으면 true</summary>
    public bool isFullCombo;
    public bool isAllPerfect;

    /// <summary>끝까지 플레이한 횟수 (결과 씬까지 간 판)</summary>
    public int playCount;

    /// <summary>처음·마지막으로 플레이한 시각 (UTC, yyyy-MM-ddTHH:mm:ssZ)</summary>
    public string firstPlayedAt = string.Empty;
    public string lastPlayedAt = string.Empty;
}
