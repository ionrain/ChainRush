# C07B Spatial Dual-Representation Allowlist Burndown (to Zero)

Date: 2026-02-22  
Phase: `Phase 4 - Orchestration Platform Remediation`  
Status: Planned

Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Spatial_Dimensionality_3DFirst_2026-02-21.md`
3. `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`
4. `Assets/Docs/Architacture/Architecture_Compliance_Standard.md`

## 1) Goal

Сжать migration allowlist dual spatial representation (`Float2` + `Float3` в одном public contract surface) до `0`.

Target result:
1. В `Framework/Core/RuntimeHost` public contracts остаётся одна каноническая spatial-форма.
2. `StrategyCombat`/`MorbooBridge` получают planar (`Float2`) данные через projection adapters локально.
3. Allowlist в архитектурном тесте становится пустым.

## 2) Current Allowlist (Baseline)

1. `Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs::WorldSnapshot`
2. `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs::ActorReadProjection`
3. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs::OrchestrationArbiterContext`
4. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs::ExecutionContext`

## 3) Preconditions (must be true before C07B)

1. `C03..C07A` completed (proposal pipeline, capabilities wiring, world-query purity, actor hard-link cleanup).
2. `StrategyCombat` planar policies/executors consume projected values locally (not from duplicated public contract fields).
3. RuntimeHost no longer requires planar compatibility fields in public contexts for domain dispatch.

## 4) Canonical Spatial Rule (for this burndown)

1. Canonical cross-layer spatial representation: `Float3`.
2. Planar representation (`Float2`) is derived locally via explicit projection policy.
3. If both are needed for performance, dual storage is allowed only in internal/non-contract caches.

## 5) Commit Plan (Burndown by Type)

### Commit B1 — Prepare consumers for canonical 3D (`no contract removal yet`)

Goal:
1. Перевести consumers на использование canonical 3D полей/свойств (`WorldAnchor`, `WorldPosition`) как primary source.
2. Оставить compatibility planar fields временно, но перестать использовать их в package runtime logic там, где возможно.

Expected changes:
1. `RuntimeHost`:
   - migrate internal callers to use `WorldAnchor` and local projection.
2. `StrategyCombat` / `MorbooBridge`:
   - project `Float3 -> Float2` at 2D adapter boundary.
3. Add/adjust tests proving no new package public contracts with dual representation.

Allowlist after commit:
1. No shrink required yet (`4 -> 4`) if only consumer migration happened.

### Commit B2 — Remove dual form from `OrchestrationArbiterContext`

Type removed from allowlist:
1. `OrchestrationArbiterContext`

Action:
1. Remove planar compatibility field (`Anchor: Float2`) from `OrchestrationArbiterContext`.
2. Keep only canonical `WorldAnchor` (+ projection policy / plane available to downstream path if needed).
3. Move planar projection to local 2D consumer boundary.

Primary files (expected):
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs`
2. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs`
3. RuntimeHost/StrategyCombat consumers that used `ctx.Anchor`

Allowlist after commit:
1. `4 -> 3`

### Commit B3 — Remove dual form from `ExecutionContext`

Type removed from allowlist:
1. `ExecutionContext`

Action:
1. Remove planar compatibility field (`Anchor: Float2`) from `ExecutionContext`.
2. ExecutionRouter / downstream 2D policy adapters compute planar anchor from `WorldAnchor` locally.
3. Keep behavior unchanged via explicit projection adapter.

Primary files (expected):
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs`
2. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`
3. StrategyCombat adapters/policies receiving anchor inputs

Allowlist after commit:
1. `3 -> 2`

### Commit B4 — Remove dual form from `ActorReadProjection`

Type removed from allowlist:
1. `ActorReadProjection`

Action:
1. Remove planar compatibility field (`Position: Float2`) from public projection contract.
2. Keep canonical `WorldPosition: Float3` (or rename to canonical `Position` in same commit only if low risk and all consumers are updated).
3. 2D selectors/policies derive planar self position at consumption site.

Primary files (expected):
1. `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs`
2. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`
3. Any package consumers reading actor projection planar field

Allowlist after commit:
1. `2 -> 1`

### Commit B5 — Remove dual form from `WorldSnapshot`

Type removed from allowlist:
1. `WorldSnapshot`

Action:
1. Remove planar compatibility field (`Anchor: Float2`) from `WorldSnapshot`.
2. Keep only canonical 3D anchor field (`WorldAnchor` or normalized canonical name per active naming decision).
3. Update proposal/policy consumers to project locally where needed.

Primary files (expected):
1. `Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs`
2. `Packages/com.morboo.framework/Runtime/State/IWorldState.cs`
3. `Packages/com.morboo.framework/Runtime/Decision/IProposalSource.cs`
4. RuntimeHost/StrategyCombat proposal consumers

Allowlist after commit:
1. `1 -> 0`

### Commit B6 — Remove allowlist + tighten gate (`zero allowed`)

Goal:
1. Make the architecture test fail on any new dual-representation public contract by default.

Action:
1. Empty/remove `DualSpatialPublicContractAllowlist`.
2. Update docs status (`allowlist == 0`).
3. Add grep/fitness evidence to Phase 4 closure notes.

Primary files:
1. `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`
2. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Spatial_Dimensionality_3DFirst_2026-02-21.md`
3. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`

Allowlist after commit:
1. `0` (hard gate)

## 6) Risk Notes

1. `ExecutionContext` / `ArbiterContext` removal too early will cause churn if `RuntimeHost` still owns planar-aware domain policy wiring.
2. `ActorReadProjection` and `WorldSnapshot` are broader contracts; remove dual form only after consumer migration is complete.
3. Prefer behavior-preserving projection insertion before contract field deletion.

## 7) Validation / Acceptance

1. Architecture test `PublicSpatialContracts_DualFloat2AndFloat3Representations_AreAllowlistedOnly` is green after each commit.
2. Allowlist count monotonically decreases (`4 -> 3 -> 2 -> 1 -> 0`).
3. No gameplay behavior drift (compile + smoke playtest).
4. Planar (`2D`) logic remains localized in `StrategyCombat` / `MorbooBridge` boundaries.
