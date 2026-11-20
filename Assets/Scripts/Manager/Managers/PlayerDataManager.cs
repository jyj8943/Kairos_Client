using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 유저 플레이 데이터(PlayerData.json: 설정 + 공식·나만의 채보 기록) 전역 매니저 (기획서 2-4).
/// Managers_DontDestroy 프리팹의 "PlayerDataManager" 오브젝트에 붙이고, Bootstrapper가 SoundManager·SettingsManager 다음에 Init()을 호출한다.
/// - 파일 읽기·쓰기는 PlayerDataStore, 값 정리는 PlayerDataRules, 기록 규칙은 PlayerRecordBook이 맡는다 (모두 순수 C#).
/// - 불러오기 (Init):
///   파일이 없으면 기본값으로 시작한다. 설정은 지금 상태(SoundManager 음량, SettingsManager 오프셋, 지금 화면)를 그대로 쓰고 화면을 바꾸지 않는다.
///   읽기에 실패하면 .bak으로 다시 시도하고, 둘 다 실패하면 깨진 파일을 broken 이름으로 옮기고 기본값으로 시작한다 (경고 로그).
///   불러온 설정은 SettingsManager·SoundManager·Screen.SetResolution에 적용한다 (화면은 파일에서 읽었을 때만).
/// - 저장 (Save): 지금 설정(음량·오프셋·화면)을 모아 settings에 넣고 savedAt을 갱신해 쓴다. 설정 창을 닫을 때, 결과 씬에서 기록을 넣을 때,
///   에디터에서 나만의 채보를 저장해 기록을 지울 때, 마지막 배속이 바뀌었을 때(SetLastScrollMultiplier) 부른다.
/// - 값은 인스턴스 필드에만 둔다 (static 값 없음). 도메인 리로드를 끈 설정에서도 Play를 다시 시작하면 파일에서 다시 읽는다.
/// </summary>
public class PlayerDataManager : MonoSingleton<PlayerDataManager>, IGameModule
{
    private const string LogPrefix = "[PlayerDataManager]";

    private PlayerDataStore store;
    private PlayerData data;
    private PlayerRecordBook book;

    /// <summary>저장 파일 경로 (persistentDataPath/PlayerData.json)</summary>
    public string FilePath => store != null ? store.FilePath : string.Empty;

    /// <summary>마지막 불러오기 결과 (확인용)</summary>
    public PlayerDataStore.LoadSource LoadedFrom { get; private set; }

    /// <summary>마지막 Save가 성공했으면 true (확인용)</summary>
    public bool LastSaveSucceeded { get; private set; }

    /// <summary>지금 들고 있는 기록 목록 (확인용, 고치지 않는다)</summary>
    public IReadOnlyList<ChartRecordData> Records => book != null ? (IReadOnlyList<ChartRecordData>)book.Records : Array.Empty<ChartRecordData>();

    /// <summary>
    /// 파일을 읽어 값을 정리하고 설정을 적용한다.
    /// </summary>
    public void Init()
    {
        store = new PlayerDataStore(Path.Combine(Application.persistentDataPath, GameConstants.PlayerData.FileName));
        LoadedFrom = store.Load(out PlayerData loaded);

        switch (LoadedFrom)
        {
            case PlayerDataStore.LoadSource.NoFile:
                data = new PlayerData { settings = CollectSettings() };
                Debug.Log($"{LogPrefix} 저장 파일이 없어 기본값으로 시작합니다 (지금 설정 그대로, 화면은 바꾸지 않음): {store.FilePath}", this);
                break;
            case PlayerDataStore.LoadSource.Broken:
                data = new PlayerData { settings = CollectSettings() };
                Debug.LogWarning($"{LogPrefix} 저장 파일과 .bak을 모두 읽지 못해 기본값으로 시작합니다 ({store.LastError}). " +
                                 $"깨진 파일: {store.LastBrokenPath ?? "(옮기지 못함)"}", this);
                break;
            default:
                data = loaded;
                if (LoadedFrom == PlayerDataStore.LoadSource.Backup)
                    Debug.LogWarning($"{LogPrefix} 저장 파일을 읽지 못해 .bak으로 되살렸습니다 ({store.LastError}). " +
                                     $"깨진 파일: {store.LastBrokenPath ?? "(없음)"}", this);
                break;
        }

        List<string> fixes = new List<string>();
        data = PlayerDataRules.Sanitize(data, fixes);
        if (fixes.Count > 0) Debug.LogWarning($"{LogPrefix} 저장 값 정리: {string.Join(", ", fixes)}", this);

        book = new PlayerRecordBook(data.records);

        bool fromFile = LoadedFrom == PlayerDataStore.LoadSource.Main || LoadedFrom == PlayerDataStore.LoadSource.Backup;
        ApplySettings(data.settings, fromFile);

        SettingsSaveData s = data.settings;
        Debug.Log($"{LogPrefix} 불러오기 {LoadedFrom}: 기록 {book.Records.Count}개, 음량 {s.masterVolume}/{s.musicVolume}/{s.sfxVolume}, " +
                  $"오프셋 {s.offsetMs}ms, 화면 {s.resolutionWidth}×{s.resolutionHeight} {s.windowMode}{(fromFile ? "" : " (적용 안 함)")}, " +
                  $"마지막 배속 x{data.lastSelection.scrollMultiplier:0.0}, 경로 {store.FilePath}", this);
    }

    // ───── 설정 ─────

    /// <summary>불러온 설정을 적용한다. 화면은 파일에서 읽었을 때만 바꾼다.</summary>
    private void ApplySettings(SettingsSaveData s, bool applyScreen)
    {
        if (SettingsManager.HasInstance) GameManager.Settings.OffsetMs = s.offsetMs;

        if (SoundManager.HasInstance)
        {
            GameManager.Sound.MasterVolume = Popup_Settings.ToVolume01(s.masterVolume);
            GameManager.Sound.MusicVolume = Popup_Settings.ToVolume01(s.musicVolume);
            GameManager.Sound.SfxVolume = Popup_Settings.ToVolume01(s.sfxVolume);
        }

        if (applyScreen)
        {
            FullScreenMode mode = PlayerDataRules.ParseWindowMode(s.windowMode);
            Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, mode);
        }
    }

    /// <summary>
    /// 지금 설정을 모은다: 음량(SoundManager, 0~100 반올림), 오프셋(SettingsManager), 화면(Screen.width·height·fullScreenMode).
    /// 화면 값이 목록에 없으면 Sanitize가 기본값으로 바꾼다 (Unity 에디터에서는 Game 뷰 크기가 들어간다).
    /// </summary>
    private static SettingsSaveData CollectSettings()
    {
        SettingsSaveData s = new SettingsSaveData();
        if (SoundManager.HasInstance)
        {
            s.masterVolume = Popup_Settings.ToSliderValue(GameManager.Sound.MasterVolume);
            s.musicVolume = Popup_Settings.ToSliderValue(GameManager.Sound.MusicVolume);
            s.sfxVolume = Popup_Settings.ToSliderValue(GameManager.Sound.SfxVolume);
        }
        if (SettingsManager.HasInstance) s.offsetMs = GameManager.Settings.OffsetMs;

        s.resolutionWidth = Screen.width;
        s.resolutionHeight = Screen.height;
        s.windowMode = Screen.fullScreenMode.ToString();
        return s;
    }

    /// <summary>
    /// 지금 설정을 모아 저장한다. 성공·실패를 로그로 남긴다.
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool Save()
    {
        if (store == null || data == null)
        {
            Debug.LogWarning($"{LogPrefix} Init 전이라 저장하지 않습니다.", this);
            return false;
        }

        data.version = GameConstants.PlayerData.CurrentVersion;
        data.settings = CollectSettings();
        PlayerDataRules.SanitizeSettings(data.settings);
        // 기록은 PlayerRecordBook이 고치는 목록 그대로 쓴다 (불러올 때 이미 정리했고, 이후에는 Submit·Remove로만 바뀐다)
        data.records = book.Records;
        data.savedAt = PlayerDataRules.FormatTime(DateTime.UtcNow);

        LastSaveSucceeded = store.Save(data);
        if (LastSaveSucceeded)
            Debug.Log($"{LogPrefix} 저장했습니다: 기록 {data.records.Count}개, savedAt {data.savedAt}, {store.FilePath}", this);
        else
            Debug.LogError($"{LogPrefix} 저장하지 못했습니다: {store.LastError} ({store.FilePath})", this);
        return LastSaveSucceeded;
    }

    // ───── 마지막 선택 (곡 선택) ─────

    /// <summary>저장된 마지막 배속. Init 전이거나 저장된 값이 없으면 기본 배속(1.0)</summary>
    public float LastScrollMultiplier => data != null && data.lastSelection != null
        ? data.lastSelection.scrollMultiplier
        : GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>
    /// 마지막 배속을 바꾸고 바로 저장한다. 값이 같으면(0.1 단위로 정리한 뒤) 저장하지 않는다.
    /// 일반 플레이에 들어갈 때·떠날 때만 부른다 (테스트 플레이·자동 플레이는 부르지 않는다, PlayerDataRules.ShouldSaveLastScrollMultiplier).
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastScrollMultiplier(float value)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();
        if (!PlayerDataRules.TrySetLastScrollMultiplier(data.lastSelection, value)) return false;

        Debug.Log($"{LogPrefix} 마지막 배속 x{data.lastSelection.scrollMultiplier:0.0} 저장", this);
        return Save();
    }

    /// <summary>저장된 마지막 에디터 테스트 플레이 배속 (정리한 값). Init 전이거나 저장된 값이 없으면 기본 배속(1.0)</summary>
    public float LastEditorTestPlayMultiplier => data != null && data.lastSelection != null
        ? PlayerDataRules.SanitizeScrollMultiplier(data.lastSelection.editorTestPlayMultiplier)
        : GameConstants.Play.DefaultScrollMultiplier;

    /// <summary>
    /// 마지막 에디터 테스트 플레이 배속을 바꾸고 바로 저장한다 (에디터 왼쪽 패널 ◀ ▶). 값이 같으면 저장하지 않는다. 인게임 배속과 따로다.
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastEditorTestPlayMultiplier(float value)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();
        if (!PlayerDataRules.TrySetEditorTestPlayMultiplier(data.lastSelection, value)) return false;

        Debug.Log($"{LogPrefix} 마지막 테스트 플레이 배속 x{data.lastSelection.editorTestPlayMultiplier:0.0} 저장", this);
        return Save();
    }

    /// <summary>저장된 에디터 「테스트 후 결과 보기」 값. Init 전이거나 저장된 값이 없으면 처음 값(true)</summary>
    public bool LastEditorShowTestResult => data != null && data.lastSelection != null
        ? data.lastSelection.editorShowTestResult
        : GameConstants.Editor.ShowTestResultDefaultOn;

    /// <summary>
    /// 에디터 「테스트 후 결과 보기」 값을 바꾸고 바로 저장한다 (에디터 왼쪽 패널 스위치). 값이 같으면 저장하지 않는다.
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastEditorShowTestResult(bool value)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();
        if (!PlayerDataRules.TrySetEditorShowTestResult(data.lastSelection, value)) return false;

        Debug.Log($"{LogPrefix} 테스트 후 결과 보기 {(value ? "켜짐" : "꺼짐")} 저장", this);
        return Save();
    }

    /// <summary>저장된 마지막 인게임 배경 밝기 (%, 단계로 정리한 값). Init 전이거나 저장된 값이 없으면 기본값(100)</summary>
    public int LastBackgroundBrightness => data != null && data.lastSelection != null
        ? BackgroundBrightnessRules.Sanitize(data.lastSelection.backgroundBrightness)
        : GameConstants.Play.DefaultBackgroundBrightness;

    /// <summary>
    /// 마지막 배경 밝기를 바꾸고 바로 저장한다 (인게임 PgUp·PgDn, 일반·테스트 플레이 같은 값). 값이 같으면 저장하지 않는다.
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastBackgroundBrightness(int value)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();
        if (!PlayerDataRules.TrySetLastBackgroundBrightness(data.lastSelection, value)) return false;

        Debug.Log($"{LogPrefix} 마지막 배경 밝기 {data.lastSelection.backgroundBrightness}% 저장", this);
        return Save();
    }

    /// <summary>저장된 마지막 선택 (배속 · 탭마다 마지막 곡·채보, 기획서 2-4 11/18). Init 전이면 null. 고치지 않는다</summary>
    public LastSelectionSaveData LastSelection => data != null ? data.lastSelection : null;

    /// <summary>
    /// 일반 플레이로 인게임에 들어갈 때 마지막 배속과 플레이한 탭의 칸(공식 / 나만의 채보)을 바꾸고, 바뀐 것이 있으면 한 번 저장한다. 다른 탭 칸은 그대로.
    /// 테스트 플레이는 부르지 않는다 (곡 선택에서 시작하는 것은 항상 일반 플레이).
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastPlayed(float scrollMultiplier, string songId, string chartId, ChartType chartType)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();

        bool speedChanged = PlayerDataRules.TrySetLastScrollMultiplier(data.lastSelection, scrollMultiplier);
        bool playedChanged = PlayerDataRules.TrySetLastPlayed(data.lastSelection, songId, chartId, chartType);
        if (!speedChanged && !playedChanged) return false;

        TabSelectionSaveData tab = data.lastSelection.Get(chartType);
        Debug.Log($"{LogPrefix} 마지막 선택 저장: 배속 x{data.lastSelection.scrollMultiplier:0.0}, {chartType} 칸 곡 {tab.songId}, 채보 {tab.chartId}", this);
        return Save();
    }

    /// <summary>
    /// 나만의 채보 칸만 바꾸고 바뀌었으면 저장한다 (배속·공식 칸은 그대로): 에디터로 들어갈 때(E 편집 { songId, chartId } · 새 채보 { songId, 빈 chartId }),
    /// 에디터에서 새 채보를 처음 저장해 chartId가 생겼을 때.
    /// </summary>
    /// <returns>저장했으면 true</returns>
    public bool SetLastCustom(string songId, string chartId)
    {
        if (data == null) return false;
        if (data.lastSelection == null) data.lastSelection = new LastSelectionSaveData();
        if (!PlayerDataRules.TrySetTab(data.lastSelection, ChartType.Custom, songId, chartId)) return false;

        Debug.Log($"{LogPrefix} 마지막 선택 저장: Custom 칸 곡 {songId}, 채보 {(string.IsNullOrEmpty(chartId) ? "(+ 줄)" : chartId)}", this);
        return Save();
    }

    // ───── 기록 ─────

    /// <summary>chartId의 기록. 없으면 null (고치지 않는다)</summary>
    public ChartRecordData FindRecord(string chartId)
    {
        return book != null ? book.Find(chartId) : null;
    }

    /// <summary>
    /// 한 판의 결과를 기록에 넣고, 기록했으면 저장한다. 테스트 플레이·자동 플레이·chartId 없음은 기록하지 않는다.
    /// </summary>
    public RecordUpdateResult SubmitResult(InGameEntryData entry, ResultData result)
    {
        if (book == null) return new RecordUpdateResult { skipReason = "Init 전" };

        RecordUpdateResult update = book.Submit(entry, result, DateTime.UtcNow);
        if (!update.isRecorded)
        {
            Debug.Log($"{LogPrefix} 기록하지 않음 ({update.skipReason})", this);
            return update;
        }

        Debug.Log($"{LogPrefix} 기록: chartId={entry.chartId} ({entry.chartType}), 점수 {result.score} → 최고 {update.bestScore} " +
                  $"(이전 {(update.hadPreviousRecord ? update.previousBestScore.ToString() : "없음")}), 신기록 {update.isNewRecord}, 플레이 {update.playCount}번", this);
        Save();
        return update;
    }

    /// <summary>
    /// chartId의 기록을 지운다 (나만의 채보를 수정해 저장했을 때, 기획서 2-1(8)). 지웠으면 저장한다.
    /// </summary>
    /// <returns>지운 기록이 있었으면 true</returns>
    public bool ResetRecord(string chartId)
    {
        if (book == null || !book.Remove(chartId)) return false;

        Debug.Log($"{LogPrefix} 기록 초기화: chartId={chartId}", this);
        Save();
        return true;
    }
}
