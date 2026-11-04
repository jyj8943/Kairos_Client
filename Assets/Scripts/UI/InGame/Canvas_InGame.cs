using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 화면 UI (기획서 3-4 최소 형태). InGameScene이 Init()을 호출한다.
/// - 곡 정보(제목·채보 이름·Lv), 판정 글자, 콤보, 점수, 안내 문구, 테스트 플레이 표시.
/// - 결과는 결과 씬(ResultScene)이 보여준다.
/// - 플레이 중 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다.
/// </summary>
public class Canvas_InGame : MonoBehaviour
{
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private TMP_Text judgeText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("불러오기 실패 등 안내 문구 (Txt_Message)")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("테스트 플레이 표시 (Txt_TestPlay). 테스트 플레이일 때만 켠다")]
    [SerializeField] private TMP_Text testPlayText;

    [Header("판정 색")]
    [SerializeField] private Color perfectColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    [SerializeField] private Color greatColor = new Color32(0x6E, 0xC8, 0xF5, 0xFF);
    [SerializeField] private Color goodColor = new Color32(0x9C, 0xCC, 0x65, 0xFF);
    [SerializeField] private Color missColor = new Color32(0xEF, 0x53, 0x50, 0xFF);

    /// <summary>
    /// 곡 정보와 테스트 플레이 표시를 채우고 점수·콤보를 비운다.
    /// </summary>
    public void Init(string songTitle, string chartName, int level, bool isTestPlay)
    {
        songInfoText.text = string.Format(GameConstants.Editor.ChartInfoFormat, songTitle, chartName, level);
        judgeText.text = string.Empty;
        comboText.text = string.Empty;
        scoreText.SetText("{0}", 0);
        messageText.text = string.Empty;

        testPlayText.text = GameConstants.Play.TestPlayInGameText;
        testPlayText.gameObject.SetActive(isTestPlay);
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

        if (combo > 0) comboText.SetText("{0}", combo);
        else comboText.text = string.Empty;

        scoreText.SetText("{0}", score);
    }
}
