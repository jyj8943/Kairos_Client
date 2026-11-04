using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 노트 표시. InGameScene.unity의 "Gear/Notes"에 붙이고, InGameScene이 Init()을 호출한 뒤 매 프레임 Refresh()를 부른다.
/// - 노트 y = 판정선 y + (노트 시간 − 곡 시간) × 스크롤 속도.
/// - 노트 오브젝트는 풀로 재사용한다. 화면에 들어오기 직전에 꺼내고, 판정(키로 친 것)되거나 화면 아래로 벗어나면 돌려놓는다.
/// - 풀은 Init에서 채보를 보고 동시에 보일 최대 수만큼 미리 만든다. 플레이 중 Instantiate·Destroy와 매 프레임 할당이 없다.
/// </summary>
public class NoteLayer : MonoBehaviour
{
    [SerializeField] private NoteView notePrefab;
    [Tooltip("노트가 내려오는 속도 (월드 단위/초). 기본값은 판정선부터 화면 위까지 약 1.2초")]
    [Min(0.1f)] [SerializeField] private float scrollSpeed = 7f;
    [Tooltip("화면 위아래 밖에 두는 여유 (월드 단위). 노트가 화면 끝에서 갑자기 나타나거나 사라지지 않게 한다")]
    [Min(0f)] [SerializeField] private float screenMargin = 0.5f;
    [Tooltip("미리 만드는 노트 수에 더하는 여유")]
    [Min(0)] [SerializeField] private int extraPoolCount = 8;

    private readonly Stack<NoteView> pool = new Stack<NoteView>();
    private readonly List<int> activeNotes = new List<int>();
    private NoteView[] viewByNote;

    private GearView gear;
    private NoteJudge judge;
    private int nextSpawnIndex;
    private float spawnAheadSeconds;
    private float despawnY;

    /// <summary>미리 만든 노트 오브젝트 수 (확인용)</summary>
    public int PooledCount { get; private set; }

    /// <summary>풀이 모자라 플레이 중 새로 만든 수 (확인용, 0이어야 한다)</summary>
    public int GrowCount { get; private set; }

    /// <summary>지금 화면에 있는 노트 수 (확인용)</summary>
    public int ActiveCount => activeNotes.Count;

    /// <summary>
    /// 기어·판정을 받아 풀을 미리 채운다.
    /// </summary>
    public void Init(GearView gear, NoteJudge judge)
    {
        this.gear = gear;
        this.judge = judge;

        ReleaseAll();
        viewByNote = new NoteView[judge.TotalCount];
        nextSpawnIndex = 0;

        spawnAheadSeconds = (gear.ScreenTopY + screenMargin - gear.JudgeLineY) / scrollSpeed;
        despawnY = gear.ScreenBottomY - screenMargin;

        // 화면에 동시에 있는 노트: 나타난 뒤 화면 아래로 사라질 때까지의 시간 안에 있는 노트 수의 최대값
        float visibleSeconds = spawnAheadSeconds + (gear.JudgeLineY - despawnY) / scrollSpeed;
        int needed = CountMaxNotesInWindow(judge.Notes, visibleSeconds) + extraPoolCount;
        while (PooledCount < needed) pool.Push(CreateView());
        GrowCount = 0;
    }

    /// <summary>
    /// 곡 시간에 맞춰 노트를 꺼내고, 옮기고, 돌려놓는다.
    /// </summary>
    public void Refresh(double songTime)
    {
        if (judge == null) return;

        IReadOnlyList<PlayNote> notes = judge.Notes;

        while (nextSpawnIndex < notes.Count && notes[nextSpawnIndex].Time - songTime <= spawnAheadSeconds)
        {
            int index = nextSpawnIndex++;
            // 화면에 나오기 전에 이미 판정된 노트는 꺼내지 않는다
            if (judge.IsJudged(index)) continue;

            NoteView view = pool.Count > 0 ? pool.Pop() : Grow();
            view.Init(notes[index].Lane);
            view.gameObject.SetActive(true);
            viewByNote[index] = view;
            activeNotes.Add(index);
        }

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            int index = activeNotes[i];
            PlayNote note = notes[index];
            float y = gear.JudgeLineY + (float)((note.Time - songTime) * scrollSpeed);

            if (y < despawnY)
            {
                Release(i);
                continue;
            }

            viewByNote[index].SetPosition(gear.GetLaneX(note.Lane), y);
        }
    }

    /// <summary>
    /// 키로 쳐서 판정된 노트를 바로 돌려놓는다. 놓친 MISS는 화면 아래로 내려간 뒤 돌려놓는다.
    /// </summary>
    public void OnNoteHit(int noteIndex)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] != noteIndex) continue;
            Release(i);
            return;
        }
    }

    private void Release(int activeIndex)
    {
        int noteIndex = activeNotes[activeIndex];
        NoteView view = viewByNote[noteIndex];
        viewByNote[noteIndex] = null;

        // 순서는 상관없으므로 맨 뒤와 바꿔 지운다 (목록을 당기지 않는다)
        int last = activeNotes.Count - 1;
        activeNotes[activeIndex] = activeNotes[last];
        activeNotes.RemoveAt(last);

        view.gameObject.SetActive(false);
        pool.Push(view);
    }

    private void ReleaseAll()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--) Release(i);
    }

    private NoteView Grow()
    {
        GrowCount++;
        Debug.LogWarning($"[NoteLayer] 미리 만든 노트가 모자라 새로 만듭니다 (총 {PooledCount + 1}개).", this);
        return CreateView();
    }

    private NoteView CreateView()
    {
        NoteView view = Instantiate(notePrefab, transform);
        view.name = notePrefab.name;
        view.gameObject.SetActive(false);
        PooledCount++;
        return view;
    }

    /// <summary>
    /// windowSeconds 길이 구간 안에 들어가는 노트 수의 최대값 (노트는 시간 순).
    /// </summary>
    private static int CountMaxNotesInWindow(IReadOnlyList<PlayNote> notes, float windowSeconds)
    {
        int max = 0;
        int start = 0;
        for (int end = 0; end < notes.Count; end++)
        {
            while (notes[end].Time - notes[start].Time > windowSeconds) start++;
            if (end - start + 1 > max) max = end - start + 1;
        }
        return max;
    }
}
