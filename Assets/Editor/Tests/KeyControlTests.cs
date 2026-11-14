using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조작 키 정리 (기획서 3-9) EditMode 테스트: 입력 에셋 바인딩, KeyGuideBar 키 이름, 인게임 F5 허용 규칙.
/// 화면 흐름(키를 실제로 눌러 씬이 넘어가는지)은 KeyControlPlayTests가 본다.
/// </summary>
public class KeyControlTests
{
    private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";
    private const string PrefabPath = "Assets/Prefabs/Common/KeyGuideBar.prefab";

    private static InputActionAsset LoadAsset()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
        Assert.IsNotNull(asset, "입력 에셋이 없습니다");
        return asset;
    }

    private static InputAction Find(string path)
    {
        InputAction action = LoadAsset().FindAction(path, true);
        return action;
    }

    /// <summary>액션의 키보드 바인딩 경로 (합성 바인딩 부분 포함)</summary>
    private static List<string> KeyboardPaths(InputAction action)
    {
        return action.bindings.Where(b => !b.isComposite && b.path.StartsWith("<Keyboard>")).Select(b => b.path).ToList();
    }

    // ───── 1. 입력 에셋 ─────

    [Test]
    public void UiNavigate_HasNoWasdKeyboardBindings_ButKeepsArrowsAndGamepad()
    {
        InputAction navigate = Find("UI/Navigate");
        List<string> keys = KeyboardPaths(navigate);
        foreach (string wasd in new[] { "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d" })
            CollectionAssert.DoesNotContain(keys, wasd, "레인 키와 겹치는 W·A·S·D는 메뉴 이동에 쓰지 않는다");
        CollectionAssert.IsSupersetOf(keys, new[] { "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" });
        Assert.IsTrue(navigate.bindings.Any(b => b.path.StartsWith("<Gamepad>")), "게임패드 바인딩은 그대로");
    }

    [Test]
    public void SongSelectSpeed_IsOnly1And2()
    {
        CollectionAssert.AreEqual(new[] { "<Keyboard>/2" }, KeyboardPaths(Find("SongSelect/SpeedUp")));
        CollectionAssert.AreEqual(new[] { "<Keyboard>/1" }, KeyboardPaths(Find("SongSelect/SpeedDown")));
        Assert.AreEqual(1, Find("SongSelect/SpeedUp").bindings.Count);
        Assert.AreEqual(1, Find("SongSelect/SpeedDown").bindings.Count);
    }

    [Test]
    public void ResultAndGameplay_RetryRestartAreF5_ReturnIsEnter()
    {
        CollectionAssert.AreEqual(new[] { "<Keyboard>/f5" }, KeyboardPaths(Find("Result/Retry")));
        CollectionAssert.AreEqual(new[] { "<Keyboard>/f5" }, KeyboardPaths(Find("Gameplay/Restart")));
        CollectionAssert.AreEqual(new[] { "<Keyboard>/enter", "<Keyboard>/numpadEnter" }, KeyboardPaths(Find("Result/Return")));
        Assert.IsFalse(KeyboardPaths(Find("Result/Return")).Contains("<Keyboard>/escape"), "ESC는 UIManager → ResultScene.OnBack");
    }

    // ───── 2. KeyGuideBar 키 이름 ─────

    [Test]
    public void GetKeyText_ReadsBindingName()
    {
        Assert.AreEqual("F5", KeyGuideBar.GetKeyText(Find("Result/Retry"), "?"));
        Assert.AreEqual("F5", KeyGuideBar.GetKeyText(Find("Gameplay/Restart"), "?"));
        Assert.AreEqual("Enter", KeyGuideBar.GetKeyText(Find("Result/Return"), "?"), "바인딩이 둘이면 첫 키 이름만");
        Assert.AreEqual("Tab", KeyGuideBar.GetKeyText(Find("SongSelect/SwitchTab"), "?"));
        Assert.AreEqual("1", KeyGuideBar.GetKeyText(Find("SongSelect/SpeedDown"), "?"));
        Assert.AreEqual("2", KeyGuideBar.GetKeyText(Find("SongSelect/SpeedUp"), "?"));
    }

    [Test]
    public void GetKeyText_UsageBindingOrNoAction_UsesFallback()
    {
        Assert.AreEqual("Enter", KeyGuideBar.GetKeyText(Find("UI/Submit"), "Enter"), "*/{Submit}은 키 이름을 읽을 수 없어 고정 글자");
        Assert.AreEqual("ESC", KeyGuideBar.GetKeyText((InputAction)null, "ESC"));
        Assert.AreEqual("↑↓", KeyGuideBar.GetKeyText(KeyGuideKey.Fixed("↑↓")));
    }

    [Test]
    public void KeyGuideBar_Init_BuildsItemsFromBindings_AndSetLabelChangesOne()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "KeyGuideBar 프리팹이 없습니다");
        GameObject canvasObject = new GameObject("Canvas_Test", typeof(Canvas));
        GameObject instance = Object.Instantiate(prefab, canvasObject.transform);
        try
        {
            KeyGuideBar bar = instance.GetComponent<KeyGuideBar>();
            InputActionReference retry = InputActionReference.Create(Find("Result/Retry"));
            InputActionReference down = InputActionReference.Create(Find("SongSelect/SpeedDown"));
            InputActionReference up = InputActionReference.Create(Find("SongSelect/SpeedUp"));

            bar.Init(new List<KeyGuideEntry>
            {
                new KeyGuideEntry("다시 하기", KeyGuideKey.FromAction(retry)),
                new KeyGuideEntry("배속", KeyGuideKey.FromAction(down), KeyGuideKey.FromAction(up)),
                new KeyGuideEntry("난이도", KeyGuideKey.Fixed("←→")).WithId("chart")
            });

            Assert.AreEqual(3, bar.ItemCount);
            CollectionAssert.AreEqual(new[] { "F5 다시 하기", "1 2 배속", "←→ 난이도" }, bar.Describe());
            Assert.IsTrue(bar.SetLabel("chart", "채보"));
            Assert.AreEqual("채보", bar.GetLabel("chart"));
            Assert.IsFalse(bar.SetLabel("없는 ID", "x"));

            // 키 모양 박스는 이름 앞에, 키 수만큼
            Transform speedItem = instance.transform.GetChild(2);
            Assert.AreEqual(GameConstants.KeyGuide.KeyCapName, speedItem.GetChild(0).name);
            Assert.AreEqual(GameConstants.KeyGuide.KeyCapName, speedItem.GetChild(1).name);
            Assert.AreEqual(GameConstants.KeyGuide.KeyLabelName, speedItem.GetChild(2).name);

            // 다시 Init하면 이전 항목을 지운다 (틀 1개 + 새 항목)
            bar.Init(new List<KeyGuideEntry> { new KeyGuideEntry("이동", KeyGuideKey.Fixed("↑↓")) });
            Assert.AreEqual(1, bar.ItemCount);
            Assert.AreEqual(2, instance.transform.childCount);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    // ───── 3. 인게임 F5 허용 ─────

    [TestCase(true, true, false, false, false, false, true, TestName = "F5_플레이중_카운트다운중_결과대기중")]
    [TestCase(true, true, false, false, true, true, true, TestName = "F5_일시정지메뉴가_맨위")]
    [TestCase(true, true, false, false, true, false, false, TestName = "F5_확인팝업이나_설정창이_맨위")]
    [TestCase(true, true, false, true, false, false, false, TestName = "F5_씬로딩중")]
    [TestCase(true, true, true, false, false, false, false, TestName = "F5_떠나는중")]
    [TestCase(false, false, false, false, false, false, false, TestName = "F5_시작전_로딩실패")]
    [TestCase(true, false, false, false, false, false, false, TestName = "F5_OnEnter전")]
    public void CanQuickRestart(bool isReady, bool isPlaying, bool isLeaving, bool isSceneLoading, bool hasOpenPopup, bool isPauseTop, bool expected)
    {
        Assert.AreEqual(expected, QuickRestartRules.CanQuickRestart(isReady, isPlaying, isLeaving, isSceneLoading, hasOpenPopup, isPauseTop));
    }
}
