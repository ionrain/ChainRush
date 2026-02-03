# Player Unit AI — FSM (v0.1)

## Purpose
Player units act autonomously in the top auto-battle lane:
- hold the line, engage enemies, attack, and avoid jittering behavior (rapid forward/backward ping-pong)
- remain within a controlled area relative to an anchor

This spec defines states, transitions, and timing rules to prevent oscillations.

---

## Definitions

### Anchor
Primary reference point for unit movement constraints.
Default: Hero position (follow anchor).
(Alternative for some modes: fixed left-side base position — not in v0.1)

### Distances / Radii (from balance_parameters.yaml)
- followRadius: distance to hero under which unit may idle near hero
- returnRadius: distance to hero beyond which unit must return to hero
- maxChaseDistanceFromAnchor: max distance from anchor unit can chase while pursuing an enemy
- enemyDetectRadius: detection radius to consider targets

### Targeting Timing
- targetEvaluationInterval: how often unit scans for targets (seconds)
- targetLockTime: minimum time target remains locked (seconds)
- retargetCooldown: minimum time between changing targets (seconds)
- enemyCommitTime: minimum time unit commits to moving/attacking a chosen target before reconsidering

---

## High-level Rules (Anti-jitter Guarantees)

### Rule A — Single Movement Intent
At any moment the unit has exactly one movement intent:
- RETURN (go back to anchor zone)
- ENGAGE (move towards target)
- HOLD (maintain position / small adjustments)

The unit must not alternate intents more often than retargetCooldown.

### Rule B — Commit Window
When a target is acquired, unit enters a commit window (enemyCommitTime) during which:
- it will not switch target
- it will not decide to RETURN unless returnRadius is violated

### Rule C — Target Lock
After commit window ends, target remains locked until:
- targetLockTime elapsed since acquisition, OR
- target becomes invalid (dead/out of world), OR
- target is out of allowed chase boundary for longer than a grace duration (see Rule E)

### Rule D — Death of Target Handling (Grace)
If the current target dies:
- unit enters a short "Reacquire Grace" period where it does NOT immediately RETURN
- instead it tries to reacquire a new target once
This prevents “enemy dies → unit snaps back → immediately goes forward” loops.

### Rule E — Chase Boundary Grace
If unit is engaging but target drifts beyond maxChaseDistanceFromAnchor:
- unit does not instantly RETURN
- it continues ENGAGE for a short grace time (e.g., 0.25–0.5s) to finish an attack / avoid jitter
If still beyond boundary after grace, it transitions to RETURN.

---

## State Machine

### States
1) **FOLLOW_ANCHOR**
- Move towards anchor until within followRadius.
- If within followRadius, transition to HOLD_POSITION.

2) **HOLD_POSITION**
- Unit stays in place (minor local separation/avoidance optional).
- Periodically evaluates targets.

3) **ACQUIRE_TARGET**
- Triggered by target evaluation tick.
- Selects best target inside enemyDetectRadius and within chase boundary.
- If found → ENGAGE_TARGET.
- If none → HOLD_POSITION (or FOLLOW_ANCHOR if outside followRadius).

4) **ENGAGE_TARGET**
- Movement intent: ENGAGE
- Move towards target until within attack range.
- On entering this state:
  - set targetAcquiredTime = now
  - set commitUntil = now + enemyCommitTime
  - set lockUntil = now + targetLockTime
  - set nextRetargetAllowed = now + retargetCooldown

Transitions:
- If distanceToAnchor > returnRadius → RETURN_TO_ANCHOR (override)
- If inAttackRange → ATTACK
- If target invalid → REACQUIRE_GRACE

5) **ATTACK**
- Execute attack animation/projectile cast.
- During ATTACK:
  - unit must NOT change movement intent
  - target switching is disabled until attack finishes
Transition:
- On attack finished → POST_ATTACK_DECISION

6) **POST_ATTACK_DECISION**
- Decide next action:
  - If target still valid AND within chase boundary AND within detect → ENGAGE_TARGET (continue pressure)
  - Else → REACQUIRE_GRACE

7) **REACQUIRE_GRACE**
- Duration: short fixed time (recommend 0.35s default; tune in balance)
- Behavior:
  - unit does NOT RETURN unless returnRadius is violated
  - unit attempts to acquire a new target once (immediately) and then waits until grace ends
Transitions:
- If new target found → ENGAGE_TARGET (but must respect nextRetargetAllowed)
- If grace ends:
  - If distanceToAnchor > followRadius → FOLLOW_ANCHOR
  - Else → HOLD_POSITION

8) **RETURN_TO_ANCHOR**
- Movement intent: RETURN
- Move towards anchor until within followRadius
Transitions:
- If within followRadius → HOLD_POSITION
- Target acquisition is disabled while returning (prevents ping-pong)
- Exception: if an enemy enters very close range to protected object (future “panic” rule; not in v0.1)

---

## Target Selection Policy (v0.1)
Target candidates:
- must be alive/valid
- must be within enemyDetectRadius of the unit
- must not require unit to exceed maxChaseDistanceFromAnchor from anchor (soft boundary with grace)

Priority (highest first):
1) Enemy currently attacking protected object (if such signal exists)
2) Closest enemy to protected object (x position smallest, towards left)
3) Closest enemy to the unit (distance)
Tie-breakers:
- lowest HP (optional, helps finish targets)

---

## Update Loops

### Target evaluation tick
- Runs every targetEvaluationInterval
- Only active in HOLD_POSITION, ACQUIRE_TARGET, ENGAGE_TARGET (after commitUntil), POST_ATTACK_DECISION, REACQUIRE_GRACE (one attempt)

### Movement update
- Every frame: apply current movement intent towards destination
- Destination is:
  - anchor (FOLLOW/RETURN)
  - target position (ENGAGE)

---

## Tunable Parameters (add to balance)
- reacquireGraceSeconds: 0.35 (default)
- chaseBoundaryGraceSeconds: 0.35 (default)
- (existing) targetEvaluationInterval, targetLockTime, retargetCooldown, enemyCommitTime