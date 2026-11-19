using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 왼쪽 ⑥ 판정 패널 (Img_JudgePanel, 기획서 3-4 "판정 패널 규칙" · 11/18 톤 개편). Canvas_InGame.Init이 Init()을 부르고, 판정마다 Refresh를 부른다.
/// - 라벤더 불투명 패널(#F4F1F8, 11/19) 위: PERFECT · GREAT · GOOD · MISS 개수 / FAST · SLOW 개수 / 맨 아래 RATE 박스(Img_RateBox) · SCORE 박스(Img_ScoreBox, 점수는 Canvas_InGame이 쓴다).
/// - 판정 이름은 넥슨 카트 고딕 ExtraBold, 색은 밝은 배경용 진한 판정 색(UITheme.GetMenuJudge, PERFECT는 금 → 분홍 가로 그라데이션), 숫자는 기본 글자색 NEXON Lv2.
/// - 개수는 앞에 0을 채우지 않고 오른쪽 정렬, 자릿수가 바뀌어도 흔들리지 않게 고정폭 숫자(&lt;mspace&gt;)로 쓴다.
/// - RATE는 플레이 중 정확도(ScoreCalculator.LiveAccuracy, 지금까지 판정한 수로 나눔)를 소수 둘째 자리까지 쓴다 (판정 전 0.00%).
/// - 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다.
/// </summary>
public class Img_JudgePanel : MonoBehaviour
{
    /// <summary>개수 숫자 형식: 고정폭 숫자, 0 채움 없음</summary>
    private const string CountFormat = "<mspace=0.62em>{0}</mspace>";

    /// <summary>RATE 형식: 소수 둘째 자리 + %</summary>
    private const string RateFormat = "{0:2}%";

    [Header("라벨 (Txt_*Title)")]
    [SerializeField] private TMP_Text perfectTitle;
    [SerializeField] private TMP_Text greatTitle;
    [SerializeField] private TMP_Text goodTitle;
    [SerializeField] private TMP_Text missTitle;
    [SerializeField] private TMP_Text fastTitle;
    [SerializeField] private TMP_Text slowTitle;
    [Tooltip("RATE 박스 라벨 (Img_RateBox/Txt_RateTitle)")]
    [SerializeField] private TMP_Text rateTitle;
    [Tooltip("SCORE 박스 라벨 (Img_ScoreBox/Txt_ScoreTitle)")]
    [SerializeField] private TMP_Text scoreTitle;

    [Header("숫자 (Txt_*Count, Txt_Rate)")]
    [SerializeField] private TMP_Text perfectCount;
    [SerializeField] private TMP_Text greatCount;
    [SerializeField] private TMP_Text goodCount;
    [SerializeField] private TMP_Text missCount;
    [SerializeField] private TMP_Text fastCount;
    [SerializeField] private TMP_Text slowCount;
    [Tooltip("RATE 값 (Img_RateBox/Txt_Rate)")]
    [SerializeField] private TMP_Text rate;

    /// <summary>RATE 글자 (확인용)</summary>
    public string RateText => rate != null ? rate.text : string.Empty;

    /// <summary>
    /// 라벨 문구·색(UITheme 메뉴 묶음)을 정하고 숫자·RATE를 이번 판의 점수 계산 값으로 채운다.
    /// 판 시작에는 판정 개수 0, RATE는 판정 전 LiveAccuracy(0.00%)가 된다 (초기값을 따로 두지 않는다).
    /// </summary>
    /// <param name="score">이번 판의 점수 계산 (판정 전 상태)</param>
    public void Init(ScoreCalculator score)
    {
        UITheme theme = UITheme.Current;
        SetTitle(perfectTitle, GameConstants.Play.PerfectText, theme.GetMenuJudge(JudgeType.Perfect));
        SetTitle(greatTitle, GameConstants.Play.GreatText, theme.GetMenuJudge(JudgeType.Great));
        SetTitle(goodTitle, GameConstants.Play.GoodText, theme.GetMenuJudge(JudgeType.Good));
        SetTitle(missTitle, GameConstants.Play.MissText, theme.GetMenuJudge(JudgeType.Miss));
        SetTitle(fastTitle, GameConstants.Play.FastText, JudgeColor.Solid(theme.judgeFast));
        SetTitle(slowTitle, GameConstants.Play.SlowText, JudgeColor.Solid(theme.judgeSlow));
        SetTitle(rateTitle, GameConstants.Play.RateTitleText, JudgeColor.Solid(theme.textDescription));
        SetTitle(scoreTitle, GameConstants.Play.ScoreTitleText, JudgeColor.Solid(theme.textDescription));

        Refresh(score);
    }

    /// <summary>판정 개수·FAST/SLOW·RATE를 점수 계산 값으로 바꾼다 (숏노트·롱노트 종합 판정 때)</summary>
    public void Refresh(ScoreCalculator score)
    {
        SetCounts(score.PerfectCount, score.GreatCount, score.GoodCount, score.MissCount, score.FastCount, score.SlowCount);
        rate.SetText(RateFormat, (float)score.LiveAccuracy);
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

    private static void SetTitle(TMP_Text title, string text, JudgeColor color)
    {
        if (title == null) return;
        title.text = text;
        UIThemePaint.JudgeText(title, color);
    }

    /// <summary>판정 이름 라벨 (확인용: PERFECT · GREAT · GOOD · MISS · FAST · SLOW)</summary>
    public TMP_Text[] JudgeTitles => new[] { perfectTitle, greatTitle, goodTitle, missTitle, fastTitle, slowTitle };
}
