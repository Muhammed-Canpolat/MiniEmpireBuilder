using System;
using UnityEngine;

/// <summary>
/// 3. oturum (1.2): menü rozetlerinin ortak koşulları. Rozet yalnız oyuncunun o menüde gerçekten yapabileceği bir şey varken yanar;
/// koşullar oyunun kendi kurallarından (GameManager.CanUpgrade, BuildMenuController.MenuCost, kurulu sayı sınırı) okunur.
/// </summary>
public static class BaseBadges
{
    /// <summary>İnşa Et menüsünde kurulabilecek (kilit açık, sayı &lt; sınır, altın yeter, boşta inşaatçı) yeni bir yapı var mı.</summary>
    public static bool CanBuildNew()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.PlayerData == null || !gm.HasFreeBuilder) return false;
        int baseLevel = gm.GetMainBaseLevel();
        foreach (var type in BuildMenuController.MenuTypes)
        {
            if (!gm.Buildings.TryGetValue(type, out var data) || data == null || !data.isUnlocked) continue;
            if (DepotService.OwnedCount(DepotService.RegionBase, type.ToString()) >= data.MaxCountAtBaseLevel(baseLevel)) continue;   // D121: yerleşik + depo
            if (gm.Gold >= BuildMenuController.MenuCost(type)) return true;
        }
        return false;
    }

    /// <summary>Kurulu bina sayısı: sahnede harita yöneticisinden, yoksa kayıttan.</summary>
    public static int PlacedCount(BuildingType type)
    {
        if (BaseMapManager.Instance != null) return BaseMapManager.Instance.GetPlacedCount(type);
        int n = 0;
        var list = GameManager.Instance?.PlayerData?.placedBuildings;
        if (list != null) foreach (var p in list) if (p != null && p.buildingType == type.ToString()) n++;
        return n;
    }
}
