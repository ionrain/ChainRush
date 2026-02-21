# Phase 3 Entity Backbone Playtest Checklist

Date: 2026-02-20  
Phase: `Phase 3 — Entity Backbone Foundation`  
Slice: `C3.2 Manager Bridge To Registry Facade`  
Owner: `Architecture & Gameplay Owner`  
Status: `Closed (owner sign-off)`

## Goal

Подтвердить, что bridge-связка (`UnitManager`/`EnemyManager` -> `Entity Backbone`) работает корректно и не меняет поведение игры.

## Scene / Setup

1. Scene: `Assets/Game/Scenes/Level.unity`
2. Object: `EnemyManager` (в сцене содержит `GameEntityBackboneBridge`)
3. Component refs:
   - `unitManager` -> `UnitManager`
   - `enemyManager` -> `EnemyManager`

## Runtime Checks

1. [x] Войти в Play Mode, убедиться что `GameEntityBackboneBridge` активен.
2. [x] Проверить debug-поля на bridge:
   - `registeredEntityCount` > 0 после начала уровня,
   - `createdEventsCount` растет при спавне,
   - `destroyedEventsCount` растет при смертях/очистке.
3. [x] Дождаться нескольких спавнов и убийств:
   - `registeredEntityCount` корректно увеличивается/уменьшается.
4. [x] Включить `logLifecycleEvents` (опционально) и проверить, что логи регистрации/дерегистрации соответствуют игровым событиям.

## Regression Checks

1. [x] Спавн юнитов/врагов работает как до bridge.
2. [x] Победа/поражение и прогресс волны не изменились.
3. [x] Нет Missing Script в `Level.unity`.
4. [x] Нет новых ошибок в Console, связанных с `GameEntityBackboneBridge`.

## Close Decision (2026-02-20)

1. Slice `C3.2` принят: bridge подключен в `Assets/Game/Scenes/Level.unity`.
2. Lifecycle smoke-path подтвержден: менеджеры (`UnitManager`/`EnemyManager`) публикуют события создания/удаления, bridge обновляет entity registry.
3. Решение по этапу: `Phase 3` продолжается с `C3.3 Entity Source-Of-Truth Hardening`.
