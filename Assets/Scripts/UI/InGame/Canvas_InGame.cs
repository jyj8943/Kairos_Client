using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 화면 UI (기획서 3-4 인게임 화면 배치 개정 11/13). InGameScene이 Init()을 호출한다.
/// - 왼쪽 곡 정보 패널(Img_SongInfo): 곡 제목 / 뱃지(공식: 난이도 이름·난이도별 색, 나만의 채보: CUSTOM) + 채보 이름(나만의 채보만) + Lv.
///   / 배속 + "[1] [2]로 변경" / 곡 진행 바 / 오른쪽 위 테스트 플레이 표시. 무엇을 보일지는 InGameHudRules.BuildSongInfo가 정한다.
/// - 왼쪽 판정 패널(Img_JudgePanel): 판정 개수·FAST/SLOW 개수·정확도(Img_JudgePanel 스크립트) + 맨 아래 점수.
/// - 기어 위쪽 콤보(Txt_Combo: 오름·쉬면 숨기기 애니메이션), 판정선 위 판정 글자와 그 위 FAST/SLOW(Txt_FastSlow), 안내 문구, 재개 카운트다운.
///   FAST/SLOW를 띄울지는 InGameScene이 FastSlowRules로 정해 넘긴다 (이 스크립트는 규칙을 판단하지 않는다).
///   판정 글자에는 페이드가 없어 다음 판정까지 남고, FAST/SLOW도 판정 글자와 함께 바뀐다 (시간으로 움직이지 않아 일시정지 중에도 그대로).
/// - 플레이 중 배속을 바꾸면 곡 정보의 배속을 고치고, 기어 옆(Txt_SpeedChange)에 "x5.6"을 잠깐(SpeedChangeShowSeconds) 보여준다.
/// - 결과는 결과 씬(ResultScene)이 보여준다.
/// - 플레이 중 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다. 진행 바는 채움의 anchorMax.x만 바꾼다.
/// </summary>
public class Canvas_InGame : UIBase
{
    [Header("곡 정보 패널 (Img_SongInfo)")]
    [Tooltip("곡 제목 (Txt_SongTitle, 넘치면 줄임표)")]
    [SerializeField] private TMP_Text songTitleText;
    [Tooltip("채보 뱃지 배경 (Img_ChartBadge). 공식은 난이도별 색, 나만의 채보는 CUSTOM 색")]
    [SerializeField] private Image chartBadgeImage;
    [Tooltip("채보 뱃지 글자 (Img_ChartBadge/Txt_ChartBadge)")]
    [SerializeField] private TMP_Text chartBadgeText;
    [Tooltip("나만의 채보 이름 (Txt_ChartName). 나만의 채보일 때만 켜고, 넘치면 이 글자만 줄임표")]
    [SerializeField] private TMP_Text chartNameText;
    [Tooltip("레벨 (Txt_Level, \"Lv.3\"). 항상 보인다")]
    [SerializeField] private TMP_Text levelText;
    [Tooltip("배속 (Txt_Speed, \"배속 x5.5 · [1] [2]로 변경\")")]
    [SerializeField] private TMP_Text speedText;
    [Tooltip("곡 진행 바 채움 (Img_Progress/Img_ProgressFill, Sliced). 왼쪽 기준 anchorMax.x = 비율로 채운다")]
    [SerializeField] private RectTransform progressFill;
    [Tooltip("테스트 플레이 표시 (Img_TestPlay). 테스트 플레이일 때만 켠다")]
    [SerializeField] private GameObject testPlayBadge;
    [Tooltip("테스트 플레이 표시 글자 (Img_TestPlay/Txt_TestPlay)")]
    [SerializeField] private TMP_Text testPlayText;

    [Header("뱃지 색")]
    [Tooltip("공식 채보 난이도별 뱃지 색 (GameConstants.Chart.Difficulties 순서: EASY · NORMAL · HARD)")]
    [SerializeField] private Color[] difficultyBadgeColors =
    {
        GameConstants.Play.ChartBadgeEasyColor,
        GameConstants.Play.ChartBadgeNormalColor,
        GameConstants.Play.ChartBadgeHardColor
    };
    [Tooltip("나만의 채보(CUSTOM) 뱃지 색. 모르는 난이도 이름도 이 색을 쓴다")]
    [SerializeField] private Color customBadgeColor = GameConstants.Play.ChartBadgeCustomColor;

    [Header("판정 패널 (Img_JudgePanel)")]
    [Tooltip("판정 개수·FAST/SLOW·정확도. 점수는 scoreText")]
    [SerializeField] private Img_JudgePanel judgePanel;

    [Header("화면 가운데")]
    [SerializeField] private TMP_Text judgeText;
    [Tooltip("판정 글자 위 FAST / SLOW (Txt_FastSlow, GREAT · GOOD일 때만)")]
    [SerializeField] private TMP_Text fastSlowText;
    [Tooltip("콤보 숫자 (Combo/Txt_Combo). 오름·쉬면 숨기기 애니메이션을 맡는다")]
    [SerializeField] private Txt_Combo comboText;
    [Tooltip("콤보 숫자 위 제목 (Combo/Txt_ComboTitle). 움직이지 않는다")]
    [SerializeField] private TMP_Text comboTitleText;
    [Tooltip("점수 (Img_JudgePanel/Img_Score/Txt_Score, 오른쪽 정렬)")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("불러오기 실패 등 안내 문구 (Txt_Message)")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("일시정지 후 재개 카운트다운 숫자 (Txt_Countdown, 화면 가운데)")]
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("배속을 바꿀 때 기어 옆에 잠깐 보여주는 배속 (Txt_SpeedChange)")]
    [SerializeField] private TMP_Text speedChangeText;

    [Header("판정 색")]
    [SerializeField] private Color perfectColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    [SerializeField] private Color greatColor = new Color32(0x6E, 0xC8, 0xF5, 0xFF);
    [SerializeField] private Color goodColor = new Color32(0x9C, 0xCC, 0x65, 0xFF);
    [SerializeField] private Color missColor = new Color32(0xEF, 0x53, 0x50, 0xFF);

    [Header("FAST / SLOW 색 (판정 글자 위·판정 패널 라벨)")]
    [SerializeField] private Color fastColor = GameConstants.Play.FastColor;
    [SerializeField] private Color slowColor = GameConstants.Play.SlowColor;

    /// <summary>배속 변경 표시를 숨길 시각 (realtime, 음수면 숨김)</summary>
    private float speedChangeHideAt = -1f;

    /// <summary>진행 바에 마지막으로 쓴 비율 (같은 값이면 다시 쓰지 않는다)</summary>
    private float shownProgress = -1f;

    /// <summary>
    /// 곡 정보 패널·테스트 플레이 표시를 채우고 점수·콤보·진행 바를 비운다.
    /// </summary>
    /// <param name="chartType">채보 종류 (ChartData.type). 공식이면 난이도 뱃지, 나만의 채보면 CUSTOM 뱃지 + 채보 이름</param>
    /// <param name="difficulty">난이도 이름 (ChartData.difficulty)</param>
    /// <param name="isAutoPlay">자동 플레이면 테스트 플레이 표시에 "자동 플레이"를 함께 쓴다</param>
    /// <param name="scrollMultiplier">시작 배속 (곡 정보에 표시)</param>
    public void Init(string songTitle, ChartType chartType, string difficulty, string chartName, int level,
        bool isTestPlay, bool isAutoPlay, float scrollMultiplier)
    {
        SongInfoDisplay info = InGameHudRules.BuildSongInfo(chartType, difficulty, chartName, level, isTestPlay, isAutoPlay);
        songTitleText.text = songTitle ?? string.Empty;
        chartBadgeText.text = info.BadgeText;
        chartBadgeImage.color = GetBadgeColor(info);
        chartNameText.text = info.ChartName;
        chartNameText.gameObject.SetActive(info.ShowChartName);
        levelText.text = info.LevelText;
        testPlayText.text = info.TestPlayText;
        testPlayBadge.SetActive(info.ShowTestPlay);

        SetSpeed(scrollMultiplier, false);
        if (speedChangeText != null) speedChangeText.gameObject.SetActive(false);
        speedChangeHideAt = -1f;
        shownProgress = -1f;
        SetProgress(0f);
        judgeText.text = string.Empty;
        ShowFastSlow(FastSlow.None);
        if (judgePanel != null) judgePanel.Init(perfectColor, greatColor, goodColor, missColor, fastColor, slowColor);
        if (comboTitleText != null) comboTitleText.text = GameConstants.Play.ComboTitleText;
        comboText.ResetView();
        scoreText.SetText("{0}", 0);
        messageText.text = string.Empty;
        HideCountdown();
    }

    /// <summary>뱃지 색: 나만의 채보·모르는 난이도는 CUSTOM 색, 공식은 난이도 순번의 색</summary>
    private Color GetBadgeColor(SongInfoDisplay info)
    {
        if (info.IsCustom || difficultyBadgeColors == null) return customBadgeColor;
        if (info.DifficultyIndex < 0 || info.DifficultyIndex >= difficultyBadgeColors.Length) return customBadgeColor;
        return difficultyBadgeColors[info.DifficultyIndex];
    }

    /// <summary>
    /// 곡 정보의 배속을 바꾼다. showChange면 기어 옆에 "x5.6"을 잠깐 보여준다 (플레이 중 1 / 2 키).
    /// </summary>
    public void SetSpeed(float scrollMultiplier, bool showChange)
    {
        speedText.text = string.Format(GameConstants.Play.InGameSpeedFormat, scrollMultiplier);
        if (!showChange || speedChangeText == null) return;

        speedChangeText.text = string.Format(GameConstants.Play.SpeedChangeFormat, scrollMultiplier);
        speedChangeText.gameObject.SetActive(true);
        speedChangeHideAt = Time.realtimeSinceStartup + GameConstants.Play.SpeedChangeShowSeconds;
    }

    /// <summary>
    /// 곡 진행 바를 채운다 (0~1). InGameScene이 플레이 중 매 프레임 부른다 (일시정지·카운트다운 중에는 부르지 않아 멈춘다).
    /// 화면 1px보다 작은 변화는 건너뛰어 매 프레임 메시를 다시 만들지 않는다.
    /// </summary>
    public void SetProgress(float ratio)
    {
        if (progressFill == null) return;
        if (shownProgress >= 0f && Mathf.Abs(ratio - shownProgress) < ProgressStep && ratio < 1f) return;

        shownProgress = ratio;
        progressFill.anchorMax = new Vector2(ratio, progressFill.anchorMax.y);
    }

    /// <summary>진행 바를 다시 칠하는 최소 변화 (바 폭 552px 기준 약 1px)</summary>
    private const float ProgressStep = 1f / 552f;

    /// <summary>진행 바 채움 비율 (확인용)</summary>
    public float Progress => progressFill != null ? progressFill.anchorMax.x : 0f;

    /// <summary>배속 변경 표시가 켜져 있으면 true (확인용)</summary>
    public bool IsSpeedChangeVisible => speedChangeText != null && speedChangeText.gameObject.activeSelf;

    private void Update()
    {
        // 일시정지·카운트다운 중에도 시간이 지나면 숨긴다 (realtime)
        if (speedChangeHideAt < 0f || Time.realtimeSinceStartup < speedChangeHideAt) return;

        speedChangeHideAt = -1f;
        if (speedChangeText != null) speedChangeText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 재개 카운트다운 숫자를 보여준다.
    /// </summary>
    public void ShowCountdown(int count)
    {
        countdownText.SetText("{0}", count);
        countdownText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 재개 카운트다운 숫자를 숨긴다.
    /// </summary>
    public void HideCountdown()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 안내 문구를 표시한다 (불러오기 실패 등). Init 전에도 쓸 수 있다.
    /// </summary>
    public void ShowMessage(string message)
    {
        messageText.text = message;
    }

    /// <summary>
    /// 판정 하나를 표시한다 (판정 글자·FAST/SLOW·콤보·점수). 숏노트·롱노트 종합 판정 때 부른다.
    /// </summary>
    /// <param name="fastSlow">판정 글자 위에 띄울 방향 (FastSlowRules.GetDisplay). None이면 앞의 FAST/SLOW를 지운다</param>
    public void ShowJudge(JudgeType judge, FastSlow fastSlow, int combo, int score)
    {
        switch (judge)
        {
            case JudgeType.Perfect: judgeText.text = GameConstants.Play.PerfectText; judgeText.color = perfectColor; break;
            case JudgeType.Great: judgeText.text = GameConstants.Play.GreatText; judgeText.color = greatColor; break;
            case JudgeType.Good: judgeText.text = GameConstants.Play.GoodText; judgeText.color = goodColor; break;
            default: judgeText.text = GameConstants.Play.MissText; judgeText.color = missColor; break;
        }

        ShowFastSlow(fastSlow);
        ShowCombo(combo, score);
    }

    /// <summary>
    /// 판정 글자 위 FAST / SLOW를 바꾼다. None이면 숨긴다 (앞의 FAST/SLOW가 다음 PERFECT에 붙어 남지 않게).
    /// 롱노트 시작 판정은 판정 글자를 바꾸지 않고 이것만 부른다.
    /// </summary>
    public void ShowFastSlow(FastSlow fastSlow)
    {
        if (fastSlowText == null) return;
        switch (fastSlow)
        {
            case FastSlow.Fast: fastSlowText.text = GameConstants.Play.FastText; fastSlowText.color = fastColor; break;
            case FastSlow.Slow: fastSlowText.text = GameConstants.Play.SlowText; fastSlowText.color = slowColor; break;
            default: fastSlowText.text = string.Empty; break;
        }
    }

    /// <summary>판정 패널 숫자(판정 개수·FAST/SLOW·정확도)를 바꾼다. 숏노트·롱노트 종합 판정 때 부른다</summary>
    public void RefreshJudgePanel(ScoreCalculator score)
    {
        if (judgePanel != null) judgePanel.Refresh(score);
    }

    /// <summary>지금 FAST / SLOW 글자 (확인용, 숨김이면 빈 문자열)</summary>
    public string FastSlowShown => fastSlowText != null ? fastSlowText.text : string.Empty;

    /// <summary>
    /// 판정 글자는 그대로 두고 콤보·점수만 바꾼다 (롱노트 시작 판정·유지 틱).
    /// </summary>
    public void ShowCombo(int combo, int score)
    {
        comboText.SetCombo(combo);
        scoreText.SetText("{0}", score);
    }

    /// <summary>
    /// 콤보 애니메이션·쉬는 시간을 진행한다. InGameScene이 플레이 중 매 프레임 부른다 (일시정지·재개 카운트다운 중에는 부르지 않는다).
    /// </summary>
    public void TickCombo(float deltaTime)
    {
        comboText.Tick(deltaTime);
    }

    /// <summary>콤보 표시 (확인용)</summary>
    public Txt_Combo Combo => comboText;
}
