using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 UI (기획서 3-5 결과 11/17 · 3장 UI 톤 · 라벤더 안개). ResultScene이 Init()을 호출한다.
/// - 배경: MenuBackground (곡 선택과 같은 처리, 처음은 페이드 없이). 색·글꼴은 모두 UITheme (고정 그래픽은 UIThemeColor, 바뀌는 색은 UIThemePaint).
/// - 화면 이름 "RESULT" + 테스트 플레이 표시. 결과 패널(흰색 58%) 위에서 아래로:
///   ⑧ 곡 정보 (앨범 · 제목 · 아티스트 · 난이도 뱃지 · Lv · 배속. 나만의 채보는 CUSTOM 뱃지 + 채보 이름, 길면 채보 이름만 줄임표)
///   → ② 점수 + 이전 최고와 차이 / ① 랭크 이미지 (점수 오른쪽 240×160 칸, S~F 높이 150 정사각형, AP는 가로로 김. 기획서 3-3 「랭크·FC 표시 (11/18 개정)」)
///   → ④ 판정별 개수(메뉴용 판정 색) + FAST/SLOW 막대 / ⑥ RATE · ⑤ MAX COMBO (왼쪽 최대 / 전체, 오른쪽 ⑦ FC 이미지 폭 78 → 그 아래 NEW RECORD).
///   FC가 아니면 FC 이미지만 숨긴다 (NEW RECORD 자리는 그대로). AP면 FC도 함께 띄운다.
///   ③ NEW RECORD는 SCORE · RATE 값 오른쪽, MAX COMBO는 FC 아래. 그 항목이 신기록일 때만 (테스트 플레이는 띄우지 않음).
/// - 오른쪽 x 1300~1860은 카이로스 일러스트 자리로 비워 둔다 (KairosSlot).
/// - 하단 바: 키 안내 "F5 다시 하기 · Enter ESC 곡 선택으로"(테스트 플레이면 "에디터로") + 오른쪽 끝 버튼 2개 (다시 하기 · 곡 선택으로/에디터로).
/// - 키 (기획서 3-8): F5 다시 하기(Result/Retry), Enter 돌아가기(Result/Return), ESC 돌아가기(UIManager → ResultScene.OnBack). 팝업이 열려 있으면 무시.
///   Enter가 선택된 버튼에 따라 바뀌지 않게 버튼은 키보드 선택을 받지 않는다 (Navigation None, 처음 선택 없음).
/// </summary>
public class Canvas_Result : UIBase
{
    /// <summary>판정 한 줄 (이름 · 개수)</summary>
    [Serializable]
    public class JudgeRow
    {
        public TMP_Text nameText;
        public TMP_Text countText;
    }

    [Header("배경 · 화면 이름")]
    [SerializeField] private MenuBackground menuBackground;
    [SerializeField] private TMP_Text screenNameText;
    [SerializeField] private TMP_Text testPlayText;

    [Header("⑧ 곡 정보")]
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text songTitleText;
    [SerializeField] private TMP_Text artistText;
    [Tooltip("난이도 뱃지 (나만의 채보는 CUSTOM 뱃지)")]
    [SerializeField] private Image chartBadge;
    [SerializeField] private TMP_Text chartBadgeText;
    [Tooltip("나만의 채보 이름 (공식 채보면 숨긴다)")]
    [SerializeField] private TMP_Text chartNameText;
    [SerializeField] private LayoutElement chartNameLayout;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text speedText;

    [Header("② 점수 · ① 랭크")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject newScoreBadge;
    [Tooltip("이전 최고 + 차이 (BEST 933602 +12334). 이전 기록이 없거나 테스트 플레이면 숨긴다")]
    [SerializeField] private TMP_Text bestText;
    [Tooltip("랭크 이미지 (RankArea/Img_Rank, 높이 150 · 칸 폭 240)")]
    [SerializeField] private Img_Rank rankImage;

    [Header("④ 판정별 개수 · FAST/SLOW")]
    [SerializeField] private JudgeRow perfectRow;
    [SerializeField] private JudgeRow greatRow;
    [SerializeField] private JudgeRow goodRow;
    [SerializeField] private JudgeRow missRow;
    [SerializeField] private TMP_Text fastText;
    [SerializeField] private TMP_Text slowText;
    [Tooltip("막대 바탕 (FAST·SLOW가 둘 다 0이면 구분선 색 빈 막대)")]
    [SerializeField] private Image fastSlowTrack;
    [SerializeField] private Image fastFill;
    [SerializeField] private Image slowFill;

    [Header("⑥ RATE · ⑤ MAX COMBO")]
    [SerializeField] private TMP_Text rateText;
    [SerializeField] private GameObject newRateBadge;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text comboTotalText;
    [Tooltip("⑦ MAX COMBO 카드 오른쪽 FC 이미지 (폭 78)")]
    [SerializeField] private Img_FullCombo fullComboImage;
    [SerializeField] private GameObject newComboBadge;

    [Header("버튼")]
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryLabelText;
    [SerializeField] private TMP_Text retryKeyText;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text returnLabelText;
    [SerializeField] private TMP_Text returnKeyText;

    [Header("입력")]
    [Tooltip("입력 에셋의 Result/Retry (F5)")]
    [SerializeField] private InputActionReference retryAction;
    [Tooltip("입력 에셋의 Result/Return (Enter)")]
    [SerializeField] private InputActionReference returnAction;

    [Tooltip("화면 아래쪽 키 안내 줄 (KeyGuideBar 프리팹)")]
    [SerializeField] private KeyGuideBar keyGuideBar;

    private Action onRetry;
    private Action onReturn;
    private bool isInputEnabled;
    private bool isLeaving;

    /// <summary>키 안내 줄 (확인용)</summary>
    public KeyGuideBar KeyGuide => keyGuideBar;

    /// <summary>
    /// 결과를 표시하고 버튼·키 안내 줄을 연결한다. 버튼은 키보드 선택을 받지 않는다 (Enter는 항상 Result/Return).
    /// </summary>
    public void Init(ResultData result, Action onRetry, Action onReturn)
    {
        this.onRetry = onRetry;
        this.onReturn = onReturn;
        isLeaving = false;

        UIThemeColor.ApplyAll(gameObject);
        if (menuBackground != null)
        {
            menuBackground.Init();
            menuBackground.SetIllustration(result.background != null ? result.background : result.cover, true);
        }

        if (screenNameText != null)
        {
            screenNameText.text = GameConstants.Result.ScreenName;
            screenNameText.characterSpacing = GameConstants.SongSelect.ScreenNameCharacterSpacing;
        }
        testPlayText.text = result.isAutoPlay ? GameConstants.Play.AutoTestPlayLabel : GameConstants.Play.TestPlayLabel;
        testPlayText.gameObject.SetActive(result.isTestPlay);

        ShowSongInfo(result);
        ShowScoreAndRank(result);
        ShowJudges(result);
        ShowRateAndCombo(result);
        InitButtons(result.isTestPlay);
        InitKeyGuide(result.isTestPlay);
    }

    // ───── 표시 ─────

    /// <summary>⑧ 곡 정보: 앨범 · 제목 · 아티스트 · 뱃지(난이도 / CUSTOM + 채보 이름) · Lv · 배속</summary>
    private void ShowSongInfo(ResultData result)
    {
        UITheme theme = UITheme.Current;
        coverImage.sprite = result.cover;
        coverImage.color = result.cover != null ? Color.white : theme.emptyCover;
        songTitleText.text = result.songTitle ?? string.Empty;
        artistText.text = result.artist ?? string.Empty;

        bool isCustom = result.chartType == ChartType.Custom;
        chartBadgeText.text = isCustom ? GameConstants.Play.ChartBadgeCustomText : (result.difficulty ?? string.Empty).ToUpperInvariant();
        UIThemePaint.Badge(chartBadge, chartBadgeText, isCustom ? theme.difficultyCustom : theme.GetDifficulty(result.difficulty));

        chartNameText.gameObject.SetActive(isCustom);
        if (isCustom)
        {
            chartNameText.text = result.chartName ?? string.Empty;
            if (chartNameLayout != null)
                chartNameLayout.preferredWidth = Mathf.Min(chartNameText.GetPreferredValues(chartNameText.text).x, GameConstants.Result.MaxChartNameWidth);
        }

        levelText.text = string.Format(GameConstants.SongSelect.LevelFormat, result.level);
        speedText.text = string.Format(CultureInfo.InvariantCulture, GameConstants.Result.SpeedFormat, result.scrollMultiplier);
    }

    /// <summary>② 점수 + NEW RECORD + 이전 최고와 차이 / ① 랭크 이미지</summary>
    private void ShowScoreAndRank(ResultData result)
    {
        UITheme theme = UITheme.Current;
        scoreText.text = SongSelectRecordFormat.FormatScore(result.score);
        newScoreBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewBestScore, result.isTestPlay));

        string diff = ResultFormat.FormatScoreDiff(result.hadPreviousRecord, result.isTestPlay, result.score, result.previousBestScore);
        bestText.gameObject.SetActive(diff != null);
        if (diff != null)
        {
            Color diffColor = result.score > result.previousBestScore ? theme.accent : theme.textDescription;
            bestText.text = string.Format(GameConstants.Result.BestFormat, SongSelectRecordFormat.FormatScore(result.previousBestScore)) +
                            $" <color=#{ColorUtility.ToHtmlStringRGB(diffColor)}><b>{diff}</b></color>";
        }

        rankImage.SetRank(result.rank);
    }

    /// <summary>④ 판정별 개수 (이름은 메뉴용 판정 색, 숫자는 기본 글자색) + FAST/SLOW 막대</summary>
    private void ShowJudges(ResultData result)
    {
        UITheme theme = UITheme.Current;
        SetJudgeRow(perfectRow, GameConstants.Play.PerfectText, result.perfectCount, theme.judgePerfect);
        SetJudgeRow(greatRow, GameConstants.Play.GreatText, result.greatCount, theme.judgeGreat);
        SetJudgeRow(goodRow, GameConstants.Play.GoodText, result.goodCount, theme.judgeGood);
        SetJudgeRow(missRow, GameConstants.Play.MissText, result.missCount, theme.judgeMiss);

        fastText.text = string.Format(GameConstants.Result.FastFormat, ResultFormat.FormatCount(result.fastCount));
        fastText.color = theme.judgeFast;
        slowText.text = string.Format(GameConstants.Result.SlowFormat, ResultFormat.FormatCount(result.slowCount));
        slowText.color = theme.judgeSlow;

        bool hasData = ResultFormat.TryGetFastRatio(result.fastCount, result.slowCount, out float ratio);
        fastSlowTrack.color = theme.divider;
        fastFill.gameObject.SetActive(hasData && ratio > 0f);
        slowFill.gameObject.SetActive(hasData && ratio < 1f);
        fastFill.color = theme.judgeFast;
        slowFill.color = theme.judgeSlow;
        SetHorizontalRange(fastFill.rectTransform, 0f, ratio);
        SetHorizontalRange(slowFill.rectTransform, ratio, 1f);
        FastRatio = hasData ? ratio : -1f;
    }

    private static void SetJudgeRow(JudgeRow row, string name, int count, Color color)
    {
        if (row == null) return;
        row.nameText.text = name;
        row.nameText.color = color;
        row.countText.text = ResultFormat.FormatCount(count);
        UIThemePaint.Text(row.countText, UITextLevel.Primary);
    }

    private static void SetHorizontalRange(RectTransform rect, float min, float max)
    {
        rect.anchorMin = new Vector2(min, 0f);
        rect.anchorMax = new Vector2(max, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>⑥ RATE (소수 둘째 자리 %) · ⑤ MAX COMBO (최대 / 전체 = 노트 수 + 롱노트 유지 틱 수) + ⑦ FC (AP면 FC도) + 각 NEW RECORD</summary>
    private void ShowRateAndCombo(ResultData result)
    {
        rateText.text = SongSelectRecordFormat.FormatRate(result.accuracy);
        newRateBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewBestAccuracy, result.isTestPlay));
        maxComboText.text = SongSelectRecordFormat.FormatCombo(result.maxCombo);
        comboTotalText.text = string.Format(GameConstants.Result.MaxComboTotalFormat, SongSelectRecordFormat.FormatCombo(result.maxPossibleCombo));
        fullComboImage.SetFullCombo(result.isFullCombo || result.isAllPerfect);
        newComboBadge.SetActive(ResultFormat.ShowNewRecord(result.isNewMaxCombo, result.isTestPlay));
    }

    // ───── 버튼 · 키 안내 ─────

    private void InitButtons(bool isTestPlay)
    {
        retryLabelText.text = GameConstants.Play.RetryLabel;
        returnLabelText.text = isTestPlay ? GameConstants.KeyGuide.ToEditor : GameConstants.KeyGuide.ToSongSelect;
        if (retryKeyText != null) retryKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(retryAction));
        if (returnKeyText != null) returnKeyText.text = KeyGuideBar.GetKeyText(KeyGuideKey.FromAction(returnAction, GameConstants.KeyGuide.EnterKey));

        retryButton.onClick.RemoveListener(OnClickRetry);
        retryButton.onClick.AddListener(OnClickRetry);
        returnButton.onClick.RemoveListener(OnClickReturn);
        returnButton.onClick.AddListener(OnClickReturn);
        SetInteractable(true);

        // 클릭해도 선택되지 않게 (선택된 버튼이 있으면 Enter가 Submit으로 그 버튼도 누른다)
        retryButton.navigation = new Navigation { mode = Navigation.Mode.None };
        returnButton.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    /// <summary>키 안내 줄: F5 다시 하기 · Enter ESC 곡 선택으로 (테스트 플레이면 "에디터로")</summary>
    private void InitKeyGuide(bool isTestPlay)
    {
        if (keyGuideBar == null)
        {
            Debug.LogWarning("[Canvas_Result] keyGuideBar가 연결되지 않았습니다.", this);
            return;
        }

        string returnLabel = isTestPlay ? GameConstants.KeyGuide.ToEditor : GameConstants.KeyGuide.ToSongSelect;
        keyGuideBar.Init(new List<KeyGuideEntry>
        {
            new KeyGuideEntry(GameConstants.KeyGuide.Retry, KeyGuideKey.FromAction(retryAction)),
            new KeyGuideEntry(returnLabel, KeyGuideKey.FromAction(returnAction, GameConstants.KeyGuide.EnterKey), KeyGuideKey.Fixed(GameConstants.KeyGuide.EscKey))
                .WithId(GameConstants.KeyGuide.ReturnId)
        });
    }

    // ───── 확인용 ─────

    /// <summary>NEW RECORD 표시 여부 (점수 · RATE · MAX COMBO, 확인용)</summary>
    public bool IsNewScoreShown => newScoreBadge != null && newScoreBadge.activeSelf;
    public bool IsNewRateShown => newRateBadge != null && newRateBadge.activeSelf;
    public bool IsNewComboShown => newComboBadge != null && newComboBadge.activeSelf;

    /// <summary>곡 정보 뱃지 글자·색, 채보 이름 표시 여부 (확인용)</summary>
    public string ChartBadgeLabel => chartBadgeText != null ? chartBadgeText.text : null;
    public Color ChartBadgeColor => chartBadge != null ? chartBadge.color : Color.clear;
    public bool IsChartNameShown => chartNameText != null && chartNameText.gameObject.activeSelf;
    public string ChartNameLabel => chartNameText != null ? chartNameText.text : null;

    /// <summary>랭크 이미지 · FC 이미지 (확인용)</summary>
    public Img_Rank RankImage => rankImage;
    public Img_FullCombo FullComboImage => fullComboImage;

    /// <summary>FAST 막대 비율 (빈 막대면 -1, 확인용)</summary>
    public float FastRatio { get; private set; }

    /// <summary>돌아가기 버튼 글자 (확인용)</summary>
    public string ReturnLabel => returnLabelText != null ? returnLabelText.text : null;

    // ───── 입력 ─────

    /// <summary>
    /// F5·Enter 입력을 받기 시작한다. 씬의 OnEnter에서 호출한다.
    /// </summary>
    public void EnableInput()
    {
        if (isInputEnabled) return;

        if (retryAction == null || returnAction == null)
        {
            Debug.LogWarning("[Canvas_Result] retryAction·returnAction이 연결되지 않아 키로 다시 하기·돌아가기를 할 수 없습니다.", this);
            return;
        }

        retryAction.action.performed += OnRetryKey;
        returnAction.action.performed += OnReturnKey;
        retryAction.action.Enable();
        returnAction.action.Enable();
        isInputEnabled = true;
    }

    /// <summary>
    /// F5·Enter 입력 구독을 해제한다. 씬의 OnExit에서 호출한다.
    /// 액션은 다른 곳과 함께 쓰는 입력 에셋의 것이라 Disable하지 않고 구독만 끊는다.
    /// </summary>
    public void DisableInput()
    {
        if (!isInputEnabled) return;

        retryAction.action.performed -= OnRetryKey;
        returnAction.action.performed -= OnReturnKey;
        isInputEnabled = false;
    }

    /// <summary>ESC (ResultScene.OnBack): 돌아가기 버튼과 같다</summary>
    public void RequestReturn()
    {
        OnClickReturn();
    }

    /// <summary>씬 이동을 시작하면 버튼을 막아 중복 실행을 막는다.</summary>
    public void SetInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        returnButton.interactable = interactable;
        isLeaving = !interactable;
    }

    /// <summary>
    /// 다른 코드가 버튼을 선택해 두었어도 다음 프레임에 풀어, Enter가 Submit으로 그 버튼까지 누르지 않게 한다
    /// (Enter는 항상 Result/Return 하나만 실행).
    /// </summary>
    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && (selected == retryButton.gameObject || selected == returnButton.gameObject))
            eventSystem.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        DisableInput();
        if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        if (returnButton != null) returnButton.onClick.RemoveListener(OnClickReturn);
    }

    private void OnRetryKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickRetry();
    }

    private void OnReturnKey(InputAction.CallbackContext context)
    {
        if (IsShortcutBlocked) return;
        OnClickReturn();
    }

    /// <summary>팝업이 열려 있으면 키를 받지 않는다</summary>
    private static bool IsShortcutBlocked => UIManager.HasInstance && GameManager.UI.HasOpenPopup;

#if UNITY_EDITOR
    /// <summary>테스트 전용 (Unity 에디터): 다시 하기·돌아가기가 실행된 횟수 (Enter 한 번에 한 번만 실행되는지 확인)</summary>
    public int TestRetryCount { get; private set; }
    public int TestReturnCount { get; private set; }
#endif

    private void OnClickRetry()
    {
        if (isLeaving) return;
#if UNITY_EDITOR
        TestRetryCount++;
#endif
        onRetry?.Invoke();
    }

    private void OnClickReturn()
    {
        if (isLeaving) return;
#if UNITY_EDITOR
        TestReturnCount++;
#endif
        onReturn?.Invoke();
    }
}
