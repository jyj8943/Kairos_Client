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

        /// <summary>마지막 노트 뒤 결과까지 기다리는 시간 (초)</summary>
        public const double EndPaddingSeconds = 1.0;

        /// <summary>
        /// 인게임 기본 낙하 속도 (월드 유닛/초). 판정선부터 화면 위까지 약 1.2초가 보인다.
        /// 화면 높이 10유닛 = 1080px이라 약 756px/초 (에디터 그리드 PixelsPerSecond의 약 3배).
        /// </summary>
        public const float DefaultScrollSpeed = 7f;

        /// <summary>배속. TODO: 배속 설정(곡 선택 화면)을 만들면 그 값으로 바꾼다 (지금은 1.0 고정)</summary>
        public const float DefaultScrollMultiplier = 1f;

        /// <summary>테스트 플레이: 시작 위치 이만큼(초) 앞의 음악부터 들려준다 (첫 노트 전 준비 시간)</summary>
        public const double TestPlayLeadInSeconds = 2.0;

        /// <summary>음원 중간부터 예약 재생할 때 두는 최소 지연 (초). 오디오 버퍼보다 길어야 시작이 늦지 않는다</summary>
        public const double ScheduleLatencySeconds = 0.1;

        /// <summary>테스트 플레이 표시와 결과 화면 돌아가기 버튼 문구</summary>
        public const string TestPlayLabel = "테스트 플레이";
        public const string TestPlayInGameText = "테스트 플레이 · ESC 에디터로";
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
        public const string FullComboText = "FULL COMBO";
        public const string AllPerfectText = "ALL PERFECT";

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

        /// <summary>저장하지 않고 나가려 할 때 안내. 이 시간(초) 안에 한 번 더 누르면 나간다</summary>
        public const string ExitUnsavedWarning = "저장하지 않았습니다 · 한 번 더 누르면 나갑니다";
        public const float ExitConfirmSeconds = 3f;

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

        /// <summary>메트로놈 클릭 소리 (코드로 만드는 사인파). 마디 첫 박은 더 높은 소리</summary>
        public const float MetronomeBeatFrequency = 1000f;
        public const float MetronomeMeasureFrequency = 1600f;
        public const float MetronomeClickSeconds = 0.03f;

        /// <summary>재생 중 현재 위치 선이 박마다 밝아지는 시간 (박의 비율, 0~1)</summary>
        public const float BeatFlashFraction = 0.15f;

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

        /// <summary>공식 채보 폴더: streamingAssetsPath/Charts/{songId}/</summary>
        public const string OfficialFolder = "Charts";

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
