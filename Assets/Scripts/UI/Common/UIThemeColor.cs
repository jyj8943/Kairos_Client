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
    PanelBorder,
    StatBox,
    StatBoxBorder
}

/// <summary>
/// 바뀌지 않는 그래픽(패널·라벨·선·키 모양 등)에 붙여 역할만 정해 두면, 화면 Init에서 UITheme 색으로 칠한다 (메뉴 화면 공용 컴포넌트).
/// - 씬·프리팹에는 색 대신 역할(enum)만 남긴다. 화면 스크립트가 Init에서 UIThemeColor.ApplyAll(루트)을 부른다.
/// - 선택에 따라 바뀌는 색(탭·목록 항목·난이도 카드·랭크)은 각 스크립트가 UIThemePaint로 칠한다.
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
        if (graphic != null) graphic.color = GetColor(UITheme.Current, role);
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

    /// <summary>역할 → 테마 색</summary>
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
            case UIColorRole.PanelBorder: return theme.panelBorder;
            case UIColorRole.StatBox: return theme.statBox;
            case UIColorRole.StatBoxBorder: return theme.statBoxBorder;
            default: return Color.magenta;
        }
    }
}
