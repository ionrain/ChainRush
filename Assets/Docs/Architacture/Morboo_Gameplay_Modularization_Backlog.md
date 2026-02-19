# Morboo.Gameplay Modularization Backlog

Date: 2026-02-19  
Scope: `Assets/Scripts/Game/**` -> `Morboo.Gameplay` modules (packages), без поломки текущих слоёв.

## 1) Цель

Вынести reusable gameplay-логику из `Game.Runtime` в `Morboo.Gameplay` с:

1. более абстрактными контрактами (чтобы текущий код встраивался, а не переписывался с нуля),
2. разделением по системам (Units, Combat, Economy, Goals, UI и т.д.),
3. сохранением архитектуры слоёв:
   `Framework -> Systems -> Core -> RuntimeHost -> Integration.StrategyCombat -> Gameplay -> Bridge -> Project`.

Ключевой принцип: переносим доменную логику в Gameplay-пакеты, а проектные интеграции (analytics/social/sdk/local notifications/scene glue) оставляем в `MorbooBridge` и `Assets/Scripts/Game` до отдельной фазы.

## 2) Что уже есть (инвентарь)

Measured snapshot:

1. `Assets/Scripts/Game`: 193 `.cs` файлов.
2. Прямые `TopDownEngine`-ссылки в runtime code: 27 файлов.
3. Сильные доменные кластеры уже видны по структуре:
   - `Core/Characters`, `Core/Skills`, `Core/LevelGoals`, `Core/ItemDrop`, `Core/Board`
   - `Managers/*`
   - `Data/*`
   - `UI/*`
   - `Rewards/*`, `Triggers/*`

Это хороший кандидат на модульный split без смены поведения.

## 3) Целевая структура Morboo.Gameplay

Рекомендация: один UPM-пакет `com.morboo.gameplay` + несколько asmdef внутри, чтобы:

1. не размножать сразу десятки пакетов,
2. получить строгие границы между подсистемами,
3. позже при необходимости вынести самые зрелые модули в отдельные UPM.

### 3.1 Package skeleton

`Packages/com.morboo.gameplay/Runtime/`

- `Abstractions/` (`Morboo.Gameplay.Abstractions`)
- `Events/` (`Morboo.Gameplay.Events`)
- `Stats/` (`Morboo.Gameplay.Stats`)
- `Units/` (`Morboo.Gameplay.Units`)
- `Abilities/` (`Morboo.Gameplay.Abilities`)
- `Combat/` (`Morboo.Gameplay.Combat`)
- `Spawning/` (`Morboo.Gameplay.Spawning`)
- `Board/` (`Morboo.Gameplay.Board`)
- `Goals/` (`Morboo.Gameplay.Goals`)
- `LevelFlow/` (`Morboo.Gameplay.LevelFlow`)
- `Economy/` (`Morboo.Gameplay.Economy`)
- `Inventory/` (`Morboo.Gameplay.Inventory`) [optional module]
- `Merge/` (`Morboo.Gameplay.Merge`) [optional module]
- `Rewards/` (`Morboo.Gameplay.Rewards`)
- `UI.Widgets/` (`Morboo.Gameplay.UI.Widgets`)
- `UI.Presenters/` (`Morboo.Gameplay.UI.Presenters`)
- `Adapters.TopDown/` (`Morboo.Gameplay.Adapters.TopDown`) [temporary, migration-only]

### 3.2 Dependency rules (DAG)

1. `Morboo.Gameplay.Abstractions` -> `Morboo.Framework`
2. `Morboo.Gameplay.Events` -> `Morboo.Gameplay.Abstractions`
3. `Morboo.Gameplay.Stats` -> `Morboo.Gameplay.Abstractions`
4. `Morboo.Gameplay.Units` -> `Abstractions`, `Stats`, `Events`
5. `Morboo.Gameplay.Abilities` -> `Abstractions`, `Stats`, `Events`
6. `Morboo.Gameplay.Combat` -> `Units`, `Abilities`, `Stats`
7. `Morboo.Gameplay.Spawning` -> `Units`, `Combat`
8. `Morboo.Gameplay.Board` -> `Units`, `Combat`
9. `Morboo.Gameplay.Goals` -> `Abstractions`, `Events`, `Units`
10. `Morboo.Gameplay.LevelFlow` -> `Goals`, `Spawning`
11. `Morboo.Gameplay.Economy` -> `Abstractions`, `Events`
12. `Morboo.Gameplay.Inventory` -> `Economy`, `Abstractions` [optional]
13. `Morboo.Gameplay.Merge` -> `Units`, `Inventory`, `Economy` [optional]
14. `Morboo.Gameplay.Rewards` -> `Economy`, `Inventory`, `Units`
15. `Morboo.Gameplay.UI.Widgets` -> (Unity only, без доменных ссылок)
16. `Morboo.Gameplay.UI.Presenters` -> `UI.Widgets` + нужные gameplay-модули
17. `Morboo.Gameplay.Adapters.TopDown` -> gameplay modules + `MoreMountains.TopDownEngine` (временный слой)

Critical bans:

1. Ни один `Morboo.Gameplay.*` asmdef не ссылается на `Game.Runtime`.
2. Ни один `Morboo.Gameplay.*` asmdef не ссылается на `Morboo.Bridge`.
3. `RuntimeHost/Core/Framework` не ссылаются на `Morboo.Gameplay.*`.

## 4) Какие абстракции добавить (чтобы существующее встроить)

## 4.1 Units / Enemy unification

Новая абстракция:

1. `ActorDefinition` (вместо раздвоения UnitData/EnemyData по ядру).
2. `ActorRuntime` (общий runtime-носитель, enemy как частный случай через role/faction flags).
3. `FactionId`, `ActorKind`, `RoleId`, `SpawnTag`.
4. `IActorLifecycle`, `IActorCombatant`, `IActorTargetable`.

Что это даёт:

1. Enemy становится конфигурацией `ActorDefinition`, а не отдельной базовой моделью.
2. UnitManager/EnemyManager режутся на сервисы `ActorRosterService` + `SpawnService`.

## 4.2 Stats/Attributes

Новая абстракция:

1. `StatId` (ContentId-based) вместо жёсткого enum-гейта.
2. `StatValue`, `StatModifier`, `ModifierOp`.
3. `ElementId` как data-driven идентификатор.

Что это даёт:

1. Добавление новых атрибутов без перекомпиляции enum-кода.
2. Skills, Buffs, Economy, Goals используют единый stat-пайплайн.

## 4.3 Skills -> Abilities

Новая абстракция:

1. `AbilityDefinition`
2. `AbilityLevelDefinition`
3. `AbilityEffect` (композиция эффектов)
4. `TargetingPolicy`
5. `CooldownPolicy`

Что это даёт:

1. Не только боевые active/passive скиллы.
2. Встраивание non-combat abilities без отдельной ветки логики.

## 4.4 Economy

Новая абстракция:

1. `Wallet`, `ResourceId`, `Transaction`, `Cost`
2. `ITransactionPolicy` (ограничения и валидация)
3. `EconomyEvent` (унифицированная публикация)

Что это даёт:

1. Все покупки/награды/апгрейды через единый ledger.
2. Goal/Reward/Inventory интегрируются без прямых вызовов Manager-to-Manager.

## 4.5 Goals

Новая абстракция:

1. `ObjectiveDefinition`
2. `ObjectiveProgress`
3. `ObjectiveTracker`
4. `ObjectivePredicate` (collection/enemy/resource/time/etc)

Что это даёт:

1. `LevelGoal` становится частным типом objective.
2. Goals можно переиспользовать за пределами текущего режима.

## 4.6 Inventory + Merge (optional modules)

Inventory:

1. `ItemDefinition`
2. `InventoryContainer`
3. `InventoryRuleSet`

Merge:

1. `MergeRecipe`
2. `MergeRuleSet`
3. `MergeResolver`

Что это даёт:

1. Оба модуля подключаемые (feature toggle), без загрязнения базового Units API.

## 4.7 UI

Новая абстракция:

1. `UI.Widgets` только reusable components (`Popup`, `ItemList`, `ListItem`, кнопочные/лейбл шаблоны).
2. `UI.Presenters` связывают widgets и gameplay-события.
3. Правила игры остаются вне UI.

## 5) Маппинг текущих компонентов -> Morboo.Gameplay модули

## 5.1 Units / Spawning / Combat / AI

`Morboo.Gameplay.Units`

- `Assets/Scripts/Game/Core/Characters/Unit.cs`
- `Assets/Scripts/Game/Core/Characters/Enemy.cs`
- `Assets/Scripts/Game/Managers/UnitManager.cs` (roster/lifecycle часть)
- `Assets/Scripts/Game/Data/UnitData.cs` (ядро actor-модели)
- `Assets/Scripts/Game/Data/EnemyData.cs` (после унификации -> ActorDefinition variant)
- `Assets/Scripts/Game/Data/AllUnitsData.cs`
- `Assets/Scripts/Game/Data/AllEnemiesData.cs`
- `Assets/Scripts/Game/Data/FormationProfile.cs`
- `Assets/Scripts/Game/Data/UnitAIProfile.cs`

`Morboo.Gameplay.Spawning`

- `Assets/Scripts/Game/Managers/EnemyManager.cs` (spawn/wave часть)
- `Assets/Scripts/Game/Managers/UnitManager.cs` (spawn часть)
- `Assets/Scripts/Game/Core/Characters/IPostSpawnSetup.cs`
- `Assets/Scripts/Game/Data/EnemyGenerationData.cs`
- `Assets/Scripts/Game/Data/ItemGenerationData.cs`

`Morboo.Gameplay.Combat`

- `Assets/Scripts/Game/Core/Characters/WeaponManager.cs`
- `Assets/Scripts/Game/Core/Characters/AttackMark.cs`
- `Assets/Scripts/Game/Core/Characters/DamageOnTouchController.cs`
- `Assets/Scripts/Game/Core/Characters/EnemyProjectileWeapon.cs`
- `Assets/Scripts/Game/Core/EnemyRemover.cs`
- `Assets/Scripts/Game/Core/Skills/AttackSkill.cs`
- `Assets/Scripts/Game/Core/Skills/DistantAttackSkill.cs`
- `Assets/Scripts/Game/Core/Skills/DistantWeapon.cs`
- `Assets/Scripts/Game/Core/Skills/MeleeAttackSkill.cs`
- `Assets/Scripts/Game/Core/Skills/SupportSkill.cs`
- `Assets/Scripts/Game/Core/Skills/AttackSkillAimer.cs`
- `Assets/Scripts/Game/Core/Skills/TargetOrbiter.cs`
- `Assets/Scripts/Game/Data/SkillData.cs`
- `Assets/Scripts/Game/Data/ElementsData.cs`
- `Assets/Scripts/Game/Data/BuffsData.cs`

`Morboo.Gameplay.Adapters.TopDown` (temporary)

- `Assets/Scripts/Game/TopDownEngineExt/**`
- TDE-specific части из `Unit/Enemy/Skill` пока не снят TDE

## 5.2 Board / Goals / LevelFlow

`Morboo.Gameplay.Board`

- `Assets/Scripts/Game/Core/Board/Board.cs`
- `Assets/Scripts/Game/Core/Board/Cell.cs`
- `Assets/Scripts/Game/Core/Board/CellItem.cs`
- `Assets/Scripts/Game/Core/Board/CellTrap.cs`

`Morboo.Gameplay.Goals`

- `Assets/Scripts/Game/Core/LevelGoals/**`
- `Assets/Scripts/Game/Managers/LevelGoalManager.cs`

`Morboo.Gameplay.LevelFlow`

- `Assets/Scripts/Game/Managers/LevelManager.cs`
- `Assets/Scripts/Game/Data/LevelData.cs`
- `Assets/Scripts/Game/Data/LevelDifficultyData.cs`
- `Assets/Scripts/Game/Data/LocationData.cs`
- `Assets/Scripts/Game/Data/AllLocationsData.cs`

## 5.3 Economy / Inventory / Merge / Rewards

`Morboo.Gameplay.Economy`

- `Assets/Scripts/Game/Managers/GameResourcesManager.cs`
- `Assets/Scripts/Game/Managers/ExperienceManager.cs`
- `Assets/Scripts/Game/Data/ResourcesData.cs`
- `Assets/Scripts/Game/Data/BankData.cs`
- `Assets/Scripts/Game/Data/BankItemData.cs`
- `Assets/Scripts/Game/Data/BoostersData.cs`

`Morboo.Gameplay.Inventory` [optional]

- `Assets/Scripts/Game/Data/ItemData.cs`
- `Assets/Scripts/Game/Data/ItemTemplateData.cs`
- `Assets/Scripts/Game/Data/AllItemsData.cs`
- inventory часть в `UnitData.cs`
- `Assets/Scripts/Game/UI/InventoryList.cs`
- `Assets/Scripts/Game/UI/InventoryItem.cs`

`Morboo.Gameplay.Merge` [optional]

- merge часть в `Assets/Scripts/Game/Data/UnitData.cs` (`MergeStateData`)
- merge часть в `Assets/Scripts/Game/Core/Characters/Unit.cs`
- `Assets/Scripts/Game/UI/UnitUI/UnitMergePanel.cs`

`Morboo.Gameplay.Rewards`

- `Assets/Scripts/Game/Rewards/**`
- `Assets/Scripts/Game/Core/ItemDrop/**`
- `Assets/Scripts/Game/Managers/RewardManager.cs`
- `Assets/Scripts/Game/Managers/LootManager.cs`
- `Assets/Scripts/Game/Managers/RewardFlyManager.cs`
- `Assets/Scripts/Game/Data/RewardsData.cs`
- `Assets/Scripts/Game/Data/DailyRewardsData.cs`

## 5.4 UI (widgets vs screens)

`Morboo.Gameplay.UI.Widgets`

- `Assets/Scripts/Game/UI/Popup.cs`
- `Assets/Scripts/Game/UI/ItemList.cs`
- `Assets/Scripts/Game/UI/ListItem.cs`
- `Assets/Scripts/Game/UI/ItemPopup.cs`
- `Assets/Scripts/Game/UI/Progressbar.cs`
- `Assets/Scripts/Game/UI/TextItem.cs`
- `Assets/Scripts/Game/UI/IconTextItem.cs`
- `Assets/Scripts/Game/UI/IconMultiTextItem.cs`
- `Assets/Scripts/Game/UI/BuyButton.cs`
- `Assets/Scripts/Game/UI/RewardContainer.cs`

`Morboo.Gameplay.UI.Presenters`

- `Assets/Scripts/Game/UI/Notifications/**`
- `Assets/Scripts/Game/UI/BattleUI/**`
- `Assets/Scripts/Game/UI/UnitUI/**`
- `Assets/Scripts/Game/UI/Bank/**`
- `Assets/Scripts/Game/UI/LevelUI/**`
- `Assets/Scripts/Game/UI/Tabs/**`
- `Assets/Scripts/Game/UI/Tutorial/**`
- `Assets/Scripts/Game/UI/RewardPopup.cs`
- `Assets/Scripts/Game/UI/RewardListPopup.cs`
- `Assets/Scripts/Game/UI/CheatsPopup.cs`
- `Assets/Scripts/Game/UI/SceneLoader.cs`
- `Assets/Scripts/Game/UI/SafeArea/**`

## 5.5 Events / Utils / Infra

`Morboo.Gameplay.Events`

- `Assets/Scripts/Game/Triggers/**`
- event structs сейчас разбросаны по `Managers/*`, `Core/*`, `Data/*` -> постепенно консолидировать в module-local event contracts

`Morboo.Gameplay.Abstractions`

- `Assets/Scripts/Game/OverTimeAction.cs` (runtime utility)
- `Assets/Scripts/Game/ScreenBounds.cs`
- `Assets/Scripts/Game/MyExtension.cs` (разбить на module-specific extensions)

## 5.6 Что оставить project-specific (не переносить в Gameplay на первом проходе)

- `Assets/Scripts/Game/Managers/MyAnalyticsManager.cs`
- `Assets/Scripts/Game/Managers/SocialManager.cs`
- `Assets/Scripts/Game/Managers/LocalNotificationsManager.cs`
- `Assets/Scripts/Game/TinySaucePreloader.cs`
- `Assets/Scripts/Game/Managers/CinemachineCameraManager.cs`
- `Assets/Scripts/Game/Managers/BackgroundScroller.cs`
- `Assets/Scripts/Game/SpineSkeletonModel.cs` (если остаётся строго project-art binding)

## 6) Пошаговый план миграции (без параллельной структуры)

## Slice G0 — Package/asmdef scaffolding

1. Создать `Packages/com.morboo.gameplay/` + asmdef по модулям (пустые).
2. Зафиксировать DAG зависимостей.
3. Добавить архитектурные тесты на запреты ссылок.

Acceptance:

- compile green,
- asmdef graph acyclic.

## Slice G1 — Abstractions + Events + Stats

1. Ввести `StatId`, `ResourceId`, `ActorId`, базовые event contracts.
2. Не переносить поведение, только контракты + адаптеры совместимости.

Acceptance:

- существующий код компилируется через переходные мапперы,
- без изменения поведения.

## Slice G2 — Units unification (Unit/Enemy -> Actor)

1. Создать `ActorDefinition`/`ActorRuntime`.
2. Перенести `UnitData/EnemyData` в общий формат с `FactionId`.
3. Разделить `UnitManager/EnemyManager` на:
   - `ActorRosterService`
   - `ActorSpawnService`

Acceptance:

- Enemy работает как частный случай Actor,
- старые prefabs совместимы через adapter layer.

## Slice G3 — Abilities/Combat split

1. Перенести `Core/Skills` и боевые части `Core/Characters` в `Abilities` + `Combat`.
2. Ввести data-driven ability parameters (не enum-only).
3. Сохранить старую формулу через compatibility converters.

Acceptance:

- бой и таргетинг без регрессий,
- нет cross-calls manager-to-manager вне модулей.

## Slice G4 — Board + Goals + LevelFlow

1. Перенести `Core/Board`, `Core/LevelGoals`, `LevelManager`.
2. Ввести `ObjectiveTracker` и `LevelFlowOrchestrator`.

Acceptance:

- цели уровня и прогресс уровня совпадают по поведению.

## Slice G5 — Economy + Inventory + Merge

1. Внедрить `Wallet/Transaction`.
2. Перенести inventory в отдельный optional module.
3. Перенести merge в optional module с зависимостью на Units+Economy.

Acceptance:

- покупка/траты/баланс и merge работают через единый transaction pipeline.

## Slice G6 — Rewards/Loot

1. Перенести `Rewards/*`, `Core/ItemDrop/*`, reward-managers.
2. Подключить к Economy/Inventory через contracts, а не прямые менеджеры.

Acceptance:

- выдача наград и дропы без регрессий.

## Slice G7 — UI split

1. Перенести reusable widgets в `UI.Widgets`.
2. Перенести экраны/презентеры в `UI.Presenters`.
3. UI читает только view models/events.

Acceptance:

- UI без бизнес-правил.

## Slice G8 — Bridge rewire + cleanup

1. `Morboo.Bridge` ссылается на `Morboo.Gameplay.*`, а не на legacy `Game.Runtime` домены.
2. Удалить дубли в `Assets/Scripts/Game`.
3. Legacy folder cleanup после подтверждения parity.

Acceptance:

- `Game.Runtime` остаётся тонким project shell,
- `Morboo.Gameplay` несёт gameplay ядро.

## 7) Architecture tests (обязательные)

Добавить/обновить:

1. `GameplayPackages_HaveNoProjectRefs`  
   scan `Packages/com.morboo.gameplay/**` на `Assets/Scripts`, `Morboo.Bridge`, `Game.Runtime`.

2. `GameplayUiWidgets_HasNoDomainRules`  
   запрет на gameplay services inside `UI.Widgets`.

3. `GameplayEconomy_NoDirectUiCalls`  
   `Economy` не знает про UI namespace.

4. `GameplayGoals_NoManagerCrossCalls`  
   запрет прямых вызовов чужих manager классов.

5. `RuntimeHost_Core_DoNotDependOnGameplay`  
   строгий запрет ссылок на `Morboo.Gameplay.*` из `Core/RuntimeHost`.

## 8) Команды быстрой проверки

```bash
rg -n "Game\.Runtime|Morboo\.Bridge|Assets/Scripts" Packages/com.morboo.gameplay --glob "*.cs" --glob "*.asmdef"
rg -n "using UnityEngine" Packages/com.morboo.gameplay/Runtime/Abstractions --glob "*.cs"
rg -n "MoreMountains\.TopDownEngine" Packages/com.morboo.gameplay Assets/Scripts/Game --glob "*.cs"
rg -n "Morboo\.Gameplay" Assets/Scripts/Game --glob "*.cs"
```

## 9) Рекомендуемый первый PR

Small, safe, compile-first:

1. Создать `com.morboo.gameplay` package + asmdef skeleton (без переносов).
2. Вынести только `Abstractions/Events/Stats` контракты.
3. Добавить архитектурные тесты из раздела 7.

Это даст каркас для дальнейшего переноса Units/Economy/UI без хаотичных пересечений.

