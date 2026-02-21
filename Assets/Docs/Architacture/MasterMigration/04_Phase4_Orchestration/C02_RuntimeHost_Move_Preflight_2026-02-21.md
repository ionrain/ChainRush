# C02 RuntimeHost Move Preflight

Date: 2026-02-21  
Status: in progress (`C02.2` and `C02.3` completed; `C02.4` static checks completed; Unity compile/test gate pending)

Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md`

## Goal

Подготовить перенос host-инфраструктуры из `Morboo.Integration.StrategyCombat` в `Morboo.RuntimeHost` без циклов asmdef и без изменения поведения.

## A) Host Files Targeted For Move In C02

Current location: `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/`

1. `OrchestrationLoop.cs`
2. `Arbitration/OrchestrationArbiter.cs`
3. `Arbitration/OrchestrationArbiterContext.cs`
4. `Arbitration/OrchestrationArbiterProposals.cs`
5. `Arbitration/OrchestrationTickResult.cs`
6. `Arbitration/OrchestrationWorldCache.cs`
7. `Execution/ExecutionContext.cs`
8. `Execution/ExecutionRouter.cs`

## B) RuntimeHost Placement (Target)

1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs`
2. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs`
3. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs`
4. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterProposals.cs`
5. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationTickResult.cs`
6. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs`
7. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs`
8. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`

## C) Pre-Move Constraints (No Cycle Rule)

1. `Morboo.RuntimeHost` must not reference `Morboo.Integration.StrategyCombat`.
2. `Morboo.Integration.StrategyCombat` may reference `Morboo.RuntimeHost`.
3. Any type required by moved host files must be in:
   - `Morboo.Framework`, or
   - `Morboo.Core`, or
   - `Morboo.RuntimeHost`, or
   - `Morboo.Systems`.

## D) Required Contract Lift Before Mechanical `git mv`

These dependencies are currently still resolved from integration layer and must be lifted to host-safe layers first:

1. actor/read contracts used by host runtime path:
   - `IStateReporter`, `IOrchestrationActor`, `IFactionAssetProvider`,
   - `ICombatCommandReceiver`, `IIdleCommandReceiver`, `IRoleAssetProvider`.
2. dispatch payload dependencies used by `ExecutionRouter`:
   - `CombatCommand`, `IdleCommand`,
   - `DispatchCombatCommand`, `DispatchIdleCommand`.
3. policy/context contracts used by arbiter/router:
   - `IdleRolePolicyMapAsset`, `CombatRolePolicyMapAsset`, `CombatRoleConstraintsMapAsset`,
   - `IdlePolicyAsset` selection seam.
4. world/registry-facing contracts used by `OrchestrationWorldCache` and `OrchestrationArbiter`:
   - `OrchestrationRegistry`, `IdleBoundsRegistry`, `CombatTargetSet`.

Note:
1. Domain implementations, selectors, executors stay in `Morboo.Integration.StrategyCombat`.
2. A small host-facing subset of policy/map/contract types was lifted to `Morboo.RuntimeHost` to keep no-cycle asmdef constraints for moved host files; domain behavior logic remains in integration.
3. C02 does not include proposal-model change (`C03`) and does not include spatial seam change (`C02A`).

## E) Execution Order Inside C02

1. lift contracts/dependencies required by host files (compile-green checkpoint);
2. run mechanical file moves with `git mv` only;
3. fix references/asmdef graph;
4. run architecture tests and compile checks.

## F) Acceptance Snapshot For C02

1. all host loop/arbitration/router/cache files live in `Morboo.RuntimeHost`;
2. `Morboo.Integration.StrategyCombat` keeps domain behavior, selectors, executors, adapters and strategy-specific assets; host loop/arbitration/router/cache dependencies required by moved host files are lifted to `Morboo.RuntimeHost`;
3. no asmdef cycles;
4. no behavior changes introduced in this slice.

## G) Progress Log

2026-02-21 (`C02.2` completed: mechanical moves via `git mv` with `.meta`):

1. `OrchestrationLoop.cs`
2. `Arbitration/OrchestrationArbiter.cs`
3. `Arbitration/OrchestrationArbiterContext.cs`
4. `Arbitration/OrchestrationArbiterProposals.cs`
5. `Arbitration/OrchestrationTickResult.cs`
6. `Arbitration/OrchestrationWorldCache.cs`
7. `Execution/ExecutionContext.cs`
8. `Execution/ExecutionRouter.cs`

2026-02-21 (`C02.3` completed: reference/contract lift and asmdef-safe dependency cut, no behavior changes):

1. Lifted host-required contract set from integration to runtimehost:
   - actor/role/faction contracts (`IOrchestrationActor`, `IRoleAssetProvider`, `IFactionAssetProvider`);
   - dispatch + command contracts (`CombatCommand`, `IdleCommand`, `DispatchCombatCommand`, `DispatchIdleCommand`);
   - host policy map dependencies used by arbiter/router (`IdleRolePolicyMapAsset`, `CombatRolePolicyMapAsset`, `CombatRoleConstraintsMapAsset`, `IdlePolicyAsset`, `CombatTargetingPolicyAsset`, `CombatMoveConstraintsAsset`).
2. Lifted host world/registry dependencies used by world cache / arbiter:
   - `OrchestrationRegistry`, `IdleBoundsRegistry`, `CombatTargetSet`.
3. Updated architecture tests/document paths to runtimehost locations for moved host files.

2026-02-21 (`C02.4` static checks completed):

1. Verified host infra classes exist only in `Packages/com.morboo.runtimehost/Runtime/Orchestration/`.
2. Verified moved host classes are absent from `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/`.
3. Verified `Morboo.RuntimeHost.asmdef` has no reference to `Morboo.Integration.StrategyCombat`.
4. Verified no project-layer tokens in runtimehost runtime sources (`Assets/Scripts/MorbooBridge`, `Game.Runtime`, `Morboo.Bridge`, `Integration.Project`).

Next:
1. Run Unity compile + architecture tests as final C02 gate.
