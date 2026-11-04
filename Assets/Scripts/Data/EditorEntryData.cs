/// <summary>
/// 곡 고르기 씬(또는 인게임·결과) → 편집 씬으로 넘기는 데이터.
/// GameManager.Scene.Load(sceneName, data)로 넘기고 EditorScene Init에서 TakeSceneData로 받는다.
/// </summary>
public class EditorEntryData
{
    public string songId;
    public string chartName;
    public int level;

    /// <summary>새 채보면 null. 내 채보 열기를 만들 때 사용한다.</summary>
    public string chartId;

    /// <summary>테스트 플레이에서 돌아올 때 되돌릴 편집 상태. 있으면 파일을 읽지 않고 이 상태로 시작한다</summary>
    public EditorRestoreState restore;
}
