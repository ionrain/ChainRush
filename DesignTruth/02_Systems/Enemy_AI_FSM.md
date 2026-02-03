# Enemy System — AI FSM + Spawning Spec (v0.2)

This document covers:
A) Enemy behavior FSM (runtime behavior after spawn)
B) Enemy spawning model as implemented now (fill + waves + triggers tied to level progress)

---

# A) Enemy AI FSM (Behavior after spawn)

## Purpose
Enemies spawn from the right and pressure the Protected Object on the far left.
Behavior must be:
- predictable for balance
- scalable to many enemies
- parameter-driven via archetypes

## Definitions
- Protected Object: left-side target. If destroyed -> player loses.
- Lane: top battle area, effectively horizontal movement.
- Archetype: data-driven behavior profile (swarm, runner, ranged, elite, support).

## High-level Rules (Anti-jitter contract)
- Default intent is ADVANCE_TO_BASE (move left).
- Enemies should not ping-pong between base-run and combat more often than retargetCooldown.
- Target switching is limited by commitTime and lockTime.

## States
1) SPAWN_IN
- optional spawn animation.
- -> ADVANCE_TO_BASE

2) ADVANCE_TO_BASE
- move left toward Protected Object.
- may evaluate targets depending on archetype.
- -> ACQUIRE_TARGET (if valid target found and allowed)
- -> ATTACK_BASE (if in range)

3) ACQUIRE_TARGET
- chooses target based on Target Policy.
- sets commitUntil, lockUntil, nextRetargetAllowed.
- -> ENGAGE_TARGET or -> ADVANCE_TO_BASE

4) ENGAGE_TARGET
- move toward target until within attackRange.
- during commit window no retarget.
- -> ATTACK_TARGET, -> REACQUIRE_GRACE, -> DISABLED

5) ATTACK_TARGET
- perform attack.
- no retarget during attack.
- -> POST_ATTACK_DECISION, -> DISABLED

6) POST_ATTACK_DECISION
- continue on same target if still valid, else -> REACQUIRE_GRACE

7) REACQUIRE_GRACE
- short grace; attempt to acquire a new target once (respect nextRetargetAllowed).
- after grace ends -> ADVANCE_TO_BASE

8) ATTACK_BASE
- attack Protected Object.
- usually no retargeting unless archetype allows.
- if knocked away -> ADVANCE_TO_BASE

9) DISABLED
- stunned/frozen/knockdown.
- on end -> resume ATTACK_BASE if still in range else ENGAGE/ADVANCE.

10) DEAD

## Target Selection Policy (v0.2 default)
Candidates: hero + player units (alive & in aggroRadius).

Priority default:
1) Targets that are closest to the Protected Object (most "left" / most threatening)
2) Closest to self (distance)
Tie-breaker (optional): lowest HP

Archetype flags:
- preferBase: mostly ignore units, push to base
- ignoreUnitsUntilBlocked: only fight if blocked / recently damaged / taunted
- preferHero: hero is top priority when in aggro

---

# B) Enemy Spawning & Waves (as implemented now)

This section describes CURRENT prototype behavior:
- EnemyManager drives spawn based on LevelProgressEvent.Progress (0..1)
- Spawning consists of:
  1) Fill spawning (continuous population control)
  2) Timed waves at specific progress thresholds
  3) Triggered waves via events (string keys)

## B1) Core Inputs / Data
### LevelData
- LevelData.enemyData : EnemyGenerationData
- LevelData.enemyMultipliers : Dictionary<Attribute, float>
- LevelData.boardSize : Vector2Int (not enemy-related but ties to difficulty)

### EnemyGenerationData
Fill options:
- maxSimulteneousCount
- maxFillCount
- enemyCountCurve (AnimationCurve): target enemy count multiplier vs progress
- enemyProportions: Dictionary<float, Dictionary<GameObject,float>>
  - keys are progress thresholds (0..1)
  - values are spawn shares per prefab

Waves:
- waves: Dictionary<float, EnemyWaveGenerationData>
  - key is progress threshold (0..1), sorted ascending
  - on reaching threshold -> SpawnWave()

Triggers:
- triggers: Dictionary<string, EnemyWaveGenerationData>
  - keyed by string event name
  - triggered by EnemySpawnTriggerEvent(name, shape, pos, size, rotation, followTarget)

### EnemyWaveGenerationData (wave payload)
- shares: List<Dictionary<GameObject,float>>  (variants of shares)
- shareSelection: First / Sequential / Random
- notify: bool (notification trigger)
- amount: enemies per wave step
- wavesCount: number of wave steps
- waveInterval: seconds between steps
- spawnShape: SpawnShape (FillBounds/Point/InsideBox/OnBox/InsideCircle/OnCircle)
- spawnerSize, spawnerDistance
- multipliers: Dictionary<Attribute,float> (per-wave multipliers)

## B2) Progress-driven Fill Spawning (EnemyManager.SpawnFill)
- EnemyManager listens LevelProgressEvent and stores _progress in [0..1]
- On FixedUpdate while spawning:
  - targetFillCount = enemyCountCurve.Evaluate(progress) * maxFillCount
  - enemyProportions are updated when progress passes thresholds
  - manager spawns up to:
    slots = min(targetFillCount, maxSimulteneousCount) - currentEnemiesCount
- Each spawn picks prefab by weighted random from current share dictionary.

**Design Truth note:**
- This is effectively a "population controller": keep enemy count near curve-defined target.
- Difficulty can be shaped by the curve and by shares switching over progress.

## B3) Timed Waves (EnemyManager.CheckWaves + SpawnWave)
- waves are stored by progress thresholds.
- when progress >= nextWaveTime:
  - remove it and start coroutine SpawnWave(waveData, defaultTarget, ...)

SpawnWave behavior:
- repeats wave steps wavesCount times
- each step spawns `amount` enemies
- waits waveInterval seconds between steps
- selects share variant based on shareSelection:
  - Sequential: cycles through shares list
  - Random: picks random index with some extra random loops (prototype detail)
- positions are generated by spawnShape:
  - FillBounds: uses fillBounds random
  - Otherwise: uses shape-specific random around (target.position + spawnerDistance)

## B4) Triggered Waves (EnemyManager.OnMMEvent(EnemySpawnTriggerEvent))
- If triggers contains event name:
  - SpawnWave(triggerData, followTarget ? defaultTarget : null, size, position, rotation, shape)
- This allows scripted spikes or special spawns (boss, ambush, etc.)

## B5) Multipliers Stacking (EnemyManager.SetupEnemy)
Enemy.Setup receives combined multipliers:
- base: levelData.enemyMultipliers
- multiplied by wave multipliers (EnemyWaveGenerationData.multipliers) if present
- multiplied by skill-driven enemy multipliers (_multipliersFromSkills) (Support skills targeting enemies)

**Design Truth note:**
- Multipliers are multiplicative by Attribute and stack in order:
  base * wave * skills
- Enemy spawning system is already ready for balance knobs without code changes.

## B6) Targets and Follow
- EnemyManager passes `defaultTarget` into Enemy.Setup (unless trigger sets followTarget=false).
- defaultTarget is set when UnitActionEvent(UnitActionType.Spawn) occurs.

**Design Truth note:**
- In the prototype, enemy "target/follow anchor" concept exists via defaultTarget injection.
- If we want Protected Object to be the lose-condition, we should align what defaultTarget means:
  - either defaultTarget = ProtectedObject transform
  - or Enemy has internal priority between player units and Protected Object.

---

# C) Locked Contracts (Design Truth)
This section defines the runtime contracts relied upon by enemy spawning and balancing.

To avoid ambiguity between "prototype spawn model" and "design narrative":
1) What exactly is defaultTarget in Slime Lords:
   - Hero? Protected Object? "frontline"? (recommended: Protected Object)
   
## C2) LevelProgressEvent.Progress (as implemented now)

Enemy spawning (fill + timed waves) is driven by LevelProgressEvent.Progress in range [0..1].

### Progress source depends on current Level Goal Type:
- If LevelGoalType == Distance:
  - distance = hero.x - heroStartX
  - progress = clamp01(distance / goalAmount)
  - LevelProgressEvent also includes Distance = distance (float)
- Else (default, including Survive):
  - progress = clamp01(elapsedTimeSeconds / goalAmount)
  - LevelProgressEvent also includes Time = elapsedTimeSeconds (int seconds)

### Update frequency
- Progress event is emitted once per second (integer time tick), not every frame.

### Notes / Constraints
- Distance mode requires Hero transform to be known (captured on UnitActionType.Spawn for UnitType.Hero).
- goalAmount is read from LevelData.Goal.GoalAmount.


3) Whether fill spawning is used in all level types or only some.