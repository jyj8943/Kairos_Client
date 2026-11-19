using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 인게임 화면 UI (기획서 3-4, 배치 11/13 · 톤 11/18 라벤더 안개 · 11/19 배경·BGA 개편). InGameScene이 Init()을 호출한다.
/// - 왼쪽 패널(곡 정보 카드 · 판정 패널 · 배속·밝기 카드)은 라벤더 불투명 #F4F1F8 + 흰색 1px 테두리 (배경 밝기와 상관없이 고정, 뒤 흐림 없음).
/// - 왼쪽 ⑧ 곡 정보 카드(Img_SongInfo):
///   윗줄 Img_Album(곡 앨범 150×150, 없으면 빈 앨범 색) + 오른쪽 3줄 (곡 제목 + 테스트 플레이 뱃지 / 난이도·CUSTOM 뱃지 + 채보 이름 + Lv. / "BPM" + 지금 곡 시간 위치의 BPM),
///   아랫줄 카드 폭 전체 곡 진행 바(Img_Progress/Img_ProgressFill). 무엇을 보일지는 InGameHudRules.BuildSongInfo가 정한다.
/// - 왼쪽 ⑥ 판정 패널(Img_JudgePanel): 판정 개수·FAST/SLOW 개수·RATE 박스(Img_JudgePanel 스크립트) + SCORE 박스(이 스크립트).
/// - 왼쪽 아래 배속·밝기 카드(Img_SpeedBrightnessCard): 배속 · 배경 밝기 값과 키. 늘 보이고 값이 바뀌면 숫자·막대만 바뀐다.
/// - 기어 위쪽 콤보(Txt_Combo: 오름·쉬면 숨기기 애니메이션), 판정선 위 판정 글자 이미지(Img_Judge)와 그 위 FAST/SLOW 이미지(Img_FastSlow), 안내 문구, 재개 카운트다운.
///   판정 글자·FAST/SLOW는 색·그라데이션·빛이 들어 있는 이미지(JudgeSprites, Resources/Images/InGame/Judge/)라 색을 칠하지 않는다 (11/19).
/// - 재개 카운트다운 (기획서 3-7 ③): 일시정지 메뉴가 닫힌 뒤 블러 없이 덮개(Img_CountdownDim, rgba(36,32,46) 28%)만 깔고
///   기어 가운데에 흰 숫자(180, 하늘색 빛 = TMP Underlay) 3 → 2 → 1. 끝나면 덮개도 숨긴다.
///   보일 때마다 덮개·숫자를 맨 앞(마지막 형제)으로 옮긴다: 일시정지 팝업은 열 때마다 맨 앞으로 가므로(UIPopup.Open),
///   그대로 두면 닫히는 일시정지 팝업(뒤 화면 캡처 블러 + 패널)이 페이드 동안 숫자를 덮는다.
///   FAST/SLOW를 띄울지는 InGameScene이 FastSlowRules로 정해 넘긴다 (이 스크립트는 규칙을 판단하지 않는다).
///   콤보·판정 글자·FAST/SLOW 애니메이션은 TickAnimations로만 흐른다 (일시정지·카운트다운 중에는 InGameScene이 부르지 않아 멈춤).
/// - 색은 모두 UITheme에서 Init 때 읽는다 (씬에 색을 직렬화하지 않는다):
///   왼쪽 패널은 메뉴 묶음(글자·판정 이름·난이도 뱃지·포인트색) + 인게임 패널 색(inGamePanel 등), 기어 위 콤보는 인게임 묶음(밝은 색).
///   판정 패널 PERFECT 이름은 금 → 분홍 가로 그라데이션 (UIThemePaint.JudgeText).
///   바뀌지 않는 그래픽은 UIThemeColor 역할로 칠하고(UIThemeColor.ApplyAll), 바뀌는 색은 여기서 칠한다.
/// - 플레이 중 배속·배경 밝기를 바꾸면 배속·밝기 카드의 값만 바뀐다 (따로 뜨는 글자 없음).
/// - 결과는 결과 씬(ResultScene)이 보여준다.
/// - 플레이 중 숫자는 TMP SetText(형식, 숫자)로 바꿔 GC 할당을 만들지 않는다 (점수는 곡 선택·결과와 같은 FormatScore, 바뀔 때만). 진행 바는 채움의 anchorMax.x만 바꾼다.
/// </summary>
public class Canvas_InGame : UIBase
{
    [Header("⑧ 곡 정보 카드 (Img_SongInfo)")]
    [Tooltip("앨범 칸 (Img_Album, RoundedRect10 + Mask). 앨범이 없으면 이 칸의 빈 앨범 색이 보인다")]
    [SerializeField] private Image albumFrame;
    [Tooltip("앨범 이미지 (Img_Album/Img_AlbumCover). 진입 데이터 곡의 SongData.Cover")]
    [SerializeField] private Image albumImage;
    [Tooltip("곡 제목 (Txt_SongTitle, 넘치면 제목만 줄임표)")]
    [SerializeField] private TMP_Text songTitleText;
    [Tooltip("채보 뱃지 배경 (Img_ChartBadge). 공식은 메뉴용 난이도 색, 나만의 채보는 CUSTOM 색")]
    [SerializeField] private Image chartBadgeImage;
    [Tooltip("채보 뱃지 글자 (Img_ChartBadge/Txt_ChartBadge, 흰색)")]
    [SerializeField] private TMP_Text chartBadgeText;
    [Tooltip("나만의 채보 이름 (Txt_ChartName). 나만의 채보일 때만 켜고, 넘치면 이 글자만 줄임표")]
    [SerializeField] private TMP_Text chartNameText;
    [Tooltip("레벨 (Txt_Level, \"Lv.3\"). 항상 보인다")]
    [SerializeField] private TMP_Text levelText;
    [Tooltip("3줄 BPM 라벨 (BpmRow/Txt_BpmTitle, \"BPM\" 14 보조 글자색)")]
    [SerializeField] private TMP_Text bpmTitleText;
    [Tooltip("3줄 BPM 값 (BpmRow/Txt_Bpm, \"150.61\" 22 기본 글자색). 지금 곡 시간 위치의 BPM")]
    [SerializeField] private TMP_Text bpmText;
    [Tooltip("곡 진행 바 채움 (Img_Progress/Img_ProgressFill). 왼쪽 기준 anchorMax.x = 비율로 채운다")]
    [SerializeField] private RectTransform progressFill;
    [Tooltip("테스트 플레이 뱃지 (Img_TestPlay, 포인트색). 테스트 플레이일 때만 켠다")]
    [SerializeField] private GameObject testPlayBadge;
    [Tooltip("테스트 플레이 뱃지 글자 (Img_TestPlay/Txt_TestPlay)")]
    [SerializeField] private TMP_Text testPlayText;

    [Header("FULL COMBO / ALL PERFECT 연출 (ClearEffect)")]
    [SerializeField] private ClearEffect clearEffect;

    [Header("배속·밝기 카드 (Img_SpeedBrightnessCard, 왼쪽 아래)")]
    [SerializeField] private Img_SpeedBrightnessCard speedBrightnessCard;

    [Header("⑥ 판정 패널 (Img_JudgePanel)")]
    [Tooltip("판정 개수·FAST/SLOW·RATE. 점수는 scoreText")]
    [SerializeField] private Img_JudgePanel judgePanel;
    [Tooltip("점수 (Img_JudgePanel/Img_ScoreBox/Txt_Score, 오른쪽 아래)")]
    [SerializeField] private TMP_Text scoreText;

    [Header("화면 가운데 (기어 위)")]
    [Tooltip("판정 글자 이미지 (Img_Judge, 판정선 위). 등장·유지·퇴장 애니메이션을 맡는다")]
    [FormerlySerializedAs("judgeText")]
    [SerializeField] private Img_Judge judgeImage;
    [Tooltip("판정 글자 위 FAST / SLOW 이미지 (Img_FastSlow, GREAT · GOOD일 때만). 자기 시간만큼 유지 뒤 사라진다")]
    [FormerlySerializedAs("fastSlowText")]
    [SerializeField] private Img_FastSlow fastSlowImage;
    [Tooltip("콤보 숫자 (Combo/Txt_Combo). 오름·쉬면 숨기기 애니메이션을 맡는다")]
    [SerializeField] private Txt_Combo comboText;
    [Tooltip("콤보 숫자 위 제목 (Combo/Txt_ComboTitle). 움직이지 않는다")]
    [SerializeField] private TMP_Text comboTitleText;
    [Tooltip("불러오기 실패 등 안내 문구 (Txt_Message)")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("일시정지 후 재개 카운트다운 숫자 (Txt_Countdown, 화면 가운데)")]
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("카운트다운 덮개 (Img_CountdownDim, 화면 전체, UIThemeColor 역할 PopupCountdownDim)")]
    [SerializeField] private GameObject countdownDim;

    /// <summary>테스트 플레이 뱃지 글자 좌우 여백 (px)</summary>
    private const float BadgePaddingX = 12f;

    /// <summary>점수 칸에 마지막으로 쓴 값 (같으면 다시 쓰지 않는다)</summary>
    private int shownScore = -1;

    /// <summary>BPM 칸에 마지막으로 쓴 값 (같으면 다시 쓰지 않는다, 음수면 아직 안 씀)</summary>
    private float shownBpm = -1f;

    /// <summary>진행 바에 마지막으로 쓴 비율 (같은 값이면 다시 쓰지 않는다)</summary>
    private float shownProgress = -1f;

    /// <summary>카운트다운 숫자 빛 (Underlay를 켠 머티리얼 사본, 처음 한 번 만든다)</summary>
    private Material countdownGlowMaterial;

    /// <summary>카운트다운 빛 퍼짐 · 부드러움 (TMP Underlay)</summary>
    private const float CountdownGlowDilate = 0.4f;
    private const float CountdownGlowSoftness = 0.7f;

    /// <summary>카운트다운이 보이는 중이면 true (확인용)</summary>
    public bool IsCountdownShown => countdownText != null && countdownText.gameObject.activeSelf;

    /// <summary>카운트다운 덮개 (확인용)</summary>
    public GameObject CountdownDim => countdownDim;

    /// <summary>카운트다운 숫자 (확인용)</summary>
    public TMP_Text CountdownText => countdownText;

    /// <summary>
    /// 테마 색을 칠하고, 곡 정보 카드·테스트 플레이 뱃지를 채우고, 점수·콤보·진행 바를 비운다.
    /// </summary>
    /// <param name="cover">곡 앨범 (SongData.Cover). 없으면 빈 앨범 색</param>
    /// <param name="keys">배속·밝기 카드 키 이름 (입력 에셋 바인딩에서 읽은 것)</param>
    /// <param name="brightness">시작 배경 밝기 (%, 배속·밝기 카드에 표시)</param>
    /// <param name="bpm">시작 위치의 BPM (곡 정보 3줄)</param>
    /// <param name="chartType">채보 종류 (ChartData.type). 공식이면 난이도 뱃지, 나만의 채보면 CUSTOM 뱃지 + 채보 이름</param>
    /// <param name="difficulty">난이도 이름 (ChartData.difficulty)</param>
    /// <param name="isAutoPlay">자동 플레이면 테스트 플레이 표시에 "자동 플레이"를 함께 쓴다</param>
    /// <param name="scrollMultiplier">시작 배속 (배속·밝기 카드에 표시)</param>
    /// <param name="score">이번 판의 점수 계산 (판정 패널의 시작 값: 판정 0개, 정확도 0.00%)</param>
    public void Init(string songTitle, Sprite cover, ChartType chartType, string difficulty, string chartName, int level,
        bool isTestPlay, bool isAutoPlay, float scrollMultiplier, int brightness, float bpm, SpeedBrightnessKeys keys, ScoreCalculator score)
    {
        UITheme theme = UITheme.Current;
        UIThemeColor.ApplyAll(gameObject);

        SongInfoDisplay info = InGameHudRules.BuildSongInfo(chartType, difficulty, chartName, level, isTestPlay, isAutoPlay);
        SetAlbum(cover);
        songTitleText.text = songTitle ?? string.Empty;
        chartBadgeText.text = info.BadgeText;
        UIThemePaint.Badge(chartBadgeImage, chartBadgeText, GetBadgeColor(theme, info));
        chartNameText.text = info.ChartName;
        chartNameText.gameObject.SetActive(info.ShowChartName);
        levelText.text = info.LevelText;
        testPlayText.text = info.TestPlayText;
        testPlayBadge.SetActive(info.ShowTestPlay);
        // 줄 안에서 줄어들지 않을 것(테스트 플레이 뱃지·Lv.)은 글자 폭을 최소 폭으로 둔다.
        // 넘치면 최소 폭 0인 곡 제목·채보 이름만 줄어들고 줄임표가 붙는다
        FitWidthToText(testPlayBadge, testPlayText, BadgePaddingX * 2f);
        FitWidthToText(levelText.gameObject, levelText, 0f);

        if (bpmTitleText != null)
        {
            bpmTitleText.text = GameConstants.Play.BpmLabel;
            FitWidthToText(bpmTitleText.gameObject, bpmTitleText, 0f);
        }
        shownBpm = -1f;
        SetBpm(bpm);
        if (speedBrightnessCard != null) speedBrightnessCard.Init(keys, scrollMultiplier, brightness);
        shownProgress = -1f;
        SetProgress(0f);
        judgeImage.ResetView();
        if (clearEffect != null) clearEffect.ResetView();
        if (fastSlowImage != null) fastSlowImage.ResetView();
        shownJudge = JudgeType.Miss;
        shownFastSlow = FastSlow.None;
        if (judgePanel != null) judgePanel.Init(score);
        if (comboTitleText != null)
        {
            comboTitleText.text = GameConstants.Play.ComboTitleText;
            comboTitleText.color = theme.comboTitle;
        }
        comboText.SetColor(theme.comboNumber);
        comboText.ResetView();
        shownScore = -1;
        SetScore(0);
        messageText.text = string.Empty;
        InitCountdown(theme);
        HideCountdown();
    }

    /// <summary>카운트다운 숫자 색·크기·빛과 아래 문구</summary>
    private void InitCountdown(UITheme theme)
    {
        if (countdownText != null)
        {
            countdownText.color = theme.countdownNumber;
            countdownText.fontSize = GameConstants.Pause.CountdownFontSize;
            if (countdownGlowMaterial == null)
            {
                countdownGlowMaterial = countdownText.fontMaterial;
                countdownGlowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                countdownGlowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                countdownGlowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
                countdownGlowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, CountdownGlowDilate);
                countdownGlowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, CountdownGlowSoftness);
                countdownText.UpdateMeshPadding();
            }
            countdownGlowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, theme.countdownGlow);
        }
    }

    /// <summary>뱃지 색 (메뉴용 UITheme): 나만의 채보(테스트 플레이 포함)는 CUSTOM, 공식은 난이도 이름의 색 (모르는 이름도 CUSTOM). 글자는 흰색</summary>
    private static Color GetBadgeColor(UITheme theme, SongInfoDisplay info)
    {
        return info.IsCustom ? theme.difficultyCustom : theme.GetDifficulty(info.BadgeText);
    }

    /// <summary>
    /// 줄(HorizontalLayoutGroup) 안 항목의 최소·선호 폭을 글자 폭 + 여백으로 맞춘다 (LayoutElement가 있을 때만).
    /// </summary>
    private static void FitWidthToText(GameObject target, TMP_Text text, float padding)
    {
        if (target == null || text == null) return;
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null) return;
        float width = Mathf.Ceil(text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x + padding);
        element.minWidth = width;
        element.preferredWidth = width;
    }

    /// <summary>앨범을 넣는다. 없으면 앨범 이미지를 끄고 앨범 칸의 빈 앨범 색(UIThemeColor EmptyCover)이 보인다</summary>
    private void SetAlbum(Sprite cover)
    {
        if (albumImage == null) return;
        albumImage.sprite = cover;
        albumImage.color = Color.white;
        albumImage.preserveAspect = false;
        albumImage.gameObject.SetActive(cover != null);
    }

    /// <summary>점수를 쓴다 (곡 선택·결과와 같은 형식: 쉼표 없음). 같은 값이면 다시 쓰지 않는다</summary>
    private void SetScore(int score)
    {
        if (score == shownScore) return;
        shownScore = score;
        scoreText.text = SongSelectRecordFormat.FormatScore(score);
    }

    /// <summary>앨범 이미지 (확인용, 앨범이 없으면 null)</summary>
    public Sprite AlbumSprite => albumImage != null && albumImage.gameObject.activeSelf ? albumImage.sprite : null;

    /// <summary>채보 뱃지 바탕 색 (확인용)</summary>
    public Color ChartBadgeColor => chartBadgeImage != null ? chartBadgeImage.color : Color.clear;

    /// <summary>점수 글자 (확인용)</summary>
    public string ScoreText => scoreText != null ? scoreText.text : string.Empty;

    /// <summary>판정 패널 (확인용)</summary>
    public Img_JudgePanel JudgePanel => judgePanel;

    /// <summary>배속·밝기 카드 (확인용)</summary>
    public Img_SpeedBrightnessCard SpeedBrightnessCard => speedBrightnessCard;

    /// <summary>배속·밝기 카드의 배속 값을 바꾼다 (플레이 중 1 / 2 키)</summary>
    public void SetSpeed(float scrollMultiplier)
    {
        if (speedBrightnessCard != null) speedBrightnessCard.SetSpeed(scrollMultiplier);
    }

    /// <summary>배속·밝기 카드의 밝기 값·막대를 바꾼다 (플레이 중 PgUp·PgDn)</summary>
    public void SetBrightness(int brightness)
    {
        if (speedBrightnessCard != null) speedBrightnessCard.SetBrightness(brightness);
    }

    /// <summary>
    /// 곡 정보 3줄의 BPM을 바꾼다 (SongData.FormatBpm 형식). 값이 같으면 글자를 다시 만들지 않는다 (InGameScene이 매 프레임 불러도 된다).
    /// </summary>
    public void SetBpm(float bpm)
    {
        if (bpmText == null || bpm == shownBpm) return;
        shownBpm = bpm;
        bpmText.text = SongData.FormatBpm(bpm);
    }

    /// <summary>곡 정보 3줄 BPM 글자 (확인용, 예: "BPM 150.61")</summary>
    public string BpmLine => (bpmTitleText != null ? bpmTitleText.text : string.Empty) + " " + (bpmText != null ? bpmText.text : string.Empty);

    /// <summary>
    /// 곡 진행 바를 채운다 (0~1). InGameScene이 플레이 중 매 프레임 부른다 (일시정지·카운트다운 중에는 부르지 않아 멈춘다).
    /// 화면 1px보다 작은 변화는 건너뛰어 매 프레임 메시를 다시 만들지 않는다.
    /// </summary>
    public void SetProgress(float ratio)
    {
        if (progressFill == null) return;
        if (shownProgress >= 0f && Mathf.Abs(ratio - shownProgress) < ProgressStep && ratio < 1f) return;

        shownProgress = ratio;
        progressFill.anchorMax = new Vector2(ratio, progressFill.anchorMax.y);
    }

    /// <summary>진행 바를 다시 칠하는 최소 변화 (바 폭 572px 기준 약 1px)</summary>
    private const float ProgressStep = 1f / 572f;

    /// <summary>진행 바 채움 비율 (확인용)</summary>
    public float Progress => progressFill != null ? progressFill.anchorMax.x : 0f;

    /// <summary>
    /// 재개 카운트다운 숫자를 보여준다.
    /// </summary>
    public void ShowCountdown(int count)
    {
        countdownText.SetText("{0}", count);
        if (countdownDim != null)
        {
            countdownDim.SetActive(true);
            countdownDim.transform.SetAsLastSibling();
        }
        countdownText.gameObject.SetActive(true);
        countdownText.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 재개 카운트다운 숫자를 숨긴다.
    /// </summary>
    public void HideCountdown()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (countdownDim != null) countdownDim.SetActive(false);
    }

    /// <summary>
    /// 안내 문구를 표시한다 (불러오기 실패 등). Init 전에도 쓸 수 있다.
    /// </summary>
    public void ShowMessage(string message)
    {
        messageText.text = message;
    }

    /// <summary>
    /// 판정 결과 하나를 표시한다 (판정 글자·FAST/SLOW·콤보·점수). 숏노트·롱노트 시작·유지 틱·롱노트 종합 모두 부른다.
    /// 판정 글자는 그 판정 이미지로 바꾸고 애니메이션을 처음부터 다시 재생한다. 어떤 등급을 띄울지는 InGameScene이 정해 넘긴다.
    /// </summary>
    /// <param name="judge">띄울 등급 (JudgeResult.JudgeTextType: 틱은 시작 판정)</param>
    /// <param name="fastSlow">판정 글자 위에 띄울 방향 (FastSlowRules.GetDisplay). None이면 앞의 FAST/SLOW를 건드리지 않는다</param>
    public void ShowJudge(JudgeType judge, FastSlow fastSlow, int combo, int score)
    {
        shownJudge = judge;
        judgeImage.Show(JudgeSprites.Get(judge));

        ShowFastSlow(fastSlow);
        ShowCombo(combo, score);
    }

    /// <summary>
    /// 판정 글자 위 FAST / SLOW를 띄운다: 이미지를 바꾸고 유지 시간을 처음부터 센다.
    /// None이면 아무것도 하지 않는다 (앞의 FAST/SLOW는 남은 시간만큼 그대로 떠 있다).
    /// 같은 프레임에 여러 번 부르면 마지막 것이 보인다.
    /// </summary>
    public void ShowFastSlow(FastSlow fastSlow)
    {
        if (fastSlowImage == null || fastSlow == FastSlow.None) return;
        shownFastSlow = fastSlow;
        fastSlowImage.Show(JudgeSprites.Get(fastSlow));
    }

    /// <summary>판정 패널 숫자(판정 개수·FAST/SLOW·정확도)를 바꾼다. 숏노트·롱노트 종합 판정 때 부른다</summary>
    public void RefreshJudgePanel(ScoreCalculator score)
    {
        if (judgePanel != null) judgePanel.Refresh(score);
    }

    private JudgeType shownJudge = JudgeType.Miss;
    private FastSlow shownFastSlow = FastSlow.None;

    /// <summary>마지막으로 띄운 판정 (확인용, 보이는지는 Judge.IsVisible)</summary>
    public JudgeType JudgeShown => shownJudge;

    /// <summary>지금 보이는 FAST / SLOW (확인용, 숨김이면 None)</summary>
    public FastSlow FastSlowShown => fastSlowImage != null && fastSlowImage.IsVisible ? shownFastSlow : FastSlow.None;

    /// <summary>판정 글자 이미지 (확인용)</summary>
    public Img_Judge Judge => judgeImage;

    /// <summary>FAST / SLOW 이미지 (확인용)</summary>
    public Img_FastSlow FastSlowImage => fastSlowImage;

    /// <summary>
    /// 판정 글자는 그대로 두고 콤보·점수만 바꾼다.
    /// </summary>
    public void ShowCombo(int combo, int score)
    {
        comboText.SetCombo(combo);
        SetScore(score);
    }

    /// <summary>
    /// 콤보·판정 글자·FAST/SLOW 애니메이션 시간을 진행한다. InGameScene이 플레이 중 매 프레임 부른다
    /// (일시정지·재개 카운트다운 중에는 부르지 않아 셋 다 멈추고, 풀리면 이어서 진행한다).
    /// </summary>
    public void TickAnimations(float deltaTime)
    {
        comboText.Tick(deltaTime);
        judgeImage.Tick(deltaTime);
        if (fastSlowImage != null) fastSlowImage.Tick(deltaTime);
    }

    /// <summary>
    /// FULL COMBO / ALL PERFECT 연출을 고른다 (모든 노트 판정 순간, InGameScene이 ClearEffectRules로 정해 넘긴다). None이면 띄우지 않는다.
    /// </summary>
    public void PlayClear(ClearKind kind)
    {
        if (clearEffect != null) clearEffect.Play(kind);
    }

    /// <summary>FULL COMBO / ALL PERFECT 연출 시간을 맞춘다 (모든 노트 판정 뒤 지난 곡 시간, 초)</summary>
    public void SetClearTime(float elapsed)
    {
        if (clearEffect != null) clearEffect.SetTime(elapsed);
    }

    /// <summary>FULL COMBO / ALL PERFECT 연출 (확인용)</summary>
    public ClearEffect Clear => clearEffect;

    /// <summary>콤보 표시 (확인용)</summary>
    public Txt_Combo Combo => comboText;
}
