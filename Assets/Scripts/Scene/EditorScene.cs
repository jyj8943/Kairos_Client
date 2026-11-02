using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 씬 진입점. EditorScene.unity 루트의 "EditorScene" 오브젝트에 붙인다.
/// </summary>
public class EditorScene : SceneBase
{
    /// <summary>
    /// 에디터 씬 초기화. 곡 고르기 씬이 넘긴 채보 정보를 받는다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[EditorScene] Init");

        EditorEntryData entry = GameManager.Scene.TakeSceneData<EditorEntryData>();
        if (entry == null)
            Debug.LogWarning("[EditorScene] 넘겨받은 채보 정보가 없습니다. (EditorScene을 직접 실행한 경우)");
        else
            Debug.Log($"[EditorScene] songId={entry.songId}, chartName={entry.chartName}, level={entry.level}, chartId={entry.chartId ?? "(새 채보)"}");

        // 이후 아래 순서로 초기화한다.
        // 1. 넘겨받은 songId로 곡 정보 불러오기
        // 2. 에디터 UI Init
        // 3. 그리드·타임라인 Init

        yield break;
    }

    /// <summary>
    /// 초기화가 끝난 뒤 호출된다.
    /// </summary>
    protected override void OnEnter()
    {
        Debug.Log("[EditorScene] OnEnter");
    }
}
