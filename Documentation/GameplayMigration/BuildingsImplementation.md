# Здания ChainRush и требования производства

Актуальный предлагаемый шаг после обсуждения количества и автоматического выпуска: [План выпуска партий через рецепты](#план-выпуска-партий-через-рецепты). Этот раздел заменяет прежнее предложение режима Wait для шести агентов солдат. Описание выполненных этапов и диагностические свидетельства ниже сохранены отдельно от нового плана.

**Implementation decision: working model.** Здания подключены через существующие Economy, Production, Population, Objectives и Orchestration. Выбранное здание сразу занимает свободное место как экземпляр уровня 0; первые карточки включают производство в том же экземпляре. Изменения C# исходного этапа ограничены UI; последующие согласованные исправления общего подбора Production и адреса требования описаны ниже со свидетельствами ручных запусков.

## Existing System Fit

Economy хранит каталог зданий, выбор и развитие. Production оценивает входы, принимает заказы, списывает ресурсы и выпускает результат. Population выбирает места и исполняет конечные партии. Objectives и Orchestration запускают строительство, улучшения и очередные партии. CapabilityHost переносит реализацию в прототип Activity и создаёт экземпляр. Activity монтирует временный кошелёк карточек; Board и Autobattle используют одного runtime-игрока.

Production UI-адаптер предоставляет одну оценку кнопке, спискам и шкалам. Sources управляют наблюдением и сменой действия; views не читают сервисы. Регистрация целей Activity, её features, игровые исполнители и OrchestrationSandbox сохранены.

Представления загружают существующие Projection и Addressables. Девять новых префабов зданий/солдат зарегистрированы в группе `ChainRush-Activity-Autobattle`, три карточки — в `ChainRush-Activity-Board`. Регистрация определений Economy сама по себе не регистрирует их префабы в Addressables.

## Best Practices

Сохранена модель [Presentation Model](https://martinfowler.com/eaaDev/PresentationModel.html): Source предоставляет состояние отображения, а Production остаётся владельцем цены и доступности. `PurchaseProgressUI` принимает готовую строку; он не вычисляет цену и не подписывается на экономику. Это позволяет нескольким строкам использовать одну оценку и общий набор наблюдений.

## Каталог и постоянное развитие

Все новые определения находятся в `Assets/Game/Activities/Buildings`. В `Economy/BuildingCatalog.asset` хранятся самостоятельные реализации трёх зданий. Кошелёк не участвует в проекции игрока.

| Определение | Обычный солдат | Улучшенный солдат | Начальное владение |
|---|---|---|---|
| `InfantryBuilding` | `InfantrySoldier`, графика Water | `InfantryVeteran`, графика Water2 | Выдано |
| `RangedBuilding` | `RangedSoldier`, графика Cola | `RangedVeteran`, графика Cola2 | Выдано |
| `HeavyBuilding` | `HeavySoldier`, графика Water3 | `HeavyVeteran`, графика Water4 | Открывается за 1000 raw Coins |

FrameworkProfile содержит первые два здания с `Available` и `Entitled`. Выбор задаётся `Selected` в группе Building: минимум 0, максимум 3. Открытие тяжёлого здания выдаёт настоящую запись в BuildingCatalog и `HeavyUnlocked`; интервал ресурса открытия `[0, 1)` запрещает повторную покупку.

`Production/{Type}PermanentUpgrade.asset` читает `PermanentLevel` реализации, расходует Coins и выдаёт ей `PermanentLevel +1` и `CooldownReduction +3`. Уровни 0–5 используют первые пять цен WaterUpgrade: 1000, 1100, 1400, 1900, 2700 raw Coins. При уровне 5 оценка сообщает MAX.

У каждого из шести солдат собственные определения и исходные Attributes. Их базовые кошельки не ссылаются на развитие реализации элитных Water/Cola. Сохраняются существующие навыки и мозги соответствующих форм. По уточнению пользователя от 2026-10-11 изображение уменьшено вдвое на визуальном дочернем Transform; root scale равен 1 и сохраняет контракт мирового pose. Footprint равен 500, тела соответствуют половине исходной формы, spacing собственного Movement равен 125. Пулы расширяемые без лимита вместимости.

## Строительство и улучшения в забеге

В SurviveWall находятся три `BuildingSite`: локальные позиции `(7, 0, -7)`, `(7, 0, 0)`, `(7, 0, 7)`. Каждый публикует одну клетку с типом BuildingSites, случайным выбором и исчерпанием доступных маркеров. Развалины находятся в представлении экземпляра уровня 0; прежние независимые декорации мест удалены. Здание имеет footprint 1000 и существующий `StaticSolid`, участвует в проверке занятости, но не имеет боевых тегов, области приёма урона или мозга смерти.

Для каждого типа в Autobattle подключены три статических Objective:

| Objective | Активация | Исполнение и результат |
|---|---|---|
| `{Type}Placement` | Реализация выбрана | Population бесплатно размещает один экземпляр уровня 0 |
| `{Type}RunUpgrade` | Здание материализовано | Конечный Production release 1, затем штатный Reset |
| `{Type}SoldierRelease` | Здание материализовано | Population выпускает конечную партию, затем штатный Reset |

PlacementAgent использует `Entries`: точную выбранную запись BuildingCatalog с `Selected` и `Available`. Обычный бесплатный заказ PlayerProduction создаёт заготовку. `ProductionOrderOutputBinding` сохраняет источник постоянного развития при создании прототипа и здания. Seed уровня и ProductionData отсутствует, поэтому начальный уровень равен 0 и производство ещё не подключено. После размещения место не выбирается повторно.

RunUpgradeAgent выбирает обычный рецепт по точному ресурсу `{Type}RunLevel`. Платит участник; получатель — прототип выбранного здания текущей Autobattle, разрешённый существующим owner binding в `ProductionReleaseFactBindingData`. OwnerRef запроса и Requirement указывают на этого же получателя; ParticipantEntityId сохраняет участника-плательщика. Для перехода 0 → 1 рецепт расходует три карточки, выдаёт уровень +1 и ProductionData ×1. Следующие операции выдают уровень +1 и CooldownReduction +15, ProductionData повторно не выдаётся. Отдельные ресурсы уровня трёх типов исключают совпадение чужого рецепта улучшения.

| Уровень после операции | Карточки на операцию | Размер партии | Базовый интервал |
|---|---:|---:|---:|
| 1 | 3 | 2 | 6 с |
| 2 | 5 | 3 | 5,5 с |
| 3 | 8 | 4 | 5 с |
| 4 | 12 | 5 | 4,5 с |
| 5 | 18 | 6 | 4 с |

Интервал каталога равен `180 − CooldownReduction` при шаге Autobattle 1/30 секунды. Постоянный уровень дополнительно уменьшает его на 0,1 секунды. Рецепт строительства/улучшения ограничен интервалом `[0, 5)`; при уровне 5 нового заказа нет.

Производитель солдат — экземпляр здания, получатель — участник. SoldierAgent и VeteranAgent взаимоисключаются условиями постоянного уровня реализации `<3` и `>=3`; исполнителя и цель допускает существующий EconomyAmountCriterionData с собственным RunLevel ≥1. Объём партии равен `2 + (RunLevel − 1)`. CapabilityHostProductionController подключает выданную ProductionData обычным Reconcile; каталог готов при подключении, следующий цикл использует существующий cooldown. Точки выпуска — девять клеток рядом со зданием с его собственным типом provider. Начатая партия сохраняет рассчитанный объём. Живое количество солдат не поддерживается и не ограничивается этой конфигурацией.

## Карточки и две Activity

Autobattle монтирует `BuildingCardsWallet` и выдаёт в обычный ActivityWallet `BuildingRunEnabled ×1`. В Board добавлены три рецепта выпуска карточек и три рецепта их обработки после merge. Выпускаются карточки выбранных зданий; обработка каждого выбранного Token выдаёт одну соответствующую карточку ресурса в BuildingCardsWallet участника.

BoardPopulationAgent выбирает release по BuildingRunEnabled. При 0 сохраняется прежний состав. При 1 прежние категории получают 80% доли, карточки зданий — 20%. Существующая проверка доступного содержимого исключает пустую группу и перераспределяет её долю, если здания не выбраны. Добавлены три Objective обработки выбранных карточек; условие очистки доски учитывает все новые типы.

Board начисляет карточки, Autobattle расходует их из того же кошелька игрока. Закрытие Autobattle освобождает временный кошелёк. Distance не монтирует его, не выдаёт флаг и не содержит целей зданий. Изменения прототипа и карточки обратно в постоянную реализацию не переносятся.

## UI и изменённые компоненты

| Компонент package | Подключение |
|---|---|
| `ProductionUIActionData` | Явный `RecipientType`: Owner или ViewedEntry |
| `ProductionUIAdapter` | Все Consume в Prices, все Require в Requirements; аргумент прогрессии и исчерпание верхней границы |
| `ProductionUIResponse`, `PurchaseUIData` | Обе коллекции `EconomyItemUIData`, без новых ресурсных DTO |
| `ProductionUISource` | FixedAction и SetupAction, общий snapshot, поле прогрессии, MAX и события нижней границы интервала |
| `PurchaseButton` | Необязательный общий список Requirements рядом с Prices |
| `EconomyListViewItem` | Необязательный PurchaseProgressUI и очистка вместе со строкой |
| `EconomyListView` | Стабильные строки без выбора обновляются без пересоздания views |
| `PurchaseProgressUI` | Одна строка Balance/Amount с визуальным ограничением 0–1 |
| `ValueProgressBarController` | Обычная числовая Setup с прежней анимацией |
| `CapabilityHostUISource` | OnBound после успешной однозначной привязки |
| `CapabilityHostProjectionUIBinding` | Точный ProjectionInstanceHandle → контекст владельца и handle реализации; cleanup при unbind/pool |
| `CapabilityHostProjectionUIReadRequest`, `EconomyUIAdapter.Projection` | Один медиированный запрос контекста представления; отказ при отсутствующей или неоднозначной записи |

Owner разрешён без handle только для явно заданного действия. ViewedEntry проверяет настоящий handle и принадлежность записи. Цены и требования сохраняют отдельные строки даже при совпадении asset; баланс читается по точному кошельку, форме и runtime tags. Одинаковые чтения в одном обновлении переиспользуются. Watches общие для входов; смена действия освобождает прежние наблюдения и игнорирует поздние ответы.

`PurchaseProgressUI` не выбирает рецепт или владельца. При пропавшем балансе либо неположительном Amount он очищается; изменение баланса той же строки использует прежнюю анимацию. Доступность кнопки берётся из Production, поэтому заполненные строки не обходят cooldown, очередь и прочие ограничения.

В FrameworkMain добавлена вкладка Buildings на существующих каталоге, выборе и PurchaseButton. Тяжёлое здание представлено отдельной настроенной карточкой открытия Owner; после выдачи каталог показывает настоящую запись. Все четыре PurchaseButton используют `PurchaseConsumeItem.prefab` и отдельный список `PurchaseRequireItem.prefab`.

SurviveFlow открывает `FrameworkSurviveGameplay`. ExperienceUI находится внутри корня FrameworkGameplay с GameScene и получает обычный UI-контекст. Верхние BuildingPanels удалены. Каждый префаб здания содержит WorldSpace Canvas ниже его изображения, одну ProductionUISource с RunUpgradeAction и общий BuildingRequirementItem: иконка, баланс/потребность и шкала. CapabilityHostProjectionUIBinding получает точный handle собственного представления, а UI-адаптер возвращает владельца и единственную запись его реализации. Поиск первого подходящего здания и скрытый каталог больше не используются.

События Source OnProgressionAtMinimum/OnProgressionAboveMinimum переключают Ruins/Building через authored UnityEvents. MAX использует прежний путь. Шкала получает готовый EconomyItemUIData и не отправляет заказы; строительство и улучшения продолжаются при закрытом UI. Наблюдение освобождается при unbind, отключении и возврате в pool; поздние ответы отсеиваются версией привязки. Три прежних ConstructionAction удалены, Placement-рецепты/агенты/Objectives переименованы через Unity с сохранением GUID и прежних runtime Id.

Оригинальные Main/Gameplay, UI Distance, движущиеся префабы уровней и элитные определения не изменены. Новые подписи добавлены в существующие Meta/UI таблицы en-US. Новые `.meta` создала Unity; GUID существующих файлов сохранены.

## Приёмка

Проверено без запуска игры:

- Unity скомпилировала runtime проекта без ошибок.
- Затронутые EditMode и PlayMode fixtures отдельно скомпилированы без ошибок; тестовые методы не выполнялись.
- Чтение настоящих полей 135 новых ScriptableObject assets не выявило отсутствующих ссылок или managed-reference типов. Проверены регистрация definitions, уникальность экономических Id, UnityEvents, доли Board, совпадение provider и размеры солдат.
- Исходные верхние панели заменены шкалами в префабах. Предварительные изображения уровня 0 и 1 просмотрены в редакторе; смещение заливки исправлено. Реальный игровой поток и анимация после этих изменений ещё не проверены.

Эти проверки не подтверждают исполнение игровых сценариев; оно входит в приёмку после отдельного разрешения.

Fixtures подготовлены для нескольких Consume/Require одного asset в разных кошельках и формах, Owner без записи, MAX, старого ответа после смены действия, однократного OnBound, сохранения строк списка и очистки/уменьшения шкалы. **Тесты и Play Mode не запускались.** Их выполнение требует отдельного разрешения после завершения реализации.

Приёмка включает открытие и сохранение зданий, выбор максимум трёх, случайные свободные места, первую постройку и улучшения без повторной материализации, обмен карточками между Board/Autobattle, партии и cooldown, ветеранов с постоянного уровня 3, отсутствие влияния на элитных юнитов, смешанные входы рецепта, MAX и cleanup UI, отсутствие зданий в Distance и сохранение поведения Sandbox.

## Разбор отказа запуска — 2026-10-10

**Implementation decision: working model.** Исправлены настройки подключения нового контента; runtime-код не изменён.

Сообщённый симптом: «юниты не выпускаются и вообще уровень нормально не стартует, герой не выставляется, доска не заполняется, враги не идут».

**Verified — до исправления.** В выделенном `ChainRush.log` Addressables выдаёт `InvalidKeyException`: отсутствует location для GUID `06cbb722b70e94ddea786b11fd391383` (`InfantryVeteran.prefab`). Чтение настроек редактора установило отсутствие записей у всех 12 новых префабов; выбран `BuildScriptFastMode`. Старые Perfume, BugBrownSmall и ColaBoardBase сохраняют правильные GUID и зарегистрированные группы — подмена их ссылок исключена.

В логе достигнута ветка `OnProjectionPrefabLoadCompleted → FailProjectionScope`. Живые scopes Activity 2 (Autobattle) и 3 (Board) имеют `HasFailed=true`, `IsReady=false` и эту же ошибку. Затем лог фиксирует отказ материализации Perfume, BugBrownSmall и ColaBoardBase. Путь исполнения: Production готовит Token → ActivityProductionOutputMaterializationModule вызывает Activity materialization → Projection отвергает запрос из-за Failed scope → модуль отклоняет output и публикует неуспешное завершение. Это подтверждает общее препятствие созданию героя, врагов и доски.

Также зафиксирован отказ процесса `chainrush-autobattle-turn-token:economy:0:executor-available:207`. Живой `PlayerSpawner` 207 занят резервированием `PerWork` агента `infantryconstructionagent:2`; строительное назначение Preparing, очередь и pipelines PlayerProduction пусты. Балансы InfantryCards, RangedCards и HeavyCards равны нулю. `AgentSelectionResolver.IsAvailable(207)=false`, `AgentService.IsExecutorAllocated(207)=true`. Строительство начинает удерживать общего исполнителя до накопления карточек.

**Внесённые действия.** После явного разрешения пользователя остановлен только Play Mode ChainRush. Через Unity зарегистрированы все 12 префабов в двух существующих группах; в три ConstructionAgent добавлено условие наличия трёх карточек до назначения исполнителя. GUID, рецепты, политики reservation и код сервисов сохранены. Условия используют существующие Economy watches и applicability wait, без новых опросов или подписок виджетов.

**Verified — после записи.** Прочитаны сохранённые поля: все 12 записей имеют правильные группы и адреса; все три условия валидны, выбирают точный ресурс/кошелёк рецепта и порог 3. Managed-reference типов не потеряно. Play Mode выключен.

**Inference / предел проверки.** Новый игровой запуск не выполнялся. Поэтому восстановление материализации и полного цикла токенов после исправлений ещё требует ручного запуска; успех authoring-проверки не означает успешный игровой прогон. Дополнительных изменений Orchestration по тексту отказа не внесено.

## Строительство не начинается после merge — 2026-10-10

Сообщённый симптом: «герой и враги появляются, доска заполняется. Но после выбора карточек элитиники не появляются. Также здания не производят юнитов».

**Verified.** В оставленном пользователем Play Mode ChainRush на паузе scopes Autobattle 2 и Board 3 готовы, без ошибки. Герой Perfume существует; оба производителя Water/Cola доступны, их очереди и pipelines пусты. История Production содержит первоначальное заполнение и четыре завершённых InfantryCardMerge; ColaMerge/WaterMerge и deployment-заказов в этом запуске нет. В кошельке участника InfantryCards = 4 при цене строительства 3. Все восемь целей deployment элитных форм остаются Inactive с ActivationGeneration = 1; цели выбора Water/Cola также не активировались. Это не подтверждает самостоятельный отказ их исполнения.

InfantryConstruction назначено Population, состояние Planning / назначение Preparing. Executor 207 PlayerSpawner удерживается PerWork. Набор производителей Population содержит одного исполнителя, но методов 0, доступного содержимого 0; выбор региона и постановка заказа ещё не начались. В настоящем ProductionData PlayerProduction поле MaterializationMarkerProvider пусто. Лог содержит два предупреждения об отклонении InfantryConstruction и RangedConstruction из-за отсутствия provider. Read-only вызов существующего TryReadCurrentMethod возвращает false для всех трёх строительных рецептов; это соответствует его обязательной проверке provider до чтения catalog entry. Ресурсов пехотного строительства достаточно; Projection scope не Failed — эти ближайшие конкурирующие причины исключены.

BoardUI после четырёх merge остаётся selectionLocked / awaitingBoardRefresh. На доске осталось 12 карточек; следующего заполнения нет. PlayerSpawner удерживается строительством; процесс turn-token ожидает dependencies. В этом снимке также нет Experience, поэтому утверждение, что одно лишь освобождение исполнителя немедленно заполнит доску, не делается.

**Verified — визуальная неоднозначность.** InfantryBoardCard использует тот же sprite GUID ca61d7b760e884bf897678a2f3b4c871, что и WaterBoardBase. Это временное оформление добавленного контента; четыре обработанные карточки выдавали строительные карточки, а не элитных юнитов. Различие экономических записей и рецептов сохранено.

**Existing System Fit.** ProductionData описывает существующий маршрут materialization. ProductionStateOrchestrationModule и PopulationProductionCandidates допускают materialized-рецепт только с этим маршрутом. Population владеет подбором мест, reservation, точным OutputBinding и созданием заказа. ActivityProductionOutputMaterializationModule использует уже зарегистрированное Population placement либо существующий provider; CapabilityHost/Projection создают экземпляр. Производство солдат и улучшение здания пока не активировались, поскольку здания нет. Economy, Production admission, Orchestration и Board UI не требуют новой ветки выполнения для этой настройки.

**Working model — конкретное исправление.** В существующем Assets/Game/Activities/Autobattle/Production/PlayerProduction.asset задать MaterializationMarkerProviderType = External и MaterializationMarkerProvider = существующий Buildings/Taxonomy/BuildingSites.asset. Это соединяет уже добавленные три строительных рецепта с уже настроенными местами SurviveWall. Population продолжает передавать точный выбранный marker/reservation; обработка ресурсных outputs остальных рецептов не меняется. Runtime-код, цена, cooldown, политики reservation и новые assets не нужны. Перед записью дождаться разрешения на остановку текущего Play Mode, затем сохранить только этот asset через Unity и прочитать оба поля. Игру и тесты не запускать.

**Best Practices.** Переиспользуется один существующий маршрут вместо отдельного исполнителя: контракт ProductionData.MaterializationMarkerProvider, проверка ProductionStateOrchestrationModule.TryReadCurrentMethod и ActivityProductionOutputMaterializationModule.OnEvent(ProductionTokenOutputPreparedEvent). Настройка соединяет владельцев; вычисление и выполнение остаются у прежних подсистем.

**Inference / пределы.** Отказ выпуска солдат после успешной постройки пока не воспроизведён: экземпляров зданий нет и SoldierRelease не активен. Отдельная ошибка элитного выпуска также не установлена этим запуском. Не менять их runtime или параметры по отсутствию заказа. Визуальное различение карточек записано как отдельное обнаруженное неудобство; в этом исправлении оформление не меняется.

Свидетельства: /tmp/chainrush-buildings-followup-20261010/manual-run.log и пять JSON readbacks. Редактор явно выбран по /Users/ionrain/ChainRush, Play Mode не изменён, тесты и игровые прогоны агент не запускал.

### Подключение BuildingSites — согласовано и выполнено

Пользователь явно разрешил остановить текущий Play Mode и исправить настройку. Остановлен только ChainRush. Через Unity сохранены два поля PlayerProduction: External и существующий BuildingSites. Повторное чтение подтверждает оба значения; asset не dirty, Play Mode выключен, isCompiling/isUpdating/scriptCompilationFailed = false. Diff относительно сохранённой копии до правки содержит только эти два поля; .meta и GUID 550ba59fa12aa47858d39349a31ded26 не изменены. Runtime-код, рецепты, другие assets и оформление карточек не менялись. Свидетельства записи: provider-fix-tool.json, provider-readback-tool.json, provider-fix.patch в /tmp/chainrush-buildings-followup-20261010.

Работа по согласованному исправлению завершена. Нового игрового запуска и тестов агент не выполнял; успешная постройка, первая партия солдат и обработка настоящих Water/Cola остаются наблюдением следующего ручного запуска.


## Новый ручной запуск — 2026-10-11: карточки и отказ строительства

**Сообщённый симптом (буквально):** «построение/улучшение карточек зданий на доске не появляются, соответственно, построить здания нельзя, элитники не выпускаются, когда их выбираешь на доске». Пользователь оставил ChainRush в Play Mode на паузе. Чтение выполнено только для /Users/ionrain/ChainRush и его выделенного ChainRush.log; Play Mode не изменялся.

### Verified

- Hero Perfume и враги материализованы; Board и Autobattle находятся в Running. Экземпляров зданий и солдат нет.
- В первом заполнении доски успешно выпущены четыре InfantryBoardCard, во втором — три. Все семь обработаны успешными InfantryCardMerge. В кошельке участника InfantryCards = 7. Выданы именно карточки здания; их префаб использует тот же Water sprite, что и карточка элитного юнита. Это ошибка различимости созданного оформления.
- В истории нового запуска нет WaterMerge/ColaMerge или заказа выпуска элитного юнита. Их Objectives остаются Inactive. Поэтому отдельный отказ элитного выпуска этим запуском не установлен. Не менять рецепты и правила элитного выпуска без свидетельства соответствующего вызова.
- Два InfantryConstruction (2:4 и 2:5) приняты и закончились OperationalFailure: «Multiple spatial marker providers … BuildingSite1 … BuildingSite2 … BuildingSite3». CallerCorrelationKey обоих заказов — orchestration-process, SourceCount = 0. Они пошли через общий materialization endpoint, а не через Population с точной reservation и источником развития. Обычное разрешение provider по типу требует единственности; три независимых области пригодны для Population, но не для этого неявного выбора.
- InfantryConstructionAgent / assignment 4 завершился Failed с «Production progression owner is unavailable at the selected level». Чтение существующего TryReadCurrentMethod по всему текущему каталогу и тому же владельцу возвращает успешные три Construction и этот отказ для каждого RunUpgrade. RunUpgrade читает Prototype получателя; получатель при подборе строительного метода — runtime-игрок, не прототип здания.
- PopulationProductionCandidates.Compile передаёт в TryReadCurrentMethod все записи выбранного каталога и завершает подбор при ненулевом failure. TryReadCurrentMethod сначала рассчитывает progression, и лишь затем отсеивает outputs, не материализующие CapabilityHost Token. PopulationAgent передаёт Invalid этого подбора в Failed assignment. Рецепт ресурсного улучшения, который не является кандидатом строительства, способен оборвать подбор. Проверен и второй путь ошибки той же прогрессии при production probe: Compile обрабатывает каталог до probe и уже получает отказ на InfantryRunUpgrade.
- Ближайшие альтернативы: для пехотного строительства есть 7 карточек при цене 3; сам Construction метод доступен. В новом запуске нет прежнего отказа Addressables или отсутствующего MaterializationMarkerProvider. Отсутствие солдат пока следует за отсутствием здания: SoldierRelease не активировался.

**Inference / ограничения:** визуальная одинаковость объясняет, почему обработку строительных карточек можно принять за выбор элитников; намерение пользователя по картинке не восстанавливается из runtime. Выпуск настоящей Water/Cola карточки нужно наблюдать после различения оформления. Успешная постройка и выпуск партий после исправления ещё не проверены.

### Existing System Fit

- Economy хранит общий ресурс карточек; Board Selection/Orchestration запускает существующий CardMerge. BoardItemView связывает представление точного Entity с клеткой и не меняет рецепты.
- ProductionStateOrchestrationModule владеет чтением production methods; его TryReadCurrentMethod используется только PopulationProductionCandidates. Структурный отбор подходящих outputs должен предшествовать вычислению их progression. Admission, стоимость, выпуск, refund и терминальные события остаются в Production.
- PlayerBrain предоставляет альтернативы глобальному требованию. В нём уже есть HeroPopulationAgent и три ConstructionAgent. Элитные DeploymentObjectives требуют EconomyAmount и используют существующий input-consumption / production путь с output bindings; общий MaterializedEntityProduction вариант не нужен для их исполнения. Enemy brains имеют собственные конфигурации и не меняются.
- Population владеет выбором области, exact marker, reservation, Entries и ProductionOrderOutputBinding. ActivityProductionOutputMaterializationModule использует зарегистрированное Population placement. Три BuildingSite должны сохраняться как независимо размещаемые области; новый провайдер, сборщик маркеров и изменение Spatial не нужны, если исполняется согласованный Population путь.
- CapabilityHost/Projection продолжают создание здания и наследование выбранной реализации. UI использует существующие prefab-компоненты и локализованный Title asset. Новых сервисов и контроллеров нет.

### Working model — согласованный пакет исправлений

1. **Один runtime-файл package:** ProductionStateOrchestrationModule.cs / TryReadCurrentMethod. До TryResolveNextRecipeOrdinal / TryEvaluateYield проверить authored outputs теми же структурными условиями, которыми метод уже отбирает materializable CapabilityHost Token после расчёта. Ресурсный RunUpgrade не является таким методом и не вычисляется в этом чтении. Проверка рассчитанного количества, error подходящего materialization-рецепта и admission сохраняются. Не перехватывать произвольные ошибки и не добавлять fallback. Обновить существующий Population fixture смешанным каталогом с ресурсным Prototype-рецептом; тест не запускать.
2. **Один asset ChainRush:** PlayerBrain.asset. Удалить общий MaterializedEntityProductionDecompOpData и его production-materialized decision из этого brain. Материализация героя и зданий выполняется уже настроенными Population agents; Economy/production путь элитников, turn tokens, upgrades и остальные операторы остаются. Это исключает конкурирующий строительный заказ без точного места и источника. Регионы SurviveWall и PlayerProduction provider сохраняются.
3. **Три префаба карточек:** InfantryBoardCard / RangedBoardCard / HeavyBoardCard. Добавить отличающую рамку и подпись существующего локализованного Title соответствующего здания через готовые uGUI/TMP/LocalizeStringEvent компоненты. Не менять Entity, теги, selection, merge и экономические outputs. Новые строки идентификаторов и новый UI-код не нужны.
4. После разрешения остановить только текущий Play Mode ChainRush, выполнить весь пакет, обновить документацию и прочитать сохранённые поля. Проверить компиляцию при остановленной игре. Игру и тесты не запускать.

**Best Practices.** Предварительный структурный отбор исключает вычисление рецепта вне его контекста, сохраняя доменные проверки при реальном исполнении. Применяется в уже существующих контрактах ProductionOutputData.Asset/FormType и TryReadCurrentMethod. Один явно настроенный Population путь сохраняет владельца reservation и источника (PopulationProductionSession.TryStartMaterialization → ActivityMaterializationEndpointRunner → ActivityProductionOutputMaterializationModule). Визуальное различие задаётся префабом и существующим LocalizedString, без переноса игровой логики в UI.

Свидетельства сохранены в /tmp/chainrush-buildings-diagnostics-20261011: manual-run.log, state/orders/deeper/routes/methods/authoring JSON. До ответа пользователя изменения runtime и assets не выполнялись. Тесты и игровые прогоны агент не запускал.


### Пакет согласован и полностью выполнен — 2026-10-11

Пользователь явно согласовал все три исправления и остановку текущего Play Mode. Остановлен только ChainRush.

- В TryReadCurrentMethod структурный отбор outputs перенесён перед разрешением ordinal и вычислением progression. Сохраняются ограничения одного CapabilityHost Token, запрет других Token outputs и проверка рассчитанного количества = 1. Рецепты, действительно подходящие этому маршруту, сохраняют ошибки расчёта. Новых подписок, сервисов и промежуточных коллекций нет.
- Существующий PopulationCatalog_RandomChoiceIgnoresDuplicateProducersAndSurvivesBudgetedWait дополнен вторым вариантом со смешанным каталогом: перед двумя materialization recipes стоит ресурсный рецепт, читающий Prototype получателя. Сохраняется исходный вариант без этого рецепта. Fixture проверяет прежние выбор, ограниченный бюджет, отсутствие входа, повторное пробуждение и materialization; его выполнение не запускалось.
- В PlayerBrain сохранены 36 операторов и 38 решений вместо 37/39: удалены только MaterializedEntityProductionDecompOpData и production-materialized decision. Повторное чтение подтверждает отсутствие обоих и сохранность HeroPopulationAgent, трёх ConstructionAgent и остальных прежних агентов. PlayerProduction, три BuildingSite, рецепты, балансы и elite deployment operators не изменялись.
- У трёх префабов карточек добавлены четыре полосы рамки и отдельная тёмная плашка с TMP-текстом. Название получает один LocalizeStringEvent из уже существующего Title здания: Infantry Camp / Ranged Camp / Heavy Camp, table GUID 3780f36a-553b-640f-bbe5-7d99ea3ba447. Для редакторского предпросмотра сохранён текст существующей en-US таблицы; во время исполнения локализацию обновляет штатный компонент. Cairo SDF переиспользован. Декорации имеют raycastTarget=false; исходные BoardItemView, ProjectionBindingController, размер 72×72 и картинки сохранены. Предпросмотр трёх карточек отрендерен в изолированном PreviewRenderUtility и визуально проверен; Play Mode для этого не запускался.
- Все четыре .meta побайтно совпадают с сохранёнными копиями. GUID PlayerBrain и трёх префабов не изменились. Unity сама сериализовала новые дочерние компоненты; ручных YAML/meta правок не было.
- После явного импорта двух изменённых C# файлов и RequestScriptCompilation редактор ChainRush завершил компиляцию и reload: isCompiling=false, isUpdating=false, scriptCompilationFailed=false, Play Mode выключен. Новых ошибок C# в этой компиляции нет. Тестовая сборка package не загружена в этом редакторе; компиляция обновлённого fixture отдельно не подтверждена. Никакие тесты и игровые прогоны не выполнялись.

Свидетельства записи и проверки: stop-tool.json, brain-fix-tool.json, infantry-card-fix-tool.json, other-cards-fix-tool.json, card-labels-preview-tool.json, compilation-request.json, final-readback.json, compiled-fixture-readback.json, PNG предпросмотра и четыре asset patches / package-code.patch в /tmp/chainrush-buildings-diagnostics-20261011.

Согласованный пакет завершён. Исполнение новой постройки, выпуск партии здания и выбор настоящих Water/Cola требуют следующего ручного игрового запуска; прежняя запись семи InfantryCardMerge не доказывает их выполнение. Тесты ждут отдельного разрешения пользователя.

## Ручной запуск 2026-10-11: места строительства и неподключённые шкалы

**Сообщённый симптом (буквально):** «Карточки появляются, но во-первых непонятно, на каком именно месте стоит непостроенное здание (заготовка), соответственно рядом с этим местом должен быть прогресс бар карточек, а он где-то наверху и вообще непонятно к чему относится. Должно это выглядеть как прогрессбар здоровья у юнитов в MorbooFramework, только не сверху, а снизу. Плюс кажется, что карточки никак не влияют, постройка здания не начинается, хотя по идее необходимое количество собрано».

Чтение выполнено только в текущем ChainRush, с явным project-path и проверкой Application.dataPath. Play Mode остаётся включённым на паузе. Игра и тесты агентом не запускались; runtime, сцены и assets не изменялись.

### Verified

- Восемь InfantryCardMerge завершены. InfantryConstruction 2:4 завершён без ошибки, CallerCorrelationKey = 2:agent-assignment:4:population:0:0, SourceCount = 1. Construction assignment 4 и соответствующий Objective Completed. Создан экземпляр InfantryBuilding 474, прототип 473; в кошельке игрока осталось 5 InfantryCards после списания 3 за строительство. RangedCards отсутствуют; RangedConstruction участника 1 ожидает applicability invalidation. Поэтому сообщение «постройка не начинается» не описывает фактический результат пехотного заказа этого запуска.
- Представление InfantryBuilding активно, его Renderer включён, bounds = (1, 0.7, 1), позиция корня (7, 0, 8). Оно использует временный куб. Качественная различимость этого оформления не подтверждается одним isVisible; необходимая пользователю явная связь места и шкалы в текущем authoring отсутствует.
- Все три ProductionUISource верхних BuildingPanels имеют Owner = null, Recipient невалиден, Prices пустой, Failure = «Production UI action requires an explicit owner». Все три CapabilityHostUISource панелей имеют owner = null и queryReady = false. Источник героя получил игрока и привязан к Entity 277. Панели являются потомками отдельного корня ExperienceUI; GameScene расположен в корне FrameworkGameplay. UIFlowServiceCore.CollectContextReceivers обходит только поддерево корня контроллера представления. Эти панели не получают контекст и lifecycle через существующую границу UIFlow; самостоятельные чтения ExperienceUI не распространяют контекст соседним Sources.
- У процесса улучшения 2:646 (участник 1) DesiredFact.OwnerRef = player:FrameworkPrototypesProfile / Entity 1, а ProductionReleaseRequirement.RecipientKey = capability-host:473. Процесс Blocked с отсутствием исполняемых кандидатов. В реально построенном кеше ProductionState для player присутствуют Construction и soldier methods, но нет InfantryRunUpgrade; revision содержит отказ его progression owner. ProductionReleaseFactBindingData.CreateQuery разрешает точного получателя Requirement, однако сохраняет входной owner участника в самом query. ProductionAgentDependencyResolver получает methods по DesiredFact.OwnerRef; ContinueCatalogBuild рассчитывает progression относительно этого владельца и пропускает неподходящий рецепт. Прототип 473 доступен и разрешается от фактической реализации InfantryBuilding; конкурирующее объяснение «прототип не создан» для этой стадии исключено. Карточек 5 — цена первого улучшения также 5.
- В сохранённом AgentApplicabilityRuntime участника 1 у InfantrySoldierAgent и InfantryVeteranAgent Failure = «The requested economic owner level is unavailable in this domain», EconomyConditions.Count = 0. Процесс выпуска 2:647 Blocked с той же ошибкой. Prepare вызывается при создании participant runtime, до появления прототипа; он записывает отказ Owner.Resolve в Method.Failure. Evaluate возвращает этот отказ без повторной подготовки. Authoring обоих агентов выбирает Prototype для постоянного уровня здания. Во время чтения после строительства тот же binding уже разрешается в 473, но первоначальный Method.Failure остаётся. Постоянный уровень является данными реализации; чтение Implementation доступно до создания здания и соответствует выбранной игровой модели.
- До строительства выбранная запись здания не связана с конкретным BuildingSite. Population выбирает свободное место при исполнении Construction; условие наличия 3 карточек предотвращает назначение строительного агента раньше. Следовательно, нельзя достоверно показать под конкретными развалинами шкалу конкретного здания простым переносом текущих HUD-панелей. Нужен отдельно описанный и согласованный момент закрепления места до накопления карточек; UI не должен самостоятельно выбирать или резервировать место вместо Population.
- Для сравнения прочитаны только файлы MorbooFramework: HealthFeedbackAnchor использует TransformValueProgressBarView, ValueProgressBarController и CameraBillboardController. Эти же представление и анимация переиспользуемы для PurchaseProgressUI; обработчики HostValue здоровья для карточек не нужны. Editor и логи MorbooFramework не использовались.

**Inference / пределы:** текущая временная графика и пустой HUD могут скрывать успешную постройку от игрока. Два отказа дальнейшего исполнения установлены по сохранённым query, cache и Method.Failure; правки ещё не выполнялись. Выбор ранней привязки места и оформление под ним требуют конкретного дополнения к authoring/lifecycle, а не предположения о свободной позиции.

Свидетельства: launch-log.txt и chainrush-sites-{deeper,orders,state,ui,owners,planning,processes}.json в /tmp/chainrush-building-sites-20261011. Новых игровых прогонов, тестов и изменений состояния Play Mode нет.


## Исправление адресации и UI-контекста — 2026-10-11

**Implementation decision: working model.** Пользователь ответил «хорошо, исправляй» на результаты предыдущего чтения. Свидетельства сохранены до остановки; остановлен только Play Mode ChainRush, все открытые игровые сцены были без несохранённых изменений. Новый игровой запуск и тесты не выполняются.

Выполнено:

- В ProductionReleaseFactBindingData.CreateQuery один раз разрешается recipient. Этот же владелец задаётся в OwnerRef запроса и ProductionReleaseRequirement. ParticipantEntityId сохраняется от исходного требования: игрок продолжает быть участником и плательщиком. Переиспользована существующая схема ObjectiveEconomyFactProjector. Расчёт прогрессии, подбор методов и ProductionAgent теперь получают один адрес требования.
- У Infantry/Ranged/Heavy SoldierAgent и VeteranAgent через Unity изменён только ownerLevel условия постоянного уровня: Prototype → Implementation. Progress Population, размер партии и cooldown прототипа не изменены. Все шесть GUID сохранены.
- В FrameworkSurviveGameplay корень ExperienceUI помещён внутрь корня FrameworkGameplay с GameScene. UIFlow собирает существующие Sources и lifecycle receivers своим прежним обходом; чтение и подписки UIFlow не расширены. Интерфейс других режимов не меняется.
- Существующий fixture ProductionReleaseBinding_ExtractsBeforeAdmissionAndIgnoresPreviousActivation расширен двумя вариантами получателя: тот же владелец и отдельный. Помимо Requirement проверяются адрес OwnerRef и сохранение ParticipantEntityId. Fixture не запускался.

### Дополнение для шкалы на месте до строительства — согласовано и выполнено

**Implementation decision: working model.** Это изменение момента появления заготовки относительно первоначального плана, не автоматическая миграция и не исправление гипотетического отказа Population.

**Existing System Fit.** Population остаётся владельцем случайного свободного размещения и reservation; Production — владельцем списания карточек и выдачи ресурсов; CapabilityHostProductionController.Reconcile уже подключает производство при появлении ProductionData в проекции и допускает отсутствие производства до этого. CapabilityHostProjectionController распространяет изменения прототипа. UI получает явный контекст и точную запись через адаптер; состояние и стоимость остаются у Production. Activity, общий алгоритм Population, Spatial и Orchestration runtime этим дополнением не меняются.

Пользователь явно выбрал «Согласовать уровень 0 и завершить исправления». Реализованный путь:

1. Выбранное здание сразу размещается существующим Population как тот же CapabilityHost уровня 0. Бесплатный рецепт размещения заменяет нынешнюю материализующую часть Construction. Условия трёх карточек с размещающего агента снимаются; подготовка занимает случайное свободное место штатным способом. Заготовка и построенное здание имеют один Entity и один источник развития; отдельного CapabilityHost развалин нет.
2. В исходной реализации сохраняются кошельки и постоянное развитие. Seed RunLevel ×1 и ProductionData ×1 из определения убираются: уровень 0 представлен отсутствием баланса в уже зарегистрированном кошельке. Catalog и параметры cooldown сохраняются. До первой постройки ProductionOwner не подключён, поскольку проекция не содержит ProductionData.
3. Существующий RunUpgrade становится единственным рецептом строительства/улучшения прототипа, для уровней 0–4. Цена: 3/5/8/12/18 карточек; Output RunLevel +1. Output CooldownReduction: 0/15/15/15/15. Output ProductionData: 1/0/0/0/0 — выдаётся только при первом исполнении. Все три outputs используют существующие LongListProgressionData/LongFlatProgressionData и Prototype, без нового оператора и без обработки текста ошибок. Уровень 5 остаётся MAX. Плательщик — участник.
4. Существующие конечные Soldier/Veteran Population агенты допускают исполнителя только с собственным RunLevel ≥1 через уже существующий EconomyAmountCriterionData. Проверка читается при выборе Entity, а не разрешает отсутствующий прототип во время создания participant runtime. Условия постоянного уровня продолжают читать Implementation. До строительства партия не запускается; после появления ProductionData используется прежний путь партий и cooldown.
5. Шкала находится в префабе самого здания, под его изображением. Переиспользуются EconomyListViewItem, PurchaseProgressUI, TransformValueProgressBarView, ValueProgressBarController и CameraBillboardController. У этого префаба одна ProductionUISource для общей оценки. Верхние BuildingPanels удаляются после переноса их обязанностей.
6. Небольшая общая UI-привязка CapabilityHostProjectionUIBinding в Core/UI/CapabilityHosts получает точный ProjectionInstanceHandle при materialization, один раз запрашивает у UI-адаптера хозяина/исходную запись и передаёт EconomyUIContext и настоящий handle в Sources. Сообщение CapabilityHostProjectionUIReadRequest и его обработчик в существующем EconomyUIAdapter принадлежат UI; адаптер пользуется CapabilityHostService/Economy, сервисы не получают UI-зависимостей. Binding снимает наблюдение при unbind/возврате в pool и игнорирует поздний ответ по версии привязки. Поиск Source выполняется один раз и требует единственной точной реализации, без выбора первого совпадения.
7. В ProductionUISource добавлены OnProgressionAtMinimum/OnProgressionAboveMinimum для нижней границы интервала, симметрично существующему уведомлению о MAX. Authoring префаба связывает их с отображением развалин/построенного здания. Нового BuildingUIController, отдельного чтения уровня, таймера строительства и дублирующего вычислителя не добавляется.

**Best Practices.** Переиспользуется уже существующая композиция экономической возможности: CaptureProjection/TryAttach/Reconcile в CapabilityHostProductionController, вместо переключателя строительства в Activity. Source остаётся presentation model: один Production evaluation обслуживает числовые строки, шкалу и состояние представления. Unity-представление связывается с уже выбранным Population Entity, а не выбирает место самостоятельно. Это сохраняет существующие границы и исключает второй реестр зданий/мест.

**Стоимость.** Три заготовки размещаются один раз за запуск выбранных зданий. Один запрос контекста на bind, одна общая оценка/набор watches на здание. Нет обхода всех солдат, чтений из каждой шкалы и повторного выбора места после накопления карточек. Смена уровня меняет ресурсы и отображение того же экземпляра; новый строительный Token не создаётся.

Пункты 1–7 и ранее согласованные исправления адресации, applicability и UI-контекста завершены. Все три здания сохранены через Unity; новые .meta созданы её импортом. Существующие GUID сохранены. Из SurviveWall удалены только три старые декорации Ruins: пространственные BuildingSites и их границы не менялись. Старые ConstructionAction больше не имеют потребителей и удалены; девять файлов Placement перемещены через AssetDatabase.MoveAsset.

**Проверка без игрового запуска.** Повторное чтение настоящих полей Unity подтвердило отсутствие стартового RunLevel/ProductionData, бесплатное Placement, цены 3/5/8/12/18, outputs ProductionData 1/0/0/0/0, Prototype у трёх outputs, Implementation постоянного уровня и RunLevel ≥1 в обоих критериях всех шести агентов. В префабах проверены точный Source, действие RunUpgrade, все четыре UnityEvents и начальное отображение Ruins. В FrameworkSurviveGameplay нет верхних BuildingPanels и пропавших MonoBehaviour; ExperienceUI находится под FrameworkGameplay. Unity: Play Mode выключен, isCompiling=false, scriptCompilationFailed=false. Source и binding сохраняют защиты от повторного входа и устаревшего ответа.

Предварительные изображения `/tmp/chainrush-building-sites-20261011/building-stage0.png` и `building-stage1.png` получены из изолированных Editor preview объектов с демонстрационными UI-данными; Production, Activity и экономические операции для них не запускались. Чтение authoring: `/tmp/chainrush_sites_final_read.cs`; исходные свидетельства ручного запуска сохранены в ранее указанном каталоге. Fixture адресации обновлён, но не запускался. Рабочий игровой цикл, партии, cooldown и cleanup во время игры остаются приёмкой после отдельного разрешения; успешный игровой результат этими проверками не заявляется.


## Четыре сбоя в оставленном запуске — 2026-10-11

**Implementation decision: working model.** Это конкретное дополнение к завершённому шагу уровня 0. Пользователь согласовал весь описанный пакет ответом «Окей, действуй»; результат выполнения записан ниже.

**Сообщённые симптомы (буквально):**
1. «Одно здание сразу построено и сразу выпускает юнитов, хотя формально это только заготовка».
2. «юниты одинакового размера с героем, давай сделаем их в 2 раза меньше».
3. «юниты выпускаются в одной точке и залезают друг на друга. Нужно дать площадку для выпуска, смотри MorbooFramewok».
4. «Перестал работать выбор на доске, она заполняется но нажатие ничего не выбирает». Play Mode оставлен пользователем на паузе.

### Verified

Проверен только Editor ChainRush, projectPath /Users/ionrain/ChainRush, порт 7800. Читался только ChainRush.log. Свидетельства сохранены в /tmp/chainrush-buildings-paused-20261011: run.log, state.json, origin.json, storage.json, endpoint-selection.json. Игру, тесты и искусственные клики не запускали; пауза и Play Mode не менялись.

- InfantryPlacement завершён; затем выполнены два InfantrySoldierRelease. InfantryRunUpgrade в истории заказов отсутствует. У Infantry Instance 277 и Prototype 276 RunLevel=1, ProductionData=1. Настоящие новые authored seeds InfantryBuilding не содержат ни одного из этих двух ресурсов. Они присутствуют в зарегистрированных кошельках исходной Implementation. Снимки Storage для InfantryBuilding__runtime и RangedBuilding__runtime содержат прежние RunLevel=1 и ProductionData=1. Таким образом начальное состояние наследуется из сохранённого содержимого, а не выдаётся новым Placement или ценой первого улучшения. Catalog/BoardCard в этих же сохранениях остаются корректными.
- ProductionUISource этого здания имеет аргумент 1, AtMinimum=false, AboveMinimum=true. В представлении одновременно активны Building и Ruins. Publish уведомляет UnityEvent только при изменении bool; первое false относительно исходного false не передаётся authored получателю. Начальное состояние объекта-получателя события не гарантируется первой оценкой.
- Runtime root солдат имеет scale=1; ProjectionBindingContext.Scale=1. WorldProjectionTarget.TryApplyPose назначает root.localScale из materialization pose, поэтому prefab root scale=0.2 не сохраняется. Renderer солдата имеет размер примерно 3.50×3.87, героя — 3.40×3.83. Настройка размера должна находиться на визуальном дочернем Transform, независимо от мирового pose.
- Два солдата имеют разные исходные позиции (8.6,0,8.6) и (8.6,0,8.0), расстояние 0.6; изображения шириной примерно 3.5 перекрываются. Существующая площадка — SpatialShapeProviderController с Volume 3×1×3, cellSize X/Z=300 (0.3), якорь сбоку на X=1. Это не отсутствие provider. Пример MorbooFramework BarracksRed использует тот же provider и пространственные reservations, Volume 5×1×5 с шагом 1500. Новый путь выпуска не нужен.
- Autobattle 2 Running с незавершённым закрытием дочерней Board 3 Closing. BoardUI активна, 16 ячеек, selectionLocked=false, awaitingBoardRefresh=false, context и receiver заданы. SelectionRuntime содержит один нетерминальный запрос Board с пятью Inputs и ProcessedInputCount=0. Нажатия доходят до посредника; UI выделяет карточки только после SelectionProgressEvent, публикуемого обработкой SelectionAgent. CloseCore снимает tick callback Board, поэтому этот ввод не обрабатывается.
- Лог содержит исполненную ветку отказа: AfterObjective ActivityOrchestrationConfigData бросает «Agent assignment endpoint completion handle is not active», после чего ActivityService.QueueClose(ExternalAbort) закрывает ребёнка Board. У четырёх назначений AgentService статус Completed; их handles отсутствуют в _recordsByHandle endpoint. AgentService.CompleteProcessAssignment публикует OrchestrationEndpointTerminatedEvent, синхронный слушатель endpoint удаляет запись; Orchestration ставит terminal event и completion Objective в очередь. TryCompleteObjectiveWork/TrySettleAcceptedProcess затем может обращаться к уже удалённому handle. Явная отмена как ближайшая конкурирующая причина не соответствует найденным назначениям Completed.

### Inference / ограничения

Игровой результат после предлагаемых изменений ещё не проверен. Ошибка «Economy wallet is released or closed to new work» во время закрытия Board — отдельное наблюдение cleanup; её конкретная причина этим чтением не установлена. Обход или изменение правил Economy cleanup в этот пакет не включаются. Отказы executor-availability также сохранены в логе; не изменять decomposition operators по одному тексту отказа. Сначала устранить доказанный сбой завершения и проверить следующий пользовательский запуск.

### Existing System Fit

- EconomyStorageBridge/Storage загружает сохранённое содержимое; CapabilityHost runtime наследует Implementation → Prototype → Instance. Начальное авторство уже исправлено; автоматический reset в этом пути не добавляется.
- ProductionUISource владеет оценкой и событиями представления; Ruins/Building переключаются authored UnityEvents. Он обязан опубликовать значения первого снимка независимо от исходного bool получателя.
- ProjectionService/WorldProjectionTarget владеют мировым pose, дочерний Transform — размером рисунка. SpatialBodyData, footprint и Movement spacing задают физические размеры. Общий pose-контракт не меняется ради солдат.
- SpatialShapeProviderController и Population владеют площадкой, проверкой занятости и reservations; Production материализует по их точной placement. Переиспользуется путь Sandbox Barracks.
- AgentService владеет назначением; AgentAssignmentOrchestrationEndpointRuntime — регистрацией endpoint; OrchestrationProcessRuntime — подтверждением завершения процесса. Activity управляет tick и закрытием; SelectionRuntime/SelectionAgent обрабатывают ввод; BoardUI только отображает результат. UI не получает прямого доступа к этим сервисам.

### Конкретный пакет исправлений

1. С разрешения остановить только оставленный ChainRush Play Mode. После штатного сохранения и остановки сделать резервную копию двух точных Storage snapshots. Явно удалить только четыре старых holdings: Infantry RunLevel/ProductionData и Ranged RunLevel/ProductionData. Ключи owners/wallets сохранить; остальные ресурсы, выбранные записи, Coins и постоянное развитие сохранить. Проверить отсутствие этих четырёх записей и полное совпадение остальных. Это разовое ручное действие над данными данного профиля, без runtime/editor migration и без полного сброса профиля. Heavy не изменять: такого сохранения в проверенном составе нет.
2. ProductionUISource.Publish: публиковать AtMinimum/AboveMinimum/MAX также при первом получении снимка; повторные неизменные обновления сохраняют нынешнее подавление. Сохранить защиту от повторного входа и устаревшей Data. Не добавлять BuildingUIController.
3. В шести Soldier/Veteran projection prefabs root scale=1; уменьшение до половины размера исходного изображения перенести на визуальный дочерний Transform. Увеличить нынешние физические размеры с прежних 0.2 до согласованных 0.5 от исходной формы: соответствующие body usage, footprint и movement spacing. Характеристики, навыки и элитные Water/Cola не менять.
4. В трёх Building prefabs расширить существующий Volume до площадки 3×1×3 с шагом 2500 по X/Z. Разместить её перед зданием с внешней стороны стены: якорь (0,0,-4), центры относительно якоря от (-2500,0,-2500) до (2500,0,2500). Сохранить provider type, Random, reuse policy, markers и существующие проверки занятости. Площадка даёт девять разнесённых мест; повторное использование после освобождения остаётся существующим поведением.
5. AgentAssignmentOrchestrationEndpointRuntime: не удалять запись при простом получении terminal event до подтверждения её владельцем. Сохранять её до успешного TryComplete с результатом Completed, отмены или терминального состояния процесса; Continues оставляет регистрацию. Удаление по завершению процесса использует существующую _admissions и точный handle, без второго журнала. Dispose освобождает оставшиеся записи существующим TryCancel. AgentService, EventBus, общий порядок фаз и UI доски не менять. Существующие endpoint fixtures обновить по этому контракту, не запускать.
6. После всего пакета прочитать сохранённые поля и проверить компиляцию Editor при выключенном Play Mode. Игровой запуск и тесты не выполнять. Обновить ImplementationStatus.md фактическим результатом.

**Best Practices.** Регистрация живёт до подтверждения её владельцем, а не до отправки уведомления: существующие IOrchestrationEndpointRuntime.TryComplete/TryCancel и OrchestrationProcessRuntime.TrySettleAcceptedProcess определяют эту границу. Первое состояние представления публикуется полностью, следующие обновления адресно подавляются в одном Source. Размер визуального ребёнка не подменяет контракт мирового pose. Для размещения переиспользуется проверенный общий provider/reservation путь BarracksRed.prefab; отдельный spawn manager, опрос каждый кадр и новые подписки не вводятся.


### Выполнение согласованного пакета

Все шесть пунктов выполнены. Остановлен только ChainRush Play Mode; повторного запуска не было.

- ProductionUISource публикует AtMinimum/AboveMinimum/MAX также при первом hasSnapshot. Повторный неизменный снимок не повторяет bool-события. Защита от повторного входа через identity Data сохранена.
- AgentAssignmentOrchestrationEndpointRuntime больше не слушает terminal event для удаления записи. Удаление происходит при подтверждённом TryComplete(Completed), успешном TryCancel или терминальном событии процесса через точный handle существующей admission. Continues оставляет запись; Dispose использует прежний cleanup. Другие endpoint и общий порядок фаз не менялись.
- Шесть Soldier/Veteran prefabs: root scale 1, визуальный дочерний Spine Transform 0.4 вместо 0.8. Это половина изображения в наблюдавшемся мировом pose, а не половина перезаписываемого prefab root. Шесть definitions: footprint 500 вместо 200. В шести Body геометрия и её offset увеличены в 2.5 раза относительно прежней настройки 0.2; в шести Movement spacing 125 вместо 50. Характеристики и навыки сохранены.
- Три Building prefabs: существующий Volume 3×1×3, cellSize X/Z 2500, position (-2500,0,-2500), якорь выпуска (0,0,-4). Provider types, Random, reuse, markers и Population reservations сохранены. Это девять ячеек области; существующий Volume-подбор сохраняет своё вычисление конкретных позиций внутри области. Нового ограничения числа живых солдат не добавлено.
- После остановки из двух точных Storage snapshots удалены четыре согласованные holdings: Infantry/Ranged RunLevel и ProductionData. Использованы существующий DefaultStorageRuntime и прежние SettingsKey/SettingsContext, без установки нового runtime, изменения формата и автоматического преобразования данных. Сравнение payload подтвердило совпадение всех остальных записей, owner/wallet IDs, форм и тегов. Хеши остальных 18 settings.json не изменились. Тяжёлое здание и общий профиль не записывались. Резервные копии: /tmp/chainrush-buildings-paused-20261011/storage-backup/infantry-before.json и ranged-before.json; там же отдельные snapshots и исходные hashes. Постоянная копия этой резервной папки сохранена вне рабочих settings: /Users/ionrain/Library/Application Support/Morboo/Slime Lords/morboo/backups/buildings-level0-20261011-160129.
- Существующий CoreUIEditModeTests fixture дополнен проверкой первого false для authored представления, повторного неизменного снимка и перехода к уровню 0. AgentAssignmentEditModeTests finite-release fixture обрабатывает завершение Objective до удаления цели и проверяет отсутствие исключения после terminal event. Fixtures не запускались. Package EditMode fixtures не входят в текущий compilation context ChainRush; их отдельная компиляция не выполнялась.

**Проверка без игрового прогона.** Все 27 authoring-файлов сохранены через Unity; сравнение с резервными копиями показывает только согласованные поля. Все 27 .meta неизменны. Настройки повторно прочитаны из настоящих C# полей: у всех зданий Ruins активны, Building неактивен, seeds не содержат стартового уровня или производства; размеры и области совпадают с записанными значениями. Editor ChainRush после принудительного refresh/компиляции: isPlaying=false, isCompiling=false, isUpdating=false, scriptCompilationFailed=false. Фактические readbacks и отдельный authoring diff сохранены в /tmp/chainrush-buildings-paused-20261011.

Игру и тесты агент не запускал. Приёмка исправленного выбора, стартового уровня и размещения солдат требует следующего ручного запуска. Успешный игровой результат не заявляется. Наблюдавшаяся ошибка cleanup Board и отказы executor-availability остаются сохранёнными свидетельствами; поведение этих веток вне согласованного пакета не менялось.

## Накопление карточек без улучшения и остановка пересборки доски — 2026-10-11

**Сообщённый симптом (буквально):** «Возникают ошибки, карточки накапливаются, прогресс отображается в прогресс баре, но переход на следующий уровень не происходит (на прогрессбаре отображается 4 из 3 необходимых карточек). Соответственно, юниты не выпускаются из здания. Также опыт прилетает, но не позволяет пересобрать доску. Видимо это твои правки в AgentAssignmentOrchestrationEndpointRuntime (line 253)».

Пользователь оставил ChainRush Play Mode на паузе. Только чтение текущего запуска с явным project-path и проверкой Application.dataPath. Состояния и лог сохранены в `/tmp/chainrush-upgrades-paused-20261011/`. Игру и тесты агент не запускал; Play Mode не менялся.

### Verified

- Autobattle (2) и Board (3) Running. Пехотная заготовка — Entity 277, прототип 276, уровень 0. Источник её UI показывает InfantryCards Balance=4, Amount=3, ProgressionArgument=0. Четыре InfantryCardMerge завершены; InfantryRunUpgrade не поставлен. Исходные экономические данные совпадают с отображением шкалы.
- Опыт игрока — 9, ExperienceUI прочитал те же 9; выполненных ExperienceToTurnToken — 0. PlayerSpawner 207 существует, Production принимает заказы, очередь и pipelines пусты. Поиск проблемы в начислении опыта либо в заполненности очереди не объясняет этот запуск.
- Единственная удерживаемая Agent executor reservation в Autobattle: RangedPlacementAgent, assignment 3, процесс 2:3, PlayerSpawner 207, PerWork. Ranged выбран и Available; Population content присутствует. Поэтому это не ожидание отсутствующей выбранной записи.
- Ranged Population Materializing: PlannedCount=1, MaterializedCount=0, PendingAdmissionCount=0, Placements.Count=0; `_groupIndex=1`, `_scanNeeded=false`. Запланировано `(7,0,8)`. Последний region probe Contains=true; occupancy probe CanPlace=false, CommittedOccupant=277. Именно в этом месте уже стоит Infantry. Чтение occupancy по якорям двух других выбранных областей `(7,0,15)` и `(7,0,22)` возвращает CanPlace=true.
- `SpatialPopulationDistributionAlgorithmData.Session.Run` при RequireFullVolume сразу выполняет второй проход в выбранном pattern/region; на втором проходе допускает занятое место как ожидаемое. Другие свободные regions ещё не исчерпаны. `PopulationProductionSession.Advance` затем отвергает это место по текущей occupancy, наблюдает блокировщик и оставляет ту же запланированную позицию. Подтверждённое состояние этой ветки объясняет незавершённое размещение и удержание исполнителя.
- ProductionAgentDependencyResolver для занятого 207 создаёт ExecutorAvailableOrchestrationQueryData. Лог доказывает отказ OrchestrationPlanningFactRecordFactory.TryCreate для двух таких фактов: 2:24 (токен) и 2:151 (улучшение). В factory нет ветки этого query; соответствующего OrchestrationPlanningFactType также нет. Родители 2:19 и 2:146 Failed из-за invalid dependency. Это существовавшие ранее ошибки; прошлый пакет их явно не менял.
- У факта доступности 2:151 OwnerRef=capability-host:276 / Entity 276, а владелец производителя 207 — player:FrameworkPrototypesProfile. CreateExecutorAvailableFact копирует адрес получателя выпуска. Resolver ExecutorAvailable сверяет host.Owner с OwnerRef, поэтому этот адрес также не подходит для наблюдения исполнителя. Производитель выбирается у участника, получатель улучшения остаётся прототипом; это разные роли.
- Проверка гипотезы об endpoint: прежнего исключения «endpoint completion handle is not active» в текущем запуске нет. Selection request обработал все четыре inputs и Committed; Board остаётся Running. Активная запись placement endpoint сохранена. Процессы токена и улучшения не дошли до приёма Agent assignment: отказ возникает при построении их зависимости. Обработчик терминального состояния процесса на строке 253 не является веткой этих двух отказов.
- Отдельно сохранён warning assignment 5 InfantrySoldierAgent: EconomyAmountCriterionData (RunLevel >=1) не проходит при уровне 0, после чего Agent пытается представить её как запрос производства исполнителя и получает отказ. Процесс выпуска WaitingInvalidation. Нельзя заявлять успешный выпуск после устранения первых блокировок без следующего наблюдения; это дополнительное свидетельство, не доказательство отсутствия других проблем выпуска.

### Inference

Свободное размещение Ranged освободит PlayerSpawner, но само по себе не исправляет контракт ожидания: оба корневых процесса уже Failed. Полноценное устранение блокировок требует исправить выбор места и представление существующего факта доступности. Успешное исполнение последующего улучшения и партии в текущем чтении не наблюдалось.

### Согласованный пакет — working model, реализован 2026-10-11

1. `SpatialPopulationDistributionAlgorithmData`: сначала исчерпать свободные варианты во всех доступных patterns/regions. Только после этого RequireFullVolume может сохранить занятый вариант для ожидания. Сохранить текущий seed, бюджет, shape/content allocation, точные Entity targets и spatial reservations. Не перебирать занятый первый region вместо двух свободных. Не добавлять менеджер мест или обход всех объектов каждый кадр.
2. Довести существующий ExecutorAvailable query до общего планирования: отдельное значение OrchestrationPlanningFactType без изменения старых чисел, runtime payload без повторного копирования списка Entity, ветка OrchestrationPlanningFactRecordFactory. В PlayerBrain явно подключить существующий AwaitFactDecompOpData для этого типа и decision для OrchestrationFactType.ExecutorAvailable. Ожидание пробуждается существующим AgentExecutorAvailabilityChangedEvent, а не периодическим опросом. Состояние Impossible не подменять успешным результатом.
3. В ProductionAgentDependencyResolver формировать OwnerRef факта доступности от исполняющего участника. Адрес ProductionRelease и получатель заказа остаются точным прототипом. Не менять плательщика, цену, резервирование и выпуск.
4. Обновить затронутые существующие fixtures и явно проверить остальные authored потребители общего пути Wait, включая OrchestrationSandbox; при наличии такого пути подключить тот же authored AwaitFact, без автоматической подстановки операторов. Fixtures не запускать. После остановки Play Mode и окончания всего пакета проверить сохранённые поля и компиляцию; игру не запускать.

**Existing System Fit.** Population владеет выбором места и удержанием region turn; Spatial владеет footprint/occupancy/reservations и остаётся без знаний о зданиях. AgentService владеет executor reservation и её освобождением. ProductionAgentDependencyResolver создаёт требования, factory передаёт их общему планировщику, существующий AwaitFact и fact-change adapters обеспечивают ожидание. Production остаётся владельцем admission, списания и выпуска; Economy — балансов. BoardUI ждёт обычный токен и не должен обходить отказ Orchestration. Endpoint completion, UI шкалы, Skills, Activity lifecycle и Storage этим пакетом не изменяются.

**Best Practices.** Не путать временную занятость с ошибкой контракта; сохранить различие participant/executor/recipient. Переиспользовать существующие AwaitFactDecompOpData и AgentExecutorAvailabilityChangedEvent с адресной инвалидацией. Свободные альтернативы размещения проверяются перед ожиданием занятого места внутри существующей бюджетной Session.Run. Источники этих контрактов: `OrchestrationProcessRuntime.TryPlan`, `ExecutorAvailableOrchestrationFactSnapshotResolver.TryResolve`, `ProductionAgentDependencyResolver.TryCreateDependencyOption`, `AgentService.TryEnterPopulationRegions`, `PopulationProductionSession.Advance`. Новая параллельная очередь или расчёт цены не требуется.


### Выполненные изменения и проверка

Пользователь явно согласовал весь пакет и остановку Play Mode ответом «Согласовать исправления и остановку Play Mode». Остановлен только Editor ChainRush; после остановки `isPlaying=false`, `isPaused=false`. Повторного игрового запуска, снятия паузы для игры или исполнения тестовых методов агентом не было.

- В `SpatialPopulationDistributionAlgorithmData.Session.Run` свободный проход распространяется на все подходящие patterns/regions. Если он исчерпан, RequireFullVolume переходит к проходу, допускающему ожидание occupancy. Для двух проходов сохраняются отдельные ревизии ожидания; после успешного размещения или изменения входной ревизии приоритет возвращается свободному поиску. Используются прежние IEnumerator, workBudget, seeded random, проверки footprint/пары размещений, content allocation/refund и reservations. Новых списков кандидатов по кадрам либо второго владельца пространственного состояния нет.
- `OrchestrationPlanningFactType.ExecutorAvailable=53` добавлен без изменения старых чисел. Factory создаёт `ExecutorAvailablePlanningFactPayload`, который удерживает исходный query, а не копирует список Entity. Факт исполняется прежним AwaitFact; resolver и событие адресного освобождения исполнителя сохранены. Impossible остаётся Impossible.
- `ProductionAgentDependencyResolver.CreateExecutorAvailableFact` получает planning context и адресует наблюдение исполняющему участнику. ProductionAvailable/Release, исходное требование и получатель выпуска остаются адресованными прежнему получателю; улучшение прототипа не подменяется улучшением игрока.
- В PlayerBrain существующий AwaitFact принимает EconomyAmount и ExecutorAvailable; добавлено отдельное условие FactType=ExecutorAvailable. В EnemyBrain, где EnemyWaveAgent также использует общий Production Wait, явно добавлен AwaitFact только для ExecutorAvailable. Повторное чтение Unity после импорта: PlayerBrain — 36 operators / 39 nodes, EnemyBrain — 6 / 6; по одному совпадающему AwaitFact decision. GUID обоих assets сохранены. Новый authored string id для decision не задавался. Явный авторing-helper `ChainRushAutobattleVerticalSliceAuthoring` сохраняет эти настройки при его ручном применении; он в этом шаге не запускался целиком.
- В Sandbox обнаружены ConstructionAgent и ProductionAgent с Wait. В существующий OrchestrationSandboxOrchestratorAIBrain явно добавлены AwaitFact, decision и его отдельное условие. Переиспользован существующий taxonomy term `entity.available`: реестр различает оператор также по input fact type, и ветка 53 отделена от прежней MaterializedEntity ветки. Никакого нового строкового ключа или автоматической установки оператора нет. Editor MorbooFramework закрыт, поэтому изменён существующий asset на диске; его 102 managed-reference определения проверены на повторение и отсутствующие rid, GUID taxonomy и asset сохранены. Импорт этого файла настоящим Editor MorbooFramework в этом шаге не выполнялся.
- BoardBrain и Level01/Level02EnemyBrain используют Population/Selection factories; общий ProductionAgentDependencyResolver для их назначений не вызывается. AwaitFact им не подставлялся.
- Обновлены существующие fixtures ProductionCapabilityHostEditModeTests и AgentAssignmentEditModeTests: отдельный runtime-получатель при участнике-производителе, Awaitable → Satisfied при освобождении исполнителя, представление/ожидание существующего query без копии Entity, приоритет свободной второй области и сохранение ожидаемого размещения при отсутствии свободных, бюджеты 1/17/256. Эти случаи не исполнялись.

Компиляция runtime и Editor ChainRush завершена: `isCompiling=false`, `isUpdating=false`, `scriptCompilationFailed=false`; новый payload присутствует в sourceFiles Core. Затронутые fixtures и их существующие helpers (9 исходных файлов) отдельно скомпилированы в `/tmp` под именем существующей тестовой сборки: 0 ошибок. DLL не импортирована в проект и тестовые методы не вызывались. Настройки сцены, балансы сохранения и код endpoint completion этим пакетом не менялись. Новую `.meta` payload создал импорт Unity.

Свидетельства проверки: `/tmp/chainrush-upgrades-paused-20261011/final-authoring.json`, `fixture-compilation.txt`, `package-fixture-compilation.txt`; исходное состояние остаётся в файлах раздела Verified. Компиляция не подтверждает игровой результат: следующий запуск должен показать размещение второго здания, расход карточек и выдачу токена. Отдельное предупреждение выпуска солдат при RunLevel=0 сохранено выше; его исчезновение после уровня 1 пока не проверено.


**Отдельное ограничение общей компиляции fixtures.** Помимо целевой сборки из 9 файлов выполнена только компиляция всех 60 EditMode-исходников package, без вызова тестов. Она выявила два отказа в неизменённом этим пакетом `GameplayFoundationEditModeTests.cs:4044`: CS0200 (назначение readonly `WalletEntry.Wallet`) и CS0117 (`WalletEntry.SeedEntries` отсутствует). Это старый object initializer другой fixture; текущий production/economy runtime по этому свидетельству не менялся. Полный отчёт сохранён в `package-fixture-compilation.txt`. Общая сборка fixtures не объявляется прошедшей; эти два отказа требуют отдельного исправления вне согласованного пакета размещения и ожидания.

## Одно здание на карточках и отсутствие солдат после уровня 1 — 2026-10-11

Исходный симптом пользователя: «Здание обновляется, но: 1) появляются карточки только одного здания; 2) юниты из здания не выпускаются». Прочитан только отдельный ChainRush.log; Editor явно выбран по /Users/ionrain/ChainRush, Play Mode оставлен на паузе. Новых изменений исполнения не внесено.

### Verified

- Autobattle 2 и Board 3 Running, время 73.79585. Infantry instance 277 имеет InfantryRunLevel=1 и InfantryProduction=1. Его производитель зарегистрирован с InfantryCatalog, принимает заказы, очередь и активные pipelines пусты. Ranged instance 485 существует на уровне 0. Reservations агентов пусты.
- В этом запуске принято и завершено девять InfantryBoardCard, распределённых по трём конечным release: 4/3/2 карточки. Заказов RangedBoardCard нет. Источник outputs всех девяти — точная запись InfantryBoardCard в реализации выбранного InfantryBuilding.
- В кошельке игрока InfantryBuilding и RangedBuilding положительны и имеют Available, Entitled, Selected. Во втором интервале BoardPopulationAgent правило Building сохраняет share 0.2, EntryWallets и DeterministicRandom. Прочитанный снимок разрешённого содержимого содержит оба точных источника — InfantryBoardCard и RangedBoardCard. Обе записи рецептов и outputs доступны в текущих C#-полях каталога. Это исключает отсутствие выбранного Ranged как объяснение нынешнего состояния.
- BoardPopulationAgent.SingleAssetPerShape=true. PopulationContentSelectionSession выбирает один результат на начало фигуры, затем использует его для остальных клеток этой фигуры. Значения seed, полный список методов и выбор из них для уже завершённых заполнений не сохранились: соответствующие Population executions очищены.
- Лог содержит исполненную ветку Agent.TryProjectExecutorAvailabilityFact: Required EconomyAmountCriterionData не может быть превращён в требование производства исполнителя. Оба SoldierAgent получили Failed. Infantry release сейчас WaitingInvalidation с сообщением об исчерпании кандидатов; заказов выпуска солдат нет, хотя производитель уже появился. У шести Soldier/Veteran agents остаются критерии RunLevel >= 1 на исполнителе и цели; их предварительная применимость проверяет только постоянный уровень < 3 / >= 3.
- Agent.EnsureExecutorByCriteria после пустого результата выбора всегда пытается сформировать запрос недостающего исполнителя; существующая BusyPolicy.Wait относится к занятому подходящему исполнителю. AgentSelectionSession уже имеет роль Executor, кеш результата, адреса экономических владельцев и обработчики изменения экономических записей. Этот механизм сейчас используется для целей, но не подключён к обычному выбору исполнителя в указанной ветке.

### Inference и ограничения

Ошибка выдачи солдат установлена по исполненному отказу, состоянию Failed и отсутствию дальнейшего заказа. Полный путь: цель активируется после размещения заготовки → агент доступен по постоянному уровню → RunLevel=0 исключает исполнителя → попытка заказа исполнителя → неподдерживаемый критерий → отказ и ожидание изменения внешнего факта. Изменение RunLevel не меняет внешний success-факт цели. Недоступный либо занятый производитель после постройки не соответствует текущему снимку.

Причина выбора только Infantry во всех трёх заполнениях **не установлена**. Однородность фигуры сокращает число независимых случайных выборов; девять карточек не являются девятью независимыми выборами. Нельзя объявлять генератор неисправным, изменять seed либо выключать SingleAssetPerShape по этому свидетельству. Для различения случайного повторения и потери метода Ranged нужен снимок внутри фактической ветки выбора при новом заполнении.

Свидетельства: /tmp/chainrush-building-release-paused-20261011/run.log, state.json, processes.json, detail.json, board-rules.json, board-catalog.json. Read-only capture разрешённого содержимого сделан отдельным диагностическим чтением; это не сохранённый список кандидатов прежнего исполнения.

## План выпуска партий через рецепты

**Implementation decision: working model. Статус: реализация завершена; игровые прогоны и тесты не выполнены.**

Одна партия солдат — один цикл рецепта. Рецепт определяет количество; Objectives и Orchestration запускают очередной цикл, существующий Production endpoint ставит обычный заказ и наблюдает его завершение. `ProductionStartPolicyType.Automatic` не меняется.

### Existing System Fit

| Ответственность | Владелец и переиспользование |
|---|---|
| Количество outputs и аргумент прогрессии | `ProductionOutputData`, `ProductionRecipeData`, общий `ProductionProgressionResolver` |
| Подготовка, приём заказа, списание, очередь, выпуск, компенсация | `ProductionRuntime`, его существующие admission и settlement, `ProductionOrderId` |
| Интервал между партиями | Существующий cooldown каталога конкретного производителя |
| Условия начала и повторение | Три существующих Objective зданий, штатные условия и Reset |
| Выбор рецепта и производителя, ожидание заказа | `ProductionYieldDecompOpData`, `ProductionDecompOpUtility`, `ProductionOrchestrationEndpointRuntime` |
| Результат текущей активации цели | Существующие `ObjectiveExternalFactScope` и `ObjectiveExternalFactStateRegistry` |
| Пространственное размещение outputs | `ActivityProductionOutputMaterializationModule`, провайдер маркеров здания и Spatial reservations |
| Размещение заготовок и заполнение доски | Нынешний Population; его участие в этих операциях сохраняется |
| Развитие и передача состояния здания | Economy и CapabilityHost; нынешние реализация, прототип и экземпляр |
| Оценка и наблюдение производства | UI-адаптер, Knowledge, существующие снимки и события Production |

**Verified по исходникам:** output рецепта уже имеет `LongProgressionData`; один цикл может выдавать несколько отдельных Token. Общий endpoint уже хранит `OrderId`, обрабатывает завершение и отмену. `ProductionRelease` считает количество экономических outputs, а `ProductionYield` — исполнения рецепта. Нынешний `ProductionYield` читает накопленное число исполнений; он ещё не выражает «одно исполнение в текущей активации Objective». Прогрессия рецепта пока читает только номер исполнения либо ресурс получателя. Поле `Automatic` не создаёт заказы. `MaxParallelPipelines` объявлен, но текущий runtime создаёт и обрабатывает одну линию; разные производители обрабатываются независимо.

Последние два ограничения не объявляются причиной наблюдаемого отказа солдат: свидетельство фактического отказа Agent приведено выше. Многолинейность и самостоятельный автоматический запуск не нужны для партии из нескольких outputs и в этот шаг не входят.

### 1. Итоговый путь партии

`RunLevel >= 1 → повторяемая цель → выбор рецепта и точного производителя → заказ с RequestedYields = 1 → outputs рецепта → размещение каждого Token → подтверждённое завершение заказа → успех текущей активации → штатный Reset`.

Получатель солдат и плательщик остаются участником. Источник количества — прототип выбранного экземпляра здания. У каждого здания свой заказ и свой cooldown; ожидание места у одного здания не должно удерживать заказы других зданий через общую очередь Agent.

Используется прямой endpoint Orchestration, уже исполняющий Production. Шесть Soldier/Veteran Population-агентов удаляются из контента и больше не участвуют в выпуске солдат. Заменяющий их общий `ProductionAgent` не создаётся. Изменения `Agent.Tick`, режима ожидания отсутствующего исполнителя и захвата Entity ради этого выпуска исключаются из плана.

### 2. Количество в рецепте и явный источник производителя

В существующий `ProductionProgressionSourceType` добавить `ProducerResource`, сохранив числовые значения `RecipeOrdinal` и `RecipientResource`. Других новых сериализуемых полей рецепта не требуется: используются нынешние `ProgressionOwnerLevel`, `ProgressionResource`, `RestrictProgression`, `ProgressionInterval` и `Outputs[].AmountProgression`.

У шести рецептов выпуска задать:

| Поле | Значение |
|---|---|
| ProgressionSource | ProducerResource |
| ProgressionOwnerLevel | Prototype |
| ProgressionResource | Точный `{Type}RunLevel`, Stack, существующий кошелёк развития |
| Допустимый интервал | Уровни 1–5 включительно |
| Output | Свой обычный либо улучшенный солдат, Token, кошелёк участника |
| AmountProgression | 2 / 3 / 4 / 5 / 6 для уровней 1 / 2 / 3 / 4 / 5 |
| RequestedYields в заказе | 1, независимо от количества солдат |

Отдельные ресурсы RunLevel трёх зданий сохраняются: их объединение не требуется, поскольку каждый рецепт выбирает собственный ресурс. В агенте и Objective количество 2–6 не дублируется.

Для чтения производителя расширить `ProductionAttachRequest`, внутреннюю регистрацию и `ProductionSnapshot` явным `ProducerEconomyOwner`. `CapabilityHostProductionController` передаёт `record.Snapshot.SelfEconomyOwner` — экономического владельца самого экземпляра. Нынешние `ControllerOwner` и технический `ProductionOwner` сохраняют свои роли; они не подставляются вместо владельца здания. Обновить все вызовы attach и fixtures явно, без совместимого конструктора с догадкой о владельце.

Общий resolver получает контекст зарегистрированного производителя и получателя; нового реестра владельцев нет. Production работает через `IEconomyAssetOwner`/`IEconomyOwnerLineage`, не обращается к конкретному `CapabilityHostService`. Отсутствующий запрошенный владелец или уровень — явная ошибка; перехода к игроку или номеру исполнения нет.

Переключить все вызовы resolver: оценку, validation/admission, подготовку входов, фиксацию аргумента выпуска, проверку Release, Orchestration methods, materialization planning, существующие Production/Construction agent callers, UI и Knowledge. Их алгоритмы исполнения не переписываются — им передаётся один полный контекст расчёта.

Аргумент фиксируется существующей политикой резервирования. Для партий зданий явно оставить `OnEnqueue`: окончательный admission фиксирует размер партии; изменение уровня во время незавершённой подготовки учитывается существующей повторной проверкой. После подтверждённого admission размер не меняется. Следующий заказ читает актуальный уровень. Оценка до admission остаётся предварительной.

### 3. Одно исполнение рецепта в текущей активации Objective

Добавить одну authored-привязку `ProductionYieldFactBindingData` в `Core/Production/Orchestration`. Она наследует существующий `OrchestrationExternalFactBindingData` и имеет два собственных сериализуемых поля:

- `recipe`: `ProductionRecipeData`;
- `recipient`: существующий `EconomyOperationOwnerBindingData`.

Использовать существующие ключи внешних фактов целей; новых строковых полей, числового объёма, списка исполнителей или параметров здания здесь нет.

Привязка создаёт существующий `ProductionYieldOrchestrationQueryData` с runtime-режимом `CurrentActivation` и `ObjectiveExternalFactScope`. В runtime enum `ProductionYieldGoalType` различить `TotalYields` и `CurrentActivation`. Первое сохраняет настоящее поведение накопленного счётчика; второе означает ровно один заказ на один цикл, принадлежащий данной активации. Это разные действующие требования, а не совместимая ветка старого формата.

Обновить существующие query identity, `ProductionYieldPlanningFactPayload`, `OrchestrationPlanningFactRecordFactory`, snapshot resolver и `ProductionYieldDecompOpData`. Для CurrentActivation факт до результата неудовлетворён; после результата читается существующий scoped external fact. Исторический выпуск, чужой заказ того же рецепта и результат предыдущей активации его не удовлетворяют.

Для адресной инвалидации добавить `ProductionYieldFactChangeMatcher` и подключить к существующему обработчику `ObjectiveExternalFactStateChangedEvent` в Orchestration. Нового типа planning fact, сервиса целей, scheduler или журнала выполненных партий не вводить. `ObjectiveConditionProductionYield` с накопленным счётчиком сохраняет свой контракт.

`ProductionYieldDecompOpData` выбирает метод рецепта обычным путём. После выбора существующий payload должен сохранять точную Entity производителя; Start проверяет именно её, участника, domain и доступность рецепта. При её исчезновении запрос не перенаправляется молча другому зданию. До выбора допустим штатный подбор подходящего производителя.

В `ProductionOrchestrationEndpointRuntime` для CurrentActivation:

1. Проверить, что scope относится к действующей активации цели.
2. Создать обычный заказ с `RequestedYields = 1` и запомнить scope рядом с уже хранимым OrderId.
3. Оставаться выполняющимся при PreparingAdmission, Working, AwaitingOutput и компенсации. Принятую команду повторно не отправлять.
4. Только при успешном конечном результате этого OrderId отметить scoped external fact и опубликовать штатное завершение endpoint. Непосредственно перед публикацией снова проверить поколение активации.
5. Отказ, ошибка или отмена завершают существующий endpoint соответствующим результатом без success-факта. Временная недоступность до отправки использует нынешний wait по событиям Production.
6. Закрытие/Reset цели или Activity отменяет незавершённый заказ обычным путём. Сохраняется ожидание компенсации; поздний ответ не изменяет новую активацию. Очистка идемпотентна.

Использовать нынешние словари endpoint по handle и OrderId. Отдельный менеджер партий и подписка на каждый заказ не нужны. Конечные `ProductionRelease` для улучшений, Population и других заданий на заданный объём остаются действующим механизмом; солдатские цели переходят на цикл рецепта.

### 4. Authoring целей и повторения

Оставить три существующих Objective выпуска — по одному на тип здания. В каждом явно задать активацию после материализации здания и `RunLevel >= 1` через существующие `ResourceActivityAnalyticsMetricData`/`ObjectiveConditionActivityAnalytics`. Метрика читает текущий ресурс собственного экземпляра с тегами этого типа здания и нужного кошелька, а не потенциальное производство. Участник и Activity задают область наблюдения; пассивные прототипы не являются наблюдаемыми CapabilityHost instances Knowledge. В выбранной конфигурации существует один экземпляр каждого типа.

Внутри цели настроить две взаимоисключающие ветки:

- `PermanentLevel < 3`: один цикл обычного рецепта;
- `PermanentLevel >= 3`: один цикл рецепта улучшенного солдата.

Постоянный уровень читается существующим `ObjectiveConditionEconomyMetric` из точной реализации здания в BuildingCatalog. Каждая ветка использует `ObjectiveConditionExternalFactState` и новую привязку к своему рецепту. В условия активации обеих веток включить готовность здания и отсутствие Active у другой ветки через существующее условие состояния Objective. Это сохраняет выбранную ветку до конца партии, даже если постоянный уровень изменился во время заказа.

Родителю явно задать `ExplicitSuccessConditions`: `ObjectiveConditionComposite` с операцией Or объединяет два `ObjectiveConditionTargetNodesState`, каждый содержит собственный узел и проверяет Completed. Поэтому завершения неактивной альтернативы ждать не требуется. Граф оформить существующей Odin-сериализацией ObjectiveTemplate, без общих inline-элементов между SerializeReference-списками. После успеха штатный Reset повторяет всю цель и заново выбирает ветку. Уже принятый заказ сохраняет свой рецепт.

В `PlayerBrain` подключить один существующий `ProductionYieldDecompOpData` и общее правило выбора для фактов ProductionYield. Использовать существующую taxonomy и ссылки на operators, не добавлять authored строковые ключи. Удалить шесть ссылок на Soldier/Veteran agents, соответствующие decisions и сами неиспользуемые assets через Unity. Проверить ссылки во всех consumers перед удалением. Рецепты и определения шести солдат сохраняются.

Level 0 не создаёт процесса выпуска. Дополнительно интервал рецепта исключает производство при нулевом уровне, даже если заказ пришёл другим обычным путём. Первое улучшение по-прежнему выдаёт ProductionData; существующий Reconcile подключает производителя. Отсутствие свободного места или cooldown приводит к ожиданию существующих механизмов, а не к заказу нового здания.

### 5. Размещение, cooldown и независимость зданий

Один цикл с количеством N выдаёт N отдельных Token. Каждый имеет собственные output identity, позицию и подтверждение. Использовать общий маршрут от `ProductionTokenOutputPreparedEvent` к провайдеру площадки здания; не назначать партии один точный marker, предназначенный для одного объекта. Сохраняются текущие footprint, occupancy, reservations, ожидание освобождения маркера и размер солдат 0,5 от исходного представления.

Завершение заказа следует существующему подтверждению всех outputs. При заполненной площадке недостающие размещения ждут; уже подготовленный заказ не дублируется. Ошибка и отмена используют текущий rollback/compensation, не вводя нового обещания атомарности визуального появления всей партии.

Cooldown остаётся общим для каталога конкретного производителя. Сохраняется нынешний момент его запуска — первое подтверждённое положительное output; поздние подтверждения того же заказа его не перезапускают. Следующая активация не стартует новый заказ раньше доступности каталога. Бесконечный заказ не используется: его recovery и повторение имеют другой контракт.

Три здания выполняются как три зарегистрированных производителя с независимыми заказами и одной линией у каждого. Для этого не требуется многолинейность внутри одного здания. `MaxParallelPipelines`, `Agent.Tick`, общий арбитраж физически занятых исполнителей и поведение ConstructionAgent не перерабатываются.

### 6. Наблюдение и оптимизация

Новый ProducerResource должен одинаково работать в оценке и исполнении. Production разрешает и наблюдает используемые selectors своего производителя через существующие Economy watches. Одинаковые адреса/selector внутри регистрации разделяют наблюдение; изменение помечает регистрацию для одного обновления существующей очередью обработки. Добавить runtime `RecipeProgressionVersion` в состояние/snapshot и сравнение изменений, чтобы существующий `ProductionStateChangedEvent` уведомлял об изменении прогрессии даже при прежней длине очереди. Смена каталога, detach, ошибка attach и закрытие domain освобождают leases; старые callbacks проверяют точную регистрацию.

`ProductionStateOrchestrationModule` включает версию в cache revision, использует общий resolver и не подставляет фиктивного производителя для ещё не материализованного метода. Неизвестная прогрессия остаётся недоступной до появления точного producer context. `ProductionMethodRuntimeResolver` рассчитывает выбранный метод тем же кодом.

`ProductionUIAdapter` получает ProducerResource из той же регистрации и реагирует на ProductionStateChanged; отдельного чтения из каждого widget нет. Existing RecipientResource watches сохраняются. `KnowledgeProductionSourceHandler` строит факторы ProducerResource из точного snapshot и обновляется существующим событием. Получателя для RecipientResource при его отсутствии не выдумывать. Analytics/Forecast продолжают читать Knowledge; второго вычислителя и прямого чтения живых кошельков в них не добавлять.

Для партии один расчёт прогрессии на точку подготовки и один заказ; N outputs не порождают N чтений уровня. При ожидании нет опроса балансов или перебора всех зданий каждый кадр. Подбор и планирование остаются в существующем бюджете Orchestration, ожидание — по событиям. Подписки на каждого солдата ради партии/UI не появляются.

### 7. Перечень изменений кода

Все пути ниже относятся к MorbooFrameworkPackage/Scripts/Core.

| Файлы или группа | Изменение и необходимость |
|---|---|
| Production/Authoring/ProductionProgressionSourceType.cs | ProducerResource: нынешний recipient-only контекст не даёт уровень здания при получателе-игроке |
| Production/Contracts/ProductionAttachRequest.cs, ProductionSnapshot.cs; CapabilityHosts/Runtime/Internal/CapabilityHostProductionController.cs | Передать и сохранить явного экономического владельца производителя без зависимости Production от конкретного CapabilityHost |
| Production/Internal/ProductionProgressionResolver.cs; ProductionRuntime.cs, .Evaluation.cs, .Admission.cs, .InputPreparation.cs, .Releases.cs | Общий producer context, существующее фиксирование аргумента, наблюдения и их cleanup; один расчёт цены/количества |
| Production/Orchestration/ProductionYieldFactBindingData.cs — новый | Два authored поля Recipe/Recipient для одного исполнения в scope текущей цели |
| Production/Orchestration/ProductionYieldGoalType.cs — новый runtime enum; существующие QueryData, PlanningFactPayload, SnapshotResolver, DecompOpData | Различить накопленный прогресс и один цикл текущей активации без нового planning fact type |
| Orchestration/Planning/OrchestrationPlanningFactRecordFactory.cs | Сохранить режим/scope и не объединять разные активации в один факт |
| Production/Orchestration/ProductionYieldFactChangeMatcher.cs — новый; Orchestration/OrchestrationService.cs | Адресная инвалидация scoped результата через существующее событие |
| Production/Orchestration/ProductionOrchestrationEndpointRuntime.cs, ProductionDecompOpUtility.cs, ProductionMethodRuntimeResolver.cs | Сохранить точного producer, один заказ на цикл, связать его конечный результат с scope; переиспользовать существующее ожидание и отмену |
| Production/Orchestration/ProductionStateOrchestrationModule.cs; Activities/Orchestration/MaterializationOrchestrationEndpointRuntime.cs; нынешние вызовы resolver в ProductionAgentWork и ConstructionAgent | Передать общий контекст, обновить кеш; без собственной арифметики или новой модели Agent |
| UI/Production/ProductionUIAdapter.cs; Knowledge/Internal/Sources/KnowledgeProductionSourceHandler.cs | Оценка и наблюдение ProducerResource тем же путём |
| Существующие ProductionCapabilityHost, AgentAssignment и ObjectiveService fixtures | Обновить attach-контракт и проверить реальное поведение новых режимов; не запускать до отдельного разрешения |

Новых сервисов нет. Общие Economy операции, Skills/Rewards, регистрация целей Activity, UI прогрессбары карточек, данные солдат и механика строительства не меняются. При удалении шести assets агентов удалить ставшие неиспользуемыми настройки солдатского Population; настройки Board и Placement сохраняются.

### 8. Карточки только одного здания

Сохранить отдельным диагностическим подпунктом этого шага: причина пока не установлена. Добавить в существующий фактический выбор `PopulationContentSelectionSession` ограниченную временную запись списка допустимых вариантов, выбранного asset, источника и seed — максимум 16 записей на заполнение, без дополнительных запросов Economy.

После реализации и ручного запуска пользователем сопоставить Infantry/Ranged/Heavy choices. `SingleAssetPerShape`, веса, seed и распределение не менять без свидетельства ошибки. Сохранить результат и удалить диагностику. Этот подпункт не представлять как уже готовое исправление распределения.

### Best Practices

Единственный владелец правила количества — рецепт, а сохранённый результат admission отделён от предварительной оценки. Это применение DRY и encapsulation: UI, планировщик и исполнение не должны рассчитывать одну партию тремя способами. [Microsoft architectural principles](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles).

Принятие запроса и завершение работы имеют разные состояния; корреляция идёт по реальному OrderId. Такой контракт позволяет инициатору обслуживать независимые операции, не присваивая себе их исполнение. Здесь принцип asynchronous request/reply реализуется существующими endpoint records и EventBus, без HTTP, polling, брокера или нового dispatcher. [Microsoft asynchronous request-reply](https://learn.microsoft.com/en-us/azure/architecture/patterns/asynchronous-request-reply).

### 9. Порядок и приёмка

1. Реализовать явный producer context и ProducerResource, переключить все перечисленные оценки и наблюдения.
2. Добавить authored-привязку одного цикла и scoped режим существующего ProductionYield, подключить completion/cancel/cleanup к нынешнему endpoint.
3. Явно обновить шесть рецептов, три цели, метрики активации и PlayerBrain; удалить солдатские agents и неиспользуемый контент.
4. Добавить ограниченную диагностику выбора карточек; обновить документацию и необходимые fixtures. Существующие GUID сохранять, новые .meta создаёт Unity. Автоматических миграций и authored строк не добавлять.
5. После полного завершения реализации сообщить готовность. Тесты и игровые прогоны выполнять только по отдельному разрешению пользователя. Остановку оставленного Play Mode также согласовывать отдельно; этот документ разрешением не является.

После разрешения проверить:

- Level 0 не запускает выпуск; первый апгрейд активирует его без запроса создания исполнителя.
- RunLevel 1–5 даёт 2–6 отдельных солдат одним циклом; уровни соседнего здания и игрока не подменяют уровень производителя.
- Изменение уровня до admission отражается в принятом заказе, после admission — только в следующей партии.
- Постоянный уровень 3 переключает следующий запуск на улучшенный рецепт; принятую партию не меняет.
- Все три здания работают независимо, включая заполненную площадку одного из них; cooldown и места не смешиваются.
- Начатая партия не завершается по первому солдату; отказ admission, отмена и компенсация не публикуют успех.
- Чужой заказ, прежний scope и поздний ответ после Reset не завершают новую цель; нет повторной отправки принятого заказа.
- Preview, Orchestration, Knowledge и UI показывают актуальный размер; без изменений входов нет новых чтений кошельков.
- Карточки, элитный выпуск, строительство и улучшения, Board/Autobattle, Distance и OrchestrationSandbox сохраняют свои контракты.
- По диагностике установлен фактический состав choices карточек; отдельная причина распределения не объявлена без свидетельства.

Результат этапа: размер партии задаётся один раз в рецепте; существующий Production исполняет обычный заказ, а Orchestration запускает следующий цикл и получает адресный результат без солдатских агентов и изменений Automatic.


### Выполнено по плану выпуска партий — 2026-10-11

- ProductionAttachRequest и ProductionSnapshot содержат явный ProducerEconomyOwner. CapabilityHost передаёт владельца своего экземпляра. Все вызовы общего resolver, включая предварительную оценку дропа, получают точный snapshot производителя. Технический ProductionOwner и ControllerOwner не подменяют его.
- ProducerResource читает выбранный уровень производителя. Production наблюдает selectors активного каталога с дедупликацией адресов, помечает регистрацию для обновления и публикует RecipeProgressionVersion существующим ProductionStateChangedEvent. При смене каталога, detach и ошибке подготовки watches освобождаются; callbacks проверяют текущую регистрацию и каталог. Orchestration учитывает версию без повторного чтения ProducerResource при построении ключа кеша. UI использует событие Production, Knowledge — тот же resolver.
- ProductionYield поддерживает TotalYields и CurrentActivation. Новая привязка задаёт Recipe/Recipient, а scope входит в identity и planning payload. Endpoint проверяет точную выбранную Entity, создаёт один обычный заказ, хранит scope рядом с OrderId и публикует успех только после завершения всех outputs при действующем поколении Objective. Отмена использует существующий путь заказа и компенсации; старый scope не получает успеха.
- Шесть рецептов солдат настроены на Prototype своего производителя, RunLevel 1–5 и линейное количество 2–6. Catalog reservation остаётся OnEnqueue, cooldown и площадки выпуска сохранены. Automatic и обработка Agent не изменены.
- Три Objective имеют материализацию и Analytics CurrentAmount >= 1 в условиях активации. Две ветки выбирают обычный или улучшенный рецепт по PermanentLevel реализации. Ветки исключают одновременную активацию друг друга, root завершается по Or и повторяется штатным Reset. Три новые метрики подключены в SurvivePlayerAnalytics. Для идентичностей веток переиспользованы существующие recipe.Id; обычные внешние факты сохраняют прежние ключи release, улучшенные используют существующие recipe.Id. Новых строковых полей или контрактов нет.
- PlayerBrain использует один прежний ProductionYield operator; его decision допускает глобальные цели и прежние локальные зависимости. Шесть солдатских Agent definitions и шесть ставших неиспользуемыми operator terms удалены через Unity после проверки ссылок. Taxonomy installer обновлён. Placement, upgrade, Board и остальные agents сохранены.
- В PopulationContentSelectionSession добавлена согласованная временная диагностика: до 16 предупреждений на заполнение с доступными choices, выбранной записью, source и seed. Распределение карточек не изменено; его причина всё ещё не установлена. После разрешённого ручного запуска следует сохранить свидетельства и удалить диагностику.
- Attach fixtures обновлены; добавлены проверки точного Prototype-источника и версии прогрессии, а также различия scoped фактов между активациями. В существующем GameplayFoundation fixture создание WalletEntry переведено на нынешний конструктор — runtime API не менялся.

**Проверено без запуска игры и тестов:** Unity ChainRush завершил компиляцию runtime без ошибок; вся существующая EditMode assembly с обновлёнными fixtures и новым файлом скомпилирована отдельно без выполнения тестовых методов. Сохранённые C#-поля трёх целей, шести рецептов, marker providers, reservation policies и external bindings прочитаны из редактора. В PlayerBrain один Yield operator; ссылки на удалённые солдатские agents отсутствуют. Новые .meta созданы Unity. Play Mode остановлен с отдельного разрешения пользователя и повторно не включался.

Приёмка из раздела 9 ожидает отдельного разрешения. Совпадение игрового поведения, обработка ошибок/компенсации в новом сценарии и распределение карточек ещё не подтверждены игровым запуском.

### Исправление отказа запуска на OR дочерних целей — 2026-10-11

**Implementation decision: working model. Реализация исправления завершена; запуск уровня и тесты не выполнены.**

Симптом пользователя: «не загружается уровень». Play Mode был включён. Пользователь отдельно разрешил его остановить для исправления; повторного запуска не было.

**Verified.** В `/Users/ionrain/Library/Logs/Unity/ChainRush.log`, строки 82270–82356, ActivityLauncher возвращает InvalidDefinition при проверке InfantrySoldierRelease: `A composite orchestration requirement needs a fact projector for every child: ObjectiveConditionTargetNodesState`. GameFlow после этого завершает launch-autobattle и контейнер survive с ошибкой. В редакторе прочитаны три настоящих графа Infantry/Ranged/Heavy: каждый содержит OR двух ссылок на дочерние цели; обе ветки и ссылки на рецепты доступны. Потеря ссылок этих графов не объясняет данный отказ. Ограничение находится одновременно в проверке и сборе требований ObjectiveOrchestrationFactContainer, до открытия Activity.

**Existing System Fit.** ActivityOrchestrationConfigData вызывает проверку шаблонов через OrchestrationService. ObjectiveGraphTraversal и ObjectiveRuntimeGraphBuilder владеют обходом и клонированием графа; ObjectiveConditionPipeline и ObjectiveCompositePlugin — проверкой AND/OR и состояний узлов. ObjectiveOrchestrationFactContainer извлекает требования активных узлов, OrchestrationFactExtractor сохраняет их provenance, а нынешние слушатели событий состояния/Reset в OrchestrationService запускают обновление. Изменён только контейнер извлечения требований; исполнение Objectives, Activity, Production и authored assets сохранены.

- Общая функция IsNodeStateGroup распознаёт AND/OR, состоящие только из ObjectiveConditionTargetNodesState и вложенных таких же групп. Её используют и проверка шаблона, и извлечение требований.
- Такая группа описывает завершение графа. Orchestration обходит её ссылки существующим путём и извлекает требования только из Active узлов. Родителю не создаётся отдельное требование Composite; неактивной альтернативе заказ не назначается. Идентификаторы условий и scope берутся у исходных дочерних узлов.
- Общий visitedNodeIds исключает повторный сбор одного узла; для группы не создаются дополнительные списки планируемых фактов. Дополнительного polling, чтения кошельков, подписок и authored настроек нет.
- AND/OR над планируемыми фактами сохраняют прежнюю обработку. Смешивание в одной группе ссылок на узлы и экономических требований остаётся явно отклоняемым: объединять их в один планируемый факт без отдельного контракта нельзя.
- В ObjectiveServiceEditModeTests добавлены случаи AND/OR, вложенной группы, повторной ссылки, разных состояний веток и сохранения идентичности исходной цели. Отдельно описан отказ смешанной группы. Тесты не запускались.

**Best Practices.** Разделение управления графом и требований исполнения сохраняет единственного владельца каждой семантики: ObjectiveConditionComposite.Check вычисляет состояние графа, а CollectFromNode читает Active-состояние и формирует требования конкретного узла. Повторное использование этого обхода сохраняет существующие activation scope, обработку событий и дедупликацию вместо введения второго планировщика дочерних целей. Источники: `Scripts/Core/Objectives/Conditions/ObjectiveConditionComposite.cs`, `Scripts/Core/Objectives/Orchestration/ObjectiveOrchestrationFactContainer.cs`, `Scripts/Core/Orchestration/Facts/OrchestrationFactExtractor.cs` в MorbooFrameworkPackage.

**Проверено:** Unity ChainRush завершил компиляцию runtime без ошибок; вся EditMode assembly с изменёнными fixtures скомпилирована отдельно без выполнения тестов. Проверка пробелов изменений C#/документации прошла. **Inference:** устранена подтверждённая ветка отказа, но успешный запуск уровня и повторный выпуск партий требуют нового разрешённого запуска; эти результаты пока не заявляются.

### Возврат в Main при отсутствии закрытого здания — 2026-10-12

Симптом пользователя: «Теперь уровень не загружается а сразу возвращается на главный экран после нажатия на кнопку старта».

**Verified.** Новые запуски проходят launch-autobattle, но await-autobattle-ready завершается ошибкой. В сохранённых ActivityLifecycleRecord активного редактора обе Autobattle (2 и 4) имеют Ready=Fail и сообщение `Economy owner entry path step 0 requires exactly one positive entry whose runtime asset owns wallets.` В закрытых Activity сохранено по 31 успешно зарегистрированной цели: Infantry/Ranged SoldierRelease присутствуют, за HeavyRunUpgrade следующая HeavySoldierRelease не зарегистрирована. Точный authored selector её проверки PermanentLevel возвращает игроку 0 записей HeavyBuilding; InfantryBuilding и RangedBuilding возвращают по одной положительной записи-владельцу. Закрытое тяжёлое здание действительно отсутствует. Ссылки трёх графов доступны; прежний отказ OR отсутствует.

Путь отказа: InitializeRuntimeContentAsync → RegisterTeamObjectives → ObjectiveService.AddObjective → ObjectiveConditionPipeline.BuildIndexes → ObjectiveEconomyMetricPlugin.Index → ResolveObservedOwner → EconomyOperationOwnerBindingData.Resolve. Индексация выполняется для всех условий графа до проверки условий активации. Поэтому отсутствие ещё не открытого здания ошибочно делает невозможной регистрацию всей Activity. Сам строгий Resolve корректен для экономической команды; его немедленное использование для наблюдения здесь недостаточно. Изменение отсутствующей записи в нулевой PermanentLevel также некорректно: проверка `<3` стала бы истинной без владельца.

### Отложенная подготовка зависимостей Objectives — 2026-10-12

**Implementation decision: working model.** Утверждённое исправление заменяет прежнее предложение наблюдать отсутствующего владельца. EconomyOwnerBindingObserver не вводится. Строгий Resolve, ActivityServiceCore.RegisterTeamObjectives, Production и прототипы сохраняют свои контракты.

**Existing System Fit.** Activity регистрирует все статические шаблоны. ObjectiveConditionPipeline определяет порядок проверки и срок жизни достигнутых зависимостей. ObjectiveRuntimeProcessor готовит Success/Fail до публикации Active. Plugins владеют общими Economy watches, Analytics reads и регистрациями операций; ObjectiveNumericTargets — источниками порогов. События материализации, триггеров и результатов по-прежнему будят существующую очередь Objectives. Orchestration получает полные требования активного узла.

Изменения реализации:

- BuildIndexes выполняет статические проверки. Подключение метрик, порогов и регистрация экономических операций происходят при достижении условия либо подготовке требований активируемого узла.
- Списки условий — последовательный AND. Вложенные AND/OR обходятся в authored-порядке; после определившего результат дочернего условия остальные проверки не вызываются. Результаты берутся у runtime plugins, без повторного обхода дочерних Check.
- У неактивного узла сохраняются только достигнутые зависимости. При ложной предыдущей предпосылке зависимое чтение освобождается, его значение становится недоступным. Одинаковые метрики внутри runtime разделяют один snapshot/watch по разрешённому ключу.
- Success/Fail готовятся полностью до Active, включая обе альтернативы OR. Ошибка освобождает частичную подготовку, переход Active не публикуется. Pending числового порога остаётся недоступным значением в snapshot.
- Trigger, SkillResult, ExternalFact и прочие накопительные события сохраняют прежние временные границы. Подготовленный handle Economy не исполняется автоматически и сохраняется при изменении результата логической проверки; Reset/удаление закрывают его существующим путём.
- Удалены неиспользуемые ObjectiveConditionIndexer, IObjectiveConditionIndexer и ObjectiveEconomyConditionHandler. Другой путь подготовки не сохраняется.

**Authoring ChainRush.** Через Unity сохранены InfantrySoldierRelease, RangedSoldierRelease и HeavySoldierRelease с прежними GUID. У корня и обеих ветвей первая проверка — положительная Stack-запись соответствующего здания в BuildingCatalog с Selected. Далее идут существующие MaterializedEntity и RunLevel ≥ 1. Ветви затем проверяют PermanentLevel (< 3 / ≥ 3) и отсутствие исполнения противоположной ветви. Условия создаются отдельно для каждого узла, наследование родительских предпосылок не подразумевается.

Нет выбранной записи → Inactive и watch участника → запись появилась/выбрана → ожидание экземпляра → ожидание RunLevel ≥ 1 → чтение постоянного уровня → нужная ветвь выпуска. Само наличие экземпляра без Selected не разрешает выпуск. Настройки рецептов, партий, cooldown, размещения и завершения принятых заказов этим шагом не меняются.

**Best Practices.** Короткое вычисление AND/OR предотвращает обращение к зависимым данным после уже определённого результата. Оно соответствует [семантике условных операторов C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/boolean-logical-operators). Lifecycle подписок на накопительные события отделён от lifecycle чтений, иначе пропущенная проверка теряла бы ранее поступивший результат. Полная подготовка активных требований отдельно от вычисления их истинности сохраняет все варианты плана Orchestration.

**Приёмка после отдельного разрешения.** Проверить отсутствующее/невыбранное здание, появление записи и экземпляра во время уровня, уровень 0→1, вложенные AND/OR, освобождение/повторное подключение чтений, неоднозначный достигнутый адрес, ранние события, полные OR-требования до Active, единственный handle, Reset и закрытие Activity. Fixtures обновляются в package; они общие для ChainRush и MorbooFramework. Тесты и игровые прогоны этим шагом не запускаются.

Подробнее о контракте: MorbooFrameworkPackage/Documentation~/Objectives.md.

**Завершение реализации.** Код и три графа обновлены. Повторное чтение Unity после reload подтвердило порядок условий всех девяти узлов; GUID сохранены. Общий AttachRuntime обеспечивает одинаковый cleanup ошибки при первой регистрации и Reset. Неиспользуемые классы удалены вместе с их Unity metadata; metadata новых файлов созданы импортом Unity.

Компиляция ChainRush завершена: `isPlaying=false`, `isCompiling=false`, `scriptCompilationFailed=false`; новый внутренний binding-контракт загружен. Общие EditMode fixtures package скомпилированы отдельно против актуального Core.dll, compiler exit 0. Тесты не исполнялись, Play Mode не запускался. Успешный запуск уровня и игровой выпуск после исправления ещё не проверены; приёмка требует отдельного разрешения.

## Нет карточек зданий и заготовок — наблюдение 2026-10-12

**Сообщённый симптом (буквально):** «Теперь не появляются карточки зданий на доске и не уверен, что места под здания материализуются».

**Verified.** Только ChainRush, Play Mode включён и на паузе. Autobattle 2 и Board 3 Running. Герой, враги, элитные юниты и обычные карточки существуют. В CapabilityHost runtime нет экземпляров зданий; в assignment board нет заданий их размещения. Три объекта мест BuildingSite1/2/3 активны в пространстве уровня на (7,0,8), (7,0,15), (7,0,22).

У текущего игрока player:FrameworkPrototypesProfile есть InfantryBuilding и RangedBuilding по одной Stack-записи с Available/Entitled, без Selected. HeavyBuilding отсутствует. Runtime-условия активации InfantryPlacement и RangedPlacement имеют HasValue=true, CurrentValue=0, TargetValue=1, RequiredRuntimeTags=[Selected]; оба узла Inactive. Такие же первые проверки выпуска солдат возвращают наблюдаемое количество 0. BuildingRunEnabled=1, поэтому отсутствие режима зданий не объясняет этот снимок.

**Проверенный путь.** Placement Objective ожидает выбранную запись и ещё не публиковал Active; Orchestration не создала assignment и заказ заготовки. BoardPopulation использует PopulationContentRuleData с participantEntries; PopulationContentAvailabilitySession исключает группу при hasContent=false. В существующей трассе последнего заполнения Board перечислены Gold, Water/Cola, buffs, Heal, LightningBolt; строительных карточек нет.

**Разрешено пользователем.** Пользователь уточнил: «Отбой здания не выбраны, но есть другая проблема: юниты не выпускаются». Отсутствие заготовок и карточек в этом наблюдении не требует исправления. Автоматически выбирать здания или подставлять Selected не нужно. Код, assets и состояние игры не менялись; тесты и игровые прогоны не запускались.

Снимки: /tmp/chainrush-buildings-20261012/current.json и gates-sites.json. Прочитан только /Users/ionrain/Library/Logs/Unity/ChainRush.log; редактор во всех обращениях выбран явно по проекту.


## Солдаты построенных зданий и выбор карточек — 2026-10-12

**Сообщённые симптомы (буквально).** «Солдаты из построенных зданий» не выпускаются. «Кроме того, карточки появляются только из здания Water. Cola н появляется, не знаю, может вероятность не выпала, но тем не менее».

**Verified.** Чтение только текущего ChainRush, `/Users/ionrain/ChainRush/Assets`; Play Mode оставлен включённым на паузе. Infantry instance 277 имеет InfantryRunLevel=1, InfantryProduction и InfantryCatalog. Ranged instance 485 ещё не имеет RunLevel/Production. У Infantry производитель принимает заказы, очередь и pipelines пусты. Условия Selected и MaterializedEntity всех трёх узлов InfantrySoldierRelease имеют 1 при пороге 1. Узлы остаются Inactive: следующее условие ActivityAnalytics не имеет текущего значения; существующий read возвращает Pending с Reason «Analytics sources changed.».

Для Infantry/Ranged/Heavy ProductionRunLevel расчёты имеют SourceRevision=10514, ComputedRevision=177, ReadingRevision=10514, NeedsWork=true. RecomputeCount=31; последний опубликованный sample — 1.066667 секунды. Текущее время по ElapsedSimulationMetric — 63.6327057. Бюджет SurvivePlayerAnalytics=256, ReviewInterval=1, последний расход=256; каждая из трёх выборок дошла до записи 85 из 216. История до 1.066667 ещё не содержала CurrentAmount. Эти данные доказывают незавершённое чтение в достигнутом условии, но не определяют конкретное уведомление, которое заставляет его перечитывать источники.

**Проверенный путь / Existing System Fit.** ObjectiveRuntimeProcessor проверяет активацию через ObjectiveConditionPipeline; ObjectiveActivityAnalyticsPlugin держит existing ActivityAnalyticsRead и помечает runtime dirty по его уведомлениям. ActivityAnalyticsDomainState распределяет общий бюджет; ActivityAnalyticsCalculation.Advance перезапускает курсор при смене SourceRevision, Publish сообщает готовое значение. KnowledgeBroker синхронно передаёт invalidation из существующих source handlers. При удалении графа plugin освобождает read; закрытие Analytics освобождает calculations/cursors. Production/Orchestration пока не получают Active для выпуска Infantry. Недостаточный RunLevel, отсутствие здания и занятый производитель не объясняют этот конкретный отказ.

**Inference.** Вероятно, частые invalidation мешают бюджетному расчёту завершить обход. Причина конкретных invalidation пока не установлена; отдельная правка Analytics, увеличение бюджета или замена условия другим чтением не согласованы и не выполнялись.

**Карточки — Verified.** Существующая трасса текущего запуска (ChainRush.log:98603, 99454, 100207) содержит три разных seed: 966011030619920429, 965052256480309662, 964097880387211739. Во всех трёх первых выборах группы Allowed содержит InfantryBoardCard и RangedBoardCard; выбран Infantry. SingleAssetPerShape=true в текущем BoardPopulationAgent. PopulationContentSelectionSession использует один выбор для остальных клеток фигуры, поэтому количество карточек не равно количеству независимых выборов. Отсутствие Ranged среди кандидатов исключено. Эти три результата сами по себе не доказывают дефект случайного распределения; настройки не менялись.

**Следующий диагностический шаг, ожидает согласования.** В текущем Editor временно подключить ограниченный наблюдатель к уже существующему KnowledgeBroker.Invalidated через Editor eval, без изменения файлов runtime/assets. Сохранить первые 32 уведомления, которые меняют revision выбранного расчёта InfantryProductionRunLevel: тип/facet, Entity, owner, asset, broad, source/computed/reading revisions и положение курсора. Данные остаются только диагностическими; обработчик не вызывает пересчёт и не изменяет результат. Пользователь вручную снимает паузу на несколько секунд и возвращает её; затем наблюдатель снимается и свидетельства сохраняются. При остановке Play Mode диагностическая подписка также снимается. Не запускать игру, тесты или ручные шаги Simulation. После установления исполняемой причины сформулировать конкретное исправление.

**Best Practices.** Используется уже существующее уведомление об изменении, без второго пути расчёта, опроса игровых объектов или изменения бюджета. Ограниченная запись сохраняет фактический порядок invalidation; это необходимо, чтобы отличить нехватку бюджета, сброс по нерелевантным данным и проблему публикации. Точные владельцы: KnowledgeBroker.NotifyChanged, ActivityAnalyticsDomainState.MarkDirty/Tick, ActivityAnalyticsCalculation.Invalidate/Advance/Publish.

Свидетельства: `/tmp/chainrush-buildings-20261012/soldier-gates.json`, `analytics-details.json`; исходный лог — `/Users/ionrain/Library/Logs/Unity/ChainRush.log`. Ни код, ни игровые настройки не изменены. Тесты и игровые прогоны не запускались.


### Результат согласованной записи и исправление условия выпуска — 2026-10-12

**Verified.** Пользователь разрешил запись и вручную продолжил игру. В первые 32 уведомления, изменившие revision InfantryProductionRunLevelMetric, вошли обезличенные Content/Resource (Entity/Owner/Asset не указаны) и Content/HostValue героя 279. SourceRevision вырос с 10514 до 10546 за кадры 6175–6181. ReadingRevision несколько раз переходил на новую revision, курсор каждый раз снова оказывался на 85, ComputedRevision остался 177. После пользовательского интервала SourceRevision=11190, ComputedRevision=177, RecomputeCount=31, курсор 85 из 239. Это подтверждает повторный старт бюджетного обхода до завершения. Общая метрика уровня не получает опубликованного результата и удерживает цель Inactive. Наблюдатель снялся после 32 записей; отдельное чтение подтвердило observerRemoved=true. Play Mode остался на паузе.

Ресурсные уведомления без адреса нельзя по этой записи привязать к конкретному кошельку. Нельзя считать, что все они вызваны движением, или объявлять установленной точную исходную Economy-операцию. Проблема слишком широкого сброса Analytics реальна и остаётся отдельной неисправностью, не исправленной предлагаемой настройкой цели.

**Verified, доступность штатного чтения.** Из фактических записей InfantryBuilding и RangedBuilding выбранного runtime-игрока существующий EconomyOwnerLevelResolver однозначно разрешает Prototype в domain 2. В прототипе InfantryRunLevel=1; в Ranged уровень ещё не выдан. Никаких Entity, прототипов или операций этим чтением не создавалось. Существующие EconomyOperationOwnerBindingData и ObjectiveConditionEconomyMetric поддерживают этот адрес; plugin объединяет одинаковые чтения runtime и наблюдает точный selector через Economy.Watch. Отложенная подготовка уже гарантирует, что чтение подключается после Selected и MaterializedEntity.

**Согласовано и реализовано. Implementation decision: working model.** Условие запуска солдат проверяет экономическое состояние выбранного здания у владельца его развития. Для этого настроить существующее ObjectiveConditionEconomyMetric вместо ActivityAnalytics в трёх графах InfantrySoldierRelease/RangedSoldierRelease/HeavySoldierRelease, по три узла в каждом:

1. Сохранить текущие проверки Selected и MaterializedEntity первыми.
2. Следующая проверка: ContextOwner участника → существующий selector точной записи соответствующего BuildingCatalog с Selected → OwnerLevel=Prototype в текущем domain → кошелёк с тегом BuildingParameters → точный Infantry/Ranged/HeavyRunLevel, Stack, >=1. В каждом условии собственный inline binding/selector; не разделять SerializeReference identity между authored списками. Использовать фактическую существующую taxonomy кошелька, сверив её с рецептом улучшения.
3. Сохранить затем PermanentLevel реализации и взаимное исключение ветвей; Success/Fail/Reset, release bindings, recipes, batch amounts, cooldown, placement не менять.
4. Удалить три ProductionRunLevelMetric из SurvivePlayerAnalytics; поиск ссылок показал, что больше их читают только указанные три графа. После замены удалить эти три неиспользуемых asset через Unity. ElapsedSimulationMetric оставить.
5. Удалить согласованную временную запись PopulationContentSelectionSession: свидетельство обоих доступных вариантов сохранено, алгоритм и SingleAssetPerShape не менять.
6. Сохранить authoring через Unity с прежними GUID изменяемых assets, перечитать все девять условий и остальные поля графов. Проверить компиляцию после удаления диагностического кода при выключенном Play Mode. Игру и тесты не запускать.

**Existing System Fit.** Economy владеет уровнем и уведомлением о его изменении, CapabilityHosts — разрешением Prototype в Activity. Objectives владеют lifecycle чтения и публикуют Active; Orchestration/Production получают то же требование после RunLevel>=1. Используются ObjectiveEconomyMetricPlugin, ObjectiveMetricBindingKey, Economy.Watch и существующий owner binding. Изменение алгоритма Analytics/Knowledge, новые агенты, условия, таймеры и исполнители не входят в этот шаг.

**Best Practices.** Для проверки собственного конкретного состояния используется его авторитетный владелец и адресная подписка, уже реализованные в ObjectiveEconomyMetricPlugin.PrepareBinding/CreateWatcher; новый кеш/вычислитель не создаётся. Одинаковые условия одного runtime используют один binding, смена предпосылок освобождает его. Сводная аналитика сохраняется для её остальных потребителей. Устранение общей проблемы invalidation требует отдельного механизма зависимостей и не объявляется выполненным этим изменением authoring.

Свидетельства: `/tmp/chainrush-buildings-20261012/analytics-trace.json`, `analytics-after-trace.json`, `prototype-level-read.json`.

**Реализация завершена.** Пользователь согласовал весь шаг и остановку Play Mode. В трёх шаблонах через Unity заменены все девять условий уровня на EconomyMetric с независимыми inline bindings/selectors. В SurvivePlayerAnalytics оставлена ElapsedSimulationMetric; три неиспользуемых ProductionRunLevelMetric удалены через AssetDatabase вместе с принадлежащими им meta. Четыре изменённых asset сохранили исходные meta/GUID. Из PopulationContentSelectionSession удалены только временный счётчик и предупреждение; выбор и распределение не менялись.

**Проверено без игровых прогонов и тестов.** Сериализуемый граф каждого шаблона до/после сравнен целиком с исключением ровно заменённых условий; остальные поля, порядок, ссылки, Success/Fail/Reset совпали. После компиляции и перезагрузки сборок Unity выполнено повторное чтение всех девяти сохранённых условий, их selectors, Prototype, wallet tag BuildingParameters и порога >=1; проверены отсутствие разделяемых inline bindings и единственная оставшаяся метрика. Ссылок на удалённые метрики в Assets не осталось. Unity: playing=false, compiling=false, scriptCompilationFailed=false; компилятор завершился с ExitCode=0. Успешный выпуск солдат в игре пока не заявляется. Общая неисправность invalidation Analytics остаётся зафиксированной выше и этим authoring-исправлением не устранена.

Результаты сохранения/проверки: `authoring-result.json`, `authoring-readback.json` в `/tmp/chainrush-buildings-20261012/`; исходные семь assets сохранены в `before-level-fix`. Play Mode и тесты самостоятельно не запускались.
