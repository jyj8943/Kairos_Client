using UnityEngine;

/// <summary>
/// 에디터 노래 재생 계산 모음. 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// </summary>
public static class EditorPlaybackMath
{
    /// <summary>박을 정수 박으로 볼 때의 허용 오차 (박)</summary>
    private const float BeatEpsilon = 0.0001f;

    /// <summary>노래를 예약할 음원 위치와 지연</summary>
    public readonly struct PlayStart
    {
        /// <summary>음원 안에서 재생을 시작할 위치 (초, 0 이상)</summary>
        public readonly double StartPositionSeconds;

        /// <summary>지금부터 재생 시작까지 기다리는 시간 (초)</summary>
        public readonly double DelaySeconds;

        public PlayStart(double startPositionSeconds, double delaySeconds)
        {
            StartPositionSeconds = startPositionSeconds;
            DelaySeconds = delaySeconds;
        }
    }

    /// <summary>
    /// 현재 박부터 재생할 때의 음원 위치와 지연.
    /// - 시작 시간(박 → 초, offset 포함)이 0 이상이면: 그 위치부터, 최소 지연만 두고 재생한다.
    /// - 0보다 앞(offset 이전)이면: 음원 0초부터 재생하고, 모자란 시간만큼 더 기다린다.
    /// 어느 쪽이든 재생을 예약한 순간의 곡 시간은 (시작 시간 − 최소 지연)이다.
    /// </summary>
    /// <param name="beat">재생을 시작할 박</param>
    /// <param name="timing">박 ↔ 시간 변환 (offset 포함)</param>
    public static PlayStart GetPlayStart(float beat, TimingMap timing)
    {
        double delay = GameConstants.Editor.PlaybackStartDelaySeconds;
        double startTime = timing.BeatToSeconds(beat);

        if (startTime >= 0) return new PlayStart(startTime, delay);
        return new PlayStart(0, delay - startTime);
    }

    /// <summary>
    /// 메트로놈 첫 클릭 박. 지금 박 위에 있으면 그 박, 박 중간이면 다음 정수 박.
    /// </summary>
    public static int GetFirstClickBeat(float currentBeat)
    {
        return Mathf.Max(0, Mathf.CeilToInt(currentBeat - BeatEpsilon));
    }

    /// <summary>
    /// 마디 첫 박이면 true (4/4 고정, 0·4·8…박).
    /// </summary>
    public static bool IsMeasureStart(int beat)
    {
        return beat % GameConstants.Editor.BeatsPerMeasure == 0;
    }

    /// <summary>
    /// 재생 중 현재 위치 선 밝기 (0~1). 박 바로 뒤에 가장 밝고 BeatFlashFraction 동안 줄어든다.
    /// </summary>
    public static float GetBeatFlash(float beat)
    {
        if (beat < 0f) return 0f;

        float fraction = beat - Mathf.Floor(beat);
        float flash = GameConstants.Editor.BeatFlashFraction;
        return fraction < flash ? 1f - fraction / flash : 0f;
    }
}
