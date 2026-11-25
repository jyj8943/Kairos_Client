using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 제공 곡 하나의 정보. 곡마다 에셋 하나를 만든다.
/// - songId는 곡마다 고유하며, 채보 파일과 연결하는 키로 쓴다.
/// - BPM 구간(변속)은 timingPoints 목록이 기준이다. 목록이 비어 있으면 bpm 하나(0박)를 쓴다.
///   기존 곡 에셋(목록 없음)을 고치지 않아도 동작하게 하려고 bpm 필드를 남겼고, 읽는 쪽은 항상 TimingPoints / StartBpm만 쓴다 (기준은 한 곳).
/// - bpmDisplay · promptBpm은 화면 BPM 표기(BpmDisplayRules) 기준이다. 타이밍 계산에는 절대 쓰지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Kairos/Song Data", fileName = "SongData")]
public class SongData : ScriptableObject
{
    [Tooltip("곡 고유 ID (소문자 영문). 채보 파일과 연결하는 키")]
    [SerializeField] private string songId;
    [SerializeField] private string title;
    [SerializeField] private string artist;
    [Tooltip("곡 시작 BPM (BPM 구간 목록이 비어 있을 때만 기준). 목록이 있으면 목록 첫 구간 값으로 자동으로 맞춘다")]
    [SerializeField] private float bpm;
    [Tooltip("BPM 구간 (채보 timingPoints와 같은 형식, beat 오름차순, 첫 구간은 0박). 비어 있으면 bpm 하나를 쓴다. 새 커스텀 채보가 이 구간으로 시작한다")]
    [SerializeField] private List<TimingPointData> timingPoints = new List<TimingPointData>();
    [Tooltip("BPM 표기 방식. Main = 구간 값 중 promptBpm과 가장 가까운 값 하나, Range = 최소 ~ 최대. 타이밍 계산에는 쓰지 않는다")]
    [SerializeField] private SongBpmDisplay bpmDisplay = SongBpmDisplay.Main;
    [Tooltip("곡을 만들 때 수노 프롬프트에 넣은 BPM (대표 BPM을 고르는 기준). 0이면 시작 BPM을 기준으로 한다. 타이밍 계산에는 쓰지 않는다")]
    [SerializeField] private float promptBpm;
    [Tooltip("음원 시작부터 첫 박까지의 시간 (ms)")]
    [SerializeField] private int offsetMs;
    [SerializeField] private AudioClip clip;
    [Tooltip("선택. 없으면 회색 칸으로 표시")]
    [SerializeField] private Sprite cover;
    [Tooltip("배경 일러스트 (1920×1080). 곡 선택·결과는 흐리게, 인게임은 그대로 쓴다. 없으면 앨범 이미지(cover)로 대신한다")]
    [SerializeField] private Sprite background;

    [Header("BGA (기획서 3-4 「배경 · BGA (11/19)」, 지금은 비어 있음)")]
    [Tooltip("BGA 동영상. 있으면 인게임 배경에 일러스트 대신 쓴다 (재생·싱크는 아직 없음, Canvas_InGameBackground TODO)")]
    [SerializeField] private VideoClip bga;
    [Tooltip("BGA 시작 오프셋 (ms). 곡 시간 0초에 동영상의 몇 ms를 보여 줄지")]
    [SerializeField] private int bgaOffsetMs;

    public string SongId => songId;

    /// <summary>BGA 동영상 (없으면 null)</summary>
    public VideoClip Bga => bga;

    /// <summary>BGA 시작 오프셋 (ms)</summary>
    public int BgaOffsetMs => bgaOffsetMs;

    public string Title => title;
    public string Artist => artist;
    /// <summary>
    /// BPM 구간 목록 (beat 순, 첫 구간은 0박). 목록이 비어 있으면 { (0박, bpm) } 하나를 돌려준다.
    /// BPM 구간이 필요한 곳은 항상 이것만 쓴다. 돌려준 항목은 고치지 않는다 (F1 곡 조정은 SerializedObject로 쓴다).
    /// </summary>
    public IReadOnlyList<TimingPointData> TimingPoints => HasTimingPoints ? timingPoints : SingleTimingPoint(bpm);

    /// <summary>곡 시작 BPM (= TimingPoints 첫 구간 BPM)</summary>
    public float StartBpm => HasTimingPoints ? timingPoints[0].bpm : bpm;

    /// <summary>BPM 표기 방식 (BpmDisplayRules)</summary>
    public SongBpmDisplay BpmDisplay => bpmDisplay;

    /// <summary>대표 BPM을 고르는 기준. promptBpm이 0 이하면 StartBpm. 타이밍 계산에는 쓰지 않는다</summary>
    public float PromptBpm => promptBpm > 0f ? promptBpm : StartBpm;

    /// <summary>BPM 구간 목록을 직접 가지고 있으면 true (비어 있으면 bpm 하나를 쓴다)</summary>
    public bool HasTimingPoints => timingPoints != null && timingPoints.Count > 0 && timingPoints[0] != null;

    /// <summary>0박 BPM 하나짜리 구간 목록을 새로 만든다</summary>
    public static List<TimingPointData> SingleTimingPoint(float bpm)
    {
        return new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = bpm } };
    }

#if UNITY_EDITOR
    /// <summary>
    /// 목록이 있으면 bpm 필드를 목록 첫 구간 값으로 맞춘다 (인스펙터에서 두 값이 어긋나 보이지 않게).
    /// 기준은 목록이고 bpm 필드는 목록이 없는 기존 에셋 호환용이다.
    /// </summary>
    private void OnValidate()
    {
        if (HasTimingPoints && bpm != timingPoints[0].bpm) bpm = timingPoints[0].bpm;
    }
#endif

    /// <summary>음원 시작부터 첫 박까지의 시간 (ms)</summary>
    public int OffsetMs => offsetMs;

    public AudioClip Clip => clip;
    public Sprite Cover => cover;

    /// <summary>곡 선택 배경 일러스트. 없으면 앨범 이미지 (둘 다 없으면 null)</summary>
    public Sprite Background => background != null ? background : cover;

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
