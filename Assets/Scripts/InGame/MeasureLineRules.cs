using System.Collections.Generic;

/// <summary>
/// 인게임 마디선 계산 (기획서 3-4 마디선). 상태가 없는 순수 함수라 EditMode 테스트로 검사할 수 있다.
/// - 한 마디 = GameConstants.Editor.BeatsPerMeasure(4박). 선은 0박부터 4박마다 하나씩이고, 시각은 TimingMap.BeatToSeconds로 구한다.
///   BPM 구간이 있어도 박 기준으로 4박마다다 (에디터 마디선과 같은 위치).
/// - 마지막 선 = 마지막 노트(롱노트면 끝 박)가 들어 있는 마디의 끝. 마디 경계에 딱 맞는 노트는 그 경계에서 시작하는 마디에 들어 있다.
/// - 테스트 플레이 시작 박 앞의 선은 뺀다. 박이 같으면 넣는다 (노트를 거르는 TestPlayRules.FilterNotesFrom과 같게 tick으로 비교).
/// - 시작 메트로놈(−1마디)에는 선을 그리지 않는다. 판이 시작되면 판정선 위에서 0박 선이 내려온다 (11/16 사용자 결정).
/// </summary>
public static class MeasureLineRules
{
    /// <summary>
    /// 노트 목록의 마지막 박 (숏노트는 beat, 롱노트는 endBeat 중 큰 값). 노트가 없으면 -1.
    /// </summary>
    public static float GetLastBeat(IReadOnlyList<NoteData> notes)
    {
        float last = -1f;
        if (notes == null) return last;

        foreach (NoteData note in notes)
        {
            if (note == null) continue;
            float end = note.type == NoteData.TypeLong && note.endBeat > note.beat ? note.endBeat : note.beat;
            if (end > last) last = end;
        }
        return last;
    }

    /// <summary>
    /// 마디선을 그릴 박 목록 (시작 박 이상, 마지막 박이 든 마디의 끝까지, 4박마다). lastBeat가 0보다 작으면(노트 없음) 빈 목록.
    /// </summary>
    /// <param name="lastBeat">마지막 노트 박 (GetLastBeat)</param>
    /// <param name="startBeat">테스트 플레이 시작 박 (보통 플레이는 0)</param>
    public static List<int> GetMeasureBeats(float lastBeat, float startBeat)
    {
        List<int> beats = new List<int>();
        if (lastBeat < 0f) return beats;

        int beatsPerMeasure = GameConstants.Editor.BeatsPerMeasure;
        int ticksPerMeasure = beatsPerMeasure * GameConstants.Editor.TicksPerBeat;
        // 마지막 노트가 든 마디 번호 (tick으로 내려 경계 오차를 없앤다). 그 마디의 끝 = 다음 마디의 시작
        int lastMeasure = BeatGridMath.BeatToTick(lastBeat) / ticksPerMeasure + 1;
        int startTick = BeatGridMath.BeatToTick(startBeat > 0f ? startBeat : 0f);

        for (int measure = 0; measure <= lastMeasure; measure++)
        {
            if (measure * ticksPerMeasure < startTick) continue;
            beats.Add(measure * beatsPerMeasure);
        }
        return beats;
    }

    /// <summary>
    /// 마디선 시각 목록 (초, 오름차순). 각 선 = timing.BeatToSeconds(마디 시작 박). 노트 시간과 같은 변환이라 같은 박의 노트와 정확히 겹친다.
    /// </summary>
    /// <param name="timing">채보의 박 ↔ 시간 변환 (곡 offset + 사용자 판정 오프셋 반영)</param>
    /// <param name="lastBeat">마지막 노트 박 (GetLastBeat)</param>
    /// <param name="startBeat">테스트 플레이 시작 박 (보통 플레이는 0)</param>
    public static List<double> GetMeasureTimes(TimingMap timing, float lastBeat, float startBeat)
    {
        List<int> beats = GetMeasureBeats(lastBeat, startBeat);
        List<double> times = new List<double>(beats.Count);
        if (timing == null) return times;

        foreach (int beat in beats) times.Add(timing.BeatToSeconds(beat));
        return times;
    }
}
