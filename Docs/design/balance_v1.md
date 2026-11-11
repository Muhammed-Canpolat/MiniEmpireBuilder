# Balance v1: Campaign difficulty (target: MEDIUM)

Goal: stage 1 of each region is a comfortable win. Stage 5 (boss) takes effort. Every loss should have a reason the player can see.
Source: bot telemetry (2 runs/stage, "expected player" profile), `GameData.cs`, `BattleManager.cs`, `Resources/Campaign/region_0*.asset`.

## 1. Diagnosis
- **Variance comes from spawn sides, not from skill.** `BattleManager.SpawnEnemy` picks a random edge for *every* enemy. Some runs spread the enemies out and the hero handles them one by one. Other runs stack 5+ on the side the hero isn't on, and they reach the base together. The same profile gives win/lose for this reason only (R1S5, R2S5, R3S5, R6S1).
- **Wave 1 is undefended.** `totalGoldEarned` starts at 0, so no tower can be built until about 5 kills (50 g). Every loss happened in wave 1 within about 40 s. The player had no decision to make yet, so the loss can't be explained to them.
- **Wave type = `region.enemyTypes[wave]`.** Wave 1 is always the first type. For R1 to R5 that is Wolf, which hunts the hero, so the base only gets hit if the hero falls or is far away. For R6 it is **18 Orcs** (`02,04,01`), which ignore the hero and hit the base. That is 18 × 30 dmg × 0.7/s ≈ 380 dps against 1500 HP: the base dies in about 4 s of contact, and nothing the player does changes that.
- **Spawn interval tightens per region** (`1.25 − 0.1·(region−1)`, min 0.65). R6 drops 18 enemies in about 13 s, which makes the stacking problem worse.
- **R4 to R5 are too easy because towers grow faster than enemies.** Archer damage is `8+3(L−1)` × units `1+L/3`, roughly quadratic in L. Enemy HP grows ×(1+0.085·bl), which is linear. At MB 9 to 14 the towers are level 8 to 13 and outgrow the enemies. R6 then gets only +2 MB levels for +5 battle levels.

## 2. Changes (ranked by impact; minimal and data-first)
| # | Change | Where | Current → New |
|---|---|---|---|
| 1 | **Spawn by side per wave, with a telegraph.** Each wave chooses its edges once: 1 edge for R1 to R2, 2 edges (opposite or adjacent at random) for R3+. Enemies spawn within a ±30% segment of that edge. Show an edge arrow/flash 2 s before the first spawn. | `BattleManager.SpawnWave/SpawnEnemy` (edge choice moves from per-enemy to per-wave); edge count as `Progression.WaveSpawnSides(region)` | per-enemy `Random.Range(0,4)` → per-wave side(s) + 2 s telegraph |
| 2 | **Starting battle gold** so one tower can be placed before wave 1. Payout must not mint it: pay `totalGoldEarned − startGold` (min 0). | new `Progression.BattleStartGold(region)`; init in `BattleManager` where `totalGoldEarned` is set; payout line ~896 | 0 → `60 + 15·(region−1)` (R1 60 = 1 archer, R6 135 = 1 cannon + 1 archer). If no tower is unlocked yet (MB1), the gold just stays in the bank |
| 3 | **R6 wave order and counts.** Orcs no longer open the battle. | `region_06.asset` `enemyTypes`, `enemyCounts` | types `02,04,01` (Orc, ShadowWolf, Zombie) → `04,01,02` (ShadowWolf, Zombie, Orc). Wave-1 counts: S1 18→12, S2 16→12, S3 18→13, S4 19→14, S5 18→14 (later waves unchanged) |
| 4 | **Wave-1 size cap** as a guard for all regions and Endless. | `BattleManager.CreateCampaignWaves` (i==0), cap value in `Progression.Wave1Cap(region)` | uncapped → `min(count, 8 + region)` (R1 9 … R6 14; R1 to R5 data is already at or under this except R5 15/16/17 → 13/14/14) |
| 5 | **Softer early base HP** (only matters in R1 to R2; under 7% change after MB10). | `BuildingData.ApplyLevelStats` MainBase | `200 + 100(L−1)` → `300 + 100(L−1)` |
| 6 | **Spawn interval floor.** | `CreateCampaignWaves` | `max(0.65, 1.25 − 0.1(r−1))` → `max(0.9, 1.25 − 0.07(r−1))` (R6 0.75 → 0.9 s) |
| 7 | **R4 to R5 tuning (only if #1 to #6 leave them at 100% wins with base > 0.9).** Waves 2+ get +2 enemies each. Wave 1 is untouched. | `region_04/05.asset` `enemyCounts` | e.g. R4S5 `14,14,7` → `14,16,9`; R5S5 `16,16,9` → `14,18,11` |

Not changing: hero/enemy scaling constants, tower prices and unlocks (50/75/60, +50% rebuild, MB 2/4/6). All new numbers go into `Progression`, none hard-coded in `BattleManager`.

Order of work: ship #1 to #4 together, re-measure, then apply #5 to #7 only where the targets below are missed.

## 3. Telemetry success criteria
Run the bot with the expected-player profile, **N = 20 runs per stage**, and seed `Random` per run so results can be reproduced and losses replayed.
| Stage | Bot win-rate | Median base-end HP | Losses |
|---|---|---|---|
| S1 (every region) | ≥ 95% | 0.55–0.90 | none in wave 1 |
| S2 to S4 | 80–95% | 0.35–0.75 | — |
| S5 boss | 55–75% | 0.15–0.50 | ≥ 70% of losses in the boss wave |
- No region's S1 or S5 win-rate differs by more than ±15 pp from the band (this catches R4 to R5 being too easy and R6 too hard).
- Wave 1 never loses more than 35% of base HP; no loss before 60 s.
- Every loss logs a cause (hero dead / tower count 0 at loss / side breached). At least 80% of losses should show "≤ 1 tower built" or "hero dead". Those causes are explainable through gold spending or hero level.
- Variance: across 20 runs, base-end HP std dev ≤ 0.20 per stage (today 0.70 vs 0 on R1S5).
- Bot +2 MB levels → S5 win-rate ≥ 90% (shows that upgrading fixes the loss).

## 4. Test risks (`Assets/Tests/PlayMode`)
- `HudExpansionReviewTests` sets `totalGoldEarned` to 500 by reflection. Safe, but any assertion that expects 0 gold at battle start, or that the payout equals gold earned, needs `startGold` subtracted. Keep the field name.
- `ProgressionTests` checks hero max HP of 200 (unchanged). Add tests for `BattleStartGold`, `Wave1Cap` and `WaveSpawnSides`, and update any MainBase HP expectation if #5 ships.
- `RegionBossTests` / `ForestRegionMechanicsTests`: the R6 type reorder moves ShadowWolf (fog mechanic) to wave 1. Check any test that assumes the boss wave's escort type or the R6 wave index.
- `SpecialModesTests` (Endless BattleLevel 31) goes through `CreateCampaignWaves`, so the #4 cap and #6 floor apply. Assert-level values are unaffected.
- `PlaytestBotTests`: if seeding is added, keep the default unseeded path for the game itself. Telemetry baselines in `PlaytestOutput/` become invalid, so re-baseline them.
