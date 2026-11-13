using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 에디터 하단 타임라인 슬라이더 (Slider_Timeline). Canvas_Editor가 Init()을 호출한다.
/// - 값은 현재 시간 ÷ 곡 길이 (0~1). 코드로 바꿀 때는 SetValueWithoutNotify로 이벤트를 다시 부르지 않는다.
/// - 누를 때·드래그로 값이 바뀔 때·놓을 때를 따로 알려준다 (누르면 재생 정지, 놓으면 스냅 선에 맞춤).
/// - BPM 구간 눈금: 막대 위 BpmMarkers(막대와 같은 폭)에 구간 위치마다 청록 눈금(Img_BpmMarker)과 작은 BPM 숫자(Txt_BpmMarker)를 둔다.
///   곡 시작 구간은 표시하지 않는다. 눈금은 클릭을 받지 않는다.
/// </summary>
[RequireComponent(typeof(Slider))]
public class Slider_Timeline : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("BPM 구간 눈금")]
    [Tooltip("눈금을 놓을 곳 (BpmMarkers, 막대와 같은 폭, 왼쪽 = 0)")]
    [SerializeField] private RectTransform markerParent;
    [Tooltip("눈금 템플릿 (Img_BpmMarker, 자식 Txt_BpmMarker). 꺼 둔다")]
    [SerializeField] private Image markerTemplate;

    private readonly List<Image> markers = new List<Image>();
    private int markerCount;

    /// <summary>지금 보이는 눈금 수 (확인용)</summary>
    public int MarkerCount => markerCount;

    /// <summary>i번째 눈금 (확인용)</summary>
    public Image GetMarker(int index) => index >= 0 && index < markerCount ? markers[index] : null;

    private Slider slider;
    private Action onPressed;
    private Action<float> onDragged;
    private Action<float> onReleased;
    private bool isPressed;

    /// <summary>마우스로 누르고 있는 중이면 true</summary>
    public bool IsPressed => isPressed;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    /// <summary>
    /// 범위를 0~1로 맞추고 이벤트를 받을 곳을 받는다.
    /// </summary>
    /// <param name="onPressed">누르기 시작했을 때</param>
    /// <param name="onDragged">누른 채 값이 바뀔 때 (0~1)</param>
    /// <param name="onReleased">놓았을 때 (0~1)</param>
    public void Init(Action onPressed, Action<float> onDragged, Action<float> onReleased)
    {
        this.onPressed = onPressed;
        this.onDragged = onDragged;
        this.onReleased = onReleased;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.RemoveListener(OnValueChanged);
        slider.onValueChanged.AddListener(OnValueChanged);
    }

    /// <summary>
    /// 값을 이벤트 없이 바꾼다 (재생·스크롤·복원 때). 누르고 있는 중에는 바꾸지 않는다.
    /// </summary>
    public void SetPosition(float normalized)
    {
        if (isPressed) return;
        slider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
    }

    /// <summary>
    /// BPM 구간 눈금을 다시 놓는다.
    /// </summary>
    /// <param name="positions">구간 위치 (곡 길이에 대한 비율 0~1, 곡 시작 구간 제외)</param>
    /// <param name="labels">눈금 위 BPM 글자 (positions와 같은 순서)</param>
    public void SetBpmMarkers(IReadOnlyList<float> positions, IReadOnlyList<string> labels)
    {
        if (markerParent == null || markerTemplate == null) return;

        markerTemplate.gameObject.SetActive(false);
        while (markers.Count < positions.Count)
        {
            Image marker = Instantiate(markerTemplate, markerParent);
            marker.name = markerTemplate.name;
            markers.Add(marker);
        }

        float width = markerParent.rect.width;
        for (int i = 0; i < markers.Count; i++)
        {
            bool used = i < positions.Count;
            if (markers[i].gameObject.activeSelf != used) markers[i].gameObject.SetActive(used);
            if (!used) continue;

            RectTransform rect = markers[i].rectTransform;
            rect.anchoredPosition = new Vector2(Mathf.Clamp01(positions[i]) * width, rect.anchoredPosition.y);
            TMP_Text label = markers[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = labels[i];
        }
        markerCount = positions.Count;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        onPressed?.Invoke();
        // 클릭한 순간에도 그 위치로 간다 (Slider가 값을 먼저 바꾼 뒤라 현재 값을 넘긴다)
        onDragged?.Invoke(slider.value);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;

        isPressed = false;
        onReleased?.Invoke(slider.value);
    }

    private void OnValueChanged(float value)
    {
        // 키보드 조작(방향키)도 드래그처럼 다룬다
        if (isPressed) onDragged?.Invoke(value);
        else
        {
            onPressed?.Invoke();
            onReleased?.Invoke(value);
        }
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(OnValueChanged);
    }
}
