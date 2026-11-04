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
    /// 곡 목록을 검사한다. 빈 songId·중복 songId·BPM ≤ 0인 곡이 있으면 에러 로그를 남긴다.
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

            if (song.Bpm <= 0f)
                Debug.LogError($"[SongManager] BPM이 0 이하입니다: {song.SongId} ({song.name}), bpm={song.Bpm}", song);
        }
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
