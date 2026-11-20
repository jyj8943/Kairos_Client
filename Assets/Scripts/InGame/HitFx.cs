using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 판정 이펙트 R2 하나 (HitFx_n, 월드 오브젝트). HitFxPool이 Init에서 미리 만들고 Play() · Tick()을 부른다.
/// - 자식 SpriteRenderer: Glow · OrbHalo · OrbCore · Ring · Dot_0 ~ 7 · DiamondOuter · DiamondInner (아래부터 그리는 순서).
///   SortingGroup 하나(GameConstants.InGameSort.HitFx)로 묶어 노트·판정선 위, 키 칸 아래에 그리고, 그룹 안 순서는 위 순서대로다.
/// - 값은 HitFxRules.Evaluate(u)를 그대로 옮긴다. u = 지금 곡 시간 − 터진 곡 시간이라 일시정지 중에는 멈추고 재생 배율을 따른다.
/// - 반짝이 8개의 크기·각도·거리는 Play 때마다 새로 뽑는다 (풀이 넘겨준 System.Random 하나, 할당 없음).
/// - Additive 머티리얼은 쓰지 않는다 (배경 밝기·정렬에 따라 달라 보이지 않게). 빛 번짐은 Fx_Glow를 C 색 알파로 밑에 깐다.
/// </summary>
public class HitFx : MonoBehaviour
{
    private SpriteRenderer glow;
    private SpriteRenderer halo;
    private SpriteRenderer core;
    private SpriteRenderer ring;
    private SpriteRenderer diamondOuter;
    private SpriteRenderer diamondInner;
    private readonly SpriteRenderer[] dots = new SpriteRenderer[HitFxRules.DotCount];

    private readonly float[] dotAngles = new float[HitFxRules.DotCount];
    private readonly float[] dotOffsets = new float[HitFxRules.DotCount];
    private readonly float[] dotRadii = new float[HitFxRules.DotCount];

    private float unitsPerPx;
    private Color color;
    private Color lightColor;

    /// <summary>터진 곡 시간 (초)</summary>
    public double StartSongTime { get; private set; }

    /// <summary>보이는 중이면 true</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>터뜨린 판정 (확인용)</summary>
    public JudgeType Judge { get; private set; }

    /// <summary>이펙트 색 C (확인용)</summary>
    public Color Color => color;

    /// <summary>마지막으로 그린 값 (확인용)</summary>
    public HitFxFrame LastFrame { get; private set; }

    /// <summary>파츠를 만든다 (HitFxPool.Init에서 한 번). 판 도중에는 부르지 않는다.</summary>
    public void Build(HitFxSprites sprites, float unitsPerPx)
    {
        this.unitsPerPx = unitsPerPx;
        SortingGroup group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = GameConstants.InGameSort.HitFx;

        int order = 0;
        glow = CreatePart("Glow", sprites.Glow, order++);
        halo = CreatePart("OrbHalo", sprites.OrbHalo, order++);
        core = CreatePart("OrbCore", sprites.OrbCore, order++);
        ring = CreatePart("Ring", sprites.Ring, order++);
        for (int i = 0; i < dots.Length; i++) dots[i] = CreatePart("Dot_" + i, sprites.Dot, order);
        order++;
        diamondOuter = CreatePart("DiamondOuter", sprites.Diamond, order++);
        diamondInner = CreatePart("DiamondInner", sprites.Diamond, order);

        IsPlaying = false;
        gameObject.SetActive(false);
    }

    private SpriteRenderer CreatePart(string partName, Sprite sprite, int order)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(transform, false);
        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>
    /// 터뜨린다: 색을 정하고 반짝이를 새로 뽑은 뒤 u = 0 모양으로 그린다.
    /// </summary>
    /// <param name="fxColor">C (UITheme.TryGetHitFxColor)</param>
    /// <param name="lightMix">L = C와 흰색을 섞는 비율 (UITheme.hitFxLightMix)</param>
    public void Play(JudgeType judge, Color fxColor, float lightMix, double songTime, System.Random random)
    {
        Judge = judge;
        color = fxColor;
        lightColor = UITheme.HitFxLight(fxColor, lightMix);
        StartSongTime = songTime;
        for (int i = 0; i < dots.Length; i++)
        {
            dotAngles[i] = (float)(random.NextDouble() * Mathf.PI * 2.0);
            dotOffsets[i] = (float)(random.NextDouble() * HitFxRules.DotOffsetMax);
            dotRadii[i] = Mathf.Lerp(HitFxRules.DotRadiusMin, HitFxRules.DotRadiusMax, (float)random.NextDouble());
        }
        IsPlaying = true;
        gameObject.SetActive(true);
        Apply(0f);
    }

    /// <summary>
    /// 지금 곡 시간으로 다시 그린다. 0.38초가 지나면 끄고 false를 돌려준다.
    /// </summary>
    public bool Tick(double songTime)
    {
        if (!IsPlaying) return false;
        float u = (float)(songTime - StartSongTime);
        if (!HitFxRules.IsAlive(u) && u >= 0f)
        {
            Stop();
            return false;
        }
        Apply(Mathf.Max(0f, u));
        return true;
    }

    /// <summary>바로 끈다 (풀로 돌아감)</summary>
    public void Stop()
    {
        IsPlaying = false;
        gameObject.SetActive(false);
    }

    private void Apply(float u)
    {
        HitFxFrame f = HitFxRules.Evaluate(u);
        LastFrame = f;

        SetCircle(glow, f.GlowSize, f.GlowAlpha, color);
        SetCircle(halo, HitFxRules.HaloDisplayPxAtFrom * f.HaloRadius / HitFxRules.HaloRadiusFrom, f.HaloAlpha, color);
        SetCircle(core, HitFxRules.CoreDisplayPxAtFrom * f.CoreRadius / HitFxRules.CoreRadiusFrom, f.CoreAlpha, Color.white);
        SetCircle(ring, HitFxRules.RingDisplayPxAtTo * f.RingRadius / HitFxRules.RingRadiusTo, f.RingAlpha, color);

        for (int i = 0; i < dots.Length; i++)
        {
            SpriteRenderer dot = dots[i];
            float distancePx = f.RingRadius + dotOffsets[i];
            dot.transform.localPosition = new Vector3(Mathf.Cos(dotAngles[i]), Mathf.Sin(dotAngles[i]), 0f) * (distancePx * unitsPerPx);
            SetCircle(dot, HitFxRules.DotDisplayPxAtMax * dotRadii[i] / HitFxRules.DotRadiusMax, f.DotAlpha, lightColor);
        }

        // 마름모는 화면에서 시계 방향으로 돈다 (디자인 캔버스의 + 회전)
        Quaternion rotation = Quaternion.Euler(0f, 0f, -f.DiamondAngle);
        SetCircle(diamondOuter, HitFxRules.DiamondDisplayPxAtFrom * f.DiamondHalfDiagonal / HitFxRules.DiamondHalfFrom, f.DiamondOuterAlpha, color);
        SetCircle(diamondInner, HitFxRules.DiamondDisplayPxAtFrom * f.DiamondInnerHalfDiagonal / HitFxRules.DiamondHalfFrom, f.DiamondInnerAlpha, Color.white);
        diamondOuter.transform.localRotation = rotation;
        diamondInner.transform.localRotation = rotation;
    }

    /// <summary>파츠 하나를 표시 크기(px) · 알파 · 색으로 놓는다. 알파 0이면 끈다</summary>
    private void SetCircle(SpriteRenderer renderer, float displayPx, float alpha, Color tint)
    {
        bool visible = alpha > 0f && displayPx > 0f;
        renderer.enabled = visible;
        if (!visible) return;
        float scale = HitFxRules.SpriteScale(displayPx, renderer.sprite, unitsPerPx);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        tint.a = alpha;
        renderer.color = tint;
    }
}

/// <summary>판정 이펙트 스프라이트 묶음 (Resources/Images/InGame/Fx/, HitFxPool.Init에서 한 번 읽는다)</summary>
public struct HitFxSprites
{
    public Sprite Glow, OrbHalo, OrbCore, Ring, Dot, Diamond;
}
