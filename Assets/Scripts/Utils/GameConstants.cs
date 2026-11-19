using UnityEngine;

/// <summary>
/// 게임 전체에서 쓰는 상수 모음. 문자열·경로·수치를 코드 곳곳에 직접 쓰지 않고 여기서 관리한다.
/// </summary>
public static class GameConstants
{
    /// <summary>Resources.Load 경로 (Assets/Resources 기준, 확장자 제외)</summary>
    public static class ResourcePaths
    {
        /// <summary>전역 매니저 프리팹: Assets/Resources/Prefabs/Manager/Managers_DontDestroy.prefab</summary>
        public const string ManagersPrefab = "Prefabs/Manager/Managers_DontDestroy";
    }

    /// <summary>씬 이름 (Build Profiles에 등록된 씬 파일 이름과 같아야 한다)</summary>
    public static class Scenes
    {
        public const string Title = "TitleScene";
        public const string SongSelect = "SongSelectScene";
        public const string Editor = "EditorScene";
        public const string InGame = "InGameScene";
        public const string Result = "ResultScene";
    }

    /// <summary>인게임 플레이 규칙 (기획서 2-1). 판정 범위 기본값은 잠정치이며 InGameScene 인스펙터에서 조정한다</summary>
    public static class Play
    {
        /// <summary>판정 범위 기본값 (±ms). MISS 범위 안에서 누르면 MISS 판정, 밖이면 빈 타격</summary>
        public const float DefaultPerfectMs = 40f;
        public const float DefaultGreatMs = 80f;
        public const float DefaultGoodMs = 120f;
        public const float DefaultMissMs = 160f;

        /// <summary>
        /// 롱노트를 뗄 때 시작 판정을 그대로 인정하는 범위 ±ms. 이보다 일찍 떼면 끝 MISS, 이보다 오래 누르면 끝 GOOD.
        /// (DJMAX RESPECT 방식. 기획서 2-1의 "끝 판정 범위 = 숏노트와 같은 범위"와 일부러 다르게 간다)
        /// </summary>
        public const float LongReleaseMs = DefaultGoodMs;

        /// <summary>만점</summary>
        public const int MaxScore = 1000000;

        /// <summary>판정별 점수 비율 (%). 정확도 계산에도 같은 값을 쓴다</summary>
        public const int PerfectRatio = 100;
        public const int GreatRatio = 70;
        public const int GoodRatio = 30;
        public const int MissRatio = 0;
        public const int RatioBase = 100;

        /// <summary>랭크 기준 점수 (이상)</summary>
        public const int RankSScore = 950000;
        public const int RankAScore = 900000;
        public const int RankBScore = 800000;
        public const int RankCScore = 700000;
        public const int RankDScore = 600000;

        /// <summary>
        /// 랭크·FC 이미지 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」, Resources 안 경로). 랭크 이미지 이름 = RankSpritePrefix + PlayRank 이름 (Rank_AP, Rank_S ~ Rank_F).
        /// S~F는 542×542 정사각형, AP·FC는 737×477로 가로로 길다.
        /// </summary>
        public const string RankSpriteFolder = "Images/Rank/";
        public const string RankSpritePrefix = "Rank_";
        public const string FullComboSpriteName = "Clear_FC";

        /// <summary>
        /// 기어 판정 글자·FAST/SLOW 이미지 폴더 (Resources 기준, 11/19). 이름 = JudgeSpritePrefix + JudgeType 또는 FastSlow 이름 (Judge_Perfect, Judge_Fast 등).
        /// 색·PERFECT 그라데이션·빛이 이미지 안에 있다.
        /// </summary>
        public const string JudgeSpriteFolder = "Images/InGame/Judge/";
        public const string JudgeSpritePrefix = "Judge_";

        /// <summary>판정 이미지 표시 배율: 이미지를 화면 크기의 2배로 만들었으므로 원본 크기 × 0.5로 그린다</summary>
        public const float JudgeSpriteDisplayScale = 0.5f;

        /// <summary>
        /// 인게임 왼쪽 판정 패널의 판정 이름 이미지 배율 (11/19, 기어 판정 글자와 같은 이미지).
        /// 판정 4개: 이미지 안 글자 높이 약 93px × 0.195 ≈ 18px (바꾸기 전 TMP 카트 고딕 21의 대문자 높이 18.1px).
        /// FAST·SLOW: 이미지 안 글자 높이 약 34px × 0.405 ≈ 13.8px (바꾸기 전 TMP 16의 대문자 높이 13.8px).
        /// </summary>
        public const float PanelJudgeImageScale = 0.195f;
        public const float PanelFastSlowImageScale = 0.405f;

        /// <summary>첫 노트 전 최소 준비 시간 (초)</summary>
        public const double LeadInSeconds = 2.0;

        /// <summary>노래 시작 지연의 최소값 (초). 첫 노트가 늦어도 이만큼은 기다린다</summary>
        public const double MinStartDelaySeconds = 1.0;

        /// <summary>
        /// 시작 메트로놈 (기획서 3-4, CountInRules): −1마디(곡 첫 박 앞 4박)에 클릭을 이만큼 울린다.
        /// 클릭 소리는 에디터 메트로놈 박 소리(ClickSoundFactory.CreateMetronomeBeat)와 같다
        /// </summary>
        public const int CountInBeats = 4;

        /// <summary>시작 메트로놈: 첫 클릭(−4박) 앞에 두는 여유 (초, 곡 시간). 판은 (−4박 시각 − 여유)부터 흐른다</summary>
        public const double CountInMarginSeconds = 0.3;

        /// <summary>
        /// 모든 노트가 판정된 뒤 결과 씬으로 넘어가기까지 기다리는 시간 (초, 곡 시간 기준).
        /// 이 동안 노래는 계속 흐르고, 일시정지는 받지 않는다 (결과로 넘어가는 중)
        /// </summary>
        public const double ResultDelaySeconds = 3.0;

        /// <summary>곡 끝 FC/AP 연출 이미지 폴더 (Resources 안)</summary>
        public const string ClearSpriteFolder = "Images/InGame/Clear/";

        /// <summary>곡 끝 연출 개수 캡슐 라벨 (ALL PERFECT)</summary>
        public const string ClearPerfectLabel = "PERFECT";

        /// <summary>곡 끝 연출 개수 캡슐 라벨 (FULL COMBO)</summary>
        public const string ClearComboLabel = "COMBO";

        /// <summary>곡 끝 연출 개수 캡슐 전체 글자 형식 ("/ 329")</summary>
        public const string ClearTotalFormat = "/ {0}";

        /// <summary>곡 끝 연출 개수 칸의 자릿수당 폭 (px, NEXON Lv2 Gothic Bold 48 숫자 폭). 칸 폭 = 전체 자릿수 × 이 값</summary>
        public const float ClearCountDigitWidth = 29f;

        /// <summary>
        /// 곡 시간(MusicClock)과 실제 음원 재생 위치의 차이가 이보다 크면 진단 경고를 남긴다 (초, 약 2프레임. Unity 에디터 전용 진단)
        /// </summary>
        public const double SyncWarningSeconds = 0.034;

        /// <summary>싱크 진단 경고를 같은 판에서 다시 남기기까지의 간격 (초, realtime)</summary>
        public const double SyncWarningIntervalSeconds = 1.0;

        /// <summary>
        /// 1.0배에서 노트 중심이 화면 위 끝 → 판정선까지 걸리는 시간 (초). DJMAX RESPECT V x5.5 측정 기반 (0.489초 × 5.5).
        /// 배속 s의 보이는 시간 = 2.69 ÷ s, 낙하 속도 = (화면 위 끝 y − 판정선 y) ÷ 2.69 × s (ScrollSpeedRules).
        /// </summary>
        public const float BaseVisibleSeconds = 2.69f;

        /// <summary>
        /// 배속 범위·단위·기본값 (기획서 2-1). 곡 선택·에디터에서 정해 InGameEntryData로 넘기고, 플레이 중에도 1 / 2 키로 바꾼다.
        /// 0.1 단위 오차가 쌓이지 않게 배속 × ScrollMultiplierScale(정수 단계)로 계산한다 (ScrollSpeedRules).
        /// </summary>
        public const float MinScrollMultiplier = 1f;
        public const float MaxScrollMultiplier = 8f;
        public const float ScrollMultiplierStep = 0.1f;
        public const float DefaultScrollMultiplier = 1f;

        /// <summary>배속을 정수로 계산할 때 곱하는 값 (0.1 단위 → 10)</summary>
        public const int ScrollMultiplierScale = 10;

        /// <summary>
        /// 인게임 마디선 굵기 (1080 화면 기준 px, 기획서 3-4 마디선). 월드 굵기 = 이 값 × GearView.UnitsPerReferencePx.
        /// 살짝 보일 정도로 얇게 둔다. 색(불투명도)은 UITheme.measureLine
        /// </summary>
        public const float MeasureLineThickness = 2f;

        /// <summary>인게임 배속 키(1 / 2)를 누르고 있을 때: 이 시간 뒤부터 반복 (초)</summary>
        public const double SpeedKeyRepeatDelaySeconds = 0.4;

        /// <summary>인게임 배속 키 반복 간격 (초)</summary>
        public const double SpeedKeyRepeatIntervalSeconds = 0.08;

        /// <summary>인게임 배속·밝기 카드(Img_SpeedBrightnessCard) 배속 값: {0} 배속 "x5.5" (플레이 중 1 / 2 키로 바꾸면 다시 쓴다)</summary>
        public const string InGameSpeedFormat = "x{0:0.0}";

        /// <summary>인게임 배속·밝기 카드 왼쪽 칸 이름 (위 줄 한글, Txt_SpeedTitle)</summary>
        public const string SpeedCardLabel = "배속";

        /// <summary>인게임 배속·밝기 카드 왼쪽 칸 이름 아래 영문 (Txt_SpeedTitleEn)</summary>
        public const string SpeedCardLabelEn = "SPEED";

        /// <summary>인게임 배속·밝기 카드 오른쪽 칸 이름 (위 줄 한글, Txt_BrightnessTitle)</summary>
        public const string BrightnessCardLabel = "밝기";

        /// <summary>인게임 배속·밝기 카드 오른쪽 칸 이름 아래 영문 (Txt_BrightnessTitleEn)</summary>
        public const string BrightnessCardLabelEn = "BG";

        /// <summary>인게임 배속·밝기 카드 밝기 값: {0} 밝기 % "100%"</summary>
        public const string BrightnessFormat = "{0}%";

        /// <summary>인게임 곡 정보 카드 3줄 BPM 라벨 (값은 SongData.FormatBpm, 지금 곡 시간 위치의 BPM)</summary>
        public const string BpmLabel = "BPM";

        /// <summary>
        /// 인게임 배경 밝기 단계 (%, 기획서 3-4 「배경 · BGA (11/19)」). PgUp 밝게 · PgDn 어둡게 한 단계씩, 끝값에서 더 누르면 그대로.
        /// 배경 위 검은 덮개 알파 = 1 − 밝기 (BackgroundBrightnessRules)
        /// </summary>
        public static readonly int[] BackgroundBrightnessSteps = { 0, 25, 50, 75, 100 };

        /// <summary>인게임 배경 밝기 기본값 (%, 저장값이 없을 때)</summary>
        public const int DefaultBackgroundBrightness = 100;

        /// <summary>배속·밝기 카드 밝기 단계 막대 칸 수 (25%마다 한 칸)</summary>
        public const int BrightnessBarCount = 4;

        /// <summary>배경 밝기 키 이름 (입력 에셋 바인딩에서 읽지 못했을 때 쓴다)</summary>
        public const string BrightnessDownKeyFallback = "PgDn";
        public const string BrightnessUpKeyFallback = "PgUp";

        /// <summary>인게임 곡 정보 카드 배속 키 (입력 에셋 바인딩에서 읽지 못했을 때 쓴다)</summary>
        public const string SpeedDownKeyFallback = "1";
        public const string SpeedUpKeyFallback = "2";

        /// <summary>인게임 곡 정보 패널 레벨: {0} 레벨</summary>
        public const string InGameLevelFormat = "Lv.{0}";

        /// <summary>인게임 곡 정보 패널: 나만의 채보 뱃지 문구 (공식 채보는 난이도 이름을 쓴다)</summary>
        public const string ChartBadgeCustomText = "CUSTOM";


        /// <summary>키 칸 글자 (입력 에셋 바인딩에서 읽지 못했을 때 쓴다). Lane0 ~ Lane3 순서</summary>
        public static readonly string[] LaneKeyLabels = { "A", "S", ";", "'" };

        /// <summary>
        /// 키 칸 크기 (1080 화면 기준 px). 화면 아래 끝에 붙이고, 칸 폭 = 라인 폭 − 양옆 여백 × 2.
        /// 월드 크기는 카메라 높이(orthographicSize × 2)를 KeyCellReferenceHeightPx로 나눠 바꾼다 (해상도가 바뀌어도 같은 비율)
        /// </summary>
        public const float KeyCellHeightPx = 120f;
        public const float KeyCellSidePaddingPx = 4f;
        public const float KeyCellReferenceHeightPx = 1080f;

        /// <summary>
        /// 판정선 두께 (1080 화면 기준 px, 기획서 3-4 「인게임 톤 개편 (11/18)」 판정선 이미지). 세로 중심은 씬의 JudgeLine 위치(y 900) 그대로이고,
        /// 폭은 기어 폭(라인 폭 × 라인 수). GearView가 Init에서 SpriteRenderer.size를 맞춘다.
        /// JudgeLine.png(542×69)의 Pixels Per Unit 310.5는 이 값 24px에 이미지 세로 69px가 맞게 정한 값이다 (좌우 9-slice 테두리가 위아래 테두리와 같은 굵기로 보이게).
        /// </summary>
        public const float JudgeLineThicknessPx = 24f;

        /// <summary>테스트 플레이: 시작 위치 이만큼(초) 앞의 음악부터 들려준다 (첫 노트 전 준비 시간)</summary>
        public const double TestPlayLeadInSeconds = 2.0;

        /// <summary>음원 중간부터 예약 재생할 때 두는 최소 지연 (초). 오디오 버퍼보다 길어야 시작이 늦지 않는다</summary>
        public const double ScheduleLatencySeconds = 0.1;

        /// <summary>테스트 플레이 표시와 결과 화면 돌아가기 버튼 문구</summary>
        public const string TestPlayLabel = "테스트 플레이";
        public const string AutoTestPlayLabel = "테스트 플레이 · 자동 플레이";

        /// <summary>인게임 개발용 F1 패널 상태 한 줄 ({0} 빛기둥, {1} 키음에 켬·끔이 들어간다)</summary>
        public const string DebugStatusFormat = "빛기둥 {0} · 키음 {1}";
        public const string DebugOnLabel = "켬";
        public const string DebugOffLabel = "끔";

        /// <summary>자동 플레이가 노트를 칠 때 라인 빛남을 켜 두는 시간 (초, realtime)</summary>
        public const double AutoPlayGlowSeconds = 0.08;
        public const string ReturnToEditorLabel = "에디터로 돌아가기";
        public const string ReturnToSongSelectLabel = "곡 선택으로";
        public const string RetryLabel = "다시 하기";

        /// <summary>판정 글자</summary>
        public const string PerfectText = "PERFECT";
        public const string GreatText = "GREAT";
        public const string GoodText = "GOOD";
        public const string MissText = "MISS";

        /// <summary>FAST / SLOW 글자 (판정 글자 위, 판정 패널 라벨). GREAT · GOOD에서만 쓴다</summary>
        public const string FastText = "FAST";
        public const string SlowText = "SLOW";

        public const string FullComboText = "FULL COMBO";
        public const string AllPerfectText = "ALL PERFECT";

        /// <summary>인게임 콤보 숫자 위 제목 (Txt_ComboTitle)</summary>
        public const string ComboTitleText = "COMBO";

        /// <summary>인게임 판정 패널 맨 아래 박스 라벨 (정확도 표기 이름 RATE, 곡 선택·결과와 같음)</summary>
        public const string RateTitleText = "RATE";
        public const string ScoreTitleText = "SCORE";

        /// <summary>결과 화면 신기록 표시 (기획서 3-5 ③, SCORE · RATE · MAX COMBO 각각 옆)</summary>
        public const string NewRecordText = "NEW RECORD";

        /// <summary>
        /// 에디터 노트의 라인별 색 (A · S · ; · ' 순서, Img_ShortNote·Img_LongNote). 인게임 노트 색은 UITheme 인게임 묶음.
        /// </summary>
        public static readonly Color32[] LaneColors =
        {
            new Color32(0x6E, 0xC8, 0xF5, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            new Color32(0x6E, 0xC8, 0xF5, 0xFF)
        };
    }

    /// <summary>타이틀 화면 (기획서 3-2, 11/17 개편)</summary>
    public static class Title
    {
        /// <summary>메뉴 버튼의 영문 작은 글씨 (한글 이름 옆)</summary>
        public const string PlayEn = "PLAY";
        public const string EditorEn = "EDITOR";
        public const string SettingsEn = "SETTINGS";
        public const string QuitEn = "EXIT";

        /// <summary>로고 "KAIROS" 글자 간격 (TMP Character Spacing, 와이어프레임 폭에 맞춤)</summary>
        public const float LogoCharacterSpacing = 15f;

        /// <summary>메뉴 영문 글자 간격 (TMP Character Spacing)</summary>
        public const float MenuEnCharacterSpacing = 20f;
    }

    /// <summary>
    /// 선택 강조 (UISelectHighlight) 색. 선택된 버튼: 하늘색 반투명 바탕 + 왼쪽 포인트색 막대 + 흰 Bold 한글 + 포인트색 영문.
    /// 선택 안 된 버튼: 바탕·막대 없이 흐린 흰 글자만.
    /// </summary>
    public static class SelectHighlight
    {
        /// <summary>포인트색 하늘색 #7CC4FA (캐릭터 디자인 포인트색)</summary>
        public static readonly Color32 AccentColor = new Color32(0x7C, 0xC4, 0xFA, 0xFF);

        /// <summary>선택된 버튼 바탕 불투명도 (포인트색 위에 곱함)</summary>
        public const float SelectedBackgroundAlpha = 0.18f;

        /// <summary>선택 안 된 버튼의 한글·영문 글자 불투명도 (흰색)</summary>
        public const float NormalMainTextAlpha = 0.7f;
        public const float NormalSubTextAlpha = 0.45f;

        /// <summary>왼쪽 막대 폭 (px)</summary>
        public const float AccentBarWidth = 4f;
    }

    /// <summary>UI 공통 값 (UIManager, UIBase)</summary>
    public static class UI
    {
        /// <summary>열고 닫는 연출 시간 (초, unscaled)</summary>
        public const float TransitionSeconds = 0.15f;

        /// <summary>열 때 패널 시작 크기 (→ 1.0)</summary>
        public const float TransitionStartScale = 0.9f;

        /// <summary>오버레이 Canvas의 Sort Order (씬 Canvas는 0)</summary>
        public const int OverlaySortOrder = 100;
    }

    /// <summary>
    /// 팝업 공통 톤 크기 (기획서 3-7 「팝업 공통 톤 (11/18)」). 색은 UITheme 팝업 묶음에 둔다.
    /// 크기는 1920×1080 기준 px.
    /// </summary>
    public static class Popup
    {
        /// <summary>뒤 화면 블러 세기 (기준 해상도 px, 가우시안 표준편차). 채도·밝기 보정은 하지 않는다 (100%)</summary>
        public const float BlurRadiusPx = 6f;

        /// <summary>블러 반복 횟수 (가로·세로 한 쌍). 곡 선택 배경(BlurIterations 2)보다 약하게 한 번</summary>
        public const int BlurIterations = 1;

        /// <summary>패널 모서리 · 테두리 두께</summary>
        public const float PanelRadius = 20f;
        public const float PanelBorder = 1f;

        /// <summary>패널 그림자: 아래로 24 · 퍼짐 64 (부드러운 가장자리 스프라이트 PopupShadow를 패널 뒤에 둔다)</summary>
        public const float ShadowOffsetY = 24f;
        public const float ShadowSpread = 64f;

        /// <summary>제목: 한글 30 (NEXON Lv2 Gothic Bold) + 영문 13 (포인트색) + 짧은 선 56×3 (포인트색)</summary>
        public const float TitleFontSize = 30f;
        public const float TitleEnFontSize = 13f;
        public const float TitleEnCharacterSpacing = 15f;
        public const float TitleLineWidth = 56f;
        public const float TitleLineHeight = 3f;

        /// <summary>버튼 높이 · 모서리 · 글자</summary>
        public const float ButtonHeight = 56f;
        public const float ButtonRadius = 12f;
        public const float ButtonFontSize = 20f;

        /// <summary>버튼 선택 링: 안쪽 흰 링 3 + 바깥 포인트색 링 3 (버튼 바깥에 그린다)</summary>
        public const float RingWidth = 3f;

        /// <summary>메뉴·항목 선택: 왼쪽 막대 폭</summary>
        public const float SelectBarWidth = 4f;
    }

    /// <summary>
    /// 확인 팝업 문구 (기획서 3-7 ② 사용처 표). 화면 스크립트에 문구를 직접 쓰지 않고 여기 모은다.
    /// - 사용처마다 위험 표시(…IsDanger)를 둔다: true면 확인 버튼·선택 링·"!" 원이 위험 빨강 (나가기·삭제). 호출하는 쪽은 이 값을 Confirm의 isDanger로 넘긴다.
    /// - 문구에 보조 문구가 필요하면 본문과 SubSeparator로 나눠 쓴다 (Popup_Confirm이 나눈다). 지금 문구는 모두 본문만.
    /// </summary>
    public static class Confirm
    {
        /// <summary>취소 버튼 기본 이름</summary>
        public const string CancelLabel = "취소";

        /// <summary>본문과 보조 문구를 나누는 글자 (이 글자 뒤는 보조 문구: 15, 흐린 글자). 없으면 본문만</summary>
        public const char SubSeparator = '|';

        /// <summary>문구 크기: 본문 21 굵게 (1~3줄) · 보조 15</summary>
        public const float MessageFontSize = 21f;
        public const float SubMessageFontSize = 15f;

        /// <summary>팝업 폭 · "!" 원 지름</summary>
        public const float Width = 560f;
        public const float IconSize = 44f;
        public const string IconText = "!";

        /// <summary>위험 표시 (게임 종료 · 나가기 · 삭제만 true)</summary>
        public const bool QuitIsDanger = true;
        public const bool OfficialOverwriteIsDanger = false;
        public const bool EditorExitIsDanger = true;
        public const bool RestartIsDanger = false;
        public const bool InGameExitIsDanger = true;
        public const bool OffsetRestartIsDanger = false;
        public const bool BpmSectionRemoveIsDanger = true;
        public const bool BpmResetIsDanger = false;

        /// <summary>타이틀 게임 종료</summary>
        public const string QuitMessage = "게임을 종료하시겠습니까?";
        public const string QuitLabel = "종료하기";

        /// <summary>F1 패널 공식 채보로 추가: 그 난이도 파일이 이미 있을 때 ({0} 곡 제목, {1} 난이도)</summary>
        public const string OfficialOverwriteMessageFormat = "{0} {1} 공식 채보가 이미 있습니다.\n덮어쓰시겠습니까?";
        public const string OfficialOverwriteLabel = "덮어쓰기";

        /// <summary>에디터 나가기 (저장하지 않은 변경이 있을 때만)</summary>
        public const string EditorExitMessage = "저장하지 않은 변경이 있습니다.\n저장하지 않고 나가시겠습니까?";
        public const string EditorExitLabel = "나가기";

        /// <summary>인게임 재시작 (일시정지 메뉴)</summary>
        public const string RestartMessage = "처음부터 다시 시작하시겠습니까?";
        public const string RestartLabel = "재시작";

        /// <summary>인게임 나가기 (일시정지 메뉴, 보통 플레이만. 테스트 플레이는 확인 없이 에디터로)</summary>
        public const string InGameExitMessage = "플레이를 그만두고 나가시겠습니까?\n이번 기록은 저장되지 않습니다.";
        public const string InGameExitLabel = "나가기";

        /// <summary>일시정지 중 설정 창에서 판정 오프셋을 바꾸고 닫았을 때 (확인하면 바로 재시작, 취소하면 다음 판부터 적용)</summary>
        public const string OffsetRestart = "판정 오프셋이 바뀌었습니다.\n지금 재시작해야 이번 판에 적용됩니다.\n재시작하시겠습니까?";
        public const string OffsetRestartLabel = "재시작";

        /// <summary>에디터 BPM 구간 삭제 (그 구간 뒤에 노트가 있을 때만): {0} 시간이 바뀌는 노트 수</summary>
        public const string BpmSectionRemoveMessageFormat = "이 BPM 구간을 삭제하시겠습니까?\n뒤에 있는 노트 {0}개의 시간이 바뀝니다.";

        /// <summary>BPM 구간 삭제로 앞뒤 BPM이 같아질 때 덧붙이는 줄</summary>
        public const string BpmSectionRemoveMergedLine = "\n같은 BPM이 된 다음 구간도 함께 삭제됩니다.";
        public const string BpmSectionRemoveLabel = "삭제";

        /// <summary>에디터 곡 기본 BPM으로 되돌리기: {0} 곡 기본 BPM</summary>
        public const string BpmResetMessageFormat = "모든 BPM 구간을 삭제하고 곡의 기본 BPM({0})으로 되돌리시겠습니까?";
        public const string BpmResetLabel = "되돌리기";
    }

    /// <summary>인게임 일시정지 메뉴 (기획서 3-7 ③, 2-1 재개 3초 카운트다운)</summary>
    public static class Pause
    {
        /// <summary>팝업 제목 (한글 + 영문)</summary>
        public const string Title = "일시정지";
        public const string TitleEn = "PAUSE";

        /// <summary>테스트 플레이 뱃지 (제목 오른쪽, 포인트색)</summary>
        public const string TestPlayBadge = "테스트 플레이";

        /// <summary>메뉴 이름 (위에서부터 계속 · 재시작 · 설정 · 나가기): 한글 22 + 영문 12</summary>
        public const string ResumeLabel = "계속";
        public const string RestartLabel = "재시작";
        public const string SettingsLabel = "설정";
        public const string ExitLabel = "나가기";
        public const string ResumeLabelEn = "RESUME";
        public const string RestartLabelEn = "RESTART";
        public const string SettingsLabelEn = "SETTINGS";
        public const string ExitLabelEn = "EXIT";

        /// <summary>메뉴 오른쪽 안내: 계속 "3초 후 재개", 나가기는 가는 곳</summary>
        public const string ResumeHint = "3초 후 재개";
        public const string ExitHintSongSelect = "곡 선택으로";
        public const string ExitHintEditor = "에디터로";

        /// <summary>진행 시간 "1:04 / 2:31" ({0} 지금, {1} 곡 길이, 둘 다 m:ss)</summary>
        public const string ProgressFormat = "{0} / {1}";

        /// <summary>
        /// 팝업 폭 · 위치: 패널을 화면 한가운데(가로 x 960 = 기어 가운데, 세로 y 540)에 둔다 (씬에서 가운데 앵커·피벗, 11/19).
        /// 폭 600: 아래 키 안내 줄(↑↓ 이동 · Enter 선택 · F5 다시 하기 · ESC 계속)이 안쪽 여백(좌우 32) 안에 들어가는 폭.
        /// 곡 카드·메뉴는 패널 안쪽 폭을 따라 늘어난다 (씬에서 좌우 늘이기 앵커).
        /// </summary>
        public const float Width = 600f;

        /// <summary>메뉴 줄 높이 · 간격</summary>
        public const float MenuHeight = 60f;
        public const float MenuSpacing = 14f;

        /// <summary>카운트다운 숫자 크기</summary>
        public const float CountdownFontSize = 180f;

        /// <summary>패널 안쪽 좌우 여백 (곡 카드·메뉴·키 안내 줄이 이 안에 들어간다)</summary>
        public const float PanelPadding = 32f;

        /// <summary>재개 카운트다운 (초, unscaled). 1초마다 숫자 하나씩 3 → 2 → 1</summary>
        public const int CountdownSeconds = 3;
    }

    /// <summary>
    /// 화면 아래쪽 키 안내 줄(KeyGuideBar) 문구 (기획서 3-8 조작 키 표). 키 이름은 바인딩에서 읽고 여기에는 짧은 이름만 둔다.
    /// 고정 글자는 바인딩 하나로 이름을 읽기 어려운 키(방향키 묶음, UI 맵의 Submit·Cancel)에만 쓴다.
    /// </summary>
    public static class KeyGuide
    {
        /// <summary>항목 틀 안 오브젝트 이름 (KeyGuideBar가 찾는다)</summary>
        public const string KeyCapName = "Img_KeyCap";
        public const string KeyLabelName = "Txt_KeyLabel";

        /// <summary>고정 글자 키</summary>
        public const string UpDownKeys = "↑↓";
        public const string LeftRightKeys = "←→";
        public const string EnterKey = "Enter";
        public const string EscKey = "ESC";

        /// <summary>짧은 이름</summary>
        public const string Move = "이동";
        public const string Select = "선택";
        public const string Quit = "종료";
        public const string Song = "곡";
        public const string Difficulty = "난이도";
        public const string Chart = "채보";
        public const string Tab = "탭";
        public const string Speed = "배속";
        public const string Start = "시작";
        public const string ToTitle = "타이틀";
        public const string Resume = "계속";
        public const string Retry = "다시 하기";
        public const string ToSongSelect = "곡 선택으로";
        public const string ToEditor = "에디터로";
        public const string PrevNextSong = "이전 · 다음 곡";
        public const string TabSwitch = "공식 ↔ 나만의 채보";

        /// <summary>나만의 채보 탭 키 안내 (↑↓ 채보 · 새 채보 줄 · ←→ 이전 · 다음 곡 묶음 · E 편집)</summary>
        public const string MyChartVertical = "채보 · 새 채보 줄";
        public const string PrevNextSongGroup = "이전 · 다음 곡 묶음";
        public const string Edit = "편집";
        public const string EditKeyFallback = "E";
        public const string StartId = "start";
        public const string EditId = "edit";

        /// <summary>새 채보 팝업 키 안내 (↑↓ 항목 · ←→ 레벨 · 버튼 · Enter 결정 · ESC 취소)</summary>
        public const string NewChartItem = "항목";
        public const string NewChartLevelOrButton = "레벨 · 버튼";

        /// <summary>확인 팝업 키 안내 (←→ 선택 · Enter 결정 · ESC 취소)</summary>
        public const string ConfirmSelect = "선택";
        public const string ConfirmDecide = "결정";
        public const string ConfirmCancel = "취소";

        /// <summary>설정 창 키 안내 (↑↓ 항목 · ←→ 값 조절 · Tab 탭 전환 · ESC 닫기 (자동 저장))</summary>
        public const string SettingsItem = "항목";
        public const string SettingsValue = "값 조절";
        public const string SettingsTab = "탭 전환";
        public const string SettingsClose = "닫기 (자동 저장)";
        public const string TabKeyFallback = "Tab";

        /// <summary>이름을 바꾸는 항목의 ID (KeyGuideBar.SetLabel)</summary>
        public const string ChartMoveId = "chartMove";
        public const string VerticalMoveId = "verticalMove";
        public const string ReturnId = "return";
        public const string BackId = "back";
    }

    /// <summary>설정 창(Popup_Settings) 문구·값 범위 (기획서 3-7, 2-4). 저장은 PlayerData.json (GameConstants.PlayerData)</summary>
    public static class Settings
    {
        public const string Title = "설정";
        public const string TitleEn = "SETTINGS";
        public const string CloseLabel = "×";
        public const string TabSound = "사운드";
        public const string TabGame = "게임";
        public const string TabScreen = "화면";
        public const string TabSoundEn = "SOUND";
        public const string TabGameEn = "GAME";
        public const string TabScreenEn = "SCREEN";

        /// <summary>창 크기 (화면 가운데, 탭을 바꿔도 그대로) · 항목 줄 높이 · 간격 · 드롭다운 폭 · 닫기 버튼 (1920×1080 기준 px)</summary>
        public const float Width = 960f;
        public const float Height = 540f;
        public const float RowHeight = 84f;
        public const float RowSpacing = 10f;
        public const float DropdownWidth = 300f;
        public const float CloseButtonSize = 44f;

        /// <summary>음량 ◀▶ 버튼 한 번에 바뀌는 값 (마우스 전용, 키보드 ←→는 슬라이더 기본 1씩)</summary>
        public const int VolumeStep = 5;

        public const string MasterVolumeLabel = "메인 음량";
        public const string MusicVolumeLabel = "음악 음량";
        public const string SfxVolumeLabel = "효과음 음량";

        public const string OffsetLabel = "판정 오프셋";
        public const string OffsetRangeHint = "-200 ~ +200ms, 5ms 단위";
        public const string MeasureOffsetLabel = "오프셋 측정하기";
        public const string MeasureOffsetRowLabel = "오프셋 측정";
        public const string MeasureOffsetDescription = "박자에 맞춰 키를 눌러 평균 오차 표시";

        public const string ResolutionLabel = "해상도";
        public const string WindowModeLabel = "창 모드";

        /// <summary>◀ ▶ 버튼 글자 (폰트에 있는 글자만 쓴다. U+2212 −는 폰트에 없어 ASCII -를 쓴다)</summary>
        public const string StepDownLabel = "◀";
        public const string StepUpLabel = "▶";

        /// <summary>
        /// 아래 키 안내 줄 전체 문구 (KeyGuideBar 항목을 " · "로 이은 것과 같다: 키 모양 + 짧은 이름 KeyGuide.SettingsItem 등).
        /// Tab 키 이름은 입력 에셋 바인딩(Settings/NextTab)에서 읽는다.
        /// </summary>
        public const string HintText = "↑↓ 항목 · ←→ 값 조절 · Tab 탭 전환 · ESC 닫기 (자동 저장)";

        /// <summary>음량 범위 (정수)와 처음 값</summary>
        public const int MinVolume = 0;
        public const int MaxVolume = 100;
        public const int DefaultVolume = 100;

        /// <summary>
        /// 판정 오프셋 범위·단위 (ms)와 표시 형식: 양수 "+15 ms", 0 "0 ms", 음수 "-15 ms".
        /// 부호: + = 노트를 늦게 (소리가 늦게 들리거나 늦게 칠 때), - = 노트를 빠르게. 인게임 노트 시간 전체(화면·판정·롱노트 틱·MISS)를 옮긴다.
        /// </summary>
        public const int MinOffsetMs = -200;
        public const int MaxOffsetMs = 200;
        public const int OffsetStepMs = 5;
        public const string OffsetPositiveFormat = "+{0} ms";
        public const string OffsetZeroText = "0 ms";
        public const string OffsetNegativeFormat = "-{0} ms";

        /// <summary>해상도 선택지와 기본값. 설정 창은 창 모드일 때 모니터보다 큰 항목을 빼고 보여준다</summary>
        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440)
        };
        public static readonly Vector2Int DefaultResolution = new Vector2Int(1920, 1080);
        public const string ResolutionFormat = "{0} × {1}";

        /// <summary>창 모드 선택지 (FullScreenMode 순서와 함께 쓴다)와 표시 이름</summary>
        public static readonly FullScreenMode[] WindowModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow
        };
        public static readonly string[] WindowModeLabels = { "전체 화면", "창 모드", "테두리 없는 창" };

        /// <summary>저장 파일의 창 모드가 목록에 없을 때 쓰는 값 (Player Settings 기본값과 같은 테두리 없는 창)</summary>
        public const FullScreenMode DefaultWindowMode = FullScreenMode.FullScreenWindow;
    }

    /// <summary>화면 프레임 설정 (FrameRateRules)</summary>
    public static class Display
    {
        /// <summary>
        /// 모니터 주사율이 이 값 미만이면 VSync를 끄고 이 프레임을 목표로 한다 (이상이면 VSync).
        /// 최소 프레임은 보장할 수 없고 상한만 정할 수 있다. VSync가 켜져 있으면 targetFrameRate는 무시된다.
        /// </summary>
        public const int MinTargetFrameRate = 60;
    }

    /// <summary>유저 플레이 데이터 파일 (기획서 2-4): 설정 + 공식·나만의 채보 기록</summary>
    public static class PlayerData
    {
        /// <summary>저장 파일 이름 (persistentDataPath 아래)</summary>
        public const string FileName = "PlayerData.json";

        /// <summary>지금 쓰는 파일 형식 버전. 형식이 바뀌면 올리고 불러올 때 변환한다</summary>
        public const int CurrentVersion = 1;

        /// <summary>직전 파일 백업 (PlayerData.json.bak)과 쓰는 중인 임시 파일 (PlayerData.json.tmp)</summary>
        public const string BackupSuffix = ".bak";
        public const string TempSuffix = ".tmp";

        /// <summary>읽지 못한 파일을 옮겨 둘 이름: {0} 시각 (yyyyMMdd_HHmmss)</summary>
        public const string BrokenFileFormat = "PlayerData_broken_{0}.json";
        public const string BrokenTimeFormat = "yyyyMMdd_HHmmss";

        /// <summary>기록 시각 형식 (UTC, ISO 8601)</summary>
        public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    }

    /// <summary>결과 화면 표시 문구 (기획서 3-5 결과 11/17). 색은 UITheme(메뉴 화면 톤)에 둔다</summary>
    public static class Result
    {
        /// <summary>화면 이름 (곡 선택 "SONG SELECT"와 같은 형식)</summary>
        public const string ScreenName = "RESULT";
        public const string ScreenNameKo = "결과";

        public const string ScoreLabel = "SCORE";
        public const string RateLabel = "RATE";
        public const string MaxComboLabel = "MAX COMBO";

        /// <summary>이전 최고 점수 줄: {0} 이전 최고 점수 (쉼표 없음). 뒤에 차이를 붙인다</summary>
        public const string BestFormat = "BEST {0}";

        /// <summary>점수 차이 부호 (게임 글꼴에 U+2212가 없어 "-")</summary>
        public const string DiffPlus = "+";
        public const string DiffMinus = "-";
        public const string DiffZero = "±0";

        /// <summary>최대 콤보 전체: {0} = 노트 수 + 롱노트 유지 틱 수</summary>
        public const string MaxComboTotalFormat = "/ {0}";

        /// <summary>
        /// 판정 이름 이미지 배율 (11/19, 인게임 판정 패널과 같은 이미지 JudgeSprites). 결과 화면 글자가 인게임보다 작아 따로 둔다.
        /// 판정 4개: 이미지 안 글자 높이 약 93px × 0.176 ≈ 16.4px (바꾸기 전 카트 고딕 19의 대문자 높이 16.4px).
        /// FAST·SLOW: 약 34px × 0.356 ≈ 12.1px (바꾸기 전 카트 고딕 14의 대문자 높이 12.1px).
        /// </summary>
        public const float JudgeImageScale = 0.176f;
        public const float FastSlowImageScale = 0.356f;

        /// <summary>곡 정보 셋째 줄 배속: {0} 배속</summary>
        public const string SpeedFormat = "· x{0:0.0}";

        /// <summary>나만의 채보 이름이 이 폭(px)을 넘으면 줄임표 (뱃지·Lv·배속은 항상 보이게)</summary>
        public const float MaxChartNameWidth = 520f;
    }

    /// <summary>곡 선택 화면 표시 문구·값 (기획서 3-3 곡 선택 화면 개편 11/17 · 나만의 채보 탭 11/18 개정 · 3-7 ④ 새 채보 팝업). 색은 여기 두지 않고 UITheme(메뉴 화면 톤)에 둔다</summary>
    public static class SongSelect
    {
        /// <summary>
        /// 값이 없을 때 표시 (최고 기록·랭크, 없는 난이도의 레벨). 긴 줄표 모양이지만 게임 글꼴(NEXON Lv2 Gothic)에
        /// U+2014(—)가 없어 같은 모양의 U+2015(―)를 쓴다.
        /// </summary>
        public const string EmptyValue = "―";

        /// <summary>난이도 카드 레벨: {0} 레벨</summary>
        public const string LevelFormat = "Lv. {0}";

        /// <summary>곡 정보 줄: {0} BPM, {1} 곡 길이</summary>
        public const string SongInfoFormat = "BPM {0} · {1}";

        /// <summary>배속 표시: {0} 배속 (소수 첫째 자리까지)</summary>
        public const string SpeedFormat = "x{0:0.0}";

        /// <summary>화면 이름 (타이틀 로고와 같은 형식)</summary>
        public const string ScreenName = "SONG SELECT";
        public const string ScreenNameKo = "곡 선택";
        /// <summary>화면 이름 글자 간격 (TMP characterSpacing, 0.01em 단위)</summary>
        public const float ScreenNameCharacterSpacing = 15f;

        /// <summary>탭 이름 (한글 + 영문 작은 글씨)</summary>
        public const string TabOfficial = "공식 채보";
        public const string TabOfficialEn = "OFFICIAL";
        public const string TabMyChart = "나만의 채보";
        public const string TabMyChartEn = "MY CHARTS";

        /// <summary>구역 머리글</summary>
        public const string DifficultyHeader = "DIFFICULTY · 난이도";
        public const string MyChartHeader = "MY CHART · 고른 채보";
        /// <summary>+ 줄을 골랐을 때 고른 채보 자리 머리글</summary>
        public const string NewChartHeader = "NEW CHART · 새 채보";
        public const string SongListHeader = "SONGS · 곡 목록";
        public const string MyChartListHeader = "MY CHARTS · 곡별 나만의 채보";
        public const string SpeedLabel = "SPEED · 배속";

        /// <summary>목록 위쪽 숫자: {0} 지금 순번, {1} 전체 수 (두 자리)</summary>
        public const string SongCountFormat = "{0:00} / {1:00}";
        /// <summary>나만의 채보 탭 위쪽 숫자: {0} 지금 고른 곡 순번, {1} 전체 곡 수, {2} 전체 채보 수 (예: "곡 02 / 08 · 채보 5")</summary>
        public const string MyChartCountFormat = "곡 {0:00} / {1:00} · 채보 {2}";

        /// <summary>나만의 채보 곡 머리 줄 채보 수: {0} 채보 수 (1이면 단수, 0이면 NO CHART)</summary>
        public const string GroupChartCountFormat = "{0} CHARTS";
        public const string GroupChartCountOne = "1 CHART";
        public const string GroupNoChart = "NO CHART";

        /// <summary>"+ 새 채보 만들기" 줄 (Btn_NewChart): 이름 · 고르면 오른쪽 Enter 키 힌트</summary>
        public const string NewChartRowLabel = "새 채보 만들기";
        public const string NewChartRowPlus = "+";
        public const string NewChartRowHint = "이름 · 레벨 입력";

        /// <summary>+ 줄을 골랐을 때 왼쪽 점선 카드 (고른 채보 카드 자리)</summary>
        public const string NewChartCardTitle = "이 곡으로 새 채보를 만들어요";
        public const string NewChartCardHint = "Enter를 누르면 이름·레벨을 넣는 창이 열려요";

        /// <summary>+ 줄을 골랐을 때 최고 기록 카드 불투명도 (값은 비우고 흐리게)</summary>
        public const float NewChartRecordAlpha = 0.35f;

        /// <summary>목록 항목의 난이도 레벨 표시: {0} 난이도 첫 글자, {1} 레벨 (예: "E 3")</summary>
        public const string ListLevelFormat = "{0} {1}";

        /// <summary>나만의 채보 항목·카드의 레벨: {0} 레벨</summary>
        public const string MyChartLevelFormat = "Lv. {0}";

        /// <summary>고른 채보 카드 아랫줄: {0} 노트 수, {1} 롱노트 수, {2} 마지막 수정일 (MM/dd)</summary>
        public const string MyChartStatsFormat = "노트 <b>{0}</b>    롱노트 <b>{1}</b>    마지막 수정 <b>{2}</b>";
        public const string ModifiedDateFormat = "MM/dd";

        /// <summary>최고 기록 카드 이름</summary>
        public const string BestScoreLabel = "BEST SCORE";
        public const string RateLabel = "RATE";
        public const string ComboLabel = "COMBO";

        /// <summary>RATE 표시: 소수 둘째 자리 % (반올림하지 않고 버림, InvariantCulture)</summary>
        public const string RateFormat = "{0:0.00}%";

        /// <summary>시작 버튼 (하단 바 오른쪽 끝). + 줄을 고르면 "새 채보 / NEW"</summary>
        public const string StartLabel = "시작";
        public const string StartLabelEn = "START";
        public const string NewChartStartLabel = "새 채보";
        public const string NewChartStartLabelEn = "NEW";

        /// <summary>빈 상태 문구</summary>
        public const string OfficialEmpty = "공식 채보가 없습니다";

        /// <summary>새 채보 팝업 (Popup_NewChart, 기획서 3-7 ④)</summary>
        public const string NewChartTitle = "새 채보 만들기";
        public const string NewChartTitleEn = "NEW CHART";
        public const string NewChartNameLabel = "채보 이름";
        public const string NewChartLevelLabelFormat = "레벨 ({0}~{1})";
        public const string NewChartDefaultNameFormat = "새 채보 {0}";
        public const string NewChartNameEmpty = "채보 이름을 입력해 주세요";
        public const string NewChartCancelLabel = "취소";
        public const string NewChartCreateLabel = "만들기";
        /// <summary>곡 카드 둘째 줄: {0} 아티스트, {1} BPM, {2} 곡 길이</summary>
        public const string NewChartSongInfoFormat = "{0} · BPM {1} · {2}";
        /// <summary>처음 레벨</summary>
        public const int NewChartDefaultLevel = 7;
        /// <summary>팝업 폭 · 입력칸 높이 · 오류 테두리 두께</summary>
        public const float NewChartWidth = 600f;
        public const float NewChartInputHeight = 52f;

        /// <summary>곡 배경 크로스페이드 시간 (초, 실제 시간)</summary>
        public const float BackgroundFadeSeconds = 0.25f;

        /// <summary>블러 처리 해상도 = 입력 ÷ 이 값</summary>
        public const int BlurDownsample = 4;

        /// <summary>블러 반복 횟수 (가로 1회 + 세로 1회가 한 번)</summary>
        public const int BlurIterations = 2;

        /// <summary>블러 결과 기준 화면 비율 (16:9). 다른 비율 입력은 가운데를 잘라 맞춘다</summary>
        public const float BlurAspect = 16f / 9f;
    }

    /// <summary>채보 에디터 규칙</summary>
    public static class Editor
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 15;

        /// <summary>1마디의 박 수 (4/4 고정)</summary>
        public const int BeatsPerMeasure = 4;

        /// <summary>라인 수 (4키)</summary>
        public const int LaneCount = 4;

        /// <summary>편집 중 위치 단위. 1박 = 48틱 (모든 스냅 선택지가 정수로 나누어떨어진다)</summary>
        public const int TicksPerBeat = 48;

        /// <summary>
        /// 박자 스냅 선택지. Division n은 1박을 n칸으로 나눈다 (스냅 한 칸 = TicksPerBeat ÷ n 틱).
        /// 드롭다운 옵션은 이 순서와 표시 이름으로 만든다.
        /// </summary>
        public static readonly SnapOption[] SnapOptions =
        {
            new SnapOption(1, "4분음표 · 1박 1칸"),
            new SnapOption(2, "8분음표 · 1박 2칸"),
            new SnapOption(3, "셋잇단 8분 · 1박 3칸"),
            new SnapOption(4, "16분음표 · 1박 4칸"),
            new SnapOption(6, "셋잇단 16분 · 1박 6칸"),
            new SnapOption(8, "32분음표 · 1박 8칸"),
            new SnapOption(12, "셋잇단 32분 · 1박 12칸"),
            new SnapOption(16, "64분음표 · 1박 16칸"),
        };

        /// <summary>박자 스냅 기본값 (n = 4, 16분음표)</summary>
        public const int DefaultSnapDivision = 4;

        /// <summary>
        /// 그리드 높이 x1.0일 때 1초의 높이 (1920×1080 기준 px). 1박 높이는 곡 BPM에 따라 달라진다
        /// (1박 = PixelsPerSecond × 60 ÷ BPM, 150.61 BPM이면 약 99.6px). 인게임처럼 시간 기준으로 밀도를 보여준다.
        /// </summary>
        public const float PixelsPerSecond = 250f;

        /// <summary>지금 스냅의 선 간격이 이보다 좁으면(px) "선 간격이 좁습니다" 안내를 보여준다</summary>
        public const float MinSnapLineSpacingPixels = 6f;
        public const string DenseGridHint = "선 간격이 좁습니다 · Ctrl+휠로 넓히기";

        /// <summary>그리드 높이 배율 범위와 Ctrl+휠 한 칸의 변화량</summary>
        public const float MinGridHeight = 0.5f;
        public const float MaxGridHeight = 4f;
        public const float GridHeightStep = 0.25f;
        public const float DefaultGridHeight = 1f;

        /// <summary>화면 이름 (상단 바 왼쪽, 다른 화면과 같은 형식: 한글 + 하늘색 영문)</summary>
        public const string ScreenTitle = "에디터";
        public const string ScreenTitleEn = "EDITOR";

        /// <summary>상단 바 곡 정보 둘째 줄 레벨: {0} 레벨</summary>
        public const string LevelFormat = "Lv. {0}";

        /// <summary>타임라인 정보 뒤쪽 (현재 시간은 Bold 따로): {0} 곡 길이, {1} 마디 번호</summary>
        public const string TimeTotalFormat = "/ {0} · 마디 {1}";

        /// <summary>저장 상태 표시. Saved의 {0}에 저장 시각(SaveTimeFormat)이 들어간다</summary>
        public const string SaveStatusSaved = "저장됨 {0}";
        public const string SaveStatusFailed = "저장 실패";
        public const string SaveStatusUnsaved = "● 저장 안 됨";
        public const string SaveTimeFormat = "HH:mm";

        /// <summary>테스트 플레이를 시작할 수 없을 때 안내. 이 시간(초)만 보여주고 저장 상태 표시로 되돌린다</summary>
        public const string TestPlayNoNotesMessage = "노트가 없습니다 · 노트를 찍은 뒤 테스트 플레이하세요";
        public const string TestPlayNoNotesAfterMessage = "현재 위치 뒤에 노트가 없습니다";
        public const float StatusMessageSeconds = 3f;

        /// <summary>채보를 열 때 곡 BPM과 채보 BPM이 다르면 보여주는 경고 (자동으로 고치지 않는다)</summary>
        public const string BpmMismatchWarning = "곡 BPM과 채보 BPM이 다릅니다";

        /// <summary>에디터 재생: 재생 버튼 글자 (재생 중이 아니면 ▶, 재생 중이면 ❚❚)</summary>
        public const string PlayIcon = "▶";
        public const string PauseIcon = "❚❚";

        /// <summary>에디터 재생: 예약 재생까지 두는 지연 (초). 오디오 버퍼보다 길어야 시작이 늦지 않는다</summary>
        public const double PlaybackStartDelaySeconds = 0.1;

        /// <summary>메트로놈: 이 시간(초) 안에 올 박의 클릭을 미리 예약한다</summary>
        public const double MetronomeLookaheadSeconds = 0.25;

        /// <summary>에디터 키음: 이 시간(초) 안에 현재 위치 선에 닿을 노트의 키음을 미리 예약한다</summary>
        public const double KeySoundLookaheadSeconds = 0.25;

        /// <summary>에디터 키음 토글(Toggle_KeySound) 기본값</summary>
        public const bool KeySoundDefaultOn = true;

        /// <summary>메트로놈 클릭 소리 (코드로 만드는 사인파). 마디 첫 박은 더 높은 소리</summary>
        public const float MetronomeBeatFrequency = 1000f;
        public const float MetronomeMeasureFrequency = 1600f;
        public const float MetronomeClickSeconds = 0.03f;

        /// <summary>재생 중 현재 위치 선이 박마다 밝아지는 시간 (박의 비율, 0~1)</summary>
        public const float BeatFlashFraction = 0.15f;

        /// <summary>롱노트 최소 길이 (틱) = 1/4박. 이보다 짧은 롱노트는 만들 수 없다 (기획서 2-3)</summary>
        public const int MinLongNoteTicks = TicksPerBeat / 4;

        /// <summary>노트 배치 실패 안내 (StatusMessageSeconds 동안 상태 표시줄에 보여준다)</summary>
        public const string LongNoteTooShortMessage = "롱노트가 너무 짧습니다";
        public const string NoteOverlapMessage = "다른 노트와 겹칩니다";

        /// <summary>실행 취소 기록 최대 개수. 넘으면 가장 오래된 기록부터 버린다</summary>
        public const int MaxUndoCount = 200;

        /// <summary>실행 취소 · 다시 실행 기록 설명 (상태 표시줄에 "실행 취소: 숏노트 추가"처럼 보여 준다)</summary>
        public const string HistoryAddShortNote = "숏노트 추가";
        public const string HistoryRemoveShortNote = "숏노트 삭제";
        public const string HistoryAddLongNote = "롱노트 추가";
        public const string HistoryRemoveLongNote = "롱노트 삭제";

        /// <summary>실행 취소 · 다시 실행 결과 안내: {0} 기록 설명 (StatusMessageSeconds 동안 보여 준다)</summary>
        public const string UndoStatusFormat = "실행 취소: {0}";
        public const string RedoStatusFormat = "다시 실행: {0}";

        /// <summary>맨 아래 키 안내 줄 (KeyGuideBar, 11/19 그리드 아래 회색 글씨에서 옮김). 키 글자는 바인딩에서 읽고, 마우스·Esc는 고정 글자</summary>
        public const string GuidePlay = "재생";
        public const string GuidePause = "멈춤";
        public const string GuideMove = "이동";
        public const string GuidePlace = "놓기";
        public const string GuideRemove = "지우기";
        public const string GuideTools = "도구";
        public const string GuideUndo = "실행 취소";
        public const string GuideRedo = "다시 실행";
        public const string GuideSave = "저장";
        public const string GuideExit = "나가기";
        public const string KeyWheel = "휠";
        public const string KeyClick = "클릭";
        public const string KeyRightClick = "우클릭";
        public const string KeyEsc = "Esc";
        public const string KeyEnter = "Enter";
        /// <summary>키 안내 줄에서 재생 항목 id (재생 중이면 "멈춤"으로 바꾼다)</summary>
        public const string GuidePlayId = "play";

        /// <summary>묶음 제목: {0} 굵은 한글, {1} 작은 회색 보조 글자, {2} 보조 글자 색 (UITheme.textMuted 16진수). 와이어프레임 「박자 스냅 SNAP」 형식</summary>
        public const string SectionLabelFormat = "{0} <size=72%><color=#{2}><cspace=0.12em>{1}</cspace></color></size>";
        /// <summary>왼쪽 패널 묶음 제목 (한글 / 작은 영문)</summary>
        public const string NotesTitle = "노트 수";
        public const string NotesTitleEn = "NOTES";
        public const string ToolsTitle = "도구";
        public const string ToolsTitleEn = "TOOLS";
        public const string SpeedTitle = "테스트 플레이 배속";
        public const string SpeedTitleEn = "SPEED";
        /// <summary>노트 수 카드 이름</summary>
        public const string NotesCardLabel = "노트";
        public const string LongNotesCardLabel = "롱노트";
        /// <summary>도구 이름</summary>
        public const string ToolShortNoteName = "숏노트";
        public const string ToolLongNoteName = "롱노트";
        public const string ToolBpmSectionName = "BPM 구간";
        /// <summary>고른 도구 사용법 (숏노트 · 롱노트는 같은 문구)</summary>
        public const string ToolShortNoteHint = "클릭으로 놓고, 오른쪽 클릭으로 지워요.";
        public const string ToolLongNoteHint = "클릭으로 놓고, 오른쪽 클릭으로 지워요.";
        public const string ToolBpmSectionHint = "가로선 왼쪽을 클릭하면 BPM 입력 창이 열려요. 구간선 오른쪽 클릭은 삭제예요.";

        /// <summary>오른쪽 패널 묶음 제목</summary>
        public const string SnapTitle = "박자 스냅";
        public const string SnapTitleEn = "SNAP";
        public const string ZoomTitle = "그리드 높이";
        public const string ZoomTitleEn = "ZOOM";
        public const string CurrentBpmTitle = "BPM";
        public const string CurrentBpmTitleEn = "현재 위치";
        public const string BpmSectionsTitle = "BPM 구간";
        /// <summary>BPM 구간 제목 옆 개수: {0} 구간 수</summary>
        public const string BpmSectionsCountFormat = "{0}개";
        /// <summary>그리드 높이 값: {0} 배율 (키 안내는 오른쪽 키 칩)</summary>
        public const string GridHeightFormat = "x{0}";

        /// <summary>스위치(메트로놈 · 키음) 손잡이와 바탕 끝 사이 여백 (px, 바탕 52×30 · 손잡이 24)</summary>
        public const float SwitchKnobInset = 3f;

        /// <summary>상단 버튼 글자 (아이콘 포함)</summary>
        public const string SaveButtonLabel = "저장";
        public const string AutoTestButtonLabel = "◎ 자동 테스트";
        public const string TestPlayButtonLabel = "▶ 테스트 플레이";
        public const string ExitButtonLabel = "나가기";

        /// <summary>BPM 구간 기록 설명</summary>
        public const string HistoryAddBpmSection = "BPM 구간 추가";
        public const string HistorySetBpmSection = "BPM 구간 수정";
        public const string HistoryRemoveBpmSection = "BPM 구간 삭제";
        public const string HistoryRemoveMergedBpmSection = "같은 BPM이 된 다음 구간 삭제";
        public const string HistoryResetBpm = "곡 기본 BPM으로 되돌리기";
        public const string HistoryTuneStartBpm = "곡 시작 BPM 변경 (F1)";

        /// <summary>BPM 구간 BPM 범위 (기획서 3-6, 소수 둘째 자리까지)</summary>
        public const float MinSectionBpm = 30f;
        public const float MaxSectionBpm = 400f;

        /// <summary>그리드 BPM 구간선 풀 크기 (한 화면에 보이는 구간 수보다 넉넉히)</summary>
        public const int BpmSectionLinePoolSize = 8;

        /// <summary>BPM 구간 입력 오류 문구 (Popup_BpmSection 안내 자리에 빨간 글씨)</summary>
        public const string BpmSectionSameAsPreviousMessage = "이전 BPM 구간과 같은 BPM을 입력할 수 없습니다.";
        public const string BpmSectionSameAsNextMessage = "다음 BPM 구간과 같은 BPM을 입력할 수 없습니다.";
        public const string BpmSectionOutOfRangeMessage = "30 ~ 400 사이의 숫자를 입력해 주세요.";
        public const string BpmSectionDuplicateMessage = "이 위치에는 이미 BPM 구간이 있습니다.";
        public const string BpmSectionCannotRemoveStartMessage = "곡 시작 BPM 구간은 지울 수 없습니다.";
        public const string BpmSectionOutOfSongMessage = "곡 범위 밖에는 BPM 구간을 둘 수 없습니다.";
        public const string BpmSectionNotFoundMessage = "그 위치에 BPM 구간이 없습니다.";

        /// <summary>BPM 입력 안내 (평소 회색)</summary>
        public const string BpmSectionInputHint = "30 ~ 400, 소수 둘째 자리까지";

        /// <summary>위치 문구: {0} 마디, {1} 박 (예: 마디 17 · 1박, 마디 25 · 3.5박)</summary>
        public const string BpmSectionPositionFormat = "마디 {0} · {1}박";

        /// <summary>짧은 위치 문구 (적용 범위·다음 구간): 마디 첫 박이면 "마디 17", 아니면 "마디 25 3박"</summary>
        public const string BpmSectionMeasureFormat = "마디 {0}";
        public const string BpmSectionMeasureBeatFormat = "마디 {0} {1}박";

        /// <summary>적용 범위: {0} 시작, {1} 다음 구간 위치</summary>
        public const string BpmSectionRangeFormat = "{0} ~ {1} (다음 구간 전까지)";
        public const string BpmSectionRangeToEndFormat = "{0} ~ 곡 끝";

        /// <summary>뒤 노트 경고: {0} 노트 수 (안내 박스 왼쪽 ! 표시와 함께, 11/19)</summary>
        public const string BpmSectionNotesWarningFormat = "이 뒤 노트 {0}개는 마디 · 박 위치 그대로, 시간이 바뀝니다.";

        /// <summary>BPM 구간 입력 창 글자 (11/19 라벤더 톤: 추가 / 수정 모드로 제목·버튼이 바뀐다)</summary>
        public const string BpmSectionPopupTitleAdd = "BPM 구간 추가";
        public const string BpmSectionPopupTitleEdit = "BPM 구간 수정";
        public const string BpmSectionPopupTitleEn = "BPM SECTION";
        public const string BpmSectionIcon = "♩";
        public const string BpmSectionConfirmAdd = "추가";
        public const string BpmSectionConfirmEdit = "수정";
        public const string BpmSectionCancel = "취소";
        public const string BpmSectionUnit = "BPM";
        /// <summary>BPM 입력칸 위 범위 안내</summary>
        public const string BpmSectionRangeHint = "30 ~ 400 · 소수 둘째 자리까지";
        /// <summary>안내 자리 평소 문구 (추가 / 수정)</summary>
        public const string BpmSectionNoticeAdd = "추가를 눌러야 구간이 생겨요. 취소하면 아무것도 생기지 않아요.";
        public const string BpmSectionNoticeEdit = "수정을 눌러야 바뀌어요. 취소하면 아무것도 바뀌지 않아요.";
        /// <summary>안내 박스 왼쪽 표시</summary>
        public const string BpmSectionNoticeMark = "!";
        /// <summary>이전 · 다음 구간 BPM: {0} 이전, {1} 다음 (값은 화면에서 호박색으로 감싼다). 없으면 NoneLabel</summary>
        public const string BpmSectionNeighborFormat = "이전 구간 {0}<space=18>다음 구간 {1}";
        /// <summary>위치 카드 왼쪽 이름</summary>
        public const string BpmSectionPositionLabel = "위치";
        /// <summary>입력칸 위 이름 (옆에 작은 회색 범위 안내 BpmSectionRangeHint)</summary>
        public const string BpmSectionInputLabel = "BPM";
        public const string BpmSectionNeighborNone = "없음";

        /// <summary>오른쪽 패널 현재 위치 BPM 오른쪽 다음 구간: {0} BPM</summary>
        public const string NextBpmFormat = "다음 {0}";

        /// <summary>BPM 구간 목록에서 곡 시작 줄 표시, 그리드 라벨 아래 줄 (곡 시작 구간)</summary>
        public const string BpmSectionStartLabel = "곡 시작";

        /// <summary>그리드 구간선 라벨: {0} BPM / 아래 줄 {0} 이전 BPM → {1} 이 구간 BPM</summary>
        public const string BpmSectionLabelFormat = "♩ {0} BPM";
        public const string BpmSectionChangeFormat = "{0} → {1}";

        /// <summary>BPM 구간 편집 결과 안내 (상태 표시줄)</summary>
        public const string BpmSectionAddedFormat = "BPM 구간 추가: {0} · {1}";
        public const string BpmSectionChangedFormat = "BPM 구간 수정: {0} · {1}";
        public const string BpmSectionRemovedFormat = "BPM 구간 삭제: {0}";
        public const string BpmResetDoneMessage = "곡 기본 BPM으로 되돌렸습니다";


        /// <summary>롱노트 드래그 미리보기의 불투명도 (0~1)</summary>
        public const float LongNotePreviewAlpha = 0.45f;

        /// <summary>에디터 롱노트 몸통 불투명도 (라인 색에 곱한다). 머리·꼬리는 불투명, 몸통은 뒤 그리드 선이 비쳐 보이게 한다</summary>
        public const float LongNoteBodyAlpha = 0.45f;

        /// <summary>도구 패널 "테스트 플레이 배속" 칸(Txt_Speed): {0} 배속 (버튼으로만 바꾼다, 단축키 없음)</summary>
        public const string TestPlaySpeedFormat = "x{0:0.0}";

        /// <summary>박자 스냅 선택지 하나: 1박을 나누는 칸 수와 드롭다운 표시 이름</summary>
        public readonly struct SnapOption
        {
            public readonly int Division;
            public readonly string Label;

            public SnapOption(int division, string label)
            {
                Division = division;
                Label = label;
            }
        }
    }

    /// <summary>채보 파일 형식과 저장 위치 (기획서 2-2, 2-4)</summary>
    public static class Chart
    {
        /// <summary>지금 쓰는 채보 형식 버전. 이 값과 다른 파일은 읽지 않는다</summary>
        public const int CurrentVersion = 1;

        /// <summary>나만의 채보 폴더: persistentDataPath/MyCharts/{songId}/</summary>
        public const string CustomFolder = "MyCharts";

        /// <summary>
        /// 공식 채보 폴더 (Resources.Load 경로): Assets/Resources/Data/OfficialCharts/{songId}/{난이도}.json.
        /// 읽기와 저장(Unity 에디터 전용)이 같은 곳을 쓴다
        /// </summary>
        public const string OfficialResourcesFolder = "Data/OfficialCharts";

        /// <summary>프로젝트 안 Resources 폴더 이름 (Application.dataPath 아래). 공식 채보 저장 경로에 쓴다</summary>
        public const string ResourcesFolder = "Resources";

        /// <summary>공식 채보 작성자 (F1 패널의 공식 채보로 추가)</summary>
        public const string OfficialAuthor = "Kairos";

        /// <summary>공식 채보 난이도 이름 (공식 채보 파일 이름이기도 하다)</summary>
        public const string DifficultyEasy = "EASY";
        public const string DifficultyNormal = "NORMAL";
        public const string DifficultyHard = "HARD";

        /// <summary>공식 채보 난이도 (낮은 것부터). 공식 채보 파일 이름이기도 하다</summary>
        public static readonly string[] Difficulties = { DifficultyEasy, DifficultyNormal, DifficultyHard };

        public const string FileExtension = ".json";

        /// <summary>저장 중 임시 파일 확장자 ({chartId}.json.tmp)</summary>
        public const string TempExtension = ".tmp";

        /// <summary>읽지 못한 파일을 백업할 때 붙이는 확장자 ({chartId}.json.broken)</summary>
        public const string BrokenExtension = ".broken";

        /// <summary>백업 파일 이름이 이미 있을 때 뒤에 붙이는 시간 형식</summary>
        public const string BrokenBackupTimeFormat = "yyyyMMdd_HHmmss";

        /// <summary>새 나만의 채보 ID: my_yyyyMMdd_HHmmss_xxxx</summary>
        public const string CustomIdPrefix = "my_";
        public const string CustomIdDateFormat = "yyyyMMdd_HHmmss";
        public const int CustomIdRandomLength = 4;
        public const string CustomIdRandomChars = "abcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>같은 ID가 있을 때 다시 만드는 최대 횟수</summary>
        public const int CustomIdMaxAttempts = 100;

        /// <summary>
        /// beat → tick 변환에서 허용하는 반올림 오차 (틱). 넘으면 스냅 칸에서 벗어난 위치로 보고 경고한다.
        /// float 저장 오차(셋잇단 1/3박 등)는 이보다 훨씬 작다.
        /// </summary>
        public const float MaxTickRoundingError = 0.01f;
    }
}
