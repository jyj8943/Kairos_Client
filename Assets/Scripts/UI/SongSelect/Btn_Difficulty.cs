using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면(공식 채보 탭)의 난이도 버튼 하나 (NORMAL · HARD · EXPERT + 레벨).
/// - 고른 곡에 그 난이도 채보가 없으면 레벨을 "-"로 두고 버튼을 비활성으로 보인다.
/// - 고른 난이도는 배경을 검게, 글자를 희게 표시한다 (Btn_SongItem과 같은 모양).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_Difficulty : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text levelText;

    [Header("색")]
    [SerializeField] private Color chosenBackgroundColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color chosenTextColor = Color.white;
    [SerializeField] private Color unavailableTextColor = new Color32(0xC4, 0xC4, 0xC8, 0xFF);

    private Button button;
    private Color normalBackgroundColor;
    private Color normalNameColor;
    private Color normalLevelColor;

    private string difficulty;
    private ChartData chart;

    /// <summary>클릭 리스너 연결용</summary>
    public Button Button => button;

    /// <summary>이 버튼의 난이도 이름 (GameConstants.Chart.Difficulties 중 하나)</summary>
    public string Difficulty => difficulty;

    /// <summary>지금 곡의 이 난이도 채보. 없으면 null</summary>
    public ChartData Chart => chart;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// 버튼과 기본 색을 한 번만 가져온다. 꺼진 패널 안에 있어 Awake보다 Init이 먼저 불려도 쓸 수 있게 Init에서도 부른다.
    /// </summary>
    private void CacheComponents()
    {
        if (button != null) return;

        button = GetComponent<Button>();
        normalBackgroundColor = background.color;
        normalNameColor = difficultyText.color;
        normalLevelColor = levelText.color;
    }

    /// <summary>
    /// 난이도 이름을 정하고 채보 없음 상태로 시작한다.
    /// </summary>
    public void Init(string difficulty)
    {
        CacheComponents();
        this.difficulty = difficulty;
        difficultyText.text = difficulty;
        SetChart(null);
    }

    /// <summary>
    /// 지금 곡의 이 난이도 채보를 표시한다. null이면 레벨 "-"와 비활성으로 보인다.
    /// </summary>
    public void SetChart(ChartData chart)
    {
        this.chart = chart;
        levelText.text = chart != null
            ? string.Format(GameConstants.SongSelect.LevelFormat, chart.level)
            : GameConstants.SongSelect.EmptyValue;
        button.interactable = chart != null;
        SetChosen(false);
    }

    /// <summary>
    /// 고른 난이도 표시를 켜거나 끈다. 채보가 없으면 켜지 않는다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        bool isChosen = chosen && chart != null;
        background.color = isChosen ? chosenBackgroundColor : normalBackgroundColor;

        if (isChosen)
        {
            difficultyText.color = chosenTextColor;
            levelText.color = chosenTextColor;
        }
        else if (chart == null)
        {
            difficultyText.color = unavailableTextColor;
            levelText.color = unavailableTextColor;
        }
        else
        {
            difficultyText.color = normalNameColor;
            levelText.color = normalLevelColor;
        }
    }
}
