# Orchestration Remediation Backlog (By Commits)

Date: 2026-02-19  
Base: `Assets/Docs/Architacture/Orchestration_Implementation_Audit_2026-02-19.md`

## Goal

Поэтапно довести текущую orchestration-реализацию от `Combat/Idle vertical slice` к reusable platform-модели без параллельной архитектуры и без резкого переписывания.

## Rules For This Backlog

1. Каждый commit компилируется в Unity.
2. Без изменения поведения, если commit явно не помечен как behavior-affecting.
3. Сначала фиксируем швы/границы, затем переносим ответственность между слоями.
4. Архитектурные тесты добавляются вместе с каждым ключевым шагом.

## Commit Plan

## C01 — Add Fitness Tests For Current Runtime Boundaries

Type: tests-only  
Goal: Зафиксировать работающие инварианты текущего runtime-пайплайна.

Changes:

1. Добавить `Assets/Game/Tests/Architecture/OrchestrationImplementationFitnessTests.cs`.
2. Добавить активные тесты:
   - Domains не вызывают `Publish(...)` напрямую.
   - Domains не используют `EntityTransformResolver`.
   - `ExecutionRouter` не вызывает `Apply*Command` напрямую.
   - `OrchestrationArbiter` не публикует команды.
   - `OrchestrationLoop` использует `ITickSource` (и не зависит от `RealtimeScheduler` напрямую).
3. Добавить future-gates как `[Ignore]`:
   - ProposalSource pipeline adoption.
   - EventBus/DomainEvent pipeline adoption.
   - Убрать downcast `IWorldQuery -> OrchestrationWorldCache` в Domains.

Acceptance:

1. Тесты компилируются.
2. Активные тесты зелёные.
3. Ignored-тесты видны в Test Runner как roadmap-gates.

## C02 — Normalize RuntimeHost Responsibility (Move Host Infrastructure Back)

Type: refactor (no behavior change expected)  
Goal: Вернуть host-инфраструктуру из `Integration.StrategyCombat` в `RuntimeHost`.

Changes:

1. Перенести в `Packages/com.morboo.runtimehost/Runtime/Orchestration/**`:
   - `OrchestrationLoop`, `ExecutionRouter`, `ExecutionContext`.
   - `OrchestrationArbiter`, `OrchestrationArbiterContext`, `OrchestrationArbiterProposals`, `OrchestrationTickResult`.
   - `OrchestrationWorldCache`, registry contracts/interfaces для host-runtime.
2. В `Integration.StrategyCombat` оставить:
   - домены, policy assets, executors, adapters, map assets.
3. Обновить asmdef refs без циклов.

Acceptance:

1. `Morboo.RuntimeHost` содержит host-runtime код.
2. `Morboo.Integration.StrategyCombat` не содержит host-loop/arbitration infrastructure.
3. Тесты C01 остаются зелёными.

## C03 — Introduce Proposal Collection Seam (No Behavior Change)

Type: refactor-seam  
Goal: Подготовить migration к proposal-list модели, сохранив текущую логику выбора.

Changes:

1. Ввести host-уровневый proposal collector.
2. Адаптировать текущие DomainOrchestrator’ы к producer-формату proposal entries.
3. На этом шаге допускается внутренний adapter из старых `HasCombat/HasIdle` в новую коллекцию, чтобы не ломать поведение.

Acceptance:

1. Arbiter получает proposals через единый collector seam.
2. Поведение в игре не изменилось.
3. Добавить/включить тест: runtime pipeline реально использует collector.

## C04 — Move Arbitration To Proposal List

Type: refactor  
Goal: Убрать fixed 2-domain input (`HasPrimary/HasSecondary/ThreatPresent`) и перейти на список proposals.

Changes:

1. Расширить/заменить вход `IArbiter` на список proposal records.
2. `ArbitrationInput` как legacy bridge убрать после перевода call-sites.
3. Ввести policy выбора (priority/score/tie-break) в явном виде.

Acceptance:

1. `IProposalSource`/`Proposal` используются в runtime-пайплайне.
2. Добавление нового домена не требует менять arbiter switch по конкретным доменам.
3. Включить future-gate тест ProposalSource (из C01).

## C05 — Event Pipeline Activation

Type: feature-complete (platform)  
Goal: Включить доменные события как часть orchestration loop.

Changes:

1. Определить минимальный набор `IDomainEvent` для orchestration lifecycle.
2. Подключить `IEventBus` publish на execution boundary.
3. Добавить подписчики в integration только там, где это нужно.

Acceptance:

1. Есть хотя бы один runtime publisher и subscriber доменных событий.
2. Включить future-gate тест по EventBus/DomainEvent usage (из C01).

## C06 — Capabilities Integration Into Decision/Execution

Type: behavior-affecting (controlled)  
Goal: Убрать “declared but unused” статус capability-системы.

Changes:

1. Включить capability snapshot в world read model/query seam.
2. Добавить минимальный реальный consumer:
   - policy filtering by capability,
   - либо constraints override by capability.
3. Добавить тест, что capability providers влияют на итоговое решение/команду.

Acceptance:

1. `ICapabilityProvider` не только регистрируется, но и реально читается в pipeline.
2. Есть regression tests на capability-driven variation.

## C07 — Remove Domain Downcasts To Concrete World Cache

Type: refactor  
Goal: Соблюсти чистый `IWorldQuery` boundary.

Changes:

1. Убрать `as OrchestrationWorldCache` из доменов.
2. Добавить недостающие query-contract методы в абстракции (если нужно).
3. Перенести concrete-specific данные в query interfaces.

Acceptance:

1. В `Domains/**` нет downcast к concrete world cache.
2. Включить соответствующий future-gate тест (из C01).

## C08 — Core Cleanup: Remove Unity-Coupled Contracts From Morboo.Core

Type: layered-refactor  
Goal: приблизить `Morboo.Core` к engine-agnostic роли.

Changes:

1. Выделить Unity-coupled типы (`RoleAsset`, `FactionAsset`, ScriptableObject profiles) в integration-level contracts.
2. В core оставить value-types/contracts без Unity API.
3. Обновить все call-sites на value-id based APIs.

Acceptance:

1. В `Packages/com.morboo.core/Runtime` нет `using UnityEngine` (или остаются только явно разрешённые переходные исключения с TODO).
2. Арх-тесты обновлены под финальную границу.

## C09 — Remove/Finalize Legacy Intent/Instruction Branch

Type: cleanup  
Goal: убрать “мертвую ветку” или сделать её рабочей частью pipeline.

Decision:

1. Либо удалить `Intent/Instruction` pathway из core/runtime.
2. Либо встроить её как canonical input format и покрыть тестами.

Acceptance:

1. Нет деклараций без runtime-consumers.
2. Документация и тесты совпадают с фактическим путем данных.

## C10 — Final Architecture Locks

Type: tests/docs hardening  
Goal: финальная фиксация архитектуры fitness-тестами.

Changes:

1. Перевести временные `[Ignore]` tests в активные.
2. Добавить checks на отсутствие доменной логики в RuntimeHost.
3. Сверить README/charter/reference docs с фактическим кодом.

Acceptance:

1. Все архитектурные тесты зелёные.
2. Audit-долги из базового документа закрыты или явно перенесены в отдельный backlog.

## Optional Parallel Workstreams

1. После C02 можно параллелить C06 (Capabilities) и C07 (WorldQuery cleanup), если proposal seam уже стабилен.
2. C08 (Core cleanup) лучше делать после C04/C07, чтобы не делать двойную миграцию контрактов.

## Suggested PR Grouping

1. PR-A: C01-C02 (границы + тесты).
2. PR-B: C03-C04 (proposal model migration).
3. PR-C: C05-C07 (events/capabilities/query purity).
4. PR-D: C08-C10 (core cleanup + final locks).
