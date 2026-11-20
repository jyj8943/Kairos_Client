using System.Collections.Generic;
using UnityEngine;

/// <summary>판정 이미지 세트 (11/20). 이름은 두 세트가 같고 폴더만 다르다</summary>
public enum JudgeSpriteSet
{
    /// <summary>기어 위 판정 글자 (어두운 기어 위 밝은 색, Images/InGame/Judge/)</summary>
    Gear,

    /// <summary>판정 패널 · 결과 (밝은 바탕 위 진한 색, Images/InGame/JudgeMenu/)</summary>
    Menu
}

/// <summary>
/// 판정 글자·FAST/SLOW 이미지 찾기 (11/19, RankSprites와 같은 방식). 처음 한 번 읽어 세트마다 따로 캐시한다.
/// - 세트(JudgeSpriteSet): Gear = GameConstants.Play.JudgeSpriteFolder (기어 위 판정 글자), Menu = JudgeMenuSpriteFolder (Img_JudgePanel · Canvas_Result).
/// - 이름 = GameConstants.Play.JudgeSpritePrefix + enum 이름: Judge_Perfect · Judge_Great · Judge_Good · Judge_Miss · Judge_Fast · Judge_Slow.
/// - 없으면 경고를 한 번만 남기고 null (같은 이름은 다시 찾지 않는다).
/// - 화면 크기 = 원본 크기 × GameConstants.Play.JudgeSpriteDisplayScale (DisplaySize).
/// </summary>
public static class JudgeSprites
{
    private static readonly Dictionary<string, Sprite> gearCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> menuCache = new Dictionary<string, Sprite>();

    /// <summary>기어 위 판정 글자 이미지 (PERFECT · GREAT · GOOD · MISS)</summary>
    public static Sprite Get(JudgeType judge)
    {
        return Get(judge, JudgeSpriteSet.Gear);
    }

    /// <summary>기어 위 FAST / SLOW 이미지 (None이면 null)</summary>
    public static Sprite Get(FastSlow fastSlow)
    {
        return Get(fastSlow, JudgeSpriteSet.Gear);
    }

    /// <summary>세트를 골라 판정 글자 이미지를 찾는다</summary>
    public static Sprite Get(JudgeType judge, JudgeSpriteSet set)
    {
        return Load(judge.ToString(), set);
    }

    /// <summary>세트를 골라 FAST / SLOW 이미지를 찾는다 (None이면 null)</summary>
    public static Sprite Get(FastSlow fastSlow, JudgeSpriteSet set)
    {
        return fastSlow == FastSlow.None ? null : Load(fastSlow.ToString(), set);
    }

    /// <summary>세트의 Resources 폴더 (순수 함수)</summary>
    public static string GetFolder(JudgeSpriteSet set)
    {
        return set == JudgeSpriteSet.Menu ? GameConstants.Play.JudgeMenuSpriteFolder : GameConstants.Play.JudgeSpriteFolder;
    }

    /// <summary>이미지의 Resources 경로 (순수 함수, 예: Images/InGame/JudgeMenu/Judge_Miss)</summary>
    public static string GetPath(string name, JudgeSpriteSet set)
    {
        return GetFolder(set) + GameConstants.Play.JudgeSpritePrefix + name;
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

    private static Sprite Load(string name, JudgeSpriteSet set)
    {
        Dictionary<string, Sprite> cache = set == JudgeSpriteSet.Menu ? menuCache : gearCache;
        if (cache.TryGetValue(name, out Sprite sprite)) return sprite;

        string path = GetPath(name, set);
        sprite = Resources.Load<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[JudgeSprites] Resources/{path} 판정 이미지가 없습니다.");
        cache[name] = sprite;
        return sprite;
    }
}
