using System.Collections;
using UnityEngine;

/// <summary>
/// 결과 씬 진입점. ResultScene.unity 루트의 "ResultScene" 오브젝트에 붙인다.
/// - 인게임이 넘긴 ResultData를 Canvas_Result로 보여준다.
/// - 들어올 때 기록을 갱신·저장하고(GameManager.PlayerData.SubmitResult, 테스트·자동 플레이는 기록하지 않음) 신기록 여부를 결과에 넣는다.
///   일시정지 → 나가기·재시작으로 끝난 판은 이 씬을 거치지 않으므로 기록되지 않는다.
/// - 다시 하기: 같은 InGameEntryData로 곡 선택 시작과 같은 경로(InGameLauncher.Start)로 인게임에 들어간다 (테스트 플레이면 같은 시작 위치).
/// - 키: F5 다시 하기 · Enter 돌아가기는 Canvas_Result가, ESC 돌아가기는 OnBack이 받는다 (기획서 2-6).
/// - 돌아가기: ResultData의 돌아갈 곳(씬 이름·데이터)대로 이동한다. 이 씬은 에디터·곡 선택 씬을 직접 알지 않는다.
/// - 등장 연출 (11/20, 기획서 3-5): 일반 플레이 결과만. Init에서 0초 상태(값 요소 숨김)를 만들고, 암전 로딩이 다 걷힌 OnEnter에서 시작한다 (3.9초).
///   테스트 플레이 · 자동 테스트 결과는 연출 없이 Init에서 바로 끝 상태다 (구분은 ResultData.isTestPlay, 진입 데이터로 넘어온 값).
///   연출 중 Enter · Esc · F5 · 버튼 클릭은 건너뛰기만 한다 (TryConsumeSkip). 건너뛴 프레임의 다른 입력도 쓰지 않는다 (다시 눌러야 이동).
/// - 씬을 직접 실행하면(진입 데이터 없음) 인스펙터의 가짜 결과로 화면을 확인한다.
/// </summary>
public class ResultScene : SceneBase
{
    [SerializeField] private Canvas_Result canvas;

    [Header("테스트 값 (씬을 직접 실행해 진입 데이터가 없을 때 보여줄 가짜 결과)")]
    [SerializeField] private string testSongTitle = "Critical Velocity";
    [SerializeField] private string testChartName = "테스트 채보";
    [SerializeField] private int testLevel = 7;
    [Tooltip("PERFECT · GREAT · GOOD · MISS 개수")]
    [SerializeField] private int[] testJudgeCounts = { 6, 1, 0, 0 };
    [SerializeField] private bool testIsTestPlay;

    private ResultData result;
    private bool isReady;
    /// <summary>등장 연출을 건너뛴 프레임 (그 프레임의 다른 입력은 쓰지 않는다)</summary>
    private int skippedFrame = -1;

#if UNITY_EDITOR
    /// <summary>
    /// 테스트 전용 (Unity 에디터): true면 일반 플레이 결과도 등장 연출 없이 끝 상태로 시작한다 (결과에 들를 때마다 3.9초 늘지 않게).
    /// TestSpeedRunCallback이 테스트 실행 동안 켜고, 결과 등장 연출 테스트만 끈다.
    /// </summary>
    public static bool SkipResultIntroForTests;
#endif

    private static bool IsIntroSkippedForTests
    {
        get
        {
#if UNITY_EDITOR
            return SkipResultIntroForTests;
#else
            return false;
#endif
        }
    }

    /// <summary>결과 화면 (확인용)</summary>
    public Canvas_Result Canvas => canvas;

    /// <summary>
    /// 결과를 받아 화면을 채운다.
    /// </summary>
    protected override IEnumerator Init()
    {
        isReady = false;

        if (canvas == null)
        {
            Debug.LogError("[ResultScene] canvas가 연결되지 않았습니다.", this);
            yield break;
        }

        ResultData taken = GameManager.Scene.TakeSceneData<ResultData>();
        result = taken ?? CreateTestResult();
        if (taken != null) SubmitRecord(result);
        canvas.Init(result, Retry, Return, TryConsumeSkip);
        skippedFrame = -1;
        // 일반 플레이 결과만 등장 연출 (0초 상태로 두고 OnEnter에서 시작). 테스트 플레이 · 자동 테스트는 바로 끝 상태
        canvas.PrepareIntro(!result.isTestPlay && !IsIntroSkippedForTests);

        Debug.Log($"[ResultScene] 결과: 점수 {result.score}, 랭크 {result.rank}, 노트 {result.totalNotes}개, " +
                  $"테스트 플레이 {result.isTestPlay}, 신기록 점수 {result.isNewBestScore}·정확도 {result.isNewBestAccuracy}·콤보 {result.isNewMaxCombo}{(result.hadPreviousRecord ? $" (이전 최고 {result.previousBestScore})" : "")}, " +
                  $"돌아갈 곳 {result.returnTarget?.sceneName ?? "(없음)"}", this);
        isReady = true;
    }

    /// <summary>
    /// 기록을 갱신·저장하고 항목별 신기록 여부(점수·정확도·최대 콤보)·이전 최고 점수를 결과에 넣는다 (다시 하기 데이터의 채보 기준).
    /// </summary>
    private void SubmitRecord(ResultData data)
    {
        if (!PlayerDataManager.HasInstance) return;

        RecordUpdateResult update = GameManager.PlayerData.SubmitResult(data.retryEntry, data);
        data.isNewRecord = update.isRecorded && update.isNewRecord;
        data.isNewBestScore = update.isRecorded && update.isNewBestScore;
        data.isNewBestAccuracy = update.isRecorded && update.isNewBestAccuracy;
        data.isNewMaxCombo = update.isRecorded && update.isNewMaxCombo;
        data.hadPreviousRecord = update.hadPreviousRecord;
        data.previousBestScore = update.previousBestScore;
    }

    /// <summary>
    /// 암전 로딩이 다 걷힌 뒤(닫기 끝) 불린다: 결과 화면 등장 애니메이션을 시작하고 F5·Enter 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
        if (!isReady) return;
        PlayEnterAnimation();
        canvas.EnableInput();
    }

    /// <summary>
    /// 결과 화면 등장 애니메이션 자리 (기획서 3-10 「암전 로딩」: 걷힌 뒤 OnEnter에서 시작). 애니메이션은 나중에 정함 — 지금은 비어 있고 바로 입력을 받는다.
    /// </summary>
    private void PlayEnterAnimation()
    {
        if (canvas.IsIntroPending) canvas.PlayIntro();
    }

    /// <summary>
    /// 버튼 · 키 입력을 처리하기 전에 부른다 (Canvas_Result 버튼 · F5 · Enter, OnBack의 ESC).
    /// 등장 연출 중이면 끝 상태로 건너뛰고 true (이 입력은 이동에 쓰지 않는다). 건너뛴 그 프레임의 다른 입력도 true.
    /// </summary>
    public bool TryConsumeSkip()
    {
        if (skippedFrame == Time.frameCount) return true;
        if (canvas == null || !canvas.IsIntroPlaying) return false;
        canvas.SkipIntro();
        skippedFrame = Time.frameCount;
        return true;
    }

    /// <summary>
    /// 씬을 떠나기 직전 입력 구독을 해제한다.
    /// </summary>
    public override void OnExit()
    {
        if (canvas != null) canvas.DisableInput();
    }

    /// <summary>
    /// ESC (팝업이 없을 때 UIManager가 부름): 돌아가기 (곡 선택 / 테스트 플레이면 에디터).
    /// </summary>
    public override void OnBack()
    {
        if (!isReady || canvas == null) return;
        if (TryConsumeSkip()) return;
        canvas.RequestReturn();
    }

    private void Retry()
    {
        if (result.retryEntry == null)
        {
            Debug.LogWarning("[ResultScene] 다시 하기 데이터가 없습니다 (가짜 결과로 실행한 경우).", this);
            return;
        }

        // 곡 선택 시작과 같은 경로 (나중에 곡 로딩 화면을 넣으면 함께 거친다)
        if (InGameLauncher.Start(result.retryEntry.CloneForRetry()))
            canvas.SetInteractable(false);
    }

    private void Return()
    {
        ResultReturnTarget target = result.returnTarget;
        if (target == null || string.IsNullOrEmpty(target.sceneName))
        {
            Debug.LogWarning("[ResultScene] 돌아갈 곳이 없어 타이틀로 갑니다.", this);
            target = new ResultReturnTarget { sceneName = GameConstants.Scenes.Title };
        }

        if (GameManager.Scene.LoadWithLoading(target.sceneName, target.sceneData))
            canvas.SetInteractable(false);
    }

    /// <summary>
    /// 씬을 직접 실행했을 때 보여줄 가짜 결과. 점수 계산은 실제 규칙(ScoreCalculator)을 쓴다.
    /// </summary>
    private ResultData CreateTestResult()
    {
        Debug.LogWarning("[ResultScene] 결과 데이터가 없어 테스트용 가짜 결과를 보여줍니다. (ResultScene을 직접 실행한 경우)", this);

        int total = 0;
        foreach (int count in testJudgeCounts) total += count;

        ScoreCalculator score = new ScoreCalculator(total);
        JudgeType[] types = { JudgeType.Perfect, JudgeType.Great, JudgeType.Good, JudgeType.Miss };
        for (int i = 0; i < types.Length && i < testJudgeCounts.Length; i++)
        {
            for (int n = 0; n < testJudgeCounts[i]; n++) score.Add(types[i]);
        }

        ResultData data = ResultData.FromScore(score);
        data.songTitle = testSongTitle;
        data.chartName = testChartName;
        data.chartType = ChartType.Custom;
        data.level = testLevel;
        data.isTestPlay = testIsTestPlay;
        data.retryEntry = null;
        data.returnTarget = new ResultReturnTarget
        {
            sceneName = GameConstants.Scenes.Title,
            buttonLabel = GameConstants.Play.ReturnToSongSelectLabel
        };
        return data;
    }
}
