# Phase 3 Entity Backbone Playtest Checklist

Date: 2026-02-20  
Phase: `Phase 3 — Entity Backbone Foundation`  
Slice: `C3.2 Manager Bridge To Registry Facade`  
Status: `Ready for manual execution`

## Goal

Подтвердить, что bridge-связка (`UnitManager`/`EnemyManager` -> `Entity Backbone`) работает корректно и не меняет поведение игры.

## Scene / Setup

1. Scene: `Assets/Game/Scenes/Level.unity`
2. Object: `EnemyManager` (в сцене содержит `GameEntityBackboneBridge`)
3. Component refs:
   - `unitManager` -> `UnitManager`
   - `enemyManager` -> `EnemyManager`

## Runtime Checks

1. Войти в Play Mode, убедиться что `GameEntityBackboneBridge` активен.
2. Проверить debug-поля на bridge:
   - `registeredEntityCount` > 0 после начала уровня,
   - `createdEventsCount` растет при спавне,
   - `destroyedEventsCount` растет при смертях/очистке.
3. Дождаться нескольких спавнов и убийств:
   - `registeredEntityCount` должен корректно увеличиваться/уменьшаться.
4. Включить `logLifecycleEvents` (опционально) и проверить, что логи регистрации/дeregister соответствуют игровым событиям.

## Regression Checks

1. Спавн юнитов/врагов работает как до bridge.
2. Победа/поражение и прогресс волны не изменились.
3. Нет Missing Script в `Level.unity`.
4. Нет новых ошибок в Console, связанных с `GameEntityBackboneBridge`.
