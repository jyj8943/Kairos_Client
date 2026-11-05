using System.Collections;
using UnityEngine;

/// <summary>
/// 곡 선택 씬 진입점 (기획서 3-3). SongSelectScene.unity 루트의 "SongSelectScene" 오브젝트에 붙인다.
/// - 곡 목록은 GameManager.Songs에서 받아 Canvas_SongSelect를 초기화한다.
/// - 결과 화면에서 돌아오면 SongSelectEntryData의 곡·탭·채보·배속이 선택된 채로 시작한다.
/// - 초기화가 끝나면(OnEnter) 키 입력을 받고, 떠날 때(OnExit) 구독을 해제한다.
/// </summary>
public class SongSelectScene : SceneBase
{
    [SerializeField] private Canvas_SongSelect canvas;

    /// <summary>
    /// 곡 목록과 진입 데이터를 확인하고 곡 선택 UI를 초기화한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        if (GameManager.Songs.Songs.Count == 0)
            Debug.LogError("[SongSelectScene] 곡 목록이 비어 있습니다.", this);

        if (canvas == null)
        {
            Debug.LogError("[SongSelectScene] canvas가 연결되지 않았습니다.", this);
            yield break;
        }

        SongSelectEntryData entry = GameManager.Scene.TakeSceneData<SongSelectEntryData>();
        canvas.Init(GameManager.Songs.Songs, entry);
    }

    /// <summary>
    /// 초기화가 끝나면 키 입력을 받는다.
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
