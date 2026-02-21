# Phase 3 C3.3 Entity Source-Of-Truth Hardening Plan

Date: 2026-02-20  
Phase: `Phase 3 — Entity Backbone Foundation`  
Slice: `C3.3 Entity Source-Of-Truth Hardening`  
Status: `Closed (C3.3.1/C3.3.2/C3.3.3/C3.3.4 implemented; exit checklist/sign-off completed)`

## Goal

Сделать `Entity Backbone` единственным источником истины для состояния сущностей в мигрируемом runtime-пути, без изменения игрового поведения.

## Scope (In)

1. `Unit/Enemy` lifecycle и state-read/state-write пути, которые уже подключены через `GameEntityBackboneBridge`.
2. Устранение double-source состояния в мигрируемых местах (`EntityId`-first reads).
3. Добавление guardrail-тестов на архитектурные нарушения для C3.3.

## Scope (Out)

1. Полный рефактор всех gameplay систем (это фазы 4+ и 6).
2. Переписывание доменной логики оркестратора.
3. Полный save/load pipeline и реплеи.

## Execution Slices (Commit Plan)

1. [x] `C3.3.1 Define Authoritative State Contract`
   - зафиксировать список полей, которые считаются authoritative в `EntityState` для мигрируемого пути,
   - добавить/уточнить query API для безопасного чтения по `EntityId`,
   - договориться о правилах sync (`model -> view` единственное направление истины).

2. [x] `C3.3.2 Redirect Read Paths`
   - перевести выбранные runtime-read точки с `Unit/Enemy` прямого чтения на чтение через `EntityId` + registry/query seam,
   - оставить `MonoBehaviour` как view/adapter, не как источник истины.

3. [x] `C3.3.3 Redirect Write Paths`
   - перевести мигрируемые write-пути (создание, удаление, критичные state transitions) на model-first обновление,
   - обеспечить корректную синхронизацию в view.

4. [x] `C3.3.4 Remove Double-Source In Migrated Paths`
   - убрать дублирующее чтение/запись state в мигрированных путях,
   - зафиксировать остаточные legacy-paths как явный technical debt (если остались).

## Mandatory Fitness Tests (for C3.3)

1. `EntityBackbone_MigratedPaths_DoNotReadStateFromUnitEnemyDirectly`
2. `EntityBackbone_MigratedPaths_UseEntityIdQueries`
3. `EntityBackbone_ModelViewSync_NoDoubleSourceWritesInMigratedScope`
4. `EntityBackbone_BridgeLifecycle_StillBehaviorNeutral` (smoke)

## Playtest Checklist (C3.3 Exit)

1. [x] Базовый loop уровня работает как до миграции.
2. [x] Спавн/деспавн не регресснул.
3. [x] Мигрированные фичи читают state через `EntityId`, не через прямой доступ к `Unit/Enemy`.
4. [x] В мигрируемом scope нет double-source state update.
5. [x] Нет новых runtime ошибок в Console.

## Entry Gate

1. `C3.1` закрыт.
2. `C3.2` закрыт.
3. Scene wiring bridge уже в `Assets/Game/Scenes/Level.unity`.

## Exit Gate

1. В мигрированном scope источником истины является `Entity Backbone`.
2. Основные runtime-read/runtime-write пути переведены на `EntityId`-first.
3. C3.3 fitness-tests добавлены и green.
4. Playtest checklist закрыт owner sign-off.

## Notes

1. Любой bypass к `Transform/MonoBehaviour` как источнику state в мигрированном scope считается нарушением C3.3.
2. Если для стабильности нужен временный adapter, он должен быть односторонним (`model -> view`) и иметь явный removal target.

## Progress Log

1. 2026-02-20: `C3.3.1` выполнен.
2. Добавлены `IEntityStateAccessor` и `IEntityStateQuery` в `com.morboo.core`.
3. `InMemoryEntityRegistry` реализует typed state query.
4. `GameEntityBackboneBridge` переведен с downcast (`IEntityModel -> EntityState`) на typed seam (`TryGetState`).
5. Добавлены smoke/architecture tests для нового контракта и запрета downcast в bridge.
6. 2026-02-20: `C3.3.2` выполнен для `UnitStateReporter`/`EnemyStateReporter` read-path.
7. Добавлен runtime context (`EntityBackboneRuntimeContext`) для package-level read access к `IEntityStateQuery`.
8. `GameEntityBackboneBridge`:
   - публикует runtime context,
   - выравнивает registration EntityId с `IEntityIdProvider` (если доступен),
   - заполняет bridge-layer compatibility traits (`BridgeEntityStateTraitKeys`).
9. State-reporters в StrategyCombat читают migrated metadata из `EntityState` через `TryGetState`, с fallback на legacy source.
10. 2026-02-20: `C3.3.3` выполнен для writer-path в `UnitStateReporter`/`EnemyStateReporter`.
11. Репортеры синхронизируют authoritative liveness в `IEntityStateAccessor` через `SetAlive`; snapshot metrics (Hp01/MergeState/EnemyType/UnitClass) остаются behavior-neutral и читаются из текущих runtime owners.
12. Добавлен architecture guardrail: state-reporters обязаны иметь write-back в Entity Backbone.
13. 2026-02-20: `C3.3.4` выполнен для migrated reporter scope.
14. Legacy `entityState == null` fallback branches удалены из migrated state-reporter scope.
15. Добавлен guardrail-тест на запрет использования legacy fallback branches и на обязательный write-back в `IEntityStateAccessor`.
16. 2026-02-21: exit checklist закрыт, C3.3 переведен в `closed`.
17. Transitional `EntityStateTraitKeys` удален из `com.morboo.core` и из package-runtime usages; оставлен только Bridge-layer compatibility key set.
18. Добавлен active architecture gate на запрет trait-key compatibility constants в package runtime слоях.
