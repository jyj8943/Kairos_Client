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
/// 메뉴·인게임 색 한 곳 (기획서 3장 「UI 톤 · 라벤더 안개 (11/17)」, 3-4 「인게임 톤 개편 (11/18)」).
/// 에셋 하나(Resources/Data/UITheme.asset)를 UITheme.Current로 쓴다.
/// - 필드 기본값 = 기획서 표 값. 톤을 바꿀 때는 에셋 값만 고친다.
/// - 메뉴 묶음: 곡 선택·결과·인게임 왼쪽 패널(유리 패널 위 글자·판정 이름·난이도 뱃지)이 본다.
/// - 인게임 묶음: 기어·노트·판정선·키 칸·라인 빛·기어 위 판정 글자·콤보 (어두운 기어 위라 밝은 색).
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

    [Header("판정 색 (메뉴용 = 밝은 배경용 진한 색. 기어 위 판정 글자는 인게임 묶음)")]
    public Color judgePerfect = new Color32(0xB7, 0x86, 0x1E, 0xFF);
    public Color judgeGreat = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color judgeGood = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color judgeMiss = new Color32(0xC9, 0x50, 0x4A, 0xFF);
    public Color judgeFast = new Color32(0x2E, 0x9C, 0x8F, 0xFF);
    public Color judgeSlow = new Color32(0xD2, 0x7A, 0x3C, 0xFF);

    [Header("난이도 색 (메뉴용, 위 글자 흰색. 인게임 곡 정보 카드 뱃지도 이 색)")]
    public Color difficultyEasy = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color difficultyNormal = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color difficultyHard = new Color32(0xC2, 0x45, 0x7E, 0xFF);
    public Color difficultyCustom = new Color32(0x8B, 0x7F, 0xD1, 0xFF);
    public Color onDifficultyText = Color.white;

    [Header("랭크 빈 자리 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」, 기록이 없는 채보의 기록 카드 랭크 칸)")]
    [Tooltip("빈 자리 바탕 Img_RankEmpty (흰색 55%)")]
    public Color rankEmptyFill = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("빈 자리 점선 Img_RankEmpty/Img_RankEmptyDash (#2C2838 35%)")]
    public Color rankEmptyDash = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.35f);

    [Header("인게임 왼쪽 패널 (유리 패널 위, 글자는 메뉴용 색)")]
    [Tooltip("유리 패널 1px 테두리 (흰색 70%). 패널 바탕은 panel")]
    public Color panelBorder = new Color(1f, 1f, 1f, 0.70f);
    [Tooltip("RATE · SCORE 박스 바탕 (흰색 62%)")]
    public Color statBox = new Color(1f, 1f, 1f, 0.62f);
    [Tooltip("RATE · SCORE 박스 1px 테두리 (#2C2838 20%)")]
    public Color statBoxBorder = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.20f);

    [Header("인게임 · 기어 (기획서 3-4 「인게임 톤 개편 (11/18)」, 어두운 기어)")]
    [Tooltip("기어 바탕 LaneBackground (rgba(36,32,46) 86%)")]
    public Color gearBackground = new Color(36 / 255f, 32 / 255f, 46 / 255f, 0.86f);
    [Tooltip("양옆 테두리 Border_Left · Border_Right (#7CC4FA 55%)")]
    public Color gearBorder = new Color(0x7C / 255f, 0xC4 / 255f, 0xFA / 255f, 0.55f);
    [Tooltip("라인 구분선 (흰색 8%)")]
    public Color laneDivider = new Color(1f, 1f, 1f, 0.08f);
    [Tooltip("마디선 (흰색 35%)")]
    public Color measureLine = new Color(1f, 1f, 1f, 0.35f);

    [Header("인게임 · 노트")]
    [Tooltip("숏노트 (#F7F5FA)")]
    public Color shortNote = new Color32(0xF7, 0xF5, 0xFA, 0xFF);
    [Tooltip("숏노트 빛 (은은한 하늘색, 알파 = 빛 세기)")]
    public Color shortNoteGlow = new Color(0x7C / 255f, 0xC4 / 255f, 0xFA / 255f, 0.35f);
    [Tooltip("롱노트 몸통 색 (알파는 아래 위·아래 값으로 세로 그라데이션)")]
    public Color longNoteBody = Color.white;
    [Tooltip("롱노트 몸통 위(꼬리 쪽) 불투명도")]
    [Range(0f, 1f)] public float longNoteTopAlpha = 0.55f;
    [Tooltip("롱노트 몸통 아래(머리 쪽) 불투명도")]
    [Range(0f, 1f)] public float longNoteBottomAlpha = 0.85f;
    [Tooltip("시작 MISS 롱노트 색 (끝까지 이 색, 알파는 같은 그라데이션)")]
    public Color longNoteMissed = new Color32(0x5A, 0x5A, 0x5E, 0xFF);

    [Header("인게임 · 판정선 · 키 칸 · 라인 빛")]
    [Tooltip("판정선 (#3E9BE0)")]
    public Color judgeLine = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("키 칸 평소 바탕 (rgba(44,40,56) 86%)")]
    public Color keyCell = new Color(44 / 255f, 40 / 255f, 56 / 255f, 0.86f);
    [Tooltip("키 칸 평소 테두리 (흰색 12%)")]
    public Color keyCellFrame = new Color(1f, 1f, 1f, 0.12f);
    [Tooltip("키 칸 평소 글자 (#D9D5E2)")]
    public Color keyLabel = new Color32(0xD9, 0xD5, 0xE2, 0xFF);
    [Tooltip("키 칸 누를 때 채움 (#3E9BE0)")]
    public Color keyCellPressed = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("키 칸 누를 때 테두리 (채움과 같은 색)")]
    public Color keyCellFramePressed = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("키 칸 누를 때 글자 (흰색)")]
    public Color keyLabelPressed = Color.white;
    [Tooltip("누른 라인 빛 LaneGlow (하늘색, 알파 = 판정선 쪽 가장 진한 곳)")]
    public Color laneGlow = new Color(0x3E / 255f, 0x9B / 255f, 0xE0 / 255f, 0.55f);

    [Header("인게임 · 기어 위 글자 (밝은 색)")]
    [Tooltip("콤보 제목 COMBO (#A9A3B8)")]
    public Color comboTitle = new Color32(0xA9, 0xA3, 0xB8, 0xFF);
    [Tooltip("콤보 숫자 (흰색)")]
    public Color comboNumber = Color.white;
    public Color inGamePerfect = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    [Tooltip("GREAT (#7CC4FA)")]
    public Color inGameGreat = new Color32(0x7C, 0xC4, 0xFA, 0xFF);
    public Color inGameGood = new Color32(0x9C, 0xCC, 0x65, 0xFF);
    public Color inGameMiss = new Color32(0xEF, 0x53, 0x50, 0xFF);
    [Tooltip("FAST (#5EEAD4)")]
    public Color inGameFast = new Color32(0x5E, 0xEA, 0xD4, 0xFF);
    public Color inGameSlow = new Color32(0xFB, 0x92, 0x3C, 0xFF);
    [Tooltip("판정 글자 빛 세기 (판정 색 × 이 알파로 TMP Glow를 칠한다, 0이면 빛 없음)")]
    [Range(0f, 1f)] public float inGameJudgeGlowAlpha = 0.45f;

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

    /// <summary>메뉴용(밝은 배경) 판정 이름 색 (인게임 왼쪽 판정 패널 라벨)</summary>
    public Color GetMenuJudge(JudgeType judge)
    {
        switch (judge)
        {
            case JudgeType.Perfect: return judgePerfect;
            case JudgeType.Great: return judgeGreat;
            case JudgeType.Good: return judgeGood;
            default: return judgeMiss;
        }
    }

    /// <summary>인게임용(어두운 기어 위) 판정 글자 색</summary>
    public Color GetInGameJudge(JudgeType judge)
    {
        switch (judge)
        {
            case JudgeType.Perfect: return inGamePerfect;
            case JudgeType.Great: return inGameGreat;
            case JudgeType.Good: return inGameGood;
            default: return inGameMiss;
        }
    }

    /// <summary>
    /// 롱노트 몸통 세로 불투명도 (순수 함수): t = 0 아래 끝(머리) → 1 위 끝(꼬리), 사이는 직선으로 잇는다.
    /// 셰이더 Kairos/LongNoteBody가 같은 식을 쓴다 (롱노트 길이와 상관없이 막대 전체에 걸친다).
    /// </summary>
    public static float LongNoteAlphaAt(float t, float bottom, float top)
    {
        return Mathf.Lerp(bottom, top, Mathf.Clamp01(t));
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
