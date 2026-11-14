using System.Collections.Generic;

/// <summary>
/// 6. oturum (1.1): "tamamlanmış seviye" için TEK doğru kaynak.
/// - <see cref="PlacedBuildingData.level"/> yalnız TAMAMLANMIŞ seviyeyi tutar: yükseltme süresi bitmeden artmaz.
/// - Yeni kurulan bina ise kayda hemen 1. seviye olarak girer (yer tutulsun diye); "build" görevi sürerken
///   tamamlanmış seviyesi 0'dır: savaş, bonuslar, kart atölyesi, görevler onu yokmuş gibi görür.
/// - Süren yükseltmenin hedef seviyesi yalnız görünüm içindir: <see cref="PendingLevel"/>.
/// Savaş, maden deposu, Kışla, Kart Atölyesi ve rozetler buradan okur.
/// </summary>
public static class BuildingLevels
{
    /// <summary>Bu binada süren yeni inşa ("build") görevi var mı?</summary>
    public static bool UnderConstruction(PlayerSaveData data, string slotId)
    {
        if (data?.builderTasks == null || string.IsNullOrEmpty(slotId)) return false;
        foreach (var task in data.builderTasks)
            if (task != null && task.IsActive && task.kind == "build" && task.slotId == slotId) return true;
        return false;
    }

    /// <summary>Bu binada süren yükseltme ("upgrade") görevi var mı?</summary>
    public static bool Upgrading(PlayerSaveData data, string slotId)
    {
        if (data?.builderTasks == null || string.IsNullOrEmpty(slotId)) return false;
        foreach (var task in data.builderTasks)
            if (task != null && task.IsActive && task.kind == "upgrade" && task.slotId == slotId) return true;
        return false;
    }

    /// <summary>Tamamlanmış seviye: inşası bitmemiş bina 0.</summary>
    public static int Completed(PlayerSaveData data, PlacedBuildingData placed)
    {
        if (placed == null || placed.level <= 0) return 0;
        return UnderConstruction(data, placed.slotId) ? 0 : placed.level;
    }

    /// <summary>Süren iş bitince ulaşılacak seviye (yalnız gösterim için; oyun mantığı kullanmaz).</summary>
    public static int PendingLevel(PlayerSaveData data, PlacedBuildingData placed)
    {
        if (placed == null) return 0;
        if (UnderConstruction(data, placed.slotId)) return 1;
        return Upgrading(data, placed.slotId) ? placed.level + 1 : placed.level;
    }

    /// <summary>Bu türün tamamlanmış binalarının seviyeleri (inşası süren dahil değil).</summary>
    public static List<int> CompletedLevels(PlayerSaveData data, BuildingType type)
    {
        var levels = new List<int>();
        if (data?.placedBuildings == null) return levels;
        string name = type.ToString();
        foreach (var placed in data.placedBuildings)
        {
            if (placed == null || placed.buildingType != name) continue;
            int level = Completed(data, placed);
            if (level > 0) levels.Add(level);
        }
        return levels;
    }

    public static int CompletedCount(PlayerSaveData data, BuildingType type) => CompletedLevels(data, type).Count;

    /// <summary>Türün en yüksek tamamlanmış seviyesi (yoksa 0).</summary>
    public static int HighestCompleted(PlayerSaveData data, BuildingType type)
    {
        int best = 0;
        foreach (int level in CompletedLevels(data, type)) if (level > best) best = level;
        return best;
    }
}
