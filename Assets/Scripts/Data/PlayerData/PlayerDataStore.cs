using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// PlayerData.json 읽기·쓰기 (순수 C#). 파일 경로를 생성자로 받아 테스트에서는 임시 폴더를 쓴다.
/// - 읽기: 본 파일 → 실패하면(없음 제외) .bak → 둘 다 실패하면 본 파일을 PlayerData_broken_yyyyMMdd_HHmmss.json으로 옮기고 기본값.
///   본 파일이 없고 .bak만 있으면(저장 도중 끊긴 경우) .bak을 읽는다. .bak으로 되살렸으면 깨진 본 파일도 broken 이름으로 옮겨
///   다음 저장 때 깨진 파일이 .bak을 덮지 않게 한다.
/// - 쓰기: 임시 파일(.tmp)에 쓰기 → 기존 파일을 .bak으로 옮기기 → 임시 파일을 본 파일 이름으로 바꾸기.
/// - 읽은 값의 정리(범위·enum·중복 기록)는 PlayerDataRules.Sanitize가 한다. 이 클래스는 파일만 다룬다.
/// </summary>
public class PlayerDataStore
{
    /// <summary>불러온 결과</summary>
    public enum LoadSource
    {
        /// <summary>본 파일과 .bak이 모두 없다 (처음 실행)</summary>
        NoFile,
        /// <summary>본 파일을 읽었다</summary>
        Main,
        /// <summary>본 파일을 못 읽어(또는 없어) .bak을 읽었다</summary>
        Backup,
        /// <summary>둘 다 못 읽어 기본값으로 시작한다 (broken 파일을 남겼다)</summary>
        Broken
    }

    public string FilePath { get; }
    public string BackupPath => FilePath + GameConstants.PlayerData.BackupSuffix;
    public string TempPath => FilePath + GameConstants.PlayerData.TempSuffix;

    /// <summary>마지막 Load에서 깨진 파일을 옮긴 경로 (없으면 null)</summary>
    public string LastBrokenPath { get; private set; }

    /// <summary>마지막 Load·Save의 오류 메시지 (없으면 빈 문자열)</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <param name="filePath">PlayerData.json 전체 경로</param>
    public PlayerDataStore(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// 파일을 읽는다. 값 정리는 하지 않는다 (호출하는 쪽이 Sanitize).
    /// </summary>
    /// <param name="data">읽은 데이터. NoFile·Broken이면 null</param>
    /// <param name="nowLocal">broken 파일 이름에 쓸 시각 (null이면 지금)</param>
    public LoadSource Load(out PlayerData data, DateTime? nowLocal = null)
    {
        LastBrokenPath = null;
        LastError = string.Empty;
        data = null;

        bool hasMain = File.Exists(FilePath);
        bool hasBackup = File.Exists(BackupPath);
        if (!hasMain && !hasBackup) return LoadSource.NoFile;

        string mainError = null;
        if (hasMain)
        {
            if (TryRead(FilePath, out data, out mainError)) return LoadSource.Main;
        }

        string backupError = null;
        if (hasBackup && TryRead(BackupPath, out data, out backupError))
        {
            if (hasMain)
            {
                LastError = $"본 파일 읽기 실패: {mainError}";
                MoveToBroken(nowLocal);
            }
            return LoadSource.Backup;
        }

        LastError = $"본 파일: {mainError ?? "없음"} / .bak: {backupError ?? "없음"}";
        if (hasMain) MoveToBroken(nowLocal);
        data = null;
        return LoadSource.Broken;
    }

    /// <summary>
    /// 저장한다: .tmp에 쓰기 → 기존 파일을 .bak으로 → .tmp를 본 파일로. 실패하면 false (LastError).
    /// </summary>
    public bool Save(PlayerData data)
    {
        LastError = string.Empty;
        try
        {
            string folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(FilePath))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(FilePath, BackupPath);
            }
            File.Move(TempPath, FilePath);
            return true;
        }
        catch (Exception e)
        {
            LastError = $"{e.GetType().Name}: {e.Message}";
            return false;
        }
    }

    /// <summary>JSON을 읽어 PlayerData로. 비었거나 JSON이 아니거나 null이면 실패</summary>
    private static bool TryRead(string path, out PlayerData data, out string error)
    {
        data = null;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "빈 파일";
                return false;
            }

            data = JsonUtility.FromJson<PlayerData>(json);
            if (data == null)
            {
                error = "JSON을 읽지 못함";
                return false;
            }
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = $"{e.GetType().Name}: {e.Message}";
            data = null;
            return false;
        }
    }

    /// <summary>본 파일을 PlayerData_broken_yyyyMMdd_HHmmss.json으로 옮긴다 (같은 이름이 있으면 뒤에 번호)</summary>
    private void MoveToBroken(DateTime? nowLocal)
    {
        try
        {
            string folder = Path.GetDirectoryName(FilePath) ?? string.Empty;
            string stamp = (nowLocal ?? DateTime.Now).ToString(GameConstants.PlayerData.BrokenTimeFormat, CultureInfo.InvariantCulture);
            string target = Path.Combine(folder, string.Format(GameConstants.PlayerData.BrokenFileFormat, stamp));
            for (int i = 1; File.Exists(target); i++)
                target = Path.Combine(folder, string.Format(GameConstants.PlayerData.BrokenFileFormat, $"{stamp}_{i}"));

            File.Move(FilePath, target);
            LastBrokenPath = target;
        }
        catch (Exception e)
        {
            LastError += $" / broken 파일로 옮기지 못함: {e.Message}";
        }
    }
}
