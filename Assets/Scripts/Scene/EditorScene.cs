using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 씬 진입점. EditorScene.unity 루트의 "EditorScene" 오브젝트에 붙인다.
/// - 곡 고르기 씬이 넘긴 채보 정보의 songId로 곡(SongData)을 찾아 BPM·offset·곡 길이로 편집을 시작한다.
/// - 진입 데이터에 chartId가 있으면 그 내 채보를 불러와 세션을 채운다. 실패하면 곡 고르기로 돌아간다.
/// - 저장 요청(Btn_Save·Ctrl+S)을 받아 곡을 확인한 뒤 세션에 저장을 맡긴다.
/// - 나가기 요청(Btn_Exit)을 받아 곡 고르기로 돌아간다 (저장 안 한 변경이 있으면 확인 팝업을 띄운다).
/// - ESC(OnBack)는 지금 아무것도 하지 않는다 (기존 동작 그대로).
/// - 테스트 플레이 요청(Btn_TestPlay)을 받아 지금 상태를 저장하지 않고 인게임으로 넘긴다.
///   돌아올 때는 진입 데이터의 restore로 노트·위치·스냅·그리드 높이·도구·수정 여부를 되돌린다.
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

    [Header("재생")]
    [SerializeField] private EditorPlayback playback;

#if UNITY_EDITOR
    [Header("개발용 (Unity 에디터 전용, 빌드에 포함되지 않음)")]
    [Tooltip("BPM·offsetMs 조정 패널 (F1). EditorOnly 태그라 빌드에서 빠진다")]
    [SerializeField] private Canvas_SongTuning songTuning;
#endif

    private SongData song;

    /// <summary>Init을 끝까지 마쳤으면 true. 곡을 찾지 못해 돌아가는 중이면 OnEnter에서 입력을 켜지 않는다</summary>
    private bool isReady;

    /// <summary>저장 처리 중이면 true (연타로 겹쳐 실행되지 않게)</summary>
    private bool isSaving;

    /// <summary>잠깐 보여준 안내를 저장 상태 표시로 되돌릴 시각 (unscaledTime). 없으면 −1</summary>
    private float statusResetAt = -1f;

    /// <summary>다른 씬(곡 고르기·인게임)으로 이동을 시작했으면 true</summary>
    private bool isLeaving;

    /// <summary>
    /// 에디터 씬 초기화. 채보 정보와 곡을 정하고
    /// 편집 세션 → 그리드 → 노트 표시 → 클릭 처리 → Canvas_Editor 순으로 Init한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[EditorScene] Init");
        isReady = false;

        if (session == null || grid == null || noteLayer == null || notePlacer == null || canvas == null || playback == null)
        {
            Debug.LogError("[EditorScene] session, grid, noteLayer, notePlacer, canvas, playback 중 연결되지 않은 것이 있습니다.", this);
            yield break;
        }

        EditorEntryData entry = GameManager.Scene.TakeSceneData<EditorEntryData>();
        song = FindSong(entry);
        if (song == null || song.Bpm <= 0f)
        {
            Debug.LogError($"[EditorScene] 편집할 곡을 쓸 수 없어 곡 고르기로 돌아갑니다: songId={entry?.songId ?? "(없음)"}, " +
                           $"bpm={(song != null ? song.Bpm : 0f)}", this);
            GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect);
            yield break;
        }

        // 테스트 플레이에서 돌아온 경우: 파일을 읽지 않고 넘겨받은 편집 상태로 되돌린다
        EditorRestoreState restore = entry != null ? entry.restore : null;

        // 내 채보 열기: chartId가 있으면 파일을 읽는다. 실패하면 곡 고르기로 돌아간다 (같은 곡이 선택되게)
        ChartData chart = restore != null ? restore.chart : null;
        if (restore == null && entry != null && !string.IsNullOrEmpty(entry.chartId)
            && !ChartRepository.TryLoad(entry.songId, entry.chartId, ChartType.Custom, out chart, out string loadError))
        {
            Debug.LogError($"[EditorScene] 채보를 불러오지 못해 곡 고르기로 돌아갑니다: {loadError}", this);
            GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect, new EditorSongSelectEntryData { songId = entry.songId });
            yield break;
        }

        session.Init(entry, song);
        if (chart != null)
        {
            // 채보 이름·레벨·노트·chartId는 채보 값을 쓴다
            session.ApplyChartData(chart);
            if (restore != null) session.RestoreDirty(restore.isDirty);
            Debug.Log($"[EditorScene] 채보를 {(restore != null ? "테스트 플레이 전 상태로 되돌렸습니다" : "불러왔습니다")}: " +
                      $"chartId={session.ChartId ?? "(새 채보)"}, chartName={session.ChartName}, level={session.Level}, " +
                      $"노트 {session.Data.Notes.Count}개, 수정 여부 {session.IsDirty}", this);
        }

        // 1박 높이는 편집에 쓰는 BPM(채보를 열었으면 채보 BPM)으로 정한다
        grid.Init(session.TotalBeats, session.Bpm);
        if (restore != null)
        {
            grid.SetSnap(restore.snapDivision);
            grid.SetGridHeight(restore.gridHeightScale);
            grid.SetCurrentBeat(restore.currentBeat);
        }

        noteLayer.Init(grid, session);
        notePlacer.Init(grid, session, () => canvas.CurrentTool);
        canvas.Init(grid, session, song, OnSaveRequested, OnExitRequested, OnTestPlayRequested);

        // 재생은 정지 상태로 시작한다 (테스트 플레이에서 돌아온 경우도)
        playback.Init(grid, session, song, () => canvas.SetPlaying(playback.IsPlaying), canvas.RefreshTimeline);
        SubscribeCanvasEvents();

        if (restore != null)
        {
            canvas.SetTool(restore.tool);
            canvas.RefreshSaveStatus();
        }
        else if (chart != null && !Mathf.Approximately(chart.timingPoints[0].bpm, song.Bpm))
        {
            // 곡 BPM을 바꾼 뒤 예전 채보를 연 경우. 자동으로 고치지 않고 알리기만 한다 (편집은 채보 BPM으로)
            Debug.LogWarning($"[EditorScene] {GameConstants.Editor.BpmMismatchWarning}: 곡 {SongData.FormatBpm(song.Bpm)}, " +
                             $"채보 {SongData.FormatBpm(chart.timingPoints[0].bpm)} (chartId={chart.chartId}). 편집은 채보 BPM으로 합니다.", this);
            canvas.ShowStatusMessage(GameConstants.Editor.BpmMismatchWarning);
        }

#if UNITY_EDITOR
        if (songTuning != null) songTuning.Init(song, session, ApplyTiming);
#endif

        statusResetAt = -1f;
        isLeaving = false;
        isReady = true;
    }

    /// <summary>
    /// 잠깐 보여준 안내(테스트 플레이 안내 등) 시간이 지나면 저장 상태 표시를 되돌린다.
    /// </summary>
    private void Update()
    {
        if (statusResetAt < 0f || Time.unscaledTime < statusResetAt) return;

        statusResetAt = -1f;
        canvas.RefreshSaveStatus();
    }

    /// <summary>
    /// 초기화가 끝나면 휠·저장 단축키 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
        if (!isReady) return;

        Debug.Log("[EditorScene] OnEnter");
        canvas.EnableInput();
#if UNITY_EDITOR
        if (songTuning != null) songTuning.EnableInput();
#endif
    }

    /// <summary>
    /// 씬을 떠나기 직전 입력 구독을 해제한다.
    /// </summary>
    public override void OnExit()
    {
        if (playback != null) playback.Stop(false);
        if (canvas != null) canvas.DisableInput();
#if UNITY_EDITOR
        if (songTuning != null) songTuning.DisableInput();
#endif
        UnsubscribeCanvasEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeCanvasEvents();
    }

    private void SubscribeCanvasEvents()
    {
        UnsubscribeCanvasEvents();
        canvas.PlayToggleRequested += playback.Toggle;
        canvas.ManualMoveStarting += StopPlaybackWithSnap;
        canvas.SeekStarted += StopPlaybackWithoutSnap;
        canvas.SeekDragged += OnSeekDragged;
        canvas.SeekReleased += OnSeekReleased;
        canvas.MetronomeChanged += playback.SetMetronome;
    }

    private void UnsubscribeCanvasEvents()
    {
        if (canvas == null || playback == null) return;

        canvas.PlayToggleRequested -= playback.Toggle;
        canvas.ManualMoveStarting -= StopPlaybackWithSnap;
        canvas.SeekStarted -= StopPlaybackWithoutSnap;
        canvas.SeekDragged -= OnSeekDragged;
        canvas.SeekReleased -= OnSeekReleased;
        canvas.MetronomeChanged -= playback.SetMetronome;
    }

    private void StopPlaybackWithSnap()
    {
        playback.Stop(true);
    }

    private void StopPlaybackWithoutSnap()
    {
        playback.Stop(false);
    }

    /// <summary>
    /// 타임라인 슬라이더를 끄는 중: 그 시간으로 그리드를 옮긴다 (스냅에 맞추지 않음).
    /// </summary>
    private void OnSeekDragged(float normalized)
    {
        MoveToTime(normalized * session.SongLengthSeconds, false);
    }

    /// <summary>
    /// 타임라인 슬라이더를 놓음: 그 위치의 가장 가까운 스냅 선으로 맞춘다.
    /// </summary>
    private void OnSeekReleased(float normalized)
    {
        MoveToTime(normalized * session.SongLengthSeconds, true);
    }

    private void MoveToTime(float seconds, bool snapToGrid)
    {
        float beat = BeatGridMath.SecondsToBeat(seconds, session.Bpm, session.OffsetMs);
        if (snapToGrid) beat = BeatGridMath.SnapBeat(beat, grid.SnapDivision);
        grid.SetCurrentBeat(beat);
        canvas.RefreshTimeline();
    }

    /// <summary>
    /// BPM·offsetMs를 바꾼다 (Unity 에디터의 조정 도구). 지금 보고 있는 시간을 유지한 채 그리드를 다시 맞추므로,
    /// offset을 바꾸면 마디선이 음악에 대해 움직인다. 재생 중이면 그 자리에서 다시 재생한다.
    /// </summary>
    private void ApplyTiming(float bpm, int offsetMs)
    {
        float currentSeconds = BeatGridMath.BeatToSeconds(grid.CurrentBeat, session.Bpm, session.OffsetMs);

        session.SetTiming(bpm, offsetMs);
        grid.SetBpm(session.Bpm);
        grid.SetTotalBeats(session.TotalBeats);
        grid.SetCurrentBeat(BeatGridMath.SecondsToBeat(currentSeconds, session.Bpm, session.OffsetMs));
        canvas.RefreshTiming();
        playback.Restart();

        Debug.Log($"[EditorScene] 박자 정보 변경: bpm={session.Bpm}, offsetMs={session.OffsetMs}, 곡 끝 {session.TotalBeats:0.###}박, " +
                  $"지금 {currentSeconds:0.000}초 = {grid.CurrentBeat:0.###}박", this);
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
    /// 나가기 요청 (Btn_Exit). 곡 고르기로 돌아가며, 방금 편집한 곡이 선택된 상태로 시작한다.
    /// 저장하지 않은 변경(IsDirty)이 있으면 노래를 멈추고 확인 팝업을 띄운다 (취소하면 정지 상태 그대로 에디터에 남는다).
    /// 저장했으면 바로 나간다.
    /// </summary>
    private void OnExitRequested()
    {
        if (!isReady || isLeaving || isSaving) return;

        if (session.IsDirty)
        {
            playback.Stop(false);
            GameManager.UI.Confirm(GameConstants.Confirm.EditorExitMessage, GameConstants.Confirm.EditorExitLabel, ExitToSongSelect);
            return;
        }

        ExitToSongSelect();
    }

    /// <summary>
    /// 저장하지 않고 곡 고르기로 나간다.
    /// </summary>
    private void ExitToSongSelect()
    {
        if (isLeaving) return;

        playback.Stop(false);
        if (GameManager.Scene.Load(GameConstants.Scenes.EditorSongSelect, new EditorSongSelectEntryData { songId = session.SongId }))
            isLeaving = true;
    }

    /// <summary>
    /// 테스트 플레이 요청 (Btn_TestPlay). 지금 편집 상태를 저장하지 않고 그대로 인게임에 넘긴다.
    /// 시작 위치는 그리드의 현재 위치 선이다. 그 뒤에 노트가 없으면 안내만 한다.
    /// </summary>
    private void OnTestPlayRequested()
    {
        if (!isReady || isLeaving || isSaving) return;

        // 재생 중이었으면 노래를 멈추고 스냅 선에 맞춘 위치에서 테스트 플레이를 시작한다
        playback.Stop(true);

        TestPlayData testPlay = session.CreateTestPlayData(grid.CurrentBeat, grid.SnapDivision, grid.GridHeightScale, canvas.CurrentTool, out string error);
        if (testPlay == null)
        {
            statusResetAt = Time.unscaledTime + GameConstants.Editor.StatusMessageSeconds;
            canvas.ShowStatusMessage(error);
            return;
        }

        InGameEntryData data = new InGameEntryData
        {
            songId = session.SongId,
            chartId = session.ChartId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = testPlay
        };

        Debug.Log($"[EditorScene] 테스트 플레이: songId={session.SongId}, 시작 {grid.CurrentBeat}박 (마디 {grid.CurrentMeasure}), " +
                  $"노트 {session.Data.Notes.Count}개, 수정 여부 {session.IsDirty}", this);

        if (GameManager.Scene.Load(GameConstants.Scenes.InGame, data))
            isLeaving = true;
    }

    /// <summary>
    /// 저장 요청 (Btn_Save·Ctrl+S). 곡이 제공 곡인지 확인하고 나만의 채보로 저장한 뒤 결과를 표시한다.
    /// </summary>
    private void OnSaveRequested()
    {
        if (!isReady || isSaving || isLeaving) return;

        // 저장하면 잠깐 보여준 안내는 끝난다 (저장 결과 표시로 바뀐다)
        statusResetAt = -1f;
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
