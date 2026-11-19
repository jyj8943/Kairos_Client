using System;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(TestSpeedRunCallback))]

/// <summary>
/// 테스트 실행 전체 설정 한 곳 (CLAUDE.md 작업 확인 규칙).
/// - 재생 배율 PlaybackRate: 노래·곡 시간을 이만큼 빠르게 돌린다 (SoundManager.SetPlaybackRateForTests). 곡 시간으로 재는 대기가 모두 짧아진다.
/// - 음량 Volume: 테스트 중 AudioListener.volume. PlayerData.json의 음량 설정은 바꾸지 않는다.
/// - 실행이 시작될 때 TestSpeedRunCallback이 적용하고 끝나면 되돌린다. 실제 시간 정확도를 보는 테스트는 [NormalSpeed]를 붙여 1.0으로 돌린다.
/// - 테스트의 마감 시간은 Deadline(곡 시간 초)로 정한다: 실제 초 = 곡 시간 ÷ 지금 배율, 최소 MinDeadlineSeconds.
/// - 테스트 중 다른 창을 써도 인게임이 멈추지 않게 InGameScene.IgnoreFocusPauseForTests를 켠다 (창 포커스·앱 일시정지 자동 일시정지 끔).
/// - 인게임 진입이 곡 로딩 오버레이(3초 이상)를 건너뛰게 InGameLauncher.SkipSongLoadingForTests를 켠다 (곡 로딩 테스트만 잠깐 끈다).
/// </summary>
public static class TestSpeed
{
    /// <summary>테스트 재생 배율 (기본 2.0)</summary>
    public const double PlaybackRate = 2.0;

    /// <summary>테스트 중 전체 음량 (AudioListener.volume)</summary>
    public const float Volume = 0.2f;

    /// <summary>마감 시간 최소값 (실제 초). 씬 로딩처럼 배율과 상관없는 대기도 이보다 짧아지지 않는다</summary>
    public const float MinDeadlineSeconds = 5f;

    /// <summary>지금 적용 중인 재생 배율 ([NormalSpeed] 테스트면 1.0)</summary>
    public static double CurrentRate => SoundManager.CurrentPlaybackRate;

    /// <summary>
    /// 마감 시각 (realtimeSinceStartup 기준) = 지금 + max(MinDeadlineSeconds, songSeconds ÷ 지금 배율).
    /// </summary>
    /// <param name="songSeconds">배율 1.0에서 기다릴 시간 (초)</param>
    public static float Deadline(float songSeconds)
    {
        return Time.realtimeSinceStartup + Mathf.Max(MinDeadlineSeconds, songSeconds / (float)CurrentRate);
    }

    /// <inheritdoc cref="Deadline(float)"/>
    public static float Deadline(double songSeconds) => Deadline((float)songSeconds);
}

/// <summary>
/// 이 속성을 붙인 테스트(메서드 또는 클래스)는 재생 배율 1.0으로 돈다. 실제 시간 정확도(메트로놈 예약 오차, 키 반복 지연 등)를 볼 때 쓴다.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NormalSpeedAttribute : Attribute
{
}

/// <summary>
/// 테스트 실행 콜백: 시작할 때 재생 배율·음량을 적용하고, 끝나면 원래 값으로 되돌린다.
/// - 테스트마다 [NormalSpeed]가 있으면 그 테스트 동안만 1.0으로 바꾼다.
/// - 플레이 모드에 들어갈 때 AudioListener.volume이 되돌아가도 다시 맞추도록 playModeStateChanged도 구독한다.
/// </summary>
public class TestSpeedRunCallback : ITestRunCallback
{
    /// <summary>실행 중이면 true (같은 실행 안에서 콜백이 두 번 불려도 원래 값을 덮어쓰지 않게)</summary>
    private static bool isActive;
    private static float originalVolume = 1f;

    public void RunStarted(ITest testsToRun)
    {
        if (isActive) return;
        isActive = true;
        originalVolume = AudioListener.volume;
        AudioListener.volume = TestSpeed.Volume;
        SoundManager.SetPlaybackRateForTests(TestSpeed.PlaybackRate);
        InGameScene.IgnoreFocusPauseForTests = true;
        InGameLauncher.SkipSongLoadingForTests = true;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Debug.Log($"[TestSpeed] 테스트 실행 시작: 재생 배율 x{TestSpeed.PlaybackRate}, 음량 {TestSpeed.Volume} (원래 {originalVolume})");
    }

    public void RunFinished(ITestResult testResults)
    {
        if (!isActive) return;
        isActive = false;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SoundManager.SetPlaybackRateForTests(1.0);
        InGameScene.IgnoreFocusPauseForTests = false;
        InGameLauncher.SkipSongLoadingForTests = false;
        AudioListener.volume = originalVolume;
        Debug.Log($"[TestSpeed] 테스트 실행 끝: 재생 배율 x1.0, 음량 {originalVolume}으로 되돌림");
    }

    public void TestStarted(ITest test)
    {
        if (!isActive || test.IsSuite) return;
        SoundManager.SetPlaybackRateForTests(IsNormalSpeed(test) ? 1.0 : TestSpeed.PlaybackRate);
    }

    public void TestFinished(ITestResult result)
    {
        if (!isActive || result.Test.IsSuite) return;
        SoundManager.SetPlaybackRateForTests(TestSpeed.PlaybackRate);
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && isActive) AudioListener.volume = TestSpeed.Volume;
    }

    /// <summary>테스트 메서드나 그 클래스에 [NormalSpeed]가 있으면 true</summary>
    private static bool IsNormalSpeed(ITest test)
    {
        if (test.Method != null && test.Method.GetCustomAttributes<NormalSpeedAttribute>(true).Length > 0) return true;
        return test.TypeInfo != null && test.TypeInfo.GetCustomAttributes<NormalSpeedAttribute>(true).Length > 0;
    }
}
