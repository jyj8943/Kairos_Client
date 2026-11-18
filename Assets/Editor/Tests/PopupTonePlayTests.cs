using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 팝업 3종 라벤더 톤 PlayMode 확인 (기획서 3-7, EnterPlayMode 방식). 짧게: 타이틀 또는 노트 하나짜리 테스트 플레이 채보.
/// - P1 확인 팝업: 위험 표시면 확인 버튼·"!" 원·선택 링 빨강, 아니면 하늘색. 처음 선택은 취소. 3줄 문구면 패널이 늘어난다
/// - P2 일시정지: 열면 뒤 화면 블러 텍스처, 패널 폭 600 · 키 안내 줄이 패널 안쪽, 테스트 플레이 뱃지(테스트 플레이일 때만), 나가기 선택은 빨강 막대.
///   설정 창을 위에 열면 다시 캡처. 계속 → 팝업 닫힘 + 덮개 28% + 카운트다운 3 (숫자가 팝업·덮개보다 위, 아래 문구 없음)
/// - P3 설정 창: 탭을 바꿔도 패널 960×540 그대로, 뒤 화면 블러, 키 안내 줄 = HintText
/// 인게임은 테스트 플레이 데이터로 들어가 기록(PlayerData.json)을 남기지 않는다.
/// </summary>
public class PopupTonePlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const float Tolerance = 1.5f / 255f;

    [UnityTest]
    public IEnumerator P1_Confirm_DangerRedElseAccent_FirstSelectCancel()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return LoadAndWait(GameConstants.Scenes.Title, null);
        UITheme theme = UITheme.Current;
        Popup_Confirm confirm = GameManager.UI.Get<Popup_Confirm>();

        // 위험 (인게임 나가기 문구, 2줄)
        Assert.IsTrue(GameManager.UI.Confirm(GameConstants.Confirm.InGameExitMessage, GameConstants.Confirm.InGameExitLabel, null, null,
            GameConstants.Confirm.InGameExitIsDanger));
        yield return null;
        Assert.IsTrue(confirm.IsDanger);
        AssertColor(theme.danger, confirm.ConfirmButton.targetGraphic.color, "확인 버튼 빨강");
        AssertColor(theme.danger, confirm.IconText.color, "\"!\" 빨강");
        AssertColor(UITheme.WithAlpha(theme.danger, theme.dangerSelectedAlpha), confirm.IconImage.color, "\"!\" 원 빨강 16%");
        AssertColor(theme.popupButton, confirm.CancelButton.targetGraphic.color, "취소 = 기본 버튼");
        Assert.AreEqual(confirm.CancelButton.gameObject, EventSystem.current.currentSelectedGameObject, "처음 선택은 취소");
        UIPopupSelect cancelSelect = confirm.CancelButton.GetComponent<UIPopupSelect>();
        UIPopupSelect confirmSelect = confirm.ConfirmButton.GetComponent<UIPopupSelect>();
        Assert.IsTrue(cancelSelect.RingOuter.gameObject.activeSelf, "취소 버튼 링");
        AssertColor(theme.accent, cancelSelect.RingOuter.color, "취소 버튼 링은 하늘색");
        EventSystem.current.SetSelectedGameObject(confirm.ConfirmButton.gameObject);
        yield return null;
        Assert.IsTrue(confirmSelect.RingOuter.gameObject.activeSelf, "확인 버튼 링");
        AssertColor(theme.danger, confirmSelect.RingOuter.color, "위험 확인 버튼 링 빨강");
        Assert.IsFalse(cancelSelect.RingOuter.gameObject.activeSelf);
        float twoLineHeight = confirm.Panel.rect.height;
        GameManager.UI.TopPopup.OnCancelRequested();
        yield return WaitRealtime(GameConstants.UI.TransitionSeconds + 0.1f);

        // 위험 아님 (오프셋 재시작 문구, 3줄)
        Assert.IsTrue(GameManager.UI.Confirm(GameConstants.Confirm.OffsetRestart, GameConstants.Confirm.OffsetRestartLabel, null, null,
            GameConstants.Confirm.OffsetRestartIsDanger));
        yield return null;
        Assert.IsFalse(confirm.IsDanger);
        AssertColor(theme.accent, confirm.ConfirmButton.targetGraphic.color, "확인 버튼 하늘색");
        AssertColor(theme.accent, confirm.IconText.color, "\"!\" 하늘색");
        Assert.AreEqual(confirm.CancelButton.gameObject, EventSystem.current.currentSelectedGameObject, "처음 선택은 취소");
        EventSystem.current.SetSelectedGameObject(confirm.ConfirmButton.gameObject);
        yield return null;
        AssertColor(theme.accent, confirmSelect.RingOuter.color, "확인 버튼 링 하늘색");
        Assert.AreEqual(3, confirm.MessageText.textInfo.lineCount, "오프셋 재시작 문구 3줄");
        Assert.IsFalse(confirm.MessageText.isTextOverflowing);
        Assert.Greater(confirm.Panel.rect.height, twoLineHeight, "줄 수에 맞춰 패널이 늘어난다");
        Assert.IsFalse(confirm.SubMessageText.gameObject.activeSelf, "보조 문구가 없으면 숨김");
        Debug.Log($"[PopupTonePlayTests] 확인 팝업 높이: 2줄 {twoLineHeight:0} / 3줄 {confirm.Panel.rect.height:0}, 캡처+블러 {confirm.Backdrop.LastCaptureMilliseconds:0.###}ms");
        GameManager.UI.TopPopup.OnCancelRequested();
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator P2_Pause_BlurBadgeExitRed_ResumeCountdownDim()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        InGameScene.IgnoreFocusPauseForTests = true;
        yield return LoadAndWait(GameConstants.Scenes.InGame, CreateTestPlayEntry());
        yield return WaitInGamePlaying();
        UITheme theme = UITheme.Current;
        InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
        Canvas_InGame canvas = (Canvas_InGame)typeof(InGameScene).GetField("canvas", Private).GetValue(scene);

        scene.OnBack();
        Assert.IsTrue(GameManager.UI.TopPopup is Popup_Pause, "ESC → 일시정지");
        Popup_Pause pause = (Popup_Pause)GameManager.UI.TopPopup;
        yield return WaitCapture(pause.Backdrop, 1);
        Assert.IsTrue(pause.Backdrop.HasBlurTexture, "뒤 화면 블러 텍스처");
        Assert.AreEqual(Vector2.one, pause.Backdrop.Blur.ColorGrade, "채도·밝기 보정 없음");
        AssertColor(theme.popupDim, pause.Backdrop.Dim.color, "덮개 42%");
        Assert.IsTrue(pause.TestPlayBadge.activeSelf, "테스트 플레이 뱃지");
        yield return WaitRealtime(GameConstants.UI.TransitionSeconds + 0.1f);
        AssertPausePanelFits(pause);
        StringAssert.IsMatch(@"^\d+:\d\d / \d+:\d\d$", pause.TimeText.text, "진행 시간");
        Debug.Log($"[PopupTonePlayTests] 일시정지 캡처+블러 {pause.Backdrop.LastCaptureMilliseconds:0.###}ms, 시간 {pause.TimeText.text}");

        // 나가기 선택 = 빨강 막대 · 빨강 16% 바탕
        EventSystem.current.SetSelectedGameObject(pause.ExitButton.gameObject);
        yield return null;
        UIPopupSelect exitSelect = pause.ExitButton.GetComponent<UIPopupSelect>();
        Assert.IsTrue(exitSelect.Bar.gameObject.activeSelf, "나가기 막대");
        AssertColor(theme.danger, exitSelect.Bar.color, "나가기 막대 빨강");
        AssertColor(theme.DangerSelectedBackground, exitSelect.Background.color, "나가기 바탕 빨강 16%");
        UIPopupSelect resumeSelect = pause.ResumeButton.GetComponent<UIPopupSelect>();
        Assert.IsFalse(resumeSelect.Bar.gameObject.activeSelf);
        EventSystem.current.SetSelectedGameObject(pause.ResumeButton.gameObject);
        yield return null;
        AssertColor(theme.accent, resumeSelect.Bar.color, "계속 막대 하늘색");

        // 설정 창을 위에 열면 위 팝업이 다시 캡처한다 (닫으면 일시정지 그대로)
        Assert.IsTrue(GameManager.UI.OpenSettings());
        Popup_Settings settings = GameManager.UI.Get<Popup_Settings>();
        int before = settings.Backdrop.CaptureCount;
        yield return WaitCapture(settings.Backdrop, before + 1);
        Assert.IsTrue(settings.Backdrop.HasBlurTexture, "설정 창 블러");
        GameManager.UI.TopPopup.OnCancelRequested();
        yield return null;
        Assert.AreEqual(pause, GameManager.UI.TopPopup);
        Assert.IsTrue(pause.Backdrop.HasBlurTexture, "아래 팝업 블러 그대로");

        // 테스트 플레이가 아니면 뱃지 없음, 나가기 안내는 곡 선택으로
        pause.SetSong("곡", null, InGameHudRules.BuildSongInfo(ChartType.Official, GameConstants.Chart.DifficultyNormal, null, 7, false, false), false);
        Assert.IsFalse(pause.TestPlayBadge.activeSelf, "보통 플레이는 뱃지 없음");

        // 계속 → 팝업 닫힘 (블러 꺼짐) + 덮개 28% + 카운트다운 3
        pause.ResumeButton.onClick.Invoke();
        yield return null;
        Assert.IsFalse(pause.IsOpen);
        Assert.IsTrue(canvas.IsCountdownShown, "카운트다운");
        Assert.AreEqual("3", canvas.CountdownText.text);
        Assert.Greater(canvas.CountdownText.transform.GetSiblingIndex(), pause.transform.GetSiblingIndex(),
            "숫자는 닫히는 일시정지 팝업(블러)보다 위에 그린다");
        Assert.Greater(canvas.CountdownText.transform.GetSiblingIndex(), canvas.CountdownDim.transform.GetSiblingIndex(), "숫자는 덮개 위");
        Assert.IsNull(canvas.transform.Find("Txt_CountdownCaption"), "카운트다운 아래 문구 없음");
        canvas.CountdownText.ForceMeshUpdate();
        Assert.AreEqual(1, canvas.CountdownText.textInfo.characterCount, "숫자 글자가 들어감");
        Assert.IsTrue(canvas.CountdownText.isActiveAndEnabled);
        Assert.IsTrue(canvas.CountdownDim.activeSelf, "카운트다운 덮개");
        AssertColor(theme.popupCountdownDim, canvas.CountdownDim.GetComponent<Image>().color, "덮개 28%");
        yield return WaitRealtime(GameConstants.UI.TransitionSeconds + 0.1f);
        Assert.IsFalse(pause.gameObject.activeSelf, "팝업(블러) 꺼짐");
        yield return WaitRealtime(GameConstants.Pause.CountdownSeconds + 0.3f);
        Assert.IsFalse(canvas.IsCountdownShown);
        Assert.IsFalse(canvas.CountdownDim.activeSelf, "끝나면 덮개도 사라진다");

        InGameScene.IgnoreFocusPauseForTests = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator P3_Settings_SizeFixedAcrossTabs_Blur()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return LoadAndWait(GameConstants.Scenes.Title, null);
        Assert.IsTrue(GameManager.UI.OpenSettings());
        Popup_Settings settings = GameManager.UI.Get<Popup_Settings>();
        yield return WaitCapture(settings.Backdrop, 1);
        Assert.IsTrue(settings.Backdrop.HasBlurTexture);
        Assert.AreEqual(GameConstants.Settings.HintText, string.Join(" · ", settings.KeyGuide.Describe()), "키 안내 줄 = HintText");
        // 여는 연출(크기 0.9 → 1.0)이 끝난 뒤에 잰다
        yield return WaitRealtime(GameConstants.UI.TransitionSeconds + 0.1f);

        Vector3[] first = new Vector3[4];
        settings.Panel.GetWorldCorners(first);
        for (int i = 0; i < 3; i++)
        {
            settings.ShowNextTab();
            yield return null;
            Assert.AreEqual(GameConstants.Settings.Width, settings.Panel.rect.width, 0.01f, $"탭 {settings.CurrentTab} 폭");
            Assert.AreEqual(GameConstants.Settings.Height, settings.Panel.rect.height, 0.01f, $"탭 {settings.CurrentTab} 높이");
            Vector3[] corners = new Vector3[4];
            settings.Panel.GetWorldCorners(corners);
            Assert.AreEqual(first[0], corners[0], $"탭 {settings.CurrentTab} 위치 그대로");
        }

        GameManager.UI.TopPopup.OnCancelRequested();
        yield return null;
        yield return new ExitPlayMode();
    }

    // ───── 도우미 ─────

    /// <summary>패널 폭 600, 기어 가운데(화면 가운데), 키 안내 줄 모든 항목이 패널 안쪽 여백(좌우 32) 안</summary>
    private static void AssertPausePanelFits(Popup_Pause pause)
    {
        RectTransform panel = (RectTransform)pause.KeyGuide.transform.parent;
        Assert.AreEqual(GameConstants.Pause.Width, panel.rect.width, 0.01f, "일시정지 패널 폭");
        Canvas root = panel.GetComponentInParent<Canvas>().rootCanvas;
        RectTransform canvasRect = (RectTransform)root.transform;
        Vector3[] pc = new Vector3[4];
        panel.GetWorldCorners(pc);
        float scale = root.transform.lossyScale.x;
        Assert.AreEqual(canvasRect.position.x, (pc[0].x + pc[2].x) * 0.5f, 0.5f * scale, "패널 가운데 = 화면(기어) 가운데");
        float innerMin = pc[0].x + GameConstants.Pause.PanelPadding * scale;
        float innerMax = pc[2].x - GameConstants.Pause.PanelPadding * scale;
        int items = 0;
        foreach (Transform item in pause.KeyGuide.transform)
        {
            if (!item.gameObject.activeSelf) continue;
            items++;
            Vector3[] c = new Vector3[4];
            ((RectTransform)item).GetWorldCorners(c);
            Assert.GreaterOrEqual(c[0].x, innerMin - 0.01f, $"키 안내 {item.GetSiblingIndex()}번 왼쪽");
            Assert.LessOrEqual(c[2].x, innerMax + 0.01f, $"키 안내 {item.GetSiblingIndex()}번 오른쪽");
            Assert.GreaterOrEqual(c[0].y, pc[0].y - 0.01f, "키 안내 아래");
        }
        Assert.AreEqual(4, items, "키 안내 항목 4개");
        Vector3[] g = new Vector3[4];
        ((RectTransform)pause.KeyGuide.transform).GetWorldCorners(g);
        Debug.Log($"[PopupTonePlayTests] 일시정지 패널 {panel.rect.width:0}, 키 안내 줄 폭 {(g[2].x - g[0].x) / scale:0} (안쪽 {GameConstants.Pause.Width - 2f * GameConstants.Pause.PanelPadding:0})");
    }

    private static void AssertColor(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, Tolerance, message + " R");
        Assert.AreEqual(expected.g, actual.g, Tolerance, message + " G");
        Assert.AreEqual(expected.b, actual.b, Tolerance, message + " B");
        Assert.AreEqual(expected.a, actual.a, Tolerance, message + " A");
    }

    private static IEnumerator WaitCapture(PopupBackdrop backdrop, int count)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (backdrop.CaptureCount < count)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "뒤 화면 캡처 대기 시간 초과");
            yield return null;
        }
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    private static IEnumerator LoadAndWait(string sceneName, object data)
    {
        float deadline = TestSpeed.Deadline(20f);
        while (!GameManager.IsInitialized || GameManager.Scene.IsLoading)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "전역 매니저 대기 시간 초과");
            yield return null;
        }
        Assert.IsTrue(GameManager.Scene.Load(sceneName, data), $"{sceneName} 불러오기 시작");
        while (GameManager.Scene.IsLoading || SceneManager.GetActiveScene().name != sceneName)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, $"{sceneName} 대기 시간 초과");
            yield return null;
        }
        yield return null;
        yield return null;
    }

    private static IEnumerator WaitInGamePlaying()
    {
        float deadline = TestSpeed.Deadline(20f);
        while (true)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "인게임 시작 대기 시간 초과");
            InGameScene scene = Object.FindFirstObjectByType<InGameScene>();
            if (scene != null && !GameManager.Scene.IsLoading && (bool)typeof(InGameScene).GetField("isPlaying", Private).GetValue(scene)) break;
            yield return null;
        }
    }

    /// <summary>노트 하나짜리 테스트 플레이 (파일을 읽지 않고, 기록·마지막 배속을 저장하지 않는다)</summary>
    private static InGameEntryData CreateTestPlayEntry()
    {
        ChartData chart = new ChartData
        {
            version = GameConstants.Chart.CurrentVersion,
            songId = TestCharts.OfficialSongId,
            type = ChartData.TypeCustom,
            chartName = "팝업 톤 확인",
            keyCount = GameConstants.Editor.LaneCount,
            level = 1,
            timingPoints = new List<TimingPointData> { new TimingPointData { beat = 0f, bpm = 150.61f } },
            notes = new List<NoteData> { new NoteData { beat = 64f, lane = 0, type = NoteData.TypeShort } }
        };
        return new InGameEntryData
        {
            songId = chart.songId,
            chartType = ChartType.Custom,
            mode = InGameMode.TestPlay,
            testPlay = new TestPlayData { songId = chart.songId, chart = chart, startBeat = 0f }
        };
    }
}
