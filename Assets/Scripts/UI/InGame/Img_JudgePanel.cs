using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 왼쪽 판정 패널 (Img_JudgePanel, 기획서 3-4 "판정 패널 규칙"). Canvas_InGame.Init이 Init()을 부르고, 판정마다 Refresh를 부른다.
/// - 위에서부터 PERFECT · GREAT · GOOD · MISS 개수 / FAST · SLOW 개수 / 정확도 / 점수(점수는 Canvas_InGame이 쓴다).
/// - 개수는 앞에 0을 채우지 않고 오른쪽 정렬, 자릿수가 바뀌어도 흔들리지 않게 고정폭 숫자(&lt;mspace&gt;)로 쓴다.
/// - 정확도는 플레이 중 정확도(ScoreCalculator.LiveAccuracy, 지금까지 판정한 수로 나눔)를 소수 둘째 자리까지 쓴다.
/// - 라벨 색은 Canvas_InGame이 판정 글자·FAST/SLOW 글자와 같은 색으로 넘긴다 (색을 한 곳에서만 고치게).
/// - 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다.
/// </summary>
public class Img_JudgePanel : MonoBehaviour
{
    /// <summary>개수 숫자 형식: 고정폭 숫자, 0 채움 없음</summary>
    private const string CountFormat = "<mspace=0.62em>{0}</mspace>";

    /// <summary>정확도 형식: 소수 둘째 자리 + %</summary>
    private const string AccuracyFormat = "{0:2}%";

    [Header("라벨 (Txt_*Title)")]
    [SerializeField] private TMP_Text perfectTitle;
    [SerializeField] private TMP_Text greatTitle;
    [SerializeField] private TMP_Text goodTitle;
    [SerializeField] private TMP_Text missTitle;
    [SerializeField] private TMP_Text fastTitle;
    [SerializeField] private TMP_Text slowTitle;

    [Header("숫자 (Txt_*Count, Txt_Accuracy)")]
    [SerializeField] private TMP_Text perfectCount;
    [SerializeField] private TMP_Text greatCount;
    [SerializeField] private TMP_Text goodCount;
    [SerializeField] private TMP_Text missCount;
    [SerializeField] private TMP_Text fastCount;
    [SerializeField] private TMP_Text slowCount;
    [SerializeField] private TMP_Text accuracy;

    /// <summary>
    /// 라벨 문구·색을 정하고 숫자·정확도를 이번 판의 점수 계산 값으로 채운다.
    /// 판 시작에는 판정 개수 0, 정확도는 판정 전 LiveAccuracy(0.00%)가 된다 (초기값을 따로 두지 않는다).
    /// </summary>
    /// <param name="score">이번 판의 점수 계산 (판정 전 상태)</param>
    public void Init(Color perfectColor, Color greatColor, Color goodColor, Color missColor, Color fastColor, Color slowColor, ScoreCalculator score)
    {
        SetTitle(perfectTitle, GameConstants.Play.PerfectText, perfectColor);
        SetTitle(greatTitle, GameConstants.Play.GreatText, greatColor);
        SetTitle(goodTitle, GameConstants.Play.GoodText, goodColor);
        SetTitle(missTitle, GameConstants.Play.MissText, missColor);
        SetTitle(fastTitle, GameConstants.Play.FastText, fastColor);
        SetTitle(slowTitle, GameConstants.Play.SlowText, slowColor);

        Refresh(score);
    }

    /// <summary>판정 개수·FAST/SLOW·정확도를 점수 계산 값으로 바꾼다 (숏노트·롱노트 종합 판정 때)</summary>
    public void Refresh(ScoreCalculator score)
    {
        SetCounts(score.PerfectCount, score.GreatCount, score.GoodCount, score.MissCount, score.FastCount, score.SlowCount);
        accuracy.SetText(AccuracyFormat, (float)score.LiveAccuracy);
    }

    private void SetCounts(int perfect, int great, int good, int miss, int fast, int slow)
    {
        perfectCount.SetText(CountFormat, perfect);
        greatCount.SetText(CountFormat, great);
        goodCount.SetText(CountFormat, good);
        missCount.SetText(CountFormat, miss);
        fastCount.SetText(CountFormat, fast);
        slowCount.SetText(CountFormat, slow);
    }

    private static void SetTitle(TMP_Text title, string text, Color color)
    {
        if (title == null) return;
        title.text = text;
        title.color = color;
    }
}
