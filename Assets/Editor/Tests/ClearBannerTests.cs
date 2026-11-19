using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 곡 끝 FULL COMBO · ALL PERFECT 연출 (기획서 3-4 「FC/AP 연출 (11/19)」) EditMode 확인.
/// 1. 분기: AP / FC / 일반(MISS 있음) / 테스트 플레이 / 자동 플레이 (판정 개수 기준, AP 우선)
/// 2. 시간표 값 (README 5절): 띠 · 반짝임 · 글자 · 바늘(1.0초에 정확히 0°) · 개수(1.0초에 전체) · 강조 · 링 · 알갱이(시드 고정) · 사라짐
/// 3. 실제 씬(InGameScene.unity)의 ClearBanner: 시간별 값 표, 캡슐 폭이 0.3~1.0초 동안 그대로, 글자 넘침 없음, 줄맞춤, Canvas 안
/// 4. 연출 중 ESC(OnBack) 무시
/// 씬을 여는 테스트는 열려 있던 씬 구성을 저장해 두고 끝나면 되돌린다 (씬 파일은 저장하지 않는다). 저장 안 한 씬이 있으면 건너뛴다.
/// </summary>
public class ClearBannerTests
{
    private const string ScenePath = "Assets/Scenes/InGameScene.unity";
    private static readonly float[] ReportTimes = { 0.15f, 0.6f, 1.0f, 1.15f, 1.8f, 2.7f, 3.0f };

    [Test]
    public void Select_ApFcNormalTestPlay()
    {
        Assert.AreEqual(ClearType.AllPerfect, ClearBannerRules.Select(false, false, 329, 0, 329, 329), "전부 PERFECT → AP (FC이기도 하지만 AP 우선)");
        Assert.AreEqual(ClearType.FullCombo, ClearBannerRules.Select(false, false, 301, 0, 329, 329), "MISS 0 → FC");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(false, false, 320, 1, 329, 329), "MISS 1 → 연출 없음");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(true, false, 329, 0, 329, 329), "테스트 플레이는 AP여도 없음");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(true, false, 301, 0, 329, 329), "테스트 플레이 FC도 없음");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(false, true, 329, 0, 329, 329), "자동 플레이 없음");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(false, false, 0, 0, 0, 0), "노트 0개");
        Assert.AreEqual(ClearType.None, ClearBannerRules.Select(false, false, 300, 0, 300, 329), "다 판정하지 않음");

        // 점수 계산기와 같은 기준 (랭크 AP = RankRules.IsAllPerfect)
        ScoreCalculator ap = new ScoreCalculator(3);
        for (int i = 0; i < 3; i++) ap.Add(JudgeType.Perfect);
        Assert.AreEqual(PlayRank.AP, ap.Rank);
        Assert.AreEqual(ClearType.AllPerfect, ClearBannerRules.Select(false, false, ap.PerfectCount, ap.MissCount, ap.JudgedCount, ap.TotalNotes));

        Debug.Log("[ClearBannerTest] 분기: AP(329/329 PERFECT) → AllPerfect, FC(MISS 0) → FullCombo, 일반(MISS 1) → None, 테스트 플레이 → None, 자동 플레이 → None");
    }

    [Test]
    public void Timeline_ScheduleValues()
    {
        ClearBannerTimeline tl = new ClearBannerTimeline();
        const int n = 329;

        Assert.IsFalse(tl.Evaluate(-0.01f, n).Visible, "시작 전 숨김");
        Assert.IsFalse(tl.Evaluate(3.0f, n).Visible, "3초에 숨김 (결과로)");

        Assert.AreEqual(0f, tl.Evaluate(0f, n).BandScaleY, 1e-4f);
        Assert.AreEqual(1f, tl.Evaluate(0.2f, n).BandScaleY, 1e-4f, "0.2초에 띠 다 펼침");
        Assert.AreEqual(1f - 0.125f, tl.Evaluate(0.1f, n).BandScaleY, 1e-4f, "easeOutCubic(0.5)");

        Assert.AreEqual(0.22f, tl.Evaluate(0.05f, n).FlashAlpha, 1e-4f, "0.05초 반짝임 22%");
        Assert.AreEqual(0f, tl.Evaluate(0.35f, n).FlashAlpha, 1e-4f, "0.35초 반짝임 0");

        ClearBannerFrame before = tl.Evaluate(0.08f, n);
        Assert.AreEqual(0f, before.LogoAlpha, 1e-4f);
        Assert.AreEqual(1.15f, before.LogoScale, 1e-4f);
        Assert.AreEqual(60f, before.LogoOffsetX, 1e-4f);
        ClearBannerFrame logo = tl.Evaluate(0.38f, n);
        Assert.AreEqual(1f, logo.LogoAlpha, 1e-4f, "0.38초 글자 다 나타남");
        Assert.AreEqual(1f, logo.LogoScale, 1e-4f);
        Assert.AreEqual(0f, logo.LogoOffsetX, 1e-4f);

        ClearBannerFrame start = tl.Evaluate(0f, n);
        Assert.AreEqual(-720f, start.MinuteAngle, 1e-3f);
        Assert.AreEqual(-60f, start.HourAngle, 1e-3f);
        ClearBannerFrame noon = tl.Evaluate(1.0f, n);
        Assert.AreEqual(0f, noon.MinuteAngle, 1e-4f, "1.0초 분침 12시");
        Assert.AreEqual(0f, noon.HourAngle, 1e-4f, "1.0초 시침 12시");
        Assert.AreEqual(n, noon.Count, "1.0초 개수 = 전체");
        Assert.Less(tl.Evaluate(0.999f, n).Count, n, "1.0초 전에는 전체에 닿지 않음");
        Assert.AreEqual(0, tl.Evaluate(0.3f, n).Count, "0.3초까지 0");
        Assert.AreEqual(-720f * 0.16f, tl.Evaluate(0.6f, n).MinuteAngle, 1e-3f, "easeOutQuad(0.6) = 0.84");
        Assert.AreEqual(20f * 2.5f, tl.Evaluate(2.5f, n).DashesAngle, 1e-3f, "점선 원 초당 20°");
        int last = -1;
        for (float t = 0.3f; t <= 1.0f; t += 0.01f)
        {
            int c = tl.Evaluate(t, n).Count;
            Assert.GreaterOrEqual(c, last, "개수는 줄지 않음");
            last = c;
        }

        ClearBannerFrame peak = tl.Evaluate(1.175f, n);
        Assert.AreEqual(1.05f, peak.ClockScale, 1e-3f, "강조 최고: 시계 1.05");
        Assert.AreEqual(1.08f, peak.LogoScale, 1e-3f, "글자 1.08");
        Assert.AreEqual(0.45f, peak.LogoBright, 1e-3f, "밝기 +45%p");
        Assert.AreEqual(0.9f, peak.HighlightAlpha, 1e-3f);
        Assert.AreEqual(0.9f, peak.TopDotAlpha, 1e-3f);
        Assert.AreEqual(0.10f, peak.FlashAlpha, 1e-3f, "반짝임 +10%p");
        Assert.AreEqual(0f, tl.Evaluate(1.35f, n).HighlightAlpha, 1e-4f, "1.35초 강조 끝");

        Assert.AreEqual(1f, tl.Evaluate(1.0f, n).BurstScale, 1e-4f);
        Assert.AreEqual(0.9f, tl.Evaluate(1.0001f, n).BurstAlpha, 1e-3f);
        Assert.AreEqual(0f, tl.Evaluate(1.55f, n).BurstAlpha, 1e-4f, "1.55초 링 끝");
        Assert.AreEqual(280f / 170f, tl.Evaluate(1.5499f, n).BurstScale, 1e-3f, "반지름 170 → 280");
        Assert.IsTrue(tl.Evaluate(1.3f, n).ShineVisible);
        Assert.IsFalse(tl.Evaluate(1.6f, n).ShineVisible, "1.6초 빛 줄기 끝");

        Assert.AreEqual(620f, tl.Evaluate(0.8f, n).PillarHeight, 1e-3f, "0.8초 기둥 620");
        Assert.AreEqual(1f, tl.Evaluate(0.4f, n).CapsuleAlpha, 1e-4f, "0.4초 캡슐 다 나타남");

        ClearBannerFrame end = tl.Evaluate(2.9999f, n);
        Assert.AreEqual(0f, end.RootAlpha, 1e-3f, "사라짐 끝 알파 0");
        Assert.AreEqual(16f, end.LogoRise, 1e-2f, "글자 위로 16px");
        Assert.AreEqual(16f, end.CapsuleRise, 1e-2f, "캡슐 위로 16px");
        Assert.AreEqual(0.35f, end.PillarAlpha, 1e-3f, "기둥 35%");
        Assert.AreEqual(0.4f, end.ComboAlpha, 1e-3f, "콤보 숫자 40%");
        Assert.AreEqual(1f, tl.Evaluate(2.4f, n).RootAlpha, 1e-4f, "2.4초까지 그대로");

        // 빛 알갱이: 시드 고정이라 같은 값, 값 범위
        ClearMote[] a = ClearBannerTimeline.CreateMotes(ClearBannerTimeline.DefaultSeed);
        ClearMote[] b = ClearBannerTimeline.CreateMotes(ClearBannerTimeline.DefaultSeed);
        Assert.AreEqual(30, a.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].Start, b[i].Start, "재현");
            Assert.That(a[i].Start.x, Is.InRange(-140f, 140f));
            Assert.That(-a[i].Start.y, Is.InRange(60f, 150f));
            Assert.That(a[i].Size, Is.InRange(6f, 13f));
            Assert.That(a[i].StartTime, Is.InRange(1.1f, 1.7f));
            Assert.That(a[i].Life, Is.InRange(0.9f, 1.4f));
        }
        Assert.AreEqual(0, tl.ActiveMoteCount(1.09f), "1.1초 전 알갱이 없음");
        Assert.Greater(tl.ActiveMoteCount(1.8f), 0);

        StringBuilder sb = new StringBuilder("[ClearBannerTest] 시간표 (전체 329): 시간 | 띠 scaleY | 글자 알파·크기 | 분침·시침 | 개수 | 알갱이\n");
        foreach (float t in ReportTimes)
        {
            ClearBannerFrame f = tl.Evaluate(t, n);
            sb.AppendLine($"{t:0.00} | {f.BandScaleY:0.000} | {f.LogoAlpha:0.000}·{f.LogoScale:0.000} | {f.MinuteAngle:0.0}°·{f.HourAngle:0.0}° | {f.Count} | {tl.ActiveMoteCount(t)} (보임 {f.Visible})");
        }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void Scene_ValuesWidthAlignmentCanvas()
    {
        SceneSetup[] setup = OpenScene();
        try
        {
            Canvas_InGame canvas = Object.FindFirstObjectByType<Canvas_InGame>();
            ClearBanner banner = canvas.Banner;
            Assert.IsNotNull(banner, "Canvas_InGame에 ClearBanner 연결");
            Assert.AreEqual(canvas.transform, banner.transform.parent);
            Assert.Greater(banner.transform.GetSiblingIndex(), canvas.transform.Find("Img_Judge").GetSiblingIndex(), "판정 글자·콤보·패널보다 앞");
            Assert.Less(banner.transform.GetSiblingIndex(), canvas.transform.Find("Popup_Pause").GetSiblingIndex(), "일시정지 팝업보다 뒤");
            Assert.AreEqual(0, canvas.transform.Find("LanePillars").GetSiblingIndex(), "라인 빛 기둥은 맨 뒤 (기어 위, HUD 아래)");
            Assert.IsNull(canvas.transform.Find("ClearEffect"), "옛 연출 지움");
            Assert.IsNull(canvas.transform.Find("Img_ScreenFlash"), "옛 반짝임 지움");
            Assert.AreEqual(30, banner.transform.Find("Motes").childCount);

            banner.Init();
            Assert.AreEqual(0f, banner.GetComponent<CanvasGroup>().alpha, "Init 뒤 숨김");
            Assert.IsFalse(banner.IsPlaying);
            foreach (Image p in banner.LanePillars) Assert.IsFalse(p.enabled, "기둥 숨김");

            StringBuilder sb = new StringBuilder();
            foreach (ClearType type in new[] { ClearType.AllPerfect, ClearType.FullCombo })
                ReportType(banner, canvas, type, sb);
            Debug.Log(sb.ToString());
        }
        finally
        {
            Restore(setup);
        }
    }

    private static void ReportType(ClearBanner banner, Canvas_InGame canvas, ClearType type, StringBuilder sb)
    {
        const int total = 329;
        banner.Init();
        banner.Play(type, total, total);
        Assert.IsTrue(banner.IsPlaying);
        UITheme theme = UITheme.Current;
        ClearColors colors = theme.ClearColorsOf(type);
        Assert.AreEqual(colors.clock, banner.MinuteHand.color, "바늘 색 = UITheme");
        Assert.AreEqual(type == ClearType.AllPerfect ? ClearBanner.AllPerfectLogoSize : ClearBanner.FullComboLogoSize, banner.LogoRect.sizeDelta);
        Assert.AreEqual(type == ClearType.AllPerfect ? "AllPerfect" : "FullCombo", banner.Logo.sprite.name);
        Assert.AreEqual(type == ClearType.AllPerfect ? "PERFECT" : "COMBO", banner.CountLabel.text);
        Assert.AreEqual("/ 329", banner.CountTotal.text);

        RectTransform root = (RectTransform)canvas.transform;
        sb.AppendLine($"[ClearBannerTest] {type} 씬 값: 시간 | 띠 scaleY | 글자 알파·크기 | 분침·시침 | 개수 | 캡슐 폭 | 알갱이");
        foreach (float t in ReportTimes)
        {
            banner.SetTime(t);
            Rebuild(banner);
            ClearBannerFrame f = banner.Frame;
            sb.AppendLine($"{t:0.00} | {banner.BandRect.localScale.y:0.000} | {banner.Logo.color.a:0.000}·{banner.LogoRect.localScale.x:0.000} | " +
                          $"{-banner.MinuteHand.rectTransform.localEulerAngles.z:0.0}°·{-banner.HourHand.rectTransform.localEulerAngles.z:0.0}° | " +
                          $"{Shown(banner.CountText)} | {banner.CapsuleRect.rect.width:0.0} | {banner.ActiveMoteCount}{(f.Visible ? "" : " (숨김)")}");
            if (Mathf.Approximately(t, 1.0f))
            {
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, banner.MinuteHand.rectTransform.localEulerAngles.z), 1e-3f, "1.0초 분침 0°");
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, banner.HourHand.rectTransform.localEulerAngles.z), 1e-3f, "1.0초 시침 0°");
                Assert.AreEqual(total.ToString(), Shown(banner.CountText), "1.0초 개수 = 전체");
            }
        }
        Assert.IsFalse(banner.IsPlaying, "3.0초 끝");
        Assert.AreEqual(0f, banner.GetComponent<CanvasGroup>().alpha, "3.0초 숨김");

        // 캡슐 폭: 0.3~1.0초 동안 그대로
        banner.Play(type, total, total);
        float min = float.MaxValue, max = float.MinValue;
        for (float t = 0.3f; t <= 1.0001f; t += 0.01f)
        {
            banner.SetTime(t);
            Rebuild(banner);
            float w = banner.CapsuleRect.rect.width;
            min = Mathf.Min(min, w);
            max = Mathf.Max(max, w);
        }
        Assert.AreEqual(0f, max - min, 1e-3f, "숫자가 올라가는 동안 캡슐 폭 그대로");
        Assert.AreEqual(ClearBannerRules.CountWidth(total), banner.CountText.rectTransform.rect.width, 1e-3f, "개수 칸 = 3자리 × 29");
        sb.AppendLine($"캡슐 폭 0.3~1.0초: 최소 {min:0.00} · 최대 {max:0.00} (차 {max - min:0.00})");

        // 글자 넘침 · 줄맞춤 (1.8초 = 다 나온 상태)
        banner.SetTime(1.8f);
        Rebuild(banner);
        TMP_Text[] texts = { banner.CountLabel, banner.CountText, banner.CountTotal };
        float[] mid = new float[3];
        float[] baseLine = new float[3];
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            text.ForceMeshUpdate();
            Assert.IsFalse(text.isTextOverflowing, text.name + " 넘침");
            Measure(text, root, out mid[i], out baseLine[i]);
        }
        sb.AppendLine($"줄맞춤 (위에서 y): 라벨 가운데 {mid[0]:0.0} · 아랫선 {baseLine[0]:0.0} / 숫자 가운데 {mid[1]:0.0} · 아랫선 {baseLine[1]:0.0} / 전체 가운데 {mid[2]:0.0} · 아랫선 {baseLine[2]:0.0} → " +
                      $"라벨−숫자 가운데 차 {mid[0] - mid[1]:+0.0;-0.0;0.0}px, 숫자−전체 아랫선 차 {baseLine[1] - baseLine[2]:+0.0;-0.0;0.0}px, isTextOverflowing 셋 다 false");
        Assert.AreEqual(mid[1], mid[0], 1.5f, "라벨과 숫자 대문자·숫자 높이 가운데가 같은 y");
        Assert.AreEqual(baseLine[1], baseLine[2], 1.5f, "전체와 큰 숫자 아랫선이 같은 y");

        // 라벨 가로 그라데이션 (글자 전체 폭: 맨 왼쪽 = 시작 색, 맨 오른쪽 = 끝 색)
        TMP_TextInfo info = banner.CountLabel.textInfo;
        TMP_CharacterInfo first = info.characterInfo[0];
        TMP_CharacterInfo lastChar = info.characterInfo[info.characterCount - 1];
        Color32 left = info.meshInfo[first.materialReferenceIndex].colors32[first.vertexIndex];
        Color32 right = info.meshInfo[lastChar.materialReferenceIndex].colors32[lastChar.vertexIndex + 2];
        Assert.AreEqual(colors.labelStart.r, left.r / 255f, 0.03f, "라벨 왼쪽 = 시작 색");
        Assert.AreEqual(colors.labelEnd.g, right.g / 255f, 0.03f, "라벨 오른쪽 = 끝 색");

        // Canvas 안 (1.8초 = 다 나온 상태, 1.175초 = 강조 최고 크기)
        foreach (float t in new[] { 1.175f, 1.8f })
        {
            banner.SetTime(t);
            Rebuild(banner);
            sb.Append($"Canvas 안 ({t:0.000}초): ");
            foreach (RectTransform r in new[] { banner.BandRect, banner.CapsuleRect, banner.LogoRect, banner.ClockRect })
            {
                Rect b = Bounds(r, root);
                bool inside = b.xMin >= -960.5f && b.xMax <= 960.5f && b.yMin >= -540.5f && b.yMax <= 540.5f;
                Assert.IsTrue(inside, $"{r.name} Canvas 밖: {b}");
                sb.Append($"{r.name} x {b.xMin + 960:0}~{b.xMax + 960:0} y {540 - b.yMax:0}~{540 - b.yMin:0} ✔, ");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    /// <summary>대문자·숫자 높이 가운데와 아랫선 (Canvas 위에서 잰 y, px). '/'와 빈칸은 빼고 잰다</summary>
    private static void Measure(TMP_Text text, RectTransform root, out float middle, out float baseLine)
    {
        TMP_TextInfo info = text.textInfo;
        float top = float.MinValue, bottom = 0f;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible || !char.IsLetterOrDigit(c.character)) continue;
            top = Mathf.Max(top, c.topLeft.y);
            bottom = c.baseLine;
        }
        float topY = root.InverseTransformPoint(text.transform.TransformPoint(new Vector3(0f, top, 0f))).y;
        float baseY = root.InverseTransformPoint(text.transform.TransformPoint(new Vector3(0f, bottom, 0f))).y;
        middle = 540f - (topY + baseY) * 0.5f;
        baseLine = 540f - baseY;
    }

    /// <summary>태그를 뺀 보이는 글자</summary>
    private static string Shown(TMP_Text text)
    {
        text.ForceMeshUpdate();
        return text.GetParsedText();
    }

    private static Rect Bounds(RectTransform r, RectTransform root)
    {
        Vector3[] corners = new Vector3[4];
        r.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 c in corners)
        {
            Vector3 p = root.InverseTransformPoint(c);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void Rebuild(ClearBanner banner)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(banner.CapsuleRect);
        Canvas.ForceUpdateCanvases();
    }

    [Test]
    public void Scene_EscIgnoredWhilePlaying()
    {
        SceneSetup[] setup = OpenScene();
        try
        {
            Canvas_InGame canvas = Object.FindFirstObjectByType<Canvas_InGame>();
            InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
            Popup_Pause pause = canvas.GetComponentInChildren<Popup_Pause>(true);
            canvas.Banner.Init();
            canvas.Banner.Play(ClearType.FullCombo, 10, 10);
            canvas.Banner.SetTime(1.2f);
            Assert.IsTrue(canvas.IsClearPlaying);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("곡 끝 연출 중 ESC 무시"));
            scene.OnBack();
            Assert.IsFalse(pause.gameObject.activeSelf, "연출 중 ESC → 일시정지 팝업 안 열림");

            canvas.Banner.SetTime(3.0f);
            Assert.IsFalse(canvas.IsClearPlaying, "3초 뒤 재생 끝");
            Debug.Log("[ClearBannerTest] 연출 중(1.2초) ESC → '곡 끝 연출 중 ESC 무시' 로그, Popup_Pause 닫힌 채");
        }
        finally
        {
            Restore(setup);
        }
    }

    private static SceneSetup[] OpenScene()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                Assert.Ignore("저장하지 않은 씬이 열려 있어 건너뜁니다 (씬을 저장한 뒤 다시 돌리세요).");
        }
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return setup;
    }

    /// <summary>열려 있던 씬 구성으로 되돌린다 (저장된 씬만. 없으면 빈 씬)</summary>
    private static void Restore(SceneSetup[] setup)
    {
        System.Collections.Generic.List<SceneSetup> saved = new System.Collections.Generic.List<SceneSetup>();
        foreach (SceneSetup s in setup) if (!string.IsNullOrEmpty(s.path)) saved.Add(s);
        if (saved.Count > 0 && !saved.Exists(s => s.isActive)) saved[0].isActive = true;
        if (saved.Count > 0 && saved.Exists(s => s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(saved.ToArray());
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
