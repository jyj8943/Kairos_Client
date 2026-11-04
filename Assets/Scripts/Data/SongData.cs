using System.Globalization;
using UnityEngine;

/// <summary>
/// 제공 곡 하나의 정보. 곡마다 에셋 하나를 만든다.
/// - songId는 곡마다 고유하며, 채보 파일과 연결하는 키로 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "Kairos/Song Data", fileName = "SongData")]
public class SongData : ScriptableObject
{
    [Tooltip("곡 고유 ID (소문자 영문). 채보 파일과 연결하는 키")]
    [SerializeField] private string songId;
    [SerializeField] private string title;
    [SerializeField] private string artist;
    [Tooltip("기본 BPM. 소수 그대로 저장 (예: 150.61). 모르면 0")]
    [SerializeField] private float bpm;
    [Tooltip("음원 시작부터 첫 박까지의 시간 (ms)")]
    [SerializeField] private int offsetMs;
    [SerializeField] private AudioClip clip;
    [Tooltip("선택. 없으면 회색 칸으로 표시")]
    [SerializeField] private Sprite cover;

    public string SongId => songId;
    public string Title => title;
    public string Artist => artist;
    public float Bpm => bpm;

    /// <summary>음원 시작부터 첫 박까지의 시간 (ms)</summary>
    public int OffsetMs => offsetMs;

    public AudioClip Clip => clip;
    public Sprite Cover => cover;

    /// <summary>곡 길이(초). 음원이 없으면 0</summary>
    public float Length => clip != null ? clip.length : 0f;

    /// <summary>
    /// 곡 길이를 m:ss 형식으로 돌려준다. (예: 2:24)
    /// </summary>
    public string GetLengthText()
    {
        return TimeFormat.ToMinSec(Length);
    }

    /// <summary>
    /// BPM 표시 문자열. 반올림하지 않는다: 정수면 "216", 소수면 둘째 자리까지 "150.61".
    /// 곡 목록과 에디터가 같은 규칙을 쓰도록 여기서만 만든다.
    /// </summary>
    public static string FormatBpm(float bpm)
    {
        return bpm.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
