using UnityEngine;

/// <summary>
/// 한 순간의 곡 로딩 오버레이 모습 (SongLoadingClockTimeline 결과). 위치는 제자리 기준 px (위가 +), 크기는 배율.
/// </summary>
public struct SongLoadingFrame
{
    /// <summary>곡 배경 + Dim (Background 묶음) 알파</summary>
    public float BackgroundAlpha;
    /// <summary>앨범 그림(Img_AlbumCover) 알파 · Img_Album 크기</summary>
    public float AlbumAlpha;
    public float AlbumScale;
    /// <summary>Img_AlbumFlash 알파</summary>
    public float FlashAlpha;
    /// <summary>Img_Glow 알파 · 크기 (앨범 크기 기준 배율)</summary>
    public float GlowAlpha;
    public float GlowScale;
    /// <summary>시계 링(눈금 · 호 · 점) 알파 · Clock 크기</summary>
    public float ClockAlpha;
    public float ClockScale;
    /// <summary>제목 · 아티스트 · 뱃지 · 채보 이름 (Info 묶음) 알파 · y 이동</summary>
    public float InfoAlpha;
    public float InfoOffsetY;
    /// <summary>SD · 막대 · LOADING (Sd 묶음) 알파 · SD y 이동</summary>
    public float SdAlpha;
    public float SdOffsetY;
    /// <summary>12시 반짝 Img_ClockSpark 알파</summary>
    public float SparkAlpha;
    /// <summary>Img_Blackout 알파</summary>
    public float BlackoutAlpha;
    /// <summary>오버레이 전체 알파 (걷힘)</summary>
    public float RootAlpha;
}

/// <summary>
/// 곡 로딩 · 시계 연출 시간표 (11/26, 와이어프레임 21_SongLoading_Clock README 연출표, 순수 함수 · EditMode 테스트용). 시간은 초 (unscaled), 상수는 GameConstants.SongLoading.
/// 열기 (0초 = Open 다음 프레임):
/// - 0 ~ 0.25 곡 배경 + Dim 페이드인 · 0.1 ~ 0.45 앨범 알파 0 → 1, 크기 0.9 → 1 (OutCubic) · 0.1 ~ 0.24 앨범 번쩍 0.25 → 0
/// - 0.1 ~ 0.8 앨범 뒤 빛 크기 1.05 → 1.5배, 알파 0 → 0.35 → 0 (sin) · 0.3 ~ 0.7 시계 링 알파 0 → 1, 크기 1.15 → 1 (OutCubic)
/// - 0.5 ~ 0.8 글자 알파 0 → 1, 아래 10px → 제자리 (OutCubic) · 0.6 ~ 0.95 SD · 막대 · LOADING 알파 (0.25초), SD 위 60px → 제자리 (OutCubic 0.35초)
/// 바늘 (0.8 ~): fillAmount = min(EaseInOut((t − 0.8) / 1.6), 로딩 진행), 줄어들지 않는다 (HandFill).
/// 끝 (e = 바늘이 12시에 닿은 순간 T부터 지난 시간):
/// - 0 ~ 0.3 12시 반짝 0 → 1 → 0 (sin) · 0 ~ 0.25 글자 · SD · 막대 · LOADING 1 → 0 · 0.3 ~ 0.65 암전 0 → 1 (InOutCubic)
/// - 0.65 ~ revealStart(0.8 이상, 정리가 늦으면 늘어남) 검은 화면 유지 · revealStart ~ +0.35 오버레이 전체 1 → 0
/// 링 · 앨범은 끝에서 커지지 않는다.
/// 암전이 다 덮이면(BlackedOutAt) 검은 화면 아래의 곡 배경 · 앨범 · 빛 · 시계 · 글자 · SD를 모두 0으로 숨긴다 (11/26).
/// 걷힘은 오버레이 전체 알파를 줄이므로, 숨기지 않으면 검은 화면이 옅어지는 동안 그 아래 그림이 함께 비쳐 잔상처럼 보인다.
/// </summary>
public static class SongLoadingClockTimeline
{
    /// <summary>다 나온 모습 (열기 끝 · 바늘 0)</summary>
    public static SongLoadingFrame Shown => EvaluateOpen(GameConstants.SongLoading.OpenSeconds);

    /// <summary>숨긴 모습 (모두 0, 크기 1)</summary>
    public static SongLoadingFrame Hidden => new SongLoadingFrame { AlbumScale = 1f, GlowScale = 1f, ClockScale = 1f };

    /// <summary>열기 연출 t초 모습 (끝 연출 전)</summary>
    public static SongLoadingFrame EvaluateOpen(float t)
    {
        SongLoadingFrame f = new SongLoadingFrame { RootAlpha = 1f };
        f.BackgroundAlpha = UIEasing.Progress(t, 0f, GameConstants.SongLoading.BackgroundFadeSeconds);

        float album = UIEasing.OutCubic(UIEasing.Progress(t, GameConstants.SongLoading.AlbumStart, GameConstants.SongLoading.AlbumSeconds));
        f.AlbumAlpha = album;
        f.AlbumScale = Mathf.Lerp(GameConstants.SongLoading.AlbumStartScale, 1f, album);

        f.FlashAlpha = t < GameConstants.SongLoading.FlashStart ? 0f
            : GameConstants.SongLoading.FlashAlpha * (1f - UIEasing.Progress(t, GameConstants.SongLoading.FlashStart, GameConstants.SongLoading.FlashSeconds));

        float glow = UIEasing.Progress(t, GameConstants.SongLoading.GlowStart, GameConstants.SongLoading.GlowSeconds);
        f.GlowScale = Mathf.Lerp(GameConstants.SongLoading.GlowStartScale, GameConstants.SongLoading.GlowEndScale, glow);
        f.GlowAlpha = GameConstants.SongLoading.GlowMaxAlpha * Sin01(glow);

        float clock = UIEasing.OutCubic(UIEasing.Progress(t, GameConstants.SongLoading.ClockStart, GameConstants.SongLoading.ClockSeconds));
        f.ClockAlpha = clock;
        f.ClockScale = Mathf.Lerp(GameConstants.SongLoading.ClockStartScale, 1f, clock);

        float info = UIEasing.OutCubic(UIEasing.Progress(t, GameConstants.SongLoading.InfoStart, GameConstants.SongLoading.InfoSeconds));
        f.InfoAlpha = info;
        f.InfoOffsetY = -GameConstants.SongLoading.InfoRise * (1f - info);

        f.SdAlpha = UIEasing.Progress(t, GameConstants.SongLoading.SdStart, GameConstants.SongLoading.SdFadeSeconds);
        float drop = UIEasing.OutCubic(UIEasing.Progress(t, GameConstants.SongLoading.SdStart, GameConstants.SongLoading.SdDropSeconds));
        f.SdOffsetY = GameConstants.SongLoading.SdDrop * (1f - drop);
        return f;
    }

    /// <summary>
    /// 끝 연출을 열기 모습 위에 얹는다. e = T(바늘 12시)부터 지난 시간 (끝 전이면 음수), revealStart = 걷히기 시작 (T 기준, 0.8보다 이르지 않다).
    /// </summary>
    public static SongLoadingFrame ApplyEnd(SongLoadingFrame f, float e, float revealStart)
    {
        if (e < 0f) return f;
        f.SparkAlpha = e < GameConstants.SongLoading.SparkSeconds ? Sin01(UIEasing.Progress(e, 0f, GameConstants.SongLoading.SparkSeconds)) : 0f;
        float fade = 1f - UIEasing.Progress(e, 0f, GameConstants.SongLoading.EndFadeSeconds);
        f.InfoAlpha *= fade;
        f.SdAlpha *= fade;
        f.BlackoutAlpha = UIEasing.InOutCubic(UIEasing.Progress(e, GameConstants.SongLoading.BlackoutStart, GameConstants.SongLoading.BlackoutSeconds));
        if (e >= GameConstants.SongLoading.BlackedOutAt)
        {
            // 다 덮인 뒤에는 검은 화면만 남긴다: 걷힐 때 검은색만 옅어지고 그 뒤 인게임이 바로 드러난다 (잔상 없음)
            f.BlackoutAlpha = 1f;
            f.BackgroundAlpha = 0f;
            f.AlbumAlpha = 0f;
            f.FlashAlpha = 0f;
            f.GlowAlpha = 0f;
            f.ClockAlpha = 0f;
            f.SparkAlpha = 0f;
            f.InfoAlpha = 0f;
            f.SdAlpha = 0f;
        }
        f.RootAlpha = 1f - UIEasing.Progress(e, Mathf.Max(GameConstants.SongLoading.RevealStart, revealStart), GameConstants.SongLoading.RevealSeconds);
        return f;
    }

    /// <summary>t초 · 끝 e초의 모습 (끝 전이면 e &lt; 0)</summary>
    public static SongLoadingFrame Evaluate(float t, float e, float revealStart)
    {
        return ApplyEnd(EvaluateOpen(t), e, revealStart);
    }

    /// <summary>바늘 시간 진행 (로딩 진행을 빼고): EaseInOut((t − 0.8) / 1.6), 0.8초 전은 0</summary>
    public static float HandTime(float t)
    {
        float p = UIEasing.Progress(t, GameConstants.SongLoading.HandStart, GameConstants.SongLoading.HandSeconds);
        // InOutCubic은 끝 몇 ms 전부터 float로 1이 되므로, 2.4초가 되기 전에는 12시(1)에 닿지 않게 1 바로 아래로 묶는다
        return p >= 1f ? 1f : Mathf.Min(UIEasing.InOutCubic(p), HandBeforeTwelve);
    }

    /// <summary>바늘 시간이 다 차기 전의 가장 큰 값 (1 바로 아래)</summary>
    public const float HandBeforeTwelve = 0.99999f;

    /// <summary>
    /// 바늘 fillAmount = min(EaseInOut((t − 0.8) / 1.6), 로딩 진행). 이전 값보다 작아지지 않는다 (로딩을 기다리면 그 자리에 멈춘다).
    /// </summary>
    public static float HandFill(float t, float loadProgress, float previousFill)
    {
        float fill = Mathf.Min(HandTime(t), Mathf.Clamp01(loadProgress));
        return Mathf.Max(previousFill, fill);
    }

    /// <summary>
    /// 로딩 진행 (0~1): 로드 시작 전 0 / 씬 로드 중 AsyncOperation.progress ÷ 0.9 × 0.9 / 다음 씬 Init 끝 1.
    /// </summary>
    public static float LoadProgress(bool loadStarted, float operationProgress, bool initDone)
    {
        if (initDone) return 1f;
        if (!loadStarted) return 0f;
        return Mathf.Clamp01(operationProgress / 0.9f) * GameConstants.SongLoading.SceneLoadProgressWeight;
    }

    /// <summary>바늘 끝 점 위치 (시계 중심 기준 px): 12시에서 시계 방향으로 fill × 360°</summary>
    public static Vector2 DotPosition(float fill, float radius)
    {
        float angle = Mathf.Clamp01(fill) * Mathf.PI * 2f;
        return new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
    }

    /// <summary>0 → 1 → 0 (sin 반 바퀴)</summary>
    private static float Sin01(float x) => Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI);
}

/// <summary>곡 로딩에 그릴 곡 · 채보 값 (SongLoadingInfoRules.Build 결과)</summary>
public struct SongLoadingInfo
{
    public string Title;
    public string Artist;
    public string BadgeText;
    public bool IsCustom;
    public string LevelText;
    /// <summary>나만의 채보만 true (공식 채보는 CHART 줄을 숨긴다)</summary>
    public bool ShowChartName;
    /// <summary>"CHART  {채보 이름}" (나만의 채보만)</summary>
    public string ChartText;
}

/// <summary>
/// 진입 데이터(곡 · 채보)로 곡 로딩 글자 값을 만든다 (순수 함수, EditMode 테스트용). 배속 · 기록은 이 화면에서 쓰지 않는다 (11/26).
/// 뱃지 · 채보 이름은 인게임 곡 정보 카드와 같은 규칙(InGameHudRules.BuildSongInfo).
/// </summary>
public static class SongLoadingInfoRules
{
    public static SongLoadingInfo Build(InGameEntryData entry, SongData song, ChartData chart)
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
            ShowChartName = display.ShowChartName,
            ChartText = display.ShowChartName ? string.Format(GameConstants.SongLoading.ChartFormat, display.ChartName) : string.Empty
        };
    }
}
