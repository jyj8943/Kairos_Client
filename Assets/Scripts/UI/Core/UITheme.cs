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
/// - 팝업 묶음: 설정 창·확인 팝업·일시정지의 덮개·패널·그림자·기본 버튼·위험 빨강 (포인트색·글자·선택 바탕은 메뉴 묶음 값을 같이 쓴다).
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

    [Header("판정 색 (메뉴용 = 밝은 배경용 진한 색, 11/19 B3안. 인게임 판정 패널·결과. 기어 위 판정 글자는 인게임 묶음)")]
    [Tooltip("PERFECT 가로 그라데이션 왼쪽 (금 #B87A10)")]
    public Color judgePerfectStart = new Color32(0xB8, 0x7A, 0x10, 0xFF);
    [Tooltip("PERFECT 가로 그라데이션 오른쪽 (분홍 #C8358C)")]
    public Color judgePerfectEnd = new Color32(0xC8, 0x35, 0x8C, 0xFF);
    [Tooltip("GREAT (남보라 #6650D8)")]
    public Color judgeGreat = new Color32(0x66, 0x50, 0xD8, 0xFF);
    [Tooltip("GOOD (초록 #2C9C66)")]
    public Color judgeGood = new Color32(0x2C, 0x9C, 0x66, 0xFF);
    [Tooltip("MISS (장미빛 #C24D6B)")]
    public Color judgeMiss = new Color32(0xC2, 0x4D, 0x6B, 0xFF);
    [Tooltip("FAST (하늘 #2389C4)")]
    public Color judgeFast = new Color32(0x23, 0x89, 0xC4, 0xFF);
    [Tooltip("SLOW (호박 #C97418)")]
    public Color judgeSlow = new Color32(0xC9, 0x74, 0x18, 0xFF);

    [Header("난이도 색 (메뉴용, 위 글자 흰색. 인게임 곡 정보 카드 뱃지도 이 색)")]
    public Color difficultyEasy = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color difficultyNormal = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color difficultyHard = new Color32(0xC2, 0x45, 0x7E, 0xFF);
    public Color difficultyCustom = new Color32(0x8B, 0x7F, 0xD1, 0xFF);
    public Color onDifficultyText = Color.white;

    [Header("나만의 채보 탭 (기획서 3-3 「나만의 채보 탭 (11/17, 11/18 개정)」)")]
    [Tooltip("지금 고른 곡의 머리 줄 곡 이름 (#2F7FC8)")]
    public Color myChartChosenSongTitle = new Color32(0x2F, 0x7F, 0xC8, 0xFF);

    [Header("랭크 빈 자리 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」, 기록이 없는 채보의 기록 카드 랭크 칸)")]
    [Tooltip("빈 자리 바탕 Img_RankEmpty (흰색 55%)")]
    public Color rankEmptyFill = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("빈 자리 점선 Img_RankEmpty/Img_RankEmptyDash (#2C2838 35%)")]
    public Color rankEmptyDash = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.35f);

    [Header("팝업 (기획서 3-7 「팝업 공통 톤 (11/18)」: 설정 창 · 확인 팝업 · 일시정지)")]
    [Tooltip("뒤 화면 블러 위 덮개 (rgba(36,32,46) 42%)")]
    public Color popupDim = new Color(36 / 255f, 32 / 255f, 46 / 255f, 0.42f);
    [Tooltip("일시정지 재개 카운트다운 덮개 (같은 색 28%, 블러 없음)")]
    public Color popupCountdownDim = new Color(36 / 255f, 32 / 255f, 46 / 255f, 0.28f);
    [Tooltip("팝업 패널 (rgba(250,248,253) 86% 유리)")]
    public Color popupPanel = new Color(250 / 255f, 248 / 255f, 253 / 255f, 0.86f);
    [Tooltip("팝업 패널 테두리 (흰색)")]
    public Color popupPanelBorder = Color.white;
    [Tooltip("팝업 패널 그림자 (rgba(36,32,46) 28%, 가장자리가 부드러운 스프라이트에 곱한다)")]
    public Color popupShadow = new Color(36 / 255f, 32 / 255f, 46 / 255f, 0.28f);
    [Tooltip("위험 빨강 #C9504A (나가기·삭제 버튼 · 링 · \"!\" 원 · 나가기 막대·글자)")]
    public Color danger = new Color32(0xC9, 0x50, 0x4A, 0xFF);
    [Tooltip("위험 선택 바탕 = 위험 빨강 × 이 불투명도 (16%)")]
    [Range(0f, 1f)] public float dangerSelectedAlpha = 0.16f;
    [Tooltip("기본 버튼·항목 줄 바탕 (흰색 75%). 테두리는 구분선 색(divider)")]
    public Color popupButton = new Color(1f, 1f, 1f, 0.75f);
    [Tooltip("일시정지 지금 곡 카드 바탕 (#2C2838 6%, 패널보다 살짝 어둡게)")]
    public Color popupCard = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.06f);
    [Tooltip("버튼 선택 링 안쪽 (흰색). 바깥쪽은 포인트색 (위험 버튼은 위험 빨강)")]
    public Color popupRingInner = Color.white;
    [Tooltip("일시정지 재개 카운트다운 숫자 (흰색)")]
    public Color countdownNumber = Color.white;
    [Tooltip("카운트다운 숫자 빛 (하늘색, 알파 = 빛 세기, TMP Underlay)")]
    public Color countdownGlow = new Color(0x3E / 255f, 0x9B / 255f, 0xE0 / 255f, 0.6f);

    [Header("인게임 왼쪽 패널 (11/19 라벤더 불투명: 곡 정보 카드 · 판정 패널 · 배속·밝기 카드, 밝기와 상관없이 고정. 글자는 메뉴용 색)")]
    [Tooltip("패널 바탕 (라벤더 불투명 #F4F1F8, 뒤 흐림 없음)")]
    public Color inGamePanel = new Color32(0xF4, 0xF1, 0xF8, 0xFF);
    [Tooltip("패널 1px 테두리 (흰색)")]
    public Color inGamePanelBorder = Color.white;
    [Tooltip("RATE · SCORE 박스 바탕 (흰색)")]
    public Color inGameStatBox = Color.white;
    [Tooltip("RATE · SCORE 박스 1px 테두리 (rgba(44,40,56) 14%)")]
    public Color inGameStatBoxBorder = new Color(44 / 255f, 40 / 255f, 56 / 255f, 0.14f);
    [Tooltip("배속·밝기 카드 밝기 막대 켜진 칸 (#3E9BE0)")]
    public Color inGameBrightnessBarOn = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("배속·밝기 카드 밝기 막대 꺼진 칸 (rgba(44,40,56) 14%)")]
    public Color inGameBrightnessBarOff = new Color(44 / 255f, 40 / 255f, 56 / 255f, 0.14f);

    [Header("인게임 · 기어 (기획서 3-4 「인게임 톤 개편 (11/18)」, 어두운 기어)")]
    [Tooltip("기어 바탕 LaneBackground (불투명 #17141F, 11/19: 레인 안으로 BGA·일러스트가 비치지 않게)")]
    public Color gearBackground = new Color32(0x17, 0x14, 0x1F, 0xFF);
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

    [Header("인게임 · 키 칸 · 라인 빛 (판정선은 이미지 그대로라 색이 없다)")]
    [Tooltip("키 칸 평소 바탕 (불투명 rgb(44,40,56), 11/19)")]
    public Color keyCell = new Color(44 / 255f, 40 / 255f, 56 / 255f, 1f);
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

    [Header("인게임 · 기어 위 콤보 (밝은 색. 기어 판정 글자·FAST/SLOW는 색이 든 이미지 Images/InGame/Judge/, 11/19)")]
    [Tooltip("콤보 제목 COMBO (#A9A3B8)")]
    public Color comboTitle = new Color32(0xA9, 0xA3, 0xB8, 0xFF);
    [Tooltip("콤보 숫자 (흰색)")]
    public Color comboNumber = Color.white;

    [Header("에디터 (기획서 3-6 「에디터 라벤더 톤 개편 (11/19)」. 패널·글자·포인트색·위험·팝업은 위 공용 값을 쓴다)")]
    [Tooltip("자동 테스트 버튼 채움 (보라 #7B6BD8)")]
    public Color editorAutoTest = new Color32(0x7B, 0x6B, 0xD8, 0xFF);
    [Tooltip("흰 버튼 테두리 (rgba(44,40,56) 16%: 저장 · 나가기 · 취소 · 배속 ◀ ▶)")]
    public Color editorButtonBorder = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.16f);
    [Tooltip("버튼 그림자 세기 (채움 색 × 이 알파, 오프셋 (0, 6) 퍼짐 16)")]
    [Range(0f, 1f)] public float editorButtonShadowAlpha = 0.30f;
    [Tooltip("BPM 구간 강조 (호박 #C97418): 라벨 바탕 · 목록 값 · 타임라인 눈금 · BPM(현재 위치) 막대 · 입력 창 강조 · \"● 저장 안 됨\"")]
    public Color editorAmber = new Color32(0xC9, 0x74, 0x18, 0xFF);
    [Tooltip("그리드 위 BPM 구간선 (밝은 호박 #FFB45C)")]
    public Color editorAmberLight = new Color32(0xFF, 0xB4, 0x5C, 0xFF);
    [Tooltip("호박 연한 바탕 알파 (고른 구간 줄 · 아이콘 바탕 14%, 노트 경고 박스 12%)")]
    [Range(0f, 1f)] public float editorAmberSoftAlpha = 0.14f;
    [Range(0f, 1f)] public float editorWarningBoxAlpha = 0.12f;
    [Tooltip("입력 오류 박스 바탕 알파 (위험 색 × 10%)")]
    [Range(0f, 1f)] public float editorErrorBoxAlpha = 0.10f;

    [Header("에디터 · 배경 (라벤더 + 아주 옅은 곡 일러스트 + 라벤더 세로 그라데이션)")]
    [Tooltip("곡 일러스트 불투명도 (22%)")]
    [Range(0f, 1f)] public float editorIllustrationOpacity = 0.22f;
    [Tooltip("곡 일러스트 흐림 (1920×1080 기준 sigma px)")]
    [Min(0f)] public float editorIllustrationBlur = 40f;
    [Tooltip("곡 일러스트 채도 (50%)")]
    [Range(0f, 2f)] public float editorIllustrationSaturation = 0.5f;
    [Tooltip("라벤더 덮기 (overlayColor) 위 → 아래 불투명도")]
    [Range(0f, 1f)] public float editorOverlayTop = 0.55f;
    [Range(0f, 1f)] public float editorOverlayBottom = 0.85f;

    [Header("에디터 · 도구 선택 표시 (다른 화면의 선택 표시와 같음: 하늘 14% + 하늘 테두리 + 선택 링)")]
    public Color toolSelectedBackground = new Color(0x3E / 255f, 0x9B / 255f, 0xE0 / 255f, 0.14f);
    public Color toolSelectedBorder = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    public Color toolSelectedText = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color toolNormalBackground = Color.white;
    public Color toolNormalBorder = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.10f);
    public Color toolNormalText = new Color32(0x2C, 0x28, 0x38, 0xFF);
    [Tooltip("고른 도구 테두리 두께 (px)")]
    [Min(0.5f)] public float toolSelectedBorderWidth = 1.5f;
    [Tooltip("고르지 않은 도구 테두리 두께 (px)")]
    [Min(0.5f)] public float toolNormalBorderWidth = 1f;
    [Tooltip("스위치 꺼짐 바탕 (rgba(44,40,56) 18%). 켜짐은 포인트색")]
    public Color editorSwitchOff = new Color(0x2C / 255f, 0x28 / 255f, 0x38 / 255f, 0.18f);

    [Header("에디터 · 그리드 (바탕은 인게임 기어와 같은 #17141F, 양쪽 테두리는 gearBorder)")]
    public Color editorGridBackground = new Color32(0x17, 0x14, 0x1F, 0xFF);
    [Tooltip("곡 시작 전(마디 1 아래) 빗금 (흰 4%)")]
    public Color editorGridHatch = new Color(1f, 1f, 1f, 0.04f);
    [Tooltip("숏노트 · 롱노트 머리 (인게임 노트와 같은 #F4F1F9)")]
    public Color editorNote = new Color32(0xF4, 0xF1, 0xF9, 0xFF);
    [Tooltip("롱노트 몸통 불투명도 (노트 색 × 이 알파, 35%)")]
    [Range(0f, 1f)] public float editorLongNoteBodyAlpha = 0.35f;
    [Tooltip("놓을 자리 미리 보기 테두리 (하늘)")]
    public Color editorPreview = new Color32(0x7D, 0xD3, 0xFC, 0xFF);
    public GridLineStyle editorLineMeasure = new GridLineStyle(new Color(1f, 1f, 1f, 0.70f), 2f);
    public GridLineStyle editorLineBeat = new GridLineStyle(new Color(0xFF / 255f, 0x7A / 255f, 0xC6 / 255f, 0.50f), 1f);
    [Tooltip("8분 (1박의 1/2)")]
    public GridLineStyle editorLine8th = new GridLineStyle(new Color(0x7D / 255f, 0xD3 / 255f, 0xFC / 255f, 0.32f), 1f);
    [Tooltip("16분 (1박의 1/4)")]
    public GridLineStyle editorLine16th = new GridLineStyle(new Color(0x7D / 255f, 0xD3 / 255f, 0xFC / 255f, 0.14f), 1f);
    [Tooltip("32분 (1박의 1/8)")]
    public GridLineStyle editorLine32nd = new GridLineStyle(new Color(0x7D / 255f, 0xD3 / 255f, 0xFC / 255f, 0.08f), 1f);
    [Tooltip("셋잇단 (1박의 1/3 · 1/6)")]
    public GridLineStyle editorLineTriplet = new GridLineStyle(new Color(0xA9 / 255f, 0x9C / 255f, 0xFF / 255f, 0.30f), 1f);
    [Tooltip("셋잇단 잘게 (1박의 1/12)")]
    public GridLineStyle editorLineTripletFine = new GridLineStyle(new Color(0xA9 / 255f, 0x9C / 255f, 0xFF / 255f, 0.14f), 1f);
    [Tooltip("나머지 (그 밖의 잘게 나눈 선)")]
    public GridLineStyle editorLineOther = new GridLineStyle(new Color(1f, 1f, 1f, 0.06f), 1f);
    [Tooltip("라인 구분선 (흰 10%)")]
    public GridLineStyle editorLaneDivider = new GridLineStyle(new Color(1f, 1f, 1f, 0.10f), 1f);
    [Tooltip("마디 번호 (그리드 왼쪽 바깥)")]
    public Color editorMeasureNumber = new Color32(0x5A, 0x54, 0x70, 0xFF);

    [Header("에디터 · 현재 위치 선 (판정선과 같은 분홍 → 보라 가로 그라데이션 + 왼쪽 분홍 삼각형)")]
    public Color editorCurrentLineStart = new Color32(0xFF, 0x5F, 0xB4, 0xFF);
    public Color editorCurrentLineEnd = new Color32(0x9B, 0x6B, 0xFF, 0xFF);
    [Tooltip("재생 중 박마다 현재 위치 선을 밝게 할 때 덮는 색 (흰색)")]
    public Color editorCurrentLineFlash = Color.white;

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

    /// <summary>위험 항목 선택 바탕 (위험 빨강 × dangerSelectedAlpha)</summary>
    public Color DangerSelectedBackground => WithAlpha(danger, dangerSelectedAlpha);

    /// <summary>포인트색 또는 위험 빨강 (위험 표시면 빨강)</summary>
    public Color AccentOrDanger(bool isDanger) => isDanger ? danger : accent;

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

    /// <summary>메뉴용(밝은 배경) 판정 이름 색 (인게임 왼쪽 판정 패널 라벨 · 결과). PERFECT는 금 → 분홍 가로 그라데이션</summary>
    public JudgeColor GetMenuJudge(JudgeType judge)
    {
        switch (judge)
        {
            case JudgeType.Perfect: return JudgeColor.Gradient(judgePerfectStart, judgePerfectEnd);
            case JudgeType.Great: return JudgeColor.Solid(judgeGreat);
            case JudgeType.Good: return JudgeColor.Solid(judgeGood);
            default: return JudgeColor.Solid(judgeMiss);
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

/// <summary>
/// 판정 글자 색 한 벌 (단색 또는 가로 그라데이션). PERFECT만 그라데이션이고 나머지는 단색이다.
/// 그라데이션은 TMP colorGradient(enableVertexGradient)로 칠한다: 왼쪽 위·아래 = Start, 오른쪽 위·아래 = End (UIThemePaint.JudgeText).
/// </summary>
public readonly struct JudgeColor
{
    /// <summary>단색이면 그 색, 그라데이션이면 왼쪽 색</summary>
    public readonly Color Start;

    /// <summary>그라데이션 오른쪽 색 (단색이면 Start와 같음)</summary>
    public readonly Color End;

    /// <summary>가로 그라데이션이면 true</summary>
    public readonly bool IsGradient;

    private JudgeColor(Color start, Color end, bool isGradient)
    {
        Start = start;
        End = end;
        IsGradient = isGradient;
    }

    public static JudgeColor Solid(Color color) => new JudgeColor(color, color, false);

    public static JudgeColor Gradient(Color start, Color end) => new JudgeColor(start, end, true);

    /// <summary>TMP 가로 그라데이션 (왼쪽 위·아래 = Start, 오른쪽 위·아래 = End)</summary>
    public TMPro.VertexGradient ToVertexGradient() => new TMPro.VertexGradient(Start, End, Start, End);
}
