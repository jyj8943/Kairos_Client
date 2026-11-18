using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FULL COMBO 이미지 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」). 곡 선택 기록 카드(COMBO 글자 옆, 폭 30)와 결과(MAX COMBO 카드 오른쪽, 폭 78)가 쓴다.
/// - 이미지는 RankSprites.FullCombo (Clear_FC). 폭(width)에 맞추고 세로는 비율대로.
/// - FC(MISS 0개)면 켜고 아니면 끈다. AP면 FC도 true라 함께 보인다. 곡 목록에는 두지 않는다.
/// - RectTransform 앵커는 한 점으로 둔다 (sizeDelta = 실제 크기).
/// </summary>
[RequireComponent(typeof(Image))]
public class Img_FullCombo : MonoBehaviour
{
    [Tooltip("이미지 폭 (기록 카드 30 · 결과 78). 세로는 이미지 비율대로")]
    [Min(1f)] [SerializeField] private float width = 30f;

    private Image image;

    /// <summary>FC 이미지를 보이는 중이면 true (확인용)</summary>
    public bool IsShown => gameObject.activeSelf;

    /// <summary>이미지 실제 크기 (확인용)</summary>
    public Vector2 Size => ((RectTransform)transform).sizeDelta;

    /// <summary>
    /// FC면 이미지를 켜고 크기를 맞춘다. 아니면 끈다.
    /// </summary>
    public void SetFullCombo(bool isFullCombo)
    {
        if (!isFullCombo)
        {
            gameObject.SetActive(false);
            return;
        }

        if (image == null) image = GetComponent<Image>();
        Sprite sprite = RankSprites.FullCombo;
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        ((RectTransform)transform).sizeDelta = new Vector2(width, width / RankSprites.AspectOf(sprite));
        gameObject.SetActive(sprite != null);
    }
}
