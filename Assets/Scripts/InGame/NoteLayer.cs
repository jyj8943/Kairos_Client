using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 노트 표시. InGameScene.unity의 "Gear/Notes"에 붙이고, InGameScene이 Init()을 호출한 뒤 매 프레임 Refresh()를 부른다.
/// - 노트 y = 판정선 y + (스크롤 거리(노트 시간) − 스크롤 거리(곡 시간)) × 낙하 속도. 낙하 속도 = 기본 낙하 속도 × 배속 (GameConstants.Play).
///   스크롤 거리는 ScrollMap이 맡는다 (지금은 배속 구간이 없어 곡 시간과 같다). 판정은 계속 곡 시간 기준이다 (NoteJudge).
/// - 숏노트는 NoteView, 롱노트는 LongNoteView를 각각 풀로 재사용한다. 화면에 들어오기 직전에 꺼내고,
///   판정(키로 친 것)되거나 화면 아래로 벗어나면 돌려놓는다. 플레이 중 Instantiate·Destroy와 매 프레임 할당이 없다.
/// - 롱노트 (롱노트 판정 세부 규칙 4)
///   - 머리·꼬리 y를 시작·끝 시간의 스크롤 거리로 각각 구하고 그 사이를 몸통으로 채운다.
///   - 누르는 동안(Held): 머리를 판정선에 붙이고 몸통은 판정선 ~ 꼬리만 그려 점점 짧아진다.
///   - 끝 판정이 나면(Done) 바로 돌려놓는다. 시작 MISS(Missed)면 회색으로 바꿔 꼬리가 화면 아래로 벗어나면 돌려놓는다.
/// - 풀은 Init에서 채보를 보고 동시에 보일 최대 수만큼 미리 만든다 (롱노트는 끝까지 화면에 남는 시간을 넣어 센다).
/// </summary>
public class NoteLayer : MonoBehaviour
{
    [SerializeField] private NoteView notePrefab;
    [SerializeField] private LongNoteView longNotePrefab;
    [Tooltip("화면 위아래 밖에 두는 여유 (월드 단위). 노트가 화면 끝에서 갑자기 나타나거나 사라지지 않게 한다")]
    [Min(0f)] [SerializeField] private float screenMargin = 0.5f;
    [Tooltip("미리 만드는 노트 수에 더하는 여유")]
    [Min(0)] [SerializeField] private int extraPoolCount = 8;

    /// <summary>
    /// 노트 낙하 속도 (월드 유닛/초) = 기본 낙하 속도 × 배속. Init에서 정한다.
    /// </summary>
    public float ScrollSpeed { get; private set; } = GameConstants.Play.DefaultScrollSpeed;

    private readonly Stack<NoteView> pool = new Stack<NoteView>();
    private readonly Stack<LongNoteView> longPool = new Stack<LongNoteView>();
    private readonly List<int> activeNotes = new List<int>();
    private NoteView[] viewByNote;
    private LongNoteView[] longViewByNote;

    private GearView gear;
    private NoteJudge judge;
    private ScrollMap scroll;
    private int nextSpawnIndex;
    private float spawnAheadSeconds;
    private float despawnY;

    /// <summary>미리 만든 노트 오브젝트 수 (숏노트 + 롱노트, 확인용)</summary>
    public int PooledCount => PooledShortCount + PooledLongCount;

    /// <summary>미리 만든 숏노트 오브젝트 수 (확인용)</summary>
    public int PooledShortCount { get; private set; }

    /// <summary>미리 만든 롱노트 오브젝트 수 (확인용)</summary>
    public int PooledLongCount { get; private set; }

    /// <summary>풀이 모자라 플레이 중 새로 만든 수 (확인용, 0이어야 한다)</summary>
    public int GrowCount { get; private set; }

    /// <summary>지금 화면에 있는 노트 수 (확인용)</summary>
    public int ActiveCount => activeNotes.Count;

    /// <summary>
    /// 기어·판정·스크롤 거리·배속을 받아 낙하 속도를 정하고 풀을 미리 채운다.
    /// </summary>
    /// <param name="scroll">곡 시간 → 스크롤 거리 변환 (노트 높이 계산용)</param>
    /// <param name="scrollMultiplier">배속 (GameConstants.Play.MinScrollMultiplier ~ MaxScrollMultiplier 밖이면 범위 안으로 맞춘다)</param>
    public void Init(GearView gear, NoteJudge judge, ScrollMap scroll, float scrollMultiplier)
    {
        this.gear = gear;
        this.judge = judge;
        this.scroll = scroll;

        float multiplier = Mathf.Clamp(scrollMultiplier, GameConstants.Play.MinScrollMultiplier, GameConstants.Play.MaxScrollMultiplier);
        if (!Mathf.Approximately(multiplier, scrollMultiplier))
            Debug.LogWarning($"[NoteLayer] 배속 {scrollMultiplier}이(가) 범위 밖이라 {multiplier}(으)로 맞춥니다.", this);
        ScrollSpeed = GameConstants.Play.DefaultScrollSpeed * multiplier;

        ReleaseAll();
        viewByNote = new NoteView[judge.TotalCount];
        longViewByNote = new LongNoteView[judge.TotalCount];
        nextSpawnIndex = 0;

        // 노트를 꺼내는 기준과 풀 크기는 곡 시간 기준이다 (스크롤 거리 = 곡 시간일 때만 맞다).
        // 배속 변속을 넣으면 시간 대신 스크롤 거리 기준으로 바꿔야 한다.
        spawnAheadSeconds = (gear.ScreenTopY + screenMargin - gear.JudgeLineY) / ScrollSpeed;
        despawnY = gear.ScreenBottomY - screenMargin;

        // 화면에 동시에 있는 노트: 나타난 뒤(시작 − spawnAhead) 화면 아래로 사라질 때까지(끝 + belowSeconds) 겹치는 수의 최대값.
        // 롱노트는 끝 시간까지 화면에 남으므로 숏노트보다 길게 센다
        float belowSeconds = (gear.JudgeLineY - despawnY) / ScrollSpeed;
        int neededShort = CountMaxOverlap(judge.Notes, false, spawnAheadSeconds, belowSeconds) + extraPoolCount;
        int neededLong = CountMaxOverlap(judge.Notes, true, spawnAheadSeconds, belowSeconds);
        if (neededLong > 0) neededLong += extraPoolCount;

        if (neededLong > 0 && longNotePrefab == null)
            Debug.LogError("[NoteLayer] longNotePrefab(LongNote)이 연결되지 않아 롱노트를 그릴 수 없습니다.", this);

        while (PooledShortCount < neededShort) pool.Push(CreateView());
        while (longNotePrefab != null && PooledLongCount < neededLong) longPool.Push(CreateLongView());
        GrowCount = 0;
    }

    /// <summary>
    /// 곡 시간에 맞춰 노트를 꺼내고, 옮기고, 돌려놓는다.
    /// </summary>
    public void Refresh(double songTime)
    {
        if (judge == null) return;

        IReadOnlyList<PlayNote> notes = judge.Notes;
        double songPosition = scroll.PositionAt(songTime);

        while (nextSpawnIndex < notes.Count && notes[nextSpawnIndex].Time - songTime <= spawnAheadSeconds)
        {
            int index = nextSpawnIndex++;
            // 화면에 나오기 전에 이미 판정된 노트는 꺼내지 않는다 (시작 MISS 롱노트는 회색으로 내려보낸다)
            if (judge.IsJudged(index) && judge.GetLongState(index) != LongNoteState.Missed) continue;

            if (notes[index].IsLong)
            {
                if (longNotePrefab == null) continue;
                LongNoteView longView = longPool.Count > 0 ? longPool.Pop() : GrowLong();
                longView.Init(notes[index].Lane);
                longView.gameObject.SetActive(true);
                longViewByNote[index] = longView;
            }
            else
            {
                NoteView view = pool.Count > 0 ? pool.Pop() : Grow();
                view.Init(notes[index].Lane);
                view.gameObject.SetActive(true);
                viewByNote[index] = view;
            }
            activeNotes.Add(index);
        }

        float judgeLineY = gear.JudgeLineY;
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            int index = activeNotes[i];
            PlayNote note = notes[index];
            float x = gear.GetLaneX(note.Lane);
            float headY = judgeLineY + (float)((scroll.PositionAt(note.Time) - songPosition) * ScrollSpeed);

            if (!note.IsLong)
            {
                if (headY < despawnY)
                {
                    Release(i);
                    continue;
                }
                viewByNote[index].SetPosition(x, headY);
                continue;
            }

            LongNoteState state = judge.GetLongState(index);
            if (state == LongNoteState.Done)
            {
                Release(i);
                continue;
            }

            float tailY = judgeLineY + (float)((scroll.PositionAt(note.EndTime) - songPosition) * ScrollSpeed);
            if (state == LongNoteState.Held)
            {
                // 누르는 동안 머리는 판정선에 붙고, 꼬리가 판정선을 지나도(늦게 떼는 중) 판정선 아래로 내려가지 않는다
                headY = judgeLineY;
                if (tailY < judgeLineY) tailY = judgeLineY;
            }
            else if (tailY < despawnY)
            {
                Release(i);
                continue;
            }

            LongNoteView longView = longViewByNote[index];
            longView.SetMissed(state == LongNoteState.Missed);
            longView.SetLayout(x, headY, tailY);
        }
    }

    /// <summary>
    /// 키로 쳐서 판정이 끝난 노트(숏노트, 롱노트 종합)를 바로 돌려놓는다. 놓친 MISS는 화면 아래로 내려간 뒤 돌려놓는다.
    /// 롱노트 시작 판정 때는 부르지 않는다 (누르는 동안 계속 그린다).
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

        // 순서는 상관없으므로 맨 뒤와 바꿔 지운다 (목록을 당기지 않는다)
        int last = activeNotes.Count - 1;
        activeNotes[activeIndex] = activeNotes[last];
        activeNotes.RemoveAt(last);

        NoteView view = viewByNote[noteIndex];
        if (view != null)
        {
            viewByNote[noteIndex] = null;
            view.gameObject.SetActive(false);
            pool.Push(view);
        }

        LongNoteView longView = longViewByNote[noteIndex];
        if (longView != null)
        {
            longViewByNote[noteIndex] = null;
            longView.gameObject.SetActive(false);
            longPool.Push(longView);
        }
    }

    private void ReleaseAll()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--) Release(i);
    }

    private NoteView Grow()
    {
        GrowCount++;
        Debug.LogWarning($"[NoteLayer] 미리 만든 숏노트가 모자라 새로 만듭니다 (총 {PooledShortCount + 1}개).", this);
        return CreateView();
    }

    private LongNoteView GrowLong()
    {
        GrowCount++;
        Debug.LogWarning($"[NoteLayer] 미리 만든 롱노트가 모자라 새로 만듭니다 (총 {PooledLongCount + 1}개).", this);
        return CreateLongView();
    }

    private NoteView CreateView()
    {
        NoteView view = Instantiate(notePrefab, transform);
        view.name = notePrefab.name;
        view.gameObject.SetActive(false);
        PooledShortCount++;
        return view;
    }

    private LongNoteView CreateLongView()
    {
        LongNoteView view = Instantiate(longNotePrefab, transform);
        view.name = longNotePrefab.name;
        view.gameObject.SetActive(false);
        PooledLongCount++;
        return view;
    }

    /// <summary>
    /// 숏노트(또는 롱노트)가 화면에 있는 구간 [시작 − spawnAhead, 끝 + below]이 동시에 겹치는 최대 수. Init에서 한 번만 부른다.
    /// 롱노트는 시작 MISS면 꼬리가 화면 아래로 벗어날 때까지 남으므로 끝 + below까지로 센다.
    /// </summary>
    private static int CountMaxOverlap(IReadOnlyList<PlayNote> notes, bool longNotes, float spawnAhead, float below)
    {
        List<double> starts = new List<double>();
        List<double> ends = new List<double>();
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i].IsLong != longNotes) continue;
            starts.Add(notes[i].Time - spawnAhead);
            ends.Add(notes[i].EndTime + below);
        }
        starts.Sort();
        ends.Sort();

        int max = 0;
        int current = 0;
        int e = 0;
        for (int s = 0; s < starts.Count; s++)
        {
            // 같은 시각이면 들어오는 쪽을 먼저 세서 넉넉하게 잡는다
            while (e < ends.Count && ends[e] < starts[s])
            {
                current--;
                e++;
            }
            current++;
            if (current > max) max = current;
        }
        return max;
    }
}
