# System Interaction Contract: Actor <-> Orchestrator

Date: 2026-02-20  
Status: Draft (pre-refactor freeze target)

## 1) Passport

1. `System name`: Actor <-> Orchestrator Boundary
2. `Owner`: Gameplay Domains Owner + Orchestration Platform Owner
3. `Layer/package`: `Core` + `RuntimeHost` + `Integration.StrategyCombat` + `MorbooBridge`
4. `Related blueprint`:
   - `Assets/Docs/Architacture/System_Blueprint_Actor.md`
   - `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`

## 2) State Ownership

1. `Source-of-truth state`:
   - Actor state is owned by Actor system stores/model (`Core` contracts + runtime impl).
   - Orchestrator state is owned by orchestration host tick snapshot.
2. `Who can write`:
   - Actor writes to actor state.
   - Orchestrator writes only by dispatching commands to actor receivers.
3. `Who can read`:
   - Orchestrator reads actor snapshot/query projection only.
   - Actor may read orchestration outputs only via command dispatch.
4. `Forbidden write paths`:
   - Arbiter/Router direct mutation of actor stores.
   - Direct concrete calls into `Unit/Enemy` from host/runtime layers.
5. `Compatibility rule`:
   - `IsAlive/SetAlive` are compatibility aliases only and must not be introduced as new package boundary APIs.

## 3) Inbound Surface (Orchestrator consumes from Actor)

1. `State snapshot` -> `IStateReporter.ReportState()` -> Actor adapters -> must be query-only.
2. `Capabilities` -> `ICapabilityProvider.ReportCapabilities()` -> Actor adapters -> no side effects.
3. `Identity` -> `IEntityIdProvider.GetEntityId()` -> Actor identity provider -> stable id required.
4. `Faction` -> `IFactionAssetProvider.GetFactionAsset()` -> Actor identity provider.
5. `Role` -> `IRoleAssetProvider.GetRoleAsset()` (transition) / `RoleId` projection (target) -> Actor role provider.
6. `World data` -> `IWorldQuery` snapshot projection -> RuntimeHost -> no concrete downcast in domains (`3D-first` seam; `2D` specializations only via explicit projection adapters).
7. `Lifecycle` -> `EntityLifecycleState` projection from entity/actor contracts (state-first).

## 4) Outbound Surface (Orchestrator emits to Actor)

1. `Combat dispatch` -> `DispatchCombatCommand` -> adapter -> `ICombatCommandReceiver.ApplyCombatCommand(...)`.
2. `Idle dispatch` -> `DispatchIdleCommand` -> adapter -> `IIdleCommandReceiver.ApplyIdleCommand(...)`.
3. `Lifecycle expectations` -> `IEntityLifecycleService` events consumed by bridge/actor integration when needed.

## 5) Allowed Integration Channels

1. Command bus dispatch (`InProcessCommandBus` / future bus abstraction).
2. Read-only query projection (`IWorldQuery` + actor snapshot providers).
3. Typed identity/faction/role/capability providers.
4. `MorbooBridge` mapping adapters for legacy game classes.

## 6) Forbidden Coupling

1. RuntimeHost direct references to `Game.Runtime`/`Morboo.Bridge`.
2. Orchestrator domain code calling `Unit`/`Enemy` concrete methods directly.
3. Actor system reading orchestration internals outside command/query contracts.
4. Untyped dependency holders as service locator refs in orchestration wiring.

## 7) Dependency Rules

1. Allowed assembly references:
   - `RuntimeHost -> Framework/Core/Systems`.
   - `Integration.StrategyCombat -> Framework/Core/RuntimeHost/Systems`.
   - `MorbooBridge -> Game.Runtime + packages above`.
2. Forbidden assembly references:
   - `RuntimeHost/Core/Framework -> MorbooBridge/Game.Runtime`.
3. Bridge points:
   - `Assets/Scripts/MorbooBridge/EntityBackbone/*`
   - future `Assets/Scripts/MorbooBridge/Actor/Adapters/*`.

## 8) Failure & Recovery

1. Expected failure modes:
   - missing identity/faction/role mapping;
   - receiver not resolvable by EntityId;
   - stale trait key usage across layers.
2. Fallback behavior:
   - fail closed (`Hold`) when role/policy mapping missing;
   - skip receiver when id invalid;
   - warn once and continue tick.
3. Rollback-safe checkpoint:
   - keep legacy `Unit/Enemy` behavior behind bridge adapters while contract freeze is validated.

## 9) Tests/Fitness Gates

1. Architecture tests:
   - no project refs in package layers;
   - no domain downcast from `IWorldQuery` to concrete cache in domains;
   - no direct Apply calls from router.
2. Behavior regression tests:
   - unit/enemy command dispatch still applies expected movement/target behavior.
3. Data-driven/file-sprawl gates:
   - taxonomy keys layered correctly (generic vs strategy vs project);
   - onboarding a new actor subtype does not require host-runtime code edits.

## 10) Final Contract Freeze (v1)

This v1 boundary is considered frozen for orchestrator refactor window:

1. Read contracts:
   - `IStateReporter`, `ICapabilityProvider`, `IEntityIdProvider`, `IFactionAssetProvider`, role projection interface.
2. Write contracts:
   - `ICombatCommandReceiver`, `IIdleCommandReceiver`, dispatch messages.
3. State policy:
   - query-read + command-write only.
4. Integration policy:
   - legacy class adaptation only in `MorbooBridge`.
