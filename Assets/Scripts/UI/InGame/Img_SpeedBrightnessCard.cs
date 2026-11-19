using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>배속·밝기 카드의 키 이름 네 개 (입력 에셋 바인딩에서 읽은 것)</summary>
public readonly struct SpeedBrightnessKeys
{
    public readonly string SpeedDown;
    public readonly string SpeedUp;
    public readonly string BrightnessDown;
    public readonly string BrightnessUp;

    public SpeedBrightnessKeys(string speedDown, string speedUp, string brightnessDown, string brightnessUp)
    {
        SpeedDown = speedDown;
        SpeedUp = speedUp;
        BrightnessDown = brightnessDown;
        BrightnessUp = brightnessUp;
    }
}

/// <summary>
/// 인게임 왼쪽 아래 배속·밝기 카드 (Img_SpeedBrightnessCard, 기획서 3-4 「배경 · BGA 와이어프레임 (11/19)」). 늘 보인다.
/// Canvas_InGame.Init이 Init()을 부르고, 배속·밝기가 바뀌면 SetSpeed·SetBrightness를 부른다.
/// - 위치: x 60~680, y 960~1048 (620×88, 판정 패널과 폭이 같고 20px 아래), 안쪽 여백 좌우 12.
/// - 모든 요소를 한 줄에 두고 세로 가운데(카드 위에서 44)를 맞춘다 (11/19 와이어프레임).
/// - 왼쪽 칸 (너비 238): 이름 묶음 SpeedName[배속(15, Bold) / SPEED(10, 자간 0.2em)] → 값 "x5.5"(26, Bold, 오른쪽 정렬 고정 폭) → 키 [1][2] /
///   가운데 세로 구분선 1×44 /
///   오른쪽 칸: 이름 묶음 BrightnessName[밝기 / BG] → 단계 막대 4칸(16×8, 간격 4, 25%마다 한 칸 #3E9BE0, 나머지 rgba(44,40,56) 14%)
///   → 값 "100%"(26, Bold, 오른쪽 정렬 고정 폭) → 키 [PgDn][PgUp].
/// - 값 칸 폭은 x10.0 · 100%가 들어가는 고정 폭이라 값이 바뀌어도 키 위치가 흔들리지 않는다.
/// - 값이 바뀌면 숫자·막대만 바뀐다 (강조 색·나타남 효과 없음). 같은 값이면 다시 쓰지 않는다.
/// - 키 이름은 입력 에셋 바인딩에서 읽어 넘겨받는다 (Page Up/Down은 카드 폭에 맞게 PgUp/PgDn으로 줄인다, ShortKeyName).
/// - 바탕·글자·막대 색은 UITheme (UIThemeColor 역할 + 막대 켜진 칸은 여기서 칠함). 씬에 색을 직렬화하지 않는다.
/// </summary>
public class Img_SpeedBrightnessCard : MonoBehaviour
{
    [Header("왼쪽 칸: 배속")]
    [Tooltip("이름 (SpeedName/Txt_SpeedTitle, \"배속\")")]
    [SerializeField] private TMP_Text speedTitle;
    [Tooltip("이름 아래 영문 (SpeedName/Txt_SpeedTitleEn, \"SPEED\")")]
    [SerializeField] private TMP_Text speedTitleEn;
    [Tooltip("배속 값 (Txt_SpeedValue, \"x5.5\")")]
    [SerializeField] private TMP_Text speedValue;
    [Tooltip("배속 내리기 키 (Img_SpeedDownKey/Txt_SpeedDownKey)")]
    [SerializeField] private TMP_Text speedDownKey;
    [Tooltip("배속 올리기 키 (Img_SpeedUpKey/Txt_SpeedUpKey)")]
    [SerializeField] private TMP_Text speedUpKey;

    [Header("오른쪽 칸: 밝기")]
    [Tooltip("이름 (BrightnessName/Txt_BrightnessTitle, \"밝기\")")]
    [SerializeField] private TMP_Text brightnessTitle;
    [Tooltip("이름 아래 영문 (BrightnessName/Txt_BrightnessTitleEn, \"BG\")")]
    [SerializeField] private TMP_Text brightnessTitleEn;
    [Tooltip("단계 막대 칸 (BrightnessBars/Img_BrightnessBar0~3, 왼쪽부터)")]
    [SerializeField] private Image[] brightnessBars;
    [Tooltip("밝기 값 (Txt_BrightnessValue, \"100%\")")]
    [SerializeField] private TMP_Text brightnessValue;
    [Tooltip("밝기 내리기 키 (Img_BrightnessDownKey/Txt_BrightnessDownKey)")]
    [SerializeField] private TMP_Text brightnessDownKey;
    [Tooltip("밝기 올리기 키 (Img_BrightnessUpKey/Txt_BrightnessUpKey)")]
    [SerializeField] private TMP_Text brightnessUpKey;

    private float shownSpeed = -1f;
    private int shownBrightness = -1;

    /// <summary>배속 값 글자 (확인용)</summary>
    public string SpeedText => speedValue != null ? speedValue.text : string.Empty;

    /// <summary>밝기 값 글자 (확인용)</summary>
    public string BrightnessText => brightnessValue != null ? brightnessValue.text : string.Empty;

    /// <summary>켜진 막대 칸 수 (확인용)</summary>
    public int FilledBarCount { get; private set; }

    /// <summary>값 글자 두 개 (확인용: 배속, 밝기)</summary>
    public TMP_Text[] ValueTexts => new[] { speedValue, brightnessValue };

    /// <summary>
    /// 한 줄에 놓인 요소 (확인용, 왼쪽부터): 이름 묶음 · 값 · 키 두 개 / 구분선 / 이름 묶음 · 막대 · 값 · 키 두 개.
    /// 키 글자는 키 칸(Img_*Key), 이름은 묶음(SpeedName · BrightnessName)을 돌려준다.
    /// </summary>
    public RectTransform[] RowItems
    {
        get
        {
            Transform card = transform;
            string[] names = { "SpeedName", "Txt_SpeedValue", "Img_SpeedDownKey", "Img_SpeedUpKey", "Img_SpeedBrightnessDivider",
                "BrightnessName", "BrightnessBars", "Txt_BrightnessValue", "Img_BrightnessDownKey", "Img_BrightnessUpKey" };
            RectTransform[] items = new RectTransform[names.Length];
            for (int i = 0; i < names.Length; i++) items[i] = card.Find(names[i]) as RectTransform;
            return items;
        }
    }

    /// <summary>키 글자 네 개 (확인용: 배속 내림·올림, 밝기 내림·올림)</summary>
    public string[] KeyTexts => new[]
    {
        speedDownKey != null ? speedDownKey.text : string.Empty,
        speedUpKey != null ? speedUpKey.text : string.Empty,
        brightnessDownKey != null ? brightnessDownKey.text : string.Empty,
        brightnessUpKey != null ? brightnessUpKey.text : string.Empty
    };

    /// <summary>
    /// 라벨·키 글자를 쓰고 시작 배속·밝기를 보여 준다 (테마 색은 Canvas_InGame이 UIThemeColor.ApplyAll로 먼저 칠한다).
    /// </summary>
    public void Init(SpeedBrightnessKeys keys, float scrollMultiplier, int brightness)
    {
        if (speedTitle != null) speedTitle.text = GameConstants.Play.SpeedCardLabel;
        if (speedTitleEn != null) speedTitleEn.text = GameConstants.Play.SpeedCardLabelEn;
        if (brightnessTitle != null) brightnessTitle.text = GameConstants.Play.BrightnessCardLabel;
        if (brightnessTitleEn != null) brightnessTitleEn.text = GameConstants.Play.BrightnessCardLabelEn;
        SetKey(speedDownKey, keys.SpeedDown);
        SetKey(speedUpKey, keys.SpeedUp);
        SetKey(brightnessDownKey, keys.BrightnessDown);
        SetKey(brightnessUpKey, keys.BrightnessUp);

        shownSpeed = -1f;
        shownBrightness = -1;
        SetSpeed(scrollMultiplier);
        SetBrightness(brightness);
    }

    /// <summary>배속 값만 바꾼다 (같은 값이면 그대로)</summary>
    public void SetSpeed(float scrollMultiplier)
    {
        if (speedValue == null || Mathf.Approximately(scrollMultiplier, shownSpeed)) return;
        shownSpeed = scrollMultiplier;
        speedValue.SetText(GameConstants.Play.InGameSpeedFormat, scrollMultiplier);
    }

    /// <summary>밝기 값·단계 막대만 바꾼다 (같은 값이면 그대로)</summary>
    public void SetBrightness(int brightness)
    {
        if (brightness == shownBrightness) return;
        shownBrightness = brightness;
        if (brightnessValue != null) brightnessValue.SetText(GameConstants.Play.BrightnessFormat, brightness);

        UITheme theme = UITheme.Current;
        FilledBarCount = BackgroundBrightnessRules.FilledBars(brightness);
        if (brightnessBars == null) return;
        for (int i = 0; i < brightnessBars.Length; i++)
        {
            if (brightnessBars[i] == null) continue;
            brightnessBars[i].color = i < FilledBarCount ? theme.inGameBrightnessBarOn : theme.inGameBrightnessBarOff;
        }
    }

    /// <summary>
    /// 키 이름을 카드에 맞게 줄인다 (순수 함수): "Page Up" → "PgUp", "Page Down" → "PgDn". 나머지는 그대로.
    /// </summary>
    public static string ShortKeyName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return string.Empty;
        switch (keyName.Replace(" ", string.Empty).ToUpperInvariant())
        {
            case "PAGEUP": return GameConstants.Play.BrightnessUpKeyFallback;
            case "PAGEDOWN": return GameConstants.Play.BrightnessDownKeyFallback;
            default: return keyName;
        }
    }

    private static void SetKey(TMP_Text text, string keyName)
    {
        if (text != null) text.text = ShortKeyName(keyName);
    }
}
