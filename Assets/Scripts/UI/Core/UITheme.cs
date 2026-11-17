using TMPro;
using UnityEngine;

/// <summary>글자 단계 (기획서 3장 UI 톤: 기본 · 보조 · 설명 · 흐린 글자)</summary>
public enum UITextLevel
{
    Primary,
    Secondary,
    Description,
    Muted
}

/// <summary>
/// 메뉴 화면 색 한 곳 (기획서 3장 「UI 톤 · 라벤더 안개 (11/17)」). 에셋 하나(Resources/Data/UITheme.asset)를 UITheme.Current로 쓴다.
/// - 필드 기본값 = 기획서 표 값. 톤을 바꿀 때는 에셋 값만 고친다.
/// - 메뉴 화면(곡 선택, 나중에 결과 등)만 이 색을 본다. 인게임 색(DifficultyColors, 인게임 판정 색)은 따로 둔다.
/// - 씬·프리팹의 스크립트에 색을 직렬화하지 않고, 화면 Init에서 이 값을 읽어 칠한다 (UIThemeColor · UIThemePaint).
/// </summary>
[CreateAssetMenu(menuName = "Kairos/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    /// <summary>Resources 안 경로 (Resources/Data/UITheme.asset)</summary>
    public const string ResourcePath = "Data/UITheme";

    [Header("배경")]
    [Tooltip("맨 아래 바탕색")]
    public Color backgroundBase = new Color32(0xE7, 0xE2, 0xEE, 0xFF);
    [Tooltip("곡 일러스트 채도 (1 = 원래, 0.45 = 45%)")]
    [Range(0f, 2f)] public float illustrationSaturation = 0.45f;
    [Tooltip("곡 일러스트 밝기 (1 = 원래, 1.2 = 120%)")]
    [Range(0f, 2f)] public float illustrationBrightness = 1.2f;
    [Tooltip("일러스트를 덮는 색 (알파는 아래 세 값을 쓴다)")]
    public Color overlayColor = new Color32(0xEC, 0xE8, 0xF2, 0xFF);
    [Range(0f, 1f)] public float overlayAlphaLeft = 0.80f;
    [Range(0f, 1f)] public float overlayAlphaCenter = 0.62f;
    [Range(0f, 1f)] public float overlayAlphaRight = 0.82f;

    [Header("패널 · 하단 바 · 구분선")]
    [Tooltip("패널·카드 (흰색 58%, 반투명 유리)")]
    public Color panel = new Color(1f, 1f, 1f, 0.58f);
    [Tooltip("하단 바 (흰색 62%)")]
    public Color bottomBar = new Color(1f, 1f, 1f, 0.62f);
    [Tooltip("구분선 (#2C2838 12%)")]
    public Color divider = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.12f);
    [Tooltip("앨범이 없을 때 칸 색")]
    public Color emptyCover = new Color32(0xCF, 0xC8, 0xDB, 0xFF);

    [Header("포인트색 · 선택")]
    [Tooltip("포인트색 (밝은 배경용 하늘색)")]
    public Color accent = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("포인트색 위 글자")]
    public Color onAccentText = Color.white;
    [Tooltip("선택 항목 바탕 = 포인트색 × 이 불투명도")]
    [Range(0f, 1f)] public float selectedAlpha = 0.18f;

    [Header("글자 4단계")]
    public Color textPrimary = new Color32(0x2C, 0x28, 0x38, 0xFF);
    public Color textSecondary = new Color32(0x3E, 0x38, 0x52, 0xFF);
    public Color textDescription = new Color32(0x5A, 0x54, 0x70, 0xFF);
    public Color textMuted = new Color32(0x7A, 0x73, 0x90, 0xFF);

    [Header("판정 색 (메뉴용. 인게임은 따로)")]
    public Color judgePerfect = new Color32(0xB7, 0x86, 0x1E, 0xFF);
    public Color judgeGreat = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color judgeGood = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color judgeMiss = new Color32(0xC9, 0x50, 0x4A, 0xFF);
    public Color judgeFast = new Color32(0x2E, 0x9C, 0x8F, 0xFF);
    public Color judgeSlow = new Color32(0xD2, 0x7A, 0x3C, 0xFF);

    [Header("난이도 색 (메뉴용, 위 글자 흰색. 인게임은 DifficultyColors)")]
    public Color difficultyEasy = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color difficultyNormal = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color difficultyHard = new Color32(0xC2, 0x45, 0x7E, 0xFF);
    public Color difficultyCustom = new Color32(0x8B, 0x7F, 0xD1, 0xFF);
    public Color onDifficultyText = Color.white;

    [Header("글꼴")]
    [Tooltip("랭크 글자 글꼴 (비우면 그 글자의 원래 글꼴. 랭크 글꼴이 정해지면 이 칸만 바꾼다)")]
    public TMP_FontAsset rankFont;

    private static UITheme current;

    /// <summary>
    /// 지금 테마 (Resources/Data/UITheme.asset을 처음 한 번 읽어 둔다). 에셋이 없으면 기본값으로 만든 것을 쓴다.
    /// </summary>
    public static UITheme Current
    {
        get
        {
            if (current != null) return current;
            current = Resources.Load<UITheme>(ResourcePath);
            if (current == null)
            {
                Debug.LogWarning($"[UITheme] Resources/{ResourcePath}.asset이 없어 기본값을 씁니다.");
                current = CreateInstance<UITheme>();
            }
            return current;
        }
    }

    /// <summary>선택 항목 바탕 (포인트색 × selectedAlpha)</summary>
    public Color SelectedBackground => WithAlpha(accent, selectedAlpha);

    /// <summary>글자 단계 색</summary>
    public Color GetText(UITextLevel level)
    {
        switch (level)
        {
            case UITextLevel.Secondary: return textSecondary;
            case UITextLevel.Description: return textDescription;
            case UITextLevel.Muted: return textMuted;
            default: return textPrimary;
        }
    }

    /// <summary>메뉴용 난이도 색 (이름으로 찾고 대소문자는 가리지 않는다. 모르는 이름은 CUSTOM)</summary>
    public Color GetDifficulty(string difficulty)
    {
        string name = string.IsNullOrEmpty(difficulty) ? string.Empty : difficulty.Trim().ToUpperInvariant();
        switch (name)
        {
            case GameConstants.Chart.DifficultyEasy: return difficultyEasy;
            case GameConstants.Chart.DifficultyNormal: return difficultyNormal;
            case GameConstants.Chart.DifficultyHard: return difficultyHard;
            default: return difficultyCustom;
        }
    }

    /// <summary>도장 색: FC는 포인트색, AP는 PERFECT 색</summary>
    public Color GetStamp(RecordStamp stamp)
    {
        return stamp == RecordStamp.AllPerfect ? judgePerfect : accent;
    }

    /// <summary>랭크 글자색: S는 포인트색, 나머지는 기본 글자색 (기록 없음 "―"는 흐린 글자)</summary>
    public Color GetRank(RecordTexts texts)
    {
        if (!texts.HasRecord) return textMuted;
        return texts.IsRankS ? accent : textPrimary;
    }

    /// <summary>
    /// 덮는 색 가로 불투명도 (순수 함수): t = 0 왼쪽 → 0.5 가운데 → 1 오른쪽, 사이는 직선으로 잇는다.
    /// </summary>
    public static float OverlayAlphaAt(float t, float left, float center, float right)
    {
        t = Mathf.Clamp01(t);
        return t <= 0.5f ? Mathf.Lerp(left, center, t * 2f) : Mathf.Lerp(center, right, (t - 0.5f) * 2f);
    }

    /// <summary>이 테마의 덮는 색 가로 위치 t의 색 (알파 포함)</summary>
    public Color OverlayAt(float t)
    {
        return WithAlpha(overlayColor, OverlayAlphaAt(t, overlayAlphaLeft, overlayAlphaCenter, overlayAlphaRight));
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
