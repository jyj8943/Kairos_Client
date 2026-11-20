using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 UI (기획서 3-5 결과 11/17 · 3장 UI 톤 · 라벤더 안개). ResultScene이 Init()을 호출한다.
/// - 배경: MenuBackground (곡 선택과 같은 처리, 처음은 페이드 없이). 색·글꼴은 모두 UITheme (고정 그래픽은 UIThemeColor, 바뀌는 색은 UIThemePaint).
/// - 화면 이름 "RESULT" + 테스트 플레이 표시. 결과 패널(흰색 58%) 위에서 아래로:
///   ⑧ 곡 정보 (앨범 · 제목 · 아티스트 · 난이도 뱃지 · Lv · 배속. 나만의 채보는 CUSTOM 뱃지 + 채보 이름, 길면 채보 이름만 줄임표)
///   → ② 점수 + 이전 최고와 차이 / ① 랭크 이미지 (점수 오른쪽 240×160 칸, S~F 높이 150 정사각형, AP는 가로로 김. 기획서 3-3 「랭크·FC 표시 (11/18 개정)」)
///   → ④ 판정별 개수(이름은 판정 이미지) + FAST/SLOW 막대 / ⑥ RATE · ⑤ MAX COMBO (왼쪽 최대 / 전체, 오른쪽 ⑦ FC 이미지 폭 78 → 그 아래 NEW RECORD).
///   FC가 아니면 FC 이미지만 숨긴다 (NEW RECORD 자리는 그대로). AP면 FC도 함께 띄운다.
///   ③ NEW RECORD는 SCORE · RATE 값 오른쪽, MAX COMBO는 FC 아래. 그 항목이 신기록일 때만 (테스트 플레이는 띄우지 않음).
/// - 오른쪽 x 1300~1860은 카이로스 일러스트 자리로 비워 둔다 (KairosSlot).
/// - 하단 바: 키 안내 "F5 다시 하기 · Enter ESC 곡 선택으로"(테스트 플레이면 "에디터로") + 오른쪽 끝 버튼 2개 (다시 하기 · 곡 선택으로/에디터로).
/// - 키 (기획서 2-6): F5 다시 하기(Result/Retry), Enter 돌아가기(Result/Return), ESC 돌아가기(UIManager → ResultScene.OnBack). 팝업이 열려 있으면 무시.
///   Enter가 선택된 버튼에 따라 바뀌지 않게 버튼은 키보드 선택을 받지 않는다 (Navigation None, 처음 선택 없음).
/// - 등장 연출 (11/20, 기획서 3-5 「결과」 등장 연출 · 🔧, 시간표 ResultIntroTimeline): 일반 플레이 결과만. 화면 이름 · 패널 바탕 · 카이로스 자리 · 하단 바는 처음부터 보이고
///   패널 안의 값만 차례로 나타난다. ResultScene이 Init에서 PrepareIntro로 0초 상태(또는 테스트 플레이면 끝 상태)를 만들고, OnEnter에서 PlayIntro를 부른다.
///   알파는 요소마다 CanvasGroup, 위치는 패널 바로 아래 자식의 anchoredPosition(레이아웃 그룹 안의 뱃지는 크기 · 알파만), 숫자는 지금 표시 형식 그대로 올라간다.
///   숫자가 늘어나며 옆 뱃지가 밀리지 않게 점수 · RATE · MAX COMBO 칸 폭을 최종 글자 폭으로 고정한다 (LayoutElement.preferredWidth).
///   랭크 번쩍임: 랭크 이미지와 같은 스프라이트를 겹친 Img_RankFlash(Additive UI 머티리얼 Kairos/UIAdditive, 색 = 흰색 × 밝기 더할 양)로 밝기를 더한다.
///   AP면 Img_Rank가 Clear_AP 이미지를 쓰므로 번쩍임도 같은 이미지다.
/// - 건너뛰기: 버튼 클릭 · F5 · Enter는 먼저 consumeSkip(ResultScene.TryConsumeSkip)을 거친다. 연출 중이면 끝 상태로 두고 이동하지 않는다.
/// </summary>
public class Canvas_Result : UIBase
{
    /// <summary>판정 한 줄 (이름 이미지 · 개수)</summary>
    [Serializable]
    public class JudgeRow
    {
        [Tooltip("판정 이름 이미지 (Img_JudgeName, JudgeSprites)")]
        public Image nameImage;
        public TMP_Text countText;
    }

    [Header("배경 · 화면 이름")]
    [SerializeField] private MenuBackground menuBackground;
    [SerializeField] private TMP_Text screenNameText;
    [SerializeField] private TMP_Text testPlayText;

    [Header("⑧ 곡 정보")]
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text songTitleText;
    [SerializeField] private TMP_Text artistText;
    [Tooltip("난이도 뱃지 (나만의 채보는 CUSTOM 뱃지)")]
    [SerializeField] private Image chartBadge;
    [SerializeField] private TMP_Text chartBadgeText;
    [Tooltip("나만의 채보 이름 (공식 채보면 숨긴다)")]
    [SerializeField] private TMP_Text chartNameText;
    [SerializeField] private LayoutElement chartNameLayout;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text speedText;

    [Header("② 점수 · ① 랭크")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject newScoreBadge;
    [Tooltip("이전 최고 + 차이 (BEST 933602 +12334). 이전 기록이 없거나 테스트 플레이면 숨긴다")]
    [SerializeField] private TMP_Text bestText;
    [Tooltip("랭크 이미지 (RankArea/Img_Rank, 높이 150 · 칸 폭 240)")]
    [SerializeField] private Img_Rank rankImage;

    [Header("④ 판정별 개수 · FAST/SLOW")]
    [SerializeField] private JudgeRow perfectRow;
    [SerializeField] private JudgeRow greatRow;
    [SerializeField] private JudgeRow goodRow;
    [SerializeField] private JudgeRow missRow;
    [Tooltip("왼쪽 FAST 이름 이미지 (Img_FastTitle)")]
    [SerializeField] private Image fastTitle;
    [Tooltip("FAST 개수 (Txt_FastCount, FAST 이름 오른쪽)")]
    [FormerlySerializedAs("fastText")]
    [SerializeField] private TMP_Text fastCountText;
    [Tooltip("SLOW 개수 (Txt_SlowCount, SLOW 이름 왼쪽)")]
    [FormerlySerializedAs("slowText")]
    [SerializeField] private TMP_Text slowCountText;
    [Tooltip("오른쪽 SLOW 이름 이미지 (Img_SlowTitle)")]
    [SerializeField] private Image slowTitle;
    [Tooltip("막대 바탕 (FAST·SLOW가 둘 다 0이면 구분선 색 빈 막대)")]
    [SerializeField] private Image fastSlowTrack;
    [SerializeField] private Image fastFill;
    [SerializeField] private Image slowFill;

    [Header("⑥ RATE · ⑤ MAX COMBO")]
    [SerializeField] private TMP_Text rateText;
    [SerializeField] private GameObject newRateBadge;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text comboTotalText;
    [Tooltip("⑦ MAX COMBO 카드 오른쪽 FC 이미지 (폭 78)")]
    [SerializeField] private Img_FullCombo fullComboImage;
    [SerializeField] private GameObject newComboBadge;

    [Header("버튼")]
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryLabelText;
    [SerializeField] private TMP_Text retryKeyText;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text returnLabelText;
    [SerializeField] private TMP_Text returnKeyText;

    [Header("입력")]
    [Tooltip("입력 에셋의 Result/Retry (F5)")]
    [SerializeField] private InputActionReference retryAction;
    [Tooltip("입력 에셋의 Result/Return (Enter)")]
    [SerializeField] private InputActionReference returnAction;

    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    [Header("등장 연출 (11/20)")]
    [Tooltip("SCORE 라벨 (Txt_ScoreLabel). 점수 숫자 줄과 함께 나타난다")]
    [SerializeField] private TMP_Text scoreLabelText;
    [Tooltip("랭크 번쩍임 (Img_Rank/Img_RankFlash, 같은 스프라이트 · Additive UI 머티리얼)")]
    [SerializeField] private Image rankFlashImage;

    private Action onRetry;
    private Action onReturn;
    private Func<bool> consumeSkip;
    private bool isInputEnabled;
    private bool isLeaving;

    // 등장 연출
    private ResultData shownResult;
    private bool isIntroPlaying;
    private bool isIntroPending;
    private float introTime;
    private readonly List<IntroPart> introParts = new List<IntroPart>();
    private RectTransform rankRect;
    private readonly List<string> introStageLog = new List<string>();
    private readonly Dictionary<string, float> introStageTimes = new Dictionary<string, float>();
    private bool scoreCueDone;
    private bool rankCueDone;

    /// <summary>등장 연출 요소 하나 (CanvasGroup 알파 · 제자리 · 위치 이동 가능 여부)</summary>
    private class IntroPart
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 BasePosition;
        public bool CanMove;
        public Func<ResultIntroFrame, ResultIntroElement> Pick;
    }

    /// <summary>등장 연출이 재생 중이면 true (0 ~ 3.9초)</summary>
    public bool IsIntroPlaying => isIntroPlaying;

    /// <summary>등장 연출을 OnEnter에서 시작할 차례면 true (Init에서 0초 상태를 만들어 둠)</summary>
    public bool IsIntroPending => isIntroPending;

    /// <summary>지금 등장 연출 시간 (초, 끝났으면 EndSeconds)</summary>
    public float IntroTime => introTime;

    /// <summary>마지막으로 적용한 등장 연출 값 (확인용)</summary>
    public ResultIntroFrame IntroFrame { get; private set; }

    /// <summary>단계별 실제 시작 시각 (OnEnter부터 unscaled 초, 확인용. 건너뛰면 남은 단계는 없다)</summary>
    public IReadOnlyDictionary<string, float> IntroStageTimes => introStageTimes;

    /// <summary>등장 연출 소리 자리 (숫자 올라감 · 랭크 찍힘). 효과음은 나중에 정한다 (확인용 이벤트)</summary>
    public event Action<ResultIntroCue> IntroCue;

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>
    /// 결과를 표시하고 버튼·키 안내 줄을 연결한다. 버튼은 키보드 선택을 받지 않는다 (Enter는 항상 Result/Return).
    /// </summary>
    /// <param name="consumeSkip">버튼 · 키를 처리하기 전에 부른다. true면 (등장 연출 건너뛰기에 썼으므로) 이동하지 않는다</param>
    public void Init(ResultData result, Action onRetry, Action onReturn, Func<bool> consumeSkip = null)
    {
        this.onRetry = onRetry;
        this.onReturn = onReturn;
        this.consumeSkip = consumeSkip;
        isLeaving = false;
        shownResult = result;

        UIThemeColor.ApplyAll(gameObject);
        if (menuBackground != null)
        {
            menuBackground.Init();
            menuBackground.SetIllustration(result.background != null ? result.background : result.cover, true);
        }

        if (screenNameText != null)
        {
            screenNameText.text = GameConstants.Result.ScreenName;
            screenNameText.characterSpacing = GameConstants.SongSelect.ScreenNameCharacterSpacing;
        }
        testPlayText.text = result.isAutoPlay ? GameConstants.Play.AutoTestPlayLabel : GameConstants.Play.TestPlayLabel;
        testPlayText.gameObject.SetActive(result.isTestPlay);

        ShowSongInfo(result);
        ShowScoreAndRank(result);
        ShowJudges(result);
        ShowRateAndCombo(result);
        InitButtons(result.isTestPlay);
        InitKeyGuide(result.isTestPlay);
        BuildIntroParts();
    }

    // ───── 등장 연출 ─────

    /// <summary>
    /// 연출로 움직일 요소를 모은다 (요소마다 CanvasGroup, 제자리 위치는 지금 위치). 숫자 칸 폭은 최종 글자 폭으로 고정한다.
    /// </summary>
    private void BuildIntroParts()
    {
        introParts.Clear();
        AddPart(coverImage, f => f.Album, false);
        AddPart(songTitleText, f => f.Title, true);
        AddPart(artistText, f => f.Artist, true);
        AddPart(chartBadge != null ? chartBadge.transform.parent : null, f => f.ChartLine, true);
        AddPart(scoreLabelText, f => f.Score, true);
        AddPart(scoreText != null ? scoreText.transform.parent : null, f => f.Score, true);
        AddPart(newScoreBadge != null ? newScoreBadge.transform : null, f => f.NewScore, false);
        AddPart(bestText, f => f.Best, false);
        JudgeRow[] rows = { perfectRow, greatRow, goodRow, missRow };
        for (int i = 0; i < rows.Length; i++)
        {
            int row = i;
            AddPart(rows[i] != null && rows[i].nameImage != null ? rows[i].nameImage.transform.parent : null, f => f.GetJudge(row), true);
        }
        AddPart(fastTitle != null ? fastTitle.transform.parent : null, f => f.FastSlow, false);
        AddPart(rateText != null ? CardOf(rateText.transform) : null, f => f.Rate, true);
        AddPart(newRateBadge != null ? newRateBadge.transform : null, f => f.NewRate, false);
        AddPart(maxComboText != null ? CardOf(maxComboText.transform) : null, f => f.Combo, true);
        AddPart(fullComboImage != null ? fullComboImage.transform : null, f => f.FullCombo, false);
        AddPart(newComboBadge != null ? newComboBadge.transform : null, f => f.NewCombo, false);
        rankRect = rankImage != null ? (RectTransform)rankImage.transform : null;
        AddPart(rankRect, f => f.Rank, false);

        // 숫자 칸 폭 고정 (최종 글자 기준): 올라가는 동안 옆 NEW RECORD · "/ 전체"가 밀리지 않게
        FixWidth(scoreText);
        FixWidth(rateText);
        FixWidth(maxComboText);

        if (rankFlashImage != null)
        {
            rankFlashImage.raycastTarget = false;
            rankFlashImage.enabled = false;
        }
    }

    /// <summary>RATE · MAX COMBO 칸 (값 글자 → 줄 → 카드)</summary>
    private static Transform CardOf(Transform valueText)
    {
        Transform row = valueText.parent;
        return row != null ? row.parent : null;
    }

    private void AddPart(Component target, Func<ResultIntroFrame, ResultIntroElement> pick, bool canMove)
    {
        if (target == null) return;
        RectTransform rect = target.transform as RectTransform;
        if (rect == null) return;
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        introParts.Add(new IntroPart { Rect = rect, Group = group, BasePosition = rect.anchoredPosition, CanMove = canMove, Pick = pick });
    }

    private static void FixWidth(TMP_Text text)
    {
        if (text == null) return;
        LayoutElement layout = text.GetComponent<LayoutElement>();
        if (layout == null) layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = text.GetPreferredValues(text.text).x;
    }

    /// <summary>
    /// 등장 연출을 준비한다 (ResultScene.Init). play면 0초 상태(값 요소 숨김)로 두고 OnEnter에서 PlayIntro를 기다린다.
    /// 아니면(테스트 플레이 · 자동 테스트 · 테스트용 건너뛰기) 바로 끝 상태다. 암전 로딩이 걷히는 동안 끝 상태가 먼저 보이지 않는다.
    /// </summary>
    public void PrepareIntro(bool play)
    {
        isIntroPlaying = false;
        introStageLog.Clear();
        introStageTimes.Clear();
        scoreCueDone = false;
        rankCueDone = false;
        if (play)
        {
            isIntroPending = true;
            introTime = 0f;
            ApplyIntro(ResultIntroTimeline.Evaluate(0f));
        }
        else
        {
            isIntroPending = false;
            introTime = GameConstants.ResultIntro.EndSeconds;
            ApplyIntro(ResultIntroTimeline.Finished);
        }
    }

    /// <summary>등장 연출을 0초부터 재생한다 (ResultScene.OnEnter, 암전 로딩이 다 걷힌 뒤)</summary>
    public void PlayIntro()
    {
        if (!isIntroPending) return;
        isIntroPending = false;
        isIntroPlaying = true;
        introTime = 0f;
        StepIntro(0f);
    }

    /// <summary>건너뛰기: 모든 값을 끝 상태(최종값 · 크기 1 · 번쩍임 없음)로 바로 두고 연출을 끝낸다</summary>
    public void SkipIntro()
    {
        if (!isIntroPlaying && !isIntroPending) return;
        isIntroPending = false;
        isIntroPlaying = false;
        Debug.Log($"[Canvas_Result] 등장 연출 건너뜀: {introTime:0.000}초에 끝 상태로 (지나온 단계 {introStageLog.Count}개)", this);
        introTime = GameConstants.ResultIntro.EndSeconds;
        ApplyIntro(ResultIntroTimeline.Finished);
    }

    private void StepIntro(float time)
    {
        introTime = time;
        foreach ((string name, float start) in ResultIntroTimeline.Stages)
        {
            if (time + 1e-6f < start || introStageTimes.ContainsKey(name)) continue;
            introStageTimes[name] = time;
            introStageLog.Add($"{name} {start:0.00}→{time:0.000}");
        }
        if (!scoreCueDone && time >= GameConstants.ResultIntro.ScoreCountStart) { scoreCueDone = true; PlayIntroSound(ResultIntroCue.ScoreCount); }
        if (!rankCueDone && time >= GameConstants.ResultIntro.RankStart + GameConstants.ResultIntro.RankDuration * GameConstants.ResultIntro.RankImpactAt)
        {
            rankCueDone = true;
            PlayIntroSound(ResultIntroCue.RankStamp);
        }

        ResultIntroFrame frame = ResultIntroTimeline.Evaluate(time);
        ApplyIntro(frame);
        if (!frame.IsFinished) return;

        isIntroPlaying = false;
        Debug.Log($"[Canvas_Result] 등장 연출 끝 {time:0.000}초 — 단계 (표 시작→실제): {string.Join(" / ", introStageLog)}", this);
    }

    /// <summary>
    /// 효과음 자리 (숫자 올라감 시작 · 랭크 찍힘). 효과음은 나중에 정한다 — 지금은 알림만 보낸다.
    /// </summary>
    private void PlayIntroSound(ResultIntroCue cue)
    {
        IntroCue?.Invoke(cue);
    }

    /// <summary>한 프레임 값을 화면에 옮긴다 (알파 · 위치 · 크기 · 숫자 · 막대 · 번쩍임)</summary>
    private void ApplyIntro(ResultIntroFrame f)
    {
        IntroFrame = f;
        foreach (IntroPart part in introParts)
        {
            ResultIntroElement e = part.Pick(f);
            part.Group.alpha = e.Alpha;
            part.Rect.localScale = new Vector3(e.Scale, e.Scale, 1f);
            if (part.CanMove) part.Rect.anchoredPosition = part.BasePosition + e.Offset;
        }

        ResultData r = shownResult;
        if (r == null) return;
        if (scoreText != null) scoreText.text = SongSelectRecordFormat.FormatScore(ResultIntroTimeline.CountInt(r.score, f.ScoreCount));
        SetCount(perfectRow, r.perfectCount, f.JudgeCount0);
        SetCount(greatRow, r.greatCount, f.JudgeCount1);
        SetCount(goodRow, r.goodCount, f.JudgeCount2);
        SetCount(missRow, r.missCount, f.JudgeCount3);
        if (fastCountText != null) fastCountText.text = ResultFormat.FormatCount(ResultIntroTimeline.CountInt(r.fastCount, f.FastSlowProgress));
        if (slowCountText != null) slowCountText.text = ResultFormat.FormatCount(ResultIntroTimeline.CountInt(r.slowCount, f.FastSlowProgress));
        if (ResultFormat.TryGetFastRatio(r.fastCount, r.slowCount, out float ratio))
        {
            float p = f.FastSlowProgress;
            if (fastFill != null) SetHorizontalRange(fastFill.rectTransform, 0f, ratio * p);
            if (slowFill != null) SetHorizontalRange(slowFill.rectTransform, 1f - (1f - ratio) * p, 1f);
        }
        if (rateText != null) rateText.text = SongSelectRecordFormat.FormatRate(ResultIntroTimeline.CountDouble(r.accuracy, f.RateValue));
        if (maxComboText != null) maxComboText.text = SongSelectRecordFormat.FormatCombo(ResultIntroTimeline.CountInt(r.maxCombo, f.ComboValue));

        if (rankFlashImage != null && rankImage != null)
        {
            Image rank = rankImage.GetComponent<Image>();
            bool flash = f.RankFlash > 0f && rank != null && rank.sprite != null && rank.enabled;
            rankFlashImage.enabled = flash;
            if (flash)
            {
                rankFlashImage.sprite = rank.sprite;
                rankFlashImage.preserveAspect = rank.preserveAspect;
                rankFlashImage.color = new Color(1f, 1f, 1f, f.RankFlash); // 랭크 알파 · 크기는 부모 Img_Rank의 CanvasGroup · scale을 따른다
            }
        }
    }

    private static void SetCount(JudgeRow row, int count, float progress)
    {
        if (row != null && row.countText != null) row.countText.text = ResultFormat.FormatCount(ResultIntroTimeline.CountInt(count, progress));
    }


    // ───── 표시 ─────

    /// <summary>⑧ 곡 정보: 앨범 · 제목 · 아티스트 · 뱃지(난이도 / CUSTOM + 채보 이름) · Lv · 배속</summary>
    private void ShowSongInfo(ResultData result)
    {
        UITheme theme = UITheme.Current;
        coverImage.sprite = result.cover;
        coverImage.color = result.cover != null ? Color.white : theme.emptyCover;
        songTitleText.text = result.songTitle ?? string.Empty;
        artistText.text = result.artist ?? string.Empty;

        bool isCustom = result.chartType == ChartType.Custom;
        chartBadgeText.text = isCustom ? GameConstants.Play.ChartBadgeCustomText : (result.difficulty ?? string.Empty).ToUpperInvariant();
        UIThemePaint.Badge(chartBadge, chartBadgeText, isCustom ? theme.difficultyCustom : theme.GetDifficulty(result.difficulty));

        chartNameText.gameObject.SetActive(isCustom);
        if (isCustom)
        {
            chartNameText.text = result.chartName ?? string.Empty;
            if (chartNameLayout != null)
                chartNameLayout.preferredWidth = Mathf.Min(chartNameText.GetPreferredValues(chartNameText.text).x, GameConstants.Result.MaxChartNameWidth);
        }

        levelText.text = string.Format(GameConstants.SongSelect.LevelFormat, result.level);
        speedText.text = string.Format(CultureInfo.InvariantCulture, GameConstants.Result.SpeedFormat, result.scrollMultiplier);
    }

    /// <summary>② 점수 + NEW RECORD + 이전 최고와 차이 / ① 랭크 이미지</summary>
    private void ShowScoreAndRank(ResultData result)
    {
        UITheme theme = UITheme.Current;
        scoreText.text = SongSelectRecordFormat.FormatScore(result.score);
        newScoreBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewBestScore, result.isTestPlay));

        string diff = ResultFormat.FormatScoreDiff(result.hadPreviousRecord, result.isTestPlay, result.score, result.previousBestScore);
        bestText.gameObject.SetActive(diff != null);
        if (diff != null)
        {
            Color diffColor = result.score > result.previousBestScore ? theme.accent : theme.textDescription;
            bestText.text = string.Format(GameConstants.Result.BestFormat, SongSelectRecordFormat.FormatScore(result.previousBestScore)) +
                            $" <color=#{ColorUtility.ToHtmlStringRGB(diffColor)}><b>{diff}</b></color>";
        }

        rankImage.SetRank(result.rank);
    }

    /// <summary>
    /// ④ 판정별 개수 (이름은 인게임 판정 패널과 같은 판정 이미지 JudgeSprites, 크기 = 원본 × GameConstants.Result.JudgeImageScale / 숫자는 기본 글자색) + FAST/SLOW 막대.
    /// FAST/SLOW: 왼쪽 [FAST 이미지][개수] · 오른쪽 [개수][SLOW 이미지] (이미지 배율 Result.FastSlowImageScale), 개수 글자색은 메뉴용 judgeFast · judgeSlow.
    /// </summary>
    private void ShowJudges(ResultData result)
    {
        UITheme theme = UITheme.Current;
        SetJudgeRow(perfectRow, JudgeType.Perfect, result.perfectCount);
        SetJudgeRow(greatRow, JudgeType.Great, result.greatCount);
        SetJudgeRow(goodRow, JudgeType.Good, result.goodCount);
        SetJudgeRow(missRow, JudgeType.Miss, result.missCount);

        SetJudgeImage(fastTitle, JudgeSprites.Get(FastSlow.Fast, JudgeSpriteSet.Menu), GameConstants.Result.FastSlowImageScale);
        SetJudgeImage(slowTitle, JudgeSprites.Get(FastSlow.Slow, JudgeSpriteSet.Menu), GameConstants.Result.FastSlowImageScale);
        fastCountText.text = ResultFormat.FormatCount(result.fastCount);
        fastCountText.color = theme.judgeFast;
        slowCountText.text = ResultFormat.FormatCount(result.slowCount);
        slowCountText.color = theme.judgeSlow;

        bool hasData = ResultFormat.TryGetFastRatio(result.fastCount, result.slowCount, out float ratio);
        fastSlowTrack.color = theme.divider;
        fastFill.gameObject.SetActive(hasData && ratio > 0f);
        slowFill.gameObject.SetActive(hasData && ratio < 1f);
        fastFill.color = theme.judgeFast;
        slowFill.color = theme.judgeSlow;
        SetHorizontalRange(fastFill.rectTransform, 0f, ratio);
        SetHorizontalRange(slowFill.rectTransform, ratio, 1f);
        FastRatio = hasData ? ratio : -1f;
    }

    private static void SetJudgeRow(JudgeRow row, JudgeType judge, int count)
    {
        if (row == null) return;
        SetJudgeImage(row.nameImage, JudgeSprites.Get(judge, JudgeSpriteSet.Menu), GameConstants.Result.JudgeImageScale);
        row.countText.text = ResultFormat.FormatCount(count);
        UIThemePaint.Text(row.countText, UITextLevel.Primary);
    }

    /// <summary>판정 이름 이미지를 넣고 크기를 원본 × 배율로 맞춘다 (색은 이미지 안이라 흰색)</summary>
    private static void SetJudgeImage(Image target, Sprite sprite, float scale)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.preserveAspect = true;
        target.raycastTarget = false;
        target.color = Color.white;
        target.rectTransform.sizeDelta = JudgeSprites.DisplaySize(sprite, scale);
    }

    private static void SetHorizontalRange(RectTransform rect, float min, float max)
    {
        rect.anchorMin = new Vector2(min, 0f);
        rect.anchorMax = new Vector2(max, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>⑥ RATE (소수 둘째 자리 %) · ⑤ MAX COMBO (최대 / 전체 = 노트 수 + 롱노트 유지 틱 수) + ⑦ FC (AP면 FC도) + 각 NEW RECORD</summary>
    private void ShowRateAndCombo(ResultData result)
    {
        rateText.text = SongSelectRecordFormat.FormatRate(result.accuracy);
        newRateBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewBestAccuracy, result.isTestPlay));
        maxComboText.text = SongSelectRecordFormat.FormatCombo(result.maxCombo);
        comboTotalText.text = string.Format(GameConstants.Result.MaxComboTotalFormat, SongSelectRecordFormat.FormatCombo(result.maxPossibleCombo));
        fullComboImage.SetFullCombo(result.isFullCombo || result.isAllPerfect);
        newComboBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewMaxCombo, result.isTestPlay));
    }

    // ───── 버튼 · 키 안내 ─────

    private void InitButtons(bool isTestPlay)
    {
        retryLabelText.text = GameConstants.Play.RetryLabel;
        returnLabelText.text = isTestPlay ? GameConstants.KeyGuide.ToEditor : GameConstants.KeyGuide.ToSongSelect;
        if (retryKeyText != null) retryKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(retryAction));
        if (returnKeyText != null) returnKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(returnAction, GameConstants.KeyGuide.EnterKey));

        retryButton.onClick.RemoveListener(OnClickRetry);
        retryButton.onClick.AddListener(OnClickRetry);
        returnButton.onClick.RemoveListener(OnClickReturn);
        returnButton.onClick.AddListener(OnClickReturn);
        SetInteractable(true);

        // 클릭해도 선택되지 않게 (선택된 버튼이 있으면 Enter가 Submit으로 그 버튼도 누른다)
        retryButton.navigation = new Navigation { mode = Navigation.Mode.None };
        returnButton.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    /// <summary>키 안내 줄: F5 다시 하기 · Enter ESC 곡 선택으로 (테스트 플레이면 "에디터로")</summary>
    private void InitKeyGuide(bool isTestPlay)
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Canvas_Result] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        string returnLabel = isTestPlay ? GameConstants.KeyGuide.ToEditor : GameConstants.KeyGuide.ToSongSelect;
        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Retry, KeyGuideKey.FromAction(retryAction)),
            new KeyGuideEntry(returnLabel, KeyGuideKey.FromAction(returnAction, GameConstants.KeyGuide.EnterKey), KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
                .WithId(GameConstants.KeyGuide.ReturnId)
        });
    }

    // ───── 확인용 ─────

    /// <summary>NEW RECORD 표시 여부 (점수 · RATE · MAX COMBO, 확인용)</summary>
    public bool IsNewScoreShown => newScoreBadge != null && newScoreBadge.activeSelf;
    public bool IsNewRateShown => newRateBadge != null && newRateBadge.activeSelf;
    public bool IsNewComboShown => newComboBadge != null && newComboBadge.activeSelf;

    /// <summary>곡 정보 뱃지 글자·색, 채보 이름 표시 여부 (확인용)</summary>
    public string ChartBadgeLabel => chartBadgeText != null ? chartBadgeText.text : null;
    public Color ChartBadgeColor => chartBadge != null ? chartBadge.color : Color.clear;
    public bool IsChartNameShown => chartNameText != null && chartNameText.gameObject.activeSelf;
    public string ChartNameLabel => chartNameText != null ? chartNameText.text : null;

    /// <summary>랭크 이미지 · FC 이미지 (확인용)</summary>
    public Img_Rank RankImage => rankImage;
    public Img_FullCombo FullComboImage => fullComboImage;

    /// <summary>FAST 막대 비율 (빈 막대면 -1, 확인용)</summary>
    public float FastRatio { get; private set; }

    /// <summary>판정 이름 이미지 (확인용: PERFECT · GREAT · GOOD · MISS · FAST · SLOW 순서)</summary>
    public Image[] JudgeTitles => new[] { perfectRow?.nameImage, greatRow?.nameImage, goodRow?.nameImage, missRow?.nameImage, fastTitle, slowTitle };

    /// <summary>판정 개수 글자 (확인용: PERFECT · GREAT · GOOD · MISS · FAST · SLOW 순서)</summary>
    public TMP_Text[] JudgeCountTexts => new[] { perfectRow?.countText, greatRow?.countText, goodRow?.countText, missRow?.countText, fastCountText, slowCountText };

    /// <summary>돌아가기 버튼 글자 (확인용)</summary>
    public string ReturnLabel => returnLabelText != null ? returnLabelText.text : null;

    // ───── 입력 ─────

    /// <summary>
    /// F5·Enter 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (retryAction == null || returnAction == null)
        {
            Debug.LogWarning("[Canvas_Result] retryAction·returnAction이 연결되지 않아 키로 다시 하기·돌아가기를 할 수 없습니다.", this);
            return;
        }

        retryAction.action.performed += OnRetryKey;
        returnAction.action.performed += OnReturnKey;
        retryAction.action.Enable();
        returnAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// F5·Enter 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        retryAction.action.performed -= OnRetryKey;
        returnAction.action.performed -= OnReturnKey;
        isInputEnabled = false;
    }

    /// <summary>ESC (ResultScene.OnBack, 건너뛰기 판단은 OnBack이 먼저 한다): 돌아가기 버튼과 같다</summary>
    public void RequestReturn()
    {
        OnClickReturn();
    }

    /// <summary>씬 이동을 시작하면 버튼을 막아 중복 실행을 막는다.</summary>
    public void SetInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        returnButton.interactable = interactable;
        isLeaving = !interactable;
    }

    /// <summary>
    /// 등장 연출을 unscaled 프레임 시간으로 진행한다.
    /// 다른 코드가 버튼을 선택해 두었어도 다음 프레임에 풀어, Enter가 Submit으로 그 버튼까지 누르지 않게 한다
    /// (Enter는 항상 Result/Return 하나만 실행).
    /// </summary>
    private void Update()
    {
        if (isIntroPlaying) StepIntro(introTime + Time.unscaledDeltaTime);

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && (selected == retryButton.gameObject || selected == returnButton.gameObject))
            eventSystem.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        DisableInput();
        if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        if (returnButton != null) returnButton.onClick.RemoveListener(OnClickReturn);
    }

    private void OnRetryKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickRetry();
    }

    private void OnReturnKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickReturn();
    }

    /// <summary>팝업이 열려 있으면 키를 받지 않는다</summary>
    private static bool IsShortcutBlocked => (UIManager.HasInstance && GameManager.UI.HasOpenPopup) || (GameSceneManager.HasInstance && GameManager.Scene.IsLoading);

#if UNITY_EDITOR
    /// <summary>테스트 전용 (Unity 에디터): 다시 하기·돌아가기가 실행된 횟수 (Enter 한 번에 한 번만 실행되는지 확인)</summary>
    public int TestRetryCount { get; private set; }
    public int TestReturnCount { get; private set; }
#endif

    private void OnClickRetry()
    {
        if (consumeSkip != null && consumeSkip()) return;
        if (isLeaving) return;
#if UNITY_EDITOR
        TestRetryCount++;
#endif
        onRetry?.Invoke();
    }

    private void OnClickReturn()
    {
        if (consumeSkip != null && consumeSkip()) return;
        if (isLeaving) return;
#if UNITY_EDITOR
        TestReturnCount++;
#endif
        onReturn?.Invoke();
    }
}

/// <summary>결과 등장 연출의 소리 자리 (효과음은 나중에 정한다)</summary>
public enum ResultIntroCue
{
    /// <summary>점수 숫자 올라감 시작 (0.50초)</summary>
    ScoreCount,

    /// <summary>랭크 찍힘 (3.45 + 0.35 × 0.55초)</summary>
    RankStamp
}
