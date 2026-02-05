# Tile Pools & Weights (v0.1)

## Canonical Note: Availability-first Model

Tile generation is driven by availability intervals, not pure random weights.

Weights are only used:
- after availability checks
- within the set of READY categories or sub-pools

This prevents bad luck streaks and enables deterministic pacing control.

Defines what can appear on the bottom grid, how probabilities are structured,
and how pool profiles support Meaningful vs Filler refresh scheduling.

This spec works with:
- Grid Refresh Economy (Meaningful vs Filler)
- Chain selection rules (4-way adjacency, break-on-mismatch)
- Reward scaling by chain length (tier system)
- Level pacing via progress-driven danger windows

---

## 1) Tile Categories (Design Truth)

A tile always belongs to exactly one category:

1) **UNIT**
- Spawns or upgrades a player unit (slime-based).
- Tier/level is derived from chain length.

2) **UPGRADE_MINOR**
- Small stat improvements (padding / filler).
- Examples: +HP%, +ATK%, +AS%, +Range, +CritChance, +CritDamage
- Usually low impact per pick, stacks over time.

3) **UPGRADE_MAJOR**
- High-impact upgrades (meaningful).
- Examples: big multipliers, new mechanics (pierce, splash), strong % boosts.

4) **HERO_ABILITY**
- Triggers hero ability (or grants/levels it if meta-driven).
- In-run activation can be “cast now” or “charge” (see reward type below).

5) **BOOSTER**
- One-off strong effects:
  - time slow
  - heal
  - destroy all enemies on screen
- These are rare and typically meaningful.

6) **GOLD**
- Currency pickup (filler), used for meta progression.

(Optional future categories)
- RELIC / ARTIFACT
- QUEST / KEY

---

## 2) Reward Types (what happens when tile is claimed)

A tile claim yields a reward object of a specific type:
- **SpawnUnit(unitId, tierOrCount)**
- **ApplyUpgrade(statId, magnitude, duration?)**
- **CastHeroAbility(abilityId, powerByTier)**
- **GrantHeroAbility(abilityId, levelUpByTier)**
- **UseBooster(boosterId, powerByTier)**
- **GrantGold(amountByTier)**

Note: whether hero abilities are cast-now or grant/upgrade is a design choice per tile.

---

## 3) Pool Profiles (driven by refresh scheduler)

Each refresh cycle selects a **Tile Pool Profile**.
A profile defines:
- category weights
- sub-pool weights within categories (e.g., which unit IDs)
- rarity rules

Profiles referenced by `grid_refresh.scheduler`:
- `FillerLowDanger`
- `NeutralMixed`
- `MeaningfulPower`
- `MeaningfulBooster`

### 3.1 Profile: FillerLowDanger
Intent: breathers, small progression, low immediate power.
Allowed categories and typical weights (conceptual):
- GOLD: high
- UPGRADE_MINOR: high
- UNIT: low
- HERO_ABILITY: very low
- BOOSTER: near zero
- UPGRADE_MAJOR: near zero

### 3.2 Profile: NeutralMixed
Intent: default loop, balanced.
- GOLD: medium
- UPGRADE_MINOR: medium
- UNIT: medium
- HERO_ABILITY: low
- UPGRADE_MAJOR: low
- BOOSTER: very low

### 3.3 Profile: MeaningfulPower
Intent: player gets real combat options before danger.
- UNIT: high
- UPGRADE_MAJOR: medium
- HERO_ABILITY: medium
- UPGRADE_MINOR: low
- GOLD: low
- BOOSTER: low (or none)

### 3.4 Profile: MeaningfulBooster
Intent: emergency tools / dramatic moments.
- BOOSTER: medium-high
- UNIT: medium
- HERO_ABILITY: medium
- UPGRADE_MAJOR: low-medium
- GOLD/MINOR: low

---

## 4) Availability + Weighting Model

Tile selection uses a two-phase process:

### Phase 1 — Availability Filtering
- determine which categories are READY
- apply guaranteed presence rules (`alwaysAvailableFraction`)
- build a candidate set of allowed categories

Categories not READY cannot appear, regardless of weight.

### Phase 2 — Weighted Selection
- from the READY set, select categories using profile weights
- then select concrete tile payloads from sub-pools

Weights never override availability.

---

## 5) Tier Scaling by Chain Length (Design Truth hook)

Chain length maps to tier:
- tier 1: chain length 1
- tier 2: chain length 2
- tier 3: chain length 3
Default maxTier=3.

If chainLength > maxTier:
- reward tier is clamped to maxTier
- extraCount = chainLength - maxTier
- output becomes:
  - "maxTier reward" + "extraCount" (meaning depends on reward type)

### 5.1 How extraCount applies (canonical rules)
- UNIT: spawn additional copies OR add levels (choose one per design)
  - v0.1 default: spawn additional copies (`+extraCount` units at maxTier)
- GOLD: gold amount increases linearly by extraCount
- UPGRADES: magnitude increases by extraCount * overflowStep (small)
- HERO_ABILITY: increases power (or adds extra casts) by extraCount
- BOOSTER: increases duration/power slightly, capped

(Exact numbers live in balance YAML.)

---

## 6) “Meaningful extraction difficulty” (layout control)

A refresh being Meaningful does NOT guarantee the player gets a strong outcome;
it guarantees the grid contains meaningful options.

Difficulty can be tuned by layout rules:
- Meaningful tiles can be placed in:
  - longer, thinner 4-way paths
  - split clusters that require precise routing
- Filler tiles can be placed in:
  - compact clusters that yield quick chains

This creates skill expression without changing raw weights.

---

## 7) Rarity and Safety Rules

### 7.1 Rarity tiers (optional, but recommended)
Each tile payload may have a rarity:
- Common / Rare / Epic / Legendary

Rarity can modify:
- appearance probability
- reward magnitudes
- whether it can appear in filler profiles

### 7.2 Safety (anti-bad-luck) rules
- In High danger windows:
  - ensure at least `minMeaningfulTilesOnGrid` meaningful-category tiles exist
  - optionally ensure at least 1 UNIT or BOOSTER tile exists
- Streak prevention exists at scheduler level; this is additional per-grid safety.

---

## 8) Authoring Checklist
For each tile payload define:
- id
- category
- sub-pool group
- base weight
- tier scaling behavior (tier1/2/3 magnitudes)
- overflow handling (extraCount mapping)
- UI text/icon reference

---

## 9) Next Data File (for numbers)
This spec defines structure.
Concrete weights and magnitudes should live in:
- design_truth/04_balance/tile_pool_parameters.yaml (recommended)
or ScriptableObjects (prototype), but mirrored into YAML for balance work.

---

## 10) Cooldown Reset Rule

When a category is used during grid generation:
- its cooldown is reset to 0
- other categories continue accumulating cooldown

This ensures natural spacing between appearances of powerful options.

---

## 11) Progress-driven Availability

Availability intervals are evaluated using normalized level progress (0..1).

This allows:
- early game: longer intervals for powerful categories
- late game: shorter intervals, more frequent meaningful options

Exact curves and values are defined in LevelDifficulty / balance data.