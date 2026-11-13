using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 FULL COMBO / ALL PERFECT 연출 (ClearEffect, Canvas_InGame 안 판정 글자·콤보·패널보다 위).
/// - 자식: Img_ClearBand(반투명 검은 띠) / Img_ClearAfterimage(잔상) / Img_ClearTitle(글자, Mask) └ Img_ClearShine(빛 훑기, 글자 모양으로 잘림)
/// - 같은 Canvas의 Img_ScreenFlash(화면 전체 흰색)도 이 스크립트가 바꾼다.
/// - 어떤 연출을 띄울지는 InGameScene이 ClearEffectRules로 정해 Play로 넘기고, 시간은 InGameScene이 결과 대기와 같은 곡 시간
///   (모든 노트 판정 뒤 지난 곡 시간)으로 SetTime에 넘긴다. 곡 시간이라 일시정지·카운트다운 중에는 멈추고 풀리면 이어진다.
/// - 계산은 ClearEffectAnimation(순수 클래스), 이 스크립트는 값을 적용만 한다. 자체 Update 없음.
/// - 글자 등장 순간(titleStart)에 appearSound를 한 번 낸다 (비어 있으면 내지 않음, 이번에는 비워 둔다).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ClearEffect : MonoBehaviour
{
    [Header("레이어")]
    [SerializeField] private Image band;
    [SerializeField] private Image title;
    [SerializeField] private Image afterimage;
    [SerializeField] private Image shine;
    [Tooltip("화면 전체 흰색 번쩍임 (ClearEffect 밖, 같은 Canvas)")]
    [SerializeField] private Image screenFlash;

    [Header("이미지")]
    [SerializeField] private Sprite fullComboSprite;
    [SerializeField] private Sprite allPerfectSprite;

    [Header("크기 (1920×1080 기준 px)")]
    [Tooltip("띠 높이 (폭은 화면 전체)")]
    [SerializeField] private float bandHeight = 220f;
    [Tooltip("글자 폭 (높이는 이미지 비율대로)")]
    [SerializeField] private float titleWidth = 1000f;

    [Header("효과음 (비어 있으면 재생 안 함)")]
    [Tooltip("글자가 찍히는 순간(titleStart)에 한 번 재생")]
    [SerializeField] private AudioClip appearSound;

    [Header("타이밍 (모든 노트 판정 뒤 초)")]
    [SerializeField] private ClearEffectAnimation timing = new ClearEffectAnimation();

    private CanvasGroup group;
    private RectTransform bandRect;
    private RectTransform titleRect;
    private RectTransform afterimageRect;
    private RectTransform shineRect;
    private ClearKind kind = ClearKind.None;
    private float lastTime = -1f;
    private Color bandBaseColor = Color.black;
    private Color flashBaseColor = Color.white;

    /// <summary>지금 연출 종류 (확인용)</summary>
    public ClearKind Kind => kind;

    /// <summary>계산 설정 (확인용)</summary>
    public ClearEffectAnimation Timing => timing;

    /// <summary>마지막으로 적용한 모습 (확인용)</summary>
    public ClearEffectFrame Frame { get; private set; } = ClearEffectAnimation.Hidden;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        group = GetComponent<CanvasGroup>();
        if (band != null) bandRect = band.rectTransform;
        if (title != null) titleRect = title.rectTransform;
        if (afterimage != null) afterimageRect = afterimage.rectTransform;
        if (shine != null) shineRect = shine.rectTransform;
    }

    /// <summary>
    /// 처음 상태 (전부 숨김, 크기·알파 처음 값). Canvas_InGame.Init(씬 Init·재시작)에서 부른다.
    /// 띠 높이·글자 폭을 적용하고, 연출 길이가 결과로 가는 시간보다 길면 경고한다.
    /// </summary>
    public void ResetView()
    {
        if (group == null) CacheComponents();
        if (band != null) { bandBaseColor = band.color; bandBaseColor.a = 1f; }
        if (screenFlash != null) { flashBaseColor = screenFlash.color; flashBaseColor.a = 1f; }
        ApplyLayout();
        WarnIfTooLong();
        kind = ClearKind.None;
        lastTime = -1f;
        Apply(ClearEffectAnimation.Hidden);
    }

    /// <summary>연출을 고른다 (모든 노트 판정 순간). None이면 숨긴 채로 둔다</summary>
    public void Play(ClearKind clearKind)
    {
        if (group == null) CacheComponents();
        kind = clearKind;
        lastTime = -1f;
        Sprite sprite = clearKind == ClearKind.AllPerfect ? allPerfectSprite : fullComboSprite;
        if (title != null) title.sprite = sprite;
        if (afterimage != null) afterimage.sprite = sprite;
        Apply(ClearEffectAnimation.Hidden);
    }

    /// <summary>모든 노트 판정 뒤 지난 시간(초, 곡 시간 기준)으로 연출을 맞춘다. 연출이 없으면 아무것도 하지 않는다</summary>
    public void SetTime(float elapsed)
    {
        if (kind == ClearKind.None) return;

        // 글자 등장 순간을 지나는 프레임에 효과음 한 번
        if (appearSound != null && lastTime < timing.titleStart && elapsed >= timing.titleStart && SoundManager.HasInstance)
            GameManager.Sound.PlaySfx(appearSound);
        lastTime = elapsed;

        Apply(timing.Evaluate(elapsed));
    }

    /// <summary>띠 높이·글자 폭(이미지 비율대로 높이)을 RectTransform에 적용한다</summary>
    private void ApplyLayout()
    {
        if (bandRect != null) bandRect.sizeDelta = new Vector2(bandRect.sizeDelta.x, bandHeight);
        Sprite sprite = fullComboSprite != null ? fullComboSprite : allPerfectSprite;
        float aspect = sprite != null && sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 358f / 2126f;
        Vector2 size = new Vector2(titleWidth, titleWidth * aspect);
        if (titleRect != null) titleRect.sizeDelta = size;
        if (afterimageRect != null) afterimageRect.sizeDelta = size;
    }

    /// <summary>연출이 결과로 가는 시간(ResultDelaySeconds)보다 길면 경고 로그</summary>
    private void WarnIfTooLong()
    {
        if (timing.TotalSeconds > GameConstants.Play.ResultDelaySeconds)
            Debug.LogWarning($"[ClearEffect] 연출 길이 {timing.TotalSeconds:0.##}초가 결과로 가는 시간 {GameConstants.Play.ResultDelaySeconds}초보다 깁니다. 퇴장 전에 결과로 넘어갑니다.", this);
    }

    /// <summary>계산값을 레이어에 적용한다</summary>
    private void Apply(ClearEffectFrame f)
    {
        Frame = f;
        if (group != null) group.alpha = f.Visible ? f.GroupAlpha : 0f;

        if (band != null)
        {
            bandRect.localScale = new Vector3(f.BandScaleX, 1f, 1f);
            band.color = WithAlpha(bandBaseColor, f.BandAlpha);
        }
        if (title != null)
        {
            titleRect.localScale = new Vector3(f.TitleScale, f.TitleScale, 1f);
            title.color = WithAlpha(Color.white, f.TitleAlpha);
        }
        if (afterimage != null)
        {
            afterimageRect.localScale = new Vector3(f.AfterimageScale, f.AfterimageScale, 1f);
            afterimage.color = WithAlpha(Color.white, f.AfterimageAlpha);
        }
        if (shine != null)
        {
            shine.enabled = f.ShineVisible;
            // 글자 왼쪽 밖 → 오른쪽 밖 (글자 폭 절반 + 막대 폭만큼 밖에서 시작·끝)
            float half = (titleRect != null ? titleRect.rect.width : titleWidth) * 0.5f + shineRect.rect.width;
            shineRect.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, f.ShineProgress), shineRect.anchoredPosition.y);
        }
        if (screenFlash != null)
        {
            // 연출 전체 퇴장과 상관없이 번쩍임 알파만 쓴다 (ClearEffect 밖)
            screenFlash.color = WithAlpha(flashBaseColor, f.FlashAlpha);
            screenFlash.enabled = f.FlashAlpha > 0f;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        WarnIfTooLong();
    }
#endif
}
