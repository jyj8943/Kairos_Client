using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 카이로스 대화창 (11/25, 590×160). Canvas_Result가 Init()을 부르고 등장 연출 값을 SetState로 넣는다.
/// - 바탕(이 오브젝트 Image) · 테두리 2px(RoundedRect20Frame 1px 두 겹: Img_DialogFrame · Img_DialogFrameInner).
/// - 꼬리: DialogTail(RectMask2D, 아래 끝 = 테두리 안쪽) └ 45° 돌린 Img_DialogTail └ Img_DialogTailFrame.
///   마스크 아래로는 꼬리가 잘리고 꼬리 바탕이 본체 위쪽 테두리를 덮어 본체와 이어져 보인다.
/// - 글자 3줄: 라벨 · 곡 키워드(비어 있으면 숨김, 다른 줄 위치는 그대로) · 랭크 멘트. 색은 UITheme, 문구는 GameConstants.
/// </summary>
[RequireComponent(typeof(Image), typeof(CanvasGroup))]
public class Img_Dialog : MonoBehaviour
{
    [Tooltip("바탕 (본체 · 꼬리)")]
    [SerializeField] private Image[] fills;
    [Tooltip("테두리 (본체 두 겹 · 꼬리)")]
    [SerializeField] private Image[] borders;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text keywordText;
    [SerializeField] private TMP_Text lineText;

    private CanvasGroup group;

    /// <summary>알파 · 크기 · 글자 (확인용)</summary>
    public float Alpha => Group.alpha;
    public float Scale => transform.localScale.x;
    public TMP_Text LabelText => labelText;
    public TMP_Text KeywordText => keywordText;
    public TMP_Text LineText => lineText;

    private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

    /// <summary>색을 칠하고 글자를 넣는다. 처음에는 숨긴 상태</summary>
    /// <param name="keyword">곡의 감정 키워드 (비어 있으면 그 줄을 숨긴다)</param>
    /// <param name="line">랭크 멘트</param>
    public void Init(string keyword, string line)
    {
        UITheme theme = UITheme.Current;
        foreach (Image fill in fills) fill.color = theme.resultDialogFill;
        foreach (Image border in borders) border.color = theme.resultDialogBorder;
        labelText.color = theme.resultDialogLabel;
        keywordText.color = theme.resultDialogKeyword;
        lineText.color = theme.resultDialogLine;

        labelText.text = GameConstants.Result.DialogLabel;
        keywordText.text = keyword ?? string.Empty;
        keywordText.gameObject.SetActive(!string.IsNullOrEmpty(keyword));
        lineText.text = line ?? string.Empty;

        Group.interactable = false;
        Group.blocksRaycasts = false;
        SetState(0f, 1f);
    }

    /// <summary>등장 연출 값 (알파 · 크기)</summary>
    public void SetState(float alpha, float scale)
    {
        Group.alpha = alpha;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
