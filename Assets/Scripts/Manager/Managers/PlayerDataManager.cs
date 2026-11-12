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
///   에디터에서 나만의 채보를 저장해 기록을 지울 때 부른다.
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
                  $"오프셋 {s.offsetMs}ms, 화면 {s.resolutionWidth}×{s.resolutionHeight} {s.windowMode}{(fromFile ? "" : " (적용 안 함)")}, 경로 {store.FilePath}", this);
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
