# C06 — Capabilities Integration Into Decision/Execution

Date: 2026-02-24
Phase: `Phase 4` (`Orchestration Platform Remediation`)
Type: behavior-affecting (controlled)
Status: `open`

---

## Why This Step Exists

Capability infrastructure was built fully during C04/C04D: `ICapabilityProvider`, `CapabilitySnapshot`, `CapabilitiesProfile`, `CommonCapabilities`, `UnitCapabilityProvider`, `EnemyCapabilityProvider`, and `OrchestrationRegistry.CapabilityProviders`. Every structural piece is present. Providers register at runtime via `OnEnable`. However:

1. `OrchestrationWorldCache.BuildWorldCache` never iterates `OrchestrationRegistry.CapabilityProviders` — no per-actor capability data is snapshotted.
2. `IWorldQuery` and `OrchestrationArbiterContext` have no capability query surface — domains cannot ask "does this actor have Combat.Ranged?" through the frozen world snapshot.
3. No consumer exists: `CombatDomainComponent.FindClosestHostileIndex` is capability-blind — it filters by hostility and spatial distance only. A flying enemy is selected as a target even if no friendly unit can engage it.
4. No cross-capability matching — there is no mechanism to express "target with capability X requires source with capability Y."
5. No unified policy capability gating — there is no way for an `IdlePolicyAsset` or `CombatTargetingPolicyAsset` to declare "this policy requires Walk capability" and have the execution route enforce it uniformly across domains.
6. The capability type system uses string-based `CapabilityId` + untyped `ParamSet` bags — error-prone, no reference-equality safety, and `ParamSet` is unused.
7. `ICapabilityProvider` is "green" at the registry level but has no regression surface — any deletion or breakage would go undetected.

Additionally, the current system conflates "capability" (a generic concept) with "actor capability" (the orchestration-specific scope). Renaming to `ActorCapability` prevents confusion with potential future capability systems in other subsystems.

---

## Goal

1. Replace string-based `CapabilityId`/`CapabilityEntry`/`ParamSet` with typed `ActorCapability : ScriptableObject` — identity by reference (like `FactionAsset`), no strings, no typos.
2. Rename all `Capability*` → `ActorCapability*` across codebase — scope clarity.
3. Simplify `ActorCapabilityProfile` to `List<ActorCapability>` — remove unused `ParamSet` (capabilities are boolean presence only).
4. Add `IActorCapabilityQuery` to `RuntimeHost` — domain-agnostic, per-actor only, two methods: `GetActorCapabilities(int index)` and `TryGetActorCapabilities(EntityId, out)`.
5. Implement `IActorCapabilityQuery` in `OrchestrationWorldCache`, wire into `OrchestrationArbiterContext`.
6. Add `ActorCapabilityEngagementPolicy : ScriptableObject` — data-driven cross-capability matching (`sourceCaps` vs `targetCaps`).
7. `CombatDomainComponent`: consumer iterates friendly actors to build aggregate caps, filters hostile targets via engagement policy → `CombatTargetSet` contains only engageable targets.
8. Unified policy capability gating: shared `IActorCapabilityGatedPolicy` interface on ALL domain policy assets; `ActorCapabilityPolicyGate` static helper; execution routes check required capabilities uniformly before applying any policy.
9. Regression tests proving capabilities influence combat target selection and idle behavior.

---

## Non-Goals (For C06)

1. **Universal command system** (`Engage`/`Cancel`/`None` replacing `CombatCommand` + `IdleCommand`) — deferred to `C06A`. `CombatCommand` and `IdleCommand` stay as-is in C06.
2. **Universal target search** (shared search mechanism extracted from `CombatDomainComponent`, usable by all domains) — deferred to `C06A`.
3. **Receiver interface unification** (collapsing `ICombatCommandReceiver` + `IIdleCommandReceiver` into one) — deferred to `C06A`.
4. **Command adapter unification** — deferred to `C06A`.
5. **ExecutionRoute convergence** — deferred to `C06A`.
6. **Generic `OrchestrationTarget` abstraction** (position/actor/building as unified target entity) — future step.
7. **AI brain integration** (how actor decides specific behavior after assignment) — existing TDE/AIBrain path unchanged.
8. **Formation system** — deferred.
9. **Objective-driven policy switching** — deferred (no Objective system yet).
10. **`EntityState._capabilities` ↔ `ActorCapabilitySnapshot` bridge** — separate concern, different layers.
11. **Per-unit capability gating in combat execution route** — domain-level aggregate filtering is sufficient for C06.
12. **Parameterized capability reads** (`TryGetFloat`, `TryGetBool`) — capabilities are boolean presence only; `ParamSet` is removed.

---

## Hard Rules (For C06)

1. **`ActorCapability` is `ScriptableObject`** — identity by reference equality, no string matching. Same pattern as `FactionAsset`.
2. **`IActorCapabilityQuery` in RuntimeHost** — alongside capability types. Domain-agnostic.
3. **`IActorCapabilityQuery` has ONLY per-actor methods**: `GetActorCapabilities(int)`, `TryGetActorCapabilities(EntityId, out)`. No receiver-specific methods. No domain-specific methods. No aggregate methods. Consumer computes aggregate by iterating actors with needed faction filter from `IWorldQuery`.
4. **Per-tick full rebuild, frozen before domain polling** — consistent with `OrchestrationWorldCache` pattern for all other actor data.
5. **Provider → actor correlation via `GetComponent<IActorCapabilityProvider>()` on actor Transform** — actor-first scan during `SnapshotActorCapabilities`. One `GetComponent` per actor per tick. Registry's `CapabilityProviders` list is NOT iterated for snapshot.
6. **PERF: no per-tick allocations beyond warmup, no LINQ** — `_actorCapabilitySnapshots` is a pre-allocated `List<ActorCapabilitySnapshot>(256)`. `ActorCapabilitySnapshot` is a struct. `ActorCapability` references are shared (not copied). No boxing.
7. **Cross-capability matching is data-driven** (`ActorCapabilityEngagementPolicy` ScriptableObject), not code branches.
8. **`IWorldQuery` NOT modified** — `IActorCapabilityQuery` is a separate interface. `OrchestrationArbiterContext` gets a dedicated `ActorCapabilities` field.
9. **Existing behavior preserved when no policy/filter configured** — null `engagementPolicy` = passthrough; null/empty `requiredCapabilities` = no gating.
10. **No `ExecutionContext` binding slot consumed** — capability data flows through `OrchestrationArbiterContext`, not `ExecutionContext` binding store.
11. **Capability gating on policies is UNIFIED across domains** — same `IActorCapabilityGatedPolicy` interface, same `ActorCapabilityPolicyGate` static helper, same check pattern in all execution routes.
12. **`CombatCommand`/`IdleCommand` stay unchanged** — universal commands are `C06A` scope.
13. **Engagement policy uses neutral naming**: `sourceCaps` (not `attackerCaps`) and `targetCaps` (not `defenderCaps`) — orchestrator may assign support/healing, not just attack.

---

## Prerequisite State (Before C06)

1. `C05` closed — `IEventBusProvider`, `ICommandBusProvider`, `EventBusSubscriber` all in place.
2. `OrchestrationWorldCache` implements `IWorldQuery`, `IWorldQuery3D`, `IActorReadProjectionQuery` — pattern for extending with new interfaces is established.
3. `UnitCapabilityProvider` and `EnemyCapabilityProvider` register to `OrchestrationRegistry.CapabilityProviders` on `OnEnable`.
4. `CapabilitySnapshot`, `CapabilitySet`, `CapabilityEntry`, `CapabilityId`, `CommonCapabilities` — all exist in `com.morboo.runtimehost/Runtime/Orchestration/Capabilities/`.
5. `CombatDomainComponent` reads from `IWorldQuery` only — no direct registry access.
6. `CombatTargetingPolicyAsset` and `IdlePolicyAsset` are abstract `ScriptableObject` base classes in `RuntimeHost` — can implement new interfaces.
7. `StrategyCombatCombatExecutionRoute` and `StrategyCombatIdleExecutionRoute` consume policy assets — can add capability gate checks.

---

## Type System Reform

### New Types (in `Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/`)

**`ActorCapability : ScriptableObject`** — pure identity token.

```csharp
/// <summary>
/// Typed actor capability identity. Comparison by reference equality.
/// IMPORTANT: Same pattern as FactionAsset — no string matching, no typos.
/// </summary>
[CreateAssetMenu(fileName = "ActorCapability", menuName = "Game/Orchestration/Actor Capability")]
public sealed class ActorCapability : ScriptableObject { }
```

**`ActorCapabilitySnapshot`** struct — two-layer capability set.

```csharp
/// <summary>
/// Per-actor capability snapshot. Base = from profile; Add = runtime additions.
/// IMPORTANT: Has() checks Add first, then Base (reference equality).
/// PERF: Linear scan — intentional for small N (typical 3–8 capabilities per actor).
/// </summary>
[Serializable]
public struct ActorCapabilitySnapshot
{
    public List<ActorCapability> Base;
    public List<ActorCapability> Add;

    public bool Has(ActorCapability cap)
    {
        if (cap == null) return false;
        if (Add != null) { for (int i = 0; i < Add.Count; i++) if (ReferenceEquals(Add[i], cap)) return true; }
        if (Base != null) { for (int i = 0; i < Base.Count; i++) if (ReferenceEquals(Base[i], cap)) return true; }
        return false;
    }

    public bool HasAny(IReadOnlyList<ActorCapability> caps)
    {
        if (caps == null || caps.Count == 0) return true;
        for (int i = 0; i < caps.Count; i++)
            if (Has(caps[i])) return true;
        return false;
    }

    /// <summary>
    /// Unions another snapshot into this one (adds all caps from other.Base and other.Add to this.Add).
    /// IMPORTANT: Used by consumers to build aggregate capability sets.
    /// PERF: Allocates on first call (Add list creation). Call once per tick, not per actor.
    /// </summary>
    public void MergeFrom(in ActorCapabilitySnapshot other) { /* union into Add */ }
}
```

**`ActorCapabilityProfile : ScriptableObject`** — replaces `CapabilitiesProfile`.

```csharp
[CreateAssetMenu(fileName = "ActorCapabilityProfile", menuName = "Game/Orchestration/Actor Capability Profile")]
public sealed class ActorCapabilityProfile : ScriptableObject
{
    [SerializeField] List<ActorCapability> capabilities;

    /// <summary>
    /// IMPORTANT: Base list is shared by reference, not copied. Do not mutate via snapshot.
    /// </summary>
    public ActorCapabilitySnapshot ToSnapshot()
    {
        return new ActorCapabilitySnapshot { Base = capabilities };
    }
}
```

**`IActorCapabilityProvider`** interface — replaces `ICapabilityProvider`.

```csharp
public interface IActorCapabilityProvider
{
    ActorCapabilitySnapshot ReportCapabilities();
}
```

### Removed Types

| File | Reason |
|------|--------|
| `CapabilityId.cs` | Replaced by `ActorCapability` SO reference |
| `CapabilityEntry.cs` | Replaced by `ActorCapability` SO reference (no ParamSet) |
| `CapabilitySet.cs` | Replaced by `List<ActorCapability>` |
| `CapabilitySnapshot.cs` | Replaced by `ActorCapabilitySnapshot` |
| `CommonCapabilities.cs` | Replaced by actual SO assets |
| `CapabilitiesProfile.cs` | Replaced by `ActorCapabilityProfile` |
| `CapabilityContracts.cs` (`ICapabilityProvider`) | Replaced by `IActorCapabilityProvider` |

### Renamed + Adapted

| Old Name | New Name | Package |
|----------|----------|---------|
| `UnitCapabilityProvider` | `UnitActorCapabilityProvider` | MorbooBridge |
| `EnemyCapabilityProvider` | `EnemyActorCapabilityProvider` | MorbooBridge |
| `RoleCapabilitiesMapAssetBase` | `RoleActorCapabilitiesMapAssetBase` | StrategyCombat |
| `EnemyCapabilitiesMapAssetBase` | `EnemyActorCapabilitiesMapAssetBase` | MorbooBridge |
| `RoleCapabilitiesMapAsset` | `RoleActorCapabilitiesMapAsset` | MorbooBridge |
| `EnemyTypeCapabilitiesMap` | `EnemyTypeActorCapabilitiesMap` | MorbooBridge |
| Folder `Capabilities/` | `ActorCapabilities/` | RuntimeHost |

### Content Assets To Create

Common `ActorCapability` SO assets (project data folder):

| Asset | Description |
|-------|-------------|
| `Walk` | Ground movement |
| `Run` | Fast ground movement |
| `Fly` | Aerial movement |
| `Swim` | Water movement |
| `Melee` | Close-range interaction |
| `Ranged` | Long-range interaction |
| `Block` | Defensive blocking |
| `Dodge` | Evasive movement |
| `StoneSkin` | Damage resistance |
| `Sow` | Plant crops (economy) |
| `Harvest` | Gather resources (economy) |
| `Herd` | Manage livestock (economy) |

Default `ActorCapabilityEngagementPolicy` asset with baseline rules.

---

## IActorCapabilityQuery Contract

```csharp
// Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/IActorCapabilityQuery.cs

/// <summary>
/// Read-only capability query over a frozen world snapshot.
/// IMPORTANT: Domain-agnostic — no receiver-specific or domain-specific methods.
/// Consumer gets EntityId from IWorldQuery and queries capabilities here.
/// Aggregate capability sets are computed by consumers, not by this interface.
/// </summary>
public interface IActorCapabilityQuery
{
    /// <summary>
    /// Capability snapshot for actor at the given index (parallel to IWorldQueryBase actor list).
    /// Returns default(ActorCapabilitySnapshot) if no provider on the actor.
    /// IMPORTANT: Index in [0, ActorCount). Same indexing as IWorldQueryBase.
    /// </summary>
    ActorCapabilitySnapshot GetActorCapabilities(int index);

    /// <summary>
    /// Capability snapshot by EntityId. O(1) via internal index map.
    /// Returns false if entity is not in the current snapshot.
    /// </summary>
    bool TryGetActorCapabilities(EntityId entityId, out ActorCapabilitySnapshot snapshot);
}
```

---

## ActorCapabilityEngagementPolicy Design

```csharp
// Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/ActorCapabilityEngagementPolicy.cs

/// <summary>
/// Data-driven cross-capability matching rules.
/// Determines whether a source (pipeline's actors collectively) can engage a target.
/// IMPORTANT: "Source" and "Target" are neutral terms — orchestrator may assign
/// attack, support, healing, or any other interaction.
/// RATIONALE: Rules are target-centric: "if target has capability X, source must have ≥1 of [A, B, C]."
/// If no rule matches the target's capabilities, engagement is allowed (default: engageable).
/// </summary>
[CreateAssetMenu(fileName = "EngagementPolicy", menuName = "Game/Orchestration/Actor Capability Engagement Policy")]
public sealed class ActorCapabilityEngagementPolicy : ScriptableObject
{
    [SerializeField] List<EngagementRule> rules;

    [Serializable]
    public struct EngagementRule
    {
        [Tooltip("If the target has this capability...")]
        public ActorCapability targetCapability;

        [Tooltip("...the source side must have at least one of these.")]
        public List<ActorCapability> sourceRequiresAny;
    }

    /// <summary>
    /// Returns true if sourceCaps can engage targetCaps.
    /// For each rule: if target has the rule's capability and source has none of the required → false.
    /// PERF: Linear scan per rule. Rules count is small (typically 2–5).
    /// </summary>
    public bool CanEngage(in ActorCapabilitySnapshot sourceCaps, in ActorCapabilitySnapshot targetCaps)
    {
        if (rules == null) return true;
        for (int i = 0; i < rules.Count; i++)
        {
            EngagementRule rule = rules[i];
            if (rule.targetCapability == null) continue;
            if (!targetCaps.Has(rule.targetCapability)) continue;

            // Target has the guarded capability — check source
            if (!sourceCaps.HasAny(rule.sourceRequiresAny))
                return false;
        }
        return true;
    }
}
```

Example rules:

| Target Has | Source Requires Any |
|------------|---------------------|
| `Fly` | `Ranged`, `Fly` |
| `StoneSkin` | `Melee` |

---

## Unified Policy Capability Gating

```csharp
// Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/IActorCapabilityGatedPolicy.cs

/// <summary>
/// Shared interface for any policy asset that requires specific actor capabilities.
/// IMPORTANT: Implemented by ALL domain policy base classes (CombatTargetingPolicyAsset,
/// IdlePolicyAsset, and any future domain policy). Same pattern, same check logic.
/// No domain-specific divergence.
/// </summary>
public interface IActorCapabilityGatedPolicy
{
    IReadOnlyList<ActorCapability> RequiredCapabilities { get; }
}

/// <summary>
/// Shared capability gate check. Used by all execution routes uniformly.
/// IMPORTANT: NOT in a base class — avoids inheritance coupling.
/// All-of semantics: actor must have ALL listed capabilities.
/// </summary>
public static class ActorCapabilityPolicyGate
{
    public static bool CanApply(IActorCapabilityGatedPolicy policy, in ActorCapabilitySnapshot actorCaps)
    {
        var req = policy.RequiredCapabilities;
        if (req == null || req.Count == 0) return true; // no requirement = always applies
        for (int i = 0; i < req.Count; i++)
            if (!actorCaps.Has(req[i])) return false;
        return true; // all-of: actor has every required capability
    }
}
```

Implemented by:
- `CombatTargetingPolicyAsset` — add `[SerializeField] List<ActorCapability> requiredCapabilities`
- `IdlePolicyAsset` — add `[SerializeField] List<ActorCapability> requiredCapabilities`
- Future domain policies — same interface

In execution routes (UNIFIED pattern, same code in both combat and idle routes):

```csharp
if (policy is IActorCapabilityGatedPolicy gated && capabilityQuery != null)
{
    capabilityQuery.TryGetActorCapabilities(receiverEntityId, out var actorCaps);
    if (!ActorCapabilityPolicyGate.CanApply(gated, actorCaps))
    {
        cmd = FallbackHoldCommand; // Hold
        // skip applying this policy, use fallback
    }
}
```

---

## Pipeline-Level Aggregate Capability Pattern

`CombatDomainComponent.EvaluateDomain` builds a friendly aggregate on demand (consumer responsibility):

```csharp
// 1. Build aggregate of friendly capabilities (consumer-side, not on IActorCapabilityQuery)
ActorCapabilitySnapshot friendlyAggregate = default;
if (engagementPolicy != null && ctx.ActorCapabilities != null)
{
    IWorldQuery world = ctx.World;
    for (int i = 0; i < world.ActorCount; i++)
    {
        if (world.GetActorIsHostile(i)) continue;
        if (!world.GetActorIsAlive(i)) continue;
        var caps = ctx.ActorCapabilities.GetActorCapabilities(i);
        friendlyAggregate.MergeFrom(caps); // union of all friendly capabilities
    }
}

// 2. Filter hostile candidates using engagement policy
for (int i = 0; i < world.ActorCount; i++)
{
    if (!world.GetActorIsHostile(i)) continue;
    // ... existing spatial filters (radius / screen viewport) ...

    // C06: Engagement policy filter (opt-in, null = passthrough)
    if (engagementPolicy != null && ctx.ActorCapabilities != null)
    {
        var targetCaps = ctx.ActorCapabilities.GetActorCapabilities(i);
        if (!engagementPolicy.CanEngage(friendlyAggregate, targetCaps))
            continue; // no friendly actor can engage this target
    }

    // ... distance scoring, TopK insertion ...
}
```

RATIONALE: Aggregate is computed by the consumer because different consumers may need different filters (friendly vs neutral vs all non-hostile). No aggregate method on `IActorCapabilityQuery` — keeps the interface clean and domain-agnostic.

---

## BuildWorldCache Sequence (C06 Target)

```text
OrchestrationArbiter.BuildWorldCache(ctx)
  │
  ├─ _world.Clear()
  ├─ Set anchors / ProjectionPlane / Now
  │
  ├─ Scan StateReporters → populate _world.Actors
  ├─ Scan CombatReceivers → FriendlyCombatReceivers
  ├─ Scan IdleReceivers → FriendlyIdleReceivers
  ├─ Build crowd transforms
  ├─ Build RoleByTransform
  ├─ Resolve IdleBounds
  │
  ├─ _world.SnapshotActors(faction, relations)       ← positions, EntityIds, lifecycle, hostile
  ├─ [NEW] _world.SnapshotActorCapabilities()         ← per-actor ActorCapabilitySnapshot
  │         For each actor in Actors[0..N]:
  │           t = actor.GetTransform()
  │           provider = t.GetComponent<IActorCapabilityProvider>()
  │           _actorCapabilitySnapshots[i] = provider != null
  │               ? provider.ReportCapabilities()
  │               : default(ActorCapabilitySnapshot)
  ├─ _world.SnapshotCrowd()
  ├─ _world.BuildRoleByEntityId()
  ├─ _world.SnapshotReceivers()
  └─ _world.Freeze()

After BuildWorldCache:
  _ctx.ActorCapabilities = _world    ← _world implements IActorCapabilityQuery
```

IMPORTANT: `SnapshotActorCapabilities()` must be called after `SnapshotActors()` (requires `Actors` list and `_actorIndexByEntityId` map to be populated).

---

## Final Layer Mapping (C06 Target)

### RuntimeHost (`com.morboo.runtimehost`) — new

| File | Type |
|------|------|
| `Runtime/Orchestration/ActorCapabilities/ActorCapability.cs` | `ScriptableObject` identity |
| `Runtime/Orchestration/ActorCapabilities/ActorCapabilitySnapshot.cs` | struct |
| `Runtime/Orchestration/ActorCapabilities/ActorCapabilityProfile.cs` | `ScriptableObject` profile |
| `Runtime/Orchestration/ActorCapabilities/IActorCapabilityProvider.cs` | interface |
| `Runtime/Orchestration/ActorCapabilities/IActorCapabilityQuery.cs` | interface |
| `Runtime/Orchestration/ActorCapabilities/ActorCapabilityEngagementPolicy.cs` | `ScriptableObject` policy |
| `Runtime/Orchestration/ActorCapabilities/IActorCapabilityGatedPolicy.cs` | interface + static helper |

### RuntimeHost — updated

| File | Change |
|------|--------|
| `Arbitration/OrchestrationWorldCache.cs` | Implement `IActorCapabilityQuery`, add parallel list, `SnapshotActorCapabilities()` |
| `Arbitration/OrchestrationArbiter.cs` | Wire `SnapshotActorCapabilities()` into `BuildWorldCache`, populate `ctx.ActorCapabilities` |
| `Arbitration/OrchestrationArbiterContext.cs` | Add `IActorCapabilityQuery ActorCapabilities` field |
| `World/OrchestrationRegistry.cs` | Update `ICapabilityProvider` → `IActorCapabilityProvider` type references |
| `Domains/Combat/Targeting/CombatTargetingPolicyAsset.cs` | Implement `IActorCapabilityGatedPolicy` |
| `Domains/Idle/IdlePolicyAsset.cs` | Implement `IActorCapabilityGatedPolicy` |

### StrategyCombat (`com.morboo.integration.strategycombat`) — updated

| File | Change |
|------|--------|
| `Domains/Combat/CombatDomainComponent.cs` | Add engagement policy, aggregate caps, capability filter |
| `Execution/StrategyCombatIdleExecutionRoute.cs` | Capability gate before policy application |
| `Execution/StrategyCombatCombatExecutionRoute.cs` | Capability gate before policy application |
| `Maps/RoleCapabilitiesMapAssetBase.cs` | Rename + adapt to `ActorCapabilityProfile` |

### MorbooBridge — renamed + adapted

| File | Change |
|------|--------|
| `Orchestration/Units/UnitCapabilityProvider.cs` | Rename → `UnitActorCapabilityProvider`, adapt to new types |
| `Orchestration/Enemies/EnemyCapabilityProvider.cs` | Rename → `EnemyActorCapabilityProvider`, adapt |
| `Maps/RoleCapabilitiesMapAsset.cs` | Rename + adapt |
| `Maps/EnemyCapabilitiesMapAssetBase.cs` | Rename + adapt |
| `Maps/EnemyTypeCapabilitiesMap.cs` | Rename + adapt |

### Architecture Tests — updated

| File | Change |
|------|--------|
| `OrchestrationImplementationFitnessTests.cs` | Architecture gates for capability integration |

### RuntimeHost Tests — new/updated

| File | Change |
|------|--------|
| `RuntimeHostTests.cs` (or new `ActorCapabilityIntegrationTests.cs`) | Regression tests |

---

## Migration Plan (Execution Slices)

### S0 — Type System Reform (Rename + Simplify)

Create new types in `Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/`:
1. `ActorCapability.cs` — ScriptableObject identity token.
2. `ActorCapabilitySnapshot.cs` — struct with `Has()`, `HasAny()`, `MergeFrom()`.
3. `ActorCapabilityProfile.cs` — ScriptableObject wrapping `List<ActorCapability>`.
4. `IActorCapabilityProvider.cs` — interface replacing `ICapabilityProvider`.

Remove old types from `Packages/com.morboo.runtimehost/Runtime/Orchestration/Capabilities/`:
5. `CapabilityId.cs`, `CapabilityEntry.cs`, `CapabilitySet.cs`, `CapabilitySnapshot.cs`.
6. `CommonCapabilities.cs`.
7. `CapabilitiesProfile.cs`, `CapabilityContracts.cs`.

Rename and adapt:
8. `UnitCapabilityProvider` → `UnitActorCapabilityProvider` — adapt to `IActorCapabilityProvider`, use `ActorCapabilityProfile`.
9. `EnemyCapabilityProvider` → `EnemyActorCapabilityProvider` — same.
10. `RoleCapabilitiesMapAssetBase` → `RoleActorCapabilitiesMapAssetBase` — maps `RoleAsset/RoleId` → `ActorCapabilityProfile`.
11. `EnemyCapabilitiesMapAssetBase` → `EnemyActorCapabilitiesMapAssetBase`.
12. Concrete maps in MorbooBridge — rename + adapt.
13. `OrchestrationRegistry` — update `ICapabilityProvider` → `IActorCapabilityProvider`.

Create content assets:
14. Common `ActorCapability` SO assets in project data folder.

Acceptance:
1. Compiles in Unity.
2. `OrchestrationRegistry.CapabilityProviders` returns `IReadOnlyList<IActorCapabilityProvider>`.
3. `UnitActorCapabilityProvider.ReportCapabilities()` returns `ActorCapabilitySnapshot` with `ActorCapability` SO references.
4. Old string-based types are deleted.

### S1 — IActorCapabilityQuery + WorldCache Integration

Create:
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/IActorCapabilityQuery.cs` — two methods only.

Modify:
2. `OrchestrationWorldCache.cs`:
   - Add `readonly List<ActorCapabilitySnapshot> _actorCapabilitySnapshots = new List<ActorCapabilitySnapshot>(256);`.
   - Add `SnapshotActorCapabilities()` method — iterates `Actors`, calls `GetComponent<IActorCapabilityProvider>()`, fills parallel list.
   - Implement `IActorCapabilityQuery`: `GetActorCapabilities(int)` → `_actorCapabilitySnapshots[index]`; `TryGetActorCapabilities(EntityId)` → lookup via `_actorIndexByEntityId`.
   - Update `Clear()` to clear `_actorCapabilitySnapshots`.
   - Class declaration becomes: `... : IWorldQuery, IWorldQuery3D, IActorReadProjectionQuery, IActorCapabilityQuery`.
3. `OrchestrationArbiterContext.cs` — add `public IActorCapabilityQuery ActorCapabilities;`.
4. `OrchestrationArbiter.cs` — in `BuildWorldCache`, after `_world.SnapshotActors(...)`, call `_world.SnapshotActorCapabilities()`. After `BuildWorldCache`, set `_ctx.ActorCapabilities = _world`.

Acceptance:
1. `OrchestrationWorldCache` implements `IActorCapabilityQuery`.
2. `_actorCapabilitySnapshots.Count == Actors.Count` after snapshot.
3. `GetActorCapabilities(i)` returns `default` for actors without provider.
4. `ctx.ActorCapabilities` is non-null during domain polling.

### S2 — ActorCapabilityEngagementPolicy

Create:
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/ActorCapabilityEngagementPolicy.cs` — ScriptableObject with `EngagementRule` list and `CanEngage(sourceCaps, targetCaps)`.

Create content:
2. Default engagement policy asset with baseline rules:
   - `Fly` → requires `Ranged` or `Fly`
   - (Additional rules as needed)

Acceptance:
1. `CanEngage(snapWithRanged, snapWithFly)` returns `true`.
2. `CanEngage(snapWithMeleeOnly, snapWithFly)` returns `false`.
3. `CanEngage(anyCaps, snapWithoutRules)` returns `true` (no blocking rules).

### S3 — Combat Target Selection (Domain-Level Aggregate)

Modify:
1. `CombatDomainComponent.cs`:
   - Add `[Header("Engagement Policy")] [SerializeField] ActorCapabilityEngagementPolicy engagementPolicy`.
   - In `EvaluateDomain()`: before `FindClosestHostileIndex`, build friendly aggregate by iterating non-hostile alive actors.
   - In `FindClosestHostileIndex()`: after hostile check, add engagement policy gate (when `engagementPolicy != null && ctx.ActorCapabilities != null`).
   - In `FillTargetSet()`: same gate after hostile + alive checks.
   - Null `engagementPolicy` = passthrough (existing behavior preserved).
   - Pass `ctx` to `FindClosestHostileIndex` and `FillTargetSet` (already receiving it).
   - Cache friendly aggregate in a field (rebuilt each `EvaluateDomain` call, not per candidate).

Acceptance:
1. With `engagementPolicy` set and `Fly→Ranged|Fly` rule: flying enemy excluded from primary target and TopK when no friendly has `Ranged` or `Fly`.
2. With `engagementPolicy == null`: behavior identical to pre-C06.
3. No LINQ, no per-tick allocations in filter hot path (aggregate built once per EvaluateDomain call).

### S4 — Unified Policy Capability Gating

Create:
1. `Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/IActorCapabilityGatedPolicy.cs` — interface + `ActorCapabilityPolicyGate` static helper.

Modify:
2. `CombatTargetingPolicyAsset.cs` — implement `IActorCapabilityGatedPolicy`, add `[SerializeField] List<ActorCapability> requiredCapabilities`.
3. `IdlePolicyAsset.cs` — implement `IActorCapabilityGatedPolicy`, add `[SerializeField] List<ActorCapability> requiredCapabilities`.
4. `StrategyCombatIdleExecutionRoute.cs` — in `EmitIdlePerUnit`, before calling `policy.ChooseCommand(...)`: if `policy is IActorCapabilityGatedPolicy gated`, query receiver capabilities, check `ActorCapabilityPolicyGate.CanApply(gated, actorCaps)`, fallback to `IdleCommand.Hold()` if unmet.
5. `StrategyCombatCombatExecutionRoute.cs` or `CombatCommandAdapter.cs` — same pattern for combat targeting policy: if the targeting policy has required capabilities and the receiver lacks them, skip custom target selection (use primary target or Hold as fallback).

Acceptance:
1. `IdlePolicyAsset` with `requiredCapabilities = [Walk]`: actor without `Walk` → `IdleCommand.Hold()` regardless of policy.
2. `IdlePolicyAsset` with empty `requiredCapabilities`: normal policy behavior.
3. Same pattern works for `CombatTargetingPolicyAsset`.
4. Pattern is identical in both routes — no domain-specific divergence.

### S5 — Regression + Architecture Tests

Add to `Packages/com.morboo.runtimehost/Tests/`:

| # | Test | Type |
|---|------|------|
| 1 | `ActorCapabilitySnapshot_Has_ReturnsTrueForPresentCapability` | Unit |
| 2 | `ActorCapabilitySnapshot_Has_ReturnsFalseForAbsentCapability` | Unit |
| 3 | `ActorCapabilitySnapshot_HasAny_ReturnsTrueWhenOnePresent` | Unit |
| 4 | `ActorCapabilitySnapshot_MergeFrom_UnionsCapabilities` | Unit |
| 5 | `CapabilityQuery_ActorWithProvider_ReturnsSnapshot` | Unit |
| 6 | `CapabilityQuery_ActorWithoutProvider_ReturnsDefault` | Unit |
| 7 | `EngagementPolicy_CanEngage_SourceHasRequired_ReturnsTrue` | Unit |
| 8 | `EngagementPolicy_CanEngage_SourceLacksRequired_ReturnsFalse` | Unit |
| 9 | `EngagementPolicy_CanEngage_NoRulesForTarget_ReturnsTrue` | Unit |
| 10 | `PolicyGate_CanApply_ActorHasAllRequired_ReturnsTrue` | Unit |
| 11 | `PolicyGate_CanApply_ActorLacksRequired_ReturnsFalse` | Unit |
| 12 | `PolicyGate_CanApply_EmptyRequirements_ReturnsTrue` | Unit |
| 13 | `CombatDomain_EngagementPolicy_ExcludesUnreachableTargets` | Regression |
| 14 | `IdleRoute_PolicyGate_ActorWithoutWalk_FallsBackToHold` | Regression |

Add to `Packages/com.morboo.architecture.tests/Tests/Editor/OrchestrationImplementationFitnessTests.cs`:

| # | Test | Type |
|---|------|------|
| 15 | `RuntimeHost_IActorCapabilityQuery_Exists` | Architecture |
| 16 | `RuntimeHost_WorldCache_Implements_IActorCapabilityQuery` | Architecture |
| 17 | `RuntimeHost_ArbiterContext_Has_ActorCapabilities_Field` | Architecture |
| 18 | `RuntimeHost_Arbiter_Calls_SnapshotActorCapabilities` | Architecture |
| 19 | `StrategyCombat_CombatDomainComponent_References_EngagementPolicy` | Architecture |
| 20 | `RuntimeHost_CombatTargetingPolicyAsset_Implements_IActorCapabilityGatedPolicy` | Architecture |
| 21 | `RuntimeHost_IdlePolicyAsset_Implements_IActorCapabilityGatedPolicy` | Architecture |

---

## Tests / Gates

Acceptance criteria for C06 closure:
1. All 21 tests green.
2. `CombatDomainComponent.EvaluateDomain` with engagement policy returns different targeting outcome depending on actor capability state.
3. Idle execution route with policy capability gating produces `Hold` for actors missing required capabilities.
4. `SnapshotActorCapabilities` runs each tick and snapshots per-actor capabilities via actor-first `GetComponent<IActorCapabilityProvider>()` correlation (not by iterating `OrchestrationRegistry.CapabilityProviders`).
5. No string-based capability types remain in codebase (`CapabilityId`, `CapabilityEntry`, `CapabilitySet` deleted).
6. All `ActorCapability` SO assets are created and assignable in Inspector.

---

## Risks / Tradeoffs

1. **`GetComponent<IActorCapabilityProvider>()` per actor per tick** — O(1) per actor (Unity component scan on small component count). With 256 actors at 5–20 Hz tick rate, this is ~256 calls per tick. Acceptable for mobile. If profiling shows a bottleneck, future optimization: cache provider reference in WorldCache at registration time. `PERF:` comment documents the decision.

2. **`IActorCapabilityQuery` in RuntimeHost, not Framework** — `ActorCapabilitySnapshot` holds `List<ActorCapability>` where `ActorCapability` is a RuntimeHost `ScriptableObject`. Framework cannot reference RuntimeHost. Decision: query contract stays with data types. If capabilities are later promoted to Framework, query moves with them.

3. **Friendly aggregate computed by consumer (O(N) per EvaluateDomain call)** — iterating all actors once per tick to build the aggregate is O(N) with N ≤ 256. This is negligible compared to the existing spatial distance calculations in the same loop. Future optimization: cache aggregate in WorldCache if multiple consumers need it.

4. **`ActorCapabilitySnapshot.MergeFrom` may allocate on first call** — the `Add` list is created lazily. This is a one-time warmup allocation per domain component instance. Subsequent ticks reuse the list.

5. **Type system reform (S0) touches many files** — mechanical renames + type changes. Risk mitigated by: each slice compiles independently; rename is type-safe (compiler catches missing references); no behavior change in S0.

6. **Policy gating at execution route level means capabilities must be queryable after arbitration** — `IActorCapabilityQuery` is populated in `BuildWorldCache` (before arbitration/domain polling). The same `_world` object is passed to execution routes. Execution routes already receive `OrchestrationWorldCache world` as parameter — they can cast to `IActorCapabilityQuery` or receive it separately. No additional snapshot pass needed.

---

## Decision Record (C06 Position)

1. **`ActorCapability` is `ScriptableObject`, not string/enum.** Reference equality, no typos, Unity-native asset authoring. Same pattern as `FactionAsset`, `RoleAsset`. Adding new capabilities = creating new SO asset, no code changes.

2. **`ParamSet` removed from capabilities.** Capabilities are boolean presence only. Parameterized behavior (attack range, movement speed) belongs in role policies/constraints, not in capability definitions. If parameterized capabilities are needed in the future, they can be added as typed fields on `ActorCapability` subclasses, not as untyped bags.

3. **`IActorCapabilityQuery` in RuntimeHost, not Framework.** `ActorCapability` is a RuntimeHost `ScriptableObject`. Framework cannot depend on RuntimeHost. Placing the query interface at the same layer as the data types maintains clean dependency direction.

4. **`IActorCapabilityQuery` is domain-agnostic with only per-actor methods.** No `GetCombatReceiverCapabilities` or `GetIdleReceiverCapabilities`. Capabilities belong to the actor, not to a domain. Consumer gets `EntityId` from the receiver list in WorldCache and queries capabilities through the single `TryGetActorCapabilities(EntityId)` method.

5. **No aggregate method on `IActorCapabilityQuery`.** Aggregate is consumer responsibility. Different consumers may need different filters (friendly vs neutral vs all). Putting aggregate on the interface would require faction/domain parameters, introducing coupling.

6. **Engagement policy uses neutral naming: `sourceCaps` / `targetCaps`.** The orchestrator may assign healing, support, escort — not just attack. "Attacker/Defender" framing is domain-biased.

7. **Consumer is domain-level (CombatDomainComponent), not execution-route-level.** Filtering happens during proposal formation. Targets excluded by engagement policy never enter `CombatTargetSet`. This is consistent with how hostility filtering already works at the domain level.

8. **Policy capability gating is unified across all domains.** Same `IActorCapabilityGatedPolicy` interface, same `ActorCapabilityPolicyGate.CanApply()` check, same fallback-to-Hold pattern. No domain-specific variation. When a third domain is added, it uses the same pattern without new code in the gating infrastructure.

9. **`CombatCommand`/`IdleCommand` stay unchanged in C06.** Universal command system (`Engage`/`Cancel`/`None`) is a structural refactor that affects the entire dispatch chain (commands, bus, adapters, receivers, executors, routes). This is explicitly deferred to `C06A` to keep C06 focused on capability integration.

10. **Folder renamed from `Capabilities/` to `ActorCapabilities/`.** Prevents confusion with potential future capability systems in other subsystems. All types in the folder are `ActorCapability`-prefixed for consistency.

---

## C06A — Deferred: Universal Command System + Target Search (Documented Scope)

The following is explicitly out of C06 scope and tracked as the next step:

1. **Universal `OrchestrationCommand`** — replaces `CombatCommand` + `IdleCommand` with `{ Engage(target), Cancel, None }`. Actor decides behavior based on its capabilities.
2. **Universal target search** — the radius/screen-viewport search logic is extracted from `CombatDomainComponent` into a shared mechanism usable by all domains.
3. **Receiver interface unification** — `ICombatCommandReceiver` + `IIdleCommandReceiver` → single `IOrchestrationCommandReceiver`.
4. **Command adapter unification** — `CombatCommandAdapter` + `IdleCommandAdapter` → single universal adapter.
5. **`ExecutionRoute` convergence** — combat and idle routes share the same dispatch pattern; domain-specific behavior moves into the actor.

RATIONALE: Universal commands touch the entire dispatch chain (producing routes → bus → adapters → receivers → executors). C06 focuses on making capability data available and consumed; C06A restructures how commands flow based on that data.
