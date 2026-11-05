using System;
using System.Collections.Generic;

/// <summary>
/// 채보 파일 하나 (기획서 2-2). 공식 채보와 나만의 채보가 같은 형식을 쓰고, JsonUtility로 저장한다.
/// - JsonUtility가 저장할 수 있도록 공개 필드만 쓴다 (프로퍼티·Dictionary 금지).
/// - 곡의 offsetMs는 곡 정보(SongData)에 있으므로 여기에 넣지 않는다.
/// - 파일 읽기·쓰기는 ChartRepository가 맡는다.
/// </summary>
[Serializable]
public class ChartData
{
    /// <summary>type 값</summary>
    public const string TypeOfficial = "official";
    public const string TypeCustom = "custom";

    /// <summary>채보 형식 버전 (GameConstants.Chart.CurrentVersion)</summary>
    public int version = GameConstants.Chart.CurrentVersion;

    /// <summary>채보 고유 ID. 파일 이름으로도 쓴다 ({chartId}.json)</summary>
    public string chartId;

    /// <summary>어떤 곡의 채보인지 (SongData.SongId)</summary>
    public string songId;

    /// <summary>"official" / "custom"</summary>
    public string type = TypeCustom;

    /// <summary>채보 이름 (한글·특수문자 가능, 파일 이름에는 쓰지 않는다)</summary>
    public string chartName;

    /// <summary>난이도 이름 (EASY·NORMAL·HARD). 나만의 채보는 지금 빈 문자열</summary>
    public string difficulty = string.Empty;

    /// <summary>만든 사람. 나만의 채보는 지금 빈 문자열</summary>
    public string author = string.Empty;

    /// <summary>키 수</summary>
    public int keyCount = GameConstants.Editor.LaneCount;

    /// <summary>레벨 (GameConstants.Editor.MinLevel ~ MaxLevel)</summary>
    public int level;

    /// <summary>BPM 구간 목록. 최소 1개</summary>
    public List<TimingPointData> timingPoints = new List<TimingPointData>();

    /// <summary>노트 목록 (beat, lane 순)</summary>
    public List<NoteData> notes = new List<NoteData>();

    /// <summary>
    /// enum을 JSON에 쓰는 문자열로 바꾼다.
    /// </summary>
    public static string ToTypeString(ChartType chartType)
    {
        return chartType == ChartType.Official ? TypeOfficial : TypeCustom;
    }

    /// <summary>
    /// JSON의 type 문자열을 enum으로 바꾼다. 모르는 값이면 false.
    /// </summary>
    public static bool TryParseType(string value, out ChartType chartType)
    {
        switch (value)
        {
            case TypeOfficial:
                chartType = ChartType.Official;
                return true;
            case TypeCustom:
                chartType = ChartType.Custom;
                return true;
            default:
                chartType = ChartType.Custom;
                return false;
        }
    }
}
