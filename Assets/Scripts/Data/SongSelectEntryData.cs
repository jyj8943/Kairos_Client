/// <summary>
/// 곡 선택 씬으로 넘기는 데이터 (결과 화면·에디터·타이틀 에디터 메뉴에서 들어올 때).
/// GameManager.Scene.Load(GameConstants.Scenes.SongSelect, data)로 넘기고 SongSelectScene Init에서 TakeSceneData로 받는다.
/// 받은 곡·탭·채보·배속이 선택된 채로 시작한다 (PlayerData lastSelection보다 우선). 없거나 목록에 없는 값은 기본 선택을 쓴다.
/// </summary>
public class SongSelectEntryData
{
    /// <summary>배속 값이 없다는 뜻 (scrollMultiplier 기본값). 이때는 PlayerData에 저장된 마지막 배속을 쓴다</summary>
    public const float NoScrollMultiplier = 0f;

    public string songId;

    /// <summary>보여줄 탭 (공식 / 나만의 채보)</summary>
    public ChartType chartType = ChartType.Official;

    /// <summary>
    /// 고를 채보. 공식 탭은 없으면 가장 낮은 난이도.
    /// 나만의 채보 탭은 있으면 그 채보 줄, 없으면 그 곡의 "+ 새 채보 만들기" 줄 (songId도 없으면 첫 줄).
    /// </summary>
    public string chartId;

    /// <summary>시작 배속. NoScrollMultiplier(0)면 넘기지 않은 것으로 보고 저장된 마지막 배속을 쓴다 (에디터·타이틀에서 들어올 때)</summary>
    public float scrollMultiplier = NoScrollMultiplier;
}
