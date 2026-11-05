using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 씬 진입점 (숏노트 한 판). InGameScene.unity 루트의 "InGameScene" 오브젝트에 붙인다.
/// - 진입 데이터(곡·채보)로 채보를 불러와 노트 시간을 계산하고, 노래를 준비 시간 뒤에 예약 재생한다.
/// - 곡 시간은 GameManager.Sound.SongTime(DSP 기준). 판정은 NoteJudge, 점수는 ScoreCalculator가 맡는다.
/// - 곡이 끝나면(음원 길이와 마지막 노트 + 1초 중 늦은 쪽) 결과 씬으로 ResultData를 넘긴다.
/// - 테스트 플레이(진입 데이터에 TestPlayData)면 파일 대신 넘겨받은 채보를 쓰고, 시작 박 앞의 노트는 건너뛴다.
///   노래는 시작 위치 2초 전부터 들려준다. ESC를 누르면 결과 없이 에디터로 돌아간다.
/// - 씬을 직접 실행하면(진입 데이터 없음) 인스펙터의 테스트 값으로 시작한다.
/// </summary>
public class InGameScene : SceneBase
{
    [Header("화면")]
    [SerializeField] private GearView gear;
    [SerializeField] private NoteLayer noteLayer;
    [SerializeField] private InGameInput input;
    [SerializeField] private Canvas_InGame canvas;

    [Header("판정 범위 (±ms, 기획서 잠정치)")]
    [SerializeField] private JudgeWindows judgeWindows = new JudgeWindows();

    [Header("테스트 값 (씬을 직접 실행해 진입 데이터가 없을 때)")]
    [SerializeField] private string testSongId = "critical_velocity";
    [Tooltip("비어 있으면 그 곡의 첫 채보")]
    [SerializeField] private string testChartId = "";
    [SerializeField] private ChartType testChartType = ChartType.Custom;

#if UNITY_EDITOR
    [Header("확인용 (Unity 에디터 전용, 빌드에 포함되지 않음)")]
    [Tooltip("켜면 노트 시간마다 키 입력과 같은 경로로 판정 함수에 입력을 넣는다 (싱크·점수 검증용)")]
    [SerializeField] private bool autoPlay;
#endif

    private readonly List<JudgeResult> missResults = new List<JudgeResult>();

    private InGameEntryData entry;
    private SongData song;
    private ChartData chart;
    private NoteJudge judge;
    private ScoreCalculator score;
    private double startDelaySeconds;
    private double musicStartPosition;
    private double endTime;
    private bool isReady;
    private bool isPlaying;
    private bool isFinished;

    // 판정 오차 기록 (키로 친 판정만)
    private int hitCount;
    private double hitErrorSumMs;
    private double hitErrorMaxMs;

    /// <summary>테스트 플레이 중이면 true</summary>
    private bool IsTestPlay => entry != null && entry.testPlay != null;

    /// <summary>
    /// 진입 데이터로 곡과 채보를 준비해 판정·점수·화면을 만든다. 실패하면 안내를 띄우고 시작하지 않는다.
    /// </summary>
    protected override IEnumerator Init()
    {
        isReady = false;

        if (gear == null || noteLayer == null || input == null || canvas == null)
        {
            Debug.LogError("[InGameScene] gear, noteLayer, input, canvas 중 연결되지 않은 것이 있습니다.", this);
            yield break;
        }

        entry = GameManager.Scene.TakeSceneData<InGameEntryData>() ?? CreateTestEntry();
        if (entry.mode == InGameMode.TestPlay && entry.testPlay == null)
            Debug.LogWarning("[InGameScene] 테스트 플레이 모드인데 TestPlayData가 없어 보통 플레이로 시작합니다.", this);

        if (!TryLoad(out string error))
        {
            Debug.LogError($"[InGameScene] 플레이를 시작할 수 없습니다: {error}", this);
            canvas.ShowMessage($"채보를 불러오지 못했습니다\n{error}");
            yield break;
        }

        List<PlayNote> notes = BuildNotes(out int skippedLong, out int skippedBefore);
        if (skippedLong > 0)
            Debug.LogWarning($"[InGameScene] 롱노트는 아직 지원하지 않아 {skippedLong}개를 건너뜁니다.", this);

        judge = new NoteJudge(notes, GameConstants.Editor.LaneCount, judgeWindows);
        score = new ScoreCalculator(judge.TotalCount);

        double firstNoteTime = judge.TotalCount > 0 ? judge.Notes[0].Time : 0;
        double lastNoteTime = judge.TotalCount > 0 ? judge.Notes[judge.TotalCount - 1].Time : 0;
        if (IsTestPlay)
        {
            double startTime = BeatGridMath.BeatToSeconds(entry.testPlay.startBeat, chart.timingPoints[0].bpm, song.OffsetMs);
            TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(startTime);
            musicStartPosition = start.StartPositionSeconds;
            startDelaySeconds = start.DelaySeconds;
        }
        else
        {
            musicStartPosition = 0;
            startDelaySeconds = System.Math.Max(GameConstants.Play.MinStartDelaySeconds, GameConstants.Play.LeadInSeconds - firstNoteTime);
        }
        endTime = System.Math.Max(song.Length, lastNoteTime + GameConstants.Play.EndPaddingSeconds);

        gear.Init();
        noteLayer.Init(gear, judge, entry.scrollMultiplier);
        input.Init(OnLanePressed, gear.SetPressed);
        canvas.Init(song.Title, chart.chartName, chart.level, IsTestPlay);

        Debug.Log($"[InGameScene] 시작{(IsTestPlay ? " (테스트 플레이)" : "")}: songId={song.SongId}, chartId={chart.chartId ?? "(저장 안 한 새 채보)"}, " +
                  $"노트 {judge.TotalCount}개{(IsTestPlay ? $" (시작 {entry.testPlay.startBeat}박 앞 {skippedBefore}개 제외)" : "")}, " +
                  $"bpm={chart.timingPoints[0].bpm}, offsetMs={song.OffsetMs}, 음원 시작 위치 {musicStartPosition:0.###}초, 시작 지연 {startDelaySeconds:0.###}초, " +
                  $"첫 노트 {firstNoteTime:0.###}초, 끝 {endTime:0.###}초, 노트 풀 {noteLayer.PooledCount}개, " +
                  $"배속 x{entry.scrollMultiplier:0.0} → 낙하 속도 {noteLayer.ScrollSpeed:0.###} (기본 {GameConstants.Play.DefaultScrollSpeed})", this);

        isReady = true;
    }

    /// <summary>
    /// 노래를 예약하고 입력을 받기 시작한다.
    /// </summary>
    protected override void OnEnter()
    {
        if (!isReady) return;

        GameManager.Sound.PlayMusicScheduled(song.Clip, startDelaySeconds, musicStartPosition);
        input.EnableInput();
        isPlaying = true;
    }

    /// <summary>
    /// 씬을 떠나기 직전 노래를 멈추고 입력 구독을 해제한다.
    /// </summary>
    public override void OnExit()
    {
        isPlaying = false;
        if (input != null) input.DisableInput();
        if (SoundManager.HasInstance) GameManager.Sound.StopMusic();
    }

    private void Update()
    {
        if (!isPlaying) return;

        double songTime = GameManager.Sound.SongTime;

#if UNITY_EDITOR
        if (autoPlay) RunAutoPlay(songTime);
#endif

        if (judge.CollectMisses(songTime, missResults) > 0)
        {
            for (int i = 0; i < missResults.Count; i++) ApplyJudge(missResults[i]);
        }

        noteLayer.Refresh(songTime);

        if (songTime >= endTime && judge.AllJudged) Finish();
    }

    /// <summary>
    /// 라인 키 입력 (InGameInput, 자동 플레이). 판정 범위 밖이면 빈 타격으로 아무것도 하지 않는다.
    /// </summary>
    /// <param name="inputSongTime">입력 시각 (곡 시간, 초)</param>
    private void OnLanePressed(int lane, double inputSongTime)
    {
        if (!isPlaying) return;

        if (judge.TryHit(lane, inputSongTime, out JudgeResult result))
        {
            noteLayer.OnNoteHit(result.NoteIndex);
            ApplyJudge(result);
        }
    }

    private void ApplyJudge(JudgeResult result)
    {
        score.Add(result.Type);
        canvas.ShowJudge(result.Type, score.Combo, score.Score);

        if (!result.IsHit) return;

        double error = System.Math.Abs(result.DeltaMs);
        hitCount++;
        hitErrorSumMs += error;
        if (error > hitErrorMaxMs) hitErrorMaxMs = error;
    }

    /// <summary>
    /// 곡이 끝나면 결과 씬으로 넘어간다. 돌아갈 곳은 여기서 정해 넘긴다 (결과 씬은 에디터·곡 선택을 직접 알지 않는다).
    /// </summary>
    private void Finish()
    {
        if (isFinished) return;

        isFinished = true;
        isPlaying = false;
        input.DisableInput();
        GameManager.Sound.StopMusic();

        Debug.Log($"[InGameScene] 결과: 점수 {score.Score}, 랭크 {score.Rank}, P/G/Go/M {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, " +
                  $"최대 콤보 {score.MaxCombo}/{score.TotalNotes}, 정확도 {score.Accuracy:0.00}%, FC={score.IsFullCombo}, AP={score.IsAllPerfect}, " +
                  $"판정 오차(친 노트 {hitCount}개) 평균 {(hitCount > 0 ? hitErrorSumMs / hitCount : 0):0.###}ms · 최대 {hitErrorMaxMs:0.###}ms, " +
                  $"노트 풀 {noteLayer.PooledCount}개(추가 생성 {noteLayer.GrowCount})", this);

        ResultData result = ResultData.FromScore(score);
        result.songTitle = song.Title;
        result.chartName = chart.chartName;
        result.level = chart.level;
        result.isTestPlay = IsTestPlay;
        result.retryEntry = entry.Clone();
        result.returnTarget = CreateReturnTarget();

        GameManager.Scene.Load(GameConstants.Scenes.Result, result);
    }

    /// <summary>
    /// 결과 화면 돌아가기 버튼으로 갈 곳. 테스트 플레이면 에디터(편집 상태 복원), 아니면 곡 선택(같은 곡·탭·채보·배속).
    /// </summary>
    private ResultReturnTarget CreateReturnTarget()
    {
        if (IsTestPlay)
        {
            return new ResultReturnTarget
            {
                sceneName = GameConstants.Scenes.Editor,
                sceneData = entry.testPlay.CreateEditorEntry(),
                buttonLabel = GameConstants.Play.ReturnToEditorLabel
            };
        }

        // 곡 선택으로 돌아가면 같은 곡·탭·채보·배속이 선택된 채로 시작한다
        return new ResultReturnTarget
        {
            sceneName = GameConstants.Scenes.SongSelect,
            sceneData = new SongSelectEntryData
            {
                songId = entry.songId,
                chartType = entry.chartType,
                chartId = chart.chartId,
                scrollMultiplier = entry.scrollMultiplier
            },
            buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
        };
    }

    /// <summary>
    /// ESC (팝업이 없을 때 UIManager가 부름). 테스트 플레이면 노래를 멈추고 결과 없이 에디터로 돌아간다.
    /// 보통 플레이는 아직 아무것도 하지 않는다.
    /// TODO: 일시정지 팝업을 만들면 보통 플레이는 일시정지 팝업을 연다.
    /// </summary>
    public override void OnBack()
    {
        if (!IsTestPlay || isFinished || !isReady) return;

        isFinished = true;
        isPlaying = false;
        GameManager.Sound.StopMusic();
        GameManager.Scene.Load(GameConstants.Scenes.Editor, entry.testPlay.CreateEditorEntry());
    }

#if UNITY_EDITOR
    /// <summary>
    /// 노트 시간이 된 라인에 키 입력과 같은 함수로 입력을 넣는다. 입력 시각은 지금 곡 시간이라 오차는 1프레임 이내다.
    /// </summary>
    private void RunAutoPlay(double songTime)
    {
        double inputTime = GameManager.Sound.GetSongTimeAtRealtime(Time.realtimeSinceStartupAsDouble);
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            while (isPlaying && judge.TryPeekNext(lane, out int noteIndex) && judge.Notes[noteIndex].Time <= songTime)
                OnLanePressed(lane, inputTime);
        }
    }
#endif

    private InGameEntryData CreateTestEntry()
    {
        Debug.LogWarning($"[InGameScene] 진입 데이터가 없어 테스트 값으로 시작합니다: songId={testSongId}, chartId={(string.IsNullOrEmpty(testChartId) ? "(첫 채보)" : testChartId)}", this);
        return new InGameEntryData { songId = testSongId, chartId = testChartId, chartType = testChartType, mode = InGameMode.Normal };
    }

    /// <summary>
    /// 곡과 채보를 준비한다. 테스트 플레이면 넘겨받은 채보를, 아니면 파일을 읽는다 (chartId가 비어 있으면 그 곡의 첫 채보).
    /// </summary>
    private bool TryLoad(out string error)
    {
        song = GameManager.Songs.Find(entry.songId);
        if (song == null) { error = $"곡 목록에 없는 곡입니다: {entry.songId}"; return false; }
        if (song.Clip == null) { error = $"곡에 음원이 없습니다: {entry.songId}"; return false; }

        if (IsTestPlay)
        {
            chart = entry.testPlay.chart;
            if (chart == null) { error = "테스트 플레이 채보가 없습니다."; return false; }
        }
        else
        {
            if (string.IsNullOrEmpty(entry.chartId))
            {
                List<ChartData> charts = ChartRepository.GetCharts(entry.songId, entry.chartType);
                if (charts.Count == 0) { error = $"{entry.songId}의 {ChartData.ToTypeString(entry.chartType)} 채보가 없습니다."; return false; }
                entry.chartId = charts[0].chartId;
            }

            if (!ChartRepository.TryLoad(entry.songId, entry.chartId, entry.chartType, out chart, out error)) return false;
        }

        if (chart.timingPoints == null || chart.timingPoints.Count == 0 || chart.timingPoints[0].bpm <= 0f)
        {
            error = $"BPM이 없거나 0 이하입니다: {chart.chartId}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 채보 노트를 시간(초)으로 바꾼다. 에디터와 같은 BeatGridMath 변환 (offsetMs 포함, BPM은 timingPoints[0]).
    /// 테스트 플레이면 시작 박 앞의 노트는 건너뛴다 (전체 노트 수에도 넣지 않는다).
    /// </summary>
    private List<PlayNote> BuildNotes(out int skippedLong, out int skippedBefore)
    {
        float bpm = chart.timingPoints[0].bpm;
        IReadOnlyList<NoteData> source = chart.notes;
        skippedBefore = 0;
        if (IsTestPlay)
        {
            List<NoteData> filtered = TestPlayRules.FilterNotesFrom(chart.notes, entry.testPlay.startBeat);
            skippedBefore = chart.notes.Count - filtered.Count;
            source = filtered;
        }

        List<PlayNote> notes = new List<PlayNote>(source.Count);
        skippedLong = 0;
        foreach (NoteData note in source)
        {
            if (note.type == NoteData.TypeLong)
            {
                skippedLong++;
                continue;
            }
            notes.Add(new PlayNote(note.lane, BeatGridMath.BeatToSeconds(note.beat, bpm, song.OffsetMs)));
        }
        return notes;
    }
}
