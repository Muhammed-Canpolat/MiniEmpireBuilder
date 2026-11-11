# Round 2 design: hero + tower synergy, counters, region identity, timers (target: MEDIUM)

Owner feedback: 1–2 towers clear any region and the hero is not needed. Goal: you can win **without the hero, but it is hard**. The ideal path is towers + hero together.
Builds on `balance_v1.md`. Conflicts are flagged at the end. Every number lives in `Progression` (`Data/GameData.cs:272`), with the exception of `GemEconomy`/`LivesSystem` constants.

## 0. Root cause (numbers)
- Tower DPS grows roughly with L² because unit count rises every 3 levels (`GameData.cs:193-205`). Hero DPS is linear: `15·(1+0.15(L−1))·0.8/s` (`GameData.cs:108-118`).
  Archer vs hero DPS by level: L3 36 vs 16, L9 144 vs 26, L12 **225 vs 32 (7×)**. Enemy HP is linear, ×(1+0.085·(bl−1)) (`GameData.cs:508`). Hero growth keeps pace with enemy HP; towers outgrow it.
- Battle build cost is flat 50/75/60 (`BattleManager.cs:19-21`), but kill gold grows ×(1+0.08·(bl−1)) (`GameData.cs:511`). An R6 wolf pays about 32 g, so towers are effectively free there.

## A. Tower re-curve (linear, like the hero) + cost
| | Damage per unit | Units | Atk/s | DPS L3 / L9 / L12 / L14 (hero) |
|---|---|---|---|---|
| Archer (now) | 8+2(L−1) | 1+L/3 | 1.5 | 36 / 144 / 225 / 255 |
| **Archer (new)** | 7·(1+0.15(L−1)) | 1+L/5 | 1.25 | 11 / 39 / 70 / 77 (16 / 26 / 32 / 35) |
| Cannon (now) | 20+5(L−1) | 1+L/4 | 0.5 | 15 / 90 / 150 / 170 |
| **Cannon (new)** | 18·(1+0.15(L−1)) | 1+L/6 | 0.5 | 12 / 40 / 72 / 80 |
- R1–R2 stay almost the same (−30% at L1–3). Late regions drop about 70%. A single tower is now about 1.5–2.2× the hero, not 7×.
- New constants: `Progression.TowerDamageGrowth = 0.15`, `ArcherUnitsEvery = 5`, `CannonUnitsEvery = 6`, read by `ApplyLevelStats`. `BattleGarrison` will show fewer soldiers.
- **Battle build cost** `Progression.BattleBuildCost(type, region) = base·(1+0.2(region−1))`, rounded to 5. R1 50/75/60 → R6 100/150/120. Rebuild +50% stays (`BattleBuildSlot.cs:15`). The build allowance (max 2 per type) and slots 3/4/5 stay.

## C. Counter enemies (variants on existing bodies; `EnemyVariant` enum + `EnemySpawnInfo.variant`)
Multipliers apply on top of the base type's `EnemyStats` after `ScaleToLevel`. Constants go in `Progression.Counters`. Damage needs a source: `EnemyController.TakeDamage(dmg, DamageSource)` (`EnemyController.cs:417`). Sources: Hero / Apprentice / Archer / Cannon / Env. The default is Hero, so existing callers still work. `TowerController.cs:151` passes Archer or Cannon.

| Variant | Body | HP | Dmg | Speed | Gold | Damage taken | Behaviour / look |
|---|---|---|---|---|---|---|---|
| **Armored** | Orc | ×1.4 | ×1.0 | ×0.8 | ×1.5 | Archer ×0.35, Cannon ×0.6, Hero/Apprentice ×1.0 | Breaker role (kept). Scale ×1.15, steel tint (0.62,0.68,0.80). Tower hits show a grey "resisted" number |
| **Flying** | ShadowWolf | ×1.2 | ×1.3 | ×0.85 | ×1.3 | Archer/Hero ×1.0. **Cannon cannot target** (skip in `FindNearestEnemy`, `TowerController.cs:121`) | Siege role. Flies in a straight line and ignores `BattleObstacles`. Sprite +0.45 u Y, bob 0.08 u at 1.6 Hz, shadow ellipse (alpha 0.35), violet tint at alpha 0.85. Can be provoked |
| **Saboteur** | Wolf | ×0.4 | — | ×1.3 (2.86) | ×0.5 | Towers ×0.7, Hero ×1.0 | Targets the nearest standing tower at any distance (Main Base if none). **Ignores Provoke.** On arrival it stands on a 0.6 s fuse, then detonates for **30% of tower max HP** (Main Base: 5%) and dies without dropping gold. Spawns as a burst (0.35 s interval) from its own edge with an orange telegraph arrow. Scale ×0.85, orange tint, spark FX |
- Saboteur invariant: a hero at the matching MB level kills it in one hit (R2 19 HP vs 19.5 dmg; R6 40 vs 44). The hero is faster than the bomb only when intercepting (hero speed 2.5 < 2.86). Positioning matters.
- **Spawn table** `Progression.CounterMix(region, stage, wave)`. Variants *replace* base enemies, so wave totals don't change; clamp to count−2. **Wave 1 never has counters.** S1 has a single wave, so no S1 stage has counters. Each new variant gets a "NEW ENEMY" banner on first appearance.
| Region | S2–S4, wave 2 | S5 wave 2 | S5 wave 3 |
|---|---|---|---|
| R1 | — | — | — |
| R2 | Sab 2 (S3–S4 only) | Sab 3 | — |
| R3 | Sab 2; Arm 2 (S3–S4) | Sab 3, Arm 2 | Arm 3 |
| R4 | Fly 3; Arm 2 (S3+); Sab 2 (S4) | Fly 3, Sab 3 | Arm 3, Fly 3 |
| R5 | Fly 3, Arm 2, Sab 3 | Fly 4, Sab 4 | Arm 4, Fly 3 |
| R6 | Fly 4, Arm 3, Sab 3 | Fly 5, Sab 4 | Arm 5, Fly 4 |
- **Counters:** Armored → hero, or cannon over archer. Flying → archers + hero, so the player has to choose an archer slot. Saboteur → hero intercept; Monastery repairs.

### Boss Ward (all 6 bosses; `RegionBoss`, `ForestWitchBoss`, `DragonBoss`)
- Ward pool = `WardFraction` × boss max HP: R1 0.15, R2 0.20, R3–6 0.25. Shown as a blue bar over the HP bar plus a bubble.
- While the ward is up, tower damage to the boss is multiplied by `WardTowerMult`: R1 0.5, R2 0.35, **R3–6 0.15**. Hero/Apprentice damage hits the ward at ×1.0.
- When the ward breaks, the boss is **Exposed for 6 s**: ×1.5 damage from all sources and a 1.5 s stagger. The ward re-forms at full when the window ends.
- Check: FrostLord at R4S5 has 732 HP and a 183 ward. A hero at L9 breaks it in about 7.5 s. One exposed window with 2 towers in range deals about 700, so 1–2 cycles kill it. Tower-only deals about 23 DPS for about 32 s while the escorts hit the base, so it usually loses.
- Defeat tag `ward_never_broken` makes the loss readable on the defeat screen. CastleGuardian's Shield Wall stacks with the ward (unchanged).

## C-metrics: targets (bot, N=20 seeded runs per stage per profile)
| Region (entry MB) | Expected S1 / S5 win | Median base HP S1 / S5 | Duration S1 / S5 | Tower-only S5 | Hero-only S5 | Combined S5 |
|---|---|---|---|---|---|---|
| R1 (1) | ≥95% / 75% | 0.85 / 0.45 | 1.5–2 / 3–4 min | 40–70% | 30–60% | ≥75% |
| R2 (3) | ≥95% / 72% | 0.80 / 0.40 | 1.5–2.5 / 3.5–4.5 | ≤50% | ≤40% | ≥70% |
| R3 (6) | ≥95% / 68% | 0.75 / 0.35 | 2–2.5 / 4–5 | **≤30%** | **≤20%** | **≥65%** |
| R4 (9) | ≥92% / 65% | 0.70 / 0.30 | 2–3 / 4–5.5 | ≤30% | ≤20% | ≥62% |
| R5 (12) | ≥90% / 62% | 0.65 / 0.25 | 2–3 / 4.5–6 | ≤25% | ≤15% | ≥60% |
| R6 (14) | ≥90% / 60% | 0.60 / 0.20 | 2.5–3 / 5–6.5 | ≤25% | ≤15% | ≥60% |
- Profiles in `PlaytestBotTests`: `towerOnly` (hero idle at base, auto-attack only), `heroOnly` (no builds), `combined` (= expected player).
- Log per battle:
  - damage by source;
  - ward breaks;
  - saboteur outcome (hero kill / tower kill / detonated);
  - towers lost; thaws; shrines destroyed; eruption damage.
- Pass criteria:
  - combined profile: hero + apprentice damage share is ≥20% in R1–2 and 30–50% in R3+;
  - ≥1 ward break in ≥90% of boss wins;
  - ≥50% of saboteurs are killed by the hero;
  - median towers lost in R3+ is 1–2;
  - every other `balance_v1.md` §3 criterion still holds.

## D. Battle start gold ("War Chest"): unlocked by **Main Base level**
- `Progression.BattleStartGold(int mainBaseLevel)` (replaces the region version at `GameData.cs:278`; call site `BattleManager.cs:117`): **MB 1–6: 0 · MB 7–9: 25 · MB 10–12: 50 · MB 13–15: 75**.
- Why MB level and not "after region 5":
  - it gives three modest steps instead of one late cliff that only helps R6 and Endless;
  - it rewards upgrading, which is the core loop;
  - a player stuck in R4 can earn it.
- After the cost scaling in §A it never covers the first tower on its own: R4 25/80, R5 50/90, R6 75/100.
- Shown as a perk in the MB 6→7 / 9→10 / 12→13 upgrade panels.
- Never purchasable. Not in `GemEconomy`, not boosted by Royal or battle cards. Endless and Invasion use the same rule.
- Payout still subtracts it (`BattleGoldNet`, `BattleManager.cs:31`).
- **Mitigation for an undefended wave 1:** `Wave1Cap` `8+r` → `6+r` (R1 7 … R6 12). Wave 1 is a hunter type (Wolf/ShadowWolf), which goes for the hero, so the hero carries it. Hero respawn is 8 s (`HeroController.cs:30`).

## E. Region identity (constants in `Progression.RegionMechanics`; hooks in `BattleRegionEnvironment.cs:26-28` via `specialMechanic`)
- **R4 Frost "Don"** (extends `BattleWeather.cs`):
  - each blizzard **freezes 1 tower (S1–S2) / 2 towers (S3–S5)** for 5 s. It picks the towers nearest the active spawn edge and reuses `TowerController.Freeze` (`TowerController.cs:74`).
  - The hero standing within 1.6 u for 1 s thaws a tower.
  - To compensate, storm tower cooldown goes 1.4 → **1.25**; hero speed 0.75, interval 22 s and duration 6 s stay.
  - Fits: Wolves (they hunt the hero during the thaw run) and Flying, which is not affected by the storm.
- **R5 Dark "Karanlik"** (new `ShadowShrines.cs`, `region_05.asset` `specialMechanic`):
  - **Cursed Shrines**: 1 at S1, 2 at S2–S5, at fixed mid-field points between the spawn edges and the base. Built from circle sprites + purple tint (RegionBoss aura code) and a Cainos prop.
  - Every 18 s, after a 2 s warning ring, each standing shrine casts **Darkness for 6 s**: towers within 4.5 u get ×0.6 range.
  - **Only the hero can damage shrines.** Shrine HP is 120 × the enemy HP level multiplier; L12 hero ≈ 9 hits ≈ 11 s. A destroyed shrine pays +30 battle gold and stays down.
  - Fits: ShadowWolf hunters (punish the shrine run), Armored, Flying (wraiths). This adds to the Dark King's Shadow Call.
- **R6 Dragon "Lav"** (new `VolcanicEruptions.cs`):
  - every 24 s, after a 2 s red warning circle, **4 lava bombs** (r 1.1) land: 1 aimed at each of up to 3 built towers, plus 1 near the hero.
  - Impact deals 8% max HP to structures and the hero, then leaves **burning ground for 6 s** at 4% max HP/s to the hero *and ground enemies*. Flying units are immune.
  - The hero dodges and kites enemies into the fire. The Monastery offsets chip damage. Reuse the AoE pattern from `DragonBoss.cs:247` and `TinyFx.Explosion`.
  - Fits: Orc/Armored (slow, so they burn), Saboteur and Flying (immune).

## H. Upgrade time curve (`Progression.UpgradeSeconds`, `GameData.cs:306`; one curve for all buildings, as today)
| Target L | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Now | 15s | 1m | 30m | 1h | 3h | 6h | 12h | 18h | 24h | 30h | 36h | 42h | 48h | 48h |
| Gems now | 0 | 2 | 12 | 17 | 30 | 42 | 59 | 72 | 83 | 93 | 102 | 110 | 118 | 118 |
| **New** | 10s | 1m | 3m | 6m | 10m | 15m | 30m | 1h | 2h | 3h | 4h | 6h | 8h | 12h |
| Gems new | 0 | 2 | 4 | 5 | 7 | 9 | 12 | 17 | 24 | 30 | 34 | 42 | 48 | 59 |
- One building 1→15: **268.5 h → 37.1 h** (−86%). Rushing every level of one building: **858 → 293 gems**.
- MB 1→6 (R3 entry) needs 20 min of builder time, and no level up to L7 exceeds 15 min.
- Keep `FinishCost = 2.2·√min` (`GemEconomy.cs:62`). It is fair per minute and player-friendly.
- **Risks:**
  - the gem sink shrinks about 66%;
  - builders 3–5 (100/200/300 gems, `GameData.cs:302`) and 4× potions lose value;
  - re-check gem income vs spend in telemetry before touching the 2.2 coefficient.
- Lives: `LivesSystem.RegenMinutes` 30 → **20** (`LivesSystem.cs:11`; notifications follow automatically at `GameNotifications.cs:178`). A full refill drops from 150 to 100 min.

## Conflicts / risks to flag
- `balance_v1.md` #2 (start gold 60+15(r−1)) is **reversed** at owner request. v1's main diagnosis was an undefended wave 1. Mitigated by the lower `Wave1Cap` and hunter-type wave 1, but the v1 criterion "no loss in wave 1" must be re-checked.
- `balance_v1.md` said "not changing tower prices/scaling". Overridden here by owner feedback.
- `AI_README.md:24` lists the old timers. Update it when §H ships.
- Tests to update:
  - `ProgressionTests`: `BattleStartGold` signature, timers;
  - `RegionBossTests`: ward, "Don" behaviour;
  - `HudExpansionReviewTests`: start gold 0;
  - tower damage asserts;
  - `ForestRegionMechanicsTests`: saboteurs inside fog.
- Ship order: §A + §D → measure → §C counters + ward → measure → §E region by region (R4, R5, R6) → §H anytime (independent).
