using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 나만의 채보 탭의 "+ 새 채보 만들기" 줄 (기획서 3-3 「나만의 채보 탭 (11/18 개정)」, Prefabs/SongSelect/Btn_NewChart).
/// - 곡 선택은 이 줄 하나를 만들어 지금 고른 곡 묶음의 끝(채보가 없는 곡은 머리 줄 바로 아래)으로 옮겨 쓴다.
/// - 평소: 높이 46, 하늘색 점선 테두리(Img_Dash, 곡 선택의 점선 빈 자리와 같은 타일 방식) / 골랐을 때: 높이 60, 하늘색 실선(Img_Border) + 선택 바탕,
///   오른쪽에 "Enter 이름 · 레벨 입력" 힌트(Hint). 색은 UITheme (포인트색 · 선택 바탕).
/// - 선택(키보드·마우스 올림) = 이 줄을 고르기, 클릭 = 새 채보 팝업. ↑↓는 EventSystem 이동 이벤트를 곡 선택에 넘긴다 (Btn_MyChartItem과 같다).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_NewChart : MonoBehaviour, ISelectHandler, IMoveHandler
{
    [SerializeField] private Image background;
    [Tooltip("평소 점선 테두리 (Img_Dash)")]
    [SerializeField] private Image dashBorder;
    [Tooltip("골랐을 때 실선 테두리 (Img_Border)")]
    [SerializeField] private Image solidBorder;
    [Tooltip("+ 원 (Img_Plus) · + 글자")]
    [SerializeField] private Image plusImage;
    [SerializeField] private TMP_Text plusText;
    [SerializeField] private TMP_Text labelText;
    [Tooltip("골랐을 때만 보이는 오른쪽 힌트 (Hint: Img_EnterKey/Txt_EnterKey + Txt_Hint)")]
    [SerializeField] private GameObject hint;
    [SerializeField] private TMP_Text enterKeyText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private LayoutElement layoutElement;

    /// <summary>평소 · 골랐을 때 높이</summary>
    public const float NormalHeight = 46f;
    public const float ChosenHeight = 60f;

    private Button button;
    private Action onSelected;
    private Action onClicked;
    private Action<int> onMove;

    /// <summary>버튼 (확인용)</summary>
    public Button Button => button;

    /// <summary>골랐으면 true (확인용)</summary>
    public bool IsChosen { get; private set; }

    /// <summary>힌트 (확인용)</summary>
    public GameObject Hint => hint;

    /// <summary>
    /// 글자·색·콜백을 정한다 (곡 선택 Init에서 한 번).
    /// </summary>
    /// <param name="enterKey">Enter 키 이름 (바인딩에서 읽은 것)</param>
    public void Init(string enterKey, Action onSelected, Action onClicked, Action<int> onMove)
    {
        if (button == null) button = GetComponent<Button>();
        this.onSelected = onSelected;
        this.onClicked = onClicked;
        this.onMove = onMove;

        labelText.text = GameConstants.SongSelect.NewChartRowLabel;
        if (plusText != null) plusText.text = GameConstants.SongSelect.NewChartRowPlus;
        if (enterKeyText != null) enterKeyText.text = enterKey;
        if (hintText != null) hintText.text = GameConstants.SongSelect.NewChartRowHint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        SetChosen(false);
    }

    /// <summary>
    /// 골랐을 때 / 평소 모양: 높이 60 · 실선 · 선택 바탕 · 힌트 / 높이 46 · 점선.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        IsChosen = chosen;
        UITheme theme = UITheme.Current;
        if (background != null) background.color = chosen ? theme.SelectedBackground : UITheme.WithAlpha(theme.accent, 0f);
        if (dashBorder != null)
        {
            dashBorder.color = theme.accent;
            dashBorder.gameObject.SetActive(!chosen);
        }
        if (solidBorder != null)
        {
            solidBorder.color = theme.accent;
            solidBorder.gameObject.SetActive(chosen);
        }
        if (plusImage != null) plusImage.color = theme.accent;
        if (plusText != null) plusText.color = theme.onAccentText;
        labelText.color = theme.accent;
        if (hint != null) hint.SetActive(chosen);
        if (hintText != null) hintText.color = theme.textDescription;
        if (layoutElement != null) layoutElement.preferredHeight = chosen ? ChosenHeight : NormalHeight;
    }

    public void OnSelect(BaseEventData eventData)
    {
        onSelected?.Invoke();
    }

    /// <summary>EventSystem 이동 이벤트: ↑↓만 넘긴다</summary>
    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Up) onMove?.Invoke(-1);
        else if (eventData.moveDir == MoveDirection.Down) onMove?.Invoke(1);
    }

    private void OnClick()
    {
        onClicked?.Invoke();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }
}
