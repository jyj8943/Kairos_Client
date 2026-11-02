using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제공 곡 목록. 곡 고르기 화면은 이 목록 순서대로 곡을 보여준다.
/// </summary>
[CreateAssetMenu(menuName = "Kairos/Song Database", fileName = "SongDatabase")]
public class SongDatabase : ScriptableObject
{
    [SerializeField] private List<SongData> songs = new List<SongData>();

    public IReadOnlyList<SongData> Songs => songs;
}
