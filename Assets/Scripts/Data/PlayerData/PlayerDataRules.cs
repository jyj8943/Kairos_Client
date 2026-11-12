using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 불러온 PlayerData의 값 정리 규칙 (순수 함수, 테스트에서 바로 쓴다).
/// - 음량 0~100, 오프셋 -200~+200·5ms 단위, 해상도·창 모드는 GameConstants.Settings 목록에 없으면 기본값.
/// - 기록: chartId가 비어 있으면 버리고, 같은 chartId가 여러 개면 하나로 합친다 (항목별 최고값, playCount 합).
/// - enum 문자열(창 모드·채보 종류·랭크)은 모르는 값이면 기본값으로 바꾼다.
/// - 형식이 바뀌면(version) 여기서 변환한다. 지금은 version 1뿐이다.
/// </summary>
public static class PlayerDataRules
{
    /// <summary>지금 시각을 저장 형식(UTC ISO 8601)으로</summary>
    public static string FormatTime(DateTime utc)
    {
        return utc.ToUniversalTime().ToString(GameConstants.PlayerData.TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 값을 정리한다 (null 묶음 채우기, 범위·목록 맞추기, 같은 chartId 기록 합치기). 넘긴 data를 고쳐서 돌려준다.
    /// </summary>
    /// <param name="log">바꾼 내용을 남길 목록 (null이면 남기지 않는다)</param>
    public static PlayerData Sanitize(PlayerData data, List<string> log = null)
    {
        if (data == null) data = new PlayerData();

        if (data.version != GameConstants.PlayerData.CurrentVersion)
        {
            // TODO: version 2부터는 여기서 이전 형식을 변환한다
            log?.Add($"형식 버전 {data.version} → {GameConstants.PlayerData.CurrentVersion}");
            data.version = GameConstants.PlayerData.CurrentVersion;
        }
        if (data.savedAt == null) data.savedAt = string.Empty;

        if (data.settings == null)
        {
            log?.Add("settings 없음 → 기본값");
            data.settings = new SettingsSaveData();
        }
        SanitizeSettings(data.settings, log);

        // 기록 목록은 새로 만들지 않고 같은 List 안에서 정리한다 (이 목록을 들고 있는 PlayerRecordBook과 어긋나지 않게)
        List<ChartRecordData> merged = MergeRecords(data.records, log);
        if (data.records == null) data.records = merged;
        else
        {
            data.records.Clear();
            data.records.AddRange(merged);
        }
        return data;
    }

    /// <summary>설정 값만 정리한다 (음량·오프셋 범위, 해상도·창 모드 목록). 넘긴 s를 고친다</summary>
    public static void SanitizeSettings(SettingsSaveData s, List<string> log = null)
    {
        s.masterVolume = ClampVolume(s.masterVolume, "masterVolume", log);
        s.musicVolume = ClampVolume(s.musicVolume, "musicVolume", log);
        s.sfxVolume = ClampVolume(s.sfxVolume, "sfxVolume", log);

        int offset = SettingsManager.ClampOffset(s.offsetMs);
        if (offset != s.offsetMs) log?.Add($"offsetMs {s.offsetMs} → {offset}");
        s.offsetMs = offset;

        Vector2Int size = new Vector2Int(s.resolutionWidth, s.resolutionHeight);
        if (Array.IndexOf(GameConstants.Settings.Resolutions, size) < 0)
        {
            Vector2Int fallback = GameConstants.Settings.DefaultResolution;
            log?.Add($"해상도 {size.x}×{size.y} → {fallback.x}×{fallback.y}");
            s.resolutionWidth = fallback.x;
            s.resolutionHeight = fallback.y;
        }

        FullScreenMode mode = ParseWindowMode(s.windowMode);
        if (mode.ToString() != s.windowMode) log?.Add($"windowMode \"{s.windowMode}\" → {mode}");
        s.windowMode = mode.ToString();
    }

    private static int ClampVolume(int value, string name, List<string> log)
    {
        int clamped = Mathf.Clamp(value, GameConstants.Settings.MinVolume, GameConstants.Settings.MaxVolume);
        if (clamped != value) log?.Add($"{name} {value} → {clamped}");
        return clamped;
    }

    /// <summary>창 모드 이름 → FullScreenMode. 모르는 값이거나 목록(WindowModes)에 없으면 기본값</summary>
    public static FullScreenMode ParseWindowMode(string name)
    {
        if (TryParseName(name, out FullScreenMode mode) && Array.IndexOf(GameConstants.Settings.WindowModes, mode) >= 0) return mode;
        return GameConstants.Settings.DefaultWindowMode;
    }

    /// <summary>채보 종류 이름 → ChartType. 모르는 값이면 Custom (공식 채보 목록은 게임에 들어 있어 chartId로 다시 알 수 있다)</summary>
    public static ChartType ParseChartType(string name)
    {
        return TryParseName(name, out ChartType type) ? type : ChartType.Custom;
    }

    /// <summary>랭크 이름 → PlayRank. 모르는 값이면 F</summary>
    public static PlayRank ParseRank(string name)
    {
        return TryParseName(name, out PlayRank rank) ? rank : PlayRank.F;
    }

    /// <summary>enum 이름만 받는다 (숫자 문자열 "2"는 순서가 바뀌면 뜻이 달라지므로 받지 않는다)</summary>
    private static bool TryParseName<T>(string name, out T value) where T : struct
    {
        value = default;
        if (string.IsNullOrEmpty(name) || !Enum.IsDefined(typeof(T), name)) return false;
        return Enum.TryParse(name, false, out value);
    }

    /// <summary>
    /// 기록 하나의 값을 정리한다: 점수 0~1,000,000, 정확도 0~100, 콤보·횟수 0 이상, enum 문자열 정리.
    /// </summary>
    public static void SanitizeRecord(ChartRecordData r)
    {
        if (r.songId == null) r.songId = string.Empty;
        r.chartType = ParseChartType(r.chartType).ToString();
        r.bestScore = Mathf.Clamp(r.bestScore, 0, GameConstants.Play.MaxScore);
        r.bestRank = ParseRank(r.bestRank).ToString();
        r.bestAccuracy = double.IsNaN(r.bestAccuracy) ? 0 : Math.Max(0, Math.Min(100, r.bestAccuracy));
        r.maxCombo = Math.Max(0, r.maxCombo);
        r.playCount = Math.Max(0, r.playCount);
        if (r.firstPlayedAt == null) r.firstPlayedAt = string.Empty;
        if (r.lastPlayedAt == null) r.lastPlayedAt = string.Empty;
    }

    /// <summary>
    /// chartId가 비어 있는 기록을 버리고 같은 chartId 기록을 하나로 합친다 (처음 나온 순서 유지).
    /// 합칠 때: 점수·정확도·콤보는 최고값, bestRank는 최고 점수를 낸 기록의 것, FC/AP는 하나라도 true면 true,
    /// playCount는 합, firstPlayedAt은 가장 이른 것, lastPlayedAt은 가장 늦은 것.
    /// </summary>
    public static List<ChartRecordData> MergeRecords(List<ChartRecordData> records, List<string> log = null)
    {
        List<ChartRecordData> merged = new List<ChartRecordData>();
        if (records == null) return merged;

        Dictionary<string, ChartRecordData> byId = new Dictionary<string, ChartRecordData>();
        foreach (ChartRecordData record in records)
        {
            if (record == null || string.IsNullOrEmpty(record.chartId))
            {
                log?.Add("chartId 없는 기록 버림");
                continue;
            }
            SanitizeRecord(record);

            if (!byId.TryGetValue(record.chartId, out ChartRecordData kept))
            {
                byId.Add(record.chartId, record);
                merged.Add(record);
                continue;
            }

            log?.Add($"같은 chartId 기록 합침: {record.chartId}");
            if (record.bestScore > kept.bestScore)
            {
                kept.bestScore = record.bestScore;
                kept.bestRank = record.bestRank;
            }
            kept.bestAccuracy = Math.Max(kept.bestAccuracy, record.bestAccuracy);
            kept.maxCombo = Math.Max(kept.maxCombo, record.maxCombo);
            kept.isFullCombo |= record.isFullCombo;
            kept.isAllPerfect |= record.isAllPerfect;
            kept.playCount += record.playCount;
            kept.firstPlayedAt = EarlierTime(kept.firstPlayedAt, record.firstPlayedAt);
            kept.lastPlayedAt = LaterTime(kept.lastPlayedAt, record.lastPlayedAt);
            if (string.IsNullOrEmpty(kept.songId)) kept.songId = record.songId;
        }
        return merged;
    }

    /// <summary>두 시각 중 이른 것 (빈 값은 무시). 같은 형식(ISO 8601 UTC)이라 문자열 비교로 순서가 맞다</summary>
    private static string EarlierTime(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
        if (string.IsNullOrEmpty(b)) return a;
        return string.CompareOrdinal(a, b) <= 0 ? a : b;
    }

    /// <summary>두 시각 중 늦은 것 (빈 값은 무시)</summary>
    private static string LaterTime(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? string.Empty;
        if (string.IsNullOrEmpty(b)) return a;
        return string.CompareOrdinal(a, b) >= 0 ? a : b;
    }
}
