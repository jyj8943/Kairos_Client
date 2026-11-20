using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 일반 로딩 (기획서 3-9 「일반 로딩」) EditMode 확인.
/// 1. 들어오기 · 나가기 시간표 (「일러스트 먼저」, 각 0.5초) · 불러오는 중 점 3개
/// 2. 가는 곳 문구 (다음 씬 이름으로)
/// 3. TIP · 일러스트 무작위: 바로 앞 것은 피함, 20번 열기 분포 · 연속 중복 0
/// 4. TIP 문구 속 키가 입력 에셋(InputSystem_Actions) 바인딩과 같은지
/// </summary>
public class LoadingTests
{
    [Test]
    public void Timeline_InOut_Dots()
    {
        LoadingFrame in0 = LoadingTimeline.EvaluateIn(0f);
        Assert.AreEqual(216f, in0.BandDrop, 1e-3f, "띠는 화면 아래(y 1080)에서 시작");
        Assert.AreEqual(0f, in0.FrontAlpha, 1e-4f);
        Assert.AreEqual(0.5f, LoadingTimeline.EvaluateIn(0.15f).FrontAlpha, 1e-4f, "일러스트 0~0.3 가운데 0.5");
        Assert.AreEqual(1f, LoadingTimeline.EvaluateIn(0.3f).FrontAlpha, 1e-4f, "0.3초에 일러스트 다 덮음");
        Assert.AreEqual(216f, LoadingTimeline.EvaluateIn(0.2f).BandDrop, 1e-3f, "띠는 0.2초부터");
        Assert.AreEqual(216f * 0.125f, LoadingTimeline.EvaluateIn(0.35f).BandDrop, 1e-3f, "easeOutCubic(0.5) = 0.875");
        LoadingFrame in5 = LoadingTimeline.EvaluateIn(GameConstants.Loading.InSeconds);
        Assert.AreEqual(1f, in5.FrontAlpha, 1e-4f, "0.5초 들어오기 끝");
        Assert.AreEqual(0f, in5.BandDrop, 1e-4f, "0.5초에 띠 제자리 (y 864)");

        LoadingFrame out0 = LoadingTimeline.EvaluateOut(0f);
        Assert.AreEqual(1f, out0.FrontAlpha, 1e-4f);
        Assert.AreEqual(0f, out0.BandDrop, 1e-4f);
        Assert.AreEqual(216f * 0.125f, LoadingTimeline.EvaluateOut(0.15f).BandDrop, 1e-3f, "easeInCubic(0.5) = 0.125");
        Assert.AreEqual(216f, LoadingTimeline.EvaluateOut(0.3f).BandDrop, 1e-3f, "0.3초에 띠 화면 밖 (y 1080)");
        Assert.AreEqual(1f, LoadingTimeline.EvaluateOut(0.15f).FrontAlpha, 1e-4f, "일러스트는 0.15초까지 그대로");
        Assert.AreEqual(0.5f, LoadingTimeline.EvaluateOut(0.325f).FrontAlpha, 1e-4f, "0.15~0.5 가운데 0.5");
        Assert.AreEqual(0f, LoadingTimeline.EvaluateOut(GameConstants.Loading.OutSeconds).FrontAlpha, 1e-4f, "0.5초에 일러스트 사라짐");

        Assert.AreEqual(1, LoadingTimeline.LitDots(0f));
        Assert.AreEqual(2, LoadingTimeline.LitDots(0.34f));
        Assert.AreEqual(3, LoadingTimeline.LitDots(0.67f));
        Assert.AreEqual(1, LoadingTimeline.LitDots(1.0f), "3개 뒤 처음부터");

        StringBuilder sb = new StringBuilder("[LoadingTest] 들어오기: 시간 | 일러스트 · 화면 이름 알파 | 띠 위 y\n");
        foreach (float t in new[] { 0.1f, 0.3f, 0.4f, 0.5f }) { LoadingFrame f = LoadingTimeline.EvaluateIn(t); sb.AppendLine($"{t:0.0} | {f.FrontAlpha:0.000} | {864f + f.BandDrop:0.0}"); }
        sb.AppendLine("나가기:");
        foreach (float t in new[] { 0.1f, 0.3f, 0.4f, 0.5f }) { LoadingFrame f = LoadingTimeline.EvaluateOut(t); sb.AppendLine($"{t:0.0} | {f.FrontAlpha:0.000} | {864f + f.BandDrop:0.0}"); }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void Destination_ByNextScene()
    {
        Assert.AreEqual("곡 선택으로 이동", LoadingRules.DestinationOf(GameConstants.Scenes.SongSelect));
        Assert.AreEqual("에디터로 이동", LoadingRules.DestinationOf(GameConstants.Scenes.Editor));
        Assert.AreEqual("타이틀로 이동", LoadingRules.DestinationOf(GameConstants.Scenes.Title));
        Assert.AreEqual("테스트 플레이로 이동", LoadingRules.DestinationOf(GameConstants.Scenes.InGame));
        Assert.AreEqual(string.Empty, LoadingRules.DestinationOf("Nowhere"));
    }

    [Test]
    public void Pick_AvoidsPrevious_Distribution20()
    {
        System.Random random = new System.Random(20261120);
        Assert.AreEqual(-1, LoadingRules.PickIndex(0, -1, random), "비면 -1");
        Assert.AreEqual(0, LoadingRules.PickIndex(1, 0, random), "1장이면 늘 0 (같은 것 허용)");

        int count = GameConstants.Loading.Tips.Length;
        int[] hits = new int[count];
        int previous = -1;
        int repeats = 0;
        for (int i = 0; i < 20; i++)
        {
            int pick = LoadingRules.PickIndex(count, previous, random);
            Assert.That(pick, Is.InRange(0, count - 1));
            if (pick == previous) repeats++;
            hits[pick]++;
            previous = pick;
        }
        Assert.AreEqual(0, repeats, "연속 중복 0");
        for (int k = 0; k < 2000; k++) Assert.AreNotEqual(5, LoadingRules.PickIndex(count, 5, random), "바로 앞은 안 나옴");
        Debug.Log($"[LoadingTest] TIP 20번: {string.Join(" / ", System.Array.ConvertAll(hits, h => h.ToString()))} (TIP {count}개), 연속 중복 {repeats}");
    }

    [Test]
    public void Tips_KeysMatchInputBindings()
    {
        InputActionAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
        Assert.IsNotNull(asset);
        AssertBinding(asset, "SongSelect/SpeedDown", "<Keyboard>/1");
        AssertBinding(asset, "SongSelect/SpeedUp", "<Keyboard>/2");
        AssertBinding(asset, "Gameplay/SpeedDown", "<Keyboard>/1");
        AssertBinding(asset, "Gameplay/SpeedUp", "<Keyboard>/2");
        AssertBinding(asset, "Gameplay/BackgroundBrighter", "<Keyboard>/pageUp");
        AssertBinding(asset, "Gameplay/BackgroundDarker", "<Keyboard>/pageDown");
        AssertBinding(asset, "SongSelect/SwitchTab", "<Keyboard>/tab");
        AssertBinding(asset, "ChartEditor/Undo", "<Keyboard>/z");
        AssertBinding(asset, "ChartEditor/Redo", "<Keyboard>/y");
        AssertBinding(asset, "ChartEditor/PlayPause", "<Keyboard>/space");
        AssertBinding(asset, "SongSelect/EditChart", "<Keyboard>/e");
        Assert.AreEqual(7, GameConstants.Loading.Tips.Length);
        HashSet<string> categories = new HashSet<string>();
        foreach (GameConstants.LoadingTip tip in GameConstants.Loading.Tips) categories.Add(tip.Category);
        CollectionAssert.AreEquivalent(new[] { GameConstants.Loading.CategoryControl, GameConstants.Loading.CategoryJudge, GameConstants.Loading.CategoryEditor }, categories);
    }

    private static void AssertBinding(InputActionAsset asset, string action, string path)
    {
        InputAction a = asset.FindAction(action);
        Assert.IsNotNull(a, action);
        bool found = false;
        foreach (InputBinding b in a.bindings) if (b.path == path) found = true;
        Assert.IsTrue(found, $"{action}에 {path} 바인딩");
    }
}
