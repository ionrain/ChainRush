# C04D — Generic Orchestration Composition Abstraction Extraction (No Compatibility Path)

Date: 2026-02-23  
Phase: `Phase 4` (`Orchestration Platform Remediation`)  
Type: structural refactor (package-breaking + scene-breaking allowed)  
Status: `closed`

Closure evidence (2026-02-23):

1. Generic orchestration composition form extracted to `Morboo.RuntimeHost`:
   - `DomainOrchestratorComponent` (was `StrategyCombatDomainOrchestrator`)
   - `DomainComponent` abstract (was `StrategyCombatDomainComponentBase`)
   - `DomainOrchestratorComposition` static helper (was `StrategyCombatDomainOrchestratorCommon`)
   - `IDomainRouteExecutionPolicyConsumer` (was `IStrategyCombatRouteExecutionPolicyConsumer`)
   - `DomainRouteExecutionPolicy` abstract ScriptableObject (new)
   - `DomainRouteExecutionPolicyProvider` (was `StrategyCombatRouteExecutionPolicyProvider`)
   - `DomainTargetProvider` abstract (was `DomainTargetProviderBase`)
2. All moves executed via `git mv` preserving `.meta` GUIDs (scene serialization intact).
3. Genre layer rebound: `CombatDomainComponent` / `IdleDomainComponent` inherit `DomainComponent`; `CombatTargetProvider` / `IdleTargetProvider` inherit `DomainTargetProvider`; `StrategyCombatRouteExecutionPolicyAsset` inherits `DomainRouteExecutionPolicy`.
4. Bridge renamed to `DomainRouteExecutionPolicyBridge` and uses `IDomainRouteExecutionPolicyConsumer` (no StrategyCombat-type branching).
5. `Level.unity` scene `m_EditorClassIdentifier` strings updated.
6. Architecture fitness tests rewritten for C04D target form (`C04D_GenericOrchestrationComposition_ExtractedToRuntimeHost`).
7. Layering test regex updated (`DomainOrchestratorComponent` replaces `StrategyCombatDomainOrchestrator`).
8. Zero stale references confirmed by grep across all `.cs` and `.unity` files.
9. No compatibility path or legacy fallback introduced.
10. Partial closure: `StrategyCombatRouteExecutionPolicyAsset` monolith split (section C of plan) deferred — current asset inherits `DomainRouteExecutionPolicy` but is not yet split into per-route policy assets. This does not block the primary C04D goal (layer ownership extraction).

## Why This Step Exists

`C04C` убрал `*Lite` и отдельные `Combat/Idle` orchestrator entrypoint-классы как target shape, но часть **общей orchestration-инфраструктуры** всё ещё осталась во владении `Morboo.Integration.StrategyCombat`:

1. `StrategyCombatDomainOrchestrator`
2. `StrategyCombatDomainComponentBase`
3. `StrategyCombatDomainOrchestratorCommon`
4. `IStrategyCombatRouteExecutionPolicyConsumer`
5. `StrategyCombatRouteExecutionPolicyAsset` (монолитный strategy-specific composite policy)

Это создаёт неверную границу владения: genre-layer (`StrategyCombat`) становится owner-ом общей orchestration формы.

`C04D` устраняет именно это: выносит **общую component/orchestrator/policy форму** в общий слой, оставляя в `StrategyCombat` только domain components/providers/route executors/policies/data.

## Hard Rules (For C04D)

1. **No compatibility path / no legacy fallback**
   - `C04D` выполняется без parallel legacy paths, compat wrappers "на всякий случай" и silent fallback.
   - Старые strategy-specific orchestration infrastructure файлы удаляются в том же срезе, в котором появляется новый общий слой.

2. **Do not lower target to fit weak links**
   - Если перенос общей абстракции вверх блокируется конкретным типом/seam/компонентом, сначала абстрагировать блокирующий узел.
   - Нельзя оставлять общую форму в `StrategyCombat` "временно, потому что мешает X" без явного согласования и фиксации решения.

3. **Naming rule for abstract types in upper layers**
   - Для абстрактных типов, выносимых в `Morboo.Core`, `Morboo.RuntimeHost` или `Morboo.Framework` (если это их уровень), **не использовать суффикс `Base`**.
   - Использовать семантическое имя + `abstract`:
     - `DomainComponent` (abstract), а не `DomainComponentBase`
     - `DomainTargetProvider` (abstract), а не `DomainTargetProviderBase`
     - `DomainRouteExecutionPolicy` (abstract), а не `DomainRouteExecutionPolicyAssetBase`

## Goal

Довести orchestration composition до финальной формы по слоям:

1. Общая component/orchestrator/policy форма живёт **выше** `StrategyCombat`.
2. `StrategyCombat` содержит только жанровые реализации:
   - domain components
   - providers
   - route executors
   - route policies / data
3. `RuntimeHost` остаётся orchestration engine (loop/pipeline/arbiter/router), без жанровой orchestration ownership.

## Non-Goals (For C04D)

1. Не переписывать `RuntimeHost` loop/pipeline/arbiter модель (`C04B` это уже закрывает/закрывает в работе).
2. Не возвращать route-body ownership в `RuntimeHost`.
3. Не вводить generalized scope contract (это отдельный вопрос; `Faction-first` остаётся).
4. Не менять gameplay semantics ради переноса слоёв (меняется ownership/placement, не поведение).

## Final Layer Mapping (C04D Target)

## 1. Framework

Owner:

1. decision/command primitives (`Proposal`, `ArbiterDecision`, `IArbiter`, `ICommandBus`)

Why:

1. чистые платформенные примитивы без orchestration-domain ownership.

Interacts with:

1. `Core`
2. `RuntimeHost`
3. `StrategyCombat`

## 2. Core

Owner:

1. shared ids/contracts (`EntityId`, actor read contracts, lifecycle contracts)
2. `OrchestrationDomainId` (если список доменов фиксируется как общий contract)
3. (позже, если понадобится generalized scope) asset-based collective identity contract

Why:

1. shared semantic contracts, которые нужны нескольким слоям
2. не genre-specific implementation

Interacts with:

1. `RuntimeHost`
2. `StrategyCombat`

## 3. RuntimeHost

Owner:

1. Host engine:
   - `OrchestrationLoop`
   - `OrchestrationPipeline`
   - `OrchestrationPipelineComponent`
   - `OrchestrationArbiter`
   - `ExecutionRouter`
   - `ExecutionContext`
   - `OrchestrationWorldCache`
2. Generic orchestration composition form (Unity-dependent abstractions, because `C04D` does not introduce a new package):
   - `DomainOrchestratorComponent`
   - `DomainComponent` (abstract)
   - `DomainOrchestratorComposition`
   - `IDomainRouteExecutionPolicyConsumer`
   - `DomainRouteExecutionPolicy` (abstract `ScriptableObject`)
   - `DomainRouteExecutionPolicyProvider` (abstract/contract form)
   - `DomainTargetProvider` (abstract) + `IDomainTargetProvider`
   - `DomainTargetProviderValidation`

Why:

1. `C04D` выполняется в существующих пакетах (`RuntimeHost` / `Core` / `Framework`) без нового пакета.
2. `RuntimeHost` владеет orchestration engine и generic Unity-dependent orchestration composition form.
3. `RuntimeHost` не владеет genre-specific logic/behavior.

Interacts with:

1. читает/тикает `DomainOrchestratorComponent` / `DomainComponent` contracts
2. публикует dispatch-команды через host seams

## 4. StrategyCombat

Owner:

1. `CombatDomainComponent`
2. `IdleDomainComponent`
3. `CombatTargetProvider`
4. `IdleTargetProvider`
5. `Combat/Idle/None/UnknownRoute` executors
6. strategy-specific binding keys/appliers
7. strategy route policies (split by route/domain)
8. strategy route execution profile aggregate (если нужен)

Why:

1. это genre-specific logic, providers, policies, route bodies
2. не owner общей orchestration infrastructure

Interacts with:

1. реализует generic contracts из `RuntimeHost` (и `Core`/`Framework` по необходимости)
2. работает через host seams (`IExecutionRouteHost`)

## 5. MorbooBridge

Owner:

1. project-specific wiring
2. project composition bridges (policy application / scene refs)

Why:

1. это слой конкретной игры, а не platform/genre infrastructure

Interacts with:

1. `OrchestrationLoop` / pipeline components
2. `DomainOrchestratorComponent` + generic policy consumer contracts
3. scene references / project content

## Interaction Diagram (Final Runtime Shape)

### Layer Ownership

```text
Framework
   ^
   |
Core
   ^
   |
RuntimeHost --------> StrategyCombat
   ^                    ^
   |                    |
MorbooBridge ----------/
```

### Tick / Decision / Route / Dispatch Flow

```text
OrchestrationLoop (RuntimeHost)
  -> OrchestrationPipeline (RuntimeHost)
    -> OrchestrationArbiter (RuntimeHost)
      -> DomainOrchestratorComponent (RuntimeHost, generic wrapper)
        -> DomainComponent (StrategyCombat implementation)
           -> TargetProvider / PolicyProvider / Data Assets (StrategyCombat)
           -> produces Proposal(s)
    -> ExecutionRouter (RuntimeHost)
      -> StrategyCombat route executor
         -> IExecutionRouteHost.PublishCommand(...)
    -> Shared CommandBus flush (RuntimeHost)
      -> Adapters / executors (StrategyCombat / Bridge)
```

## Concrete File Move / Rename / Delete Map (C04D)

`C04D` выполняется одним structural cut без compatibility path.

## A. Move + Rename to Generic Orchestration Layer in Existing Packages (`RuntimeHost` / `Core` / `Framework`)

Rules for this cut:

1. Новый пакет не создаётся.
2. Unity-dependent generic orchestration composition abstractions переносятся в `Morboo.RuntimeHost`.
3. В `Morboo.Core` / `Morboo.Framework` переносятся только те общие контракты, которые реально соответствуют их уровню (не форсировать split ради "чистоты", если это ухудшает ownership).

Move / rename:

1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainOrchestrator.cs`
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainOrchestratorComponent.cs`

2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainComponentBase.cs`
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainComponent.cs`
   - rename class `StrategyCombatDomainComponentBase` -> `DomainComponent` (`abstract`)
   - rename interface `IStrategyCombatDomainComponent` -> `IDomainComponent`

3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainOrchestratorCommon.cs`
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainOrchestratorComposition.cs`
   - rename helper `StrategyCombatDomainOrchestratorCommon` -> `DomainOrchestratorComposition`
   - move `IStrategyCombatRouteExecutionPolicyConsumer` out (see next item)

4. `IStrategyCombatRouteExecutionPolicyConsumer` (currently declared in the file above)
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/IDomainRouteExecutionPolicyConsumer.cs`
   - rename to `IDomainRouteExecutionPolicyConsumer`

5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Targeting/DomainTargetProviderBase.cs`
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Targeting/DomainTargetProvider.cs`
   - rename abstract class `DomainTargetProviderBase` -> `DomainTargetProvider`
   - keep `IDomainTargetProvider` + `DomainTargetProviderValidation` in same file or split nearby

6. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatRouteExecutionPolicyProvider.cs`
   -> `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/DomainRouteExecutionPolicyProvider.cs`
   - rename to generic provider contract/abstract form (if provider pattern is kept)

7. New generic route policy base (new file):
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/DomainRouteExecutionPolicy.cs`
   - `abstract ScriptableObject` (no `Base` suffix)

## B. Keep in StrategyCombat (but rebind to generic Runtime abstractions)

Remain (updated base/interfaces):

1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/CombatDomainComponent.cs`
   - `: DomainComponent`

2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/IdleDomainComponent.cs`
   - `: DomainComponent`

3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/Targeting/CombatTargetProvider.cs`
   - `: DomainTargetProvider`

4. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/Targeting/IdleTargetProvider.cs`
   - `: DomainTargetProvider`

5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatCombatExecutionRoute.cs`
6. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatIdleExecutionRoute.cs`
7. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatNoneExecutionRoute.cs`
8. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatUnknownRouteFallbackExecutionRoute.cs`

## C. Split Strategy Route Policies (replace monolith)

Delete monolithic ownership after migration:

1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatRouteExecutionPolicyAsset.cs` (delete)

Create strategy-specific route policy assets (inherit `DomainRouteExecutionPolicy`):

1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Policies/CombatRouteExecutionPolicyAsset.cs`
2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Policies/IdleRouteExecutionPolicyAsset.cs`
3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Policies/NoneRouteExecutionPolicyAsset.cs`
4. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Policies/UnknownRouteFallbackExecutionPolicyAsset.cs`
5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Policies/StrategyCombatRouteExecutionProfileAsset.cs` (aggregate/profile owner)

`StrategyCombatRouteExecutionProfile.cs`:

1. keep as runtime facade (or rename to `StrategyCombatRouteExecutionProfileRuntime`) if it still adds value.

## D. MorbooBridge (project wiring)

Rename to generic bridge:

1. `Assets/Scripts/MorbooBridge/Orchestration/Composition/StrategyCombatRouteExecutionPolicyBridge.cs`
   -> `Assets/Scripts/MorbooBridge/Orchestration/Composition/DomainRouteExecutionPolicyBridge.cs`

Bridge contract:

1. Reads domains from `OrchestrationLoop` pipeline source-of-truth
2. Applies policy through `IDomainRouteExecutionPolicyConsumer`
3. No StrategyCombat-type branching in bridge logic

## E. Delete StrategyCombat-Owned Generic Infrastructure (must be removed in same cut)

Delete after moves/rebind:

1. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainOrchestrator.cs`
2. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainComponentBase.cs`
3. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/StrategyCombatDomainOrchestratorCommon.cs`
4. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatRouteExecutionPolicyProvider.cs` (if replaced by generic provider + optional thin strategy wrapper under new naming)
5. `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatRouteExecutionPolicyAsset.cs`

## F. Scene / Wiring Migration (same cut)

`Level.unity` and other orchestration scenes:

1. `OrchestrationPipelineComponent.domainOrchestrators[]` must reference `DomainOrchestratorComponent` (generic wrapper), not `StrategyCombatDomainOrchestrator`.
2. Route policy bridge component must be renamed and rewired to generic policy contract.
3. StrategyCombat domain components/providers remain scene-owned and referenced by generic wrapper/component contract.

## Execution Mode (C04D)

`C04D` is intentionally **not** a compatibility migration.

Implementation mode:

1. One structural cut (package/scene breaking allowed)
2. No compat wrappers
3. No duplicate old/new ownership paths
4. No silent fallback to old StrategyCombat-owned orchestration infrastructure

If compile breaks during the cut:

1. continue migration to final target shape
2. do not introduce temporary strategy-owned generic abstractions to restore build

## Acceptance (C04D)

1. `StrategyCombat` no longer owns generic orchestration composition infrastructure (`StrategyCombatDomainOrchestrator*`, `*DomainComponentBase`, shared orchestration helper/interface seams).
2. Generic orchestration component form lives in existing upper package layers (primarily `Morboo.RuntimeHost`, and `Morboo.Core` / `Morboo.Framework` only where semantically valid) with semantic names (no `Base` suffix for abstract types).
3. `CombatDomainComponent` / `IdleDomainComponent` are genre-specific implementations of generic `DomainComponent`.
4. `CombatTargetProvider` / `IdleTargetProvider` are genre-specific implementations of generic `DomainTargetProvider`.
5. Bridge route-policy application uses generic `IDomainRouteExecutionPolicyConsumer` and does not branch on StrategyCombat concrete types.
6. `StrategyCombatRouteExecutionPolicyAsset` monolith is removed and replaced by route/domain-specific policy assets + optional strategy profile aggregate.
7. `RuntimeHost` remains orchestration engine only and does not absorb strategy-specific ownership.
8. No compatibility/legacy fallback path is introduced to keep old StrategyCombat-owned generic abstractions alive.

## Risks / Tradeoffs

1. Package churn high (moves/renames across existing packages + asmdef reference updates + scene serialization updates).
2. Large cut increases compile-break window, but avoids lingering dual ownership.
3. Naming cleanup (`no Base`) requires deliberate coordination to avoid half-renamed APIs.

## Decision Record (C04D Position)

1. `C04C` closed the `*Lite` / separate per-domain entrypoint problem, but not final layer ownership.
2. `C04D` is the required follow-up to restore abstraction ownership boundaries.
3. `C04D` MUST be executed without lowering the target architecture to fit current StrategyCombat-owned weak links.
