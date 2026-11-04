using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 채보 파일 읽기·쓰기 창구 (기획서 2-4). 상태를 들고 있지 않는 static 클래스다.
/// - 나만의 채보: persistentDataPath/MyCharts/{songId}/{chartId}.json
/// - 공식 채보: streamingAssetsPath/Charts/{songId}/{chartId}.json (Unity 에디터에서만 저장)
/// - 파일 이름은 chartId만 쓴다. 채보 이름은 파일 이름에 쓰지 않는다.
/// - 저장은 임시 파일({chartId}.json.tmp)에 먼저 쓰고 다 쓰면 원래 파일과 바꿔치기한다.
/// - 읽을 때 형식을 검사하고, JSON이 깨진 파일은 .broken으로 이름을 바꿔 백업한다.
/// - 곡 검사는 호출하는 쪽에서 GameManager.Songs로 한다 (이 클래스는 파일만 다룬다).
/// </summary>
public static class ChartRepository
{
    private const string LogPrefix = "[ChartRepository]";

    /// <summary>BOM 없는 UTF-8 (다른 도구에서 열어도 첫 글자가 깨지지 않게)</summary>
    private static readonly Encoding FileEncoding = new UTF8Encoding(false);

    private static readonly System.Random Random = new System.Random();

    /// <summary>
    /// 이번 실행에서 이미 만든 새 채보 ID. 저장 전에 연속으로 만들어도 겹치지 않게 한다.
    /// </summary>
    private static readonly HashSet<string> IssuedCustomIds = new HashSet<string>();

    /// <summary>
    /// 채보 종류별 최상위 폴더.
    /// </summary>
    public static string GetRootFolder(ChartType type)
    {
        return type == ChartType.Official
            ? Path.Combine(Application.streamingAssetsPath, GameConstants.Chart.OfficialFolder)
            : Path.Combine(Application.persistentDataPath, GameConstants.Chart.CustomFolder);
    }

    /// <summary>
    /// 곡 하나의 채보 폴더.
    /// </summary>
    public static string GetSongFolder(string songId, ChartType type)
    {
        return Path.Combine(GetRootFolder(type), songId);
    }

    /// <summary>
    /// 채보 파일 경로: {곡 폴더}/{chartId}.json
    /// </summary>
    public static string GetChartPath(string songId, string chartId, ChartType type)
    {
        return Path.Combine(GetSongFolder(songId, type), chartId + GameConstants.Chart.FileExtension);
    }

    /// <summary>
    /// 채보를 저장한다. 같은 chartId 파일이 있으면 덮어쓴다.
    /// - 형식 검사를 통과해야 저장한다.
    /// - 공식 채보는 Unity 에디터에서 실행할 때만 저장할 수 있다.
    /// </summary>
    /// <param name="error">실패 이유. 성공하면 빈 문자열</param>
    public static bool TrySave(ChartData chart, out string error)
    {
        if (chart == null)
        {
            error = "채보가 없습니다.";
            return false;
        }

        if (!Validate(chart, out error)) return false;

        ChartData.TryParseType(chart.type, out ChartType type);

#if !UNITY_EDITOR
        if (type == ChartType.Official)
        {
            error = "공식 채보는 Unity 에디터에서만 저장할 수 있습니다.";
            return false;
        }
#endif

        string path = GetChartPath(chart.songId, chart.chartId, type);
        string tempPath = path + GameConstants.Chart.TempExtension;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(tempPath, JsonUtility.ToJson(chart, true), FileEncoding);

            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            TryDeleteFile(tempPath);
            error = $"파일을 쓰지 못했습니다: {e.Message}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 채보 하나를 읽는다. 파일이 없거나 형식이 맞지 않으면 false와 이유를 돌려준다.
    /// </summary>
    public static bool TryLoad(string songId, string chartId, ChartType type, out ChartData chart, out string error)
    {
        chart = null;

        if (!IsSafeId(songId) || !IsSafeId(chartId))
        {
            error = $"쓸 수 없는 ID입니다: songId={songId}, chartId={chartId}";
            return false;
        }

        string path = GetChartPath(songId, chartId, type);
        if (!File.Exists(path))
        {
            error = $"채보 파일이 없습니다: {path}";
            return false;
        }

        return TryReadFile(path, songId, chartId, out chart, out error);
    }

    /// <summary>
    /// 곡 폴더의 채보를 모두 읽어 chartId 순으로 돌려준다. 폴더가 없으면 빈 목록.
    /// 읽지 못한 파일은 경고만 남기고 건너뛴다.
    /// </summary>
    public static List<ChartData> GetCharts(string songId, ChartType type)
    {
        List<ChartData> charts = new List<ChartData>();

        if (!IsSafeId(songId))
        {
            Debug.LogWarning($"{LogPrefix} 쓸 수 없는 songId입니다: {songId}");
            return charts;
        }

        string folder = GetSongFolder(songId, type);
        if (!Directory.Exists(folder)) return charts;

        string[] files = Directory.GetFiles(folder);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string path in files)
        {
            // "*.json" 검색은 .json.tmp·.json.broken까지 걸릴 수 있어 확장자를 직접 비교한다
            if (!string.Equals(Path.GetExtension(path), GameConstants.Chart.FileExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            string chartId = Path.GetFileNameWithoutExtension(path);
            if (TryReadFile(path, songId, chartId, out ChartData chart, out string error))
                charts.Add(chart);
            else
                Debug.LogWarning($"{LogPrefix} 채보를 건너뜁니다: {error}");
        }

        charts.Sort((a, b) => string.CompareOrdinal(a.chartId, b.chartId));
        return charts;
    }

    /// <summary>
    /// 새 나만의 채보 ID를 만든다: my_yyyyMMdd_HHmmss_xxxx (xxxx는 영문 소문자·숫자).
    /// 같은 이름의 파일이 있거나 이번 실행에서 이미 만든 ID면 다시 만든다.
    /// </summary>
    /// <returns>만든 ID. songId를 쓸 수 없거나 계속 겹치면 null</returns>
    public static string CreateNewCustomChartId(string songId)
    {
        if (!IsSafeId(songId))
        {
            Debug.LogError($"{LogPrefix} 쓸 수 없는 songId라 채보 ID를 만들 수 없습니다: {songId}");
            return null;
        }

        for (int attempt = 0; attempt < GameConstants.Chart.CustomIdMaxAttempts; attempt++)
        {
            string id = GameConstants.Chart.CustomIdPrefix
                        + DateTime.Now.ToString(GameConstants.Chart.CustomIdDateFormat, CultureInfo.InvariantCulture)
                        + "_" + CreateRandomSuffix();

            if (IssuedCustomIds.Contains(id)) continue;
            if (File.Exists(GetChartPath(songId, id, ChartType.Custom))) continue;

            IssuedCustomIds.Add(id);
            return id;
        }

        Debug.LogError($"{LogPrefix} 겹치지 않는 채보 ID를 만들지 못했습니다: songId={songId}");
        return null;
    }

    /// <summary>
    /// 파일 하나를 읽고 검사한다.
    /// - JSON이 깨졌으면 .broken으로 백업한다.
    /// - songId·chartId가 폴더·파일 이름과 다르거나 형식이 맞지 않으면 실패한다 (파일은 그대로 둔다).
    /// </summary>
    private static bool TryReadFile(string path, string expectedSongId, string expectedChartId, out ChartData chart, out string error)
    {
        chart = null;

        string json;
        try
        {
            json = File.ReadAllText(path, FileEncoding);
        }
        catch (Exception e)
        {
            error = $"파일을 읽지 못했습니다: {path} ({e.Message})";
            return false;
        }

        ChartData parsed = null;
        string parseError = null;
        try
        {
            parsed = JsonUtility.FromJson<ChartData>(json);
        }
        catch (Exception e)
        {
            parseError = e.Message;
        }

        if (parsed == null)
        {
            string backupPath = BackupBrokenFile(path);
            error = $"JSON을 읽지 못했습니다: {path} ({parseError ?? "빈 파일"}) → 백업: {backupPath ?? "실패"}";
            return false;
        }

        if (parsed.songId != expectedSongId)
        {
            error = $"폴더의 songId({expectedSongId})와 파일의 songId({parsed.songId})가 다릅니다: {path}";
            return false;
        }

        if (parsed.chartId != expectedChartId)
        {
            error = $"파일 이름의 chartId({expectedChartId})와 파일의 chartId({parsed.chartId})가 다릅니다: {path}";
            return false;
        }

        if (parsed.notes == null) parsed.notes = new List<NoteData>();

        if (!Validate(parsed, out string validateError))
        {
            error = $"{validateError}: {path}";
            return false;
        }

        SortNotes(parsed.notes);
        chart = parsed;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 채보 형식을 검사한다. 저장 전과 읽은 뒤에 같은 규칙을 쓴다.
    /// </summary>
    private static bool Validate(ChartData chart, out string error)
    {
        if (chart.version != GameConstants.Chart.CurrentVersion)
        {
            error = $"지원하지 않는 채보 버전입니다: {chart.version} (지원: {GameConstants.Chart.CurrentVersion})";
            return false;
        }

        if (!IsSafeId(chart.songId))
        {
            error = $"쓸 수 없는 songId입니다: {chart.songId}";
            return false;
        }

        if (!IsSafeId(chart.chartId))
        {
            error = $"쓸 수 없는 chartId입니다: {chart.chartId}";
            return false;
        }

        if (!ChartData.TryParseType(chart.type, out _))
        {
            error = $"알 수 없는 채보 종류입니다: {chart.type}";
            return false;
        }

        if (chart.keyCount != GameConstants.Editor.LaneCount)
        {
            error = $"지원하지 않는 키 수입니다: {chart.keyCount} (지원: {GameConstants.Editor.LaneCount})";
            return false;
        }

        if (chart.timingPoints == null || chart.timingPoints.Count == 0)
        {
            error = "BPM 구간(timingPoints)이 없습니다.";
            return false;
        }

        for (int i = 0; i < chart.timingPoints.Count; i++)
        {
            TimingPointData point = chart.timingPoints[i];
            if (point == null || point.bpm <= 0f || point.beat < 0f)
            {
                error = $"BPM 구간 {i}이(가) 잘못되었습니다 (bpm은 0보다 크고 beat는 0 이상이어야 합니다).";
                return false;
            }
        }

        if (chart.notes == null)
        {
            error = "노트 목록(notes)이 없습니다.";
            return false;
        }

        for (int i = 0; i < chart.notes.Count; i++)
        {
            NoteData note = chart.notes[i];
            if (note == null)
            {
                error = $"노트 {i}이(가) 비어 있습니다.";
                return false;
            }

            if (note.lane < 0 || note.lane >= chart.keyCount)
            {
                error = $"노트 {i}의 lane({note.lane})이 범위(0 ~ {chart.keyCount - 1}) 밖입니다.";
                return false;
            }

            if (note.beat < 0f)
            {
                error = $"노트 {i}의 beat({note.beat})가 0보다 작습니다.";
                return false;
            }

            if (note.type != NoteData.TypeShort && note.type != NoteData.TypeLong)
            {
                error = $"노트 {i}의 type({note.type})을 알 수 없습니다.";
                return false;
            }

            if (note.type == NoteData.TypeLong && note.endBeat <= note.beat)
            {
                error = $"롱노트 {i}의 endBeat({note.endBeat})가 beat({note.beat})보다 크지 않습니다.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 파일 이름으로 쓸 수 있는 ID인지 확인한다. 경로 구분자·상위 폴더·파일 이름에 못 쓰는 문자를 막는다.
    /// </summary>
    private static bool IsSafeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim()) return false;
        if (id == "." || id == "..") return false;
        if (id.IndexOf('/') >= 0 || id.IndexOf('\\') >= 0) return false;
        return id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    /// <summary>
    /// 노트를 beat, lane 순으로 정렬한다.
    /// </summary>
    private static void SortNotes(List<NoteData> notes)
    {
        notes.Sort((a, b) =>
        {
            int byBeat = a.beat.CompareTo(b.beat);
            return byBeat != 0 ? byBeat : a.lane.CompareTo(b.lane);
        });
    }

    /// <summary>
    /// 깨진 파일을 {파일}.broken으로 이름을 바꾼다. 같은 이름이 있으면 시간(같은 초면 번호까지)을 붙인다.
    /// </summary>
    /// <returns>백업 경로. 실패하면 null</returns>
    private static string BackupBrokenFile(string path)
    {
        string backupPath = path + GameConstants.Chart.BrokenExtension;
        if (File.Exists(backupPath))
        {
            string time = DateTime.Now.ToString(GameConstants.Chart.BrokenBackupTimeFormat, CultureInfo.InvariantCulture);
            backupPath = $"{path}.{time}{GameConstants.Chart.BrokenExtension}";
            for (int number = 1; File.Exists(backupPath); number++)
                backupPath = $"{path}.{time}_{number}{GameConstants.Chart.BrokenExtension}";
        }

        try
        {
            File.Move(path, backupPath);
            Debug.LogWarning($"{LogPrefix} 읽지 못한 채보를 백업했습니다: {backupPath}");
            return backupPath;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogPrefix} 읽지 못한 채보를 백업하지 못했습니다: {path} ({e.Message})");
            return null;
        }
    }

    private static string CreateRandomSuffix()
    {
        string chars = GameConstants.Chart.CustomIdRandomChars;
        char[] suffix = new char[GameConstants.Chart.CustomIdRandomLength];
        lock (Random)
        {
            for (int i = 0; i < suffix.Length; i++)
                suffix[i] = chars[Random.Next(chars.Length)];
        }
        return new string(suffix);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogPrefix} 임시 파일을 지우지 못했습니다: {path} ({e.Message})");
        }
    }
}
