using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 3. oturum (AŞAMA 4): Google Play kapalı test için yayın AAB'si.
/// Menü: Tools/Mini Empire Builder/Google Play Release AAB · Komut satırı: -executeMethod AndroidReleaseBuild.BuildFromCommandLine
/// Ayarlar: AAB, IL2CPP + yalnız ARM64, hedef API 36, en düşük API 25, Development KAPALI, Log yığın izi yok (yalnız uyarı/hata).
/// İmza: ortam değişkenleri MEB_KEYSTORE_PATH, MEB_KEYSTORE_PASS, MEB_KEY_ALIAS, MEB_KEY_PASS (anahtar deposu ve şifreler ASLA git'e girmez).
/// Bunlar yoksa Unity hata ayıklama anahtarıyla imzalar: yalnız yerel doğrulama içindir, Play Console kabul etmez.
/// Sürüm: MEB_VERSION_NAME (örn. 0.1.0) ve MEB_VERSION_CODE (her yüklemede +1; tamsayı). Verilmezse proje ayarındaki değerler.
/// Çıktı: Builds/Release/MiniEmpireBuilder-{ad}-{kod}.aab (git dışı).
/// </summary>
public static class AndroidReleaseBuild
{
    public const int TargetApi = 36, MinApi = 25;
    private const string OutputDir = "Builds/Release";

    [MenuItem("Tools/Mini Empire Builder/Google Play Release AAB")]
    public static void BuildMenu() => Build();

    public static void BuildFromCommandLine()
    {
        string path = Build();
        EditorApplication.Exit(path != null ? 0 : 1);
    }

    /// <summary>Yayın oyuncu ayarlarını uygular (derlemeden bağımsız da çağrılabilir).</summary>
    public static void ApplyReleaseSettings()
    {
        AndroidTestBuild.ApplyPlayerSettings();   // dikey, IL2CPP, ARM64
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApi;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinApi;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // Yayında Debug.Log yığın izi üretilmez (performans); uyarı/hata izleri kalır. Log filtresi ReleaseLogging'de.
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        string name = Environment.GetEnvironmentVariable("MEB_VERSION_NAME");
        if (!string.IsNullOrEmpty(name)) PlayerSettings.bundleVersion = name;
        if (int.TryParse(Environment.GetEnvironmentVariable("MEB_VERSION_CODE"), out int code) && code > 0)
            PlayerSettings.Android.bundleVersionCode = code;
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        AssetDatabase.SaveAssets();
    }

    private static void ApplySigning()
    {
        string store = Environment.GetEnvironmentVariable("MEB_KEYSTORE_PATH");
        if (string.IsNullOrEmpty(store) || !File.Exists(store))
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.LogWarning("[AndroidReleaseBuild] MEB_KEYSTORE_PATH yok: hata ayıklama anahtarıyla imzalanıyor (Play'e yüklenemez, yalnız yerel doğrulama).");
            return;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = store;
        PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("MEB_KEYSTORE_PASS") ?? "";
        PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("MEB_KEY_ALIAS") ?? "upload";
        PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("MEB_KEY_PASS") ?? PlayerSettings.Android.keystorePass;
    }

    public static string Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[AndroidReleaseBuild] Android hedefine geçilemedi.");
            return null;
        }
        ApplyReleaseSettings();
        ApplySigning();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(OutputDir);
        string path = Path.Combine(OutputDir, $"MiniEmpireBuilder-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None,
        });
        // Şifreler proje ayarlarına yazılmasın (ProjectSettings.asset git'te)
        PlayerSettings.Android.keystorePass = "";
        PlayerSettings.Android.keyaliasPass = "";
        AssetDatabase.SaveAssets();
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[AndroidReleaseBuild] Başarısız: {report.summary.result}, {report.summary.totalErrors} hata");
            return null;
        }
        Debug.Log($"[AndroidReleaseBuild] Başarılı: {Path.GetFullPath(path)} ({new FileInfo(path).Length / (1024f * 1024f):F1} MB)");
        return path;
    }
}
