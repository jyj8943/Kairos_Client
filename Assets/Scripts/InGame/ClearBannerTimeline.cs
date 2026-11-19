using UnityEngine;

/// <summary>곡 끝 연출 종류 (모든 노트 판정 뒤 3초)</summary>
public enum ClearType
{
    /// <summary>연출 없음 (MISS 있음 · 테스트 플레이 · 자동 플레이)</summary>
    None,

    /// <summary>FULL COMBO (MISS 0개)</summary>
    FullCombo,

    /// <summary>ALL PERFECT (전부 PERFECT)</summary>
    AllPerfect
}

/// <summary>
/// 곡 끝 연출을 고르는 규칙 (순수 함수, EditMode 테스트용). 점수가 아니라 판정 개수로 고른다.
/// </summary>
public static class ClearBannerRules
{
    /// <summary>
    /// 테스트 플레이·자동 플레이면 None. 아니면 모든 판정이 PERFECT면 AllPerfect (랭크 AP와 같은 RankRules.IsAllPerfect),
    /// 아니고 모든 노트를 판정했고 MISS 0개면 FullCombo, 둘 다 아니면 None. AP가 우선이다.
    /// </summary>
    public static ClearType Select(bool isTestPlay, bool isAutoPlay, int perfectCount, int missCount, int judgedCount, int totalNotes)
    {
        if (isTestPlay || isAutoPlay || totalNotes <= 0) return ClearType.None;
        if (RankRules.IsAllPerfect(perfectCount, totalNotes)) return ClearType.AllPerfect;
        if (missCount == 0 && judgedCount == totalNotes) return ClearType.FullCombo;
        return ClearType.None;
    }

    /// <summary>개수 칸 폭 (px) = 전체 자릿수 × 자릿수당 폭. 숫자가 올라가는 동안 캡슐 폭이 바뀌지 않게 고정한다</summary>
    public static float CountWidth(int total)
    {
        return DigitCount(total) * GameConstants.Play.ClearCountDigitWidth;
    }

    /// <summary>자연수 자릿수 (0 이하는 1)</summary>
    public static int DigitCount(int value)
    {
        int digits = 1;
        while (value >= 10) { value /= 10; digits++; }
        return digits;
    }
}

/// <summary>빛 알갱이 하나 (시드로 만든 고정 값, 시계 가운데 기준 UI 좌표 · 위가 +)</summary>
public struct ClearMote
{
    /// <summary>나타나는 시각 (초, 연출 시작 기준)</summary>
    public float StartTime;
    /// <summary>수명 (초)</summary>
    public float Life;
    /// <summary>시작 위치 (시계 가운데 기준, y는 아래가 −)</summary>
    public Vector2 Start;
    /// <summary>크기 (px, 정사각형)</summary>
    public float Size;
    /// <summary>true면 흰색, false면 판정 색</summary>
    public bool White;
}

/// <summary>한 순간의 곡 끝 연출 모습 (ClearBannerTimeline.Evaluate 결과)</summary>
public struct ClearBannerFrame
{
    /// <summary>연출이 보이는지 (재생 전·3초 뒤 false)</summary>
    public bool Visible;
    /// <summary>띠·글자·시계·캡슐·알갱이 전체 알파 (ClearBanner CanvasGroup, 2.4초부터 → 0)</summary>
    public float RootAlpha;

    /// <summary>띠 세로 크기 (0 → 1)</summary>
    public float BandScaleY;
    /// <summary>화면 반짝임 알파 (0.05초 22% → 0.35초 0, 12시 강조 때 +10%p)</summary>
    public float FlashAlpha;

    /// <summary>글자 알파 · 크기 · 오른쪽 밀림(px) · 위로 오름(px) · 흰색 덧칠 알파(밝기 +45%p)</summary>
    public float LogoAlpha;
    public float LogoScale;
    public float LogoOffsetX;
    public float LogoRise;
    public float LogoBright;

    /// <summary>빛 줄기 진행 (0 = 글자 왼쪽 밖, 1 = 오른쪽 밖)</summary>
    public float ShineProgress;
    public bool ShineVisible;

    /// <summary>시계 그룹 알파 (0 → 1, 색 자체가 80%) · 크기</summary>
    public float ClockAlpha;
    public float ClockScale;
    /// <summary>바늘 각도 (시계 방향 +, 0 = 12시)</summary>
    public float MinuteAngle;
    public float HourAngle;
    /// <summary>바깥 점선 원 각도 (시계 방향 +, 초당 20°)</summary>
    public float DashesAngle;
    /// <summary>12시 강조: 흰 테두리 알파 · 12시 점 알파</summary>
    public float HighlightAlpha;
    public float TopDotAlpha;
    /// <summary>퍼지는 링: 크기 배율(반지름 170 → 280) · 알파</summary>
    public float BurstScale;
    public float BurstAlpha;

    /// <summary>개수 캡슐 알파 · 위로 오름(px) · 지금 보이는 개수</summary>
    public float CapsuleAlpha;
    public float CapsuleRise;
    public int Count;

    /// <summary>라인 빛 기둥 높이(px) · 알파 배율 (사라질 때 35%까지)</summary>
    public float PillarHeight;
    public float PillarAlpha;

    /// <summary>기어 위 콤보 숫자 알파 (사라질 때 40%까지, 연출 밖이면 음수 = 콤보 자체 표시 그대로)</summary>
    public float ComboAlpha;
}

/// <summary>
/// 곡 끝 FULL COMBO · ALL PERFECT 연출 시간표 (README 5절 · 기획서 3-4 「FC/AP 연출 (11/19)」). MonoBehaviour가 아니어서 EditMode로 테스트한다.
/// 시간 t = 연출 시작(모든 노트 판정 순간)부터 지난 곡 시간(초). 3초(ResultDelaySeconds)에 결과로 넘어간다.
/// - 0~0.2 띠 scaleY 0 → 1 (easeOutCubic) · 0~0.35 반짝임 (0.05초 22% → 0.35초 0)
/// - 0.08~0.38 글자 알파 0 → 1, 크기 1.15 → 1, 오른쪽 60px → 0 (easeOutCubic)
/// - 0~0.3 시계 나타남 · 0~1.0 분침 −720° → 0°, 시침 −60° → 0° (easeOutQuad) · 점선 원 초당 20°
/// - 0~0.8 라인 빛 기둥 0 → 620 (easeOutCubic) · 0.25~0.4 캡슐 나타남 · 0.3~1.0 개수 0 → 전체 (easeOutCubic)
/// - 1.0~1.35 12시 강조 p = sin(π·진행도): 시계 1 + 0.05p, 흰 테두리 · 12시 점 0.9p, 글자 1 + 0.08p · 밝기 +45%p, 반짝임 +10%p
/// - 1.0~1.55 퍼지는 링 170 → 280 (easeOutCubic), 알파 90% → 0 · 1.0~1.6 빛 줄기
/// - 1.1~ 빛 알갱이 30개 (시작 1.1~1.7초, 수명 0.9~1.4초, 위로 170px, 알파 sin(π·수명 진행도) × 90%)
/// - 2.4~3.0 사라짐: 전체 알파 → 0, 글자·캡슐 위로 16px, 라인 빛 기둥 35%, 콤보 숫자 40%
/// </summary>
public class ClearBannerTimeline
{
    public const float TotalSeconds = 3.0f;
    public const int MoteCount = 30;
    /// <summary>빛 알갱이 시드 (고정이라 테스트가 재현된다)</summary>
    public const int DefaultSeed = 1119;

    public const float BandDuration = 0.2f;
    public const float FlashPeakTime = 0.05f;
    public const float FlashEnd = 0.35f;
    public const float FlashPeak = 0.22f;
    public const float LogoStart = 0.08f;
    public const float LogoDuration = 0.3f;
    public const float LogoStartScale = 1.15f;
    public const float LogoStartOffsetX = 60f;
    public const float ClockFadeDuration = 0.3f;
    public const float HandsDuration = 1.0f;
    public const float MinuteStartAngle = -720f;
    public const float HourStartAngle = -60f;
    public const float DashesDegreesPerSecond = 20f;
    public const float PillarDuration = 0.8f;
    public const float PillarMaxHeight = 620f;
    public const float CapsuleFadeStart = 0.25f;
    public const float CapsuleFadeDuration = 0.15f;
    public const float CountStart = 0.3f;
    public const float CountEnd = 1.0f;
    public const float HighlightStart = 1.0f;
    public const float HighlightDuration = 0.35f;
    public const float BurstStart = 1.0f;
    public const float BurstDuration = 0.55f;
    public const float BurstEndScale = 280f / 170f;
    public const float ShineStart = 1.0f;
    public const float ShineDuration = 0.6f;
    public const float FadeStart = 2.4f;
    public const float FadeDuration = 0.6f;
    public const float FadeRise = 16f;
    public const float PillarEndAlpha = 0.35f;
    public const float ComboEndAlpha = 0.4f;
    public const float MoteRise = 170f;
    public const float MoteMaxAlpha = 0.9f;

    private readonly ClearMote[] motes;

    /// <summary>빛 알갱이 30개 (시드 고정)</summary>
    public ClearMote[] Motes => motes;

    public ClearBannerTimeline(int seed = DefaultSeed)
    {
        motes = CreateMotes(seed);
    }

    /// <summary>
    /// 빛 알갱이를 시드로 만든다: 시계 가운데 기준 x −140~+140, y 60~150 아래(UI 좌표 −60~−150), 크기 6~13,
    /// 시작 1.1~1.7초, 수명 0.9~1.4초, 둘 중 하나는 흰색.
    /// </summary>
    public static ClearMote[] CreateMotes(int seed)
    {
        System.Random random = new System.Random(seed);
        ClearMote[] result = new ClearMote[MoteCount];
        for (int i = 0; i < MoteCount; i++)
        {
            result[i] = new ClearMote
            {
                Start = new Vector2(Range(random, -140f, 140f), -Range(random, 60f, 150f)),
                Size = Range(random, 6f, 13f),
                StartTime = Range(random, 1.1f, 1.7f),
                Life = Range(random, 0.9f, 1.4f),
                White = random.NextDouble() < 0.5
            };
        }
        return result;
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    /// <summary>숨김 상태 (Init · 재생 전 · 끝난 뒤)</summary>
    public static readonly ClearBannerFrame Hidden = new ClearBannerFrame
    {
        Visible = false, RootAlpha = 0f, LogoScale = 1f, ClockScale = 1f, BurstScale = 1f,
        MinuteAngle = MinuteStartAngle, HourAngle = HourStartAngle, PillarAlpha = 1f, ComboAlpha = -1f
    };

    /// <summary>연출 시작부터 t초 뒤 모습. count = 끝에 보일 개수</summary>
    public ClearBannerFrame Evaluate(float t, int count)
    {
        if (t < 0f || t >= TotalSeconds) return Hidden;

        ClearBannerFrame f = Hidden;
        f.Visible = true;

        float fade = Progress(t, FadeStart, FadeDuration);
        f.RootAlpha = 1f - fade;

        f.BandScaleY = EaseOutCubic(Progress(t, 0f, BandDuration));

        // 반짝임: 0.05초에 22%까지 오르고 0.35초에 0
        if (t < FlashPeakTime) f.FlashAlpha = FlashPeak * t / FlashPeakTime;
        else if (t < FlashEnd) f.FlashAlpha = FlashPeak * (1f - (t - FlashPeakTime) / (FlashEnd - FlashPeakTime));

        // 12시 강조
        float p = 0f;
        if (t >= HighlightStart && t < HighlightStart + HighlightDuration)
            p = Mathf.Sin(Mathf.PI * (t - HighlightStart) / HighlightDuration);

        // 글자
        float logo = EaseOutCubic(Progress(t, LogoStart, LogoDuration));
        f.LogoAlpha = t < LogoStart ? 0f : logo;
        f.LogoScale = Mathf.Lerp(LogoStartScale, 1f, logo) * (1f + 0.08f * p);
        f.LogoOffsetX = LogoStartOffsetX * (1f - logo);
        f.LogoRise = FadeRise * fade;
        f.LogoBright = 0.45f * p;
        f.FlashAlpha += 0.10f * p;

        // 빛 줄기
        if (t >= ShineStart && t < ShineStart + ShineDuration)
        {
            f.ShineVisible = true;
            f.ShineProgress = EaseInOutQuad(Progress(t, ShineStart, ShineDuration));
        }

        // 시계
        f.ClockAlpha = Progress(t, 0f, ClockFadeDuration);
        f.ClockScale = 1f + 0.05f * p;
        float hands = EaseOutQuad(Progress(t, 0f, HandsDuration));
        f.MinuteAngle = MinuteStartAngle * (1f - hands);
        f.HourAngle = HourStartAngle * (1f - hands);
        f.DashesAngle = DashesDegreesPerSecond * t;
        f.HighlightAlpha = 0.9f * p;
        f.TopDotAlpha = 0.9f * p;
        if (t >= BurstStart && t < BurstStart + BurstDuration)
        {
            float x = Progress(t, BurstStart, BurstDuration);
            f.BurstScale = Mathf.Lerp(1f, BurstEndScale, EaseOutCubic(x));
            f.BurstAlpha = 0.9f * (1f - x);
        }

        // 캡슐 · 개수
        f.CapsuleAlpha = Progress(t, CapsuleFadeStart, CapsuleFadeDuration);
        f.CapsuleRise = FadeRise * fade;
        f.Count = CountAt(t, count);

        // 라인 빛 기둥 · 콤보
        f.PillarHeight = PillarMaxHeight * EaseOutCubic(Progress(t, 0f, PillarDuration));
        f.PillarAlpha = Mathf.Lerp(1f, PillarEndAlpha, fade);
        f.ComboAlpha = Mathf.Lerp(1f, ComboEndAlpha, fade);
        return f;
    }

    /// <summary>0.3~1.0초 개수 0 → count (easeOutCubic, 내림이라 정확히 1.0초에 count에 닿는다)</summary>
    public static int CountAt(float t, int count)
    {
        if (count <= 0) return 0;
        if (t >= CountEnd) return count;
        float e = EaseOutCubic(Progress(t, CountStart, CountEnd - CountStart));
        // 1.0초 바로 앞은 float 반올림으로 e가 1이 될 수 있어 전체 − 1까지만 둔다 (전체에는 정확히 1.0초에 닿는다)
        return Mathf.Min(count - 1, Mathf.FloorToInt(count * e));
    }

    /// <summary>i번째 알갱이의 알파 (안 보이면 0)와 위치 (시계 가운데 기준)</summary>
    public float MoteAt(int index, float t, out Vector2 position)
    {
        ClearMote m = motes[index];
        float age = m.Life > 0f ? (t - m.StartTime) / m.Life : 1f;
        position = m.Start + new Vector2(0f, MoteRise * Mathf.Clamp01(age));
        if (t < 0f || t >= TotalSeconds || age < 0f || age >= 1f) return 0f;
        return Mathf.Sin(Mathf.PI * age) * MoteMaxAlpha;
    }

    /// <summary>t초에 보이는 알갱이 수</summary>
    public int ActiveMoteCount(float t)
    {
        int n = 0;
        for (int i = 0; i < motes.Length; i++) if (MoteAt(i, t, out _) > 0f) n++;
        return n;
    }

    private static float Progress(float t, float start, float duration)
    {
        if (t <= start) return 0f;
        if (duration <= 0f) return 1f;
        return Mathf.Clamp01((t - start) / duration);
    }

    public static float EaseOutCubic(float x) { float y = 1f - x; return 1f - y * y * y; }

    public static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseInOutQuad(float x) => x < 0.5f ? 2f * x * x : 1f - 2f * (1f - x) * (1f - x);
}
