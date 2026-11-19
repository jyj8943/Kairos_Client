using UnityEngine;
using UnityEngine.UI;

/// <summary>인게임 배경 소스 종류</summary>
public enum InGameBackgroundSource
{
    /// <summary>그림 없음 (검은 바탕만)</summary>
    None,

    /// <summary>곡 1920×1080 일러스트 (SongData.Background, 없으면 Cover)</summary>
    Illustration,

    /// <summary>BGA 동영상 (SongData.Bga). 재생은 아직 없다 (TODO)</summary>
    Bga
}

/// <summary>
/// 인게임 배경 (기획서 3-4 「배경 · BGA (11/19)」). Canvas_InGameBackground(기어·노트·왼쪽 패널보다 뒤) 루트에 붙인다. InGameScene이 Init()을 부른다.
/// - 그리는 순서 (아래부터): Img_BackgroundBase(검은 바탕) → Source/Img_BackgroundSource(RawImage, 화면 꽉 채움, 비율 유지·잘라서 채우기)
///   → Img_BackgroundDim(검은 덮개, 알파 = 1 − 밝기) → (그 위 Canvas_InGame의 패널·기어·카드).
/// - 배경 소스: BGA 동영상이 있으면 BGA 자리(아래 TODO), 없으면 곡 일러스트를 흐림·채도 보정 없이 그대로 쓴다.
///   판 시작(Init) 때 한 번 넣는다. 재시작(F5·일시정지 재시작)은 씬을 다시 불러오므로 새 씬의 Init에서 한 번 넣는 것이 전부다
///   (텍스처 참조만 바꾸고 블러 같은 처리가 없어 비용이 없다).
/// - 밝기 0%는 덮개 알파 1 = 검은 화면 그대로 (Kairos 기본 배경은 나중).
/// - 색은 코드에서 정한다: 검은 바탕·덮개는 검정 고정이라 UITheme 값이 아니다 (씬에 색을 직렬화하지 않는다).
/// TODO (BGA, 곡에 BGA가 들어올 때 만든다):
///   1) VideoPlayer(renderMode = RenderTexture)를 이 Canvas에 두고 targetTexture를 1920×1080 RenderTexture로 만들어 sourceImage.texture에 넣는다.
///   2) 곡 시간 싱크: GameManager.Sound.SongTime + BgaOffsetMs로 VideoPlayer.time을 맞추고, 어긋나면 다시 맞춘다 (DSP 시계 기준).
///   3) 일시정지·재개 카운트다운: InGameScene이 멈출 때 Pause, 재개할 때 같은 곡 시간으로 맞춘 뒤 Play.
///   4) 재시작·나가기: Stop하고 RenderTexture를 해제한다.
/// </summary>
public class Canvas_InGameBackground : UIBase
{
    [Tooltip("검은 바탕 (Img_BackgroundBase, 화면 전체)")]
    [SerializeField] private Image baseImage;
    [Tooltip("배경 소스 (Source/Img_BackgroundSource, RawImage). 부모 Source의 AspectRatioFitter(EnvelopeParent)가 비율 유지·잘라서 채우기를 맡는다")]
    [SerializeField] private RawImage sourceImage;
    [Tooltip("배경 소스 비율 맞추기 (Source의 AspectRatioFitter, EnvelopeParent)")]
    [SerializeField] private AspectRatioFitter sourceFitter;
    [Tooltip("검은 덮개 (Img_BackgroundDim, 화면 전체). 알파 = 1 − 밝기")]
    [SerializeField] private Image dimImage;

    /// <summary>지금 배경 소스 종류 (확인용)</summary>
    public InGameBackgroundSource SourceKind { get; private set; }

    /// <summary>지금 밝기 (%, 확인용)</summary>
    public int Brightness { get; private set; } = GameConstants.Play.DefaultBackgroundBrightness;

    /// <summary>배경 소스 RawImage (확인용)</summary>
    public RawImage SourceImage => sourceImage;

    /// <summary>검은 덮개 (확인용)</summary>
    public Image DimImage => dimImage;

    /// <summary>
    /// 검은 바탕을 칠하고 곡 배경 소스와 밝기를 넣는다 (판 시작 때 한 번).
    /// </summary>
    /// <param name="song">이번 판의 곡 (BGA · 일러스트)</param>
    /// <param name="brightness">밝기 (%, 저장값)</param>
    public void Init(SongData song, int brightness)
    {
        if (baseImage != null)
        {
            baseImage.color = Color.black;
            baseImage.raycastTarget = false;
        }
        SetSource(song);
        SetBrightness(brightness);
    }

    /// <summary>밝기를 바꾼다: 검은 덮개 알파 = 1 − 밝기 (단계에 없는 값은 가까운 단계로)</summary>
    public void SetBrightness(int brightness)
    {
        Brightness = BackgroundBrightnessRules.Sanitize(brightness);
        if (dimImage == null) return;
        dimImage.color = new Color(0f, 0f, 0f, BackgroundBrightnessRules.DimAlpha(Brightness));
        dimImage.raycastTarget = false;
    }

    /// <summary>배경 소스를 고른다: BGA가 있으면 BGA 자리(지금은 일러스트로 대신), 없으면 일러스트, 둘 다 없으면 검은 바탕만</summary>
    private void SetSource(SongData song)
    {
        if (sourceImage == null) return;
        sourceImage.raycastTarget = false;
        sourceImage.color = Color.white;

        if (song != null && song.Bga != null)
        {
            // TODO (BGA): VideoPlayer → RenderTexture를 sourceImage.texture로 쓰고 곡 시간에 맞춰 재생한다 (클래스 주석 TODO 1~4).
            // 지금은 재생을 만들지 않았으므로 아래 일러스트로 대신한다.
            Debug.Log($"[Canvas_InGameBackground] BGA({song.Bga.name}, 오프셋 {song.BgaOffsetMs}ms)는 아직 재생하지 않아 일러스트로 대신합니다.", this);
        }

        Sprite illustration = song != null ? song.Background : null;
        if (illustration == null)
        {
            SourceKind = InGameBackgroundSource.None;
            sourceImage.texture = null;
            sourceImage.gameObject.SetActive(false);
            return;
        }

        SourceKind = InGameBackgroundSource.Illustration;
        Texture2D texture = illustration.texture;
        Rect rect = illustration.textureRect;
        sourceImage.texture = texture;
        sourceImage.uvRect = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
        if (sourceFitter != null) sourceFitter.aspectRatio = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
        sourceImage.gameObject.SetActive(true);
    }
}
