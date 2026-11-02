using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 씬 진입점. EditorScene.unity 루트의 "EditorScene" 오브젝트에 붙인다.
/// </summary>
public class EditorScene : SceneBase
{
    /// <summary>
    /// 에디터 씬 초기화. 지금은 초기화할 오브젝트가 없다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[EditorScene] Init");

        // 이후 아래 순서로 초기화한다.
        // 1. 제공 곡 목록 불러오기
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
