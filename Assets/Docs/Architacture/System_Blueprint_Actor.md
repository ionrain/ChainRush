# System Blueprint: Actor System

Date: 2026-02-20  
Template: `Assets/Docs/Architacture/New_System_Requirements_Template.md`  
Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
3. `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`
4. `Assets/Docs/Architacture/MasterMigration/00_Program/Game_Runtime_System_Decomposition_Layer_Mapping_2026-02-20.md`
5. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md`

## 1) System Passport

1. `System Name`: Actor System
2. `Owner`: Gameplay Domains Owner
3. `Target Phase`: Phase 4 preparation gate + Phase 6 implementation
4. `Scope Type`: major refactor + extraction/modularization
5. `Behavior Impact`: controlled

## 2) Problem / Outcome

1. `Problem Statement`:
   - текущие `Unit/Enemy` являются одновременно моделью, behavior owner, bridge и engine adapter;
   - actor-требования (faction/stats/abilities/role/group/inventory/brain/control/merge/spawn/state exchange) распределены по разным классам без единого owner;
   - оркестратор читает/пишет состояние через project-type concrete glue вместо стабильного system boundary.
2. `Business/Game Outcome`:
   - единая Actor-модель, пригодная для переиспользования и расширения;
   - фиксированный, финальный контур интеграции с оркестратором;
   - возможность рефакторинга оркестратора без постоянного изменения Actor-side API.
3. `In Scope`:
   - actor contract surface;
   - actor sub-systems and ownership;
   - orchestrator integration block;
   - migration bridge from current `Unit/Enemy` to actor interfaces.
4. `Out of Scope`:
   - полный уход от TDE в этом шаге;
   - финальная реализация всех внутренних actor подсистем;
   - UI/Board.

## 3) Architecture Archetype (Analogy)

1. `Selected Archetype`: Simulation Domain + Integration Adapter
2. `Why this archetype`:
   - Actor является базовым gameplay-доменом;
   - текущая игра требует bridge-слой между legacy MB (`Unit/Enemy`) и контрактами системы.
3. `What differs from reference`:
   - role эволюция (`single role` -> `role set`) учитывается сразу;
   - inventory/merge/player-control считаются отдельными actor-подсистемами, не полями одного класса.

## 4) Layer & Package Placement

1. `Framework`:
   - `EntityId`, `RoleId`, базовые world query/value types.
2. `Systems`:
   - generic in-memory runtime services (stores, registry helpers, lifecycle helpers).
3. `Core`:
   - Actor contracts and kernel-level models (identity, stats contracts, ability contracts, group/control contracts).
4. `RuntimeHost`:
   - orchestration-facing host seams and read models (`IWorldQuery`-oriented actor projection interfaces).
5. `Integration.StrategyCombat`:
   - strategycombat specializations (`AttackAbility`, `PassiveAbility`, combat/idle receivers, strategy policies).
6. `MorbooBridge`:
   - adapters from `Unit/Enemy/UnitData/EnemyData` to Actor contracts;
   - project maps (`UnitClass -> Role`, `EnemyType -> capabilities`) and runtime wiring.
7. `Game.Runtime`:
   - temporary behavior owners and engine-specific MB until adapters absorb them.

## 5) Folder Topology (Inside Layer)

1. `Core`:
   - `Packages/com.morboo.core/Runtime/Actor/Contracts`
   - `Packages/com.morboo.core/Runtime/Actor/Models`
   - `Packages/com.morboo.core/Runtime/Actor/State`
2. `RuntimeHost`:
   - `Packages/com.morboo.runtimehost/Runtime/Actor/Projection`
   - `Packages/com.morboo.runtimehost/Runtime/Actor/Routing`
3. `Integration.StrategyCombat`:
   - `Packages/com.morboo.integration.strategycombat/Runtime/Actor/Strategy`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Actor/Execution`
4. `MorbooBridge`:
   - `Assets/Scripts/MorbooBridge/Actor/Adapters`
   - `Assets/Scripts/MorbooBridge/Actor/Maps`
   - `Assets/Scripts/MorbooBridge/Actor/Wiring`

## 6) Actor Composition Model

Actor = aggregate root + composable sub-systems with separate state owners.

Mandatory actor sub-systems:
1. `Identity`:
   - entity id, archetype id, faction id/asset, role set, tags.
2. `Stats`:
   - global stat definitions;
   - per-actor stat block + modifiers + computed view.
3. `Abilities`:
   - abstract ability model in actor contracts;
   - strategycombat adds `AttackAbility`, `PassiveAbility`, etc.
4. `InventoryLink`:
   - actor reference to inventory container/service (parallel subsystem).
5. `Brain`:
   - AI state interface (engine-agnostic contract).
6. `Control`:
   - player/ai/scripted control mode and intent source binding.
7. `Group`:
   - actor membership (`GroupId`, formation role in group).
8. `Merge` (optional module):
   - merge progression and merge policies (can be role-driven).
9. `StateExchange`:
   - actor runtime state read model exposed to world/orchestrator.
10. `Bio`:
   - display/localized metadata (name/description/icon refs).

Infrastructure around actor:
1. `ActorManager`:
   - lifecycle/lookup owner only (no stats/abilities logic).
2. `ActorSpawner`:
   - spawn orchestration service and placement contracts.

## 7) Role Evolution Model

Rule:
1. actor does not have only one immutable role.

Model:
1. `PrimaryRoleId`
2. `SecondaryRoleIds`
3. `RoleTags`
4. `CapabilitySet`

Behavior:
1. Equipment/ability access is capability-driven, not `if role == X`.
2. Role evolution is adding/removing roles/capabilities, not replacing core actor identity.
3. Orchestrator routing still uses a stable routing role id per domain context (resolved from role set policy).

## 8) Stats And Abilities Model

Stats:
1. global catalog: `StatDefinition` (`StatId`, type, bounds, stacking policy).
2. actor stat state: base + layered modifiers (`role`, `progression`, `equipment`, `effect`).
3. strategy semantics like HP are stat definitions, not hardcoded core trait constants.

Abilities:
1. `Core`: `AbilityDefinition`, ability state, cooldown and activation contracts.
2. `Integration.StrategyCombat`: combat-specific ability kinds and targeting/execution policies.
3. Engine-specific execution (TDE or replacement) is adapter-only in `MorbooBridge/Game.Runtime`.

## 9) Communication Contract (No Direct Concrete Coupling)

1. `Inbound`:
   - actor commands from orchestration adapters (`Combat`, `Idle`, later generic intent commands);
   - actor state mutation commands from progression/inventory/reward systems.
2. `Outbound`:
   - actor lifecycle events;
   - actor state snapshots/query surface;
   - domain events for stat/ability/group changes.
3. `Forbidden`:
   - direct orchestrator calls into `Unit/Enemy` concrete classes;
   - actor subsystem concrete-to-concrete calls without contracts.

## 10) Orchestrator Integration Block (Final Boundary For Actor)

This block is the minimum final seam to freeze before orchestration refactor.

Read side (orchestrator consumes):
1. actor identity projection:
   - stable `EntityId`,
   - faction,
   - routing role id,
   - alive state.
2. actor world snapshot projection:
   - position,
   - role/group refs,
   - capability snapshot,
   - selected stat projections needed by policies.
3. optional idle/combat spatial providers:
   - idle bounds by role/group,
   - combat target set/group target providers.

Write side (orchestrator emits):
1. domain command dispatch to actor receivers:
   - combat receiver contract,
   - idle receiver contract.
2. no direct writes to actor stores from arbiter/router.
3. all writes flow through bus + adapter + actor command handlers.

Message contracts:
1. dispatch commands are typed and EntityId-addressed.
2. actor lifecycle events are typed and id-addressed.
3. state read is query-only; mutation is command-only.

Bridge rule:
1. `MorbooBridge` is the only layer where legacy `Unit/Enemy` map to Actor contracts during transition.

Post-refactor normalization target:
1. Actor boundary in package layers is domain-agnostic and does not expose hard-wired Combat/Idle contracts.
2. `Combat`/`Idle` remain StrategyCombat domain implementations/adapters, not Actor kernel contracts.
3. Any remaining Unit/Enemy-specific seams stay Bridge-only until removed.

## 11) Pre-Refactor Minimum For Actor (Must Have Before Orchestrator Refactor)

1. `Actor contract package surface` exists in `Core` (identity/stats/abilities/group/control/lifecycle/query/write contracts).
2. `Actor -> Orchestrator read projection` is stabilized and used via query contracts.
3. `Orchestrator -> Actor write path` is stabilized through dispatch command handlers.
4. `Trait taxonomy split`:
   - generic keys only in core;
   - strategy/project keys moved below core.
5. `MorbooBridge adapters`:
   - `Unit/Enemy` implement actor-facing adapters, not direct orchestrator coupling.
6. `No new Game.Runtime dependency` introduced into `RuntimeHost/Core`.
7. Architecture tests for the above are added/updated.
8. Temporary Actor-side coupling to `Combat/Idle` contracts (if still present) is explicitly tagged as migration debt with removal gate right after orchestration C07.

## 11.1) Post-Refactor Cleanup Gate (Immediately After Orchestrator C07)

1. Remove hard references from Actor boundary to concrete `Combat/Idle` domain contracts in package layers.
2. Keep StrategyCombat-specific command payloads/policies in `com.morboo.integration.strategycombat`, consumed through generic actor/orchestrator seams.
3. Introduce compact domain packaging pattern:
   - one domain module descriptor,
   - one domain handler/orchestrator entry,
   - data-driven policy/config assets,
   - avoid per-domain interface/class explosion by default.
4. Activate anti-file-sprawl budget for domain onboarding:
   - target: no more than 6 non-test code files to onboard a new orchestration domain module.
5. Add/enable architecture tests for:
   - no `Combat/Idle` hard tokens in actor contracts/runtime host actor boundary,
   - domain onboarding fan-out budget gate.

## 12) Testing & Fitness Gates

1. `Architecture`:
   - actor contracts in correct layer;
   - no project-layer refs in package actor code;
   - no domain/project trait keys in core;
   - no hard `Combat/Idle` coupling in actor package boundary after post-refactor cleanup gate.
2. `Behavior`:
   - bridge adapters preserve current unit/enemy behavior.
3. `Integration`:
   - orchestration reads actor state only through final projection interfaces;
   - dispatch commands reach actor handlers through bus/adapters;
   - new domain onboarding stays within fan-out budget (no file explosion).

## 13) Commit Slices (Actor Track)

1. `A0`: Actor blueprint + contract freeze doc + test plan.
2. `A1`: Core actor contracts and taxonomy cleanup.
3. `A2`: Bridge adapters for current Unit/Enemy to actor contracts.
4. `A3`: RuntimeHost query projection adoption for orchestration read side.
5. `A4`: command handler stabilization for orchestration write side.
6. `A5`: enforce gates in architecture tests.
7. `A6`: post-orchestrator-C07 cleanup: remove hard Actor boundary links to `Combat/Idle` contracts.
8. `A7`: compact domain module structure + file-sprawl budget gates (data-driven-first onboarding).

## 14) Definition Of Done

1. Actor system has clear owners for lifecycle, stats, abilities, roles, groups, control, merge, spawn.
2. Orchestrator integration block is fixed and does not require API churn during orchestrator refactor.
3. Legacy `Unit/Enemy` interact through `MorbooBridge` adapters.
4. Core remains engine-agnostic and free of project/genre taxonomy leaks.
5. Actor-orchestrator package boundary is free of hard `Combat/Idle` coupling.
6. Domain onboarding uses compact module structure without file explosion.
