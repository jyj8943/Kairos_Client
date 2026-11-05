using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 씬 진입점 (숏노트 한 판). InGameScene.unity 루트의 "InGameScene" 오브젝트에 붙인다.
/// - 진입 데이터(곡·채보)로 채보를 불러와 노트 시간을 계산하고, 노래를 준비 시간 뒤에 예약 재생한다.
/// - 곡 시간은 GameManager.Sound.SongTime(DSP 기준). 판정은 NoteJudge, 점수는 ScoreCalculator가 맡는다.
/// - 곡이 끝나면(음원 길이와 마지막 노트 + 1초 중 늦은 쪽) 결과 씬으로 ResultData를 넘긴다.
/// - 테스트 플레이(진입 데이터에 TestPlayData)면 파일 대신 넘겨받은 채보를 쓰고, 시작 박 앞의 노트는 건너뛴다.
///   노래는 시작 위치 2초 전부터 들려준다.
/// - ESC(팝업이 없을 때 UIManager가 OnBack을 부름)나 창 포커스를 잃으면 일시정지 메뉴(Popup_Pause)를 연다.
///   멈추면 노래·곡 시간·노트·놓침 MISS·라인 키·자동 플레이가 모두 멈추고, "계속"을 누르면 3초 카운트다운 뒤 멈춘 위치에서 이어진다.
///   재시작·나가기는 결과 씬을 거치지 않는다. 나가는 곳은 CreateReturnTarget()이 진입 데이터로 정한다 (곡 선택 / 테스트 플레이면 에디터).
/// - 씬을 직접 실행하면(진입 데이터 없음) 인스펙터의 테스트 값으로 시작한다.
/// </summary>
public class InGameScene : SceneBase
{
    [Header("화면")]
    [SerializeField] private GearView gear;
    [SerializeField] private NoteLayer noteLayer;
    [SerializeField] private InGameInput input;
    [SerializeField] private Canvas_InGame canvas;
    [SerializeField] private Popup_Pause pausePopup;

    [Header("소리")]
    [Tooltip("라인 키를 누를 때마다 내는 키음 (NoteSFX). 판정과 상관없이 누를 때마다 난다")]
    [SerializeField] private AudioClip keySound;

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
    /// <summary>일시정지 메뉴가 열렸거나 재개 카운트다운 중이면 true (Update와 라인 키를 멈춘다)</summary>
    private bool isPaused;
    private Coroutine countdownRoutine;

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

        if (gear == null || noteLayer == null || input == null || canvas == null || pausePopup == null)
        {
            Debug.LogError("[InGameScene] gear, noteLayer, input, canvas, pausePopup 중 연결되지 않은 것이 있습니다.", this);
            yield break;
        }

        isPaused = false;
        countdownRoutine = null;
        pausePopup.Init(OnClickResume, OnClickRestart, OnClickExit);

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
        StopCountdown();
        if (input != null) input.DisableInput();
        if (SoundManager.HasInstance) GameManager.Sound.StopMusic();
    }

    private void Update()
    {
        if (!isPlaying || isPaused) return;

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
    /// 라인 키 입력 (InGameInput의 키 눌림 콜백, 자동 플레이). 누를 때마다 키음을 바로 내고(빈 타격 포함) 판정한다.
    /// 판정 범위 밖이면 빈 타격으로 키음만 난다. 일시정지·카운트다운 중에는 소리도 판정도 없다.
    /// </summary>
    /// <param name="inputSongTime">입력 시각 (곡 시간, 초)</param>
    private void OnLanePressed(int lane, double inputSongTime)
    {
        if (!isPlaying || isPaused) return;

        GameManager.Sound.PlaySfx(keySound);

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

    /// <summary>일시정지할 수 있으면 true. 시작하지 못했거나(채보 로딩 실패), 결과·다른 씬으로 넘어가는 중, 씬 로딩 중이면 false</summary>
    private bool CanPause => isReady && isPlaying && !isFinished && !GameManager.Scene.IsLoading;

    /// <summary>
    /// ESC (팝업이 없을 때 UIManager가 부름). 보통·테스트 플레이 모두 일시정지 메뉴를 연다.
    /// 재개 카운트다운 중이면 카운트다운을 멈추고 일시정지 메뉴를 다시 연다.
    /// </summary>
    public override void OnBack()
    {
        RequestPause("ESC");
    }

    /// <summary>
    /// 창 포커스를 잃으면 자동으로 일시정지한다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) RequestPause("창 포커스 잃음");
    }

    /// <summary>
    /// 앱이 백그라운드로 가면 자동으로 일시정지한다.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) RequestPause("앱 일시정지");
    }

    /// <summary>
    /// 일시정지 메뉴를 연다. 이미 팝업(일시정지·확인)이 열려 있으면 그대로 둔다.
    /// </summary>
    private void RequestPause(string reason)
    {
        if (!CanPause) return;
        if (UIManager.HasInstance && GameManager.UI.HasOpenPopup) return;

        bool wasCountingDown = countdownRoutine != null;
        StopCountdown();

        if (!isPaused)
        {
            isPaused = true;
            GameManager.Sound.PauseMusic();
        }

        pausePopup.Open();
        Debug.Log($"[InGameScene] 일시정지 ({reason}{(wasCountingDown ? ", 카운트다운 중단" : "")}): 곡 시간 {GameManager.Sound.SongTime:0.###}초, " +
                  $"노래 정지 {GameManager.Sound.IsMusicPaused}, 남은 노트 {judge.TotalCount - judge.JudgedCount}개", this);
    }

    /// <summary>
    /// 계속 (버튼, ESC): 일시정지 메뉴를 닫고 3초 카운트다운 뒤 멈춘 위치에서 재개한다.
    /// </summary>
    private void OnClickResume()
    {
        if (isFinished || !isPaused || countdownRoutine != null) return;

        pausePopup.Close();
        countdownRoutine = StartCoroutine(CountdownAndResume());
    }

    /// <summary>
    /// 3 → 2 → 1을 1초씩(unscaled) 보여준 뒤 노래와 곡 시간을 이어서 재개한다. 카운트다운 동안 노트·판정은 멈춰 있다.
    /// </summary>
    private IEnumerator CountdownAndResume()
    {
        for (int count = GameConstants.Pause.CountdownSeconds; count > 0; count--)
        {
            canvas.ShowCountdown(count);
            yield return new WaitForSecondsRealtime(1f);
        }

        canvas.HideCountdown();
        countdownRoutine = null;
        isPaused = false;
        GameManager.Sound.ResumeMusic();
        Debug.Log($"[InGameScene] 재개: 곡 시간 {GameManager.Sound.SongTime:0.###}초", this);
    }

    /// <summary>
    /// 카운트다운을 멈추고 숫자를 숨긴다 (일시정지 상태는 그대로).
    /// </summary>
    private void StopCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
        if (canvas != null) canvas.HideCountdown();
    }

    /// <summary>
    /// 재시작: 확인 팝업을 띄우고, 확인하면 같은 진입 데이터로 인게임을 다시 불러온다 (테스트 플레이면 같은 시작 위치부터).
    /// 취소하면 일시정지 메뉴로 돌아온다.
    /// </summary>
    private void OnClickRestart()
    {
        if (isFinished) return;

        GameManager.UI.Confirm(GameConstants.Confirm.RestartMessage, GameConstants.Confirm.RestartLabel, Restart);
    }

    private void Restart()
    {
        if (isFinished) return;

        StopForLeave();
        Debug.Log("[InGameScene] 재시작", this);
        GameManager.Scene.Load(GameConstants.Scenes.InGame, entry.Clone());
    }

    /// <summary>
    /// 나가기: 보통 플레이는 확인 팝업 뒤, 테스트 플레이는 확인 없이 이전 화면으로 간다 (결과 씬은 거치지 않는다).
    /// </summary>
    private void OnClickExit()
    {
        if (isFinished) return;

        if (IsTestPlay)
            ExitToReturnTarget();
        else
            GameManager.UI.Confirm(GameConstants.Confirm.InGameExitMessage, GameConstants.Confirm.InGameExitLabel, ExitToReturnTarget);
    }

    /// <summary>
    /// 노래를 멈추고 진입 데이터로 정한 이전 화면(곡 선택 / 에디터)으로 간다.
    /// </summary>
    private void ExitToReturnTarget()
    {
        if (isFinished) return;

        ResultReturnTarget target = CreateReturnTarget();
        StopForLeave();
        Debug.Log($"[InGameScene] 나가기 → {target.sceneName}", this);
        GameManager.Scene.Load(target.sceneName, target.sceneData);
    }

    /// <summary>
    /// 씬을 떠나기 전에 플레이를 끝내고 노래를 멈춘다 (이후 ESC·버튼·포커스 이벤트를 무시한다).
    /// </summary>
    private void StopForLeave()
    {
        isFinished = true;
        isPlaying = false;
        StopCountdown();
        input.DisableInput();
        GameManager.Sound.StopMusic();
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
            {
                // 판정 범위 밖이라 처리되지 않으면 같은 노트를 계속 보게 되므로 멈춘다 (놓침은 CollectMisses가 처리)
                int judgedBefore = judge.JudgedCount;
                OnLanePressed(lane, inputTime);
                if (judge.JudgedCount == judgedBefore) break;
            }
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
