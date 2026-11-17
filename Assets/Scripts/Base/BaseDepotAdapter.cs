using System;
using UnityEngine;

/// <summary>
/// D121: Ana Üs depo adaptörü. Binalar <see cref="PlayerSaveData.placedBuildings"/> + <see cref="BaseMapManager"/>, dekorlar
/// <see cref="PlayerSaveData.placedDecor"/> + <see cref="DecorPlacementController"/>. Sahne yoksa (testler, sahne dışı) yalnız kayıt
/// listeleri güncellenir. Kurallar <see cref="DepotService"/>'tedir; burada yalnız bul / düşür / kur / kaydet.
/// Konum: bina için dünya merkezi, dekor için sol-alt hücre (x, y).
/// </summary>
public sealed class BaseDepotAdapter : IDepotRegion
{
    public static readonly BaseDepotAdapter Instance = new BaseDepotAdapter();

    public int Region => DepotService.RegionBase;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    public DepotItem Snapshot(string id, int category)
    {
        var data = Data;
        if (data == null || string.IsNullOrEmpty(id)) return null;
        if (category == DepotService.CategoryDecor)
        {
            var decor = data.placedDecor?.Find(d => d != null && d.instanceId == id);
            return decor == null ? null : new DepotItem { id = id, type = decor.decorId, level = 1, category = category };
        }
        var placed = data.placedBuildings?.Find(p => p != null && p.slotId == id);
        if (placed == null) return null;
        return new DepotItem { id = id, type = placed.buildingType, level = Mathf.Max(1, placed.level), category = category };
    }

    public bool Despawn(DepotItem item)
    {
        var data = Data;
        if (data == null) return false;
        if (item.category == DepotService.CategoryDecor)
        {
            var controller = DecorPlacementController.Instance;
            if (controller != null && controller.DespawnDecor(item.id)) controller.WriteToPlayerData();
            else data.placedDecor?.RemoveAll(d => d != null && d.instanceId == item.id);
            return data.placedDecor == null || !data.placedDecor.Exists(d => d != null && d.instanceId == item.id);
        }
        // Altın Madeni: o ana kadarki üretim madene yazılır (depodayken üretmez; kazanılmış altın kaybolmaz).
        if (item.type == nameof(BuildingType.GoldMine)) GameManager.Instance.ApplyOfflineGoldMineProduction();
        var map = BaseMapManager.Instance;
        if (map != null && map.DespawnBuilding(item.id)) map.WriteToPlayerData();
        else data.placedBuildings?.RemoveAll(p => p != null && p.slotId == item.id);
        bool gone = data.placedBuildings == null || !data.placedBuildings.Exists(p => p != null && p.slotId == item.id);
        if (gone) GameManager.Instance.RefreshPlacedBuildingState();
        return gone;
    }

    public bool Spawn(DepotItem item, Vector2 at)
    {
        var data = Data;
        if (data == null) return false;
        if (item.category == DepotService.CategoryDecor)
        {
            var cell = new Vector2Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y));
            var controller = DecorPlacementController.Instance;
            if (controller != null)
            {
                if (!controller.SpawnRestored(item.type, cell, item.id)) return false;
                controller.WriteToPlayerData();
                return true;
            }
            if (data.placedDecor == null) data.placedDecor = new System.Collections.Generic.List<PlacedDecorSave>();
            data.placedDecor.Add(new PlacedDecorSave { instanceId = item.id, decorId = item.type, x = cell.x, y = cell.y });
            return true;
        }
        if (!Enum.TryParse(item.type, out BuildingType type) || type == BuildingType.MainBase) return false;
        var map = BaseMapManager.Instance;
        if (map != null)
        {
            var obj = map.SpawnRestored(type, at, item.id, item.level);
            if (obj == null) return false;
            map.WriteToPlayerData();
            // Kimlik çakışırsa harita yeni kimlik verir; kayıt ona göre yazıldı (seviye BuildingInstance.InitialLevel'den).
        }
        else
        {
            if (data.placedBuildings == null) data.placedBuildings = new System.Collections.Generic.List<PlacedBuildingData>();
            data.placedBuildings.Add(new PlacedBuildingData
            {
                slotId = item.id, buildingType = item.type, level = item.level, hasPosition = true, x = at.x, y = at.y,
            });
        }
        if (type == BuildingType.GoldMine) data.goldMineLastCollectTime = GameClock.UtcNow.Ticks;   // depodaki süre üretim sayılmaz
        GameManager.Instance.RefreshPlacedBuildingState();
        return true;
    }

    public void Persist() => GameManager.Instance?.SaveGame();
}
