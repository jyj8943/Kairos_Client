using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 에디터 실행 취소 · 다시 실행 실제 씬 확인 (EnterPlayMode + Bootstrapper, Critical Velocity 새 채보).
/// - 실제 키 이벤트로 Ctrl+Z · Ctrl+Y · Ctrl+Shift+Z (Ctrl+Shift+Z가 실행 취소로 같이 잡히지 않는지)
/// - 팝업이 열려 있을 때 · 입력창에 글자를 쓰는 중 · 롱노트 드래그 중에는 무시
/// - 테스트 플레이 → 에디터로 돌아온 뒤에도 기록 · 도구가 남아 있음
/// - 도구 키 1 · 2 (3은 없음) · 클릭 · ↑↓, 왼쪽 패널 도구 두 줄 배치와 사용법 한 줄
/// </summary>
public class EditorUndoPlayTests
{
    private const string Song = TestCharts.OfficialSongId;
    private const int T = GameConstants.Editor.TicksPerBeat;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
    private InputSettings.BackgroundBehavior savedBackground;

    private EditorScene editorScene;
    private ChartEditSession session;
    private EditorNotePlacer placer;
    private Canvas_Editor canvas;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        yield return OpenEditor(new EditorEntryData { songId = Song, chartName = "실행 취소 확인", level = 1 });
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return new ExitPlayMode();
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        InputSystem.settings.backgroundBehavior = savedBackground;
    }

    private IEnumerator OpenEditor(EditorEntryData entry)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저·씬 로딩 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(GameConstants.Scenes.Editor, entry));
        yield return WaitEditor();
    }

    private IEnumerator WaitEditor()
    {
        float deadline = TestSpeed.Deadline(20f);
        yield return null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "에디터 시작 대기 시간 초과");
            editorScene = Object.FindFirstObjectByType<EditorScene>();
            if (editorScene != null && !GameManager.Scene.IsLoading && Get<bool>(editorScene, "isReady")) break;
            yield return null;
        }
        session = Object.FindFirstObjectByType<ChartEditSession>();
        placer = Object.FindFirstObjectByType<EditorNotePlacer>();
        canvas = Object.FindFirstObjectByType<Canvas_Editor>();
    }

    private static TValue Get<TValue>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        Assert.IsNotNull(info, $"필드 {field}가 없습니다");
        return (TValue)info.GetValue(target);
    }

    private static void Call(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method, Private);
        Assert.IsNotNull(info, $"함수 {method}가 없습니다");
        info.Invoke(target, null);
    }

    /// <summary>Ctrl(+Shift)을 먼저 누르고 다음 이벤트에 글자 키를 더해 누른 뒤 모두 뗀다 (실제 키 이벤트).</summary>
    private static IEnumerator Shortcut(Key key, bool shift = false)
    {
        Keyboard keyboard = Keyboard.current;
        Assert.IsNotNull(keyboard, "키보드 장치가 없습니다");
        KeyboardState modifiers = shift ? new KeyboardState(Key.LeftCtrl, Key.LeftShift) : new KeyboardState(Key.LeftCtrl);
        InputSystem.QueueStateEvent(keyboard, modifiers);
        InputSystem.Update();
        yield return null;
        KeyboardState pressed = shift ? new KeyboardState(Key.LeftCtrl, Key.LeftShift, key) : new KeyboardState(Key.LeftCtrl, key);
        InputSystem.QueueStateEvent(keyboard, pressed);
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private string StatusText => Get<TMP_Text>(canvas, "saveStatusText").text;

    /// <summary>U1: Ctrl+Z · Ctrl+Y · Ctrl+Shift+Z, 상태 표시, 수정 여부, 조작 안내 문구</summary>
    [UnityTest]
    public IEnumerator U1_Shortcuts()
    {
        Assert.AreEqual(9, canvas.KeyGuide.ItemCount, "맨 아래 키 안내 줄 9개 (조작 안내를 옮김, 11/19)");
        Assert.AreEqual(GameConstants.Editor.GuidePlay, canvas.KeyGuide.GetLabel(GameConstants.Editor.GuidePlayId), "정지 중 Space = 재생");

        Assert.IsTrue(session.AddNote(0, 8 * T));
        Assert.IsTrue(session.AddNote(1, 9 * T));
        Assert.IsTrue(session.AddLongNote(2, 10 * T, 12 * T, out _));
        Assert.IsTrue(session.IsDirty);

        yield return Shortcut(Key.Z);
        Assert.AreEqual(2, session.Data.Notes.Count, "Ctrl+Z: 롱노트가 먼저 사라짐");
        Assert.AreEqual(string.Format(GameConstants.Editor.UndoStatusFormat, GameConstants.Editor.HistoryAddLongNote), StatusText);

        yield return Shortcut(Key.Z);
        yield return Shortcut(Key.Z);
        Assert.AreEqual(0, session.Data.Notes.Count, "Ctrl+Z 연타: 순서대로 사라짐");
        Assert.IsFalse(session.IsDirty, "시작 상태(빈 새 채보)로 돌아옴");

        yield return Shortcut(Key.Y);
        Assert.AreEqual(1, session.Data.Notes.Count, "Ctrl+Y: 다시 생김");
        Assert.AreEqual(string.Format(GameConstants.Editor.RedoStatusFormat, GameConstants.Editor.HistoryAddShortNote), StatusText);

        int undoBefore = session.UndoCount;
        yield return Shortcut(Key.Z, true);
        Assert.AreEqual(2, session.Data.Notes.Count, "Ctrl+Shift+Z: 다시 실행만 (실행 취소가 같이 잡히면 1개)");
        Assert.AreEqual(undoBefore + 1, session.UndoCount);
        Assert.IsTrue(session.IsDirty);
        Debug.Log($"[EditorUndoPlayTest] U1: Ctrl+Z ×3 → 노트 0, Ctrl+Y → 1, Ctrl+Shift+Z → 2, 상태 \"{StatusText}\"");
    }

    /// <summary>U2: 팝업이 열려 있을 때 · 입력창에 글자를 쓰는 중 · 롱노트 드래그 중에는 무시</summary>
    [UnityTest]
    public IEnumerator U2_BlockedCases()
    {
        Assert.IsTrue(session.AddNote(0, 8 * T));

        // 팝업
        Assert.IsTrue(GameManager.UI.Confirm("확인", "확인", null));
        yield return null;
        Assert.IsTrue(GameManager.UI.HasOpenPopup);
        yield return Shortcut(Key.Z);
        Assert.AreEqual(1, session.Data.Notes.Count, "팝업이 열려 있으면 무시");
        GameManager.UI.CloseAllPopups();
        yield return null;
        Assert.IsFalse(GameManager.UI.HasOpenPopup);

        // 입력창 (F1 곡 조정 패널의 BPM 칸을 잠시 열어 포커스를 준다. 에디터 화면에 남은 입력 칸은 이것뿐이다)
        Canvas_SongTuning tuning = Object.FindFirstObjectByType<Canvas_SongTuning>(FindObjectsInactive.Include);
        Assert.IsNotNull(tuning, "F1 곡 조정 패널");
        GameObject tuningPanel = Get<GameObject>(tuning, "panel");
        tuningPanel.SetActive(true);
        TMP_InputField input = Get<TMP_InputField>(tuning, "bpmInput");
        EventSystem.current.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
        yield return null;
        yield return null;
        Assert.IsTrue(input.isFocused, "입력창 포커스");
        yield return Shortcut(Key.Z);
        Assert.AreEqual(1, session.Data.Notes.Count, "입력창에 쓰는 중이면 무시");
        input.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);
        tuningPanel.SetActive(false);
        yield return null;

        // 롱노트 드래그 중
        SetPressing(true);
        Assert.IsTrue(placer.IsPressing);
        yield return Shortcut(Key.Z);
        Assert.AreEqual(1, session.Data.Notes.Count, "롱노트 드래그 중이면 무시");
        SetPressing(false);

        yield return Shortcut(Key.Z);
        Assert.AreEqual(0, session.Data.Notes.Count, "막는 조건이 없으면 실행 취소");
        Debug.Log("[EditorUndoPlayTest] U2: 팝업 · 입력창 · 드래그 중 무시, 그 뒤 Ctrl+Z 동작");
    }

    private void SetPressing(bool pressing)
    {
        typeof(EditorNotePlacer).GetField("isPressing", Private).SetValue(placer, pressing);
    }

    /// <summary>U3: 테스트 플레이 → 에디터로 돌아온 뒤에도 기록이 남아 Ctrl+Z가 동작하고, 저장 시점(시작 상태)도 이어진다</summary>
    [UnityTest]
    public IEnumerator U3_TestPlayRoundTripKeepsHistory()
    {
        Assert.IsTrue(session.AddNote(0, 8 * T));
        Assert.IsTrue(session.AddNote(1, 9 * T));
        Assert.IsTrue(session.AddNote(2, 10 * T));
        session.Undo();
        Assert.AreEqual(2, session.UndoCount);
        Assert.AreEqual(1, session.RedoCount);

        Call(editorScene, "OnTestPlayRequested");
        float deadline = TestSpeed.Deadline(20f);
        InGameScene inGame = null;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            inGame = Object.FindFirstObjectByType<InGameScene>();
            if (inGame != null && !GameManager.Scene.IsLoading && Get<bool>(inGame, "isPlaying")) break;
            yield return null;
        }

        // ESC → 일시정지 → 나가기와 같은 경로로 에디터에 돌아간다
        Call(inGame, "ExitToReturnTarget");
        yield return WaitEditor();

        Assert.AreEqual(EditorTool.Note, canvas.CurrentTool, "돌아와도 노트 도구 그대로");
        Assert.AreEqual(2, session.Data.Notes.Count);
        Assert.AreEqual(2, session.UndoCount, "돌아와도 실행 취소 기록이 남아 있음");
        Assert.AreEqual(1, session.RedoCount, "다시 실행 기록도 남아 있음");
        Assert.IsTrue(session.IsDirty);

        yield return Shortcut(Key.Y);
        Assert.AreEqual(3, session.Data.Notes.Count, "되돌렸던 노트 다시 실행");
        yield return Shortcut(Key.Z);
        yield return Shortcut(Key.Z);
        yield return Shortcut(Key.Z);
        Assert.AreEqual(0, session.Data.Notes.Count);
        Assert.IsFalse(session.IsDirty, "시작 상태(빈 새 채보)로 돌아오면 저장할 것이 없음");
        Debug.Log("[EditorUndoPlayTest] U3: 테스트 플레이 왕복 뒤 실행 취소 2 · 다시 실행 1 유지, Ctrl+Y·Ctrl+Z 동작");
    }

    /// <summary>
    /// U4: 사용자와 같은 경로. 그리드 클릭으로 노트 → Btn_TestPlay 클릭 → 인게임에서 ESC 키 → 일시정지 "나가기" 버튼 →
    /// 에디터에서 Ctrl+Z
    /// </summary>
    [UnityTest]
    public IEnumerator U4_RealPathTestPlayThenUndo()
    {
        Assert.IsTrue(session.AddNote(0, 8 * T));
        Assert.IsTrue(session.AddNote(1, 9 * T));

        Get<UnityEngine.UI.Button>(canvas, "testPlayButton").onClick.Invoke();
        float deadline = TestSpeed.Deadline(20f);
        InGameScene inGame;
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            inGame = Object.FindFirstObjectByType<InGameScene>();
            if (inGame != null && !GameManager.Scene.IsLoading && Get<bool>(inGame, "isPlaying")) break;
            yield return null;
        }

        yield return Tap(Key.Escape);
        Assert.IsTrue(Get<bool>(inGame, "isPaused"), "ESC로 일시정지");
        Popup_Pause pause = Object.FindFirstObjectByType<Popup_Pause>();
        Assert.IsNotNull(pause);
        Get<UnityEngine.UI.Button>(pause, "exitButton").onClick.Invoke();
        yield return WaitEditor();
        yield return null;

        string state = $"노트 {session.Data.Notes.Count}, 실행 취소 {session.UndoCount}, 팝업 {GameManager.UI.HasOpenPopup}, " +
                       $"선택 {(EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "없음")}, " +
                       $"Undo 액션 켜짐 {Get<UnityEngine.InputSystem.InputActionReference>(canvas, "undoAction").action.enabled}, " +
                       $"입력 구독 {Get<bool>(canvas, "isInputEnabled")}";
        Debug.Log($"[EditorUndoPlayTest] U4 복귀 직후: {state}");
        Assert.AreEqual(2, session.UndoCount, state);

        yield return Shortcut(Key.Z);
        Assert.AreEqual(1, session.Data.Notes.Count, "복귀 뒤 Ctrl+Z: " + state);
    }

    /// <summary>
    /// U5: 키 1 → 노트, 2 → BPM 구간, 3 → 아무 일 없음. 마우스 클릭으로도 바뀐다. 키 칩 · 키 안내 줄 글자.
    /// 도구는 노트 · BPM 구간 두 줄뿐이고(롱노트 버튼 삭제) ↑↓로 바로 이어진다. 두 줄 간격 10px, 왼쪽 패널 요소는 모두 패널 안, 사용법은 한 줄.
    /// </summary>
    [UnityTest]
    public IEnumerator U5_ToolKeysAndLeftPanel()
    {
        System.Text.StringBuilder log = new System.Text.StringBuilder("[EditorUndoPlayTest] U5");
        Assert.AreEqual(EditorTool.Note, canvas.CurrentTool, "처음은 노트 도구");
        (Key key, EditorTool expected)[] steps =
        {
            (Key.Digit3, EditorTool.Note), (Key.Digit2, EditorTool.BpmSection), (Key.Digit3, EditorTool.BpmSection),
            (Key.Digit1, EditorTool.Note), (Key.Digit1, EditorTool.Note)
        };
        foreach ((Key key, EditorTool expected) in steps)
        {
            yield return Tap(key);
            log.Append($" | {key} → {canvas.CurrentTool}");
            Assert.AreEqual(expected, canvas.CurrentTool, key.ToString());
        }

        Assert.AreEqual("노트", Get<TMP_Text>(canvas, "shortNoteLabel").text);
        Assert.AreEqual("1", Get<TMP_Text>(canvas, "shortNoteKeyText").text, "키 칩은 바인딩에서");
        Assert.AreEqual("2", Get<TMP_Text>(canvas, "bpmSectionKeyText").text);
        CollectionAssert.Contains(canvas.KeyGuide.Describe(), "1 2 도구");

        // 도구 두 줄: 선택 이동 · 클릭 · 간격
        RectTransform panel = (RectTransform)canvas.transform.Find("LeftTools");
        Toggle note = Get<Toggle>(canvas, "shortNoteToggle");
        Toggle bpm = Get<Toggle>(canvas, "bpmSectionToggle");
        ToggleGroup group = panel.GetComponent<ToggleGroup>();
        var tools = new System.Collections.Generic.List<Toggle>(panel.GetComponentsInChildren<Toggle>(true)).FindAll(t => t.group == group);
        CollectionAssert.AreEqual(new[] { note, bpm }, tools, "도구는 노트 · BPM 구간 두 줄");
        Assert.AreSame(bpm, note.FindSelectableOnDown(), "노트 ↓ BPM 구간");
        Assert.AreSame(note, bpm.FindSelectableOnUp(), "BPM 구간 ↑ 노트");
        foreach ((Toggle toggle, EditorTool expected) in new[] { (bpm, EditorTool.BpmSection), (note, EditorTool.Note) })
        {
            ClickCenter((RectTransform)toggle.transform);
            log.Append($" | {toggle.name} 클릭 → {canvas.CurrentTool}");
            Assert.AreEqual(expected, canvas.CurrentTool, toggle.name + " 클릭");
        }
        float gap = BottomIn(panel, (RectTransform)note.transform) - TopIn(panel, (RectTransform)bpm.transform);
        log.Append($" | 노트 ~ BPM 구간 간격 {gap:0.0}px");
        Assert.AreEqual(10f, gap, 0.01f, "도구 줄 간격");

        // 왼쪽 패널 요소는 모두 패널 안
        foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == panel) continue;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = panel.InverseTransformPoint(corners[0]);
            Vector2 max = panel.InverseTransformPoint(corners[2]);
            Assert.IsTrue(min.x >= panel.rect.xMin - 0.5f && max.x <= panel.rect.xMax + 0.5f && min.y >= panel.rect.yMin - 0.5f && max.y <= panel.rect.yMax + 0.5f, rect.name + " 패널 안");
        }

        // 사용법 한 줄
        TMP_Text hint = Get<TMP_Text>(canvas, "toolHintText");
        foreach (EditorTool tool in new[] { EditorTool.BpmSection, EditorTool.Note })
        {
            canvas.SetTool(tool);
            hint.ForceMeshUpdate();
            log.Append($" | {tool} 문구 {hint.textInfo.lineCount}줄 {hint.preferredHeight:0.0}/{hint.rectTransform.rect.height:0.0} 넘침 {hint.isTextOverflowing}");
            Assert.AreEqual(1, hint.textInfo.lineCount, tool + " 안내 문구 한 줄");
            Assert.IsFalse(hint.isTextOverflowing, tool + " 안내 문구 넘침");
        }
        Assert.AreEqual("클릭: 놓기 · 끌기: 롱노트 · 우클릭: 지우기", canvas.ToolHint);
        Debug.Log(log.ToString());
    }

    /// <summary>화면에서 rect 가운데에 있는 것을 레이캐스트로 찾아 클릭한다 (가려져 있으면 그것이 클릭된다)</summary>
    private void ClickCenter(RectTransform rect)
    {
        Canvas root = canvas.GetComponentInParent<Canvas>().rootCanvas;
        Camera eventCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center))
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.Greater(hits.Count, 0, rect.name + " 자리에 걸리는 것이 없음");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static float BottomIn(RectTransform panel, RectTransform rect) => panel.InverseTransformPoint(rect.TransformPoint(rect.rect.min)).y;
    private static float TopIn(RectTransform panel, RectTransform rect) => panel.InverseTransformPoint(rect.TransformPoint(rect.rect.max)).y;

    private static IEnumerator Tap(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }
}
