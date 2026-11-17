using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// 흐린 배경 (기획서 3-3 ⑨ 곡 선택 배경). 여러 화면에서 쓸 수 있는 공용 컴포넌트로, 앞 RawImage에 붙인다.
/// - 입력은 Texture (Sprite의 texture, 나중에 BGA VideoPlayer의 RenderTexture도).
/// - 처리: 입력을 기준 해상도(1920×1080)의 1/4 RenderTexture로 줄이고(16:9가 아니면 가운데를 잘라 맞춤) →
///   블러 셰이더(Kairos/UIBlur)로 가로 1회·세로 1회를 BlurIterations번 반복 → 결과 RenderTexture를 RawImage에 표시.
/// - 기본은 입력이 바뀔 때 한 번만 처리한다. updateEveryFrame을 켜면 LateUpdate에서 매 프레임 처리한다 (BGA용, 지금은 끔).
/// - RenderTexture는 처음 한 번 만들어(결과 2장 + 중간 1장) 계속 다시 쓰고, OnDestroy에서 해제한다.
/// - 입력이 바뀌면 결과 2장을 번갈아 쓰고 앞·뒤 RawImage 알파로 섞는다 (BackgroundFadeSeconds, 실제 시간). 처음과 instant는 페이드 없이 바로.
/// - 머티리얼(UIBlur.mat)은 직렬화 필드로 연결해 빌드에서 셰이더가 빠지지 않게 한다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class UIBlurBackground : MonoBehaviour
{
    private const string LogPrefix = "[UIBlurBackground]";

    /// <summary>블러 기준 해상도 (1920×1080). 결과 크기 = 이 값 ÷ BlurDownsample</summary>
    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;

    private const int HorizontalPass = 0;
    private const int VerticalPass = 1;

    [Tooltip("블러 머티리얼 (Assets/Materials/UIBlur.mat, 셰이더 Kairos/UIBlur)")]
    [SerializeField] private Material blurMaterial;

    [Tooltip("크로스페이드할 때 이전 배경을 보여 줄 뒤 RawImage (앞 RawImage 아래 형제)")]
    [SerializeField] private RawImage backImage;

    [Tooltip("켜면 LateUpdate에서 매 프레임 다시 처리한다 (BGA 영상용). 지금은 끔")]
    [SerializeField] private bool updateEveryFrame;

    private RawImage frontImage;
    private readonly RenderTexture[] results = new RenderTexture[2];
    private RenderTexture temp;
    private int frontIndex = -1;

    private Texture source;
    private float fadeElapsed;
    private bool isFading;

    /// <summary>지금 입력 (없으면 null)</summary>
    public Texture Source => source;

    /// <summary>블러 처리를 한 횟수 (같은 입력이면 늘지 않는다, 확인용)</summary>
    public int ProcessCount { get; private set; }

    /// <summary>지금까지 만든 RenderTexture 수 (다시 쓰면 늘지 않는다, 확인용)</summary>
    public int CreatedTextureCount { get; private set; }

    /// <summary>마지막 처리에 걸린 CPU 시간 (ms, GPU 명령을 넣는 데 걸린 시간)</summary>
    public double LastProcessMilliseconds { get; private set; }

    /// <summary>크로스페이드 중이면 true</summary>
    public bool IsFading => isFading;

    /// <summary>앞 RawImage (이 오브젝트)</summary>
    public RawImage FrontImage => GetFrontImage();

    /// <summary>뒤 RawImage</summary>
    public RawImage BackImage => backImage;

    /// <summary>결과 RenderTexture 크기 (확인용)</summary>
    public Vector2Int ResultSize => new Vector2Int(ReferenceWidth / Downsample, ReferenceHeight / Downsample);

    private static int Downsample => Mathf.Max(1, GameConstants.SongSelect.BlurDownsample);

    private void Awake()
    {
        GetFrontImage();
    }

    /// <summary>Sprite 입력 (sprite.texture를 쓴다. Sprite Mode Single 기준)</summary>
    public void SetSource(Sprite sprite, bool instant)
    {
        SetSource(sprite != null ? sprite.texture : null, instant);
    }

    /// <summary>
    /// 배경 입력을 바꾼다. 같은 입력이면 아무것도 하지 않는다 (다시 처리하지 않는다).
    /// </summary>
    /// <param name="instant">true면 페이드 없이 바로 보인다 (화면에 처음 들어올 때)</param>
    public void SetSource(Texture texture, bool instant)
    {
        if (texture == source && (frontIndex >= 0 || texture == null)) return;
        source = texture;
        RawImage front = GetFrontImage();

        if (texture == null)
        {
            front.texture = null;
            front.color = Color.black;
            HideBack();
            isFading = false;
            return;
        }

        bool first = frontIndex < 0;
        int target = first ? 0 : 1 - frontIndex;
        Process(texture, target);

        if (first || instant || backImage == null)
        {
            ShowFront(target, 1f);
            HideBack();
            isFading = false;
            return;
        }

        // 이전 결과를 뒤에 두고, 새 결과를 앞에서 0 → 1로 섞는다
        backImage.texture = results[frontIndex];
        backImage.color = Color.white;
        backImage.enabled = true;
        ShowFront(target, 0f);
        fadeElapsed = 0f;
        isFading = true;
    }

    private void LateUpdate()
    {
        if (updateEveryFrame && source != null && frontIndex >= 0) Process(source, frontIndex);
        if (!isFading) return;

        float duration = Mathf.Max(0.0001f, GameConstants.SongSelect.BackgroundFadeSeconds);
        fadeElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(fadeElapsed / duration);
        SetAlpha(GetFrontImage(), t);
        if (t < 1f) return;

        isFading = false;
        HideBack();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < results.Length; i++) Release(ref results[i]);
        Release(ref temp);
    }

    /// <summary>
    /// 입력을 results[index]에 줄여 그리고 블러한다. 머티리얼이 없으면 줄이기만 한다.
    /// </summary>
    private void Process(Texture texture, int index)
    {
        EnsureTextures();
        Stopwatch watch = Stopwatch.StartNew();

        RenderTexture target = results[index];
        float sourceAspect = texture.height > 0 ? (float)texture.width / texture.height : GameConstants.SongSelect.BlurAspect;
        GetCrop(sourceAspect, GameConstants.SongSelect.BlurAspect, out Vector2 scale, out Vector2 offset);
        Graphics.Blit(texture, target, scale, offset);

        if (blurMaterial != null)
        {
            for (int i = 0; i < GameConstants.SongSelect.BlurIterations; i++)
            {
                Graphics.Blit(target, temp, blurMaterial, HorizontalPass);
                Graphics.Blit(temp, target, blurMaterial, VerticalPass);
            }
        }
        else if (ProcessCount == 0)
        {
            Debug.LogWarning($"{LogPrefix} blurMaterial이 연결되지 않아 흐리게 하지 않습니다.", this);
        }

        watch.Stop();
        LastProcessMilliseconds = watch.Elapsed.TotalMilliseconds;
        ProcessCount++;
    }

    /// <summary>
    /// 입력 비율을 결과 비율(16:9)에 맞추는 UV 배율·시작점 (가운데를 잘라낸다, 순수 함수).
    /// </summary>
    public static void GetCrop(float sourceAspect, float targetAspect, out Vector2 scale, out Vector2 offset)
    {
        scale = Vector2.one;
        offset = Vector2.zero;
        if (sourceAspect <= 0f || targetAspect <= 0f) return;

        if (sourceAspect > targetAspect)
        {
            scale.x = targetAspect / sourceAspect;
            offset.x = (1f - scale.x) * 0.5f;
        }
        else if (sourceAspect < targetAspect)
        {
            scale.y = sourceAspect / targetAspect;
            offset.y = (1f - scale.y) * 0.5f;
        }
    }

    /// <summary>결과 2장 + 중간 1장을 처음 한 번만 만든다</summary>
    private void EnsureTextures()
    {
        Vector2Int size = ResultSize;
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == null) results[i] = Create($"BlurResult{i}", size);
        }
        if (temp == null) temp = Create("BlurTemp", size);
    }

    private RenderTexture Create(string label, Vector2Int size)
    {
        RenderTexture texture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_{label}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
        };
        texture.Create();
        CreatedTextureCount++;
        return texture;
    }

    private static void Release(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        if (Application.isPlaying) Destroy(texture);
        else DestroyImmediate(texture);
        texture = null;
    }

    private RawImage GetFrontImage()
    {
        if (frontImage == null) frontImage = GetComponent<RawImage>();
        return frontImage;
    }

    private void ShowFront(int index, float alpha)
    {
        frontIndex = index;
        RawImage front = GetFrontImage();
        front.texture = results[index];
        front.color = new Color(1f, 1f, 1f, alpha);
        front.enabled = true;
    }

    private void HideBack()
    {
        if (backImage == null) return;
        SetAlpha(backImage, 0f);
        backImage.enabled = false;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
