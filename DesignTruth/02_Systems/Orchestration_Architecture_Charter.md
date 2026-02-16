# Orchestration Architecture Charter (v0.1)

## Purpose
Build an orchestration platform that:
- scales from realtime to turn-based without rewrites,
- supports many roles/professions without “100 copy‑paste domains”,
- supports hierarchical command (unit → squad → army),
- supports mixed player control levels,
- grows by adding modules and data, not by piling ad-hoc rules into a monolith.

## Core principle
**One decision loop. Many proposal sources. One conflict resolver. Many executors.**

## Glossary
- **Actor** — world entity that can be reasoned about (unit, enemy, interactable, group).
- **Capability** — what the entity can execute (move, attack, heal, fish, build, cook).
- **Intent** — what we want (defend, engage, follow, harvest, repair).
- **Policy / Tactic** — how we pick/shape an action (targeting, spacing, formation, avoidance).
- **Command / Action** — what we actually execute this tick/step (MoveTo, Hold, UseTool, Attack).
- **Proposal** — candidate plan for some scope with priority and constraints.
- **Arbiter** — resolves conflicts and chooses final commands.
- **Planner (optional)** — assigns tasks across multiple units/groups.
- **Executor** — applies chosen commands to the engine (TDE/Unit.SetTarget/etc).

## Layering (non‑negotiable)

### Layer 1 — Scheduling
- Provides “decision ticks”: realtime dt, turn step, event triggers.
- Owns time (`Now`) and step count.
- Orchestrators and policies **must not** self‑schedule; they run when the scheduler calls.

### Layer 2 — World model / sensing
- Single place builds per‑tick world cache (actors, relations, hot lists).
- Exposes query interfaces (nearest hostile, visibility, distance checks).
- No gameplay decisions here; **facts only**.

### Layer 3 — Proposal sources
- Modules that propose intents/actions but **do not dispatch commands**.
- Examples: Combat module, Idle module, Fishing module, Player input module, Commander module.
- Read world cache + configuration + unit capabilities.
- Output: proposals only.

### Layer 4 — Arbitration
- Resolves conflicts between proposals:
  - priority (player > commander > AI > background),
  - hysteresis / anti‑thrash,
  - compatibility rules (some proposals can coexist),
  - engagement / ROE gating.
- Output: final per‑unit commands (and optionally group commands).

### Layer 5 — Planning / assignment (optional)
- For multi‑unit coordination (squad tasks, distribution, formation slot assignment).
- Can sit between proposals and arbitration, but must follow the same flow:
  **writes assignments/constraints, not engine commands**.

### Layer 6 — Execution
- Applies commands to the engine (TDE).
- Owns low‑level anti‑thrash state if needed (waypoint timers, smoothing).
- Must never read global registries; receives needed context via injection.

## Cross‑cutting: primitives library
Reusable primitives used by many modules/policies (avoid duplicated “weights/sliders” everywhere):
- **Geometry**: clamp to circle/box/polygon; nearest point on boundary.
- **Sampling**: deterministic sampling inside region; ring/grid patterns.
- **Crowd**: scoring, separation, neighbor counting (engine‑agnostic utilities).
- **Formation**: slot generation and assignment (separate from “idle policy”).
- **Engagement rules**: gating / pursuit windows / allowed targets.

## Hard invariants
1) **Single tick source**
- Only the scheduler (via arbiter) decides tick frequency.
- No `Update` loops in orchestrators/executors; coroutine/scheduler tick only.

2) **No domain dispatch**
- Proposal sources never call `Apply*Command` directly.
- Only arbiter (or executor pipeline invoked by arbiter) dispatches.

3) **Typed routing keys**
- Roles, factions, constraints maps are typed assets (`RoleAsset`, `FactionAsset`).
- Strings allowed only for debug labels, never for routing.

4) **Data‑driven by default + explicit override**
- Default behavior comes from role maps + capability profiles.
- Per‑unit override exists, but is a deliberate higher‑priority slot.

5) **Reuse primitives instead of adding flags**
- If two systems need “crowd avoidance”, it becomes a primitive/service,
  not duplicated config inside each policy.

6) **Policies are pure decision logic**
- Policies return “recommended intent/action parameters”.
- Policies must not mutate global state; deterministic given seeds/context.

7) **Executors are the only place with engine glue**
- Waypoints, `SetTarget`, nav glue, animation triggers live in executors.
- Persistent anti‑thrash state belongs to executor unless it’s global hysteresis.

## Extension points

### A) Add a new profession/role (e.g., Fisher)
- Define capability: `CanFish`.
- Add module: `FishingProposalSource` (reads world, proposes “Fish at spot”).
- Add policies: fishing target selection, spacing near fishing spots (reuse crowd primitive).
- Add executor mapping: `UseTool/Fish` → engine.

### B) Add turn‑based mode
- Swap scheduler with step‑based scheduler.
- Keep proposal sources and arbiter.
- Optionally actions carry “duration in steps” that executor respects.

### C) Add hierarchical control
- Introduce Group entity + GroupContext (anchor, members, leader).
- `CommanderProposalSource` writes group‑level intents/constraints.
- Planner assigns per‑unit tasks based on group directives.
- Arbiter merges group directives with unit‑level proposals.

### D) Add player control levels
- `PlayerProposalSource` writes proposals:
  - direct unit commands,
  - group commands,
  - ROE rules (“hold position”, “free fire”, “do not chase”).
- Arbiter prioritizes player proposals and defines override semantics.

## Engagement / ROE (Rules of Engagement)
Engagement is an arbitration + planning concern, not pure economy.

Conceptually define an `EngagementRulesAsset`:
- when to engage (detection thresholds, allowed target types),
- how far to pursue (leash forward‑only, chase windows),
- when to disengage (threat gone, timer, health),
- aggression level (passive/defensive/aggressive),
- formation discipline (maintain line vs break).

Economy/progression may modify:
- numeric parameters (ranges, timers),
- unlock additional tactics,
- change default ROE per role,

…but should not own orchestration logic.

## Guardrails for future feature plans
Every feature plan must answer:
1) Which layer does this belong to? (sensing / proposal / arbitration / planning / execution / primitives)
2) What data drives it? (asset map / capability profile / role rules)
3) What is the routing key? (`RoleAsset` / `FactionAsset` / `GroupId`)
4) Which primitives does it reuse? If none exist, should we add one?
5) Where does anti‑thrash state belong? (executor vs arbiter hysteresis)
6) How would it work in turn‑based? (note even if not implemented)
7) How will it be debugged? (debug labels, gizmos, inspector visibility)

## Notes on “BoundsProviders”
“BoundsProvider” is just one way to supply an allowed region.
Architecturally this should generalize to **RegionSource / ConstraintSource** (level zones, navmesh areas,
objective areas, squad contexts, etc.). The key constraint: region supply belongs to World/Sensing or injected
context — not embedded ad‑hoc per policy.

---

## Suggested next actions
- Commit this document into the repo (e.g., `Docs/Orchestration/orchestration_architecture_charter.md`).
- Add the guardrails checklist to your “PR template” or to each Claude plan.
- Optionally write a separate short spec: **Engagement/ROE** (parameters + ownership boundaries).
