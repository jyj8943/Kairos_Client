using UnityEngine;

/// <summary>한 순간의 곡 로딩 오버레이 모습 (SongLoadingTimeline 결과). 위치는 제자리 기준 px (오른쪽 · 위가 +)</summary>
public struct SongLoadingFrame
{
    /// <summary>배경(MenuBackground + 시계 원 문양) 알파</summary>
    public float BackgroundAlpha;
    public float AlbumAlpha;
    public float AlbumOffsetX;
    /// <summary>화면 이름 (LOADING + 인게임으로 이동)</summary>
    public float ScreenNameAlpha;
    public float PanelAlpha;
    public float PanelOffsetX;
    public float SdAlpha;
    public float SdOffsetY;
    /// <summary>불러오는 중 (작은 시계 + 글자)</summary>
    public float LoadingAlpha;
    public float BubbleAlpha;
    public float BubbleOffsetX;
    public float BubbleOffsetY;
}

/// <summary>
/// 곡 로딩 열기 · 닫기 연출 시간표 (README 4절 · 5절, 순수 함수 · EditMode 테스트용). 시간은 초 (unscaled).
/// 열기 (Enter = 0초):
/// - 0 ~ 0.35 배경 + 앨범 알파 0 → 1 (easeInOutCubic)
/// - 0.25 ~ 0.55 화면 이름 · 0.3 ~ 0.75 정보 판 오른쪽 60px → 제자리, 알파 0 → 1 (easeOutCubic)
/// - 0.55 ~ 0.95 SD 아래 70px → 제자리 (easeOutBack), 알파는 앞 절반 · 0.7 ~ 1.0 불러오는 중 · 0.85 ~ 1.1 말풍선 왼쪽 16px → 제자리
/// 닫기 (0초 = 불러오기 끝, 그리고 열기 끝 + 최소 2초):
/// - 0 ~ 0.3 앨범 왼쪽 90px, 판 오른쪽 70px, SD · 말풍선 아래 90px로 빠지며 알파 → 0 (easeInCubic), 화면 이름 · 불러오는 중은 제자리에서 옅어짐
/// - 0.22 ~ 0.64 배경 알파 1 → 0 (easeInOutCubic) · 0.64 끝
/// </summary>
public static class SongLoadingTimeline
{
    public const float PanelSlide = 60f;
    public const float SdRise = 70f;
    public const float BubbleSlide = 16f;
    public const float CloseAlbumSlide = 90f;
    public const float ClosePanelSlide = 70f;
    public const float CloseSdDrop = 90f;
    public const float CloseFadeSeconds = 0.3f;
    public const float CloseBackgroundStart = 0.22f;

    /// <summary>다 나온 모습</summary>
    public static readonly SongLoadingFrame Shown = new SongLoadingFrame
    {
        BackgroundAlpha = 1f, AlbumAlpha = 1f, ScreenNameAlpha = 1f, PanelAlpha = 1f, SdAlpha = 1f, LoadingAlpha = 1f, BubbleAlpha = 1f
    };

    /// <summary>열기 연출 t초 모습</summary>
    public static SongLoadingFrame EvaluateOpen(float t)
    {
        SongLoadingFrame f = new SongLoadingFrame();
        float cover = EaseInOutCubic(Progress(t, 0f, GameConstants.SongLoading.CoverSeconds));
        f.BackgroundAlpha = cover;
        f.AlbumAlpha = cover;
        f.ScreenNameAlpha = Progress(t, 0.25f, 0.3f);

        float panel = EaseOutCubic(Progress(t, 0.3f, 0.45f));
        f.PanelAlpha = panel;
        f.PanelOffsetX = PanelSlide * (1f - panel);

        float sd = Progress(t, 0.55f, 0.4f);
        f.SdAlpha = Mathf.Clamp01(sd * 2f);
        f.SdOffsetY = -SdRise * (1f - EaseOutBack(sd));

        f.LoadingAlpha = Progress(t, 0.7f, 0.3f);

        float bubble = EaseOutCubic(Progress(t, 0.85f, 0.25f));
        f.BubbleAlpha = bubble;
        f.BubbleOffsetX = -BubbleSlide * (1f - bubble);
        return f;
    }

    /// <summary>닫기 연출 t초 모습 (0.64초에 모두 0)</summary>
    public static SongLoadingFrame EvaluateClose(float t)
    {
        SongLoadingFrame f = new SongLoadingFrame();
        float x = EaseInCubic(Progress(t, 0f, CloseFadeSeconds));
        float fade = 1f - x;
        f.AlbumAlpha = fade;
        f.AlbumOffsetX = -CloseAlbumSlide * x;
        f.PanelAlpha = fade;
        f.PanelOffsetX = ClosePanelSlide * x;
        f.SdAlpha = fade;
        f.SdOffsetY = -CloseSdDrop * x;
        f.BubbleAlpha = fade;
        f.BubbleOffsetY = -CloseSdDrop * x;
        f.ScreenNameAlpha = fade;
        f.LoadingAlpha = fade;
        float end = GameConstants.SongLoading.CloseSeconds;
        f.BackgroundAlpha = 1f - EaseInOutCubic(Progress(t, CloseBackgroundStart, end - CloseBackgroundStart));
        return f;
    }

    private static float Progress(float t, float start, float duration)
    {
        if (t <= start) return 0f;
        if (duration <= 0f) return 1f;
        return Mathf.Clamp01((t - start) / duration);
    }

    public static float EaseOutCubic(float x) { float y = 1f - x; return 1f - y * y * y; }

    public static float EaseInCubic(float x) => x * x * x;

    public static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    /// <summary>easeOutBack (튀어 오름 1.70158)</summary>
    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }
}

/// <summary>곡 로딩 카드에 그릴 값 (SongLoadingInfo.Build 결과)</summary>
public struct SongLoadingInfo
{
    public string Title;
    public string Artist;
    public string BadgeText;
    public bool IsCustom;
    public string LevelText;
    public string SpeedText;
    /// <summary>나만의 채보만 true (공식 채보는 CHART 줄을 숨긴다)</summary>
    public bool ShowChartName;
    public string ChartName;
    public RecordTexts Record;
}

/// <summary>
/// 진입 데이터(곡 · 채보 · 배속 · 기록)로 곡 로딩 카드 값을 만든다 (순수 함수, EditMode 테스트용).
/// 뱃지 · 채보 이름은 인게임 곡 정보 카드와 같은 규칙(InGameHudRules.BuildSongInfo), 기록은 곡 선택과 같은 형식(SongSelectRecordFormat).
/// </summary>
public static class SongLoadingInfoRules
{
    public static SongLoadingInfo Build(InGameEntryData entry, SongData song, ChartData chart, ChartRecordData record)
    {
        ChartType type = entry != null ? entry.chartType : ChartType.Official;
        if (chart != null && ChartData.TryParseType(chart.type, out ChartType parsed)) type = parsed;
        SongInfoDisplay display = InGameHudRules.BuildSongInfo(type, chart != null ? chart.difficulty : null, chart != null ? chart.chartName : null,
            chart != null ? chart.level : 0, false, false);
        return new SongLoadingInfo
        {
            Title = song != null ? song.Title : string.Empty,
            Artist = song != null ? song.Artist : string.Empty,
            BadgeText = display.BadgeText,
            IsCustom = display.IsCustom,
            LevelText = string.Format(GameConstants.SongLoading.LevelFormat, chart != null ? chart.level : 0),
            SpeedText = string.Format(System.Globalization.CultureInfo.InvariantCulture, GameConstants.SongLoading.SpeedFormat, entry != null ? entry.scrollMultiplier : 1f),
            ShowChartName = display.ShowChartName,
            ChartName = display.ChartName,
            Record = SongSelectRecordFormat.Build(record)
        };
    }
}
