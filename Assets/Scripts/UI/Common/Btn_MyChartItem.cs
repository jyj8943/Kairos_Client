using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 곡 선택 나만의 채보 탭의 채보 줄 하나 (기획서 3-3 「나만의 채보 탭 (11/17, 11/18 개정)」, Prefabs/SongSelect/Btn_MyChartItem).
/// - CUSTOM 뱃지 · 채보 이름 · Lv · 최고 랭크 이미지 (높이 26, FC는 띄우지 않음, 11/18).
/// - 키보드·마우스로 선택되면 곧바로 그 채보를 고른다 (SetSelectCallback). 고른 채보는 왼쪽 하늘색 막대(selectBar)와 포인트색 18% 바탕 (UITheme).
/// - ↑↓는 EventSystem의 Navigate(이동 이벤트)를 받아 넘긴다 (SetMoveCallback): 정지점에 + 줄이 끼어 있고 + 줄은 하나를 옮겨 쓰므로
///   Selectable 내비게이션 대신 곡 선택이 MyChartListNavigator로 다음 정지점을 정한다. ←→ 이동 이벤트는 무시한다 (곡 묶음 이동은 SongSelect 맵 액션).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_MyChartItem : MonoBehaviour, ISelectHandler, IMoveHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text chartNameText;
    [Tooltip("레벨 (\"Lv. 6\")")]
    [SerializeField] private TMP_Text chartInfoText;
    [Tooltip("고른 채보 왼쪽 하늘색 막대 (Img_SelectBar)")]
    [SerializeField] private Image selectBar;
    [Tooltip("최고 랭크 이미지 (Img_Rank, 기록이 없으면 Txt_RankEmpty \"—\")")]
    [SerializeField] private Img_Rank rank;

    private Button button;
    private ChartData chart;
    private Action<ChartData> onChosen;
    private Action<ChartData> onSelected;
    private Action<int> onMove;
    private bool isChosen;

    /// <summary>버튼 (내비게이션·클릭)</summary>
    public Button Button => button;

    public ChartData Chart => chart;

    /// <summary>고른 채보 표시 중이면 true (확인용)</summary>
    public bool IsChosen => isChosen;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>버튼을 한 번만 가져온다. 꺼진 패널 안에서 만들어져 Awake보다 Init이 먼저 불려도 쓸 수 있게 Init에서도 부른다.</summary>
    private void CacheComponents()
    {
        if (button == null) button = GetComponent<Button>();
    }

    /// <summary>
    /// 채보 정보를 표시하고, 클릭 때 부를 콜백을 보관한다.
    /// </summary>
    public void Init(ChartData chart, Action<ChartData> onChosen)
    {
        CacheComponents();
        this.chart = chart;
        this.onChosen = onChosen;

        chartNameText.text = chart.chartName;
        chartInfoText.text = string.Format(GameConstants.SongSelect.MyChartLevelFormat, chart.level);
        // ↑↓는 OnMove로 받으므로 Selectable 내비게이션은 끈다
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        SetChosen(false);
    }

    /// <summary>선택(키보드·마우스 올림)되면 부를 콜백 (선택 = 고르기)</summary>
    public void SetSelectCallback(Action<ChartData> onSelected)
    {
        this.onSelected = onSelected;
    }

    /// <summary>↑(-1)·↓(+1) 이동 이벤트를 받을 콜백</summary>
    public void SetMoveCallback(Action<int> onMove)
    {
        this.onMove = onMove;
    }

    /// <summary>최고 랭크 이미지를 채운다. 기록이 없으면 "—".</summary>
    public void SetRecord(ChartRecordData record)
    {
        RecordTexts texts = SongSelectRecordFormat.Build(record);
        if (rank != null) rank.Show(texts.HasRecord, texts.Rank);
    }

    /// <summary>최고 랭크 이미지 (확인용)</summary>
    public Img_Rank RankImage => rank;

    /// <summary>채보 이름 글자 (확인용)</summary>
    public TMP_Text ChartNameText => chartNameText;

    /// <summary>
    /// 고른 채보 표시를 켜거나 끈다: 포인트색 18% 바탕 + 왼쪽 막대 (UIThemePaint.SelectableRow). EventSystem 선택이 풀려도 남는다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        isChosen = chosen;
        UIThemePaint.SelectableRow(background, selectBar, chosen);
        UIThemePaint.Text(chartNameText, UITextLevel.Primary);
        UIThemePaint.Text(chartInfoText, UITextLevel.Primary);
    }

    public void OnSelect(BaseEventData eventData)
    {
        onSelected?.Invoke(chart);
    }

    /// <summary>EventSystem 이동 이벤트: ↑↓만 넘긴다</summary>
    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Up) onMove?.Invoke(-1);
        else if (eventData.moveDir == MoveDirection.Down) onMove?.Invoke(1);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        onChosen?.Invoke(chart);
    }
}
