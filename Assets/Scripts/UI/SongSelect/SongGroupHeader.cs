using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 나만의 채보 탭 목록의 곡 머리 줄 (기획서 3-3 「나만의 채보 탭 (11/17, 11/18 개정)」). 정렬용 루트(SongGroupHeader)에 붙는다.
/// - 작은 앨범 · 제목 · 아티스트 · 채보 수 ("2 CHARTS", 채보가 없으면 "NO CHART")를 보여 준다. 모든 곡이 머리 줄을 가진다 (11/18).
/// - 글자색은 UITheme (제목 기본 · 고른 곡이면 myChartChosenSongTitle, 아티스트·채보 수 설명), 가로선은 구분선 색(UIThemeColor). 앨범이 없으면 빈 앨범 색.
/// - 선택되지 않는 줄이다 (Selectable 없음, 정지점 아님). ↑↓는 채보 줄과 + 줄만 오간다.
/// </summary>
public class SongGroupHeader : MonoBehaviour
{
    [SerializeField] private Image cover;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private TMP_Text countText;

    [Header("긴 제목·아티스트는 이 폭에서 줄임표 (가로선·채보 수가 밀려나지 않게)")]
    [SerializeField] private LayoutElement titleLayout;
    [SerializeField] private LayoutElement artistLayout;
    [SerializeField] private float maxTitleWidth = 360f;
    [SerializeField] private float maxArtistWidth = 140f;

    /// <summary>이 줄의 곡 (확인용)</summary>
    public SongData Song { get; private set; }

    /// <summary>지금 고른 곡의 머리 줄이면 true (확인용)</summary>
    public bool IsChosen { get; private set; }

    /// <summary>제목 글자 (확인용)</summary>
    public TMP_Text TitleText => titleText;

    /// <summary>채보 수 글자 (확인용)</summary>
    public TMP_Text CountText => countText;

    /// <summary>
    /// 곡 정보와 채보 수를 채운다. 채보가 0개면 "NO CHART".
    /// </summary>
    public void Init(SongData song, int chartCount)
    {
        Song = song;
        cover.sprite = song.Cover;
        cover.color = song.Cover != null ? Color.white : UITheme.Current.emptyCover;
        UIThemePaint.Text(artistText, UITextLevel.Description);
        UIThemePaint.Text(countText, UITextLevel.Description);
        titleText.text = song.Title;
        artistText.text = string.IsNullOrEmpty(song.Artist) ? string.Empty : song.Artist;
        countText.text = FormatCount(chartCount);
        SetChosen(false);
        RefreshWidths();
    }

    /// <summary>채보 수 글자 (순수 함수): 0 → "NO CHART", 1 → "1 CHART", 그 밖 → "n CHARTS"</summary>
    public static string FormatCount(int chartCount)
    {
        if (chartCount <= 0) return GameConstants.SongSelect.GroupNoChart;
        return chartCount == 1
            ? GameConstants.SongSelect.GroupChartCountOne
            : string.Format(GameConstants.SongSelect.GroupChartCountFormat, chartCount);
    }

    /// <summary>지금 고른 곡이면 제목을 하늘색(#2F7FC8), 아니면 기본 글자색으로</summary>
    public void SetChosen(bool chosen)
    {
        IsChosen = chosen;
        titleText.color = chosen ? UITheme.Current.myChartChosenSongTitle : UITheme.Current.textPrimary;
    }

    /// <summary>
    /// 제목·아티스트 폭을 글자 길이만큼(최대 maxTitleWidth·maxArtistWidth) 잡는다. 넘는 부분은 줄임표(TMP Ellipsis).
    /// 제목을 Init 뒤에 바꿨으면 다시 부른다.
    /// </summary>
    public void RefreshWidths()
    {
        if (titleLayout != null) titleLayout.preferredWidth = Mathf.Min(titleText.GetPreferredValues(titleText.text).x, maxTitleWidth);
        if (artistLayout != null) artistLayout.preferredWidth = Mathf.Min(artistText.GetPreferredValues(artistText.text).x, maxArtistWidth);
    }
}
