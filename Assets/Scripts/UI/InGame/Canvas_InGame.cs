using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 화면 UI (기획서 3-4 최소 형태). InGameScene이 Init()을 호출한다.
/// - 곡 정보(제목·채보 이름·Lv·배속), 판정 글자, 콤보, 점수, 안내 문구, 테스트 플레이 표시, 재개 카운트다운.
/// - 플레이 중 배속을 바꾸면 곡 정보의 배속을 고치고, 기어 옆(Txt_SpeedChange)에 "x5.6"을 잠깐(SpeedChangeShowSeconds) 보여준다.
/// - 결과는 결과 씬(ResultScene)이 보여준다.
/// - 플레이 중 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다.
/// </summary>
public class Canvas_InGame : UIBase
{
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private TMP_Text judgeText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("불러오기 실패 등 안내 문구 (Txt_Message)")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("테스트 플레이 표시 (Txt_TestPlay). 테스트 플레이일 때만 켠다")]
    [SerializeField] private TMP_Text testPlayText;
    [Tooltip("일시정지 후 재개 카운트다운 숫자 (Txt_Countdown, 화면 가운데)")]
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("배속을 바꿀 때 기어 옆에 잠깐 보여주는 배속 (Txt_SpeedChange)")]
    [SerializeField] private TMP_Text speedChangeText;

    [Header("판정 색")]
    [SerializeField] private Color perfectColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    [SerializeField] private Color greatColor = new Color32(0x6E, 0xC8, 0xF5, 0xFF);
    [SerializeField] private Color goodColor = new Color32(0x9C, 0xCC, 0x65, 0xFF);
    [SerializeField] private Color missColor = new Color32(0xEF, 0x53, 0x50, 0xFF);

    private string songTitle;
    private string chartName;
    private int level;

    /// <summary>배속 변경 표시를 숨길 시각 (realtime, 음수면 숨김)</summary>
    private float speedChangeHideAt = -1f;

    /// <summary>
    /// 곡 정보와 테스트 플레이 표시를 채우고 점수·콤보를 비운다.
    /// </summary>
    /// <param name="isAutoPlay">자동 플레이면 테스트 플레이 표시에 "자동 플레이"를 함께 쓴다</param>
    /// <param name="scrollMultiplier">시작 배속 (곡 정보에 표시)</param>
    public void Init(string songTitle, string chartName, int level, bool isTestPlay, bool isAutoPlay, float scrollMultiplier)
    {
        this.songTitle = songTitle;
        this.chartName = chartName;
        this.level = level;
        SetSpeed(scrollMultiplier, false);
        if (speedChangeText != null) speedChangeText.gameObject.SetActive(false);
        speedChangeHideAt = -1f;
        judgeText.text = string.Empty;
        comboText.text = string.Empty;
        scoreText.SetText("{0}", 0);
        messageText.text = string.Empty;

        testPlayText.text = isAutoPlay ? GameConstants.Play.AutoTestPlayInGameText : GameConstants.Play.TestPlayInGameText;
        testPlayText.gameObject.SetActive(isTestPlay || isAutoPlay);
        HideCountdown();
    }

    /// <summary>
    /// 곡 정보의 배속을 바꾼다. showChange면 기어 옆에 "x5.6"을 잠깐 보여준다 (플레이 중 1 / 2 키).
    /// </summary>
    public void SetSpeed(float scrollMultiplier, bool showChange)
    {
        songInfoText.text = string.Format(GameConstants.Play.InGameSongInfoFormat, songTitle, chartName, level, scrollMultiplier);
        if (!showChange || speedChangeText == null) return;

        speedChangeText.text = string.Format(GameConstants.Play.SpeedChangeFormat, scrollMultiplier);
        speedChangeText.gameObject.SetActive(true);
        speedChangeHideAt = Time.realtimeSinceStartup + GameConstants.Play.SpeedChangeShowSeconds;
    }

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
    /// 판정 하나를 표시한다 (판정 글자·콤보·점수).
    /// </summary>
    public void ShowJudge(JudgeType judge, int combo, int score)
    {
        switch (judge)
        {
            case JudgeType.Perfect: judgeText.text = GameConstants.Play.PerfectText; judgeText.color = perfectColor; break;
            case JudgeType.Great: judgeText.text = GameConstants.Play.GreatText; judgeText.color = greatColor; break;
            case JudgeType.Good: judgeText.text = GameConstants.Play.GoodText; judgeText.color = goodColor; break;
            default: judgeText.text = GameConstants.Play.MissText; judgeText.color = missColor; break;
        }

        ShowCombo(combo, score);
    }

    /// <summary>
    /// 판정 글자는 그대로 두고 콤보·점수만 바꾼다 (롱노트 시작 판정·유지 틱).
    /// </summary>
    public void ShowCombo(int combo, int score)
    {
        if (combo > 0) comboText.SetText("{0}", combo);
        else comboText.text = string.Empty;

        scoreText.SetText("{0}", score);
    }
}
