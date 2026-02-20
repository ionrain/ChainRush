# Master Migration Roadmap

Date: 2026-02-19  
Status: Active plan (`Phase 1 closed`, `Phase 2 closed`, `Phase 3 in progress (C3.1 foundation + C3.2 manager bridge)`)  
Scope: migration from current mixed codebase to kernel-first architecture with reusable systems and no TopDownEngine runtime dependency.

## 1) Sources Of Truth

This roadmap orchestrates existing docs, not replacing them:

1. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
2. `Assets/Docs/Architacture/Architecture_Layers_Reference.md`
3. `Assets/Docs/Architacture/Game_Systems_Architecture_Framework.md`
4. `Assets/Docs/Architacture/Orchestration_Implementation_Audit_2026-02-19.md`
5. `Assets/Docs/Architacture/Orchestration_Remediation_Backlog_By_Commits.md`
6. `Assets/Docs/Architacture/TopDownEngine_Exit_Migration_Backlog.md`
7. `Assets/Docs/Architacture/Morboo_Gameplay_Modularization_Backlog.md`
8. `Assets/Docs/Architacture/New_System_Requirements_Template.md`
9. `Assets/Docs/Architacture/System_Interaction_Contract_Template.md`
10. `Assets/Docs/Architacture/System_Blueprint_Index.md`
11. `Assets/Docs/Architacture/ADR/README.md`

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
5. `Packages/com.morboo.*` do not depend on project layer (`Assets/Scripts/...`).
6. TDE usage is allowed only behind adapter seams until cutover phase.
7. Changes are sliced to compile-green checkpoints.
8. Systems do not communicate via direct concrete-to-concrete runtime calls; only via contracts/events/queries.
9. New cross-system shared code is extracted deliberately (`Common`/lower layer) only after proving multi-system reuse.
10. Sirenix Odin is allowed for Unity editor/data authoring workflows, but must not become a required runtime dependency of kernel/runtime packages.
11. Untyped dependency holders (`GameObject`/`MonoBehaviour`/`Component` used as service locator inputs) are forbidden in new runtime architecture code.
12. For new domain/feature variability, `data-driven` solutions are preferred over new code branches.
13. `Architecture-first` is mandatory for any feature work: reuse existing contracts/patterns/extension points first; direct bypass solutions require ADR + cleanup plan with due phase.

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
2. `com.morboo.*` packages must not depend on project-layer assemblies.
3. Any new package family must be introduced only via ADR.

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

1. `Orchestration_Remediation_Backlog_By_Commits.md` -> `C01`.
2. `TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 0`/`Slice 1` test checks.

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
   - `Assets/Docs/Architacture/Phase1_Baseline_Playtest_Checklist_2026-02-20.md`
   - `Assets/Docs/Architacture/Phase1_Complexity_Baseline_2026-02-20.md`

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
   - add minimal `EntityState` model in `com.morboo.core`,
   - add `InMemoryEntityRegistry`, `InMemoryEntityFactory`, `InMemoryEntityLifecycleService`, `InMemoryEntityViewBinder` in `com.morboo.systems`,
   - add smoke tests for `create/destroy/get/events` and `entity -> view` binding.
2. `C3.2 Manager Bridge To Registry Facade`
   - bridge existing `Unit/Enemy` lifecycle managers to registry/factory facade,
   - keep runtime behavior unchanged (adapter-only migration).
3. `C3.3 Entity Source-Of-Truth Hardening`
   - align migrated features to model-first ownership (state reads by `EntityId`),
   - remove double-source state in migrated paths.

## Phase 4 — Orchestration Platform Remediation

Goal: make orchestration a reusable platform seam.

Backlog links:

1. `Orchestration_Remediation_Backlog_By_Commits.md` -> `C02..C07` + `C04A` mandatory.
2. `C08..C10` continue in later hardening phase.

Execution order inside phase:

1. `C02` move host responsibilities to `Morboo.RuntimeHost`.
2. `C03` introduce proposal collection seam.
3. `C04` move arbiter to proposal-list input.
4. `C04A` add low-friction domain onboarding seam (no file explosion on new domain add).
5. `C05` activate domain event pipeline.
6. `C06` connect capabilities to runtime decisions.
7. `C07` remove domain downcasts to concrete world cache.

Entry Gate:

1. Phase 3 entity backbone stable.
2. Phase 1 tests green.

Exit Gate:

1. RuntimeHost contains host infrastructure.
2. Proposal contracts (`IProposalSource`/`Proposal`) are used in runtime path.
3. No fixed Combat/Idle-only arbitration input.
4. Capabilities are consumed (not only registered).
5. Relevant future-gate tests are un-ignored and green.

## Phase 5 — Engine Anti-Corruption Layer (TDE Containment)

Goal: isolate engine-specific behavior behind adapters before removal.

Backlog links:

1. `TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 2` + `Slice 3`.

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

1. `Morboo_Gameplay_Modularization_Backlog.md` full scope.

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

1. `TopDownEngine_Exit_Migration_Backlog.md` -> `Slice 4..Slice 6`.

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

1. `Orchestration_Remediation_Backlog_By_Commits.md` -> `C08..C10`.
2. `TopDownEngine_Exit_Migration_Backlog.md` -> optional `Slice 7` (MMTools event migration).

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
2. `C02..C07` + `C04A` -> Phase 4
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
