using System;
using System.Collections.Generic;

/// <summary>
/// 채보 기록 목록과 chartId → 기록 찾기 (순수 C#, 테스트에서 바로 쓴다). PlayerDataManager가 하나 들고 있다.
/// - 기록 목록(List)은 PlayerData.records 그 자체를 쓰고, 찾기용 Dictionary는 메모리에만 둔다.
/// - 기록 규칙 (기획서 2-1(8), 공식·나만의 채보 같은 방식):
///   bestScore·bestAccuracy·maxCombo는 각각 따로 최고값, bestRank는 bestScore가 바뀔 때 그 판의 랭크,
///   isFullCombo·isAllPerfect는 한 번이라도 true면 true, playCount +1, 처음이면 firstPlayedAt, lastPlayedAt 갱신.
///   신기록 = 처음 플레이했거나 bestScore가 올랐을 때.
/// - 기록하지 않는 판: 테스트 플레이, 자동 플레이, chartId가 비어 있는 경우.
/// </summary>
public class PlayerRecordBook
{
    private readonly List<ChartRecordData> records;
    private readonly Dictionary<string, ChartRecordData> byChartId = new Dictionary<string, ChartRecordData>();

    /// <summary>기록 목록 (저장할 때 이 목록을 그대로 쓴다)</summary>
    public List<ChartRecordData> Records => records;

    /// <param name="records">정리된(PlayerDataRules.MergeRecords) 기록 목록. 이 목록을 직접 고친다</param>
    public PlayerRecordBook(List<ChartRecordData> records)
    {
        this.records = records ?? new List<ChartRecordData>();
        foreach (ChartRecordData record in this.records)
        {
            if (record != null && !string.IsNullOrEmpty(record.chartId)) byChartId[record.chartId] = record;
        }
    }

    /// <summary>chartId의 기록. 없으면 null</summary>
    public ChartRecordData Find(string chartId)
    {
        if (string.IsNullOrEmpty(chartId)) return null;
        return byChartId.TryGetValue(chartId, out ChartRecordData record) ? record : null;
    }

    /// <summary>chartId의 기록을 지운다 (나만의 채보를 수정해 저장했을 때). 지웠으면 true</summary>
    public bool Remove(string chartId)
    {
        ChartRecordData record = Find(chartId);
        if (record == null) return false;

        byChartId.Remove(chartId);
        records.Remove(record);
        return true;
    }

    /// <summary>기록하지 않는 판이면 이유, 기록하는 판이면 null</summary>
    public static string GetSkipReason(InGameEntryData entry, ResultData result)
    {
        if (entry == null || result == null) return "진입 데이터·결과 없음";
        if (result.isTestPlay || entry.testPlay != null || entry.mode == InGameMode.TestPlay) return "테스트 플레이";
        if (result.isAutoPlay) return "자동 플레이";
        if (string.IsNullOrEmpty(entry.chartId)) return "chartId 없음";
        return null;
    }

    /// <summary>
    /// 한 판의 결과를 기록에 넣는다. 기록하지 않는 판이면 아무것도 바꾸지 않고 isRecorded=false를 돌려준다.
    /// </summary>
    /// <param name="nowUtc">이 판을 마친 시각 (UTC)</param>
    public RecordUpdateResult Submit(InGameEntryData entry, ResultData result, DateTime nowUtc)
    {
        RecordUpdateResult update = new RecordUpdateResult();
        string skip = GetSkipReason(entry, result);
        if (skip != null)
        {
            update.skipReason = skip;
            return update;
        }

        string now = PlayerDataRules.FormatTime(nowUtc);
        ChartRecordData record = Find(entry.chartId);
        update.hadPreviousRecord = record != null;
        update.previousBestScore = record != null ? record.bestScore : 0;

        if (record == null)
        {
            record = new ChartRecordData
            {
                chartId = entry.chartId,
                songId = entry.songId ?? string.Empty,
                chartType = entry.chartType.ToString(),
                bestScore = result.score,
                bestRank = result.rank.ToString(),
                bestAccuracy = result.accuracy,
                maxCombo = result.maxCombo,
                firstPlayedAt = now
            };
            records.Add(record);
            byChartId.Add(record.chartId, record);
            update.isNewRecord = true;
        }
        else
        {
            if (result.score > record.bestScore)
            {
                record.bestScore = result.score;
                record.bestRank = result.rank.ToString();
                update.isNewRecord = true;
            }
            record.bestAccuracy = Math.Max(record.bestAccuracy, result.accuracy);
            record.maxCombo = Math.Max(record.maxCombo, result.maxCombo);
            if (string.IsNullOrEmpty(record.firstPlayedAt)) record.firstPlayedAt = now;
            if (!string.IsNullOrEmpty(entry.songId)) record.songId = entry.songId;
            record.chartType = entry.chartType.ToString();
        }

        record.isFullCombo |= result.isFullCombo;
        record.isAllPerfect |= result.isAllPerfect;
        record.playCount++;
        record.lastPlayedAt = now;

        update.isRecorded = true;
        update.bestScore = record.bestScore;
        update.playCount = record.playCount;
        return update;
    }
}
