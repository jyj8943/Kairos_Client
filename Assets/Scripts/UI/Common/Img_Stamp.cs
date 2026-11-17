using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FC/AP 도장 하나 (기획서 3-3 ⑥ 최고 기록 카드, ② 목록 랭크 옆). 곡 선택의 기록 카드·곡 항목·채보 항목이 같이 쓴다.
/// - 도장 이미지가 아직 없어 테두리 상자(Img_Stamp) + 글자(Txt_Stamp)로 그린다. 나중에 이미지로 바꿀 때는 이 오브젝트만 고친다.
/// - FULL COMBO → "FC"(하늘색), ALL PERFECT → "AP"(금색, AP면 AP만). 없으면 오브젝트를 끈다.
/// </summary>
[RequireComponent(typeof(Image))]
public class Img_Stamp : MonoBehaviour
{
    [Tooltip("도장 글자 (Txt_Stamp)")]
    [SerializeField] private TMP_Text label;

    private Image frame;

    /// <summary>지금 도장 (확인용)</summary>
    public RecordStamp Stamp { get; private set; }

    /// <summary>
    /// 도장을 정한다. None이면 오브젝트를 끈다.
    /// </summary>
    public void SetStamp(RecordStamp stamp)
    {
        Stamp = stamp;
        gameObject.SetActive(stamp != RecordStamp.None);
        if (stamp == RecordStamp.None) return;

        if (frame == null) frame = GetComponent<Image>();
        Color color = stamp == RecordStamp.AllPerfect
            ? (Color)GameConstants.SongSelect.StampAllPerfectColor
            : (Color)GameConstants.SongSelect.StampFullComboColor;
        frame.color = color;
        if (label != null)
        {
            label.text = SongSelectRecordFormat.GetStampText(stamp);
            label.color = color;
        }
    }
}
