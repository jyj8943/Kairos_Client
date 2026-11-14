using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 아래쪽 키 안내 줄 (기획서 3-9). 타이틀·곡 선택·일시정지·결과가 같은 프리팹(Prefabs/Common/KeyGuideBar)을 쓴다.
/// - 오브젝트 구조: KeyGuideBar(HorizontalLayoutGroup) → Item(꺼 둔 틀) → Img_KeyCap(키 모양 박스) → Txt_KeyName, 그리고 Txt_KeyLabel(짧은 이름).
/// - 화면 스크립트가 Init(항목 목록)을 부르면 틀을 복제해 항목을 만든다. 키 이름은 그때 한 번 바인딩에서 읽는다 (GetKeyText).
/// - 키는 입력 에셋 한 곳에만 정의한다. 안내 문구에 키 이름을 직접 쓰지 않는다 (고정 글자는 방향키·UI 맵 키만).
/// </summary>
public class KeyGuideBar : MonoBehaviour
{
    private const string LogPrefix = "[KeyGuideBar]";

    /// <summary>키 이름을 읽을 바인딩 그룹 (입력 에셋의 control scheme)</summary>
    private const string KeyboardGroup = "Keyboard&Mouse";

    [Tooltip("복제할 항목 틀 (Item). 꺼 둔다. 자식: Img_KeyCap(자식 Txt_KeyName), Txt_KeyLabel")]
    [SerializeField] private RectTransform itemTemplate;

    private class ItemView
    {
        public string id;
        public GameObject root;
        public TMP_Text label;
        public readonly List<TMP_Text> keyNames = new List<TMP_Text>();
    }

    private readonly List<ItemView> items = new List<ItemView>();

    /// <summary>만든 항목 수 (확인용)</summary>
    public int ItemCount => items.Count;

    /// <summary>
    /// 항목을 새로 만든다 (이전 항목은 지운다). 키 이름은 이때 한 번 읽는다.
    /// </summary>
    public void Init(IReadOnlyList<KeyGuideEntry> entries)
    {
        Clear();
        if (itemTemplate == null)
        {
            Debug.LogError($"{LogPrefix} itemTemplate이 연결되지 않았습니다.", this);
            return;
        }
        itemTemplate.gameObject.SetActive(false);
        if (entries == null) return;

        Transform keyCapTemplate = itemTemplate.Find(GameConstants.KeyGuide.KeyCapName);
        Transform labelTemplate = itemTemplate.Find(GameConstants.KeyGuide.KeyLabelName);
        if (keyCapTemplate == null || labelTemplate == null)
        {
            Debug.LogError($"{LogPrefix} 항목 틀에 {GameConstants.KeyGuide.KeyCapName} 또는 {GameConstants.KeyGuide.KeyLabelName}이 없습니다.", this);
            return;
        }

        foreach (KeyGuideEntry entry in entries)
        {
            if (entry == null) continue;
            items.Add(CreateItem(entry));
        }
    }

    /// <summary>
    /// ID가 같은 항목의 이름을 바꾼다 (예: 곡 선택 탭에 따라 "난이도" ↔ "채보"). 없으면 false.
    /// </summary>
    public bool SetLabel(string id, string label)
    {
        foreach (ItemView item in items)
        {
            if (item.id != id) continue;
            item.label.text = label;
            return true;
        }
        return false;
    }

    /// <summary>ID가 같은 항목의 지금 이름 (확인용, 없으면 null)</summary>
    public string GetLabel(string id)
    {
        foreach (ItemView item in items)
        {
            if (item.id == id) return item.label.text;
        }
        return null;
    }

    /// <summary>만든 항목의 "키 이름들 + 이름"을 한 줄씩 (확인 로그·테스트용, 예: "1 2 배속")</summary>
    public List<string> Describe()
    {
        List<string> lines = new List<string>();
        foreach (ItemView item in items)
        {
            List<string> parts = new List<string>();
            foreach (TMP_Text key in item.keyNames) parts.Add(key.text);
            parts.Add(item.label.text);
            lines.Add(string.Join(" ", parts));
        }
        return lines;
    }

    /// <summary>
    /// 액션의 첫 키보드 바인딩 이름 (예: &lt;Keyboard&gt;/f5 → "F5", &lt;Keyboard&gt;/enter → "Enter").
    /// 액션이 없거나 키보드 바인딩이 없으면(UI 맵의 */{Submit} 같은 용도 바인딩 포함) fallback.
    /// </summary>
    public static string GetKeyText(InputAction action, string fallback)
    {
        if (action != null)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                if (string.IsNullOrEmpty(binding.effectivePath) || !binding.effectivePath.StartsWith("<Keyboard>")) continue;
                if (!string.IsNullOrEmpty(binding.groups) && !binding.groups.Contains(KeyboardGroup)) continue;

                string text = action.GetBindingDisplayString(i);
                if (!string.IsNullOrEmpty(text)) return text;
            }
        }
        return fallback ?? string.Empty;
    }

    /// <summary>키 하나의 글자: 액션이 있으면 바인딩 이름, 없으면 고정 글자</summary>
    public static string GetKeyText(KeyGuideKey key)
    {
        if (key == null) return string.Empty;
        InputAction action = key.Action != null ? key.Action.action : null;
        return action != null ? GetKeyText(action, key.Text) : (key.Text ?? string.Empty);
    }

    private ItemView CreateItem(KeyGuideEntry entry)
    {
        GameObject root = Instantiate(itemTemplate.gameObject, transform);
        root.name = itemTemplate.name;
        root.SetActive(true);

        ItemView view = new ItemView { id = entry.Id, root = root };
        Transform keyCap = root.transform.Find(GameConstants.KeyGuide.KeyCapName);
        view.label = root.transform.Find(GameConstants.KeyGuide.KeyLabelName).GetComponent<TMP_Text>();
        view.label.text = entry.Label ?? string.Empty;

        // 키 모양 박스: 틀에 있는 하나를 첫 키로 쓰고, 키가 더 있으면 복제해 이름 앞에 둔다
        for (int i = 0; i < entry.Keys.Count; i++)
        {
            Transform cap = keyCap;
            if (i > 0)
            {
                cap = Instantiate(keyCap.gameObject, root.transform).transform;
                cap.name = GameConstants.KeyGuide.KeyCapName;
                cap.SetSiblingIndex(view.label.transform.GetSiblingIndex());
            }
            TMP_Text keyName = cap.GetComponentInChildren<TMP_Text>(true);
            keyName.text = GetKeyText(entry.Keys[i]);
            view.keyNames.Add(keyName);
        }
        if (entry.Keys.Count == 0) keyCap.gameObject.SetActive(false);
        return view;
    }

    private void Clear()
    {
        foreach (ItemView item in items)
        {
            if (item.root == null) continue;
            if (Application.isPlaying) Destroy(item.root);
            else DestroyImmediate(item.root);
        }
        items.Clear();
    }
}
