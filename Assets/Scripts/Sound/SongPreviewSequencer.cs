using System;
using UnityEngine;

/// <summary>SongPreviewSequencer가 AudioSource에 시키는 일</summary>
public enum SongPreviewCommand
{
    /// <summary>음량만 맞춘다</summary>
    None,

    /// <summary>Clip을 Start 위치부터 (다시) 재생한다</summary>
    Start,

    /// <summary>멈춘다</summary>
    Stop
}

/// <summary>
/// 곡 미리 듣기 시간표 (11/25). 소리를 내지 않는 일반 클래스라 EditMode 테스트로 검사할 수 있다. SoundManager가 매 프레임 Tick(unscaledDeltaTime)을 부르고
/// 돌려받은 명령과 Volume을 미리 듣기 AudioSource에 적용한다.
/// - Play: 쉬는 중이면 바로 시작, 같은 곡을 재생 중이면 아무것도 하지 않음, 다른 곡이면 SwitchFadeOut 뒤 새 곡 시작
/// - SetDucked(true): 곡을 바꾸려고 고른 순간부터 지금 곡을 SwitchFadeOut 동안 0으로 줄인다 (멈추지 않고 조용히 흐른다). 다 줄었으면 다른 곡 Play는 바로 시작한다.
///   SetDucked(false)면 같은 속도로 되살린다 (훑다가 원래 곡으로 돌아옴)
/// - 구간 끝에 닿으면 시작 지점으로 돌아가 다시 페이드 인 (반복)
/// - Stop: fade초 동안 줄인 뒤 멈춘다 (0 이하면 바로). 기다리던 다음 곡도 버린다
/// - Tick은 할당하지 않는다
/// </summary>
public sealed class SongPreviewSequencer
{
    private enum Mode
    {
        Idle,
        Playing,
        FadingOut
    }

    private Mode mode = Mode.Idle;
    private double length;
    private double elapsed;
    private double fadeElapsed;
    private double fadeDuration;
    private float fadeFrom;
    private double duckGain = 1.0;
    private bool isDucked;
    private AudioClip pendingClip;
    private double pendingStart;
    private double pendingLength;

    /// <summary>지금 재생하는 곡 (멈췄으면 null)</summary>
    public AudioClip Clip { get; private set; }

    /// <summary>미리 듣기 시작 위치 (초)</summary>
    public double Start { get; private set; }

    /// <summary>구간 안에서 지난 시간 (초)</summary>
    public double Elapsed => elapsed;

    /// <summary>음량 계수 (0 ~ 1, 설정 음량과 따로)</summary>
    public float Volume { get; private set; }

    /// <summary>재생 중이거나 줄이는 중이면 true</summary>
    public bool IsActive => mode != Mode.Idle;

    /// <summary>멈추거나 바꾸려고 줄이는 중이면 true</summary>
    public bool IsFadingOut => mode == Mode.FadingOut;

    /// <summary>구간을 되돌아간 횟수 (확인용)</summary>
    public int LoopCount { get; private set; }

    /// <summary>곡을 요청한다.</summary>
    public SongPreviewCommand Play(AudioClip clip, double start, double length)
    {
        if (clip == null || length <= 0) return SongPreviewCommand.None;
        if (mode == Mode.Idle) return Begin(clip, start, length);
        if (mode == Mode.Playing && clip == Clip)
        {
            isDucked = false;
            return SongPreviewCommand.None;
        }
        if (mode == Mode.Playing && duckGain <= 0) return Begin(clip, start, length);

        pendingClip = clip;
        pendingStart = start;
        pendingLength = length;
        FadeOut(GameConstants.SongPreview.SwitchFadeOut * duckGain);
        return SongPreviewCommand.None;
    }

    /// <summary>다른 곡을 고른 순간 지금 곡을 줄이기 시작하거나(true), 원래 곡으로 돌아와 되살린다(false). 재생 중이 아니면 아무것도 하지 않는다</summary>
    public void SetDucked(bool ducked)
    {
        if (mode == Mode.Playing) isDucked = ducked;
    }

    /// <summary>멈춘다 (fadeSeconds가 0 이하면 바로).</summary>
    public SongPreviewCommand Stop(double fadeSeconds)
    {
        pendingClip = null;
        if (mode == Mode.Idle) return SongPreviewCommand.None;
        if (fadeSeconds <= 0) return End();

        FadeOut(fadeSeconds);
        return SongPreviewCommand.None;
    }

    /// <summary>시간을 흘린다.</summary>
    public SongPreviewCommand Tick(double deltaTime)
    {
        switch (mode)
        {
            case Mode.Playing:
                elapsed += deltaTime;
                double step = deltaTime / GameConstants.SongPreview.SwitchFadeOut;
                duckGain = isDucked ? Math.Max(0, duckGain - step) : Math.Min(1, duckGain + step);
                if (elapsed >= length)
                {
                    LoopCount++;
                    elapsed = 0;
                    Volume = 0f;
                    return SongPreviewCommand.Start;
                }
                Volume = SongPreviewRules.GetVolume(elapsed, length) * (float)duckGain;
                return SongPreviewCommand.None;

            case Mode.FadingOut:
                fadeElapsed += deltaTime;
                if (fadeElapsed < fadeDuration)
                {
                    Volume = fadeFrom * (float)(1.0 - fadeElapsed / fadeDuration);
                    return SongPreviewCommand.None;
                }
                return pendingClip != null ? Begin(pendingClip, pendingStart, pendingLength) : End();

            default:
                return SongPreviewCommand.None;
        }
    }

    private SongPreviewCommand Begin(AudioClip clip, double start, double newLength)
    {
        pendingClip = null;
        Clip = clip;
        Start = start;
        length = newLength;
        elapsed = 0;
        LoopCount = 0;
        Volume = 0f;
        duckGain = 1.0;
        isDucked = false;
        mode = Mode.Playing;
        return SongPreviewCommand.Start;
    }

    private SongPreviewCommand End()
    {
        pendingClip = null;
        Clip = null;
        Volume = 0f;
        mode = Mode.Idle;
        return SongPreviewCommand.Stop;
    }

    /// <summary>지금 음량에서 0까지 줄이기 시작한다. 이미 줄이는 중이면 이어서 줄인다 (더 일찍 끝나는 쪽)</summary>
    private void FadeOut(double seconds)
    {
        if (mode == Mode.FadingOut)
        {
            fadeDuration = Math.Min(fadeDuration, fadeElapsed + seconds);
            return;
        }
        mode = Mode.FadingOut;
        fadeFrom = Volume;
        fadeElapsed = 0;
        fadeDuration = seconds;
    }
}
