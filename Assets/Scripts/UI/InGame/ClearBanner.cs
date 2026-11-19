using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 끝 FULL COMBO · ALL PERFECT 연출 (기획서 2-1 7) 한 판의 흐름 · 3-4 「FC/AP 연출 (11/19)」, 와이어프레임 11_InGame_FcAp).
/// Canvas_InGame 위쪽 맨 앞 "ClearBanner"(빈 루트 + CanvasGroup)에 붙인다. 자식:
/// - Img_Flash(화면 반짝임) · Img_BandBg(띠 y 320~760) · Img_BandLineTop/Bottom(위아래 3px 선)
/// - Clock(가운데 (960, 540), CanvasGroup): Img_ClockDashes · Img_ClockFace · Img_ClockHighlight · Img_ClockBurst · Img_MinuteHand · Img_HourHand · Img_ClockCenter · Img_ClockTopDot
/// - Img_ClearLogo(글자, Mask) └ Img_LogoBright(12시 강조 때 흰색 덧칠 = 밝기 +45%p) · Img_LogoShine(빛 줄기)
/// - Img_CountCapsule(HorizontalLayoutGroup + ContentSizeFitter, CanvasGroup): Img_CapsuleLine · Txt_CountLabel · Txt_Count · Txt_CountTotal
/// - Motes: Img_Mote × 30 (Image 풀, ParticleSystem 쓰지 않음)
/// - 기어 쪽 라인 빛 기둥 Img_LanePillar × 4는 Canvas_InGame 맨 뒤(기어 위, 왼쪽 패널·콤보 아래)에 따로 있고 이 스크립트가 바꾼다.
/// 흐름: Init()(Canvas_InGame.Init → InGameScene Init)에서 스프라이트를 Resources에서 읽고 숨긴다 →
/// 모든 노트 판정 순간 InGameScene이 Play(종류, 개수, 전체) → 결과 대기 타이머와 같은 곡 시간(판정 뒤 지난 시간)으로 SetTime.
/// 연출용 타이머는 따로 두지 않는다 (곡 시간이라 테스트 재생 배율에서도 비율이 맞다). 계산은 ClearBannerTimeline, 여기는 적용만 한다.
/// 색은 UITheme(clearAllPerfect · clearFullCombo · clearCapsuleFill …)에서 Play 때 입힌다 (씬에 색을 직렬화하지 않는다).
/// 빛(glow) 효과는 넣지 않는다 (빛 알갱이 · 빛 줄기 · 반짝임만).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ClearBanner : MonoBehaviour
{
    /// <summary>띠 높이 (px, y 320~760)</summary>
    public const float BandHeight = 440f;
    /// <summary>띠 위아래 선 두께 (px)</summary>
    public const float BandLineThickness = 3f;
    /// <summary>글자 표시 크기 (px, README 2절)</summary>
    public static readonly Vector2 AllPerfectLogoSize = new Vector2(760f, 166f);
    public static readonly Vector2 FullComboLogoSize = new Vector2(706f, 166f);
    /// <summary>
    /// 개수 글자 형식: 숫자를 0.6em(48pt에서 28.8px) 고정 폭으로 (자릿수당 29px 칸 안에 들어가고, 숫자가 바뀌어도 글자 폭이 흔들리지 않게).
    /// NEXON Lv2 Gothic Bold 48 숫자는 원래 약 30.5px라 그대로 두면 칸(자릿수 × 29)을 넘친다.
    /// </summary>
    public const string CountFormat = "<mspace=0.6em>{0}</mspace>";

    /// <summary>빛 줄기 폭 (px)</summary>
    public const float ShineWidth = 80f;

    [Header("띠 · 반짝임")]
    [SerializeField] private Image flash;
    [SerializeField] private Image bandBg;
    [SerializeField] private Image bandLineTop;
    [SerializeField] private Image bandLineBottom;

    [Header("시계 (Clock)")]
    [SerializeField] private CanvasGroup clockGroup;
    [SerializeField] private Image clockDashes;
    [SerializeField] private Image clockFace;
    [SerializeField] private Image clockHighlight;
    [SerializeField] private Image clockBurst;
    [SerializeField] private Image minuteHand;
    [SerializeField] private Image hourHand;
    [SerializeField] private Image clockCenter;
    [SerializeField] private Image clockTopDot;

    [Header("글자 (Img_ClearLogo)")]
    [SerializeField] private Image logo;
    [SerializeField] private Image logoBright;
    [SerializeField] private Image logoShine;

    [Header("개수 캡슐 (Img_CountCapsule)")]
    [SerializeField] private Image capsule;
    [SerializeField] private CanvasGroup capsuleGroup;
    [SerializeField] private Image capsuleLine;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text countText;
    [Tooltip("Txt_Count 칸 폭 고정 (preferredWidth = 전체 자릿수 × 29)")]
    [SerializeField] private LayoutElement countLayout;
    [SerializeField] private TMP_Text countTotal;

    [Header("빛 알갱이 (Motes/Img_Mote × 30) · 라인 빛 기둥 (Img_LanePillar × 4)")]
    [SerializeField] private Image[] motes;
    [SerializeField] private Image[] lanePillars;

    private readonly ClearBannerTimeline timeline = new ClearBannerTimeline();
    private CanvasGroup group;
    private RectTransform logoRect;
    private RectTransform shineRect;
    private RectTransform capsuleRect;
    private RectTransform clockRect;
    private Vector2 logoBase;
    private Vector2 capsuleBase;
    private bool hasBase;

    private Sprite allPerfectSprite;
    private Sprite fullComboSprite;
    private Sprite bandLineAp;
    private Sprite bandLineFc;

    private ClearType type = ClearType.None;
    private int count;
    private int total;
    private int shownCount = -1;
    private float elapsed;
    private ClearColors colors;
    private Color labelStart;
    private Color labelEnd;

    /// <summary>지금 연출 종류 (None이면 숨김)</summary>
    public ClearType Type => type;

    /// <summary>연출 재생 중이면 true (Play 뒤 3초 전까지). 이 동안 인게임 ESC(OnBack)는 무시한다</summary>
    public bool IsPlaying => type != ClearType.None && elapsed < ClearBannerTimeline.TotalSeconds;

    /// <summary>Play 뒤 지난 시간 (초, 곡 시간)</summary>
    public float Elapsed => elapsed;

    /// <summary>마지막으로 적용한 모습 (확인용)</summary>
    public ClearBannerFrame Frame { get; private set; } = ClearBannerTimeline.Hidden;

    /// <summary>시간표 (확인용)</summary>
    public ClearBannerTimeline Timeline => timeline;

    /// <summary>지금 보이는 알갱이 수 (확인용)</summary>
    public int ActiveMoteCount { get; private set; }

    /// <summary>확인용 참조</summary>
    public RectTransform CapsuleRect => capsuleRect;
    public RectTransform LogoRect => logoRect;
    public RectTransform ClockRect => clockRect;
    public RectTransform BandRect => bandBg != null ? bandBg.rectTransform : null;
    public TMP_Text CountLabel => countLabel;
    public TMP_Text CountText => countText;
    public TMP_Text CountTotal => countTotal;
    public Image Logo => logo;
    public Image MinuteHand => minuteHand;
    public Image HourHand => hourHand;
    public Image[] LanePillars => lanePillars;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        group = GetComponent<CanvasGroup>();
        if (logo != null) logoRect = logo.rectTransform;
        if (logoShine != null) shineRect = logoShine.rectTransform;
        if (capsule != null) capsuleRect = capsule.rectTransform;
        if (clockGroup != null) clockRect = (RectTransform)clockGroup.transform;
    }

    private void OnDestroy()
    {
        if (countLabel != null) countLabel.OnPreRenderText -= ApplyLabelGradient;
    }

    /// <summary>
    /// 스프라이트를 Resources(Images/InGame/Clear/)에서 읽고 바뀌지 않는 색을 입힌 뒤 전부 숨긴다. Canvas_InGame.Init(InGameScene Init)에서 부른다.
    /// </summary>
    public void Init()
    {
        if (group == null) CacheComponents();
        if (!hasBase)
        {
            if (logoRect != null) logoBase = logoRect.anchoredPosition;
            if (capsuleRect != null) capsuleBase = capsuleRect.anchoredPosition;
            hasBase = true;
        }
        group.blocksRaycasts = false;
        group.interactable = false;

        allPerfectSprite = Load("AllPerfect");
        fullComboSprite = Load("FullCombo");
        bandLineAp = Load("Band_Line_AP");
        bandLineFc = Load("Band_Line_FC");
        Sprite dot = Load("Clock_Dot");
        Sprite ring = Load("Clock_Ring");
        SetSprite(bandBg, Load("Band_Bg"));
        SetSprite(clockDashes, Load("Clock_Dashes"));
        SetSprite(clockFace, Load("Clock_Face"));
        SetSprite(clockHighlight, ring);
        SetSprite(clockBurst, ring);
        SetSprite(minuteHand, Load("Clock_MinuteHand"));
        SetSprite(hourHand, Load("Clock_HourHand"));
        SetSprite(clockCenter, dot);
        SetSprite(clockTopDot, dot);
        SetSprite(logoShine, Load("Logo_Shine"));
        SetSprite(capsule, Load("Capsule_Fill"));
        SetSprite(capsuleLine, Load("Capsule_Line"));
        Sprite pillar = Load("Lane_Pillar");
        if (lanePillars != null) foreach (Image p in lanePillars) SetSprite(p, pillar);
        if (motes != null) foreach (Image m in motes) SetSprite(m, dot);

        UITheme theme = UITheme.Current;
        if (bandBg != null) bandBg.color = theme.clearImageTint;
        if (bandLineTop != null) bandLineTop.color = theme.clearImageTint;
        if (bandLineBottom != null) bandLineBottom.color = theme.clearImageTint;
        if (capsule != null) capsule.color = theme.clearCapsuleFill;
        if (countText != null) countText.color = theme.clearCountNumber;
        if (countTotal != null) countTotal.color = theme.clearCountTotal;

        if (countLabel != null)
        {
            countLabel.OnPreRenderText -= ApplyLabelGradient;
            countLabel.OnPreRenderText += ApplyLabelGradient;
        }

        type = ClearType.None;
        elapsed = 0f;
        Apply(ClearBannerTimeline.Hidden);
    }

    /// <summary>
    /// 연출을 시작한다 (모든 노트 판정 순간). count = AP면 PERFECT 수, FC면 최대 콤보 (끝에 보일 값), totalNotes = 캡슐의 전체.
    /// None이면 숨긴 채로 둔다.
    /// </summary>
    public void Play(ClearType clearType, int count, int totalNotes)
    {
        if (group == null) CacheComponents();
        type = clearType;
        elapsed = 0f;
        this.count = Mathf.Max(0, count);
        total = Mathf.Max(0, totalNotes);
        shownCount = -1;
        if (type == ClearType.None)
        {
            Apply(ClearBannerTimeline.Hidden);
            return;
        }

        UITheme theme = UITheme.Current;
        colors = theme.ClearColorsOf(type);
        bool ap = type == ClearType.AllPerfect;

        if (logo != null)
        {
            logo.sprite = ap ? allPerfectSprite : fullComboSprite;
            logoRect.sizeDelta = ap ? AllPerfectLogoSize : FullComboLogoSize;
        }
        SetSprite(bandLineTop, ap ? bandLineAp : bandLineFc);
        SetSprite(bandLineBottom, ap ? bandLineAp : bandLineFc);

        Paint(clockDashes, colors.clock);
        Paint(clockFace, colors.clock);
        Paint(minuteHand, colors.clock);
        Paint(hourHand, colors.clock);
        Paint(clockCenter, colors.clock);
        Paint(capsuleLine, colors.capsuleLine);

        if (motes != null)
        {
            ClearMote[] data = timeline.Motes;
            for (int i = 0; i < motes.Length && i < data.Length; i++)
                motes[i].rectTransform.sizeDelta = new Vector2(data[i].Size, data[i].Size);
        }

        labelStart = colors.labelStart;
        labelEnd = colors.labelEnd;
        if (countLabel != null)
        {
            countLabel.color = Color.white;
            countLabel.text = ap ? GameConstants.Play.ClearPerfectLabel : GameConstants.Play.ClearComboLabel;
            countLabel.havePropertiesChanged = true;
        }
        if (countTotal != null) countTotal.text = string.Format(GameConstants.Play.ClearTotalFormat, total);
        if (countLayout != null) countLayout.preferredWidth = ClearBannerRules.CountWidth(total);
        if (countText != null) countText.SetText(CountFormat, 0);
        if (capsuleRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(capsuleRect);

        Debug.Log($"[ClearBanner] 연출 시작: {type}, {(ap ? "PERFECT" : "최대 콤보")} {this.count} / 전체 {total}, 개수 칸 폭 {ClearBannerRules.CountWidth(total):0}px", this);
        Apply(timeline.Evaluate(0f, this.count));
    }

    /// <summary>Play 뒤 지난 곡 시간(초)으로 연출을 맞춘다 (InGameScene의 결과 대기 타이머 값). 연출이 없으면 아무것도 하지 않는다</summary>
    public void SetTime(float time)
    {
        if (type == ClearType.None) return;
        elapsed = Mathf.Max(0f, time);
        Apply(timeline.Evaluate(elapsed, count));
    }

    /// <summary>계산값을 오브젝트에 적용한다</summary>
    private void Apply(ClearBannerFrame f)
    {
        Frame = f;
        group.alpha = f.Visible ? f.RootAlpha : 0f;

        if (flash != null)
        {
            flash.enabled = f.Visible && f.FlashAlpha > 0f;
            if (flash.enabled) flash.color = WithAlpha(colors.flash, f.FlashAlpha);
        }

        if (bandBg != null)
        {
            bandBg.rectTransform.localScale = new Vector3(1f, f.BandScaleY, 1f);
            float lineY = (BandHeight * 0.5f - BandLineThickness * 0.5f) * f.BandScaleY;
            if (bandLineTop != null) bandLineTop.rectTransform.anchoredPosition = new Vector2(0f, lineY);
            if (bandLineBottom != null) bandLineBottom.rectTransform.anchoredPosition = new Vector2(0f, -lineY);
        }

        if (clockGroup != null)
        {
            clockGroup.alpha = f.ClockAlpha;
            clockRect.localScale = new Vector3(f.ClockScale, f.ClockScale, 1f);
            // 시계 각도(시계 방향 +) → Unity z 회전(반시계 방향 +)
            if (clockDashes != null) clockDashes.rectTransform.localEulerAngles = new Vector3(0f, 0f, -f.DashesAngle);
            if (minuteHand != null) minuteHand.rectTransform.localEulerAngles = new Vector3(0f, 0f, -f.MinuteAngle);
            if (hourHand != null) hourHand.rectTransform.localEulerAngles = new Vector3(0f, 0f, -f.HourAngle);
            if (clockHighlight != null)
            {
                clockHighlight.enabled = f.HighlightAlpha > 0f;
                clockHighlight.color = WithAlpha(Color.white, f.HighlightAlpha);
            }
            if (clockTopDot != null)
            {
                clockTopDot.enabled = f.TopDotAlpha > 0f;
                clockTopDot.color = WithAlpha(Color.white, f.TopDotAlpha);
            }
            if (clockBurst != null)
            {
                clockBurst.enabled = f.BurstAlpha > 0f;
                clockBurst.rectTransform.localScale = new Vector3(f.BurstScale, f.BurstScale, 1f);
                clockBurst.color = WithAlpha(colors != null ? colors.clock : Color.white, f.BurstAlpha);
            }
        }

        if (logo != null)
        {
            logoRect.anchoredPosition = logoBase + new Vector2(f.LogoOffsetX, f.LogoRise);
            logoRect.localScale = new Vector3(f.LogoScale, f.LogoScale, 1f);
            logo.color = WithAlpha(Color.white, f.LogoAlpha);
            if (logoBright != null)
            {
                logoBright.enabled = f.LogoBright > 0f;
                logoBright.color = WithAlpha(Color.white, f.LogoBright);
            }
            if (logoShine != null)
            {
                logoShine.enabled = f.ShineVisible;
                float half = logoRect.rect.width * 0.5f + ShineWidth;
                shineRect.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, f.ShineProgress), 0f);
            }
        }

        if (capsule != null)
        {
            if (capsuleGroup != null) capsuleGroup.alpha = f.CapsuleAlpha;
            capsuleRect.anchoredPosition = capsuleBase + new Vector2(0f, f.CapsuleRise);
            if (f.Visible && f.Count != shownCount && countText != null)
            {
                shownCount = f.Count;
                countText.SetText(CountFormat, shownCount);
            }
        }

        int active = 0;
        if (motes != null)
        {
            Color moteColor = colors != null ? colors.mote : Color.white;
            ClearMote[] data = timeline.Motes;
            for (int i = 0; i < motes.Length; i++)
            {
                float alpha = 0f;
                Vector2 pos = Vector2.zero;
                if (f.Visible && i < data.Length) alpha = timeline.MoteAt(i, elapsed, out pos);
                bool on = alpha > 0f;
                motes[i].enabled = on;
                if (!on) continue;
                active++;
                motes[i].rectTransform.anchoredPosition = pos;
                motes[i].color = WithAlpha(data[i].White ? Color.white : moteColor, alpha);
            }
        }
        ActiveMoteCount = active;

        if (lanePillars != null)
        {
            for (int i = 0; i < lanePillars.Length; i++)
            {
                Image p = lanePillars[i];
                if (p == null) continue;
                p.enabled = f.Visible && f.PillarHeight > 0f;
                if (!p.enabled) continue;
                RectTransform r = p.rectTransform;
                r.sizeDelta = new Vector2(r.sizeDelta.x, f.PillarHeight);
                p.color = WithAlpha(colors.pillar, colors.pillar.a * f.PillarAlpha);
            }
        }
    }

    /// <summary>
    /// Txt_CountLabel을 글자 전체 폭 기준 가로 그라데이션으로 칠한다 (TMP가 메시를 만들 때마다, 왼쪽 = labelStart, 오른쪽 = labelEnd).
    /// TMP colorGradient는 글자마다 따로 칠해져서 쓰지 않는다.
    /// </summary>
    private void ApplyLabelGradient(TMP_TextInfo info)
    {
        if (info == null || info.characterCount == 0) return;
        float xMin = float.MaxValue, xMax = float.MinValue;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue;
            xMin = Mathf.Min(xMin, c.bottomLeft.x);
            xMax = Mathf.Max(xMax, c.topRight.x);
        }
        if (xMax <= xMin) return;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue;
            Color32[] colors32 = info.meshInfo[c.materialReferenceIndex].colors32;
            Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            for (int v = 0; v < 4; v++)
            {
                int index = c.vertexIndex + v;
                float t = Mathf.InverseLerp(xMin, xMax, vertices[index].x);
                Color color = Color.Lerp(labelStart, labelEnd, t);
                color.a *= colors32[index].a / 255f;
                colors32[index] = color;
            }
        }
    }

    private static Sprite Load(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(GameConstants.Play.ClearSpriteFolder + name);
        if (sprite == null) Debug.LogWarning($"[ClearBanner] Resources/{GameConstants.Play.ClearSpriteFolder}{name} 이미지가 없습니다.");
        return sprite;
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image != null) image.sprite = sprite;
    }

    private static void Paint(Image image, Color color)
    {
        if (image != null) image.color = color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
