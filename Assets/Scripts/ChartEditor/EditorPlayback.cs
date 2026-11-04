using System;
using UnityEngine;

/// <summary>
/// 에디터 노래 재생. EditorScene.unity의 "EditorPlayback" 오브젝트에 붙이고, EditorScene이 Init()을 호출한다.
/// - 재생: 현재 위치 선의 박부터 SoundManager로 예약 재생한다. 재생 중에는 곡 시간 → 박을 매 프레임 그리드에 넣어
///   스냅에 맞추지 않고 부드럽게 흐르게 한다. 곡 끝에 닿으면 자동으로 정지한다.
/// - 정지: 노래와 예약한 클릭을 멈추고, 현재 위치를 지금 스냅의 가장 가까운 선으로 맞춘다.
/// - 메트로놈: 켜져 있으면 다음 박의 클릭을 DSP 시각으로 미리 예약한다 (프레임마다 재생하지 않는다).
///   마디 첫 박은 더 높은 소리다. 현재 위치 선도 박마다 잠깐 밝아진다.
/// - 박 ↔ 시간은 인게임과 같은 BeatGridMath(offset 포함)를 쓴다.
/// </summary>
public class EditorPlayback : MonoBehaviour
{
    private ChartGridView grid;
    private ChartEditSession session;
    private SongData song;
    private Action onPlayingChanged;
    private Action onPositionChanged;

    private AudioClip beatClick;
    private AudioClip measureClick;
    private bool isMetronomeOn;
    private int nextClickBeat;

    /// <summary>재생을 시작한 박. 예약 지연 동안 그리드가 뒤로 가지 않게 이 박보다 앞으로는 표시하지 않는다</summary>
    private float playStartBeat;

    // 확인용 기록 (재생 한 번마다)
    private int frameCount;
    private double maxBeatTimeErrorMs;
    private double maxClickErrorMs;
    private int scheduledClickCount;
    private int skippedClickCount;

    /// <summary>재생 중이면 true (예약 대기 포함)</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>메트로놈이 켜져 있으면 true</summary>
    public bool IsMetronomeOn => isMetronomeOn;

    /// <summary>
    /// 그리드·세션·곡과 상태가 바뀔 때 부를 함수를 받고, 메트로놈 클릭 소리를 만든다. 정지 상태로 시작한다.
    /// </summary>
    /// <param name="onPlayingChanged">재생·정지가 바뀌었을 때 (재생 버튼 아이콘용)</param>
    /// <param name="onPositionChanged">재생으로 위치가 바뀌었을 때 (시간 표시·슬라이더용)</param>
    public void Init(ChartGridView grid, ChartEditSession session, SongData song, Action onPlayingChanged, Action onPositionChanged)
    {
        this.grid = grid;
        this.session = session;
        this.song = song;
        this.onPlayingChanged = onPlayingChanged;
        this.onPositionChanged = onPositionChanged;

        if (beatClick == null)
            beatClick = ClickSoundFactory.Create("MetronomeBeat", GameConstants.Editor.MetronomeBeatFrequency, GameConstants.Editor.MetronomeClickSeconds);
        if (measureClick == null)
            measureClick = ClickSoundFactory.Create("MetronomeMeasure", GameConstants.Editor.MetronomeMeasureFrequency, GameConstants.Editor.MetronomeClickSeconds);

        IsPlaying = false;
        grid.SetCurrentLineHighlight(0f);
    }

    /// <summary>재생 중이면 정지하고, 아니면 재생한다.</summary>
    public void Toggle()
    {
        if (IsPlaying) Stop(true);
        else Play();
    }

    /// <summary>
    /// 현재 위치 선의 박부터 재생한다. 박 → 시간은 offset을 포함하고, 시작 시간이 0보다 앞이면 음원 0초부터 기다렸다가 재생한다.
    /// </summary>
    public void Play()
    {
        if (IsPlaying || grid == null || song == null || song.Clip == null) return;

        // 곡 끝이면 처음부터
        if (grid.CurrentBeat >= grid.TotalBeats) grid.SetCurrentBeat(0f);

        playStartBeat = grid.CurrentBeat;
        EditorPlaybackMath.PlayStart start = EditorPlaybackMath.GetPlayStart(playStartBeat, session.Bpm, session.OffsetMs);
        GameManager.Sound.PlayMusicScheduled(song.Clip, start.DelaySeconds, start.StartPositionSeconds);

        IsPlaying = true;
        nextClickBeat = EditorPlaybackMath.GetFirstClickBeat(playStartBeat);
        frameCount = 0;
        maxBeatTimeErrorMs = 0;
        maxClickErrorMs = 0;
        scheduledClickCount = 0;
        skippedClickCount = 0;

        Debug.Log($"[EditorPlayback] 재생: {playStartBeat:0.###}박 (마디 {grid.CurrentMeasure}), 음원 {start.StartPositionSeconds:0.###}초부터, 지연 {start.DelaySeconds:0.###}초, " +
                  $"bpm={session.Bpm}, offsetMs={session.OffsetMs}, 메트로놈 {(isMetronomeOn ? "켬" : "끔")}", this);
        onPlayingChanged?.Invoke();
    }

    /// <summary>
    /// 노래와 예약한 클릭을 멈춘다.
    /// </summary>
    /// <param name="snapToGrid">true면 현재 위치를 지금 스냅의 가장 가까운 선으로 맞춘다</param>
    public void Stop(bool snapToGrid)
    {
        if (!IsPlaying) return;

        IsPlaying = false;
        if (SoundManager.HasInstance)
        {
            GameManager.Sound.StopMusic();
            GameManager.Sound.CancelScheduledSfx();
        }

        grid.SetCurrentLineHighlight(0f);
        if (snapToGrid) grid.SetCurrentBeat(BeatGridMath.SnapBeat(grid.CurrentBeat, grid.SnapDivision));

        Debug.Log($"[EditorPlayback] 정지: {grid.CurrentBeat:0.###}박 (마디 {grid.CurrentMeasure}, " +
                  $"{BeatGridMath.BeatToSeconds(grid.CurrentBeat, session.Bpm, session.OffsetMs):0.000}초), 재생 프레임 {frameCount}개, " +
                  $"곡 시간과 그리드 박 시간 차이 최대 {maxBeatTimeErrorMs:0.###}ms, " +
                  $"메트로놈 예약 {scheduledClickCount}개 (예약 시각 − 박 시각 최대 {maxClickErrorMs:0.###}ms, 늦어서 건너뜀 {skippedClickCount}개)", this);

        onPlayingChanged?.Invoke();
        onPositionChanged?.Invoke();
    }

    /// <summary>
    /// 재생 중이면 지금 위치에서 다시 재생한다 (BPM·offset을 바꿨을 때). 정지 중이면 아무것도 하지 않는다.
    /// </summary>
    public void Restart()
    {
        if (!IsPlaying) return;

        Stop(false);
        Play();
    }

    /// <summary>
    /// 메트로놈을 켜고 끈다. 재생 중이면 예약을 지우고 지금 위치부터 다시 잡는다.
    /// </summary>
    public void SetMetronome(bool on)
    {
        isMetronomeOn = on;
        if (!IsPlaying) return;

        GameManager.Sound.CancelScheduledSfx();
        float beat = Mathf.Max(grid.CurrentBeat, 0f);
        nextClickBeat = EditorPlaybackMath.GetFirstClickBeat(beat + 0.01f);
    }

    private void Update()
    {
        if (!IsPlaying) return;

        double songTime = GameManager.Sound.SongTime;
        float beat = BeatGridMath.SecondsToBeat((float)songTime, session.Bpm, session.OffsetMs);

        // 곡 끝이면 자동 정지
        if (beat >= grid.TotalBeats || songTime >= session.SongLengthSeconds)
        {
            grid.SetCurrentBeat(grid.TotalBeats);
            Stop(true);
            return;
        }

        // 예약 지연 동안은 시작 박에 머문다 (그리드가 뒤로 가지 않게)
        float shownBeat = Mathf.Max(beat, playStartBeat);
        grid.SetCurrentBeat(shownBeat);
        frameCount++;

        if (beat >= playStartBeat)
        {
            double shownTime = BeatGridMath.BeatToSeconds(grid.CurrentBeat, session.Bpm, session.OffsetMs);
            double errorMs = Math.Abs(songTime - shownTime) * 1000.0;
            if (errorMs > maxBeatTimeErrorMs) maxBeatTimeErrorMs = errorMs;
        }

        grid.SetCurrentLineHighlight(EditorPlaybackMath.GetBeatFlash(beat));

        if (isMetronomeOn) ScheduleClicks(songTime);

        onPositionChanged?.Invoke();
    }

    /// <summary>
    /// 앞으로 MetronomeLookaheadSeconds 안에 올 박의 클릭을 DSP 시각으로 예약한다.
    /// </summary>
    private void ScheduleClicks(double songTime)
    {
        double lookaheadEnd = songTime + GameConstants.Editor.MetronomeLookaheadSeconds;
        double dspNow = AudioSettings.dspTime;

        while (nextClickBeat <= grid.TotalBeats)
        {
            double clickTime = BeatGridMath.BeatToSeconds(nextClickBeat, session.Bpm, session.OffsetMs);
            if (clickTime > lookaheadEnd) break;

            double dsp = GameManager.Sound.GetDspTimeOfSongTime(clickTime);
            if (dsp > dspNow)
            {
                AudioClip clip = EditorPlaybackMath.IsMeasureStart(nextClickBeat) ? measureClick : beatClick;
                if (GameManager.Sound.ScheduleSfx(clip, dsp))
                {
                    scheduledClickCount++;

                    // 확인용: 실제 음원 재생 위치로 박의 DSP 시각을 따로 구해 예약 시각과 비교한다
                    // (음원이 이미 나오고 있을 때만. dspTime은 오디오 버퍼 단위라 그만큼의 오차는 정상이다)
                    double sourceTime = GameManager.Sound.MusicSourceTime;
                    if (sourceTime > 0 && songTime > BeatGridMath.BeatToSeconds(playStartBeat, session.Bpm, session.OffsetMs))
                    {
                        double beatDspFromSource = dspNow + (clickTime - sourceTime);
                        double errorMs = Math.Abs(dsp - beatDspFromSource) * 1000.0;
                        if (errorMs > maxClickErrorMs) maxClickErrorMs = errorMs;
                    }
                }
            }
            else
            {
                skippedClickCount++;
            }

            nextClickBeat++;
        }
    }

    private void OnDestroy()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            if (SoundManager.HasInstance)
            {
                GameManager.Sound.StopMusic();
                GameManager.Sound.CancelScheduledSfx();
            }
        }

        if (beatClick != null) Destroy(beatClick);
        if (measureClick != null) Destroy(measureClick);
    }
}
