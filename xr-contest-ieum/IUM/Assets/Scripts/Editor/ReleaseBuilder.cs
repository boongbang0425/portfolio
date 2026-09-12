using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 배치 모드 릴리스 빌드 진입점. Addressables 콘텐츠를 먼저 빌드하고 이어서 Android(Quest)
/// 플레이어를 빌드한다. 에디터가 닫힌 상태에서 CLI로 돌리기 위한 것이다:
///
/// <code>
/// Unity.exe -batchmode -quit -projectPath D:/Unity/XR_Contest/IUM -buildTarget Android
///     -executeMethod ReleaseBuilder.BuildAndroid -logFile build.log
/// </code>
///
/// 씬 목록·플레이어 설정은 프로젝트에 저장된 값을 그대로 쓴다(여기서 바꾸지 않는다).
/// 실패 시 비제로 종료 코드로 나가 CLI에서 감지할 수 있다.
/// </summary>
public static class ReleaseBuilder
{
    const string OutputPath = "Builds/IUM.apk";

    public static void BuildAndroid()
    {
        try
        {
            BuildAddressables();
            BuildPlayer();
            Debug.Log($"[ReleaseBuilder] 빌드 완료: {Path.GetFullPath(OutputPath)}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ReleaseBuilder] 빌드 실패: {exception}");
            EditorApplication.Exit(1);
        }
    }

    static void BuildAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new InvalidOperationException("Addressables 설정을 찾지 못했습니다.");

        Debug.Log("[ReleaseBuilder] Addressables 콘텐츠 빌드 시작");
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var result);

        if (!string.IsNullOrEmpty(result.Error))
            throw new InvalidOperationException($"Addressables 빌드 실패: {result.Error}");

        Debug.Log($"[ReleaseBuilder] Addressables 완료 ({result.Duration:F1}s)");
    }

    static void BuildPlayer()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabled = Array.FindAll(scenes, s => s.enabled);
        var paths = Array.ConvertAll(enabled, s => s.path);
        if (paths.Length == 0)
            throw new InvalidOperationException("빌드 씬 목록이 비어 있습니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        Debug.Log($"[ReleaseBuilder] 플레이어 빌드 시작 — 씬 {paths.Length}개");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = paths,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"플레이어 빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}건");
    }
}
