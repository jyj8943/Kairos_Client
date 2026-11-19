using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기어 판정 글자 이미지(Img_Judge)·FAST/SLOW 이미지(Img_FastSlow) 공통: JudgeTextAnimation 값을 Image에 적용만 한다 (11/19 TMP 글자 → 이미지).
/// - 색·PERFECT 그라데이션·빛은 이미지 안에 있다 (Resources/Images/InGame/Judge/, JudgeSprites). 색 칠하기·머티리얼 처리는 하지 않는다.
/// - 이미지가 바뀌면 크기 = 원본 크기 × GameConstants.Play.JudgeSpriteDisplayScale(0.5, 이미지는 화면 크기의 2배로 만듦). pivot (0.5, 0.5) 가운데 기준.
/// - 크기 애니메이션은 RectTransform localScale(1에서 시작), 투명도는 Image.color 알파(흰색 × 알파)로 바꾼다.
/// - 시간은 Canvas_InGame.TickAnimations → Tick으로만 흐른다 (자체 Update 없음, 일시정지·카운트다운 중에는 멈춘다).
/// - 숨길 때는 Image를 끈다 (그리지 않음). 이미지는 Show 때만 바꿔 매 프레임 GC 할당이 없다.
/// </summary>
[RequireComponent(typeof(Image))]
public abstract class AnimatedJudgeImage : MonoBehaviour
{
    private Image image;
    private RectTransform rect;
    private readonly JudgeTextAnimation textAnimation = new JudgeTextAnimation();

    /// <summary>계산 상태 (확인용)</summary>
    public JudgeTextAnimation Animation => textAnimation;

    /// <summary>지금 보이는지 (확인용)</summary>
    public bool IsVisible => image != null && image.enabled && textAnimation.IsPlaying;

    /// <summary>지금 이미지 (확인용, Show 전이면 null)</summary>
    public Sprite ShownSprite => image != null ? image.sprite : null;

    /// <summary>Image 컴포넌트 (확인용)</summary>
    public Image ImageComponent => image != null ? image : GetComponent<Image>();

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
    }

    /// <summary>Inspector 값을 계산 클래스로 옮긴다 (각 화면 스크립트가 자기 기본값으로 채운다)</summary>
    protected abstract void ApplySettings(JudgeTextAnimation target);

    /// <summary>처음 상태 (숨김, 크기 1). Canvas_InGame.Init(씬 Init·재시작)에서 부른다</summary>
    public void ResetView()
    {
        if (image == null) CacheComponents();
        ApplySettings(textAnimation);
        textAnimation.Stop();
        Apply();
    }

    /// <summary>이미지를 바꾸고 애니메이션을 처음부터 다시 재생한다 (이미지가 바뀌면 크기도 맞춘다)</summary>
    public void Show(Sprite sprite)
    {
        if (image == null) CacheComponents();
        ApplySettings(textAnimation);
        if (image.sprite != sprite)
        {
            image.sprite = sprite;
            rect.sizeDelta = JudgeSprites.DisplaySize(sprite);
        }
        textAnimation.Restart();
        Apply();
    }

    /// <summary>시간을 진행한다 (플레이 중 매 프레임, 일시정지·카운트다운 중에는 부르지 않는다)</summary>
    public void Tick(float deltaTime)
    {
        if (image == null || !textAnimation.IsPlaying) return;
        textAnimation.Tick(deltaTime);
        Apply();
    }

    /// <summary>계산값을 Image에 적용한다</summary>
    private void Apply()
    {
        JudgeTextFrame frame = textAnimation.Current;
        image.enabled = frame.Visible && image.sprite != null;
        rect.localScale = new Vector3(frame.Scale, frame.Scale, 1f);
        image.color = new Color(1f, 1f, 1f, frame.Alpha);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중 Inspector에서 바꾼 값을 바로 쓴다
        ApplySettings(textAnimation);
    }
#endif
}
