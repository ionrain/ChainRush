# Orchestration Spatial Dimensionality Decision (3D-First, 2D-Specialized)

Date: 2026-02-21  
Status: In progress (`C02A.1` started: 3D primitives + conversion seams added)

Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md`
3. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md`
4. `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`
5. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C07B_Spatial_DualRepresentation_Allowlist_Burndown_2026-02-22.md`

## 1) Problem

Current orchestration seams are largely planar (`Float2`, `AABB2D`).
This is safe for strict 2D/2.5D use-cases, but it creates architectural risk for reusable 3D-capable systems.

## 2) Decision

1. Cross-layer spatial contracts are `3D-first`.
2. `StrategyCombat` is not forced to be pure 2D.
3. Types with hard planar assumptions must be explicit `2D` specializations.
4. Planar (`2D`) modules must consume common `3D` seams via projection adapters.
5. Full 3D domain behavior implementation is not required in this slice.

## 3) Contract Direction

### 3.1 Common seam (Framework/Core/RuntimeHost)

1. Introduce canonical 3D value contracts (e.g. `Float3`, `AABB3D` / `AABBCC3D` naming by ADR choice).
2. Stabilize common spatial query seam as 3D-first (no genre coupling).
3. Keep existing 2D contracts only as transition compatibility where needed.

### 3.2 StrategyCombat specialization

1. If a class/policy is inherently planar, add suffix `2D`.
2. Planar classes use a projection adapter from 3D seam (`Float3 -> Float2`, bounds projection with fixed axis policy).
3. Classes that can remain generic/3D keep unsuffixed names.

## 4) Naming Rule (Mandatory)

1. Any type whose logic depends on planar assumptions (`Float2`, `AABB2D`, planar distance/containment) must end with `2D`.
2. Unsuffixed type names imply dimension-agnostic or 3D-capable behavior.
3. For variables/fields/properties, avoid `*3D` suffix; when both projections coexist, use `World*` for 3D (`WorldAnchor`, `WorldPosition`) and unsuffixed/`*2D` for planar compatibility.

## 5) Projection Policy

1. Projection from 3D to 2D must be explicit and centralized.
2. Projection policy includes:
   - plane mode (`XZ`, `XY`, etc.),
   - fixed axis rule (`Y=const`, `Z=const`, etc.),
   - deterministic conversion for position and bounds.
3. No hidden ad-hoc axis cuts inside domain policies.

## 6) Recommended Execution Point

Best window: `after C02` and `before C03` (new slice `C02A`).

Why:
1. `C02` first avoids doing spatial refactor while host files are being moved.
2. `C03/C04` proposal/arbitration seam should not be built on temporary planar-only contracts.
3. This minimizes double refactor of arbitration/world-query contracts.

## 7) Slice Proposal (`C02A`)

1. `C02A.1`: Add 3D-first spatial contracts and compatibility adapter seams.
2. `C02A.2`: Wire StrategyCombat planar modules through projection adapters (no behavior drift).
3. `C02A.3`: Apply `2D` suffix to strictly planar classes.
4. `C02A.4`: Add architecture tests and naming gates.

## 8) Required Architecture Gates

1. Package boundary exposes 3D-first spatial contracts.
2. No new planar-only contracts in package boundary without explicit `2D` specialization.
3. StrategyCombat planar classes use suffix `2D`.
4. Planar classes consume 3D seam via projection adapter (no direct scene-object truth bypass).
5. Public contracts (`Framework/Core/RuntimeHost`) must converge to a single canonical spatial representation (target: 3D-first). Dual `Float2` + `Float3` representation is migration-only and allowlist-gated.

## 9) Non-Goals (This Slice)

1. Implement complete 3D combat/idle behavior.
2. Remove all 2D types immediately.
3. Change gameplay behavior while introducing seam-level spatial contracts.

## 9.1) Transition Allowlist (Dual Spatial Representation)

Temporary migration exception (must shrink to zero after compatibility slices):

1. `Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs::WorldSnapshot`
2. `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs::ActorReadProjection`
3. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs::OrchestrationArbiterContext`
4. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs::ExecutionContext`

Rules:
1. Dual `Float2` + `Float3` in these types is compatibility-only.
2. No new public interface/struct may introduce the same duplication without explicit ADR/update of allowlist.
3. Internal hot-path caches may keep dual forms for performance, but this must not leak as public package contract shape.

## 10) C02A File Inventory by Layer (Rename vs Refactor)

Legend:
1. `Rename` = explicit planar specialization (`*2D`) with no behavior change.
2. `Refactor` = switch to 3D-first seam and/or projection adapter wiring.

### 10.1 Framework (`Packages/com.morboo.framework`)

Rename:
1. `Float2` is explicitly kept as-is in this migration (acts as stable 2D counterpart to `Float3`).
2. `Packages/com.morboo.framework/Runtime/Math/AABB2D.cs` remains explicit 2D and unchanged.

Refactor:
1. `Packages/com.morboo.framework/Runtime/State/IWorldQuery.cs` (3D-first position/bounds seam).
2. `Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs` (`WorldAnchor` for 3D, `Anchor` stays planar compatibility).
3. `Packages/com.morboo.framework/Runtime/State/IWorldState.cs` (signature cascade through `WorldSnapshot`).
4. `Packages/com.morboo.framework/Runtime/Decision/IProposalSource.cs` (signature cascade through `WorldSnapshot`).

Keep as-is (already explicit 2D):
1. `Packages/com.morboo.framework/Runtime/Math/Float2.cs`.
2. `Packages/com.morboo.framework/Runtime/Math/AABB2D.cs`.

### 10.2 Systems (`Packages/com.morboo.systems`)

Refactor:
1. `Packages/com.morboo.systems/Runtime/Unity/FrameworkUnityConversions.cs`:
   - add canonical 3D conversions for package seam,
   - keep explicit planar projection helpers (`3D -> 2D`) centralized here.

### 10.3 Core (`Packages/com.morboo.core`)

Refactor:
1. `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs` (`WorldPosition` for 3D seam, `Position` stays planar compatibility).

### 10.4 RuntimeHost (Target Layer After `C02` Move)

Current state after `C02.2`: host spatial files are already located in `Morboo.RuntimeHost`.
For `C02A`, these files are `RuntimeHost` seam refactor targets:

Refactor:
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs`
2. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs`
3. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs`
4. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`
5. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs`
6. `Packages/com.morboo.runtimehost/Runtime/Orchestration/DomainContracts/Idle/IIdleBoundsProvider.cs`
7. `Packages/com.morboo.runtimehost/Runtime/Orchestration/World/IdleBoundsRegistry.cs`

Rule:
1. `RuntimeHost` keeps unsuffixed names (3D-first seam), no `*2D` specialization types.

### 10.5 Integration.StrategyCombat (`Packages/com.morboo.integration.strategycombat`)

Rename (`*2D` specializations):
1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/Policies/IdleFillAreaPolicyAsset.cs` -> `IdleFillAreaPolicy2DAsset`.
2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/Policies/IdleRingSlotPolicyAsset.cs` -> `IdleRingSlotPolicy2DAsset`.
3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/Policies/IdleHoldPolicyAsset.cs` -> `IdleHoldPolicy2DAsset`.
4. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Common/CrowdScoringUtility.cs` -> `CrowdScoringUtility2D`.
5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Formations/FormationPatternAsset.cs` -> `FormationPattern2DAsset`.
6. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Formations/Patterns/GridFormationPatternAsset.cs` -> `GridFormationPattern2DAsset`.
7. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Formations/Patterns/RingFormationPatternAsset.cs` -> `RingFormationPattern2DAsset`.

Note:
1. `IdlePolicyAsset` currently resides in `Morboo.RuntimeHost` and stays unsuffixed per RuntimeHost 3D-first naming rule.

Refactor (3D seam consumption + projection adapters):
1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Combat/CombatCommand.cs`.
2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Idle/IdleCommand.cs`.
3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Combat/CombatAdapter.cs`.
4. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/IdleCommandAdapter.cs`.
5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/CombatOrchestratorLite.cs`.
6. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/Targeting/CombatTargetingPolicyAsset.cs`.
7. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/Targeting/Policies/NearestToSelfPolicyAsset.cs`.
8. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/Targeting/Policies/PrimaryTargetPolicyAsset.cs`.
9. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Combat/CombatInstructionBuilders.cs`.
10. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Combat/CombatIntentBuilders.cs`.
11. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/DomainContracts/Combat/CombatState.cs`.
12. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Instruction.cs`.
13. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Intent.cs`.
14. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/StateSnapshot.cs`.
15. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/ParamSet.cs` (planar param type path must be explicit and isolated).

### 10.6 MorbooBridge (`Assets/Scripts/MorbooBridge`)

Rename (`*2D` where behavior is strictly planar):
1. `Assets/Scripts/MorbooBridge/Orchestration/Execution/Combat/UnitCombatCommandExecutor.cs` -> `UnitCombatCommandExecutor2D`.
2. `Assets/Scripts/MorbooBridge/Orchestration/Execution/Combat/UnitCombatTargetSelector.cs` -> `UnitCombatTargetSelector2D`.
3. `Assets/Scripts/MorbooBridge/Orchestration/Execution/Combat/EnemyCombatCommandExecutor.cs` -> `EnemyCombatCommandExecutor2D`.
4. `Assets/Scripts/MorbooBridge/Orchestration/Execution/Idle/UnitIdleCommandExecutor.cs` -> `UnitIdleCommandExecutor2D`.
5. `Assets/Scripts/MorbooBridge/Orchestration/Units/UnitIdleBoundsProvider.cs` -> `UnitIdleBoundsProvider2D`.

Already explicit 2D:
1. `Assets/Scripts/MorbooBridge/Orchestration/Execution/Combat/TDE/AIActionSetBrainTargetFromExternal2D.cs`.

Refactor:
1. `Assets/Scripts/MorbooBridge/Orchestration/Units/UnitStateReporter.cs` (read 3D transform, project only at 2D adapter boundary).
2. `Assets/Scripts/MorbooBridge/Orchestration/Enemies/EnemyStateReporter.cs` (same rule).

## 11) Execution Note (Order)

1. Apply `C02` moves first (RuntimeHost placement stabilization).
2. Run `C02A` rename batch (`*2D`) with `git mv` only.
3. Apply 3D seam refactor batch per layer (Framework -> Systems/Core -> RuntimeHost -> StrategyCombat -> MorbooBridge).
4. Keep behavior parity gate after each batch (compile + architecture tests + smoke playtest).

## 12) Progress Log

2026-02-21 (`C02A.1` bootstrap done, no behavior changes):
1. Added `Float3` to framework:
   - `Packages/com.morboo.framework/Runtime/Math/Float3.cs`
2. Added `AABB3D` to framework:
   - `Packages/com.morboo.framework/Runtime/Math/AABB3D.cs`
3. Extended Unity conversion seam (systems):
   - `Packages/com.morboo.systems/Runtime/Unity/FrameworkUnityConversions.cs`
   - added `ToFloat3`/`ToVector3(Float3)`,
   - added explicit projection adapter `ProjectToFloat2(..., SpatialProjectionPlane)`,
   - added `Bounds <-> AABB3D` conversions.

Next (`C02A.2`):
1. Start runtimehost/world-query migration to consume 3D seam while preserving current StrategyCombat behavior via explicit projection path.

2026-02-21 (`C02A.2` compatibility slice started, no behavior changes):
1. Added optional 3D query seam in framework:
   - `Packages/com.morboo.framework/Runtime/State/IWorldQuery.cs`:
   - `IWorldQueryBase3D`, `ICrowdQuery3D`, `IWorldQuery3D`.
2. Upgraded actor/world projections to carry 3D snapshots with 2D compatibility:
   - `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs` (`WorldPosition` + compatible `Position` projection),
   - `Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs` (`WorldAnchor` + compatible `Anchor`).
3. RuntimeHost world cache now snapshots 3D and projects to 2D explicitly:
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs`,
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs` (anchor projection via `SpatialProjectionPlane`).
4. Added architecture gates for spatial seam bootstrap:
   - `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`
   - checks for `Float3`/`AABB3D` and explicit projection adapter presence.

2026-02-21 (`C02A.3` rename batch done, no behavior changes):
1. StrategyCombat planar classes renamed with `*2D` suffix:
   - `IdleFillAreaPolicy2DAsset`, `IdleRingSlotPolicy2DAsset`, `IdleHoldPolicy2DAsset`,
   - `CrowdScoringUtility2D`,
   - `FormationPattern2DAsset`, `GridFormationPattern2DAsset`, `RingFormationPattern2DAsset`.
2. MorbooBridge planar executors/providers renamed with `*2D` suffix:
   - `UnitCombatCommandExecutor2D`,
   - `UnitCombatTargetSelector2D`,
   - `EnemyCombatCommandExecutor2D`,
   - `UnitIdleCommandExecutor2D`,
   - `UnitIdleBoundsProvider2D`.
3. `Float2` intentionally not renamed in this slice.

2026-02-22 (`C02A` naming/contract hygiene hardening):
1. Variable/property naming aligned to `World*` for 3D snapshots (`WorldAnchor`, `WorldPosition`) instead of `*3D` suffix in internal/public fields.
2. Added architecture gate: dual `Float2` + `Float3` in public interfaces/structs is allowlist-only during migration.
