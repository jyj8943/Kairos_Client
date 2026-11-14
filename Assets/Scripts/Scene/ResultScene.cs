using System.Collections;
using UnityEngine;

/// <summary>
/// 결과 씬 진입점. ResultScene.unity 루트의 "ResultScene" 오브젝트에 붙인다.
/// - 인게임이 넘긴 ResultData를 Canvas_Result로 보여준다.
/// - 들어올 때 기록을 갱신·저장하고(GameManager.PlayerData.SubmitResult, 테스트·자동 플레이는 기록하지 않음) 신기록 여부를 결과에 넣는다.
///   일시정지 → 나가기·재시작으로 끝난 판은 이 씬을 거치지 않으므로 기록되지 않는다.
/// - 다시 하기: 같은 InGameEntryData로 인게임을 다시 시작한다 (테스트 플레이면 같은 시작 위치).
/// - 키: F5 다시 하기 · Enter 돌아가기는 Canvas_Result가, ESC 돌아가기는 OnBack이 받는다 (기획서 3-9).
/// - 돌아가기: ResultData의 돌아갈 곳(씬 이름·데이터)대로 이동한다. 이 씬은 에디터·곡 선택 씬을 직접 알지 않는다.
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
        canvas.Init(result, Retry, Return);

        Debug.Log($"[ResultScene] 결과: 점수 {result.score}, 랭크 {result.rank}, 노트 {result.totalNotes}개, " +
                  $"테스트 플레이 {result.isTestPlay}, 신기록 {result.isNewRecord}{(result.hadPreviousRecord ? $" (이전 최고 {result.previousBestScore})" : "")}, " +
                  $"돌아갈 곳 {result.returnTarget?.sceneName ?? "(없음)"}", this);
        isReady = true;
    }

    /// <summary>
    /// 기록을 갱신·저장하고 신기록 여부·이전 최고 점수를 결과에 넣는다 (다시 하기 데이터의 채보 기준).
    /// </summary>
    private void SubmitRecord(ResultData data)
    {
        if (!PlayerDataManager.HasInstance) return;

        RecordUpdateResult update = GameManager.PlayerData.SubmitResult(data.retryEntry, data);
        data.isNewRecord = update.isRecorded && update.isNewRecord;
        data.hadPreviousRecord = update.hadPreviousRecord;
        data.previousBestScore = update.previousBestScore;
    }

    /// <summary>
    /// 초기화가 끝나면 F5·Enter 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
        if (isReady) canvas.EnableInput();
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
        if (isReady && canvas != null) canvas.RequestReturn();
    }

    private void Retry()
    {
        if (result.retryEntry == null)
        {
            Debug.LogWarning("[ResultScene] 다시 하기 데이터가 없습니다 (가짜 결과로 실행한 경우).", this);
            return;
        }

        if (GameManager.Scene.Load(GameConstants.Scenes.InGame, result.retryEntry.Clone()))
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

        if (GameManager.Scene.Load(target.sceneName, target.sceneData))
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
