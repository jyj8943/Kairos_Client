using System.Collections;
using UnityEngine;

/// <summary>
/// 에디터 곡 고르기 씬 진입점. EditorSongSelectScene.unity 루트의 "EditorSongSelectScene" 오브젝트에 붙인다.
/// - 곡 목록은 GameManager.Songs에서 받는다.
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

        canvas.Init(GameManager.Songs.Songs);
    }

    /// <summary>
    /// 초기화가 끝나면 ESC 입력을 받는다.
    /// </summary>
    protected override void OnEnter()
    {
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
