using System;

/// <summary>
/// PlayerData.json에 저장하는 곡 선택의 마지막 선택 상태 (기획서 3-3 "저장 데이터: 마지막으로 고른 곡·난이도·배속").
/// 설정 창에서 바꾸는 값이 아니라서 settings와 따로 둔다. 지금은 배속만 저장하고, 나중에 마지막 곡·난이도를 이 묶음에 넣는다.
/// 불러온 뒤 PlayerDataRules.Sanitize로 범위를 정리한다.
/// </summary>
[Serializable]
public class LastSelectionSaveData
{
    /// <summary>마지막 배속 (1.0 ~ 8.0, 0.1 단위). 일반 플레이에 들어갈 때·떠날 때만 저장한다</summary>
    public float scrollMultiplier = GameConstants.Play.DefaultScrollMultiplier;
}
