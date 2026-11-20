using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 숏노트 표시 (Img_ShortNote 프리팹). EditorNoteLayer가 풀에서 꺼내 Init(lane)과 SetSize(size)를 호출한다.
/// - 인게임 숏노트와 같은 모양 · 색 (11/20): 본체 = ShortNote 스프라이트(Sliced) × 라인 색(UITheme.NoteColor, 바깥 라벤더 · 안쪽 하늘),
///   자식 Img_Shine = ShortNote_Shine(Sliced) 흰색 광택(UITheme.noteShine, 라인 색을 입히지 않음). 빛 번짐은 없다.
/// - 광택 크기·위치는 인게임 NoteShape를 그리드 px(unitsPerPx = 1)로 그대로 쓴다: (노트 폭 − 6) × 4, 위 끝에서 2px 아래.
/// - 9-slice 배율은 GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier (노트 높이 대비 테두리 비율이 인게임과 같다).
/// - 위치는 EditorNoteLayer가 정한다. 클릭은 노트가 아니라 GridArea에서 받으므로 raycastTarget을 끈다.
/// - 롱노트 머리(Img_LongNote의 Img_LongHead)도 Apply로 같은 모양을 그린다.
/// </summary>
public class Img_ShortNote : MonoBehaviour
{
    [Tooltip("위 광택 Img_Shine (ShortNote_Shine, Sliced)")]
    [SerializeField] private Image shine;

    private Image image;

    /// <summary>노트 크기·위치를 정할 때 쓰는 RectTransform</summary>
    public RectTransform RectTransform { get; private set; }

    /// <summary>본체 Image (확인용)</summary>
    public Image Body => image;

    /// <summary>광택 Image (확인용)</summary>
    public Image Shine => shine;

    private void Awake()
    {
        image = GetComponent<Image>();
        RectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// 라인 색과 광택 색을 적용한다.
    /// </summary>
    public void Init(int lane)
    {
        if (lane < 0 || lane >= GameConstants.Editor.LaneCount)
        {
            Debug.LogWarning($"[Img_ShortNote] 라인 번호가 범위 밖입니다: lane {lane}", this);
            return;
        }

        UITheme theme = UITheme.Current;
        ApplyColors(image, shine, theme.NoteColor(lane), theme.noteShine);
    }

    /// <summary>노트 크기를 정하고 광택을 그 크기에 맞춰 놓는다.</summary>
    public void SetSize(Vector2 size)
    {
        RectTransform.sizeDelta = size;
        ApplyShape(image, shine, size);
    }

    /// <summary>
    /// 숏노트 모양 본체 · 광택 색을 칠한다 (숏노트와 롱노트 머리가 같이 쓴다).
    /// </summary>
    public static void ApplyColors(Image body, Image shine, Color bodyColor, Color shineColor)
    {
        if (body != null) body.color = bodyColor;
        if (shine != null) shine.color = shineColor;
    }

    /// <summary>
    /// 숏노트 모양을 놓는다: 본체 크기, 9-slice 배율, 광택 크기 · 위치 (인게임 NoteShape, 그리드 px 기준 unitsPerPx = 1).
    /// 광택은 본체 가운데 기준(앵커 · 피벗 가운데)으로 놓는다.
    /// </summary>
    public static void ApplyShape(Image body, Image shine, Vector2 size)
    {
        if (body != null)
        {
            body.rectTransform.sizeDelta = size;
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier;
        }
        if (shine == null) return;

        RectTransform rect = shine.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = NoteShape.ShineSize(size, 1f);
        rect.anchoredPosition = new Vector2(0f, NoteShape.ShineLocalY(size, 1f));
        shine.type = Image.Type.Sliced;
        shine.pixelsPerUnitMultiplier = GameConstants.Editor.NoteSpritePixelsPerUnitMultiplier;
    }
}
