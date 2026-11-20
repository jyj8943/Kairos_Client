using TMPro;
using UnityEngine;

/// <summary>
/// 4라인 기어 표시 (SpriteRenderer 월드 오브젝트). InGameScene.unity의 "Gear"에 붙이고, InGameScene이 Init()을 호출한다.
/// - 라인 위치·판정선 높이·화면 위아래 끝(월드 y)을 노트 표시에 알려준다.
/// - 키를 누르는 동안 그 라인을 빛나게 하고, 아래 키 칸을 포인트색으로 채운다.
/// - 키 칸 가운데(y 1020)에는 라인 색 마름모(11/20): 평소 Key_DiamondLine(반대각 16, 선 3) + 가운데 Key_Diamond 0.375배, 누를 때 Key_Diamond(반대각 16) 흰색 채움.
///   키 칸 글자(keyLabels)는 평소에 끄고, 판 시작 때만 어두운 판(Key_LabelPlate 56×56) 위에 보여 준다 (SetKeyLabelHintAlpha, 규칙은 KeyLabelHintRules).
///   스프라이트는 Resources/Images/InGame/KeyCell/에서 Init 때 읽는다. 그리는 순서는 GameConstants.InGameSort.
/// - 키 칸(KeyCells)은 화면 아래 끝에 붙인다. 크기·위치는 Init에서 카메라 높이로 계산한다 (1080 화면 기준 px → 월드, 해상도가 바뀌어도 같은 비율).
///   노트보다 위에 그려(sortingOrder) 놓친 노트가 판정선 아래를 지나 키 칸 뒤로 사라진다.
/// - 색은 모두 UITheme 인게임 묶음에서 Init 때 읽어 칠한다 (기획서 3-4 「인게임 톤 개편 (11/18)」, 어두운 기어). 씬에 색을 직렬화하지 않는다.
/// - 판정선은 이미지(Resources/Images/InGame/JudgeLine, 분홍 → 보라)를 그대로 보여 준다: 색은 흰색(덧칠 없음), Draw Mode Sliced(좌우 9-slice),
///   크기 = 기어 폭 × 두께(GameConstants.Play.JudgeLineThicknessPx). 세로 중심(JudgeLineY)은 씬 위치 그대로라 노트가 닿는 높이는 바뀌지 않는다.
/// </summary>
public class GearView : MonoBehaviour
{
    [Tooltip("라인 한 칸의 폭 (월드 단위)")]
    [Min(0.1f)] [SerializeField] private float laneWidth = 1f;
    [Tooltip("판정선 (Gear 아래 자식, SpriteRenderer Sliced, JudgeLine 이미지). 세로 중심에 노트가 닿는 순간이 노트 시간이다")]
    [SerializeField] private Transform judgeLine;
    [Tooltip("라인별 빛남 (A · S · ; · ' 순서). 키를 누르는 동안 켠다. 스프라이트는 아래가 진하고 위로 갈수록 투명한 LaneGlowGradient")]
    [SerializeField] private SpriteRenderer[] laneGlows;
    [Tooltip("화면 위아래 끝 계산에 쓰는 카메라")]
    [SerializeField] private Camera viewCamera;

    [Header("기어 바탕·선 (색은 UITheme)")]
    [Tooltip("기어 바탕 (LaneBackground)")]
    [SerializeField] private SpriteRenderer laneBackground;
    [Tooltip("양옆 테두리 (Border_Left, Border_Right)")]
    [SerializeField] private SpriteRenderer[] borders;
    [Tooltip("라인 구분선 (LaneDivider_1 ~ 3)")]
    [SerializeField] private SpriteRenderer[] laneDividers;

    [Header("키 칸 (KeyCells, 라인 순서)")]
    [Tooltip("키 칸 배경 (KeyCell_0 ~ 3, Sliced). 노트·판정 이펙트보다 위에 그린다 (GameConstants.InGameSort.KeyCell)")]
    [SerializeField] private SpriteRenderer[] keyCells;
    [Tooltip("키 칸 테두리 (KeyCell_n/Frame, Sliced)")]
    [SerializeField] private SpriteRenderer[] keyCellFrames;
    [Tooltip("키 칸 글자 (KeyCell_n/Label, TextMeshPro). 평소에는 끄고 시작 키 글자 안내 때만 보인다")]
    [SerializeField] private TMP_Text[] keyLabels;
    [Tooltip("키 칸 마름모 테두리 (KeyCell_n/KeyDiamondLine_n, Key_DiamondLine 스프라이트)")]
    [SerializeField] private SpriteRenderer[] keyDiamondLines;
    [Tooltip("키 칸 가운데 마름모 (KeyCell_n/KeyDiamond_n, Key_Diamond 스프라이트). 평소 0.375배 라인 색, 누를 때 1배 흰색")]
    [SerializeField] private SpriteRenderer[] keyDiamonds;
    [Tooltip("시작 키 글자 뒤 판 (KeyCell_n/KeyLabelPlate_n, Key_LabelPlate 스프라이트)")]
    [SerializeField] private SpriteRenderer[] keyLabelPlates;

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

    /// <summary>판정선 그림 (확인용)</summary>
    public SpriteRenderer JudgeLineRenderer => judgeLine != null ? judgeLine.GetComponent<SpriteRenderer>() : null;

    /// <summary>라인 키 칸의 지금 채움 색 (확인용, 없으면 투명)</summary>
    public Color GetKeyCellColor(int lane) => keyCells != null && lane >= 0 && lane < keyCells.Length && keyCells[lane] != null ? keyCells[lane].color : Color.clear;

    /// <summary>라인 키가 눌려 보이는지 (키 칸이 밝은지, 확인용)</summary>
    public bool IsKeyCellPressed(int lane) => keyPressed != null && lane >= 0 && lane < keyPressed.Length && keyPressed[lane];

    private bool[] keyPressed;

    /// <summary>지금 시작 키 글자 알파 (확인용)</summary>
    public float KeyLabelHintAlpha { get; private set; }

    /// <summary>키 칸 마름모 (확인용)</summary>
    public SpriteRenderer GetKeyDiamond(int lane) => Pick(keyDiamonds, lane);

    /// <summary>키 칸 마름모 테두리 (확인용)</summary>
    public SpriteRenderer GetKeyDiamondLine(int lane) => Pick(keyDiamondLines, lane);

    /// <summary>시작 키 글자 뒤 판 (확인용)</summary>
    public SpriteRenderer GetKeyLabelPlate(int lane) => Pick(keyLabelPlates, lane);

    /// <summary>키 칸 글자 (확인용)</summary>
    public TMP_Text GetKeyLabel(int lane) => keyLabels != null && lane >= 0 && lane < keyLabels.Length ? keyLabels[lane] : null;

    private static SpriteRenderer Pick(SpriteRenderer[] renderers, int lane) => renderers != null && lane >= 0 && lane < renderers.Length ? renderers[lane] : null;

    /// <summary>
    /// 화면 끝을 계산하고 키 칸·판정선 크기를 맞춘 뒤, 기어·라인 빛에 UITheme 색을 입히고 라인 빛과 키 칸 눌림을 끈다.
    /// </summary>
    public void Init()
    {
        if (viewCamera == null) viewCamera = Camera.main;

        float halfHeight = viewCamera != null && viewCamera.orthographic ? viewCamera.orthographicSize : 5f;
        float cameraY = viewCamera != null ? viewCamera.transform.position.y : 0f;
        ScreenTopY = cameraY + halfHeight - transform.position.y;
        ScreenBottomY = cameraY - halfHeight - transform.position.y;
        LayoutKeyCells(halfHeight * 2f);
        LayoutJudgeLine();

        ApplyTheme(UITheme.Current);
        LayoutKeyMarks();

        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
            SetPressed(lane, false);
        SetKeyLabelHintAlpha(0f);
    }

    /// <summary>
    /// 키 칸 마름모 · 키 글자 판 · 키 글자를 키 칸 가운데(y 1020)에 놓고 스프라이트 · 크기 · 그리는 순서를 정한다.
    /// 크기는 표시 px(반대각 16, 판 56)를 원본 px와 PPU로 나눈 배율로 맞춘다 (판은 원본 112px 9-slice 모서리 20 → 표시 56px 모서리 10).
    /// </summary>
    private void LayoutKeyMarks()
    {
        Sprite diamondLine = LoadKeyCellSprite("Key_DiamondLine");
        Sprite diamond = LoadKeyCellSprite("Key_Diamond");
        Sprite plate = LoadKeyCellSprite("Key_LabelPlate");
        float centerY = ScreenTopY - GameConstants.Play.KeyDiamondCenterYPx * UnitsPerReferencePx;
        float fontSize = KeyLabelFontSize(UnitsPerReferencePx);

        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            Vector3 center = new Vector3(GetLaneX(lane), centerY, 0f);
            Place(Pick(keyDiamondLines, lane), diamondLine, center, KeyDiamondScale(diamondLine, 1f), GameConstants.InGameSort.KeyDiamond);
            Place(Pick(keyDiamonds, lane), diamond, center, KeyDiamondScale(diamond, GameConstants.Play.KeyDiamondDotScale), GameConstants.InGameSort.KeyDiamond);
            Place(Pick(keyLabelPlates, lane), plate, center, HitFxRules.SpriteScale(GameConstants.Play.KeyLabelPlateSizePx, plate, UnitsPerReferencePx), GameConstants.InGameSort.KeyLabelPlate);

            TMP_Text label = GetKeyLabel(lane);
            if (label == null) continue;
            label.transform.position = transform.TransformPoint(center);
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            Renderer labelRenderer = label.GetComponent<Renderer>();
            if (labelRenderer != null) labelRenderer.sortingOrder = GameConstants.InGameSort.KeyLabel;
        }
    }

    /// <summary>키 글자 TMP(월드) 글꼴 크기: 표시 px × 1px 월드 길이 × 10 (TextMeshPro 월드 글꼴 크기 1 = 0.1유닛)</summary>
    public static float KeyLabelFontSize(float unitsPerPx) => GameConstants.Play.KeyLabelHintFontPx * unitsPerPx * 10f;

    /// <summary>키 칸 마름모 배율: 반대각 16px × 배율이 되게 (원본 반대각 32px, PPU 100)</summary>
    private float KeyDiamondScale(Sprite sprite, float scale)
    {
        if (sprite == null) return 0f;
        float nativeHalfUnits = GameConstants.Play.KeyDiamondSourceHalfDiagonalPx / sprite.pixelsPerUnit;
        return GameConstants.Play.KeyDiamondHalfDiagonalPx * scale * UnitsPerReferencePx / nativeHalfUnits;
    }

    private void Place(SpriteRenderer renderer, Sprite sprite, Vector3 gearLocalCenter, float scale, int sortingOrder)
    {
        if (renderer == null) return;
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.transform.position = transform.TransformPoint(gearLocalCenter);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        renderer.sortingOrder = sortingOrder;
    }

    private static Sprite LoadKeyCellSprite(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(GameConstants.Play.KeyCellSpriteFolder + name);
        if (sprite == null) Debug.LogWarning($"[GearView] Resources/{GameConstants.Play.KeyCellSpriteFolder}{name} 이미지가 없습니다.");
        return sprite;
    }

    /// <summary>
    /// 시작 키 글자 안내의 알파 (판과 글자를 함께). 0이면 끈다. 규칙은 KeyLabelHintRules (InGameScene이 매 프레임 부른다).
    /// </summary>
    public void SetKeyLabelHintAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        KeyLabelHintAlpha = alpha;
        bool visible = alpha > 0f;
        UITheme theme = UITheme.Current;
        for (int lane = 0; lane < GameConstants.Editor.LaneCount; lane++)
        {
            SpriteRenderer plate = Pick(keyLabelPlates, lane);
            if (plate != null)
            {
                plate.enabled = visible;
                plate.color = UITheme.WithAlpha(theme.keyLabelPlate, theme.keyLabelPlate.a * alpha);
            }
            TMP_Text label = GetKeyLabel(lane);
            if (label != null)
            {
                label.enabled = visible;
                label.color = UITheme.WithAlpha(theme.keyLabel, theme.keyLabel.a * alpha);
            }
        }
    }

    /// <summary>기어 바탕·테두리·구분선·라인 빛 색을 테마로 칠한다 (키 칸은 SetPressed가, 판정선은 이미지 그대로라 칠하지 않는다)</summary>
    private void ApplyTheme(UITheme theme)
    {
        if (laneBackground != null) laneBackground.color = theme.gearBackground;
        Paint(borders, theme.gearBorder);
        Paint(laneDividers, theme.laneDivider);
        Paint(laneGlows, theme.laneGlow);
    }

    /// <summary>
    /// 판정선을 기어 폭 × 두께(JudgeLineThicknessPx, 1080 기준 px)로 맞춘다. 위치(세로 중심)는 그대로 둔다.
    /// 이미지를 그대로 보여 주므로 색은 흰색, 크기는 scale이 아니라 Sliced size로 정한다 (좌우 끝 모양 유지).
    /// </summary>
    private void LayoutJudgeLine()
    {
        SpriteRenderer line = JudgeLineRenderer;
        if (line == null) return;

        line.transform.localScale = Vector3.one;
        line.drawMode = SpriteDrawMode.Sliced;
        line.color = Color.white;
        line.size = new Vector2(laneWidth * GameConstants.Editor.LaneCount, GameConstants.Play.JudgeLineThicknessPx * UnitsPerReferencePx);
    }

    private static void Paint(SpriteRenderer[] renderers, Color color)
    {
        if (renderers == null) return;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null) renderer.color = color;
        }
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
            cell.sortingOrder = GameConstants.InGameSort.KeyCell;
            if (keyCellFrames != null && lane < keyCellFrames.Length && keyCellFrames[lane] != null)
            {
                keyCellFrames[lane].size = size;
                keyCellFrames[lane].sortingOrder = GameConstants.InGameSort.KeyCellFrame;
            }
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
    /// 키를 누르는 동안 라인을 빛나게 하고 키 칸을 포인트색으로 채운다. 색은 UITheme 인게임 묶음.
    /// 마름모: 평소 테두리(라인 색) + 가운데 0.375배(라인 색), 누를 때 테두리를 끄고 Key_Diamond 1배 흰색으로 채운다.
    /// 라인 빛남은 개발용 설정(DevPlaySettings.LaneGlowEnabled)이 꺼져 있으면 켜지 않는다 (끄기는 항상 한다). 키 칸은 설정과 상관없이 바꾼다.
    /// 자동 플레이·일시정지도 이 함수 하나로 켜고 끈다 (InGameScene).
    /// </summary>
    public void SetPressed(int lane, bool pressed)
    {
        if (lane < 0 || lane >= GameConstants.Editor.LaneCount) return;

        if (laneGlows != null && lane < laneGlows.Length && laneGlows[lane] != null)
            laneGlows[lane].enabled = pressed && DevPlaySettings.LaneGlowEnabled;

        UITheme theme = UITheme.Current;
        if (keyPressed != null && lane < keyPressed.Length) keyPressed[lane] = pressed;
        if (keyCells != null && lane < keyCells.Length && keyCells[lane] != null)
            keyCells[lane].color = pressed ? theme.keyCellPressed : theme.keyCell;
        if (keyCellFrames != null && lane < keyCellFrames.Length && keyCellFrames[lane] != null)
            keyCellFrames[lane].color = pressed ? theme.keyCellFramePressed : theme.keyCellFrame;
        SpriteRenderer line = Pick(keyDiamondLines, lane);
        if (line != null)
        {
            line.enabled = !pressed;
            line.color = theme.NoteColor(lane);
        }
        SpriteRenderer diamond = Pick(keyDiamonds, lane);
        if (diamond != null)
        {
            diamond.color = pressed ? theme.keyDiamondPressed : theme.NoteColor(lane);
            float scale = KeyDiamondScale(diamond.sprite, pressed ? 1f : GameConstants.Play.KeyDiamondDotScale);
            diamond.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
