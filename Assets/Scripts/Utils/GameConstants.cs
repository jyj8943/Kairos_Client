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
        public const string EditorSongSelect = "EditorSongSelectScene";
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

        /// <summary>첫 노트 전 최소 준비 시간 (초)</summary>
        public const double LeadInSeconds = 2.0;

        /// <summary>노래 시작 지연의 최소값 (초). 첫 노트가 늦어도 이만큼은 기다린다</summary>
        public const double MinStartDelaySeconds = 1.0;

        /// <summary>
        /// 모든 노트가 판정된 뒤 결과 씬으로 넘어가기까지 기다리는 시간 (초, 곡 시간 기준).
        /// 이 동안 노래는 계속 흐르고, 일시정지는 받지 않는다 (결과로 넘어가는 중)
        /// </summary>
        public const double ResultDelaySeconds = 3.0;

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

        /// <summary>인게임 배속 키(1 / 2)를 누르고 있을 때: 이 시간 뒤부터 반복 (초)</summary>
        public const double SpeedKeyRepeatDelaySeconds = 0.4;

        /// <summary>인게임 배속 키 반복 간격 (초)</summary>
        public const double SpeedKeyRepeatIntervalSeconds = 0.08;

        /// <summary>인게임 배속을 바꿀 때 기어 옆에 잠깐 보여주는 문구 형식과 시간 (초, realtime)</summary>
        public const string SpeedChangeFormat = "x{0:0.0}";
        public const float SpeedChangeShowSeconds = 0.8f;

        /// <summary>인게임 곡 정보 패널 3줄: {0} 배속 (플레이 중 1 / 2 키로 바꾸면 다시 쓴다)</summary>
        public const string InGameSpeedFormat = "배속 x{0:0.0} · [1] [2]로 변경";

        /// <summary>인게임 곡 정보 패널 레벨: {0} 레벨</summary>
        public const string InGameLevelFormat = "Lv.{0}";

        /// <summary>인게임 곡 정보 패널: 나만의 채보 뱃지 문구 (공식 채보는 난이도 이름을 쓴다)</summary>
        public const string ChartBadgeCustomText = "CUSTOM";

        /// <summary>인게임 곡 정보 패널 뱃지 기본 색 (Canvas_InGame Inspector에서 바꿀 수 있다). 난이도 순서는 Chart.Difficulties와 같다</summary>
        public static readonly Color32 ChartBadgeEasyColor = new Color32(0x16, 0xA3, 0x4A, 0xFF);
        public static readonly Color32 ChartBadgeNormalColor = new Color32(0x25, 0x63, 0xEB, 0xFF);
        public static readonly Color32 ChartBadgeHardColor = new Color32(0xDC, 0x26, 0x26, 0xFF);
        public static readonly Color32 ChartBadgeCustomColor = new Color32(0x7C, 0x3A, 0xED, 0xFF);

        /// <summary>키 칸 글자 (입력 에셋 바인딩에서 읽지 못했을 때 쓴다). Lane0 ~ Lane3 순서</summary>
        public static readonly string[] LaneKeyLabels = { "A", "S", ";", "'" };

        /// <summary>
        /// 키 칸 크기 (1080 화면 기준 px). 화면 아래 끝에 붙이고, 칸 폭 = 라인 폭 − 양옆 여백 × 2.
        /// 월드 크기는 카메라 높이(orthographicSize × 2)를 KeyCellReferenceHeightPx로 나눠 바꾼다 (해상도가 바뀌어도 같은 비율)
        /// </summary>
        public const float KeyCellHeightPx = 120f;
        public const float KeyCellSidePaddingPx = 4f;
        public const float KeyCellReferenceHeightPx = 1080f;

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

        /// <summary>결과 버튼 단축키 표시 (버튼 문구 뒤에 붙인다)</summary>
        public const string RetryKeyHint = " (R)";
        public const string ReturnKeyHint = " (Enter)";

        /// <summary>판정 글자</summary>
        public const string PerfectText = "PERFECT";
        public const string GreatText = "GREAT";
        public const string GoodText = "GOOD";
        public const string MissText = "MISS";

        /// <summary>FAST / SLOW 글자 (판정 글자 위, 판정 패널 라벨). GREAT · GOOD에서만 쓴다</summary>
        public const string FastText = "FAST";
        public const string SlowText = "SLOW";

        /// <summary>FAST / SLOW 기본 색 (Canvas_InGame Inspector에서 바꿀 수 있다). 판정 색과 겹치지 않는 청록 · 주황</summary>
        public static readonly Color32 FastColor = new Color32(0x2D, 0xD4, 0xBF, 0xFF);
        public static readonly Color32 SlowColor = new Color32(0xFB, 0x92, 0x3C, 0xFF);
        public const string FullComboText = "FULL COMBO";
        public const string AllPerfectText = "ALL PERFECT";

        /// <summary>인게임 콤보 숫자 위 제목 (Txt_ComboTitle)</summary>
        public const string ComboTitleText = "COMBO";

        /// <summary>결과 화면 신기록 표시 (기획서 3-5 ③). 처음 플레이: NewRecordText, 기록을 넘었을 때: {0} 이전 최고 점수 (천 단위 쉼표)</summary>
        public const string NewRecordText = "NEW RECORD";
        public const string NewRecordPreviousFormat = "NEW RECORD  (이전 {0:N0})";

        /// <summary>시작 MISS 롱노트의 비활성 색 (끝까지 이 색으로 내려간다)</summary>
        public static readonly Color32 LongNoteMissedColor = new Color32(0x5A, 0x5A, 0x5E, 0xFF);

        /// <summary>
        /// 라인별 노트 색 (A · S · ; · ' 순서). 에디터 Img_ShortNote의 기본 색과 같은 값이다.
        /// </summary>
        public static readonly Color32[] LaneColors =
        {
            new Color32(0x6E, 0xC8, 0xF5, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            new Color32(0x6E, 0xC8, 0xF5, 0xFF)
        };
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

    /// <summary>확인 팝업 문구 (기획서 3-8 사용처 표). 화면 스크립트에 문구를 직접 쓰지 않고 여기 모은다</summary>
    public static class Confirm
    {
        /// <summary>취소 버튼 기본 이름</summary>
        public const string CancelLabel = "취소";

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
    }

    /// <summary>인게임 일시정지 메뉴 (기획서 3-4, 2-1 재개 3초 카운트다운)</summary>
    public static class Pause
    {
        /// <summary>팝업 제목</summary>
        public const string Title = "일시정지";

        /// <summary>버튼 이름 (위에서부터 계속 · 재시작 · 설정 · 나가기)</summary>
        public const string ResumeLabel = "계속 (3초 후 재개)";
        public const string RestartLabel = "재시작";
        public const string SettingsLabel = "설정";
        public const string ExitLabel = "나가기";

        /// <summary>패널 아래 조작 안내</summary>
        public const string HintText = "ESC 계속 · ↑↓ 이동 · Enter 선택";

        /// <summary>재개 카운트다운 (초, unscaled). 1초마다 숫자 하나씩 3 → 2 → 1</summary>
        public const int CountdownSeconds = 3;
    }

    /// <summary>설정 창(Popup_Settings) 문구·값 범위 (기획서 3-7, 2-4). 저장은 PlayerData.json (GameConstants.PlayerData)</summary>
    public static class Settings
    {
        public const string Title = "설정";
        public const string CloseLabel = "×";
        public const string TabSound = "사운드";
        public const string TabGame = "게임";
        public const string TabScreen = "화면";

        public const string MasterVolumeLabel = "메인 음량";
        public const string MusicVolumeLabel = "음악 음량";
        public const string SfxVolumeLabel = "효과음 음량";

        public const string OffsetLabel = "판정 오프셋";
        public const string OffsetRangeHint = "-200 ~ +200ms, 5ms 단위";
        public const string MeasureOffsetLabel = "오프셋 측정하기";

        public const string ResolutionLabel = "해상도";
        public const string WindowModeLabel = "창 모드";

        /// <summary>◀ ▶ 버튼 글자 (폰트에 있는 글자만 쓴다. U+2212 −는 폰트에 없어 ASCII -를 쓴다)</summary>
        public const string StepDownLabel = "◀";
        public const string StepUpLabel = "▶";

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

    /// <summary>곡 선택 화면 표시 문구 (기획서 3-3)</summary>
    public static class SongSelect
    {
        /// <summary>값이 없을 때 표시 (최고 기록, 없는 난이도의 레벨)</summary>
        public const string EmptyValue = "-";

        /// <summary>난이도 버튼 레벨: {0} 레벨</summary>
        public const string LevelFormat = "Lv.{0}";

        /// <summary>곡 정보 줄: {0} BPM, {1} 곡 길이</summary>
        public const string SongInfoFormat = "BPM {0} · {1}";

        /// <summary>배속 표시: {0} 배속 (소수 첫째 자리까지)</summary>
        public const string SpeedFormat = "배속 x{0:0.0} ( 1 / 2 · - / + )";

        /// <summary>최고 기록 칸: {0} 값. 점수는 천 단위 쉼표 (InvariantCulture)</summary>
        public const string RecordScoreFormat = "{0:N0}";
        public const string RecordRankFormat = "랭크 {0}";
        public const string RecordComboFormat = "최대 콤보 {0}";

        /// <summary>빈 상태 문구</summary>
        public const string OfficialEmpty = "공식 채보가 없습니다";
        public const string MyChartEmpty = "저장된 채보가 없습니다";

        /// <summary>하단 조작 안내 (탭마다 ←→ 대상이 다르다)</summary>
        public const string KeyHintOfficial = "↑↓ 곡 · ←→ 난이도 · Tab 탭 · ESC 타이틀";
        public const string KeyHintMyChart = "↑↓ 곡 · ←→ 채보 · Tab 탭 · ESC 타이틀";
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

        /// <summary>상단 바 채보 정보: {0} 곡 제목, {1} 채보 이름, {2} 레벨</summary>
        public const string ChartInfoFormat = "{0} · {1} · Lv.{2}";

        /// <summary>타임라인 정보: {0} 현재 위치 시간, {1} 곡 길이, {2} 마디 번호</summary>
        public const string TimeInfoFormat = "{0} / {1} · 마디 {2}";

        /// <summary>저장 상태 표시. Saved의 {0}에 저장 시각(SaveTimeFormat)이 들어간다</summary>
        public const string SaveStatusSaved = "저장됨 {0}";
        public const string SaveStatusFailed = "저장 실패";
        public const string SaveStatusUnsaved = "저장 안 됨";
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

        /// <summary>오른쪽 패널 노트 수: {0} 전체 노트 수(숏 + 롱), {1} 롱노트 수</summary>
        public const string NoteCountFormat = "노트 {0} · 롱노트 {1}";

        /// <summary>롱노트 드래그 미리보기의 불투명도 (0~1)</summary>
        public const float LongNotePreviewAlpha = 0.45f;

        /// <summary>에디터 롱노트 몸통 불투명도 (라인 색에 곱한다). 머리·꼬리는 불투명, 몸통은 뒤 그리드 선이 비쳐 보이게 한다</summary>
        public const float LongNoteBodyAlpha = 0.45f;

        /// <summary>도구 패널 "테스트 플레이 배속" 칸(Txt_Speed): {0} 배속 (버튼으로만 바꾼다, 단축키 없음)</summary>
        public const string TestPlaySpeedFormat = "x{0:0.0}";

        /// <summary>곡 고르기의 내 채보 항목 정보: {0} 레벨, {1} 노트 수</summary>
        public const string MyChartInfoFormat = "Lv.{0} · 노트 {1}";

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

        /// <summary>공식 채보 난이도 (낮은 것부터). 공식 채보 파일 이름이기도 하다</summary>
        public static readonly string[] Difficulties = { "EASY", "NORMAL", "HARD" };

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
