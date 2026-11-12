using System;
using UnityEngine;

/// <summary>
/// 지금 설정 값을 들고 있는 전역 매니저. Managers_DontDestroy 프리팹의 "SettingsManager" 오브젝트에 붙이고, Bootstrapper가 Init()을 호출한다.
/// - 들고 있는 값: 판정 오프셋(OffsetMs). 음량은 SoundManager, 해상도·창 모드는 Screen이 들고 있으므로 여기로 옮기지 않는다.
/// - 값은 인스턴스 필드에만 둔다 (static 값 없음). 도메인 리로드를 끈 설정에서도 Play를 다시 시작하면 프리팹을 새로 만들어 기본값(0)으로 시작한다.
/// - 값이 바뀌면 OnChanged를 알린다.
/// - 파일 읽기·쓰기는 PlayerDataManager가 한다: Init은 기본값(0)으로 두고, 뒤이어 PlayerDataManager.Init이 PlayerData.json의 값을 넣는다.
///   저장은 설정 창을 닫을 때 PlayerDataManager.Save()가 지금 값을 모아 쓴다 (값이 바뀔 때마다 쓰지 않는다).
/// </summary>
public class SettingsManager : MonoSingleton<SettingsManager>, IGameModule
{
    private int offsetMs;

    /// <summary>값이 바뀌면 알린다 (같은 값을 넣으면 알리지 않는다)</summary>
    public event Action OnChanged;

    /// <summary>
    /// 판정 오프셋 (ms, -200 ~ +200, 5ms 단위). 넣는 값은 범위로 자르고 5ms 단위로 반올림한다.
    /// + = 노트를 늦게 (소리가 늦게 들리거나 늦게 칠 때), - = 노트를 빠르게. 인게임은 판이 시작될 때 한 번 읽는다.
    /// </summary>
    public int OffsetMs
    {
        get => offsetMs;
        set
        {
            int next = ClampOffset(value);
            if (next == offsetMs) return;
            offsetMs = next;
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// 기본값(0)으로 시작한다. 저장된 값은 뒤이어 PlayerDataManager.Init이 넣는다.
    /// </summary>
    public void Init()
    {
        offsetMs = 0;
    }

    /// <summary>오프셋을 범위(-200 ~ +200)로 자르고 5ms 단위로 맞춘다</summary>
    public static int ClampOffset(int value)
    {
        int step = GameConstants.Settings.OffsetStepMs;
        int snapped = Mathf.RoundToInt(value / (float)step) * step;
        return Mathf.Clamp(snapped, GameConstants.Settings.MinOffsetMs, GameConstants.Settings.MaxOffsetMs);
    }
}
