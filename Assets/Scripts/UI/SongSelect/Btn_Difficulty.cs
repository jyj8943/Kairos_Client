using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 화면(공식 채보 탭)의 난이도 탭 하나 (기록 패널 위에 붙는 EASY · NORMAL · HARD, 기획서 3-3 11/25).
/// - 왼쪽부터 레벨 숫자(고름 44 / 안 고름 34, 난이도 색) · 난이도 이름 + LEVEL 두 줄 묶음 · 오른쪽 끝에 작은 랭크(최대 44×32), 기록이 없으면 NOT PLAYED.
/// - 바탕은 패널(흰색 58%, Img_TabFill) 위에 난이도 색(Img_TabTint)을 겹친다: 고른 탭 16%, 나머지 7%. 고른 탭은 위 3px 줄(TopLine, 난이도 색)을 켠다.
/// - 탭 높이는 모두 같다. 아래 모서리를 각지게 하려고 루트의 RectMask2D가 둥근 바탕의 아래쪽을 잘라 기록 패널과 붙인다.
/// - 고른 곡에 그 난이도 채보가 없으면 레벨 "―"(흐린 글자), 이름·칠도 흐린 색, 랭크·NOT PLAYED 없이 버튼을 비활성으로 한다.
/// - 안 요소는 모두 TabArea(탭에서 위 3px을 뺀 영역)의 세로 가운데다 (앵커·피벗 y 0.5, 위치 y 0). 가로 위치만 글자 폭에 맞춰 정한다.
/// - 색은 모두 UITheme에서 읽는다.
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_Difficulty : MonoBehaviour
{
    [Tooltip("난이도 색 칠 (Img_TabTint, 패널 바탕 위에 겹친다)")]
    [SerializeField] private Image tint;
    [Tooltip("고른 탭 위 3px 줄 (TopLine/Img_TopLine)")]
    [SerializeField] private Image topLine;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("난이도 이름 + LEVEL 두 줄 묶음 (NameGroup)")]
    [SerializeField] private RectTransform nameGroup;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text levelLabelText;
    [Tooltip("작은 랭크 (Img_Rank, 최대 44×32)")]
    [SerializeField] private Img_Rank rank;
    [Tooltip("기록이 없을 때 글자 (Txt_NotPlayed)")]
    [SerializeField] private TMP_Text notPlayedText;

    /// <summary>레벨 숫자 크기: 고름 / 안 고름</summary>
    public const float ChosenLevelSize = 44f;
    public const float NormalLevelSize = 34f;

    /// <summary>좌우 여백 · 레벨 숫자와 이름 묶음 사이</summary>
    private const float SidePadding = 22f;
    private const float LevelGap = 16f;

    private Button button;

    private string difficulty;
    private ChartData chart;
    private bool hasRecord;

    /// <summary>클릭 리스너 연결용</summary>
    public Button Button => button;

    /// <summary>이 버튼의 난이도 이름 (GameConstants.Chart.Difficulties 중 하나)</summary>
    public string Difficulty => difficulty;

    /// <summary>지금 곡의 이 난이도 채보. 없으면 null</summary>
    public ChartData Chart => chart;

    /// <summary>고른 난이도 표시 중이면 true (확인용)</summary>
    public bool IsChosen { get; private set; }

    /// <summary>작은 랭크 (확인용)</summary>
    public Img_Rank RankImage => rank;

    /// <summary>NOT PLAYED 글자 (확인용)</summary>
    public TMP_Text NotPlayedText => notPlayedText;

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
        levelLabelText.text = GameConstants.SongSelect.LevelLabel;
        notPlayedText.text = GameConstants.SongSelect.NotPlayed;
        SetChart(null, null);
    }

    /// <summary>
    /// 지금 곡의 이 난이도 채보와 기록을 표시한다. 채보가 null이면 레벨 "―"와 비활성으로 보인다.
    /// </summary>
    /// <param name="record">최고 기록 (없으면 null → NOT PLAYED)</param>
    public void SetChart(ChartData chart, ChartRecordData record)
    {
        this.chart = chart;
        hasRecord = chart != null && record != null;
        levelText.text = chart != null ? chart.level.ToString() : GameConstants.SongSelect.EmptyValue;
        button.interactable = chart != null;

        RecordTexts texts = SongSelectRecordFormat.Build(record);
        rank.Show(hasRecord, texts.Rank);
        if (hasRecord) ((RectTransform)rank.transform).anchoredPosition = new Vector2(-(SidePadding + rank.Size.x * 0.5f), 0f);
        notPlayedText.gameObject.SetActive(chart != null && !hasRecord);
        SetChosen(false);
    }

    /// <summary>
    /// 고른 난이도 표시를 켜거나 끈다. 채보가 없으면 켜지 않는다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        UITheme theme = UITheme.Current;
        IsChosen = chosen && chart != null;

        Color color = chart != null ? theme.GetDifficulty(difficulty) : theme.textMuted;
        tint.color = UITheme.WithAlpha(color, IsChosen ? theme.difficultyTintChosenAlpha : theme.difficultyTintAlpha);
        topLine.color = color;
        topLine.gameObject.SetActive(IsChosen);

        levelText.color = color;
        levelText.fontSize = IsChosen ? ChosenLevelSize : NormalLevelSize;
        UIThemePaint.Text(difficultyText, chart == null ? UITextLevel.Muted : IsChosen ? UITextLevel.Primary : UITextLevel.Secondary);
        UIThemePaint.Text(levelLabelText, chart == null ? UITextLevel.Muted : UITextLevel.Description);
        UIThemePaint.Text(notPlayedText, UITextLevel.Description);

        float levelWidth = levelText.GetPreferredValues(levelText.text).x;
        nameGroup.anchoredPosition = new Vector2(SidePadding + levelWidth + LevelGap, 0f);
    }

    /// <summary>겹친 난이도 색 (확인용)</summary>
    public Color TintColor => tint != null ? tint.color : Color.clear;
}
