using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 그리드 클릭·드래그로 노트를 추가·삭제한다. EditorScene.unity의 GridArea에 붙이고, EditorScene이 Init()을 호출한다.
/// - 노트는 직접 바꾸지 않고 ChartEditSession.AddNote / AddLongNote / RemoveNote를 거친다.
/// - 포인터는 EventSystem 이벤트로 받는다. Img_Grid가 이벤트를 받아 부모인 GridArea로 전달한다 (노트 오브젝트는 이벤트를 받지 않음).
/// - 위치는 누른 x의 라인 칸, y를 지금 스냅의 가장 가까운 선으로 반올림한 tick이다.
/// - 숏노트 도구: 왼쪽 클릭 = 숏노트. 같은 라인에서 1/4박 이상 드래그하면 롱노트, 그보다 짧은 드래그는 클릭으로 본다 (기획서 2-3).
/// - 롱노트 도구: 왼쪽 버튼을 누른 칸 = 시작, 놓은 칸 = 끝. 위아래 어느 쪽으로 끌어도 작은 쪽이 시작이다.
///   드래그하는 동안 끝을 스냅 칸에 맞춘 반투명 미리보기를 보여준다 (EditorNoteLayer).
/// - 오른쪽 클릭(숏노트·롱노트 도구), 지우개 도구의 왼쪽 클릭: 그 자리를 덮는 노트를 지운다 (롱노트는 어느 부분이든).
/// - 배치가 실패하면(너무 짧음, 겹침) 이유를 상태 표시줄에 잠깐 보여준다.
/// </summary>
public class EditorNotePlacer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    private ChartGridView grid;
    private ChartEditSession session;
    private EditorNoteLayer noteLayer;
    private Func<EditorTool> getCurrentTool;
    private Action<string> showMessage;

    // 왼쪽 버튼으로 누르고 있는 동안의 상태 (숏노트·롱노트 도구)
    private bool isPressing;
    private EditorTool pressTool;
    private int pressLane;
    private int pressTick;
    private int dragTick;

    /// <summary>
    /// 그리드·편집 세션·노트 표시(미리보기용)와 지금 고른 도구를 읽을 함수, 안내를 보여줄 함수를 받는다.
    /// </summary>
    /// <param name="showMessage">배치 실패 이유를 상태 표시줄에 잠깐 보여주는 함수</param>
    public void Init(ChartGridView grid, ChartEditSession session, EditorNoteLayer noteLayer, Func<EditorTool> getCurrentTool, Action<string> showMessage)
    {
        this.grid = grid;
        this.session = session;
        this.noteLayer = noteLayer;
        this.getCurrentTool = getCurrentTool;
        this.showMessage = showMessage;
        CancelPress();
    }

    private bool IsReady => grid != null && session != null && getCurrentTool != null;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsReady || eventData.button != PointerEventData.InputButton.Left) return;

        EditorTool tool = getCurrentTool();
        if (tool != EditorTool.ShortNote && tool != EditorTool.LongNote) return;
        if (!TryGetGridPosition(eventData, out int lane, out int tick)) return;

        isPressing = true;
        pressTool = tool;
        pressLane = lane;
        pressTick = tick;
        dragTick = tick;
        UpdatePreview();
    }

    // TODO: 드래그 중 마우스가 그리드 위아래 끝에 가면 화면을 자동으로 스크롤한다 (이번 범위 밖)
    public void OnDrag(PointerEventData eventData)
    {
        if (!isPressing || eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetTickAtY(eventData, out int tick) || tick == dragTick) return;

        dragTick = tick;
        UpdatePreview();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressing || eventData.button != PointerEventData.InputButton.Left) return;

        if (TryGetTickAtY(eventData, out int tick)) dragTick = tick;
        int lane = pressLane;
        int start = pressTick;
        int end = dragTick;
        EditorTool tool = pressTool;
        CancelPress();

        bool isLongDrag = Mathf.Abs(end - start) >= GameConstants.Editor.MinLongNoteTicks;
        NoteEditError error;
        bool added;
        if (tool == EditorTool.LongNote || isLongDrag)
            added = session.AddLongNote(lane, start, end, out error);
        else
            added = session.AddNote(lane, start, out error);

        if (added) return;

        string message = ChartEditSession.GetErrorMessage(error);
        if (message != null) showMessage?.Invoke(message);
        Debug.Log($"[EditorNotePlacer] 노트를 놓지 못했습니다 ({error}): lane {lane}, tick {Mathf.Min(start, end)}~{Mathf.Max(start, end)}", this);
    }

    /// <summary>
    /// 오른쪽 클릭(숏노트·롱노트 도구), 지우개 왼쪽 클릭으로 그 자리를 덮는 노트를 지운다.
    /// 숏노트·롱노트 도구의 왼쪽 버튼은 OnPointerDown / OnPointerUp에서 처리한다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsReady) return;

        EditorTool tool = getCurrentTool();
        bool erase = eventData.button == PointerEventData.InputButton.Right
            ? tool == EditorTool.ShortNote || tool == EditorTool.LongNote || tool == EditorTool.Eraser
            : eventData.button == PointerEventData.InputButton.Left && tool == EditorTool.Eraser;
        if (!erase) return;

        // 스냅 선 위치(숏노트를 누른 자리)를 먼저 보고, 없으면 가리킨 tick 그대로(롱노트 몸통 중간) 본다
        if (TryGetGridPosition(eventData, out int lane, out int snappedTick) && session.RemoveNote(lane, snappedTick)) return;
        if (TryGetGridPosition(eventData, out lane, out int rawTick, false)) session.RemoveNote(lane, rawTick);
    }

    private void OnDisable()
    {
        CancelPress();
    }

    /// <summary>누르고 있던 상태와 미리보기를 지운다.</summary>
    private void CancelPress()
    {
        isPressing = false;
        if (noteLayer != null) noteLayer.ClearPreview();
    }

    /// <summary>
    /// 지금 누른 칸 ~ 드래그한 칸의 롱노트 미리보기를 보여준다.
    /// 숏노트 도구는 1/4박 이상 끌었을 때만 보여준다 (그보다 짧으면 클릭으로 본다).
    /// </summary>
    private void UpdatePreview()
    {
        if (noteLayer == null) return;

        bool show = pressTool == EditorTool.LongNote || Mathf.Abs(dragTick - pressTick) >= GameConstants.Editor.MinLongNoteTicks;
        if (show) noteLayer.SetPreview(pressLane, Mathf.Min(pressTick, dragTick), Mathf.Max(pressTick, dragTick));
        else noteLayer.ClearPreview();
    }

    /// <summary>
    /// 포인터 위치를 라인·tick으로 바꾼다. 그리드 좌우 밖이거나 0 ~ 곡 끝을 벗어나면 false.
    /// </summary>
    /// <param name="snap">true면 tick을 지금 스냅에 맞춘다. 지울 때는 맞추지 않고 가리킨 tick을 그대로 쓴다 (롱노트 몸통 어디든 지우게)</param>
    private bool TryGetGridPosition(PointerEventData eventData, out int lane, out int tick, bool snap = true)
    {
        lane = -1;
        tick = -1;
        if (!TryGetLocalPoint(eventData, out float x, out float y)) return false;
        if (x < 0f || x >= grid.GridRect.rect.width) return false;

        lane = Mathf.Clamp(Mathf.FloorToInt(x / grid.LaneWidth), 0, GameConstants.Editor.LaneCount - 1);
        tick = snap
            ? BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision)
            : BeatGridMath.BeatToTick(grid.LocalYToBeat(y));
        return tick >= 0 && tick <= session.Data.TotalTicks;
    }

    /// <summary>
    /// 드래그 중 포인터 y의 스냅 tick. 그리드 위아래 밖이어도 0 ~ 곡 끝으로 제한해 돌려준다 (라인은 누른 라인 그대로).
    /// </summary>
    private bool TryGetTickAtY(PointerEventData eventData, out int tick)
    {
        tick = -1;
        if (!TryGetLocalPoint(eventData, out _, out float y)) return false;

        tick = Mathf.Clamp(BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision), 0, session.Data.TotalTicks);
        return true;
    }

    /// <summary>화면 좌표를 그리드 왼쪽 아래 기준 좌표로 바꾼다.</summary>
    private bool TryGetLocalPoint(PointerEventData eventData, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        RectTransform gridRect = grid.GridRect;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, eventData.position, eventCamera, out Vector2 local))
            return false;

        // 피벗 기준 좌표를 그리드 왼쪽 아래 기준으로 바꾼다
        Rect rect = gridRect.rect;
        x = local.x - rect.xMin;
        y = local.y - rect.yMin;
        return true;
    }
}
