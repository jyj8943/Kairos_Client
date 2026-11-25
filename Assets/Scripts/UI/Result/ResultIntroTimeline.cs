using UnityEngine;
using C = GameConstants.ResultIntro;

/// <summary>결과 등장 연출 요소 하나의 모습 (알파 · 위치 오프셋(px, +x 오른쪽 · +y 위) · 크기)</summary>
public struct ResultIntroElement
{
    public float Alpha;
    public Vector2 Offset;
    public float Scale;

    public static readonly ResultIntroElement Shown = new ResultIntroElement { Alpha = 1f, Offset = Vector2.zero, Scale = 1f };
}

/// <summary>결과 등장 연출 한 프레임의 값 (ResultIntroTimeline.Evaluate)</summary>
public struct ResultIntroFrame
{
    public float Time;
    public ResultIntroElement Album, Title, Artist, ChartLine;
    public ResultIntroElement Score, NewScore, Best;
    /// <summary>점수 숫자 진행도 (0 ~ 1, 이징 적용)</summary>
    public float ScoreCount;
    /// <summary>판정 줄 PERFECT · GREAT · GOOD · MISS</summary>
    public ResultIntroElement Judge0, Judge1, Judge2, Judge3;
    public float JudgeCount0, JudgeCount1, JudgeCount2, JudgeCount3;
    public ResultIntroElement FastSlow;
    /// <summary>FAST/SLOW 막대 폭 · 개수 진행도</summary>
    public float FastSlowProgress;
    public ResultIntroElement Rate, NewRate, Combo, FullCombo, NewCombo;
    public float RateValue, ComboValue;
    /// <summary>랭크 도장 (알파 · 크기)과 번쩍임 세기 (0 ~ RankFlashBoost, 밝기에 더하는 양)</summary>
    public ResultIntroElement Rank;
    public float RankFlash;
    /// <summary>카이로스: Talk(입 벌림) 알파 · 위로 튀는 오프셋 (px, +y 위)</summary>
    public float KairosTalkAlpha;
    public float KairosOffsetY;
    /// <summary>카이로스 대화창 (알파 · 크기)</summary>
    public ResultIntroElement Dialog;
    /// <summary>연출이 끝났으면 true (t ≥ EndSeconds)</summary>
    public bool IsFinished;

    public ResultIntroElement GetJudge(int row) => row == 0 ? Judge0 : row == 1 ? Judge1 : row == 2 ? Judge2 : Judge3;
    public float GetJudgeCount(int row) => row == 0 ? JudgeCount0 : row == 1 ? JudgeCount1 : row == 2 ? JudgeCount2 : JudgeCount3;
}

/// <summary>
/// 결과 화면 등장 연출 시간표 (기획서 3-5 「결과」 등장 연출 · 🔧 시간표, README 2절). 상태가 없는 순수 함수라 EditMode 테스트로 검사한다.
/// - t = OnEnter부터 지난 unscaled 시간 (초). 이징은 easeOutCubic (UIEasing.OutCubic), 카이로스 Talk 알파만 직선 · 튐은 sin. 시간 상수는 GameConstants.ResultIntro.
/// - 단계를 빼는 판단(NEW RECORD · FC가 없음)은 화면이 한다. 시간표는 그 단계가 있든 없든 같은 값을 낸다 (뒤 단계가 당겨지지 않는다).
/// - Finished = 모든 요소 끝 상태 (최종값 · 크기 1 · 번쩍임 없음). 건너뛰기와 테스트 플레이 결과가 쓴다.
/// </summary>
public static class ResultIntroTimeline
{
    /// <summary>단계 이름과 시작 시각 (로그 · 테스트용, README 표 순서)</summary>
    public static readonly (string Name, float Start)[] Stages =
    {
        ("앨범", C.AlbumStart), ("제목", C.TitleStart), ("아티스트", C.ArtistStart), ("난이도 줄", C.ChartLineStart),
        ("SCORE", C.ScoreStart), ("점수 숫자", C.ScoreCountStart), ("NEW RECORD 점수", C.NewScoreStart), ("BEST", C.BestStart),
        ("PERFECT 줄", JudgeRowStart(0)), ("GREAT 줄", JudgeRowStart(1)), ("GOOD 줄", JudgeRowStart(2)), ("MISS 줄", JudgeRowStart(3)),
        ("FAST/SLOW", C.FastSlowStart), ("RATE", C.RateStart), ("NEW RECORD RATE", C.NewRateStart), ("MAX COMBO", C.ComboStart),
        ("FC", C.FullComboStart), ("NEW RECORD 콤보", C.NewComboStart), ("랭크", C.RankStart), ("카이로스 · 대화창", C.KairosTalkStart), ("끝", C.EndSeconds)
    };

    /// <summary>판정 줄 시작 시각 (줄 번호 0 = PERFECT)</summary>
    public static float JudgeRowStart(int row) => C.JudgeRowStart + C.JudgeRowInterval * row;

    /// <summary>구간 진행도 q (0 ~ 1)</summary>
    public static float Progress(float t, float start, float duration) => UIEasing.Progress(t, start, duration);

    /// <summary>이징 진행도 = easeOutCubic(q)</summary>
    public static float Eased(float t, float start, float duration) => UIEasing.OutCubic(Progress(t, start, duration));

    /// <summary>알파 + 위치 이동 (offset px에서 제자리로)</summary>
    public static ResultIntroElement Slide(float t, float start, float duration, Vector2 fromOffset)
    {
        float e = Eased(t, start, duration);
        return new ResultIntroElement { Alpha = e, Offset = fromOffset * (1f - e), Scale = 1f };
    }

    /// <summary>알파만</summary>
    public static ResultIntroElement Fade(float t, float start, float duration)
    {
        return new ResultIntroElement { Alpha = Eased(t, start, duration), Offset = Vector2.zero, Scale = 1f };
    }

    /// <summary>톡 튀어나옴: 크기 0.6 → 1.15 (앞 절반) → 1, 알파는 앞 절반 동안 0 → 1. 시작 전에는 알파 0</summary>
    public static ResultIntroElement Pop(float t, float start, float duration)
    {
        float q = Progress(t, start, duration);
        if (t < start) return new ResultIntroElement { Alpha = 0f, Offset = Vector2.zero, Scale = C.PopStartScale };
        float scale = q < C.PopPeakAt
            ? Mathf.Lerp(C.PopStartScale, C.PopPeakScale, UIEasing.OutCubic(q / C.PopPeakAt))
            : Mathf.Lerp(C.PopPeakScale, 1f, UIEasing.OutCubic((q - C.PopPeakAt) / (1f - C.PopPeakAt)));
        float alpha = UIEasing.OutCubic(Mathf.Clamp01(q / C.PopPeakAt));
        return new ResultIntroElement { Alpha = alpha, Offset = Vector2.zero, Scale = scale };
    }

    /// <summary>랭크 도장: 크기 2.0 → 0.88 (0.55 지점) → 1.0, 알파 처음 1/3 동안 0 → 1</summary>
    public static ResultIntroElement Stamp(float t)
    {
        if (t < C.RankStart) return new ResultIntroElement { Alpha = 0f, Offset = Vector2.zero, Scale = C.RankStartScale };
        float q = Progress(t, C.RankStart, C.RankDuration);
        float scale = q < C.RankImpactAt
            ? Mathf.Lerp(C.RankStartScale, C.RankSquashScale, UIEasing.OutCubic(q / C.RankImpactAt))
            : Mathf.Lerp(C.RankSquashScale, 1f, UIEasing.OutCubic((q - C.RankImpactAt) / (1f - C.RankImpactAt)));
        float alpha = UIEasing.OutCubic(Mathf.Clamp01(q / C.RankFadeFraction));
        return new ResultIntroElement { Alpha = alpha, Offset = Vector2.zero, Scale = scale };
    }

    /// <summary>랭크 번쩍임 세기: 0.55 지점에서 +80%, 앞뒤 RankFlashHalfWidth(진행도) 동안 직선으로 0. 끝(q = 1)에는 항상 0</summary>
    public static float RankFlash(float t)
    {
        if (t < C.RankStart) return 0f;
        float q = Progress(t, C.RankStart, C.RankDuration);
        if (q >= 1f) return 0f;
        float k = 1f - Mathf.Abs(q - C.RankImpactAt) / C.RankFlashHalfWidth;
        return C.RankFlashBoost * Mathf.Clamp01(k);
    }

    /// <summary>카이로스 Talk 알파: t0부터 KairosTalkFade 동안 0 → 1 (직선)</summary>
    public static float KairosTalkAlpha(float t) => Progress(t, C.KairosTalkStart, C.KairosTalkFade);

    /// <summary>카이로스 튐: t0부터 KairosBounce 동안 위로 KairosBouncePx · sin(π · q). 시작 전 · 끝난 뒤는 정확히 0</summary>
    public static float KairosOffsetY(float t)
    {
        float q = Progress(t, C.KairosTalkStart, C.KairosBounce);
        return q <= 0f || q >= 1f ? 0f : C.KairosBouncePx * Mathf.Sin(Mathf.PI * q);
    }

    /// <summary>카이로스 대화창: t0부터 DialogDuration 동안 알파 0 → 1, 크기 DialogStartScale → 1</summary>
    public static ResultIntroElement Dialog(float t)
    {
        float e = Eased(t, C.KairosTalkStart, C.DialogDuration);
        return new ResultIntroElement { Alpha = e, Offset = Vector2.zero, Scale = Mathf.Lerp(C.DialogStartScale, 1f, e) };
    }

    /// <summary>t초의 모든 요소 값</summary>
    public static ResultIntroFrame Evaluate(float t)
    {
        if (t >= C.EndSeconds) return Finished;

        Vector2 left30 = new Vector2(-C.SongInfoOffsetPx, 0f);
        Vector2 down16 = new Vector2(0f, -C.ScoreOffsetPx);
        Vector2 left24 = new Vector2(-C.JudgeRowOffsetPx, 0f);
        Vector2 down12 = new Vector2(0f, -C.CardOffsetPx);

        ResultIntroFrame f = new ResultIntroFrame { Time = t };
        float albumE = Eased(t, C.AlbumStart, C.SongInfoDuration);
        f.Album = new ResultIntroElement { Alpha = albumE, Offset = Vector2.zero, Scale = Mathf.Lerp(C.AlbumStartScale, 1f, albumE) };
        f.Title = Slide(t, C.TitleStart, C.SongInfoDuration, left30);
        f.Artist = Slide(t, C.ArtistStart, C.SongInfoDuration, left30);
        f.ChartLine = Slide(t, C.ChartLineStart, C.SongInfoDuration, left30);

        f.Score = Slide(t, C.ScoreStart, C.ScoreDuration, down16);
        f.ScoreCount = Eased(t, C.ScoreCountStart, C.ScoreCountDuration);
        f.NewScore = Pop(t, C.NewScoreStart, C.PopDuration);
        f.Best = Fade(t, C.BestStart, C.BestDuration);

        f.Judge0 = Slide(t, JudgeRowStart(0), C.JudgeRowDuration, left24);
        f.Judge1 = Slide(t, JudgeRowStart(1), C.JudgeRowDuration, left24);
        f.Judge2 = Slide(t, JudgeRowStart(2), C.JudgeRowDuration, left24);
        f.Judge3 = Slide(t, JudgeRowStart(3), C.JudgeRowDuration, left24);
        f.JudgeCount0 = Eased(t, JudgeRowStart(0), C.JudgeCountDuration);
        f.JudgeCount1 = Eased(t, JudgeRowStart(1), C.JudgeCountDuration);
        f.JudgeCount2 = Eased(t, JudgeRowStart(2), C.JudgeCountDuration);
        f.JudgeCount3 = Eased(t, JudgeRowStart(3), C.JudgeCountDuration);

        f.FastSlow = Fade(t, C.FastSlowStart, C.FastSlowFadeDuration);
        f.FastSlowProgress = Eased(t, C.FastSlowStart, C.FastSlowDuration);

        f.Rate = Slide(t, C.RateStart, C.CardDuration, down12);
        f.RateValue = Eased(t, C.RateStart, C.CardValueDuration);
        f.NewRate = Pop(t, C.NewRateStart, C.PopDuration);
        f.Combo = Slide(t, C.ComboStart, C.CardDuration, down12);
        f.ComboValue = Eased(t, C.ComboStart, C.CardValueDuration);
        f.FullCombo = Pop(t, C.FullComboStart, C.FullComboDuration);
        f.NewCombo = Pop(t, C.NewComboStart, C.PopDuration);

        f.Rank = Stamp(t);
        f.RankFlash = RankFlash(t);
        f.KairosTalkAlpha = KairosTalkAlpha(t);
        f.KairosOffsetY = KairosOffsetY(t);
        f.Dialog = Dialog(t);
        f.IsFinished = false;
        return f;
    }

    /// <summary>끝 상태: 모든 요소 알파 1 · 제자리 · 크기 1, 숫자 최종값, 번쩍임 없음</summary>
    public static ResultIntroFrame Finished
    {
        get
        {
            ResultIntroElement s = ResultIntroElement.Shown;
            return new ResultIntroFrame
            {
                Time = C.EndSeconds,
                Album = s, Title = s, Artist = s, ChartLine = s, Score = s, NewScore = s, Best = s,
                Judge0 = s, Judge1 = s, Judge2 = s, Judge3 = s, JudgeCount0 = 1f, JudgeCount1 = 1f, JudgeCount2 = 1f, JudgeCount3 = 1f,
                FastSlow = s, FastSlowProgress = 1f, Rate = s, NewRate = s, Combo = s, FullCombo = s, NewCombo = s,
                ScoreCount = 1f, RateValue = 1f, ComboValue = 1f, Rank = s, RankFlash = 0f,
                KairosTalkAlpha = 1f, KairosOffsetY = 0f, Dialog = s, IsFinished = true
            };
        }
    }

    /// <summary>정수 값 올라감 (0 → 최종값, 진행도 1이면 정확히 최종값)</summary>
    public static int CountInt(int final, float progress)
    {
        if (progress >= 1f) return final;
        return Mathf.Clamp(Mathf.RoundToInt(final * progress), 0, final);
    }

    /// <summary>실수 값 올라감 (RATE %, 진행도 1이면 정확히 최종값)</summary>
    public static double CountDouble(double final, float progress)
    {
        return progress >= 1f ? final : final * progress;
    }
}
