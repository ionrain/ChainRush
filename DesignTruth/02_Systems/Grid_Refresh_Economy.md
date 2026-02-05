# Grid Refresh Economy — Meaningful vs Filler (v0.1)

## Purpose
The bottom grid refresh loop alternates between:
- Meaningful refreshes: give real combat power or critical options
- Filler refreshes: give minor benefits (gold, small buffs) and create pacing/breathers

The system must:
- support level pacing and difficulty spikes
- be parameter-driven
- sync naturally with enemy waves (progress-driven)

---

## Definitions

### Refresh Cycle
A refresh cycle is:
1) Grid is filled with tiles
2) Countdown runs for `refreshIntervalSeconds`
3) Player makes selections (tap or swipe chains)
4) Reward popup appears and is confirmed
5) Next cycle begins

### Refresh Types
**Meaningful Refresh**:
- Outcomes that can materially affect survival in the next 10–30 seconds:
  - Unit tiles (spawn/upgrade units)
  - Hero ability tiles (if impactful)
  - Major combat boosters (heal, time slow, screen nuke)
  - Rare / high-tier upgrades (big % buffs, new mechanics)

**Filler Refresh**:
- Outcomes that do NOT immediately change survival odds (unless stacked a lot):
  - Gold
  - Minor upgrades (small %)
  - Cosmetic/collection drops (if any)
  - “padding” tiles used to limit meaningful density

---

## Availability-based Generation (Canonical Model)

Grid content generation is NOT based on pure random weights.

Each tile category/type has an availability cooldown model:
- every type accumulates time since last appearance
- a type becomes "available" only when its accumulated cooldown
  reaches or exceeds its required interval
- intervals are driven by level progress via curves

This model is the primary balancing mechanism for grid generation.

---

## Core Design Contract

### Contract A — Minimum Meaningful Frequency
There must be a guaranteed upper bound on time between meaningful refreshes.
Example target: at least 1 meaningful refresh in any window of N refreshes.

### Contract B — Spike Alignment
Meaningful refreshes should be aligned to upcoming danger:
- before or at major wave spikes (`notify=true` waves)
- before runner rushes / elites
- before boss phases (via triggers)

### Contract C — Fairness Guardrail
During high danger segments, the grid must not roll exclusively filler outcomes.
A safety system ensures a minimum meaningful probability.

### Contract D — Level-driven Scheduling
Refresh schedule is driven by progress `p` (0..1) and/or wave notifications.
This keeps pacing consistent across Survive and Distance levels.

---

## Scheduling Model (v0.2 — Availability-driven)

RefreshType (Meaningful / Filler) does NOT directly spawn rewards.
Instead, it modifies availability rules:

Scheduler controls:
1) availability intervals for categories
2) guaranteed minimum presence per refresh
3) safety overrides during danger windows

Actual grid content is selected only from READY types.

RefreshType for each cycle is determined by a scheduler with 3 inputs:
1) Progress `p`
2) Danger signal `dangerLevel` (derived from enemy schedule)
3) Recent history (streak prevention)

### Inputs
- `p`: from LevelProgressEvent (0..1)
- `dangerLevel`: one of {Low, Medium, High}
  - High when:
    - within `dangerWindowSeconds` of a `notify=true` wave threshold, OR
    - a trigger wave fired with `notify=true`, OR
    - a runner/elite archetype density threshold is expected (optional future)
- `history`: last K refresh types and timestamps

### Output
- `refreshType`: Meaningful or Filler
- `tilePoolProfile`: which pools/weights to use for this refresh

---

## Scheduler Rules (Availability-focused)

### Rule 1 — Base Intervals
Each category has a base interval curve:
- shorter interval → appears more often
- longer interval → appears more rarely

Intervals may change with level progress.

### Rule 2 — Meaningful Refresh Effect
When RefreshType == Meaningful:
- intervals for meaningful categories are temporarily reduced
- guaranteed presence for meaningful categories may be increased

Meaningful categories include:
- UNIT
- HERO_ABILITY
- UPGRADE_MAJOR
- BOOSTER

### Rule 3 — Filler Refresh Effect
When RefreshType == Filler:
- intervals for meaningful categories are increased or unchanged
- filler categories (GOLD, UPGRADE_MINOR) remain short-interval

### Rule 4 — Guaranteed Presence
Some categories may define:
- `alwaysAvailableFraction`
Meaning: at least this fraction of grid patterns must come
from this category if it is READY.

This rule is applied before random filling.

### Rule 5 — Danger Override
During High danger windows:
- meaningful category intervals are force-reduced
- guaranteed fractions are force-increased
- safety rules may bypass interval checks if needed

---

## Tile Pool Profiles

RefreshType selects a “profile” that defines which tile categories can appear and their weights.

Example profiles:
- `FillerLowDanger`: mostly gold + minor upgrades, small chance of unit
- `NeutralMixed`: balanced mix
- `MeaningfulPower`: units + hero skills + major upgrades
- `MeaningfulBooster`: includes time slow/heal/nuke (rare)

Profiles are defined in data (see balance YAML + content catalog).

---

## Chain Length Difficulty Coupling (Design Hook)
Meaningful refreshes can be made “harder to extract” by:
- distributing meaningful tiles into longer, thinner chains
- adding distractor tiles nearby

Filler refreshes can be “easy” (short chains), so player feels reward quickly.

This allows tuning difficulty without changing reward numbers.

---

## UI/Feedback Requirements (contract)
- Refresh type must be perceivable:
  - subtle label: “Power Surge” / “Supply Drop” (or icons)
  - or background tint behind the grid
- If Meaningful is forced by danger override:
  - show a brief warning banner aligned with wave notification.
  
---

## Availability Cooldowns

For each Tile Category (or Cell Item Type):

- `cooldown[type]`:
  - accumulates elapsed time continuously
  - reset to 0 when the type is used in grid generation

- `interval[type](progress)`:
  - required cooldown before the type becomes available
  - evaluated from a curve using normalized level progress (0..1)

A type is considered **READY** when:
cooldown[type] >= interval[type](progress)