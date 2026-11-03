using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 씬 진입점. EditorScene.unity 루트의 "EditorScene" 오브젝트에 붙인다.
/// </summary>
public class EditorScene : SceneBase
{
    [Header("편집 세션")]
    [SerializeField] private ChartEditSession session;

    [Header("화면")]
    [SerializeField] private ChartGridView grid;
    [SerializeField] private EditorNoteLayer noteLayer;
    [SerializeField] private EditorNotePlacer notePlacer;
    [SerializeField] private Canvas_Editor canvas;

    // TODO: 채보 데이터 연결 시 교체 (넘겨받은 songId의 곡 정보·채보의 BPM과 곡 길이를 쓴다)
    [Header("테스트 데이터")]
    [SerializeField] private float testBpm = 150f;
    [Tooltip("곡 길이 (초)")]
    [SerializeField] private float testSongLengthSeconds = 144f;

    /// <summary>
    /// 에디터 씬 초기화. 곡 고르기 씬이 넘긴 채보 정보를 받아
    /// 편집 세션 → 그리드 → 노트 표시 → 클릭 처리 → Canvas_Editor 순으로 Init한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[EditorScene] Init");

        // 채보 정보가 없으면(씬을 직접 실행) 세션이 테스트 기본값을 쓰고 경고를 남긴다
        EditorEntryData entry = GameManager.Scene.TakeSceneData<EditorEntryData>();

        if (session == null || grid == null || noteLayer == null || notePlacer == null || canvas == null)
        {
            Debug.LogError("[EditorScene] session, grid, noteLayer, notePlacer, canvas 중 연결되지 않은 것이 있습니다.", this);
            yield break;
        }

        // TODO: 채보 데이터 연결 시 교체 (테스트 BPM·곡 길이 대신 songId의 곡 정보를 쓴다)
        session.Init(entry, testBpm, testSongLengthSeconds);

        float totalBeats = BeatGridMath.SecondsToBeats(testSongLengthSeconds, testBpm);
        grid.Init(totalBeats);
        noteLayer.Init(grid, session);
        notePlacer.Init(grid, session, () => canvas.CurrentTool);
        canvas.Init(grid, session);
    }

    /// <summary>
    /// 초기화가 끝나면 휠 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
        Debug.Log("[EditorScene] OnEnter");
        if (canvas != null) canvas.EnableInput();
    }

    /// <summary>
    /// 씬을 떠나기 직전 입력 구독을 해제한다.
    /// </summary>
    public override void OnExit()
    {
        if (canvas != null) canvas.DisableInput();
    }
}
