/// <summary>
/// 에디터 → 인게임 테스트 플레이 데이터. InGameEntryData.testPlay에 담아 보낸다.
/// - 채보는 파일이 아니라 메모리의 ChartData를 그대로 쓴다 (저장하지 않아도 된다). 인게임은 읽기만 한다.
/// </summary>
public class TestPlayData
{
    public string songId;

    /// <summary>편집 중인 채보 (저장하지 않았어도 메모리 그대로)</summary>
    public ChartData chart;

    /// <summary>시작 위치 (박). 이보다 앞의 노트는 건너뛴다</summary>
    public float startBeat;

    /// <summary>자동 플레이로 시작하면 true (에디터의 자동 테스트). 재시작·다시 하기도 같은 값으로 시작한다</summary>
    public bool autoPlay;

    /// <summary>에디터로 돌아갈 때 되돌릴 편집 상태</summary>
    public EditorRestoreState editorState;

    /// <summary>
    /// 에디터로 돌아갈 때 넘길 진입 데이터를 만든다.
    /// </summary>
    public EditorEntryData CreateEditorEntry()
    {
        return new EditorEntryData
        {
            songId = songId,
            chartId = chart != null ? chart.chartId : null,
            chartName = chart != null ? chart.chartName : null,
            level = chart != null ? chart.level : 0,
            restore = editorState
        };
    }
}
