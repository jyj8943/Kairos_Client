using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 팝업 3종 라벤더 톤 EditMode 확인 (기획서 3-7 「팝업 공통 톤 (11/18)」 · ② 사용처 표 · ③ 일시정지).
/// 1. UITheme 팝업 묶음 기본값·에셋 값: 덮개 42% · 카운트다운 덮개 28% · 패널 rgba(250,248,253) 86% · 위험 #C9504A · 위험 선택 16% · 기본 버튼 흰색 75%
/// 2. 위험 사용처: GameConstants.Confirm의 …IsDanger 8개 중 Quit · InGameExit · EditorExit · BpmSectionRemove 네 곳만 true
/// 3. 순수 함수: 버튼 색(기본 · 강조 · 위험), 줄 선택 모양(포인트색 / 위험), 확인 문구 나누기, 진행 시간 글자, 음량 ◀▶, 블러 세기(px → 셰이더 반경), 캡처 위아래 뒤집기
/// </summary>
public class PopupToneTests
{
    private const float Tolerance = 1.5f / 255f;

    private UITheme theme;

    [SetUp]
    public void SetUp()
    {
        theme = ScriptableObject.CreateInstance<UITheme>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(theme);
    }

    // ───── 1. UITheme 팝업 묶음 ─────

    [Test]
    public void Theme_PopupDefaults_AndAsset()
    {
        foreach (UITheme target in new[] { theme, Resources.Load<UITheme>(UITheme.ResourcePath) })
        {
            Assert.IsNotNull(target, "Resources/Data/UITheme 에셋");
            AssertRgba(36, 32, 46, 0.42f, target.popupDim, "뒤 덮개 42%");
            AssertRgba(36, 32, 46, 0.28f, target.popupCountdownDim, "카운트다운 덮개 28%");
            AssertRgba(250, 248, 253, 0.86f, target.popupPanel, "패널 86%");
            AssertRgba(255, 255, 255, 1f, target.popupPanelBorder, "패널 흰 테두리");
            AssertRgba(36, 32, 46, 0.28f, target.popupShadow, "그림자 28%");
            AssertRgba(0xC9, 0x50, 0x4A, 1f, target.danger, "위험 빨강");
            Assert.AreEqual(0.16f, target.dangerSelectedAlpha, 1e-4f, "위험 선택 바탕 16%");
            AssertRgba(255, 255, 255, 0.75f, target.popupButton, "기본 버튼 흰색 75%");
            AssertRgba(0x3E, 0x9B, 0xE0, 1f, target.accent, "강조 버튼·링 하늘색 (기존 포인트색)");
            AssertRgba(0x7A, 0x73, 0x90, 1f, target.textMuted, "보조 문구 (기존 흐린 글자)");
            Assert.AreEqual(0.18f, target.selectedAlpha, 1e-4f, "선택 바탕 18% (기존)");
        }
    }

    // ───── 2. 위험 사용처 ─────

    [Test]
    public void Confirm_DangerOnlyForQuitExitAndDelete()
    {
        Dictionary<string, bool> flags = new Dictionary<string, bool>();
        foreach (FieldInfo field in typeof(GameConstants.Confirm).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(bool) && field.Name.EndsWith("IsDanger")) flags[field.Name] = (bool)field.GetValue(null);
        }

        Assert.AreEqual(8, flags.Count, "확인 팝업 사용처 8곳");
        HashSet<string> danger = new HashSet<string> { "QuitIsDanger", "InGameExitIsDanger", "EditorExitIsDanger", "BpmSectionRemoveIsDanger" };
        foreach (KeyValuePair<string, bool> pair in flags)
            Assert.AreEqual(danger.Contains(pair.Key), pair.Value, pair.Key);
    }

    // ───── 3. 순수 함수 ─────

    [Test]
    public void PopupButtonColors_NormalAccentDanger()
    {
        UIThemePaint.GetPopupButtonColors(theme, PopupButtonStyle.Normal, out Color fill, out Color border, out Color label);
        Assert.AreEqual(theme.popupButton, fill);
        Assert.AreEqual(theme.divider, border, "기본 버튼 테두리 = 구분선 색");
        Assert.AreEqual(theme.textPrimary, label);

        UIThemePaint.GetPopupButtonColors(theme, PopupButtonStyle.Accent, out fill, out border, out label);
        Assert.AreEqual(theme.accent, fill);
        Assert.AreEqual(0f, border.a, "채움 버튼은 테두리 없음");
        Assert.AreEqual(Color.white, label);

        UIThemePaint.GetPopupButtonColors(theme, PopupButtonStyle.Danger, out fill, out border, out label);
        Assert.AreEqual(theme.danger, fill);
        Assert.AreEqual(Color.white, label);
    }

    [Test]
    public void RowLook_AccentOrDanger()
    {
        UIPopupSelect.RowLook normal = UIPopupSelect.GetRowLook(theme, false, false);
        Assert.IsFalse(normal.ShowBar);
        Assert.AreEqual(theme.popupButton, normal.Background);

        UIPopupSelect.RowLook selected = UIPopupSelect.GetRowLook(theme, true, false);
        Assert.IsTrue(selected.ShowBar);
        Assert.AreEqual(theme.accent, selected.Bar);
        Assert.AreEqual(0.18f, selected.Background.a, 1e-4f);
        Assert.AreEqual(theme.textPrimary, selected.MainText);
        Assert.AreEqual(theme.accent, selected.SubText);

        UIPopupSelect.RowLook danger = UIPopupSelect.GetRowLook(theme, true, true);
        Assert.AreEqual(theme.danger, danger.Bar, "나가기 막대 빨강");
        Assert.AreEqual(theme.danger, danger.MainText, "나가기 글자 빨강");
        Assert.AreEqual(UITheme.WithAlpha(theme.danger, 0.16f), danger.Background, "나가기 바탕 빨강 16%");

        Assert.AreEqual(theme.textPrimary, UIPopupSelect.GetRowLook(theme, false, true).MainText, "선택 안 된 나가기는 기본 글자");
    }

    [Test]
    public void SplitMessage_BodyAndSub()
    {
        Popup_Confirm.SplitMessage(GameConstants.Confirm.OffsetRestart, out string body, out string sub);
        Assert.AreEqual(GameConstants.Confirm.OffsetRestart, body, "지금 문구는 본문만");
        Assert.AreEqual(string.Empty, sub);

        Popup_Confirm.SplitMessage("본문입니다. | 보조 문구", out body, out sub);
        Assert.AreEqual("본문입니다.", body);
        Assert.AreEqual("보조 문구", sub);

        Popup_Confirm.SplitMessage(null, out body, out sub);
        Assert.AreEqual(string.Empty, body);
    }

    [Test]
    public void PauseProgress_Format()
    {
        Assert.AreEqual("1:04 / 2:31", Popup_Pause.FormatProgress(64.9, 151.5f));
        Assert.AreEqual("0:00 / 2:31", Popup_Pause.FormatProgress(-1.0, 151f), "카운트인 중(음수)은 0:00");
        Assert.AreEqual("2:31 / 2:31", Popup_Pause.FormatProgress(200.0, 151f), "곡 길이를 넘지 않는다");
    }

    [Test]
    public void VolumeStep_ClampsToRange()
    {
        Assert.AreEqual(85, Popup_Settings.StepVolumeValue(80, 1));
        Assert.AreEqual(75, Popup_Settings.StepVolumeValue(80, -1));
        Assert.AreEqual(100, Popup_Settings.StepVolumeValue(98, 1));
        Assert.AreEqual(0, Popup_Settings.StepVolumeValue(3, -1));
    }

    [Test]
    public void BlurRadius_PxToShaderAndFlip()
    {
        // σ = 1.637 × 반경 × 4(줄임) × √1 → 6px면 반경 ≈ 0.916
        float radius = UIBlurBackground.ShaderRadiusFor(GameConstants.Popup.BlurRadiusPx, 4, GameConstants.Popup.BlurIterations);
        Assert.AreEqual(6f / (UIBlurBackground.KernelSigmaTexels * 4f), radius, 1e-4f);
        Assert.AreEqual(radius / Mathf.Sqrt(2f), UIBlurBackground.ShaderRadiusFor(6f, 4, 2), 1e-4f, "반복 2번이면 √2로 나눈다");

        Vector2 scale = Vector2.one, offset = Vector2.zero;
        UIBlurBackground.FlipY(ref scale, ref offset);
        Assert.AreEqual(new Vector2(1f, -1f), scale);
        Assert.AreEqual(new Vector2(0f, 1f), offset, "v → 1 - v");
    }

    private static void AssertRgba(int r, int g, int b, float a, Color actual, string message)
    {
        Assert.AreEqual(r / 255f, actual.r, Tolerance, message + " R");
        Assert.AreEqual(g / 255f, actual.g, Tolerance, message + " G");
        Assert.AreEqual(b / 255f, actual.b, Tolerance, message + " B");
        Assert.AreEqual(a, actual.a, 1e-3f, message + " A");
    }
}
