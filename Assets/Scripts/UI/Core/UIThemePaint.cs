using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UITheme 색으로 자주 칠하는 것 모음 (메뉴 화면 공용 함수). 화면·항목 스크립트의 Init·선택 표시에서 부른다.
/// - Text: 글자 단계 · Panel: 패널(흰색 58%) · SelectableRow: 목록 항목 선택 표시(포인트색 18% 바탕 + 왼쪽 막대)
/// - Rank: 랭크 글자(S 포인트색, 랭크 글꼴) · Badge: 뱃지 바탕 + 흰 글자
/// </summary>
public static class UIThemePaint
{
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
    /// 목록 항목 선택 표시: 고르면 포인트색 18% 바탕 + 왼쪽 포인트색 막대, 아니면 패널 바탕·막대 없음.
    /// </summary>
    public static void SelectableRow(Graphic background, Graphic bar, bool selected)
    {
        UITheme theme = UITheme.Current;
        if (background != null) background.color = selected ? theme.SelectedBackground : theme.panel;
        if (bar != null)
        {
            bar.color = theme.accent;
            bar.gameObject.SetActive(selected);
        }
    }

    /// <summary>랭크 글자: 글자·색(S 포인트색, 나머지 기본, 기록 없음 흐린 글자)·랭크 글꼴(테마에 있으면)</summary>
    public static void Rank(TMP_Text text, RecordTexts record)
    {
        if (text == null) return;
        UITheme theme = UITheme.Current;
        text.text = record.Rank;
        text.color = theme.GetRank(record);
        if (theme.rankFont != null && text.font != theme.rankFont) text.font = theme.rankFont;
    }

    /// <summary>뱃지: 바탕 색 + 흰 글자</summary>
    public static void Badge(Graphic background, TMP_Text label, Color color)
    {
        if (background != null) background.color = color;
        if (label != null) label.color = UITheme.Current.onDifficultyText;
    }
}
