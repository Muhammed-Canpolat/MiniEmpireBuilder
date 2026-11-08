using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Cihaz testi için tek tıkla Android APK üretir.
/// Menü: Tools/Mini Empire Builder/Android Test APK (Development | Release)
/// Çıktı: Builds/Android/ (git'te ignore'lu)
/// </summary>
public static class AndroidTestBuild
{
    private const string OutputDir = "Builds/Android";

    [MenuItem("Tools/Mini Empire Builder/Android Test APK (Development)")]
    public static void BuildDevelopment() => Build(true);

    [MenuItem("Tools/Mini Empire Builder/Android Test APK (Release)")]
    public static void BuildRelease() => Build(false);

    [MenuItem("Tools/Mini Empire Builder/Apply Mobile Player Settings")]
    public static void ApplyPlayerSettings()
    {
        // Oyun dikey tasarlandı: yatay döndürmeyi kapat.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // Kurulu en yüksek SDK'yı hedefle (28 sabit değeri Play için çok eski).
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        AssetDatabase.SaveAssets();
        Debug.Log("[AndroidTestBuild] Mobil oyuncu ayarları uygulandı (Portrait, SDK Auto, IL2CPP ARM64).");
    }

    private static void Build(bool development)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[AndroidTestBuild] Android hedefine geçilemedi. Android Build Support modülü kurulu mu?");
            return;
        }

        ApplyPlayerSettings();
        EditorUserBuildSettings.buildAppBundle = false;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[AndroidTestBuild] Build listesinde etkin sahne yok.");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        string suffix = development ? "dev" : "release";
        string path = Path.Combine(OutputDir, $"MiniEmpireBuilder-{PlayerSettings.bundleVersion}-{suffix}.apk");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = BuildTarget.Android,
            options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            // summary.totalSize sıkıştırılmamış içerik toplamıdır; gerçek APK boyutunu dosyadan oku.
            float apkMb = new FileInfo(path).Length / (1024f * 1024f);
            Debug.Log($"[AndroidTestBuild] Başarılı: {Path.GetFullPath(path)} (APK {apkMb:F1} MB, {summary.totalTime.TotalSeconds:F0} sn)");
            EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError($"[AndroidTestBuild] Build {summary.result}: {summary.totalErrors} hata. Console'daki ilk hatayı inceleyin.");
        }
    }
}
