using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 4. oturum (1.4): adadaki çalı ve kayalar temizlenebilir. Pişmiş <c>IslandNature</c> prefab'ındaki "Bushes"/"Rocks" gruplarının
/// çocukları (ad: "Bush{v}_{c}_{r}" / "Rock{v}_{c}_{r}") çalışma anında bulunur. Dokununca küçük bedelle kaldırılır
/// (çalı: altın → odun, kaya: odun → altın), kayda "bush:c,r" / "rock:c,r" yazılır ve sahnede sönerek kaybolur (toz + parça).
/// Bir binanın altında kalan çalı/kaya (eski kayıtlar dahil) bedelsiz gizlenir.
/// </summary>
public static class IslandNatureClear
{
    public enum Kind { Bush, Rock }

    public sealed class Item
    {
        public Kind kind;
        public Vector2Int cell;
        public Transform root;
        public SpriteRenderer renderer;
        public bool Cleared;
    }

    private static readonly List<Item> items = new List<Item>();
    public static IReadOnlyList<Item> Items => items;

    public static string Key(Kind kind, Vector2Int cell) => (kind == Kind.Bush ? "bush:" : "rock:") + cell.x + "," + cell.y;

    public static bool IsCleared(IslandSaveData save, Kind kind, Vector2Int cell) =>
        save != null && save.clearedNature != null && save.clearedNature.Contains(Key(kind, cell));

    // Bedel/ödül (DECISIONS D45): küçük ve bir akın ödülünün çok altında.
    public const int BushGold = 5, BushWood = 4, RockWood = 3, RockGold = 10;

    public static string CostText(Kind kind) => kind == Kind.Bush
        ? L.T($"{BushGold} altın → +{BushWood} odun", $"{BushGold} gold → +{BushWood} wood")
        : L.T($"{RockWood} odun → +{RockGold} altın", $"{RockWood} wood → +{RockGold} gold");

    /// <summary>Sahnedeki doğa nesnesinden kayıtları toplar; temizlenmiş ve bina altındakileri gizler.</summary>
    public static void Bind(GameObject nature)
    {
        items.Clear();
        if (nature == null) return;
        Collect(nature.transform.Find("Bushes"), Kind.Bush, "Bush");
        Collect(nature.transform.Find("Rocks"), Kind.Rock, "Rock");
        Refresh(true);
    }

    private static void Collect(Transform group, Kind kind, string prefix)
    {
        if (group == null) return;
        foreach (Transform child in group)
        {
            // "Bush3_8_9" → c=8, r=9
            var parts = child.name.Split('_');
            if (parts.Length < 3 || !parts[0].StartsWith(prefix)) continue;
            if (!int.TryParse(parts[1], out int c) || !int.TryParse(parts[2], out int r)) continue;
            items.Add(new Item { kind = kind, cell = new Vector2Int(c, r), root = child, renderer = child.GetComponentInChildren<SpriteRenderer>() });
        }
    }

    /// <summary>Kayıt + bina ayak izine göre görünürlüğü günceller. <paramref name="migrate"/>: bina altındakileri kayda da yazar.</summary>
    public static void Refresh(bool migrate = false)
    {
        var save = IslandSession.Save;
        bool changed = false;
        foreach (var item in items)
        {
            if (item.root == null) continue;
            bool cleared = IsCleared(save, item.kind, item.cell);
            if (!cleared && UnderBuilding(save, item.cell))
            {
                cleared = true;
                if (migrate && save != null)
                {
                    if (save.clearedNature == null) save.clearedNature = new List<string>();
                    save.clearedNature.Add(Key(item.kind, item.cell));
                    changed = true;
                }
            }
            item.Cleared = cleared;
            if (item.root.gameObject.activeSelf == cleared) item.root.gameObject.SetActive(!cleared);
        }
        if (changed) GameManager.Instance?.SaveGame();
    }

    private static bool UnderBuilding(IslandSaveData save, Vector2Int cell)
    {
        if (IslandGrid.CastleRect.Contains(cell)) return true;
        if (save?.placed == null) return false;
        foreach (var b in save.placed)
            if (b != null && IslandGrid.Rect(IslandGrid.ParseKind(b.kind), b.col, b.row).Contains(cell)) return true;
        return false;
    }

    /// <summary>Dokunulan noktadaki görünür çalı/kaya (sprite sınırı, en yakın ayak).</summary>
    public static Item At(Vector2 world)
    {
        Item best = null;
        float bestDistance = float.MaxValue;
        foreach (var item in items)
        {
            if (item.Cleared || item.root == null || !item.root.gameObject.activeInHierarchy) continue;
            bool hit;
            if (item.renderer != null)
            {
                var b = item.renderer.bounds;
                b.Expand(new Vector3(0.25f, 0.25f, 10f));
                hit = b.Contains(new Vector3(world.x, world.y, b.center.z));
            }
            else hit = Vector2.Distance(world, item.root.position) < 0.5f;
            if (!hit) continue;
            float d = Vector2.Distance(world, item.root.position);
            if (d < bestDistance) { bestDistance = d; best = item; }
        }
        return best;
    }

    public static Item Find(Kind kind, Vector2Int cell) => items.Find(i => i.kind == kind && i.cell == cell);

    public static string BlockedReason(Kind kind, Vector2Int cell)
    {
        var save = IslandSession.Save;
        if (save == null) return L.T("Kayıt yok", "No save");
        if (IsCleared(save, kind, cell)) return kind == Kind.Bush ? L.T("Çalı yok", "No bush") : L.T("Kaya yok", "No rock");
        if (IslandEconomy.InDefense) return L.T("Savaşta temizlenmez", "Cannot clear during defense");
        if (!IslandGrid.ZoneOpen(IslandLayout.ZoneAt(cell.x, cell.y), save.zoneMask)) return L.T("Bölge bulutların altında", "Zone is hidden under clouds");
        if (kind == Kind.Bush && (GameManager.Instance == null || GameManager.Instance.Gold < BushGold)) return L.T("Altın yetersiz", "Not enough gold");
        if (kind == Kind.Rock && save.wood < RockWood) return L.T("Odun yetersiz", "Not enough wood");
        return null;
    }

    /// <summary>Bedeli öder, ödülü verir, kayda yazar ve görseli söndürür.</summary>
    public static bool TryClear(Kind kind, Vector2Int cell, out string reason)
    {
        reason = BlockedReason(kind, cell);
        if (reason != null) return false;
        var save = IslandSession.Save;
        if (kind == Kind.Bush)
        {
            if (!GameManager.Instance.SpendGold(BushGold)) { reason = L.T("Altın yetersiz", "Not enough gold"); return false; }
            IslandEconomy.AddWood(BushWood);
        }
        else
        {
            save.wood -= RockWood;
            GameManager.Instance.AddGold(RockGold);
        }
        if (save.clearedNature == null) save.clearedNature = new List<string>();
        save.clearedNature.Add(Key(kind, cell));
        GameManager.Instance.SaveGame();
        IslandWalkGrid.Invalidate();
        var item = Find(kind, cell);
        if (item != null) PlayRemoval(item);
        return true;
    }

    /// <summary>Kaldırma animasyonu: hafif zıplayıp küçülerek söner, toz ve (kayada) taş kırıntısı/ (çalıda) yaprak parçacığı.</summary>
    private static void PlayRemoval(Item item)
    {
        item.Cleared = true;
        var root = item.root;
        Vector2 feet = root.position;
        IslandFx.Play("dust_1", feet + Vector2.up * 0.25f, 1.1f, 14f);
        var color = item.kind == Kind.Bush ? new Color(0.45f, 0.72f, 0.3f) : new Color(0.62f, 0.6f, 0.56f);
        for (int i = 0; i < 7; i++) SpawnChip(feet + Vector2.up * 0.3f, color, item.renderer != null ? item.renderer.sortingOrder + 1 : 500);
        root.DOKill();
        var seq = DOTween.Sequence().SetLink(root.gameObject);
        seq.Append(root.DOScale(root.localScale * 1.12f, 0.08f));
        seq.Append(root.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InBack));
        if (item.renderer != null) seq.Join(item.renderer.DOFade(0f, 0.28f));
        seq.OnComplete(() => { if (root != null) root.gameObject.SetActive(false); });
    }

    private static Sprite chip;

    private static void SpawnChip(Vector2 at, Color color, int order)
    {
        if (chip == null) chip = BaseMapManager.MakeCircleSprite(Color.white);
        var go = new GameObject("NatureChip");
        go.transform.position = at;
        go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.14f) / Mathf.Max(0.01f, chip.bounds.size.x);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = chip;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector2 end = at + new Vector2(dir.x * Random.Range(0.3f, 0.6f), Mathf.Abs(dir.y) * 0.2f - 0.25f);
        go.transform.DOJump(end, Random.Range(0.25f, 0.45f), 1, 0.45f).SetLink(go);
        sr.DOFade(0f, 0.25f).SetDelay(0.3f).SetLink(go);
        Object.Destroy(go, 0.65f);
    }
}
