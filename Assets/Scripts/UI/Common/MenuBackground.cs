using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 화면 배경 묶음 (기획서 3장 「UI 톤 · 라벤더 안개」, 3-3 ⑨). Prefabs/Common/MenuBackground 프리팹 루트(정렬용)에 붙인다.
/// 곡 선택이 쓰고, 결과 씬도 같은 프리팹을 쓸 수 있다.
/// - 아래부터: Img_BackgroundBase(바탕색 #E7E2EE) → Illustration(16:9 화면 덮기) 안 흐린 곡 일러스트 앞·뒤 RawImage(UIBlurBackground, 채도 45%·밝기 120%)
///   → Img_Overlay(덮는 색 #ECE8F2, 왼쪽 80% → 가운데 62% → 오른쪽 82% 가로 그라데이션)
/// - 그라데이션은 이미지 에셋 없이 Init에서 가로 텍스처를 코드로 한 번 만든다 (UITheme 값). 색은 모두 UITheme에서 읽는다.
/// - 화면 스크립트가 Init에서 Init()을 부르고, 곡이 바뀌면 SetIllustration을 부른다.
/// </summary>
public class MenuBackground : MonoBehaviour
{
    /// <summary>그라데이션 텍스처 가로 칸 수 (bilinear로 늘린다)</summary>
    public const int OverlayTextureWidth = 64;

    [SerializeField] private Image baseImage;
    [SerializeField] private UIBlurBackground blur;
    [SerializeField] private RawImage overlayImage;

    private Texture2D overlayTexture;

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
        if (baseImage != null) baseImage.color = theme.backgroundBase;
        if (blur != null) blur.SetColorGrade(theme.illustrationSaturation, theme.illustrationBrightness);

        if (overlayImage != null)
        {
            if (overlayTexture == null) overlayTexture = CreateOverlayTexture(theme, OverlayTextureWidth);
            else FillOverlayTexture(overlayTexture, theme);
            overlayImage.texture = overlayTexture;
            overlayImage.color = Color.white;
            overlayImage.raycastTarget = false;
        }
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
        if (Application.isPlaying) Destroy(overlayTexture);
        else DestroyImmediate(overlayTexture);
        overlayTexture = null;
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
