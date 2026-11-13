using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 되돌릴 수 있는 편집 하나 (노트 추가·삭제, 다음 작업의 BPM 구간 추가·수정·삭제 등).
/// - 기록은 값(레인·tick 등)만 들고, 오브젝트 참조를 들고 있지 않는다.
/// - Apply / Revert는 편집 세션의 기록을 남기지 않는 내부 경로로 바꾼다 (검사는 그대로 거친다).
/// </summary>
public interface IEditAction
{
    /// <summary>상태 표시줄에 보여 줄 짧은 설명 (예: "숏노트 추가")</summary>
    string Description { get; }

    /// <summary>편집을 다시 적용한다 (다시 실행). 실패하면 false이고 아무것도 바꾸지 않는다</summary>
    bool Apply(ChartEditSession session);

    /// <summary>편집을 되돌린다 (실행 취소). 실패하면 false이고 아무것도 바꾸지 않는다</summary>
    bool Revert(ChartEditSession session);
}

/// <summary>
/// 여러 편집을 하나로 묶은 기록. 실행 취소 한 번에 전부 되돌린다 (예: BPM 구간 삭제 + 의미 없어진 다음 구간 삭제).
/// 적용은 넣은 순서대로, 되돌리기는 거꾸로 한다. 도중에 하나가 실패하면 이미 한 것을 되돌리고 false를 돌려준다.
/// </summary>
public class GroupEditAction : IEditAction
{
    private readonly List<IEditAction> actions;

    public string Description { get; }

    /// <summary>묶은 편집 수</summary>
    public int Count => actions.Count;

    public GroupEditAction(string description, IEnumerable<IEditAction> actions)
    {
        Description = description;
        this.actions = new List<IEditAction>(actions);
    }

    public bool Apply(ChartEditSession session)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Apply(session)) continue;

            for (int j = i - 1; j >= 0; j--) actions[j].Revert(session);
            return false;
        }
        return true;
    }

    public bool Revert(ChartEditSession session)
    {
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i].Revert(session)) continue;

            for (int j = i + 1; j < actions.Count; j++) actions[j].Apply(session);
            return false;
        }
        return true;
    }
}

/// <summary>
/// 노트 하나 추가·삭제 기록 (숏노트·롱노트). 노트는 레인·시작 tick·끝 tick 값으로 들고 있다.
/// </summary>
public class NoteEditAction : IEditAction
{
    private readonly EditorNote note;
    private readonly bool isAdd;

    /// <summary>기록한 노트 (확인용)</summary>
    public EditorNote Note => note;

    /// <summary>추가 기록이면 true, 삭제 기록이면 false</summary>
    public bool IsAdd => isAdd;

    public string Description
    {
        get
        {
            if (note.IsLong) return isAdd ? GameConstants.Editor.HistoryAddLongNote : GameConstants.Editor.HistoryRemoveLongNote;
            return isAdd ? GameConstants.Editor.HistoryAddShortNote : GameConstants.Editor.HistoryRemoveShortNote;
        }
    }

    /// <param name="note">추가했거나 지운 노트</param>
    /// <param name="isAdd">추가면 true, 삭제면 false</param>
    public NoteEditAction(EditorNote note, bool isAdd)
    {
        this.note = note;
        this.isAdd = isAdd;
    }

    public bool Apply(ChartEditSession session)
    {
        return isAdd ? session.ApplyAddNote(note) : session.ApplyRemoveNote(note);
    }

    public bool Revert(ChartEditSession session)
    {
        return isAdd ? session.ApplyRemoveNote(note) : session.ApplyAddNote(note);
    }
}

/// <summary>
/// 편집 기록 (실행 취소 · 다시 실행). MonoBehaviour가 아닌 일반 클래스라 EditMode 테스트로 검사한다.
/// - 편집 하나 = 기록 하나(IEditAction). 새 편집이 들어오면 다시 실행 목록은 비운다.
/// - 최대 개수(GameConstants.Editor.MaxUndoCount)를 넘으면 가장 오래된 기록부터 버린다.
/// - 저장 시점: 기록마다 번호를 붙이고, 지금 상태 번호(맨 위 기록의 번호)가 저장할 때의 번호와 같으면 저장 상태로 본다.
///   새 편집으로 갈라지면 저장 시점은 다시 돌아올 수 없게 된다 (번호가 새로 붙으므로).
/// - 묶음: BeginGroup ~ EndGroup 사이에 Record한 편집은 GroupEditAction 하나로 남는다.
/// - 테스트 플레이 왕복 때는 이 객체를 씬 데이터(EditorRestoreState.history)에 담아 그대로 넘긴다.
/// </summary>
public class EditHistory
{
    /// <summary>기록 하나와 그 기록을 적용한 뒤의 상태 번호</summary>
    private readonly struct Entry
    {
        public readonly IEditAction Action;
        public readonly int StateId;

        public Entry(IEditAction action, int stateId)
        {
            Action = action;
            StateId = stateId;
        }
    }

    /// <summary>저장 시점에 다시 돌아올 수 없을 때의 번호 (기록하지 않는 변경이 있었을 때)</summary>
    private const int UnreachableStateId = -1;

    private readonly List<Entry> undoList = new List<Entry>();
    private readonly List<Entry> redoList = new List<Entry>();
    private readonly int capacity;

    /// <summary>다음 기록에 붙일 번호</summary>
    private int nextStateId = 1;

    /// <summary>실행 취소 목록이 비었을 때의 상태 번호 (처음, 또는 오래된 기록을 버린 뒤)</summary>
    private int baseStateId;

    /// <summary>저장(또는 불러오기)한 시점의 상태 번호</summary>
    private int savedStateId;

    /// <summary>묶는 중인 편집. 묶지 않는 중이면 null</summary>
    private List<IEditAction> pendingGroup;
    private string pendingGroupDescription;

    /// <param name="capacity">실행 취소 기록 최대 개수 (1 이상)</param>
    public EditHistory(int capacity = GameConstants.Editor.MaxUndoCount)
    {
        this.capacity = Mathf.Max(1, capacity);
    }

    /// <summary>실행 취소 기록 최대 개수</summary>
    public int Capacity => capacity;

    public int UndoCount => undoList.Count;
    public int RedoCount => redoList.Count;

    /// <summary>묶는 중이면 true (그동안은 실행 취소·다시 실행을 하지 않는다)</summary>
    public bool IsGrouping => pendingGroup != null;

    public bool CanUndo => !IsGrouping && undoList.Count > 0;
    public bool CanRedo => !IsGrouping && redoList.Count > 0;

    /// <summary>다음 실행 취소의 설명. 없으면 null</summary>
    public string UndoDescription => undoList.Count > 0 ? undoList[undoList.Count - 1].Action.Description : null;

    /// <summary>다음 다시 실행의 설명. 없으면 null</summary>
    public string RedoDescription => redoList.Count > 0 ? redoList[redoList.Count - 1].Action.Description : null;

    /// <summary>지금 상태가 저장한 시점과 같으면 true (묶는 중에 편집이 들어갔으면 false)</summary>
    public bool IsAtSavedState => CurrentStateId == savedStateId && (pendingGroup == null || pendingGroup.Count == 0);

    private int CurrentStateId => undoList.Count > 0 ? undoList[undoList.Count - 1].StateId : baseStateId;

    /// <summary>
    /// 이미 적용한 편집을 기록한다. 다시 실행 목록은 비운다. 묶는 중이면 묶음에 넣는다.
    /// </summary>
    public void Record(IEditAction action)
    {
        if (action == null) return;

        if (pendingGroup != null)
        {
            pendingGroup.Add(action);
            return;
        }

        Push(action);
    }

    /// <summary>
    /// 묶음을 시작한다. EndGroup까지 Record한 편집은 실행 취소 한 번에 함께 되돌아간다. 이미 묶는 중이면 무시한다 (겹쳐 묶지 않음).
    /// </summary>
    public void BeginGroup(string description)
    {
        if (pendingGroup != null)
        {
            Debug.LogWarning($"[EditHistory] 이미 묶는 중이라 새 묶음을 시작하지 않습니다: {description}");
            return;
        }

        pendingGroup = new List<IEditAction>();
        pendingGroupDescription = description;
    }

    /// <summary>
    /// 묶음을 끝낸다. 편집이 하나도 없으면 기록하지 않고, 하나뿐이면 그 편집을 그대로 기록한다.
    /// </summary>
    public void EndGroup()
    {
        if (pendingGroup == null) return;

        List<IEditAction> actions = pendingGroup;
        string description = pendingGroupDescription;
        pendingGroup = null;
        pendingGroupDescription = null;

        if (actions.Count == 0) return;
        Push(actions.Count == 1 ? actions[0] : new GroupEditAction(description, actions));
    }

    /// <summary>
    /// 마지막 편집을 되돌린다.
    /// </summary>
    /// <returns>되돌린 기록. 되돌릴 것이 없거나 실패하면 null</returns>
    public IEditAction Undo(ChartEditSession session)
    {
        if (!CanUndo) return null;

        Entry entry = undoList[undoList.Count - 1];
        undoList.RemoveAt(undoList.Count - 1);
        if (!entry.Action.Revert(session))
        {
            // 되돌리지 못한 기록은 버린다. 노트는 그대로지만 기록 위치가 바뀌었으므로 저장 시점과 같다고 보지 않는다
            Debug.LogWarning($"[EditHistory] 실행 취소하지 못해 기록을 버립니다: {entry.Action.Description}");
            redoList.Clear();
            savedStateId = UnreachableStateId;
            return null;
        }

        redoList.Add(entry);
        return entry.Action;
    }

    /// <summary>
    /// 마지막으로 되돌린 편집을 다시 적용한다.
    /// </summary>
    /// <returns>다시 적용한 기록. 없거나 실패하면 null</returns>
    public IEditAction Redo(ChartEditSession session)
    {
        if (!CanRedo) return null;

        Entry entry = redoList[redoList.Count - 1];
        redoList.RemoveAt(redoList.Count - 1);
        if (!entry.Action.Apply(session))
        {
            Debug.LogWarning($"[EditHistory] 다시 실행하지 못해 기록을 버립니다: {entry.Action.Description}");
            redoList.Clear();
            return null;
        }

        undoList.Add(entry);
        return entry.Action;
    }

    /// <summary>지금 상태를 저장 시점으로 기억한다 (저장 성공)</summary>
    public void MarkSaved()
    {
        savedStateId = CurrentStateId;
    }

    /// <summary>
    /// 기록하지 않는 변경(개발용 BPM·offset 조정 등)이 있어, 실행 취소로는 저장 시점에 돌아갈 수 없게 한다.
    /// </summary>
    public void MarkSavedUnreachable()
    {
        savedStateId = UnreachableStateId;
    }

    /// <summary>기록을 모두 비우고 지금 상태를 저장 시점으로 본다 (채보 불러오기·새 채보 시작)</summary>
    public void Clear()
    {
        undoList.Clear();
        redoList.Clear();
        pendingGroup = null;
        pendingGroupDescription = null;
        baseStateId = nextStateId++;
        savedStateId = baseStateId;
    }

    private void Push(IEditAction action)
    {
        redoList.Clear();
        undoList.Add(new Entry(action, nextStateId++));

        // 가장 오래된 기록부터 버린다. 버린 기록을 적용한 뒤의 상태가 이제 맨 아래 상태다
        while (undoList.Count > capacity)
        {
            baseStateId = undoList[0].StateId;
            undoList.RemoveAt(0);
        }
    }
}
