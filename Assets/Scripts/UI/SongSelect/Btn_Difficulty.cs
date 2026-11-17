using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면(공식 채보 탭)의 난이도 카드 하나 (EASY · NORMAL · HARD + 레벨, 기획서 3-3 ⑤ 11/17).
/// - 어두운 카드 위에 난이도 이름을 그 난이도 색(DifficultyColors 배경색 그대로: EASY 초록 · NORMAL 노랑 · HARD 빨강), 레벨을 흰색으로 쓴다.
/// - 고른 난이도는 하늘색 테두리(chosenBorder)를 켜고 카드 바탕을 조금 밝게 한다.
/// - 고른 곡에 그 난이도 채보가 없으면 레벨을 "—"로 두고 버튼을 비활성, 글자를 회색으로 보인다.
/// - 색 값은 직렬화하지 않고 DifficultyColors·프리팹 기본 색을 쓴다.
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_Difficulty : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text levelText;

    [Header("고름 표시")]
    [Tooltip("고른 난이도의 하늘색 테두리 (Img_ChosenBorder)")]
    [SerializeField] private Image chosenBorder;
    [SerializeField] private Color chosenBackgroundColor = new Color32(0x2A, 0x3A, 0x55, 0xE6);
    [SerializeField] private Color unavailableTextColor = new Color32(0x71, 0x71, 0x7A, 0xFF);

    private Button button;
    private Color normalBackgroundColor;
    private Color normalLevelColor;

    private string difficulty;
    private ChartData chart;

    /// <summary>클릭 리스너 연결용</summary>
    public Button Button => button;

    /// <summary>이 버튼의 난이도 이름 (GameConstants.Chart.Difficulties 중 하나)</summary>
    public string Difficulty => difficulty;

    /// <summary>지금 곡의 이 난이도 채보. 없으면 null</summary>
    public ChartData Chart => chart;

    /// <summary>고른 난이도 표시 중이면 true (확인용)</summary>
    public bool IsChosen { get; private set; }

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
    /// 지금 곡의 이 난이도 채보를 표시한다. null이면 레벨 "—"와 비활성으로 보인다.
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
        IsChosen = chosen && chart != null;
        background.color = IsChosen ? chosenBackgroundColor : normalBackgroundColor;
        if (chosenBorder != null) chosenBorder.gameObject.SetActive(IsChosen);

        if (chart == null)
        {
            difficultyText.color = unavailableTextColor;
            levelText.color = unavailableTextColor;
            return;
        }

        difficultyText.color = DifficultyColors.Get(difficulty).Background;
        levelText.color = normalLevelColor;
    }
}
