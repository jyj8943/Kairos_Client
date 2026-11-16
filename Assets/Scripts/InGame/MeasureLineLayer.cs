using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 마디선 표시 (기획서 3-4 마디선). InGameScene.unity의 "Gear/MeasureLines"(Notes 바로 앞 형제)에 붙이고,
/// InGameScene이 NoteLayer.Init 다음에 Init()을 호출한 뒤 매 프레임 NoteLayer와 같은 곡 시간으로 Refresh()를 부른다.
/// - 4박(한 마디)마다 레인 전체 폭의 얇은 흰 가로선 (MeasureLine 프리팹, SpriteRenderer, sortingOrder 1).
///   레인 배경(0) 위, 라인 빛남(2)·노트(3)·판정선(4)·키 칸(6~) 아래에 그린다. 판정·점수·콤보와 상관없고 끌 수 없다.
/// - 선 y = 판정선 y + (스크롤 거리(마디 시각) − 스크롤 거리(곡 시간)) × NoteLayer.ScrollSpeed.
///   노트와 같은 ScrollMap·낙하 속도·판정선을 그대로 쓰므로 배속을 바꾸거나 일시정지·재개해도 노트와 똑같이 움직인다.
///   선의 세로 중심이 이 y다 (노트 기준점과 같다).
/// - 화면에 보이는 선은 마디 순서로 이어진 구간 [first, end)이다. 선 i는 풀의 (i % 풀 크기)번 오브젝트에 그린다
///   (구간 길이 ≤ 풀 크기라 겹치지 않는다). 플레이 중 Instantiate·Destroy와 매 프레임 할당이 없다.
/// - 풀 크기 = 가장 느린 배속(1.0)에서 화면 위 여유 ~ 아래 여유 사이에 동시에 들어오는 최대 선 수 + 여유. Init에서 한 번 만든다.
/// </summary>
public class MeasureLineLayer : MonoBehaviour
{
    [Tooltip("마디선 한 줄 (MeasureLine 프리팹, SpriteRenderer). 색·크기는 Init에서 정한다")]
    [SerializeField] private SpriteRenderer linePrefab;
    [Tooltip("미리 만드는 선 수에 더하는 여유")]
    [Min(0)] [SerializeField] private int extraPoolCount = 2;

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private double[] times = new double[0];

    private GearView gear;
    private NoteLayer noteLayer;
    private ScrollMap scroll;

    /// <summary>화면에 그리고 있는 선 구간 [activeFirst, activeEnd) (마디선 번호)</summary>
    private int activeFirst;
    private int activeEnd;

    /// <summary>마디선 수 (확인용)</summary>
    public int LineCount => times.Length;

    /// <summary>미리 만든 선 오브젝트 수 (확인용)</summary>
    public int PooledCount => pool.Count;

    /// <summary>풀이 모자라 플레이 중 새로 만든 수 (확인용, 0이어야 한다)</summary>
    public int GrowCount { get; private set; }

    /// <summary>지금 화면에 있는 선 수 (확인용)</summary>
    public int ActiveCount => activeEnd - activeFirst;

    /// <summary>선 굵기 (월드 단위, 확인용) = MeasureLineThickness px × GearView.UnitsPerReferencePx</summary>
    public float LineThickness { get; private set; }

    /// <summary>선 폭 (월드 단위, 확인용) = 라인 수 × 라인 폭</summary>
    public float LineWidth { get; private set; }

    /// <summary>i번 마디선의 시각 (초, 확인용)</summary>
    public double GetTime(int index) => times[index];

    /// <summary>i번 마디선이 지금 화면에 있으면 그 오브젝트, 없으면 null (확인용)</summary>
    public SpriteRenderer GetActiveLine(int index)
    {
        if (index < activeFirst || index >= activeEnd || pool.Count == 0) return null;
        return pool[index % pool.Count];
    }

    /// <summary>
    /// 기어·노트 표시·스크롤 거리와 마디선 시각을 받아 선 모양을 정하고 풀을 미리 채운다. NoteLayer.Init 다음에 부른다.
    /// </summary>
    /// <param name="measureTimes">마디선 시각 (초, 오름차순, MeasureLineRules.GetMeasureTimes)</param>
    public void Init(GearView gear, NoteLayer noteLayer, ScrollMap scroll, IReadOnlyList<double> measureTimes)
    {
        this.gear = gear;
        this.noteLayer = noteLayer;
        this.scroll = scroll;

        DeactivateRange(activeFirst, activeEnd);
        activeFirst = 0;
        activeEnd = 0;

        times = new double[measureTimes != null ? measureTimes.Count : 0];
        for (int i = 0; i < times.Length; i++) times[i] = measureTimes[i];

        if (linePrefab == null)
        {
            if (times.Length > 0) Debug.LogError("[MeasureLineLayer] linePrefab(MeasureLine)이 연결되지 않아 마디선을 그릴 수 없습니다.", this);
            return;
        }

        // 레인 왼쪽 끝 ~ 오른쪽 끝, 판정선 기준 높이에 맞춘 굵기 (1080 기준 px → 월드)
        int laneCount = GameConstants.Editor.LaneCount;
        LineWidth = laneCount * gear.LaneWidth;
        LineThickness = GameConstants.Play.MeasureLineThickness * gear.UnitsPerReferencePx;

        int needed = CountMaxVisible(times, VisibleSecondsAt(noteLayer.SpeedFor(GameConstants.Play.MinScrollMultiplier)));
        if (times.Length > 0) needed += extraPoolCount;
        while (pool.Count < needed) pool.Add(CreateLine());
        GrowCount = 0;

        foreach (SpriteRenderer line in pool) ApplyShape(line);
    }

    /// <summary>
    /// 곡 시간에 맞춰 선을 꺼내고, 옮기고, 돌려놓는다 (NoteLayer.Refresh와 같은 곡 시간으로 부른다).
    /// </summary>
    public void Refresh(double songTime)
    {
        if (gear == null || noteLayer == null || scroll == null || pool.Count == 0) return;

        double songPosition = scroll.PositionAt(songTime);
        float judgeLineY = gear.JudgeLineY;
        float speed = noteLayer.ScrollSpeed;
        float topY = gear.ScreenTopY + noteLayer.ScreenMargin;
        float bottomY = gear.ScreenBottomY - noteLayer.ScreenMargin;

        // 화면 아래로 벗어난 선은 다시 올라오지 않는다 (곡 시간은 되돌아가지 않고, 재시작은 씬을 다시 불러온다)
        int first = activeFirst;
        while (first < times.Length && LineY(first, songPosition, judgeLineY, speed) < bottomY) first++;
        int end = first;
        while (end < times.Length && LineY(end, songPosition, judgeLineY, speed) <= topY) end++;

        if (end - first > pool.Count)
        {
            // 풀이 모자라면(계산상 생기지 않는다) 늘린다. 풀 크기가 바뀌면 번호 → 오브젝트 대응이 바뀌므로 모두 끄고 다시 꺼낸다
            DeactivateRange(activeFirst, activeEnd);
            activeFirst = activeEnd = first;
            while (pool.Count < end - first) pool.Add(Grow());
        }

        // 구간을 벗어난 선을 끈다 (속도를 올리면 위쪽 선도 빠진다)
        for (int i = activeFirst; i < activeEnd; i++)
        {
            if (i < first || i >= end) pool[i % pool.Count].gameObject.SetActive(false);
        }

        for (int i = first; i < end; i++)
        {
            SpriteRenderer line = pool[i % pool.Count];
            line.transform.localPosition = new Vector3(0f, LineY(i, songPosition, judgeLineY, speed), 0f);
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
        }

        activeFirst = first;
        activeEnd = end;
    }

    /// <summary>i번 선의 y (Gear 기준 로컬) = 판정선 y + (스크롤 거리 차이) × 낙하 속도. NoteLayer의 노트 y와 같은 식</summary>
    private float LineY(int index, double songPosition, float judgeLineY, float speed)
    {
        return judgeLineY + (float)((scroll.PositionAt(times[index]) - songPosition) * speed);
    }

    /// <summary>선이 화면(위아래 여유 포함)에 머무는 시간 (초) = (위 끝 − 아래 끝) ÷ 낙하 속도</summary>
    private float VisibleSecondsAt(float speed)
    {
        float span = gear.ScreenTopY - gear.ScreenBottomY + noteLayer.ScreenMargin * 2f;
        return speed > 0f ? span / speed : 0f;
    }

    /// <summary>길이 window(초)인 구간에 동시에 들어가는 최대 선 수 (시각은 오름차순). Init에서 한 번만 부른다</summary>
    private static int CountMaxVisible(double[] sortedTimes, float window)
    {
        int max = 0;
        int lo = 0;
        for (int hi = 0; hi < sortedTimes.Length; hi++)
        {
            while (sortedTimes[hi] - sortedTimes[lo] > window) lo++;
            if (hi - lo + 1 > max) max = hi - lo + 1;
        }
        return max;
    }

    private void DeactivateRange(int from, int to)
    {
        if (pool.Count == 0) return;
        for (int i = from; i < to; i++) pool[i % pool.Count].gameObject.SetActive(false);
    }

    /// <summary>흰색 × MeasureLineAlpha, 폭 = 레인 전체, 굵기 = LineThickness. 스프라이트 크기로 나눠 스케일을 정한다</summary>
    private void ApplyShape(SpriteRenderer line)
    {
        line.color = new Color(1f, 1f, 1f, GameConstants.Play.MeasureLineAlpha);
        Vector2 spriteSize = line.sprite != null ? (Vector2)line.sprite.bounds.size : Vector2.one;
        if (line.drawMode != SpriteDrawMode.Simple)
        {
            line.size = new Vector2(LineWidth, LineThickness);
            line.transform.localScale = Vector3.one;
            return;
        }
        line.transform.localScale = new Vector3(LineWidth / spriteSize.x, LineThickness / spriteSize.y, 1f);
    }

    private SpriteRenderer Grow()
    {
        GrowCount++;
        Debug.LogWarning($"[MeasureLineLayer] 미리 만든 마디선이 모자라 새로 만듭니다 (총 {pool.Count + 1}개).", this);
        SpriteRenderer line = CreateLine();
        ApplyShape(line);
        return line;
    }

    private SpriteRenderer CreateLine()
    {
        SpriteRenderer line = Instantiate(linePrefab, transform);
        line.name = linePrefab.name;
        line.gameObject.SetActive(false);
        return line;
    }
}
