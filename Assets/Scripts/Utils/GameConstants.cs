using UnityEngine;

/// <summary>
/// 게임 전체에서 쓰는 상수 모음. 문자열·경로·수치를 코드 곳곳에 직접 쓰지 않고 여기서 관리한다.
/// </summary>
public static class GameConstants
{
    /// <summary>Resources.Load 경로 (Assets/Resources 기준, 확장자 제외)</summary>
    public static class ResourcePaths
    {
        /// <summary>전역 매니저 프리팹: Assets/Resources/Prefabs/Manager/Managers_DontDestroy.prefab</summary>
        public const string ManagersPrefab = "Prefabs/Manager/Managers_DontDestroy";
    }

    /// <summary>씬 이름 (Build Profiles에 등록된 씬 파일 이름과 같아야 한다)</summary>
    public static class Scenes
    {
        public const string Title = "TitleScene";
        public const string EditorSongSelect = "EditorSongSelectScene";
        public const string Editor = "EditorScene";
    }

    /// <summary>채보 에디터 규칙</summary>
    public static class Editor
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 15;
    }
}
