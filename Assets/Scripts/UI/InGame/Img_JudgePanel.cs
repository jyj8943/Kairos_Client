using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 왼쪽 ⑥ 판정 패널 (Img_JudgePanel, 기획서 3-4 "판정 패널 규칙" · 11/18 톤 개편). Canvas_InGame.Init이 Init()을 부르고, 판정마다 Refresh를 부른다.
/// - 라벤더 불투명 패널(#F4F1F8, 11/19) 위: PERFECT · GREAT · GOOD · MISS 개수 / FAST · SLOW 개수 / 맨 아래 RATE 박스(Img_RateBox) · SCORE 박스(Img_ScoreBox, 점수는 Canvas_InGame이 쓴다).
/// - 판정 이름(Img_*Title)은 기어 판정 글자와 같은 이미지(JudgeSprites, 11/19)를 Init에서 한 번 넣는다. 색은 이미지 안에 있어 칠하지 않는다.
///   크기 = 원본 × GameConstants.Play.PanelJudgeImageScale(판정 4개) · PanelFastSlowImageScale(FAST·SLOW).
///   씬에서 pivot x를 이미지 안 글자 왼쪽 끝에 맞춰 두어, 배율을 바꿔도 글자 시작 x가 그대로다. 세로는 줄 가운데(개수 숫자와 같은 높이).
/// - 숫자는 기본 글자색 NEXON Lv2.
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

    [Header("판정 이름 이미지 (Img_*Title)")]
    [SerializeField] private Image perfectTitle;
    [SerializeField] private Image greatTitle;
    [SerializeField] private Image goodTitle;
    [SerializeField] private Image missTitle;
    [SerializeField] private Image fastTitle;
    [SerializeField] private Image slowTitle;
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
    /// 판정 이름 이미지·RATE/SCORE 라벨(UITheme 메뉴 묶음 색)을 정하고 숫자·RATE를 이번 판의 점수 계산 값으로 채운다.
    /// 판 시작에는 판정 개수 0, RATE는 판정 전 LiveAccuracy(0.00%)가 된다 (초기값을 따로 두지 않는다).
    /// </summary>
    /// <param name="score">이번 판의 점수 계산 (판정 전 상태)</param>
    public void Init(ScoreCalculator score)
    {
        UITheme theme = UITheme.Current;
        SetJudgeImage(perfectTitle, JudgeSprites.Get(JudgeType.Perfect, JudgeSpriteSet.Menu), GameConstants.Play.PanelJudgeImageScale);
        SetJudgeImage(greatTitle, JudgeSprites.Get(JudgeType.Great, JudgeSpriteSet.Menu), GameConstants.Play.PanelJudgeImageScale);
        SetJudgeImage(goodTitle, JudgeSprites.Get(JudgeType.Good, JudgeSpriteSet.Menu), GameConstants.Play.PanelJudgeImageScale);
        SetJudgeImage(missTitle, JudgeSprites.Get(JudgeType.Miss, JudgeSpriteSet.Menu), GameConstants.Play.PanelJudgeImageScale);
        SetJudgeImage(fastTitle, JudgeSprites.Get(FastSlow.Fast, JudgeSpriteSet.Menu), GameConstants.Play.PanelFastSlowImageScale);
        SetJudgeImage(slowTitle, JudgeSprites.Get(FastSlow.Slow, JudgeSpriteSet.Menu), GameConstants.Play.PanelFastSlowImageScale);
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

    /// <summary>판정 이름 이미지를 넣고 크기를 원본 × 배율로 맞춘다 (색은 흰색 그대로)</summary>
    private static void SetJudgeImage(Image target, Sprite sprite, float scale)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.preserveAspect = true;
        target.raycastTarget = false;
        target.color = Color.white;
        target.rectTransform.sizeDelta = JudgeSprites.DisplaySize(sprite, scale);
    }

    private static void SetTitle(TMP_Text title, string text, JudgeColor color)
    {
        if (title == null) return;
        title.text = text;
        UIThemePaint.JudgeText(title, color);
    }

    /// <summary>판정 이름 이미지 (확인용: PERFECT · GREAT · GOOD · MISS · FAST · SLOW 순서)</summary>
    public Image[] JudgeTitles => new[] { perfectTitle, greatTitle, goodTitle, missTitle, fastTitle, slowTitle };

    /// <summary>판정 이름 이미지 (확인용)</summary>
    public Image GetJudgeTitle(JudgeType judge)
    {
        switch (judge)
        {
            case JudgeType.Perfect: return perfectTitle;
            case JudgeType.Great: return greatTitle;
            case JudgeType.Good: return goodTitle;
            default: return missTitle;
        }
    }

    /// <summary>FAST · SLOW 이름 이미지 (확인용, None이면 null)</summary>
    public Image GetFastSlowTitle(FastSlow fastSlow) => fastSlow == FastSlow.Fast ? fastTitle : fastSlow == FastSlow.Slow ? slowTitle : null;
}
