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

        /// <summary>그리드 높이 x1.0일 때 1박의 높이 (1920×1080 기준 px)</summary>
        public const float PixelsPerBeat = 100f;

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
