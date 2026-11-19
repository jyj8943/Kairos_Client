using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UIThemeColor가 칠하는 역할 (UITheme의 어느 색을 쓸지)</summary>
public enum UIColorRole
{
    BackgroundBase,
    Panel,
    BottomBar,
    Divider,
    Accent,
    OnAccent,
    TextPrimary,
    TextSecondary,
    TextDescription,
    TextMuted,
    KeyCapFrame,
    KeyCapFill,
    DifficultyCustom,
    EmptyCover,
    /// <summary>인게임 왼쪽 패널 1px 테두리 (11/19 이름 바꿈, 직렬화 값 그대로)</summary>
    InGamePanelBorder,
    /// <summary>인게임 RATE · SCORE 박스 바탕 (11/19 이름 바꿈)</summary>
    InGameStatBox,
    /// <summary>인게임 RATE · SCORE 박스 테두리 (11/19 이름 바꿈)</summary>
    InGameStatBoxBorder,
    RankEmptyFill,
    RankEmptyDash,
    PopupDim,
    PopupCountdownDim,
    PopupPanel,
    PopupPanelBorder,
    PopupShadow,
    PopupButton,
    PopupCard,
    Danger,

    // 아래는 11/19 추가 (직렬화 값이 바뀌지 않게 뒤에 붙인다)
    /// <summary>인게임 왼쪽 패널 바탕 (라벤더 불투명)</summary>
    InGamePanel,
    InGameBrightnessBarOff,
    /// <summary>메뉴용 PERFECT (TMP 글자면 금 → 분홍 가로 그라데이션)</summary>
    JudgePerfect
    // 11/19: 인게임용 PERFECT 역할(InGameJudgePerfect)은 기어 판정 글자가 이미지가 되어 지웠다 (쓰던 씬·프리팹 없음)
}

/// <summary>
/// 바뀌지 않는 그래픽(패널·라벨·선·키 모양 등)에 붙여 역할만 정해 두면, 화면 Init에서 UITheme 색으로 칠한다 (메뉴 화면 공용 컴포넌트).
/// - 씬·프리팹에는 색 대신 역할(enum)만 남긴다. 화면 스크립트가 Init에서 UIThemeColor.ApplyAll(루트)을 부른다.
/// - 판정 PERFECT 역할(JudgePerfect)은 TMP 글자면 가로 그라데이션(UIThemePaint.JudgeText)으로, 다른 그래픽이면 왼쪽 색으로 칠한다.
/// - 선택에 따라 바뀌는 색(탭·목록 항목·난이도 카드)은 각 스크립트가 UIThemePaint로 칠한다. 랭크·FC는 이미지라 색을 칠하지 않는다.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIThemeColor : MonoBehaviour
{
    [SerializeField] private UIColorRole role;

    /// <summary>역할 (확인용)</summary>
    public UIColorRole Role => role;

    /// <summary>역할을 정하고 바로 칠한다 (만들 때·코드에서 쓸 때)</summary>
    public void SetRole(UIColorRole newRole)
    {
        role = newRole;
        Apply();
    }

    /// <summary>테마 색으로 칠한다</summary>
    public void Apply()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null) return;

        if (TryGetJudgeColor(UITheme.Current, role, out JudgeColor judge) && graphic is TMP_Text text)
        {
            UIThemePaint.JudgeText(text, judge);
            return;
        }
        graphic.color = GetColor(UITheme.Current, role);
    }

    /// <summary>
    /// root 아래(꺼진 오브젝트 포함) 모든 UIThemeColor를 칠한다. 화면 Init 처음에 부른다.
    /// </summary>
    /// <returns>칠한 개수</returns>
    public static int ApplyAll(GameObject root)
    {
        if (root == null) return 0;
        UIThemeColor[] all = root.GetComponentsInChildren<UIThemeColor>(true);
        foreach (UIThemeColor item in all) item.Apply();
        return all.Length;
    }

    /// <summary>그라데이션을 칠하는 판정 역할이면 그 색 한 벌</summary>
    public static bool TryGetJudgeColor(UITheme theme, UIColorRole role, out JudgeColor color)
    {
        switch (role)
        {
            case UIColorRole.JudgePerfect: color = theme.GetMenuJudge(JudgeType.Perfect); return true;
            default: color = default; return false;
        }
    }

    /// <summary>역할 → 테마 색 (그라데이션 역할은 왼쪽 색)</summary>
    public static Color GetColor(UITheme theme, UIColorRole role)
    {
        switch (role)
        {
            case UIColorRole.BackgroundBase: return theme.backgroundBase;
            case UIColorRole.Panel: return theme.panel;
            case UIColorRole.BottomBar: return theme.bottomBar;
            case UIColorRole.Divider: return theme.divider;
            case UIColorRole.Accent: return theme.accent;
            case UIColorRole.OnAccent: return theme.onAccentText;
            case UIColorRole.TextPrimary: return theme.textPrimary;
            case UIColorRole.TextSecondary: return theme.textSecondary;
            case UIColorRole.TextDescription: return theme.textDescription;
            case UIColorRole.TextMuted: return theme.textMuted;
            case UIColorRole.KeyCapFrame: return UITheme.WithAlpha(theme.textMuted, 0.7f);
            case UIColorRole.KeyCapFill: return UITheme.WithAlpha(Color.white, 0.75f);
            case UIColorRole.DifficultyCustom: return theme.difficultyCustom;
            case UIColorRole.EmptyCover: return theme.emptyCover;
            case UIColorRole.InGamePanelBorder: return theme.inGamePanelBorder;
            case UIColorRole.InGameStatBox: return theme.inGameStatBox;
            case UIColorRole.InGameStatBoxBorder: return theme.inGameStatBoxBorder;
            case UIColorRole.RankEmptyFill: return theme.rankEmptyFill;
            case UIColorRole.RankEmptyDash: return theme.rankEmptyDash;
            case UIColorRole.PopupDim: return theme.popupDim;
            case UIColorRole.PopupCountdownDim: return theme.popupCountdownDim;
            case UIColorRole.PopupPanel: return theme.popupPanel;
            case UIColorRole.PopupPanelBorder: return theme.popupPanelBorder;
            case UIColorRole.PopupShadow: return theme.popupShadow;
            case UIColorRole.PopupButton: return theme.popupButton;
            case UIColorRole.PopupCard: return theme.popupCard;
            case UIColorRole.Danger: return theme.danger;
            case UIColorRole.InGamePanel: return theme.inGamePanel;
            case UIColorRole.InGameBrightnessBarOff: return theme.inGameBrightnessBarOff;
            case UIColorRole.JudgePerfect: return theme.judgePerfectStart;
            default: return Color.magenta;
        }
    }
}
