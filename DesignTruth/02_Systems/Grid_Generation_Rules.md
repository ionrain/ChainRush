# Grid Generation Rules (v0.1)
Design Truth specification of how the bottom grid is generated and refreshed.

This document reflects the CURRENT prototype behavior and is the canonical
reference for grid generation logic.

Related systems:
- Grid Refresh Economy (Meaningful vs Filler)
- Tile Pools & Availability
- Level Progress (time / distance based)

---

## 1) Core Concept

The grid is generated using an availability-first model driven by:
- per-type cooldown intervals
- guaranteed fractions
- level progress (0..1)

The system does NOT use pure random generation.
All randomness is constrained by availability and guarantees.

---

## 2) Grid Structure

### Grid
- Square grid of size NxN (e.g. 4x4, 6x6)
- Grid is fully regenerated on each refresh cycle

### Cell
- Each cell contains exactly one Cell Item
- A Cell Item belongs to exactly one Cell Item Type
  (e.g. UNIT, GOLD, UPGRADE_MINOR, etc.)

---

## 3) Generation Unit: Pattern

### Pattern
Grid is not filled cell-by-cell.
Instead, it is filled using **selection patterns**.

A pattern:
- occupies one or more cells
- defines which cells form a selectable chain
- has an associated Cell Item Type

Patterns are defined by `CellSelectPatternType` and represent
the atomic unit of grid population.

Examples:
- single-cell pattern
- straight 2–3 cell line
- L-shape
- longer 4-way paths

Pattern shapes are content-defined and not part of balancing logic.

---

## 4) Availability Model (Canonical)

Each Cell Item Type has:

- `cooldown[type]`
  - accumulates elapsed time continuously
  - reset to 0 when the type is used in grid generation

- `interval[type](progress)`
  - required cooldown before the type becomes AVAILABLE
  - evaluated from a curve using normalized level progress (0..1)

A type is **READY** when:
cooldown[type] >= intervaltype

Types that are not READY cannot be used for generation
(unless overridden by safety rules).

---

## 5) Guaranteed Availability (alwaysAvailableOnRefresh)

Some Cell Item Types define:

- `alwaysAvailableOnRefresh[type]` ∈ [0..1]

Meaning:
- this fraction of total patterns on the grid
  must be generated using this type, IF the type is READY

Guaranteed patterns are placed before any random filling occurs.

If a guaranteed type is not READY:
- the guarantee is skipped
- it is NOT force-spawned (unless danger override applies)

---

## 6) Generation Algorithm (Step-by-step)

For each grid refresh:

### Step 1 — Collect READY Types
- For each Cell Item Type:
  - evaluate interval(type, progress)
  - check cooldown >= interval
- Build a set of READY types

---

### Step 2 — Place Guaranteed Patterns
For each type with alwaysAvailableOnRefresh > 0:
- if type is READY:
  - calculate:
    ```
    guaranteedPatterns = floor(totalPatterns * alwaysAvailableFraction)
    ```
  - place that many patterns of this type
  - mark grid cells as occupied
  - reset cooldown[type] = 0

---

### Step 3 — Fill Remaining Patterns
For remaining unoccupied pattern slots:
- build a candidate list of READY types
- optionally filtered by current RefreshType
  (Meaningful / Filler scheduling)

Selection rules:
- types must be READY
- weights may be applied inside READY set
- no type outside READY set can be selected

For each selected type:
- place a pattern of that type
- reset cooldown[type] = 0

---

### Step 4 — Cooldown Accumulation
After generation:
- cooldowns of used types are reset
- cooldowns of unused types continue accumulating over time

---

## 7) Interaction with Meaningful / Filler Refresh

Meaningful / Filler does NOT directly spawn rewards.

Instead, it modifies:
- availability intervals
- guaranteed fractions
- safety overrides

### Meaningful Refresh
- reduce intervals for meaningful categories
- increase alwaysAvailable fractions
- may allow bypassing interval checks for safety

### Filler Refresh
- keep or increase intervals for meaningful categories
- filler categories remain frequently available

---

## 8) Danger Override (Safety)

During High danger windows:
- meaningful categories may bypass availability checks
- guaranteed meaningful patterns may be force-placed
- this is a last-resort safety net, not default behavior

Danger is derived from:
- upcoming notify=true waves
- triggered wave events

---

## 9) Progress Dependency

Availability intervals depend on normalized progress `p ∈ [0..1]`:

- Survive levels:
  - p = elapsedTime / goalTime
- Distance levels:
  - p = distanceTraveled / goalDistance

Progress updates once per second.

---

## 10) Non-Goals (Explicit)

- No gravity / falling tiles
- No match-3 clearing mechanics
- No chain reactions on grid regeneration
- No per-cell independent RNG

The grid is a timed decision surface, not a simulation.

---

## 11) Design Guarantees

This model guarantees:
- no extreme bad-luck streaks
- controlled pacing of powerful options
- predictable balance knobs via curves
- compatibility with auto-battle pacing

Any deviation from this behavior must be explicitly documented
as a new version of this spec.