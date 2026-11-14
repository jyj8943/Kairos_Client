using System.Collections;
using UnityEngine;

/// <summary>
/// 타이틀 씬 진입점. TitleScene.unity 루트의 "TitleScene" 오브젝트에 붙인다.
/// </summary>
public class TitleScene : SceneBase
{
    [SerializeField] private Canvas_Title canvasTitle;

    /// <summary>
    /// 타이틀 씬 초기화. 타이틀 화면 UI(Canvas_Title)를 초기화한다.
    /// </summary>
    protected override IEnumerator Init()
    {
        Debug.Log("[TitleScene] Init");

        if (canvasTitle == null)
            Debug.LogError("[TitleScene] canvasTitle이 연결되지 않았습니다.", this);
        else
            canvasTitle.Init();

        yield break;
    }

    /// <summary>
    /// 초기화가 끝난 뒤 호출된다.
    /// </summary>
    protected override void OnEnter()
    {
        Debug.Log("[TitleScene] OnEnter");
    }

    /// <summary>
    /// ESC (팝업이 없을 때 UIManager가 부름): 게임 종료 확인 팝업을 연다.
    /// </summary>
    public override void OnBack()
    {
        if (canvasTitle != null) canvasTitle.RequestQuit();
    }
}
