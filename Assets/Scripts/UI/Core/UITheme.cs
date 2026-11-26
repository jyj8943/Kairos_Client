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

    [Header("판정 색 (메뉴용 = 밝은 배경용 진한 색, 11/19 B3안, 11/20 GREAT·MISS·FAST·SLOW 바꿈. 인게임 판정 패널·결과 (이름은 JudgeMenu 이미지, 이 색은 결과 FAST/SLOW 개수·막대). 기어 위 판정 글자는 인게임 묶음)")]
    [Tooltip("PERFECT 가로 그라데이션 왼쪽 (금 #B87A10)")]
    public Color judgePerfectStart = new Color32(0xB8, 0x7A, 0x10, 0xFF);
    [Tooltip("PERFECT 가로 그라데이션 오른쪽 (분홍 #C8358C)")]
    public Color judgePerfectEnd = new Color32(0xC8, 0x35, 0x8C, 0xFF);
    [Tooltip("GREAT (코랄 #E0503E)")]
    public Color judgeGreat = new Color32(0xE0, 0x50, 0x3E, 0xFF);
    [Tooltip("GOOD (초록 #2C9C66)")]
    public Color judgeGood = new Color32(0x2C, 0x9C, 0x66, 0xFF);
    [Tooltip("MISS (회색 #6E6A7A)")]
    public Color judgeMiss = new Color32(0x6E, 0x6A, 0x7A, 0xFF);
    [Tooltip("FAST (코발트 #3E63D6)")]
    public Color judgeFast = new Color32(0x3E, 0x63, 0xD6, 0xFF);
    [Tooltip("SLOW (자주 #A93BC2)")]
    public Color judgeSlow = new Color32(0xA9, 0x3B, 0xC2, 0xFF);

    [Header("난이도 색 (메뉴용, 위 글자 흰색. 인게임 곡 정보 카드 뱃지도 이 색)")]
    public Color difficultyEasy = new Color32(0x2E, 0x9C, 0x6A, 0xFF);
    public Color difficultyNormal = new Color32(0x2F, 0x7F, 0xC8, 0xFF);
    public Color difficultyHard = new Color32(0xC2, 0x45, 0x7E, 0xFF);
    public Color difficultyCustom = new Color32(0x8B, 0x7F, 0xD1, 0xFF);
    public Color onDifficultyText = Color.white;
    [Tooltip("곡 선택: 고른 난이도 탭 · 채보 머리 · 기록 패널에 겹쳐 칠하는 난이도 색 농도 (16%)")]
    [Range(0f, 1f)] public float difficultyTintChosenAlpha = 0.16f;
    [Tooltip("곡 선택: 고르지 않은 난이도 탭에 겹쳐 칠하는 난이도 색 농도 (7%)")]
    [Range(0f, 1f)] public float difficultyTintAlpha = 0.07f;
    [Tooltip("곡 선택: 목록의 새 채보 생성 줄에 칠하는 CUSTOM 보라 농도 (12%)")]
    [Range(0f, 1f)] public float newChartFillAlpha = 0.12f;
    [Tooltip("곡 선택: 왼쪽 새 채보 생성 카드에 칠하는 CUSTOM 보라 농도 (8%)")]
    [Range(0f, 1f)] public float newChartCardFillAlpha = 0.08f;

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

    [Header("암전 로딩 (기획서 3-10 「암전 로딩」, Canvas_FadeLoading. 인게임 → 결과)")]
    [Tooltip("검은 덮개 Img_Fade (알파는 연출이 0 ~ 1로 곱한다)")]
    public Color fadeColor = Color.black;

    [Header("일반 로딩 (기획서 3-9 「일반 로딩」, Canvas_Loading. 화면 이름 · TIP 글자는 위 글자 색, TIP 알약은 포인트색)")]
    [Tooltip("아래 띠 (#F4F1F8, 뒤 흐림 대신 85% 단색)")]
    public Color loadingBand = new Color(0xF4 / 255f, 0xF1 / 255f, 0xF8 / 255f, 0.85f);
    [Tooltip("띠 위 흰 선 1px")]
    public Color loadingBandLine = Color.white;
    [Tooltip("불러오는 중 시계 · 켜진 점 (보라 #7B6BD8)")]
    public Color loadingClock = new Color32(0x7B, 0x6B, 0xD8, 0xFF);

    [Header("곡 로딩 · 시계 (11/26 개편, Canvas_SongLoading. 뱃지는 위 난이도 색 · onDifficultyText)")]
    [Tooltip("곡 배경 위 덮개 Img_Dim (40, 30, 70) 59%")]
    public Color songLoadingDim = new Color(40f / 255f, 30f / 255f, 70f / 255f, 0.59f);
    [Tooltip("시곗바늘 호 Img_ClockArc (#7CC4FA)")]
    public Color songLoadingArc = new Color32(0x7C, 0xC4, 0xFA, 0xFF);
    [Tooltip("시계 눈금 Img_ClockTicks (그림의 흰색 그대로)")]
    public Color songLoadingTicks = Color.white;
    [Tooltip("바늘 끝 점 Img_ClockDot (흰색)")]
    public Color songLoadingDot = Color.white;
    [Tooltip("12시 반짝 Img_ClockSpark (흰색)")]
    public Color songLoadingSpark = Color.white;
    [Tooltip("앨범 뒤 빛 Img_Glow (210, 232, 255)")]
    public Color songLoadingGlow = new Color32(210, 232, 255, 255);
    [Tooltip("앨범 등장 번쩍 Img_AlbumFlash (흰색)")]
    public Color songLoadingFlash = Color.white;
    [Tooltip("곡 제목 · Lv 글자 (250, 248, 255)")]
    public Color songLoadingTitle = new Color32(250, 248, 255, 255);
    [Tooltip("아티스트 · CHART 채보 이름 (210, 204, 230)")]
    public Color songLoadingSub = new Color32(210, 204, 230, 255);
    [Tooltip("SD가 앉는 막대 Img_SdLine (흰색 59%)")]
    public Color songLoadingSdLine = new Color(1f, 1f, 1f, 0.59f);
    [Tooltip("LOADING 글자 (235, 232, 250)")]
    public Color songLoadingText = new Color32(235, 232, 250, 255);
    [Tooltip("끝 암전 Img_Blackout (검정)")]
    public Color songLoadingBlackout = Color.black;

    [Header("결과 · 카이로스 대화창 (11/25, Img_Dialog)")]
    [Tooltip("대화창 · 꼬리 바탕 (흰색 94%)")]
    public Color resultDialogFill = new Color(1f, 1f, 1f, 0.94f);
    [Tooltip("대화창 · 꼬리 테두리 2px (라벤더 #B4A6FF)")]
    public Color resultDialogBorder = new Color32(0xB4, 0xA6, 0xFF, 0xFF);
    [Tooltip("첫 줄 \"이 곡에서 되찾은 감정\" (120, 110, 150)")]
    public Color resultDialogLabel = new Color32(120, 110, 150, 255);
    [Tooltip("둘째 줄 감정 키워드 (70, 60, 110)")]
    public Color resultDialogKeyword = new Color32(70, 60, 110, 255);
    [Tooltip("셋째 줄 랭크 멘트 (80, 72, 110)")]
    public Color resultDialogLine = new Color32(80, 72, 110, 255);

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

    [Header("인게임 · 노트 (11/20: 라인 색 노트. 바깥 라인 0·3 라벤더, 안쪽 라인 1·2 하늘. NoteColor(lane)으로 고른다)")]
    [Tooltip("바깥 라인(0·3, A · ') 노트 · 키 칸 마름모 (#B4A6FF)")]
    public Color noteOuter = new Color32(0xB4, 0xA6, 0xFF, 0xFF);
    [Tooltip("안쪽 라인(1·2, S · ;) 노트 · 키 칸 마름모 (#7CC4FA)")]
    public Color noteInner = new Color32(0x7C, 0xC4, 0xFA, 0xFF);
    [Tooltip("숏노트 뒤 빛(Glow, SoftGlow)을 켤지. 11/20 실물 확인용으로 끔. 다시 켜면 noteGlowAlpha로 그대로 나온다")]
    public bool noteGlowEnabled = false;
    [Tooltip("숏노트 뒤 빛 알파 (색은 라인 색)")]
    [Range(0f, 1f)] public float noteGlowAlpha = 0.35f;
    [Tooltip("숏노트 · 롱노트 머리 위 광택 ShortNote_Shine (흰색 75%, 라인 색을 입히지 않는다)")]
    public Color noteShine = new Color(1f, 1f, 1f, 0.75f);
    [Tooltip("롱노트 몸통 가운데 선 LongNoteCenterLine (흰색 78%)")]
    public Color longNoteCenterLine = new Color(1f, 1f, 1f, 0.78f);
    [Tooltip("롱노트 몸통 위(꼬리 쪽) 불투명도 (11/20: 45%)")]
    [Range(0f, 1f)] public float longNoteTopAlpha = 0.45f;
    [Tooltip("롱노트 몸통 아래(머리 쪽) 불투명도 (11/20: 70%)")]
    [Range(0f, 1f)] public float longNoteBottomAlpha = 0.70f;
    [Tooltip("시작 MISS 롱노트 색 (몸통 · 머리 끝까지 이 색, 몸통 알파는 같은 그라데이션)")]
    public Color longNoteMissed = new Color32(0x5A, 0x5A, 0x5E, 0xFF);

    [Header("인게임 · 키 칸 · 라인 빛 (판정선은 이미지 그대로라 색이 없다)")]
    [Tooltip("키 칸 평소 바탕 (불투명 rgb(44,40,56), 11/19)")]
    public Color keyCell = new Color(44 / 255f, 40 / 255f, 56 / 255f, 1f);
    [Tooltip("키 칸 평소 테두리 (흰색 12%)")]
    public Color keyCellFrame = new Color(1f, 1f, 1f, 0.12f);
    [Tooltip("시작 키 글자 안내 글자 (흰색, 11/20: 평소에는 글자를 끄고 판 시작 때만 보인다)")]
    public Color keyLabel = Color.white;
    [Tooltip("키 칸 누를 때 채움 (#3E9BE0)")]
    public Color keyCellPressed = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("키 칸 누를 때 테두리 (채움과 같은 색)")]
    public Color keyCellFramePressed = new Color32(0x3E, 0x9B, 0xE0, 0xFF);
    [Tooltip("키 칸 마름모 누를 때 채움 Key_Diamond (흰색, 평소는 라인 색 NoteColor)")]
    public Color keyDiamondPressed = Color.white;
    [Tooltip("누른 라인 빛 LaneGlow (하늘색, 알파 = 판정선 쪽 가장 진한 곳)")]
    public Color laneGlow = new Color(0x3E / 255f, 0x9B / 255f, 0xE0 / 255f, 0.55f);

    [Header("인게임 · 판정 이펙트 R2 (11/20). 색 C = 판정별 색, 밝은 색 L = C와 흰색을 hitFxLightMix만큼 섞은 색. MISS는 이펙트 없음")]
    [Tooltip("PERFECT 이펙트 색 (#FFA698, 기어 PERFECT 그라데이션의 가운데)")]
    public Color hitFxPerfect = new Color32(0xFF, 0xA6, 0x98, 0xFF);
    [Tooltip("GREAT 이펙트 색 (#FF7E6E)")]
    public Color hitFxGreat = new Color32(0xFF, 0x7E, 0x6E, 0xFF);
    [Tooltip("GOOD 이펙트 색 (#6FE0A4)")]
    public Color hitFxGood = new Color32(0x6F, 0xE0, 0xA4, 0xFF);
    [Tooltip("밝은 색 L = Lerp(C, 흰색, 이 값)")]
    [Range(0f, 1f)] public float hitFxLightMix = 0.55f;

    [Header("인게임 · 기어 위 콤보 (밝은 색. 기어 판정 글자·FAST/SLOW는 색이 든 이미지 Images/InGame/Judge/, 11/19)")]
    [Tooltip("콤보 제목 COMBO (#A9A3B8)")]
    public Color comboTitle = new Color32(0xA9, 0xA3, 0xB8, 0xFF);
    [Tooltip("콤보 숫자 (흰색)")]
    public Color comboNumber = Color.white;

    [Header("인게임 · 곡 끝 FC/AP 연출 (기획서 3-4 「FC/AP 연출 (11/19)」, ClearBanner. 흰색 이미지에 입히는 색)")]
    [Tooltip("ALL PERFECT 색 묶음")]
    public ClearColors clearAllPerfect = ClearColors.AllPerfectDefault();
    [Tooltip("FULL COMBO 색 묶음")]
    public ClearColors clearFullCombo = ClearColors.FullComboDefault();
    [Tooltip("개수 캡슐 바탕 (#0C0A12, 94%, AP · FC 같음)")]
    public Color clearCapsuleFill = new Color(0x0C / 255f, 0x0A / 255f, 0x12 / 255f, 0.94f);
    [Tooltip("개수 숫자 (흰색)")]
    public Color clearCountNumber = Color.white;
    [Tooltip("개수 전체 \"/ 329\" (#C9C3D6)")]
    public Color clearCountTotal = new Color32(0xC9, 0xC3, 0xD6, 0xFF);
    [Tooltip("띠 바탕 · 글자 · 12시 강조 흰 테두리 (흰색 그대로, 이미지에 색이 들어 있음)")]
    public Color clearImageTint = Color.white;
    /// <summary>곡 끝 연출 종류에 맞는 색 묶음 (None이면 FULL COMBO)</summary>
    public ClearColors ClearColorsOf(ClearType type) => type == ClearType.AllPerfect ? clearAllPerfect : clearFullCombo;

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
    // 에디터 노트 색은 인게임 노트 색(NoteColor(lane) · noteShine · longNote*)을 같이 쓴다 (11/20)
    [Tooltip("놓을 자리 미리 보기 · 롱노트 드래그 미리 보기 (하늘)")]
    public Color editorPreview = new Color32(0x7D, 0xD3, 0xFC, 0xFF);
    [Tooltip("놓을 자리 미리 보기 채움 불투명도 (하늘 × 15%, 테두리는 하늘 점선)")]
    [Range(0f, 1f)] public float editorPlaceFillAlpha = 0.15f;
    [Tooltip("롱노트 드래그 미리 보기 몸통 채움 불투명도 (하늘 × 22%, 테두리는 하늘 2px)")]
    [Range(0f, 1f)] public float editorPreviewFillAlpha = 0.22f;
    [Tooltip("롱노트 드래그 미리 보기 머리 · 꼬리 끝 막대 불투명도 (하늘 × 55%)")]
    [Range(0f, 1f)] public float editorPreviewPartAlpha = 0.55f;
    [Tooltip("커서가 올라간 노트 흰 테두리 Img_NoteHover (흰색)")]
    public Color editorNoteHover = Color.white;
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

    /// <summary>
    /// 곡 선택의 강조 색 한 곳 (11/25): 난이도 탭 · 채보 머리 · 기록 패널 · 목록 줄(막대 · 바탕) · 고른 난이도 숫자가 모두 이 색을 받는다.
    /// 나만의 채보는 CUSTOM 보라, 공식은 고른 채보의 난이도 색, 고른 채보가 없으면(공식 채보 0개 곡) 흐린 글자색.
    /// </summary>
    public Color GetChartAccent(ChartType type, ChartData chart)
    {
        if (type == ChartType.Custom) return difficultyCustom;
        return chart != null ? GetDifficulty(chart.difficulty) : textMuted;
    }

    /// <summary>
    /// 라인의 노트 색 (11/20): 바깥 라인(0, 마지막)은 noteOuter, 안쪽 라인은 noteInner. 키 칸 마름모도 이 색이다.
    /// </summary>
    public Color NoteColor(int lane) => IsOuterLane(lane) ? noteOuter : noteInner;

    /// <summary>바깥 라인인지 (0 또는 마지막 라인). 순수 함수</summary>
    public static bool IsOuterLane(int lane) => lane <= 0 || lane >= GameConstants.Editor.LaneCount - 1;

    /// <summary>라인의 숏노트 뒤 빛 색 = 라인 색 × noteGlowAlpha</summary>
    public Color NoteGlowColor(int lane) => WithAlpha(NoteColor(lane), noteGlowAlpha);

    /// <summary>
    /// 판정 이펙트 색 C (11/20). PERFECT · GREAT · GOOD만 있고 MISS는 false (이펙트 없음).
    /// </summary>
    public bool TryGetHitFxColor(JudgeType judge, out Color color)
    {
        switch (judge)
        {
            case JudgeType.Perfect: color = hitFxPerfect; return true;
            case JudgeType.Great: color = hitFxGreat; return true;
            case JudgeType.Good: color = hitFxGood; return true;
            default: color = Color.clear; return false;
        }
    }

    /// <summary>판정 이펙트 밝은 색 L = C와 흰색을 mix만큼 섞은 색 (알파 1). 순수 함수</summary>
    public static Color HitFxLight(Color color, float mix)
    {
        Color light = Color.Lerp(color, Color.white, mix);
        light.a = 1f;
        return light;
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

/// <summary>
/// 곡 끝 FC/AP 연출 색 한 벌 (README 2절 표). 흰색 이미지의 Image.color로 입힌다 (ClearBanner).
/// </summary>
[System.Serializable]
public class ClearColors
{
    [Tooltip("시계 (Face · Dashes · 바늘 · 가운데 점 · 퍼지는 링), 알파 80%")]
    public Color clock;
    [Tooltip("개수 캡슐 테두리, 알파 55%")]
    public Color capsuleLine;
    [Tooltip("빛 알갱이 (흰색과 섞어 쓴다)")]
    public Color mote;
    [Tooltip("라인 빛 기둥")]
    public Color pillar;
    [Tooltip("화면 반짝임 (알파는 시간표가 정한다)")]
    public Color flash;
    [Tooltip("개수 라벨 가로 그라데이션 왼쪽")]
    public Color labelStart;
    [Tooltip("개수 라벨 가로 그라데이션 오른쪽")]
    public Color labelEnd;

    private static Color Hex(int rgb, float alpha) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    /// <summary>ALL PERFECT 기본값 (금 → 분홍)</summary>
    public static ClearColors AllPerfectDefault() => new ClearColors
    {
        clock = Hex(0xFFC9A8, 0.8f), capsuleLine = Hex(0xFFAABE, 0.55f), mote = Hex(0xFFE3A3, 1f), pillar = Hex(0xFF8CC8, 0.32f),
        flash = Hex(0xFFF3F9, 1f), labelStart = Hex(0xFFD36B, 1f), labelEnd = Hex(0xFF7AC6, 1f)
    };

    /// <summary>FULL COMBO 기본값 (연두)</summary>
    public static ClearColors FullComboDefault() => new ClearColors
    {
        clock = Hex(0xCFF59A, 0.8f), capsuleLine = Hex(0xAAE66E, 0.55f), mote = Hex(0xE4FF9A, 1f), pillar = Hex(0x96E150, 0.28f),
        flash = Hex(0xF4FFE8, 1f), labelStart = Hex(0xE4FF9A, 1f), labelEnd = Hex(0x8BD13A, 1f)
    };
}
