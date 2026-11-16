using UnityEngine;

/// <summary>
/// 노래·효과음 재생 전역 매니저. Managers_DontDestroy 프리팹의 "SoundManager" 오브젝트에 붙이고, Bootstrapper가 Init()을 호출한다.
/// - 노래는 PlayScheduled로 DSP 시각에 맞춰 시작하고, 곡 시간(SongTime)은 MusicClock이 dspTime 기준으로 계산한다.
/// - 판정·노트 위치는 SongTime을 쓴다. AudioSource.time은 기준으로 쓰지 않는다 (기획서: 판정 기준은 DSP 시간).
/// - 다른 스크립트보다 먼저 Update해 그 프레임의 SongTime을 맞춰 둔다.
/// - 음량: 노래·BGM = 메인 × 음악, 키음·효과음(예약 효과음 포함) = 메인 × 효과음 (ApplyVolumes). 설정 창이 MasterVolume·MusicVolume·SfxVolume을 바꾼다.
///   앞으로 추가하는 BGM·효과음도 따로 AudioSource를 두지 말고 이 매니저를 거쳐 재생해야 음량이 적용된다.
/// - 재생 배율(PlaybackRate, 기본 1.0): 에디터 테스트를 빠르게 돌릴 때만 바꾼다 (SetPlaybackRateForTests, Unity 에디터 전용).
///   노래 pitch, 곡 시간(MusicClock), 곡 시간 ↔ DSP 시각, 입력 시각 → 곡 시간이 모두 같은 배율을 따른다. 키음·효과음 pitch는 바꾸지 않는다.
///   게임 코드·설정 창에서는 바꾸지 않는다. 1.0이면 배율이 없던 때와 동작·숫자가 같다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SoundManager : MonoSingleton<SoundManager>, IGameModule
{
    [Header("AudioSource")]
    [Tooltip("노래용 (playOnAwake·loop 끔)")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("효과음용. 돌아가며 쓴다")]
    [SerializeField] private AudioSource[] sfxSources;
    [Tooltip("DSP 시각에 예약하는 효과음용 (메트로놈 등). 소스 하나에 예약 하나씩, 돌아가며 쓴다")]
    [SerializeField] private AudioSource[] scheduledSfxSources;

    [Header("음량 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private readonly MusicClock clock = new MusicClock();

#if UNITY_EDITOR
    /// <summary>
    /// 테스트가 정한 재생 배율. 플레이 모드에 들어갈 때 도메인을 다시 불러오지 않는 설정이라(Enter Play Mode Options)
    /// 정적 값이 플레이 모드를 넘어 남는다. 테스트 실행이 끝나면 테스트 쪽(TestSpeedRunCallback)이 1.0으로 되돌린다.
    /// </summary>
    private static double editorTestPlaybackRate = 1.0;

    /// <summary>
    /// 테스트 전용 (Unity 에디터): 재생 배율을 바꾼다. 게임 코드·설정 창에서 부르지 않는다.
    /// 매니저가 있으면 바로 적용하고(재생 중이면 곡 시간이 튀지 않게), 없으면 다음 Init부터 적용한다.
    /// </summary>
    /// <param name="rate">배율 (0 이하면 1.0)</param>
    public static void SetPlaybackRateForTests(double rate)
    {
        editorTestPlaybackRate = rate > 0 ? rate : 1.0;
        if (HasInstance) Instance.ApplyPlaybackRate();
    }

    /// <summary>지금 재생 배율 (정적, 매니저가 없어도 읽는다. 테스트의 마감 시간 계산용)</summary>
    public static double CurrentPlaybackRate => editorTestPlaybackRate;
#else
    /// <summary>지금 재생 배율 (빌드에서는 항상 1.0)</summary>
    public static double CurrentPlaybackRate => 1.0;
#endif

    /// <summary>재생 배율 (실제 1초에 흐르는 곡 시간, 기본 1.0). 테스트에서만 바뀐다</summary>
    public double PlaybackRate => CurrentPlaybackRate;
    private int nextSfxIndex;
    private int nextScheduledSfxIndex;

    /// <summary>곡 시간 (초). 노래 시작 전 준비 시간에는 음수, 재생 중이 아니면 0</summary>
    public double SongTime => clock.SongTime;

    /// <summary>노래가 예약됐거나 재생 중이면 true (일시정지 중에는 false)</summary>
    public bool IsMusicScheduledOrPlaying => clock.IsRunning && !clock.IsPaused;

    /// <summary>노래가 일시정지 중이면 true</summary>
    public bool IsMusicPaused => clock.IsPaused;

    /// <summary>재개 직후 곡 시간을 붙잡아 두는 중이면 true (이때는 곡 시간과 음원 위치가 일부러 다르다)</summary>
    public bool IsClockHolding => clock.IsHolding;

    /// <summary>PlaySfx로 효과음을 재생한 횟수 (확인용, 게임 실행 후 누적)</summary>
    public int SfxPlayCount { get; private set; }

    /// <summary>지금 소리를 내는 중인 효과음 AudioSource 수 (확인용)</summary>
    public int ActiveSfxSourceCount
    {
        get
        {
            int count = 0;
            if (sfxSources == null) return 0;
            foreach (AudioSource source in sfxSources)
            {
                if (source != null && source.isPlaying) count++;
            }
            return count;
        }
    }

    /// <summary>예약 효과음 소스가 모두 쓰는 중이라 다른 예약을 덮어쓴 횟수 (확인용, 0이어야 정상)</summary>
    public int ScheduledSfxOverwriteCount { get; private set; }

    /// <summary>지금 예약됐거나 소리를 내는 중인 예약 효과음 소스 수 (확인용)</summary>
    public int ActiveScheduledSfxCount
    {
        get
        {
            int active = 0;
            if (scheduledSfxSources == null) return 0;
            foreach (AudioSource source in scheduledSfxSources)
            {
                if (source != null && source.isPlaying) active++;
            }
            return active;
        }
    }

    /// <summary>효과음 AudioSource 수 (확인용)</summary>
    public int SfxSourceCount => sfxSources != null ? sfxSources.Length : 0;

    /// <summary>노래 AudioSource가 소리를 내는 중이면 true (확인용)</summary>
    public bool IsMusicSourcePlaying => musicSource != null && musicSource.isPlaying;

    /// <summary>
    /// 노래 AudioSource의 실제 재생 위치 (초, timeSamples 기준). 판정·노트 위치에는 쓰지 않고 확인용으로만 쓴다.
    /// 재생 중이 아니면 0.
    /// </summary>
    public double MusicSourceTime =>
        musicSource != null && musicSource.clip != null && musicSource.isPlaying
            ? (double)musicSource.timeSamples / musicSource.clip.frequency
            : 0;

    public float MasterVolume
    {
        get => masterVolume;
        set { masterVolume = Mathf.Clamp01(value); ApplyVolumes(); }
    }

    public float MusicVolume
    {
        get => musicVolume;
        set { musicVolume = Mathf.Clamp01(value); ApplyVolumes(); }
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set { sfxVolume = Mathf.Clamp01(value); ApplyVolumes(); }
    }

    /// <summary>
    /// AudioSource 설정을 확인하고 음량을 적용한다.
    /// </summary>
    public void Init()
    {
        if (musicSource == null)
        {
            Debug.LogError("[SoundManager] musicSource가 연결되지 않았습니다.", this);
        }
        else
        {
            musicSource.playOnAwake = false;
            musicSource.loop = false;
        }

        if (sfxSources == null || sfxSources.Length == 0)
            Debug.LogWarning("[SoundManager] sfxSources가 없어 효과음을 재생할 수 없습니다.", this);
        if (scheduledSfxSources == null || scheduledSfxSources.Length == 0)
            Debug.LogWarning("[SoundManager] scheduledSfxSources가 없어 효과음을 예약할 수 없습니다.", this);

        nextSfxIndex = 0;
        nextScheduledSfxIndex = 0;
        CancelScheduledSfx();
        StopMusic();
        ApplyVolumes();
        ApplyPlaybackRate();
    }

    /// <summary>재생 배율을 노래 pitch와 곡 시간 계산에 적용한다 (배율이 같으면 아무것도 바꾸지 않는다)</summary>
    private void ApplyPlaybackRate()
    {
        double rate = PlaybackRate;
        if (musicSource != null) musicSource.pitch = (float)rate;
        clock.SetRate(rate, AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble);
    }

    private void Update()
    {
        clock.Tick(AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble);
    }

    /// <summary>
    /// 지금 DSP 시각 + delaySeconds에 노래를 시작하도록 예약한다. 곡 시간은 −delay에서 시작해 늘어난다.
    /// delaySeconds는 곡 시간 기준이다: 실제로 기다리는 시간 = delay ÷ 재생 배율 (배율 1.0이면 같다).
    /// 배율을 올려도 예약 여유가 오디오 버퍼보다 짧아져 시작이 늦지 않게, 실제 대기는 min(delay, SeekStartDelaySeconds)보다 짧아지지 않는다
    /// (배율 1.0이면 delay 그대로).
    /// </summary>
    /// <param name="startPositionSeconds">음원 안에서 시작할 위치 (초)</param>
    public void PlayMusicScheduled(AudioClip clip, double delaySeconds, double startPositionSeconds = 0)
    {
        if (musicSource == null || clip == null)
        {
            Debug.LogError($"[SoundManager] 노래를 재생할 수 없습니다: musicSource={(musicSource != null)}, clip={(clip != null)}", this);
            return;
        }

        // 이전 재생·정지 상태와 상관없이 지정한 위치에서 시작하도록, 멈춘 뒤 클립을 다시 넣고 시작 위치를 샘플 단위로 넣는다
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.timeSamples = ToSamples(clip, startPositionSeconds);

        ApplyPlaybackRate();
        double dspNow = AudioSettings.dspTime;
        double delay = System.Math.Max(0, delaySeconds);
        double realDelay = System.Math.Max(delay / PlaybackRate, System.Math.Min(delay, SeekStartDelaySeconds));
        double startDsp = dspNow + realDelay;
        musicSource.PlayScheduled(startDsp);
        clock.Schedule(startDsp, startPositionSeconds, dspNow, Time.realtimeSinceStartupAsDouble);
    }

    /// <summary>
    /// 노래를 일시정지한다. 곡 시간도 멈춘다.
    /// 아직 시작되지 않은 예약 재생(준비 시간)도 확실히 취소되도록 AudioSource는 Pause가 아니라 Stop한다
    /// (재개할 때 ResumeMusic이 멈춘 위치로 다시 예약한다).
    /// </summary>
    public void PauseMusic()
    {
        if (!clock.IsRunning || clock.IsPaused) return;

        clock.Pause(AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble);
        if (musicSource != null) musicSource.Stop();
    }

    /// <summary>
    /// 일시정지한 노래를 이어서 재생한다. 곡 시간은 멈춘 값에서 이어진다.
    /// - 준비 시간에 멈췄으면: 남은 대기 시간 뒤에 시작 위치부터 재생하도록 다시 예약한다.
    /// - 재생 중에 멈췄으면: 멈춘 곡 시간 위치부터 재생하도록 다시 예약한다.
    /// 예약은 최소 SeekStartDelaySeconds 뒤로 잡고, 그동안 곡 시간은 멈춘 값에 머문다.
    /// </summary>
    public void ResumeMusic()
    {
        if (!clock.IsPaused) return;

        double position = clock.ResumePosition;
        clock.Resume(AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble, SeekStartDelaySeconds);

        if (musicSource == null || musicSource.clip == null) return;
        if (position >= musicSource.clip.length) return;

        musicSource.Stop();
        musicSource.timeSamples = ToSamples(musicSource.clip, position);
        musicSource.PlayScheduled(clock.SongTimeToDsp(position));
    }

    /// <summary>노래를 멈추고 곡 시간을 0으로 되돌린다.</summary>
    public void StopMusic()
    {
        clock.Stop();
        if (musicSource != null) musicSource.Stop();
    }

    /// <summary>
    /// 지금 노래의 seconds 위치부터 다시 재생한다 (곧바로 예약 재생). 일시정지 중이었으면 재생 상태가 된다.
    /// (에디터 재생에서 쓸 예정)
    /// </summary>
    public void SeekMusic(double seconds)
    {
        if (musicSource == null || musicSource.clip == null) return;

        PlayMusicScheduled(musicSource.clip, SeekStartDelaySeconds, seconds);
    }

    /// <summary>
    /// 입력 이벤트 시각(realtimeSinceStartup 기준)을 곡 시간으로 바꾼다 (재생 배율 반영).
    /// </summary>
    public double GetSongTimeAtRealtime(double realtime)
    {
        return clock.GetSongTimeAtRealtime(realtime);
    }

    /// <summary>
    /// 효과음을 재생한다. 효과음 AudioSource를 돌아가며 쓴다.
    /// </summary>
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSources == null || sfxSources.Length == 0) return;

        AudioSource source = sfxSources[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxSources.Length;
        if (source == null) return;

        source.PlayOneShot(clip);
        SfxPlayCount++;
    }

    /// <summary>
    /// 곡 시간을 DSP 시각으로 바꾼다. 효과음을 박에 맞춰 예약할 때 쓴다 (노래가 예약·재생 중일 때만 의미가 있다).
    /// </summary>
    public double GetDspTimeOfSongTime(double songTime)
    {
        return clock.SongTimeToDsp(songTime);
    }

    /// <summary>
    /// 효과음을 DSP 시각에 예약 재생한다 (메트로놈·에디터 키음). 예약 전용 소스를 돌아가며 쓴다.
    /// - 다음 차례부터 쉬고 있는 소스(예약·재생 중이 아닌 것)를 먼저 찾아 쓴다.
    /// - 모두 쓰는 중이면 다음 차례 소스를 덮어쓰고(그 예약은 빠진다) ScheduledSfxOverwriteCount를 늘린다.
    /// </summary>
    /// <returns>예약했으면 true</returns>
    public bool ScheduleSfx(AudioClip clip, double dspTime)
    {
        if (clip == null || scheduledSfxSources == null || scheduledSfxSources.Length == 0) return false;

        int count = scheduledSfxSources.Length;
        int chosen = -1;
        for (int i = 0; i < count; i++)
        {
            int index = (nextScheduledSfxIndex + i) % count;
            AudioSource candidate = scheduledSfxSources[index];
            if (candidate != null && !candidate.isPlaying)
            {
                chosen = index;
                break;
            }
        }

        if (chosen < 0)
        {
            chosen = nextScheduledSfxIndex;
            ScheduledSfxOverwriteCount++;
        }

        nextScheduledSfxIndex = (chosen + 1) % count;
        AudioSource source = scheduledSfxSources[chosen];
        if (source == null) return false;

        source.Stop();
        source.clip = clip;
        source.volume = masterVolume * sfxVolume;
        source.PlayScheduled(dspTime);
        return true;
    }

    /// <summary>
    /// 예약한 효과음을 모두 취소한다 (재생 중인 것도 멈춘다).
    /// </summary>
    public void CancelScheduledSfx()
    {
        if (scheduledSfxSources == null) return;
        foreach (AudioSource source in scheduledSfxSources)
        {
            if (source != null) source.Stop();
        }
    }

    /// <summary>
    /// 음원 안 위치(초)를 샘플 위치로 바꾼다. 0 ~ 마지막 샘플로 맞춘다.
    /// </summary>
    public static int ToSamples(AudioClip clip, double positionSeconds)
    {
        if (clip == null || clip.samples <= 0) return 0;

        long samples = (long)System.Math.Round(positionSeconds * clip.frequency);
        return (int)System.Math.Max(0, System.Math.Min(clip.samples - 1, samples));
    }

    /// <summary>Seek 후 예약 재생까지 두는 시간 (초). 오디오 버퍼보다 길어야 시작이 늦지 않는다</summary>
    private const double SeekStartDelaySeconds = 0.1;

    private void ApplyVolumes()
    {
        if (musicSource != null) musicSource.volume = masterVolume * musicVolume;

        if (sfxSources != null)
        {
            foreach (AudioSource source in sfxSources)
            {
                if (source != null) source.volume = masterVolume * sfxVolume;
            }
        }

        if (scheduledSfxSources != null)
        {
            foreach (AudioSource source in scheduledSfxSources)
            {
                if (source != null) source.volume = masterVolume * sfxVolume;
            }
        }
    }
}
