using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 랭크·FC 이미지 찾기 (기획서 3-3 「랭크·FC 표시 (11/18 개정)」). Resources/Images/Rank/에서 처음 한 번 읽어 둔다.
/// - 랭크: Rank_AP · Rank_S ~ Rank_F (이름 = GameConstants.Play.RankSpritePrefix + PlayRank 이름). FC: Clear_FC.
/// - 곡 선택 기록 카드·곡 목록·결과가 같이 쓴다 (Img_Rank · Img_FullCombo).
/// </summary>
public static class RankSprites
{
    private static readonly Dictionary<PlayRank, Sprite> cache = new Dictionary<PlayRank, Sprite>();
    private static Sprite fullCombo;

    /// <summary>랭크 이미지 (없으면 경고를 남기고 null)</summary>
    public static Sprite Get(PlayRank rank)
    {
        if (cache.TryGetValue(rank, out Sprite sprite) && sprite != null) return sprite;

        string path = GameConstants.Play.RankSpriteFolder + GameConstants.Play.RankSpritePrefix + rank;
        sprite = Resources.Load<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[RankSprites] Resources/{path} 랭크 이미지가 없습니다.");
        cache[rank] = sprite;
        return sprite;
    }

    /// <summary>FULL COMBO 이미지 (없으면 경고를 남기고 null)</summary>
    public static Sprite FullCombo
    {
        get
        {
            if (fullCombo != null) return fullCombo;

            string path = GameConstants.Play.RankSpriteFolder + GameConstants.Play.FullComboSpriteName;
            fullCombo = Resources.Load<Sprite>(path);
            if (fullCombo == null) Debug.LogWarning($"[RankSprites] Resources/{path} FC 이미지가 없습니다.");
            return fullCombo;
        }
    }

    /// <summary>스프라이트의 가로 ÷ 세로 (없으면 1)</summary>
    public static float AspectOf(Sprite sprite)
    {
        if (sprite == null || sprite.rect.height <= 0f) return 1f;
        return sprite.rect.width / sprite.rect.height;
    }
}
