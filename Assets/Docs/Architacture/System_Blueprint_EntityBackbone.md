# System Blueprint: Entity Backbone

Date: 2026-02-20  
Template: `Assets/Docs/Architacture/New_System_Requirements_Template.md`  
Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/MasterMigration/03_Phase3_EntityBackbone/Phase3_EntityBackbone_Playtest_Checklist_2026-02-20.md`
3. `Assets/Docs/Architacture/MasterMigration/03_Phase3_EntityBackbone/Phase3_C3.3_Entity_SourceOfTruth_Hardening_Plan_2026-02-20.md`
4. `Assets/Docs/Architacture/System_Blueprint_Actor.md`

## 1) System Passport

1. `System Name`: Entity Backbone
2. `Owner`: Entity Backbone Owner
3. `Target Phase`: Phase 3 (C3.1-C3.3) + Phase 4 entry gate support
4. `Scope Type`: major refactor + extraction/modularization
5. `Behavior Impact`: controlled

## 2) Problem / Outcome

1. `Problem Statement`:
   - entity lifecycle/state historically split between scene MB classes and managers;
   - multiple runtime truth sources made orchestration integration fragile;
   - identity-driven contracts existed partially, but were not the default integration path.
2. `Business/Game Outcome`:
   - single source of truth for gameplay entity state;
   - stable registry/factory/lifecycle boundary;
   - orchestration and other systems operate via `EntityId` and contracts, not scene objects.
3. `In Scope`:
   - entity model contracts;
   - registry/factory/lifecycle/snapshot contracts and in-memory runtime implementation;
   - view binding contract seam;
   - bridge integration for current `Unit/Enemy` lifecycle paths.
4. `Out of Scope`:
   - full gameplay domain rewrite;
   - full TDE removal;
   - UI/Board behavior.

## 3) Architecture Archetype (Analogy)

1. `Selected Archetype`: Kernel Service + Runtime Platform Host + Integration Adapter
2. `Why this archetype`:
   - entity ownership is kernel-level;
   - runtime lifecycle services are infra concerns;
   - migration from legacy game classes requires adapter bridge.
3. `What differs from reference`:
   - rollout is incremental via bridge-first migration slices;
   - temporary compatibility remains only in `MorbooBridge`.

## 4) Layer & Package Placement

1. `Framework`:
   - universal identity primitives and base contracts.
2. `Core`:
   - entity contracts/models (`IEntityRegistry`, `IEntityFactory`, `IEntityLifecycleService`, `IEntitySnapshotStore`, `EntityState`).
3. `Systems`:
   - generic runtime implementations (`InMemory*` entity services).
4. `RuntimeHost`:
   - orchestration host seams consume entity read/write contracts, no project concrete types.
5. `Integration.StrategyCombat`:
   - strategycombat entity specializations and adapters.
6. `MorbooBridge`:
   - current game lifecycle mapping (`Unit/Enemy` managers -> entity facade).
7. `Game.Runtime`:
   - temporary behavior owners during migration (non-owner of entity truth).

## 5) Current Implementation Status

1. `C3.1`: closed (entity model + in-memory backbone + smoke tests).
2. `C3.2`: closed (manager bridge to registry facade + playtest checklist).
3. `C3.3`: closed (implementation + exit checklist/sign-off completed).

## 6) Communication Contract (No Direct Concrete Coupling)

1. `Inbound`:
   - lifecycle commands (create/destroy/register/unregister);
   - state mutation commands via owner contracts.
2. `Outbound`:
   - lifecycle events;
   - typed snapshots/read models by `EntityId`.
3. `Bridge points`:
   - `Assets/Scripts/MorbooBridge/EntityBackbone/*`.
4. `Forbidden`:
   - direct concrete MB-to-MB lifecycle coupling bypassing entity contracts;
   - package-layer reads/writes through project scene objects as truth.

## 7) State Ownership & Invariants

1. `Source of truth state`: entity model/snapshot contracts.
2. `State owner`: entity backbone services (registry/factory/lifecycle/snapshot store).
3. `Write paths`: command/service APIs only.
4. `Read paths`: query/snapshot APIs only.
5. `Critical invariants`:
   - no double-source state for migrated entity paths;
   - all migrated logic addresses entities by `EntityId`;
   - canonical lifecycle API is `EntityLifecycleState`/`SetLifecycleState`;
   - `IsAlive/SetAlive` are compatibility aliases only;
   - transitional compatibility forms remain Bridge-only.

## 8) Testing & Fitness Gates

1. `Architecture`:
   - no project refs in package entity layers;
   - no transition-only forms above `MorbooBridge`;
   - no forbidden dependency direction.
2. `Behavior`:
   - smoke for create/destroy/get/events;
   - bridge parity checks for current unit/enemy lifecycle behavior.
3. `Integration`:
   - orchestration reads state via entity contracts/snapshots.

## 9) Post-Closure Hardening (Non-Blocking)

1. Expand typed state model beyond current migrated scope (reduce trait-string usage further).
2. Grow active architecture gates as additional migrated paths move to Entity Backbone.

## 10) Definition Of Done

1. Entity lifecycle ownership is centralized and explicit.
2. Orchestration/gameplay systems consume entity contracts without scene-object truth leakage.
3. Bridge-only compatibility is enforced.
4. Roadmap/status docs and tests are aligned with actual implementation.
