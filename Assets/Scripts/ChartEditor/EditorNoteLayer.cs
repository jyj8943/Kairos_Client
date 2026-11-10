using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 편집 중인 노트 표시. EditorScene.unity의 Notes에 붙이고, EditorScene이 Init()을 호출한다.
/// - ChartEditData가 바뀌거나 그리드가 다시 배치될 때(스크롤·높이 변경) 보이는 구간(위아래 여유 1마디)의 노트만 배치한다.
///   보이는 구간 앞에서 시작해 구간 안으로 들어오는 롱노트도 배치한다 (ChartEditData.GetRange).
/// - 숏노트는 Img_ShortNote, 롱노트는 Img_LongNote를 각각 풀에서 재사용한다. ChartEditSession.Data는 읽기만 하고 바꾸지 않는다.
/// - 롱노트 높이는 시작과 끝의 y를 각각 그리드(BeatGridMath.BeatToY, 시간 기준)로 구해 그 사이를 채운다 (변속이 들어가도 맞다).
/// - 롱노트 드래그 미리보기(반투명)도 여기서 그린다 (EditorNotePlacer가 SetPreview / ClearPreview를 부른다).
/// </summary>
public class EditorNoteLayer : MonoBehaviour
{
    [SerializeField] private Img_ShortNote shortNotePrefab;
    [SerializeField] private Img_LongNote longNotePrefab;

    [Tooltip("노트 높이 (그리드 높이 배율과 관계없이 고정, 1920×1080 기준 px). 롱노트 머리·꼬리 높이도 같다")]
    [Min(1f)] [SerializeField] private float noteHeight = 16f;

    [Tooltip("라인 칸 좌우 여백 (px)")]
    [Min(0f)] [SerializeField] private float laneHorizontalPadding = 2f;

    [Tooltip("미리 만들어 둘 숏노트 수. 보이는 노트가 이보다 많으면 그때만 늘어난다")]
    [Min(0)] [SerializeField] private int prewarmCount = 128;

    [Tooltip("미리 만들어 둘 롱노트 수. 보이는 롱노트가 이보다 많으면 그때만 늘어난다")]
    [Min(0)] [SerializeField] private int longPrewarmCount = 32;

    private readonly List<EditorNote> visibleNotes = new List<EditorNote>();
    private ChartGridView grid;
    private ChartEditData data;
    private ComponentPool<Img_ShortNote> notePool;
    private ComponentPool<Img_LongNote> longNotePool;

    private Img_LongNote preview;
    private bool hasPreview;
    private int previewLane;
    private int previewStartTick;
    private int previewEndTick;

    /// <summary>지금 켜져 있는 숏노트 오브젝트 수 (확인용)</summary>
    public int ActiveNoteCount => notePool != null ? notePool.ActiveCount : 0;

    /// <summary>숏노트 풀의 전체 인스턴스 수 (확인용)</summary>
    public int PooledNoteCount => notePool != null ? notePool.TotalCount : 0;

    /// <summary>지금 켜져 있는 롱노트 오브젝트 수 (확인용, 미리보기 제외)</summary>
    public int ActiveLongNoteCount => longNotePool != null ? longNotePool.ActiveCount : 0;

    /// <summary>롱노트 풀의 전체 인스턴스 수 (확인용)</summary>
    public int PooledLongNoteCount => longNotePool != null ? longNotePool.TotalCount : 0;

    /// <summary>드래그 미리보기를 보여주는 중이면 true (확인용)</summary>
    public bool IsPreviewVisible => hasPreview;

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

        if (longNotePool == null)
        {
            if (longNotePrefab == null)
                Debug.LogError("[EditorNoteLayer] longNotePrefab(Img_LongNote)이 연결되지 않아 롱노트를 그릴 수 없습니다.", this);
            else
            {
                longNotePool = new ComponentPool<Img_LongNote>(longNotePrefab, transform);
                longNotePool.Prewarm(longPrewarmCount);
            }
        }

        if (preview == null && longNotePrefab != null)
        {
            preview = Instantiate(longNotePrefab, transform);
            preview.name = longNotePrefab.name + "_Preview";
            preview.gameObject.SetActive(false);
        }
        hasPreview = false;

        grid.OnViewChanged += Refresh;
        data.OnChanged += Refresh;
        Refresh();
    }

    /// <summary>
    /// 롱노트 드래그 미리보기를 보여준다 (반투명). 스크롤해도 같은 자리를 따라간다.
    /// </summary>
    public void SetPreview(int lane, int startTick, int endTick)
    {
        hasPreview = true;
        previewLane = lane;
        previewStartTick = Mathf.Min(startTick, endTick);
        previewEndTick = Mathf.Max(startTick, endTick);
        RefreshPreview();
    }

    /// <summary>드래그 미리보기를 지운다.</summary>
    public void ClearPreview()
    {
        hasPreview = false;
        if (preview != null) preview.gameObject.SetActive(false);
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
        longNotePool?.BeginFrame();
        foreach (EditorNote note in visibleNotes)
        {
            if (note.IsLong)
            {
                if (longNotePool == null) continue;
                Img_LongNote longView = longNotePool.Get();
                longView.Init(note.Lane, 1f);
                PlaceLong(longView, note.Lane, note.Tick, note.EndTick, laneWidth, size.x);
                continue;
            }

            Img_ShortNote view = notePool.Get();
            view.Init(note.Lane);

            RectTransform rect = view.RectTransform;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(
                (note.Lane + 0.5f) * laneWidth,
                grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)));
        }
        notePool.EndFrame();
        longNotePool?.EndFrame();

        RefreshPreview();
    }

    /// <summary>미리보기를 지금 그리드 위치에 맞춰 다시 놓는다 (맨 위에 그린다).</summary>
    private void RefreshPreview()
    {
        if (preview == null || grid == null) return;
        if (!hasPreview)
        {
            preview.gameObject.SetActive(false);
            return;
        }

        float laneWidth = grid.LaneWidth;
        preview.gameObject.SetActive(true);
        preview.transform.SetAsLastSibling();
        preview.Init(previewLane, GameConstants.Editor.LongNotePreviewAlpha);
        PlaceLong(preview, previewLane, previewStartTick, previewEndTick, laneWidth, Mathf.Max(1f, laneWidth - laneHorizontalPadding * 2f));
    }

    /// <summary>
    /// 롱노트를 시작 y ~ 끝 y 사이에 놓는다. 머리·꼬리 중심이 각각 시작·끝 박선 위에 오도록 위아래로 노트 높이 절반씩 늘린다.
    /// </summary>
    private void PlaceLong(Img_LongNote view, int lane, int startTick, int endTick, float laneWidth, float width)
    {
        float startY = grid.BeatToLocalY(BeatGridMath.TickToBeat(startTick));
        float endY = grid.BeatToLocalY(BeatGridMath.TickToBeat(endTick));

        view.SetCapHeight(noteHeight);
        RectTransform rect = view.RectTransform;
        rect.sizeDelta = new Vector2(width, endY - startY + noteHeight);
        rect.anchoredPosition = new Vector2((lane + 0.5f) * laneWidth, startY - noteHeight * 0.5f);
    }
}
