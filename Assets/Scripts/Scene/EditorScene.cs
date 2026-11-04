using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 씬 진입점. EditorScene.unity 루트의 "EditorScene" 오브젝트에 붙인다.
/// - 곡 고르기 씬이 넘긴 채보 정보의 songId로 곡(SongData)을 찾아 BPM·offset·곡 길이로 편집을 시작한다.
/// - 저장 요청(Btn_Save·Ctrl+S)을 받아 곡을 확인한 뒤 세션에 저장을 맡긴다.
/// </summary>
public class EditorScene : SceneBase
{
    [Header("편집 세션")]
    [SerializeField] private ChartEditSession session;

    [Header("화면")]
    [SerializeField] private ChartGridView grid;
    [SerializeField] private EditorNoteLayer noteLayer;
    [SerializeField] private EditorNotePlacer notePlacer;
    [SerializeField] private Canvas_Editor canvas;

    /// <summary>Init을 끝까지 마쳤으면 true. 곡을 찾지 못해 돌아가는 중이면 OnEnter에서 입력을 켜지 않는다</summary>
    private bool isReady;

    /// <summary>저장 처리 중이면 true (연타로 겹쳐 실행되지 않게)</summary>
    private bool isSaving;

    /// <summary>
    /// 에디터 씬 초기화. 채보 정보와 곡을 정하고
    /// 편집 세션 → 그리드 → 노트 표시 → 클릭 처리 → Canvas_Editor 순으로 Init한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[EditorScene] Init");
        isReady = false;

        if (session == null || grid == null || noteLayer == null || notePlacer == null || canvas == null)
        {
            Debug.LogError("[EditorScene] session, grid, noteLayer, notePlacer, canvas 중 연결되지 않은 것이 있습니다.", this);
            yield break;
        }

        EditorEntryData entry = GameManager.Scene.TakeSceneData<EditorEntryData>();
        SongData song = FindSong(entry);
        if (song == null || song.Bpm <= 0f)
        {
            Debug.LogError($"[EditorScene] 편집할 곡을 쓸 수 없어 곡 고르기로 돌아갑니다: songId={entry?.songId ?? "(없음)"}, " +
                           $"bpm={(song != null ? song.Bpm : 0f)}", this);
            GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect);
            yield break;
        }

        session.Init(entry, song);
        grid.Init(session.TotalBeats);
        noteLayer.Init(grid, session);
        notePlacer.Init(grid, session, () => canvas.CurrentTool);
        canvas.Init(grid, session, song, OnSaveRequested);

        isReady = true;
    }

    /// <summary>
    /// 초기화가 끝나면 휠·저장 단축키 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
        if (!isReady) return;

        Debug.Log("[EditorScene] OnEnter");
        canvas.EnableInput();
    }

    /// <summary>
    /// 씬을 떠나기 직전 입력 구독을 해제한다.
    /// </summary>
    public override void OnExit()
    {
        if (canvas != null) canvas.DisableInput();
    }

    /// <summary>
    /// 채보 정보의 songId로 곡을 찾는다. 채보 정보가 없으면(씬을 직접 실행) 곡 목록의 첫 곡을 쓴다.
    /// </summary>
    private SongData FindSong(EditorEntryData entry)
    {
        if (entry != null) return GameManager.Songs.Find(entry.songId);

        if (GameManager.Songs.Songs.Count == 0) return null;

        SongData firstSong = GameManager.Songs.Songs[0];
        Debug.LogWarning($"[EditorScene] 채보 정보가 없어 곡 목록의 첫 곡({(firstSong != null ? firstSong.SongId : "없음")})으로 시작합니다. " +
                         "(EditorScene을 직접 실행한 경우)", this);
        return firstSong;
    }

    /// <summary>
    /// 저장 요청 (Btn_Save·Ctrl+S). 곡이 제공 곡인지 확인하고 나만의 채보로 저장한 뒤 결과를 표시한다.
    /// </summary>
    private void OnSaveRequested()
    {
        if (!isReady || isSaving) return;

        isSaving = true;
        try
        {
            string error;
            bool saved;
            if (!GameManager.Songs.Contains(session.SongId))
            {
                saved = false;
                error = $"제공 곡 목록에 없는 곡입니다: {session.SongId}";
            }
            else
            {
                saved = session.TrySave(out error);
            }

            if (saved)
                Debug.Log($"[EditorScene] 저장했습니다: songId={session.SongId}, chartId={session.ChartId}, 노트 {session.Data.Notes.Count}개", this);
            else
                Debug.LogError($"[EditorScene] 저장하지 못했습니다: {error}", this);

            canvas.ShowSaveResult(saved);
        }
        finally
        {
            isSaving = false;
        }
    }
}
