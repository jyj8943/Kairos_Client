/// <summary>
/// 에디터 곡 고르기 씬으로 넘기는 데이터 (에디터에서 나올 때).
/// GameManager.Scene.Load(sceneName, data)로 넘기고 EditorSongSelectScene Init에서 TakeSceneData로 받는다.
/// </summary>
public class EditorSongSelectEntryData
{
    /// <summary>처음에 고른 상태로 둘 곡. 없거나 곡 목록에 없으면 첫 곡</summary>
    public string songId;
}
