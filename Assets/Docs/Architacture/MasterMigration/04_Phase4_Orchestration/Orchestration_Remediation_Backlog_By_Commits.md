# Orchestration Remediation Backlog (By Commits)

Date: 2026-02-19  
Base: `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Implementation_Audit_2026-02-19.md`
Blueprint: `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`

## Goal

Поэтапно довести текущую orchestration-реализацию от `Combat/Idle vertical slice` к reusable platform-модели без параллельной архитектуры и без резкого переписывания.

## Rules For This Backlog

1. Каждый commit компилируется в Unity.
2. Без изменения поведения, если commit явно не помечен как behavior-affecting.
3. Сначала фиксируем швы/границы, затем переносим ответственность между слоями.
4. Архитектурные тесты добавляются вместе с каждым ключевым шагом.
5. Добавление нового домена не должно требовать правки host-core файлов по цепочке (anti file-sprawl).
6. Новые нетипизированные dependency refs (`GameObject`/`MonoBehaviour`/`Component` как service locator) не допускаются.
7. Различия доменов приоритетно выражаются данными/политиками, а не форком runtime-кода.
8. Lifecycle boundary is state-first: package seams use `EntityLifecycleState`; `IsAlive/SetAlive` допускаются только как compatibility alias и не должны расширять package boundary contracts.
9. Spatial boundary is `3D-first`; planar logic is allowed only as explicit `2D` specialization behind projection adapters.

## Precondition Gate (Before C03+)

Before proposal/arbitration/runtimehost refactor commits (`C03` and later), the following must be frozen:

1. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md`
2. `Assets/Docs/Architacture/System_Blueprint_Actor.md`
3. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Spatial_Dimensionality_3DFirst_2026-02-21.md`

Minimum requirement:

1. Actor-side read/write boundary for orchestration is fixed (query-read + command-write).
2. Required bridge adapters for current `Unit/Enemy` integration are defined.
3. Core taxonomy leaks (project/genre keys) are removed or explicitly deferred via ADR.
4. Lifecycle semantics are frozen on `EntityLifecycleState` contracts (no new package-level `IsAlive` boundary APIs).
5. Spatial dimensionality decision is frozen (`3D-first` package seam + explicit `2D` specializations).

## Ownership & Phase Mapping (Phase 0 baseline)

1. `C01` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 1`
2. `C02` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
3. `C02A` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
4. `C03` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
5. `C04` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
6. `C04A` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
7. `C05` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
8. `C06` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
9. `C07` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
10. `C08` -> Owner: `Kernel Systems Owner` -> Target phase: `Phase 8`
11. `C09` -> Owner: `Kernel Systems Owner` -> Target phase: `Phase 8`
12. `C10` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 8`

## Commit Plan

## C01 — Add Fitness Tests For Current Runtime Boundaries

Type: tests-only  
Goal: Зафиксировать работающие инварианты текущего runtime-пайплайна.

Changes:

1. Добавить `Packages/com.morboo.architecture.tests/Tests/Editor/OrchestrationImplementationFitnessTests.cs`.
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
Status: `in progress` (started 2026-02-21 after C01A closure; `C02.2` mechanical move done, `C02.3` reference cut done, `C02.4` static checks done, Unity compile/test gate pending).
Preflight doc: `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C02_RuntimeHost_Move_Preflight_2026-02-21.md`

Changes:

1. Перенести в `Packages/com.morboo.runtimehost/Runtime/Orchestration/**`:
   - `OrchestrationLoop`, `ExecutionRouter`, `ExecutionContext`.
   - `OrchestrationArbiter`, `OrchestrationArbiterContext`, `OrchestrationArbiterProposals`, `OrchestrationTickResult`.
   - `OrchestrationWorldCache`, registry contracts/interfaces для host-runtime.
2. В `Integration.StrategyCombat` оставить:
   - домены, executors, adapters и strategy-specific policy implementations.
   - host-facing base contracts/assets, required by loop/arbiter/router no-cycle move, допускаются в `RuntimeHost` как transitional placement for `C02`.
3. Обновить asmdef refs без циклов.

Acceptance:

1. `Morboo.RuntimeHost` содержит host-runtime код.
2. `Morboo.Integration.StrategyCombat` не содержит host-loop/arbitration infrastructure.
3. Тесты C01 остаются зелёными.

Execution slices (C02 detail):

1. `C02.1` preflight inventory + dependency cut map (host file set, compile constraints, asmdef no-cycle plan).
2. `C02.2` mechanical moves (`git mv`) of host files into RuntimeHost folders.
3. `C02.3` reference fixup (using/asmdef), no behavior changes.
4. `C02.4` boundary verification (architecture tests + grep checks for host infra placement).

## C01A — Actor Minimum Boundary Before Host/Proposal Refactor

Type: phase-4 precondition slice (contract freeze)  
Status: `closed` (2026-02-21)

Closure evidence:

1. Core actor boundary contracts exist:
   - `Packages/com.morboo.core/Runtime/Actor/ActorContracts.cs`
   - `Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs`
2. Read-side projection is consumed in orchestration runtime:
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs`
3. Write-side dispatch path is stabilized via typed dispatch commands/adapters:
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/DomainContracts/Dispatch/DispatchCombatCommand.cs`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/DomainContracts/Dispatch/DispatchIdleCommand.cs`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/CombatCommandAdapter.cs`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/IdleCommandAdapter.cs`
4. Legacy Unit/Enemy coupling remains isolated in bridge layer:
   - `Assets/Scripts/MorbooBridge/Orchestration/`
5. Architecture fitness gates are active:
   - `Packages/com.morboo.architecture.tests/Tests/Editor/ArchitectureLayeringTests.cs`

## C02A — Spatial Dimensionality Freeze (3D-First Seam + 2D Strategy Specializations)

Type: refactor-seam (contract hardening)  
Goal: Зафиксировать пространственный seam так, чтобы orchestration platform была 3D-capable, а planar StrategyCombat-логика была явной и изолированной.
Status: `in progress` (`C02A.1` bootstrap done; `C02A.2` compatibility slice started; `C02A.3` rename batch done with `Float2` kept unchanged).

Changes:

1. Ввести/зафиксировать общий spatial контракт как `3D-first` на package boundary (Framework/Core/RuntimeHost).
2. Для StrategyCombat:
   - классы с hard planar логикой (`Float2`/`AABB2D`) переименовать с суффиксом `2D`,
   - оставшиеся dimension-agnostic/3D-capable классы оставить без суффикса.
3. Ввести явный projection adapter (`3D -> 2D`) для planar policy/runtime path (без ad-hoc axis cuts внутри доменных политик).
4. Включить architecture gates:
   - no new planar-only boundary contracts in package layers,
   - planar class naming rule (`*2D`) for hard planar types.

Acceptance:

1. `C03` стартует только после закрытия `C02A`.
2. Spatial boundary задокументирован и типизирован как 3D-first.
3. Planar логика StrategyCombat изолирована в явные `2D` specialization paths.
4. Поведение не меняется (seam-hardening only).

## C03 — Introduce Proposal Collection Seam (No Behavior Change)

Type: refactor-seam  
Goal: Подготовить migration к proposal-list модели, сохранив текущую логику выбора.
Status: `in progress` (`C03.1/C03.2` started: host proposal collector seam + legacy import adapter in arbiter).

Changes:

1. Ввести host-уровневый proposal collector.
2. Адаптировать текущие DomainOrchestrator’ы к producer-формату proposal entries.
3. На этом шаге допускается внутренний adapter из старых `HasCombat/HasIdle` в новую коллекцию, чтобы не ломать поведение.

Acceptance:

1. Arbiter получает proposals через единый collector seam.
2. Поведение в игре не изменилось.
3. Добавить/включить тест: runtime pipeline реально использует collector.
4. `C02A` spatial freeze completed before this commit starts.

## C04 — Move Arbitration To Proposal List

Type: refactor  
Goal: Убрать fixed 2-domain input (`HasPrimary/HasSecondary/ThreatPresent`) и перейти на список proposals.
Status: `in progress` (`C04` started: arbiter runtime path arbitrates from proposal collector entries; `IArbiter` proposal-list overload added; `RuntimeHostTests` arbitration suite migrated to proposal-path coverage; legacy ArbitrationInput overload kept as compatibility-only with dedicated compatibility test; combat-specific sticky classification isolated in explicit transitional seam inside arbiter).

Changes:

1. Расширить/заменить вход `IArbiter` на список proposal records.
2. `ArbitrationInput` как legacy bridge убрать после перевода call-sites.
3. Ввести policy выбора (priority/score/tie-break) в явном виде.
4. Локализовать текущую combat-centric hysteresis-классификацию в одном transitional seam (`sticky primary` classifier), чтобы убрать domain-name ветвление из основной proposal loop логики до C04A.

Acceptance:

1. `IProposalSource`/`Proposal` используются в runtime-пайплайне.
2. Добавление нового домена не требует менять arbiter switch по конкретным доменам.
3. Включить future-gate тест ProposalSource (из C01).

## C04A — Domain Onboarding Simplification (No File Explosion)

Type: refactor-seam  
Goal: Сделать подключение нового домена “низкофрикционным”, без каскадной правки десятков файлов.
Status: `in progress` (`C04A` bootstrap started: arbiter sticky-domain classifier now reads domain arbitration metadata profiles (`IDomainArbitrationProfileSource`) instead of hardcoded `Combat` branch in classifier seam; host caches `DomainRegistration` records from domains and reuses cached policy providers in runtime loop; `ExecutionRouter` entrypoint dispatches through route registrations instead of hardcoded domain switch; `OrchestrationLoop` exposes optional `OrchestrationDomainModule` composition seam for centralized domain onboarding hooks; current single-scene source-of-truth for enabled domain orchestrators is `OrchestrationLoop.domainOrchestrators` (temporary bridge composition module/asset scaffold removed for now; can be reintroduced later if multi-scene variants become necessary); `DomainRegistration` transitional policy provider slots collapsed into a single cached arbiter-binding contributor seam; base `DomainOrchestrator` no longer performs StrategyCombat-specific policy-source casts (domains provide contributors explicitly); legacy `IIdle/ICombat*Role*MapSource` discovery interfaces removed and `Combat/Idle` domains now contribute direct policy-map bindings; arbiter binding contribution payload moved from fixed fields to generic key+entry payload and `OrchestrationArbiter` applies bindings through a local key->applier registry sourced from cached domain contributors (concrete binding keys and concrete binding appliers moved to `Morboo.Integration.StrategyCombat`; `RuntimeHost` keeps generic key/registry mechanism + generic `IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer(...)` seam + RuntimeHost-owned consumer-slot keys; `DomainArbiterBindingTargetKind` switch removed from arbiter and consumer-key switch removed from apply-target method via local consumer registry); arbiter inspector domain list is hidden and `ProduceTick()` now fail-fasts until loop/composition seam applies domains, preventing dual source-of-truth fallback).

Changes:

1. Ввести единый `DomainModule`/`DomainRegistration` контракт (или эквивалент) для подключения домена через регистрацию, а не через ручные edits в loop/router/arbiter.
2. Сконцентрировать wiring домена в одном composition-entrypoint домена (в integration-слое), вместо распределения по множеству host-файлов.
3. Вынести повторяющиеся элементы доменного пайплайна (default policies, adapters, selection plumbing) в shared `Common` blocks.
4. Добавить в архитектурные тесты/future-gates проверку, что host-runtime не содержит domain-name specific branching.
   - Transitional exception during `C04`: один локализованный classifier seam в arbiter (`sticky primary`) допустим до замены на metadata/policy-driven классификацию.
5. Зафиксировать onboarding-budget для нового домена:
   - `0` правок в `Morboo.RuntimeHost` для стандартного подключения домена,
   - максимум `1` registration touchpoint вне папки нового домена,
   - остальные изменения находятся внутри пакета/папки самого нового домена.
6. Зафиксировать baseline fan-out перед рефактором (на дату этого плана):
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration`: `72` `.cs` файлов,
   - `.../Domains`: `14` `.cs` файлов (`Combat`: `7`, `Idle`: `6`, `Common`: `1`).
7. Убрать нетипизированные provider refs в orchestration wiring:
   - заменить `[SerializeField] MonoBehaviour ...` + cast на типизированные зависимости/typed providers,
   - убрать fallback `GetComponent<...>()` как основной способ разрешения critical runtime dependencies.
8. Ввести data-driven onboarding-дескриптор домена (policy/config driven), чтобы различия нового домена задавались данными, а не изменением host-пайплайна.
9. Заменить combat-centric sticky classifier в arbiter на registration/policy metadata (например `proposal traits` / `domain arbitration profile`) без правок proposal scan loop.
10. Явно убрать transitional StrategyCombat-shaped provider slots из `DomainRegistration`
    (`IIdleRolePolicyMapSource`, `ICombatRolePolicyMapSource`,
    `ICombatRoleConstraintsMapSource`) после ввода `DomainModule`/generic bindings;
    это bootstrap bridge, не целевой API `Morboo.RuntimeHost`.

Acceptance:

1. Пробный “dummy domain onboarding” выполняется без правок `Arbiter/Loop/Router` host-уровня.
2. Domain wiring fan-out зафиксирован и снижен относительно текущего baseline.
3. Правило onboarding-budget оформлено как test/checklist gate для следующих PR.
4. Runtime orchestration wiring не использует нетипизированные dependency holder refs.
5. Пробный новый домен подключается через data/config изменения с минимальным новым кодом вне domain папки.
6. Есть architecture test/future-gate на domain-name specific branching в `Morboo.RuntimeHost` arbitration/wiring с allowlist только для задокументированных transitional seams (на текущем этапе: sticky classifier seam в arbiter).

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

1. После C02A можно параллелить C06 (Capabilities) и C07 (WorldQuery cleanup), если proposal seam уже стабилен.
2. C08 (Core cleanup) лучше делать после C04/C07, чтобы не делать двойную миграцию контрактов.
3. C04A желательно завершить до массового добавления новых доменов, иначе file-sprawl закрепится как дефолт.

## Suggested PR Grouping

1. PR-A: C01-C02-C02A (границы + spatial seam + тесты).
2. PR-B: C03-C04-C04A (proposal model + onboarding simplification).
3. PR-C: C05-C07 (events/capabilities/query purity).
4. PR-D: C08-C10 (core cleanup + final locks).
