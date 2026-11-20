using UnityEngine;

/// <summary>
/// 판정 이펙트 R2 풀 (InGameScene.unity의 "Gear/HitFx"에 붙이고, InGameScene이 Init() · Play() · Tick()을 호출한다).
/// - 라인마다 GameConstants.Play.HitFxPoolPerLane(6)개를 Init에서 미리 만든다 (자식 Lane_n/HitFx_0 ~ 5). 판 도중에는 만들지 않는다.
///   다 쓰고 있으면 가장 오래 전에 터진 것을 다시 쓴다 (롱노트 유지 틱마다 겹쳐 터져도 2 ~ 3개면 충분).
/// - 위치: 라인 가운데 x, 판정선 높이 (GearView.JudgeLineY, 노트를 판정하는 높이와 같은 값. 1080 기준 y 900).
/// - 스프라이트는 Resources/Images/InGame/Fx/에서, 색은 UITheme 판정 이펙트 묶음에서 읽는다.
/// - 시간은 곡 시간(노트와 같은 기준)이라 일시정지 중에는 멈추고(InGameScene이 Tick을 부르지 않음), 재생 배율을 따른다.
/// </summary>
public class HitFxPool : MonoBehaviour
{
    private HitFx[][] pools;
    private int[] activeCounts;
    private int[] maxActive;
    private System.Random random;
    private UITheme theme;

    /// <summary>Init에서 만든 이펙트 수 (확인용)</summary>
    public int CreatedCount { get; private set; }

    /// <summary>터뜨린 횟수 (확인용)</summary>
    public int PlayCount { get; private set; }

    /// <summary>판정별 터뜨린 횟수 (확인용, JudgeType 순서)</summary>
    public int[] PlayCountByJudge { get; } = new int[4];

    /// <summary>라인별 터뜨린 횟수 (확인용)</summary>
    public int GetPlayCount(int lane) => laneCounts != null && lane >= 0 && lane < laneCounts.Length ? laneCounts[lane] : 0;
    private int[] laneCounts;

    /// <summary>라인별 최대 동시 사용 수 (확인용)</summary>
    public int GetMaxActive(int lane) => maxActive != null && lane >= 0 && lane < maxActive.Length ? maxActive[lane] : 0;

    /// <summary>라인별 지금 보이는 수 (확인용)</summary>
    public int GetActiveCount(int lane) => activeCounts != null && lane >= 0 && lane < activeCounts.Length ? activeCounts[lane] : 0;

    /// <summary>라인의 이펙트 (확인용)</summary>
    public HitFx GetFx(int lane, int index) => pools[lane][index];

    /// <summary>
    /// 라인마다 이펙트를 미리 만들고 위치를 정한다. 다시 부르면(재시작은 씬을 다시 불러오므로 보통 없음) 있던 것을 다시 쓴다.
    /// </summary>
    /// <param name="seed">반짝이 무작위 씨앗 (테스트는 고정, 플레이는 null)</param>
    public void Init(GearView gear, int? seed = null)
    {
        theme = UITheme.Current;
        random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        int laneCount = GameConstants.Editor.LaneCount;
        int perLane = GameConstants.Play.HitFxPoolPerLane;
        float px = gear.UnitsPerReferencePx;
        float y = gear.JudgeLineY;

        activeCounts = new int[laneCount];
        maxActive = new int[laneCount];
        laneCounts = new int[laneCount];
        System.Array.Clear(PlayCountByJudge, 0, PlayCountByJudge.Length);
        PlayCount = 0;

        if (pools != null && pools.Length == laneCount)
        {
            for (int lane = 0; lane < laneCount; lane++)
                foreach (HitFx fx in pools[lane]) fx.Stop();
            return;
        }

        HitFxSprites sprites = LoadSprites();
        pools = new HitFx[laneCount][];
        CreatedCount = 0;
        for (int lane = 0; lane < laneCount; lane++)
        {
            Transform laneRoot = new GameObject("Lane_" + lane).transform;
            laneRoot.SetParent(transform, false);
            laneRoot.localPosition = new Vector3(gear.GetLaneX(lane), y, 0f);
            pools[lane] = new HitFx[perLane];
            for (int i = 0; i < perLane; i++)
            {
                GameObject go = new GameObject("HitFx_" + i);
                go.transform.SetParent(laneRoot, false);
                HitFx fx = go.AddComponent<HitFx>();
                fx.Build(sprites, px);
                pools[lane][i] = fx;
                CreatedCount++;
            }
        }
    }

    private static HitFxSprites LoadSprites()
    {
        return new HitFxSprites
        {
            Glow = Load("Fx_Glow"),
            OrbHalo = Load("Fx_OrbHalo"),
            OrbCore = Load("Fx_OrbCore"),
            Ring = Load("Fx_Ring"),
            Dot = Load("Fx_Dot"),
            Diamond = Load("Fx_Diamond")
        };
    }

    private static Sprite Load(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(GameConstants.Play.HitFxSpriteFolder + name);
        if (sprite == null) Debug.LogWarning($"[HitFxPool] Resources/{GameConstants.Play.HitFxSpriteFolder}{name} 이미지가 없습니다.");
        return sprite;
    }

    /// <summary>
    /// 판정 결과로 이펙트를 터뜨린다 (HitFxRules.TryGetFxJudge: MISS면 없음). 터뜨렸으면 true.
    /// </summary>
    public bool Play(JudgeResult result, double songTime)
    {
        if (!HitFxRules.TryGetFxJudge(result, out JudgeType judge)) return false;
        return Play(result.Lane, judge, songTime);
    }

    /// <summary>라인에 판정 색 이펙트를 터뜨린다 (MISS면 false)</summary>
    public bool Play(int lane, JudgeType judge, double songTime)
    {
        if (pools == null || lane < 0 || lane >= pools.Length) return false;
        if (!theme.TryGetHitFxColor(judge, out Color color)) return false;

        HitFx fx = PickFree(pools[lane]);
        if (!fx.IsPlaying) activeCounts[lane]++;
        fx.Play(judge, color, theme.hitFxLightMix, songTime, random);
        if (activeCounts[lane] > maxActive[lane]) maxActive[lane] = activeCounts[lane];

        PlayCount++;
        laneCounts[lane]++;
        PlayCountByJudge[(int)judge]++;
        return true;
    }

    /// <summary>쉬는 것을 고르고, 없으면 가장 오래 전에 터진 것</summary>
    private static HitFx PickFree(HitFx[] pool)
    {
        HitFx oldest = pool[0];
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].IsPlaying) return pool[i];
            if (pool[i].StartSongTime < oldest.StartSongTime) oldest = pool[i];
        }
        return oldest;
    }

    /// <summary>매 프레임 곡 시간으로 이펙트를 다시 그린다 (일시정지 중에는 부르지 않는다)</summary>
    public void Tick(double songTime)
    {
        if (pools == null) return;
        for (int lane = 0; lane < pools.Length; lane++)
        {
            if (activeCounts[lane] == 0) continue;
            HitFx[] pool = pools[lane];
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].IsPlaying && !pool[i].Tick(songTime)) activeCounts[lane]--;
            }
        }
    }
}
