using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오른쪽 패널 BPM 구간 목록의 줄 하나 (Prefabs/EditorScene/Btn_BpmSectionItem 프리팹). ScrollView_BpmSections가 Init()을 호출한다.
/// - "마디 M · B박" + BPM, 곡 시작 줄에는 "곡 시작"을 표시한다.
/// - 줄 높이 44: 위치 15 Bold · 태그 12 · 값 17 Bold 호박 (11/19 라벤더 톤). 색은 UITheme (에디터 호박 editorAmber).
/// - 현재 위치가 속한 구간이면 왼쪽 호박 4px 막대(Img_SelectBar) + 호박 14% 바탕으로 강조한다.
/// - 클릭하면 그 구간 tick을 넘긴다 (그 구간선이 현재 위치 선에 오도록 이동).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_BpmSectionItem : MonoBehaviour
{
    [SerializeField] private Image background;
    [Tooltip("고른 줄 왼쪽 호박 막대 (Img_SelectBar, 4px)")]
    [SerializeField] private Image selectBar;
    [SerializeField] private TMP_Text positionText;
    [Tooltip("곡 시작 줄 표시 (Txt_BpmSectionStart)")]
    [SerializeField] private TMP_Text startText;
    [SerializeField] private TMP_Text bpmText;

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

    /// <summary>바탕 (확인용)</summary>
    public Image Background => background;

    /// <summary>고른 줄 막대 (확인용)</summary>
    public Image SelectBar => selectBar;

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

    /// <summary>현재 위치가 속한 구간이면 호박 막대 + 호박 14% 바탕으로 강조한다 (글자 색은 그대로)</summary>
    public void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        UITheme theme = UITheme.Current;
        background.color = highlighted ? UITheme.WithAlpha(theme.editorAmber, theme.editorAmberSoftAlpha) : Color.clear;
        if (selectBar != null)
        {
            selectBar.color = theme.editorAmber;
            selectBar.enabled = highlighted;
        }
        positionText.color = theme.textPrimary;
        startText.color = theme.textDescription;
        bpmText.color = theme.editorAmber;
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
