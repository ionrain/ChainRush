# Master Migration Roadmap

Date: 2026-02-19  
Status: Active plan (`Phase 1 closed`, `Phase 2 closed`, `Phase 3 closed`)  
Scope: migration from current mixed codebase to kernel-first architecture with reusable systems and no TopDownEngine runtime dependency.

## 1) Sources Of Truth

This roadmap orchestrates existing docs, not replacing them:

1. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
2. `Assets/Docs/Architacture/Architecture_Layers_Reference.md`
3. `Assets/Docs/Architacture/Game_Systems_Architecture_Framework.md`
4. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Implementation_Audit_2026-02-19.md`
5. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md`
6. `Assets/Docs/Architacture/MasterMigration/05_Phase5_EngineSeam_And_Phase7_TDECutover/TopDownEngine_Exit_Migration_Backlog.md`
7. `Assets/Docs/Architacture/MasterMigration/06_Phase6_GameplayModularization/Morboo_Gameplay_Modularization_Backlog.md`
8. `Assets/Docs/Architacture/New_System_Requirements_Template.md`
9. `Assets/Docs/Architacture/System_Interaction_Contract_Template.md`
10. `Assets/Docs/Architacture/System_Blueprint_Index.md`
11. `Assets/Docs/Architacture/ADR/README.md`
12. `Assets/Docs/Architacture/MasterMigration/00_Program/Game_Runtime_System_Decomposition_Layer_Mapping_2026-02-20.md`
13. `Assets/Docs/Architacture/System_Blueprint_Actor.md`
14. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md`

## 2) Program Goal

Build a top-down, reusable architecture where:

1. Kernel systems own game lifecycle decisions (`GameFlow`, `Scenario`, `Objective`, `Outcome`, `Rulebook`).
2. `Entity Backbone` (`Entity Model` + `Registry/Factory` + `View Binding`) is explicit and mandatory.
3. Simulation domains are modular and portable to future projects.
4. Orchestration becomes domain-agnostic platform infrastructure (not Combat/Idle special-case).
5. TopDownEngine is removed from runtime dependencies.
6. Existing game behavior is preserved during migration slices.

## 3) Program Rules (Non-Negotiable)

1. One migration path only. No permanent parallel architecture.
2. Every phase has explicit `Entry Gate` and `Exit Gate`.
3. Any new feature must declare `System Owner` before coding.
4. Any architectural change must add/adjust a fitness test.
5. `Packages/com.morboo.*` do not depend on project layer (`Assets/Scripts/...`) and must not reference `Game.Runtime` or `Morboo.Bridge`.
6. TDE usage is allowed only behind adapter seams until cutover phase.
7. Changes are sliced to compile-green checkpoints.
8. Systems do not communicate via direct concrete-to-concrete runtime calls; only via contracts/events/queries.
9. New cross-system shared code is extracted deliberately (`Common`/lower layer) only after proving multi-system reuse.
10. Sirenix Odin is allowed for Unity editor/data authoring workflows, but must not become a required runtime dependency of kernel/runtime packages.
11. Untyped dependency holders (`GameObject`/`MonoBehaviour`/`Component` used as service locator inputs) are forbidden in new runtime architecture code.
12. For new domain/feature variability, `data-driven` solutions are preferred over new code branches.
13. `Architecture-first` is mandatory for any feature work: reuse existing contracts/patterns/extension points first; direct bypass solutions require ADR + cleanup plan with due phase.
14. Migration-only transitional forms (`legacy key strings`, `compat enums`, temporary adapter DTOs/shims) are allowed only in `Assets/Scripts/MorbooBridge`; they are forbidden in all `com.morboo.*` packages.
15. Task/PR closure requires both formal gates (tests/build) and a semantic closure check (layer meaning, single source of truth, reusable abstraction scope).
16. Lifecycle semantics are canonicalized as `EntityLifecycleState` in `com.morboo.core/Runtime/Entity`; package-layer actor/orchestrator contracts must use lifecycle state, while `IsAlive/SetAlive` are compatibility aliases only and must not be introduced as new package boundary contracts.
17. Spatial seams are `3D-first` in package boundaries; planar logic is allowed only as explicit `2D` specialization behind projection adapters.

## 4) Dependency Order (Critical Path)

Do not reorder:

1. `Kernel contracts`
2. `Entity backbone foundation`
3. `Orchestration remediation`
4. `Engine anti-corruption seam`
5. `Gameplay modularization`
6. `TDE cutover`
7. `Final architecture locks`

Reason: if gameplay migration starts before seams/contracts are stable, refactor loops become unbounded.

## 4.1) Package Placement Policy (Normative)

Every migrated contract/entity must be placed by reuse level:

1. `Any game` (pure abstractions/contracts) -> `Packages/com.morboo.framework`
2. `Any game runtime infra` (generic scheduler/bus/identity runtime services) -> `Packages/com.morboo.systems`
3. `Cross-genre kernel` (flow/objective/outcome/rulebook/session contracts and domain-agnostic kernel models) -> `Packages/com.morboo.core`
4. `Cross-genre host execution` (runtime host orchestration infra, no genre payloads) -> `Packages/com.morboo.runtimehost`
5. `Genre layer` (StrategyCombat-specific domain contracts/implementations/content policies) -> `Packages/com.morboo.integration.strategycombat`
6. `Concrete game` (project wiring, scene bindings, content maps, UI glue) -> `Assets/Scripts/MorbooBridge` + `Assets/Scripts/Game`

Hard rules:

1. `Kernel contracts` must live only in `com.morboo.framework` / `com.morboo.core` (host seams in `com.morboo.runtimehost`).
2. `com.morboo.*` packages must not depend on project-layer assemblies (`Game.Runtime`, `Morboo.Bridge`, `Integration.Project`).
3. New package families are forbidden during refactoring of the current game type; exception is only a package family for a new game type and requires ADR. If an abstraction "does not fit", extract/raise the blocking abstraction into existing layers instead of creating a shortcut package.
4. Migration-only transitional forms are forbidden in `com.morboo.*` packages and must be isolated in `Assets/Scripts/MorbooBridge`.
5. `IsAlive/SetAlive` are treated as compatibility aliases; lifecycle ownership and primary API must remain `EntityLifecycleState`/`SetLifecycleState` in Entity contracts.

## 4.2) Deferred `Framework + Core` Merge Decision Gate

Current migration keeps `com.morboo.framework` and `com.morboo.core` split.  
Merge is explicitly deferred until boundary risks are lower.

Guardrails while deferred:

1. `com.morboo.framework` must not depend on `com.morboo.core`.
2. `com.morboo.core` must depend only on `com.morboo.framework`.
3. No shim/duplicate types across `framework/core`.
4. Boundary tests for `framework/core` remain mandatory.

Decision checkpoint:

1. Primary checkpoint: after `Phase 4` exit gate (`orchestration platform remediation`).
2. Optional safer checkpoint: after `Phase 5` exit gate (`engine anti-corruption seam`).

Merge preconditions:

1. `Phase 1` architecture tests are green.
2. `Phase 2` kernel/entity contracts are stable and accepted by owners.
3. No cyclic dependencies in package graph.
4. Merge can be done as mechanical move (`git mv` + asmdef refs/tests update) without behavior change.

If merge is approved:

1. Create ADR for package boundary change.
2. Execute in dedicated structural PR (no behavior changes).
3. Update all affected architecture tests and docs in same PR.

## 5) Phased Plan

## Phase 0 — Program Setup & Governance

Goal: freeze ownership model and execution discipline.

Includes:

1. Adopt `Game_System_Catalog_v2` as owner map.
2. Create architecture decision log (`ADR`) folder (if absent).
3. Define PR template fields:
   - system owner,
   - invariants touched,
   - tests added/updated,
   - rollback plan.
4. Approve folder topology convention per layer:
   - `<Layer>/<SystemName>/...` for system-local code,
   - `<Layer>/Common/...` only for proven multi-system reuse.
5. Approve `system interaction contract` template:
   - allowed inbound/outbound commands/events/queries,
   - forbidden direct concrete dependencies.
6. Require a filled system blueprint for each new system:
   - `System_Blueprint_<SystemName>.md` based on `New_System_Requirements_Template.md`.
7. Maintain `System_Blueprint_Index.md` as a mandatory registry for blueprint readiness.
8. Enforce PR gate via `.github/pull_request_template.md`.

Entry Gate:

1. Catalog approved by team.
2. Migration order agreed.

Exit Gate:

1. Every active backlog item references a target system owner.
2. PR template includes architecture checklist.
3. Blueprint template adoption is enforced for new systems.

## Phase 0 Execution Slices (C0.1-C0.5)

1. `C0.1 Owner Matrix Lock`
   - define owner roles in `Game_System_Catalog_v2.md`,
   - map active backlog items to owner + target phase.
2. `C0.2 ADR Infrastructure`
   - create `Assets/Docs/Architacture/ADR/`,
   - add `ADR_Template.md` and baseline `ADR-0001`.
3. `C0.3 PR Policy Enforcement`
   - add `.github/pull_request_template.md` with architecture gates.
4. `C0.4 Interaction Contract Template`
   - add `System_Interaction_Contract_Template.md`.
5. `C0.5 Blueprint Registry Gate`
   - add `System_Blueprint_Index.md`,
   - require blueprint reference in PR for new systems/major refactor.

## Phase 1 — Guardrails Baseline

Goal: enforce current boundaries before structural moves.

Includes:

1. Existing layering tests in `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`.
2. Orchestration fitness tests in `Packages/com.morboo.architecture.tests/Tests/Editor/OrchestrationImplementationFitnessTests.cs`.
3. Expand tests for:
   - forbidden direct TDE dependency in package layer,
   - forbidden project refs in package layer,
   - no new reverse dependencies,
   - no direct system-to-system concrete coupling in package runtime code,
   - Odin usage policy for package runtime layers,
   - no new untyped dependency holder refs in package runtime code,
   - data-driven-first checks for new domain/feature variation (PR checklist at minimum),
   - file-sprawl budget checks for new entity/domain onboarding (at least as PR checklist, automated where feasible).

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md` -> `C01`.
2. `Assets/Docs/Architacture/MasterMigration/05_Phase5_EngineSeam_And_Phase7_TDECutover/TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 0`/`Slice 1` test checks.

Entry Gate:

1. Tests compile.

Exit Gate:

1. Architecture tests green in CI/editor.
2. Baseline playtest checklist recorded.
3. Complexity baseline recorded:
   - entity onboarding touchpoints count,
   - domain wiring fan-out count,
   - data-vs-code variation baseline (what is config-driven vs code-driven).
4. Baseline artifacts:
   - `Assets/Docs/Architacture/MasterMigration/01_Phase1_Guardrails/Phase1_Baseline_Playtest_Checklist_2026-02-20.md`
   - `Assets/Docs/Architacture/MasterMigration/01_Phase1_Guardrails/Phase1_Complexity_Baseline_2026-02-20.md`

## Phase 2 — Kernel Contracts First

Goal: create top-level control-plane contracts before domain rewrites.

Includes (contract-only first, minimal impl allowed):

1. `IGameFlowService`
2. `IScenarioService`
3. `IObjectiveService` (+ scope model: Meta/Campaign/Run/Encounter/Task)
4. `IOutcomeService`
5. `IRulebookProvider`
6. `ISessionStateStore` / `IProfileStateStore`
7. `ISaveLoadService`
8. `IEconomyLedger`
9. `IRewardService`

Entity contracts (mandatory in this phase):

1. `IEntityRegistry`
2. `IEntityFactory`
3. `IEntityLifecycleService`
4. `IEntitySnapshotStore`
5. `IEntityViewBinder` (adapter contract, implemented in experience layer)

Placement target:

1. universal abstractions -> `com.morboo.framework`.
2. cross-genre kernel contracts/models -> `com.morboo.core`.
3. host-runtime orchestration seams -> `com.morboo.runtimehost`.
4. generic runtime infra implementations -> `com.morboo.systems`.
5. strategycombat-specific contracts/payloads -> `com.morboo.integration.strategycombat`.
6. project glue only in `Assets/Scripts/MorbooBridge`.

Entry Gate:

1. Phase 1 green.
2. Owner matrix approved for kernel systems.

Exit Gate:

1. Contracts exist and compile.
2. No direct UI/scene dependencies in kernel contracts.
3. At least smoke tests for contract wiring exist.
4. Entity contracts are present and reviewed as ownership baseline.

## Phase 2 Execution Slices (C2.1-C2.3)

1. `C2.1 Kernel + Entity Contracts`
   - declare required kernel contracts in `com.morboo.core`,
   - declare required entity contracts in `com.morboo.core`,
   - add architecture smoke tests for contract presence and signatures.
2. `C2.2 Minimal Runtime Store Implementations`
   - add in-memory runtime implementations for `ISessionStateStore`, `IProfileStateStore`, `IEntitySnapshotStore` in `com.morboo.systems`,
   - add smoke tests for basic store wiring behavior,
   - keep behavior-neutral (no gameplay flow rewrites in this slice).
3. `C2.3 Minimal Kernel Runtime Service Implementations`
   - add in-memory/no-op runtime implementations for remaining kernel service contracts (`IGameFlowService`, `IScenarioService`, `IObjectiveService`, `IOutcomeService`, `IRulebookProvider`, `ISaveLoadService`, `IEconomyLedger`, `IRewardService`) in `com.morboo.systems`,
   - add smoke tests for basic service wiring behavior,
   - keep behavior-neutral (no gameplay flow rewrites in this slice).

## Phase 3 — Entity Backbone Foundation

Goal: establish single source of truth for gameplay entities before deep domain migration.

Includes:

1. Implement minimal `Entity Model` (ID + state + tags/traits/capabilities seam).
2. Implement central `Entity Registry` (`create/destroy/get/events`).
3. Implement `Factory` ownership for spawn/despawn lifecycle.
4. Introduce view-binding pipeline (`entity -> view`) with explicit ownership.
5. Bridge current `Unit/Enemy` lifecycle managers to registry facade (no behavior rewrite required yet).

Entry Gate:

1. Phase 2 contracts in place.
2. Phase 1 tests green.

Exit Gate:

1. Entity lifecycle is owned by registry/factory, not scattered managers.
2. Domain logic can address entities by `EntityId`.
3. New features no longer use `Transform` as source of truth.
4. No double-source HP/state between model and view for migrated entities.

## Phase 3 Execution Slices (C3.1-C3.3)

1. `C3.1 Entity Backbone Foundation (in-memory)`
   - status: `closed`,
   - add minimal `EntityState` model in `com.morboo.core`,
   - add `InMemoryEntityRegistry`, `InMemoryEntityFactory`, `InMemoryEntityLifecycleService`, `InMemoryEntityViewBinder` in `com.morboo.systems`,
   - add smoke tests for `create/destroy/get/events` and `entity -> view` binding.
2. `C3.2 Manager Bridge To Registry Facade`
   - status: `closed` (bridge wired in `Level.unity` + playtest checklist completed),
   - bridge existing `Unit/Enemy` lifecycle managers to registry/factory facade,
   - keep runtime behavior unchanged (adapter-only migration).
3. `C3.3 Entity Source-Of-Truth Hardening`
   - status: `closed` (`C3.3.1..C3.3.4 implemented; sign-off checklist closed`),
   - execution plan: `Assets/Docs/Architacture/MasterMigration/03_Phase3_EntityBackbone/Phase3_C3.3_Entity_SourceOfTruth_Hardening_Plan_2026-02-20.md`,
   - align migrated features to model-first ownership (state reads by `EntityId`),
   - remove double-source state in migrated paths,
   - transitional trait-key constants moved out of package layers (Bridge-only), with active architecture gate.

## Phase 4 — Orchestration Platform Remediation

Goal: make orchestration a reusable platform seam.
Current execution slice: `C02` (final Unity gate pending) + `C02A.1` bootstrap, `C02A.2` compatibility slice started, `C02A.3` rename batch done (`Float2` kept unchanged) + `C03` collector seam in runtime path + `C04` in progress (arbiter proposal-list path + `IArbiter` proposal-list overload; `RuntimeHostTests` moved to proposal-path coverage; legacy ArbitrationInput overload remains compatibility-only with dedicated compatibility test) + `C04A` closed for current single-scope path (low-friction domain onboarding seams, route ownership extraction to `Morboo.Integration.StrategyCombat`, route-policy pilot, and single-source-of-truth cleanup for scene domains completed) + `C04B` in progress (`Faction-first` start; `B2-B4` are now in code: `OrchestrationLoop` hosts ordered `OrchestrationPipelineComponent[]` and per-pipeline domain composition; `B5` in code: per-pipeline faction + host-global relations composition propagates into arbiter contexts; `B6` now pivots to domain-owned `CombatTargetProvider` / `IdleTargetProvider` in `Morboo.Integration.StrategyCombat` while `RuntimeHost` pipeline remains targeting-agnostic; `B7` host/path migration is in code: `Level` scene now uses `Player + Enemy` pipelines and `OrchestrationLoop` shares one command bus across pipelines with per-flush dispatch-context override for adapter compatibility; `B7` may be accepted as host/path parity even if enemy behavior parity is incomplete due to current `UnitClass`-oriented mapping assumptions) + `C04C` closed (single shared `StrategyCombatDomainOrchestrator` entrypoint + `Combat/IdleDomainComponent` composition, shared target-provider base/interface + runtime validation helper, shared route-policy provider/bridge wiring, and scene migration to wrapper-based domain orchestrators completed; no `*Lite` or separate `Combat/Idle` domain-orchestrator entrypoint classes remain in runtime code/scene) + `C04D` closed (generic orchestration composition abstractions extracted from `Morboo.Integration.StrategyCombat` into `Morboo.RuntimeHost`: `DomainOrchestratorComponent`, `DomainComponent`, `DomainOrchestratorComposition`, `IDomainRouteExecutionPolicyConsumer`, `DomainRouteExecutionPolicy`, `DomainRouteExecutionPolicyProvider`, `DomainTargetProvider`; genre layer rebound; bridge renamed to `DomainRouteExecutionPolicyBridge` with generic contracts; scene/tests updated; no compatibility path; monolith policy split deferred — `StrategyCombatRouteExecutionPolicyAsset` inherits `DomainRouteExecutionPolicy` but per-route split is not yet done) + `C05` closed (domain event pipeline activated: `IEventBusProvider`/`ICommandBusProvider` in Framework, `InProcessEventBus` deferred multi-handler, `OrchestrationModeChangedEvent`/`OrchestrationTickExecutedEvent` in RuntimeHost, EventBus wired into pipeline tick, `EventBusSubscriber` universal base, `ModeChangeDebugSubscriber` proof-of-integration; command adapter refactor deferred — needs `IOrchestrationContextProvider`; Tier 2 events deferred).
Execution rule (Phase 4+): if a weak link blocks moving an abstraction to the correct upper layer, first abstract/fix the weak link; do not silently lower or localize the target architecture without explicit approval and roadmap/backlog note.
Phase 4 checkpoint cleanup (`C04A`, current commit boundary):
1. `StrategyCombatExecutionRoutes` aggregate helper removed; route ownership is split into per-route executors under `Morboo.Integration.StrategyCombat`.
2. StrategyCombat route executors are now instance-based (no static "route-combinator" pattern as architecture example); shared unknown-route fallback uses one singleton executor instance to keep duplicate fallback registration noise suppressed.
3. `ExecutionRouter.RegisterUnknownRouteFallback(...)` ignores duplicate registration of the same delegate and still warns on conflicting fallback registration.
4. `RuntimeHost` route side is intentionally kept as generic registry/dispatch host seam only; domain route bodies and defensive unknown-route fallback behavior live above it.
5. Data/policy-driven route execution pilot started in `Morboo.Integration.StrategyCombat`: optional `StrategyCombatRouteExecutionPolicyAsset` can override mode-change hold behavior and selected debug/warning semantics (including `Idle` fallback warnings, `NoRoleMatch` label, and debug trace toggles) for `Combat/Idle/None/UnknownRouteFallback` executors while preserving legacy defaults when unset.
6. Structured runtime-facing route settings facade introduced (`StrategyCombatRouteExecutionProfile`) and route executors now consume grouped route settings (`Combat/Idle/None/UnknownRouteFallback`); serialized `StrategyCombatRouteExecutionPolicyAsset` was also converted to grouped route sections (no flat compatibility layer kept).
7. Bridge-level route-profile preset selection seam introduced in `MorbooBridge` (`StrategyCombatRouteExecutionPolicyBridge`): a shared `StrategyCombatRouteExecutionPolicyAsset` is applied before `OrchestrationLoop` builds route registrations, while `OrchestrationLoop.domainOrchestrators` remains the single scene source-of-truth for enabled/ordered domains (bridge reads loop-configured domains instead of keeping a duplicate domain list).
8. `OrchestrationLoop` no longer exposes `domainModules` / `OrchestrationDomainModule` in the current single-scene path to avoid an unused second composition mechanism.
9. Route-policy pilot is now behavior-tested (`RuntimeHostTests`): `StrategyCombatRouteExecutionPolicyAsset` can change `None` route mode-change hold-all emission (`on/off`) without any `RuntimeHost` code changes.
10. `C04A` is formally closed for the current single-scene / single-scope path. Remaining multi-faction structural concerns (multi-arbiter host, per-pipeline scope/domain ownership, scope-aware targeting ownership) are moved to `C04B` and are no longer tracked as `C04A` blockers.
Phase 4 next slice (after this checkpoint): continue `C04B` (`multi-scope / multi-arbiter host restructure`) before broad multi-faction rollout. `C04C` (domain-orchestrator form convergence) is closed as a domain-shape milestone. `C04D` (generic orchestration composition extraction) is closed — generic component/composition/policy/provider/target-provider forms now live in `Morboo.RuntimeHost`; monolith policy split (per-route assets) is deferred but does not block layer ownership goals. Further `StrategyCombat` route-policy/profile evolution must follow `C04D` ownership boundaries and must not reintroduce RuntimeHost ownership of route bodies.
C01A status: `closed` (2026-02-21, boundary and tests active).

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md` -> `C02`, `C02A`, `C03..C07` + `C04A` mandatory.
2. `C08..C10` continue in later hardening phase.
3. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md` -> mandatory pre-refactor contract freeze gate.
4. `System_Blueprint_Actor.md` -> actor-side boundary freeze for orchestration integration.
5. `System_Blueprint_Actor.md` section `11` (`Pre-Refactor Minimum For Actor`) -> mandatory implementation slice before C02.
6. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Spatial_Dimensionality_3DFirst_2026-02-21.md` -> mandatory spatial contract decision (`C02A`) before `C03`.
7. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C02_RuntimeHost_Move_Preflight_2026-02-21.md` -> C02 preflight dependency cut map.
8. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C07B_Spatial_DualRepresentation_Allowlist_Burndown_2026-02-22.md` -> final Phase 4 plan to shrink public spatial dual-representation allowlist (`Float2` + `Float3`) to zero.
9. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C04B_MultiScope_MultiArbiter_Host_Restructure_2026-02-23.md` -> planned structural step for multi-faction/multi-cohort orchestration host (`LoopHost -> Pipelines[]`), scene-breaking allowed.
10. `C04C` -> explicit convergence of legacy per-domain orchestrator entrypoint classes toward shared/composable `StrategyCombatDomainOrchestrator` + domain-components/providers/data form in `Morboo.Integration.StrategyCombat` (closed in code/scene).
11. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C04D_Generic_Orchestration_Composition_Extraction_2026-02-23.md` -> closed (generic orchestration composition abstractions extracted to `Morboo.RuntimeHost`; monolith policy split deferred).
12. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C05_Event_Pipeline_Activation_2026-02-24.md` -> closed (domain event pipeline activation + bus provider decoupling + subscriber infrastructure; `IEventBusProvider`/`ICommandBusProvider` in Framework; `EventBusSubscriber` universal base in RuntimeHost; command adapter refactor deferred — needs `IOrchestrationContextProvider`).

Execution order inside phase:

1. `C01A` implement minimal Actor boundary for orchestration (contracts/projections/dispatch + bridge adapters) per `System_Blueprint_Actor.md` section `11`.
2. `C02` move host responsibilities to `Morboo.RuntimeHost`.
3. `C02A` freeze spatial dimensionality seam (`3D-first` contracts + `2D` strategy specializations + projection adapters).
   - rule: package public contracts converge to one canonical spatial form (target `Float3`); temporary dual `Float2`+`Float3` contracts are allowlist-gated only during compatibility migration.
4. `C03` introduce proposal collection seam.
5. `C04` move arbiter to proposal-list input.
6. `C04A` add low-friction domain onboarding seam (no file explosion on new domain add).
   - rule: host-runtime arbitration/wiring MUST NOT accumulate domain-name specific branching; temporary seams are explicit, allowlist-gated, and scheduled for replacement by registration/policy metadata.
   - continuation: route execution in `Morboo.Integration.StrategyCombat` should converge to data/policy-driven configuration, not static helper growth.
7. `C04B` restructure orchestration host into multi-scope / multi-arbiter pipeline model before broad multi-faction rollout (`LoopHost -> Pipelines[]`, single source-of-truth for scope + per-pipeline domains; scene-breaking allowed).
8. `C04C` converge StrategyCombat domain orchestration to one shared `StrategyCombatDomainOrchestrator` + domain components/providers/data (remove `*Lite` and separate `Combat/Idle` domain-orchestrator entrypoint classes as target shape).
   - target-provider rule for this step: `CombatTargetProvider` and `IdleTargetProvider` must converge to a shared parent + common orchestration-facing interface (typed domain-specific API only as extensions).
9. `C04D` extract generic orchestration composition abstractions from `Morboo.Integration.StrategyCombat` into existing upper packages (`RuntimeHost` primary owner for Unity-dependent generic orchestration component form; `Core` / `Framework` only where semantically valid) (`DomainOrchestratorComponent`, `DomainComponent`, `DomainTargetProvider`, generic route-policy contracts/providers); execute as structural cut without compatibility path or legacy fallback.
   - naming rule for this step: abstract types moved to `Core` / shared runtime / `RuntimeHost` MUST NOT use `Base` suffix; use semantic names + `abstract`.
10. `C05` (closed) activate domain event pipeline: `IEventBusProvider`/`ICommandBusProvider` in Framework; `InProcessEventBus` upgraded to deferred multi-handler; orchestration lifecycle events (`OrchestrationModeChangedEvent`, `OrchestrationTickExecutedEvent`) in RuntimeHost; EventBus wired into pipeline tick (flush after CommandBus); `IDomainEventHandler<T>` typed contract; `EventBusSubscriber` universal MonoBehaviour base; `ModeChangeDebugSubscriber` proof-of-integration; command adapter refactor deferred (needs `IOrchestrationContextProvider`); Tier 2 events deferred.
   - bus provider rule: `IEventBusProvider`/`ICommandBusProvider` live in Framework; any bus owner (not only `OrchestrationLoop`) can implement them.
   - event dispatch rule: `EventBus.Flush()` strictly after `CommandBus.Flush()` — subscribers react after commands are dispatched.
   - route executor rule: route executors are NOT migrated to EventBus — they receive `ArbiterDecision` + `world` + `ctx` directly for same-tick cross-domain Hold emission.
11. `C06` connect capabilities to runtime decisions.
12. `C07` remove domain downcasts to concrete world cache.
13. `C07A` post-refactor cleanup: remove Actor boundary hard links to Combat/Idle and compact domain onboarding structure.
14. `C07B` shrink spatial dual-representation allowlist to zero (remove temporary `Float2` + `Float3` duplication from package public contracts).

`C01A` scope (must be done before C02):

1. actor contract package surface exists and is used by orchestration read/write paths;
2. actor read projection is stabilized for orchestrator queries;
3. dispatch write path is stabilized through actor handlers (no direct concrete writes from arbiter/router);
4. `Unit/Enemy` orchestration coupling is isolated in `MorbooBridge` adapters;
5. architecture tests for actor boundary are active.
6. actor/orchestrator package boundary uses lifecycle-state contracts; legacy alive aliases are compatibility-only and not reintroduced as boundary surface.

`C01A` closure evidence:

1. actor contract surface in `Packages/com.morboo.core/Runtime/Actor/ActorContracts.cs`.
2. actor read projection in `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs`.
3. orchestration read-side adoption in `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs` and `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`.
4. command dispatch write path via `DispatchCombatCommand` / `DispatchIdleCommand` in `Packages/com.morboo.runtimehost/Runtime/Orchestration/DomainContracts/Dispatch/` and adapters in `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/`.
5. Unit/Enemy bridge adapters in `Assets/Scripts/MorbooBridge/Orchestration/`.
6. architecture gates in `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`.

Entry Gate:

1. Phase 3 entity backbone stable.
2. Phase 1 tests green.
3. Pre-refactor minimum contract blocks approved and baseline-implemented for orchestrator-coupled systems.
4. `C01A` completed and verified green.
5. `C02A` completed and verified green before starting `C03`.
6. Spatial dual-representation allowlist is not expanding (new public `Float2`+`Float3` contract types forbidden without explicit decision update).

Exit Gate:

1. RuntimeHost contains host infrastructure.
2. Proposal contracts (`IProposalSource`/`Proposal`) are used in runtime path.
3. No fixed Combat/Idle-only arbitration input.
4. Capabilities are consumed (not only registered).
5. Actor-orchestrator package boundary has no hard Combat/Idle coupling (StrategyCombat-only specialization remains below boundary).
6. Domain onboarding seam is compact and data-driven-first (no file explosion beyond agreed fan-out budget).
7. Relevant future-gate tests are un-ignored and green.
8. Public package contracts no longer duplicate spatial representation (`Float2` + `Float3`) except explicitly documented/perf-internal non-contract cases.

## Phase 5 — Engine Anti-Corruption Layer (TDE Containment)

Goal: isolate engine-specific behavior behind adapters before removal.

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/05_Phase5_EngineSeam_And_Phase7_TDECutover/TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 2` + `Slice 3`.

Includes:

1. Replace TDE types in data models with game-owned types.
2. Introduce game-owned runtime interfaces for agent/movement/health/weapon/damage.
3. Keep temporary TDE adapters as the only TDE-dependent runtime path.

Entry Gate:

1. Phase 4 orchestration seam stable and green.

Exit Gate:

1. Gameplay code depends on abstractions, not TDE concrete types.
2. Direct TDE references remain only in adapter folders.
3. Behavior parity smoke tests green.

## Phase 6 — Gameplay Modularization (Vertical Slices)

Goal: move legacy game logic to reusable modules with stable boundaries.

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/06_Phase6_GameplayModularization/Morboo_Gameplay_Modularization_Backlog.md` full scope.

Required sequence (vertical slices):

1. `Actors + Identity + Spawn`
2. `Combat + Abilities`
3. `Goals + Objective bridge + LevelFlow`
4. `Economy + Rewards + Inventory`
5. `Merge` (optional)
6. `UI widgets/presenters` split

For each slice:

1. create contracts and asmdef boundary first,
2. move code,
3. add tests,
4. remove legacy duplicate path.
5. enforce folder placement rule (`System` vs `Common`) with explicit rationale in PR.

Entry Gate:

1. Phase 5 abstractions in place.

Exit Gate:

1. Slice compiles and runs.
2. No duplicate source-of-truth for moved subsystem.
3. No forbidden dependencies introduced.

## Phase 7 — TopDownEngine Exit Cutover

Goal: remove TDE runtime dependency from project/package assemblies.

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/05_Phase5_EngineSeam_And_Phase7_TDECutover/TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 4..Slice 6`.

Includes:

1. Replace TDE AI nodes/extensions with game-owned runtime logic.
2. Migrate prefabs/scenes/assets off TDE components.
3. Remove `MoreMountains.TopDownEngine` from asmdefs:
   - `Assets/Scripts/Game/Game.Runtime.asmdef`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef`

Entry Gate:

1. Phase 6 critical slices complete.

Exit Gate:

1. Zero required runtime dependency on TDE.
2. No missing scripts in main scene/prefabs.
3. Smoke scenarios green.

## Phase 8 — Final Hardening & Architecture Locks

Goal: finalize architecture and eliminate transitional debt.

Backlog links:

1. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md` -> `C08..C10`.
2. `Assets/Docs/Architacture/MasterMigration/05_Phase5_EngineSeam_And_Phase7_TDECutover/TopDownEngine_Exit_Migration_Backlog.md` -> optional `Slice 7` (MMTools event migration).

Includes:

1. Core cleanup from Unity-coupled types where targeted.
2. Remove dead transitional pathways.
3. Turn ignored architecture tests into active gates.
4. Align docs with actual code.

Entry Gate:

1. Phase 7 complete.

Exit Gate:

1. All architecture tests green.
2. Open debt is explicit and scheduled, not hidden in code.
3. Docs and code agree on system ownership and boundaries.

## 6) Backlog-to-Phase Mapping

## 6.1 Orchestration Backlog

1. `C01` -> Phase 1
2. `C02`, `C02A`, `C03..C07` + `C04A` -> Phase 4
3. `C08..C10` -> Phase 8

## 6.2 TDE Exit Backlog

1. `Slice 0..1` -> Phase 1
2. `Slice 2..3` -> Phase 5
3. `Slice 4..6` -> Phase 7
4. `Slice 7` -> Phase 8 (optional)

## 6.3 Gameplay Modularization Backlog

1. contracts prep -> Phase 2
2. entity foundation prep -> Phase 3
3. module slices -> Phase 6
4. leftovers cleanup -> Phase 8

## 7) Parallelization Rules

Allowed in parallel:

1. Test hardening and documentation.
2. Independent gameplay slices after shared contracts are stable.

Not allowed in parallel:

1. `Orchestration C03/C04` with major gameplay domain moves.
2. `TDE cutover` before adapter seam completion.
3. `Core cleanup` before arbitration/query seams stabilize.
4. entity lifecycle rewrite and active gameplay slice rewrite in same PR.

## 8) PR Policy To Avoid Endless Refactor

Each PR must include:

1. Target phase and target system owner.
2. Exact invariant changed/added.
3. Test evidence (new or updated).
4. Rollback-safe checkpoint.
5. Link to filled system blueprint (`System_Blueprint_<SystemName>.md`) for new systems/major refactors.
6. Reuse audit:
   - what was reused from existing systems,
   - what was extracted to shared level and why.
7. Fan-out note for new entity/domain wiring:
   - touched files count,
   - justification if above team budget.
8. Typed-reference note:
   - any `GameObject`/`MonoBehaviour`/`Component` dependency refs introduced,
   - justification + removal plan or ADR link.
9. Data-driven note:
   - what variation is expressed via data/policies/maps,
   - what required new code branches and why.
10. Architecture-first note:
   - which existing contracts/patterns/extension points were considered/reused,
   - if bypassed, ADR link + temporary debt removal phase.

PR must not include:

1. Unrelated structural moves.
2. New temporary global singleton/registry unless explicitly approved in ADR.
3. Hidden behavior changes in “refactor-only” PRs.

## 9) Completion Criteria (Program Done)

Migration considered complete when:

1. Kernel systems exist with explicit owners and contracts.
2. Entity backbone exists as explicit owner (`model + registry/factory + binding`) with single source of truth.
3. Orchestration works as proposal-driven platform, not Combat/Idle special case.
4. Gameplay modules are isolated and reusable.
5. Runtime no longer depends on TopDownEngine.
6. Architecture tests enforce all critical boundaries.
7. Documentation and code boundaries are consistent.
