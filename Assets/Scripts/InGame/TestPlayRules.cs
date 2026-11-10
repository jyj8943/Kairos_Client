using System.Collections.Generic;

/// <summary>
/// 테스트 플레이 계산 모음. 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// </summary>
public static class TestPlayRules
{
    /// <summary>노래를 예약할 음원 위치와 지연</summary>
    public readonly struct MusicStart
    {
        /// <summary>음원 안에서 재생을 시작할 위치 (초, 0 이상)</summary>
        public readonly double StartPositionSeconds;

        /// <summary>지금부터 재생 시작까지 기다리는 시간 (초)</summary>
        public readonly double DelaySeconds;

        /// <summary>재생을 예약한 순간의 곡 시간 (= 시작 위치 − 지연)</summary>
        public double InitialSongTime => StartPositionSeconds - DelaySeconds;

        public MusicStart(double startPositionSeconds, double delaySeconds)
        {
            StartPositionSeconds = startPositionSeconds;
            DelaySeconds = delaySeconds;
        }
    }

    /// <summary>
    /// 시작 박 이상에서 시작하는 노트만 남긴 목록을 만든다 (원래 목록은 바꾸지 않는다). 박이 같으면 포함한다.
    /// 롱노트는 시작 박으로 본다: 시작 위치가 롱노트 도중이면(시작이 시작 박 앞) 그 롱노트는 뺀다.
    /// 부동소수 오차를 피하려고 tick으로 반올림해서 비교한다.
    /// </summary>
    public static List<NoteData> FilterNotesFrom(IReadOnlyList<NoteData> notes, float startBeat)
    {
        List<NoteData> result = new List<NoteData>();
        if (notes == null) return result;

        int startTick = BeatGridMath.BeatToTick(startBeat);
        foreach (NoteData note in notes)
        {
            if (IsPlayableFrom(note, startTick)) result.Add(note);
        }
        return result;
    }

    /// <summary>
    /// FilterNotesFrom이 남길 노트 수 (목록을 만들지 않는다). 테스트 플레이를 시작할 수 있는지 볼 때 쓴다.
    /// </summary>
    public static int CountNotesFrom(IReadOnlyList<NoteData> notes, float startBeat)
    {
        if (notes == null) return 0;

        int startTick = BeatGridMath.BeatToTick(startBeat);
        int count = 0;
        foreach (NoteData note in notes)
        {
            if (IsPlayableFrom(note, startTick)) count++;
        }
        return count;
    }

    private static bool IsPlayableFrom(NoteData note, int startTick)
    {
        return note != null && BeatGridMath.BeatToTick(note.beat) >= startTick;
    }

    /// <summary>
    /// 테스트 플레이 노래 시작. 곡 시간이 (시작 시간 − 준비 시간)부터 흐르게 한다.
    /// - 시작 시간이 준비 시간보다 뒤면: 음원을 (시작 − 준비) 위치부터, 최소 지연만 두고 재생한다.
    /// - 앞이면: 음원 0초부터 재생하고, 모자란 만큼 기다린다.
    /// </summary>
    /// <param name="startTimeSeconds">시작 위치의 음원 시간 (초)</param>
    public static MusicStart GetMusicStart(double startTimeSeconds)
    {
        double leadIn = GameConstants.Play.TestPlayLeadInSeconds;
        double latency = GameConstants.Play.ScheduleLatencySeconds;

        double startPosition = startTimeSeconds - leadIn;
        if (startPosition >= 0) return new MusicStart(startPosition, latency);

        return new MusicStart(0, System.Math.Max(latency, -startPosition));
    }
}
