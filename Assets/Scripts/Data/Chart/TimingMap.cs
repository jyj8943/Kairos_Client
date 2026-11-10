using System;
using System.Collections.Generic;

/// <summary>
/// 박 ↔ 음원 시간(초) 변환을 맡는 BPM 구간 목록 (기획서 2-2 timingPoints). 인게임과 에디터가 같이 쓴다.
/// - MonoBehaviour가 아닌 일반 클래스이고 Unity API를 쓰지 않아 EditMode 테스트로 검사할 수 있다.
/// - 만들 때 구간을 beat 순으로 정렬해 복사하고, 구간마다 시작 시각(offset 제외)을 미리 누적해 둔다. 조회할 때 메모리를 새로 잡지 않는다.
/// - 첫 구간의 beat가 0이 아니어도 첫 BPM이 0박부터 적용된 것으로 본다. 0박보다 앞(음수 beat)도 첫 BPM으로 외삽한다.
/// - 계산식: 초 = offsetMs ÷ 1000 + 구간 시작 시각 + (beat − 구간 시작 beat) × 60 ÷ 구간 BPM
///   예) offset 120ms, 0~16박 150 BPM, 16박부터 200 BPM
///       16박 = 0.12 + 16 × 60 ÷ 150 = 6.52초
///       20박 = 0.12 + 6.4 + (20 − 16) × 60 ÷ 200 = 7.72초
/// </summary>
public sealed class TimingMap
{
    /// <summary>구간 시작 박 (beat 순, 첫 값은 항상 0)</summary>
    private readonly double[] startBeats;

    /// <summary>구간 BPM</summary>
    private readonly float[] bpms;

    /// <summary>구간 시작 시각 (초, offset 제외). 첫 값은 0</summary>
    private readonly double[] startSeconds;

    /// <summary>offset (초) = OffsetMs ÷ 1000</summary>
    private readonly double offsetSeconds;

    /// <summary>음원 시작부터 0박까지의 시간 (ms)</summary>
    public int OffsetMs { get; }

    /// <summary>BPM 구간 수</summary>
    public int PointCount => bpms.Length;

    /// <summary>모든 구간 중 가장 높은 BPM (1박이 가장 낮게 그려지는 BPM. 에디터 선 풀 크기 계산용)</summary>
    public float MaxBpm { get; }

    /// <summary>
    /// BPM 구간 목록과 offset으로 만든다.
    /// </summary>
    /// <param name="points">BPM 구간 목록. 순서는 상관없다 (beat 순으로 정렬해 복사한다)</param>
    /// <param name="offsetMs">음원 시작부터 0박까지의 시간 (ms)</param>
    /// <exception cref="ArgumentException">목록이 비었거나, null 구간이 있거나, BPM이 0 이하인 구간이 있으면</exception>
    public TimingMap(IReadOnlyList<TimingPointData> points, int offsetMs)
    {
        if (points == null || points.Count == 0)
            throw new ArgumentException("BPM 구간이 하나도 없습니다.", nameof(points));

        List<TimingPointData> sorted = new List<TimingPointData>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            TimingPointData point = points[i];
            if (point == null) throw new ArgumentException($"{i}번 BPM 구간이 null입니다.", nameof(points));
            if (!(point.bpm > 0f)) throw new ArgumentException($"{i}번 BPM 구간의 BPM이 0 이하입니다: {point.bpm}", nameof(points));
            sorted.Add(point);
        }
        // 같은 beat는 입력 순서를 지키도록 안정 정렬한다 (List.Sort는 안정 정렬이 아니다)
        sorted = StableSortByBeat(sorted);

        int count = sorted.Count;
        startBeats = new double[count];
        bpms = new float[count];
        startSeconds = new double[count];
        float maxBpm = 0f;
        for (int i = 0; i < count; i++)
        {
            if (sorted[i].bpm > maxBpm) maxBpm = sorted[i].bpm;
            // 첫 구간은 0박부터 적용된 것으로 본다
            startBeats[i] = i == 0 ? 0.0 : sorted[i].beat;
            bpms[i] = sorted[i].bpm;
            startSeconds[i] = i == 0 ? 0.0 : startSeconds[i - 1] + (startBeats[i] - startBeats[i - 1]) * 60.0 / bpms[i - 1];
        }

        MaxBpm = maxBpm;
        OffsetMs = offsetMs;
        offsetSeconds = offsetMs / 1000.0;
    }

    /// <summary>
    /// 채보의 timingPoints와 곡 offset으로 만든다.
    /// </summary>
    public static TimingMap FromChart(ChartData chart, int offsetMs)
    {
        if (chart == null) throw new ArgumentException("채보가 없습니다.", nameof(chart));
        return new TimingMap(chart.timingPoints, offsetMs);
    }

    /// <summary>
    /// BPM 하나(0박부터)로 만든다.
    /// </summary>
    public static TimingMap Single(float bpm, int offsetMs)
    {
        return new TimingMap(new[] { new TimingPointData { beat = 0f, bpm = bpm } }, offsetMs);
    }

    /// <summary>
    /// 박을 음원 시간(초)으로 바꾼다. 0박 = offset. 음수 beat는 첫 구간 BPM으로 외삽한다.
    /// 초 = offset + 구간 시작 시각 + (beat − 구간 시작 beat) × 60 ÷ BPM
    /// </summary>
    public double BeatToSeconds(double beat)
    {
        int i = FindSegmentByBeat(beat);
        return offsetSeconds + startSeconds[i] + (beat - startBeats[i]) * 60.0 / bpms[i];
    }

    /// <summary>
    /// 음원 시간(초)을 박으로 바꾼다. BeatToSeconds의 반대 변환이다. offset보다 앞 시간은 음수 박이 된다.
    /// 박 = 구간 시작 beat + (초 − offset − 구간 시작 시각) × BPM ÷ 60
    /// </summary>
    public double SecondsToBeat(double seconds)
    {
        double local = seconds - offsetSeconds;
        int i = FindSegmentBySeconds(local);
        return startBeats[i] + (local - startSeconds[i]) * bpms[i] / 60.0;
    }

    /// <summary>
    /// beat 위치의 BPM. 0박보다 앞이면 첫 구간 BPM이다.
    /// </summary>
    public float BpmAt(double beat)
    {
        return bpms[FindSegmentByBeat(beat)];
    }

    /// <summary>beat가 속한 구간 번호 (구간 시작 beat ≤ beat인 마지막 구간, 없으면 0)</summary>
    private int FindSegmentByBeat(double beat)
    {
        int lo = 0;
        int hi = startBeats.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (startBeats[mid] <= beat) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>시각(offset 제외)이 속한 구간 번호 (구간 시작 시각 ≤ 시각인 마지막 구간, 없으면 0)</summary>
    private int FindSegmentBySeconds(double localSeconds)
    {
        int lo = 0;
        int hi = startSeconds.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (startSeconds[mid] <= localSeconds) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>beat 순 안정 정렬 (삽입 정렬, 구간 수가 적어 충분하다)</summary>
    private static List<TimingPointData> StableSortByBeat(List<TimingPointData> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            TimingPointData item = list[i];
            int j = i - 1;
            while (j >= 0 && list[j].beat > item.beat)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = item;
        }
        return list;
    }
}
