using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// 흐린 배경 (기획서 3-3 ⑨ 곡 선택 배경). 여러 화면에서 쓸 수 있는 공용 컴포넌트로, 앞 RawImage에 붙인다.
/// - 입력은 Texture (Sprite의 texture, 나중에 BGA VideoPlayer의 RenderTexture도).
/// - 처리: 입력을 기준 해상도(1920×1080)의 1/4 RenderTexture로 줄이고(16:9가 아니면 가운데를 잘라 맞춤) →
///   블러 셰이더(Kairos/UIBlur)로 가로 1회·세로 1회를 BlurIterations번 반복 (마지막 세로 패스에서 채도·밝기 보정, SetColorGrade) →
///   결과 RenderTexture를 RawImage에 표시. 보정 값은 실행용 머티리얼 사본에 넣는다 (에셋 머티리얼은 그대로).
/// - 기본은 입력이 바뀔 때 한 번만 처리한다. updateEveryFrame을 켜면 LateUpdate에서 매 프레임 처리한다 (BGA용, 지금은 끔).
/// - RenderTexture는 처음 한 번 만들어(결과 2장 + 중간 1장) 계속 다시 쓰고, OnDestroy에서 해제한다.
/// - 입력이 바뀌면 결과 2장을 번갈아 쓰고 앞·뒤 RawImage 알파로 섞는다 (BackgroundFadeSeconds, 실제 시간). 처음과 instant는 페이드 없이 바로.
/// - 머티리얼(UIBlur.mat)은 직렬화 필드로 연결해 빌드에서 셰이더가 빠지지 않게 한다.
/// - 블러 세기: 기본은 머티리얼 _Radius와 GameConstants.SongSelect.BlurIterations. SetBlurRadiusPx로 화면마다 바꿀 수 있다 (팝업 뒤 화면 블러 6px).
/// - 화면 캡처처럼 같은 RenderTexture에 새 내용이 들어오는 입력은 ProcessNow로 매번 다시 처리한다 (위아래 뒤집기 선택, 페이드 없음).
/// - keepResultAcrossScenes를 켜면(인게임) 처리 결과 한 장을 씬을 넘어 남겨 두고, 다음 씬에서 같은 입력·같은 색 보정이면 다시 처리하지 않고 그대로 쓴다
///   (인게임 재시작은 씬을 다시 불러오지만 같은 곡이라 블러를 다시 하지 않는다). 다른 입력이 오면 남겨 둔 결과를 해제하고 새로 처리한다.
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
    /// <summary>세로 + 색 보정(채도·밝기). 마지막 반복의 세로 패스 대신 한 번 쓴다</summary>
    private const int VerticalGradePass = 2;

    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");

    /// <summary>
    /// 셰이더 9탭 커널(가중치 0.227 · 0.316@1.385 · 0.070@3.231)의 표준편차 (텍셀, _Radius 1 기준).
    /// 분산 = 2 × (0.3162 × 1.3846² + 0.0703 × 3.2308²) ≈ 2.68
    /// </summary>
    public const float KernelSigmaTexels = 1.637f;

    [Tooltip("블러 머티리얼 (Assets/Materials/UIBlur.mat, 셰이더 Kairos/UIBlur)")]
    [SerializeField] private Material blurMaterial;

    [Tooltip("크로스페이드할 때 이전 배경을 보여 줄 뒤 RawImage (앞 RawImage 아래 형제)")]
    [SerializeField] private RawImage backImage;

    [Tooltip("켜면 LateUpdate에서 매 프레임 다시 처리한다 (BGA 영상용). 지금은 끔")]
    [SerializeField] private bool updateEveryFrame;

    [Tooltip("켜면 처리 결과를 씬을 넘어 남겨 두고 같은 입력이면 다시 처리하지 않는다 (인게임 재시작용). 크로스페이드는 하지 않는다")]
    [SerializeField] private bool keepResultAcrossScenes;

    /// <summary>씬을 넘어 남겨 둔 결과 (keepResultAcrossScenes 전용, 한 장)</summary>
    private static RenderTexture keptResult;
    private static Texture keptSource;
    private static Vector2 keptGrade;

    private RawImage frontImage;

    /// <summary>색 보정 값을 넣을 실행용 머티리얼 사본 (에셋 머티리얼은 고치지 않는다)</summary>
    private Material runtimeMaterial;
    private float saturation = 1f;
    private float brightness = 1f;

    /// <summary>셰이더 _Radius 덮어쓰기 (음수면 머티리얼 값 그대로)</summary>
    private float radiusOverride = -1f;

    /// <summary>반복 횟수 덮어쓰기 (0 이하면 GameConstants.SongSelect.BlurIterations)</summary>
    private int iterationsOverride;

    /// <summary>다음 처리에서 입력을 위아래로 뒤집는다 (화면 캡처가 뒤집혀 오는 그래픽 API용)</summary>
    private bool flipSourceY;
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

    /// <summary>
    /// 색 보정 값 (채도 1 = 원래, 밝기 1 = 원래). 다음 처리부터 쓴다 (이미 처리한 결과는 그대로). 메뉴 화면은 UITheme 값을 넘긴다.
    /// </summary>
    public void SetColorGrade(float newSaturation, float newBrightness)
    {
        saturation = newSaturation;
        brightness = newBrightness;
    }

    /// <summary>지금 색 보정 값 (확인용)</summary>
    public Vector2 ColorGrade => new Vector2(saturation, brightness);

    /// <summary>
    /// 블러 세기를 기준 해상도(1920×1080) px 표준편차로 정한다. 다음 처리부터 쓴다.
    /// </summary>
    /// <param name="sigmaPx">흐림 정도 (px, 가우시안 표준편차)</param>
    /// <param name="iterations">가로·세로 반복 횟수 (1 이상)</param>
    public void SetBlurRadiusPx(float sigmaPx, int iterations)
    {
        iterationsOverride = Mathf.Max(1, iterations);
        radiusOverride = ShaderRadiusFor(sigmaPx, Downsample, iterationsOverride);
    }

    /// <summary>지금 셰이더 _Radius (덮어쓰지 않았으면 -1, 확인용)</summary>
    public float RadiusOverride => radiusOverride;

    /// <summary>지금 반복 횟수 (확인용)</summary>
    public int Iterations => iterationsOverride > 0 ? iterationsOverride : Mathf.Max(1, GameConstants.SongSelect.BlurIterations);

    /// <summary>
    /// 기준 해상도 px 표준편차 → 셰이더 _Radius (순수 함수).
    /// 줄인 텍스처 1텍셀 = downsample px, 반복 n번이면 표준편차가 √n배가 된다: sigmaPx = KernelSigmaTexels × radius × downsample × √n
    /// </summary>
    public static float ShaderRadiusFor(float sigmaPx, int downsample, int iterations)
    {
        float divisor = KernelSigmaTexels * Mathf.Max(1, downsample) * Mathf.Sqrt(Mathf.Max(1, iterations));
        return Mathf.Max(0f, sigmaPx) / divisor;
    }

    /// <summary>
    /// 같은 텍스처라도 지금 내용으로 다시 처리해 바로 보인다 (화면 캡처용, 페이드·씬 넘김 결과 없음).
    /// </summary>
    /// <param name="flipY">입력을 위아래로 뒤집어 읽는다 (ScreenCapture 결과가 뒤집혀 오는 그래픽 API)</param>
    public void ProcessNow(Texture texture, bool flipY)
    {
        source = texture;
        if (texture == null)
        {
            RawImage empty = GetFrontImage();
            empty.texture = null;
            empty.color = Color.clear;
            HideBack();
            isFading = false;
            return;
        }

        flipSourceY = flipY;
        Process(texture, 0);
        flipSourceY = false;
        ShowFront(0, 1f);
        HideBack();
        isFading = false;
    }

    /// <summary>결과를 숨긴다 (텍스처는 남겨 두고 다음 ProcessNow에서 다시 쓴다)</summary>
    public void HideResult()
    {
        RawImage front = GetFrontImage();
        front.color = Color.clear;
        HideBack();
        isFading = false;
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
            // 입력이 없으면 아래 바탕색이 보이게 투명하게 둔다
            front.texture = null;
            front.color = Color.clear;
            HideBack();
            isFading = false;
            return;
        }

        if (keepResultAcrossScenes)
        {
            ShowKept(texture);
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

    /// <summary>
    /// 남겨 둔 결과가 같은 입력·같은 색 보정이면 그대로 보이고, 아니면 새로 처리해 그 결과를 남겨 둔다 (앞 결과는 해제).
    /// </summary>
    private void ShowKept(Texture texture)
    {
        Vector2 grade = new Vector2(saturation, brightness);
        if (keptResult == null || keptSource != texture || keptGrade != grade || !keptResult.IsCreated())
        {
            Process(texture, 0);
            Release(ref keptResult);
            keptResult = results[0];
            results[0] = null;
            keptSource = texture;
            keptGrade = grade;
        }
        else
        {
            ReusedCount++;
        }

        frontIndex = 0;
        RawImage front = GetFrontImage();
        front.texture = keptResult;
        front.color = Color.white;
        front.enabled = true;
        HideBack();
        isFading = false;
    }

    /// <summary>씬을 넘어 남겨 둔 결과를 다시 쓴 횟수 (확인용)</summary>
    public int ReusedCount { get; private set; }

    /// <summary>씬을 넘어 남겨 둔 결과의 입력 (확인용)</summary>
    public static Texture KeptSource => keptSource;

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
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(runtimeMaterial);
            else DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
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
        if (flipSourceY) FlipY(ref scale, ref offset);
        Graphics.Blit(texture, target, scale, offset);

        Material material = GetRuntimeMaterial();
        if (material != null)
        {
            material.SetFloat(SaturationId, saturation);
            material.SetFloat(BrightnessId, brightness);
            material.SetFloat(RadiusId, radiusOverride >= 0f ? radiusOverride : blurMaterial.GetFloat(RadiusId));
            int iterations = Iterations;
            for (int i = 0; i < iterations; i++)
            {
                bool last = i == iterations - 1;
                Graphics.Blit(target, temp, material, HorizontalPass);
                Graphics.Blit(temp, target, material, last ? VerticalGradePass : VerticalPass);
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

    /// <summary>에셋 머티리얼의 실행용 사본 (처음 한 번 만든다). 에셋이 없으면 null</summary>
    private Material GetRuntimeMaterial()
    {
        if (runtimeMaterial == null && blurMaterial != null)
            runtimeMaterial = new Material(blurMaterial) { name = $"{blurMaterial.name} (Runtime)", hideFlags = HideFlags.DontSave };
        return runtimeMaterial;
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

    /// <summary>UV 배율·시작점을 위아래로 뒤집는다 (순수 함수): v → 1 - v</summary>
    public static void FlipY(ref Vector2 scale, ref Vector2 offset)
    {
        offset.y += scale.y;
        scale.y = -scale.y;
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
