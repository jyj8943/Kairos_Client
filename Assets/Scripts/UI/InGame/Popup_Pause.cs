using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 일시정지 메뉴 (기획서 3-7 ③ · 3-4). InGameScene.unity의 Canvas_InGame 아래에 두고, InGameScene이 Init()을 호출한다.
/// - 모양 (11/18 팝업 공통 톤): 폭 480 패널을 기어 위 가운데(x 720 · y 176)에 둔다. 위에서부터
///   제목 "일시정지 PAUSE" + 짧은 선 (테스트 플레이면 오른쪽에 "테스트 플레이" 뱃지) → 지금 곡 카드 → 메뉴 4개 → 키 안내 줄.
/// - 지금 곡 카드: 앨범 52 · 곡 제목(길면 …) · 난이도 뱃지(메뉴용 난이도 색, 나만의 채보는 CUSTOM) + Lv · 진행 시간 "1:04 / 2:31" · 진행 바(포인트색).
///   곡 정보는 SetSong(InGameScene이 곡을 불러온 뒤 한 번), 진행 시간은 SetTime(열 때의 곡 시간)으로 채운다.
/// - 메뉴는 위에서부터 계속 · 재시작 · 설정 · 나가기 (한글 22 + 영문 12, 높이 60, 간격 14). 계속 오른쪽 "3초 후 재개", 나가기 오른쪽은 가는 곳 (곡 선택으로 / 에디터로).
///   선택 표시는 UIPopupSelect(Row): 포인트색 막대 + 18% 바탕. 나가기만 빨강 (막대·글자 + 빨강 16% 바탕).
/// - 처음 선택은 계속, ↑↓로 이동, Enter 실행. ESC(팝업이 열려 있을 때 UIManager가 부름)는 "계속"과 같다. 바깥 클릭은 무시한다.
/// - 버튼을 누르면 InGameScene이 넘긴 콜백만 부른다. 돌아갈 곳(곡 선택·에디터)은 이 팝업이 알지 않는다 (안내 글자만 SetSong으로 받는다).
/// - 아래쪽 키 안내 줄(KeyGuideBar): ↑↓ 이동 · Enter 선택 · F5 다시 하기 · ESC 계속. F5(즉시 다시 하기)는 InGameScene이 받는다.
/// - 설정 버튼은 공용 설정 창(GameManager.UI.OpenSettings)을 이 메뉴 위에 연다. 설정을 닫으면 이 메뉴로 돌아온다 (인게임은 멈춘 그대로).
/// - 계속 뒤 카운트다운(덮개 28% + 숫자)은 Canvas_InGame이 맡는다 (이 팝업은 닫힌다).
/// </summary>
public class Popup_Pause : UIPopup
{
    [Header("제목")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("영문 제목 (Txt_TitleEn, PAUSE)")]
    [SerializeField] private TMP_Text titleEnText;
    [Tooltip("테스트 플레이 뱃지 (Img_TestPlay, 포인트색). 테스트 플레이일 때만 켠다")]
    [SerializeField] private GameObject testPlayBadge;
    [SerializeField] private TMP_Text testPlayText;

    [Header("지금 곡 카드 (Img_SongCard)")]
    [Tooltip("앨범 (Img_Album/Img_AlbumCover). 없으면 빈 앨범 색 칸만 보인다")]
    [SerializeField] private Image albumImage;
    [SerializeField] private TMP_Text songTitleText;
    [Tooltip("난이도 뱃지 바탕 (Img_ChartBadge). 공식은 메뉴용 난이도 색, 나만의 채보는 CUSTOM 색")]
    [SerializeField] private Image chartBadgeImage;
    [SerializeField] private TMP_Text chartBadgeText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("진행 시간 (Txt_Time, \"1:04 / 2:31\")")]
    [SerializeField] private TMP_Text timeText;
    [Tooltip("진행 바 채움 (Img_Progress/Img_ProgressFill). 왼쪽 기준 anchorMax.x = 비율")]
    [SerializeField] private RectTransform progressFill;

    [Header("메뉴")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private TMP_Text resumeText;
    [SerializeField] private TMP_Text resumeEnText;
    [Tooltip("계속 오른쪽 \"3초 후 재개\"")]
    [SerializeField] private TMP_Text resumeHintText;
    [SerializeField] private Button restartButton;
    [SerializeField] private TMP_Text restartText;
    [SerializeField] private TMP_Text restartEnText;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Text settingsText;
    [SerializeField] private TMP_Text settingsEnText;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text exitText;
    [SerializeField] private TMP_Text exitEnText;
    [Tooltip("나가기 오른쪽 가는 곳 (곡 선택으로 / 에디터로)")]
    [SerializeField] private TMP_Text exitHintText;
    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private Action onResume;
    private Action onRestart;
    private Action onExit;

    /// <summary>
    /// 문구와 버튼 콜백, ↑↓ 이동을 연결하고 닫힌 상태로 둔다.
    /// </summary>
    /// <param name="onResume">계속 (ESC도 같다)</param>
    /// <param name="onRestart">재시작</param>
    /// <param name="onExit">나가기</param>
    /// <param name="quickRestartAction">F5 즉시 다시 하기 액션 (Gameplay/Restart, 키 안내 줄에 키 이름을 보여줄 때만 쓴다)</param>
    public void Init(Action onResume, Action onRestart, Action onExit, InputActionReference quickRestartAction)
    {
        this.onResume = onResume;
        this.onRestart = onRestart;
        this.onExit = onExit;

        InitPopupTone();
        titleText.text = GameConstants.Pause.Title;
        if (titleEnText != null) titleEnText.text = GameConstants.Pause.TitleEn;
        if (testPlayText != null) testPlayText.text = GameConstants.Pause.TestPlayBadge;
        if (testPlayBadge != null) testPlayBadge.SetActive(false);
        resumeText.text = GameConstants.Pause.ResumeLabel;
        restartText.text = GameConstants.Pause.RestartLabel;
        settingsText.text = GameConstants.Pause.SettingsLabel;
        exitText.text = GameConstants.Pause.ExitLabel;
        SetText(resumeEnText, GameConstants.Pause.ResumeLabelEn);
        SetText(restartEnText, GameConstants.Pause.RestartLabelEn);
        SetText(settingsEnText, GameConstants.Pause.SettingsLabelEn);
        SetText(exitEnText, GameConstants.Pause.ExitLabelEn);
        SetText(resumeHintText, GameConstants.Pause.ResumeHint);
        SetText(exitHintText, GameConstants.Pause.ExitHintSongSelect);
        InitKeyGuide(quickRestartAction);

        // 나가기만 위험 표시 (빨강 막대·글자·바탕), 나머지는 포인트색
        foreach (Button button in new[] { resumeButton, restartButton, settingsButton, exitButton })
        {
            UIPopupSelect select = button.GetComponent<UIPopupSelect>();
            if (select == null) continue;
            select.SetDanger(button == exitButton);
            select.SetHighlighted(false);
        }
        UITheme theme = UITheme.Current;
        foreach (TMP_Text hint in new[] { resumeHintText, exitHintText })
        {
            if (hint != null) hint.color = theme.textMuted;
        }

        resumeButton.onClick.RemoveListener(OnClickResume);
        resumeButton.onClick.AddListener(OnClickResume);
        restartButton.onClick.RemoveListener(OnClickRestart);
        restartButton.onClick.AddListener(OnClickRestart);
        exitButton.onClick.RemoveListener(OnClickExit);
        exitButton.onClick.AddListener(OnClickExit);
        settingsButton.onClick.RemoveListener(OnClickSettings);
        settingsButton.onClick.AddListener(OnClickSettings);
        settingsButton.interactable = true;

        // ↑↓: 계속 → 재시작 → 설정 → 나가기
        resumeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restartButton };
        restartButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = resumeButton, selectOnDown = settingsButton };
        settingsButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = restartButton, selectOnDown = exitButton };
        exitButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = settingsButton };

        SetTime(0, 0f);
        HideImmediate();
    }

    /// <summary>
    /// 지금 곡 카드와 테스트 플레이 표시를 채운다 (InGameScene이 곡을 불러온 뒤 한 번).
    /// </summary>
    /// <param name="info">곡 정보 표시 (InGameHudRules.BuildSongInfo: 뱃지 문구·색 · Lv.)</param>
    /// <param name="isTestPlay">테스트 플레이면 제목 오른쪽 뱃지를 켜고 나가기 안내를 "에디터로"</param>
    public void SetSong(string songTitle, Sprite cover, SongInfoDisplay info, bool isTestPlay)
    {
        UITheme theme = UITheme.Current;
        if (albumImage != null)
        {
            albumImage.sprite = cover;
            albumImage.color = Color.white;
            albumImage.gameObject.SetActive(cover != null);
        }
        SetText(songTitleText, songTitle ?? string.Empty);
        SetText(chartBadgeText, info.BadgeText);
        UIThemePaint.Badge(chartBadgeImage, chartBadgeText, info.IsCustom ? theme.difficultyCustom : theme.GetDifficulty(info.BadgeText));
        SetText(levelText, info.LevelText);
        if (testPlayBadge != null) testPlayBadge.SetActive(isTestPlay);
        SetText(exitHintText, isTestPlay ? GameConstants.Pause.ExitHintEditor : GameConstants.Pause.ExitHintSongSelect);
    }

    /// <summary>
    /// 진행 시간 "1:04 / 2:31"과 진행 바를 채운다 (열 때의 곡 시간).
    /// </summary>
    public void SetTime(double songTime, float songLength)
    {
        SetText(timeText, FormatProgress(songTime, songLength));
        if (progressFill != null)
        {
            Vector2 max = progressFill.anchorMax;
            max.x = InGameHudRules.ProgressRatio(songTime, songLength);
            progressFill.anchorMax = max;
        }
    }

    /// <summary>진행 시간 글자 (순수 함수): "지금 / 곡 길이", 둘 다 m:ss (초 아래 버림), 지금은 곡 길이를 넘지 않는다</summary>
    public static string FormatProgress(double songTime, float songLength)
    {
        float length = Mathf.Max(0f, songLength);
        float now = Mathf.Clamp((float)songTime, 0f, length);
        return string.Format(GameConstants.Pause.ProgressFormat, TimeFormat.ToMinSec(now), TimeFormat.ToMinSec(length));
    }

    /// <summary>키 안내 줄: ↑↓ 이동 · Enter 선택 · F5 다시 하기 · ESC 계속</summary>
    private void InitKeyGuide(InputActionReference quickRestartAction)
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Popup_Pause] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Move, KeyGuideKey.Fixed(GameConstants.KeyGuide.UpDownKeys)),
            new KeyGuideEntry(GameConstants.KeyGuide.Select, KeyGuideKey.Fixed(GameConstants.KeyGuide.EnterKey)),
            new KeyGuideEntry(GameConstants.KeyGuide.Retry, KeyGuideKey.FromAction(quickRestartAction)),
            new KeyGuideEntry(GameConstants.KeyGuide.Resume, KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
        });
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>테스트 플레이 뱃지 (확인용)</summary>
    public GameObject TestPlayBadge => testPlayBadge;

    /// <summary>나가기 버튼 (확인용)</summary>
    public Button ExitButton => exitButton;

    /// <summary>계속 버튼 (확인용)</summary>
    public Button ResumeButton => resumeButton;

    /// <summary>곡 제목 (확인용)</summary>
    public TMP_Text SongTitleText => songTitleText;

    /// <summary>진행 시간 (확인용)</summary>
    public TMP_Text TimeText => timeText;

    /// <summary>ESC = 계속</summary>
    public override void OnCancelRequested()
    {
        OnClickResume();
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnClickResume);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnClickRestart);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnClickExit);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnClickSettings);
    }

    /// <summary>설정: 공용 설정 창을 이 메뉴 위에 연다 (맨 위 팝업일 때만)</summary>
    private void OnClickSettings()
    {
        if (!IsOpen || GameManager.UI.TopPopup != this) return;
        GameManager.UI.OpenSettings();
    }

    private void OnClickResume()
    {
        if (!IsOpen) return;
        onResume?.Invoke();
    }

    private void OnClickRestart()
    {
        if (!IsOpen) return;
        onRestart?.Invoke();
    }

    private void OnClickExit()
    {
        if (!IsOpen) return;
        onExit?.Invoke();
    }
}
