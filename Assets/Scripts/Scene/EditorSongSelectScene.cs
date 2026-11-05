using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 곡 고르기 씬 진입점. EditorSongSelectScene.unity 루트의 "EditorSongSelectScene" 오브젝트에 붙인다.
/// - 곡 목록은 GameManager.Songs에서 받는다.
/// - 에디터에서 나오면 EditorSongSelectEntryData의 곡을 고른 상태로 시작한다.
/// </summary>
public class EditorSongSelectScene : SceneBase
{
    [SerializeField] private Canvas_EditorSongSelect canvas;

    /// <summary>
    /// 곡 목록을 확인하고 곡 고르기 UI를 초기화한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        if (GameManager.Songs.Songs.Count == 0)
            Debug.LogError("[EditorSongSelectScene] 곡 목록이 비어 있습니다.", this);

        if (canvas == null)
        {
            Debug.LogError("[EditorSongSelectScene] canvas가 연결되지 않았습니다.", this);
            yield break;
        }

        // 에디터에서 나온 경우 방금 편집한 곡을 고른 상태로 시작한다
        EditorSongSelectEntryData entry = GameManager.Scene.TakeSceneData<EditorSongSelectEntryData>();
        canvas.Init(GameManager.Songs.Songs, entry?.songId);
    }

    /// <summary>
    /// ESC (팝업이 없고 입력 중이 아닐 때 UIManager가 부름): 타이틀로 나간다.
    /// </summary>
    public override void OnBack()
    {
        if (canvas != null) canvas.ExitToTitle();
    }
}
