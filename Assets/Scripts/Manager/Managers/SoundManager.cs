using UnityEngine;

/// <summary>
/// 노래·효과음 재생 전역 매니저. Managers_DontDestroy 프리팹의 "SoundManager" 오브젝트에 붙이고, Bootstrapper가 Init()을 호출한다.
/// - 노래는 PlayScheduled로 DSP 시각에 맞춰 시작하고, 곡 시간(SongTime)은 MusicClock이 dspTime 기준으로 계산한다.
/// - 판정·노트 위치는 SongTime을 쓴다. AudioSource.time은 기준으로 쓰지 않는다 (기획서: 판정 기준은 DSP 시간).
/// - 다른 스크립트보다 먼저 Update해 그 프레임의 SongTime을 맞춰 둔다.
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
    private int nextSfxIndex;
    private int nextScheduledSfxIndex;

    /// <summary>곡 시간 (초). 노래 시작 전 준비 시간에는 음수, 재생 중이 아니면 0</summary>
    public double SongTime => clock.SongTime;

    /// <summary>노래가 예약됐거나 재생 중이면 true (일시정지 중에는 false)</summary>
    public bool IsMusicScheduledOrPlaying => clock.IsRunning && !clock.IsPaused;

    /// <summary>노래가 일시정지 중이면 true</summary>
    public bool IsMusicPaused => clock.IsPaused;

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
    }

    private void Update()
    {
        clock.Tick(AudioSettings.dspTime, Time.realtimeSinceStartupAsDouble);
    }

    /// <summary>
    /// 지금 DSP 시각 + delaySeconds에 노래를 시작하도록 예약한다. 곡 시간은 −delay에서 시작해 늘어난다.
    /// </summary>
    /// <param name="startPositionSeconds">음원 안에서 시작할 위치 (초)</param>
    public void PlayMusicScheduled(AudioClip clip, double delaySeconds, double startPositionSeconds = 0)
    {
        if (musicSource == null || clip == null)
        {
            Debug.LogError($"[SoundManager] 노래를 재생할 수 없습니다: musicSource={(musicSource != null)}, clip={(clip != null)}", this);
            return;
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.time = Mathf.Clamp((float)startPositionSeconds, 0f, clip.length);

        double dspNow = AudioSettings.dspTime;
        double startDsp = dspNow + System.Math.Max(0, delaySeconds);
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
        musicSource.time = Mathf.Clamp((float)position, 0f, musicSource.clip.length);
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
    /// 입력 이벤트 시각(realtimeSinceStartup 기준)을 곡 시간으로 바꾼다.
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
    /// 효과음을 DSP 시각에 예약 재생한다. 예약 전용 소스를 돌아가며 쓰므로,
    /// 소스 수보다 많이 미리 예약하면 가장 오래된 예약이 덮어써진다.
    /// </summary>
    /// <returns>예약했으면 true</returns>
    public bool ScheduleSfx(AudioClip clip, double dspTime)
    {
        if (clip == null || scheduledSfxSources == null || scheduledSfxSources.Length == 0) return false;

        AudioSource source = scheduledSfxSources[nextScheduledSfxIndex];
        nextScheduledSfxIndex = (nextScheduledSfxIndex + 1) % scheduledSfxSources.Length;
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
