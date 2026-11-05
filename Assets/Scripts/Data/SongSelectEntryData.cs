/// <summary>
/// 곡 선택 씬으로 넘기는 데이터 (결과 화면에서 돌아올 때).
/// GameManager.Scene.Load(GameConstants.Scenes.SongSelect, data)로 넘기고 SongSelectScene Init에서 TakeSceneData로 받는다.
/// 받은 곡·탭·채보·배속이 선택된 채로 시작한다. 없거나 목록에 없는 값은 기본 선택을 쓴다.
/// </summary>
public class SongSelectEntryData
{
    public string songId;

    /// <summary>보여줄 탭 (공식 / 나만의 채보)</summary>
    public ChartType chartType = ChartType.Official;

    /// <summary>고를 채보. 없으면 공식 탭은 가장 낮은 난이도, 나만의 채보 탭은 첫 채보</summary>
    public string chartId;

    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;
}
