using TMPro;
using UnityEngine;

/// <summary>
/// 4라인 기어 표시 (SpriteRenderer 월드 오브젝트). InGameScene.unity의 "Gear"에 붙이고, InGameScene이 Init()을 호출한다.
/// - 라인 위치·판정선 높이·화면 위아래 끝(월드 y)을 노트 표시에 알려준다.
/// - 키를 누르는 동안 그 라인을 빛나게 하고, 아래 키 칸을 밝게(글자는 어둡게) 한다.
/// - 키 칸(KeyCells)은 화면 아래 끝에 붙인다. 크기·위치는 Init에서 카메라 높이로 계산한다 (1080 화면 기준 px → 월드, 해상도가 바뀌어도 같은 비율).
///   노트보다 위에 그려(sortingOrder) 놓친 노트가 판정선 아래를 지나 키 칸 뒤로 사라진다.
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

    [Header("키 칸 (KeyCells, 라인 순서)")]
    [Tooltip("키 칸 배경 (KeyCell_0 ~ 3, Sliced). 노트(sortingOrder 3)보다 위에 그린다")]
    [SerializeField] private SpriteRenderer[] keyCells;
    [Tooltip("키 칸 테두리 (KeyCell_n/Frame, Sliced)")]
    [SerializeField] private SpriteRenderer[] keyCellFrames;
    [Tooltip("키 칸 글자 (KeyCell_n/Label, TextMeshPro)")]
    [SerializeField] private TMP_Text[] keyLabels;
    [SerializeField] private Color keyCellColor = new Color32(0x3F, 0x3F, 0x46, 0xFF);
    [SerializeField] private Color keyCellFrameColor = new Color32(0x52, 0x52, 0x5B, 0xFF);
    [SerializeField] private Color keyLabelColor = new Color32(0xD4, 0xD4, 0xD8, 0xFF);
    [SerializeField] private Color keyCellPressedColor = new Color32(0xE4, 0xE4, 0xE7, 0xFF);
    [SerializeField] private Color keyCellFramePressedColor = Color.white;
    [SerializeField] private Color keyLabelPressedColor = new Color32(0x18, 0x18, 0x1B, 0xFF);

    public float LaneWidth => laneWidth;

    /// <summary>판정선 높이 (Gear 기준 로컬 y)</summary>
    public float JudgeLineY => judgeLine != null ? judgeLine.localPosition.y : 0f;

    /// <summary>화면 위 끝 (Gear 기준 로컬 y)</summary>
    public float ScreenTopY { get; private set; }

    /// <summary>화면 아래 끝 (Gear 기준 로컬 y)</summary>
    public float ScreenBottomY { get; private set; }

    /// <summary>키 칸 위 끝 (Gear 기준 로컬 y). 키 칸 아래 끝 = ScreenBottomY</summary>
    public float KeyCellTopY { get; private set; }

    /// <summary>1080 화면 기준 1px의 월드 길이 (카메라 높이 ÷ 1080)</summary>
    public float UnitsPerReferencePx { get; private set; }

    /// <summary>라인 키가 눌려 보이는지 (키 칸이 밝은지, 확인용)</summary>
    public bool IsKeyCellPressed(int lane) => keyPressed != null && lane >= 0 && lane < keyPressed.Length && keyPressed[lane];

    private bool[] keyPressed;

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
        LayoutKeyCells(halfHeight * 2f);

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
    /// 키 칸을 화면 아래 끝에 붙여 놓는다: 높이 KeyCellHeightPx, 폭 = 라인 폭 − 양옆 KeyCellSidePaddingPx (1080 기준 px).
    /// </summary>
    /// <param name="screenHeightUnits">화면 높이 (월드 단위)</param>
    private void LayoutKeyCells(float screenHeightUnits)
    {
        UnitsPerReferencePx = screenHeightUnits / GameConstants.Play.KeyCellReferenceHeightPx;
        float height = GameConstants.Play.KeyCellHeightPx * UnitsPerReferencePx;
        float width = laneWidth - GameConstants.Play.KeyCellSidePaddingPx * 2f * UnitsPerReferencePx;
        KeyCellTopY = ScreenBottomY + height;

        if (keyPressed == null || keyPressed.Length != GameConstants.Editor.LaneCount) keyPressed = new bool[GameConstants.Editor.LaneCount];
        if (keyCells == null) return;

        Vector2 size = new Vector2(width, height);
        for (int lane = 0; lane < keyCells.Length; lane++)
        {
            SpriteRenderer cell = keyCells[lane];
            if (cell == null) continue;
            cell.transform.localPosition = new Vector3(GetLaneX(lane), ScreenBottomY + height * 0.5f, 0f);
            cell.size = size;
            if (keyCellFrames != null && lane < keyCellFrames.Length && keyCellFrames[lane] != null) keyCellFrames[lane].size = size;
        }
    }

    /// <summary>
    /// 키 칸 글자를 바꾼다 (InGameInput이 입력 에셋 바인딩에서 읽은 키 이름). 라인 순서.
    /// </summary>
    public void SetKeyLabels(string[] labels)
    {
        if (keyLabels == null || labels == null) return;
        for (int lane = 0; lane < keyLabels.Length && lane < labels.Length; lane++)
        {
            if (keyLabels[lane] != null) keyLabels[lane].text = labels[lane];
        }
    }

    /// <summary>
    /// 라인 가운데 x (Gear 기준 로컬). 라인 0이 왼쪽 끝이다.
    /// </summary>
    public float GetLaneX(int lane)
    {
        return (lane - (GameConstants.Editor.LaneCount - 1) * 0.5f) * laneWidth;
    }

    /// <summary>
    /// 키를 누르는 동안 라인을 빛나게 하고 키 칸을 밝게(글자는 어둡게) 한다.
    /// 라인 빛남은 개발용 설정(DevPlaySettings.LaneGlowEnabled)이 꺼져 있으면 켜지 않는다 (끄기는 항상 한다). 키 칸은 설정과 상관없이 바꾼다.
    /// 자동 플레이·일시정지도 이 함수 하나로 켜고 끈다 (InGameScene).
    /// </summary>
    public void SetPressed(int lane, bool pressed)
    {
        if (lane < 0 || lane >= GameConstants.Editor.LaneCount) return;

        if (laneGlows != null && lane < laneGlows.Length && laneGlows[lane] != null)
            laneGlows[lane].enabled = pressed && DevPlaySettings.LaneGlowEnabled;

        if (keyPressed != null && lane < keyPressed.Length) keyPressed[lane] = pressed;
        if (keyCells != null && lane < keyCells.Length && keyCells[lane] != null)
            keyCells[lane].color = pressed ? keyCellPressedColor : keyCellColor;
        if (keyCellFrames != null && lane < keyCellFrames.Length && keyCellFrames[lane] != null)
            keyCellFrames[lane].color = pressed ? keyCellFramePressedColor : keyCellFrameColor;
        if (keyLabels != null && lane < keyLabels.Length && keyLabels[lane] != null)
            keyLabels[lane].color = pressed ? keyLabelPressedColor : keyLabelColor;
    }
}
