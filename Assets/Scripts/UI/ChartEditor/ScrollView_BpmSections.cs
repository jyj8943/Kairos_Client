using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오른쪽 패널 BPM 구간 목록 (ScrollView_BpmSections). Canvas_Editor가 Init()을 호출한다.
/// - 줄(Btn_BpmSectionItem)은 구간 수만큼 프리팹에서 만들어 재사용한다 (Content의 VerticalLayoutGroup이 위에서부터 쌓는다).
/// - 4줄 넘게 있으면 스크롤된다 (Viewport 높이 = 4줄).
/// - 현재 위치가 속한 구간 줄을 강조한다 (SetCurrent). 줄을 클릭하면 onChosen(구간 tick).
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollView_BpmSections : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private Btn_BpmSectionItem itemPrefab;

    private readonly List<Btn_BpmSectionItem> items = new List<Btn_BpmSectionItem>();
    private Action<int> onChosen;
    private int visibleCount;
    private int currentIndex = -1;

    /// <summary>지금 보이는 줄 수 (확인용)</summary>
    public int Count => visibleCount;

    /// <summary>강조 중인 줄 번호 (확인용)</summary>
    public int CurrentIndex => currentIndex;

    /// <summary>i번째 줄 (확인용)</summary>
    public Btn_BpmSectionItem GetItem(int index) => index >= 0 && index < visibleCount ? items[index] : null;

    /// <summary>줄 클릭 콜백을 받는다</summary>
    public void Init(Action<int> onChosen)
    {
        this.onChosen = onChosen;
        currentIndex = -1;
    }

    /// <summary>구간 목록으로 줄을 다시 채운다</summary>
    public void SetSections(IReadOnlyList<BpmSection> sections)
    {
        while (items.Count < sections.Count)
        {
            Btn_BpmSectionItem item = Instantiate(itemPrefab, content);
            item.name = itemPrefab.name;
            items.Add(item);
        }

        for (int i = 0; i < items.Count; i++)
        {
            bool used = i < sections.Count;
            if (items[i].gameObject.activeSelf != used) items[i].gameObject.SetActive(used);
            if (used) items[i].Init(sections[i], onChosen);
        }

        visibleCount = sections.Count;
        currentIndex = -1;
    }

    /// <summary>현재 위치가 속한 구간 줄만 강조한다. 바뀌었을 때만 색을 바꾼다</summary>
    public void SetCurrent(int index)
    {
        if (index == currentIndex) return;

        if (currentIndex >= 0 && currentIndex < visibleCount) items[currentIndex].SetHighlighted(false);
        currentIndex = index;
        if (currentIndex >= 0 && currentIndex < visibleCount) items[currentIndex].SetHighlighted(true);
    }
}
