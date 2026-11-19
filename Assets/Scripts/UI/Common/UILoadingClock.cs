using UnityEngine;

/// <summary>
/// 불러오는 중 작은 시계 (곡 로딩 · 일반 로딩 공용). 보라 원(Clock_Ring) 오브젝트에 붙이고 바늘(자식)을 1초에 한 바퀴 돌린다.
/// 시간은 Time.unscaledDeltaTime (로딩 중 timeScale과 상관없이). 색은 쓰는 화면이 UITheme으로 칠한다.
/// </summary>
public class UILoadingClock : MonoBehaviour
{
    /// <summary>바늘이 한 바퀴 도는 시간 (초)</summary>
    public const float TurnSeconds = 1f;

    [Tooltip("돌릴 바늘 (pivot = 시계 가운데)")]
    [SerializeField] private RectTransform hand;

    private float angle;

    /// <summary>지금 바늘 각도 (시계 방향 +, 확인용)</summary>
    public float Angle => angle;

    private void Update()
    {
        angle = Mathf.Repeat(angle + 360f * Time.unscaledDeltaTime / TurnSeconds, 360f);
        if (hand != null) hand.localEulerAngles = new Vector3(0f, 0f, -angle);
    }
}
