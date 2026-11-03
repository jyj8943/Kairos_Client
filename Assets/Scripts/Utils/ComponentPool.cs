using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹 인스턴스를 재사용하는 단순 오브젝트 풀.
/// - Prewarm으로 미리 만들어 두고, Get/ReleaseAll로 꺼내고 돌려놓는다.
/// - 미리 만든 수가 모자라면 그때만 새로 만들고 경고를 남긴다.
/// </summary>
public class ComponentPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly List<T> items = new List<T>();
    private int activeCount;

    /// <summary>풀이 가진 전체 인스턴스 수</summary>
    public int TotalCount => items.Count;

    /// <summary>지금 꺼내 쓰는 인스턴스 수</summary>
    public int ActiveCount => activeCount;

    /// <summary>Prewarm 뒤에 모자라서 새로 만든 횟수</summary>
    public int GrowCount { get; private set; }

    public ComponentPool(T prefab, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
    }

    /// <summary>
    /// count개가 될 때까지 미리 만들어 꺼 둔다.
    /// </summary>
    public void Prewarm(int count)
    {
        while (items.Count < count)
            items.Add(Create());
    }

    /// <summary>
    /// 이번 갱신에서 쓸 인스턴스를 하나 꺼낸다. 꺼낸 순서대로 앞에서부터 재사용한다.
    /// </summary>
    public T Get()
    {
        if (activeCount >= items.Count)
        {
            items.Add(Create());
            GrowCount++;
            Debug.LogWarning($"[ComponentPool] {prefab.name} 풀이 모자라 새로 만들었습니다. (전체 {items.Count}개)");
        }

        T item = items[activeCount++];
        if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
        return item;
    }

    /// <summary>
    /// 갱신을 시작할 때 호출한다. 이후 Get한 만큼만 켜지고 나머지는 EndFrame에서 꺼진다.
    /// </summary>
    public void BeginFrame()
    {
        activeCount = 0;
    }

    /// <summary>
    /// 이번 갱신에서 꺼내지 않은 인스턴스를 끈다.
    /// </summary>
    public void EndFrame()
    {
        for (int i = activeCount; i < items.Count; i++)
        {
            if (items[i].gameObject.activeSelf) items[i].gameObject.SetActive(false);
        }
    }

    private T Create()
    {
        T item = Object.Instantiate(prefab, parent);
        item.name = prefab.name;
        item.gameObject.SetActive(false);
        return item;
    }
}
