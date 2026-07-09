using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// CI 호출 예: Unity -batchmode -quit -nographics -buildTarget Android -projectPath . \
//   -executeMethod BuildScript.<Method> -logFile -
public static class BuildScript
{
    // ── 어드레서블: full 빌드 (③a / 최초 baseline). ServerData/Android + content_state.bin 생성.
    public static void BuildAndroidContentFull()
    {
        var settings = EnsureSettings();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        FinishContent(result, "FULL");
    }

    // ── 어드레서블: 증분 빌드 (③b). CI가 S3 baseline을 아래 경로에 미리 배치해야 함.
    public static void BuildAndroidContentUpdate()
    {
        var settings = EnsureSettings();
        var statePath = ContentUpdateScript.GetContentStateDataPath(false); // Assets/AddressableAssetsData/Android/addressables_content_state.bin
        if (!System.IO.File.Exists(statePath))
        {
            Debug.LogError($"content_state 없음: {statePath}. ③a(앱 빌드)를 먼저 실행해 baseline을 생성하세요.");
            EditorApplication.Exit(2);
            return;
        }
        Debug.Log($"content update baseline: {statePath}");
        var result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
        FinishContent(result, "UPDATE");
    }

    // ── APK 빌드 (③a). 디버그 서명(프로젝트 기본). 콘텐츠는 별도 스텝에서 이미 빌드했으므로 재빌드 안 함.
    public static void BuildAndroidApk()
    {
        var settings = EnsureSettings();
        settings.BuildAddressablesWithPlayerBuild =
            AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/lop.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"APK build FAILED: {summary.result}, errors={summary.totalErrors}");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log($"APK OK: {summary.outputPath}, size={summary.totalSize} bytes");
        EditorApplication.Exit(0);
    }

    static AddressableAssetSettings EnsureSettings()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings를 찾을 수 없음");
            EditorApplication.Exit(1);
        }
        // 활성 프로파일은 프로젝트에 저장된 dev를 사용(원격 경로 = s3://lop-assets/dev/[BuildTarget]).
        Debug.Log($"active profile id: {settings.activeProfileId}");
        return settings;
    }

    static void FinishContent(AddressablesPlayerBuildResult result, string mode)
    {
        if (result != null && !string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"Addressables {mode} build FAILED: {result.Error}");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log($"Addressables {mode} build OK. duration={result?.Duration}s");
        EditorApplication.Exit(0);
    }
}
