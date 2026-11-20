using UnityEngine;

/// <summary>
/// 판정 이펙트 R2 한 프레임의 값 (HitFxRules.Evaluate). 크기는 1080 화면 기준 px, 각도는 도, 알파는 0 ~ 1.
/// 파츠가 꺼져 있으면 알파가 0이다.
/// </summary>
public struct HitFxFrame
{
    /// <summary>빛 구슬 바깥 Fx_OrbHalo (C 색)</summary>
    public float HaloRadius, HaloAlpha;
    /// <summary>빛 구슬 안쪽 Fx_OrbCore (흰색)</summary>
    public float CoreRadius, CoreAlpha;
    /// <summary>바깥 원 Fx_Ring (C 색)</summary>
    public float RingRadius, RingAlpha;
    /// <summary>원 위 반짝이 Fx_Dot (L 색). 위치 반지름 = RingRadius + 점마다 무작위 0 ~ 5px</summary>
    public float DotAlpha;
    /// <summary>가운데 마름모 바깥 Fx_Diamond (C 색). 반대각 · 회전(0 → 90°)</summary>
    public float DiamondHalfDiagonal, DiamondAngle, DiamondOuterAlpha;
    /// <summary>가운데 마름모 안쪽 (흰색, 바깥의 0.55배, 같은 회전)</summary>
    public float DiamondInnerHalfDiagonal, DiamondInnerAlpha;
    /// <summary>빛 번짐 Fx_Glow (C 색): 표시 크기(px) · 알파</summary>
    public float GlowSize, GlowAlpha;
}

/// <summary>
/// 판정 이펙트 R2 규칙 (11/20, 기획서 3-4 「노트 · 판정 이펙트」, README 3절 표). 상태가 없는 순수 함수라 EditMode 테스트로 검사한다.
/// - 언제: 숏노트·롱노트 머리 PERFECT · GREAT · GOOD, 롱노트 유지 틱마다(머리 판정 색), 롱노트 꼬리(꼬리 판정 색). MISS는 없음.
/// - u = 판정 후 초(곡 시간). 전체 0.38초. 이징은 easeOutCubic 1 − (1 − x)^3. q = 그 파츠 시간 안에서 0 → 1.
///   - Fx_OrbHalo: 0 ~ 0.10, 반지름 50 → 20 (이징), C 알파 0.43 → 0.17 (직선) 뒤 꺼짐
///   - Fx_OrbCore: 0 ~ 0.10, 반지름 25 → 10 (이징), 흰색 알파 1 → 0.4 (직선) 뒤 꺼짐
///   - Fx_Ring: 0.04 ~ 0.38, 반지름 38 → 62 (이징), C 알파 0.82 → 0 (직선)
///   - Fx_Dot × 8: 0.04 ~ 0.38, L 알파 0.86 → 0 (직선). 크기·각도·거리는 이펙트마다 무작위 (HitFx)
///   - Fx_Diamond 바깥: 0.03 ~ 0.38, 반대각 30 → 16.5 (직선), 0° → 90° (이징), C 알파 0.59 × (1 − q^1.4). 안쪽: 0.55배, 흰색 알파 1 − q^1.4
///   - Fx_Glow: 0 ~ 0.38, 256px → 128px (이징), C 알파 0.35 → 0 (직선). Additive 대신 알파 블렌드로 밑에 깐다
/// </summary>
public static class HitFxRules
{
    public const float Duration = 0.38f;

    public const float OrbEnd = 0.10f;
    public const float HaloRadiusFrom = 50f, HaloRadiusTo = 20f, HaloAlphaFrom = 0.43f, HaloAlphaTo = 0.17f;
    public const float CoreRadiusFrom = 25f, CoreRadiusTo = 10f, CoreAlphaFrom = 1f, CoreAlphaTo = 0.4f;

    public const float RingStart = 0.04f;
    public const float RingRadiusFrom = 38f, RingRadiusTo = 62f, RingAlphaFrom = 0.82f;

    public const int DotCount = 8;
    public const float DotAlphaFrom = 0.86f;
    public const float DotRadiusMin = 1.2f, DotRadiusMax = 2.2f;
    /// <summary>반짝이 위치 = 원 반지름 + 0 ~ 이 값(px)</summary>
    public const float DotOffsetMax = 5f;

    public const float DiamondStart = 0.03f;
    public const float DiamondHalfFrom = 30f, DiamondHalfTo = 16.5f, DiamondAngleTo = 90f;
    public const float DiamondOuterAlpha = 0.59f, DiamondInnerScale = 0.55f, DiamondFadePower = 1.4f;

    public const float GlowSizeFrom = 256f, GlowSizeTo = 128f, GlowAlphaFrom = 0.35f;

    // 이미지 원본 크기와 표시 크기 (README 5절). 표시 크기는 아래 기준 반지름일 때의 px
    /// <summary>Fx_OrbHalo 240px = 표시 120px = 반지름 50 + 흐림</summary>
    public const float HaloDisplayPxAtFrom = 120f;
    /// <summary>Fx_OrbCore 128px = 표시 64px = 반지름 25 + 흐림</summary>
    public const float CoreDisplayPxAtFrom = 64f;
    /// <summary>Fx_Ring 264px = 표시 132px = 반지름 62</summary>
    public const float RingDisplayPxAtTo = 132f;
    /// <summary>Fx_Dot 20px = 표시 10px = 반지름 DotRadiusMax (2.2) 기준</summary>
    public const float DotDisplayPxAtMax = 10f;
    /// <summary>Fx_Diamond 128px = 표시 64px = 반대각 30</summary>
    public const float DiamondDisplayPxAtFrom = 64f;

    /// <summary>easeOutCubic: 1 − (1 − x)^3 (x는 0 ~ 1로 자른다)</summary>
    public static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }

    /// <summary>파츠 시간 [start, end] 안의 진행도 q (0 ~ 1, 밖이면 자른다)</summary>
    public static float Progress(float u, float start, float end)
    {
        return Mathf.Clamp01((u - start) / (end - start));
    }

    /// <summary>이펙트가 아직 보이는지 (0 ≤ u &lt; 0.38)</summary>
    public static bool IsAlive(float u) => u >= 0f && u < Duration;

    /// <summary>
    /// u초의 파츠 값 (크기 px · 알파 · 회전). 파츠 시간 밖이면 알파 0.
    /// </summary>
    public static HitFxFrame Evaluate(float u)
    {
        HitFxFrame f = new HitFxFrame();
        if (u < 0f || u >= Duration) return f;

        // 빛 구슬 (0 ~ 0.10, 끝나면 꺼짐)
        if (u <= OrbEnd)
        {
            float q = Progress(u, 0f, OrbEnd);
            float e = EaseOutCubic(q);
            f.HaloRadius = Mathf.Lerp(HaloRadiusFrom, HaloRadiusTo, e);
            f.HaloAlpha = Mathf.Lerp(HaloAlphaFrom, HaloAlphaTo, q);
            f.CoreRadius = Mathf.Lerp(CoreRadiusFrom, CoreRadiusTo, e);
            f.CoreAlpha = Mathf.Lerp(CoreAlphaFrom, CoreAlphaTo, q);
        }

        // 바깥 원 · 반짝이 (0.04 ~ 0.38)
        if (u >= RingStart)
        {
            float q = Progress(u, RingStart, Duration);
            f.RingRadius = Mathf.Lerp(RingRadiusFrom, RingRadiusTo, EaseOutCubic(q));
            f.RingAlpha = RingAlphaFrom * (1f - q);
            f.DotAlpha = DotAlphaFrom * (1f - q);
        }

        // 가운데 마름모 (0.03 ~ 0.38)
        if (u >= DiamondStart)
        {
            float q = Progress(u, DiamondStart, Duration);
            float fade = 1f - Mathf.Pow(q, DiamondFadePower);
            f.DiamondHalfDiagonal = Mathf.Lerp(DiamondHalfFrom, DiamondHalfTo, q);
            f.DiamondAngle = DiamondAngleTo * EaseOutCubic(q);
            f.DiamondOuterAlpha = DiamondOuterAlpha * fade;
            f.DiamondInnerHalfDiagonal = f.DiamondHalfDiagonal * DiamondInnerScale;
            f.DiamondInnerAlpha = fade;
        }

        // 빛 번짐 (0 ~ 0.38)
        {
            float q = Progress(u, 0f, Duration);
            f.GlowSize = Mathf.Lerp(GlowSizeFrom, GlowSizeTo, EaseOutCubic(q));
            f.GlowAlpha = GlowAlphaFrom * (1f - q);
        }
        return f;
    }

    /// <summary>
    /// 이 판정 결과로 이펙트를 터뜨릴지와 그 색을 고를 판정 (MISS면 false).
    /// - 숏노트 · 롱노트 머리: 그 판정 / 유지 틱: 머리 판정(StartType) / 롱노트 종합(꼬리): 꼬리 판정(EndType, 일찍 떼거나 시작 MISS면 MISS → 없음)
    /// </summary>
    public static bool TryGetFxJudge(JudgeResult result, out JudgeType judge)
    {
        switch (result.Kind)
        {
            case JudgeKind.Tick: judge = result.StartType; break;
            case JudgeKind.LongFinal: judge = result.EndType; break;
            default: judge = result.Type; break;
        }
        return judge != JudgeType.Miss;
    }

    /// <summary>롱노트 하나에서 터지는 이펙트 수 (끝까지 누르고 MISS가 없을 때) = 유지 틱 + 머리 1 + 꼬리 1</summary>
    public static int CountForHeldLongNote(double startBeat, double endBeat)
    {
        return LongNoteTicks.Count(startBeat, endBeat) + 2;
    }

    /// <summary>표시 크기(px)에 맞춘 스프라이트 스케일: 표시 px × unitsPerPx ÷ (원본 px ÷ PPU)</summary>
    public static float SpriteScale(float displayPx, Sprite sprite, float unitsPerPx)
    {
        if (sprite == null) return 0f;
        float nativeUnits = sprite.rect.width / sprite.pixelsPerUnit;
        return nativeUnits <= 0f ? 0f : displayPx * unitsPerPx / nativeUnits;
    }
}
