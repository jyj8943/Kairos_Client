using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드 클릭·드래그로 노트를 추가·삭제한다. EditorScene.unity의 GridArea에 붙이고, EditorScene이 Init()을 호출한다.
/// - 노트는 직접 바꾸지 않고 ChartEditSession.AddNote / AddLongNote / RemoveNote를 거친다.
/// - 포인터는 EventSystem 이벤트로 받는다. Img_Grid가 이벤트를 받아 부모인 GridArea로 전달한다 (노트 오브젝트는 이벤트를 받지 않음).
/// - 위치는 누른 x의 라인 칸, y를 지금 스냅의 가장 가까운 선으로 반올림한 tick이다.
/// - 노트 도구: 왼쪽 클릭 = 숏노트, 끌기 = 롱노트 (누른 칸 = 시작, 놓은 칸 = 끝. 위아래 어느 쪽으로 끌어도 작은 쪽이 시작이다).
///   틱 차이 1/4박 이상 + 마우스를 세로로 LongNoteDragMinPx 이상 끌었을 때만 롱노트다 (IsLongNoteDrag, 기획서 2-3). 아니면 숏노트.
///   롱노트가 되는 동안 끝을 스냅 칸에 맞춘 반투명 미리보기를 보여준다 (EditorNoteLayer).
/// - 오른쪽 클릭(노트 도구): 화면에 그려진 노트를 지운다 (숏노트는 가장자리까지, 롱노트는 어느 부분이든. 노트가 겹쳐 보이면 가까운 쪽).
/// - BPM 구간 도구: 노트를 찍지 않는다. 왼쪽 클릭 = 구간선 근처(bpmSectionPickPixels 안)면 그 구간, 아니면 스냅 위치로 추가·수정 요청,
///   오른쪽 클릭 = 구간선 근처면 삭제 요청. 실제 처리(창·확인 팝업)는 EditorScene이 맡는다.
/// - 배치가 실패하면(너무 짧음, 겹침) 이유를 상태 표시줄에 잠깐 보여준다.
/// - 마우스 미리 보기 (노트 도구, 11/20): 매 프레임 커서 위치를 다시 보고(스크롤 · 줌으로 노트가 움직여도 맞게)
///   노트 위면 흰 테두리, 빈 칸이면 놓을 자리 미리 보기를 EditorNoteLayer에 보여 달라고 한다. 노트 판정은 우클릭 삭제와 같은 TryFindNoteAt이다.
///   그리드 밖 · BPM 구간 도구 · 재생 중 · 팝업 열림 · 드래그 중에는 둘 다 숨긴다.
/// </summary>
public class EditorNotePlacer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("BPM 구간 도구")]
    [Tooltip("구간선을 클릭한 것으로 보는 거리 (그리드 로컬 px, 위아래)")]
    [Min(0f)] [SerializeField] private float bpmSectionPickPixels = 10f;

    private ChartGridView grid;
    private Action<int> onBpmSectionClicked;
    private Action<int> onBpmSectionRemoveRequested;
    private ChartEditSession session;
    private EditorNoteLayer noteLayer;
    private Func<EditorTool> getCurrentTool;
    private Action<string> showMessage;
    private Func<bool> isPlaying;

    // 왼쪽 버튼으로 누르고 있는 동안의 상태 (노트 도구). y는 그리드 로컬 px이라 스크롤과 상관없이 마우스가 움직인 거리다
    private bool isPressing;
    private int pressLane;
    private int pressTick;
    private int dragTick;
    private float pressY;
    private float dragY;

    /// <summary>
    /// 그리드·편집 세션·노트 표시(미리보기용)와 지금 고른 도구를 읽을 함수, 안내를 보여줄 함수를 받는다.
    /// </summary>
    /// <param name="showMessage">배치 실패 이유를 상태 표시줄에 잠깐 보여주는 함수</param>
    /// <param name="onBpmSectionClicked">BPM 구간 도구 왼쪽 클릭: 구간 tick(구간선 위) 또는 스냅 tick(빈 곳)</param>
    /// <param name="onBpmSectionRemoveRequested">BPM 구간 도구 오른쪽 클릭: 구간선 위면 그 구간 tick</param>
    /// <param name="isPlaying">재생 중이면 true (마우스 미리 보기를 숨긴다)</param>
    public void Init(ChartGridView grid, ChartEditSession session, EditorNoteLayer noteLayer, Func<EditorTool> getCurrentTool, Action<string> showMessage,
                     Action<int> onBpmSectionClicked, Action<int> onBpmSectionRemoveRequested, Func<bool> isPlaying)
    {
        this.isPlaying = isPlaying;
        this.onBpmSectionClicked = onBpmSectionClicked;
        this.onBpmSectionRemoveRequested = onBpmSectionRemoveRequested;
        this.grid = grid;
        this.session = session;
        this.noteLayer = noteLayer;
        this.getCurrentTool = getCurrentTool;
        this.showMessage = showMessage;
        CancelPress();
    }

    private bool IsReady => grid != null && session != null && getCurrentTool != null;

    /// <summary>왼쪽 버튼으로 누르고(드래그하고) 있는 중이면 true. 그동안은 실행 취소·다시 실행을 하지 않는다</summary>
    public bool IsPressing => isPressing;

    /// <summary>테스트용 커서 화면 좌표. 값이 있으면 실제 마우스 대신 이 좌표로 마우스 미리 보기를 갱신한다.</summary>
    public Vector2? PointerScreenOverride { get; set; }

    /// <summary>지금 마우스 미리 보기 상태 (확인용)</summary>
    public CursorPreviewState CursorState { get; private set; }

    /// <summary>마우스 미리 보기 상태</summary>
    public enum CursorPreviewState
    {
        /// <summary>아무것도 안 보임</summary>
        Hidden,
        /// <summary>빈 칸: 놓을 자리 미리 보기</summary>
        Place,
        /// <summary>노트 위: 흰 테두리</summary>
        NoteHover
    }

    private void Update()
    {
        if (!IsReady || noteLayer == null) return;

        Vector2? pointer = PointerScreenOverride;
        if (!pointer.HasValue && Mouse.current != null) pointer = Mouse.current.position.ReadValue();
        RefreshCursorPreview(pointer);
    }

    /// <summary>
    /// 커서 화면 좌표로 마우스 미리 보기를 정한다 (매 프레임). 노트 위면 흰 테두리, 빈 칸이면 놓을 자리, 숨길 때는 둘 다 숨긴다.
    /// </summary>
    private void RefreshCursorPreview(Vector2? screenPoint)
    {
        bool blocked = getCurrentTool() != EditorTool.Note
                       || isPressing
                       || (isPlaying != null && isPlaying())
                       || (UIManager.HasInstance && GameManager.UI.HasOpenPopup)
                       || !screenPoint.HasValue;

        if (!blocked && TryScreenToLocal(screenPoint.Value, out float x, out float y) && IsInsideGrid(x, y))
        {
            if (TryFindNoteAt(x, y, out EditorNote note))
            {
                noteLayer.ShowNoteHover(note);
                CursorState = CursorPreviewState.NoteHover;
                return;
            }

            int tick = BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision);
            if (IsTickInSong(tick))
            {
                noteLayer.ShowPlacePreview(LaneAt(x), tick);
                CursorState = CursorPreviewState.Place;
                return;
            }
        }

        noteLayer.HideCursorPreview();
        CursorState = CursorPreviewState.Hidden;
    }

    /// <summary>
    /// 그리드 로컬 좌표(왼쪽 아래 기준)에 있는 노트를 찾는다. 우클릭 삭제와 마우스 흰 테두리가 같이 쓴다 (테두리가 보이는 노트 = 우클릭하면 지워지는 노트).
    /// 순서: 화면에 그려진 노트(EditorNoteLayer.TryFindNoteAtLocalY, 노트가 스냅 선 간격보다 커도 가장자리에서 찾게)
    /// → 스냅 선 위치(숏노트를 누른 자리) → 가리킨 tick 그대로(롱노트 몸통 중간).
    /// 롱노트는 끝 tick y부터 위(끝 tick 칸)를 가리키면 대상이 아니다: 다음 노트가 있으면 그 노트, 비어 있으면 놓을 자리 미리 보기다 (EditorNoteLayer.LongHitTop).
    /// </summary>
    public bool TryFindNoteAt(float x, float y, out EditorNote note)
    {
        note = default;
        if (!IsReady || x < 0f || x >= grid.GridRect.rect.width) return false;

        int lane = LaneAt(x);
        if (noteLayer != null && noteLayer.TryFindNoteAtLocalY(lane, y, out note)) return true;

        int snappedTick = BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision);
        if (TryFindAtTick(lane, snappedTick, y, out note)) return true;

        int rawTick = BeatGridMath.BeatToTick(grid.LocalYToBeat(y));
        return TryFindAtTick(lane, rawTick, y, out note);
    }

    /// <summary>tick을 차지하는 노트를 찾는다. 롱노트의 커서 대상 범위보다 위(끝 tick 칸)를 가리키면 없는 것으로 본다.</summary>
    private bool TryFindAtTick(int lane, int tick, float y, out EditorNote note)
    {
        note = default;
        if (!IsTickInSong(tick) || !session.Data.TryFindAt(lane, tick, out EditorNote found)) return false;
        if (noteLayer != null && noteLayer.IsAboveLongHit(found, y)) return false;

        note = found;
        return true;
    }

    /// <summary>화면 좌표로 TryFindNoteAt을 부른다 (확인용 · 테스트용)</summary>
    public bool TryFindNoteAtScreen(Vector2 screenPoint, out EditorNote note)
    {
        note = default;
        return IsReady && TryScreenToLocal(screenPoint, out float x, out float y) && TryFindNoteAt(x, y, out note);
    }

    private bool IsTickInSong(int tick) => tick >= 0 && tick <= session.Data.TotalTicks;

    private int LaneAt(float x) => Mathf.Clamp(Mathf.FloorToInt(x / grid.LaneWidth), 0, GameConstants.Editor.LaneCount - 1);

    private bool IsInsideGrid(float x, float y)
    {
        Rect rect = grid.GridRect.rect;
        return x >= 0f && x < rect.width && y >= 0f && y < rect.height;
    }

    /// <summary>
    /// 이 드래그가 롱노트인지. 틱 차이가 MinLongNoteTicks(1/4박) 이상이고, 마우스를 세로로 LongNoteDragMinPx 이상 끌었을 때만 롱노트다.
    /// 놓기와 드래그 미리 보기가 같이 쓴다.
    /// </summary>
    /// <param name="dragPixels">누른 곳에서 세로로 끈 거리 (그리드 로컬 px, 부호 무시)</param>
    public static bool IsLongNoteDrag(int pressTick, int dragTick, float dragPixels)
    {
        return Mathf.Abs(dragTick - pressTick) >= GameConstants.Editor.MinLongNoteTicks
               && Mathf.Abs(dragPixels) >= GameConstants.Editor.LongNoteDragMinPx;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsReady || eventData.button != PointerEventData.InputButton.Left) return;

        if (getCurrentTool() != EditorTool.Note) return;
        if (!TryGetGridPosition(eventData, out int lane, out int tick, out float y)) return;

        isPressing = true;
        pressLane = lane;
        pressTick = tick;
        dragTick = tick;
        pressY = y;
        dragY = y;
        UpdatePreview();
    }

    // TODO: 드래그 중 마우스가 그리드 위아래 끝에 가면 화면을 자동으로 스크롤한다 (이번 범위 밖)
    public void OnDrag(PointerEventData eventData)
    {
        if (!isPressing || eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetTickAtY(eventData, out int tick, out float y)) return;

        dragTick = tick;
        dragY = y;
        UpdatePreview();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressing || eventData.button != PointerEventData.InputButton.Left) return;

        if (TryGetTickAtY(eventData, out int tick, out float y))
        {
            dragTick = tick;
            dragY = y;
        }
        int lane = pressLane;
        int start = pressTick;
        int end = dragTick;
        bool isLong = IsLongNoteDrag(start, end, dragY - pressY);
        CancelPress();

        NoteEditError error;
        bool added;
        if (isLong)
            added = session.AddLongNote(lane, start, end, out error);
        else
            added = session.AddNote(lane, start, out error);

        if (added) return;

        string message = ChartEditSession.GetErrorMessage(error);
        if (message != null) showMessage?.Invoke(message);
        Debug.Log($"[EditorNotePlacer] 노트를 놓지 못했습니다 ({error}): lane {lane}, tick {Mathf.Min(start, end)}~{Mathf.Max(start, end)}", this);
    }

    /// <summary>
    /// 오른쪽 클릭(노트 도구)으로 그 자리를 덮는 노트를 지운다. BPM 구간 도구면 구간 추가·수정·삭제를 요청한다.
    /// 노트 도구의 왼쪽 버튼은 OnPointerDown / OnPointerUp에서 처리한다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsReady) return;

        if (getCurrentTool() == EditorTool.BpmSection)
        {
            HandleBpmSectionClick(eventData);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Right) return;

        // 마우스 흰 테두리와 같은 함수로 찾는다 (그려진 노트 → 스냅 선 → 가리킨 tick)
        if (TryGetLocalPoint(eventData, out float x, out float y) && TryFindNoteAt(x, y, out EditorNote note))
            session.RemoveNote(note.Lane, note.Tick);
    }

    /// <summary>
    /// BPM 구간 도구 클릭. 구간선 근처면 그 구간, 왼쪽 클릭이 빈 곳이면 지금 스냅 위치를 넘긴다.
    /// </summary>
    private void HandleBpmSectionClick(PointerEventData eventData)
    {
        if (!TryGetLocalPoint(eventData, out float x, out float y) || x < 0f || x >= grid.GridRect.rect.width) return;

        int nearTick = FindSectionTickNear(y);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (nearTick >= 0) onBpmSectionRemoveRequested?.Invoke(nearTick);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (nearTick >= 0)
        {
            onBpmSectionClicked?.Invoke(nearTick);
            return;
        }

        int tick = BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision);
        if (tick >= 0 && tick <= session.Data.TotalTicks) onBpmSectionClicked?.Invoke(tick);
    }

    /// <summary>y(그리드 로컬)에서 bpmSectionPickPixels 안의 가장 가까운 구간 tick. 없으면 −1</summary>
    private int FindSectionTickNear(float y)
    {
        int best = -1;
        float bestDistance = bpmSectionPickPixels;
        foreach (BpmSection section in session.BpmSections)
        {
            float distance = Mathf.Abs(grid.BeatToLocalY(section.Beat) - y);
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = section.Tick;
        }
        return best;
    }

    private void OnDisable()
    {
        CancelPress();
        if (noteLayer != null) noteLayer.HideCursorPreview();
        CursorState = CursorPreviewState.Hidden;
    }

    /// <summary>누르고 있던 상태와 미리보기를 지운다.</summary>
    private void CancelPress()
    {
        isPressing = false;
        if (noteLayer != null) noteLayer.ClearPreview();
    }

    /// <summary>
    /// 지금 누른 칸 ~ 드래그한 칸의 롱노트 미리보기를 보여준다.
    /// 놓으면 롱노트가 되는 동안만 보여준다 (IsLongNoteDrag, 아니면 숏노트로 본다).
    /// </summary>
    private void UpdatePreview()
    {
        if (noteLayer == null) return;

        if (IsLongNoteDrag(pressTick, dragTick, dragY - pressY)) noteLayer.SetPreview(pressLane, Mathf.Min(pressTick, dragTick), Mathf.Max(pressTick, dragTick));
        else noteLayer.ClearPreview();
    }

    /// <summary>
    /// 포인터 위치를 라인·스냅 tick으로 바꾼다. 그리드 좌우 밖이거나 0 ~ 곡 끝을 벗어나면 false.
    /// </summary>
    /// <param name="y">포인터 y (그리드 로컬 px)</param>
    private bool TryGetGridPosition(PointerEventData eventData, out int lane, out int tick, out float y)
    {
        lane = -1;
        tick = -1;
        if (!TryGetLocalPoint(eventData, out float x, out y)) return false;
        if (x < 0f || x >= grid.GridRect.rect.width) return false;

        lane = Mathf.Clamp(Mathf.FloorToInt(x / grid.LaneWidth), 0, GameConstants.Editor.LaneCount - 1);
        tick = BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision);
        return tick >= 0 && tick <= session.Data.TotalTicks;
    }

    /// <summary>
    /// 드래그 중 포인터 y의 스냅 tick. 그리드 위아래 밖이어도 0 ~ 곡 끝으로 제한해 돌려준다 (라인은 누른 라인 그대로).
    /// </summary>
    /// <param name="y">포인터 y (그리드 로컬 px, 제한하지 않음)</param>
    private bool TryGetTickAtY(PointerEventData eventData, out int tick, out float y)
    {
        tick = -1;
        if (!TryGetLocalPoint(eventData, out _, out y)) return false;

        tick = Mathf.Clamp(BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision), 0, session.Data.TotalTicks);
        return true;
    }

    /// <summary>포인터 이벤트 위치를 그리드 왼쪽 아래 기준 좌표로 바꾼다.</summary>
    private bool TryGetLocalPoint(PointerEventData eventData, out float x, out float y)
    {
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return TryScreenToLocal(eventData.position, eventCamera, out x, out y);
    }

    /// <summary>화면 좌표를 그리드 왼쪽 아래 기준 좌표로 바꾼다 (카메라는 그리드가 속한 Canvas에서 고른다).</summary>
    private bool TryScreenToLocal(Vector2 screenPoint, out float x, out float y)
    {
        Canvas canvas = grid.GridRect.GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        Camera eventCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        return TryScreenToLocal(screenPoint, eventCamera, out x, out y);
    }

    private bool TryScreenToLocal(Vector2 screenPoint, Camera eventCamera, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        RectTransform gridRect = grid.GridRect;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPoint, eventCamera, out Vector2 local))
            return false;

        // 피벗 기준 좌표를 그리드 왼쪽 아래 기준으로 바꾼다
        Rect rect = gridRect.rect;
        x = local.x - rect.xMin;
        y = local.y - rect.yMin;
        return true;
    }
}
