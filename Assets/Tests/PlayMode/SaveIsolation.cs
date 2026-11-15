using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 9. oturum (TECH_AUDIT KN-4): test koşusu geliştiricinin gerçek kaydını değiştirmesin. Bütün testlerden önce kayıt kopyaları
/// (PlayerPrefs asıl/yedek/bozuk + save.json) saklanır, koşu bitince geri konur; koşunun ürettiği yedek/karantina dosyaları silinir.
/// Eskiden her koşu, son çalışan testin kaydını diskte bırakıyordu ve bazı testler o artığa göre geçip kalıyordu.
/// </summary>
[SetUpFixture]
public class SaveIsolation
{
    private static readonly string[] Keys =
    {
        "MiniEmpireBuilder_SaveData", "MiniEmpireBuilder_SaveData_Backup", "MiniEmpireBuilder_SaveData_Corrupt"
    };
    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
    private static string BackupDir => Path.Combine(Application.persistentDataPath, "SaveBackups");

    private string[] savedKeys;
    private string savedFile;
    private DateTime startedUtc;
    [OneTimeSetUp]
    public void KeepRealSave()
    {
        savedKeys = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
        savedFile = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        startedUtc = DateTime.UtcNow.AddSeconds(-2);
    }

    [OneTimeTearDown]
    public void RestoreRealSave()
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            if (savedKeys[i] != null) PlayerPrefs.SetString(Keys[i], savedKeys[i]);
            else PlayerPrefs.DeleteKey(Keys[i]);
        }
        PlayerPrefs.Save();
        if (savedFile != null) File.WriteAllText(FilePath, savedFile);
        else if (File.Exists(FilePath)) File.Delete(FilePath);
        if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
        if (Directory.Exists(BackupDir))
            foreach (var file in Directory.GetFiles(BackupDir))
                if (File.GetLastWriteTimeUtc(file) >= startedUtc) File.Delete(file);
        var saveSystem = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SaveSystem")).FirstOrDefault(t => t != null);
        saveSystem?.GetMethod("ResetForTests", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
    }
}
