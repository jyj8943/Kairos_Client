using UnityEngine;

/// <summary>
/// 4라인 기어 표시 (SpriteRenderer 월드 오브젝트). InGameScene.unity의 "Gear"에 붙이고, InGameScene이 Init()을 호출한다.
/// - 라인 위치·판정선 높이·화면 위아래 끝(월드 y)을 노트 표시에 알려준다.
/// - 키를 누르는 동안 그 라인을 빛나게 한다.
/// </summary>
public class GearView : MonoBehaviour
{
    [Tooltip("라인 한 칸의 폭 (월드 단위)")]
    [Min(0.1f)] [SerializeField] private float laneWidth = 1f;
    [Tooltip("판정선 (Gear 아래 자식). 노트가 이 높이에 닿는 순간이 노트 시간이다")]
    [SerializeField] private Transform judgeLine;
    [Tooltip("라인별 빛남 (A · S · ; · ' 순서). 키를 누르는 동안 켠다. 스프라이트는 아래가 진하고 위로 갈수록 투명한 LaneGlowGradient")]
    [SerializeField] private SpriteRenderer[] laneGlows;
    [Tooltip("라인 빛남 색. 알파는 판정선 쪽(가장 진한 곳)의 알파다")]
    [SerializeField] private Color glowColor = new Color32(220, 220, 220, 153);
    [Tooltip("화면 위아래 끝 계산에 쓰는 카메라")]
    [SerializeField] private Camera viewCamera;

    public float LaneWidth => laneWidth;

    /// <summary>판정선 높이 (Gear 기준 로컬 y)</summary>
    public float JudgeLineY => judgeLine != null ? judgeLine.localPosition.y : 0f;

    /// <summary>화면 위 끝 (Gear 기준 로컬 y)</summary>
    public float ScreenTopY { get; private set; }

    /// <summary>화면 아래 끝 (Gear 기준 로컬 y)</summary>
    public float ScreenBottomY { get; private set; }

    /// <summary>
    /// 화면 끝을 계산하고, 라인 빛남에 색을 입힌 뒤 끈다.
    /// </summary>
    public void Init()
    {
        if (viewCamera == null) viewCamera = Camera.main;

        float halfHeight = viewCamera != null && viewCamera.orthographic ? viewCamera.orthographicSize : 5f;
        float cameraY = viewCamera != null ? viewCamera.transform.position.y : 0f;
        ScreenTopY = cameraY + halfHeight - transform.position.y;
        ScreenBottomY = cameraY - halfHeight - transform.position.y;

        if (laneGlows != null)
        {
            foreach (SpriteRenderer glow in laneGlows)
            {
                if (glow != null) glow.color = glowColor;
            }
        }

        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
            SetPressed(lane, false);
    }

    /// <summary>
    /// 라인 가운데 x (Gear 기준 로컬). 라인 0이 왼쪽 끝이다.
    /// </summary>
    public float GetLaneX(int lane)
    {
        return (lane - (GameConstants.Editor.LaneCount - 1) * 0.5f) * laneWidth;
    }

    /// <summary>
    /// 키를 누르는 동안 라인을 빛나게 한다.
    /// </summary>
    public void SetPressed(int lane, bool pressed)
    {
        if (laneGlows == null || lane < 0 || lane >= laneGlows.Length || laneGlows[lane] == null) return;
        laneGlows[lane].enabled = pressed;
    }
}
