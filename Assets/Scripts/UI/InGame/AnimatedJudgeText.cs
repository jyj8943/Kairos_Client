using TMPro;
using UnityEngine;

/// <summary>
/// 판정 글자(Txt_Judge)·FAST/SLOW 글자(Txt_FastSlow) 공통: JudgeTextAnimation 값을 글자에 적용만 한다.
/// - 크기는 RectTransform localScale(기준점 = RectTransform 가운데 pivot), 투명도는 글자 색 알파(Show로 받은 색의 알파에 곱함)로 바꾼다.
/// - 시간은 Canvas_InGame.TickAnimations → Tick으로만 흐른다 (자체 Update 없음, 일시정지·카운트다운 중에는 멈춘다. 콤보 Txt_Combo와 같은 방식).
/// - 숨길 때는 TMP 컴포넌트를 끈다 (그리지 않음). 글자·색은 Show 때만 바꿔 매 프레임 GC 할당이 없다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public abstract class AnimatedJudgeText : MonoBehaviour
{
    private TMP_Text text;
    private RectTransform rect;
    private Color baseColor = Color.white;
    private readonly JudgeTextAnimation textAnimation = new JudgeTextAnimation();

    /// <summary>계산 상태 (확인용)</summary>
    public JudgeTextAnimation Animation => textAnimation;

    /// <summary>지금 보이는지 (확인용)</summary>
    public bool IsVisible => text != null && text.enabled && textAnimation.IsPlaying;

    /// <summary>지금 글자 (확인용)</summary>
    public string Text => text != null ? text.text : string.Empty;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        text = GetComponent<TMP_Text>();
        rect = (RectTransform)transform;
    }

    /// <summary>Inspector 값을 계산 클래스로 옮긴다 (각 화면 스크립트가 자기 기본값으로 채운다)</summary>
    protected abstract void ApplySettings(JudgeTextAnimation target);

    /// <summary>처음 상태 (숨김, 크기 1). Canvas_InGame.Init(씬 Init·재시작)에서 부른다</summary>
    public void ResetView()
    {
        if (text == null) CacheComponents();
        ApplySettings(textAnimation);
        textAnimation.Stop();
        text.text = string.Empty;
        Apply();
    }

    /// <summary>글자·색을 바꾸고 애니메이션을 처음부터 다시 재생한다</summary>
    public void Show(string value, Color color)
    {
        if (text == null) CacheComponents();
        ApplySettings(textAnimation);
        text.text = value;
        baseColor = color;
        textAnimation.Restart();
        Apply();
    }

    /// <summary>시간을 진행한다 (플레이 중 매 프레임, 일시정지·카운트다운 중에는 부르지 않는다)</summary>
    public void Tick(float deltaTime)
    {
        if (text == null || !textAnimation.IsPlaying) return;
        textAnimation.Tick(deltaTime);
        Apply();
    }

    /// <summary>계산값을 글자에 적용한다</summary>
    private void Apply()
    {
        JudgeTextFrame frame = textAnimation.Current;
        text.enabled = frame.Visible;
        rect.localScale = new Vector3(frame.Scale, frame.Scale, 1f);

        Color color = baseColor;
        color.a = baseColor.a * frame.Alpha;
        text.color = color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중 Inspector에서 바꾼 값을 바로 쓴다
        ApplySettings(textAnimation);
    }
#endif
}
