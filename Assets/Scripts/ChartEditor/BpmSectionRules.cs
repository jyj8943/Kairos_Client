using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// BPM 구간 하나 (에디터 편집용). 위치는 정수 tick(1박 = GameConstants.Editor.TicksPerBeat), BPM은 소수 둘째 자리까지.
/// 채보 파일에는 TimingPointData(beat = tick ÷ TicksPerBeat)로 저장한다.
/// </summary>
public readonly struct BpmSection
{
    public readonly int Tick;
    public readonly float Bpm;

    public BpmSection(int tick, float bpm)
    {
        Tick = tick;
        Bpm = bpm;
    }

    /// <summary>시작 박 (tick ÷ TicksPerBeat)</summary>
    public float Beat => BeatGridMath.TickToBeat(Tick);

    /// <summary>곡 시작 구간(0박)이면 true</summary>
    public bool IsStart => Tick == 0;
}

/// <summary>BPM 구간 편집 검사 결과</summary>
public enum BpmSectionError
{
    None,

    /// <summary>30 ~ 400 밖이거나 숫자가 아님</summary>
    OutOfRange,

    /// <summary>이전 구간과 같은 BPM</summary>
    SameAsPrevious,

    /// <summary>다음 구간과 같은 BPM</summary>
    SameAsNext,

    /// <summary>같은 위치에 이미 구간이 있음 (추가할 때)</summary>
    DuplicatePosition,

    /// <summary>곡 시작 구간은 지울 수 없음</summary>
    CannotRemoveStart,

    /// <summary>그 위치에 구간이 없음 (수정·삭제할 때)</summary>
    NotFound,

    /// <summary>곡 범위(0 ~ 곡 끝) 밖</summary>
    OutOfSong
}

/// <summary>
/// BPM 구간 규칙 (기획서 3-6 BPM 구간 규칙). MonoBehaviour가 아닌 순수 클래스라 EditMode 테스트로 검사한다.
/// - 구간 목록은 tick 순이고 첫 구간은 항상 0 tick(곡 시작)이다.
/// - BPM은 30 ~ 400, 소수 둘째 자리까지 (반올림해 저장). 이전·다음 구간과 같은 BPM은 넣을 수 없다 (소수 둘째 자리 기준).
/// - 같은 위치(tick)에 두 개를 둘 수 없다. 곡 시작 구간은 고칠 수 있지만 지울 수 없다.
/// - 지운 뒤 앞뒤 구간 BPM이 같아지면 뒤 구간도 함께 지운다.
/// - 위치·범위 문구(마디 · 박)도 여기서 만든다. 마디는 4박마다(박자표 변경 없음).
/// </summary>
public static class BpmSectionRules
{
    /// <summary>BPM을 소수 둘째 자리로 반올림한다</summary>
    public static float RoundBpm(float bpm)
    {
        return (float)Math.Round(bpm, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>두 BPM이 소수 둘째 자리 기준으로 같으면 true</summary>
    public static bool SameBpm(float a, float b)
    {
        return Math.Abs(Math.Round(a * 100.0, MidpointRounding.AwayFromZero) - Math.Round(b * 100.0, MidpointRounding.AwayFromZero)) < 0.5;
    }

    /// <summary>
    /// 입력 글자를 BPM으로 바꾼다 (소수 둘째 자리로 반올림). 숫자가 아니거나 30 ~ 400 밖이면 OutOfRange.
    /// 소수점은 '.'만 쓴다 (컴퓨터 지역 설정과 상관없이).
    /// </summary>
    public static BpmSectionError TryParseBpm(string text, out float bpm)
    {
        bpm = 0f;
        if (string.IsNullOrWhiteSpace(text)) return BpmSectionError.OutOfRange;
        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) return BpmSectionError.OutOfRange;
        if (float.IsNaN(parsed) || float.IsInfinity(parsed)) return BpmSectionError.OutOfRange;

        bpm = RoundBpm(parsed);
        return IsInRange(bpm) ? BpmSectionError.None : BpmSectionError.OutOfRange;
    }

    /// <summary>30 ~ 400 안이면 true (반올림한 값으로 본다)</summary>
    public static bool IsInRange(float bpm)
    {
        float rounded = RoundBpm(bpm);
        return rounded >= GameConstants.Editor.MinSectionBpm && rounded <= GameConstants.Editor.MaxSectionBpm;
    }

    /// <summary>tick 위치의 구간 번호. 없으면 −1</summary>
    public static int IndexOf(IReadOnlyList<BpmSection> sections, int tick)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].Tick == tick) return i;
        }
        return -1;
    }

    /// <summary>tick이 속한 구간 번호 (구간 시작 ≤ tick인 마지막 구간, 없으면 0)</summary>
    public static int IndexAt(IReadOnlyList<BpmSection> sections, int tick)
    {
        int index = 0;
        for (int i = 1; i < sections.Count; i++)
        {
            if (sections[i].Tick <= tick) index = i;
            else break;
        }
        return index;
    }

    /// <summary>
    /// 새 구간을 tick에 넣을 수 있는지 본다. 이전 구간 = tick 앞의 마지막 구간, 다음 구간 = tick 뒤의 첫 구간.
    /// </summary>
    /// <param name="totalTicks">곡 끝 tick. 0 이하이면 범위를 보지 않는다</param>
    public static BpmSectionError ValidateAdd(IReadOnlyList<BpmSection> sections, int tick, float bpm, int totalTicks)
    {
        if (!IsInRange(bpm)) return BpmSectionError.OutOfRange;
        if (tick < 0 || (totalTicks > 0 && tick > totalTicks)) return BpmSectionError.OutOfSong;
        if (IndexOf(sections, tick) >= 0) return BpmSectionError.DuplicatePosition;

        int previous = IndexAt(sections, tick);
        if (previous >= 0 && previous < sections.Count && sections[previous].Tick < tick && SameBpm(sections[previous].Bpm, bpm))
            return BpmSectionError.SameAsPrevious;

        int next = previous + 1;
        if (next < sections.Count && SameBpm(sections[next].Bpm, bpm)) return BpmSectionError.SameAsNext;
        return BpmSectionError.None;
    }

    /// <summary>
    /// tick 위치 구간의 BPM을 바꿀 수 있는지 본다 (곡 시작 구간 포함). 자기 BPM 그대로는 허용한다 (바뀌는 것이 없음).
    /// </summary>
    public static BpmSectionError ValidateSet(IReadOnlyList<BpmSection> sections, int tick, float bpm)
    {
        if (!IsInRange(bpm)) return BpmSectionError.OutOfRange;

        int index = IndexOf(sections, tick);
        if (index < 0) return BpmSectionError.NotFound;
        if (index > 0 && SameBpm(sections[index - 1].Bpm, bpm)) return BpmSectionError.SameAsPrevious;
        if (index + 1 < sections.Count && SameBpm(sections[index + 1].Bpm, bpm)) return BpmSectionError.SameAsNext;
        return BpmSectionError.None;
    }

    /// <summary>
    /// 추가·수정 창에서 쓰는 검사: tick에 구간이 있으면 수정, 없으면 추가로 본다.
    /// </summary>
    public static BpmSectionError ValidateAddOrSet(IReadOnlyList<BpmSection> sections, int tick, float bpm, int totalTicks)
    {
        return IndexOf(sections, tick) >= 0 ? ValidateSet(sections, tick, bpm) : ValidateAdd(sections, tick, bpm, totalTicks);
    }

    /// <summary>tick 위치 구간을 지울 수 있는지 본다 (곡 시작 구간은 불가)</summary>
    public static BpmSectionError ValidateRemove(IReadOnlyList<BpmSection> sections, int tick)
    {
        int index = IndexOf(sections, tick);
        if (index < 0) return BpmSectionError.NotFound;
        if (index == 0) return BpmSectionError.CannotRemoveStart;
        return BpmSectionError.None;
    }

    /// <summary>
    /// tick 위치 구간을 지웠을 때 앞뒤 구간 BPM이 같아져 뒤 구간도 함께 지워지면 그 뒤 구간의 tick, 아니면 −1.
    /// </summary>
    public static int GetMergedNextTick(IReadOnlyList<BpmSection> sections, int tick)
    {
        int index = IndexOf(sections, tick);
        if (index <= 0 || index + 1 >= sections.Count) return -1;
        return SameBpm(sections[index - 1].Bpm, sections[index + 1].Bpm) ? sections[index + 1].Tick : -1;
    }

    /// <summary>구간 목록을 tick 순으로 복사해 넣고, tick 위치에 구간을 넣거나 바꾼 새 목록을 만든다</summary>
    public static List<BpmSection> WithSection(IReadOnlyList<BpmSection> sections, int tick, float bpm)
    {
        List<BpmSection> result = new List<BpmSection>(sections.Count + 1);
        bool placed = false;
        foreach (BpmSection section in sections)
        {
            if (!placed && section.Tick >= tick)
            {
                result.Add(new BpmSection(tick, RoundBpm(bpm)));
                placed = true;
                if (section.Tick == tick) continue;
            }
            result.Add(section);
        }
        if (!placed) result.Add(new BpmSection(tick, RoundBpm(bpm)));
        return result;
    }

    /// <summary>tick 위치 구간을 뺀 새 목록</summary>
    public static List<BpmSection> WithoutSection(IReadOnlyList<BpmSection> sections, int tick)
    {
        List<BpmSection> result = new List<BpmSection>(sections.Count);
        foreach (BpmSection section in sections)
        {
            if (section.Tick != tick) result.Add(section);
        }
        return result;
    }

    /// <summary>
    /// 채보의 timingPoints를 구간 목록으로 바꾼다. beat는 tick으로 반올림하고, 첫 구간은 0 tick으로 본다 (TimingMap과 같은 규칙).
    /// BPM이 0 이하이거나 같은 tick에 겹친 구간(앞의 것을 남김)은 빼고 skipped에 센다. 하나도 없으면 fallbackBpm 1개.
    /// BPM 값은 소수 둘째 자리로 반올림하지 않는다 (파일 값 그대로).
    /// </summary>
    public static List<BpmSection> FromTimingPoints(IReadOnlyList<TimingPointData> points, float fallbackBpm, out int skipped)
    {
        skipped = 0;
        List<BpmSection> result = new List<BpmSection>();
        if (points != null)
        {
            List<TimingPointData> sorted = new List<TimingPointData>();
            foreach (TimingPointData point in points)
            {
                if (point == null || !(point.bpm > 0f)) { skipped++; continue; }
                sorted.Add(point);
            }
            // beat 순 안정 정렬 (TimingMap과 같은 순서)
            for (int i = 1; i < sorted.Count; i++)
            {
                TimingPointData item = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].beat > item.beat) { sorted[j + 1] = sorted[j]; j--; }
                sorted[j + 1] = item;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                int tick = i == 0 ? 0 : Math.Max(0, BeatGridMath.BeatToTick(sorted[i].beat));
                if (result.Count > 0 && result[result.Count - 1].Tick >= tick) { skipped++; continue; }
                result.Add(new BpmSection(tick, sorted[i].bpm));
            }
        }

        if (result.Count == 0) result.Add(new BpmSection(0, fallbackBpm));
        return result;
    }

    /// <summary>구간 목록을 채보 파일 형식으로 바꾼다 (beat = tick ÷ TicksPerBeat)</summary>
    public static List<TimingPointData> ToTimingPoints(IReadOnlyList<BpmSection> sections)
    {
        List<TimingPointData> points = new List<TimingPointData>(sections.Count);
        foreach (BpmSection section in sections)
            points.Add(new TimingPointData { beat = section.Beat, bpm = section.Bpm });
        return points;
    }

    /// <summary>
    /// 그 위치 뒤에서 시간이 바뀌는 노트 수: 시작 tick이 구간 위치보다 뒤인 노트 (롱노트는 시작 기준).
    /// 구간 위치와 같은 tick의 노트는 시간이 바뀌지 않으므로 세지 않는다.
    /// </summary>
    public static int CountNotesAfter(IReadOnlyList<EditorNote> notes, int tick)
    {
        int count = 0;
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            if (notes[i].Tick > tick) count++;
            else break; // 노트는 tick 순이다
        }
        return count;
    }

    /// <summary>박 위치를 "마디 M · B박" 문구로 (0박 → 마디 1 · 1박, 98.5박 → 마디 25 · 3.5박)</summary>
    public static string FormatPosition(float beat)
    {
        GetMeasureAndBeat(beat, out int measure, out float beatInMeasure);
        return string.Format(GameConstants.Editor.BpmSectionPositionFormat, measure, FormatBeat(beatInMeasure));
    }

    /// <summary>tick 위치를 "마디 M · B박" 문구로</summary>
    public static string FormatPosition(int tick)
    {
        return FormatPosition(BeatGridMath.TickToBeat(tick));
    }

    /// <summary>
    /// 적용 범위·다음 구간에 쓰는 짧은 위치: 마디 첫 박이면 "마디 M", 아니면 "마디 M B박" (예: 마디 25 3박).
    /// </summary>
    public static string FormatShortPosition(int tick)
    {
        GetMeasureAndBeat(BeatGridMath.TickToBeat(tick), out int measure, out float beatInMeasure);
        return Math.Abs(beatInMeasure - 1f) < 1e-4f
            ? string.Format(GameConstants.Editor.BpmSectionMeasureFormat, measure)
            : string.Format(GameConstants.Editor.BpmSectionMeasureBeatFormat, measure, FormatBeat(beatInMeasure));
    }

    /// <summary>
    /// 적용 범위 문구: "마디 17 ~ 마디 25 3박 (다음 구간 전까지)" / 마지막 구간이면 "마디 17 ~ 곡 끝".
    /// tick에 구간이 아직 없으면(추가) 넣었다고 보고 다음 구간을 찾는다.
    /// </summary>
    public static string FormatRange(IReadOnlyList<BpmSection> sections, int tick)
    {
        int nextTick = -1;
        foreach (BpmSection section in sections)
        {
            if (section.Tick > tick) { nextTick = section.Tick; break; }
        }

        string start = FormatShortPosition(tick);
        return nextTick < 0
            ? string.Format(GameConstants.Editor.BpmSectionRangeToEndFormat, start)
            : string.Format(GameConstants.Editor.BpmSectionRangeFormat, start, FormatShortPosition(nextTick));
    }

    /// <summary>오류 문구. 창에 보여 줄 필요가 없으면(없음) 빈 문자열</summary>
    public static string GetMessage(BpmSectionError error)
    {
        switch (error)
        {
            case BpmSectionError.OutOfRange: return GameConstants.Editor.BpmSectionOutOfRangeMessage;
            case BpmSectionError.SameAsPrevious: return GameConstants.Editor.BpmSectionSameAsPreviousMessage;
            case BpmSectionError.SameAsNext: return GameConstants.Editor.BpmSectionSameAsNextMessage;
            case BpmSectionError.DuplicatePosition: return GameConstants.Editor.BpmSectionDuplicateMessage;
            case BpmSectionError.CannotRemoveStart: return GameConstants.Editor.BpmSectionCannotRemoveStartMessage;
            case BpmSectionError.OutOfSong: return GameConstants.Editor.BpmSectionOutOfSongMessage;
            case BpmSectionError.NotFound: return GameConstants.Editor.BpmSectionNotFoundMessage;
            default: return string.Empty;
        }
    }

    private static void GetMeasureAndBeat(float beat, out int measure, out float beatInMeasure)
    {
        int beatsPerMeasure = GameConstants.Editor.BeatsPerMeasure;
        // 부동소수 오차로 4.9999박이 마디를 넘기지 않게 아주 작은 값을 더해 내림한다
        int measureIndex = (int)Math.Floor(beat / beatsPerMeasure + 1e-6);
        measure = measureIndex + 1;
        beatInMeasure = beat - measureIndex * beatsPerMeasure + 1f;
    }

    private static string FormatBeat(float beatInMeasure)
    {
        return beatInMeasure.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
