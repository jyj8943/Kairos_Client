using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기어 판정 글자·FAST/SLOW 이미지 찾기 (11/19, RankSprites와 같은 방식). Resources/Images/InGame/Judge/에서 처음 한 번 읽어 둔다.
/// - 이름 = GameConstants.Play.JudgeSpritePrefix + enum 이름: Judge_Perfect · Judge_Great · Judge_Good · Judge_Miss · Judge_Fast · Judge_Slow.
/// - 없으면 경고를 한 번만 남기고 null (같은 이름은 다시 찾지 않는다).
/// - 화면 크기 = 원본 크기 × GameConstants.Play.JudgeSpriteDisplayScale (DisplaySize).
/// </summary>
public static class JudgeSprites
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>판정 글자 이미지 (PERFECT · GREAT · GOOD · MISS)</summary>
    public static Sprite Get(JudgeType judge)
    {
        return Load(judge.ToString());
    }

    /// <summary>FAST / SLOW 이미지 (None이면 null)</summary>
    public static Sprite Get(FastSlow fastSlow)
    {
        return fastSlow == FastSlow.None ? null : Load(fastSlow.ToString());
    }

    /// <summary>화면에 그릴 크기 (순수 함수): 원본 크기 × JudgeSpriteDisplayScale. 없으면 0</summary>
    public static Vector2 DisplaySize(Sprite sprite)
    {
        return sprite == null ? Vector2.zero : DisplaySize(sprite.rect.size);
    }

    /// <summary>화면에 그릴 크기 (순수 함수): 원본 픽셀 크기 × JudgeSpriteDisplayScale (예: 469×179 → 234.5×89.5)</summary>
    public static Vector2 DisplaySize(Vector2 sourcePixels)
    {
        return DisplaySize(sourcePixels, GameConstants.Play.JudgeSpriteDisplayScale);
    }

    /// <summary>원하는 배율로 그릴 크기 (순수 함수): 원본 크기 × scale. 없으면 0 (판정 패널 이름: PanelJudgeImageScale · PanelFastSlowImageScale)</summary>
    public static Vector2 DisplaySize(Sprite sprite, float scale)
    {
        return sprite == null ? Vector2.zero : DisplaySize(sprite.rect.size, scale);
    }

    /// <summary>원하는 배율로 그릴 크기 (순수 함수): 원본 픽셀 크기 × scale</summary>
    public static Vector2 DisplaySize(Vector2 sourcePixels, float scale)
    {
        return sourcePixels * scale;
    }

    private static Sprite Load(string name)
    {
        if (cache.TryGetValue(name, out Sprite sprite)) return sprite;

        string path = GameConstants.Play.JudgeSpriteFolder + GameConstants.Play.JudgeSpritePrefix + name;
        sprite = Resources.Load<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[JudgeSprites] Resources/{path} 판정 이미지가 없습니다.");
        cache[name] = sprite;
        return sprite;
    }
}
