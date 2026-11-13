using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오른쪽 패널 BPM 구간 목록의 줄 하나 (Prefabs/EditorScene/Btn_BpmSectionItem 프리팹). ScrollView_BpmSections가 Init()을 호출한다.
/// - "마디 M · B박" + BPM, 곡 시작 줄에는 "곡 시작"을 표시한다.
/// - 현재 위치가 속한 구간이면 청록 테두리·연한 청록 바탕으로 강조한다.
/// - 클릭하면 그 구간 tick을 넘긴다 (그 구간선이 현재 위치 선에 오도록 이동).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_BpmSectionItem : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Outline outline;
    [SerializeField] private TMP_Text positionText;
    [Tooltip("곡 시작 줄 표시 (Txt_BpmSectionStart)")]
    [SerializeField] private TMP_Text startText;
    [SerializeField] private TMP_Text bpmText;

    [Header("색 (기획서 3-6)")]
    [SerializeField] private Color normalBackgroundColor = Color.white;
    [SerializeField] private Color normalOutlineColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color normalBpmColor = new Color32(0x27, 0x27, 0x2A, 0xFF);
    [SerializeField] private Color highlightBackgroundColor = new Color32(0xEC, 0xFE, 0xFF, 0xFF);
    [SerializeField] private Color highlightOutlineColor = new Color32(0x08, 0x91, 0xB2, 0xFF);
    [SerializeField] private Color highlightBpmColor = new Color32(0x0E, 0x74, 0x90, 0xFF);

    private Button button;
    private Action<int> onChosen;
    private int tick;

    /// <summary>이 줄의 구간 tick</summary>
    public int Tick => tick;

    /// <summary>강조 중이면 true (확인용)</summary>
    public bool IsHighlighted { get; private set; }

    /// <summary>위치 글자 (확인용)</summary>
    public TMP_Text PositionText => positionText;

    /// <summary>BPM 글자 (확인용)</summary>
    public TMP_Text BpmText => bpmText;

    /// <summary>
    /// 구간 하나를 표시하고 클릭 콜백을 보관한다. 풀에서 다시 쓸 때도 이 함수로 채운다.
    /// </summary>
    public void Init(BpmSection section, Action<int> onChosen)
    {
        if (button == null)
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);
        }

        this.onChosen = onChosen;
        tick = section.Tick;
        positionText.text = BpmSectionRules.FormatPosition(section.Tick);
        bpmText.text = SongData.FormatBpm(section.Bpm);
        startText.text = GameConstants.Editor.BpmSectionStartLabel;
        startText.gameObject.SetActive(section.IsStart);
        SetHighlighted(false);
    }

    /// <summary>현재 위치가 속한 구간이면 청록으로 강조한다</summary>
    public void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        background.color = highlighted ? highlightBackgroundColor : normalBackgroundColor;
        if (outline != null) outline.effectColor = highlighted ? highlightOutlineColor : normalOutlineColor;
        bpmText.color = highlighted ? highlightBpmColor : normalBpmColor;
    }

    private void OnClick()
    {
        onChosen?.Invoke(tick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }
}
