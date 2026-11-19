using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MenuBackground를 곡 선택·결과와 다르게 쓸 때의 값 (에디터: 아주 옅은 일러스트 + 세로 그라데이션, 11/19).
/// 값은 화면 스크립트가 UITheme에서 읽어 채운다.
/// </summary>
public struct MenuBackgroundStyle
{
    /// <summary>true면 덮는 색 그라데이션을 위 → 아래(세로)로, false면 곡 선택과 같은 가로 3단</summary>
    public bool vertical;
    /// <summary>세로 그라데이션 위 · 아래 불투명도 (vertical일 때)</summary>
    public float overlayTopAlpha;
    public float overlayBottomAlpha;
    /// <summary>곡 일러스트 불투명도 (0 ~ 1)</summary>
    public float illustrationOpacity;
    /// <summary>곡 일러스트 흐림 (1920×1080 기준 sigma px)</summary>
    public float blurSigmaPx;
    /// <summary>곡 일러스트 채도 · 밝기</summary>
    public float saturation;
    public float brightness;
}

/// <summary>
/// 메뉴 화면 배경 묶음 (기획서 3장 「UI 톤 · 라벤더 안개」, 3-3 ⑨). Prefabs/Common/MenuBackground 프리팹 루트(정렬용)에 붙인다.
/// 곡 선택이 쓰고, 결과 씬도 같은 프리팹을 쓸 수 있다.
/// - 아래부터: Img_BackgroundBase(바탕색 #E7E2EE) → Illustration(16:9 화면 덮기) 안 흐린 곡 일러스트 앞·뒤 RawImage(UIBlurBackground, 채도 45%·밝기 120%)
///   → Img_Overlay(덮는 색 #ECE8F2, 왼쪽 80% → 가운데 62% → 오른쪽 82% 가로 그라데이션)
/// - 그라데이션은 이미지 에셋 없이 Init에서 가로 텍스처를 코드로 한 번 만든다 (UITheme 값). 색은 모두 UITheme에서 읽는다.
/// - 화면 스크립트가 Init에서 Init()을 부르고, 곡이 바뀌면 SetIllustration을 부른다.
/// - 에디터는 Init(MenuBackgroundStyle)로 일러스트 불투명도 · 흐림 · 채도와 세로 그라데이션을 따로 정한다 (Init()은 곡 선택 · 결과 그대로).
/// </summary>
public class MenuBackground : MonoBehaviour
{
    /// <summary>그라데이션 텍스처 가로 칸 수 (bilinear로 늘린다)</summary>
    public const int OverlayTextureWidth = 64;

    [SerializeField] private Image baseImage;
    [SerializeField] private UIBlurBackground blur;
    [SerializeField] private RawImage overlayImage;

    private Texture2D overlayTexture;
    private bool isVertical;
    private float verticalTopAlpha;
    private float verticalBottomAlpha;

    /// <summary>흐린 곡 일러스트 (확인용)</summary>
    public UIBlurBackground Blur => blur;

    /// <summary>덮는 색 그라데이션 (확인용)</summary>
    public RawImage Overlay => overlayImage;

    /// <summary>
    /// 바탕색·블러 색 보정·덮는 색 그라데이션을 UITheme 값으로 맞춘다.
    /// </summary>
    public void Init()
    {
        UITheme theme = UITheme.Current;
        isVertical = false;
        if (baseImage != null) baseImage.color = theme.backgroundBase;
        if (blur != null) blur.SetColorGrade(theme.illustrationSaturation, theme.illustrationBrightness);
        SetIllustrationOpacity(1f);
        ApplyOverlay(theme);
    }

    /// <summary>
    /// 곡 선택 · 결과와 다른 값으로 맞춘다 (에디터). 바탕색 · 덮는 색은 UITheme 그대로, 일러스트 불투명도 · 흐림 · 채도 · 그라데이션 방향만 바꾼다.
    /// </summary>
    public void Init(MenuBackgroundStyle style)
    {
        UITheme theme = UITheme.Current;
        isVertical = style.vertical;
        verticalTopAlpha = style.overlayTopAlpha;
        verticalBottomAlpha = style.overlayBottomAlpha;
        if (baseImage != null) baseImage.color = theme.backgroundBase;
        if (blur != null)
        {
            blur.SetColorGrade(style.saturation, style.brightness);
            blur.SetBlurRadiusPx(style.blurSigmaPx, GameConstants.SongSelect.BlurIterations);
        }
        SetIllustrationOpacity(style.illustrationOpacity);
        ApplyOverlay(theme);
    }

    /// <summary>지금 일러스트 불투명도 (확인용)</summary>
    public float IllustrationOpacity => blur != null && blur.TryGetComponent(out CanvasGroup group) ? group.alpha : 1f;

    /// <summary>덮는 색이 세로 그라데이션이면 true (확인용)</summary>
    public bool IsVerticalOverlay => isVertical;

    /// <summary>일러스트(앞 · 뒤 RawImage) 전체 불투명도. 1이 아니면 CanvasGroup을 붙여 곱한다 (페이드 알파는 그대로)</summary>
    private void SetIllustrationOpacity(float opacity)
    {
        if (blur == null) return;
        CanvasGroup group = blur.GetComponent<CanvasGroup>();
        if (group == null)
        {
            if (Mathf.Approximately(opacity, 1f)) return;
            group = blur.gameObject.AddComponent<CanvasGroup>();
        }
        group.alpha = Mathf.Clamp01(opacity);
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ApplyOverlay(UITheme theme)
    {
        if (overlayImage == null) return;
        if (overlayTexture != null && (overlayTexture.height > 1) != isVertical)
        {
            DestroyTexture(overlayTexture);
            overlayTexture = null;
        }
        if (overlayTexture == null)
            overlayTexture = isVertical ? CreateVerticalOverlayTexture(theme, verticalTopAlpha, verticalBottomAlpha, OverlayTextureWidth) : CreateOverlayTexture(theme, OverlayTextureWidth);
        else if (isVertical) FillVerticalOverlayTexture(overlayTexture, theme, verticalTopAlpha, verticalBottomAlpha);
        else FillOverlayTexture(overlayTexture, theme);
        overlayImage.texture = overlayTexture;
        overlayImage.color = Color.white;
        overlayImage.raycastTarget = false;
    }

    /// <summary>곡 일러스트를 바꾼다 (같은 그림이면 다시 처리하지 않는다)</summary>
    /// <param name="instant">true면 페이드 없이 바로 (화면에 처음 들어올 때)</param>
    public void SetIllustration(Sprite sprite, bool instant)
    {
        if (blur != null) blur.SetSource(sprite, instant);
    }

    private void OnDestroy()
    {
        if (overlayTexture == null) return;
        DestroyTexture(overlayTexture);
        overlayTexture = null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (Application.isPlaying) Destroy(texture);
        else DestroyImmediate(texture);
    }

    /// <summary>
    /// 덮는 색 세로 그라데이션 텍스처 (1 × height, 위 = topAlpha → 아래 = bottomAlpha, 색 = UITheme.overlayColor).
    /// </summary>
    public static Texture2D CreateVerticalOverlayTexture(UITheme theme, float topAlpha, float bottomAlpha, int height)
    {
        Texture2D texture = new Texture2D(1, Mathf.Max(2, height), TextureFormat.RGBA32, false)
        {
            name = "MenuBackgroundOverlayVertical",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        FillVerticalOverlayTexture(texture, theme, topAlpha, bottomAlpha);
        return texture;
    }

    private static void FillVerticalOverlayTexture(Texture2D texture, UITheme theme, float topAlpha, float bottomAlpha)
    {
        int height = texture.height;
        Color[] pixels = new Color[height];
        // 텍스처 y는 아래부터: y = 0 이 화면 아래
        for (int y = 0; y < height; y++) pixels[y] = UITheme.WithAlpha(theme.overlayColor, Mathf.Lerp(bottomAlpha, topAlpha, y / (float)(height - 1)));
        texture.SetPixels(pixels);
        texture.Apply(false);
    }

    /// <summary>
    /// 덮는 색 가로 그라데이션 텍스처 (width × 1, 왼쪽 → 오른쪽 = UITheme.OverlayAt(0 → 1)).
    /// </summary>
    public static Texture2D CreateOverlayTexture(UITheme theme, int width)
    {
        Texture2D texture = new Texture2D(Mathf.Max(2, width), 1, TextureFormat.RGBA32, false)
        {
            name = "MenuBackgroundOverlay",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        FillOverlayTexture(texture, theme);
        return texture;
    }

    private static void FillOverlayTexture(Texture2D texture, UITheme theme)
    {
        int width = texture.width;
        Color[] pixels = new Color[width];
        for (int x = 0; x < width; x++) pixels[x] = theme.OverlayAt(x / (float)(width - 1));
        texture.SetPixels(pixels);
        texture.Apply(false);
    }
}
