using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// 키 안내 줄(KeyGuideBar)의 키 하나 (키 모양 박스 하나).
/// - 액션: 입력 에셋 바인딩에서 키 이름을 읽는다 (키를 바꾸면 안내도 바뀐다). 키보드 바인딩이 없으면 fallback 글자를 쓴다.
/// - 고정 글자: 방향키("↑↓")나 UI 맵의 Submit·Cancel처럼 한 바인딩으로 이름을 읽기 어려운 키에 쓴다.
/// </summary>
public class KeyGuideKey
{
    /// <summary>키 이름을 읽을 액션 (없으면 null)</summary>
    public InputActionReference Action { get; private set; }

    /// <summary>고정 글자, 또는 액션에서 이름을 못 읽었을 때 쓸 글자</summary>
    public string Text { get; private set; }

    /// <summary>입력 에셋 액션의 첫 키보드 바인딩 이름을 쓴다 (예: Result/Retry → "F5")</summary>
    public static KeyGuideKey FromAction(InputActionReference action, string fallback = null)
    {
        return new KeyGuideKey { Action = action, Text = fallback };
    }

    /// <summary>고정 글자를 쓴다 (예: "↑↓")</summary>
    public static KeyGuideKey Fixed(string text)
    {
        return new KeyGuideKey { Text = text };
    }
}

/// <summary>
/// 키 안내 줄(KeyGuideBar)의 항목 하나: 키 모양 박스 1개 이상 + 짧은 이름 (예: [1][2] 배속).
/// </summary>
public class KeyGuideEntry
{
    /// <summary>이름을 나중에 바꿀 때 찾는 ID (KeyGuideBar.SetLabel). 바꿀 일이 없으면 null</summary>
    public string Id { get; private set; }

    /// <summary>키 뒤에 붙는 짧은 이름 (GameConstants.KeyGuide)</summary>
    public string Label { get; private set; }

    /// <summary>키 모양 박스들 (왼쪽부터)</summary>
    public IReadOnlyList<KeyGuideKey> Keys { get; private set; }

    public KeyGuideEntry(string label, params KeyGuideKey[] keys)
    {
        Label = label;
        Keys = keys ?? new KeyGuideKey[0];
    }

    /// <summary>ID를 붙인다 (SetLabel로 이름을 바꿀 항목)</summary>
    public KeyGuideEntry WithId(string id)
    {
        Id = id;
        return this;
    }
}
