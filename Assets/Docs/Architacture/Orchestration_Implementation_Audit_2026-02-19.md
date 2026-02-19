# Orchestration Implementation Audit

Date: 2026-02-19  
Scope: `Morboo.Framework` + `Morboo.Systems` + `Morboo.Core` + `Morboo.RuntimeHost` + `Morboo.Integration.StrategyCombat` orchestration path.

## 1) Executive Summary

Текущее состояние оркестрации: **рабочий вертикальный срез под 2 домена (Combat/Idle)**, но не полноценная платформа, описанная в Charter.

Оценка зрелости (по факту кода):

1. Runtime pipeline (tick -> arbitrate -> dispatch): **7/10**
2. Слоистость и переносимость: **5/10**
3. Абстрактность и расширяемость доменов: **3/10**
4. Соответствие Charter (Proposal-list, Event pipeline, domain-agnostic orchestration): **3/10**
5. Технический долг от неиспользуемых контрактов: **высокий**

Итог: система уже приносит пользу в игре, но архитектурно это пока **StrategyCombat-specific orchestration implementation**, а не универсальная orchestration platform.

## 2) Charter Compliance Matrix

### 2.1 Реализовано

1. Есть единый тик-цикл с явной точкой входа (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/OrchestrationLoop.cs:97`).
2. Есть scheduler abstraction через `ITickSource` (`Packages/com.morboo.framework/Runtime/Scheduling/ITickSource.cs:8`) и реализация `RealtimeScheduler` (`Packages/com.morboo.systems/Runtime/Scheduling/RealtimeScheduler.cs:16`).
3. Есть command pipeline через bus + adapter (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/ExecutionRouter.cs:45`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/CombatCommandAdapter.cs:59`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/IdleCommandAdapter.cs:64`).

### 2.2 Частично реализовано

1. Execution boundary есть, но не полная: команды публикуются, **domain events не публикуются** (`Packages/com.morboo.systems/Runtime/Messaging/InProcessEventBus.cs:8`, фактических использований нет).
2. World snapshot есть как runtime cache (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs:23`), но контрактный `WorldSnapshot` из Framework не используется (`Packages/com.morboo.framework/Runtime/State/WorldSnapshot.cs:5`).
3. IArbiter есть (`Packages/com.morboo.framework/Runtime/Decision/IArbiter.cs:4`), но его вход ограничен `ArbitrationInput` на 2 флага, не списком Proposal.

### 2.3 Не реализовано

1. Charter-модель `collect proposals from IProposalSource` фактически отсутствует: `IProposalSource` нигде не используется (`Packages/com.morboo.framework/Runtime/Decision/IProposalSource.cs:4`).
2. `Proposal`/`priority`/`score` модель объявлена, но не используется (`Packages/com.morboo.framework/Runtime/Decision/Proposal.cs:4`).
3. Domain-agnostic orchestration не достигнута: Arbiter/Router жёстко знают Combat/Idle.

## 3) Что недостаточно абстрактно

### 3.1 Arbiter зашит под 2 домена

1. `ArbitrationInput` фиксирован на `HasPrimaryProposal`, `HasSecondaryProposal`, `ThreatPresent` (`Packages/com.morboo.framework/Runtime/Decision/ArbitrationInput.cs:6`).
2. `OrchestrationArbiter` напрямую выбирает только `Combat/Idle/None` (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs:455`).
3. `OrchestrationArbiterProposals` хранит только Combat payload + Idle flag (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationArbiterProposals.cs:9`).

Следствие: добавление 3-го домена требует менять Framework input, Arbiter, Proposals container, Router, Context, keys, dispatch types.

### 3.2 Router/ExecutionContext доменно зафиксированы

1. `ExecutionContext` содержит только combat/idle-specific поля (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/ExecutionContext.cs:8`).
2. `ExecutionRouter` switch-case только по `Combat/Idle/None` и публикует только `DispatchCombatCommand`/`DispatchIdleCommand` (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/ExecutionRouter.cs:50`).

### 3.3 RuntimeHost слой фактически не является host-слоем

1. В `Morboo.RuntimeHost` остались только 2 файла (`Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/IRoleContextProvider.cs`, `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationDecisionKeys.cs`).
2. Основной host runtime (loop, arbiter, router, world cache, registry) находится в `Morboo.Integration.StrategyCombat`.

Это расходится с заявленной моделью слоёв, где RuntimeHost должен содержать host-инфраструктуру.

### 3.4 Контракты bus абстрактны только на бумаге

1. `ICommandBus` содержит только `Publish` (`Packages/com.morboo.framework/Runtime/Execution/ICommandBus.cs:4`).
2. Подписка/отписка есть только в concrete `InProcessCommandBus` (`Packages/com.morboo.systems/Runtime/Messaging/InProcessCommandBus.cs:38`).
3. `OrchestrationLoop` и `ExecutionRouter` зависят от concrete bus (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/OrchestrationLoop.cs:40`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/ExecutionRouter.cs:19`).

Следствие: bus подменяется с болью, а не через интерфейс.

## 4) Что заявлено, но практически не используется

### 4.1 Capabilities pipeline

1. Контракты и модели есть (`Packages/com.morboo.core/Runtime/Orchestration/Capabilities/CapabilityContracts.cs:1`, `Packages/com.morboo.core/Runtime/Orchestration/Capabilities/CommonCapabilities.cs:1`).
2. Провайдеры регистрируются (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Units/UnitCapabilityProvider.cs:27`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Enemies/EnemyCapabilityProvider.cs:17`).
3. Но их данные не участвуют в arbitration/router/domain policies: в коде нет потребителей `CapabilityProviders` кроме самой регистрации (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/World/OrchestrationRegistry.cs:19`).

### 4.2 StateSnapshot pipeline

1. `IStateReporter.ReportState()` реализован (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Units/UnitStateReporter.cs:72`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Enemies/EnemyStateReporter.cs:72`).
2. Но вызовов `ReportState()` в runtime pipeline нет.
3. Arbiter в `BuildWorldCache` использует только `IOrchestrationActor`/faction/transform/liveness и не читает snapshot metrics (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs:202`).

### 4.3 Proposal ecosystem из Framework

1. `IProposalSource`, `Proposal`, `WorldSnapshot`, `IWorldState` объявлены.
2. В production-коде orchestration они не участвуют.

### 4.4 Domain events

1. `IEventBus` и `InProcessEventBus` есть.
2. Реальных event publishers/subscribers в orchestration pipeline нет.

### 4.5 Intent/Instruction ветка

1. `Intent`, `Instruction`, `IIntentReceiver`, `IInstructionReceiver`, `IInstructionProvider` есть в Core.
2. `CombatIntentBuilders`/`CombatInstructionBuilders`/`CombatAdapter` есть в StrategyCombat.
3. В актуальном loop это не используется: команды формируются напрямую из доменов и router.

## 5) Что выглядит лишним/костылём сейчас

### 5.1 Глобальные static registry как главный механизм связности

1. `OrchestrationRegistry`, `IdleBoundsRegistry`, `EntityTransformResolver` являются process-global mutable состоянием (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/World/OrchestrationRegistry.cs:11`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/World/IdleBoundsRegistry.cs:17`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Entity/EntityTransformResolver.cs:13`).
2. Это упрощает wiring, но ухудшает тестируемость, усложняет multi-world/runtime isolation и создаёт неявные lifecycle зависимости.

### 5.2 Неявные правила разрешения конфликтов

1. В arbiter map-sources: `Last non-null wins` (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs:484`).
2. В idle bounds: duplicate role -> last wins (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/World/IdleBoundsRegistry.cs:87`).

Это рабочий компромисс для MVP, но для platform-level архитектуры лучше сделать явный deterministic policy с fail-fast режимом.

### 5.3 Каст к concrete world cache внутри домена

1. `CombatOrchestratorLite` кастует `IWorldQuery` к `OrchestrationWorldCache` ради targetSet (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/CombatOrchestratorLite.cs:141`).
2. Это ломает чистоту abstraction boundary `IWorldQuery`.

### 5.4 Core не engine-agnostic

1. В `Morboo.Core` много `UnityEngine` и `ScriptableObject` (`Packages/com.morboo.core/Runtime/Orchestration/Roles/RoleAsset.cs:1`, `Packages/com.morboo.core/Runtime/Orchestration/Factions/FactionAsset.cs:1`, `Packages/com.morboo.core/Runtime/Orchestration/Capabilities/CapabilitiesProfile.cs:2`, `Packages/com.morboo.core/Runtime/Orchestration/StateSnapshot.cs:2`).
2. Это конфликтует с целью переносимого core-слоя в ваших документах.

## 6) Почему добавление нового домена сейчас тяжёлое

Чтобы добавить, например, `Goals` домен, нужно менять сразу:

1. `ArbitrationInput` (добавлять новые сигналы) или ломать текущую модель.
2. `OrchestrationArbiterProposals` (новые поля payload/flags).
3. `OrchestrationArbiter.Arbiterate` (новая ветка выбора).
4. `ExecutionContext` (новые поля).
5. `ExecutionRouter` (новый case + dispatch).
6. Новый dispatch command + adapter + receiver contracts.
7. `OrchestrationDomainKeys`/`OrchestrationProposalKeys`.

Это не open/closed расширение, а invasive изменения центрального пайплайна.

## 7) Приоритетный техдолг (без “параллельной архитектуры”)

### 7.1 P0 — привести модель к одному truth

1. Выбрать: либо реально внедрять `Proposal`/`IProposalSource`, либо удалить неиспользуемые декларации до отдельной фазы.
2. То же для `Intent/Instruction` ветки: либо включить в pipeline, либо убрать из core path.

### 7.2 P0 — восстановить смысл слоёв RuntimeHost vs Integration

1. Host-инфраструктуру (loop/router/world cache/registry contracts) вернуть в `Morboo.RuntimeHost`.
2. В `Morboo.Integration.StrategyCombat` оставить только domain/policy/executor/adapters проект-тип слоя.

### 7.3 P1 — сделать arbitration extensible

1. Перейти от `ArbitrationInput(hasPrimary/hasSecondary/threat)` к набору proposal records.
2. Arbiter должен выбирать из коллекции proposal по priority/score/budget policy, а не через фиксированные if/else по Combat/Idle.

### 7.4 P1 — сделать capabilities полезными

1. Включить capability snapshot в world query/read model.
2. Добавить минимум один runtime consumer (например, фильтрация policy/constraint по capability).
3. Добавить fitness test: "если capability providers есть, они влияют на decision/execution path".

### 7.5 P2 — уменьшить global static coupling

1. Для registries добавить явный lifecycle owner и reset hooks для playmode/tests.
2. Убрать зависимости вида `cast IWorldQuery -> OrchestrationWorldCache` через расширение query contract.

## 8) Bottom Line

Система оркестрации уже работает как production-механизм для текущей игры и текущих 2 доменов.  
Но как архитектурная платформа (по Charter) она пока не доведена: ключевые платформенные контракты (`IProposalSource`, `Proposal`, `WorldSnapshot`, `IEventBus`, `Capabilities`) в основном декларативны, а реальный runtime завязан на конкретный StrategyCombat поток и несколько static registries.

Главное улучшение с максимальным ROI: **перейти с fixed Combat/Idle pipeline на data-driven proposal pipeline** и синхронизировать фактическое расположение ответственности между `RuntimeHost` и `Integration.StrategyCombat`.
