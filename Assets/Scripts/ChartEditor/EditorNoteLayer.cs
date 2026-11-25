using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 편집 중인 노트 표시. EditorScene.unity의 Notes에 붙이고, EditorScene이 Init()을 호출한다.
/// - ChartEditData가 바뀌거나 그리드가 다시 배치될 때(스크롤·높이 변경) 보이는 구간(위아래 여유 1마디)의 노트만 배치한다.
///   보이는 구간 앞에서 시작해 구간 안으로 들어오는 롱노트도 배치한다 (ChartEditData.GetRange).
/// - 숏노트는 Img_ShortNote, 롱노트는 Img_LongNote를 각각 풀에서 재사용한다. ChartEditSession.Data는 읽기만 하고 바꾸지 않는다.
/// - 노트 모양 · 색은 인게임 노트와 같다 (11/20): 숏노트 = 라인 색 ShortNote + 광택, 롱노트 = 머리(숏노트 모양) + 몸통(라인 폭 × 0.70) + 가운데 선 + 꼬리 끝 막대(에디터만).
///   시작과 끝의 y를 각각 그리드(BeatGridMath.BeatToY, 시간 기준)로 구해 (변속이 들어가도 맞다) 몸통은 시작 y(머리 가운데) ~ 끝 y(꼬리 끝 막대 가운데)로 놓는다.
/// - 롱노트 드래그 미리보기(하늘)도 여기서 그린다 (EditorNotePlacer가 SetPreview / ClearPreview를 부른다).
/// - 마우스 미리 보기 (EditorNotePlacer가 매 프레임 ShowPlacePreview / ShowNoteHover / HideCursorPreview 중 하나를 부른다. 둘이 같이 보이지 않는다):
///   빈 칸 = Img_PlacePreview(하늘 점선 + 하늘 15% 채움, 숏노트 크기),
///   노트 위 = Img_NoteHover(흰 2px 둥근 테두리, 노트(롱노트는 머리 ~ 꼬리 끝 막대) 둘레 3px 바깥).
/// </summary>
public class EditorNoteLayer : MonoBehaviour
{
    [SerializeField] private Img_ShortNote shortNotePrefab;
    [SerializeField] private Img_LongNote longNotePrefab;

    [Tooltip("노트 높이 (그리드 높이 배율과 관계없이 고정, 1920×1080 기준 px)")]
    [Min(1f)] [SerializeField] private float noteHeight = 22f;

    [Tooltip("라인 칸 좌우 여백 (px)")]
    [Min(0f)] [SerializeField] private float laneHorizontalPadding = 2f;

    [Tooltip("미리 만들어 둘 숏노트 수. 보이는 노트가 이보다 많으면 그때만 늘어난다")]
    [Min(0)] [SerializeField] private int prewarmCount = 128;

    [Tooltip("미리 만들어 둘 롱노트 수. 보이는 롱노트가 이보다 많으면 그때만 늘어난다")]
    [Min(0)] [SerializeField] private int longPrewarmCount = 32;

    [Header("마우스 미리 보기")]
    [Tooltip("커서가 올라간 노트 흰 테두리 Img_NoteHover (RoundedRect8Frame, Sliced)")]
    [SerializeField] private Image noteHover;
    [Tooltip("놓을 자리 미리 보기 Img_PlacePreview (Square 하늘 채움)")]
    [SerializeField] private Image placePreview;
    [Tooltip("놓을 자리 미리 보기 점선 Img_PlacePreviewDash (DashedRect12, Sliced)")]
    [SerializeField] private Image placePreviewDash;

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

    // 마우스 미리 보기 상태 (스크롤 · 줌 뒤에도 같은 노트 · 자리를 따라가게 tick으로 기억)
    private bool hasHover;
    private EditorNote hoverNote;
    private bool hasPlace;
    private int placeLane;
    private int placeTick;

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

    /// <summary>드래그 미리 보기 오브젝트 (확인용)</summary>
    public Img_LongNote Preview => preview;

    /// <summary>노트 높이 (그리드 로컬 px)</summary>
    public float NoteHeight => noteHeight;

    /// <summary>노트 폭 = 라인 폭 − 좌우 여백 × 2 (그리드 로컬 px)</summary>
    public float NoteWidth => grid != null ? Mathf.Max(1f, grid.LaneWidth - laneHorizontalPadding * 2f) : 0f;

    /// <summary>롱노트 몸통 폭 = 라인 폭 × EditorLongBodyWidthRatio (그리드 로컬 px)</summary>
    public float LongBodyWidth => grid != null ? grid.LaneWidth * GameConstants.Editor.EditorLongBodyWidthRatio : 0f;

    /// <summary>흰 테두리가 보이는 중이면 true (확인용)</summary>
    public bool IsNoteHoverVisible => noteHover != null && noteHover.gameObject.activeSelf;

    /// <summary>놓을 자리 미리 보기가 보이는 중이면 true (확인용)</summary>
    public bool IsPlacePreviewVisible => placePreview != null && placePreview.gameObject.activeSelf;

    /// <summary>흰 테두리 Image (확인용)</summary>
    public Image NoteHover => noteHover;

    /// <summary>놓을 자리 미리 보기 채움 (확인용)</summary>
    public Image PlacePreview => placePreview;

    /// <summary>놓을 자리 미리 보기 점선 (확인용)</summary>
    public Image PlacePreviewDash => placePreviewDash;

    /// <summary>지우기 위치 검색용 결과 목록 (클릭할 때마다 새로 만들지 않는다)</summary>
    private readonly List<EditorNote> hitCandidates = new List<EditorNote>();

    /// <summary>
    /// 라인에서 y(그리드 아래에서부터)에 그려진 노트를 찾는다 (지우기 · 흰 테두리용). 스냅과 상관없이 화면에 보이는 모양 기준이다.
    /// - 숏노트: 중심 y ± 노트 높이 ÷ 2 안
    /// - 롱노트: 머리 아래 끝(시작 y − 노트 높이 ÷ 2) ~ 끝 tick y 아래 (LongHitTop, 짧으면 머리 위 끝까지).
    ///   끝 tick y부터 위(꼬리 막대 위쪽 절반 포함)는 다음 노트 자리(끝 tick 칸)다. 끝 tick에 다음 노트가 있으면 그 노트가 그려진 범위 전체가 다음 노트 차지다.
    /// 여러 개가 걸리면(노트 높이가 스냅 선 간격보다 클 때) 중심(롱노트는 시작 ~ 대상 구간 위 끝)에 가장 가까운 노트를 고른다.
    /// </summary>
    /// <returns>찾았으면 true</returns>
    public bool TryFindNoteAtLocalY(int lane, float y, out EditorNote found)
    {
        found = default;
        if (grid == null || data == null) return false;

        float half = noteHeight * 0.5f;
        int fromTick = Mathf.FloorToInt(grid.LocalYToBeat(y - half) * GameConstants.Editor.TicksPerBeat) - 1;
        int toTick = Mathf.CeilToInt(grid.LocalYToBeat(y + half) * GameConstants.Editor.TicksPerBeat) + 1;
        data.GetRange(fromTick, toTick, hitCandidates);

        float bestDistance = float.MaxValue;
        foreach (EditorNote note in hitCandidates)
        {
            if (note.Lane != lane) continue;

            float startY = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick));
            float topY = note.IsLong ? GetLongHitTop(note) : startY;
            if (y < startY - half || y > Mathf.Max(topY, startY + half)) continue;

            // 시작 ~ 대상 구간 위 끝 안이면 0, 밖이면 가까운 끝까지의 거리
            float distance = y < startY ? startY - y : (y > topY ? y - topY : 0f);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            found = note;
        }
        return bestDistance < float.MaxValue;
    }

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

        if (noteHover == null || placePreview == null)
            Debug.LogError("[EditorNoteLayer] noteHover(Img_NoteHover) 또는 placePreview(Img_PlacePreview)가 연결되지 않아 마우스 미리 보기를 그릴 수 없습니다.", this);
        ApplyCursorPreviewStyle();
        HideCursorPreview();

        grid.OnViewChanged += Refresh;
        data.OnChanged += Refresh;
        Refresh();
    }

    /// <summary>
    /// 롱노트 드래그 미리보기를 보여준다 (하늘). 스크롤해도 같은 자리를 따라간다.
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
        Vector2 size = new Vector2(NoteWidth, noteHeight);

        notePool.BeginFrame();
        longNotePool?.BeginFrame();
        foreach (EditorNote note in visibleNotes)
        {
            if (note.IsLong)
            {
                if (longNotePool == null) continue;
                Img_LongNote longView = longNotePool.Get();
                longView.Init(note.Lane, false);
                PlaceLong(longView, note.Lane, note.Tick, note.EndTick);
                continue;
            }

            Img_ShortNote view = notePool.Get();
            view.Init(note.Lane);
            view.SetSize(size);
            view.RectTransform.anchoredPosition = new Vector2(
                (note.Lane + 0.5f) * laneWidth,
                grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)));
        }
        notePool.EndFrame();
        longNotePool?.EndFrame();

        RefreshPreview();
        RefreshCursorPreview();
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

        preview.gameObject.SetActive(true);
        preview.transform.SetAsLastSibling();
        preview.Init(previewLane, true);
        PlaceLong(preview, previewLane, previewStartTick, previewEndTick);
    }

    /// <summary>
    /// 롱노트를 놓는다. 몸통 아래 끝(피벗) = 시작 y(머리 가운데), 위 끝 = 끝 y(꼬리 끝 막대 가운데), 폭 = 라인 폭 × 0.70.
    /// 머리는 숏노트 크기, 꼬리 끝 막대는 몸통 폭 × 6px. 시작 = 끝이면 몸통 높이 0 (머리가 막대를 덮는다).
    /// </summary>
    private void PlaceLong(Img_LongNote view, int lane, int startTick, int endTick)
    {
        float startY = grid.BeatToLocalY(BeatGridMath.TickToBeat(startTick));
        float endY = grid.BeatToLocalY(BeatGridMath.TickToBeat(endTick));

        view.SetShape(LongBodyWidth, endY - startY, new Vector2(NoteWidth, noteHeight));
        view.RectTransform.anchoredPosition = new Vector2((lane + 0.5f) * grid.LaneWidth, startY);
    }

    /// <summary>
    /// 커서 대상(호버 · 우클릭 삭제)으로 볼 롱노트 구간의 위 끝 y (순수 함수, 시작 y 이상). 그림(몸통 위 끝 = 끝 tick y)과 별개로, 끝 tick 칸을 다음 노트 자리로 비운다.
    /// - 끝 tick에 다음 노트가 없으면: 끝 tick y 바로 아래(LongEndCellTolerancePx)까지. 꼬리 막대 아래쪽 절반은 이 롱노트, 끝 tick y부터는 놓을 자리
    /// - 다음 노트가 있으면: 다음 노트가 그려진 범위 아래 끝(끝 tick y − 노트 높이 ÷ 2)까지
    /// </summary>
    public static float LongHitTop(float startY, float endY, float noteHeight, bool hasNextNote)
    {
        float top = endY - (hasNextNote ? noteHeight * 0.5f : GameConstants.Editor.LongEndCellTolerancePx);
        return Mathf.Max(startY, top);
    }

    /// <summary>이 롱노트의 LongHitTop (그리드 로컬 y). 끝 tick에서 시작하는 같은 라인 노트가 있는지 보고 정한다.</summary>
    private float GetLongHitTop(EditorNote note)
    {
        return LongHitTop(grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)), grid.BeatToLocalY(BeatGridMath.TickToBeat(note.EndTick)),
            noteHeight, data.Contains(note.Lane, note.EndTick));
    }

    /// <summary>y가 롱노트의 커서 대상 범위(LongHitTop, 짧으면 머리 위 끝)보다 위면 true. 숏노트는 항상 false</summary>
    public bool IsAboveLongHit(EditorNote note, float y)
    {
        if (!note.IsLong || grid == null || data == null) return false;
        float headTop = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick)) + noteHeight * 0.5f;
        return y > Mathf.Max(GetLongHitTop(note), headTop);
    }

    // ───── 마우스 미리 보기 ─────

    /// <summary>
    /// 노트가 화면에 그려진 범위 (그리드 로컬, 흰 테두리 여백 전). 숏노트 = 숏노트 크기,
    /// 롱노트 = 머리 아래 끝 ~ max(꼬리 끝 막대 위 끝, 머리 위 끝), 폭 = 노트 폭(머리 폭).
    /// </summary>
    public Rect GetNoteRect(EditorNote note)
    {
        if (grid == null) return Rect.zero;
        float startY = grid.BeatToLocalY(BeatGridMath.TickToBeat(note.Tick));
        float endY = note.IsLong ? grid.BeatToLocalY(BeatGridMath.TickToBeat(note.EndTick)) : startY;
        return NoteRect((note.Lane + 0.5f) * grid.LaneWidth, NoteWidth, startY, endY, note.IsLong, noteHeight);
    }

    /// <summary>
    /// 노트 범위 계산 (순수 함수). 숏노트는 startY 가운데 noteHeight, 롱노트는 위 끝을 꼬리 끝 막대(endY + 막대 높이 ÷ 2)까지 넓힌다.
    /// </summary>
    public static Rect NoteRect(float centerX, float width, float startY, float endY, bool isLong, float noteHeight)
    {
        float bottom = startY - noteHeight * 0.5f;
        float top = startY + noteHeight * 0.5f;
        if (isLong) top = Mathf.Max(top, endY + GameConstants.Editor.EditorLongTailBarHeightPx * 0.5f);
        return new Rect(centerX - width * 0.5f, bottom, width, top - bottom);
    }

    /// <summary>
    /// 흰 테두리 Image 범위 (순수 함수) = 노트 범위 둘레로 (바깥 여백 3px + 선 2px) 넓힌 것.
    /// 선 안쪽 가장자리가 노트에서 3px 떨어진다.
    /// </summary>
    public static Rect HoverRect(Rect noteRect)
    {
        float grow = GameConstants.Editor.NoteHoverPaddingPx + GameConstants.Editor.NoteHoverBorderPx;
        return new Rect(noteRect.xMin - grow, noteRect.yMin - grow, noteRect.width + grow * 2f, noteRect.height + grow * 2f);
    }

    /// <summary>놓을 자리 미리 보기를 보이고 흰 테두리를 숨긴다.</summary>
    public void ShowPlacePreview(int lane, int tick)
    {
        hasHover = false;
        hasPlace = true;
        placeLane = lane;
        placeTick = tick;
        RefreshCursorPreview();
    }

    /// <summary>노트 흰 테두리를 보이고 놓을 자리 미리 보기를 숨긴다.</summary>
    public void ShowNoteHover(EditorNote note)
    {
        hasPlace = false;
        hasHover = true;
        hoverNote = note;
        RefreshCursorPreview();
    }

    /// <summary>마우스 미리 보기(흰 테두리 · 놓을 자리)를 모두 숨긴다.</summary>
    public void HideCursorPreview()
    {
        hasHover = false;
        hasPlace = false;
        RefreshCursorPreview();
    }

    /// <summary>지금 흰 테두리를 그린 노트. 없으면 false (확인용)</summary>
    public bool TryGetHoverNote(out EditorNote note)
    {
        note = hoverNote;
        return hasHover;
    }

    /// <summary>마우스 미리 보기를 지금 그리드 위치에 맞춰 놓는다 (스크롤 · 줌 뒤에도). 노트 · 드래그 미리 보기보다 앞에 그린다.</summary>
    private void RefreshCursorPreview()
    {
        bool showHover = hasHover && grid != null;
        bool showPlace = !showHover && hasPlace && grid != null;

        if (noteHover != null)
        {
            if (noteHover.gameObject.activeSelf != showHover) noteHover.gameObject.SetActive(showHover);
            if (showHover)
            {
                SetRect(noteHover.rectTransform, HoverRect(GetNoteRect(hoverNote)));
                BringToFront(noteHover.transform);
            }
        }

        if (placePreview != null)
        {
            if (placePreview.gameObject.activeSelf != showPlace) placePreview.gameObject.SetActive(showPlace);
            if (showPlace)
            {
                float y = grid.BeatToLocalY(BeatGridMath.TickToBeat(placeTick));
                SetRect(placePreview.rectTransform, NoteRect((placeLane + 0.5f) * grid.LaneWidth, NoteWidth, y, y, false, noteHeight));
                BringToFront(placePreview.transform);
            }
        }
    }

    /// <summary>테두리 · 점선 배율과 색을 UITheme에서 가져와 칠한다 (씬에 색을 직렬화하지 않는다).</summary>
    private void ApplyCursorPreviewStyle()
    {
        UITheme theme = UITheme.Current;
        if (noteHover != null)
        {
            noteHover.color = theme.editorNoteHover;
            noteHover.type = Image.Type.Sliced;
            noteHover.fillCenter = false;
            noteHover.pixelsPerUnitMultiplier = 1f;
            noteHover.raycastTarget = false;
        }
        if (placePreview != null)
        {
            placePreview.color = UITheme.WithAlpha(theme.editorPreview, theme.editorPlaceFillAlpha);
            placePreview.raycastTarget = false;
        }
        if (placePreviewDash != null)
        {
            placePreviewDash.color = theme.editorPreview;
            placePreviewDash.type = Image.Type.Sliced;
            placePreviewDash.pixelsPerUnitMultiplier = GameConstants.Editor.PlacePreviewDashPixelsPerUnitMultiplier;
            placePreviewDash.raycastTarget = false;
        }
    }

    /// <summary>그리드 로컬 범위(왼쪽 아래 기준)를 RectTransform(앵커 · 피벗 왼쪽 아래)에 넣는다.</summary>
    private static void SetRect(RectTransform rect, Rect r)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = r.position;
        rect.sizeDelta = r.size;
    }

    private void BringToFront(Transform child)
    {
        if (child.GetSiblingIndex() != transform.childCount - 1) child.SetAsLastSibling();
    }
}
