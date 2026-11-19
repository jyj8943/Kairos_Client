/// <summary>한 순간의 일반 로딩 오버레이 모습 (LoadingTimeline 결과)</summary>
public struct LoadingFrame
{
    /// <summary>일러스트 · 화면 이름 알파</summary>
    public float FrontAlpha;

    /// <summary>띠가 제자리(y 864~1080)보다 아래로 내려간 거리 (px, 0 = 제자리, 216 = 화면 밖)</summary>
    public float BandDrop;
}

/// <summary>
/// 일반 로딩 들어오기 · 나가기 시간표 (「일러스트 먼저」, 각 0.5초, 순수 함수 · EditMode 테스트용). 시간은 초 (unscaled), 값은 GameConstants.Loading.
/// 들어오기: 0~0.3 일러스트 · 화면 이름 알파 0 → 1 (easeInOutCubic) · 0.2~0.5 띠 y 1080 → 864 (easeOutCubic, 불투명) ·
///   0.5 들어오기 끝 → 지금 씬 OnExit · 로드 시작 (0.3초에 화면은 다 덮이지만 연출이 끝나기 전에는 로드하지 않는다).
/// 나가기: 0~0.3 띠 864 → 1080 (easeInCubic), 일러스트는 그대로 · 0.15~0.5 일러스트 · 화면 이름 알파 1 → 0 (easeInOutCubic) · 0.5 끝 → 다음 씬 OnEnter.
/// </summary>
public static class LoadingTimeline
{
    /// <summary>띠 높이 (px, y 864~1080)</summary>
    public const float BandHeight = 216f;

    public static readonly LoadingFrame Hidden = new LoadingFrame { FrontAlpha = 0f, BandDrop = BandHeight };
    public static readonly LoadingFrame Shown = new LoadingFrame { FrontAlpha = 1f, BandDrop = 0f };

    public static LoadingFrame EvaluateIn(float t)
    {
        return new LoadingFrame
        {
            FrontAlpha = UIEasing.InOutCubic(UIEasing.Progress(t, GameConstants.Loading.InFrontStart, GameConstants.Loading.InFrontDuration)),
            BandDrop = BandHeight * (1f - UIEasing.OutCubic(UIEasing.Progress(t, GameConstants.Loading.InBandStart, GameConstants.Loading.InBandDuration)))
        };
    }

    public static LoadingFrame EvaluateOut(float t)
    {
        return new LoadingFrame
        {
            BandDrop = BandHeight * UIEasing.InCubic(UIEasing.Progress(t, GameConstants.Loading.OutBandStart, GameConstants.Loading.OutBandDuration)),
            FrontAlpha = 1f - UIEasing.InOutCubic(UIEasing.Progress(t, GameConstants.Loading.OutFrontStart, GameConstants.Loading.OutFrontDuration))
        };
    }

    /// <summary>불러오는 중 점: t초에 켜진 점 수 (1 → 2 → 3, 0.33초마다, 3개 뒤 처음부터)</summary>
    public static int LitDots(float t)
    {
        if (t < 0f) t = 0f;
        int step = (int)(t / GameConstants.Loading.DotStepSeconds);
        return step % 3 + 1;
    }
}

/// <summary>일반 로딩 규칙 (순수 함수, EditMode 테스트용)</summary>
public static class LoadingRules
{
    /// <summary>가는 곳 문구 (다음 씬 이름으로). 모르는 씬은 빈 글자</summary>
    public static string DestinationOf(string sceneName)
    {
        if (sceneName == GameConstants.Scenes.SongSelect) return GameConstants.Loading.ToSongSelect;
        if (sceneName == GameConstants.Scenes.Editor) return GameConstants.Loading.ToEditor;
        if (sceneName == GameConstants.Scenes.Title) return GameConstants.Loading.ToTitle;
        if (sceneName == GameConstants.Scenes.InGame) return GameConstants.Loading.ToTestPlay;
        return string.Empty;
    }

    /// <summary>
    /// count개 중 무작위 하나 (0 ~ count-1). 2개 이상이면 바로 앞(previous)은 피한다. count가 0이면 -1.
    /// </summary>
    public static int PickIndex(int count, int previous, System.Random random)
    {
        if (count <= 0) return -1;
        if (count == 1) return 0;
        if (previous < 0 || previous >= count) return random.Next(count);
        int pick = random.Next(count - 1);
        return pick >= previous ? pick + 1 : pick;
    }
}
