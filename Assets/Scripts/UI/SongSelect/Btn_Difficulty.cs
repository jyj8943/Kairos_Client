using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면(공식 채보 탭)의 난이도 카드 하나 (EASY · NORMAL · HARD + 레벨, 기획서 3-3 ⑤ 11/17 · 3장 UI 톤).
/// - 카드 바탕은 패널(흰색 58%). 난이도 이름은 뱃지(Img_DifficultyBadge, 메뉴용 난이도 색 + 흰 글자), 레벨은 기본 글자색.
/// - 고른 난이도는 포인트색 테두리(chosenBorder)를 켜고 바탕을 선택 색(포인트색 18%)으로 한다.
/// - 고른 곡에 그 난이도 채보가 없으면 레벨 "―"(흐린 글자), 뱃지도 흐린 색으로 두고 버튼을 비활성으로 한다.
/// - 색은 모두 UITheme(메뉴용)에서 읽는다. 인게임 곡 정보 카드 뱃지도 같은 메뉴용 색을 쓴다.
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_Difficulty : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text levelText;

    [Tooltip("난이도 이름 뱃지 바탕 (Img_DifficultyBadge, 자식 Txt_DifficultyName)")]
    [SerializeField] private Image difficultyBadge;

    [Tooltip("고른 난이도의 포인트색 테두리 (Img_ChosenBorder)")]
    [SerializeField] private Image chosenBorder;

    private Button button;

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
    /// 버튼을 한 번만 가져온다. 꺼진 패널 안에 있어 Awake보다 Init이 먼저 불려도 쓸 수 있게 Init에서도 부른다.
    /// </summary>
    private void CacheComponents()
    {
        if (button == null) button = GetComponent<Button>();
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
    /// 지금 곡의 이 난이도 채보를 표시한다. null이면 레벨 "―"와 비활성으로 보인다.
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
        UITheme theme = UITheme.Current;
        IsChosen = chosen && chart != null;
        background.color = IsChosen ? theme.SelectedBackground : theme.panel;
        if (chosenBorder != null)
        {
            chosenBorder.color = theme.accent;
            chosenBorder.gameObject.SetActive(IsChosen);
        }

        Color badgeColor = chart != null ? theme.GetDifficulty(difficulty) : UITheme.WithAlpha(theme.textMuted, 0.45f);
        UIThemePaint.Badge(difficultyBadge, difficultyText, badgeColor);
        UIThemePaint.Text(levelText, chart != null ? UITextLevel.Primary : UITextLevel.Muted);
    }

    /// <summary>뱃지 바탕 색 (확인용)</summary>
    public Color BadgeColor => difficultyBadge != null ? difficultyBadge.color : Color.clear;
}
