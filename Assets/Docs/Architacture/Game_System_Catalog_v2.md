# Game System Catalog v2 (Kernel-First)

Date: 2026-02-19  
Status: Draft (Normative target for migration)

Related:

1. `Assets/Docs/Architacture/Game_Systems_Architecture_Framework.md`
2. `Assets/Docs/Architacture/Architecture_Layers_Reference.md`
3. `Assets/Docs/Architacture/Orchestration_Implementation_Audit_2026-02-19.md`
4. `Assets/Docs/Architacture/Orchestration_Remediation_Backlog_By_Commits.md`
5. `Assets/Docs/Architacture/TopDownEngine_Exit_Migration_Backlog.md`
6. `Assets/Docs/Architacture/Morboo_Gameplay_Modularization_Backlog.md`
7. `Assets/Docs/Architacture/New_System_Requirements_Template.md`

## 1) Purpose

Определить систему сверху-вниз: сначала владельцы игровых решений (Kernel systems), потом домены симуляции, потом presentation/integration.

Ключевая цель: новые фичи не пишутся в "ближайшую папку", а добавляются в заранее определённый системный владелец.

## 2) Three-Tier Model

## 2.1 Tier A — Game Kernel (Control Plane)

Системы, которые управляют жизненным циклом игры и связями между доменами.

1. `GameFlow System` — состояния игры (Boot/Menu/PreRun/Run/Pause/Win/Lose/PostRun).
2. `Scenario System` — запуск и контроль сценария (уровень/карта/режим/фазы).
3. `Objective System` — постановка и трекинг целей разных scope.
4. `Outcome System` — вычисление победы/поражения/исхода run.
5. `Rulebook System` — набор правил/политик, по которым работают Objective/Outcome/Domain gates.
6. `Entity Backbone System` — модель сущности + владелец жизненного цикла (`Entity Model`, `Registry`, `Factory`, snapshot seams).
7. `Session & Profile System` — runtime session state и persistent profile state.
8. `Save/Load System` — сериализация, миграции, восстановление состояния.
9. `Economy Ledger System` — транзакционный контур ресурсов/валют.
10. `Reward & Claim System` — выдача наград, состояние claim/redeem.
11. `Telemetry & LiveOps System` — аналитика, remote flags/config, experiment toggles.

## 2.2 Tier B — Simulation Domains (Gameplay Plane)

Доменные системы, которые описывают правила симуляции.

1. `Actors Domain` (Unit/Enemy как частный случай Actor).
2. `Identity/Faction/Role/Capabilities Domain`.
3. `Movement/Positioning/Formations Domain`.
4. `Combat Resolution Domain`.
5. `Abilities/Effects/Statuses Domain`.
6. `Spawn/Encounter Domain`.
7. `Board/Spatial Constraints Domain` (если применимо жанру).
8. `Inventory/Equipment Domain` (optional).
9. `Merge/Crafting Domain` (optional).
10. `Progression/Upgrade Domain`.

## 2.3 Tier C — Experience (Presentation/Integration)

1. `UI Presentation`.
2. `Feedback/VFX/SFX`.
3. `Input Mapping`.
4. `Engine Adapters` (TDE/physics/pathfinding wrappers).
5. `Entity View Binding` (`entity -> view`, runtime sync, no gameplay ownership).
6. `Project Bridge` (конкретная игра, конкретные ассеты/мапы/сцены).

## 3) Objective Scopes (Not Only Level)

`Objective System` обязан поддерживать scope-иерархию:

1. `Meta` — аккаунт/сезон/долгий прогресс.
2. `Campaign` — кампания/акт/глава.
3. `Run` — сессия/заход.
4. `Encounter` — бой/волна/этап внутри run.
5. `Task` — атомарная цель.

Правило: Objective только считает прогресс и статусы; Outcome решает итог победы/поражения по Rulebook.

## 4) Who Owns What (Direct Answer)

1. Кто управляет игрой: `GameFlow System`.
2. Кто управляет уровнем/сценарием: `Scenario System`.
3. Кто ставит и распределяет цели: `Objective System` (+ Rulebook).
4. Кто решает, кто победил: `Outcome System`.
5. Кто владеет сущностями (create/destroy/get by id): `Entity Backbone System`.
6. Кто связывает сущность с MonoBehaviour/prefab/view: `Entity View Binding` (Tier C).
7. Где orchestration: в execution-coordination между доменами, но не как владелец flow/objective/outcome truth.

## 4.1 Entity Backbone (Mandatory)

Состав:

1. `Entity Model` — ID, state, tags/traits/capabilities, инварианты.
2. `Entity Registry + Factory` — единое владение lifecycle (`create/destroy/lookup/events`).
3. `Entity View Binding` — односторонняя связка `entity -> view` через `EntityId`.

Ключевые инварианты:

1. `Single source of truth`: геймплейное состояние живёт в модели сущности, не в view-компонентах.
2. View не владеет правилами и не мутирует доменную модель напрямую.
3. Домены и orchestration работают по `EntityId`/queries, а не по `Transform` как источнику истины.

## 5) Canonical Kernel Contracts (Create First)

Минимальный контрактный каркас, который нужно иметь до масштабной миграции:

1. `IGameFlowService`
2. `IScenarioService`
3. `IObjectiveService`
4. `IOutcomeService`
5. `IRulebookProvider`
6. `IEntityRegistry`
7. `IEntityFactory`
8. `IEntityLifecycleService`
9. `IEntitySnapshotStore` (save/replay seam; can be minimal initially)
10. `IEntityViewBinder` (Tier C adapter contract)
11. `ISessionStateStore`
12. `IProfileStateStore`
13. `ISaveLoadService`
14. `IEconomyLedger`
15. `IRewardService`
16. `ITelemetryService`
17. `ILiveOpsConfigService`

Принцип: сначала интерфейс + state owner, потом реализация.

## 6) System Ownership Matrix (Template)

Для каждой системы в реализации фиксируем:

1. `Owns state` (какое состояние является source of truth).
2. `Consumes` (события/команды/queries).
3. `Emits` (события/решения/команды).
4. `Forbidden deps` (что запрещено импортировать).
5. `SLA invariants` (например: outcome не зависит от UI, objectives не пишут world напрямую).

## 6.1 Phase 0 Owner Matrix Baseline

Owner roles (нормативно для текущей программы миграции):

1. `Kernel Systems Owner` — game lifecycle/control-plane systems.
2. `Entity Backbone Owner` — entity model/registry/factory/view-binding contracts.
3. `Orchestration Platform Owner` — orchestration runtime platform and fitness gates.
4. `Gameplay Domains Owner` — gameplay simulation modules (actors/combat/goals/economy/etc).
5. `Engine Adapter Owner` — engine anti-corruption seams and TDE exit slices.
6. `Experience & Bridge Owner` — project bridge/wiring, UI integration, composition glue.

Baseline assignment (Phase 0, can be refined by ADR/owner decision):

1. `GameFlow`, `Scenario`, `Objective`, `Outcome`, `Rulebook`, `Session/Profile`, `Save/Load` -> `Kernel Systems Owner`.
2. `Entity Backbone` -> `Entity Backbone Owner`.
3. `Orchestration Domain` -> `Orchestration Platform Owner`.
4. `Actors`, `Identity/Faction/Role/Capabilities`, `Movement/Formations`, `Combat`, `Abilities`, `Spawn/Encounter`, `Board`, `Inventory`, `Merge`, `Progression`, `Economy`, `Rewards`, `Telemetry/LiveOps` -> `Gameplay Domains Owner`.
5. `Engine Adapter Layer` -> `Engine Adapter Owner`.
6. `UI Presentation`, `Feedback/VFX/SFX`, `Input Mapping`, `Project Bridge`, project composition/wiring in `MorbooBridge` -> `Experience & Bridge Owner`.

## 7) Current Repository Mapping Snapshot

Status legend:

1. `Exists` — есть отдельная работающая система.
2. `Partial` — есть фрагменты, нет полного контракта/ownership.
3. `Missing` — фактически нет системного владельца.

## 7.1 Kernel Systems

1. `GameFlow System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/GameManager.cs`, `Assets/Scripts/Game/Managers/LevelManager.cs`.

2. `Scenario System`: `Partial`  
Evidence: `Assets/Scripts/Game/Data/LevelData.cs`, `Assets/Scripts/Game/Managers/LevelManager.cs`, `Assets/Scripts/Game/Managers/EnemyManager.cs`.

3. `Objective System (multi-scope)`: `Partial`  
Evidence: `Assets/Scripts/Game/Core/LevelGoals/LevelGoal.cs`, `Assets/Scripts/Game/Managers/LevelGoalManager.cs`.  
Gap: нет явных Meta/Campaign/Run/Encounter scopes как контракта.

4. `Outcome System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/LevelManager.cs` (result-driven logic).  
Gap: outcome отделён от objective/rulebook неполно.

5. `Rulebook System`: `Missing`  
Evidence: policies/thresholds разложены по managers/assets, но нет единого rule-owner.

6. `Entity Backbone System`: `Missing/Fragmented`  
Evidence: lifecycle and identity are split between `Assets/Scripts/Game/Managers/UnitManager.cs`, `Assets/Scripts/Game/Managers/EnemyManager.cs`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Entity/EntityTransformResolver.cs`, `Packages/com.morboo.systems/Runtime/Identity/EntityIdAllocator.cs`.  
Gap: no single owner for entity lifecycle + registry + snapshots.

7. `Session & Profile System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/GameManager.cs` + state data classes.

8. `Save/Load System`: `Exists/Partial`  
Evidence: `Assets/Scripts/Game/Managers/GameManager.cs` (`Save/Load` plumbing).  
Gap: schema/version ownership явно не выделен отдельной системой.

9. `Economy Ledger System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/GameResourcesManager.cs`.  
Gap: нет строгого ledger abstraction на уровне package-контракта.

10. `Reward & Claim System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/RewardManager.cs`, `Assets/Scripts/Game/Rewards/Reward.cs`, `Assets/Scripts/Game/Managers/LootManager.cs`.

11. `Telemetry & LiveOps System`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/MyAnalyticsManager.cs`.  
Gap: нет полноценного LiveOps config/experiment owner.

## 7.2 Simulation Domains

1. `Orchestration Domain`: `Partial (advanced)`  
Evidence: `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/*` + audit gaps in `Orchestration_Implementation_Audit_2026-02-19.md`.

2. `Actors Domain`: `Partial`  
Evidence: `Assets/Scripts/Game/Core/Characters/Unit.cs`, `Assets/Scripts/Game/Core/Characters/Enemy.cs`.  
Gap: единый Actor model не выделен.

3. `Identity/Faction/Role`: `Partial`  
Evidence: `Packages/com.morboo.core/Runtime/Orchestration/Roles/RoleAsset.cs`, `Packages/com.morboo.core/Runtime/Orchestration/Factions/FactionAsset.cs`.

4. `Capabilities`: `Partial/Stub`  
Evidence: `Packages/com.morboo.core/Runtime/Orchestration/Capabilities/*`, providers в strategycombat.  
Gap: runtime-consumption почти отсутствует.

5. `Movement/Positioning/Formations`: `Partial`  
Evidence: `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Formations/*`, idle/combat movement policies.

6. `Combat Resolution`: `Partial`  
Evidence: `Assets/Scripts/Game/Core/Skills/*`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Combat/*`.

7. `Spawn/Encounter`: `Partial`  
Evidence: `Assets/Scripts/Game/Managers/EnemyManager.cs`, `Assets/Scripts/Game/Data/EnemyGenerationData.cs`.

8. `Board/Spatial`: `Exists/Partial`  
Evidence: `Assets/Scripts/Game/Core/Board/*`.

9. `Inventory/Equipment`: `Partial`  
Evidence: `Assets/Scripts/Game/Data/UnitData.cs`, `Assets/Scripts/Game/UI/InventoryList.cs`.

10. `Merge`: `Partial`  
Evidence: merge state embedded in unit data + UI paths.

11. `Progression/Upgrade`: `Partial`  
Evidence: `Assets/Scripts/Game/Data/UnitData.cs`, `Assets/Scripts/Game/Data/SkillData.cs`, `Assets/Scripts/Game/Managers/ExperienceManager.cs`.

## 7.3 Experience/Integration

1. `UI`: `Exists`  
Evidence: `Assets/Scripts/Game/UI/*`.

2. `Entity View Binding`: `Partial`  
Evidence: binding currently scattered across orchestration identities/executors and Unity components (`Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Units/UnitOrchestrationIdentity.cs`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Enemies/EnemyOrchestrationIdentity.cs`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Entity/EntityTransformResolver.cs`).  
Gap: no explicit binder system with clear ownership/invariants.

3. `Project Bridge`: `Exists (early)`  
Evidence: `Assets/Scripts/MorbooBridge/*`.

4. `Engine Adapter Layer`: `Partial`  
Evidence: `Assets/Scripts/Game/TopDownEngineExt/*`, `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Combat/TDE/*`.

## 8) Reuse Strategy Across Genres

Переиспользование достигается на уровнях:

1. `Kernel contracts` — одинаковые для Warcraft/Diablo/HoMM/Hotel Mania/Pokemon/Monopoly-like.
2. `Domain packs` — разные реализации при одинаковых входных контрактах.
3. `Rulebook packs` — контентно-жанровые наборы без смены kernel.
4. `Experience adapters` — UI/engine/platform-specific wrappers.

## 8.1 Package Placement By Reuse Level

Фиксируем целевой маппинг по уровням абстракции:

1. `Любая игра (универсальные контракты/типы)` -> `Packages/com.morboo.framework`
2. `Любая игра (универсальная runtime-инфра)` -> `Packages/com.morboo.systems`
3. `Кросс-жанровый game kernel и доменно-независимые контракты` -> `Packages/com.morboo.core`
4. `Кросс-жанровый host/runtime execution` -> `Packages/com.morboo.runtimehost`
5. `Жанровый слой (StrategyCombat family)` -> `Packages/com.morboo.integration.strategycombat`
6. `Конкретная игра (wiring, контентные мапы, сцены, UI glue)` -> `Assets/Scripts/MorbooBridge` и `Assets/Scripts/Game`

Нормативные запреты:

1. `Kernel contracts` размещаются только в `com.morboo.framework` / `com.morboo.core` (host seams в `com.morboo.runtimehost`).
2. Истиной по `game flow/objective/outcome` владеют только `Kernel systems` (Tier A), не feature-код проектного слоя.
3. `com.morboo.*` packages не зависят от project-слоя.
4. Новые семейства пакетов вводятся только через ADR.

## 8.2 Entity Placement Rules (Migration-Time)

Чтобы не расползалась ownership-модель сущностей, при добавлении новых entity-related типов использовать только этот маппинг:

1. `EntityId`, базовые readonly-query контракты, event/command base contracts -> `com.morboo.framework`.
2. Универсальные runtime-сервисы (`allocator`, generic `registry` infra helpers, scheduler-safe lifecycle helpers) -> `com.morboo.systems`.
3. Кросс-жанровые entity kernel contracts/models (`IEntityRegistry`, `IEntityFactory`, `IEntityLifecycleService`, базовый `EntityState`) -> `com.morboo.core`.
4. Host-side execution seams для entity pipeline (tick-time orchestration hooks, routing contracts) -> `com.morboo.runtimehost`.
5. Жанровые расширения (`combat/idle` capabilities, strategycombat payloads, targeting-specific entity policies) -> `com.morboo.integration.strategycombat`.
6. Project-only binding/config/content maps (scene binders, concrete prefab maps, bootstrap glue) -> `Assets/Scripts/MorbooBridge` + `Assets/Scripts/Game`.

Запрет:

1. Entity kernel contracts не объявляются вне `com.morboo.framework` / `com.morboo.core`.

## 8.3 System Communication Rule (No Direct Coupling)

Системы не общаются напрямую concrete-to-concrete. Разрешённые каналы:

1. Команды/события через bus/dispatcher контракты.
2. Query через интерфейсные порты (`I*Query`/`I*Provider`), без знания concrete реализации другой системы.
3. Публичные API систем допускаются только как контракты владельца системы, а не как прямые вызовы внутрь чужой реализации.

Правило приоритета:

1. Перед добавлением новой межсистемной связки сначала использовать существующие контракты/порты/паттерны интеграции; новый обходной канал допускается только с ADR и планом удаления.

Запрет:

1. `SystemA` импортирует/создаёт concrete классы `SystemB` и вызывает их runtime-методы напрямую.
2. Общение через shared mutable state между системами вне owner-контракта.

## 8.4 Odin Policy (Data Authoring Only)

Допустимость `Sirenix.Odin`:

1. Разрешено для authoring/data файлов (`SerializedScriptableObject`, editor drawers, tooling) в `UnityEditor` контексте.
2. Разрешено в project/integration data слое, если это не создаёт обязательную runtime-зависимость для нижних пакетов.
3. В `com.morboo.framework`, `com.morboo.systems`, `com.morboo.core`, `com.morboo.runtimehost` runtime-коде использование Odin запрещено.

Правило:

1. Odin не должен быть required-dependency для kernel/runtime слоёв.

## 8.5 Folder Placement Rules Inside A Layer

Внутри каждого слоя используется единая схема:

1. Код, используемый только одной системой, лежит в папке этой системы (`<Layer>/<SystemName>/...`).
2. Код, используемый как минимум двумя системами одного слоя, переносится в `<Layer>/Common/...`.
3. Перенос в `Common` допускается только при наличии стабильного контракта и двух реальных потребителей.
4. `Common` не используется как свалка “на будущее”; если второй потребитель исчез, код возвращается в owning-system папку.

## 8.6 Anti-File-Sprawl Rule (Complexity Budget)

Если для подключения новой сущности/домена требуется “куча файлов”, это считается архитектурным запахом.

Обязательные проверки при каждом PR с новой системой/доменом:

1. `Entity onboarding touchpoints`: сколько файлов нужно изменить, чтобы добавить новый archetype/entity type.
2. `Domain wiring fan-out`: сколько обязательных orchestrator/policy/adapter/map файлов требуется для минимального сценария.
3. Если fan-out выше согласованного бюджета команды, PR обязан включать план упрощения (consolidation/extraction) или ADR-обоснование.
4. Различия по доменам/сценариям сначала пытаемся выразить `data-driven` (policies/maps/config), а не добавлением новых веток кода.

Практическая цель:

1. Добавление новой сущности должно быть конфигурационно-ориентированным, а не требовать каскада новых классов и интерфейсов.
2. Разделение на файлы делается по ответственности, а не по “один класс = один микро-файл” без пользы.
3. Новый кодовый путь вводится только после явного обоснования, почему вариацию нельзя выразить данными.

## 8.7 Typed Reference Rule (No Untyped Dependency Holders)

Для межсистемных runtime-зависимостей запрещены нетипизированные ссылки:

1. Не использовать `GameObject`/`MonoBehaviour`/`Component` поле как универсальный контейнер, из которого потом ищется нужный интерфейс через `GetComponent`.
2. Для dependency wiring использовать типизированные ссылки:
   - required concrete type,
   - typed provider interface,
   - explicit registration/composition.
3. Допустимы `GameObject`/`Transform` ссылки только как view/content references (UI/prefab anchors), не как service locator.

Проверка:

1. Любая нетипизированная ссылка, сохранённая в системе, требует явного обоснования в blueprint/PR + план удаления.

## 9) Governance Rules (Anti-Emergent)

1. Новая фича не стартует без назначения `System Owner` из каталога.
2. Для новой системы/крупного рефактора сначала заполняется blueprint по `New_System_Requirements_Template.md`.
3. Если подходящей системы нет, сначала создаётся контракт системы (интерфейс + asmdef boundary), потом код фичи.
4. Один PR = одна системная цель + один архитектурный тест, который это правило фиксирует.
5. Запрещено добавлять новый runtime-код с прямой зависимостью на TDE вне adapter-слоя.
6. Запрещено хранить flow/objective/outcome rules в UI/MonoBehaviour glue.
7. При создании новой системы сначала проверяется переиспользование существующих контрактов и `Common`-компонентов.
8. Если общий фрагмент нужен нескольким системам, он выносится на общий уровень (`Common`/lower package) до дублирования.
9. Нетипизированные dependency refs (`GameObject`/`MonoBehaviour`/`Component` как service locator) не добавляются без approved ADR.
10. Для любой фичи действует `architecture-first`: сначала reuse существующих контрактов/паттернов/extension points, и только потом новый локальный путь.

## 10) Immediate Next Step

Исполнять `Master_Migration_Roadmap.md` как программу миграции, начиная с:

1. `Phase 1` (guardrails baseline),
2. `Phase 2` (kernel + entity contracts),
3. `Phase 3` (entity backbone foundation).

Параллельно добавить короткий `Entity_Backbone_Spec.md` с:

1. формальными инвариантами `single source of truth`,
2. правилами lifecycle (`create/destroy/lookup/snapshot`),
3. границей `entity -> view binding` и запретом доменной логики во view.
