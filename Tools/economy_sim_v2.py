#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Mini Empire Builder - altin ekonomisi simulasyonu v2 (yalniz Python 3 stdlib).

Gercekci F2P oyuncu (elmas yok, odullu reklam var): ilk saat / ilk gun / 3. gun / ilk hafta.
LEGACY  = koddaki bugunku degerler (kaynaklar ECONOMY.md tablolarinda, dosya:satir).
V2      = Economy_V2 onerisi (ECONOMY.md "Parametre listesi").
V2+SINK = V2 + onerilen yeni altin yutaklari (Sefer Erzaki, Ada Tuccari, Prestij Anitlari).

Butun denge degerleri PROFILE sozluklerinde (LEGACY / V2 / V2_SINKS), oyuncu davranisi PLAYER sozlugunde.
Savas icerigi (dusman sayilari, dusman altini, bolge gereksinimleri) profil disidir: Assets/Resources/Campaign/region_0X.asset
ve EnemyStats.GetBaseStats (GameData.cs ~990-1170) kopyasi.

Kullanim:
  python Tools/economy_sim_v2.py                # ozet tablolar (markdown) stdout'a
  python Tools/economy_sim_v2.py --seeds 60     # tohum sayisi (varsayilan 40)
  python Tools/economy_sim_v2.py --write        # ECONOMY.md icindeki <!-- SIM:BEGIN --> ... <!-- SIM:END --> blogunu yeniler
"""
import argparse
import math
import os
import random
import statistics
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ECON_MD = os.path.join(ROOT, "ECONOMY.md")

H = 3600.0
DAY = 24 * H
MAX_LEVEL = 15
SECOND_BUILDER_MB = 5          # Progression.SecondBuilderMainBaseLevel (GameData.cs:880)
FREE_FINISH_S = 60             # GemEconomy.FreeFinishSeconds

# ==================== SAVAS ICERIGI (profil disi) ====================
REGION_REQ = [0, 1, 3, 6, 9, 12, 14]   # region_0X.asset requiredBaseLevel
# region_0X.asset enemyCounts (asama 1..5, dalga basina)
STAGE_COUNTS = {
    1: [[7], [7, 5], [8, 6], [9, 7], [8, 8, 1]],
    2: [[9], [9, 7], [10, 8], [11, 9], [10, 10, 3]],
    3: [[11], [11, 9], [12, 10], [13, 11], [12, 12, 5]],
    4: [[13], [13, 11], [14, 12], [15, 13], [14, 14, 7]],
    5: [[15], [15, 15], [16, 16], [17, 17], [16, 18, 11]],
    6: [[12], [12, 17], [13, 18], [14, 19], [14, 20, 12]],
}
# Dalga i'nin dusman turu = region.enemyTypes[min(i, len-1)]; goldDrop (GameData.cs 998/1007/1016/1052)
WAVE_DROP = {
    1: [10, 15, 25],   # Wolf, Zombie, Orc
    2: [10, 14, 15],   # Wolf, ShadowWolf, Zombie
    3: [10, 15, 25],
    4: [10, 15, 25],
    5: [10, 15, 25],
    6: [14, 15, 25],   # ShadowWolf, Zombie, Orc
}
# Boss goldDrop: CastleGuardian 100, ForestWitch 120, MineWarden 120, FrostLord 130, DarkKing 140, DragonWarrior 150
BOSS_DROP = {1: 100, 2: 120, 3: 120, 4: 130, 5: 140, 6: 150}

INSTANCES = {  # anahtar -> (tur, acilis Ana Us seviyesi)  GameData.cs:919-944, 736, 747, 757
    "Archer#1": ("Archer", 2), "Archer#2": ("Archer", 5),
    "Cannon#1": ("Cannon", 4), "Cannon#2": ("Cannon", 8),
    "Monastery#1": ("Monastery", 6), "Monastery#2": ("Monastery", 10),
    "House#1": ("House", 3), "House#2": ("House", 9),
    "Barracks#1": ("Barracks", 7), "Barracks#2": ("Barracks", 11),
    "Workshop#1": ("Workshop", 5),
}


def wave1_cap(region):            # Progression.Wave1Cap (GameData.cs:354)
    return 6 + max(1, region)


def boss_extra(region):           # Progression.BossStageExtraEnemies (GameData.cs:373)
    return 1 if region <= 1 else 3


def round5(x):
    return int(round(x / 5.0)) * 5


def round10(x):
    return int(round(x / 10.0)) * 10


# ==================== PROFILLER ====================
LEGACY = {
    "name": "LEGACY",
    # --- bina bedelleri / sureleri ---
    "mb_costs": [0, 0, 150, 250, 400, 600, 850, 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500],
    "cost_factor": {"MainBase": 1.0, "GoldMine": 0.55, "Archer": 0.5, "Cannon": 0.7, "Monastery": 0.6,
                    "House": 0.4, "Barracks": 0.65, "Workshop": 0.45},
    "upgrade_seconds": [0, 0, 10, 60, 180, 360, 600, 900, 1800, 3600, 7200, 10800, 14400, 21600, 28800, 43200],
    "builder2_cost": 850,          # KISIT: insaatci fiyati degismez (UpgradeCost(MainBase, 6))
    "workshop_build_cost": 150,
    # --- maden ---
    "mine_gps_base": 1.0, "mine_gps_slope": 0.5,
    "mine_storage_base": 200, "mine_storage_slope": 150,
    "house_bonus_base": 0.05, "house_bonus_slope": 0.005,
    "apprentice_mine_bonus": [10, 15, 20, 25, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50],
    # --- cuzdan ---
    "wallet_cap_mult": 1.5, "wallet_cap_min": 500,
    "start_gold": 50,
    # --- aninda yukseltmeler ---
    "hero_cost_base": 60, "hero_cost_growth": 1.28,
    "app_cost_base": 80, "app_cost_growth": 1.30,
    # --- savas odulu ---
    "reward_base": 35, "reward_per_region": 35, "reward_per_stage": 18,
    "kill_gold_growth": 0.08,      # savas ici (degismiyor): goldDrop * (1 + 0.08 (sev-1))
    "kill_transfer": 1.0,          # zaferde net savas altininin cuzdana gecen orani (bugun 1.0)
    "replay_rate": 0.3,
    "x2_daily_limit": None,        # None = sinirsiz (bugun)
    "battle_build_base": {"Archer": 50, "Cannon": 75, "Monastery": 60},
    "battle_build_region_slope": 0.2,
    "battle_start_gold": [(13, 75), (10, 50), (7, 25)],
    # --- gunluk ---
    "daily_day1": (50, 50), "daily_day3": (100, 100), "daily_day7_per_level": 150,
    "quest_gold": (80, 40), "quests_per_day": 3, "quest_gold_share": 2.0 / 3.0,
    "ad_chest_gold": (100, 50), "ad_chest_gold_chance": 0.6,
    "ad_mine_boost_hours": 2, "cal_mine_boost_hours": 1,
    "ad_speedups_per_day": 3, "ad_speedup_minutes": 30,
    "lives_max": 5, "lives_regen_min": 20,
    # --- atolye / ada ---
    "craft_base_gold": 180, "craft_slope": 0.15, "crafts_per_day": 2,
    "island_unlock": 5, "vein_base": 6, "vein_slope": 3, "vein_storage_hours": 8,
    # --- Sonsuz Sefer ---
    "endless_formula": "legacy", "endless_cost_k": 1.0,
    # --- yeni yutaklar (yalniz V2_SINKS) ---
    "sinks": None,
}

V2_S5 = dict(LEGACY, **{
    "name": "V2-5.oturum",  # 6. oturumdan onceki Economy_V2 (karsilastirma icin)
    "mb_costs": [0, 0, 150, 250, 400, 600, 850, 1250, 1800, 2500, 3500, 4800, 6400, 8400, 10800, 13800],
    "mine_gps_base": 0.6, "mine_gps_slope": 0.2,
    "apprentice_mine_bonus": [5, 8, 10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25],
    "hero_cost_base": 75, "hero_cost_growth": 1.30,
    "app_cost_base": 100, "app_cost_growth": 1.32,
    "reward_base": 40, "reward_per_region": 30, "reward_per_stage": 12,
    "kill_transfer": 0.75,
    "x2_daily_limit": 5,
    "endless_formula": "v2", "endless_cost_k": 1.0,
})

# 6. oturum (2.1 + 2.3): oyundaki guncel Economy_V2. Maden saatlik uretim 600 + 90(L-1); depo = 4 saatlik uretim
# (2400 + 360(L-1)); Koylu Evi +%5, seviye basina +%1; cirak katkisi = seviye kadar %.
V2 = dict(V2_S5, **{
    "name": "V2",
    "mine_gps_base": 600 / 3600.0, "mine_gps_slope": 90 / 3600.0,
    "mine_storage_base": 2400, "mine_storage_slope": 360,
    "house_bonus_base": 0.05, "house_bonus_slope": 0.01,
    "apprentice_mine_bonus": list(range(1, 16)),
})

V2_SINKS = dict(V2, **{
    "name": "V2+SINK",
    "sinks": {
        "supplies_base": 30, "supplies_per_region": 30, "supplies_win_bonus": 0.04,   # Sefer Erzaki (ilk deneme)
        "trade_base": 400, "trade_per_mb": 120, "trades_per_day": 2,                    # Ada Tuccari (50 Et paketi)
        "monuments": [(6, 1500), (9, 4000), (12, 9000), (14, 16000)],                   # Prestij Anitlari
        "monument_reserve": 1.0,
    },
})

PROFILES = [LEGACY, V2_S5, V2, V2_SINKS]

# ==================== OYUNCU ====================
PLAYER = {
    "sessions_h": [8.0, 11.0, 14.0, 17.0, 20.0, 22.5],  # 6 oturum/gun
    "first_session_min": 60,      # ilk gun ilk oturum: ilk saat (FTUE)
    "session_min": 8,
    "base_minutes": 1.0,          # oturum basinda us isleri
    "min_battle_window_min": 1.5, # bu kadar sure kaldiysa bir savas daha baslatir
    "battle_min": [2.0, 3.0, 3.0, 3.5, 4.5],
    "battle_overhead_min": 0.6,
    "win_stage": [0.98, 0.92, 0.88, 0.85, 0.70],
    "win_region": [1.0, 0.98, 0.95, 0.92, 0.90, 0.88],
    "replay_bonus": 0.08,
    "overlevel_bonus": 0.03,      # bolge gereksiniminin 1 ustundeki her Ana Us seviyesi
    "tower_lag_penalty": 0.08,    # Okcu/Topcu ort. < Ana Us - 3
    "hero_lag_penalty": 0.06,     # kahraman < Ana Us - 2
    "save_threshold": 0.5,        # Ana Us bedelinin bu kadarina sahipse tek bos insaatciyi baska ise vermez
    "instant_reserve": 0.5,       # kahraman/cirak/atolye: sonraki Ana Us bedelinin bu kadari cuzdanda kalir
    "x2_use_prob": 0.9,
    "mine_ad_prob": 0.8,
    "chest_ad_prob": 0.9,
    "quest_complete_prob": 0.85,
    "build_priority": ["Archer#1", "Cannon#1", "Archer#2", "House#1", "Monastery#1", "Workshop#1",
                       "Cannon#2", "Barracks#1", "Monastery#2", "House#2", "Barracks#2"],
}

CHECKS = [("1 saat", None), ("1. gun", 1 * DAY), ("3. gun", 3 * DAY), ("7. gun", 7 * DAY)]


def kill_gross(prof, region, stage):
    """Zafer icin savasta oldurulen dusmanlarin toplam altini (savas ici, harcamadan once)."""
    level = (region - 1) * 5 + stage
    mult = 1.0 + prof["kill_gold_growth"] * (level - 1)
    counts = STAGE_COUNTS[region][stage - 1]
    boss = stage == 5
    total = 0
    for i, c in enumerate(counts):
        if boss:
            c += boss_extra(region)
        if i == 0:
            c = min(c, wave1_cap(region))
        drop = int(round(WAVE_DROP[region][min(i, 2)] * mult))
        if boss and i == len(counts) - 1:
            total += max(0, c - 1) * drop + int(round(BOSS_DROP[region] * mult))
        else:
            total += c * drop
    return total


def endless_build_cost(prof, typ, floor):
    base = prof["battle_build_base"][typ]
    if prof["endless_formula"] == "legacy":
        # BattleManager.GetBuildCost: bolge = 99 -> 1 + 0.2 * 98 = x20.6
        return round5(base * (1.0 + prof["battle_build_region_slope"] * (99 - 1)))
    level = 30 + max(1, floor)
    mult = 1.0 + prof["kill_gold_growth"] * (level - 1)
    per_wave = min(28, 12 + floor // 2)
    return round5(base * prof["endless_cost_k"] * mult * per_wave / 12.0)


# ==================== SIMULASYON ====================
class Sim:
    def __init__(self, prof, player, seed):
        self.p = prof
        self.pl = player
        self.rng = random.Random(seed)
        self.t0 = player["sessions_h"][0] * H
        self.t = self.t0
        self.gold = prof["start_gold"]
        self.mb = 1
        self.mine = 1
        self.inst = {k: 0 for k in INSTANCES}
        self.hero = 1
        self.app = 1
        self.builders = 1
        self.tasks = []
        self.store = 0.0
        self.store_t = self.t
        self.boost_until = 0.0
        self.lives = prof["lives_max"]
        self.regen_next = None
        self.cleared = set()
        self.frontier = (1, 1)
        self.vein_t = None
        self.daily = {"x2": 0, "speedups": 0, "crafts": 0, "trades": 0}
        self.income = defaultdict(float)
        self.spend = defaultdict(float)
        self.lost = defaultdict(float)
        self.income_log = []
        self.battle_log = []
        self.samp = defaultdict(float)
        self.monuments = 0
        self.battles = 0
        self.wins = 0

    # ---------- formuller ----------
    def cost(self, typ, target):
        table = self.p["mb_costs"]
        base = table[max(0, min(target, len(table) - 1))]
        return max(10, round10(base * self.p["cost_factor"][typ]))

    def gps(self):
        p = self.p
        base = p["mine_gps_base"] + p["mine_gps_slope"] * (self.mine - 1)
        bonus = p["apprentice_mine_bonus"][min(max(self.app, 1), MAX_LEVEL) - 1] / 100.0
        return base * (1.0 + bonus)

    def storage(self):
        p = self.p
        hb = 0.0
        for k in ("House#1", "House#2"):
            lv = self.inst[k]
            if lv > 0:
                hb += p["house_bonus_base"] + p["house_bonus_slope"] * (lv - 1)
        return int(round((p["mine_storage_base"] + p["mine_storage_slope"] * (self.mine - 1)) * (1.0 + hb)))

    def cap(self):
        nxt = self.cost("MainBase", min(self.mb + 1, MAX_LEVEL))
        return max(self.p["wallet_cap_min"], int(math.ceil(nxt * self.p["wallet_cap_mult"] / 100.0)) * 100)

    def hero_cost(self):
        return round10(self.p["hero_cost_base"] * self.p["hero_cost_growth"] ** (self.hero - 1))

    def app_cost(self):
        return round10(self.p["app_cost_base"] * self.p["app_cost_growth"] ** (self.app - 1))

    def start_gold(self):
        for lvl, g in self.p["battle_start_gold"]:
            if self.mb >= lvl:
                return g
        return 0

    def key_level(self):
        busy = [x for x in self.tasks if x["key"] == "MainBase"]
        lvl = (busy[0]["target"] if busy else self.mb) + 1
        return lvl if lvl <= MAX_LEVEL else None

    def key_cost(self):
        lvl = self.key_level()
        return self.cost("MainBase", lvl) if lvl else 0

    def reserve(self):
        return self.pl["instant_reserve"] * self.key_cost()

    # ---------- altin ----------
    def add_gold(self, amount, src):
        amount = int(amount)
        if amount <= 0:
            return 0
        room = max(0, self.cap() - self.gold)
        got = min(amount, room)
        self.gold += got
        self.income[src] += got
        self.lost["wallet_overflow"] += amount - got
        if got:
            self.income_log.append((self.t, got))
        return got

    def pay(self, amount, what):
        self.gold -= amount
        self.spend[what] += amount

    # ---------- zaman ----------
    def _mine_to(self, t):
        dt = t - self.store_t
        if dt <= 0:
            return
        boosted = max(0.0, min(t, self.boost_until) - self.store_t)
        prod = self.gps() * (dt + boosted)
        room = max(0.0, self.storage() - self.store)
        add = min(prod, room)
        self.store += add
        self.lost["mine_overflow"] += prod - add
        self.samp["mine_produced"] += prod
        self.store_t = t

    def _lives_to(self, t):
        while self.lives < self.p["lives_max"] and self.regen_next is not None and self.regen_next <= t:
            self.lives += 1
            self.regen_next = self.regen_next + self.p["lives_regen_min"] * 60 if self.lives < self.p["lives_max"] else None

    def _apply(self, task):
        key, target = task["key"], task["target"]
        if key == "MainBase":
            self.mb = target
        elif key == "GoldMine":
            self.mine = target
        else:
            self.inst[key] = target

    def advance(self, t_new):
        while True:
            due = [x for x in self.tasks if x["end"] <= t_new]
            if not due:
                break
            task = min(due, key=lambda x: x["end"])
            self._mine_to(max(task["end"], self.store_t))
            self.tasks.remove(task)
            self._apply(task)
        self._mine_to(t_new)
        self._lives_to(t_new)
        if t_new > self.t:
            self.t = t_new

    def _boost(self, hours):
        self._mine_to(self.t)
        self.boost_until = max(self.boost_until, self.t) + hours * H

    def lose_life(self):
        if self.lives >= self.p["lives_max"]:
            self.regen_next = self.t + self.p["lives_regen_min"] * 60
        self.lives -= 1

    # ---------- toplama ----------
    def collect_mine(self):
        self._mine_to(self.t)
        room = max(0, self.cap() - self.gold)
        take = int(min(self.store, room))
        if take <= 0:
            return 0
        self.store -= take
        self.gold += take
        self.income["mine"] += take
        self.income_log.append((self.t, take))
        return take

    def collect_vein(self):
        p = self.p
        if self.mb < p["island_unlock"]:
            return
        if self.vein_t is None:
            self.vein_t = self.t
            return
        keep = max(1, min(10, self.mb - 4))
        rate = p["vein_base"] + p["vein_slope"] * (keep - 1)
        hours = min((self.t - self.vein_t) / H, p["vein_storage_hours"])
        self.add_gold(int(rate * hours), "island")
        self.vein_t = self.t

    def new_day(self, day):
        p, pl, rng = self.p, self.pl, self.rng
        self.daily = {"x2": 0, "speedups": p["ad_speedups_per_day"], "crafts": 0, "trades": 0}
        L = self.mb
        cal = day % 7 + 1
        if cal == 1:
            self.add_gold(p["daily_day1"][0] + p["daily_day1"][1] * L, "calendar")
        elif cal == 3:
            self.add_gold(p["daily_day3"][0] + p["daily_day3"][1] * L, "calendar")
        elif cal == 5:
            self._boost(p["cal_mine_boost_hours"])
        elif cal == 7:
            self.add_gold(p["daily_day7_per_level"] * L, "calendar")
        if rng.random() < pl["mine_ad_prob"]:
            self._boost(p["ad_mine_boost_hours"])
        if rng.random() < pl["chest_ad_prob"] and rng.random() < p["ad_chest_gold_chance"]:
            self.add_gold(p["ad_chest_gold"][0] + p["ad_chest_gold"][1] * L, "ad_chest")

    def claim_quests(self):
        p, rng = self.p, self.rng
        for _ in range(p["quests_per_day"]):
            if rng.random() < self.pl["quest_complete_prob"] and rng.random() < p["quest_gold_share"]:
                self.add_gold(p["quest_gold"][0] + p["quest_gold"][1] * self.mb, "quests")

    # ---------- harcama ----------
    def _candidates(self):
        busy = {x["key"] for x in self.tasks}
        secs = self.p["upgrade_seconds"]
        out = []
        if self.mb < MAX_LEVEL and "MainBase" not in busy:
            out.append(("MainBase", self.mb + 1, self.cost("MainBase", self.mb + 1), secs[self.mb + 1]))
        if self.mine < min(self.mb + 1, MAX_LEVEL) and "GoldMine" not in busy:
            out.append(("GoldMine", self.mine + 1, self.cost("GoldMine", self.mine + 1), secs[self.mine + 1]))
        for key in self.pl["build_priority"]:
            typ, unlock = INSTANCES[key]
            if self.mb < unlock or key in busy:
                continue
            lv = self.inst[key]
            if lv == 0:
                price = self.p["workshop_build_cost"] if typ == "Workshop" else self.cost(typ, 2)
                out.append((key, 1, price, secs[2]))
            elif lv < self.mb:
                out.append((key, lv + 1, self.cost(typ, lv + 1), secs[lv + 1]))
        return out

    def _start(self, c):
        key, target, price, dur = c
        typ = key if key in ("MainBase", "GoldMine") else INSTANCES[key][0]
        self.pay(price, typ)
        self.tasks.append({"end": self.t + dur, "key": key, "target": target})

    def _assign_builders(self):
        started = False
        if self.builders == 1 and self.mb >= SECOND_BUILDER_MB and self.gold >= self.p["builder2_cost"]:
            self.pay(self.p["builder2_cost"], "builder2")
            self.builders = 2
            started = True
        while len(self.tasks) < self.builders:
            cands = self._candidates()
            if not cands:
                break
            mbc = cands[0] if cands[0][0] == "MainBase" else None
            if mbc is not None and self.gold >= mbc[2]:
                self._start(mbc)
                started = True
                continue
            free = self.builders - len(self.tasks)
            if mbc is not None and free == 1 and self.gold >= self.pl["save_threshold"] * mbc[2]:
                break   # Ana Us'e biriktiriyor
            pick = None
            for c in cands:
                if c[0] != "MainBase" and self.gold >= c[2]:
                    pick = c
                    break
            if pick is None:
                break
            self._start(pick)
            started = True
        return started

    def _free_finish(self):
        done = False
        for x in self.tasks:
            if x["end"] - self.t <= FREE_FINISH_S:
                x["end"] = min(x["end"], self.t)
                done = True
        if done:
            self.advance(self.t)
        return done

    def _use_speedups(self):
        used = False
        cut = self.p["ad_speedup_minutes"] * 60
        while self.daily["speedups"] > 0 and self.tasks:
            mbt = [x for x in self.tasks if x["key"] == "MainBase"]
            task = mbt[0] if mbt else max(self.tasks, key=lambda x: x["end"])
            if task["end"] - self.t < 0.75 * cut:
                break
            task["end"] -= cut
            self.daily["speedups"] -= 1
            used = True
        if used:
            self.advance(self.t)
        return used

    def _instant_buys(self):
        reserve = self.reserve()
        for _ in range(60):
            opts = []
            if self.hero < self.mb:
                opts.append(("hero", self.hero_cost()))
            if self.app < self.mb:
                opts.append(("apprentice", self.app_cost()))
            opts = [o for o in opts if self.gold - o[1] >= reserve]
            if not opts:
                break
            name, price = min(opts, key=lambda o: o[1])
            self._mine_to(self.t)
            self.pay(price, name)
            if name == "hero":
                self.hero += 1
            else:
                self.app += 1

    def _crafts(self):
        p = self.p
        wl = self.inst["Workshop#1"]
        if wl <= 0:
            return
        while self.daily["crafts"] < p["crafts_per_day"]:
            price = round10(p["craft_base_gold"] * (1.0 + p["craft_slope"] * (wl - 1)))
            if self.gold - price < self.reserve():
                break
            self.pay(price, "craft")
            self.daily["crafts"] += 1

    def _sinks(self):
        s = self.p["sinks"]
        if not s:
            return
        if self.mb >= 5:
            while self.daily["trades"] < s["trades_per_day"]:
                price = s["trade_base"] + s["trade_per_mb"] * (self.mb - 5)
                if self.gold - price < self.reserve():
                    break
                self.pay(price, "island_trade")
                self.daily["trades"] += 1
        tiers = s["monuments"]
        if self.monuments < len(tiers):
            req, price = tiers[self.monuments]
            if self.mb >= req and self.gold - price >= s["monument_reserve"] * self.key_cost():
                self.pay(price, "monument")
                self.monuments += 1

    def shop(self):
        for _ in range(40):
            changed = self._free_finish()
            changed = self._assign_builders() or changed
            changed = self._use_speedups() or changed
            if not changed:
                break
        self._instant_buys()
        self._crafts()
        self._sinks()

    # ---------- savas ----------
    def choose_stage(self):
        r, s = self.frontier
        if r <= 6 and self.mb >= REGION_REQ[r]:
            return r, s, False
        best = max(self.cleared) if self.cleared else (1, 1)
        return best[0], best[1], True

    def win_prob(self, r, s, replay):
        pl = self.pl
        p = pl["win_stage"][s - 1] * pl["win_region"][r - 1]
        if replay:
            p += pl["replay_bonus"]
        p += pl["overlevel_bonus"] * max(0, self.mb - REGION_REQ[r] - 1)
        towers = [self.inst[k] for k in ("Archer#1", "Cannon#1") if self.inst[k] > 0]
        tower_avg = sum(towers) / len(towers) if towers else 0
        if self.mb >= 4 and tower_avg < self.mb - 3:
            p -= pl["tower_lag_penalty"]
        if self.hero < self.mb - 2:
            p -= pl["hero_lag_penalty"]
        return max(0.05, min(0.98, p))

    def in_run_spend(self, r):
        p = self.p
        scale = 1.0 + p["battle_build_region_slope"] * (r - 1)
        total = 0
        for key in ("Archer#1", "Archer#2", "Cannon#1", "Cannon#2", "Monastery#1", "Monastery#2"):
            if self.inst[key] > 0:
                total += round5(p["battle_build_base"][INSTANCES[key][0]] * scale)
        return total

    def battle(self):
        r, s, replay = self.choose_stage()
        p, pl = self.p, self.pl
        dur = (pl["battle_min"][s - 1] + pl["battle_overhead_min"]) * 60
        pwin = self.win_prob(r, s, replay)
        sinks = p["sinks"]
        if sinks and not replay:
            price = sinks["supplies_base"] + sinks["supplies_per_region"] * (r - 1)
            if self.gold - price >= self.reserve():
                self.pay(price, "supplies")
                pwin = min(0.98, pwin + sinks["supplies_win_bonus"])
        won = self.rng.random() < pwin
        if not won:
            dur *= 0.7
        self.advance(self.t + dur)
        self.battles += 1
        got = 0
        if won:
            self.wins += 1
            gross = kill_gross(p, r, s)
            start = self.start_gold()
            spent = min(self.in_run_spend(r), int(0.8 * (start + gross)))
            net = max(0, gross - spent)                       # BattleGoldNet
            reward = p["reward_base"] + p["reward_per_region"] * (r - 1) + p["reward_per_stage"] * (s - 1)
            rate = p["replay_rate"] if replay else 1.0
            pay_reward = int(round(reward * rate))
            pay_kills = int(round(net * p["kill_transfer"] * rate))
            got += self.add_gold(pay_reward, "battle_reward")
            got += self.add_gold(pay_kills, "battle_kills")
            lim = p["x2_daily_limit"]
            if (lim is None or self.daily["x2"] < lim) and self.rng.random() < pl["x2_use_prob"]:
                self.daily["x2"] += 1
                got += self.add_gold(pay_reward + pay_kills, "x2_ad")
            if not replay:
                self.cleared.add((r, s))
                self.frontier = (r, s + 1) if s < 5 else (r + 1, 1)
        else:
            self.lose_life()
        self.battle_log.append((self.t, got))
        self.collect_mine()

    # ---------- olcum ----------
    def _flags(self):
        key = self.key_cost()
        return (self.gold >= self.cap() - 1, key > 0 and self.gold >= key)

    def _sample(self, flags, d):
        self.samp["t"] += d
        if flags[0]:
            self.samp["capped"] += d
        if flags[1]:
            self.samp["notbind"] += d

    def _sampled_wait(self, d):
        if d <= 0:
            return
        flags = self._flags()
        self.advance(self.t + d)
        self._sample(flags, d)
        self.collect_mine()
        self.shop()

    def session(self, start, minutes, day, first, last):
        self.advance(max(start, self.t))
        end = self.t + minutes * 60
        if first:
            self.new_day(day)
        self.collect_vein()
        self.collect_mine()
        self.shop()
        self._sampled_wait(self.pl["base_minutes"] * 60)
        while end - self.t >= self.pl["min_battle_window_min"] * 60 and self.lives > 0:
            seg0 = self.t
            flags = self._flags()
            self.battle()
            self._sample(flags, self.t - seg0)
            self.shop()
        if last:
            self.claim_quests()
            self.shop()
        if self.t < end:
            self._sampled_wait(end - self.t)

    def snapshot(self):
        window = min(DAY, self.t - self.t0)
        inc = sum(a for (tt, a) in self.income_log if tt >= self.t - window)
        rate_h = inc / (window / H) if window > 0 else 0.0
        last = [g for (_, g) in self.battle_log[-12:]]
        bavg = statistics.mean(last) if last else 0.0
        key = self.key_cost()
        mine_h = self.gps() * H
        r, s = self.frontier
        total_in = sum(self.income.values())
        total_out = sum(self.spend.values())
        return {
            "mb": self.mb, "mine": self.mine, "app": self.app, "hero": self.hero,
            "region": "tamam" if r > 6 else "B%d-%d" % (r, s),
            "gold": self.gold, "cap": self.cap(), "store": self.store,
            "key": key, "bavg": bavg, "mine_h": mine_h, "rate_h": rate_h,
            "battles_to_key": (key / bavg) if (key and bavg > 0) else (0.0 if not key else float("inf")),
            "mine_h_to_key": key / mine_h if mine_h > 0 else float("inf"),
            "wall_h_to_key": key / rate_h if rate_h > 0 else float("inf"),
            "capped_pct": 100.0 * self.samp["capped"] / self.samp["t"] if self.samp["t"] else 0.0,
            "notbind_pct": 100.0 * self.samp["notbind"] / self.samp["t"] if self.samp["t"] else 0.0,
            "wallet_lost": self.lost["wallet_overflow"],
            "mine_lost_pct": 100.0 * self.lost["mine_overflow"] / self.samp["mine_produced"] if self.samp["mine_produced"] else 0.0,
            "in_out": total_in / total_out if total_out else float("inf"),
            "income": dict(self.income), "spend": dict(self.spend),
            "battles": self.battles, "wins": self.wins,
        }


def run(prof, player, seed, days=7):
    sim = Sim(prof, player, seed)
    checks = [(lbl, (sim.t0 + H) if t is None else t) for (lbl, t) in CHECKS if t is None or t <= days * DAY]
    snaps = {}
    ci = 0
    hs = player["sessions_h"]
    for d in range(days):
        for i, h in enumerate(hs):
            start = d * DAY + h * H
            while ci < len(checks) and checks[ci][1] <= start:
                sim.advance(max(sim.t, checks[ci][1]))
                snaps[checks[ci][0]] = sim.snapshot()
                ci += 1
            minutes = player["first_session_min"] if (d == 0 and i == 0) else player["session_min"]
            sim.session(start, minutes, d, i == 0, i == len(hs) - 1)
    while ci < len(checks):
        sim.advance(max(sim.t, checks[ci][1]))
        snaps[checks[ci][0]] = sim.snapshot()
        ci += 1
    return snaps


# ==================== RAPOR ====================
def fnum(x, nd=0):
    if x is None:
        return "-"
    if isinstance(x, float) and math.isinf(x):
        return "inf"
    return ("%." + str(nd) + "f") % x


def agg(runs, label):
    snaps = [r[label] for r in runs if label in r]

    def med(k):
        return statistics.median([s[k] for s in snaps])

    def mean(k):
        vals = [s[k] for s in snaps if not (isinstance(s[k], float) and math.isinf(s[k]))]
        return statistics.mean(vals) if vals else float("inf")

    out = {k: med(k) for k in ("mb", "mine", "app", "hero")}
    for k in ("gold", "cap", "key", "bavg", "mine_h", "rate_h", "battles_to_key", "mine_h_to_key", "wall_h_to_key",
              "capped_pct", "notbind_pct", "wallet_lost", "mine_lost_pct", "in_out", "battles", "wins"):
        out[k] = mean(k)
    out["region"] = Counter(s["region"] for s in snaps).most_common(1)[0][0]
    for bucket in ("income", "spend"):
        keys = set()
        for s in snaps:
            keys.update(s[bucket].keys())
        out[bucket] = {k: statistics.mean([s[bucket].get(k, 0.0) for s in snaps]) for k in keys}
    return out


ROWS = [
    ("Ana Us (medyan)", lambda a: fnum(a["mb"])),
    ("Sefer ilerleyisi (en sik)", lambda a: a["region"]),
    ("Maden / Cirak / Kahraman (medyan)", lambda a: "%s / %s / %s" % (fnum(a["mine"]), fnum(a["app"]), fnum(a["hero"]))),
    ("Cuzdan / tavan (ort.)", lambda a: "%s / %s" % (fnum(a["gold"]), fnum(a["cap"]))),
    ("Siradaki Ana Us odemesi", lambda a: fnum(a["key"])),
    ("Ort. savas geliri (son 12 savas, reklam dahil)", lambda a: fnum(a["bavg"])),
    ("Siradaki Ana Us = kac savas", lambda a: fnum(a["battles_to_key"], 1)),
    ("= kac saat cevrimici maden", lambda a: fnum(a["mine_h_to_key"], 2)),
    ("= kac saat (son 24 sa toplam gelir hiziyla)", lambda a: fnum(a["wall_h_to_key"], 1)),
    ("Toplam gelir hizi (altin/sa, son 24 sa)", lambda a: fnum(a["rate_h"])),
    ("Oturum suresinin %'si cuzdan TAVANDA", lambda a: fnum(a["capped_pct"], 1)),
    ("Oturum suresinin %'si altin >= siradaki Ana Us", lambda a: fnum(a["notbind_pct"], 1)),
    ("Cuzdan tavanindan kaybolan altin (kum.)", lambda a: fnum(a["wallet_lost"])),
    ("Maden deposu tasmasi (uretimin %'si, kum.)", lambda a: fnum(a["mine_lost_pct"], 1)),
    ("Toplam gelir / toplam harcama (kum.)", lambda a: fnum(a["in_out"], 2)),
    ("Savas / zafer (kum.)", lambda a: "%s / %s" % (fnum(a["battles"]), fnum(a["wins"]))),
]

INCOME_KEYS = ["mine", "battle_reward", "battle_kills", "x2_ad", "calendar", "quests", "ad_chest", "island"]
SPEND_KEYS = ["MainBase", "GoldMine", "Archer", "Cannon", "Monastery", "House", "Barracks", "Workshop",
              "builder2", "hero", "apprentice", "craft", "supplies", "island_trade", "monument"]


def table(header, rows):
    lines = ["| " + " | ".join(header) + " |", "|" + "---|" * len(header)]
    for r in rows:
        lines.append("| " + " | ".join(str(c) for c in r) + " |")
    return "\n".join(lines)


def endless_table():
    rows = []
    for f in (1, 6, 11, 21, 26, 31, 51):
        level = 30 + f
        mult = 1.0 + LEGACY["kill_gold_growth"] * (level - 1)
        boss = f % 5 == 0
        per = min(28, 12 + f // 2) + (3 if boss else 0)
        theme = ((f - 1) // 5) % 6 + 1
        drop = int(round(WAVE_DROP[theme][0] * mult))
        w1 = per * drop
        start = 75   # BattleStartGold(MB 15)
        la = endless_build_cost(LEGACY, "Archer", f)
        va = endless_build_cost(V2, "Archer", f)
        vc = endless_build_cost(V2, "Cannon", f)
        vm = endless_build_cost(V2, "Monastery", f)

        def kill_no(c):
            k = int(math.ceil(max(0, c - start) / float(drop)))
            return str(k) if k <= per else "%d (dalga 2+)" % k

        rows.append((f, level, w1 + start, "%d / %s" % (la, kill_no(la)),
                     "%d / %d / %d" % (va, vc, vm), kill_no(va), "%.0f%%" % (100.0 * va / (w1 + start))))
    return table(["Kat", "Savas sev.", "Baslangic + dalga 1 altini", "LEGACY Okcu / ilk kule kacinci oldurmede",
                  "V2 Okcu/Topcu/Manastir", "V2 ilk Okcu kacinci oldurmede", "V2 Okcu / (bas.+dalga1)"], rows)


def diff_table():
    rows = []

    def ratio(a, b):
        if isinstance(a, (int, float)) and isinstance(b, (int, float)) and a:
            return b / float(a)
        return None

    for k, lv in LEGACY.items():
        vv = V2_SINKS.get(k)
        if k in ("name",) or lv == vv:
            continue
        if isinstance(lv, list) and isinstance(vv, list) and all(isinstance(x, (int, float)) for x in lv):
            rs = [ratio(a, b) for a, b in zip(lv, vv) if a]
            rmin, rmax = min(rs), max(rs)
            flag = " !! >3x" if (rmax > 3.0 or rmin < 1 / 3.0) else ""
            rows.append((k, str(lv), str(vv), "%.2f..%.2f%s" % (rmin, rmax, flag)))
        else:
            r = ratio(lv, vv)
            flag = " !! >3x" if (r is not None and (r > 3.0 or r < 1 / 3.0)) else ""
            rows.append((k, str(lv), str(vv), ("%.2f%s" % (r, flag)) if r is not None else "yeni/bicim"))
    return table(["Parametre", "LEGACY", "V2", "Oran V2/LEGACY"], rows)


def report(seeds, days):
    results = {}
    for prof in PROFILES:
        runs = [run(prof, PLAYER, sd, days) for sd in range(1, seeds + 1)]
        results[prof["name"]] = {lbl: agg(runs, lbl) for (lbl, t) in CHECKS if t is None or t <= days * DAY}
    names = [p["name"] for p in PROFILES]
    out = ["_Uretildi: `python Tools/economy_sim_v2.py --seeds %d --write` (tohum %d, %d gun, oyuncu: PLAYER)._" % (seeds, seeds, days), ""]
    for (lbl, t) in CHECKS:
        if t is not None and t > days * DAY:
            continue
        out.append("#### Kontrol noktasi: %s" % lbl)
        rows = [[name] + [fn(results[n][lbl]) for n in names] for (name, fn) in ROWS]
        out.append(table(["Metrik"] + names, rows))
        out.append("")
    for lbl in ("1. gun", "7. gun"):
        if lbl not in results[names[0]]:
            continue
        out.append("#### Kaynak bazinda gelir (kumulatif, %s)" % lbl)
        rows = []
        for k in INCOME_KEYS:
            rows.append([k] + [fnum(results[n][lbl]["income"].get(k, 0.0)) for n in names])
        rows.append(["TOPLAM"] + [fnum(sum(results[n][lbl]["income"].values())) for n in names])
        out.append(table(["Kaynak"] + names, rows))
        out.append("")
        out.append("#### Kalem bazinda harcama (kumulatif, %s)" % lbl)
        rows = []
        for k in SPEND_KEYS:
            rows.append([k] + [fnum(results[n][lbl]["spend"].get(k, 0.0)) for n in names])
        rows.append(["TOPLAM"] + [fnum(sum(results[n][lbl]["spend"].values())) for n in names])
        out.append(table(["Kalem"] + names, rows))
        out.append("")
    out.append("#### Sonsuz Sefer: savas ici kule bedeli")
    out.append(endless_table())
    out.append("")
    out.append("#### Profil farki (V2+SINK vs LEGACY, otomatik; !! = 3x kisiti asildi)")
    out.append(diff_table())
    return "\n".join(out)


def write_block(md_text):
    begin, end = "<!-- SIM:BEGIN -->", "<!-- SIM:END -->"
    with open(ECON_MD, "r", encoding="utf-8") as fh:
        doc = fh.read()
    if begin in doc and end in doc:
        head = doc.split(begin)[0]
        tail = doc.split(end, 1)[1]
        doc = head + begin + "\n" + md_text + "\n" + end + tail
    else:
        doc = doc.rstrip() + "\n\n" + begin + "\n" + md_text + "\n" + end + "\n"
    with open(ECON_MD, "w", encoding="utf-8") as fh:
        fh.write(doc)


def main():
    ap = argparse.ArgumentParser(description="Mini Empire Builder ekonomi simulasyonu v2")
    ap.add_argument("--seeds", type=int, default=40)
    ap.add_argument("--days", type=int, default=7)
    ap.add_argument("--write", action="store_true", help="ECONOMY.md SIM blogunu guncelle")
    args = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    md = report(args.seeds, args.days)
    print(md)
    if args.write:
        write_block(md)
        print("\n[ECONOMY.md SIM blogu guncellendi]")


if __name__ == "__main__":
    main()
