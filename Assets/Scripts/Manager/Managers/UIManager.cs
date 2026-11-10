using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// UI 화면 등록, 팝업 스택, ESC 처리를 맡는 전역 매니저. Managers_DontDestroy 프리팹의 "UIManager"에 붙인다.
/// - 오버레이 Canvas(Canvas_Overlay)를 가진다. 공용 팝업(Popup_Confirm, Popup_Settings)은 여기 두고 Init에서 등록한다.
/// - 씬 Canvas는 SceneBase가 Init 전에 Register하고, 씬을 떠날 때 Unregister한다. 화면은 타입으로 찾는다 (Get&lt;T&gt;).
/// - ESC(UI/Cancel)는 여기서만 받는다. 처리 순서 (앞 단계에서 처리하면 뒤로 넘기지 않는다):
///   1. 씬 로딩 중이면 무시 2. 열린 TMP_Dropdown이 있으면 무시 3. 포커스된 TMP_InputField가 있으면 입력만 끝낸다
///   4. 열린 팝업이 있으면 맨 위 팝업의 OnCancelRequested() 5. 없으면 현재 씬의 OnBack()
/// - 팝업이 열려 있으면 화면들의 키보드 단축키는 HasOpenPopup을 확인해 동작하지 않는다.
/// </summary>
public class UIManager : MonoSingleton<UIManager>, IGameModule
{
    private const string LogPrefix = "[UIManager]";

    [Tooltip("공용 팝업을 두는 오버레이 Canvas (Canvas_Overlay)")]
    [SerializeField] private Canvas overlayCanvas;
    [Tooltip("입력 에셋의 UI/Cancel (ESC)")]
    [SerializeField] private InputActionReference cancelAction;

    /// <summary>타입별 화면. 같은 타입은 하나만 등록한다</summary>
    private readonly Dictionary<Type, UIBase> uis = new Dictionary<Type, UIBase>();

    /// <summary>Canvas별로 등록한 화면 (해제할 때 쓴다)</summary>
    private readonly Dictionary<Canvas, List<UIBase>> uisByCanvas = new Dictionary<Canvas, List<UIBase>>();

    /// <summary>열린 팝업. 마지막이 맨 위</summary>
    private readonly List<UIPopup> popupStack = new List<UIPopup>();

    private bool isCancelEnabled;

    /// <summary>오버레이 Canvas</summary>
    public Canvas OverlayCanvas => overlayCanvas;

    /// <summary>등록된 화면 수 (확인용)</summary>
    public int RegisteredCount => uis.Count;

    /// <summary>등록된 Canvas 수 (확인용)</summary>
    public int RegisteredCanvasCount => uisByCanvas.Count;

    /// <summary>맨 위 팝업. 없으면 null</summary>
    public UIPopup TopPopup => popupStack.Count > 0 ? popupStack[popupStack.Count - 1] : null;

    /// <summary>열린 팝업이 있으면 true. 화면의 키보드 단축키는 이 값이 true면 동작하지 않는다</summary>
    public bool HasOpenPopup => popupStack.Count > 0;

    /// <summary>
    /// 사전·스택을 비우고 오버레이 Canvas를 등록한 뒤, 공용 팝업을 초기화하고 ESC 입력을 받는다.
    /// </summary>
    public void Init()
    {
        uis.Clear();
        uisByCanvas.Clear();
        popupStack.Clear();

        if (overlayCanvas == null)
        {
            Debug.LogError($"{LogPrefix} overlayCanvas가 연결되지 않았습니다.", this);
        }
        else
        {
            overlayCanvas.sortingOrder = GameConstants.UI.OverlaySortOrder;
            Register(overlayCanvas, UILayer.Overlay);
        }

        Popup_Confirm confirm = Get<Popup_Confirm>();
        if (confirm != null) confirm.Init();
        else Debug.LogError($"{LogPrefix} Popup_Confirm이 오버레이 Canvas 아래에 없습니다.", this);

        Popup_Settings settings = Get<Popup_Settings>();
        if (settings != null) settings.Init();
        else Debug.LogError($"{LogPrefix} Popup_Settings가 오버레이 Canvas 아래에 없습니다.", this);

        EnableCancel();
    }

    protected override void OnDestroy()
    {
        DisableCancel();
        base.OnDestroy();
    }

    // ───── 등록 ─────

    /// <summary>
    /// Canvas 아래 UIBase를 모두 등록한다. 같은 타입이 이미 있으면 경고하고 새 것으로 바꾼다.
    /// </summary>
    /// <param name="layer">이 Canvas의 층 (씬 Canvas는 Scene, 오버레이는 Overlay). 로그·확인용</param>
    public void Register(Canvas canvas, UILayer layer)
    {
        if (canvas == null) return;

        Unregister(canvas);
        List<UIBase> list = new List<UIBase>();
        foreach (UIBase ui in canvas.GetComponentsInChildren<UIBase>(true))
        {
            Type type = ui.GetType();
            if (uis.TryGetValue(type, out UIBase existing) && existing != null && existing != ui)
                Debug.LogWarning($"{LogPrefix} {type.Name}이(가) 이미 등록돼 있어 바꿉니다 ({existing.name} → {ui.name}).", ui);

            uis[type] = ui;
            list.Add(ui);
        }

        uisByCanvas[canvas] = list;
        Debug.Log($"{LogPrefix} 등록: {canvas.name} ({layer}) 화면 {list.Count}개 → 전체 화면 {uis.Count}개, Canvas {uisByCanvas.Count}개", canvas);
    }

    /// <summary>
    /// Canvas로 등록한 화면을 해제한다. 그 Canvas의 팝업은 팝업 스택에서 뺀다.
    /// </summary>
    public void Unregister(Canvas canvas)
    {
        if (canvas == null || !uisByCanvas.TryGetValue(canvas, out List<UIBase> list)) return;

        foreach (UIBase ui in list)
        {
            if (ui == null) continue;

            if (uis.TryGetValue(ui.GetType(), out UIBase registered) && registered == ui)
                uis.Remove(ui.GetType());
            if (ui is UIPopup popup) popupStack.Remove(popup);
        }

        uisByCanvas.Remove(canvas);
        PurgeDestroyed();
    }

    /// <summary>
    /// 씬을 떠날 때 SceneBase가 부른다: 씬 Canvas를 해제하고, 열린 팝업(오버레이 포함)을 닫는다.
    /// </summary>
    public void OnSceneExit(IReadOnlyList<Canvas> sceneCanvases)
    {
        foreach (Canvas canvas in sceneCanvases) Unregister(canvas);
        CloseAllPopups();
        PurgeDestroyed();
        Debug.Log($"{LogPrefix} 씬 해제 뒤 등록: 화면 {uis.Count}개, Canvas {uisByCanvas.Count}개, 팝업 {popupStack.Count}개", this);
    }

    /// <summary>
    /// 타입으로 화면을 찾는다. 없으면 null.
    /// </summary>
    public T Get<T>() where T : UIBase
    {
        if (uis.TryGetValue(typeof(T), out UIBase ui) && ui != null) return (T)ui;
        return null;
    }

    public T Open<T>() where T : UIBase
    {
        T ui = Get<T>();
        if (ui == null) Debug.LogWarning($"{LogPrefix} {typeof(T).Name}이(가) 등록되지 않아 열 수 없습니다.", this);
        else ui.Open();
        return ui;
    }

    public void Close<T>() where T : UIBase
    {
        T ui = Get<T>();
        if (ui != null) ui.Close();
    }

    /// <summary>
    /// 열린 팝업을 위에서부터 모두 닫는다.
    /// </summary>
    public void CloseAllPopups()
    {
        for (int i = popupStack.Count - 1; i >= 0; i--)
        {
            if (i >= popupStack.Count) continue;
            UIPopup popup = popupStack[i];
            if (popup != null) popup.Close();
            else popupStack.RemoveAt(i);
        }
        popupStack.Clear();
    }

    /// <summary>파괴된 화면·Canvas를 사전과 스택에서 지운다</summary>
    private void PurgeDestroyed()
    {
        List<Type> deadTypes = null;
        foreach (KeyValuePair<Type, UIBase> pair in uis)
        {
            if (pair.Value != null) continue;
            (deadTypes ??= new List<Type>()).Add(pair.Key);
        }
        if (deadTypes != null) foreach (Type type in deadTypes) uis.Remove(type);

        List<Canvas> deadCanvases = null;
        foreach (Canvas canvas in uisByCanvas.Keys)
        {
            if (canvas != null) continue;
            (deadCanvases ??= new List<Canvas>()).Add(canvas);
        }
        if (deadCanvases != null) foreach (Canvas canvas in deadCanvases) uisByCanvas.Remove(canvas);

        popupStack.RemoveAll(popup => popup == null);
    }

    // ───── 팝업 스택 ─────

    /// <summary>팝업을 스택 맨 위에 올린다 (UIPopup.Open이 부른다)</summary>
    public void PushPopup(UIPopup popup)
    {
        if (popup == null) return;
        popupStack.Remove(popup);
        popupStack.Add(popup);
    }

    /// <summary>팝업을 스택에서 뺀다 (UIPopup.Close가 부른다)</summary>
    public void RemovePopup(UIPopup popup)
    {
        popupStack.Remove(popup);
    }

    // ───── 확인 팝업 ─────

    /// <summary>
    /// 공용 확인 팝업을 연다. 취소 버튼 이름은 "취소".
    /// </summary>
    /// <returns>열었으면 true. 이미 열려 있으면 false (새 요청은 무시)</returns>
    public bool Confirm(string message, string confirmText, Action onConfirm, Action onCancel = null)
    {
        Popup_Confirm popup = Get<Popup_Confirm>();
        if (popup == null)
        {
            Debug.LogError($"{LogPrefix} Popup_Confirm이 없어 확인 팝업을 열 수 없습니다: \"{message}\"", this);
            return false;
        }

        return popup.Show(message, confirmText, GameConstants.Confirm.CancelLabel, onConfirm, onCancel);
    }

    // ───── 설정 창 ─────

    /// <summary>
    /// 공용 설정 창을 연다 (타이틀·일시정지 메뉴 등 어디서든). 다른 팝업 위에서 열면 닫을 때 그 팝업으로 돌아간다.
    /// </summary>
    /// <returns>열었으면 true. 설정 창이 없거나 이미 열려 있으면 false</returns>
    public bool OpenSettings()
    {
        Popup_Settings popup = Get<Popup_Settings>();
        if (popup == null)
        {
            Debug.LogError($"{LogPrefix} Popup_Settings가 없어 설정 창을 열 수 없습니다.", this);
            return false;
        }
        if (popup.IsOpen) return false;

        popup.Show();
        return true;
    }

    // ───── ESC ─────

    private void EnableCancel()
    {
        if (isCancelEnabled) return;

        if (cancelAction == null)
        {
            Debug.LogWarning($"{LogPrefix} cancelAction이 연결되지 않아 ESC를 받을 수 없습니다.", this);
            return;
        }

        cancelAction.action.performed += OnCancel;
        cancelAction.action.Enable();
        isCancelEnabled = true;
    }

    /// <summary>
    /// 액션은 EventSystem과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    private void DisableCancel()
    {
        if (!isCancelEnabled) return;

        cancelAction.action.performed -= OnCancel;
        isCancelEnabled = false;
    }

    /// <summary>
    /// ESC 처리. 순서는 클래스 설명 참고.
    /// </summary>
    private void OnCancel(InputAction.CallbackContext context)
    {
        GameSceneManager scene = GameManager.Scene;
        if (scene == null || scene.IsLoading || scene.Current == null) return;

        if (IsDropdownExpanded()) return;

        TMP_InputField editingInput = GetFocusedInput();
        if (editingInput != null)
        {
            // 입력한 글자는 그대로 두고 편집만 끝낸다 (선택은 입력창에 남아 방향키로 이어서 이동 가능)
            editingInput.DeactivateInputField();
            return;
        }

        UIPopup top = TopPopup;
        if (top != null)
        {
            top.OnCancelRequested();
            return;
        }

        scene.Current.OnBack();
    }

    private static bool IsDropdownExpanded()
    {
        foreach (TMP_Dropdown dropdown in FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None))
        {
            if (dropdown.IsExpanded) return true;
        }
        return false;
    }

    private static TMP_InputField GetFocusedInput()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null) return null;

        TMP_InputField input = selected.GetComponent<TMP_InputField>();
        return input != null && input.isFocused ? input : null;
    }
}
