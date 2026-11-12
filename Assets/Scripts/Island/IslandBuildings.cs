using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Ada binası (island_base_v2 §5, §7.5, §7.7): her bina savaşta bir <see cref="BattleStructure"/> olur (can §5 tablosu).
/// Yıkılan bina yok edilmez: soluk sprite + Tiny Swords Dust/Fire ile kalıntı olarak durur. Hasarlı binada çekiç simgesi,
/// kalan süre ve çekiç vuran bir Usta piyon görünür; onarım ücretsizdir, yalnız bekleme (§7.7). Kalıcı durum kayıttadır.
/// Faz B gelince seviye <see cref="LevelOf"/> üzerinden gerçek bina seviyesine bağlanır (şimdilik sabit hazır köy).
/// </summary>
public sealed class IslandBuilding : MonoBehaviour
{
    private static readonly List<IslandBuilding> all = new List<IslandBuilding>();
    public static IReadOnlyList<IslandBuilding> All => all;
    /// <summary>Piyonların durduğu kök (onarım piyonu buraya eklenir); savaşta kök kapalıdır.</summary>
    public static Transform PawnsParent;

    public IslandLayout.BuildingKind Kind { get; private set; }
    public string Id { get; private set; }
    public int Level { get; private set; }
    public BattleStructure Structure { get; private set; }
    public Vector2 Center { get; private set; }
    public float Radius { get; private set; }
    public long RepairEndTicks { get; private set; }
    public bool IsCastle => Kind == IslandLayout.BuildingKind.Castle;
    public bool IsTower => Kind == IslandLayout.BuildingKind.Tower;
    public bool IsBarracks => Kind == IslandLayout.BuildingKind.Barracks;
    public bool IsMonastery => Kind == IslandLayout.BuildingKind.Monastery;
    public bool IsProduction => Kind == IslandLayout.BuildingKind.House || Kind == IslandLayout.BuildingKind.Lumber
        || Kind == IslandLayout.BuildingKind.Pasture;
    public bool Ruined => Structure == null || Structure.IsDestroyed;
    public bool UnderRepair => RepairEndTicks > 0;
    public float RepairSecondsLeft => Mathf.Max(0f, (RepairEndTicks - IslandRepair.Now) / (float)TimeSpan.TicksPerSecond);

    private SpriteRenderer sr;
    private IslandLayout.BuildingSpot spot;
    private GameObject hammer, fire;
    private TextMeshPro timerText;
    private IslandPawn repairPawn;
    private Color baseColor = Color.white;

    /// <summary>
    /// Bina seviyesi (Faz B: kayıttan). Kale = Kıyı Kalesi; Okçuluk = Okçu Kaptan (Kıyı Kalesi'ni izler); diğerleri yerleştirilmiş
    /// bina kaydındaki seviye. Kayıt/kimlik yoksa Faz C hazır köy seviyesi (yedek).
    /// </summary>
    public static int LevelOf(IslandLayout.BuildingKind kind, int keepLevel, string id = null)
    {
        if (kind == IslandLayout.BuildingKind.Castle) return keepLevel;
        if (kind == IslandLayout.BuildingKind.Archery) return Mathf.Max(1, IslandSession.ArcherLevel());
        var entry = id != null ? IslandEconomy.Find(id) : null;
        if (entry != null && entry.level >= 1) return entry.level;
        return Progression.Island.PrebuiltLevel(kind.ToString(), keepLevel);
    }

    public Bounds Bounds => sr != null ? sr.bounds : new Bounds(Center, Vector3.one);
    public IslandLayout.BuildingSpot Spot => spot;

    public static IslandBuilding Attach(SpriteRenderer renderer, IslandLayout.BuildingSpot buildingSpot, int keepLevel, string id = null)
    {
        var go = renderer.gameObject;
        var building = go.AddComponent<IslandBuilding>();
        building.sr = renderer;
        building.spot = buildingSpot;
        building.Kind = buildingSpot.kind;
        building.Id = id ?? $"{buildingSpot.kind}@{buildingSpot.col},{buildingSpot.row}";
        building.Level = LevelOf(buildingSpot.kind, keepLevel, building.Id);
        building.baseColor = renderer.color;

        float hp;
        BuildingType type;
        if (building.IsCastle)
        {
            building.Center = IslandLayout.CastleCenter;
            building.Radius = IslandLayout.CastleRadius;
            hp = Progression.Island.CastleHp(keepLevel);
            type = BuildingType.MainBase;
        }
        else
        {
            building.Center = buildingSpot.FeetWorld + Vector2.up * (buildingSpot.h * 0.5f);
            building.Radius = Mathf.Max(buildingSpot.w, buildingSpot.h) * 0.5f + 0.2f;
            hp = Progression.Island.StructureHp(buildingSpot.kind.ToString(), building.Level);
            type = building.IsTower ? BuildingType.ArcherTower : BuildingType.GoldMine;   // yalnız tür etiketi
        }
        float visualHeight = building.IsCastle ? 2.3f : Mathf.Max(0.8f, renderer.bounds.max.y - building.Center.y);
        var structure = go.AddComponent<BattleStructure>();
        structure.Initialize(type, building.Level, null, building.Center, visualHeight);
        structure.SetMaxHealth(hp);
        structure.KeepOnCollapse = true;
        structure.Destroyed += building.OnCollapsed;
        building.Structure = structure;
        if (building.IsTower) go.AddComponent<IslandTower>();
        if (building.IsBarracks) go.AddComponent<IslandBarracks>();     // Faz E1: savaşta Lancer doğurur
        if (building.IsMonastery) go.AddComponent<IslandMonastery>();   // Faz E1: Monk şifa verir
        IslandRepair.Apply(building);
        return building;
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void OnDestroy()
    {
        if (hammer != null) Destroy(hammer);
        if (fire != null) Destroy(fire);
        if (repairPawn != null) Destroy(repairPawn.gameObject);
    }

    // ==================== YIKIM / KALINTI ====================

    private void OnCollapsed(BattleStructure structure)
    {
        IslandStats.BuildingsLost++;
        SetRuinVisual(true);
        IslandFx.Play("dust_1", Center, 2.4f, 14f);
        IslandFx.Play("explosion_1", Center + Vector2.up * 0.3f, 2.2f, 16f);
    }

    /// <summary>Kalıntı: soluk sprite + yanan enkaz (Tiny Swords Fire, bina yıkıksa).</summary>
    public void SetRuinVisual(bool ruined)
    {
        if (sr != null) sr.color = ruined ? new Color(0.5f, 0.46f, 0.46f, 0.78f) : baseColor;
        if (ruined && fire == null)
            fire = IslandFx.Play("fire_" + (1 + Mathf.Abs(Id.GetHashCode()) % 3), Center + Vector2.down * 0.2f, 1.3f, 10f, true, null,
                sr != null ? sr.sortingOrder + 3 : 500);
        else if (!ruined && fire != null) { Destroy(fire); fire = null; }
    }

    // ==================== ONARIM ====================

    /// <summary>Savaş sonrası: kayıp can oranına göre onarım süresini başlatır (Kale hariç: savaştan sonra tam canla döner).</summary>
    public void CommitAfterBattle()
    {
        if (Structure == null) return;
        if (IsCastle)
        {
            Structure.SetHealthSilently(Structure.MaxHealth);
            SetRuinVisual(false);
            RepairEndTicks = 0;
            return;
        }
        float fraction = Structure.HealthPercent;
        if (fraction >= 0.999f && !UnderRepair) return;
        if (fraction >= 0.999f) { FinishRepair(false); return; }
        bool destroyed = fraction <= 0f;
        float seconds = Progression.Island.RepairSeconds(1f - fraction, destroyed, IslandEconomy.HasMonastery());   // Faz E1: Manastır ×0,5
        RepairEndTicks = IslandRepair.Now + (long)(seconds * TimeSpan.TicksPerSecond);
        savedFraction = fraction;
        if (destroyed) SetRuinVisual(true);
    }

    private float savedFraction = 1f;
    public float SavedFraction => savedFraction;

    /// <summary>Kayıttan (sahne kurulumu): hasarlı/yıkık başlar ya da onarım bitmişse tam can.</summary>
    public void LoadState(float fraction, long repairEnd)
    {
        savedFraction = fraction;
        RepairEndTicks = repairEnd;
        Structure.SetHealthSilently(Structure.MaxHealth * fraction);
        SetRuinVisual(fraction <= 0f);
    }

    public void FinishRepair(bool persist = true)
    {
        RepairEndTicks = 0;
        savedFraction = 1f;
        if (Structure != null) Structure.SetHealthSilently(Structure.MaxHealth);
        SetRuinVisual(false);
        if (hammer != null) { Destroy(hammer); hammer = null; timerText = null; }
        if (repairPawn != null) { Destroy(repairPawn.gameObject); repairPawn = null; }
        if (sr != null) sr.color = baseColor;
        if (persist) IslandRepair.SaveAll();
    }

    private void Update()
    {
        if (!UnderRepair) return;
        bool defense = IslandSceneSetup.Instance != null && IslandSceneSetup.Instance.InDefense;
        EnsureRepairVisuals(!defense);
        if (defense) return;
        if (timerText != null)
        {
            int left = Mathf.CeilToInt(RepairSecondsLeft);
            timerText.text = $"{left / 60}:{left % 60:00}";
        }
        if (IslandRepair.Now >= RepairEndTicks) FinishRepair();
    }

    private void EnsureRepairVisuals(bool show)
    {
        if (hammer == null)
        {
            var catalog = IslandVisualCatalog.Instance;
            var sprite = catalog != null ? catalog.Single("icon_hammer") : null;
            hammer = new GameObject("RepairHammer");
            hammer.transform.SetParent(transform, true);
            hammer.transform.position = new Vector3(Center.x, sr.bounds.max.y + 0.55f, 0f);
            var icon = hammer.AddComponent<SpriteRenderer>();
            icon.sprite = sprite;
            icon.sortingOrder = IslandFx.FxSorting;
            hammer.transform.localScale = Vector3.one * 0.8f;
            hammer.transform.DOLocalRotate(new Vector3(0f, 0f, 14f), 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(hammer);
            var textObject = new GameObject("RepairTimer");
            textObject.transform.SetParent(hammer.transform, false);
            textObject.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            timerText = textObject.AddComponent<TextMeshPro>();
            timerText.fontSize = 3.2f;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color = new Color(1f, 0.92f, 0.65f);
            timerText.sortingOrder = IslandFx.FxSorting + 1;
        }
        if (hammer.activeSelf != show) hammer.SetActive(show);
        if (show && repairPawn == null && PawnsParent != null)
        {
            var catalog = IslandVisualCatalog.Instance;
            Vector2 door = IslandLayout.World(spot.DoorCell) + new Vector2(IsCastle ? 1.4f : 0.9f, -0.15f);
            repairPawn = IslandPawn.Create(PawnsParent, catalog, IslandPawn.Job.Builder, door);
            repairPawn.SetSite(door);   // onarım çekici vuran Usta
        }
    }
}

/// <summary>
/// Gözcü Kulesi (§7.5): sefer Okçu Kulesi eğrisiyle (<c>Progression.TowerDamage/TowerUnits</c>) tekneleri, kıyıya çıkanları
/// ve uçanları vurur. Menzil 6 (+1,5 yükseklik ≥1). Adacık kulesine yakın dövüşçü ulaşamaz (sudaki ada); menzililer vurabilir.
/// </summary>
public sealed class IslandTower : MonoBehaviour
{
    public const float FireRate = 1.25f;   // ArcherTower: attackSpeed (BuildingData.ApplyLevelStats)
    private IslandBuilding building;
    private float nextShot, webbedUntil;
    public bool Webbed => Time.time < webbedUntil;
    /// <summary>Örümcek ağı: <paramref name="seconds"/> sn atış hızı −%40.</summary>
    public void Web(float seconds) { webbedUntil = Mathf.Max(webbedUntil, Time.time + seconds); IslandStats.WebHits++; }
    public float Range { get; private set; }
    public float Damage { get; private set; }
    public int Shots { get; private set; }

    private void Start()
    {
        building = GetComponent<IslandBuilding>();
        int level = building != null ? building.Level : 1;
        int height = building != null ? Mathf.Max(0, IslandLayout.HeightAt((int)building.Center.x, (int)(IslandLayout.Height - building.Center.y))) : 0;
        Range = Progression.Island.TowerRange + (height >= 1 ? Progression.Island.HighGroundRangeBonus : 0f);
        Damage = Progression.TowerDamage(BuildingType.ArcherTower, level) * Progression.TowerUnits(BuildingType.ArcherTower, level);
    }

    private void Update()
    {
        if (building == null || building.Ruined || Time.time < nextShot) return;
        if (IslandSceneSetup.Instance == null || !IslandSceneSetup.Instance.InDefense) return;
        IslandTarget best = null;
        float bestDistance = Range;
        Vector2 origin = building.Center;
        foreach (var target in IslandTarget.Collect())
        {
            float d = Vector2.Distance(origin, target.Position);
            if (d < bestDistance) { bestDistance = d; best = target; }
        }
        if (best == null) return;
        nextShot = Time.time + 1f / FireRate / (Time.time < webbedUntil ? Progression.Island.WebFireRateMult : 1f);
        Shots++;
        IslandArrow.Fire(origin + Vector2.up * 1.3f, best, Damage, false);
        transform.DOComplete();
        transform.DOPunchScale(new Vector3(0.03f, -0.05f, 0f), 0.15f, 6, 0.6f).SetLink(gameObject);
    }
}

/// <summary>Onarım kaydı (§7.7): savaş sonrası süre yazar, sahne kurulumunda uygular. Zaman kaynağı testlerde değiştirilebilir.</summary>
public static class IslandRepair
{
    public static Func<long> Clock = () => GameClock.UtcNow.Ticks;   // Faz B: telefon saatini geri alma hilesi üretimi bozmaz
    public static long Now => Clock();

    /// <summary>
    /// [from, to] arası "etkin üretim saniyesi": onarım süren pencerede (en geç biten onarıma kadar) üretim
    /// <see cref="Progression.Island.RepairProductionMult"/> ile çarpılır (§7.7). Kayıttaki ve canlı onarımlara bakar.
    /// </summary>
    public static double EffectiveSeconds(IslandSaveData save, long from, long to)
    {
        if (to <= from) return 0d;
        long repairUntil = 0;
        if (save != null)
            foreach (var entry in save.buildings) if (entry != null) repairUntil = Math.Max(repairUntil, entry.repairEndTicks);
        foreach (var building in IslandBuilding.All)
            if (building != null && building.UnderRepair) repairUntil = Math.Max(repairUntil, building.RepairEndTicks);
        double total = (to - from) / (double)TimeSpan.TicksPerSecond;
        if (repairUntil <= from) return total;
        long slowTo = Math.Min(to, repairUntil);
        double slow = (slowTo - from) / (double)TimeSpan.TicksPerSecond;
        return total - slow * (1.0 - Progression.Island.RepairProductionMult);
    }

    /// <summary>Faz B üretimi bu çarpanla yavaşlatır: onarım sürerken ×0,5 (üretim yoksa yalnız durum/zamanlayıcı görünür).</summary>
    public static float ProductionMultiplier()
    {
        foreach (var building in IslandBuilding.All)
            if (building != null && building.UnderRepair) return Progression.Island.RepairProductionMult;
        var save = IslandSession.Save;
        if (save != null)
            foreach (var entry in save.buildings) if (entry.repairEndTicks > Now) return Progression.Island.RepairProductionMult;
        return 1f;
    }

    public static void Apply(IslandBuilding building)
    {
        var save = IslandSession.Save;
        if (save == null) return;
        var entry = save.buildings.Find(e => e.id == building.Id);
        if (entry == null) return;
        if (entry.repairEndTicks <= Now)
        {
            save.buildings.Remove(entry);   // süre bitti (çevrimdışıyken de): tam can
            return;
        }
        building.LoadState(entry.hpFraction, entry.repairEndTicks);
    }

    /// <summary>Savaş bitti (zafer/yenilgi): tüm binaların durumunu kaydeder.</summary>
    public static void CommitBattle()
    {
        foreach (var building in new List<IslandBuilding>(IslandBuilding.All))
            if (building != null) building.CommitAfterBattle();
        SaveAll();
    }

    public static void SaveAll()
    {
        var save = IslandSession.Save;
        if (save == null) return;
        save.buildings.Clear();
        foreach (var building in IslandBuilding.All)
        {
            if (building == null || !building.UnderRepair) continue;
            save.buildings.Add(new IslandBuildingSave { id = building.Id, hpFraction = building.SavedFraction, repairEndTicks = building.RepairEndTicks });
        }
        GameManager.Instance?.SaveGame();
    }
}
