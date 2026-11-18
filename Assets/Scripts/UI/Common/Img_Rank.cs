using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 랭크 이미지 하나 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」). 곡 선택 기록 카드·곡 목록(공식·나만의 채보)·결과가 같이 쓴다.
/// - 이미지는 RankSprites (Rank_AP · Rank_S ~ Rank_F). 색은 칠하지 않는다 (이미지 그대로, 흰색).
/// - 크기: 높이(height)에 맞추고 가로는 비율대로 → S~F는 정사각형, AP는 같은 높이로 가로로 길다.
///   가로가 랭크 칸 폭(maxWidth)을 넘으면 비율을 지키며 칸 폭에 맞게 줄인다.
/// - 기록이 없으면 이미지를 숨기고 빈 자리(emptyMark: 기록 카드는 점선 빈 자리 Img_RankEmpty, 목록은 "—" 글자 Txt_RankEmpty)를 켠다.
///   빈 자리가 글자면 "—"(GameConstants.SongSelect.EmptyValue)를 흐린 글자색으로 쓴다.
/// - RectTransform 앵커는 한 점(가운데 등)으로 둔다 (sizeDelta = 실제 크기).
/// </summary>
[RequireComponent(typeof(Image))]
public class Img_Rank : MonoBehaviour
{
    [Tooltip("이미지 높이 (기록 카드 62 · 목록 26 · 결과 150)")]
    [Min(1f)] [SerializeField] private float height = 62f;
    [Tooltip("랭크 칸 폭. 가로가 이보다 길면 비율을 지키며 줄인다 (0이면 제한 없음)")]
    [Min(0f)] [SerializeField] private float maxWidth;
    [Tooltip("기록이 없을 때 대신 켤 것 (기록 카드 Img_RankEmpty · 목록 Txt_RankEmpty). 비우면 숨기기만 한다")]
    [SerializeField] private GameObject emptyMark;

    private Image image;

    /// <summary>랭크를 보이는 중이면 true (확인용)</summary>
    public bool HasRank { get; private set; }

    /// <summary>보이는 랭크 (HasRank일 때만 뜻이 있다, 확인용)</summary>
    public PlayRank Rank { get; private set; }

    /// <summary>이미지 실제 크기 (확인용)</summary>
    public Vector2 Size => ((RectTransform)transform).sizeDelta;

    /// <summary>지금 스프라이트 (확인용)</summary>
    public Sprite Sprite => Image.sprite;

    /// <summary>빈 자리 오브젝트 (확인용)</summary>
    public GameObject EmptyMark => emptyMark;

    private Image Image
    {
        get
        {
            if (image == null) image = GetComponent<Image>();
            return image;
        }
    }

    /// <summary>
    /// 기록이 있으면 그 랭크 이미지를, 없으면 빈 자리를 보인다.
    /// </summary>
    public void Show(bool hasRecord, PlayRank rank)
    {
        if (hasRecord) SetRank(rank);
        else SetEmpty();
    }

    /// <summary>랭크 이미지를 보이고 크기를 맞춘다 (빈 자리는 끈다)</summary>
    public void SetRank(PlayRank rank)
    {
        Sprite sprite = RankSprites.Get(rank);
        HasRank = true;
        Rank = rank;

        Image target = Image;
        target.sprite = sprite;
        target.preserveAspect = true;
        target.color = Color.white;
        ((RectTransform)transform).sizeDelta = FitSize(RankSprites.AspectOf(sprite), height, maxWidth);

        gameObject.SetActive(sprite != null);
        if (emptyMark != null) emptyMark.SetActive(false);
    }

    /// <summary>기록이 없을 때: 이미지를 숨기고 빈 자리를 켠다</summary>
    public void SetEmpty()
    {
        HasRank = false;
        gameObject.SetActive(false);
        if (emptyMark == null) return;

        emptyMark.SetActive(true);
        TMP_Text text = emptyMark.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = GameConstants.SongSelect.EmptyValue;
            text.color = UITheme.Current.textMuted;
        }
    }

    /// <summary>
    /// 높이에 맞춘 크기 (가로 = 높이 × 비율). 가로가 maxWidth를 넘으면 비율을 지키며 maxWidth에 맞춘다 (순수 함수).
    /// </summary>
    public static Vector2 FitSize(float aspect, float height, float maxWidth)
    {
        float width = height * aspect;
        if (maxWidth > 0f && width > maxWidth)
        {
            height *= maxWidth / width;
            width = maxWidth;
        }
        return new Vector2(width, height);
    }
}
