using System;
using System.Collections.Generic;

/// <summary>
/// 채보 노트 → 판정 노트(PlayNote) 변환과 최대 콤보 계산 (11/25). 인게임(InGameScene)과 채보 불러오기(ChartRepository)가 같은 함수를 쓴다.
/// - 시간은 TimingMap(채보 timingPoints 전체 구간)으로 바꾸고, 롱노트는 유지 틱 시간(LongNoteTicks, 시작 + 1/4박 ~ 끝 − 1/4박 이하)도 같이 계산한다.
/// - 최대 콤보 = ScoreCalculator.MaxPossibleCombo (노트 수 + 유지 틱 수). 따로 세지 않는다.
/// </summary>
public static class PlayNoteBuilder
{
    /// <summary>노트 목록을 판정 노트로 바꾼다.</summary>
    /// <param name="longCount">롱노트 수</param>
    public static List<PlayNote> Build(IReadOnlyList<NoteData> source, TimingMap timing, out int longCount)
    {
        List<PlayNote> notes = new List<PlayNote>(source.Count);
        longCount = 0;
        foreach (NoteData note in source)
        {
            if (note.type == NoteData.TypeLong)
            {
                longCount++;
                notes.Add(new PlayNote(note.lane, timing.BeatToSeconds(note.beat), timing.BeatToSeconds(note.endBeat),
                    LongNoteTicks.GetTickTimes(timing, note.beat, note.endBeat)));
                continue;
            }
            notes.Add(new PlayNote(note.lane, timing.BeatToSeconds(note.beat)));
        }
        return notes;
    }

    /// <summary>
    /// 채보에서 낼 수 있는 최대 콤보 (인게임 ScoreCalculator.MaxPossibleCombo와 같은 값). 채보를 불러올 때 한 번 계산해 ChartData.maxCombo에 둔다.
    /// BPM 구간이 잘못된 채보(플레이할 수 없음)면 0.
    /// </summary>
    public static int GetMaxCombo(ChartData chart)
    {
        if (chart == null || chart.notes == null) return 0;

        TimingMap timing;
        try
        {
            timing = TimingMap.FromChart(chart, 0);
        }
        catch (ArgumentException)
        {
            return 0;
        }

        List<PlayNote> notes = Build(chart.notes, timing, out _);
        return new ScoreCalculator(notes.Count, LongNoteTicks.TotalTicks(notes)).MaxPossibleCombo;
    }
}
