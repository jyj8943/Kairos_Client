using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 콤보 숫자 (Txt_Combo). 부모 Combo 그룹(CanvasGroup) 아래에 COMBO 제목(Txt_ComboTitle)과 함께 있다.
/// - 오름: 콤보가 오르면 숫자만 아래에서 투명하게 나타나 제자리로 올라온다 (anchoredPosition.y와 텍스트 알파만 바꾼다, 크기는 그대로).
///   COMBO 제목은 움직이지 않는다.
/// - 쉬면 숨기기: 한동안 안 오르면 Combo 그룹 전체가 조금 내려가며 사라진다 (CanvasGroup 알파 + 그룹 위치).
/// - 값 계산은 ComboMotion이 하고, 시간은 Canvas_InGame.TickCombo → Tick으로만 흐른다 (자체 Update 없음, 일시정지 중에는 멈춘다).
/// - 숫자는 SetText("{0}", combo)로 바꿔 GC 할당을 만들지 않는다.
/// (DJMAX RESPECT V 기본 기어·PLATiNA :: LAB 기본 스킨 플레이 영상을 프레임 단위로 잰 값을 기본값으로 둔다)
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class Txt_Combo : MonoBehaviour
{
    [Tooltip("COMBO 제목과 숫자를 묶는 그룹 (Combo). 쉬면 숨길 때 알파·위치를 바꾼다")]
    [SerializeField] private CanvasGroup comboGroup;

    [Header("오름 애니메이션")]
    [Tooltip("오름 시작 위치: 제자리보다 이만큼 아래 (px, 글꼴 96 숫자 높이의 약 45%)")]
    [SerializeField] private float riseOffset = 30f;
    [Tooltip("투명 → 불투명 시간 (초)")]
    [SerializeField] private float fadeInSeconds = 0.05f;
    [Tooltip("제자리까지 올라오는 시간 (초, ease-out quad)")]
    [SerializeField] private float riseSeconds = 0.13f;

    [Header("쉬면 숨기기")]
    [Tooltip("한동안 안 오르면 콤보를 숨긴다")]
    [SerializeField] private bool hideWhenIdle = true;
    [Tooltip("마지막으로 오르고 이만큼 안 오르면 숨기기 시작 (초)")]
    [SerializeField] private float idleHideSeconds = 0.67f;
    [Tooltip("사라지는 시간 (초)")]
    [SerializeField] private float idleFadeOutSeconds = 0.07f;
    [Tooltip("사라질 때 Combo 그룹이 내려가는 거리 (px)")]
    [SerializeField] private float idleSinkOffset = 8f;

    private TMP_Text text;
    private RectTransform rect;
    private RectTransform groupRect;
    private readonly ComboMotion motion = new ComboMotion();

    private bool hasBase;
    private Vector2 basePosition;
    private Vector2 groupBasePosition;
    private Color baseColor;

    /// <summary>계산 상태 (확인용)</summary>
    public ComboMotion Motion => motion;

    /// <summary>Combo 그룹 (확인용)</summary>
    public CanvasGroup Group => comboGroup;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        text = GetComponent<TMP_Text>();
        rect = (RectTransform)transform;
    }

    /// <summary>
    /// 처음 상태로 돌린다 (숨김, 콤보 0). Canvas_InGame.Init(씬 Init·재시작)에서 부른다.
    /// 처음 부를 때 Inspector에 둔 위치·색을 기준값으로 기억한다.
    /// </summary>
    public void ResetView()
    {
        if (text == null) CacheComponents();
        if (!hasBase)
        {
            basePosition = rect.anchoredPosition;
            baseColor = text.color;
            if (comboGroup != null)
            {
                groupRect = (RectTransform)comboGroup.transform;
                groupBasePosition = groupRect.anchoredPosition;
            }
            hasBase = true;
        }

        ApplySettings();
        motion.Reset();
        text.text = string.Empty;
        Apply();
    }

    /// <summary>
    /// 콤보를 바꾼다. 이전 값보다 커졌을 때만 오름 애니메이션·쉬는 시간을 처음부터 시작한다.
    /// 같은 값이면 아무것도 하지 않고 (롱노트 종합 판정 등), 0이면 바로 숨긴다.
    /// </summary>
    public void SetCombo(int combo)
    {
        if (!hasBase) ResetView();

        int before = motion.Combo;
        if (combo == before) return;

        motion.SetCombo(combo);
        if (motion.Combo > 0) text.SetText("{0}", motion.Combo);
        else text.text = string.Empty;
        Apply();
    }

    /// <summary>애니메이션·쉬는 시간을 진행한다 (플레이 중 매 프레임, 일시정지 중에는 부르지 않는다)</summary>
    public void Tick(float deltaTime)
    {
        if (!hasBase || motion.Combo <= 0) return;

        float before = motion.Elapsed;
        motion.Tick(deltaTime);
        if (motion.Elapsed != before) Apply();
    }

    /// <summary>Inspector 값을 계산 클래스로 옮긴다</summary>
    private void ApplySettings()
    {
        motion.RiseOffset = riseOffset;
        motion.FadeInSeconds = fadeInSeconds;
        motion.RiseSeconds = riseSeconds;
        motion.HideWhenIdle = hideWhenIdle;
        motion.IdleHideSeconds = idleHideSeconds;
        motion.IdleFadeOutSeconds = idleFadeOutSeconds;
        motion.IdleSinkOffset = idleSinkOffset;
    }

    /// <summary>계산값을 화면에 적용한다 (숫자 위치·알파, 그룹 위치·알파)</summary>
    private void Apply()
    {
        rect.anchoredPosition = new Vector2(basePosition.x, basePosition.y - motion.NumberOffset);

        Color color = baseColor;
        color.a = baseColor.a * motion.NumberAlpha;
        text.color = color;

        if (comboGroup != null)
        {
            comboGroup.alpha = motion.GroupAlpha;
            groupRect.anchoredPosition = new Vector2(groupBasePosition.x, groupBasePosition.y - motion.GroupOffset);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중 Inspector에서 바꾼 값을 바로 쓴다
        if (hasBase) ApplySettings();
    }
#endif
}
