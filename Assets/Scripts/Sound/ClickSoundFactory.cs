using UnityEngine;

/// <summary>
/// 메트로놈 클릭 소리를 코드로 만든다 (음원 파일 없음, 라이선스 문제 없음).
/// 짧은 사인파에 빠르게 줄어드는 음량을 곱해 "틱" 소리를 낸다.
/// </summary>
public static class ClickSoundFactory
{
    /// <summary>만드는 소리의 샘플 레이트</summary>
    private const int SampleRate = 44100;

    /// <summary>음량 (0~1)</summary>
    private const float Amplitude = 0.6f;

    /// <summary>
    /// 클릭 소리를 만든다.
    /// </summary>
    /// <param name="frequency">음 높이 (Hz)</param>
    /// <param name="durationSeconds">길이 (초)</param>
    public static AudioClip Create(string name, float frequency, float durationSeconds)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * durationSeconds));
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = 1f - (float)i / sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * envelope * Amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
