using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인게임 화면 UI (기획서 3-4 최소 형태). InGameScene이 Init()을 호출한다.
/// - 플레이 중: 곡 정보(제목·채보 이름·Lv), 판정 글자, 콤보, 점수, 안내 문구.
/// - 끝나면 결과 패널(Img_ResultPanel): 점수·랭크·판정별 개수·최대 콤보·정확도·FULL COMBO/ALL PERFECT, 다시 하기·나가기 버튼.
/// - 플레이 중 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다.
/// </summary>
public class Canvas_InGame : MonoBehaviour
{
    [Header("플레이 중")]
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private TMP_Text judgeText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("불러오기 실패 등 안내 문구 (Txt_Message)")]
    [SerializeField] private TMP_Text messageText;

    [Header("결과")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultScoreText;
    [SerializeField] private TMP_Text resultRankText;
    [SerializeField] private TMP_Text resultJudgesText;
    [SerializeField] private TMP_Text resultComboText;
    [SerializeField] private TMP_Text resultAccuracyText;
    [SerializeField] private TMP_Text resultBadgeText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button exitButton;

    [Header("판정 색")]
    [SerializeField] private Color perfectColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    [SerializeField] private Color greatColor = new Color32(0x6E, 0xC8, 0xF5, 0xFF);
    [SerializeField] private Color goodColor = new Color32(0x9C, 0xCC, 0x65, 0xFF);
    [SerializeField] private Color missColor = new Color32(0xEF, 0x53, 0x50, 0xFF);

    private Action onRetry;
    private Action onExit;

    /// <summary>
    /// 곡 정보를 표시하고 결과 패널을 닫은 상태로 시작한다.
    /// </summary>
    public void Init(string songTitle, string chartName, int level, Action onRetry, Action onExit)
    {
        this.onRetry = onRetry;
        this.onExit = onExit;

        songInfoText.text = string.Format(GameConstants.Editor.ChartInfoFormat, songTitle, chartName, level);
        judgeText.text = string.Empty;
        comboText.text = string.Empty;
        scoreText.SetText("{0}", 0);
        messageText.text = string.Empty;
        resultPanel.SetActive(false);

        retryButton.onClick.RemoveListener(OnClickRetry);
        retryButton.onClick.AddListener(OnClickRetry);
        exitButton.onClick.RemoveListener(OnClickExit);
        exitButton.onClick.AddListener(OnClickExit);
    }

    /// <summary>
    /// 안내 문구를 표시한다 (불러오기 실패 등). Init 전에도 쓸 수 있다.
    /// </summary>
    public void ShowMessage(string message)
    {
        messageText.text = message;
        resultPanel.SetActive(false);
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

        if (combo > 0) comboText.SetText("{0}", combo);
        else comboText.text = string.Empty;

        scoreText.SetText("{0}", score);
    }

    /// <summary>
    /// 결과 패널을 연다.
    /// </summary>
    public void ShowResult(ScoreCalculator score)
    {
        resultPanel.SetActive(true);

        resultScoreText.text = score.Score.ToString("N0", CultureInfo.InvariantCulture);
        resultRankText.text = score.Rank.ToString();
        resultJudgesText.text =
            $"{GameConstants.Play.PerfectText}  {score.PerfectCount}\n" +
            $"{GameConstants.Play.GreatText}  {score.GreatCount}\n" +
            $"{GameConstants.Play.GoodText}  {score.GoodCount}\n" +
            $"{GameConstants.Play.MissText}  {score.MissCount}";
        resultComboText.text = $"MAX COMBO  {score.MaxCombo}";
        resultAccuracyText.text = $"정확도  {score.Accuracy.ToString("0.00", CultureInfo.InvariantCulture)}%";

        if (score.IsAllPerfect) resultBadgeText.text = GameConstants.Play.AllPerfectText;
        else if (score.IsFullCombo) resultBadgeText.text = GameConstants.Play.FullComboText;
        else resultBadgeText.text = string.Empty;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    /// <summary>결과 버튼을 막는다 (씬 이동을 시작한 뒤 중복 실행 방지)</summary>
    public void SetResultInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        exitButton.interactable = interactable;
    }

    private void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnClickExit);
    }

    private void OnClickRetry()
    {
        onRetry?.Invoke();
    }

    private void OnClickExit()
    {
        onExit?.Invoke();
    }
}
