using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>팝업 버튼 종류 (기획서 3-7 「팝업 공통 톤」 버튼)</summary>
public enum PopupButtonStyle
{
    /// <summary>기본: 흰색 75% + 테두리(구분선 색) + 기본 글자</summary>
    Normal,

    /// <summary>강조: 포인트색 #3E9BE0 채움 + 흰 글자 (테두리 없음)</summary>
    Accent,

    /// <summary>위험(나가기·삭제): 위험 빨강 #C9504A 채움 + 흰 글자 (테두리 없음)</summary>
    Danger
}

/// <summary>
/// UITheme 색으로 자주 칠하는 것 모음 (메뉴 화면 공용 함수). 화면·항목 스크립트의 Init·선택 표시에서 부른다.
/// - Text: 글자 단계 · Panel: 패널(흰색 58%) · SelectableRow: 목록 항목 선택 표시(강조 색 16% 칠 + 왼쪽 막대)
/// - Badge: 뱃지 바탕 + 흰 글자 (랭크·FC는 이미지라 여기서 칠하지 않는다: Img_Rank · Img_FullCombo)
/// - PopupButton: 팝업 버튼 (기본 · 강조 · 위험)
/// - JudgeText: 판정 글자 (단색 / PERFECT 가로 그라데이션)
/// </summary>
public static class UIThemePaint
{
    /// <summary>
    /// 팝업 버튼 바탕·테두리·글자 색 (순수 함수). 테두리는 기본 버튼만 보인다 (나머지는 알파 0).
    /// </summary>
    public static void GetPopupButtonColors(UITheme theme, PopupButtonStyle style, out Color fill, out Color border, out Color label)
    {
        switch (style)
        {
            case PopupButtonStyle.Accent:
                fill = theme.accent;
                border = UITheme.WithAlpha(theme.accent, 0f);
                label = theme.onAccentText;
                break;
            case PopupButtonStyle.Danger:
                fill = theme.danger;
                border = UITheme.WithAlpha(theme.danger, 0f);
                label = theme.onAccentText;
                break;
            default:
                fill = theme.popupButton;
                border = theme.divider;
                label = theme.textPrimary;
                break;
        }
    }

    /// <summary>팝업 버튼을 칠한다 (border·label은 없으면 건너뛴다)</summary>
    public static void PopupButton(Graphic fill, Graphic border, TMP_Text label, PopupButtonStyle style)
    {
        GetPopupButtonColors(UITheme.Current, style, out Color fillColor, out Color borderColor, out Color labelColor);
        if (fill != null) fill.color = fillColor;
        if (border != null)
        {
            border.color = borderColor;
            border.enabled = borderColor.a > 0f;
        }
        if (label != null) label.color = labelColor;
    }

    /// <summary>글자를 단계 색으로</summary>
    public static void Text(TMP_Text text, UITextLevel level)
    {
        if (text != null) text.color = UITheme.Current.GetText(level);
    }

    /// <summary>패널·카드 바탕 (흰색 58%)</summary>
    public static void Panel(Graphic graphic)
    {
        if (graphic != null) graphic.color = UITheme.Current.panel;
    }

    /// <summary>
    /// 곡 선택 목록 항목 선택 표시 (11/25): 바탕은 항상 패널, 고르면 그 위에 강조 색 16% 칠(기록 패널과 같은 칠) + 왼쪽 강조 색 막대.
    /// </summary>
    /// <param name="accent">강조 색 (UITheme.GetChartAccent: 난이도 색 / CUSTOM 보라)</param>
    public static void SelectableRow(Graphic background, Graphic tint, Graphic bar, bool selected, Color accent)
    {
        UITheme theme = UITheme.Current;
        if (background != null) background.color = theme.panel;
        if (tint != null)
        {
            tint.color = UITheme.WithAlpha(accent, theme.difficultyTintChosenAlpha);
            tint.gameObject.SetActive(selected);
        }
        if (bar != null)
        {
            bar.color = accent;
            bar.gameObject.SetActive(selected);
        }
    }

    /// <summary>
    /// 판정 글자 색: 단색은 vertex color(그라데이션 끔), 그라데이션(PERFECT)은 글자 색 흰색 + TMP colorGradient
    /// (왼쪽 위·아래 = Start, 오른쪽 위·아래 = End). 알파는 alpha를 글자 색에 곱한다 (그라데이션과 곱해져 함께 투명해진다).
    /// </summary>
    public static void JudgeText(TMP_Text text, JudgeColor color, float alpha = 1f)
    {
        if (text == null) return;
        if (color.IsGradient)
        {
            text.enableVertexGradient = true;
            text.colorGradient = color.ToVertexGradient();
            text.color = new Color(1f, 1f, 1f, alpha);
            return;
        }

        text.enableVertexGradient = false;
        Color solid = color.Start;
        solid.a *= alpha;
        text.color = solid;
    }

    /// <summary>뱃지: 바탕 색 + 흰 글자</summary>
    public static void Badge(Graphic background, TMP_Text label, Color color)
    {
        if (background != null) background.color = color;
        if (label != null) label.color = UITheme.Current.onDifficultyText;
    }
}
