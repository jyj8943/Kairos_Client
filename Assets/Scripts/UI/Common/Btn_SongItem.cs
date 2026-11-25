using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 곡 목록의 곡 아이템 하나. 에디터 곡 고르기(Prefabs/Common/Btn_SongItem)와 곡 선택 화면(Prefabs/SongSelect/Btn_SongItem)이 같이 쓴다.
/// - 키보드(↑↓)로 옮기거나 마우스로 클릭해 선택되면 곧바로 그 곡을 고른다.
///   마우스를 올리기만 해서는 고르지 않는다 (11/24: 곡 선택 목록 프리팹에서 UISelectOnHover를 뺐다).
/// - 고른 곡은 배경·글자색을 바꾼다. 에디터 프리팹은 프리팹 색, 곡 선택 프리팹(useMenuTheme)은 UITheme 색
///   (보통 = 패널 흰색 58% + 기본·보조 글자, 고름 = 포인트색 18% 바탕 + 왼쪽 막대).
/// - 곡 선택 프리팹만 쓰는 칸 (비워 두면 쓰지 않는다, 기획서 3-3 ② 11/17):
///   왼쪽 하늘색 막대(selectBar), 고르면 크게(높이·앨범·제목 크기), 둘째 줄 아티스트(showArtist),
///   오른쪽 난이도 칸 3개 (11/25): 레벨 숫자(고른 곡 24, 나머지 20), 기록이 있으면 숫자 밑에 작은 랭크(높이 18). 채보가 없는 난이도는 빈칸.
///   고른 곡의 고른 난이도 숫자는 난이도 색 + 밑줄 2px. FC는 목록에 띄우지 않는다. BPM은 쓰지 않는다.
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_SongItem : MonoBehaviour, ISelectHandler
{
    /// <summary>
    /// 오른쪽 난이도 칸 하나 (곡 선택 프리팹). difficulty는 GameConstants.Chart.Difficulties 중 하나.
    /// </summary>
    [Serializable]
    public class DifficultyColumn
    {
        public string difficulty;
        public TMP_Text levelText;
        [Tooltip("고른 난이도 숫자 밑줄 2px (Img_LevelUnderline)")]
        public Image underline;
        [Tooltip("최고 랭크 이미지 (Img_Rank, 기록이 없으면 빈칸)")]
        public Img_Rank rank;
    }

    [SerializeField] private Image background;
    [SerializeField] private Image cover;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("제목 칸 (TitleViewport). 고른 칸일 때만 긴 제목이 흐르고, 아니면 …")]
    [SerializeField] private UIMarqueeText titleMarquee;
    [SerializeField] private TMP_Text infoText;

    [Header("색")]
    [SerializeField] private Color chosenBackgroundColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color chosenTextColor = Color.white;
    [SerializeField] private Color emptyCoverColor = new Color32(0xD4, 0xD4, 0xD8, 0xFF);

    [Header("곡 선택 화면 전용 (비워 두면 쓰지 않는다)")]
    [Tooltip("켜면 위 색 대신 메뉴 화면 톤(UITheme)으로 칠한다")]
    [SerializeField] private bool useMenuTheme;
    [Tooltip("둘째 줄에 BPM·길이 대신 아티스트를 쓴다")]
    [SerializeField] private bool showArtist;
    [Tooltip("고른 곡 왼쪽 하늘색 막대 (Img_SelectBar)")]
    [SerializeField] private Image selectBar;
    [SerializeField] private LayoutElement layoutElement;
    [Tooltip("높이: 보통 / 고름 (0이면 바꾸지 않는다)")]
    [SerializeField] private float normalHeight;
    [SerializeField] private float chosenHeight;
    [Tooltip("앨범 한 변: 보통 / 고름 (0이면 바꾸지 않는다)")]
    [SerializeField] private float normalCoverSize;
    [SerializeField] private float chosenCoverSize;
    [Tooltip("제목 글자 크기: 보통 / 고름 (0이면 바꾸지 않는다)")]
    [SerializeField] private float normalTitleSize;
    [SerializeField] private float chosenTitleSize;
    [Tooltip("레벨 숫자 크기: 보통 / 고름 (0이면 바꾸지 않는다)")]
    [SerializeField] private float normalLevelSize;
    [SerializeField] private float chosenLevelSize;
    [Tooltip("난이도 칸 (EASY · NORMAL · HARD)")]
    [SerializeField] private DifficultyColumn[] difficultyColumns = new DifficultyColumn[0];

    /// <summary>밑줄이 숫자보다 긴 폭 (px)</summary>
    private const float UnderlineExtraWidth = 10f;

    private Button button;
    private string chosenDifficulty;
    private Color normalBackgroundColor;
    private Color normalTitleColor;
    private Color normalInfoColor;

    private SongData song;
    private Action<SongData> onChosen;

    /// <summary>내비게이션 연결과 클릭 리스너 연결용</summary>
    public Button Button => button;

    public SongData Song => song;

    /// <summary>고른 곡 표시 중이면 true (확인용)</summary>
    public bool IsChosen { get; private set; }

    /// <summary>제목 칸 (확인용)</summary>
    public UIMarqueeText TitleMarquee => titleMarquee;

    /// <summary>난이도 칸 (확인용)</summary>
    public IReadOnlyList<DifficultyColumn> DifficultyColumns => difficultyColumns;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// 버튼과 기본 색을 한 번만 가져온다. 꺼진 오브젝트 안에서 만들어져 Awake보다 Init이 먼저 불려도 쓸 수 있게 Init에서도 부른다.
    /// </summary>
    private void CacheComponents()
    {
        if (button != null) return;

        button = GetComponent<Button>();
        normalBackgroundColor = background.color;
        normalTitleColor = titleText.color;
        normalInfoColor = infoText.color;
    }

    /// <summary>
    /// 곡 정보를 표시하고, 이 곡이 선택됐을 때 부를 콜백을 보관한다.
    /// </summary>
    public void Init(SongData song, Action<SongData> onChosen)
    {
        CacheComponents();
        this.song = song;
        this.onChosen = onChosen;

        titleText.text = song.Title;

        if (showArtist)
        {
            infoText.text = string.IsNullOrEmpty(song.Artist) ? string.Empty : song.Artist;
        }
        else
        {
            infoText.text = $"BPM {BpmDisplayRules.Format(song, null)} · {song.GetLengthText()}";
        }

        cover.sprite = song.Cover;
        cover.color = song.Cover != null ? Color.white : (useMenuTheme ? UITheme.Current.emptyCover : emptyCoverColor);

        SetChosen(false);
    }

    /// <summary>
    /// 난이도 칸을 채운다 (곡 선택 화면). 채보 없는 난이도는 빈칸, 기록이 없으면 랭크 빈칸.
    /// </summary>
    /// <param name="charts">이 곡의 공식 채보</param>
    /// <param name="findRecord">chartId → 기록 (없으면 null)</param>
    public void SetDifficulties(IReadOnlyList<ChartData> charts, Func<string, ChartRecordData> findRecord)
    {
        if (difficultyColumns == null) return;

        foreach (DifficultyColumn column in difficultyColumns)
        {
            if (column == null) continue;

            ChartData chart = null;
            if (charts != null)
            {
                foreach (ChartData candidate in charts)
                {
                    if (candidate == null || !string.Equals(candidate.difficulty, column.difficulty, StringComparison.OrdinalIgnoreCase)) continue;
                    chart = candidate;
                    break;
                }
            }

            if (column.levelText != null) column.levelText.text = chart != null ? chart.level.ToString() : string.Empty;
            RecordTexts record = SongSelectRecordFormat.Build(chart != null ? findRecord?.Invoke(chart.chartId) : null);
            if (column.rank != null) column.rank.Show(record.HasRecord, record.Rank);
        }
        RefreshLevels();
    }

    /// <summary>고른 난이도 (고른 곡일 때만 보인다): 숫자를 난이도 색 + 밑줄로. 없으면 null</summary>
    public void SetChosenDifficulty(string difficulty)
    {
        chosenDifficulty = difficulty;
        RefreshLevels();
    }

    /// <summary>레벨 숫자 크기·색·밑줄을 고른 곡·고른 난이도에 맞춘다</summary>
    private void RefreshLevels()
    {
        if (difficultyColumns == null) return;

        foreach (DifficultyColumn column in difficultyColumns)
        {
            if (column == null || column.levelText == null) continue;

            bool marked = IsChosen && column.levelText.text.Length > 0 && !string.IsNullOrEmpty(chosenDifficulty)
                && string.Equals(column.difficulty, chosenDifficulty, StringComparison.OrdinalIgnoreCase);
            if (normalLevelSize > 0f && chosenLevelSize > 0f) column.levelText.fontSize = IsChosen ? chosenLevelSize : normalLevelSize;
            if (marked) column.levelText.color = UITheme.Current.GetDifficulty(column.difficulty);
            else UIThemePaint.Text(column.levelText, IsChosen ? UITextLevel.Primary : UITextLevel.Description);

            if (column.underline == null) continue;
            column.underline.gameObject.SetActive(marked);
            if (!marked) continue;
            column.underline.color = UITheme.Current.GetDifficulty(column.difficulty);
            RectTransform line = column.underline.rectTransform;
            line.sizeDelta = new Vector2(column.levelText.GetPreferredValues(column.levelText.text).x + UnderlineExtraWidth, line.sizeDelta.y);
        }
    }

    /// <summary>
    /// 고른 곡 표시를 켜거나 끈다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        CacheComponents();
        IsChosen = chosen;
        if (useMenuTheme)
        {
            UIThemePaint.SelectableRow(background, selectBar, chosen);
            UIThemePaint.Text(titleText, UITextLevel.Primary);
            UIThemePaint.Text(infoText, UITextLevel.Secondary);
        }
        else
        {
            background.color = chosen ? chosenBackgroundColor : normalBackgroundColor;
            titleText.color = chosen ? chosenTextColor : normalTitleColor;
            infoText.color = chosen ? chosenTextColor : normalInfoColor;
            if (selectBar != null) selectBar.gameObject.SetActive(chosen);
        }
        if (layoutElement != null && normalHeight > 0f && chosenHeight > 0f)
            layoutElement.preferredHeight = chosen ? chosenHeight : normalHeight;
        if (normalCoverSize > 0f && chosenCoverSize > 0f)
        {
            float size = chosen ? chosenCoverSize : normalCoverSize;
            cover.rectTransform.sizeDelta = new Vector2(size, size);
            LayoutElement coverLayout = cover.GetComponent<LayoutElement>();
            if (coverLayout != null)
            {
                coverLayout.preferredWidth = size;
                coverLayout.preferredHeight = size;
            }
        }
        if (normalTitleSize > 0f && chosenTitleSize > 0f)
            titleText.fontSize = chosen ? chosenTitleSize : normalTitleSize;
        if (titleMarquee != null) titleMarquee.SetPlaying(chosen);
        RefreshLevels();
    }

    /// <summary>
    /// 선택되면 바로 이 곡을 고른다.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        onChosen?.Invoke(song);
    }
}
