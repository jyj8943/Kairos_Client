using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 곡 고르기 화면의 내 채보 항목 하나 (Btn_MyChartItem 프리팹).
/// - 채보 이름과 "Lv.N · 노트 N"을 표시한다.
/// - 키보드·마우스로 선택되면 배경을 검게, 글자를 희게 강조한다 (Btn_SongItem과 같은 모양).
/// - 곡 항목과 달리 선택만으로는 열지 않고, 클릭·Enter 때 onChosen을 부른다.
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

    private Button button;
    private Color normalBackgroundColor;
    private Color normalNameColor;
    private Color normalInfoColor;

    private ChartData chart;
    private Action<ChartData> onChosen;

    /// <summary>내비게이션 연결용</summary>
    public Button Button => button;

    public ChartData Chart => chart;

    private void Awake()
    {
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
        this.chart = chart;
        this.onChosen = onChosen;

        chartNameText.text = chart.chartName;
        chartInfoText.text = string.Format(GameConstants.Editor.MyChartInfoFormat, chart.level, chart.notes != null ? chart.notes.Count : 0);

        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        SetSelected(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetSelected(true);
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

    private void SetSelected(bool selected)
    {
        background.color = selected ? selectedBackgroundColor : normalBackgroundColor;
        chartNameText.color = selected ? selectedTextColor : normalNameColor;
        chartInfoText.color = selected ? selectedTextColor : normalInfoColor;
    }
}
