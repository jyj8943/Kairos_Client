using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 곡 고르기 목록의 곡 아이템 하나 (Btn_SongItem 프리팹).
/// - 키보드나 마우스로 선택되면 곧바로 그 곡을 고른다. (클릭 없이 이동만으로 곡이 바뀜)
/// - 고른 곡은 배경을 검게, 글자를 희게 표시한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_SongItem : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image cover;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text infoText;

    [Header("색")]
    [SerializeField] private Color chosenBackgroundColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    [SerializeField] private Color chosenTextColor = Color.white;
    [SerializeField] private Color emptyCoverColor = new Color32(0xD4, 0xD4, 0xD8, 0xFF);

    private Button button;
    private Color normalBackgroundColor;
    private Color normalTitleColor;
    private Color normalInfoColor;

    private SongData song;
    private Action<SongData> onChosen;

    /// <summary>내비게이션 연결과 클릭 리스너 연결용</summary>
    public Button Button => button;

    public SongData Song => song;

    private void Awake()
    {
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
        this.song = song;
        this.onChosen = onChosen;

        titleText.text = song.Title;

        // 반올림하지 않는다: 정수면 "216", 소수면 둘째 자리까지 "150.61"
        string bpmText = song.Bpm > 0f ? song.Bpm.ToString("0.##", CultureInfo.InvariantCulture) : "-";
        infoText.text = $"BPM {bpmText} · {song.GetLengthText()}";

        cover.sprite = song.Cover;
        cover.color = song.Cover != null ? Color.white : emptyCoverColor;

        SetChosen(false);
    }

    /// <summary>
    /// 고른 곡 표시를 켜거나 끈다.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        background.color = chosen ? chosenBackgroundColor : normalBackgroundColor;
        titleText.color = chosen ? chosenTextColor : normalTitleColor;
        infoText.color = chosen ? chosenTextColor : normalInfoColor;
    }

    /// <summary>
    /// 선택되면 바로 이 곡을 고른다.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        onChosen?.Invoke(song);
    }
}
