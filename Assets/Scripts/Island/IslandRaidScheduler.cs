using System;
using UnityEngine;

/// <summary>
/// Zamanlı baskın zamanlayıcısı ve çözümü (island_base_v2 §7.8). Saf mantık: saat <see cref="IslandRepair.Now"/> (GameClock tabanlı)
/// ya da testten enjekte edilen tick'tir; sahne gerekmez.
/// Sıklık 8 sa ± 45 dk, ilki Akın 5 kazanılınca; varış 09–22 dışına düşerse <see cref="NotificationPlanner.AvoidNight"/> ile kayar;
/// bekleyen baskın birikmez (en çok 1); oyun 24 sa+ açılmazsa baskın saati durur. T−30 dk'da görünür, T0'dan 2 sa canlı savunma
/// penceresi açılır; pencere biterse yerleşime göre çevrimdışı çözülür. Hırsızlık hiçbir yolda <see cref="Progression.Island.TheftCap"/> (%20) aşmaz.
/// </summary>
public static class IslandRaidScheduler
{
    public enum Phase { None, Warning, Live }

    /// <summary>UTC → yerel (gece kayması için). Testler sabit ofset enjekte edebilir.</summary>
    public static Func<DateTime, DateTime> ToLocal = utc => utc.ToLocalTime();

    /// <summary>Canlı savunma sürerken pencere bitişi çözümü tetiklemez (<see cref="IslandRaidManager"/> yönetir).</summary>
    public static bool LiveFightActive;

    /// <summary>Kıyı Kalesi / Okçu seviyesi kaynağı (varsayılan IslandSession); testler enjekte eder.</summary>
    public static Func<IslandSaveData, int> KeepOf = save => IslandSession.KeepLevel();
    public static Func<IslandSaveData, int> ArcherOf = save => IslandSession.ArcherLevel();

    private static long Ticks(double hours) => (long)(hours * TimeSpan.TicksPerHour);

    // ==================== DURUM ====================

    /// <summary>Bekleyen baskının varış anı T0 (0: yok).</summary>
    public static long Arrival(IslandSaveData save) => save != null ? save.nextRaidTicks : 0;
    public static long WindowEnd(IslandSaveData save) => Arrival(save) + Ticks(Progression.Island.RaidWindowHours);
    public static long WarnStart(IslandSaveData save) => Arrival(save) - Ticks(Progression.Island.RaidWarnMinutes / 60d);

    public static Phase PhaseAt(IslandSaveData save, long now)
    {
        long t0 = Arrival(save);
        if (t0 <= 0) return Phase.None;
        if (now >= WindowEnd(save)) return Phase.None;      // pencere bitti: Tick çözecek
        if (now >= t0) return Phase.Live;
        return now >= WarnStart(save) ? Phase.Warning : Phase.None;
    }

    /// <summary>Rozet için: varışa (Warning / Sakin) ya da pencere bitişine (Live) kalan saniye; planlı baskın yoksa −1.</summary>
    public static double SecondsLeft(IslandSaveData save, long now)
    {
        long t0 = Arrival(save);
        if (t0 <= 0) return -1d;
        long target = now >= t0 ? WindowEnd(save) : t0;
        return Math.Max(0d, (target - now) / (double)TimeSpan.TicksPerSecond);
    }

    public static bool Eligible(IslandSaveData save) => save != null && save.highestAkin >= Progression.Island.RaidFirstAkin;

    // ==================== PLANLAMA ====================

    /// <summary>UTC anını yerel 09–22 aralığına kaydırır (AvoidNight yalnız ileri alır).</summary>
    public static long AvoidNightTicks(long utcTicks)
    {
        var utc = new DateTime(utcTicks, DateTimeKind.Utc);
        var local = ToLocal(utc);
        var adjusted = NotificationPlanner.AvoidNight(local);
        return utcTicks + (adjusted - local).Ticks;
    }

    /// <summary>Sonraki varış: from + 8 sa ± 45 dk, sonra gece kayması.</summary>
    public static long NextArrival(long from, System.Random rng)
    {
        double jitterMinutes = (rng.NextDouble() * 2d - 1d) * Progression.Island.RaidJitterMinutes;
        long t = from + Ticks(Progression.Island.RaidIntervalHours) + (long)(jitterMinutes * TimeSpan.TicksPerMinute);
        return AvoidNightTicks(t);
    }

    /// <summary>Açık kumsallardan (en yüksek Akın'a göre) rastgele biri.</summary>
    public static int PickBeach(int highestAkin, System.Random rng)
    {
        int open = Mathf.Clamp(Progression.Island.BeachesOpen(Mathf.Max(1, highestAkin)), 1, IslandLayout.Beaches.Length);
        return rng.Next(open);
    }

    /// <summary>Bir sonraki baskını <paramref name="from"/> anından planlar (hedef kumsal tohumdan seçilir).</summary>
    public static void Schedule(IslandSaveData save, long from)
    {
        if (save == null) return;
        save.raidSerial++;
        int seed = unchecked(save.raidSerial * 7919 + (int)((from / TimeSpan.TicksPerSecond) % 1000003));
        var rng = new System.Random(seed);
        save.raidSeed = seed;
        save.nextRaidTicks = NextArrival(from, rng);
        save.raidBeach = PickBeach(save.highestAkin, rng);
    }

    /// <summary>
    /// Zamanlayıcıyı <paramref name="now"/> anına getirir: 24 sa+ kapalı kalma payı baskın saatini öteler, ilk baskın planlanır,
    /// pencere bitmişse (canlı savunma yoksa) çevrimdışı çözülür ve sonraki baskın şimdiden planlanır (birikmez). Çözüm olduysa true.
    /// </summary>
    public static bool Tick(IslandSaveData save, long now)
    {
        if (save == null) return false;
        long seen = save.raidSeenTicks;
        long limit = Ticks(Progression.Island.RaidClockStopHours);
        if (seen > 0 && now - seen > limit && save.nextRaidTicks > 0)
            save.nextRaidTicks = AvoidNightTicks(save.nextRaidTicks + (now - seen - limit));   // saat 24 sa sonra durdu
        save.raidSeenTicks = Math.Max(seen, now);

        if (save.nextRaidTicks <= 0)
        {
            if (Eligible(save)) Schedule(save, now);
            return false;
        }
        if (now >= WindowEnd(save) && !LiveFightActive)
        {
            ResolveOffline(save, WindowEnd(save), now);
            return true;
        }
        return false;
    }

    // ==================== SAVUNMA GÜCÜ (§7.8) ====================

    public struct Assessment
    {
        public float defense, required, fraction;
        public int towers, covering;
    }

    /// <summary>Kule hedef kumsal şeridinin herhangi bir hücresine menzildeyse kapsar (CoverFull), değilse CoverPartial.</summary>
    public static bool TowerCoversBeach(Vector2 towerWorld, float range, int beach)
    {
        var path = IslandLayout.Beaches[Mathf.Clamp(beach, 0, IslandLayout.Beaches.Length - 1)].walkPath;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 a = path[i];
            Vector2 b = i + 1 < path.Length ? path[i + 1] : path[i];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                var cell = new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f);   // şerit hücresinin merkezi
                if (Vector2.Distance(towerWorld, cell) <= range) return true;
            }
        }
        return false;
    }

    /// <summary>Savunma değerlendirmesi: D, R ve çalınacak oran. Kışla ve bayrak henüz yok (Faz E): 0 / yolda değil.</summary>
    public static Assessment Assess(IslandSaveData save, int beach, int keepLevel, int archerLevel, int barracksLevel = 0, bool flagOnRoute = false)
    {
        var result = new Assessment();
        float d = 0f;
        if (save != null)
        {
            string towerKind = IslandLayout.BuildingKind.Tower.ToString();
            foreach (var b in save.placed)
            {
                if (b == null || b.level < 1 || b.kind != towerKind) continue;
                result.towers++;
                var cell = IslandGrid.CenterCell(IslandLayout.BuildingKind.Tower, b.col, b.row);
                int height = Mathf.Max(0, IslandLayout.HeightAt(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y)));
                float range = Progression.Island.TowerRange + (height >= 1 ? Progression.Island.HighGroundRangeBonus : 0f);
                bool covers = TowerCoversBeach(IslandLayout.World(cell), range, beach);
                if (covers) result.covering++;
                d += Progression.Island.RaidDefTower * b.level * (covers ? Progression.Island.CoverFull : Progression.Island.CoverPartial);
            }
        }
        d += Progression.Island.RaidDefBarracks * Mathf.Max(0, barracksLevel) * (flagOnRoute ? 1f : Progression.Island.FlagOffRoute);
        d += Progression.Island.RaidDefArcher * Mathf.Max(0, archerLevel);
        d += Progression.Island.RaidDefKeep * Mathf.Max(0, keepLevel);
        float r = Progression.Island.RaidReqPerKeep * Mathf.Max(1, keepLevel);
        result.defense = d;
        result.required = r;
        result.fraction = TheftFraction(d, r);
        return result;
    }

    /// <summary>Çalınan oran = TheftCap × clamp01((R − D) / R). D ≥ 0 olduğundan sonuç ≤ TheftCap (D = 0'da bile).</summary>
    public static float TheftFraction(float d, float r)
    {
        if (r <= 0f) return Progression.Island.TheftCap;
        return Mathf.Min(Progression.Island.TheftCap, Progression.Island.TheftCap * Mathf.Clamp01((r - Mathf.Max(0f, d)) / r));
    }

    // ==================== ÇÖZÜM ====================

    public struct Outcome
    {
        public int beach, towersCovering, wood, meat, gold;
        public float fraction;
        public bool won;
        public string kind;
    }

    /// <summary>Bir üreticinin çalınabilir miktarı: toplanmamış stok, en çok RaidStockHours saatlik üretim.</summary>
    public static float Stealable(IslandPlacedBuilding b) =>
        Mathf.Min(b.stock, IslandEconomy.FullRate(b) * Progression.Island.RaidStockHours);

    /// <summary>Oranı üreticilerin çalınabilir stokuna uygular (en çok TheftCap). Çalınan (Odun, Et) döner; stoktan düşülür.</summary>
    public static (int wood, int meat) ApplyTheft(IslandSaveData save, float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, Progression.Island.TheftCap);
        int wood = 0, meat = 0;
        if (save == null || fraction <= 0f) return (0, 0);
        foreach (var b in save.placed)
        {
            if (b == null || b.level < 1) continue;
            var kind = IslandEconomy.KindOf(b);
            if (!IslandEconomy.IsProducer(kind)) continue;
            int take = Mathf.FloorToInt(Stealable(b) * fraction);
            if (take <= 0) continue;
            b.stock -= take;
            if (kind == IslandLayout.BuildingKind.Lumber) wood += take; else meat += take;
        }
        return (wood, meat);
    }

    private static void Report(IslandSaveData save, Outcome outcome)
    {
        save.raidsResolved++;
        save.raidReportKind = outcome.kind;
        save.raidReportBeach = outcome.beach;
        save.raidReportTowers = outcome.towersCovering;
        save.raidReportWood = outcome.wood;
        save.raidReportMeat = outcome.meat;
        save.lastReport = ReportText(save);
    }

    /// <summary>Çevrimdışı çözüm: pencere bitişindeki stokla hesaplanır, sonraki baskın <paramref name="now"/>'dan planlanır.</summary>
    public static Outcome ResolveOffline(IslandSaveData save, long windowEnd, long now)
    {
        IslandEconomy.Accumulate(save, Math.Min(now, windowEnd));
        var assess = Assess(save, save.raidBeach, Mathf.Max(1, KeepOf(save)), Mathf.Max(1, ArcherOf(save)));
        var (wood, meat) = ApplyTheft(save, assess.fraction);
        var outcome = new Outcome { beach = save.raidBeach, towersCovering = assess.covering, wood = wood, meat = meat, fraction = assess.fraction, kind = "offline" };
        Report(save, outcome);
        FinishRaid(save, now);
        IslandEconomy.Accumulate(save, now);
        return outcome;
    }

    /// <summary>
    /// Canlı savunma sonucu. Kazanırsa çalıntı yalnız kumsala dönen Fare'lerden (her biri %2); +%10 bekleyen üretim ve 10+2k altın.
    /// Kaybederse çevrimdışı çalıntının yarısı + Fare'ler. Toplam her durumda TheftCap'i aşmaz.
    /// </summary>
    public static Outcome CompleteLive(IslandSaveData save, bool won, int ratEscapes, long now)
    {
        IslandEconomy.Accumulate(save, now);
        int keep = Mathf.Max(1, KeepOf(save));
        var assess = Assess(save, save.raidBeach, keep, Mathf.Max(1, ArcherOf(save)));
        float fraction = (won ? 0f : assess.fraction * Progression.Island.RaidLossTheftMult) + Mathf.Max(0, ratEscapes) * Progression.Island.RatTheft;
        fraction = Mathf.Min(Progression.Island.TheftCap, fraction);
        var (wood, meat) = ApplyTheft(save, fraction);
        int gold = 0;
        if (won)
        {
            foreach (var b in save.placed)
            {
                if (b == null || b.level < 1 || !IslandEconomy.IsProducer(IslandEconomy.KindOf(b))) continue;
                b.stock = Mathf.Min(IslandEconomy.Capacity(b), b.stock * (1f + Progression.Island.RaidWinBonus));
            }
            gold = Progression.Island.RaidWinGold(keep);
        }
        var outcome = new Outcome { beach = save.raidBeach, towersCovering = assess.covering, wood = wood, meat = meat, gold = gold, fraction = fraction, won = won, kind = won ? "won" : "lost" };
        Report(save, outcome);
        FinishRaid(save, now);
        return outcome;
    }

    private static void FinishRaid(IslandSaveData save, long now)
    {
        save.nextRaidTicks = 0;
        if (Eligible(save)) Schedule(save, now);   // birikmez: sonraki baskın şimdiden sayılır
    }

    // ==================== RAPOR / DEV ====================

    public static string BeachName(int beach) => IslandLayout.Beaches[Mathf.Clamp(beach, 0, IslandLayout.Beaches.Length - 1)].name.ToString();

    private static string StolenText(int wood, int meat) =>
        wood > 0 && meat > 0 ? L.T($"{wood} Odun ve {meat} Et", $"{wood} Wood and {meat} Meat")
        : wood > 0 ? L.T($"{wood} Odun", $"{wood} Wood")
        : meat > 0 ? L.T($"{meat} Et", $"{meat} Meat") : "";

    /// <summary>Rapor satırı (§7.8): "Korsanlar B kumsalına çıktı, 2 kuleye takıldı, 60 Et çaldı."</summary>
    public static string ReportText(IslandSaveData save)
    {
        if (save == null || string.IsNullOrEmpty(save.raidReportKind)) return "";
        string beach = BeachName(save.raidReportBeach);
        string stolen = StolenText(save.raidReportWood, save.raidReportMeat);
        if (save.raidReportKind == "won")
            return stolen.Length > 0
                ? L.T($"Korsanları {beach} kumsalında püskürttün; yine de {stolen} kaçırdılar.", $"You drove the pirates off beach {beach}; they still got away with {stolen}.")
                : L.T($"Korsanları {beach} kumsalında püskürttün.", $"You drove the pirates off beach {beach}.");
        string towers = L.T($"{save.raidReportTowers} kuleye takıldı", save.raidReportTowers == 1 ? "1 tower slowed them" : $"{save.raidReportTowers} towers slowed them");
        return stolen.Length > 0
            ? L.T($"Korsanlar {beach} kumsalına çıktı, {towers}, {stolen} çaldı.", $"Pirates landed on beach {beach}, {towers}, stole {stolen}.")
            : L.T($"Korsanlar {beach} kumsalına çıktı, {towers}, hiçbir şey çalamadı.", $"Pirates landed on beach {beach}, {towers}, stole nothing.");
    }

    /// <summary>Okunmamış raporu bir kez verir (AwayReport / ada HUD'u). Yoksa boş.</summary>
    public static string TakeReport(IslandSaveData save)
    {
        if (save == null || string.IsNullOrEmpty(save.raidReportKind)) return "";
        string text = ReportText(save);
        save.raidReportKind = "";
        return text;
    }

    /// <summary>DevMenu "BASKINI GETİR": baskını T−30 dk'ya alır (yoksa planlayıp). Kumsal korunur.</summary>
    public static void DevBring(IslandSaveData save, long now)
    {
        if (save == null) return;
        if (save.nextRaidTicks <= 0) Schedule(save, now);
        save.nextRaidTicks = now + (long)(Progression.Island.RaidWarnMinutes * TimeSpan.TicksPerMinute);
    }

    /// <summary>DevMenu "ZAMAN +N": baskın zamanlarını (ve üretim damgalarını) geçmişe kaydırır; kalan süre kısalır.</summary>
    public static void DevShift(IslandSaveData save, long ticks)
    {
        if (save == null) return;
        if (save.nextRaidTicks > 0) save.nextRaidTicks -= ticks;
        if (save.raidSeenTicks > 0) save.raidSeenTicks -= ticks;
        foreach (var b in save.placed) if (b != null && b.lastTick > 0) b.lastTick -= ticks;
    }

    private static float nextTickAt;

    /// <summary>GameManager.Update: ~2 sn'de bir gerçek saatle çalışır; çözüm olursa kaydeder.</summary>
    public static void TickIfAny()
    {
        if (Time.unscaledTime < nextTickAt) return;
        nextTickAt = Time.unscaledTime + 2f;
        var save = IslandSession.Save;
        if (save == null) return;
        if (Tick(save, IslandRepair.Now)) GameManager.Instance?.SaveGame();
    }
}
