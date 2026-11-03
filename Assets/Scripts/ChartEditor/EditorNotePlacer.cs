using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 그리드 클릭으로 노트를 추가·삭제한다. EditorScene.unity의 GridArea에 붙이고, EditorScene이 Init()을 호출한다.
/// - 노트는 직접 바꾸지 않고 ChartEditSession.AddNote / RemoveNote를 거친다.
/// - 클릭은 EventSystem 포인터 이벤트로 받는다. Img_Grid가 클릭을 받아 부모인 GridArea로 전달한다 (노트 오브젝트는 클릭을 받지 않음).
/// - 숏노트 도구일 때만 동작한다: 왼쪽 클릭 추가, 오른쪽 클릭 삭제.
/// - 위치는 클릭한 x의 라인 칸, 클릭한 y를 지금 스냅의 가장 가까운 선으로 반올림한 tick이다.
/// </summary>
public class EditorNotePlacer : MonoBehaviour, IPointerClickHandler
{
    private ChartGridView grid;
    private ChartEditSession session;
    private Func<EditorTool> getCurrentTool;

    /// <summary>
    /// 그리드·편집 세션과 지금 고른 도구를 읽을 함수를 받는다.
    /// </summary>
    public void Init(ChartGridView grid, ChartEditSession session, Func<EditorTool> getCurrentTool)
    {
        this.grid = grid;
        this.session = session;
        this.getCurrentTool = getCurrentTool;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (grid == null || session == null || getCurrentTool == null) return;
        if (getCurrentTool() != EditorTool.ShortNote) return;

        if (!TryGetGridPosition(eventData, out int lane, out int tick)) return;

        if (eventData.button == PointerEventData.InputButton.Left)
            session.AddNote(lane, tick);
        else if (eventData.button == PointerEventData.InputButton.Right)
            session.RemoveNote(lane, tick);
    }

    /// <summary>
    /// 클릭 위치를 라인·tick으로 바꾼다. 그리드 밖이거나 0 ~ 곡 끝을 벗어나면 false.
    /// </summary>
    private bool TryGetGridPosition(PointerEventData eventData, out int lane, out int tick)
    {
        lane = -1;
        tick = -1;

        RectTransform gridRect = grid.GridRect;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        // 피벗 기준 좌표를 그리드 왼쪽 아래 기준으로 바꾼다
        Rect rect = gridRect.rect;
        float x = local.x - rect.xMin;
        float y = local.y - rect.yMin;
        if (x < 0f || x >= rect.width) return false;

        lane = Mathf.Clamp(Mathf.FloorToInt(x / grid.LaneWidth), 0, GameConstants.Editor.LaneCount - 1);
        tick = BeatGridMath.SnapBeatToTick(grid.LocalYToBeat(y), grid.SnapDivision);
        return tick >= 0 && tick <= session.Data.TotalTicks;
    }
}
