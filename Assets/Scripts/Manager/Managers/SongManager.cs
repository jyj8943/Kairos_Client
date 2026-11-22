using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제공 곡 목록 전역 매니저. Managers_DontDestroy 프리팹의 "SongManager" 오브젝트에 붙이고, Bootstrapper가 Init()을 호출한다.
/// - 곡 목록(SongDatabase) 참조는 이 매니저만 가진다. 다른 곳은 GameManager.Songs로 곡을 찾는다.
/// - Init에서 곡 목록을 검사해 문제가 있으면 에러 로그를 남긴다.
/// </summary>
public class SongManager : MonoSingleton<SongManager>, IGameModule
{
    [Tooltip("제공 곡 목록 (Assets/Data/Songs/SongDatabase.asset)")]
    [SerializeField] private SongDatabase songDatabase;

    private static readonly IReadOnlyList<SongData> EmptySongs = new List<SongData>();

    /// <summary>제공 곡 목록 (SongDatabase 순서). 목록이 없으면 빈 목록</summary>
    public IReadOnlyList<SongData> Songs => songDatabase != null ? songDatabase.Songs : EmptySongs;

    /// <summary>
    /// 곡 목록을 검사한다. 빈 songId·중복 songId·시작 BPM ≤ 0·잘못된 BPM 구간이 있으면 에러 로그를 남긴다.
    /// 이웃 구간 BPM이 같으면 경고만 남긴다.
    /// </summary>
    public void Init()
    {
        if (songDatabase == null)
        {
            Debug.LogError("[SongManager] songDatabase가 연결되지 않았습니다.", this);
            return;
        }

        if (songDatabase.Songs.Count == 0)
            Debug.LogError("[SongManager] 곡 목록이 비어 있습니다.", this);

        HashSet<string> ids = new HashSet<string>();
        for (int i = 0; i < songDatabase.Songs.Count; i++)
        {
            SongData song = songDatabase.Songs[i];
            if (song == null)
            {
                Debug.LogError($"[SongManager] 곡 목록 {i}번이 비어 있습니다.", songDatabase);
                continue;
            }

            if (string.IsNullOrWhiteSpace(song.SongId))
                Debug.LogError($"[SongManager] songId가 비어 있습니다: {song.name}", song);
            else if (!ids.Add(song.SongId))
                Debug.LogError($"[SongManager] songId가 겹칩니다: {song.SongId} ({song.name})", song);

            if (song.StartBpm <= 0f)
                Debug.LogError($"[SongManager] 시작 BPM이 0 이하입니다: {song.SongId} ({song.name}), bpm={song.StartBpm}", song);

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            ValidateTimingPoints(song.TimingPoints, errors, warnings);
            foreach (string error in errors)
                Debug.LogError($"[SongManager] BPM 구간 오류: {song.SongId} ({song.name}) - {error}", song);
            foreach (string warning in warnings)
                Debug.LogWarning($"[SongManager] BPM 구간 경고: {song.SongId} ({song.name}) - {warning}", song);
        }
    }

    /// <summary>
    /// 곡의 BPM 구간 목록을 검사한다 (순수 함수, EditMode 테스트용).
    /// 오류: 비어 있음, 첫 구간이 0박(tick 0)이 아님, beat가 오름차순이 아님, 같은 위치(tick) 중복, BPM이 30 ~ 400 밖.
    /// 경고: 이웃 구간 BPM이 같음 (BpmSectionRules.SameBpm).
    /// </summary>
    /// <returns>오류가 없으면 true</returns>
    public static bool ValidateTimingPoints(IReadOnlyList<TimingPointData> points, List<string> errors, List<string> warnings)
    {
        int errorCount = errors.Count;
        if (points == null || points.Count == 0)
        {
            errors.Add("BPM 구간이 없습니다.");
            return false;
        }

        int previousTick = 0;
        float previousBpm = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            TimingPointData point = points[i];
            if (point == null)
            {
                errors.Add($"{i}번 구간이 비어 있습니다.");
                continue;
            }

            int tick = BeatGridMath.BeatToTick(point.beat);
            if (i == 0 && tick != 0)
                errors.Add($"첫 구간이 0박이 아닙니다: {point.beat}박");
            if (i > 0 && tick == previousTick)
                errors.Add($"{i}번 구간이 앞 구간과 같은 위치입니다: {point.beat}박");
            else if (i > 0 && tick < previousTick)
                errors.Add($"{i}번 구간이 beat 오름차순이 아닙니다: {point.beat}박");
            if (!BpmSectionRules.IsInRange(point.bpm))
                errors.Add($"{i}번 구간 BPM이 {GameConstants.Editor.MinSectionBpm} ~ {GameConstants.Editor.MaxSectionBpm} 밖입니다: {point.bpm}");
            if (i > 0 && BpmSectionRules.SameBpm(previousBpm, point.bpm))
                warnings.Add($"{i}번 구간 BPM이 앞 구간과 같습니다: {SongData.FormatBpm(point.bpm)} ({point.beat}박)");

            previousTick = tick;
            previousBpm = point.bpm;
        }
        return errors.Count == errorCount;
    }

    /// <summary>
    /// songId로 곡을 찾는다. 없으면 null.
    /// </summary>
    public SongData Find(string songId)
    {
        if (string.IsNullOrEmpty(songId)) return null;

        foreach (SongData song in Songs)
        {
            if (song != null && song.SongId == songId) return song;
        }
        return null;
    }

    /// <summary>songId가 제공 곡 목록에 있으면 true</summary>
    public bool Contains(string songId)
    {
        return Find(songId) != null;
    }
}
