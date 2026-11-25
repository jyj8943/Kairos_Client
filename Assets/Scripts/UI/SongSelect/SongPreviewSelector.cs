/// <summary>곡을 골랐을 때 지금 미리 듣기에 할 일</summary>
public enum SongPreviewSelectResult
{
    /// <summary>그대로 둔다</summary>
    None,

    /// <summary>다른 곡을 골랐다: 지금 곡을 줄이기 시작한다</summary>
    Duck,

    /// <summary>지금 곡으로 돌아왔다: 줄이던 것을 되살린다</summary>
    Restore
}

/// <summary>
/// 곡 선택에서 어느 곡의 미리 듣기를 언제 시작할지 (11/25). 일반 클래스라 EditMode 테스트로 검사할 수 있다.
/// - 들어가기 전(Init 중, 로딩 화면이 덮고 있음)에 고른 곡은 기억만 하고, Enter에서 지연 없이 시작한다.
/// - 들어간 뒤 곡을 바꾸면 고른 순간 지금 곡을 줄이고(Duck) StartDelay(0.3초) 뒤에 시작한다. 그 안에 다른 곡으로 바꾸면 앞 요청은 버리고 다시 기다린다.
/// - 지금 재생하는 곡을 다시 고르면(난이도 · 탭 · 채보 줄 바꾸기, 훑다가 돌아옴) 기다리던 요청만 버리고 이어서 재생한다 (줄이던 중이면 Restore).
/// - Leave 뒤에는 아무것도 시작하지 않는다.
/// </summary>
public sealed class SongPreviewSelector
{
    private SongData selected;
    private SongData pending;
    private float remaining;
    private bool isEntered;
    private bool isLeft;

    /// <summary>미리 듣기를 시작한 곡 (없으면 null)</summary>
    public SongData Current { get; private set; }

    /// <summary>0.3초를 기다리는 곡 (없으면 null, 확인용)</summary>
    public SongData Pending => pending;

    /// <summary>들어왔고 아직 떠나지 않았으면 true</summary>
    public bool IsActive => isEntered && !isLeft;

    /// <summary>처음 상태로 (화면 Init)</summary>
    public void Reset()
    {
        selected = null;
        pending = null;
        Current = null;
        isEntered = false;
        isLeft = false;
    }

    /// <summary>곡을 골랐다</summary>
    /// <returns>지금 미리 듣기에 할 일</returns>
    public SongPreviewSelectResult Select(SongData song)
    {
        if (song == null || isLeft) return SongPreviewSelectResult.None;

        selected = song;
        if (!isEntered || song == pending) return SongPreviewSelectResult.None;
        if (song == Current)
        {
            bool wasPending = pending != null;
            pending = null;
            return wasPending ? SongPreviewSelectResult.Restore : SongPreviewSelectResult.None;
        }

        pending = song;
        remaining = GameConstants.SongPreview.StartDelay;
        return SongPreviewSelectResult.Duck;
    }

    /// <summary>화면에 들어왔다 (로딩 화면이 다 걷힌 뒤).</summary>
    /// <returns>바로 시작할 곡 (없으면 null)</returns>
    public SongData Enter()
    {
        if (isEntered || isLeft) return null;
        isEntered = true;
        Current = selected;
        return Current;
    }

    /// <summary>시간을 흘린다.</summary>
    /// <returns>지금 시작할 곡 (없으면 null)</returns>
    public SongData Tick(float deltaTime)
    {
        if (pending == null) return null;

        remaining -= deltaTime;
        if (remaining > 0f) return null;

        Current = pending;
        pending = null;
        return Current;
    }

    /// <summary>화면을 떠난다. 기다리던 요청을 버린다</summary>
    public void Leave()
    {
        isLeft = true;
        pending = null;
        Current = null;
    }
}
