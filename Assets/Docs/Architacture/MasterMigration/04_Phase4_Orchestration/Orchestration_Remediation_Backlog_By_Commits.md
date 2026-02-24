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
7. `C04B` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
8. `C04C` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
9. `C04D` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
10. `C05` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4` -> Status: `closed`
11. `C06` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
12. `C07` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 4`
13. `C08` -> Owner: `Kernel Systems Owner` -> Target phase: `Phase 8`
14. `C09` -> Owner: `Kernel Systems Owner` -> Target phase: `Phase 8`
15. `C10` -> Owner: `Orchestration Platform Owner` -> Target phase: `Phase 8`

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
Status: `closed (single-scope path)` (route/body ownership extracted above `RuntimeHost`; domain onboarding seams and generic registration/binding routes stabilized for current single-scene path; bridge route-policy seam and behavior proof added; loop-level duplicate composition seam removed. Remaining multi-faction structural ownership issues are explicitly deferred to `C04B`.)

Checkpoint cleanup note (current commit boundary):

1. `StrategyCombatExecutionRoutes` aggregate helper removed; ownership stays split across per-route executors in `Morboo.Integration.StrategyCombat`.
2. StrategyCombat route executors are being normalized to instance-based executors (instead of static route-combinator style) to avoid creating a bad template for future development.
3. Unknown-route fallback registration now suppresses duplicate warning when the same delegate is registered more than once (still warns for conflicting fallback registrations).
4. Next `C04A` continuation after this checkpoint: move StrategyCombat route execution toward data/policy-driven configuration (without reintroducing RuntimeHost route-body ownership).
5. Pilot route policy seam started: optional `StrategyCombatRouteExecutionPolicyAsset` overrides mode-change hold behavior and selected debug/warning semantics (including `Idle` fallback warnings / `NoRoleMatch` label / debug trace toggles) for `Combat/Idle/None/UnknownRouteFallback` route executors; null/empty values preserve legacy behavior.
6. `StrategyCombatRouteExecutionPolicyAsset` and `StrategyCombatRouteExecutionProfile` now both use grouped route sections (`Combat/Idle/None/UnknownRouteFallback`); flat compatibility serialized fields were intentionally not kept.
7. Composition-level route-profile preset selection now has a Bridge seam (`StrategyCombatRouteExecutionPolicyBridge` in `MorbooBridge`): a shared `StrategyCombatRouteExecutionPolicyAsset` is applied before `OrchestrationLoop` builds route registrations, while `OrchestrationLoop.domainOrchestrators` remains the single scene source-of-truth for enabled/ordered domains (bridge reads loop-configured domains instead of keeping a duplicate domain list; no `RuntimeHost` route-body ownership regression).
8. `OrchestrationLoop` no longer exposes `domainModules` / `OrchestrationDomainModule` in the current single-scene path to avoid an unused second composition mechanism.
9. Route-policy pilot now has behavior coverage: `RuntimeHostTests` verifies `StrategyCombatRouteExecutionPolicyAsset` can change `None` route mode-change hold-all emission without `RuntimeHost` changes.
10. `C04A` is closed for the current single-scene / single-scope path. Multi-faction scope ownership, multi-arbiter host composition, and scope-aware targeting ownership are tracked in `C04B` and are not `C04A` blockers.

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
   - C04A continuation (route-side): route execution differences (`Combat/Idle/None/UnknownRouteFallback`) should also converge to data/policy-driven configuration in `Morboo.Integration.StrategyCombat`, not static helper growth.
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

## C04B — Multi-Scope / Multi-Arbiter Host Restructure (Scene-Breaking Allowed)

Type: structural refactor (scene-breaking allowed)  
Goal: Убрать single-scope ограничение текущего `OrchestrationLoop -> Arbiter + Domains` и перейти к модели `LoopHost -> Pipelines[]` с единым source-of-truth для scope/faction и per-pipeline domain composition.
Status: `in progress` (`Faction-first` start selected: no new typed scope seam yet; `B2-B4` are in code: `OrchestrationPipeline` runtime container extracted, `OrchestrationLoop` hosts ordered `OrchestrationPipelineComponent[]`, and per-pipeline domain composition moved under pipeline component owner; `B5` in code with per-pipeline faction + host-global relations composition propagation into arbiter/runtime contexts; `B6` pivots to domain-owned `CombatTargetProvider` / `IdleTargetProvider` in `Morboo.Integration.StrategyCombat`, removing temporary `CombatTargetSet` ownership from `RuntimeHost` pipeline API; `B7` host/path migration started in code: `Level` scene uses `Player + Enemy` pipelines and `OrchestrationLoop` now uses a shared command bus with per-flush dispatch-context override so existing adapters can consume commands from all pipelines.)
Detail plan: `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C04B_MultiScope_MultiArbiter_Host_Restructure_2026-02-23.md`

Changes (high level):

1. Стартовать с `Faction-first` pipeline scope semantics (без нового `int`-based scope seam в package interfaces).
   - если позже понадобится generalized scope contract, вводить только asset-based typed identity (base asset/contract), а не raw `int` id seam.
2. Выделить runtime container `OrchestrationPipeline` (`Arbiter`, `Router`, `Bus`, `DomainOrchestrators[]`, `Scope`).
3. Эволюционировать `OrchestrationLoop` в host для `pipelines[]` вместо single arbiter/domain list как primary model.
4. Перенести source-of-truth доменов под pipeline (не loop-global).
5. Сделать StrategyCombat targeting scope-aware через domain-owned `CombatTargetProvider` / `IdleTargetProvider` (без доменно-типизированных targeting полей в `Morboo.RuntimeHost` pipeline API), чтобы multi-faction pipelines не делили неявный global targeting state.
6. Мигрировать core scene на reference composition с двумя pipeline-ами (`Player`, `Enemy`) без дублирования host-кода.
7. При любом блокере на пути выноса абстракции вверх по слоям (`RuntimeHost` -> generic orchestration layer -> `Core`) сначала абстрагировать мешающий узел; не сужать `C04B` цель под слабое звено без явного согласования и фиксации решения в backlog.

Acceptance:

1. `OrchestrationLoop` (host-role) тикает минимум `2` независимых pipeline-а.
2. Domain composition принадлежит pipeline, а не loop-global field как primary model.
3. `Scope/Faction` имеет один typed source-of-truth на pipeline и прокидывается в arbiter/domain execution path.
4. Bridge composition не дублирует domain list отдельно от pipeline для того же pipeline.
5. Multi-pipeline smoke/playtest path задокументирован и проходит.
6. `B7` scene migration acceptance на текущем шаге ограничен host/path integration parity (`Player + Enemy` pipelines tick/dispatch/routing/faction composition). Full enemy behavior parity, зависящий от `UnitClass`-oriented mapping assumptions, переносится в `C04C` (domain-orchestrator form convergence) и не закрывается fallback-путями.

## C04C — Domain Orchestrator Form Convergence (Remove *Lite as Target Shape)

Status: `closed`

Type: structural refactor (domain-layer form cleanup)  
Goal: Явно довести один из ключевых мотивов orchestration refactor: уйти от отдельных runtime entrypoint-классов per-domain (`Combat*` / `Idle*`) как целевой формы к общей/composable форме `StrategyCombatDomainOrchestrator` + domain components/providers + data-driven config в `Morboo.Integration.StrategyCombat`.

Changes:

1. Зафиксировать shared/composable orchestration shape в `Morboo.Integration.StrategyCombat` (не в `RuntimeHost`) через `DomainOrchestrator + components/providers + data` и shared helper/interface seams (без дополнительного orchestrator inheritance layer).
2. Разделить domain-specific concerns по компонентам/провайдерам (например `CombatTargetProvider`, `IdleTargetProvider`, shared route-policy provider, route/profile config).
   - `CombatTargetProvider` и `IdleTargetProvider` должны быть приведены к общей форме:
     общий базовый тип + один общий orchestration-facing интерфейс (при необходимости с typed-расширениями поверх него для domain payload).
   - Общая форма должна использоваться не только декларативно, но и в runtime validation/wiring path (shared validation helper/guard).
3. Перевести `Idle` на новую форму первым (меньший риск).
4. Перевести `Combat` на новую форму вторым.
5. Убрать `*OrchestratorLite` и отдельные `Combat/Idle` domain-orchestrator entrypoint classes как target architecture form (допускается временная совместимость на время миграции, но с removal plan).
6. Если на пути выноса общей orchestration-формы из `Morboo.Integration.StrategyCombat` наверх мешает конкретный тип/компонент/policy/provider seam, сначала абстрагировать этот seam (общий contract/base), а не оставлять общую форму в `StrategyCombat` "потому что так проще" без явного разрешения.

Acceptance:

1. Целевая форма доменных оркестраторов описана и реализована как shared/composable `StrategyCombatDomainOrchestrator` + domain components/data.
2. `Idle` и `Combat` используют один и тот же structural pattern (без ad-hoc divergence по форме классов) через `CombatDomainComponent` / `IdleDomainComponent` под одним `StrategyCombatDomainOrchestrator`.
3. `CombatTargetProvider` и `IdleTargetProvider` имеют общий базовый тип и общий orchestration-facing интерфейс (typed domain-specific API допускается только как расширение, а не как замена общей формы).
4. Новый domain добавляется по этому шаблону без копирования `*Lite` паттерна и без создания отдельного domain-orchestrator entrypoint класса.
5. `RuntimeHost` не получает обратно domain-specific orchestrator logic/ownership.
6. Ни один шаг `C04C` не закрывается "локальной" genre-specific абстракцией вместо общей без явного решения/approval, зафиксированного в roadmap/backlog.

## C04D — Generic Orchestration Composition Abstraction Extraction (No Compatibility Path)

Status: `closed`

Type: structural refactor (package-breaking + scene-breaking)
Goal: Убрать из `Morboo.Integration.StrategyCombat` владение общей orchestration composition-инфраструктурой и разложить её по существующим верхним пакетам (`RuntimeHost` / `Core` / `Framework` по ownership), оставив в `StrategyCombat` только domain components/providers/route executors/policies/data.

Detail plan: `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C04D_Generic_Orchestration_Composition_Extraction_2026-02-23.md`

Closure evidence (2026-02-23):

1. Generic orchestration composition form extracted to `Morboo.RuntimeHost` (sections A, B, D, E, F of plan):
   - `DomainOrchestratorComponent`, `DomainComponent`, `DomainOrchestratorComposition`, `IDomainRouteExecutionPolicyConsumer`, `DomainRouteExecutionPolicy`, `DomainRouteExecutionPolicyProvider`, `DomainTargetProvider` — all in `RuntimeHost`.
2. Genre layer rebound: `CombatDomainComponent` / `IdleDomainComponent` → `DomainComponent`; `CombatTargetProvider` / `IdleTargetProvider` → `DomainTargetProvider`; `StrategyCombatRouteExecutionPolicyAsset` → `DomainRouteExecutionPolicy`.
3. Bridge renamed to `DomainRouteExecutionPolicyBridge`, uses `IDomainRouteExecutionPolicyConsumer`.
4. Scene, architecture tests, layering tests updated.
5. No compatibility path, no legacy fallback.
6. Deferred: section C (monolith policy split into per-route assets) — `StrategyCombatRouteExecutionPolicyAsset` now inherits `DomainRouteExecutionPolicy` but is not yet split. Does not block primary C04D goal.

Acceptance status:

1. [DONE] В `Morboo.Integration.StrategyCombat` больше нет strategy-owned generic orchestration infrastructure.
2. [DONE] Shared orchestration composition form в `RuntimeHost` с семантическими именами без `Base`.
3. [DONE] Genre domain components/providers являются реализациями generic контрактов.
4. [PARTIAL] `StrategyCombatRouteExecutionPolicyAsset` наследует generic base, но монолит не разбит на per-route assets (deferred).
5. [DONE] Bridge использует generic contracts без StrategyCombat branching.
6. [DONE] `RuntimeHost` не получил domain-specific ownership.
7. [DONE] Выполнен без compatibility path / legacy fallback.

## C05 — Event Pipeline Activation

Type: feature-complete (platform seam activation + bus provider decoupling)
Status: `closed`
Detail plan: `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/C05_Event_Pipeline_Activation_2026-02-24.md`
Goal: Включить доменные события как часть orchestration loop; декаплить bus consumers от конкретных bus owners через provider interfaces в Framework; ввести typed subscriber infrastructure.

Changes (delivered):

1. `IEventBusProvider` / `ICommandBusProvider` в `com.morboo.framework` для декаплинга bus consumers от конкретных bus owners.
2. `IDomainEventHandler<TEvent>` typed contract в `com.morboo.framework` (без Unity-зависимости).
3. `InProcessEventBus` переведён на deferred multi-handler model (queue + flush, единообразие с `InProcessCommandBus`).
4. Tier 1 `IDomainEvent` для orchestration lifecycle: `OrchestrationModeChangedEvent`, `OrchestrationTickExecutedEvent`.
5. `IEventBus` publish в pipeline tick (arbiter + pipeline) с `EventBus.Flush()` строго после `CommandBus.Flush()`.
6. `OrchestrationLoop` реализует `IEventBusProvider` и `ICommandBusProvider`.
7. `EventBusSubscriber` universal abstract MonoBehaviour base в RuntimeHost (depends on `IEventBusProvider`, not `OrchestrationLoop`; не orchestration-specific).
8. `ModeChangeDebugSubscriber` proof-of-integration в MorbooBridge.
9. `FutureGate_RuntimePipeline_UsesDomainEvents` un-ignored; `OrchestrationLoop_ImplementsBusProviderInterfaces` and `OrchestrationPipeline_FlushesEventBusAfterCommandBus` architecture tests added.

Deferred:

1. Command adapter refactor to `ICommandBusProvider` (S5): `ICommandBus` lacks `Subscribe`/`Unsubscribe`; adapters also need `OrchestrationLoop` for `CurrentExecContext`/`CurrentWorld` (requires `IOrchestrationContextProvider`).
2. Tier 2 events (`ThreatStateChangedEvent`, `DomainProposalArbitratedEvent`): optional enrichment for future step.

Acceptance (verified):

1. Runtime publisher (`OrchestrationArbiter` + `OrchestrationPipeline`) and subscriber (`ModeChangeDebugSubscriber`) exist.
2. Future-gate test `FutureGate_RuntimePipeline_UsesDomainEvents` un-ignored.
3. `IEventBusProvider` / `ICommandBusProvider` implemented by `OrchestrationLoop`.
4. `EventBusSubscriber` base does not depend on `OrchestrationLoop`.
5. `EventBus.Flush()` called after `CommandBus.Flush()` in pipeline tick (verified by architecture test).

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
