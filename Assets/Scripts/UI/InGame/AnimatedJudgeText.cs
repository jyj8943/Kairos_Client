using TMPro;
using UnityEngine;

/// <summary>
/// 판정 글자(Txt_Judge)·FAST/SLOW 글자(Txt_FastSlow) 공통: JudgeTextAnimation 값을 글자에 적용만 한다.
/// - 크기는 RectTransform localScale(기준점 = RectTransform 가운데 pivot), 투명도는 글자 색 알파(Show로 받은 색의 알파에 곱함)로 바꾼다.
/// - 색은 JudgeColor: 단색은 vertex color, PERFECT는 TMP colorGradient 가로 그라데이션 (UIThemePaint.JudgeText, 11/19).
/// - 시간은 Canvas_InGame.TickAnimations → Tick으로만 흐른다 (자체 Update 없음, 일시정지·카운트다운 중에는 멈춘다. 콤보 Txt_Combo와 같은 방식).
/// - 숨길 때는 TMP 컴포넌트를 끈다 (그리지 않음). 글자·색은 Show 때만 바꿔 매 프레임 GC 할당이 없다.
/// - glow를 켜면 은은한 빛을 TMP Underlay(오프셋 0, 부드러운 가장자리)로 그린다 (후처리 Bloom 없음).
///   빛 색 = Show로 받은 색(그라데이션은 오른쪽 색 = PERFECT 분홍빛) × UITheme.inGameJudgeGlowAlpha. 처음 한 번 이 글자 전용 머티리얼 사본을 만들고, 색은 Show 때만 바꾼다.
///   글자 알파(등장·퇴장)에 빛도 같이 곱해진다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public abstract class AnimatedJudgeText : MonoBehaviour
{
    /// <summary>빛(Underlay) 넓이·부드러움 (SDF 단위, 0 ~ 1)</summary>
    private const float GlowDilate = 0.35f;
    private const float GlowSoftness = 0.6f;

    [Tooltip("켜면 글자 뒤에 판정 색의 은은한 빛(TMP Underlay)을 그린다. 세기는 UITheme.inGameJudgeGlowAlpha")]
    [SerializeField] private bool glow;

    private TMP_Text text;
    private Material glowMaterial;
    private RectTransform rect;
    private JudgeColor baseColor = JudgeColor.Solid(Color.white);
    private readonly JudgeTextAnimation textAnimation = new JudgeTextAnimation();

    /// <summary>계산 상태 (확인용)</summary>
    public JudgeTextAnimation Animation => textAnimation;

    /// <summary>지금 보이는지 (확인용)</summary>
    public bool IsVisible => text != null && text.enabled && textAnimation.IsPlaying;

    /// <summary>지금 글자 색 한 벌 (확인용)</summary>
    public JudgeColor ShownColor => baseColor;

    /// <summary>TMP 글자 (확인용)</summary>
    public TMP_Text TextComponent => text != null ? text : GetComponent<TMP_Text>();

    /// <summary>지금 글자 (확인용)</summary>
    public string Text => text != null ? text.text : string.Empty;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        text = GetComponent<TMP_Text>();
        rect = (RectTransform)transform;
    }

    /// <summary>Inspector 값을 계산 클래스로 옮긴다 (각 화면 스크립트가 자기 기본값으로 채운다)</summary>
    protected abstract void ApplySettings(JudgeTextAnimation target);

    /// <summary>처음 상태 (숨김, 크기 1). Canvas_InGame.Init(씬 Init·재시작)에서 부른다</summary>
    public void ResetView()
    {
        if (text == null) CacheComponents();
        ApplySettings(textAnimation);
        textAnimation.Stop();
        text.text = string.Empty;
        Apply();
    }

    /// <summary>글자·단색을 바꾸고 애니메이션을 처음부터 다시 재생한다 (FAST/SLOW 등 단색)</summary>
    public void Show(string value, Color color)
    {
        Show(value, JudgeColor.Solid(color));
    }

    /// <summary>글자·색(단색 또는 PERFECT 가로 그라데이션)을 바꾸고 애니메이션을 처음부터 다시 재생한다</summary>
    public void Show(string value, JudgeColor color)
    {
        if (text == null) CacheComponents();
        ApplySettings(textAnimation);
        text.text = value;
        baseColor = color;
        if (glow) SetGlowColor(color.GlowColor);
        textAnimation.Restart();
        Apply();
    }

    /// <summary>지금 빛 색 (확인용, 빛이 없으면 투명)</summary>
    public Color GlowColor => glowMaterial != null ? glowMaterial.GetColor(ShaderUtilities.ID_UnderlayColor) : Color.clear;

    /// <summary>빛 색을 바꾼다. 처음 한 번 Underlay를 켠 머티리얼 사본을 만든다</summary>
    private void SetGlowColor(Color color)
    {
        if (glowMaterial == null)
        {
            glowMaterial = text.fontMaterial;
            glowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            glowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            glowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            glowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, GlowDilate);
            glowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, GlowSoftness);
            text.UpdateMeshPadding();
        }
        color.a = UITheme.Current.inGameJudgeGlowAlpha;
        glowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, color);
    }

    /// <summary>시간을 진행한다 (플레이 중 매 프레임, 일시정지·카운트다운 중에는 부르지 않는다)</summary>
    public void Tick(float deltaTime)
    {
        if (text == null || !textAnimation.IsPlaying) return;
        textAnimation.Tick(deltaTime);
        Apply();
    }

    /// <summary>계산값을 글자에 적용한다</summary>
    private void Apply()
    {
        JudgeTextFrame frame = textAnimation.Current;
        text.enabled = frame.Visible;
        rect.localScale = new Vector3(frame.Scale, frame.Scale, 1f);

        UIThemePaint.JudgeText(text, baseColor, frame.Alpha);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중 Inspector에서 바꾼 값을 바로 쓴다
        ApplySettings(textAnimation);
    }
#endif
}
