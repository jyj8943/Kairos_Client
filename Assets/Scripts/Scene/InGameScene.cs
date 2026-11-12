using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인게임 씬 진입점 (숏노트·롱노트 한 판). InGameScene.unity 루트의 "InGameScene" 오브젝트에 붙인다.
/// - 진입 데이터(곡·채보)로 채보를 불러와 노트 시간을 계산하고, 노래를 준비 시간 뒤에 예약 재생한다.
/// - 곡 시간은 GameManager.Sound.SongTime(DSP 기준). 판정은 NoteJudge, 점수는 ScoreCalculator가 맡는다.
/// - 모든 노트가 판정(친 판정 또는 놓침 MISS)되면 OnAllNotesJudged를 한 번 알리고, 그 곡 시간부터 3초(ResultDelaySeconds) 뒤
///   결과 씬으로 ResultData를 넘긴다. 그 3초 동안 노래는 계속 흐르고, 결과로 넘어가는 중이라 일시정지는 받지 않는다.
/// - 테스트 플레이(진입 데이터에 TestPlayData)면 파일 대신 넘겨받은 채보를 쓰고, 시작 박 앞의 노트는 건너뛴다.
///   노래는 시작 위치 2초 전부터 들려준다.
/// - ESC(팝업이 없을 때 UIManager가 OnBack을 부름)나 창 포커스를 잃으면 일시정지 메뉴(Popup_Pause)를 연다.
///   멈추면 노래·곡 시간·노트·놓침 MISS·라인 키·자동 플레이가 모두 멈추고, "계속"을 누르면 3초 카운트다운 뒤 멈춘 위치에서 이어진다.
///   재시작·나가기는 결과 씬을 거치지 않는다. 나가는 곳은 CreateReturnTarget()이 진입 데이터로 정한다 (곡 선택 / 테스트 플레이면 에디터).
/// - 롱노트 (롱노트 판정 세부 규칙): 키를 뗄 때 OnLaneReleased로 끝 판정을 낸다. 판정 글자는 숏노트와 롱노트 종합 판정 때만 띄우고,
///   롱노트 시작·유지 틱은 콤보만 올린다. 키음은 누를 때와 롱노트 끝 판정 때(뗄 때, 늦게 떼 자동 GOOD이 나는 순간) 낸다.
///   일시정지 중 뗀 것은 무시하고, 재개 카운트다운이 끝나는 순간 누르고 있던 롱노트의 키가 안 눌려 있으면 그 순간 뗀 것으로 본다.
/// - 자동 플레이(에디터의 자동 테스트, 테스트 플레이 전용): 노트 시간마다 키 입력과 같은 경로(OnLanePressed)로 판정·키음을 내고
///   라인 빛남을 짧게 켰다 끈다. 롱노트는 시작 시각에 누르고 끝 시각에 뗀다(OnLaneReleased, 누르는 동안 빛남 유지).
///   자동 플레이 중에는 사용자의 라인 키를 무시한다 (ESC 일시정지는 그대로).
///   일반 플레이(곡 선택에서 들어옴)는 진입 데이터에 testPlay가 없어 자동 플레이를 켤 수 없다.
/// - 배속: 1 / 2 키(Gameplay/SpeedDown·SpeedUp)로 플레이 중에도 0.1배씩 바꾼다 (누르고 있으면 0.4초 뒤부터 0.08초마다 반복).
///   팝업(일시정지·확인)이 열려 있으면 받지 않고, 재개 카운트다운 중에는 받는다. 바꾼 배속은 진입 데이터(entry.scrollMultiplier)에 남겨
///   재시작·다시 하기·곡 선택·에디터 복귀로 이어 넘긴다 (싱글톤에 남기지 않는다).
/// - 판정 오프셋(설정): 판이 시작될 때 GameManager.Settings.OffsetMs를 한 번 읽어(appliedOffsetMs) 곡 offset에 더해 TimingMap을 만든다.
///   노트 시간 전체(화면·판정·롱노트 틱·MISS·테스트 플레이 시작 위치)가 함께 움직이고 입력 시각은 그대로다. + = 노트를 늦게, - = 빠르게.
///   일시정지 중 설정 창에서 바꾸고 닫으면 재시작을 묻는다 (OnSettingsClosed). 에디터 재생·메트로놈에는 적용하지 않는다.
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

    [Header("배속 키 (입력 에셋 Gameplay/SpeedDown = 1, Gameplay/SpeedUp = 2)")]
    [SerializeField] private InputActionReference speedDownAction;
    [SerializeField] private InputActionReference speedUpAction;

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
    [Tooltip("개발용 F1 패널 (빛기둥·키음 끄기). EditorOnly 오브젝트라 빌드에서 빠진다")]
    [SerializeField] private Canvas_InGameDebug debugPanel;
    [Tooltip("켜면 진입 데이터와 상관없이 자동 플레이로 시작한다 (싱크·점수 검증용). 씬에는 꺼 둔 채로 저장한다")]
    [SerializeField] private bool autoPlay;
#endif

    private readonly List<JudgeResult> timeoutResults = new List<JudgeResult>();

    // 배속 키 반복 (누른 순간 한 번, 0.4초 뒤부터 0.08초마다)
    private readonly KeyRepeat speedDownRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);
    private readonly KeyRepeat speedUpRepeat = new KeyRepeat(GameConstants.Play.SpeedKeyRepeatDelaySeconds, GameConstants.Play.SpeedKeyRepeatIntervalSeconds);
    private bool isSpeedInputEnabled;
    private readonly List<JudgeResult> releaseResults = new List<JudgeResult>();

    /// <summary>재개할 때 라인 키가 눌려 있는지 묻는 함수 (한 번만 만들어 둔다)</summary>
    private System.Func<int, bool> isLaneHeldForResume;

    private InGameEntryData entry;
    private SongData song;
    private ChartData chart;

    /// <summary>
    /// 채보 timingPoints와 (곡 offset + 사용자 판정 오프셋)으로 만든 박 ↔ 시간 변환. TryLoad에서 만든다.
    /// 인게임의 박 → 시간 변환(노트·롱노트 끝·유지 틱·테스트 플레이 시작 위치)은 모두 이것 하나를 쓴다.
    /// </summary>
    private TimingMap timing;

    /// <summary>
    /// 이번 판에 적용한 사용자 판정 오프셋 (ms). 판이 시작될 때(TryLoad) GameManager.Settings.OffsetMs를 한 번 읽어 둔다.
    /// 플레이 중에는 노트 시간을 다시 계산하지 않는다 (일시정지 중 바꾸면 설정 창을 닫을 때 재시작을 묻는다).
    /// </summary>
    private int appliedOffsetMs;

    /// <summary>설정 창 Closed를 구독 중이면 true</summary>
    private bool isSettingsClosedSubscribed;

    /// <summary>곡 시간 → 스크롤 거리 (노트 높이 계산용). 지금은 배속 구간이 없다</summary>
    private ScrollMap scrollMap;
    private NoteJudge judge;
    private ScoreCalculator score;
    private double startDelaySeconds;
    private double musicStartPosition;
    /// <summary>모든 노트가 판정된 곡 시간 (결과까지 기다리는 기준). 아직이면 음수</summary>
    private double allJudgedSongTime = -1;
    /// <summary>모든 노트가 판정돼 결과로 넘어가기를 기다리는 중이면 true</summary>
    private bool isWaitingResult;
#if UNITY_EDITOR
    // 싱크 진단 (Unity 에디터 전용): 곡 시간과 실제 음원 재생 위치의 차이
    private double maxSyncErrorSeconds;
    private double nextSyncWarningRealtime;
#endif

    /// <summary>
    /// 모든 노트가 판정된 순간 한 번 알린다 (결과로 넘어가기 3초 전).
    /// TODO: FULL COMBO / ALL PERFECT 연출을 이 시점에 띄운다
    /// </summary>
    public event System.Action OnAllNotesJudged;
    private bool isReady;
    private bool isPlaying;
    private bool isFinished;
    /// <summary>일시정지 메뉴가 열렸거나 재개 카운트다운 중이면 true (Update와 라인 키를 멈춘다)</summary>
    private bool isPaused;
    private Coroutine countdownRoutine;
    /// <summary>자동 플레이 중이면 true (테스트 플레이의 autoPlay, 또는 Unity 에디터의 확인용 토글)</summary>
    private bool isAutoPlay;
    /// <summary>자동 플레이가 켠 라인 빛남을 끌 시각 (realtime, 0이면 꺼져 있음)</summary>
    private readonly double[] autoGlowOffAt = new double[GameConstants.Editor.LaneCount];

    /// <summary>롱노트 끝 판정 키음을 낸 수 (확인용)</summary>
    private int longEndKeySoundCount;

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

        List<PlayNote> notes = BuildNotes(out int longCount, out int skippedBefore);

        judge = new NoteJudge(notes, GameConstants.Editor.LaneCount, judgeWindows);
        score = new ScoreCalculator(judge.TotalCount, judge.TotalTicks);
        if (isLaneHeldForResume == null) isLaneHeldForResume = IsLaneHeldForResume;

        double firstNoteTime = judge.TotalCount > 0 ? judge.Notes[0].Time : 0;
        if (IsTestPlay)
        {
            double startTime = timing.BeatToSeconds(entry.testPlay.startBeat);
            TestPlayRules.MusicStart start = TestPlayRules.GetMusicStart(startTime);
            musicStartPosition = start.StartPositionSeconds;
            startDelaySeconds = start.DelaySeconds;
        }
        else
        {
            musicStartPosition = 0;
            startDelaySeconds = System.Math.Max(GameConstants.Play.MinStartDelaySeconds, GameConstants.Play.LeadInSeconds - firstNoteTime);
        }
        allJudgedSongTime = -1;
        isWaitingResult = false;
#if UNITY_EDITOR
        maxSyncErrorSeconds = 0;
        nextSyncWarningRealtime = 0;
#endif

        gear.Init();
#if UNITY_EDITOR
        if (debugPanel != null) debugPanel.Init(gear);
#endif
        scrollMap = new ScrollMap();
        noteLayer.Init(gear, judge, scrollMap, entry.scrollMultiplier);
        isAutoPlay = IsTestPlay && entry.testPlay.autoPlay;
#if UNITY_EDITOR
        if (autoPlay) isAutoPlay = true;
#endif
        System.Array.Clear(autoGlowOffAt, 0, autoGlowOffAt.Length);

        input.Init(OnUserLanePressed, OnUserLaneReleased, OnUserLaneHeld);
        canvas.Init(song.Title, chart.chartName, chart.level, IsTestPlay, isAutoPlay, noteLayer.ScrollMultiplier);
        entry.scrollMultiplier = noteLayer.ScrollMultiplier;

        Debug.Log($"[InGameScene] 시작{(IsTestPlay ? " (테스트 플레이)" : "")}{(isAutoPlay ? " (자동 플레이)" : "")}: songId={song.SongId}, chartId={chart.chartId ?? "(저장 안 한 새 채보)"}, " +
                  $"노트 {judge.TotalCount}개 (롱노트 {longCount}개, 유지 틱 {judge.TotalTicks}개 → 가능한 최대 콤보 {score.MaxPossibleCombo})" +
                  $"{(IsTestPlay ? $" (시작 {entry.testPlay.startBeat}박 앞 {skippedBefore}개 제외)" : "")}, " +
                  $"bpm={timing.BpmAt(0)} (BPM 구간 {timing.PointCount}개), offsetMs={timing.OffsetMs} (곡 {song.OffsetMs} + 사용자 판정 오프셋 {appliedOffsetMs:+0;-0;0}), 음원 시작 위치 {musicStartPosition:0.###}초, 시작 지연 {startDelaySeconds:0.###}초, " +
                  $"첫 노트 {firstNoteTime:0.###}초, 노트 풀 {noteLayer.PooledCount}개 (롱노트 {noteLayer.PooledLongCount}개), " +
                  $"배속 x{noteLayer.ScrollMultiplier:0.0} → 낙하 속도 {noteLayer.ScrollSpeed:0.###}유닛/초 (1.0배 {noteLayer.BaseScrollSpeed:0.###}유닛/초 = 화면 위 끝~판정선 {gear.ScreenTopY - gear.JudgeLineY:0.###}유닛 ÷ {GameConstants.Play.BaseVisibleSeconds}초)", this);

        isReady = true;
    }

    /// <summary>
    /// 노래를 예약하고 입력을 받기 시작한다.
    /// </summary>
    protected override void OnEnter()
    {
        if (!isReady) return;

        // 앞 씬(에디터 메트로놈 등)이 예약해 둔 효과음이 남아 울리지 않게 모두 취소한다
        GameManager.Sound.CancelScheduledSfx();
        GameManager.Sound.PlayMusicScheduled(song.Clip, startDelaySeconds, musicStartPosition);
        input.EnableInput();
        EnableSpeedInput();
        SubscribeSettingsClosed();
#if UNITY_EDITOR
        if (debugPanel != null) debugPanel.EnableInput();
#endif
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
        DisableSpeedInput();
        UnsubscribeSettingsClosed();
#if UNITY_EDITOR
        if (debugPanel != null) debugPanel.DisableInput();
#endif
        if (SoundManager.HasInstance) GameManager.Sound.StopMusic();
    }

    /// <summary>
    /// 인게임 도중 Play를 멈추거나 PlayMode 테스트가 인게임에서 끝나면 OnExit가 불리지 않는다.
    /// 입력 에셋의 액션은 도메인 리로드를 끈 설정에서 Play를 넘어 남으므로, 여기서도 배속 키 구독을 끊는다 (이미 끊었으면 아무것도 하지 않는다).
    /// 라인 키(InGameInput)·개발용 패널(Canvas_InGameDebug)은 각자 OnDestroy에서 끊는다.
    /// </summary>
    private void OnDestroy()
    {
        DisableSpeedInput();
        UnsubscribeSettingsClosed();
    }

    // ───── 일시정지 중 판정 오프셋 변경 ─────

    /// <summary>설정 창 Closed를 구독한다 (OnEnter). 설정 창은 오버레이 Canvas에 있어 씬을 넘어 남으므로 OnExit·OnDestroy에서 끊는다.</summary>
    private void SubscribeSettingsClosed()
    {
        if (isSettingsClosedSubscribed || !UIManager.HasInstance) return;
        Popup_Settings settings = GameManager.UI.Get<Popup_Settings>();
        if (settings == null) return;

        settings.Closed += OnSettingsClosed;
        isSettingsClosedSubscribed = true;
    }

    /// <summary>설정 창 Closed 구독을 끊는다 (이미 끊었으면 아무것도 하지 않는다).</summary>
    private void UnsubscribeSettingsClosed()
    {
        if (!isSettingsClosedSubscribed) return;
        isSettingsClosedSubscribed = false;
        if (!UIManager.HasInstance) return;

        Popup_Settings settings = GameManager.UI.Get<Popup_Settings>();
        if (settings != null) settings.Closed -= OnSettingsClosed;
    }

    /// <summary>
    /// 설정 창이 닫혔을 때: 일시정지 중이고 판정 오프셋이 이번 판 값(appliedOffsetMs)과 다르면 재시작을 묻는다.
    /// 확인하면 일시정지 메뉴 "재시작"과 같은 경로로 바로 재시작한다 (재시작 확인은 다시 묻지 않는다).
    /// 취소(ESC 포함)하면 일시정지 메뉴로 돌아가고, 바꾼 값은 그대로 두어 다음 판부터 적용된다.
    /// 바꿨다가 되돌려 값이 같으면 묻지 않는다.
    /// </summary>
    private void OnSettingsClosed()
    {
        if (isFinished || !isPaused || !pausePopup.IsOpen) return;
        if (!SettingsManager.HasInstance) return;

        int current = GameManager.Settings.OffsetMs;
        if (current == appliedOffsetMs) return;

        Debug.Log($"[InGameScene] 일시정지 중 판정 오프셋 변경: 이번 판 {appliedOffsetMs:+0;-0;0}ms → 설정 {current:+0;-0;0}ms, 재시작 확인", this);
        GameManager.UI.Confirm(GameConstants.Confirm.OffsetRestart, GameConstants.Confirm.OffsetRestartLabel,
            Restart, OnOffsetRestartCanceled);
    }

    /// <summary>오프셋 재시작 확인 취소: 일시정지 메뉴로 돌아간다 (확인 팝업이 이전 선택을 되돌린다).</summary>
    private void OnOffsetRestartCanceled()
    {
        Debug.Log($"[InGameScene] 오프셋 재시작 취소: 이번 판은 {appliedOffsetMs:+0;-0;0}ms 그대로, 다음 판부터 " +
                  $"{(SettingsManager.HasInstance ? GameManager.Settings.OffsetMs : 0):+0;-0;0}ms", this);
    }

    private void Update()
    {
        // 배속 키 반복은 재개 카운트다운 중에도 받는다 (팝업이 열려 있으면 ChangeSpeed가 무시한다)
        if (isPlaying) UpdateSpeedRepeat();

        if (!isPlaying || isPaused) return;

        double songTime = GameManager.Sound.SongTime;

        if (isAutoPlay)
        {
            RunAutoPlay(songTime);
            UpdateAutoGlows(false);
        }

        // 놓친 MISS, 누르고 있는 롱노트의 유지 틱, 늦게 떼 자동 GOOD
        if (judge.CollectTimeouts(songTime, timeoutResults) > 0)
        {
            for (int i = 0; i < timeoutResults.Count; i++) ApplyJudge(timeoutResults[i]);
        }

        noteLayer.Refresh(songTime);

#if UNITY_EDITOR
        CheckSync(songTime);
#endif

        if (!isWaitingResult && judge.AllJudged)
        {
            isWaitingResult = true;
            allJudgedSongTime = songTime;
            Debug.Log($"[InGameScene] 모든 노트 판정: 곡 시간 {songTime:0.###}초, {GameConstants.Play.ResultDelaySeconds}초 뒤 결과로", this);
            // TODO: FULL COMBO / ALL PERFECT 연출 (OnAllNotesJudged를 받아 띄운다)
            OnAllNotesJudged?.Invoke();
        }

        if (isWaitingResult && songTime >= allJudgedSongTime + GameConstants.Play.ResultDelaySeconds) Finish();
    }

    /// <summary>
    /// 사용자의 라인 키 눌림 (InGameInput). 자동 플레이 중에는 무시한다.
    /// </summary>
    private void OnUserLanePressed(int lane, double inputSongTime)
    {
        if (isAutoPlay) return;
        OnLanePressed(lane, inputSongTime);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터, 빌드에서 빠진다): 판정 결과가 점수에 들어가기 직전마다 알린다 (시작·틱·종합 판정을 테스트가 기록하게).
    /// </summary>
    public System.Action<JudgeResult> TestJudgeObserver;

    /// <summary>
    /// 테스트 전용 (Unity 에디터): 라인 키 누름을 사용자 입력과 같은 경로(OnUserLanePressed)로 넣는다. 입력 시각은 넘긴 곡 시간을 그대로 쓴다.
    /// 테스트 어셈블리(Assembly-CSharp-Editor)에서 부를 수 있게 public이다.
    /// </summary>
    public void TestPress(int lane, double songTime)
    {
        OnUserLanePressed(lane, songTime);
    }

    /// <summary>
    /// 테스트 전용 (Unity 에디터): 라인 키 뗌을 사용자 입력과 같은 경로(OnUserLaneReleased)로 넣는다.
    /// </summary>
    public void TestRelease(int lane, double songTime)
    {
        OnUserLaneReleased(lane, songTime);
    }
#endif

    /// <summary>배속 키 구독을 시작한다 (OnEnter). 액션은 여러 곳이 같이 쓰므로 Disable하지 않고 구독만 끊는다.</summary>
    private void EnableSpeedInput()
    {
        if (isSpeedInputEnabled) return;
        if (speedDownAction == null || speedUpAction == null)
        {
            Debug.LogWarning("[InGameScene] speedDownAction·speedUpAction이 연결되지 않아 배속 키를 받지 않습니다.", this);
            return;
        }

        speedDownAction.action.started += OnSpeedDownStarted;
        speedDownAction.action.canceled += OnSpeedDownCanceled;
        speedUpAction.action.started += OnSpeedUpStarted;
        speedUpAction.action.canceled += OnSpeedUpCanceled;
        speedDownAction.action.Enable();
        speedUpAction.action.Enable();
        isSpeedInputEnabled = true;
    }

    /// <summary>배속 키 구독을 끊는다 (OnExit).</summary>
    private void DisableSpeedInput()
    {
        speedDownRepeat.Release();
        speedUpRepeat.Release();
        if (!isSpeedInputEnabled) return;

        speedDownAction.action.started -= OnSpeedDownStarted;
        speedDownAction.action.canceled -= OnSpeedDownCanceled;
        speedUpAction.action.started -= OnSpeedUpStarted;
        speedUpAction.action.canceled -= OnSpeedUpCanceled;
        isSpeedInputEnabled = false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터): 배속 키 콜백(누름)이 불린 횟수. 모든 InGameScene 인스턴스가 같이 센다 (Play 종료 뒤 남은 구독 확인용).
    /// 도메인 리로드를 끈 설정이라 Play를 넘어 값이 남으므로 테스트가 직접 0으로 맞춘다.
    /// </summary>
    public static int TestSpeedKeyCallbackCount;
#endif

    private void OnSpeedDownStarted(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        TestSpeedKeyCallbackCount++;
#endif
        PressSpeedKey(speedDownRepeat, -1);
    }

    private void OnSpeedUpStarted(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        TestSpeedKeyCallbackCount++;
#endif
        PressSpeedKey(speedUpRepeat, 1);
    }
    private void OnSpeedDownCanceled(InputAction.CallbackContext context) => speedDownRepeat.Release();
    private void OnSpeedUpCanceled(InputAction.CallbackContext context) => speedUpRepeat.Release();

    /// <summary>배속 키를 누른 순간: 한 번 바꾸고 반복을 시작한다. 팝업이 열려 있으면 무시한다 (반복도 시작하지 않는다).</summary>
    private void PressSpeedKey(KeyRepeat repeat, int delta)
    {
        if (!CanChangeSpeed) return;

        repeat.Press(Time.realtimeSinceStartupAsDouble);
        ChangeSpeed(delta);
    }

    /// <summary>누르고 있는 배속 키의 반복 (매 프레임).</summary>
    private void UpdateSpeedRepeat()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        int down = speedDownRepeat.Tick(now);
        int up = speedUpRepeat.Tick(now);
        if (down + up == 0 || !CanChangeSpeed) return;
        ChangeSpeed(up - down);
    }

    /// <summary>배속을 바꿀 수 있으면 true. 팝업(일시정지·확인)이 열려 있으면 false (재개 카운트다운 중에는 true)</summary>
    private bool CanChangeSpeed => isReady && isPlaying && !isFinished && !(UIManager.HasInstance && GameManager.UI.HasOpenPopup);

    /// <summary>
    /// 배속을 delta 단계(0.1배)만큼 바꾼다. 범위(1.0 ~ 8.0) 끝에서는 멈춘다.
    /// 노트 낙하 속도에 바로 반영하고, 진입 데이터에 남겨 재시작·다시 하기·곡 선택·에디터 복귀로 넘긴다.
    /// </summary>
    private void ChangeSpeed(int delta)
    {
        if (delta == 0 || !CanChangeSpeed) return;

        float next = ScrollSpeedRules.StepMultiplier(noteLayer.ScrollMultiplier, delta);
        if (Mathf.Approximately(next, noteLayer.ScrollMultiplier)) return;

        noteLayer.SetScrollMultiplier(next);
        entry.scrollMultiplier = noteLayer.ScrollMultiplier;
        canvas.SetSpeed(noteLayer.ScrollMultiplier, true);
        Debug.Log($"[InGameScene] 배속 x{noteLayer.ScrollMultiplier:0.0} → 낙하 속도 {noteLayer.ScrollSpeed:0.###}유닛/초 (보이는 시간 {ScrollSpeedRules.VisibleSeconds(noteLayer.ScrollMultiplier):0.###}초)", this);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터): 배속 키를 한 번 눌렀다 뗀 것과 같다 (키 콜백과 같은 PressSpeedKey 경로, 팝업 확인 포함).
    /// </summary>
    /// <param name="up">true면 2(올림), false면 1(내림)</param>
    public void TestTapSpeedKey(bool up)
    {
        KeyRepeat repeat = up ? speedUpRepeat : speedDownRepeat;
        PressSpeedKey(repeat, up ? 1 : -1);
        repeat.Release();
    }
#endif

    /// <summary>
    /// 사용자의 라인 키 뗌 (InGameInput). 자동 플레이 중에는 무시한다.
    /// </summary>
    private void OnUserLaneReleased(int lane, double releaseSongTime)
    {
        if (isAutoPlay) return;
        OnLaneReleased(lane, releaseSongTime);
    }

    /// <summary>
    /// 사용자의 라인 키 누름·뗌 (라인 빛남). 자동 플레이 중에는 자동 플레이가 빛남을 맡으므로 무시한다.
    /// </summary>
    private void OnUserLaneHeld(int lane, bool pressed)
    {
        if (isAutoPlay) return;
        gear.SetPressed(lane, pressed);
    }

    /// <summary>
    /// 라인 키 입력 (InGameInput의 키 눌림 콜백, 자동 플레이). 누를 때마다 키음을 바로 내고(빈 타격 포함) 판정한다.
    /// 판정 범위 밖이면 빈 타격으로 키음만 난다. 일시정지·카운트다운 중에는 소리도 판정도 없다.
    /// </summary>
    /// <param name="inputSongTime">입력 시각 (곡 시간, 초)</param>
    private void OnLanePressed(int lane, double inputSongTime)
    {
        if (!isPlaying || isPaused) return;

        // 개발용 F1 패널에서 키음을 끄면 소리만 내지 않는다 (판정은 그대로)
        if (DevPlaySettings.KeySoundEnabled) GameManager.Sound.PlaySfx(keySound);

        if (judge.TryHit(lane, inputSongTime, out JudgeResult result))
        {
            // 롱노트 시작(LongStart)은 누르는 동안 계속 그리고, 시작 MISS는 회색으로 내려보낸다
            if (result.Kind == JudgeKind.Short) noteLayer.OnNoteHit(result.NoteIndex);
            ApplyJudge(result);
        }
    }

    /// <summary>
    /// 라인 키 뗌 (InGameInput의 키 뗌 콜백, 자동 플레이). 누르고 있던 롱노트가 있으면 뗀 시각까지의 틱과 끝·종합 판정을 낸다.
    /// 일시정지·카운트다운 중에 뗀 것은 무시한다 (재개하는 순간 키 상태로 다시 본다, 세부 규칙 5).
    /// </summary>
    /// <param name="releaseSongTime">뗀 시각 (곡 시간, 초)</param>
    private void OnLaneReleased(int lane, double releaseSongTime)
    {
        if (!isPlaying || isPaused) return;

        releaseResults.Clear();
        if (!judge.TryRelease(lane, releaseSongTime, releaseResults)) return;

        for (int i = 0; i < releaseResults.Count; i++) ApplyJudge(releaseResults[i]);
    }

    /// <summary>
    /// 판정 결과를 점수·화면·소리에 반영한다.
    /// - 판정 글자는 숏노트와 롱노트 종합 판정 때만, 롱노트 시작·틱은 콤보·점수만 바꾼다.
    /// - 롱노트 끝 판정(시작 MISS는 끝 판정이 없어 빼고)은 키음을 내고 노트를 바로 돌려놓는다.
    /// </summary>
    private void ApplyJudge(JudgeResult result)
    {
#if UNITY_EDITOR
        TestJudgeObserver?.Invoke(result);
#endif
        score.Apply(result);
        if (result.ShowsJudgeText) canvas.ShowJudge(result.Type, score.Combo, score.Score);
        else canvas.ShowCombo(score.Combo, score.Score);

        if (result.Kind == JudgeKind.LongFinal && judge.GetLongState(result.NoteIndex) == LongNoteState.Done)
        {
            // 끝 판정 키음: 뗄 때, 또는 끝 + GOOD 범위에서 자동 GOOD이 나는 순간
            if (DevPlaySettings.KeySoundEnabled) GameManager.Sound.PlaySfx(keySound);
            noteLayer.OnNoteHit(result.NoteIndex);
            longEndKeySoundCount++;
        }

        // 판정 오차 기록은 누른 판정(숏노트, 롱노트 시작)만 센다
        if (!result.IsHit || (result.Kind != JudgeKind.Short && result.Kind != JudgeKind.LongStart)) return;

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
        double finishSongTime = GameManager.Sound.SongTime;
        GameManager.Sound.StopMusic();

        Debug.Log($"[InGameScene] 결과 (모든 노트 판정 뒤 곡 시간 {finishSongTime - allJudgedSongTime:0.000}초): 점수 {score.Score}, 랭크 {score.Rank}, P/G/Go/M {score.PerfectCount}/{score.GreatCount}/{score.GoodCount}/{score.MissCount}, " +
                  $"최대 콤보 {score.MaxCombo}/{score.MaxPossibleCombo} (노트 {score.TotalNotes} + 유지 틱 {score.TotalTicks}), 롱노트 끝 키음 {longEndKeySoundCount}번, 정확도 {score.Accuracy:0.00}%, FC={score.IsFullCombo}, AP={score.IsAllPerfect}, " +
                  $"판정 오차(친 노트 {hitCount}개) 평균 {(hitCount > 0 ? hitErrorSumMs / hitCount : 0):0.###}ms · 최대 {hitErrorMaxMs:0.###}ms, " +
                  $"노트 풀 {noteLayer.PooledCount}개(추가 생성 {noteLayer.GrowCount})", this);
#if UNITY_EDITOR
        Debug.Log($"[InGameScene] 싱크 진단: 곡 시간과 음원 재생 위치 차이 최대 {maxSyncErrorSeconds * 1000:0.#}ms (경고 기준 {GameConstants.Play.SyncWarningSeconds * 1000:0}ms)", this);
#endif

        ResultData result = ResultData.FromScore(score);
        result.songTitle = song.Title;
        result.chartName = chart.chartName;
        result.level = chart.level;
        result.isTestPlay = IsTestPlay;
        result.isAutoPlay = isAutoPlay;
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
                sceneData = entry.testPlay.CreateEditorEntry(entry.scrollMultiplier),
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
    private bool CanPause => isReady && isPlaying && !isFinished && !isWaitingResult && !GameManager.Scene.IsLoading;

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
        if (isAutoPlay) UpdateAutoGlows(true);

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

        // 누르고 있던 롱노트: 카운트다운이 끝난 이 순간 키가 안 눌려 있으면 지금 뗀 것으로 본다 (카운트다운 중 누름·뗌은 보지 않는다)
        double resumeSongTime = GameManager.Sound.SongTime;
        releaseResults.Clear();
        int released = judge.ResolveHeldOnResume(resumeSongTime, isLaneHeldForResume, releaseResults);
        for (int i = 0; i < releaseResults.Count; i++) ApplyJudge(releaseResults[i]);

        Debug.Log($"[InGameScene] 재개: 곡 시간 {resumeSongTime:0.###}초{(released > 0 ? $", 키를 뗀 롱노트 {released}개 끝 판정" : "")}", this);
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

    /// <summary>
    /// 자동 플레이: 노트 시간이 된 라인에 키 입력과 같은 함수(OnLanePressed)로 입력을 넣어 판정·키음을 내고,
    /// 라인 빛남을 짧게(GameConstants.Play.AutoPlayGlowSeconds) 켠다. 입력 시각은 지금 곡 시간이라 오차는 1프레임 이내다.
    /// 롱노트는 끝 시간이 되면 OnLaneReleased로 떼고, 누르는 동안은 라인 빛남을 켜 둔다.
    /// </summary>
    private void RunAutoPlay(double songTime)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double inputTime = GameManager.Sound.GetSongTimeAtRealtime(now);
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            // 누르고 있는 롱노트: 끝 시간이 되면 뗀다. 그 전에는 빛남을 계속 켜 둔다 (일시정지로 꺼졌을 때도 다시 켠다)
            if (judge.TryGetHeld(lane, out int heldIndex))
            {
                if (judge.Notes[heldIndex].EndTime > songTime)
                {
                    gear.SetPressed(lane, true);
                    autoGlowOffAt[lane] = double.MaxValue;
                    continue;
                }

                OnLaneReleased(lane, inputTime);
                autoGlowOffAt[lane] = now + GameConstants.Play.AutoPlayGlowSeconds;
            }

            while (isPlaying && judge.TryPeekNext(lane, out int noteIndex) && judge.Notes[noteIndex].Time <= songTime)
            {
                // 판정 범위 밖이라 처리되지 않으면 같은 노트를 계속 보게 되므로 멈춘다 (놓침은 CollectTimeouts가 처리)
                OnLanePressed(lane, inputTime);
                if (judge.TryPeekNext(lane, out int after) && after == noteIndex) break;

                gear.SetPressed(lane, true);
                bool holding = judge.TryGetHeld(lane, out _);
                autoGlowOffAt[lane] = holding ? double.MaxValue : now + GameConstants.Play.AutoPlayGlowSeconds;
                if (holding) break;
            }
        }
    }

    /// <summary>재개할 때 라인 키가 눌려 있는지. 자동 플레이는 누르고 있던 롱노트를 계속 누르고 있는 것으로 본다.</summary>
    private bool IsLaneHeldForResume(int lane)
    {
        return isAutoPlay || input.IsLanePressed(lane);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 싱크 진단 (Unity 에디터 전용): 곡 시간(MusicClock)과 실제 음원 재생 위치를 같은 순간 값으로 비교한다.
    /// 차이가 SyncWarningSeconds보다 크면 경고 로그를 남긴다 (같은 판에서 SyncWarningIntervalSeconds마다 한 번).
    /// 노래 시작 전, 음원이 멈췄을 때, 재개 직후 곡 시간을 붙잡아 두는 동안은 비교하지 않는다.
    /// </summary>
    private void CheckSync(double songTime)
    {
        SoundManager sound = GameManager.Sound;
        if (!sound.IsMusicSourcePlaying || sound.IsMusicPaused || sound.IsClockHolding) return;
        if (songTime < musicStartPosition) return;

        double now = Time.realtimeSinceStartupAsDouble;
        double clockTime = sound.GetSongTimeAtRealtime(now);
        double sourceTime = sound.MusicSourceTime;
        double error = sourceTime - clockTime;
        if (System.Math.Abs(error) > maxSyncErrorSeconds) maxSyncErrorSeconds = System.Math.Abs(error);

        if (System.Math.Abs(error) <= GameConstants.Play.SyncWarningSeconds || now < nextSyncWarningRealtime) return;

        nextSyncWarningRealtime = now + GameConstants.Play.SyncWarningIntervalSeconds;
        Debug.LogWarning($"[InGameScene] 싱크 진단: 곡 시간 {clockTime:0.000}초, 음원 위치 {sourceTime:0.000}초, 차이 {error * 1000:0}ms " +
                         $"(음원 시작 위치 {musicStartPosition:0.###}초, 테스트 플레이 {IsTestPlay}, 자동 플레이 {isAutoPlay})", this);
    }
#endif

    /// <summary>
    /// 자동 플레이가 켠 라인 빛남을 시간이 되면 끈다.
    /// </summary>
    /// <param name="forceOff">true면 시간과 상관없이 모두 끈다 (일시정지할 때)</param>
    private void UpdateAutoGlows(bool forceOff)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        for (int lane = 0; lane < autoGlowOffAt.Length; lane++)
        {
            if (autoGlowOffAt[lane] <= 0 || (!forceOff && now < autoGlowOffAt[lane])) continue;

            gear.SetPressed(lane, false);
            autoGlowOffAt[lane] = 0;
        }
    }

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

        // 사용자 판정 오프셋: + = 노트를 늦게, - = 노트를 빠르게. 노트 시간 전체(화면·판정·롱노트 틱·MISS)가 함께 움직이고 입력 시각은 그대로다
        appliedOffsetMs = SettingsManager.HasInstance ? GameManager.Settings.OffsetMs : 0;

        // BPM 구간이 없거나 0 이하인 구간이 있으면 TimingMap이 ArgumentException을 던진다
        try
        {
            timing = TimingMap.FromChart(chart, song.OffsetMs + appliedOffsetMs);
        }
        catch (System.ArgumentException e)
        {
            error = $"BPM이 없거나 0 이하입니다: {chart.chartId} ({e.Message})";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 채보 노트를 시간(초)으로 바꾼다. 에디터와 같은 TimingMap 변환 (곡 offsetMs + 사용자 판정 오프셋, timingPoints 전체 구간).
    /// 롱노트는 끝 시간과 유지 틱 시간(LongNoteTicks, 시작 + 1/4박 ~ 끝 − 1/4박 이하)도 같이 계산한다.
    /// 테스트 플레이면 시작 박 앞의 노트(시작이 앞인 롱노트 포함)는 건너뛴다 (전체 노트 수에도 넣지 않는다).
    /// </summary>
    private List<PlayNote> BuildNotes(out int longCount, out int skippedBefore)
    {
        IReadOnlyList<NoteData> source = chart.notes;
        skippedBefore = 0;
        if (IsTestPlay)
        {
            List<NoteData> filtered = TestPlayRules.FilterNotesFrom(chart.notes, entry.testPlay.startBeat);
            skippedBefore = chart.notes.Count - filtered.Count;
            source = filtered;
        }

        List<PlayNote> notes = new List<PlayNote>(source.Count);
        longCount = 0;
        foreach (NoteData note in source)
        {
            if (note.type == NoteData.TypeLong)
            {
                longCount++;
                notes.Add(new PlayNote(note.lane, timing.BeatToSeconds(note.beat), timing.BeatToSeconds(note.endBeat),
                    LongNoteTicks.GetTickTimes(timing, note.beat, note.endBeat)));
                continue;
            }
            notes.Add(new PlayNote(note.lane, timing.BeatToSeconds(note.beat)));
        }
        return notes;
    }
}
