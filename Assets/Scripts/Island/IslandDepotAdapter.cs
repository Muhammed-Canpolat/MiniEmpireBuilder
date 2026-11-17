using UnityEngine;

/// <summary>
/// D121: Ada depo adaptörü. Kayıt <see cref="IslandSaveData.placed"/>, sahne <see cref="IslandVillage"/>, yer kuralı <see cref="IslandGrid.Check"/>.
/// Üreticinin (Oduncu/Ağıl) stoku depo öğesine taşınır (kayıp yok), depoda artmaz; kurulunca son tick = şimdi. İşçi tercihi korunur.
/// Konum: sol-üst hücre (sütun, satır). Kurallar <see cref="DepotService"/>'tedir.
/// </summary>
public sealed class IslandDepotAdapter : IDepotRegion
{
    public static readonly IslandDepotAdapter Instance = new IslandDepotAdapter();

    public int Region => DepotService.RegionIsland;

    private static IslandSaveData Save => GameManager.Instance != null && GameManager.Instance.PlayerData != null ? GameManager.Instance.PlayerData.island : null;

    public DepotItem Snapshot(string id, int category)
    {
        var save = Save;
        var b = save?.placed?.Find(p => p != null && p.id == id);
        if (b == null || category != DepotService.CategoryBuilding) return null;
        return new DepotItem { id = b.id, type = b.kind, level = Mathf.Max(1, b.level), stock = b.stock, workers = b.workers, category = category };
    }

    public bool Despawn(DepotItem item)
    {
        var save = Save;
        if (save?.placed == null) return false;
        // Stok şimdiye getirilir ve öğeye yazılır: depoya giden üretim kaybolmaz, depodayken artmaz.
        IslandEconomy.Accumulate(save, IslandEconomy.Now);
        var b = save.placed.Find(p => p != null && p.id == item.id);
        if (b == null) return false;
        item.stock = Mathf.Max(0f, b.stock);
        item.workers = b.workers;
        item.level = Mathf.Max(1, b.level);
        save.placed.Remove(b);
        save.buildings?.RemoveAll(e => e != null && e.id == item.id);   // hasarlı bina zaten reddedilir; tam can kaydı kalmasın
        IslandVillage.Instance?.OnStored(item.id);
        return true;
    }

    public bool Spawn(DepotItem item, Vector2 at)
    {
        var save = Save;
        if (save?.placed == null) return false;
        var kind = IslandGrid.ParseKind(item.type);
        int col = Mathf.RoundToInt(at.x), row = Mathf.RoundToInt(at.y);
        // Taşımadaki gibi: depodan kurulum limite yeniden sayılmaz (zaten sahip olunan sayıda), diğer kurallar aynı.
        var check = IslandGrid.Check(kind, col, row, save.placed, save.zoneMask, item.id, IslandEconomy.Keep);
        if (check != IslandGrid.Reason.None) return false;
        if (save.placed.Exists(p => p != null && p.id == item.id)) return false;
        IslandEconomy.Accumulate(save, IslandEconomy.Now);   // diğer üreticiler eski işçi dağılımıyla şimdiye kadar üretsin
        var entry = new IslandPlacedBuilding
        {
            id = item.id, kind = item.type, level = Mathf.Max(1, item.level), col = col, row = row,
            stock = Mathf.Max(0f, item.stock), lastTick = IslandEconomy.Now, workers = item.workers,
        };
        save.placed.Add(entry);
        IslandVillage.Instance?.OnRestored(entry);
        return true;
    }

    public void Persist() => GameManager.Instance?.SaveGame();
}
