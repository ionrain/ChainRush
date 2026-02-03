# Waves & Spawning Content Spec (v0.1)
Design Truth for how enemies are spawned in a level.
This spec is aligned with current prototype implementation:
- EnemyManager.cs
- EnemyGenerationData.cs
- LevelManager.cs (LevelProgressEvent.Progress)

---

## 1) Core Concept: Progress-driven spawns

All enemy spawning is driven by normalized progress `p` in range [0..1].

### Progress source (as implemented)
- If LevelGoalType == Distance:
  - distance = hero.x - heroStartX
  - p = clamp01(distance / goalAmount)
- Else (includes Survive):
  - p = clamp01(elapsedTimeSeconds / goalAmount)

Progress is updated and broadcast **once per second** via LevelProgressEvent.

**Implication for design:**
Progress thresholds are effectively evaluated on a 1-second grid.

---

## 2) EnemyGenerationData: 3 spawn channels

EnemyGenerationData contains three independent channels:

1) **Fill spawning** (population controller)
2) **Timed waves** (progress threshold → wave coroutine)
3) **Triggered waves** (external event key → wave coroutine)

All three can be used in the same level.

---

## 3) Channel A — Fill Spawning (population controller)

### Purpose
Maintain ongoing pressure by spawning enemies continuously so that the on-screen population follows a curve.

### Data Fields (EnemyGenerationData)
- `maxSimulteneousCount` (int)  
  Hard cap of concurrently active enemies.
- `maxFillCount` (int)  
  Scale for desired enemy count produced by the curve.
- `enemyCountCurve` (AnimationCurve 0..1 → 0..1)  
  Multiplier controlling fill intensity by progress.
- `enemyProportions` (Dictionary<float, Dictionary<GameObject, float>>)  
  Spawn shares that switch by progress threshold.

### Runtime Logic (current behavior)
Every FixedUpdate while p < 1:
- fillCount = int(enemyCountCurve(p) * maxFillCount)
- update enemy shares if p passes next threshold in enemyProportions
- spawn up to `slots` enemies (prototype detail: slots = min(fillCount, maxSimulteneousCount - currentEnemiesCount))

**Shares switching**
enemyProportions is keyed by progress thresholds (0..1).
When p >= threshold, the active share dictionary becomes that entry.

### Authoring Guidelines
- Use Fill for baseline pressure.
- Use enemyCountCurve to shape pacing:
  - ramp up for difficulty
  - dip for “breather” segments
- Use enemyProportions to introduce new enemy types over time (by progress).

### Example (conceptual)
At p=0.0: 90% Swarm, 10% Shooter  
At p=0.4: 60% Swarm, 25% Shooter, 15% Runner  
At p=0.7: 40% Swarm, 30% Shooter, 30% Runner

---

## 4) Channel B — Timed Waves (progress thresholds)

### Purpose
Create spikes, setpieces, and recognizable “moments”:
- elites
- runner rush
- mixed comps
- mini-boss sequences

### Data Fields (EnemyGenerationData.waves)
`waves: Dictionary<float, EnemyWaveGenerationData>`
- key: progress threshold `p_wave` (0..1)
- value: wave payload

### Runtime Logic
When current p >= earliest remaining wave threshold:
- remove it from schedule
- start SpawnWave(waveData, defaultTarget, ...)

### EnemyWaveGenerationData schema (as implemented)
- `shares: List<Dictionary<GameObject, float>>`
  - a list of share dictionaries; wave step chooses one depending on selection mode.
- `shareSelection: WaveSelectionMethod`
  - First / Sequential / Random
- `notify: bool`
  - if true, triggers notificationTrigger (UI/feedback)
- `amount: int`
  - enemies spawned per wave step
- `wavesCount: int`
  - number of wave steps
- `waveInterval: float`
  - delay between steps (seconds)
- `spawnShape: SpawnShape`
  - None / FillBounds / Point / Line / InsideCircle / OnCircle / OnBox / InsideBox
- `spawnerSize: Vector2`
  - shape size (used by some shapes)
- `spawnerDistance: Vector2`
  - position offset applied to spawn origin
- `multipliers: Dictionary<Attribute, float>`
  - per-wave attribute multipliers applied to spawned enemies

### Share selection behavior (important prototype detail)
- First: always uses shares[0]
- Sequential: cycles through shares list using internal useCount
- Random: picks a random index with extra random loops (prototype “more randomness”)

### Authoring Guidelines
- Prefer few, readable wave thresholds:
  - e.g., p=0.25, 0.50, 0.75, 0.90
- Use `notify=true` for “meaningful” waves (matches your “significant refresh” concept).
- Use `multipliers` for temporary difficulty spikes without new prefabs.
  - e.g., +HP for “armored wave”, +MoveSpeed for “rush wave”.

---

## 5) Channel C — Triggered Waves (scripted events)

### Purpose
Allow level scripts / mechanics to spawn enemies on demand:
- ambush
- boss call
- chest guard spawn
- reaction to player actions

### Data Fields
`triggers: Dictionary<string, EnemyWaveGenerationData>`
- key: string trigger name
- value: wave payload

### Runtime Trigger Event (as implemented)
EnemySpawnTriggerEvent.Trigger(
  name,
  spawnShape,
  position,
  size,
  rotation,
  followTarget
)

If triggers contains `name`, EnemyManager calls SpawnWave(triggerData, ...).

### Authoring Guidelines
- Use triggers for objective-driven levels:
  - "ChestSpawnGuard"
  - "BossPhase2"
  - "FinalPush"
- Keep trigger names stable and documented (Design Truth).

---

## 6) Spawn Shapes (practical notes)

SpawnShape controls how positions are generated:
- FillBounds: uses fill bounds random positions
- Others: calculate positions around a base `position` with `size` & `rotation`
- spawnerDistance is applied as an offset to spawn origin (very useful to spawn offscreen)

Authoring rule:
- Default enemy spawns should originate offscreen right (via spawnerDistance).

---

## 7) Multipliers Stacking (enemy stats)

Each spawned enemy receives combined multipliers:
- LevelData.enemyMultipliers (base)
× Wave multipliers (EnemyWaveGenerationData.multipliers)
× Skill-driven multipliers (EnemyManager internal)

Stacking is multiplicative per Attribute.

---

## 8) Recommended “Meaningful vs Filler” integration (Design Truth)

Your core design requires alternating refresh value:
- meaningful refresh: real power gain (units, hero skills, impactful boosts)
- filler refresh: gold / minor boosts

Spawning should support this by aligning spikes:
- meaningful refresh windows should precede or coincide with:
  - wave thresholds with `notify=true`
  - runner rushes
  - elites
- filler refresh windows should be placed during:
  - low curve segments
  - between major wave thresholds

(Exact scheduler for refresh types is specified in grid refresh/balance docs.)

---

## 9) Minimal Authoring Checklist per Level
1) Choose goal type (Survive or Distance) → defines progress.
2) Fill settings:
   - maxFillCount
   - enemyCountCurve
   - enemyProportions thresholds
3) Wave thresholds for setpieces (0..1):
   - p -> wave payload
4) Optional triggers for scripted moments:
   - name -> wave payload
5) Validate:
   - does p reach 1 reliably with this goal?
   - do wave thresholds trigger on 1s tick reasonably?
   - does maxSimultaneousCount prevent deadlocks/overflows?