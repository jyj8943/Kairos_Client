using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에디터 노래 재생. EditorScene.unity의 "EditorPlayback" 오브젝트에 붙이고, EditorScene이 Init()을 호출한다.
/// - 재생: 현재 위치 선의 박부터 SoundManager로 예약 재생한다. 재생 중에는 곡 시간 → 박을 매 프레임 그리드에 넣어
///   스냅에 맞추지 않고 부드럽게 흐르게 한다. 곡 끝에 닿으면 자동으로 정지한다.
/// - 정지: 노래와 예약한 클릭을 멈추고, 현재 위치를 지금 스냅의 가장 가까운 선으로 맞춘다.
/// - 메트로놈: 켜져 있으면 다음 박의 클릭을 DSP 시각으로 미리 예약한다 (프레임마다 재생하지 않는다).
///   마디 첫 박은 더 높은 소리다. 현재 위치 선도 박마다 잠깐 밝아진다.
/// - 키음: 켜져 있으면 노트가 현재 위치 선에 닿는 순간의 키음(인게임과 같은 NoteSFX)을 DSP 시각으로 미리 예약한다.
///   같은 박 여러 라인 노트는 노트마다 한 번씩, 롱노트는 시작에서 한 번. 재생 중 노트가 바뀌면 예약을 지우고 다시 잡는다.
///   계산(첫 노트, 예약 범위 안 노트)은 EditorKeySoundMath가 맡는다.
/// - 메트로놈과 키음은 따로 켜고 끈다. 예약은 둘 다 SoundManager.ScheduleSfx를 쓰고, 취소(CancelScheduledSfx)도 같이 된다.
/// - 박 ↔ 시간은 인게임과 같은 TimingMap(session.Timing, offset 포함)을 쓴다.
/// </summary>
public class EditorPlayback : MonoBehaviour
{
    [Tooltip("에디터 키음 (인게임과 같은 NoteSFX)")]
    [SerializeField] private AudioClip keySound;

    private ChartGridView grid;
    private ChartEditSession session;
    private SongData song;
    private Action onPlayingChanged;
    private Action onPositionChanged;

    private AudioClip beatClick;
    private AudioClip measureClick;
    private bool isMetronomeOn;
    private int nextClickBeat;

    private bool isKeySoundOn;
    private ChartEditData noteData;
    private int nextNoteIndex;
    private int keyPlayStartTick;
    private readonly List<int> collectedNotes = new List<int>();

    // 이번 재생에서 예약한 키음 (확인용: 노트 tick·라인과 예약 DSP 시각). 다시 계산할 때 아직 안 울린 것은 지운다
    private readonly List<int> keyTicks = new List<int>();
    private readonly List<int> keyLanes = new List<int>();
    private readonly List<double> keyDsps = new List<double>();
    private double maxKeyErrorMs;
    private int skippedKeyCount;
    private int rescheduleCount;

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

    /// <summary>에디터 키음이 켜져 있으면 true</summary>
    public bool IsKeySoundOn => isKeySoundOn;

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

        // 재생 중 노트를 추가·삭제하면 키음 예약을 다시 잡는다
        if (noteData != null) noteData.OnChanged -= OnNotesChanged;
        noteData = session.Data;
        noteData.OnChanged += OnNotesChanged;

        if (keySound == null)
            Debug.LogWarning("[EditorPlayback] keySound(NoteSFX)가 연결되지 않아 에디터 키음을 낼 수 없습니다.", this);

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
        EditorPlaybackMath.PlayStart start = EditorPlaybackMath.GetPlayStart(playStartBeat, session.Timing);
        GameManager.Sound.PlayMusicScheduled(song.Clip, start.DelaySeconds, start.StartPositionSeconds);

        IsPlaying = true;
        nextClickBeat = EditorPlaybackMath.GetFirstClickBeat(playStartBeat);
        keyPlayStartTick = EditorKeySoundMath.BeatToFirstTick(playStartBeat);
        nextNoteIndex = EditorKeySoundMath.FindFirstNoteAtOrAfter(noteData.Notes, keyPlayStartTick);
        keyTicks.Clear();
        keyLanes.Clear();
        keyDsps.Clear();
        maxKeyErrorMs = 0;
        skippedKeyCount = 0;
        rescheduleCount = 0;
        frameCount = 0;
        maxBeatTimeErrorMs = 0;
        maxClickErrorMs = 0;
        scheduledClickCount = 0;
        skippedClickCount = 0;

        // 시작 위치에 있는 박·노트는 시작 지연(0.1초) 뒤에 울려야 하므로 다음 프레임을 기다리지 않고 바로 예약한다
        double songTimeAtStart = GameManager.Sound.SongTime;
        if (isMetronomeOn) ScheduleClicks(songTimeAtStart);
        if (isKeySoundOn) ScheduleKeySounds(songTimeAtStart);

        Debug.Log($"[EditorPlayback] 재생: {playStartBeat:0.###}박 (마디 {grid.CurrentMeasure}), 음원 {start.StartPositionSeconds:0.###}초부터, 지연 {start.DelaySeconds:0.###}초, " +
                  $"bpm={session.Bpm}, offsetMs={session.OffsetMs}, 메트로놈 {(isMetronomeOn ? "켬" : "끔")}, 키음 {(isKeySoundOn ? "켬" : "끔")}", this);
        onPlayingChanged?.Invoke();
    }

    /// <summary>
    /// 노래와 예약한 클릭을 멈춘다.
    /// </summary>
    /// <param name="snapToGrid">true면 현재 위치를 지금 스냅의 가장 가까운 선으로 맞춘다</param>
    public void Stop(bool snapToGrid)
    {
        if (!IsPlaying) return;

        // 확인용: 정지 순간 이미 울린 키음과 취소될 키음, 재생 구간의 노트 수
        double dspNow = AudioSettings.dspTime;
        float stopBeat = SoundManager.HasInstance
            ? (float)session.Timing.SecondsToBeat(GameManager.Sound.GetSongTimeAtRealtime(Time.realtimeSinceStartupAsDouble))
            : grid.CurrentBeat;
        int playedKeys = 0;
        foreach (double dsp in keyDsps)
        {
            if (dsp <= dspNow) playedKeys++;
        }
        int rangeNotes = EditorKeySoundMath.CountNotesInRange(noteData.Notes, keyPlayStartTick,
            Mathf.FloorToInt(stopBeat * GameConstants.Editor.TicksPerBeat));

        IsPlaying = false;
        if (SoundManager.HasInstance)
        {
            GameManager.Sound.StopMusic();
            GameManager.Sound.CancelScheduledSfx();
        }

        grid.SetCurrentLineHighlight(0f);
        if (snapToGrid) grid.SetCurrentBeat(BeatGridMath.SnapBeat(grid.CurrentBeat, grid.SnapDivision));

        Debug.Log($"[EditorPlayback] 정지: {grid.CurrentBeat:0.###}박 (마디 {grid.CurrentMeasure}, " +
                  $"{session.Timing.BeatToSeconds(grid.CurrentBeat):0.000}초), 재생 프레임 {frameCount}개, " +
                  $"곡 시간과 그리드 박 시간 차이 최대 {maxBeatTimeErrorMs:0.###}ms, " +
                  $"메트로놈 예약 {scheduledClickCount}개 (예약 시각 − 박 시각 최대 {maxClickErrorMs:0.###}ms, 늦어서 건너뜀 {skippedClickCount}개), " +
                  $"키음 {(isKeySoundOn ? "켬" : "끔")}: 울림 {playedKeys}개 / 재생 구간 노트 {rangeNotes}개, 정지로 취소 {keyDsps.Count - playedKeys}개 " +
                  $"(예약 시각 − 노트 시각 최대 {maxKeyErrorMs:0.###}ms, 늦어서 건너뜀 {skippedKeyCount}개, 노트 변경으로 다시 계산 {rescheduleCount}번)", this);

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
        if (IsPlaying) RescheduleFromNow();
    }

    /// <summary>
    /// 에디터 키음을 켜고 끈다. 재생 중이면 예약을 지우고 지금 위치부터 다시 잡는다.
    /// </summary>
    public void SetKeySound(bool on)
    {
        isKeySoundOn = on;
        if (IsPlaying) RescheduleFromNow();
    }

    /// <summary>
    /// 재생 중 노트가 바뀌면 (추가·삭제·불러오기) 키음 예약을 다시 잡는다. 지운 노트 소리가 나거나 새 노트 소리가 빠지지 않게 한다.
    /// </summary>
    private void OnNotesChanged()
    {
        if (!IsPlaying) return;

        rescheduleCount++;
        RescheduleFromNow();
    }

    /// <summary>
    /// 예약한 클릭·키음을 모두 취소하고, 지금 곡 시간 이후의 것을 그 자리에서 바로 다시 예약한다
    /// (다음 프레임까지 기다리면 그 사이에 닿는 노트·박이 늦어서 빠진다).
    /// 이미 울린 키음 기록은 남기고, 아직 안 울린 기록만 지운다.
    /// </summary>
    private void RescheduleFromNow()
    {
        GameManager.Sound.CancelScheduledSfx();

        double dspNow = AudioSettings.dspTime;
        for (int i = keyDsps.Count - 1; i >= 0; i--)
        {
            if (keyDsps[i] <= dspNow) continue;
            keyTicks.RemoveAt(i);
            keyLanes.RemoveAt(i);
            keyDsps.RemoveAt(i);
        }

        // 다시 예약할 위치는 위에서 "아직 안 울림"을 가른 기준(dspTime)과 같은 시각에서 찾는다.
        // (realtime으로 보간한 곡 시간은 dspTime보다 몇 ms 앞서 있어, 그 사이 노트가 취소만 되고 다시 예약되지 않는다)
        double songTimeNow = GameManager.Sound.GetSongTimeAtRealtime(Time.realtimeSinceStartupAsDouble);
        double songTimeAtDsp = songTimeNow - (GameManager.Sound.GetDspTimeOfSongTime(songTimeNow) - dspNow);
        float beatAtDsp = Mathf.Max((float)session.Timing.SecondsToBeat(songTimeAtDsp), playStartBeat);
        nextClickBeat = EditorPlaybackMath.GetFirstClickBeat(beatAtDsp + 0.01f);
        nextNoteIndex = EditorKeySoundMath.FindFirstNoteAtOrAfter(noteData.Notes,
            Mathf.FloorToInt(beatAtDsp * GameConstants.Editor.TicksPerBeat) + 1);

        if (isMetronomeOn) ScheduleClicks(songTimeNow);
        if (isKeySoundOn) ScheduleKeySounds(songTimeNow);
    }

    private void Update()
    {
        if (!IsPlaying) return;

        double songTime = GameManager.Sound.SongTime;
        float beat = (float)session.Timing.SecondsToBeat(songTime);

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
            double shownTime = session.Timing.BeatToSeconds(grid.CurrentBeat);
            double errorMs = Math.Abs(songTime - shownTime) * 1000.0;
            if (errorMs > maxBeatTimeErrorMs) maxBeatTimeErrorMs = errorMs;
        }

        grid.SetCurrentLineHighlight(EditorPlaybackMath.GetBeatFlash(beat));

        if (isMetronomeOn) ScheduleClicks(songTime);
        if (isKeySoundOn) ScheduleKeySounds(songTime);

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
            double clickTime = session.Timing.BeatToSeconds(nextClickBeat);
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
                    if (sourceTime > 0 && songTime > session.Timing.BeatToSeconds(playStartBeat))
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

    /// <summary>
    /// 앞으로 KeySoundLookaheadSeconds 안에 현재 위치 선에 닿을 노트의 키음을 DSP 시각으로 예약한다 (노트마다 한 번).
    /// </summary>
    private void ScheduleKeySounds(double songTime)
    {
        if (keySound == null) return;

        double lookaheadEnd = songTime + GameConstants.Editor.KeySoundLookaheadSeconds;
        int endTick = Mathf.FloorToInt((float)session.Timing.SecondsToBeat(lookaheadEnd) * GameConstants.Editor.TicksPerBeat);
        IReadOnlyList<EditorNote> notes = noteData.Notes;
        nextNoteIndex = EditorKeySoundMath.CollectNotesUntil(notes, nextNoteIndex, endTick, collectedNotes);
        if (collectedNotes.Count == 0) return;

        double dspNow = AudioSettings.dspTime;
        double sourceTime = GameManager.Sound.MusicSourceTime;
        foreach (int index in collectedNotes)
        {
            EditorNote note = notes[index];
            double noteTime = session.Timing.BeatToSeconds(BeatGridMath.TickToBeat(note.Tick));
            double dsp = GameManager.Sound.GetDspTimeOfSongTime(noteTime);
            if (dsp <= dspNow)
            {
                skippedKeyCount++;
                continue;
            }

            if (!GameManager.Sound.ScheduleSfx(keySound, dsp)) continue;

            keyTicks.Add(note.Tick);
            keyLanes.Add(note.Lane);
            keyDsps.Add(dsp);

            // 확인용: 실제 음원 재생 위치로 노트의 DSP 시각을 따로 구해 예약 시각과 비교한다
            // (음원이 이미 나오고 있을 때만. 시작 대기 중에는 음원 위치가 시작 위치에 멈춰 있어 비교하지 않는다)
            if (sourceTime > 0 && songTime > session.Timing.BeatToSeconds(playStartBeat))
            {
                double noteDspFromSource = dspNow + (noteTime - sourceTime);
                double errorMs = Math.Abs(dsp - noteDspFromSource) * 1000.0;
                if (errorMs > maxKeyErrorMs) maxKeyErrorMs = errorMs;
            }
        }
    }

    private void OnDestroy()
    {
        if (noteData != null) noteData.OnChanged -= OnNotesChanged;

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
