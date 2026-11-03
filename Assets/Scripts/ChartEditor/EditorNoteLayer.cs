using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 편집 중인 노트 표시. EditorScene.unity의 Notes에 붙이고, EditorScene이 Init()을 호출한다.
/// - ChartEditData가 바뀌거나 그리드가 다시 배치될 때(스크롤·높이 변경) 보이는 구간(위아래 여유 1마디)의 노트만 배치한다.
/// - 노트 오브젝트는 풀에서 재사용한다. ChartEditSession.Data는 읽기만 하고 바꾸지 않는다.
/// </summary>
public class EditorNoteLayer : MonoBehaviour
{
    [SerializeField] private Img_ShortNote shortNotePrefab;

    [Tooltip("노트 높이 (그리드 높이 배율과 관계없이 고정, 1920×1080 기준 px)")]
    [Min(1f)] [SerializeField] private float noteHeight = 16f;

    [Tooltip("라인 칸 좌우 여백 (px)")]
    [Min(0f)] [SerializeField] private float laneHorizontalPadding = 2f;

    [Tooltip("미리 만들어 둘 노트 수. 보이는 노트가 이보다 많으면 그때만 늘어난다")]
    [Min(0)] [SerializeField] private int prewarmCount = 128;

    private readonly List<EditorNote> visibleNotes = new List<EditorNote>();
    private ChartGridView grid;
    private ChartEditData data;
    private ComponentPool<Img_ShortNote> notePool;

    /// <summary>지금 켜져 있는 노트 오브젝트 수 (확인용)</summary>
    public int ActiveNoteCount => notePool != null ? notePool.ActiveCount : 0;

    /// <summary>노트 풀의 전체 인스턴스 수 (확인용)</summary>
    public int PooledNoteCount => notePool != null ? notePool.TotalCount : 0;

    /// <summary>
    /// 그리드와 편집 세션을 받아 이벤트를 구독하고 한 번 배치한다.
    /// </summary>
    public void Init(ChartGridView grid, ChartEditSession session)
    {
        Unsubscribe();

        this.grid = grid;
        data = session.Data;

        if (notePool == null)
        {
            notePool = new ComponentPool<Img_ShortNote>(shortNotePrefab, transform);
            notePool.Prewarm(prewarmCount);
        }

        grid.OnViewChanged += Refresh;
        data.OnChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (grid != null) grid.OnViewChanged -= Refresh;
        if (data != null) data.OnChanged -= Refresh;
    }

    /// <summary>
    /// 보이는 구간의 노트를 풀에서 꺼내 배치하고 나머지는 끈다. 노트 중심이 박선 위에 온다.
    /// </summary>
    private void Refresh()
    {
        if (grid == null || data == null) return;

        grid.GetVisibleBeatRange(out float minBeat, out float maxBeat);
        int startTick = Mathf.FloorToInt(minBeat * GameConstants.Editor.TicksPerBeat);
        int endTick = Mathf.CeilToInt(maxBeat * GameConstants.Editor.TicksPerBeat);
        data.GetRange(startTick, endTick, visibleNotes);

        float laneWidth = grid.LaneWidth;
        Vector2 size = new Vector2(Mathf.Max(1f, laneWidth - laneHorizontalPadding * 2f), noteHeight);

        notePool.BeginFrame();
        foreach (EditorNote note in visibleNotes)
        {
            Img_ShortNote view = notePool.Get();
            view.Init(note.Lane);

            RectTransform rect = view.RectTransform;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(
                (note.Lane + 0.5f) * laneWidth,
                grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)));
        }
        notePool.EndFrame();
    }
}
