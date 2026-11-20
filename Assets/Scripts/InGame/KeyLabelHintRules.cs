/// <summary>시작 키 글자 안내 한 시점의 알파: 키 글자와 키 칸 마름모 (둘이 동시에 0보다 크지 않다)</summary>
public readonly struct KeyHintAlpha
{
    public readonly float Label;
    public readonly float Diamond;

    public KeyHintAlpha(float label, float diamond)
    {
        Label = label;
        Diamond = diamond;
    }

    /// <summary>평소 (글자 없음, 마름모만): 안내가 끝난 뒤 · 다시 하기 · 이어 하기</summary>
    public static readonly KeyHintAlpha DiamondOnly = new KeyHintAlpha(0f, 1f);

    /// <summary>둘 다 숨김 (메트로놈 판 시작 ~ −4박 전)</summary>
    public static readonly KeyHintAlpha Hidden = new KeyHintAlpha(0f, 0f);

    /// <summary>글자만</summary>
    public static readonly KeyHintAlpha LabelOnly = new KeyHintAlpha(1f, 0f);

    /// <summary>글자와 마름모가 같이 보이면 true (있으면 안 된다)</summary>
    public bool IsOverlapping => Label > 0f && Diamond > 0f;

    public override string ToString() => $"글자 {Label:0.00} · 마름모 {Diamond:0.00}";
}

/// <summary>
/// 시작할 때 키 칸 키 글자 안내 (11/20 수정, 기획서 3-4 키 칸 · README 4절). 상태가 없는 순수 함수라 EditMode 테스트로 검사한다.
/// 글자와 마름모가 같이 보이는 순간이 없게 차례로 바꾼다 (글자 사라짐 → 마름모 나타남). 사라지고 나타나는 구간은 직선.
/// - 메트로놈이 있을 때(CountInRules.UsesCountIn, 박 = 카운트인 클릭과 같은 곡 시간):
///   −4박 전 둘 다 0 / −4 ~ −1박 글자 1 / −1 ~ −0.5박 글자 1 → 0 / −0.5 ~ 0박 마름모 0 → 1 / 0박 뒤 마름모 1.
/// - 메트로놈이 없을 때(중간부터 테스트 플레이, 판 시작 = 0초):
///   0 ~ 1.0초 글자 1 / 1.0 ~ 1.25초 글자 1 → 0 / 1.25 ~ 1.5초 마름모 0 → 1 / 1.5초 뒤 마름모 1.
/// - 다시 하기(F5 · 일시정지 재시작 · 결과의 다시 하기, InGameEntryData.isRetry)와 일시정지 후 이어 하기: 처음부터 글자 0 · 마름모 1.
/// 시간 상수는 GameConstants.Play.KeyHint….
/// </summary>
public static class KeyLabelHintRules
{
    /// <summary>
    /// 지금 글자 · 마름모 알파.
    /// </summary>
    /// <param name="usesCountIn">시작 메트로놈이 있는지</param>
    /// <param name="isRetry">다시 하기로 들어온 판인지</param>
    /// <param name="songTime">지금 곡 시간 (초, 메트로놈이 있을 때 쓴다)</param>
    /// <param name="beats">메트로놈 박 시각 (곡 시간, BeatTimes)</param>
    /// <param name="elapsedSeconds">판 시작 후 지난 곡 시간 (초, 메트로놈이 없을 때 쓴다)</param>
    public static KeyHintAlpha GetAlpha(bool usesCountIn, bool isRetry, double songTime, BeatTimes beats, double elapsedSeconds)
    {
        if (isRetry) return KeyHintAlpha.DiamondOnly;
        return usesCountIn ? WithCountIn(songTime, beats) : WithoutCountIn(elapsedSeconds);
    }

    /// <summary>판 시작(첫 Update 전)에 둘 모습: 다시 하기면 마름모만, 메트로놈 판이면 둘 다 숨김, 중간 시작이면 글자만</summary>
    public static KeyHintAlpha Initial(bool usesCountIn, bool isRetry)
    {
        if (isRetry) return KeyHintAlpha.DiamondOnly;
        return usesCountIn ? KeyHintAlpha.Hidden : KeyHintAlpha.LabelOnly;
    }

    /// <summary>메트로놈이 있을 때 (박 시각 기준)</summary>
    public static KeyHintAlpha WithCountIn(double songTime, BeatTimes beats)
    {
        if (songTime < beats.Show) return KeyHintAlpha.Hidden;
        return Sequence(songTime, beats.LabelFade, beats.DiamondFade, beats.End);
    }

    /// <summary>메트로놈이 없을 때 (판 시작 후 초)</summary>
    public static KeyHintAlpha WithoutCountIn(double elapsedSeconds)
    {
        if (elapsedSeconds < 0) return KeyHintAlpha.LabelOnly;
        return Sequence(elapsedSeconds, GameConstants.Play.KeyHintLabelFadeSeconds, GameConstants.Play.KeyHintDiamondFadeSeconds, GameConstants.Play.KeyHintEndSeconds);
    }

    /// <summary>[.., a) 글자 1 → [a, b) 글자 1 → 0 → [b, c) 마름모 0 → 1 → [c, ..) 마름모 1</summary>
    private static KeyHintAlpha Sequence(double t, double labelFade, double diamondFade, double end)
    {
        if (t < labelFade) return KeyHintAlpha.LabelOnly;
        if (t < diamondFade) return new KeyHintAlpha(1f - Ratio(t, labelFade, diamondFade), 0f);
        if (t < end) return new KeyHintAlpha(0f, Ratio(t, diamondFade, end));
        return KeyHintAlpha.DiamondOnly;
    }

    private static float Ratio(double t, double from, double to)
    {
        double length = to - from;
        if (length <= 0) return 1f;
        double r = (t - from) / length;
        return (float)(r < 0 ? 0 : r > 1 ? 1 : r);
    }

    /// <summary>안내가 끝났는지 (이후로는 늘 마름모만)</summary>
    public static bool IsOver(bool usesCountIn, bool isRetry, double songTime, BeatTimes beats, double elapsedSeconds)
    {
        if (isRetry) return true;
        return usesCountIn ? songTime >= beats.End : elapsedSeconds >= GameConstants.Play.KeyHintEndSeconds;
    }

    /// <summary>메트로놈이 있을 때 쓰는 박 시각 (곡 시간 초): −4박 보이기 시작 · −1박 글자 사라짐 · −0.5박 마름모 나타남 · 0박 끝</summary>
    public readonly struct BeatTimes
    {
        public readonly double Show, LabelFade, DiamondFade, End;

        public BeatTimes(double show, double labelFade, double diamondFade, double end)
        {
            Show = show;
            LabelFade = labelFade;
            DiamondFade = diamondFade;
            End = end;
        }

        /// <summary>박 → 곡 시간 변환(카운트인 클릭과 같은 TimingMap)으로 만든다</summary>
        public static BeatTimes From(TimingMap songTiming)
        {
            return new BeatTimes(
                songTiming.BeatToSeconds(GameConstants.Play.KeyHintShowBeat),
                songTiming.BeatToSeconds(GameConstants.Play.KeyHintLabelFadeBeat),
                songTiming.BeatToSeconds(GameConstants.Play.KeyHintDiamondFadeBeat),
                songTiming.BeatToSeconds(GameConstants.Play.KeyHintEndBeat));
        }
    }
}
