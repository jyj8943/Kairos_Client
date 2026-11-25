using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 나만의 채보 탭의 "+ 새 채보 생성" 줄 (기획서 3-3 「나만의 채보 탭」, 11/25, Prefabs/SongSelect/Btn_NewChart).
/// - 곡 선택은 이 줄 하나를 만들어 지금 고른 곡 묶음의 끝(채보가 없는 곡은 머리 줄 바로 아래)으로 옮겨 쓴다.
/// - 높이 64, CUSTOM 보라 2px 점선 테두리(Img_Dash, 점선 스프라이트 타일). 골랐을 때만 보라 12% 칠과 오른쪽 Enter 키(Hint)가 보인다
///   (고르지 않았을 때 Enter는 채보 시작이라 키를 숨긴다). 색은 UITheme (difficultyCustom · newChartFillAlpha).
/// - 선택(키보드·마우스 올림) = 이 줄을 고르기, 클릭 = 새 채보 팝업. ↑↓는 EventSystem 이동 이벤트를 곡 선택에 넘긴다 (Btn_MyChartItem과 같다).
/// </summary>
[RequireComponent(typeof(Button))]
public class Btn_NewChart : MonoBehaviour, ISelectHandler, IMoveHandler
{
    [SerializeField] private Image background;
    [Tooltip("보라 2px 점선 테두리 (Img_Dash)")]
    [SerializeField] private Image dashBorder;
    [Tooltip("+ 원 (Img_Plus) · + 글자")]
    [SerializeField] private Image plusImage;
    [SerializeField] private TMP_Text plusText;
    [SerializeField] private TMP_Text labelText;
    [Tooltip("골랐을 때만 보이는 오른쪽 Enter 키 (Hint: Img_EnterKey/Txt_EnterKey)")]
    [SerializeField] private GameObject hint;
    [SerializeField] private TMP_Text enterKeyText;
    [SerializeField] private LayoutElement layoutElement;

    /// <summary>줄 높이 (고르거나 말거나 같다)</summary>
    public const float Height = 64f;

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
        if (layoutElement != null) layoutElement.preferredHeight = Height;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        SetChosen(false);
    }

    /// <summary>
    /// 골랐을 때 / 평소 모양: 보라 12% 칠 + Enter 키 / 점선만.
    /// </summary>
    public void SetChosen(bool chosen)
    {
        IsChosen = chosen;
        UITheme theme = UITheme.Current;
        if (background != null) background.color = UITheme.WithAlpha(theme.difficultyCustom, chosen ? theme.newChartFillAlpha : 0f);
        if (dashBorder != null) dashBorder.color = theme.difficultyCustom;
        if (plusImage != null) plusImage.color = theme.difficultyCustom;
        if (plusText != null) plusText.color = theme.onAccentText;
        labelText.color = theme.textPrimary;
        if (hint != null) hint.SetActive(chosen);
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
