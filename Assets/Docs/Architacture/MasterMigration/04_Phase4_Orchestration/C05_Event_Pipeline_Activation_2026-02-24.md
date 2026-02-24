# C05 — Event Pipeline Activation

Date: 2026-02-24
Phase: `Phase 4` (`Orchestration Platform Remediation`)
Type: feature-complete (platform seam activation + bus provider decoupling)
Status: `closed`

## Why This Step Exists

Orchestration pipeline уже имеет полноценный command dispatch path (`InProcessCommandBus` → queue → flush → adapters), но **не публикует доменные события**. Вся инфраструктура для событий подготовлена как Phase 2B seam:

1. `IEventBus` контракт в `com.morboo.framework` — готов.
2. `IDomainEvent` marker interface — готов.
3. `InProcessEventBus` реализация в `com.morboo.systems` — готова, но **не используется**.
4. `ExecutionResult.EventCount` — всегда возвращает `0` (зарезервирован под события).
5. Future-gate тест `FutureGate_RuntimePipeline_UsesDomainEvents` — `[Ignore]` с пометкой "Enable after C05".

Без event pipeline integration/bridge/UI слои вынуждены либо напрямую опрашивать pipeline state, либо использовать ad-hoc callbacks для реакции на orchestration lifecycle переходы (mode change, threat state). Это создаёт implicit coupling и мешает добавлению новых потребителей.

Дополнительная проблема: текущие command adapters (`CombatCommandAdapter`, `IdleCommandAdapter`) напрямую зависят от `OrchestrationLoop` через `[SerializeField] OrchestrationLoop`. Это создаёт coupling на конкретный pipeline owner, хотя `ICommandBus` и `IEventBus` — Framework-level абстракции, не принадлежащие orchestration. Любая система, владеющая bus instance, должна иметь возможность экспонировать его подписчикам через единый контракт.

## Goal

1. Ввести `IEventBusProvider` / `ICommandBusProvider` в Framework для декаплинга bus consumers от конкретных bus owners.
2. `InProcessEventBus` становится активным участником pipeline tick (наравне с `InProcessCommandBus`).
3. Минимальный набор `IDomainEvent` типов покрывает ключевые orchestration lifecycle переходы.
4. Ввести `IDomainEventHandler<T>` typed contract и `EventBusSubscriber` MonoBehaviour base для единообразной подписки (универсальный, не orchestration-specific).
5. Есть как минимум один runtime publisher и один runtime subscriber.
6. ~~Существующие command adapters рефакторятся на `ICommandBusProvider`.~~ Отложено: `ICommandBus` не имеет `Subscribe`/`Unsubscribe`; adapters также требуют `OrchestrationLoop` для `CurrentExecContext`/`CurrentWorld` (нужен `IOrchestrationContextProvider`). `OrchestrationLoop` implements `ICommandBusProvider` — новые consumers могут использовать.
7. Future-gate тест `FutureGate_RuntimePipeline_UsesDomainEvents` включен и зелёный.

## Non-Goals (For C05)

1. Не переписывать command dispatch path (он работает).
2. Не вводить event sourcing / event replay / event persistence.
3. Не мигрировать существующие `MMEventManager`-based game events на `IEventBus` (это разные системы: `MMEventManager` — Unity/game-level, `IEventBus` — orchestration domain-level).
4. Не вводить cross-pipeline event routing (события публикуются и потребляются в рамках одного pipeline scope).
5. Не реализовывать Tier 3 события (см. секцию "Event Tier Classification").
6. Не вводить `IOrchestrationContextProvider` — command adapters продолжают читать `CurrentExecContext` / `CurrentWorld` через `OrchestrationLoop` (отдельная orchestration-specific concern, не bus concern). Декаплинг orchestration context — отдельный scope.
7. Не заменять route executor direct `ArbiterDecision` passing на EventBus — route executors являются частью execution path и должны получать decision + world + ctx синхронно в рамках того же tick для эмиссии cross-domain Hold команд.

## Hard Rules (For C05)

1. **Deferred dispatch (queue + flush)** — единообразие с `InProcessCommandBus`.
   - Текущий `InProcessEventBus` использует синхронный dispatch. `C05` переводит его на deferred model: события накапливаются в очередь во время tick, `eventBus.Flush()` вызывается **после** `commandBus.Flush()`.
   - Порядок: команды уходят адаптерам первыми, потом подписчики событий узнают что произошло.

2. **Multi-handler per event type** — текущая реализация перезаписывает handler при повторной подписке (`_handlers[typeof(TEvent)] = handler`). `C05` поднимает до списка делегатов.
   - Event bus по определению предполагает несколько получателей.

3. **Zero allocation on publish path** — никаких LINQ, boxing, или per-frame allocations в publish/flush hot path.
   - `PERF:` событие — struct; очередь — pre-allocated list; flush — linear scan.

4. **Events are notifications, not commands** — подписчики не модифицируют pipeline state. Events сообщают "что произошло", а не "что нужно сделать".

5. **Event types live in `RuntimeHost`** — orchestration lifecycle events принадлежат host-уровню (не `Framework`, не `StrategyCombat`). `IDomainEvent` marker остаётся в `Framework`.

6. **No domain-specific event payloads in RuntimeHost** — если событие несёт domain-specific данные (конкретный `CombatCommand` payload), оно определяется в `StrategyCombat`, а не в `RuntimeHost`.

7. **Bus provider interfaces in Framework** — `IEventBusProvider` и `ICommandBusProvider` живут в `com.morboo.framework` рядом с `IEventBus` / `ICommandBus`. Любой bus owner (не только `OrchestrationLoop`) может реализовать provider interface.

## Event Tier Classification

### Tier 1 — Обязательные (C05 scope)

| Event | Trigger | Payload | Owner |
|-------|---------|---------|-------|
| `OrchestrationModeChangedEvent` | `decision.ModeChanged == true` после арбитрации | `PreviousDomain: OrchestrationDomainId`, `CurrentDomain: OrchestrationDomainId`, `Timestamp: float` | `RuntimeHost` |
| `OrchestrationTickExecutedEvent` | После `router.Execute` + command bus flush | `Domain: OrchestrationDomainId`, `CommandCount: int`, `Faction: FactionAsset`, `Timestamp: float` | `RuntimeHost` |

### Tier 2 — Опциональные (C05 scope, если не усложняют)

| Event | Trigger | Payload | Owner |
|-------|---------|---------|-------|
| `ThreatStateChangedEvent` | `ThreatPresent` flip (false↔true) в arbiter | `ThreatPresent: bool`, `Timestamp: float` | `RuntimeHost` |
| `DomainProposalArbitratedEvent` | После `Arbitrate()` | `SelectedDomain: OrchestrationDomainId`, `ProposalCount: int`, `ModeChanged: bool`, `Timestamp: float` | `RuntimeHost` |

### Tier 3 — За пределами C05 (явно не реализуем)

| Event | Trigger | Причина отложения |
|-------|---------|-------------------|
| `CombatTargetSetResolvedEvent` | После domain targeting | Domain-specific payload; требует стабилизации target-provider формы после `C04D` |
| `PolicyBindingsRefreshedEvent` | После policy map refresh в arbiter | Зависит от финальной формы policy binding seam; преждевременно до `C06` (capabilities integration) |
| `EntityLifecycleChangedEvent` | Изменение `EntityLifecycleState` актора | Принадлежит Entity backbone (Phase 3), не orchestration pipeline |
| `PipelineScopeChangedEvent` | Смена faction/scope на pipeline | Нет runtime use-case: scope фиксируется при build и не меняется mid-tick |

Tier 3 события будут рассмотрены в рамках `C06` (Capabilities Integration) и `C07` (Remove Domain Downcasts) по мере стабилизации соответствующих seams.

## Prerequisite State (Before C05)

1. `C04D` closed — generic orchestration composition in `RuntimeHost` (done).
2. `C04B` host/path migration in progress — multi-pipeline model active.
3. `InProcessEventBus` exists but unused.
4. `InProcessCommandBus` deferred dispatch pattern is the reference model.

## Final Layer Mapping (C05 Target)

### 1. Framework

Owns (new + unchanged):
1. `IEventBus` — publish contract (unchanged).
2. `IDomainEvent` — marker interface (unchanged).
3. `ICommandBus` — command publish contract (unchanged).
4. `IEventBusProvider` — **new**: bus instance provider interface for decoupled subscriber access.
5. `ICommandBusProvider` — **new**: bus instance provider interface for decoupled subscriber access.
6. `IDomainEventHandler<TEvent>` — **new**: typed handler contract for domain event consumers (no Unity dependency, usable in tests and non-MonoBehaviour services).

### 2. Systems

Owns (upgraded):
1. `InProcessEventBus` — upgraded to multi-handler + deferred dispatch (queue + flush).

### 3. RuntimeHost

Owns (new + updated):
1. Orchestration lifecycle event structs: `OrchestrationModeChangedEvent`, `OrchestrationTickExecutedEvent`, и опционально `ThreatStateChangedEvent`, `DomainProposalArbitratedEvent`.
2. Event publish points inside pipeline tick.
3. `EventBus` instance ownership в `OrchestrationPipeline` (аналогично `CommandBus`).
4. `OrchestrationLoop` — **updated**: implements `IEventBusProvider`, `ICommandBusProvider`.
5. `EventBusSubscriber` — **new**: universal abstract `MonoBehaviour` base для scene-level event subscribers (depends on `IEventBusProvider`, not `OrchestrationLoop`; not orchestration-specific — any system can use).

Does NOT own:
1. Event subscribers (integration/bridge layer).
2. Domain-specific event types.
3. Bus provider interfaces (Framework owns them).

### 4. StrategyCombat

Deferred (S5):
1. `CombatCommandAdapter` — **not refactored**: `ICommandBus` has no `Subscribe`/`Unsubscribe`; adapters also require `OrchestrationLoop` for `CurrentExecContext`/`CurrentWorld`. Meaningful decoupling requires `IOrchestrationContextProvider` (separate scope).
2. `IdleCommandAdapter` — same as above.
3. `OrchestrationLoop` implements `ICommandBusProvider` — new consumers can use the provider interface.

### 5. MorbooBridge

Owns (new):
1. `ModeChangeDebugSubscriber` — **new**: proof-of-integration event subscriber (inherits `EventBusSubscriber`).

## Tick Sequence (C05 Target)

```text
OrchestrationPipeline.Tick(now)
  │
  ├─ 1. Arbiter.ProduceTick(now)
  │     ├─ BuildWorldCache
  │     ├─ CollectProposals (domains write proposals)
  │     ├─ Arbitrate (pure decision + hysteresis)
  │     │   └─ [PUBLISH] OrchestrationModeChangedEvent  (if decision.ModeChanged)
  │     │   └─ [PUBLISH] ThreatStateChangedEvent        (if threatPresent flipped) (Tier 2)
  │     │   └─ [PUBLISH] DomainProposalArbitratedEvent  (always, Tier 2)
  │     └─ return OrchestrationTickResult
  │
  ├─ 2. Router.Execute(decision, world, ctx)
  │     └─ route executors emit commands → CommandBus (queued)
  │
  ├─ 3. DispatchContextSink (set CurrentWorld/CurrentExecContext)
  │
  ├─ 4. CommandBus.Flush()         ← команды уходят адаптерам
  │
  ├─ 5. [NEW] EventBus.Flush()    ← события уходят подписчикам
  │     └─ [PUBLISH] OrchestrationTickExecutedEvent (after command flush, before event flush)
  │
  └─ done
```

IMPORTANT: `EventBus.Flush()` вызывается строго **после** `CommandBus.Flush()`. Это гарантирует, что адаптеры уже получили и применили команды к тому моменту, когда event-подписчики реагируют на "что произошло".

## Migration Plan (Execution Slices)

### S1 — Bus Provider Interfaces + Typed Event Handler Contract (Framework)

Changes:

1. Создать `IEventBusProvider` в `Packages/com.morboo.framework/Runtime/Execution/IEventBusProvider.cs`:
   ```csharp
   public interface IEventBusProvider
   {
       IEventBus EventBus { get; }
   }
   ```
2. Создать `ICommandBusProvider` в `Packages/com.morboo.framework/Runtime/Execution/ICommandBusProvider.cs`:
   ```csharp
   public interface ICommandBusProvider
   {
       ICommandBus CommandBus { get; }
   }
   ```
3. Создать `IDomainEventHandler<TEvent>` в `Packages/com.morboo.framework/Runtime/Execution/IDomainEventHandler.cs`:
   ```csharp
   public interface IDomainEventHandler<TEvent> where TEvent : struct, IDomainEvent
   {
       void HandleEvent(TEvent domainEvent);
   }
   ```

Acceptance:

1. Interfaces компилируются.
2. Нет зависимости на Unity API (чистый C#).
3. Нет зависимости на конкретные bus implementations.

### S2 — Upgrade InProcessEventBus to Deferred Multi-Handler

Changes:

1. `InProcessEventBus` (`Packages/com.morboo.systems/Runtime/Messaging/InProcessEventBus.cs`):
   - Заменить `Dictionary<Type, Delegate>` single-handler на `Dictionary<Type, List<Delegate>>` multi-handler (или аналог без boxing).
   - Добавить внутреннюю очередь событий (аналогично `InProcessCommandBus._queues`).
   - Добавить `Flush()` метод для deferred dispatch.
   - `Publish<TEvent>(...)` ставит событие в очередь, **не** вызывает handlers.
   - `Flush()` проходит очередь и вызывает все handlers для каждого события.
2. `Flush()` остаётся implementation detail (аналогично `InProcessCommandBus`, где `ICommandBus` не имеет `Flush()`). `IEventBus` не меняется.
3. Добавить unit-тесты для `InProcessEventBus`: multi-handler, deferred dispatch, flush ordering, unsubscribe.

Acceptance:

1. `InProcessEventBus` поддерживает несколько подписчиков на один тип события.
2. `Publish` не вызывает handlers немедленно — только `Flush`.
3. Unit-тесты зелёные.
4. Zero per-frame allocation на publish path (struct events, pre-allocated queues).

### S3 — Define Orchestration Lifecycle Event Structs

Changes:

1. Создать файлы событий в `Packages/com.morboo.runtimehost/Runtime/Orchestration/Events/`:
   - `OrchestrationModeChangedEvent.cs`
   - `OrchestrationTickExecutedEvent.cs`
   - (Tier 2, опционально) `ThreatStateChangedEvent.cs`
   - (Tier 2, опционально) `DomainProposalArbitratedEvent.cs`
2. Все события — `struct : IDomainEvent`.
3. Payload — value types only, без ссылок на mutable state.

Acceptance:

1. Event structs компилируются.
2. Реализуют `IDomainEvent`.
3. Payload не содержит ссылочных типов на mutable pipeline state (допускаются `ScriptableObject` asset refs как immutable identity, например `FactionAsset`).

### S4 — Wire EventBus Into Pipeline Tick + OrchestrationLoop Implements Providers

Changes:

1. `OrchestrationPipeline`:
   - Добавить `InProcessEventBus _eventBus` field (аналогично `_commandBus`).
   - Expose `EventBus` property для subscriber registration.
   - В `Tick()`: после `_commandBus.Flush()` вызывать `_eventBus.Flush()`.
   - Перед event flush: publish `OrchestrationTickExecutedEvent` в event bus.
2. `OrchestrationArbiter` (или helper):
   - Получить `IEventBus` reference (inject через constructor/setter).
   - После `Arbitrate()`: если `decision.ModeChanged`, publish `OrchestrationModeChangedEvent`.
   - (Tier 2) Если `threatPresent` flipped, publish `ThreatStateChangedEvent`.
   - (Tier 2) Publish `DomainProposalArbitratedEvent` после каждой арбитрации.
3. `OrchestrationLoop`:
   - Реализовать `IEventBusProvider` и `ICommandBusProvider`.
   - `IEventBusProvider.EventBus` → возвращает primary pipeline event bus.
   - `ICommandBusProvider.CommandBus` → возвращает текущий `CommandBus` (уже публичный).
   - Обновить pipeline creation: передавать `InProcessEventBus` instance.
4. Обновить `ExecutionResult.EventCount` — заполнять реальным количеством опубликованных событий за tick.

Acceptance:

1. `OrchestrationPipeline.Tick()` вызывает `eventBus.Flush()` после `commandBus.Flush()`.
2. `OrchestrationModeChangedEvent` реально публикуется при смене домена.
3. `OrchestrationTickExecutedEvent` публикуется каждый non-skipped tick.
4. `OrchestrationLoop` реализует `IEventBusProvider` и `ICommandBusProvider`.
5. `ExecutionResult.EventCount` отражает реальное количество событий.

### S5 — Refactor Command Adapters to ICommandBusProvider (DEFERRED)

Status: `deferred` — `ICommandBus` does not expose `Subscribe`/`Unsubscribe` (they live on concrete `InProcessCommandBus`). Adapters also require `OrchestrationLoop` for `CurrentExecContext`/`CurrentWorld`. Meaningful decoupling requires adding `IOrchestrationContextProvider` (explicitly out of C05 scope).

Delivered instead: `OrchestrationLoop` implements `ICommandBusProvider` (S4) — new consumers can use the provider interface without depending on `OrchestrationLoop` typed API.

### S6 — EventBusSubscriber Base + ModeChangeDebugSubscriber (Proof Of Integration)

Changes:

1. Создан `EventBusSubscriber` base в `Packages/com.morboo.runtimehost/Runtime/Events/EventBusSubscriber.cs`:
   - Универсальный abstract MonoBehaviour (NOT orchestration-specific).
   - `[SerializeField] MonoBehaviour eventBusSource` — принимает любой `IEventBusProvider`.
   - `OnEnable`: cast to `IEventBusProvider`, get bus, call `SubscribeEvents(bus)`.
   - `OnDisable`: call `UnsubscribeEvents(bus)`.
   - Abstract: `SubscribeEvents(InProcessEventBus)`, `UnsubscribeEvents(InProcessEventBus)`.
2. Создан `ModeChangeDebugSubscriber` в `Assets/Scripts/MorbooBridge/Orchestration/Events/ModeChangeDebugSubscriber.cs`:
   - Наследует `EventBusSubscriber`.
   - Подписка на `OrchestrationModeChangedEvent`.
   - Реакция: `Debug.Log` с previous/current domain + timestamp.
   - Цель: proof-of-integration (publisher → queue → flush → subscriber).

Acceptance:

1. `EventBusSubscriber` base компилируется и не зависит от `OrchestrationLoop`.
2. `ModeChangeDebugSubscriber` получает `OrchestrationModeChangedEvent` при смене Combat↔Idle.
3. Подписка/отписка работает корректно через `OnEnable`/`OnDisable` lifecycle.
4. Subscriber не модифицирует pipeline state (read-only реакция).
5. `[SerializeField] MonoBehaviour eventBusSource` принимает любой `IEventBusProvider` (не только `OrchestrationLoop`).

### S7 — Enable Future-Gate Test + Add Event Pipeline Architecture Tests

Changes:

1. `OrchestrationImplementationFitnessTests.cs`:
   - Снять `[Ignore]` с `FutureGate_RuntimePipeline_UsesDomainEvents`.
   - Добавить тесты:
     - `IEventBus` / `IDomainEvent` usage exists in RuntimeHost (event publish points).
     - Event struct types in RuntimeHost implement `IDomainEvent`.
     - `IEventBusProvider` / `ICommandBusProvider` implemented by `OrchestrationLoop`.
     - (опционально) EventBus is wired into pipeline tick sequence.
2. `RuntimeHostTests` (или аналог):
   - Добавить behavior-тест: mock arbiter produces `ModeChanged=true` → verify `OrchestrationModeChangedEvent` is received by subscriber after tick.
   - Добавить bus-provider тест: verify `IEventBusProvider.EventBus` returns non-null bus.

Acceptance:

1. `FutureGate_RuntimePipeline_UsesDomainEvents` зелёный.
2. Новые architecture tests зелёные.
3. Behavior test подтверждает end-to-end event flow.

## Tests / Gates To Add During C05

1. `FutureGate_RuntimePipeline_UsesDomainEvents` — un-ignore, must pass.
2. `InProcessEventBus` unit tests: multi-handler, deferred dispatch, flush order, unsubscribe.
3. `OrchestrationModeChangedEvent` integration test: arbiter mode change → event received.
4. `OrchestrationTickExecutedEvent` integration test: non-skipped tick → event received.
5. Architecture test: event structs in RuntimeHost implement `IDomainEvent`.
6. Architecture test: no domain-specific event payload types in RuntimeHost (StrategyCombat-specific payloads stay in StrategyCombat).
7. Architecture test: `OrchestrationLoop` implements `IEventBusProvider` and `ICommandBusProvider`.
8. Bus provider test: `IEventBusProvider.EventBus` returns functional bus instance.

## Risks / Tradeoffs

1. **InProcessEventBus upgrade** — breaking change для текущего API (`Subscribe` перезаписывает), но bus не используется (zero runtime consumers), поэтому risk = 0.
2. **Deferred dispatch** — добавляет один frame of latency для event subscribers vs. synchronous dispatch. Это намеренный tradeoff ради единообразия с command path и защиты от re-entrant mutations.
3. **Event payload design** — struct events с value-type payload ограничивают гибкость (нельзя передать произвольный reference), но гарантируют zero-allocation publish path. Это правильный tradeoff для per-tick hot path.
4. **Tier 2 events** — если `ThreatStateChangedEvent` / `DomainProposalArbitratedEvent` усложняют arbiter (tracking previous state для flip detection), допускается отложить их в рамках C05 и закрыть отдельным follow-up.
5. **EventBus per-pipeline vs shared** — текущий plan: один `EventBus` per pipeline (аналогично `CommandBus`). Если позже нужен cross-pipeline event routing, это отдельный scope за пределами C05.
6. **Command adapter partial refactor** — bus access декаплится через `ICommandBusProvider`, но orchestration context (`CurrentExecContext`, `CurrentWorld`) остаётся через `OrchestrationLoop`. Полный декаплинг orchestration context через `IOrchestrationContextProvider` — отдельный scope.

## Decision Record (C05 Position)

1. `C05` активирует event pipeline как platform seam — не game-level event system (MMEventManager остаётся для Unity/game events).
2. `InProcessEventBus` переводится на deferred multi-handler model для единообразия с `InProcessCommandBus`.
3. Event types живут в `RuntimeHost` (orchestration lifecycle) — domain-specific events в `StrategyCombat`.
4. `EventBus.Flush()` строго после `CommandBus.Flush()` — подписчики событий реагируют после того, как команды уже dispatched.
5. Tier 1 события обязательны для closure. Tier 2 — опционально в рамках C05. Tier 3 — явно за пределами C05.
6. `IEventBusProvider` / `ICommandBusProvider` вводятся в Framework для декаплинга bus consumers от конкретных bus owners. `OrchestrationLoop` — первый (но не единственный) implementor.
7. `IDomainEventHandler<T>` — Framework-level typed contract для event handler'ов без Unity-зависимости.
8. `EventBusSubscriber` — RuntimeHost-level universal MonoBehaviour base для scene-level подписчиков через `IEventBusProvider` (не orchestration-specific — любая система может использовать).
9. Route executors НЕ переводятся на EventBus — они являются частью синхронного execution path и получают `ArbiterDecision` + `world` + `ctx` напрямую для эмиссии cross-domain Hold команд в рамках того же tick.
10. Декаплинг orchestration context (`CurrentExecContext`, `CurrentWorld`) через `IOrchestrationContextProvider` — потенциальный follow-up, не в scope C05.

## Closure Evidence (C05)

Date: 2026-02-24

### Delivered (S1–S4, S6–S7)

1. `Packages/com.morboo.framework/Runtime/Execution/IEventBusProvider.cs` — new.
2. `Packages/com.morboo.framework/Runtime/Execution/ICommandBusProvider.cs` — new.
3. `Packages/com.morboo.framework/Runtime/Execution/IDomainEventHandler.cs` — new.
4. `Packages/com.morboo.systems/Runtime/Messaging/InProcessEventBus.cs` — rewritten (deferred multi-handler).
5. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Events/OrchestrationModeChangedEvent.cs` — new.
6. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Events/OrchestrationTickExecutedEvent.cs` — new.
7. `Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationPipeline.cs` — updated (EventBus field, TickExecutedEvent publish, eventBus.Flush after commandBus.Flush).
8. `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs` — updated (IEventBus injection, ModeChangedEvent publish on domain switch).
9. `Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs` — updated (implements `IEventBusProvider` + `ICommandBusProvider`, shared EventBus passed to pipelines).
10. `Packages/com.morboo.runtimehost/Runtime/Events/EventBusSubscriber.cs` — new (universal base, not orchestration-specific).
11. `Assets/Scripts/MorbooBridge/Orchestration/Events/ModeChangeDebugSubscriber.cs` — new (proof-of-integration).
12. `Packages/com.morboo.architecture.tests/Tests/Editor/OrchestrationImplementationFitnessTests.cs` — updated (`FutureGate_RuntimePipeline_UsesDomainEvents` un-ignored; `OrchestrationLoop_ImplementsBusProviderInterfaces` and `OrchestrationPipeline_FlushesEventBusAfterCommandBus` added).

### Deferred (S5)

Command adapter refactor to `ICommandBusProvider` deferred: `ICommandBus` does not expose `Subscribe`/`Unsubscribe`; adapters also require `OrchestrationLoop` for `CurrentExecContext`/`CurrentWorld` (requires `IOrchestrationContextProvider` — separate scope). `OrchestrationLoop` implements `ICommandBusProvider` for new consumers.

### Tier 2 Events

`ThreatStateChangedEvent` and `DomainProposalArbitratedEvent` not implemented in C05 — deferred to future step as optional enrichment.
