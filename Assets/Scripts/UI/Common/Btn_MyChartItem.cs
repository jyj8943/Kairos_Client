using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 내 채보 항목 하나. 에디터 곡 고르기(Prefabs/Common/Btn_MyChartItem)와 곡 선택 화면 나만의 채보 탭(Prefabs/SongSelect/Btn_MyChartItem)이 같이 쓴다.
/// - 에디터: 채보 이름과 "Lv.N · 노트 N"을 표시하고, 선택만으로는 열지 않고 클릭·Enter 때 onChosen을 부른다.
/// - 곡 선택(기획서 3-3 나만의 채보 탭 11/17): CUSTOM 뱃지 · 채보 이름 · Lv · 최고 랭크 + FC/AP 도장.
///   키보드·마우스로 선택되면 곧바로 그 채보를 고른다 (SetSelectCallback). 고른 채보는 왼쪽 하늘색 막대(selectBar)와 밝은 배경.
/// - 강조: 선택(또는 고른 채보)이면 배경·글자색을 바꾼다 (색은 프리팹 값).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_MyChartItem : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text chartNameText;
    [SerializeField] private TMP_Text chartInfoText;

    [Header("색")]
    [SerializeField] private Color selectedBackgroundColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color selectedTextColor = Color.white;

    [Header("곡 선택 화면 전용 (비워 두면 쓰지 않는다)")]
    [Tooltip("켜면 chartInfoText에 레벨만 쓴다 (\"Lv. 6\")")]
    [SerializeField] private bool showLevelOnly;
    [Tooltip("고른 채보 왼쪽 하늘색 막대 (Img_SelectBar)")]
    [SerializeField] private Image selectBar;
    [Tooltip("최고 랭크 (기록이 없으면 —)")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Img_Stamp stamp;

    private Button button;
    private Color normalBackgroundColor;
    private Color normalNameColor;
    private Color normalInfoColor;

    private ChartData chart;
    private Action<ChartData> onChosen;
    private Action<ChartData> onSelected;
    private bool isChosen;

    /// <summary>내비게이션 연결용</summary>
    public Button Button => button;

    public ChartData Chart => chart;

    /// <summary>고른 채보 표시 중이면 true (확인용)</summary>
    public bool IsChosen => isChosen;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// 버튼과 기본 색을 한 번만 가져온다. 꺼진 패널 안에서 만들어져 Awake보다 Init이 먼저 불려도 쓸 수 있게 Init에서도 부른다.
    /// </summary>
    private void CacheComponents()
    {
        if (button != null) return;

        button = GetComponent<Button>();
        normalBackgroundColor = background.color;
        normalNameColor = chartNameText.color;
        normalInfoColor = chartInfoText.color;
    }

    /// <summary>
    /// 채보 정보를 표시하고, 클릭·Enter 때 부를 콜백을 보관한다. 풀에서 다시 쓸 때도 이 함수로 채운다.
    /// </summary>
    public void Init(ChartData chart, Action<ChartData> onChosen)
    {
        CacheComponents();
        this.chart = chart;
        this.onChosen = onChosen;

        chartNameText.text = chart.chartName;
        chartInfoText.text = showLevelOnly
            ? string.Format(GameConstants.SongSelect.MyChartLevelFormat, chart.level)
            : string.Format(GameConstants.Editor.MyChartInfoFormat, chart.level, chart.notes != null ? chart.notes.Count : 0);

        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        isChosen = false;
        SetSelected(false);
    }

    /// <summary>
    /// 선택(키보드·마우스 올림)되면 부를 콜백 (곡 선택 화면: 선택 = 고르기). 에디터는 부르지 않는다.
    /// </summary>
    public void SetSelectCallback(Action<ChartData> onSelected)
    {
        this.onSelected = onSelected;
    }

    /// <summary>
    /// 최고 랭크와 FC/AP 도장을 채운다 (곡 선택 화면). 기록이 없으면 "—", 도장 없음.
    /// </summary>
    public void SetRecord(ChartRecordData record)
    {
        RecordTexts texts = SongSelectRecordFormat.Build(record);
        if (rankText != null)
        {
            rankText.text = texts.Rank;
            rankText.color = texts.IsRankS ? (Color)GameConstants.SongSelect.RankSColor : Color.white;
        }
        if (stamp != null) stamp.SetStamp(texts.Stamp);
    }

    /// <summary>
    /// 고른 채보 표시를 켜거나 끈다 (곡 선택 화면용). 켜 두면 EventSystem 선택이 풀려도 강조가 남는다.
    /// 에디터 곡 고르기는 부르지 않으므로 선택될 때만 강조한다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        isChosen = chosen;
        SetSelected(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetSelected(true);
        onSelected?.Invoke(chart);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        onChosen?.Invoke(chart);
    }

    /// <summary>
    /// 선택(또는 고른 채보)이면 강조 배경·글자색으로 한다. 막대는 고른 채보일 때만.
    /// </summary>
    private void SetSelected(bool selected)
    {
        bool highlighted = selected || isChosen;
        background.color = highlighted ? selectedBackgroundColor : normalBackgroundColor;
        chartNameText.color = highlighted ? selectedTextColor : normalNameColor;
        chartInfoText.color = highlighted ? selectedTextColor : normalInfoColor;
        if (selectBar != null) selectBar.gameObject.SetActive(isChosen);
    }
}
