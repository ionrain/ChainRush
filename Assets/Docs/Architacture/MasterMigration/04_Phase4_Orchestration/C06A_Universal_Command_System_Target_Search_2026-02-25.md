# C06A — Universal Command System + Target Search

Date: 2026-02-25
Phase: `Phase 4` (`Orchestration Platform Remediation`)
Type: structural refactor (controlled)
Status: `open`

---

## Why This Step Exists

C06 delivered capability integration: typed `ActorCapability` SO tokens, `IActorCapabilityQuery` on `OrchestrationWorldCache`, engagement policy filtering in `CombatDomainComponent`, and unified policy capability gating across domains. Capabilities now influence both target selection and policy application.

However, the command and dispatch layers remain domain-split:

1. **Two command structs** — `CombatCommand` (8 fields, 8-variant `CombatCommandType`) and `IdleCommand` (4 fields, 3-variant `IdleCommandType`). The orchestrator decides *what* to do, but encodes *how* into the command (e.g., `AttackTarget` vs `KeepDistance` vs `HideBehind`). The actor — which knows its own capabilities — should decide *how*.

2. **Two dispatch wrappers** — `DispatchCombatCommand` and `DispatchIdleCommand`. Identical actor-addressed envelope (`ReceiverEntityId` + `Payload` + `ReceiverRoleId`) duplicated per domain. Two bus subscriptions required.

3. **Two receiver interfaces** — `ICombatCommandReceiver.ApplyCombatCommand(CombatCommand)` and `IIdleCommandReceiver.ApplyIdleCommand(IdleCommand)`. Actors that participate in both domains implement both interfaces, receive two distinct calls, and must reconcile internally.

4. **Two adapters** — `CombatCommandAdapter` and `IdleCommandAdapter`. Both follow the same pattern (subscribe bus → resolve EntityId → inject per-role policy → inject constraints → call Apply), but each reimplements it with domain-specific types. Policy injection logic is duplicated: `ICombatTargetPolicySelector.SetRuntimeDefaultPolicy` vs `IIdlePolicySelector.SetRuntimeDefaultPolicy` + `ResolvePolicy()` recomputation.

5. **Two execution routes** — `StrategyCombatCombatExecutionRoute` and `StrategyCombatIdleExecutionRoute`. Both iterate `OrchestrationWorldCache` receiver lists and emit dispatch commands. The idle route does per-unit policy resolution (capability-gated), the combat route broadcasts a single command to all. The dispatch envelope differs, but the iteration pattern is identical.

6. **Separate actor receiver lists in WorldCache** — `_combatReceiverEntityIds/_combatReceiverRoleIds` and `_idleReceiverEntityIds/_idleReceiverRoleIds`. Two `SnapshotReceivers()` loops. Two sets of accessors (`CombatReceiverCount`/`GetCombatReceiverEntityId` and `IdleReceiverCount`/`GetIdleReceiverEntityId`). This blocks clean operator-level addressing (`Actor|Group`) because the infrastructure is already actor-specific.

7. **Target search locked inside CombatDomainComponent** — The radius/screen-viewport hostile search, spatial scoring, Top-K selection, and `CombatTargetSet` resolution are embedded in `CombatDomainComponent`. No other domain can search for targets without duplicating this logic.

8. **`CombatCommandType` encodes actor-level behavior** — `AttackTarget`, `MoveToTarget`, `KeepDistance`, `HideBehind`, `Assist` are choices that depend on the actor's capabilities, weapon type, and AI state. The orchestrator should not make these decisions — it should say "engage this target" and let the actor decide the approach.

With C06's capability system in place, actors have the data they need to make these decisions. C06A unifies the command layer so the orchestrator speaks in intent (`Engage`, `Cancel`; `None` is a sentinel/no-dispatch default) and operator receivers (actors now, groups later) translate intent into behavior.

---

## Goal

1. Replace `CombatCommand` + `IdleCommand` with a single `OrchestrationCommand` struct (intent + `OrchestrationTargetRef`) — intent-based (`Engage`, `Cancel`, `None` sentinel).
2. Replace `DispatchCombatCommand` + `DispatchIdleCommand` with a single `DispatchOrchestrationCommand` addressed by operator `EntityId` (actors and groups are both command-targetable operator entities).
3. Replace `ICombatCommandReceiver` + `IIdleCommandReceiver` with a single `IOrchestrationCommandReceiver` (implemented by actor and/or group operator receivers).
4. Unify `CombatCommandAdapter` + `IdleCommandAdapter` into a single `OrchestrationCommandAdapter`.
5. Converge `StrategyCombatCombatExecutionRoute` + `StrategyCombatIdleExecutionRoute` dispatch envelopes — both emit `DispatchOrchestrationCommand`.
6. Unify WorldCache command-targetable lists at operator level — single `_operatorEntityIds`.
7. Unify `OrchestrationRegistry` command receiver registration — single `IOrchestrationCommandReceiver` list (actors and groups).
8. Introduce domain-neutral `OrchestrationTargetSearch` selection primitives + target-provider seam: RuntimeHost search selects among generic `TargetRef` candidates; domain/integration code provides candidate enumeration/filtering (combat hostiles, rest spots, interactables, etc.).
9. Unify policy selectors — `ICombatTargetPolicySelector` + `IIdlePolicySelector` → `IOrchestrationPolicySelector`.
10. Reform `ExecutionContext.CombatCommand` → `ExecutionContext.Command` (OrchestrationCommand).
11. Reform `OrchestrationArbiterProposals` — domain-neutral proposal API.
12. Preserve existing behavior: all actor operators respond identically to pre-C06A commands when no capabilities or new policies are configured (using `Engage(TargetRef.*)` / `Cancel` / no-dispatch mapping). Group operators are new and have no pre-C06A parity requirement.

---

## Non-Goals (For C06A)

1. **Target reservations / slot contention** (e.g., multiple workers competing for one tree/mine interaction slot) — deferred to `C06B` (reservation model) and `C06C` (group slot assignment).
2. **AI brain refactor** — actors still use TDE/AIBrain internally. `ApplyCommand` sets target; AIBrain decides transitions. No AIBrain changes in C06A (tracked in `C06D`).
3. **Formation system** — deferred to `C06C`.
4. **Objective-driven policy switching** — deferred (no Objective system yet).
5. **New domain types** (e.g., Economy domain, Escort domain) — C06A prepares the infrastructure but does not add domains.
6. **CombatTargetSet redesign** — the target set stays as-is; it is populated by the extracted search utility instead of inline code.
7. **Per-receiver capability-aware command specialization at adapter level** — the adapter delivers `OrchestrationCommand` as-is; the receiver translates intent to behavior internally. No adapter-side branching based on capabilities.
8. **Constraints system generalization** — `IConstrainedCombatReceiver` → `IConstrainedReceiver` is desirable but out of scope. Constraints continue to flow through the existing interface; the unified adapter calls it when present.
9. **InProcessCommandBus refactor** — the bus stays generic. Only the subscribed types change.
10. **Removal of combat search configuration fields** (`TargetSearchMode`, `aggroRadius`, `searchCamera`, `viewportMargin`) is not required in C06A — they may stay on `CombatDomainComponent` (or a combat provider/filter helper), but MUST NOT be baked into the RuntimeHost `OrchestrationTargetSearch` API.
11. **Group-level policy system** (`GroupBrainPolicy`: formation/cohesion/sync/role-slot rules) — deferred to `C06C`.
12. **Group slot assignment / fan-out into member intents** — deferred to `C06C`.
13. **Actor brain policy seam replacing orchestration execution hints** (`preferred range`, stop distance, avoid-clump, approach style) — deferred to `C06D` (parallel to TDE/AIBrain).
14. **`DomainComponent` / `TargetProvider` convergence around generic targets** — deferred to `C06B`.

---

## Hard Rules (For C06A)

1. **`OrchestrationCommand` is intent-only** — `Engage(target)` means "interact with this target". `Cancel` means "abort current orchestration-driven action and return to local default behavior." `None` is a sentinel/default (typically not dispatched). The orchestrator does NOT specify attack/assist/keep-distance/hide and does NOT encode actor/group execution preferences (range, stop distance, spacing, avoid-clump, urgency). The actor decides based on capabilities, role, and AI state (future `ActorBrainPolicy` / `GroupBrainPolicy` seams).
2. **`OrchestrationCommand` + `OrchestrationTargetRef` are engine-agnostic** — only engine-agnostic value types (`EntityId`, enums, primitives, `string`) and orchestration structs composed of them. Zero Unity types. `OrchestrationTargetRef` is reference-only (`Kind + target EntityId`); target position/area/route semantics are resolved by query/resolver seams keyed by that `EntityId`, not by parameters embedded in the command payload.
3. **Single receiver interface** — `IOrchestrationCommandReceiver.ApplyCommand(OrchestrationCommand)`. No domain-specific receiver interfaces in RuntimeHost after C06A. Actors and groups may both implement this interface.
4. **Single dispatch wrapper with operator addressing** — `DispatchOrchestrationCommand : ICommand` carries `ReceiverEntityId` (operator entity id only). No `RoleId` in the dispatch payload. `None` commands are normally filtered before dispatch. One bus subscription per adapter instance.
5. **Single adapter in Integration** — `OrchestrationCommandAdapter` resolves `ReceiverEntityId` to `IOrchestrationCommandReceiver`, resolves role/capabilities on demand (when needed for actor-only policy injection), applies constraints (if `IConstrainedReceiver`), and calls `ApplyCommand`.
6. **Target search in RuntimeHost is domain-neutral selection logic** — `OrchestrationTargetSearch` is a shared utility (static methods or lightweight allocator-free class) that selects/ranks generic `OrchestrationTargetRef` candidates. Combat-specific hostile enumeration, camera/viewport filtering, and aggro-radius rules belong to domain/integration provider/filter seams, not to the RuntimeHost selector API.
7. **PERF: no per-tick allocations beyond warmup, no LINQ** — same constraint as all orchestration code. `OrchestrationCommand` is a struct. Target search output reuses pre-allocated arrays.
8. **Existing behavior preserved when no capabilities configured** — `OrchestrationCommand.Engage(target)` without capability data = same as `CombatCommand.AttackTarget(target)` today. Receiver implementations must handle the general `Engage` by defaulting to attack when no capability data is available.
9. **`ExecutionRouter` stays generic** — routes register by `OrchestrationDomainId`, delegate signature unchanged. Routes decide which `DispatchOrchestrationCommand` to emit.
10. **No legacy command conversions in the runtime path** — `OrchestrationCommandConversions`-style bridges are NOT allowed in `OrchestrationArbiter`, `ExecutionRouter` routes, or integration adapters. `C06A` must migrate producers/proposals/policies so active runtime code emits/consumes `OrchestrationCommand` directly.
11. **`OrchestrationWorldCache` operator snapshot remains per-tick full rebuild** — consistent with all other WorldCache data.
12. **C06A operator abstraction is addressing-only** — actors and groups are both addressable as operator entities (`EntityId`) behind the same `IOrchestrationCommandReceiver` contract, but C06A does NOT introduce `GroupBrainPolicy`, formation logic, slot assignment, or member fan-out. Those stay in `C06C`.
13. **Domain components own domain-specific logic** — `CombatDomainComponent` still decides WHEN to engage (domain evaluation), calls target search utility for WHO, and proposes the command. The unified command only changes the command SHAPE, not the domain evaluation flow.
14. **`C06A` includes producer/proposal API migration** — `OrchestrationArbiterProposals`, `OrchestrationProposalCollector`, and active domain producers/policies (`CombatDomainComponent`, idle policy command generation path) are in scope and must stop using `CombatCommand` / `IdleCommand` on the active runtime path.

---

## Prerequisite State (Before C06A)

1. `C06` closed — `ActorCapability` SO, `IActorCapabilityQuery`, `ActorCapabilityEngagementPolicy`, `IActorCapabilityGatedPolicy`, all wired and tested.
2. `OrchestrationWorldCache` implements `IActorCapabilityQuery` — per-actor capability snapshots available during domain polling and execution.
3. `InProcessCommandBus` supports generic `Publish<T>` / `Subscribe<T>` / `Flush` — no type changes needed in bus itself.
4. `ExecutionRouter` uses registered routes with `DomainExecutionRouteExecutor` delegate — route registration pattern is established.
5. `CombatCommandAdapter` and `IdleCommandAdapter` demonstrate the adapter pattern: bus subscribe → EntityId resolve → policy inject → Apply.
6. `CombatDomainComponent` contains complete target search logic (radius, screen-viewport, Top-K, engagement policy filter) — ready for extraction.
7. `IExecutionRouteHost.PublishCommand<T>` is generic — will work with new `DispatchOrchestrationCommand` without changes.

---

## Type System Reform

### New Types (in `Packages/com.morboo.runtimehost/Runtime/Orchestration/DomainContracts/`)

**`OrchestrationCommandType`** enum — intent-based.

```csharp
/// <summary>
/// Intent-based command types. The orchestrator expresses WHAT to do;
/// the actor decides HOW based on capabilities and AI state.
/// IMPORTANT: No actor-behavior-specific types (attack, assist, keep-distance).
/// IMPORTANT: "None" is a sentinel/default value, not a normal dispatched command.
/// </summary>
public enum OrchestrationCommandType
{
    /// <summary>No command issued. Sentinel/default value; usually not published to receivers.</summary>
    None,

    /// <summary>
    /// Engage the specified target (entity/point/area/route). Actor decides approach
    /// (attack, assist, heal, escort, movement/reposition) based on its
    /// capabilities, role, and AI state.
    /// </summary>
    Engage,

    /// <summary>Cancel current action and return to default state.</summary>
    Cancel
}
```

**`OrchestrationTargetKind` + `OrchestrationTargetRef`** — universal command target.

```csharp
/// <summary>
/// Universal target kinds for orchestration intent.
/// Entity = any world entity (actor, tree, mine, building, station).
/// Point/Area/Route = target entities representing anchor/zone/route semantics.
/// </summary>
public enum OrchestrationTargetKind
{
    None,
    Entity,
    Point,
    Area,
    Route
}

/// <summary>
/// Engine-agnostic tagged target reference used by OrchestrationCommand.
/// IMPORTANT: Reference-only value data (kind + target entity id).
/// Position/anchor/area/route resolution is handled by query/resolver seams
/// keyed by the referenced EntityId, not by inline parameters in the payload.
/// C06A establishes this command payload shape; C06B expands domain producer/
/// resolver coverage and reservation/slot semantics around the same type.
/// </summary>
[System.Serializable]
public struct OrchestrationTargetRef
{
    public OrchestrationTargetKind Kind;
    public EntityId TargetEntityId;

    public static OrchestrationTargetRef None => default;

    public static OrchestrationTargetRef Entity(EntityId targetEntityId)
        => new OrchestrationTargetRef { Kind = OrchestrationTargetKind.Entity, TargetEntityId = targetEntityId };

    public static OrchestrationTargetRef Point(EntityId pointTargetEntityId)
        => new OrchestrationTargetRef { Kind = OrchestrationTargetKind.Point, TargetEntityId = pointTargetEntityId };

    public static OrchestrationTargetRef Area(EntityId areaTargetEntityId)
        => new OrchestrationTargetRef { Kind = OrchestrationTargetKind.Area, TargetEntityId = areaTargetEntityId };

    public static OrchestrationTargetRef Route(EntityId routeTargetEntityId)
        => new OrchestrationTargetRef { Kind = OrchestrationTargetKind.Route, TargetEntityId = routeTargetEntityId };

    public bool IsNone => Kind == OrchestrationTargetKind.None;
}
```

`OrchestrationTargetRef` does NOT carry inline spatial/route parameters. The command payload carries only `Kind + TargetEntityId`; resolvers/query seams locate target data by `EntityId`.

**`OrchestrationCommand`** struct — unified command.

```csharp
/// <summary>
/// Engine-agnostic orchestration command struct.
/// IMPORTANT: Contains ONLY engine-agnostic value types (TargetRef, enums, primitives, string).
/// Zero Unity types and no behavior/policy objects.
/// EntityId→Transform resolution happens strictly in Integration adapters.
/// RATIONALE: Replaces CombatCommand + IdleCommand with intent-based commands.
/// Actor decides specific behavior via capabilities + AIBrain.
/// IMPORTANT: Preferred range, stop distance, spacing, approach style, and urgency are NOT
/// encoded here; those belong to actor/group brain policies or proposal metadata.
/// </summary>
[System.Serializable]
public struct OrchestrationCommand
{
    public OrchestrationCommandType Type;
    public OrchestrationTargetRef Target;
    public string DebugLabel;

    public static OrchestrationCommand None => new OrchestrationCommand
    {
        Type = OrchestrationCommandType.None,
        Target = OrchestrationTargetRef.None,
        DebugLabel = null
    };

    public static OrchestrationCommand Engage(
        OrchestrationTargetRef target,
        string debugLabel = null)
    {
        return new OrchestrationCommand
        {
            Type = OrchestrationCommandType.Engage,
            Target = target,
            DebugLabel = debugLabel
        };
    }

    public static OrchestrationCommand Cancel(string debugLabel = null)
    {
        return new OrchestrationCommand
        {
            Type = OrchestrationCommandType.Cancel,
            DebugLabel = debugLabel
        };
    }

    public bool IsNone => Type == OrchestrationCommandType.None;
    public bool HasTarget => !Target.IsNone;
}
```

**`DispatchOrchestrationCommand`** — unified dispatch envelope.

```csharp
/// <summary>
/// Command dispatched via <see cref="ICommandBus"/> for a single operator receiver.
/// Contains the engine-agnostic <see cref="OrchestrationCommand"/> payload plus operator EntityId.
/// IMPORTANT: Integration adapters resolve ReceiverEntityId to actor/group receiver,
/// inject actor-role policies/constraints when applicable (role resolved on demand), and call ApplyCommand. RuntimeHost never
/// calls Apply directly.
/// </summary>
public struct DispatchOrchestrationCommand : ICommand
{
    public EntityId ReceiverEntityId; // Operator entity id: actor or group.
    public OrchestrationCommand Payload;
}
```

**`IOrchestrationCommandReceiver`** — unified receiver interface.

```csharp
/// <summary>
/// Unified operator receiver contract. Implementors translate an <see cref="OrchestrationCommand"/>
/// into concrete gameplay actions based on the command intent, capabilities, and AI state/policies.
/// IMPORTANT: The receiver decides HOW to respond to intent (Engage → attack/assist/heal).
/// Movement and behavior transitions remain the responsibility of the underlying AI
/// state machine (AIBrain / AIDecisions).
/// </summary>
public interface IOrchestrationCommandReceiver
{
    void ApplyCommand(OrchestrationCommand command);
}
```

**`IOrchestrationPolicySelector`** — unified policy selector.

```csharp
/// <summary>
/// Unified policy selector. Replaces ICombatTargetPolicySelector + IIdlePolicySelector.
/// IMPORTANT: Sole authorized channel for injecting orchestration policy at runtime.
/// Implemented by game-specific selectors in Integration.Game; consumed by
/// OrchestrationCommandAdapter during command dispatch.
/// </summary>
public interface IOrchestrationPolicySelector
{
    void SetRuntimeDefaultPolicy(ScriptableObject policy);
    ScriptableObject ResolvePolicy();
}
```

### Removed Types (after full migration)

| File | Replaced By |
|------|-------------|
| `DomainContracts/Combat/CombatCommand.cs` | `OrchestrationCommand` |
| `DomainContracts/Combat/CombatCommandType.cs` | `OrchestrationCommandType` |
| `DomainContracts/Combat/ICombatCommandReceiver.cs` | `IOrchestrationCommandReceiver` |
| `DomainContracts/Idle/IdleCommand.cs` | `OrchestrationCommand` |
| `DomainContracts/Idle/IdleCommandType.cs` | `OrchestrationCommandType` |
| `DomainContracts/Idle/IIdleCommandReceiver.cs` | `IOrchestrationCommandReceiver` |
| `DomainContracts/Dispatch/DispatchCombatCommand.cs` | `DispatchOrchestrationCommand` |
| `DomainContracts/Dispatch/DispatchIdleCommand.cs` | `DispatchOrchestrationCommand` |
| `StrategyCombat/.../Contracts/ICombatTargetPolicySelector.cs` | `IOrchestrationPolicySelector` |
| `StrategyCombat/.../Contracts/IIdlePolicySelector.cs` | `IOrchestrationPolicySelector` |
| `StrategyCombat/.../Adapters/CombatCommandAdapter.cs` | `OrchestrationCommandAdapter` |
| `StrategyCombat/.../Adapters/IdleCommandAdapter.cs` | `OrchestrationCommandAdapter` |

### CombatCommand → OrchestrationCommand Type Mapping

| CombatCommandType | OrchestrationCommandType | Notes |
|-------------------|--------------------------|-------|
| `None` | `None` | Sentinel/default; usually no dispatch |
| `Hold` | `Cancel` or `None` | Prefer no dispatch (`None`) when no explicit stop is needed; use `Cancel` for explicit stop/reset |
| `MoveToPoint` | `Engage` | `Engage(TargetRef.Point(...))` |
| `MoveToTarget` | `Engage` | `Engage(TargetRef.Entity(...))`; actor uses AIBrain for approach movement |
| `AttackTarget` | `Engage` | `Engage(TargetRef.Entity(...))`; actor decides attack via capabilities |
| `KeepDistance` | `Engage` | `Engage(TargetRef.Entity(...))`; actor/group brain policies decide ranged spacing and approach locally |
| `HideBehind` | `Engage` | `Engage(TargetRef.Entity/Point(...))`; actor decides cover behavior via capabilities |
| `Assist` | `Engage` | `Engage(TargetRef.Entity(...))`; actor decides support via capabilities |

| IdleCommandType | OrchestrationCommandType | Notes |
|-----------------|--------------------------|-------|
| `None` | `None` | Sentinel/default; usually no dispatch |
| `Hold` | `Cancel` or `None` | Prefer no dispatch (`None`) when idle default is already acceptable; use `Cancel` for explicit abort/reset |
| `MoveToPoint` | `Engage` | `Engage(TargetRef.Point(...))` |

---

## Universal Target Search

### Design

Target search is extracted from `CombatDomainComponent` into a shared utility class in RuntimeHost:

```csharp
// Packages/com.morboo.runtimehost/Runtime/Orchestration/TargetSearch/OrchestrationTargetSearch.cs

/// <summary>
/// Domain/integration seam: enumerates target candidates for a seeker/operator.
/// Examples: hostile actors, rest spots, mining nodes, interactable buildings.
/// </summary>
public interface IOrchestrationTargetProvider
{
    int FillCandidates(
        IWorldQuery world,
        EntityId seekerEntityId,
        OrchestrationTargetRef[] outCandidates);
}

/// <summary>
/// Domain/integration seam: optional filtering over provider candidates.
/// Combat examples: aggro radius, viewport/camera, hostility, engageability.
/// Idle examples: rest-point availability, "inside camp area", reservation-free.
/// </summary>
public interface IOrchestrationTargetFilter
{
    bool Accept(
        IWorldQuery world,
        EntityId seekerEntityId,
        in OrchestrationTargetRef candidate);
}

/// <summary>
/// Domain-agnostic ranking seam.
/// Returns higher score = better candidate.
/// </summary>
public interface IOrchestrationTargetScorer
{
    bool TryScore(
        IWorldQuery world,
        EntityId seekerEntityId,
        in OrchestrationTargetRef candidate,
        out float score);
}

/// <summary>
/// Shared selector/collector over generic TargetRef candidates.
/// IMPORTANT: Stateless — all state passed via parameters. No per-instance fields.
/// PERF: Reuses caller-provided arrays. No per-call allocations.
/// RuntimeHost knows nothing about "hostile", "rest", "camera", or combat-only modes.
/// </summary>
public static class OrchestrationTargetSearch
{
    public static bool TryFindBest(
        IWorldQuery world,
        EntityId seekerEntityId,
        IOrchestrationTargetProvider provider,
        IOrchestrationTargetFilter filter,
        IOrchestrationTargetScorer scorer,
        OrchestrationTargetRef[] scratchCandidates,
        out OrchestrationTargetRef bestTarget)
    { /* generic provider + filter + scorer selection */ }

    public static int FillTopK(
        IWorldQuery world,
        EntityId seekerEntityId,
        IOrchestrationTargetProvider provider,
        IOrchestrationTargetFilter filter,
        IOrchestrationTargetScorer scorer,
        int maxTargets,
        OrchestrationTargetRef[] outTargets,
        float[] outScores,
        OrchestrationTargetRef[] scratchCandidates)
    { /* generic Top-K selection over provider candidates */ }
}
```

### CombatDomainComponent After Extraction

`CombatDomainComponent.EvaluateDomain` no longer calls hostile-specific methods on `OrchestrationTargetSearch`. Instead it composes:

1. a **combat target provider** (enumerates hostile candidate entities),
2. one or more **combat filters** (hostility, `aggroRadius`, optional camera/viewport gating, capability engagement policy),
3. a **scorer** (e.g., closest hostile / priority score),
4. and then calls `OrchestrationTargetSearch.TryFindBest(...)` or `FillTopK(...)`.

This keeps `CombatDomainComponent` domain logic intact ("when combat is active" + combat config ownership), while making the shared search layer domain-neutral and reusable by idle/economy domains (e.g., rest-spot / resource-node search).

---

## Unified Adapter Design

### OrchestrationCommandAdapter

```csharp
// Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/OrchestrationCommandAdapter.cs

/// <summary>
/// Integration adapter: subscribes to InProcessCommandBus for DispatchOrchestrationCommand,
/// resolves operator EntityId → receiver, injects actor-role policies + constraints when applicable, and calls
/// IOrchestrationCommandReceiver.ApplyCommand.
/// IMPORTANT — This is the ONLY place ApplyCommand is called at runtime.
/// RuntimeHost emits commands via the bus; this adapter bridges to MonoBehaviours.
/// </summary>
public sealed class OrchestrationCommandAdapter : MonoBehaviour
{
    [SerializeField] OrchestrationLoop orchestrationLoopComponent;

    OrchestrationLoop _loop;

    void OnEnable()
    {
        _loop = orchestrationLoopComponent;
        if (_loop != null)
            _loop.CommandBus.Subscribe<DispatchOrchestrationCommand>(HandleCommand);
    }

    void OnDisable()
    {
        if (_loop != null)
            _loop.CommandBus.Unsubscribe<DispatchOrchestrationCommand>();
    }

    void HandleCommand(DispatchOrchestrationCommand cmd)
    {
        Transform t = EntityTransformResolver.Resolve(cmd.ReceiverEntityId);
        if (t == null) return;

        IOrchestrationCommandReceiver r = t.GetComponent<IOrchestrationCommandReceiver>();
        if (r == null) return;
        if (r is Object uo && uo == null) return;

        ExecutionContext ctx = _loop.CurrentExecContext;
        OrchestrationWorldCache world = _loop.CurrentWorld;

        // ── Unified policy injection ──
        // Resolve per-role policy from bindings, inject via selector, handle overrides
        InjectPolicy(t, cmd, ctx, world);

        // ── Constraints injection (if receiver supports it) ──
        InjectConstraints(t, r, cmd, ctx, world);

        r.ApplyCommand(cmd.Payload);
    }

    // ... policy injection, constraints injection (merged from CombatCommandAdapter + IdleCommandAdapter)
}
```

### Policy Injection Unification

The adapter merges policy injection logic from both `CombatCommandAdapter` (which injects combat targeting policy + capability gate) and `IdleCommandAdapter` (which injects idle policy + selector override + command recomputation).

The unified flow:
1. Resolve receiver role on demand from `cmd.ReceiverEntityId` (e.g., `IRoleQuery`/WorldCache lookup). If no actor role is present, skip actor-role policy injection (group or non-role receiver path).
2. Resolve policy asset from role-map binding (type determined by domain context in command or by binding availability).
3. C06 capability gate: if policy implements `IActorCapabilityGatedPolicy`, check actor capabilities. Skip if unmet.
4. Find `IOrchestrationPolicySelector` on receiver Transform.
5. `SetRuntimeDefaultPolicy(resolvedPolicy)`.
6. `ResolvePolicy()` — if effective policy differs from default, recompute command from effective policy.
7. Group receivers are still valid `IOrchestrationCommandReceiver`s in C06A, but group policy/fan-out semantics come in C06C.

### IConstrainedReceiver (Future-Compatible)

`IConstrainedCombatReceiver` stays in C06A — the unified adapter calls `SetRuntimeContext` when the receiver implements it. A future step may generalize to `IConstrainedReceiver` but this is out of C06A scope (Non-Goal #8).

---

## Operator List Unification in WorldCache

### Before C06A

```
_combatReceiverEntityIds  (actor-only lists from FriendlyCombatReceivers: List<ICombatCommandReceiver>)
_combatReceiverRoleIds
_idleReceiverEntityIds    (actor-only lists from FriendlyIdleReceivers: List<IIdleCommandReceiver>)
_idleReceiverRoleIds
```

### After C06A

```
_operatorEntityIds        (from FriendlyReceivers: List<IOrchestrationCommandReceiver>)
```

`OrchestrationRegistry`:
- `CombatReceivers` + `IdleReceivers` → `Receivers: IReadOnlyList<IOrchestrationCommandReceiver>` (actors and groups)
- Single `Register(IOrchestrationCommandReceiver)` / `Unregister(IOrchestrationCommandReceiver)`

`OrchestrationWorldCache`:
- `CombatReceiverCount` / `IdleReceiverCount` → `OperatorCount`
- `GetCombatReceiverEntityId` / `GetIdleReceiverEntityId` → `GetOperatorEntityId`
- Single `SnapshotReceivers()` loop

Execution routes iterate the single operator list. Domain-specific filtering (if needed) is done by the route based on receiver capabilities/role or other metadata looked up from the operator `EntityId`.

---

## Proposals Reform

### Before C06A

```csharp
public bool HasCombat;
public CombatCommand CombatCommand;
public bool ThreatPresent;
public bool HasIdle;
```

### After C06A

```csharp
// Domain-neutral proposals core (conceptual API)
public void SetCommand(OrchestrationDomainId domainId, OrchestrationCommand command);
public bool TryGetCommand(OrchestrationDomainId domainId, out OrchestrationCommand command);

public void MarkDomainPresent(OrchestrationDomainId domainId);
public bool HasDomain(OrchestrationDomainId domainId);

// Optional domain-scoped facts (example: Combat.ThreatPresent)
public void SetBoolFact(OrchestrationDomainId domainId, OrchestrationProposalFactKey factKey, bool value);
public bool TryGetBoolFact(OrchestrationDomainId domainId, OrchestrationProposalFactKey factKey, out bool value);
```

- `SetCombat(CombatCommand, bool)` → `SetCommand(CombatDomainId, OrchestrationCommand)` + (if needed) `SetBoolFact(CombatDomainId, ThreatPresentFactKey, ...)`
- `HasCombat` → `TryGetCommand(CombatDomainId, out _)`
- `HasIdle` → `HasDomain(IdleDomainId)` (no dedicated idle field in proposals core)
- `ThreatPresent` is no longer a top-level proposals field; it becomes a combat-domain fact (only if another route/arbiter stage actually consumes it)
- `CombatProposalKey` / `EngagementProposalKey` collapse into `OrchestrationDomainId` + generic command slot
- `ToArbitrationInput()` reads the command for the active route/domain via `TryGetCommand(route.DomainId, out ...)`

### ExecutionContext Reform

```csharp
// Before:
public CombatCommand CombatCommand;

// After:
public OrchestrationCommand Command;
```

---

## ExecutionRoute Convergence

### Combat Route After C06A

```csharp
public void Execute(IExecutionRouteHost host, ArbiterDecision decision,
                    OrchestrationWorldCache world, ExecutionContext ctx)
{
    PublishCommandForAll(host, ctx.Command, world);
    if (decision.ModeChanged && _profile.Combat.EmitCancelOnModeChange)
        PublishCancelForAll(host, world);
}

void PublishCommandForAll(IExecutionRouteHost host, OrchestrationCommand cmd,
                          OrchestrationWorldCache world)
{
    if (cmd.IsNone) return; // None = sentinel/no-dispatch

    int count = world.OperatorCount;
    for (int i = 0; i < count; i++)
    {
        EntityId opEntityId = world.GetOperatorEntityId(i);
        if (opEntityId.IsNone) continue;

        host.PublishCommand(new DispatchOrchestrationCommand
        {
            ReceiverEntityId = opEntityId,
            Payload = cmd
        });
    }
}
```

### Idle Route After C06A

Same dispatch envelope (`DispatchOrchestrationCommand`), but per-unit command resolution:

```csharp
host.PublishCommand(new DispatchOrchestrationCommand
{
    ReceiverEntityId = eid,
    Payload = cmd,   // OrchestrationCommand.Engage(TargetRef.Point/Area/Route) or .Cancel
});
```

Domain-specific behavior (per-unit idle policy resolution, capability gating, debug labeling) stays in the idle route. Only the dispatch type changes.

---

## Final Layer Mapping (C06A Target)

### RuntimeHost (`com.morboo.runtimehost`) — new

| File | Type |
|------|------|
| `Runtime/Orchestration/DomainContracts/OrchestrationCommand.cs` | struct |
| `Runtime/Orchestration/DomainContracts/OrchestrationCommandType.cs` | enum |
| `Runtime/Orchestration/DomainContracts/OrchestrationTargetRef.cs` (or `.../Targets/`) | target ref struct + enum |
| `Runtime/Orchestration/DomainContracts/IOrchestrationCommandReceiver.cs` | interface |
| `Runtime/Orchestration/DomainContracts/Dispatch/DispatchOrchestrationCommand.cs` | struct |
| `Runtime/Orchestration/TargetSearch/OrchestrationTargetSearch.cs` | domain-neutral selector utility |
| `Runtime/Orchestration/TargetSearch/IOrchestrationTargetProvider.cs` | candidate provider seam |
| `Runtime/Orchestration/TargetSearch/IOrchestrationTargetFilter.cs` | candidate filter seam |
| `Runtime/Orchestration/TargetSearch/IOrchestrationTargetScorer.cs` | ranking seam |

### RuntimeHost — updated

| File | Change |
|------|--------|
| `Execution/ExecutionContext.cs` | `CombatCommand` → `Command` (OrchestrationCommand) |
| `Arbitration/OrchestrationArbiterProposals.cs` | Replace combat/idle-specific proposal fields with domain-neutral command/domain/fact API |
| `Arbitration/OrchestrationArbiter.cs` | Updated proposal/context wiring |
| `Arbitration/OrchestrationWorldCache.cs` | Unified operator-address lists |
| `World/OrchestrationRegistry.cs` | Unified command receiver registration (actors/groups) |

### RuntimeHost — deleted (after full migration)

| File | Reason |
|------|--------|
| `DomainContracts/Combat/CombatCommand.cs` | Replaced by `OrchestrationCommand` |
| `DomainContracts/Combat/CombatCommandType.cs` | Replaced by `OrchestrationCommandType` |
| `DomainContracts/Combat/ICombatCommandReceiver.cs` | Replaced by `IOrchestrationCommandReceiver` |
| `DomainContracts/Idle/IdleCommand.cs` | Replaced by `OrchestrationCommand` |
| `DomainContracts/Idle/IdleCommandType.cs` | Replaced by `OrchestrationCommandType` |
| `DomainContracts/Idle/IIdleCommandReceiver.cs` | Replaced by `IOrchestrationCommandReceiver` |
| `DomainContracts/Dispatch/DispatchCombatCommand.cs` | Replaced by `DispatchOrchestrationCommand` |
| `DomainContracts/Dispatch/DispatchIdleCommand.cs` | Replaced by `DispatchOrchestrationCommand` |

### StrategyCombat (`com.morboo.integration.strategycombat`) — new

| File | Type |
|------|------|
| `Contracts/IOrchestrationPolicySelector.cs` | interface |
| `Adapters/OrchestrationCommandAdapter.cs` | MonoBehaviour |

### StrategyCombat — updated

| File | Change |
|------|--------|
| `Domains/Combat/CombatDomainComponent.cs` | Composes combat target provider/filters/scorer + calls `OrchestrationTargetSearch`, proposes `OrchestrationCommand` |
| `Domains/Idle/IdleDomainComponent.cs` | Minor: no idle-specific proposal changes (already domain-neutral) |
| `Execution/StrategyCombatCombatExecutionRoute.cs` | Emits `DispatchOrchestrationCommand`, unified operator iteration |
| `Execution/StrategyCombatIdleExecutionRoute.cs` | Emits `DispatchOrchestrationCommand`, unified operator iteration |

### StrategyCombat — deleted (after full migration)

| File | Reason |
|------|--------|
| `Adapters/CombatCommandAdapter.cs` | Replaced by `OrchestrationCommandAdapter` |
| `Adapters/IdleCommandAdapter.cs` | Replaced by `OrchestrationCommandAdapter` |
| `Contracts/ICombatTargetPolicySelector.cs` | Replaced by `IOrchestrationPolicySelector` |
| `Contracts/IIdlePolicySelector.cs` | Replaced by `IOrchestrationPolicySelector` |

### MorbooBridge — updated

| File | Change |
|------|--------|
| All `ICombatCommandReceiver` implementors | Implement `IOrchestrationCommandReceiver` (actor operators), translate Engage → attack/movement |
| All `IIdleCommandReceiver` implementors | Implement `IOrchestrationCommandReceiver` (actor operators), translate `Engage(TargetRef.Point)` / `Cancel` / no-dispatch |
| All `ICombatTargetPolicySelector` implementors | Implement `IOrchestrationPolicySelector` |
| All `IIdlePolicySelector` implementors | Implement `IOrchestrationPolicySelector` |

### Architecture Tests — updated

| File | Change |
|------|--------|
| `OrchestrationImplementationFitnessTests.cs` | C06A architecture gates |

### RuntimeHost Tests — new/updated

| File | Change |
|------|--------|
| `RuntimeHostTests.cs` | OrchestrationCommand construction, mapping, target search tests |

---

## Migration Plan (Execution Slices)

### S0 — New Types (Additive Only)

Create new types alongside existing ones — no deletions, no consumer changes:

1. `OrchestrationCommandType.cs` — enum.
2. `OrchestrationTargetRef.cs` (or equivalent `DomainContracts/Targets/*`) — target ref type (`Entity|Point|Area|Route`).
3. `OrchestrationCommand.cs` — struct with factory methods (`Engage`, `Cancel`, `None`).
4. `DispatchOrchestrationCommand.cs` — struct implementing `ICommand` (operator `EntityId` addressing; no `RoleId` payload field).
5. `IOrchestrationCommandReceiver.cs` — interface.
6. `IOrchestrationTargetProvider.cs` — RuntimeHost candidate provider seam.
7. `IOrchestrationTargetFilter.cs` — RuntimeHost candidate filter seam.
8. `IOrchestrationTargetScorer.cs` — RuntimeHost ranking seam.
9. `OrchestrationTargetSearch.cs` — domain-neutral static selector utility (`TryFindBest` / `FillTopK`) over provider/filter/scorer.

Acceptance:
1. Compiles in Unity.
2. Old types unchanged.
3. `OrchestrationCommand.Engage(OrchestrationTargetRef.Entity(entityId))` creates a valid command.
4. `OrchestrationTargetSearch.TryFindBest(...)` + combat provider/filter/scorer composition returns the same chosen hostile target as pre-C06A `CombatDomainComponent.FindClosestHostileIndex(...)`.

### S1 — Receiver Interface Transition

Add `IOrchestrationCommandReceiver` to existing receiver implementations (dual-interface period) and introduce operator registration/addressing:

1. All `ICombatCommandReceiver` implementors also implement `IOrchestrationCommandReceiver`.
   - `ApplyCommand(OrchestrationCommand)` dispatches internally based on `Type`:
     - `Engage(TargetRef.Entity)` → existing `ApplyCombatCommand(CombatCommand.Create(AttackTarget, ...))` (default mapping)
     - `Engage(TargetRef.Point/Area/Route)` → existing movement/reposition path (if supported by receiver)
     - `Cancel` → existing `ApplyCombatCommand(CombatCommand.Create(Hold))` (compatibility mapping)
     - `None` → no-op (not normally dispatched)
2. All `IIdleCommandReceiver` implementors also implement `IOrchestrationCommandReceiver`.
   - `ApplyCommand(OrchestrationCommand)` dispatches:
     - `Engage(TargetRef.Point)` → existing `ApplyIdleCommand(IdleCommand.MoveToPoint(...))`
     - `Cancel` → existing `ApplyIdleCommand(IdleCommand.Hold())` (compatibility mapping)
     - `None` → no-op (not normally dispatched)
3. `OrchestrationRegistry`:
   - Add `IOrchestrationCommandReceiver` list alongside existing lists.
   - `Register(IOrchestrationCommandReceiver)` / `Unregister(IOrchestrationCommandReceiver)`.
   - Existing receiver registrations continue to work in parallel.
4. Add operator identity registration/address seam:
   - Actor and group receivers are both addressed by `EntityId` (operator entity id).
   - Group receiver registration path exists in C06A (identity/address only), even if groups are not yet produced by gameplay systems until C06C.

Acceptance:
1. Compiles. All existing tests pass.
2. `OrchestrationRegistry.Receivers` populated at runtime with actor operator receivers (and group receivers if test doubles register them).
3. Calling `ApplyCommand(OrchestrationCommand.Cancel())` on a combat receiver produces same result as calling old `ApplyCombatCommand(CombatCommand.Create(Hold))` (compatibility mapping).

### S2 — Unified Adapter + Policy Selector

Create unified adapter and policy selector:

1. `IOrchestrationPolicySelector.cs` in StrategyCombat contracts.
2. All existing `ICombatTargetPolicySelector` / `IIdlePolicySelector` implementors also implement `IOrchestrationPolicySelector`:
   - `SetRuntimeDefaultPolicy(ScriptableObject)` — cast to `CombatTargetingPolicyAsset` or `IdlePolicyAsset` as appropriate.
   - `ResolvePolicy()` — returns current effective policy.
3. `OrchestrationCommandAdapter.cs` — subscribes to `DispatchOrchestrationCommand`. Resolves `ReceiverEntityId`, performs actor-role policy injection/constraints when applicable (role resolved on demand), then `ApplyCommand`.
4. Keep old adapters (`CombatCommandAdapter`, `IdleCommandAdapter`) functional — both subscriptions coexist during transition.

Acceptance:
1. `OrchestrationCommandAdapter` handles `DispatchOrchestrationCommand` correctly.
2. Policy injection works for both combat-origin and idle-origin commands.
3. Old adapters still function (dual adapter period).

### S3 — WorldCache + Proposals + Context Reform

Unify internal infrastructure:

1. `OrchestrationWorldCache`:
   - Add `_operatorEntityIds` populated from `FriendlyReceivers` (new unified list).
   - Add `OperatorCount`, `GetOperatorEntityId(int)`.
   - Keep old accessors as `[Obsolete]` forwarding to new lists (for routes not yet migrated).
2. `OrchestrationArbiterProposals`:
   - Add domain-neutral API: `SetCommand(domainId, command)`, `TryGetCommand(domainId, out command)`, `MarkDomainPresent(domainId)`, `HasDomain(domainId)`.
   - Add optional domain-scoped facts API (e.g. bool facts) for cross-domain arbitration signals such as combat threat presence.
   - Keep `HasCombat` / `SetCombat` / `HasIdle` / `ThreatPresent` as `[Obsolete]` forwarding wrappers to the generic API during transition.
3. `ExecutionContext`:
   - Add `OrchestrationCommand Command`.
   - Keep `CombatCommand CombatCommand` as `[Obsolete]` forwarding property.
4. `OrchestrationArbiter`:
   - Wire new proposal/context fields.

Acceptance:
1. `world.OperatorCount` matches sum of old `CombatReceiverCount + IdleReceiverCount` (minus duplicates — actors implementing both interfaces appear once), plus any explicitly registered group operators.
2. `ctx.Command` populated from `proposals.TryGetCommand(route.DomainId, out ...)`.
3. Old accessors still work via forwarding.

### S4 — Route Convergence + Target Search Migration

Migrate execution routes and domain components:

1. `StrategyCombatCombatExecutionRoute`:
   - Emit `DispatchOrchestrationCommand` instead of `DispatchCombatCommand`.
   - Iterate `world.OperatorCount` / `world.GetOperatorEntityId(i)`.
   - Use `ctx.Command` instead of `ctx.CombatCommand`.
2. `StrategyCombatIdleExecutionRoute`:
   - Emit `DispatchOrchestrationCommand` instead of `DispatchIdleCommand`.
   - `IdleCommand.Hold()` → usually no dispatch (`None` / skip publish); use `OrchestrationCommand.Cancel()` only when explicit abort/reset is required.
   - `IdleCommand.MoveToPoint(...)` → `OrchestrationCommand.Engage(OrchestrationTargetRef.Point(...))`.
   - Iterate unified operator list.
3. `CombatDomainComponent`:
   - Replace inline target search with combat provider/filter/scorer composition + `OrchestrationTargetSearch.TryFindBest(...)` / `FillTopK(...)`.
   - Propose `OrchestrationCommand.Engage(...)` instead of `CombatCommand.Create(AttackTarget, ...)`.
   - `proposals.SetCommand(CombatDomainId, ...)` instead of `proposals.SetCombat(...)`.
4. Remove old adapter bus subscriptions (`CombatCommandAdapter`, `IdleCommandAdapter`) from scene GameObjects — only `OrchestrationCommandAdapter` active.

Acceptance:
1. Routes emit only `DispatchOrchestrationCommand` (and only for non-`None` commands).
2. `OrchestrationCommandAdapter` handles all dispatch.
3. Target search produces identical results via utility.
4. No old dispatch commands flowing through bus.

### S5 — Cleanup + Delete Old Types + Tests

Remove all deprecated/transitional code:

1. Delete old types:
   - `CombatCommand.cs`, `CombatCommandType.cs`, `ICombatCommandReceiver.cs`
   - `IdleCommand.cs`, `IdleCommandType.cs`, `IIdleCommandReceiver.cs`
   - `DispatchCombatCommand.cs`, `DispatchIdleCommand.cs`
   - `CombatCommandAdapter.cs`, `IdleCommandAdapter.cs`
   - `ICombatTargetPolicySelector.cs`, `IIdlePolicySelector.cs`
2. Remove `[Obsolete]` forwarding from WorldCache, Proposals, ExecutionContext.
3. Remove old receiver lists from `OrchestrationRegistry` and `OrchestrationWorldCache`.
4. Remove dual-interface implementations from actor receivers — only `IOrchestrationCommandReceiver`.
5. Remove duplicated inline combat search methods from `CombatDomainComponent.cs` once provider/filter composition is in place (combat search config fields may remain on the component).
6. Update all architecture tests.
7. Add regression tests (see Tests section).

Acceptance:
1. No references to old types in codebase (except documentation).
2. All tests green.
3. Runtime behavior identical to pre-C06A.

---

## Tests / Gates

### Unit Tests (RuntimeHostTests or new OrchestrationCommandTests)

| # | Test |
|---|------|
| 1 | `OrchestrationCommand_Engage_EntityTarget_SetsTypeAndTarget` |
| 2 | `OrchestrationCommand_Engage_PointTarget_SetsTypeAndTarget` |
| 3 | `OrchestrationCommand_Cancel_SetsType` |
| 4 | `OrchestrationCommand_None_IsDefault` |
| 5 | `OrchestrationCommand_None_IsSentinel_NotNormallyDispatched` |
| 6 | `DispatchOrchestrationCommand_Uses_ReceiverEntityId_Only` |
| 7 | `OrchestrationCommand_None_DoesNotRequireTarget` |
| 8 | `OrchestrationTargetSearch_TryFindBest_ClosestScore_ReturnsBestTarget` |
| 9 | `OrchestrationTargetSearch_TryFindBest_NoAcceptedCandidates_ReturnsFalse` |
| 10 | `OrchestrationTargetSearch_FillTopK_RespectsMaxTargets` |
| 11 | `OrchestrationTargetSearch_FilterRejectedCandidates_AreSkipped` |
| 12 | `CombatTargetProvider_Parity_With_PreC06A_HostileSelection` |

### Architecture Tests (OrchestrationImplementationFitnessTests)

| # | Test |
|---|------|
| 13 | `C06A_RuntimeHost_OrchestrationCommand_Exists` |
| 14 | `C06A_RuntimeHost_OrchestrationTargetRef_Exists` |
| 15 | `C06A_DispatchOrchestrationCommand_Uses_ReceiverEntityId` |
| 16 | `C06A_DispatchOrchestrationCommand_HasNo_RoleId_Field` |
| 17 | `C06A_RuntimeHost_IOrchestrationCommandReceiver_Exists` |
| 18 | `C06A_RuntimeHost_OrchestrationTargetSearch_Exists` |
| 19 | `C06A_WorldCache_Has_UnifiedOperatorAccessors` |
| 20 | `C06A_Proposals_Has_DomainNeutral_CommandApi` |
| 21 | `C06A_ExecutionContext_Has_Command_Field` |
| 22 | `C06A_No_CombatCommand_References_In_Routes` |
| 23 | `C06A_No_DispatchCombatCommand_In_Codebase` |
| 24 | `C06A_No_DispatchIdleCommand_In_Codebase` |
| 25 | `C06A_Registry_Has_UnifiedCommandReceiverList` |

### Regression Tests

| # | Test |
|---|------|
| 26 | `Receiver_ApplyCommand_Engage_ProducesSameBehaviorAsOldAttackTarget` |
| 27 | `Receiver_ApplyCommand_Cancel_ProducesSameBehaviorAsOldHold` |
| 28 | `Receiver_ApplyCommand_EngagePoint_ProducesSameBehaviorAsOldMoveToPoint` |
| 29 | `UnifiedAdapter_PolicyInjection_SameAsOldCombatAdapter` |
| 30 | `UnifiedAdapter_ConstraintsInjection_SameAsOldCombatAdapter` |
| 31 | `UnifiedAdapter_SelectorOverride_SameAsOldIdleAdapter` |

---

## Risks / Tradeoffs

1. **Large migration surface** — 12+ types removed, 6+ new types, ~20 files modified. Risk mitigated by: incremental slices (each compiles independently), dual-interface transition period (S1–S4), `[Obsolete]` forwarding during migration, comprehensive test coverage.

2. **`Engage` loses specificity** — the orchestrator no longer says "attack" vs "assist" vs "keep distance". Risk mitigated by: receiver-side interpretation (`AIBrain` now, `ActorBrainPolicy`/`GroupBrainPolicy` later) chooses the approach; actor capabilities determine what is valid. Default `Engage` → attack preserves behavioral parity for current combat units.

3. **Receiver must interpret `Engage` based on capabilities** — more logic moves to the actor (receiver + AIBrain). Risk mitigated by: default `Engage` → attack mapping matches current behavior exactly. Capability-aware interpretation is additive, not required for C06A behavioral parity.

4. **Operator abstraction is unified by `EntityId`, so actor/group type is inferred at runtime** — dispatch payload no longer carries explicit operator kind. Risk mitigated by: type/role/capability resolution happens in adapter/world query seams, and C06A group semantics remain identity/addressing-only (no group brain logic yet).

5. **Policy selector unification loses type safety** — `IOrchestrationPolicySelector.SetRuntimeDefaultPolicy(ScriptableObject)` accepts any SO. Risk mitigated by: runtime type check in selector implementations; architecture tests verify correct policy types flow to correct selectors. Future step may add generic type parameter.

6. **Target search split into shared selector + domain providers/filters** — more abstractions than a direct utility copy, but avoids baking combat-only parameters (`camera`, `aggroRadius`, hostile-only predicates) into RuntimeHost. Risk mitigated by: keep S0/S4 parity tests for combat target choice, use functional extraction for combat provider/filter internals, and keep combat config fields on `CombatDomainComponent` during C06A.

7. **Two-adapter coexistence period (S2–S3)** — both old adapters and new `OrchestrationCommandAdapter` may be active simultaneously. Risk mitigated by: bus subscriptions are type-separate (`DispatchCombatCommand` vs `DispatchOrchestrationCommand`); no command duplication during transition.

---

## Decision Record (C06A Position)

1. **Intent-based commands, not behavior-specific.** `Engage` replaces `AttackTarget` + `MoveToTarget` + `Assist` + `KeepDistance` + `HideBehind`. The orchestrator expresses intent; the actor decides behavior. This is the natural consequence of C06's capability system — if actors know their capabilities, they should decide how to use them.

2. **`OrchestrationCommand` carries `TargetRef`, but does NOT carry preferred range / stop-distance / urgency hints.** Personal spacing/range/approach behavior belongs to the receiver's local decision layer (`AIBrain` today; `ActorBrainPolicy` / `GroupBrainPolicy` in `C06C/C06D`). This keeps the orchestrator strictly within "what intent + what target" scope. If arbitration later needs priority metadata, it should live in proposal/dispatch metadata, not in the universal command payload.

3. **No `Interact` type — `Engage` is universally overloaded.** "Engage" means "interact with this target per your role and capabilities." Attack, heal, escort, harvest — all are `Engage`. The alternative (separate types per interaction) recreates the CombatCommandType proliferation problem. Actor capabilities disambiguate.

4. **Single unified command-receiver list + operator-entity snapshot, not per-domain actor receiver lists.** Actors and groups register via the same `IOrchestrationCommandReceiver` contract and are addressed by `EntityId`; WorldCache snapshots operator entity ids for dispatch. Domain-specific behavior remains internal to the receiver's `ApplyCommand` implementation. This removes actor-only addressing assumptions early without introducing a tagged-union operator payload type.

5. **Target search is split into a domain-neutral selector plus domain/integration provider/filter/scorer seams.** `OrchestrationTargetSearch` remains a stateless utility in RuntimeHost, but it does not know combat-only concepts (`hostile`, `camera`, `viewport`, `aggroRadius`). Domains provide candidates and optional filters/scorers, then call the shared selector. This keeps RuntimeHost generic and lets idle/economy domains reuse the same selection pipeline.

6. **`IOrchestrationPolicySelector` uses `ScriptableObject` base type.** Both combat targeting policies and idle policies are `ScriptableObject` subclasses. The selector doesn't need to know the specific policy type — it stores and resolves a generic SO reference. Type safety is enforced by the consumer (adapter/route) when reading the resolved policy. Generic type parameter (`IOrchestrationPolicySelector<TPolicy>`) was considered but adds complexity without clear benefit at this scale.

7. **Incremental migration with `[Obsolete]` forwarding.** Old types are not deleted until S5. During S1–S4, both old and new types coexist. `[Obsolete]` attributes on old accessors guide migration without breaking compilation. This is safer than a big-bang replacement and allows slice-by-slice verification.

8. **`IConstrainedCombatReceiver` stays as-is.** Generalizing constraints to `IConstrainedReceiver` is desirable but orthogonal to command unification. The unified adapter checks for `IConstrainedCombatReceiver` specifically. A future step can introduce `IConstrainedReceiver` without blocking C06A.

9. **`Cancel` is the only explicit stop/reset command in the universal set.** `Hold` is not carried into `OrchestrationCommand`; old `Hold` behavior maps to either `Cancel` (explicit stop/reset) or no dispatch (`None`, sentinel) depending context. This avoids baking "idle/hold" behavior into the orchestration command layer and keeps "do nothing" distinct from "abort current action."

10. **Domain components keep their serialized fields/config, but RuntimeHost search API stays clean.** `CombatDomainComponent` may keep `aggroRadius`, `searchMode`, `searchCamera`, `viewportMargin` (or move them into a combat provider/filter helper) and use them inside combat provider/filter composition. These values are NOT passed as raw parameters to `OrchestrationTargetSearch`. The component owns combat configuration; RuntimeHost selector owns only generic candidate ranking/selection.

---

## Deferred Follow-Up After C06A (Planned)

To reach the architecture target discussed during C06A review, C06A is intentionally split from the following follow-up steps:

1. **`C06B` — TargetProvider/DomainComponent convergence + reservations on top of `TargetRef`**
   - Expand producer/resolver usage of `TargetRef` (`Entity | Point | Area | Route`) across domains (beyond combat actor search and point move commands).
   - Unify target search/selection contracts around generic targets (not combat-only actor targets).
   - Introduce a universal `DomainTargetSet` (domain-neutral target candidate/selection carrier) so `CombatDomainComponent` and `IdleDomainComponent` can converge on the same domain-output pipeline shape instead of one-off combat target-set handling.
   - Add target reservation / slot semantics for contested economic targets (tree, mine, building interaction slots).
   - Reform `DomainComponent` and `TargetProvider` seams so domains provide "what target" consistently without leaking execution behavior.
   - Converge `CombatDomainComponent` and `IdleDomainComponent` to a domain-neutral domain-output format (same proposal/output shape and evaluation contract semantics), removing the current asymmetry where combat emits a domain command while idle only marks domain presence and relies on route-side generation.
   - Architecture analysis/decision for post-convergence seam ownership: after `DomainComponent` + `DomainTargetSet` convergence, evaluate whether `DomainTargetSet` should be reusable across domains (default target-candidate carrier) and whether `DomainComponent` should remain a separate seam or collapse into `DomainOrchestrator`/`DomainOrchestratorComponent`. Record and implement one chosen direction in `C06B` (no long-lived half-state).
   - **Exit gate before `C06C`: remove remaining `ExecutionContext` migration artifacts** — `ExecutionContext.Anchor` MUST be removed (or replaced by a domain-neutral equivalent). `ExecutionContext.CombatCommand` was removed during `C06A`; group-commanding (`C06C`) must not reintroduce combat/idle-era execution payload assumptions.
   - **Exit gate before `C06C`: clean `OrchestrationWorldCache` ownership/scope** — `OrchestrationWorldCache` MUST stop mixing domain-specific receiver snapshots (`Combat*` / `Idle*`) with unified operator dispatch snapshots and unrelated crowd/spatial concerns in one compatibility-heavy surface. At minimum: remove domain-specific receiver lists/accessors from the active path and consolidate around a clean operator snapshot API; any remaining crowd/spatial/query responsibilities should be explicitly separated or clearly bounded.
   - **Exit gate before `C06C`: resolve `CombatTargetSet.cs` ownership (`RuntimeHost` domain leak)** — `Packages/com.morboo.runtimehost/Runtime/Orchestration/World/CombatTargetSet.cs` MUST no longer remain an unresolved StrategyCombat-specific type in `RuntimeHost`. `C06B` must either move it to the StrategyCombat integration package (domain-owned) or replace it with the universal `DomainTargetSet` (domain-neutral carrier) plus optional StrategyCombat-specific extensions.

2. **`C06C` — Group commanding / group brain on top of C06A operator addressing**
   - C06A already establishes operator-level addressing in dispatch infrastructure (actors and groups are command-targetable operator entities identified by `EntityId`).
   - `C06C` adds group behavior semantics: formation mode selection, slot assignment, cohesion/sync rules, and fan-out into member intents.
   - Group spacing / anti-clump behavior is modeled as `GroupBrainPolicy`, not orchestration command payload.

3. **`C06D` — New actor/group brain seam parallel to TDE**
   - Introduce a game-owned brain/policy system analogous to TDE `AIBrain`, integrated with `CommandBus`/`EventBus`/orchestration.
   - Run in parallel with existing TDE/AIBrain during rollout to validate policy storage and intent interpretation.
   - Move actor-local execution preferences (`preferred range`, approach style, stop distance) behind the new seam without requiring immediate full TDE removal.

4. **`C06E` — Operator-scoped arbitration (`operator -> proposals[] -> chosen command`) + `ThreatPresent`/`ArbitrationInput` removal**
   - Replace world-scoped single-active-domain arbitration (`Combat` vs `Idle`) with operator-scoped proposal resolution (actors/groups can receive different domain commands in the same tick).
   - Remove global `ThreatPresent` from orchestration arbitration contracts and logic (`IArbiter` proposal-list overload input semantics + `ArbitrationInput` legacy compatibility model in Framework).
   - Replace domain-specific proposal assumptions (`CombatPrimary`/`IdleDefault` semantics and hardcoded primary-vs-fallback interpretation) with per-operator conflict resolution policy.
   - **Hysteresis placement rule:** domain-level "engagement continuity" may stay in domain producers during `C06B/C06C` prep; actor-local/group-local execution hysteresis belongs in the new brain seam (`C06D`); arbitration retains only conflict-resolution hysteresis if truly cross-domain/per-operator and domain-agnostic.
   - **Preparation dependency on `C06B`:** domain output convergence (`CombatDomainComponent` and `IdleDomainComponent` same output semantics), `TargetRef`/TargetProvider convergence, and removal of route-side idle-only command synthesis assumptions.
   - **Preparation dependency on `C06C`:** operator/group commanding semantics must exist first (group is an operator entity), so arbitration can resolve conflicts per operator (actor or group) without reworking payload/addressing again.
   - **Coordination dependency on `C06D`:** finalize hysteresis ownership and brain-policy responsibilities before deleting `ThreatPresent`, to avoid moving stickiness logic twice.
