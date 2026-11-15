using System.Collections.Generic;
using UnityEngine;

public enum NatureKind { Tree, Bush, Rock, Stump }

/// <summary>
/// Kilitli araziden gelen doğa nesnesi (ağaç, çalı, kaya, kütük). Arazi genişleyince kalır; yerleştirmeyi
/// engeller (hücre kaydı <see cref="ByCell"/>) ve oyuncu altın karşılığı kaldırabilir.
/// Kimlik: tür + hücre (kayıt: PlayerSaveData.removedNatureIds).
/// </summary>
public sealed class NatureObstacle : MonoBehaviour
{
    /// <summary>
    /// 8. oturum (D100): hücreye göre doğa nesneleri — artık elle yazılan statik sözlük DEĞİL, sahnedeki etkin ve kaldırılmamış
    /// nesnelerden kurulan doluluk anlık görüntüsünün görünümü (<see cref="BaseOccupancyGrid.NatureByCell"/>). Yok edilmiş/kaldırılmış
    /// nesnenin hayalet kaydı kalamaz.
    /// </summary>
    public static IReadOnlyDictionary<Vector2Int, NatureObstacle> ByCell => BaseOccupancyGrid.NatureByCell;

    /// <summary>Kaldırıldı (altın/temizleme): aynı karede Destroy beklenirken bile doluluğa girmez.</summary>
    public bool Removed { get; private set; }

    public NatureKind kind;
    [SerializeField] private Vector2Int _cell;

    /// <summary>
    /// 5. oturum (1.1): hücre atanınca kayıt güncellenir. Eskiden alan olarak AddComponent'tan SONRA atanıyordu; OnEnable o anda (0,0)
    /// ile çalıştığından gerçek hücre hiç kaydolmuyor, (0,0)'da hayalet kayıt kalıyordu.
    /// </summary>
    public Vector2Int cell
    {
        get => _cell;
        set
        {
            _cell = value;
            BaseOccupancyGrid.MarkDirty();
        }
    }

    public string Id => MakeId(kind, cell);
    public static string MakeId(NatureKind kind, Vector2Int cell) => $"{kind}_{cell.x}_{cell.y}";

    public int RemoveCost => Progression.NatureRemoveCost(kind);

    public string DisplayName => kind == NatureKind.Tree ? L.T("Ağaç", "Tree")
        : kind == NatureKind.Bush ? L.T("Çalı", "Bush")
        : kind == NatureKind.Rock ? L.T("Kaya", "Rock") : L.T("Kütük", "Stump");

    private void OnEnable()
    {
        BaseOccupancyGrid.MarkDirty();
        if (GameManager.Instance != null) GameManager.Instance.OnBuilderTaskCompleted += OnTaskCompleted;   // 6. oturum (4.2)
    }

    private void OnDisable()
    {
        BaseOccupancyGrid.MarkDirty();
        if (GameManager.Instance != null) GameManager.Instance.OnBuilderTaskCompleted -= OnTaskCompleted;
    }

    /// <summary>Hücre dikdörtgeninde doğa nesnesi var mı?</summary>
    public static bool AnyIn(RectInt cells)
    {
        if (ByCell.Count == 0) return false;
        for (int y = cells.yMin; y < cells.yMax; y++)
        for (int x = cells.xMin; x < cells.xMax; x++)
            if (ByCell.ContainsKey(new Vector2Int(x, y))) return true;
        return false;
    }

    /// <summary>Altın harcayıp nesneyi kaldırır ve kayda yazar. Altın yetmezse false.</summary>
    /// <summary>6. oturum (4.2): görev kimliği (inşaatçı "remove" görevinin slotId'si).</summary>
    public string TaskSlot => "nature:" + Id;

    /// <summary>
    /// Süreli temizleme: sıraya girer; inşaatçı boşalınca altın ödenir, inşaatçı gelip temizler (süre FeatureConfig'ten), bitince ödül.
    /// Bitiş GameManager'da kayda yazılır (oyun kapalıyken de), sahnedeki nesne <see cref="OnTaskCompleted"/> ile söner.
    /// </summary>
    public bool RequestClear()
    {
        var gm = GameManager.Instance;
        if (gm == null || NatureClear.IsQueued(Id) || gm.TaskForSlot(TaskSlot) != null) return false;
        var size = NatureClear.SizeOf(kind);
        NatureClear.Enqueue(Id, transform, size, () =>
        {
            if (this == null) return null;
            if (!gm.HasFreeBuilder) return false;
            if (!gm.SpendGold(RemoveCost))
            {
                BaseSceneSetup.Instance?.ShowHudToast(L.T("Yeterli altın yok", "Not enough gold"));
                return null;
            }
            bool ok = BuilderSystem.Instance != null
                ? BuilderSystem.Instance.TryStartTask("Kaldırma", NatureClear.SecondsFor(size), transform.position, null, TaskSlot)
                : gm.StartBuilderTask("remove", "Kaldırma", NatureClear.SecondsFor(size), transform.position, TaskSlot);
            if (!ok) { gm.AddGold(RemoveCost); return false; }
            return true;
        }, () => GameManager.TaskProgress(gm.TaskForSlot(TaskSlot)));
        return true;
    }


    private void OnTaskCompleted(BuilderTaskSave task)
    {
        if (task == null || task.slotId != TaskSlot) return;
        NatureClear.Finish(Id, transform.position, RewardText(kind));
        MarkRemoved();
    }

    /// <summary>Ödülü verir (GameManager görev bitişinde çağırır; sahne olmasa da).</summary>
    public static string GrantReward(NatureKind kind)
    {
        var c = FeatureConfig.Instance;
        switch (kind)
        {
            case NatureKind.Tree: IslandEconomy.AddWood(c.clearRewardTreeWood); break;
            case NatureKind.Stump: IslandEconomy.AddWood(c.clearRewardStumpWood); break;
            case NatureKind.Bush: IslandEconomy.AddMeat(c.clearRewardBushMeat); break;
            default: GameManager.Instance?.AddGold(c.clearRewardRockGold); break;
        }
        return RewardText(kind);
    }

    public static string RewardText(NatureKind kind)
    {
        var c = FeatureConfig.Instance;
        switch (kind)
        {
            case NatureKind.Tree: return L.T($"+{c.clearRewardTreeWood} Odun", $"+{c.clearRewardTreeWood} Wood");
            case NatureKind.Stump: return L.T($"+{c.clearRewardStumpWood} Odun", $"+{c.clearRewardStumpWood} Wood");
            case NatureKind.Bush: return L.T($"+{c.clearRewardBushMeat} Et", $"+{c.clearRewardBushMeat} Meat");
            default: return L.T($"+{c.clearRewardRockGold} Altın", $"+{c.clearRewardRockGold} Gold");
        }
    }

    public bool TryRemove()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.SpendGold(RemoveCost)) return false;
        var data = gm.PlayerData;
        if (data.removedNatureIds == null) data.removedNatureIds = new List<string>();
        if (!data.removedNatureIds.Contains(Id)) data.removedNatureIds.Add(Id);
        BaseMapManager.Instance?.SaveAll();
        BaseBuildingVisual.SpawnDust(transform.position, 110);
        MarkRemoved();
        return true;
    }

    /// <summary>
    /// Kaldırma tek yerden: görsel (sprite + gölge çocukları), collider ve doluluk AYNI KAREDE kapanır, sonra nesne yok edilir.
    /// Eskiden yalnız sözlük kaydı silinip Destroy çağrılıyordu (Destroy kare sonunu bekler; kayıt elle tutulduğundan hayalet kalabiliyordu).
    /// </summary>
    public void MarkRemoved()
    {
        Removed = true;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        enabled = false;
        BaseOccupancyGrid.MarkDirty();
        Destroy(gameObject);
    }
}
