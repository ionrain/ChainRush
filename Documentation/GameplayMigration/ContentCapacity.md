# Вместимость контента части 1

Implementation decision: **working model**. Снимок authored-настроек от 2026-09-23. Это чтение данных и расчёт ограничений, без исполнения игрового сценария. Размеры пулов и правила игры не изменены.

## Existing System Fit

- `Population/Analytics` ограничивают живых врагов вместе с принятыми outputs; `Production` владеет заказами. Очередь производителя ограничивает незавершённую работу, а не число уже выпущенных союзников.
- `Skills` владеет исполнением и сроком жизни carrier. Урон, раннее попадание или смерть владельца могут сократить жизнь carrier, но не используются как гарантированное условие освобождения независимого снаряда.
- `ProjectionServiceCore.TryMaterialize/TryProcessRequest` запрашивает представление; `DefaultPoolService.TryRent` ограничивает число арендованных/созданных объектов конкретным пулом. Неудачная аренда возвращает ошибку представления, а не вводит лимит числа игровых Entity.
- `DropProfile/Production/Activity` создают опыт. `ExperienceCollectorBrain/ExperienceCollectionSkill` собирают по одной Entity через существующий переход к UI. Пулы не владеют правилами сохранения или уничтожения опыта.
- Окончательная остановка забега и автоматическое движение героя относятся к отложенным шагам. Их нельзя использовать как уже подключённую гарантию длительности Distance.

## Best Practices

Расчёт вместимости должен учитывать одновременно удерживаемые объекты, включая отложенное освобождение представления, а не только число живых атакующих в текущий момент. В существующем коде это различие видно непосредственно: `DefaultPoolService.TryRent` проверяет `pool.Free`, а `ProjectionServiceCore.TryProcessRequest` возвращает ошибку при исчерпании; Production recipe limits (`ProductionRuntime.IsRecipeLimitReached`) считают накопленные yields/outputs. Ни один из этих механизмов не является лимитом живых союзников. Поэтому расширение пула и изменение игровых ограничений — разные решения.

Точные источники: [пулы](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Pooling/DefaultPoolService.cs), [Projection](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Projection/Internal/ProjectionServiceCore.cs), [Production](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Production/Internal/ProductionRuntime.cs).

## Проверенные данные

- Board: 4×4, 16 занятых клеток. Представления, ещё удерживаемые завершающимся переходом, надо учитывать отдельно от занятых клеток.
- Оба уровня: максимум 20 `existing + incoming` врагов, фиксированный предел одного назначения 20. Это не ограничивает число умерших владельцев, чьи независимые снаряды ещё летят.
- Рецепты Water/Cola deployment: `limitMode = None`, `capValue = 0`. Производители имеют очередь 1 и один pipeline. В исходном `UnitManager.CreateUnit` также нет проверки максимального числа живых союзников. Новый лимит союзников не добавлялся.
- ExperienceDrop не имеет срока жизни. Collector выбирает одну Entity; навык имеет `startDelay = 10`. Его наличие не доказывает, что скорость сбора всегда выше скорости дропа.
- Distance 150 ограничивает смещение Entity; время достижения не ограничено. Автоматический маршрут отложен пользователем. Поэтому из этих настроек нельзя получить постоянный предел накопленных союзников и несобранного опыта для любого допустимого времени забега.
- SkillSpeed использует положительный Rate. Усиления накапливаются; базовую задержку нельзя принимать за минимальную задержку после усилений. Положительный бюджет требует шаг simulation.

## Бюджеты планирования

| Asset | Параметры |
|---|---|
| Autobattle/Orchestration/Level01EnemyOrchestration.asset | maxDependencyDepth=4; maxGeneratedNodes=64; maxResolverExpansions=128; maxBranchesPerRequirement=4; maxPlanningWorkItemsPerPass=128; maxInboxEventsPerPass=128; maxProcessTransitionsPerPass=128; maxEndpointAdmissionsPerPass=32; maxPlanningContinuationDuration=300; maxDirtyBranchesPerPass=16 |
| Autobattle/Orchestration/Level02EnemyOrchestration.asset | maxDependencyDepth=4; maxGeneratedNodes=64; maxResolverExpansions=128; maxBranchesPerRequirement=4; maxPlanningWorkItemsPerPass=128; maxInboxEventsPerPass=128; maxProcessTransitionsPerPass=128; maxEndpointAdmissionsPerPass=32; maxPlanningContinuationDuration=300; maxDirtyBranchesPerPass=16 |
| Board/Orchestration/BoardOrchestration.asset | maxDependencyDepth=4; maxGeneratedNodes=64; maxResolverExpansions=128; maxBranchesPerRequirement=4; maxPlanningWorkItemsPerPass=128; maxInboxEventsPerPass=128; maxProcessTransitionsPerPass=128; maxEndpointAdmissionsPerPass=32; maxPlanningContinuationDuration=300; maxDirtyBranchesPerPass=16 |
| Autobattle/Agents/Level01PopulationAgent.asset | workBudget=256 |
| Autobattle/Agents/Level02PopulationAgent.asset | workBudget=256 |
| Board/Agents/BoardPopulationAgent.asset | workBudget=256 |

## Пулы

Пути относительно `Assets/Game/Activities`. «Предел Entity» не включает задержку возврата view в пул. Даже достаточный предел Entity не означает проверенного отсутствия отказов представления.

| Asset | maxCapacity | expandable | Предел Entity / необходимый вход для оценки |
|---|---:|---:|---|
| Autobattle/Economy/BugBrownMedium.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/BugBrownSmall.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/BugGreenMedium.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/BugGreenSmall.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/BugPurpleMedium.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/BugPurpleSmall.asset | 32 | 1 | ≤20 врагов всех видов вместе, плюс удерживаемые views умерших |
| Autobattle/Economy/ColaDeploymentHost.asset | 32 | 1 | После промежуточной переделки prefab reference удалён; NonSpatial seed host не создаёт представление |
| Autobattle/Economy/EnemySpawner.asset | 32 | 1 | После промежуточной переделки prefab reference удалён; NonSpatial seed host не создаёт представление |
| Autobattle/Economy/ColaDeploymentHostDistance.asset | 32 | 1 | Вариант NonSpatial seed host без prefab reference; пул представления не создаётся |
| Autobattle/Economy/EnemySpawnerDistance.asset | 32 | 1 | Вариант NonSpatial seed host без prefab reference; пул представления не создаётся |
| Autobattle/Economy/WaterDeploymentHostDistance.asset | 32 | 1 | Вариант NonSpatial seed host без prefab reference; пул представления не создаётся |
| Autobattle/Economy/ExperienceCollector.asset | 32 | 1 | Стартовый host/герой выбранного Activity seed; не повторяющийся выпуск |
| Autobattle/Economy/ExperienceDrop.asset | 64 | 1 | Дроп минус подтверждённый сбор; постоянный предел и срок жизни не заданы |
| Autobattle/Economy/PlayerSpawner.asset | 32 | 1 | После промежуточной переделки prefab reference удалён; NonSpatial seed host не создаёт представление |
| Autobattle/Economy/SkillExecutor.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Autobattle/Economy/WaterDeploymentHost.asset | 32 | 1 | После промежуточной переделки prefab reference удалён; NonSpatial seed host не создаёт представление |
| Autobattle/Projection/BugGreenMediumWeaponArea.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/BugGreenSmallWeaponArea.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/BugPurpleMediumWeaponProjectile.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/BugPurpleSmallWeaponProjectile.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/ContactArea.asset | 64 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/DaggerProjectile.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/LightningBoltProjectile.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Autobattle/Projection/TurretKettleProjectile.asset | 128 | 1 | Сумма выпусков за окно lifetime; см. таблицу carrier. Учитывать общих владельцев и погибших владельцев |
| Board/Economy/BoardHost.asset | 1 | 0 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/BoardPopulationProducer.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/BoostersPopulationProducer.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/BuffsPopulationProducer.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/ColaBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/DefenseBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/GoldBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/GoldPopulationProducer.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/HealBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/HealthBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/LightningBoltBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/PowerBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/SkillSpeedBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/SkillsPopulationProducer.asset | 32 | 1 | Нет prefab reference: настройка пула сама по себе не создаёт представление |
| Board/Economy/SpeedBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Board/Economy/WaterBoardBase.asset | 36 | 1 | ≤16 занятых клеток; учесть ещё удерживаемые views предыдущего заполнения |
| Shared/Units/Cola/ColaUnit.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Cola/ColaUnit2.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Cola/ColaUnit3.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Cola/ColaUnit4.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Perfume/Perfume.asset | 32 | 1 | Стартовый host/герой выбранного Activity seed; не повторяющийся выпуск |
| Shared/Units/Perfume/PerfumeDistance.asset | 32 | 1 | Стартовый host/герой выбранного Activity seed; не повторяющийся выпуск |
| Shared/Units/Tabasco/Tabasco.asset | 32 | 1 | Стартовый host/герой выбранного Activity seed; не повторяющийся выпуск |
| Shared/Units/Tabasco/TabascoDistance.asset | 32 | 1 | Стартовый host/герой выбранного Activity seed; не повторяющийся выпуск |
| Shared/Units/Water/WaterUnit.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Water/WaterUnit2.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Water/WaterUnit3.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |
| Shared/Units/Water/WaterUnit4.asset | 32 | 1 | Постоянный предел живых союзников в данных не задан |

## Carrier и общие пулы

Пути навыков относительно `Autobattle/Skills`. Все перечисленные эффекты создают один carrier за action pulse. Lightning выбирает одну случайную Entity из списка, а не создаёт по carrier на каждую выбранную Entity. Время в шагах simulation; таблица показывает authored бюджеты до Rate.

| Skill | Start | End | Interval | Actions | Reload | Lifetime |
|---|---:|---:|---:|---:|---:|---|
| BugGreenMediumWeapon.asset | 0 | 3 | 3 | 1 | 90 | Execution (до завершения/отмены Skills) |
| BugGreenSmallWeapon.asset | 0 | 3 | 3 | 1 | 90 | Execution (до завершения/отмены Skills) |
| BugPurpleMediumWeapon.asset | 12 | 0 | 12 | 2 | 90 | 120 |
| BugPurpleSmallWeapon.asset | 12 | 0 | 3 | 1 | 90 | 120 |
| ColaUnit2Attack.asset | 0 | 0 | 5 | 4 | 23 | 30 |
| ColaUnit3Attack.asset | 0 | 0 | 5 | 4 | 23 | 30 |
| ColaUnit4Attack.asset | 0 | 0 | 5 | 4 | 23 | 30 |
| ColaUnitAttack.asset | 0 | 0 | 5 | 2 | 23 | 30 |
| LightningBolt1.asset | 0 | 0 | 6 | 2 | 3 | 60 |
| LightningBolt2.asset | 0 | 0 | 6 | 2 | 3 | 60 |
| LightningBolt3.asset | 0 | 0 | 6 | 3 | 3 | 60 |
| LightningBolt4.asset | 0 | 0 | 6 | 3 | 3 | 60 |
| LightningBolt5.asset | 0 | 0 | 6 | 3 | 3 | 60 |
| LightningBolt6.asset | 0 | 0 | 6 | 3 | 3 | 60 |
| PerfumeAttack.asset | 0 | 0 | 3 | 1 | 30 | 30 |
| TabascoAttack.asset | 0 | 3 | 3 | 1 | 30 | Execution (до завершения/отмены Skills) |
| WaterUnit2Attack.asset | 0 | 3 | 3 | 1 | 30 | Execution (до завершения/отмены Skills) |
| WaterUnit3Attack.asset | 0 | 3 | 3 | 1 | 30 | Execution (до завершения/отмены Skills) |
| WaterUnit4Attack.asset | 0 | 3 | 3 | 1 | 30 | Execution (до завершения/отмены Skills) |
| WaterUnitAttack.asset | 0 | 3 | 3 | 1 | 30 | Execution (до завершения/отмены Skills) |

Для независимого carrier верхняя оценка — число всех spawn-подтверждений соответствующего пула за окно его lifetime. При известном пределе числа источников `N`, burst `B` и минимальном промежутке между началами burst `I` консервативная оценка: `N × B × (ceil(lifetime / I) + 1)`. Она применима только если N учитывает замену погибших источников за это окно. Для Execution-carrier считать активные исполнения всех источников данного пула.

- `ContactArea`: общий пул пяти атак — Water 1–4 и Tabasco. По одному carrier на активное исполнение; предел количества Water отсутствует. Постоянная числовая оценка не получена.
- `DaggerProjectile`: общий пул Cola 1–4. Lifetime 30, burst 2/4; число Cola не ограничено. Постоянная числовая оценка не получена.
- `ExperienceDrop`: число неподобранных контейнеров может накапливаться; постоянная числовая оценка не получена.
- `BugGreenSmall/MediumWeaponArea`: один carrier на активное исполнение, удаляется вместе с ним. Верхняя оценка активных carrier каждого вида ≤20 при соблюдении лимита врагов; отдельно остаётся задержка освобождения view.
- `BugPurpleSmall/MediumWeaponProjectile`: lifetime 120, burst 1/2. Для оценки 128 требуется учесть выпуск за 120 шагов, включая сменившихся врагов. Текущие 20 живых врагов не дают такой оценки сами по себе.
- `TurretKettleProjectile`: один Perfume, burst 1, lifetime 30. При не более одного pulse в шаг консервативная оценка 31 carrier; отдельно учесть удержание views. Это расчётное условие, не результат исполнения.
- `LightningBoltProjectile`: общий пул шести вариантов. Burst 2/3, lifetime 60. Один Perfume назначает состояния через общий brain. Для оценки учитывать смену состояний, атомарное исполнение и выдачу накопленных Stack; runtime-замер отсутствует.

## Решение пользователя

Требование плана о гарантированной вместимости нельзя закрыть только существующими размерами 32/64/128: для союзников и несобранного опыта не задан постоянный предел. Это не воспроизведённая ошибка и не доказательство фактического переполнения. Причина runtime-сбоя не установлена; сценарии не запускались.

**Согласовано пользователем 2026-09-23:** вынести решение по лимитам и настройке вместимости в отдельный шаг после реализации механик, до тестов. Сейчас сохранить пулы и игровые правила, продолжить остальные пункты. Гарантию вместимости пока не считать выполненной.

Не добавлять новый лимит живых юнитов, уничтожение/объединение опыта, задержку выпуска или увеличение пулов. Этот отдельный шаг не блокирует реализацию остальных механик. Его завершение не заменяет отдельного разрешения пользователя на тесты.
