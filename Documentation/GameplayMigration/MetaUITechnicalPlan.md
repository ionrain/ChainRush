# Минимальная мета ChainRush: техническое предложение

**Уточнение от 2026-09-29:** текущие границы UI, исполнение покупки, числовое чтение и списки заменены утверждённым рефакторингом [UISeparationImplementation.md](UISeparationImplementation.md). Ниже сохранена история переноса меты. Упоминания InstallUIAdapter, EconomyNumericQueryData, EconomyAmountUIData/списков и UIHandle в общем GameFlow не описывают текущую реализацию. Тесты нового рефакторинга ожидают отдельного разрешения.

**Дата:** 2026-09-26. **Implementation decision: working model.** Пользователь разрешил реализацию плана с последними поправками к ListView, общему источнику числовых данных и фильтру GameFlow. Документ описывает утверждённый объём, а не готовый код. Продуктовый объём — [MetaUIMigrationProposal.md](MetaUIMigrationProposal.md). При расхождении с ранними техническими предположениями использовать действия, перечисленные здесь. Неописанные расширения требуют отдельного согласования. Тесты не разрешены.

Проверены исходники runtime, UI, сериализованные конфигурации и относящиеся к ним исходники тестов. Unity используется для импорта и явных authoring-команд. Игровые сценарии, сборка и тесты не запускались. Verified в документе означает установленный чтением контракт, а не воспроизведённый игровой сбой.

## 1. Existing System Fit

| Область | Авторитетный владелец | Действие |
|---|---|---|
| Постоянная запись персонажа и её кошельки | EconomyRuntimeOwnerAssetResolver, EconomyRuntimeAssetCopyFactory, EconomySeededOwnerKeyFactory | Оставить создание runtime-копии владельца при выдаче Stack. Ключ зависит от родителя и определения, а не от Selected |
| Кошельки материализованного объекта | CapabilityHostSkillController, EconomyRuntimeWalletBinder, EconomyRuntimeWalletGraph | Оставить локальный runtime объекта и cleanup. Не использовать этот graph как хранилище меты |
| Выбор записей | EconomyEntrySelectionData/Resolver, EconomySelectionQuery | Переиспользовать фильтры и точные handles для UI, выбранного состава и разрешения вложенного владельца |
| Покупка | ProductionService, ProductionRuntime, Recipe/Catalog/Data | Оставить очередь, reservation, work, outputs, compensation. Подключить ресурсный аргумент прогрессии и посредник UI |
| Постоянное состояние | PlayerService, EconomyOwnerStateTransfer, EconomyStorageBridge, Storage | Оставить существующий seed/restore и рекурсивную запись владельцев; подключить подтверждения меты и pause |
| Выбор уровня и проверка состава | GameFlowServiceCore; Activity entry rules; Economy | Оставить две конфигурации уровня. GameFlow выбирает уровень, Economy хранит Selected персонажей; конкретный герой не выбирает ветку GameFlow |
| Цель уровня и запись прогресса | Настройки Activity; Analytics, Objectives и Orchestration с существующими Economy operators | Survive/Distance задаются уровнем. Общая метрика наблюдает Entity героя участника; цель не хранится в определении выбранного персонажа |
| Начальная материализация героя | Activity seed; Objectives/Orchestration и Population; ProductionRuntime; ActivityProductionOutputMaterializationModule | Seed создаёт фиксированного host; Population выбирает героя из Selected-записи; Production выпускает Token; Activity материализует и подтверждает появление. Authoring — §2.5 |
| Содержимое доски | PopulationContentRuleData и существующие источники каталога | Читать содержимое кошельков выбранных записей вместо второго Loadout |
| Окна и их уничтожение | UIFlowServiceCore, UIPresentationController, UIHandle | Оставить создание, контекст, lifecycle и manual release; перенести визуальный Popup<T> |
| Данные и показ награды | Обработчик источника; RewardServiceCore/RewardRecordSnapshot для выдачи Rewards; UIFlow/Popup<T>/RewardPopup | Окно принимает готовые данные. Delivered проверяет только обработчик источника Rewards; прямой показ не требует записи реестра. Окно не исполняет выдачу |
| Прогресс входа в игру | ActivitySpacePreparationProgressEvent, ActivityReadyEvent, ActivityPreparationFailedEvent; GameFlow/UIFlow | Loading показывает подготовку Activity и завершает переход по Ready. Ожидание героя отложено пользователем; второго загрузчика не создавать |

За пределами предложения: старые игровые менеджеры, новый CharacterManager, универсальная операция меты в EconomyCostDefinition, snapshot Loadout, ActivityLaunchInput, новые виды игровых целей, автоматическая миграция сохранений, финальный lifecycle забега, tutorial, ConfirmationPopup, магазин и энергия.

## 2. Постоянные кошельки, Selected и бой

### 2.1. Структура данных и стабильная идентичность

**Оставить существующие определения CapabilityHostData героя/базового юнита владельцами кошельков.** Не вводить отдельный класс CharacterData и не создавать невидимый Entity ради хранения уровня.

В постоянном каталоге игрока хранить по одному Stack определений Perfume, Tabasco, Water и Cola. Эти записи существуют и у закрытого контента. При Issue Stack существующий EconomyRuntimeOwnerAssetResolver создаёт собственную runtime-копию определения и кошельков для данного игрока. Runtime tags права, доступности и Selected относятся к записи каталога. Изменение тегов не перевыдаёт Stack и не заменяет владельца.

У каждого персонажа задать:

- кошелёк уровня: Level как Resource, внутренние значения 0..99;
- кошельки развития: начальные значения и изменения атрибутов с существующей Precision; адрес каждой пары asset/qualifiers однозначен;
- кошелёк содержимого Board: ссылки на используемые BoardBase, включая LightningBoltBoardBase у Perfume; у Tabasco пустой набор категории HeroSkill сохраняет текущее отсутствие такого содержимого;
- существующие кошельки боевых Skills, AI, Movement и HostValues.

Монеты и четыре вида персональных карточек оставить в кошельках игрока. Персональная карточка задаётся отдельным Resource asset каждого персонажа; её отображение и расход берутся из inputs его рецепта. Поэтому все входы покупки принадлежат одному Payer. Результат принадлежит Recipient — runtime-владельцу выбранной записи. Разные владельцы для отдельных входов Production не добавляются.

**Verified:** EconomyServiceCore.TryResolveIssuedAsset различает Stack/Token; Stack проходит ResolveSeededAsset. Seeded key строится из ключа родителя и исходного владельца. EconomyStorageBridge.SaveAll/LoadAll обходит живые вложенные assets-владельцы. PlayerService переносит состояния через EconomyOwnerStateTransfer. Эти пути переиспользовать без изменения идентичности при выборе и без своего реестра персонажей.

### 2.2. Оставить существующее связывание кошельков CapabilityHost

**Удалить из предложения** All/ByTags, CapabilityHostWalletSelectionType и расширение CapabilityHostBaseData ради фильтра локальных кошельков. **Не менять для этого** CapabilityHostSkillController и EconomyRuntimeWalletBinder; не добавлять настройки и не переписывать fixtures под этот фильтр.

Предыдущее обоснование было ошибочным: оно предполагало, что BoardBase принесёт в героя вложенные Skills. Проверены все десять используемых *BoardBase.asset в Activities/Board/Economy: у них capabilities и walletEntries пусты. LightningBoltBoardBase также не содержит Skills. Рекурсивный обход EconomyRuntimeWalletGraph и сбор SkillEconomyModule существуют, но в этих данных им нечего добавлять из BoardBase. Запись Level как Resource сама по себе также не превращается в capability.

**Existing System Fit:** CapabilityHostSkillController.CreateWalletBinding передаёт существующие LocalWallets в EconomyRuntimeWalletBinder; общий RuntimeWalletGraph и Economy projection остаются владельцами обхода/сбора capabilities. ResolveIssuedTokenAsset использует исходное определение для нового Token; постоянное развитие подключается существующим CapabilityHostAttributeSourceData по §2.3. Новый фильтр только ради разделения названий кошельков не вводить.

**Best Practices:** не расширять общий контракт для устранения предполагаемого конфликта, которого нет в используемых данных. Конкретные источники: Activity BoardBase assets, EconomyRuntimeWalletGraph.RefreshWalletForm, SkillEconomyModule.Project и EconomyRuntimeOwnerAssetResolver.ResolveIssuedTokenAsset. Это статическая сверка исходников/данных, не результат запуска тестов.

### 2.3. Разрешение кошелька персонажа

**Расширить существующий `EconomyOperationOwnerBindingData` необязательным `List<EconomyEntrySelectionData> entryPath`.** Сначала разрешить существующий ContextOwner/AuthoredOwner, затем пройти selections. Каждый шаг должен вернуть ровно одну положительную запись, чей runtime Asset реализует IEconomyAssetOwner. Ноль или несколько совпадений — явный отказ; исходный authored asset и «первый найденный» не использовать как замену. Пустой путь означает существующее прямое связывание владельца.

Переиспользовать `EconomyEntrySelectionResolver.TryResolve/TryResolveExact`. Для источника атрибутов Water/его форм путь выбирает запись базового Water по exact asset, а не по Selected: уже появившийся юнит не теряет источник развития при смене UI-выбора. Для HeroSkill выбирается Hero + Available + Selected. В постоянном каталоге определения уникальны; Stack handle здесь однозначен. На другие кошельки с несколькими разными holdings одного asset это утверждение не распространять.

`CapabilityHostAttributeSourceData` уже использует этот binding; новый боевой исполнитель не требуется. При создании host захватывается владелец источника, существующие watches отслеживают изменения его атрибутов. На закрытии Activity существующий cleanup снимает watches; кошелёк меты не очищается.

### 2.4. Доска и запуск

**Изменить `PopulationContentRuleData`:** вместо специального чтения положительных entries одного Loadout разрешать два явно заданных пути: прямые entries участника либо содержимое кошельков выбранных entries-владельцев. Использовать существующие EconomyEntrySelectionData для внешнего выбора и внутреннего содержимого; отдельного языка фильтров не добавлять. Для внутреннего пути разрешать нескольких владельцев, объединять подходящие assets без дубликатов, затем пересекать с существующим Source. Замораживание состава на назначение и дальнейший lifecycle Population сохраняются.

Authoring Board: Unit + Available + Selected → BoardContentWallet выбранных Water/Cola; Hero + Available + Selected → BoardContentWallet выбранного героя. Read-only выбор ничего не выдаёт и не синхронизирует. Удалить фиксированный seed Loadout из сохраняемых конфигураций после подключения этого пути. Существующие cooldown, доли и форма расчёта Population сохраняются.

**Запуск — две конфигурации уровня: Survive и Distance.** Героя игрок выбирает в мете; Selected хранится на записи его каталога. GameFlow запускает выбранный уровень независимо от определения героя. В обоих уровнях материализация должна читать единственную доступную запись по Hero + Selected из кошелька участника. Добавление нового героя не требует нового Activity или новой ветки запуска уровня. Activity EntryRules уже умеют AssetTags/RuntimeTags: проверить одного доступного выбранного Hero и допустимое число доступных выбранных Unit перед запуском. Эта проверка состава сама по себе не выбирает объект для материализации.

**Удалить при реализации** зависимость игровых flow от exact asset Perfume/Tabasco и фиксированного героя в seed. **Оставить** AutobattleActivity и DistanceActivity как конфигурации двух уровней; после переноса ссылок **удалить** из нового пути Level01TabascoActivity, Level02TabascoActivity и соответствующие Level01TabascoFlow/Level02TabascoFlow. Исходные сцены и UI-префабы, сохранённые для сверки, этим шагом не изменять.

**Verified:** ActivityWalletSeedEntryData содержит конкретный SeedEntry; ActivityServiceCore.SeedMountedWallet выдаёт и материализует его Asset. Выбора по Selected в этом пути сейчас нет. EconomySelectionQuery умеет фильтровать записи по asset/runtime tags, а Activity EntryRules использует такое чтение для допуска. Seed не расширять: он создаёт фиксированного служебного host. Выбор и выпуск героя выполняет назначение по §2.5. ActivityLaunchInput и второй состав Loadout не добавлять.

**Изменить** `Scripts/Core/GameFlow/GameFlowData.cs` — добавить required runtime tags существующему GameFlowConditionEconomyMetric. Для выбора уровня задать Selected; exact asset уровня уже задаётся существующим условием. **Изменить** `Scripts/Core/GameFlow/Internal/GameFlowServiceCore.cs` — одинаково передавать этот фильтр при EvaluateEconomyCondition и построении watches. Asset-tag и excluded-фильтры не добавлять. Логика AND/OR и очередь GameFlow остаются прежними.

### 2.5. Seed исполнителя; выпуск выбранного героя через Orchestration

**Working model, вариант согласован пользователем:** Activity штатным seed создаёт фиксированного служебного исполнителя с Production. Выбранный герой появляется после seed через назначение Orchestration. «Материализатор героя» — роль настроенного CapabilityHost, а не новый C#-класс.

**Удалить из предложения** расширение ActivityWalletSeedEntryData режимами Authored / ParticipantEntry, новый ActivitySeedAssetSourceType и разрешение выбранного asset внутри SeedWallets/MountedWalletSeedWork. **Оставить** эти существующие seed-контракты без изменений. Динамический output asset в Production не добавлять: каталог содержит обычный рецепт каждого героя.

| Данные и существующий владелец | Конкретное действие |
|---|---|
| Seed AutobattleActivity и DistanceActivity | Убрать фиксированного Perfume/Tabasco. Выдать один и тот же служебный non-spatial CapabilityHost с Production по образцу WaterDeploymentHost. Постоянные Level/Selected остаются у каталога игрока |
| CapabilityHostData, ProductionData, ProductionCatalogData | Создать общий host и каталог начального выпуска героев; подключить штатными capabilities/кошельками. Поставщик начального размещения находится в пространстве уровня, не на ещё отсутствующем герое |
| ProductionRecipeData для Perfume и Tabasco | Для каждого героя задать рецепт: Require одной Stack-записи соответствующего героя в каталоге игрока, requiredRuntimeTags = Available + Selected; выход — один Token этого героя в runtime-кошелёк Activity. Consume для постоянной записи не использовать |
| Стартовое Objective | Настроить однократное требование появления одного Hero этого участника через ObjectiveConditionMaterializedEntity, без exact asset. После успеха не повторять и не реактивировать по смерти героя |
| Population Agent и Orchestration | Настроить объём 1, RequireFullVolume, каталог начального выпуска и выбор служебного производителя. Состав брать из прямых записей каталога участника: Stack, Hero + Available + Selected. Использовать общий selector из §2.4; отдельного selector или агента для героя не добавлять |
| ActivityProductionOutputMaterializationModule, Spatial, CapabilityHost, Projection | Переиспользовать размещение выпущенного Token, подтверждение появления, ожидание места и cleanup. Production отвечает за выпуск; фактическая материализация остаётся у Activity |

**Порядок:** EntryRules проверяют ровно одного доступного выбранного героя → seed создаёт host → стартовое Objective поступает в Orchestration → Population фиксирует выбранное содержимое и заказывает соответствующий рецепт → Production проверяет Require и выпускает Token → Activity подтверждает появление Entity → стартовое Objective завершается.

Постоянную Stack-запись, её теги и развитие не расходовать и не перемещать. Новый Token использует штатный EconomyRuntimeOwnerAssetResolver; развитие читается через §2.3. Замораживание состава и учёт existing + incoming оставить у Population. Нулевой/двойной выбор отклоняют EntryRules; отсутствие применимого рецепта не разрешает выпуск другого героя. Временная недоступность места или производителя остаётся ожиданием обычного назначения.

**Готовность и ошибки:** появление host и ActivityReady не означают появления героя. По последнему решению пользователя Loading завершает переход по ActivityReady; ожидание результата стартового Objective отложено (§4.5). Значение ActivityReady не менять, специальный флаг героя в Activity не добавлять. Ошибка seed идёт существующим путём подготовки Activity; ошибка последующего выпуска принадлежит назначению/Production и не становится ошибкой SeedWallets. Закрытие Activity и отмена запуска используют существующие отмену заказов, компенсацию и освобождение materialization-ресурсов. Позднее подтверждение прошлого запуска не завершает новый переход. Новый запуск создаёт новое стартовое Objective и нового host; постоянная мета сохраняется.

**Existing System Fit:** Activity владеет seed и lifetime host; Objectives — наблюдаемым результатом; Orchestration/Population — выбором и назначением; Production — входами, заказом и Token output; ActivityProductionOutputMaterializationModule — связью output с Entity и подтверждением появления; Spatial/Projection — размещением и представлением. GameFlow/UIFlow владеют переходом и Loading. Цель Survive/Distance и финальный lifecycle в исполнителя героя не переносить. Правила отсчёта времени Activity и отложенный автоматический маршрут этим вариантом не изменяются.

**Verified:** ProductionInputData уже содержит RequiredRuntimeTags; ProductionRuntime.TryReserveInputsCore обрабатывает Require без расхода Stack. PopulationAgentFactory поддерживает требование численности без exact asset; PopulationContentRuleData читает прямые записи участника, а фильтры Selected подключаются общей правкой §2.4. ActivityProductionOutputMaterializationModule слушает ProductionTokenOutputPreparedEvent и подтверждает результат через TryConfirmOutput. Это чтение кода; новый игровой authoring ещё не выполнен.

**Best Practices:** начальное появление использует тот же lifecycle выпуска, что последующие юниты: один владелец заказа, одно подтверждение появления и существующий cleanup. Конкретные образцы — Assets/Game/Activities/Autobattle/Economy/WaterDeploymentHost.asset и Production/WaterUnitDeploymentRecipe.asset; владельцы — ProductionRuntime и ActivityProductionOutputMaterializationModule. Для героя вход настроен как Require, поэтому постоянная запись сохраняется. Второй путь динамического выбора в Activity seed не создаётся.

**Authoring и ссылки:** создать один общий host, один каталог, два рецепта и общие настройки стартового Objective/Population; подключить к обоим уровням. Конкретные Perfume/Tabasco остаются в рецептах как определения контента; убрать их из выбора ветки GameFlow и seed героя. Обновить ссылки профиля и интеграционного сценария на удаляемые Tabasco-варианты. Начальные места появления брать из пространства уровня; после появления Distance использует поставщиков героя для последующего выпуска.

**Приёмка после разрешения тестов:** один и тот же level asset запускается с Perfume и Tabasco после изменения только Selected; нулевой/двойной выбор отклоняется; выпускается ровно один выбранный герой; Require сохраняет каталог и развитие; начальный выпуск ждёт доступного маркера в обычном pipeline; смерть не повторяет стартовый выпуск; отмена, ошибка и повторный запуск не оставляют старые заказы/Token/Entity и не принимают позднее подтверждение прошлого запуска. Проверки сейчас не выполняются.

### 2.6. Единый герой; цель и правила уровня в Activity

**Решение пользователя:** боевые свойства выбранного героя одинаковы в Survive и Distance. Убрать различия отдельных Distance-определений. Цель уровня задаётся настройками Activity через её Orchestration; Selected определяет героя. Варианты уровня на каждого героя, кошелёк боевых вариантов и дополнительную постоянную оболочку персонажа не вводить.

**Оставить существующую цепочку:** Activity подключает свои Objectives, Analytics и Orchestration. Для Survive метрика времени и требование записать прогресс время/300; для Distance — смещение/150. Общие операторы Economy исполняют требование; мета выбирает уровень и персонажей. Экран победы, остановка героя и полный lifecycle остаются вне минимальной меты.

**Конкретный authoring в ChainRush:**

| Assets | Действие |
|---|---|
| `Activities/Shared/Units/Perfume/Perfume.asset`, `Tabasco/Tabasco.asset` | Оставить единственными боевыми определениями этих героев. Один Selected и одно развитие персонажа используются в обоих уровнях. Исходные обычные определения — база для одинаковых боевых свойств |
| `PerfumeDistance.asset`, `TabascoDistance.asset` в тех же папках | Перепривязать ссылки к общим определениям и удалить. Не переносить из них GateHeroBody, отдельную идентичность персонажа или отличие HealableTarget |
| `Activities/Autobattle/Projection/Perfume.prefab`, `Tabasco.prefab` | Перенести дочерние Cells/Volume-поставщики из соответствующих Distance-prefabs с их текущими параметрами и отдельными тегами. Сохранить общий внешний вид и pool identity соответствующего героя |
| `Activities/Autobattle/Projection/PerfumeDistance.prefab`, `TabascoDistance.prefab` | После переноса поставщиков/ссылок удалить. Уровень не подменяет prefab персонажа |
| `Activities/Shared/Taxonomy/PerfumeDistanceDefinition.asset`, `TabascoDistanceDefinition.asset` | Заменить ссылки обычными definition tags и удалить. Источники развития, qualifiers и профили больше не различают режимы героя |
| `Activities/Autobattle/Space/GateHeroBody.asset`, `TabascoBody.asset` | Удалить после удаления последних ссылок Distance-вариантов, если других пользователей нет. Одинаковую геометрию брать из обычных общих определений; произвольные новые размеры не задавать |
| `PerfumeDisplacementMetric.asset`, `TabascoDisplacementMetric.asset` | Заменить одной HeroDisplacementMetric: scope участника, exactAsset пустой, ObjectTags содержит Hero, прежние ось и DisplacementAlongAxis. Analytics уже отклоняет несколько подходящих Entity |
| `PerfumeDistancePlayerAnalytics.asset`, `TabascoDistancePlayerAnalytics.asset` | Заменить общей DistancePlayerAnalytics; сохранить интервалы и бюджеты |
| `PerfumeDistanceProgressObjective.asset`, `TabascoDistanceProgressObjective.asset` | Заменить общим DistanceProgressObjective в настройках Distance Activity/Orchestration; сохранить прогрессию, шкалу 1 000 000 и существующие операторы записи |
| `Level01TabascoActivity.asset`, `Level02TabascoActivity.asset` и соответствующие flow | Удалить после перепривязки нового запуска и интеграционного сценария к двум общим уровням. Полный набор мест использования найти перед удалением, включая startup plans/installers и исходники fixtures |

Пути в таблице заданы относительно Assets/Game, кроме одиночных имён метрик/Analytics/Objectives: они остаются в соответствующих существующих подпапках Activities/Autobattle. Таблица задаёт явные authoring-действия, а не runtime-миграцию.

**Зоны производства:** на общих prefab героев присутствуют поставщики с тегами героя. В Survive существующие ProductionData/Population выбирают поставщиков и регионы стены; в Distance — героя. Разные теги исключают неоднозначность lookup. Наличие поставщика на герое само по себе не назначает ему работу и не меняет выбранного поставщика Survive. Сохраняются Host/External, области поиска, маркер начального появления и текущий lifecycle регионов. Нового контроллера режима или переключателя prefab не добавлять.

**Очистка ссылок:** обновить preload/pool definitions, catalog/attribute bindings и источники fixtures, которые ссылаются на удаляемые assets. Сохранить согласованную вместимость пулов 0 = без лимита. Не менять общие геометрические сервисы ради удаления контентных дублей. Автоматический маршрут героя остаётся отложенным отдельным шагом.

## 3. Production: цена, заказ, результат

### 3.1. Источник аргумента и существующая ReservationPolicy

Согласованный источник оформить в `ProductionRecipeData`: `ProductionProgressionSourceType` = RecipeOrdinal / RecipientResource; для второго режима — существующий EconomyEntrySelectionData с одним Resource/Stack в кошельке Recipient. Нулевой баланс — корректный Level=0, отсутствие/неоднозначность кошелька — ошибка. Нельзя искать уровень в авторском шаблоне персонажа.

Добавить необязательный допустимый `ProgressInterval` аргумента. У четырёх upgrade recipes диапазон 0..98; значение 99 возвращает недоступность покупки до списания, UI показывает максимум 100. Это общий диапазон применимости прогрессии рецепта, без поля CharacterMaxLevel и без использования временного счётчика CompletedYields как уровня.

**Правила расчёта:**

| Policy | Ресурсный аргумент |
|---|---|
| OnEnqueue | Прочитать при успешном резервировании входов во время admission. Все резервируемые тогда выпуски получают этот снимок |
| OnStart | Прочитать при резервировании входов перед работой заказа. Все резервируемые тогда выпуски получают этот снимок |
| PerYield | Прочитать перед очередным выпуском; после завершения предыдущего может получиться новое значение |

Не прибавлять номер цикла к ресурсу автоматически и не прогнозировать будущий Level. В meta catalog явно выбрать OnEnqueue по решению пользователя; один UI-запрос — один выпуск. Последующий запрос после завершения предыдущего читает уже повышенный Level. Прочитанное значение сохранить в записи резервирования и ProductionYieldAttempt. Inputs и outputs конкретного выпуска вычислять по нему. Счётчик RecipeOrdinal сохраняется отдельно как идентичность/прогресс производства; уже согласованное закрепление номеров не отменяется.

### 3.2. Единый расчёт и наблюдение

**Добавить в существующий Production путь оценки заказа**, принимающий producer Entity, recipe, requester/payer/recipient и количество выпусков. Результат: доступность/причина, аргумент, конкретные inputs/outputs, признак предварительной оценки. UI не вычисляет цену сам. Разрешение аргумента — один внутренний helper Production, переиспользуемый оценкой и исполнением; сам LongProgressionData не читает сервисы.

До резервирования ресурсный результат считается предварительным. В `ReadAcceptedOutput` использовать существующий Pending, если количество ещё не закреплено. Не возвращать Ready с предположением, что ресурс остался прежним, и не выдавать неизвестный incoming за ноль. После фиксации публиковать существующее ProductionAcceptedOutputsChangedEvent.

**Решение пользователя по R2:** OnEnqueue — настройка данных, без проверки в коде «RecipientResource требует OnEnqueue». Для meta catalog явно выбрать OnEnqueue; один заказ выдаёт одно улучшение. Не вводить ограничения сочетаний source/policy, отдельную политику меты или проверку названий рецептов. Существующие Release с RecipeOrdinal и закреплением номеров сохраняются. Мета не создаёт конечный Release. Отложенное резервирование объёма для новых динамических Release не включать в перенос UI и не объявлять уже реализованным; не подменять эту отсутствующую работу новым запретом в runtime.

**Изменить читатели одного расчёта:**

- `Scripts/Core/Production/Authoring/ProductionRecipeData.cs`, `ProductionInputData.cs`, `ProductionOutputData.cs` — отделить ProgressionContext аргумента от номера выпуска; сохранить ordinal-вариант; 0 разрешить как аргумент Resource.
- Новый `Scripts/Core/Production/Authoring/ProductionProgressionSourceType.cs`; общий resolver в `Scripts/Core/Production/Internal/ProductionProgressionResolver.cs`.
- `Scripts/Core/Production/ProductionService.cs`, новые `Contracts/ProductionEvaluationRequest.cs` и `ProductionEvaluationResult.cs` — общий read-only вход оценки.
- `Scripts/Core/Production/Internal/ProductionRuntime.cs`, `ProductionYieldAttempt.cs`, `ProductionRuntime.AcceptedOutputs.cs`, `ProductionRuntime.Releases.cs` — capture/исполнение/наблюдение; прежний расчёт RecipeOrdinal для Release сохранить без новых ограничений policy.
- `Scripts/Core/Production/Orchestration/ProductionStateOrchestrationModule.cs`, `ProductionAvailableOrchestrationFactSnapshotResolver.cs`, `ProductionDecompOpUtility.cs`, `ProductionMethodRuntimeResolver.cs` — передавать контекст получателя в оценку; не считать Inputs(1) универсальной ценой.
- `Scripts/Core/Knowledge/Internal/Sources/KnowledgeProductionSourceHandler.cs` — static recipe capability не смешивать с динамической оценкой конкретного получателя; без известного получателя количество недоступно. Изменение входного кошелька инвалидирует соответствующее чтение через существующие watches, а не весь Knowledge каждый кадр.
- `Scripts/Core/Orchestration/Agents/ProductionAgentWork.cs`, `ConstructionAgent.cs` — использовать общий расчёт с получателем; не вычислять outputs напрямую по NextOrdinal для ресурсного режима.
- `ProductionRecipeData.CollectProjectionPreloadDependencies` — у ресурсного режима перечислять зависимости assets, не подменять неизвестный аргумент числом 1. Runtime-доза prewarm уточняется штатным Projection при известном выпуске; новый загрузчик не создаётся.

**Дополнение §3.2 согласовано пользователем 2026-09-26:** включить обоих найденных читателей в общий расчёт Production и продолжить реализацию. Конкретные действия:

| Файл | Проверенное чтением поведение | Предлагаемое действие — working model |
|---|---|---|
| `Scripts/Core/Drops/DropRuntime.cs` | TryValidateContainerProduction вычисляет outputs с аргументом 1, затем StartContainerProduction отправляет настоящий заказ тому же производителю; Recipient = operation.SourceOwner | Перевести предварительную проверку на общую оценку Production из §3.2 с фактическим производителем и получателем. Сохранить требования контейнера: ноль inputs и один Token владельца кошельков. Подтверждение фактически выданного контейнера и rollback оставить существующими |
| `Scripts/Core/Activities/Orchestration/MaterializationOrchestrationEndpointRuntime.cs` | TryResolveAuthoritativeOutput вычисляет outputs только по следующему ordinal; Start уже разрешает owner, который затем становится Recipient заказа | Передать этот owner в общую оценку Production и получить output оттуда. Сохранить требование одного CapabilityHost Token и существующие admission, ожидание, подтверждение и отмену |

**Existing System Fit:** Production остаётся владельцем расчёта и заказа; Drops — подготовки/проверки контейнера; materialization endpoint — проверки и исполнения своего назначения. Новых исполнителей или политик не добавлять. **Best Practices:** все предварительные читатели используют тот же расчёт, что Production; иначе ресурсный аргумент будет учитываться при исполнении и игнорироваться при проверке. Источники: DropRuntime.TryValidateContainerProduction/StartContainerProduction/TryCompleteContainerPreparation и MaterializationOrchestrationEndpointRuntime.Start/TryResolveAuthoritativeOutput. Это статическое чтение; сценарии и тесты не запускались.

Согласованные изменения Production и перечисленных читателей внесены. Итоговое состояние authoring и оставшиеся ограничения приведены в конце документа. Тесты не запускались.

### 3.3. Рецепты прокачки и количество 0

В четырёх recipes inputs: Require соответствующей доступной записи каталога; Consume Coins и персональных карточек у игрока. Outputs: Level +1 и приращения используемых атрибутов в кошельки развития получателя. Цены и дельты для 99 переходов задаются явными LongListProgressionData по исходным данным с уже согласованными Precision/Rate. Показ и списание используют один рецепт.

Одна пара wallet+asset должна содержать один набор qualifiers. Если два значения одного Attribute требуют разных qualifiers/форм, расположить их в разных явно тегированных кошельках. Рецепт адресует эти кошельки существующим WalletTags. **Исправление согласовано пользователем:** задавать qualifiers в output рецепта, как описано ниже; нулевые seeds с тегами не использовать.

**Нужное общее уточнение outputs:** разрешить прогрессии output вернуть 0 как отсутствие выдачи данной строки; отрицательное количество по-прежнему ошибка. Нулевой output не создаёт запись, marker или materialization. Его index в рецепте сохраняется для корреляции. Это требуется, когда характеристика на конкретном переходе уровня не меняется; нельзя выдавать 1 или добавлять дробную точность ради обхода проверки. Обновить validation, accepted outputs, Knowledge и сумму Release одинаково. Входы сохраняют требование положительного количества. В экономическом кошельке постоянного развития хранить изменения; исходные значения и все непостоянные баффы не дублировать.

#### Теги первой выдачи развития — согласовано

**Working model, пользователь разрешил реализацию:** задавать runtime tags выдаваемой записи в output рецепта. Пример: первое улучшение выпускает +1000 raw Power с тегом Base в кошелёк постоянного развития. Это +1 к базовой силе; нулевой начальный баланс не требуется превращать в положительную запись ради тегов. Для отдельных форм используются соответствующие definition qualifiers и разные кошельки, как предусмотрено выше.

**Verified, статическое чтение:** EconomyLocalWalletOwnerRuntime.ApplyWalletSeeds и EconomyRuntimeWalletBinder.ApplyWalletEntrySeeds пропускают Amount == 0. EconomyServiceCore.AssignTags отказывает при балансе <= 0; ReconcileRuntimeTags удаляет теги после обнуления. ProductionOutputData и EconomyOutputEntry сейчас не содержат runtime tags; TryBuildStackOutputTransaction передаёт только адрес, asset и количество. AttributeEconomyModule берёт qualifiers из runtime tags записи; SkillHostProjectionCore сопоставляет точный набор qualifiers. Поэтому прежний нулевой seed не задаёт Base для первого улучшения. Проверена ближайшая альтернатива: EconomyAggregationRuleData выбирает форму хранения по тегам, но не добавляет теги выдаваемой записи. Это несоответствие плана существующим контрактам, а не подтверждённый запуском игровой сбой.

**Конкретное предложение:**

1. **Добавить** List<TaxonomyTermData> runtimeTags в ProductionOutputData и EconomyOutputEntry. Расчёт количества не менять. Пустой список означает выпуск без дополнительных тегов. Теги задаются явно в recipes; выводить их из названия атрибута или кошелька запрещено.
2. **Переписать** передачу Stack outputs в существующее завершение Economy: передать конкретные записи выпуска с адресами и тегами, подготовить их через существующий TryBuildIssuePlan, проверить совместимость всех записей до commit и применить количество/метаданные через существующий ApplyIssuePlanPostCommit в той же границе уведомлений, что завершение входных leases. Не выполнять отдельное начисление после успешной покупки и не назначать теги из UI. Изменяемые владельцы — EconomyService.cs, Internal/EconomyServiceCore.cs и Production/Internal/ProductionRuntime.cs; публичный EconomyTxBuilder для обычных числовых изменений не расширять.
3. **Передать** те же теги в существующую подготовку Token outputs. Материализация уже получает EconomyIssuedEntryData.RuntimeTags; отдельный путь для персонажей не создавать. Отмена и компенсация остаются у существующих заказов/leases/provisional entries. Нулевая строка не выдаётся и не назначает теги.
4. **Согласованно передать** output metadata через существующие оценку ProductionYieldEvaluation и событие ProductionOrderYieldedEvent, которые содержат EconomyOutputEntry. Читатели численности/Release продолжают считать количество; новый способ отбора целей по тегам результата этим дополнением не вводить. Проверки совместимости Stack должны учитывать и несколько строк одного выпуска, адресующих одну запись, до изменения балансов.
5. **Задать** qualifiers в четырёх upgrade recipes; убрать предложение нулевых seeds с тегами. Базовые значения хранить отдельно от постоянных приращений. В числовом просмотре использовать существующую проекцию источников определения: учитывать только qualifiers нужной формы и каждый кошелёк один раз. Показ не должен суммировать развитие всех merge-форм или повторно добавлять одну и ту же запись через local/external projection.

**Existing System Fit:** Production владеет authored outputs, фиксированными значениями выпуска и моментом settlement. Economy владеет идентичностью Stack/Token, проверкой совместимости тегов, commit и уведомлениями; переиспользуются TryBuildIssuePlan/ApplyIssuePlanPostCommit. AttributeEconomyModule и SkillHostProjectionCore владеют интерпретацией qualifiers и вычислением характеристик. CapabilityHostAttributeSourceData уже фильтрует/снимает definition selectors для выбранного host; числовой просмотр переиспользует этот расчёт без Entity. EconomyStorageBridge и EconomyOwnerStateTransfer уже сохраняют/переносят теги положительных записей, их формат менять не требуется. Нулевые holdings, старые сохранения и новая система тегов вне объёма предложения.

**Best Practices:** метаданные выдачи принадлежат описанию выдачи и фиксируются вместе с её результатом. Образец внутри проекта — EconomyServiceCore.IssueEntriesInternalCore: подготовка IssuePlan, проверка, изменение баланса, ApplyIssuePlanPostCommit и публикация в одной notification batch. Предложение переиспользует этот порядок при существующем settlement Production; batch уведомлений сам по себе не объявляется новой транзакцией. Дополнительный положительный seed ради сохранения тегов исказил бы характеристики и не используется.

### 3.4. Универсальная кнопка покупки и UI-команда

**Working model, решения пользователя:** сохранить универсальную семантику исходной BuyButton; ожидание покупки и блокировка ввода принадлежат UI. Не менять ProductionRuntime или ProductionOrderRequest ради последовательных нажатий. Отправлять один запрос, далее наблюдать принятый OrderId. Общий журнал дедупликации Production и ExpectedProgressionArgument удалить из этого этапа.

**Verified — исходная кнопка:** Assets/Game/Scripts/UI/BuyButton.cs получает цену и balances через Setup/UpdatePrice/UpdateBalance, показывает строки стоимости, управляет доступностью и вызывает OnActive/OnInactive. В ней нет покупки, Level или Upgrade. В Assets/Game/Prefabs/UI/BuyButton.prefab базовый Button.onClick пуст; в Main.unity он настроен на UnitPanel.Upgrade в двух панелях и EnergyPopup.BuyEnergy в окне энергии. Этот пример подтверждает универсальность кнопки; перенос энергии по-прежнему исключён.

| Файл package | Конкретное действие |
|---|---|
| Scripts/Core/UI/PurchaseButton.cs | Перенести универсальное представление цены и доступности. Вместо ResourcesData/ResourceType принимать общие строки цены и баланса Economy assets с их icon/Precision; строки переиспользуют ListView/Item. Сохранить OnActive/OnInactive. Добавить состояние ожидания и authored Unity Events OnPurchaseStarted / OnPurchaseEnded. Не включать recipe, персонажа, Level, Upgrade, расчёт цены, списание или вызовы сервисов |
| Scripts/Core/UI/Production/ProductionUIAction.cs | Общий UI-обработчик покупки через Production: получить настроенное действие и текущий контекст, передать их посреднику; получить OrderId и наблюдать результат. Связать начало/конец запроса с PurchaseButton. Никаких веток Hero/Unit/Upgrade; конкретную покупку задают данные |
| Scripts/Core/Production/UI/ProductionUIActionData.cs | Recipe, фильтр производителя и existing selector допустимой записи-получателя. Эти настройки принадлежат действию, а не универсальному представлению кнопки |
| Scripts/Core/Production/UI/ProductionUIRequestAdapter.cs | Принимать Evaluate/Order/Observe/Unobserve через EventBus, проверять контекст и обращаться к ProductionService. Вернуть оценку, доступность, OrderId и результат. Подключить существующим ProductionRuntimeInstallerData; очистка — §6.1 |

**Контекст команды:** передавать идентичность просмотра, точный handle записи и идентификатор UI-запроса. Root owner брать из контекста игрока; повторно разрешать handle и проверять ownership/selector. Для прокачки selector требует открытого персонажа, но не Selected. Это настройки конкретного действия; общее действие не получает требования «всякая покупка должна улучшать персонажа». Идентификатор UI-запроса связывает ответ с отправителем и не задаёт новую семантику CallerCorrelationKey в Production.

**Порядок нажатия и событий:**

1. Button.onClick вызывает общее ProductionUIAction. До вызова событий зафиксировать данные команды и текущий просмотр. Если эта UI-покупка уже ожидает результат, повторное нажатие не отправляет ещё один запрос.
2. Установить состояние ожидания и вызвать OnPurchaseStarted **до отправки** через EventBus: ответ может прийти синхронно. В новых Main/prefab настроить этим UnityEvent блокировку нужного ввода, например CanvasGroup.interactable = false. Событие допускает любые authored визуальные действия; не зашивать список блокируемых кнопок или экранов в код.
3. Отправить один запрос. Принятие с OrderId не является успехом покупки и не снимает ожидание. Последующее состояние читать/наблюдать по этому OrderId; принятую команду повторно не отправлять.
4. После подтверждённого результата обновить цену и balances через общую оценку Production, завершить ожидание и вызвать OnPurchaseEnded. Новое нажатие начинает новую покупку с актуальными данными. Недоступность по цене/условиям сохраняется: завершение ожидания само по себе не делает покупку доступной.
5. Отказ до принятия, ошибка или отмена также завершают ожидание и вызывают OnPurchaseEnded, чтобы снять authored блокировку. Это событие означает окончание попытки, а не успех. Результат сообщения определяет успешное выполнение или причину отказа. Автоматического повторного заказа нет.

Состояние ожидания не сбрасывать от простого обновления цены/баланса. OnActive/OnInactive означают текущую доступность, а OnPurchaseStarted/OnPurchaseEnded — границы попытки покупки; одно событие не подменяет другое. В authoring не отключать источник ожидания тем же действием, которым блокируется ввод.

Смена или закрытие представления снимает его watch, но не отменяет уже принятый заказ. UI-посредник сохраняет связь с OrderId для результата и подтверждённого сохранения (§5); он не хранит историю завершённых покупок и не устанавливает глобальную блокировку получателя. При восстановлении наблюдения использовать snapshot OrderId, не отправлять покупку заново. Поздний ответ старого просмотра не меняет данные новой карточки. Явная отмена и закрытие domain проходят существующий Production lifecycle.

**Удалить из плана:** журнал domain/requester/caller key, fingerprint запроса, возврат прежнего OrderId при повторном admission, ExpectedProgressionArgument и его проверки/компенсацию. Удалить запрет покупок по получателю из ProductionUIRequestAdapter: последовательность обеспечивается ожиданием UI-действия и authored блокировкой ввода. CallerCorrelationKey остаётся существующим полем корреляции; admission, очередь и ReservationPolicy ради UI не переписываются. Отдельный журнал в UI/Storage и очередь повторной доставки не добавляются. Согласованные ресурсная прогрессия и общая оценка Production из §§3.1–3.2 остаются отдельными изменениями.

**Граница цены:** показ использует общий расчёт Production; окончательные входы определяются существующей ReservationPolicy, в мете — OnEnqueue. Контракт закрепления ранее показанной цены больше не предлагается. Следующую UI-покупку открывать после результата и обновления оценки; нового обещания неизменности цены при независимом внешнем изменении ресурса не вводить.

**Existing System Fit:** PurchaseButton владеет представлением и событиями UI; ProductionUIAction/посредник связывают команду с её OrderId; ProductionRuntime владеет очередью, ценой, резервированием, выпуском и компенсацией. Результат получать через существующие snapshot и ProductionOrderFinishedEvent, а подтверждённый commit для сохранения — через ProductionOrderYieldedEvent. Никакого исполнения покупки внутри кнопки.

**Best Practices:** сохранить уже используемое в ChainRush разделение универсального представления и внешнего обработчика действия: одна BuyButton применяется к UnitPanel.Upgrade и EnergyPopup.BuyEnergy. Расширение добавляет только наблюдаемый lifecycle UI-запроса. EventBus.Trigger синхронно доставляет событие, поэтому ожидание устанавливается до отправки; ProductionService.TryEnqueueOrder возвращает идентичность принятого заказа, по которой ведётся дальнейшее наблюдение.

### 3.4.1. Данные кнопки через Source — согласовано

Пользователь не согласовал предложенный список действий на `ProductionUIAction`: PurchaseButton должна получать данные через обновление Source. Предыдущее предложение списка на обработчике снять; код этого дополнения не вносился.

**Verified:** сейчас `ProductionUIAction` совмещает наблюдение цены (`Evaluate`, `_evaluationId`, `RefreshPrice`) и выполнение покупки (`Order`, `_purchaseId`, `OrderId`). Ответ оценки напрямую вызывает `PurchaseButton.Setup`. `EconomyAssetListSource` уже публикует обновления прочитанного списка, а `EconomyListView.onSelectionChanged` — просматриваемую запись. `EconomyNumericSource` обслуживает числовые поля этой записи; расчёт цены принадлежит Production. Проверены эти владельцы и `ProductionUIRequestAdapter.TryBuildOrder/Refresh/AttachWatches`; сценарии не запускались.

**Направление исправления — working model:** Source получает просматриваемую запись, через посредника получает оценку Production и публикует обновлённые данные покупки. PurchaseButton принимает цену, balances и доступность из Source. Обработчик команды получает контекст той же покупки, фиксирует его при нажатии и ведёт принятый OrderId. Удалить наблюдение цены из обработчика команды; не добавлять выбор рецепта или список действий в PurchaseButton. Изменения баланса/уровня обновляют Source; обновление данных не снимает Pending.

**Existing System Fit:** EconomyAssetListSource/ListView сохраняют владение каталогом и просмотром; числовой Source — числовыми полями. Production остаётся единственным вычислителем цены, UI-посредник — границей запросов, Source — поставщиком данных представления, ProductionUIAction — отправителем заказа и наблюдателем результата. Источник не исполняет производство.

**Best Practices:** сохранить уже применённый в UI проекта порядок «источник публикует данные → представление получает Setup» (`EconomyAssetListSource.OnEvent` → `onSuccess` → `EconomyListView.Setup`). Исходная ChainRush BuyButton также получает данные извне и не выбирает покупку. Отделить подписку на оценку от lifecycle принятого заказа, чтобы смена просмотра обновляла представление, сохраняя наблюдение OrderId.

Пользователь поручил продолжить по этой схеме. Конкретное подключение:

- `ProductionUISource` получает `SetupSelection` от ListView; в его authoring перечислены допустимые assets действий панели. Выбор единственного действия по существующему RecipientSelection выполняет посредник. При отсутствии совпадения покупка недоступна; несколько совпадений — ошибка настройки.
- Source владеет Evaluate/watch/unwatch. `onDataChanged` передаёт общий `PurchaseUIData` в `PurchaseButton.Setup`; `onUpdated` передаёт тот же текущий контекст Source в `ProductionUIAction.Setup`. PurchaseUIData содержит только строки цены/балансов и доступность, без recipe и персонажа.
- В `ProductionUIAction` удалить authored action, evaluation id, RefreshPrice и прямую установку цены кнопки. При нажатии фиксировать конкретное действие и handle из текущего Source. После terminal result запросить обновление Source и завершить Pending после обновления данных; ошибка оценки также публикует недоступность и позволяет завершить ожидание.
- `ProductionUIRequest/Response` передают настройки источника для оценки и выбранное действие в ответе. Запрос Order содержит уже конкретное действие; повторная проверка получателя остаётся у посредника. ProductionRuntime не меняется.
- Source освобождает наблюдение при смене просмотра/закрытии; поздние ответы предыдущего request id игнорируются. Принятый OrderId принадлежит обработчику команды и сохраняется при смене просмотра.

Код этого разделения внесён; подключение к сценам продолжается. Тесты не запускались.

### 3.5. Authoring жизненного цикла производства меты

В новой Main использовать обычную Activity меты с одним non-spatial production host, одним каталогом и четырьмя recipes. Это реальное исполнение Production, а не фиктивная Activity для анимации. CapabilityHost/Production подключаются штатным seed. Сами постоянные кошельки принадлежат игроку и вложенным персонажам, не Activity. Для производителя явно задать одну pipeline и OnEnqueue; UI заказывает по одному выпуску. Новый производитель на каждого персонажа не нужен: Recipient и recipe задаёт запрос.

Переход в забег допускается после terminal result уже принятой покупки. Закрытие Main снимает UI-наблюдение, но не считается отдельной командой отмены покупки. Ошибка и явная отмена идут через обычный Production lifecycle. После завершения заказов GameFlow закрывает Activity меты и запускает выбранную игровую Activity. Никакой очереди производства в Popup/CharacterManager.

## 4. UI: конкретные компоненты и границы

### 4.1. Списки, выбор, разблокирование и числа

**Изменить существующие файлы `Scripts/Core/UI/ListView/Economy/`:**

| Файл | Действие |
|---|---|
| EconomyAssetSelectionData.cs | Добавить существующий EconomyEntryHandle. Передачу фактического runtime Asset сохранить: источник уже делает это |
| EconomyAssetListSource.cs | Отправлять mediated read/watch/unwatch через существующие EconomySelectionQuery и EconomyService.Watch; передавать их фильтры. Добавить начальный ответ, версию просмотра и снятие watch. Для точных записей использовать Detailed; агрегированную строку не считать одной записью |
| EconomyListView.cs | Для точных записей сравнивать handles. Переиспользовать существующее восстановление выбора и onSelectionChanged. Runtime-tag фильтры исполняет запрос источника, повторный независимый отбор во View не добавлять. Next/Previous подключить к существующему selection-пути |
| EconomyAssetRuntimeTagAction.cs | Оставить публичные Add/Remove/Toggle, min/max и Substitute; перенести проверку свежей группы и изменение тегов в Economy-посредник; UI отправляет намерение |
| EconomyListViewItemProfile.cs | Оставить title/description/icon, tag events и Setup от существующего onSelectionChanged списка. Новое событие выбора и ответственность за покупки или числовые подписки не добавлять |

В Economy добавить `EconomyUIRequestAdapter` и сообщения read/watch/unwatch/tag action; установку/cleanup подключить существующему EconomyRuntimeInstallerData. В tag action передавать existing selection допустимой группы и точные handles, mode/tag/min/max/policy. Handler перечитывает группу, проверяет допустимость и выполняет существующие mutations в BatchChangeNotifications. Проверка права предшествует записи; откат существующего Substitute сохранить. Не объявлять batch уведомлений новой транзакцией кошельков.

Unlock задать тем же Add tag action: allowed selection требует Entitled и исключает Available; добавить Available, сохранив Entitled как факт ранее выданного права. UI показывает Ready только для Entitled без Available. Повтор уже выполненного Unlock ничего не выдаёт. Select допускает Available. Это authoring существующей tag-механики с проверкой применимости на стороне Economy; нового Unlock executor нет.

**Working model — общий источник числовых данных просмотра:** удалить из предложения EconomyItemProfileData с полями Level/Attributes/recipes. Источник принимает текущую запись от существующего onSelectionChanged списка. Каждое числовое поле задаёт selector ресурса или Attribute, формат и получатель значения; классы HealthField/HeroProfile не создавать. Чтение выполняется через общий источник и Economy-посредник. Одинаковые запросы одного значения обслуживаются совместно. Характеристики выбранной записи рассчитываются одним проходом, затем поля выбирают свои значения из результата. Уровень — обычный ресурс кошелька. Цены поступают из ProductionUISource в PurchaseButton; ProductionUIAction отправляет заказ и наблюдает его результат. Профиль списка этим не управляет. Настройки просмотра merge-форм отложены. Для характеристик переиспользовать `EconomyService.TryProjectWallets → SkillHostProjectionCore.BuildProjectedState/BuildEffectiveAttributes`: тот же расчёт, что у боевого host, без создания Entity и без второго вычислителя характеристик в UI. Для выбранной записи использовать её определение и источники постоянного развития; временные боевые модификаторы в мете отсутствуют.

Подписки: один initial snapshot, затем адресные updates; смена просмотра снимает прежние leases, поздний ответ старой версии игнорируется. Format использует Precision asset. Активный заказ наблюдается независимо от того, открыта ли в данный момент его карточка. Источник списка и профиль не опрашивают данные каждый кадр.

### 4.1.1. Один список юнитов — согласовано

**Решение пользователя, working model:** один EconomyAssetListSource читает весь каталог Unit; один обычный EconomyListView показывает карточки в одном контейнере. `Selected` остаётся отметкой карточки. Разделение на «выбранные» и «остальные» удалить из новой UnitUI. Оригинальные Main/UnitList сохранить.

**Действия:** оставить один Source и один ListView; удалить второй Source/ListView, SelectedRoot и подписи двух групп из новой Main. Единственный onSelectionChanged обновляет профиль, числовые данные, Source покупки и tag actions. Восстановление просмотра по handle уже выполняет ListView. Не добавлять группы, EconomyListViewGroupData или изменения контейнеров в общий ListView.

**Existing System Fit:** Economy владеет Selected и уведомлениями, Source читает каталог, ListView владеет одним просмотром. Профиль и Source покупки получают один выбор. Ограничения состава 1..4 задаются существующим tag action, не расположением карточки.

**Best Practices:** сохранить одного владельца состояния просмотра через существующие `ListView.TryRestoreSelection` и `EconomyListView.IsSameData`; второй механизм синхронизации выбора не нужен.

### 4.1.2. Верхние счётчики — отложено

**Решение пользователя:** не подключать верхние счётчики в текущем этапе. Балансы входных ресурсов показывать в цене покупки через ProductionUISource/PurchaseButton. Убрать старую ResourcesPanel из новой Main; оригинал сохранить.

Не добавлять Owner / ViewedEntry, enum контекста или расширение числовых запросов. EconomyNumericSource обслуживает уровень и характеристики просматриваемого персонажа. Перенос верхних счётчиков требует отдельного шага.

### 4.2. Spine и разделение панелей — отложено

**Решение пользователя:** вынести этот объём в отдельный этап. Сначала подробно разобрать ответственность и связи UnitPanel, UnitSelector и UnitMergePanel, затем отдельно согласовать их разделение. Настоящий план не фиксирует, какие части этих классов должны стать профилями ListView.

**Не создавать в текущем этапе** SpineEconomyItemProfile, SpineItemPresentationData, интеграционную сборку Morboo.Integrations.Spine.asmdef и mappings EconomyAsset → Spine. Не переносить сейчас Spine-портреты/анимации и представление merge-форм в новые общие компоненты. Не заменять перечисленные панели предложенными профилями и не переносить старые игровые исполнители целиком под новыми именами.

Оригинальные панели, префабы, сцены и их визуальные assets сохранить для последующего разбора и сверки. Полный перенос представления этих панелей и соответствие их Spine-поведения не включать в приёмку текущего этапа. Вместо отложенной работы не добавлять временную реализацию. Общие списки, числовые данные, PurchaseButton, команды меты и боевые определения merge-форм сохраняют свой согласованный объём; отложен именно разбор и перенос описанного представления.

### 4.3. Popup<T> и UIFlow

Добавить `Scripts/Core/UI/Popup/Popup.cs`: визуальный generic Popup<T> на UIPresentationController. Принимать T из типизированного IUIContext либо GameFlowUIContext.Payload. Setup только привязывает данные. Состояния UIFlow управляют show/hide; Animator Visible, задержки, OnShow/OnShown/OnHide/OnHidden сохраняются. Не хранить второй реестр открытых окон и не менять Time.timeScale.

В UIFlowServiceCore добавить обработку сообщений открытия/закрытия/manual release через EventBus; DTO — `UIFlowRequest.cs` и результат. Подключать обработчики в существующем StartSession, снимать в ResetRuntime; повторный StartSession не дублирует подписки. Отдельного UIFlow runtime installer сейчас нет, создавать его для этого не требуется. Controller отправляет request, не вызывает Service. Завершение hide отправляет Release один раз для текущего UIHandle. На повторном show/close и уничтожении отменить прежние coroutines/анимационные callback; нулевая анимация тоже завершает lifecycle. Не определять время hide через длительность случайного текущего состояния Animator: окончание сообщается настроенным animation event, для окна без анимации — сразу.

TabManager/TabSelectButton оставить. Новая Main содержит Hero/Unit/Battle, начальная вкладка Battle; Animator/UnityEvents оформляют её ширину, цвет и масштаб. Удалить из новых select-events включение Energy/Settings/Tutorial. Новые package GUID получить через Unity согласованным шагом; оригинальные компоненты/ссылки не менять.

### 4.4. Перенос существующего RewardPopup

**Working model, исправление согласовано пользователем:** перенести исходный Assets/Game/Scripts/UI/RewardPopup.cs в Scripts/Core/UI/Rewards/RewardPopup.cs с адаптацией данных и отображения к Framework. Сохранить его роль наследника Popup<T>, принимающего готовые данные для показа. Не проектировать вместо него окно, которое умеет открываться только по записи реестра Rewards.

**Вход окна:** подготовленный список отображаемых позиций на данных Framework; передавать его как T через общий контракт Popup<T> из §4.3. Количество относится к переданной позиции, а не к текущему балансу кошелька. Иконки, подписи и форматирование брать из существующих UI-данных assets и настроек представления. Идентичность получателя/записи Rewards и состояние Delivered не являются обязательными полями или проверками окна. **Удалить из предложения RewardUIContext**, объединявший запись реестра и дублирующие строки отображения; новый обязательный reward-specific контекст не добавлять.

**Сохранить исходное представление:** initialDelay, spawnCooldown, выбор разных prefab, основной и отдельный контейнер элементов, локальные эффекты появления и OnRewardsShown. Использовать существующие ListView/Item и их профили; выбор представления/контейнера задавать authoring по данным assets, без RewardType/UnitData в Core. OnRewardsShown вызывается после окончания последовательности показа. Закрытие или новый Setup прекращают прежнюю последовательность, освобождают её элементы/обработчики; поздний callback не изменяет новое открытие. Setup, показ и закрытие не вызывают Grant/Claim и не выдают награду.

**Открытие отделить от окна:**

- Источник с уже подготовленными данными передаёт их в общий путь Popup<T>/UIFlow. Для такого показа не запрашивать реестр Rewards и не требовать запись Delivered.
- Для источника «награда выдана через Rewards» оставить RewardUIRequestAdapter обработчиком этого источника. По явному запросу recipient + identity он читает существующий RewardService.GetRecordsAsync, проверяет Delivered, подготавливает данные показа и передаёт их в тот же общий путь UIFlow. Проверка доставки принадлежит этому обработчику, не RewardPopup. Асинхронный ответ привязан к request id и отмене; окно не вызывает RewardService напрямую.
- Старую MMEvent-подписку RewardPopup и зависимости от игровых IRewardList, RewardsData, RewardType, UnitData в новый package не переносить. Не подписывать окно автоматически на все события Rewards и не открывать все прежние Delivered-записи при входе в Main.

**Verified:** исходный RewardPopup.Setup(IRewardList) принимает данные непосредственно и запускает SpawnRewards. RewardEvent.Transfer/End — отдельный способ вызвать Setup и открыть окно. LevelResultPopup.OnMMEvent(LevelResultEvent) вызывает Setup(e.Data) напрямую и использует другие подписки; запись в реестре для этого не требуется. В Main настроены разные prefab, отдельный unitRewardRoot, initialDelay = 0.5, spawnCooldown = 0.2 и OnRewardsShown. Источники: Assets/Game/Scripts/UI/RewardPopup.cs, UI/Popup.cs, UI/LevelUI/LevelResultPopup.cs и Assets/Game/Scenes/Main.unity. Это чтение кода и данных, не запуск сцены.

**Existing System Fit:** Rewards/Economy владеют выдачей; обработчик конкретного источника — получением данных и решением открыть окно; UIFlow и общий Popup<T> — lifecycle; RewardPopup и существующие элементы списка — показом. LevelResultPopup подтверждает необходимость прямого входа с данными, но его перенос и финальные награды забега остаются вне текущего этапа. Daily/idle и другие новые источники этим пунктом не добавляются.

**Best Practices:** сохранить существующее разделение данных и представления. Один Setup используется источником RewardEvent и наследником LevelResultPopup; перенос не должен сужать его до одного способа получения награды. Данные поступают снаружи, начисление не зависит от завершения UI-анимации.

**Перелёт иконок отложен пользователем как отдельная минорная задача.** Его компоненты, маршруты и пулы не входят в реализацию и приёмку этого этапа. Показ RewardPopup не ждёт перелёта.

### 4.5. Loading, принятие запуска и ошибка

**Working model, последние решения пользователя:** ожидание героя и обработку Failed процесса Orchestration отложить отдельным шагом. В текущем этапе Loading показывает только подготовку Activity; переход к игровому представлению завершается по ActivityReady. Это не подтверждение появления героя. Стартовое Objective, Population и Production продолжают штатное исполнение после Ready.

Добавить общий `Scripts/Core/UI/Flow/ActivityLoadingPresentationController.cs`. Контекст — RuntimeId GameFlow, StepId запуска, его execution/correlation и текущий ActivityId. Начальный snapshot получать медиированным запросом в GameFlow; затем получать изменения подготовки текущей Activity. Показывать реальные Value01/Phase через authored UI-события и прежнюю шкалу/локализованные подписи. PreparationFailed возвращает управление в Main через существующие условия и политику GameFlow. Временное ожидание маркера не считать ошибкой.

GameFlowServiceCore уже хранит ActivityLifecycleRecord.Progress. Добавить компактные DTO запроса/снимка/изменения в Core/GameFlow; обработчик остаётся в существующем GameFlowServiceCore. Показывать принятие запуска, ожидание, подготовку, Ready и ошибку с RuntimeId/StepId/ActivityId и текущей корреляцией. Начальное чтение переиспользует ActivityService.TryGetSpaceSnapshot. Подписки относятся к UIHandle и текущему запросу; закрытие окна/Reset снимают их. Старые ответы не завершают новый Loading. Controller не обращается к сервисам напрямую и не назначает производство. Кнопку Start блокировать по принятому запуску; повторное событие не запускает второй экземпляр того же исполняемого шага.

Не добавлять GameFlowConditionActivityObjective и GameFlowConditionActivityProcess, наблюдение Objective/процессов и ожидание их результата в Launch executor. Не переносить эти условия внутрь UI и не заменять их таймером. Самостоятельная обработка Failed стартового Objective вместе с ожиданием героя также относится к последующему разбору Loading. В текущем этапе подключаются отказы принятия/подготовки Activity через имеющийся GameFlow lifecycle.

**Existing System Fit:** ActivityLauncher принимает запуск; ActivitySpace владеет подготовкой и её прогрессом; GameFlow хранит ActivityLifecycleRecord и управляет последовательностью; UIFlow владеет представлением. Initial query и сообщения используют этих владельцев. Objectives/Orchestration/Production продолжают начальное появление героя, но не становятся источниками готовности Loading этого этапа.

**Best Practices:** сохранить существующее разделение команды запуска и наблюдения готовности: StartLaunchActivityExecutor → отдельный шаг с GameFlowConditionActivityLifecycle. Существующие AutobattleFlow и GameFlowServiceEditModeTests описывают этот порядок; тесты здесь только прочитаны. UIFlow загружает UI-сцену через SceneManager.LoadScene, поэтому не выдавать этот синхронный вызов за асинхронный процент. Показать настоящий ActivitySpaceProgress; второго загрузчика не создавать.

### 4.5.1. Имена UI-сцен — согласовано и выполнено

**Решение пользователя, working model:** новые копии называются FrameworkStart.unity, FrameworkMain.unity и FrameworkLoading.unity. Они остаются в FrameworkUI/Scenes с прежней внутренней иерархией. Переименование выполнено через Unity; новые сцены добавлены в конец EditorBuildSettings. Исходные сцены и прежний основной запуск сохранены.

GameSceneData новых сцен использует уникальные имена. UIFlow/GameSceneData не расширять адресом по пути. Это согласованное исключение из первоначального требования сохранять имена файлов новых копий.

**Existing System Fit:** GameSceneData задаёт имя; UIFlow загружает, отслеживает события и закрывает по той же идентичности. GameFlowConditionScene и GameSceneDataExtensions используют её без изменений.

**Best Practices:** сохраняется единый ключ для загрузки и её событий; дополнительное сопоставление путей и имён не вводится. Проверенные исходники: UIFlowServiceCore.PrepareScene, OnSceneLoaded, CloseScene.

## 5. Сохранение и незавершённая покупка

### Адресация сохранения — согласовано

**Verified, статическое чтение 2026-09-26:** `EconomyStorageBridge.SaveAll/LoadAll` принимают только `IEconomyAssetOwner`. `CreateStorageContext` строит `SettingsContext` из `owner.StableSimulationKey` отдельно для каждого вложенного владельца. `GameRuntimeHost.RegisterPrimaryOwnerPlayer` задаёт другой контекст `SettingsContext(owner.Id)` в `PlayerSnapshot`. PlayerSnapshot.StorageContext не является адресом сохранения Economy. Сценарии и тесты не запускались.

**Решение пользователя — working model:** оставить `EconomyStorageBridge` и адресацию по StableSimulationKey без изменений; удалить требование использовать PlayerSnapshot.StorageContext для Economy. Новый PlayerData.Id Framework отделяет корневой профиль; существующие ключи вложенных runtime-владельцев сохраняют его принадлежность. Обычное сохранение выделить из shutdown в GameRuntimeHost согласно остальному §5. Ничего не переносить между ключами и не читать старый GameData.

**Existing System Fit:** EconomyStorageBridge владеет построением адресов и рекурсивным сохранением постоянных кошельков; PlayerService — контекстом зарегистрированного игрока; GameRuntimeHost — моментом сохранения и синхронизацией runtime Player. Новый UI не назначает адреса хранения.

**Best Practices:** при подключении нового инициатора сохранения переиспользовать действующую идентичность записей. Замена ключа является изменением формата хранения, а не переносом вызова SaveAll. Точные источники: `EconomyStorageBridge.SaveAll`, `LoadAll`, `SaveOwnerRecursive`, `CreateStorageContext`; `GameRuntimeHost.RegisterPrimaryOwnerPlayer` и `SyncPrimaryOwnerPlayerEconomyForPersistence`.

Адресация EconomyStorageBridge сохранена. Подключение обычного сохранения внесено; тесты не запускались.

В `Scripts/Core/Runtime/GameRuntimeHost.cs` выделить обычное сохранение из shutdown-пути. Оно получает действующий PlayerSnapshot.EconomyOwner и сохраняет runtime-игрока напрямую через EconomyStorageBridge.SaveAll. Обратного копирования в PlayerData нет; Activity для обычного сохранения не закрывается. EconomyStorageBridge сам строит SettingsContext по StableSimulationKey каждого владельца. Отдельный PlayerData Framework имеет собственный Id и не читает старый профиль GameData. Фильтр Persistent в bridge сохранить. В новых scenes единственный постоянный GameRuntimeHost переживает Main/Loading; вторую регистрацию игрока при каждом возвращении в Main не создавать.

Для связи с UI-командами добавить общие сообщения запроса сохранения и результата в Core/Runtime. После завершения всей tag-action EconomyUIRequestAdapter отправляет подтверждение изменения; при отказе или отсутствии изменения — нет. ProductionUIRequestAdapter использует существующий ProductionOrderYieldedEvent как подтверждение commit и ProductionOrderFinishedEvent для окончания заказа. Наблюдение выполняется по принятому OrderId, а не по открытой карточке. Повторное событие одного yield не повторяет запись. GameRuntimeHost принимает запрос, отмечает dirty revision и сохраняет согласованное состояние; результат возвращает request/revision и ошибку для UI. Повтор сохранения не передаёт повторную команду покупки.

**Решение пользователя по R3: отложить вопрос незавершённой покупки.** Удалить из текущего объёма отмену UI-заказов на pause/выходе, ожидание их компенсации ради записи, сохранение/восстановление Production orders и переработку RefundReservedInputs. Не вводить вместо них неоговорённый snapshot, журнал возвратов или блокировку сохранения. Сохраняются согласованные точки обычной записи: подтверждённое изменение, pause и штатный выход; сами lifecycle заказов этим этапом не меняются.

**Явное ограничение текущего этапа:** сохранность покупки, прерванной между резервированием и результатом, не является выполненной гарантией. Production резервирует Stack через Spend, его очередь не сериализуется в профиль; текущий RefundReservedInputs не передаёт результат возврата. Эти проверенные факты оставить основанием отдельного последующего шага, а не разрешением чинить Production внутри UI-переноса. Отсутствие runtime-проверки не выдавать за воспроизведённый сбой.

Ошибка обычной записи Storage возвращается сообщением GameRuntimeSaveResult и фиксируется в диагностике. Показ ошибки и ручной повтор отложены решением §5.1. EconomyStorageBridge.SaveAll сохраняет владельцев последовательно и не обещает общей атомарности нескольких файлов. Новый crash-recovery формат также не входит в этот этап.

### 5.1. Представление ошибки сохранения — отложено

**Verified, чтение кода 2026-09-28:** EconomyUIRequestAdapter.Tags и ProductionUIRequestAdapter отправляют GameRuntimeSaveRequest. GameRuntimeHost исполняет запись и отправляет GameRuntimeSaveResult с request/revision/failure. Получателя GameRuntimeSaveResult в UI нет. Предыдущий текст §5 требует показа ошибки и повторного сохранения, но не перечисляет компонент, который это выполняет. Это пропуск плана; новый компонент ещё не добавлен. Ошибка Storage в игре не воспроизводилась; тесты не запускались.

**Решение пользователя:** отложить UI ошибки и ручной повтор. Оставить согласованные точки автоматического сохранения и GameRuntimeSaveResult. Убрать показ ошибки и кнопку повторения из текущей приёмки, записать отдельным шагом. Ошибка продолжает возвращаться сообщением и фиксироваться в диагностике. Новую покупку ради повторного сохранения не отправлять. Не создавать GameRuntimeSaveUI и дополнительную панель Main в текущем этапе.

**Existing System Fit:** EconomyUIRequestAdapter/ProductionUIRequestAdapter инициируют запись после подтверждения изменения; GameRuntimeHost владеет выполнением и revision; EconomyStorageBridge — сохранением постоянных кошельков; Storage — чтением/записью. UI-получателя результата в текущем этапе не добавлять.

**Best Practices:** повторять операцию у владельца записи, сохраняя отдельно уже совершённую покупку. Это следует существующей границе `GameRuntimeSaveRequest → GameRuntimeHost.OnEvent → GameRuntimeSaveResult`; повтор `ProductionUIAction.Purchase` повторил бы доменную операцию и не подходит для сохранения.

## 6. Authoring, порядок работ и граница согласования

**Новые данные ChainRush, без игрового C#:** отдельный профиль Framework; seed 4000 Coins/по 5 четырёх карточек, право Tabasco, исходные Available/Selected; Level и кошельки развития; BoardContent; четыре recipes, один каталог и один production host для прокачки в Activity меты; отдельные общий host начального выпуска героя, каталог с двумя recipes и стартовое Objective/Population для обоих игровых уровней; UI presentation/profile без отложенных Spine mappings; две конфигурации запуска уровня с общим критерием героя Hero + Available + Selected; копии Start/Main/Loading и затронутых UI-prefabs под Assets/Game/FrameworkUI с сохранением иерархии. Unity создаёт все .meta. Старые сцены, скрипты и префабы остаются на месте для сверки. Ненужные новые ветки реализации после замены удаляются, а не оставляются как legacy.

**Порядок:** (1) итоговый план утверждён; (2) общие Economy/Production/UI контракты; (3) assets постоянных данных и производства; (4) новые UI-scene/prefab версии и связи; (5) Selected/Board/бой/Loading; (6) сохранение и cleanup; (7) завершить весь утверждённый объём; (8) запросить отдельное разрешение на тесты. Проверки по этапам, smoke и FullUnitChain до этой границы запрещены. Все заданные вопросы решены; действия по боевым assets перечислены в §2.6. Реализовывать только описанные здесь расширения.

После разрешения тестов использовать существующие package suites и ChainRush-сценарии, дополненные значимыми проверками: устойчивый владелец после Selected; отдельные игроки не делят развитие; одна цена в оценке/исполнении; все три reservation policies; нулевой output; одно нажатие — один заказ, authored OnPurchaseStarted/OnPurchaseEnded, снятие ожидания при отказе/ошибке и чтение принятого OrderId без повторной отправки; максимум уровня; отказ без частичной покупки; pause без незавершённой покупки; восстановление профиля; фильтры запуска/Board; однократный выпуск выбранного героя и отсутствие повторного выпуска после смерти; UI initial read/смена просмотра/cleanup; показ готового списка в RewardPopup без записи реестра, проверка Delivered только у источника Rewards, отсутствие начисления из окна, отмена прежнего показа и OnRewardsShown; отсутствие старых менеджеров в новых сценах. Тестовый gameplay API и тестовые обходы не добавлять.

**Что именно новое выносится на утверждение:** selection пути к вложенному owner; фильтры прямых и вложенных записей Population для Selected/Board; ресурсный progression source с диапазоном, оценкой и capture по ReservationPolicy; 0 outputs; корректное Pending до разрешения динамического количества; универсальная PurchaseButton с authored событиями покупки, общее ProductionUIAction, медиаторы UI и профиль характеристик; Popup/Reward/Loading как общие представления; подключение обычного сохранения. Переработка возврата входов и сохранение незавершённых orders исключены решением пользователя. Это не скрывать под фразой «только перепривязать UI». Экономический исполнитель покупки, очереди, seed, pooling, Skills, Storage и запуск Activity переиспользуются.

### 6.1. Установка посредников и освобождение

EconomyUIRequestAdapter и ProductionUIRequestAdapter подключить из существующих EconomyRuntimeInstallerData/ProductionRuntimeInstallerData после настройки соответствующего сервиса. Владение adapter хранит соответствующий service facade; его ResetAll снимает EventBus-подписки, watches и записи запросов. Повторная установка предварительно освобождает прежнюю. GameRuntimeInstallerData не имеет Dispose — не приписывать ему такой lifecycle. RewardUIRequestAdapter как обработчик источника Rewards подключить через RewardsRuntimeInstallerData и освободить в RewardService.ResetRuntime. Этот адаптер не является обязательным посредником прямого показа уже подготовленных данных в Popup<T>/UIFlow. UIFlow/GameFlow привязать к их уже существующим StartSession/ResetRuntime.

Закрытие отдельного UIHandle снимает его subscriptions и responses. Принятый production order переживает смену вкладки/карточки; лишь закрытие domain или явная команда выполняют обычную отмену. Reset/новый запуск не принимает поздние ответы предыдущего UI-сеанса. Общие посредники не содержат имён персонажей, количества вкладок, цен или правил уровней.

### 6.2. Итоговый объём UI и порядок сохранения оригиналов

| Объект | Действие |
|---|---|
| Start / Main / Loading | Создать новые версии сцен в FrameworkUI/Scenes. Подключить один runtime/profile, существующие GameFlow/UIFlow, новую Activity меты и два игровых уровня |
| PurchaseButton | Сохранить универсальное представление цены; действия покупки задавать отдельно. Добавить authored OnPurchaseStarted/OnPurchaseEnded для блокировки и восстановления ввода; Production не менять ради ожидания UI |
| Hero / Unit | Подключить согласованные общие списки, числа, карточки и команды выбора/открытия/покупки. Spine-представление и разделение UnitPanel/UnitSelector/UnitMergePanel отложены по §4.2; их полную замену профилями в этот этап не включать. Оригиналы сохранить |
| Battle и маркеры | Сохранить Location001 и два уровня. Выбор — Selected записи уровня, запуск бесплатный. Никакого изменения прохождения при нажатии Start |
| TabManager / TabSelectButton | Подключить package-версии с новыми Unity GUID. Три вкладки, начальная Battle; сохранить визуальные переходы |
| Popup<T> / RewardPopup | Перенести существующее представление и прямой Setup готовых данных, настройки контейнеров/появления и OnRewardsShown. RewardUIContext не добавлять; запись Rewards не обязательна для окна. Итоговые награды забега не входят в этот этап |
| ConfirmationPopup / RewardListPopup / tutorials | Не переносить. Их callbacks и gating в новые scenes не подключать |
| Shop / energy / daily / settings и прочие исключённые экраны | Отключить точки входа в новых версиях; не переносить их игровые исполнители |
| Исходные сцены, UI-prefabs и нужные им скрипты | Оставить на прежних местах для сверки; удаление старого пути — отдельный шаг после приёмки |

Копии изменяемых вложенных prefabs создать до перепривязки родительских объектов. Переиспользовать неизменяемые sprites, fonts, Localization и animations в согласованном объёме. Spine assets сохранить для отдельного этапа §4.2. Не копировать .meta вручную. Префабы и сцены создавать/изменять явными Unity authoring-командами; runtime-конвертации не вводить. Сначала обновить все ссылки нового пути, затем удалить неиспользуемые assets нового gameplay-варианта; оригинальный UI не затрагивать.

### 6.3. Зафиксированные решения и исключения

| Пункт | Зафиксированное действие |
|---|---|
| R1 — цель уровня | Настроить её в Activity через Objectives/Analytics/Orchestration. Selected героя не выбирает цель. Удалить предложенную схему кошелька боевых вариантов как несогласованную |
| R2 — OnEnqueue | Выставить в meta catalog через authoring. Не добавлять программный запрет сочетаний ReservationPolicy и источника прогрессии |
| UI-покупка — согласовано | Универсальная PurchaseButton, authored OnPurchaseStarted/OnPurchaseEnded и одно активное UI-действие. Удалить дедупликацию admission, ExpectedProgressionArgument и запрет покупок по получателю; принятый заказ наблюдать по OrderId |
| RewardPopup — перенос исходного контракта | Сохранить Setup готовых данных и визуальные настройки; убрать RewardUIContext и обязательную запись Delivered из окна. Проверка доставки остаётся у обработчика источника Rewards |
| Список юнитов — согласовано | Один обычный список, без групп. Selected отображается отметкой карточки; общий ListView ради групп не менять |
| Верхние счётчики — отложено | Не подключать ResourcesPanel; балансы показывать в цене покупки. Контекст Owner в EconomyNumericSource не добавлять |
| Перелёт иконок — отложено | Отдельная минорная задача; не создавать UIIconFlight-компоненты и не включать эффект в текущую приёмку |
| Spine и панели — отложено | Не создавать Spine-профили, интеграционную сборку и mappings; не выполнять предложенное разделение UnitPanel/UnitSelector/UnitMergePanel. Сначала отдельный разбор и согласование; подробности — §4.2 |
| R3 — незавершённые покупки | Отложить. Отмену ради pause-save, сериализацию очереди и переработку возврата убрать из текущего плана |
| UI ошибки сохранения | Отложить сообщение в Main и ручной повтор; оставить автоматическую запись, GameRuntimeSaveResult и диагностику |
| Начальное появление героя — вариант согласован | Seed создаёт фиксированного production host; Population выбирает Selected, Production выпускает Token, Activity материализует. Удалить расширение динамического seed; подробности — §2.5 |
| Боевые свойства героя — ответ получен | Одинаковы в обоих режимах. Удалить отдельные Distance-определения и их отличия; поставщиков зон перенести в общие prefab. Конкретные assets и действия — §2.6 |

Решение по именам UI-сцен получено (§4.5.1); перечисленные направления реализации согласованы пользователем. Состояние выполнения приведено ниже. Решённые продуктовые вопросы повторно не открывать. Ни составление документа, ни ответы на вопросы не разрешают тесты.

## 7. Best Practices

**Одна команда — один владелец lifecycle.** Резервирование и компенсация остаются в ProductionRuntime; Selected — в Economy; Popup — в UIFlow. Это конкретное применение разделения чтений/команд: UI получает оценку и отправляет намерение, доменный владелец повторно проверяет состояние. Отдельная write-model меты не создаётся. [Microsoft CQRS](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs).

**Авторские определения отделены от состояния игрока.** Использовать уже реализованные runtime-копии EconomyRuntimeOwnerAssetResolver и постоянные кошельки, вместо записи Level в ScriptableObject. Такой подход позволяет переиспользовать одни определения у разных владельцев. [Unity: separate game data and logic](https://unity.com/how-to/separate-game-data-logic-scriptable-objects). Конкретные первичные источники проекта: EconomyRuntimeOwnerAssetResolver.ResolveSeededAsset, EconomySeededOwnerKeyFactory.CreateOwnerStableKey, EconomyStorageBridge.LoadAll.

**Жизненный цикл представления ограничивает подписки и ответы.** UIHandle/версия просмотра определяют, кому принадлежит ответ и watch. При закрытии старый ответ не возобновляет окно. Переиспользовать существующие контракты UIPresentationController/manual release; правила проверены по исходникам UIFlowServiceEditModeTests, сами тесты не запускались.

## Состояние реализации на 2026-09-28

**Working model: реализация согласованного объёма кода и authoring завершена.** Приёмка в игре и тесты не проводились. Для их запуска требуется отдельное явное разрешение пользователя. Исключения из §6.3 остаются отдельными шагами и не объявляются выполненными.

- Внесены постоянные кошельки FrameworkProfile, точный выбор записей через Selected, entryPath развития, чтение BoardContent выбранных персонажей. Настроены начальные 4000 Coins, по 5 карточек и право разблокирования Tabasco.
- В Production подключены ресурсный аргумент прогрессии, оценка цены, capture по ReservationPolicy, нулевые outputs и runtime tags выдачи; обновлены перечисленные читатели общего расчёта. Четыре рецепта прокачки заданы assets.
- Подключены общие mediated Sources/числовые поля, выбор/разблокирование, универсальная PurchaseButton и наблюдение заказа. Подключены обычное сохранение и освобождение подписок.
- В новой Main подключены единый список Unit, один просмотр и отметка Selected; группировка общего ListView не добавлена.
- Подключены package TabManager/TabSelectButton, три вкладки с начальной Battle. Визуальные параметры старых кнопок заданы Animator assets. Верхняя ResourcesPanel и входы в исключённые экраны удалены из новой копии.
- Создан FrameworkUI/Prefabs/UI/RewardPopup.prefab. Сохранены исходные контейнеры, initialDelay 0.5, spawnCooldown 0.2, локальные эффекты и задержка автозакрытия 1 секунду через существующий MMF_Player. Анимации скопированы, события завершения направлены в Popup. Оригиналы не изменены.
- В создаваемом для этого переноса EconomyAmountListView задан выбор prefab по asset tags: ресурсы и карточки используют один список и один основной контейнер; персонажи — отдельный контейнер. Это исполняет выбор разных представлений из §4.4.
- Новые сцены переименованы в FrameworkStart/FrameworkMain/FrameworkLoading. Добавлена FrameworkGameplay как представление интеграционного контура без второго runtime host. Старый основной запуск сохранён в Build Settings; новые сцены добавлены в конец.
- В Main подключены два уровня через LevelCatalog и Selected, бесплатный Start, новая Activity меты и GameFlow. Loading получает progress подготовки Activity, штатная шкала MoreMountains показывает его и процент. Принятый Ready ведёт к игровому представлению; отказ ведёт к Main.
- Удалены отдельные Distance-определения героев и Activity/flow-варианты Tabasco. Общие prefab содержат поставщиков зон; Distance metric фильтрует HeroRole. Удалены Loadout и неиспользуемые альтернативные geometry assets; GateHeroBody сохранён, поскольку его уже используют оба обычных определения героя.
- Исходники fixtures переведены на общие уровни и Selected героя. Их исполнение не проводилось.
- Авторинг выполнялся через Unity; игра, сборка и тесты не запускались. UI ошибки сохранения и ручной повтор отложены. Unity завершила компиляцию без ошибок. В 15 новых сценах/префабах нет отсутствующих компонентов и компонентов из Assets/Game/Scripts. В новых Data assets не обнаружено неразрешённых сериализованных ссылок.
- Завершены привязки OnPurchaseStarted/OnPurchaseEnded к CanvasGroup Main и удалены callbacks исключённых daily/idle/навигационных контролов. После сохранения и повторного открытия Main у сериализованных UnityEvents нет отсутствующих получателей. В Economy definitions installer удалены две пустые регистрации кошельков; код installer не менялся.
- Согласованная замена отсутствующих шрифтов двух подписей RewardPopup выполнена; исходная Main не изменена.
- В текущей Library обнаружено расхождение AssetDatabase GUID и исходного .meta у старых TabManager/TabSelectButton. Исходные файлы и GUID на диске сохранены; смена GUID package выполнена Unity. Повторный импорт и перезапуск этого расхождения не устранили. Причина не установлена; готовность исходного UI в этой Library не подтверждена. Новые вкладки имеют прямые ссылки на новые package-идентичности.

### Шрифты RewardPopup — согласовано и выполнено

**Verified, чтение assets и Editor 2026-09-28:** TitleLabel ссылается на отсутствующий font GUID `607795a6776ee4806b4a047fcb768f3e`, TapAnywhareLabel — на `a9b507a2395d4438fa1f717db1fbaa1e`. Такие же ссылки находятся в исходной Main; соответствующих .meta в проекте нет. На этих объектах есть LocalizeStringEvent, но нет компонента смены шрифта. Нельзя заявлять о сохранении прежнего шрифта. Отсутствующие начальные иконки строк наград заменяются данными переданного элемента; отсутствующие Avatar/Glow находятся в выключенных частях UI и не используются текущим этапом.

**Решение пользователя, working model:** только в новой RewardPopup назначены TitleLabel существующий `Assets/Game/Fonts/Cairo_Line_Blue SDF.asset`, TapAnywhareLabel — `Assets/Game/Fonts/Cairo SDF.asset`, вместе с их материалами. Текст, локализация, цвет, размер и layout сохранены. Эти шрифты уже используются в Main. Новые шрифты и fallback-код не добавлены; исходная Main не изменена. Выполнено явной командой `ChainRushMetaUIAuthoring.SetRewardPopupFonts` через Unity.

**Existing System Fit:** ссылки на font/material принадлежат TextMeshProUGUI в prefab; LocalizeStringEvent меняет текст; Popup/UIFlow не управляют шрифтами. **Best Practices:** явные ссылки на существующие assets, как у остальных подписей Main, сохраняют authoring владельцем оформления и не требуют runtime-подстановки. Основание — сериализованные m_fontAsset/m_sharedMaterial новых и исходных UI-подписей.

### Две ошибки запуска — исправления данных, 2026-09-28

Область работы ограничена двумя ошибками из пользовательского запуска. `MMSoundManager` не изменялся. Тесты, повторный запуск игры и переинициализация runtime не выполнялись.

**Verified — Economy seed.** Дословная ошибка: `ChainRushEconomyDefinitionsInstaller failed to apply local wallet owner seed: Target wallet cannot merge stack entry with different runtime tags.` В `Editor.log` цепочка проходит через `EconomyLocalWalletOwnerRuntime.ApplyWalletSeeds` и `EconomyDefinitionBootstrapper.Run` на стадии Definitions. В сохранённом после неудачного запуска состоянии Economy у `BugBrownSmall` присутствуют первые шесть записей, включая `Power = 1000 / PhysicalElement`; следующая authored-запись того же Power в том же UnitWallet имеет `FireElement`. Проверка `EconomyServiceCore.CanMaterializeStack` запрещает слияние этих разных наборов тегов. Такое же сочетание девяти записей Power находилось у всех шести BugBrown/BugGreen/BugPurple Small/Medium.

Ближайшее конкурирующее объяснение — конфликт с загруженным сохранением — исключено для этого запуска: `GameRuntimeHost.InitializeInternal` выполняет Definitions до `LoadPersistentEconomyState`, а исключение возникает внутри Definitions. Изменять сохранение или ослаблять проверку Economy не требуется.

**Working model — выполнено по правилу §3.3.** Физический Power оставлен в UnitWallet. Для Fire, Water, Earth, Air, Dark, Light, Poison и Chaos созданы восемь `EconomyWalletData` в `Activities/Autobattle/Economy`, с соответствующим существующим тегом стихии. Эти определения кошельков используются всеми шестью определениями врагов; баланс каждого владельца остаётся отдельным. Каждая стихийная запись Power перенесена в свой кошелёк вместе с исходными amount, form и runtime tags. Остальные seeds оставлены в UnitWallet. Выполнена явная команда `ChainRushMetaUIAuthoring.SeparateEnemyElementPowerWallets`; assets и `.meta` новых кошельков создала Unity. Автоматическая миграция не добавлена.

**Existing System Fit:** `EconomyDefinitionBootstrapper.DefinitionCollector` собирает LocalWallets из определения; `EconomyLocalWalletOwnerRuntime` исполняет их начальный seed; `EconomyRuntimeWalletBinder` создаёт кошельки экземпляра; `CapabilityHostSkillController.BuildRuntimeSnapshot` передаёт все локальные кошельки в существующий Economy projection; `SkillHostProjectionCore` различает атрибуты по qualifiers. Формулы Skills и адресация дропа не меняются. Код package для этого исправления не изменялся.

**Best Practices:** сохранено действующее ограничение идентичности Stack — один набор тегов на пару wallet+asset; исправлен authoring, нарушавший его. Конкретные основания: `EconomyServiceCore.ValidateStackRuntimeTags/CanMaterializeStack`, `CapabilityHostSkillController.BuildRuntimeSnapshot`, `SkillHostProjectionCore.AccumulateAttribute` и уже согласованный §3.3. Второй способ хранения атрибутов или специальная обработка врагов не вводились.

**Verified — каталог.** Дословная ошибка: `Unable to parse file Assets/Game/Activities/Board/Production/BoardProductionCatalog.asset: [Parser Failure at line 91: Expected closing '}']`. Перед девятью элементами entries находились 16 наборов workDuration/recoveryDuration/reservationPolicy без элемента списка и recipe. Этот блок добавлен коммитом `fb08a6be80f17565d195be8f146843500fdd3d28`; он присутствовал на диске, поэтому объяснение только устаревшим кешем импорта не подходит. Удалены ровно эти 48 строк. Секция девяти рецептов и её параметры не изменены. После явного импорта Unity читает каталог и все девять ссылок на recipes.

**Результат чтения assets:** у всех шести врагов наборы seeds до и после совпадают по asset, amount, form и runtime tags; изменились только кошельки восьми записей Power. **Inference / ограничение:** устранены установленные дефекты данных; успешное прохождение нового запуска не проверено и не заявляется. Для подтверждения поведения после исправления нужен следующий пользовательский запуск либо отдельно разрешённые проверки.

### Следующий пользовательский запуск — разрешения Economy, 2026-09-28

**Verified:** в запуске около 14:31 предыдущие ошибки не повторились. Дословная новая ошибка: `ChainRushEconomyDefinitionsInstaller failed to apply local wallet owner seed: Asset does not allow operation 'issue'.` Стек снова проходит через Definitions → EconomyDefinitionBootstrapper → EconomyLocalWalletOwnerRuntime.ApplyWalletSeeds → IssueEntries. Чтение оставшегося состояния после этого запуска показало: BugBrownSmall имеет все 20 записей в девяти кошельках; HeroProductionHost имеет зарегистрированный кошелёк без записей; кошельки следующих MetaProductionHost и FrameworkProfile ещё не зарегистрированы. Владельцы обрабатываются по StableSimulationKey; единственный seed HeroProductionHost — HeroProduction, у которого `AllowedOperations = None`.

Причина отказа этого seed установлена: `EconomyServiceCore.ValidateOperation` отклоняет Issue у HeroProduction. Ближайшая альтернатива — нехватка средств или вместимости — не объясняет сообщение UnsupportedOperation: этот отказ формируется по битовой маске разрешений. Сохранение ещё не загружалось. У MetaProduction, монет, четырёх карточек, Level и двух production hosts та же пропущенная настройка в новом authoring. Отказ на них в этом запуске не наблюдался, поскольку выполнение остановилось раньше.

**Working model — исправлен authoring десяти assets:** Coins, PerfumeCard, TabascoCard, WaterCard, ColaCard, Level получили Require/Issue/Consume; HeroProduction и MetaProduction — Require/Issue; HeroProductionHost и MetaProductionHost — Require/Issue/Destroy. Ресурсы допускают штатные выдачу, требования, оплату и возврат; production definitions выдаются в локальные кошельки; hosts выдаются как Token и удаляются при cleanup/rollback Activity. Каталоги и recipes используются как настройки и не получили разрешений на операции, которые над ними не выполняются. Общая проверка Economy сохранена.

**Existing System Fit:** начальной выдачей владеют EconomyLocalWalletOwnerRuntime и ActivityServiceCore.SeedMountedWallet; проверкой разрешений — EconomyServiceCore; резервированием, выдачей и возвратом покупки — существующий ProductionRuntime; удалением host Token при закрытии/неудачной подготовке — ActivityServiceCore.ReleaseMaterializedEntities/RollbackStartupWalletSeed. Изменены assets через Unity и явная команда `ChainRushMetaUIAuthoring.SetMetaEconomyOperations`; соответствующие разрешения также записаны в команды создания мета-контента. Код runtime/package не менялся.

**Best Practices:** разрешения задаются явно у authored asset под его существующий lifecycle; запрет операции не обходится особой веткой seed. Основания: `EconomyAssetData.AllowedOperations`, `EconomyServiceCore.ValidateOperation`, `ProductionRuntime.AppendInputRefund`, `ActivityServiceCore.RollbackStartupWalletSeed`.

**Проверка ограничена чтением:** сохранённые изменения десяти assets просмотрены; помимо масок Unity обновила inspector-only domainTypePreview у трёх definitions. Повторный Play Mode и тесты не запускались, успешный старт после этой правки не заявляется. `MMSoundManager` не изменялся.

### Зависание FrameworkStart — диагностика и согласованное исправление 2026-09-28

**Сообщённый симптом, дословно:** «пришлось принудительно закрыть юнити, там то ли утечка памяти, то ли еще что-то. В общем игра не запускается, бесконечное ожидание при запуске сцены FrameworkStart».

**Verified — имеющиеся свидетельства:** последний пользовательский запуск начался около 14:44:47. PlayerService зарегистрировал FrameworkProfile, шаг resolve-meta-participant завершился успешно с одним участником. Последняя запись GameFlow в Editor.log: `Starting executor 'GameFlowLaunchActivityExecutorData' for step 'launch-meta' runtime=2.` Её стек проходит через GameRuntimeHost.onInitialized → GameFlowEvent → EventBus → GameFlowServiceCore. Сообщения о завершении launch-meta или открытии FrameworkMain отсутствуют. Unity уже закрыта; стек зависшего процесса получить из неё сейчас нельзя. Найденный системный JetsamEvent относится к 11:47 и другому PID, поэтому он не доказывает утечку в запуске около 14:45.

**Verified — код:**

- EventBus.Trigger исполняет обработчик внутри AfterEventDispatch, затем вызывает ExitPhase.
- StartLaunchActivityExecutor при занятой фазе создаёт DeferredLaunchExecutionState и ставит ResumeDeferredLaunch в очередь.
- ResumeDeferredLaunch отмечает ReadyToLaunch и вызывает ProcessRuntime.
- UpdateRunningExecutor для готового DeferredLaunchExecutionState снова вызывает StartLaunchActivityExecutor.
- Во время исполнения очереди DeferredRuntimeWorkQueue.IsPhaseBlocked возвращает true из-за Flushing. Повторный StartLaunchActivityExecutor снова помещает запуск в эту же очередь. FlushPhase обходит её до опустошения; каждый круг создаёт новые временные объекты.
- В соседней ветке DeferredChildLaunchExecutionState уже используется другой путь: непосредственный ExecuteLaunchChildActivity после завершённого ожидания.
- Reset/удаление runtime очищает ExecutionState; ResumeDeferredLaunch проверяет IsDetached и точное соответствие записи исполнения. Эти проверки сохранять.

**Первоначальная inference до диагностического запуска:** повторное откладывание объясняло остановку на launch-meta, занятую главную нить и постоянные аллокации. Первый лог не содержал записи из ResumeDeferredLaunch или стека внутри повторного откладывания. Поэтому причина тогда не считалась установленной по evidence gate. Проверенная конкурирующая версия — ожидание UI/загрузки Main: open-main находится позже launch-meta, а в логе до него выполнение не дошло. Результат последующего диагностического запуска приведён ниже.

**Цель согласованной диагностики:** получить свидетельство исполнения ветки после ResumeDeferredLaunch. Не более трёх сообщений на исполнение шага; без изменения очереди, запуска Activity и новых автоматических попыток. Диагностический код удалить после получения свидетельства. Unity, Play Mode и тесты самостоятельно не запускать.

**Диагностический шаг согласован пользователем и завершён:** временный **minimum seam** добавлял три метки `[GameFlow.LaunchDiagnostic]`: Deferred, Resumed, DeferredAgain. Счётчик в StepRuntimeState ограничивал их тремя на исполнение шага. Пользователь запустил FrameworkStart; агент прочитал новый лог. После получения свидетельства helper, поле, сброс и точки вызова удалены. Исправление исполнения затем отдельно согласовано и внесено, как описано ниже. Unity, компиляция и тесты агентом не запускались.

**Verified — подтверждение причины по новому Editor.log, завершённому в 14:59:19:**

```text
1335: [GameFlow.LaunchDiagnostic] stage=Deferred runtime=2 template='chainrush.meta.flow' step='launch-meta' execution=1 frame=1.
1360: [GameFlow.LaunchDiagnostic] stage=Resumed runtime=2 template='chainrush.meta.flow' step='launch-meta' execution=1 frame=1.
1383: [GameFlow.LaunchDiagnostic] stage=DeferredAgain runtime=2 template='chainrush.meta.flow' step='launch-meta' execution=1 frame=1.
```

Стек DeferredAgain: EventBus.Trigger (выход из рассылки, строка 89) → DeferredRuntimeWorkQueue.ExitPhase → FlushPhase → callback StartLaunchActivityExecutor → ResumeDeferredLaunch → ProcessRuntime → ProcessActiveContainer → UpdateRunningExecutor → StartLaunchActivityExecutor → TraceDeferredLaunch. Все три сообщения принадлежат одной попытке в первом кадре. Это подтверждает именно повторное откладывание запуска во время исполнения той же очереди. Условие Flushing остаётся истинным до опустошения очереди, но каждый callback снова добавляет работу; цикл не завершается. Конкурирующее объяснение ожиданием загрузки Main не соответствует зафиксированному стеку. Рост удерживаемой памяти отдельно не доказан; цикл создаёт постоянные временные аллокации. До исправления свидетельство сохранено здесь; успешное исполнение после исправления ещё не проверялось.

**Working model — исправление согласовано пользователем и внесено:** в `Scripts/Core/GameFlow/Internal/GameFlowServiceCore.cs` фактический вызов ActivityLauncher.LaunchResolved и обработка его результата выделены в ExecuteLaunchActivity. StartLaunchActivityExecutor сохраняет проверку контекста и первоначальное откладывание, затем вызывает ExecuteLaunchActivity. В UpdateRunningExecutor готовый DeferredLaunchExecutionState вызывает ExecuteLaunchActivity напрямую, аналогично существующему пути дочерней Activity. При фактическом исполнении повторно проверяются действующий контекст/участники. Отмена, Reset, корреляция и проверки устаревшего callback сохранены. DeferredRuntimeWorkQueue, EventBus, ActivityLauncher, pooling и игровое authoring ради этого дефекта не изменялись.

**Existing System Fit:** EventBus владеет границей рассылки; DeferredRuntimeWorkQueue — исполнением отложенных действий; GameFlowServiceCore — состоянием шага и продолжением его исполнения; ActivityLauncher/ActivityService — фактическим запуском и жизненным циклом Activity; UIFlow загружает Main только на следующем соответствующем шаге. После подтверждения трассой изменён только потребитель очереди; новых владельцев нет.

**Best Practices:** отложенное продолжение исполняет работу после уже пройденной границы, а не возвращает её на ту же проверку отсрочки. В этом проекте такой способ уже используют GameFlowServiceCore.ExecuteLaunchChildActivity, CapabilityHostSkillController.FlushDirtyHosts и ActivityProductionOutputMaterializationModule.FailIssuedOutputImmediate. Это конкретные исходные образцы для исправления. Существующая проверка ActivitySequence в GameFlowServiceEditModeTests начинает последовательность прямым Add, поэтому её содержание не подтверждает путь запуска через GameFlowEvent. Тесты не запускались.

**Состояние после исправления:** выполнен просмотр diff и путей вызова; диагностического поля/helper/сообщений больше нет. Компиляция, Unity, Play Mode и тесты не запускались по решению пользователя. Успешный старт после исправления ещё не подтверждён запуском.

### Пользовательский запуск: пустое содержимое вкладок, 2026-09-28

**Сообщённый симптом, дословно:** «Смотри логи запуска, там какая-то ошибка + игра загружается, но внутри разделов табов ничего не отображается».

**Verified — запуск:** в новом Editor.log шаги launch-meta, await-meta-ready и open-main завершились; FrameworkMain загружена. Это свидетельство прохождения ранее зависавшего запуска Activity. Единственное исключение этого запуска — SerializationException в MMSoundManagerSettingsSO.LoadSoundSettings → MMSoundManager.Start; пользователь исключил MMSoundManager из работы, изменений нет.

**Verified — одновременно запускается прежний путь:** перед FrameworkMain загружена старая Loading, после неё — старая Start. В редакторе `EditorPrefs["Game/Play Game"] = true`. `Assets/Editor/DefaultSceneLoader.cs:21–24` при BeforeSceneLoad вызывает загрузку Loading при включённом переключателе. У SceneLoader старой Loading задано Start / OnAwake / Additive / asynchronous. Таким образом, запуск из FrameworkStart не был изолирован от прежнего запуска. Выключен существующий пункт меню Game → Play Game; код загрузчиков и исходные сцены сохранены.

**Verified — настройка новой сцены:** у `Canvas/MainPanel/Content/{HeroUI,UnitUI,BattleUI}/Content` было `activeSelf=false`. Кнопки вкладок имеют корректные selectEvent → SetActive(true) на родительские HeroUI/UnitUI/BattleUI и deselectEvent → SetActive(false). Serialized-ссылок на три внутренних Content у компонентов новой сцены нет. TabManager/TabSelectButton не включают дочерние объекты рекурсивно. У общего MainPanel CanvasGroup alpha=1. Проверена ближайшая альтернативная причина видимости — прозрачность общего контейнера: она не скрывает содержимое. Пустой результат Economy также не объясняет скрытие всех статических элементов внутри выключенных Content.

**Working model — исправлен authoring:** три внутренних Content включены в FrameworkMain. Видимость разделов по-прежнему принадлежит существующим событиям TabSelectButton на родительских объектах. Выполнена явная команда `ChainRushMetaUIAuthoring.SetMetaTabContentActive`; то же исходное состояние добавлено в ConnectMetaTabs. Unity сохранила ровно три изменения m_IsActive: 0 → 1. Новых runtime-компонентов и автоматического исправления при запуске нет.

**Existing System Fit:** DefaultSceneLoader владеет редакторским переключением на прежний запуск; GameFlow/UIFlow — новым запуском и загрузкой Main; TabManager выбирает вкладку, TabSelectButton вызывает authored события видимости. EconomyAssetListSource → EconomyUIRequestAdapter → EconomyListView владеют содержимым списков; этот путь не изменён. Изменения ограничены настройкой редактора, новой сценой и её явным authoring.

**Best Practices:** включённость содержимого задаётся в сцене, а переключение разделов — одним существующим владельцем на корнях. Основание: `TabManager.Start/SelectTab`, `TabSelectButton.SetSelected` и уже настроенные события SetActive. Отдельный контроллер видимости или обход Economy для этого не нужен.

**Граница свидетельств / Inference:** проверены лог пользовательского запуска, сохранённая сцена и связи компонентов в Edit Mode. Состояние коллекций и порядок ответов Sources в завершившемся Play Mode не записаны; отсутствие дополнительных проблем загрузки данных не установлено. После изменения сцена не запускалась, визуальное подтверждение и тесты не выполнялись.

### Ошибка загрузки уровня Survive, 2026-09-28

**Сообщённый симптом, дословно:** «Смотри логи запуска. Возникает ошибка при загрузке уровня».

**Verified:** в последнем пользовательском запуске FrameworkLoading загружена, resolve-participants завершён. На launch-autobattle Addressables выдаёт InvalidKeyException: `No Location found for Key=5bd6fee205c3b4879a9556c61577b57a`, указывая существующий `Assets/Game/Activities/Autobattle/Space/SurviveSpace.prefab` (Editor.log:4423). Стек: GameFlowServiceCore.ExecuteLaunchActivity → ActivityLauncherCore.StartResolvedLaunch → ActivityServiceCore.StartCore → ActivityPrefabSpaceProvider.BeginPrepare → Addressables.LoadAssetAsync. Затем ActivityLauncher сообщает `Activity prefab space addressable load failed`, GameFlow завершает шаг и контейнер Survive с ошибкой.

**Причина установлена:** AutobattleActivity ссылается на действительный GUID SurviveSpace, но префаб отсутствовал в Addressables. Чтение текущих AddressableAssetSettings через Unity подтвердило `FindAssetEntry(guid, true) == null`. Ближайшие альтернативы проверены: файл и GUID существуют; выбран Play Mode Script `Use Asset Database (fastest)`, поэтому для этого запуска причина не в устаревших собранных bundles. Для DistanceActivity существующий AutobattleSpace уже зарегистрирован в группе ChainRush-Activity-Autobattle.

**Working model — исправлена настройка контента:** явной командой Unity Editor зарегистрирован только SurviveSpace в существующей группе ChainRush-Activity-Autobattle, address равен пути префаба, label — существующий `activity-space`. Использованы AddressableAssetSettings.CreateOrMoveEntry, SetLabel и сохранение средствами Unity. Diff содержит одну новую запись группы; GUID, prefab, Activity, runtime-код и параметры загрузки не изменены. Автоматической регистрации при запуске нет.

**Existing System Fit:** ActivityData выбирает prefab space; ActivityPrefabSpaceProvider владеет загрузкой, созданием пространства и его освобождением; Addressables разрешает ключ через authored каталог; ActivityServiceCore наблюдает подготовку и передаёт отказ; ActivityLauncher и GameFlow завершают запрос/шаг. Исправлен каталог у владельца адресации, без обхода ошибки в этих сервисах.

**Best Practices:** префаб, загружаемый по AssetReference, должен быть явно включён в Addressables. Здесь сохранён существующий способ authoring: `ChainRushAutobattleVerticalSliceAuthoring.ConfigureAddressable` и запись AutobattleSpace в той же группе; создание второго загрузчика или поиск через AssetDatabase в runtime не требуется.

**Граница проверки:** причина отказа доказана логом и настройками до изменения; сохранённая регистрация просмотрена после изменения. Успешная загрузка уровня после правки пока не подтверждена. Play Mode, тесты и сборка Addressables не запускались. MMSoundManager не изменялся.

### Возврат из Loading: владелец кошелька прогресса, 2026-09-28

**Сообщённый симптом, дословно:** «Смотри логи запуска: уровень вернулся в главное меню с ошибкой».

**Verified — пользовательский запуск:** после регистрации SurviveSpace шаг launch-autobattle завершён (Editor.log:6382); пространство подготовлено до Ready, выполнен seed производственных hosts. Затем await-autobattle-ready и контейнер Survive завершились с ошибкой (6661, 6690). Причина подготовки Activity: `Objective progress wallet is unavailable.` (7597). Стек возврата в Main проходит через GameFlowServiceCore.PublishLaunchSnapshot → ActivityLoadingPresentationController.OnEvent → onFailed → GameFlowEventTrigger → активация Meta. Это заданная реакция Loading на отказ подготовки, а не самостоятельная ошибка выбора сцены.

**Verified — цепочка отказа:** ActivityServiceCore сначала выполняет SeedWallets, затем RegisterTeamObjectives. ObjectiveNumericTargets.Attach/Bind строит ресурсный запрос через ObjectiveLongTargetProgressionData.CreateQuery; единственный источник указанного сообщения — отсутствие однозначного кошелька у разрешённого владельца. В Level01ReplenishmentObjective и Level02ReplenishmentObjective источник прогресса и observedOwner используют AuthoredOwner со ссылкой на FrameworkProfile. EconomyOperationOwnerBindingData.Resolve возвращает сам PlayerData. Activity монтирует кошелёк на ParticipantEconomyOwner — зарегистрированный Player, что подтверждено roster в логе и seed hosts. Economy хранит их как разные объектные владельцы: TryGetWalletInstances сначала ищет точную ссылку; общий StableSimulationKey не объединяет зарегистрированные списки кошельков.

**Проверенная альтернатива:** ошибка не вызвана отсутствием LevelProgress при нулевом прогрессе — CreateQuery сначала разрешает сам кошелёк, а запрос допускает нулевой баланс. ActivityWalletTag в Objective совпадает с тегом кошелька обеих команд; кошельки монтируются до регистрации Objectives. Чтение оставшегося состояния после выхода из Play Mode подтвердило отдельные PlayerData и Player с одним ключом player:FrameworkProfile; у PlayerData четыре постоянных кошелька без ActivityWallet. После закрытия Activity временный кошелёк уже освобождён, поэтому текущий снимок не выдаётся за снимок момента отказа.

Та же AuthoredOwner-ссылка используется в Level01PopulationAgent и Level02PopulationAgent: в условии применимости и PopulationProgressData. Исправление только одного условия Objective оставит неверное чтение в остальных потребителях.

**Working model — решение пользователя:** «Пусть прогресс начисляется обоим игрокам». У каждого участника свой LevelProgress в его ActivityWallet. Для Survive оба используют время этой Activity; для Distance — смещение одного выбранного героя. Враги читают кошелёк своего участника через ContextOwner. Предложение RuntimeOwner и IEconomyRuntimeOwnerSource удалено из реализации; PlayerData, PlayerService и общий owner binding не менять.

| Файл | Конкретное действие |
|---|---|
| `AutobattleActivity.asset`, `DistanceActivity.asset` | Добавить команде бота тот же SurviveProgressObjective либо DistanceProgressObjective, который уже настроен у игрока. Шаблон общий, runtime Objective и кошелёк отдельные для каждого участника |
| `EnemyAnalytics.asset` | Подключить существующие ElapsedSimulationMetric и HeroDisplacementMetric; сохранить интервалы и бюджеты. Общая конфигурация используется обоими уровнями, а Objective каждого уровня читает соответствующую метрику |
| `HeroDisplacementMetric.asset` | Задать scope Activity при прежнем фильтре HeroRole: обе перспективы наблюдают одну Entity героя независимо от его владельца. Политику смещения, ось и точность сохранить |
| `LevelProgressMetric.asset` | Задать scope Participant: наблюдение пустого поля читает прогресс бота, а не сумму двух копий ресурса |
| `Level01EnemyBrain.asset`, `Level02EnemyBrain.asset` | Добавить существующие EconomyOperationDecompOpData Issue/Consume для LevelProgress и ActivityWalletTag. В DecisionGraph использовать FactType EconomyAmount и CompareOperation Equal. Так операции обслуживают требование `LevelProgress == значение аналитики`; ветка `LevelProgress >= 1000000` в Objective врагов остаётся наблюдаемым фактом и не получает способа искусственно начислить конечный прогресс |
| `Level01ReplenishmentObjective.asset`, `Level02ReplenishmentObjective.asset` | Задать ContextOwner для источников прогрессивного порога и observedOwner во всех условиях прогресса, включая активацию, завершение и Reset. Удалить ссылку FrameworkProfile из этих привязок |
| `Level01PopulationAgent.asset`, `Level02PopulationAgent.asset` | Задать ContextOwner у условия применимости и PopulationProgressData. Состав, объём и интервалы сохранить |
| `Tools/Authoring/ChainRushMetaUIAuthoring.cs` | Добавить явную команду SetProgressForBothParticipants для перечисленных настроек. Она исполняется в Edit Mode и не входит в runtime игры |

**Existing System Fit:** ActivityService монтирует отдельный временный кошелёк каждому runtime-участнику и создаёт экземпляры общих Objective templates. Analytics владеет временем и историей позиции; существующий Knowledge предоставляет обеим перспективам сведения о позиции героя (в PlayerKnowledge/EnemyKnowledge нет исключающих фильтров). ObjectiveNumericTargets вычисляет порог и ведёт подписки; Orchestration выбирает Issue/Consume, EconomyOrchestrationEndpointRuntime перечитывает текущий баланс перед изменением. Economy остаётся единственным владельцем записи. Population и Objectives врагов используют ContextOwner и освобождают подписки по существующему lifecycle. PlayerService, сохранение профиля и UI вне этой правки.

**Best Practices:** использовать существующий контекст участника вместо добавления нового способа преобразования определения в runtime-владельца. Один писатель на каждый кошелёк исключает конкуренцию за баланс; общая метрика задаёт одинаковый смысл прогресса. Конкретные источники: ActivityServiceCore.SeedWallets/RegisterTeamObjectives; ObjectiveNumericTargets.OpenAnalytics; ActivityAnalyticsDomainState.TryPrepareMetric и ActivityAnalyticsCalculation.ReadMovement; EconomyOperationDecompOpData.TryCreateBalanceRequest; OrchestrationDecisionGraphEvaluator.MatchesNode. Существующий CompareOperationDecisionConditionData ограничивает доступные способы достижения требования: наблюдение конца прогресса не становится командой его начисления.

**Согласованность:** обновления двух кошельков проходят существующие очереди и бюджеты. Атомарное одновременное обновление не обещается; ранее согласованное отставание записи от Analytics сохраняется. Возврат героя назад уменьшает требуемое значение в обоих кошельках через Consume. Одного писателя сохранять для каждого баланса отдельно.

**Статус:** authoring выполнен через Unity, изменены 11 существующих assets. Чтение сохранённых определений подтвердило Progress Objective у обеих команд каждого уровня, ContextOwner во всех шести привязках каждого Objective врагов и по два добавленных Economy-оператора в каждом EnemyBrain. Источники прогрессии, состав и объёмы сохранены. Runtime-код не менялся. Тесты и Play Mode не запускались; результат нового пользовательского запуска ещё не получен.

### FrameworkGameplay открыт, основной геймплей не начинается — диагностика 2026-09-28

**Сообщённый симптом, дословно:** «Смотри логи запуска, сцена уровня запускается, но сам уровень не начинается».

**Verified — лог:** последний завершённый запуск прошёл ActivityReady Autobattle (Editor.log:9705), переход в FrameworkGameplay (9908), запуск Board и ActivityReady Board (10349). Прежнего отказа разрешения кошелька прогресса в этом запуске нет. Пользователь затем повторил запуск и оставил Play Mode для чтения состояния. Агент игру и тесты не запускал.

**Verified — снимки пользовательского запуска:** Play Mode включён, пауза выключена, timeScale=1. AutobattleActivity и BoardActivity имеют State=Running. Прогресс начисляется обеим сторонам (в снимке обоим было назначено 85222). Производители зарегистрированы и принимают заказы, но их очереди и активные pipelines пусты. Следовательно, остановка загрузки сцены или общая пауза симуляции не объясняют этот случай.

1. **Герой — пропущен seed формы.** HeroPopulationAgent использует SpawnArea из ActivityWallet своего участника. В runtime у игрока SpawnArea=0, у бота SpawnArea=1. В обоих authored уровнях форма есть только в seed бота. Стартовое назначение героя сохранено: RequestedCount=1, PlannedCount=0, Groups=0, Placements=0, State=Materializing; найден один производитель и два метода каталога, категория содержимого готова. Population.TryResolveShapes читает Spatial-проекцию кошелька; SpatialPopulationDistributionAlgorithmData.Run исключает правила без положительного количества формы. Нет формы — нет шаблона размещения. ProductionSession завершает пустой проход, RequireFullVolume оставляет назначение ожидающим. Заказ в Production ещё не поступал. **Исправить authoring:** добавить SpawnArea ×1, Stack, materialization None в ActivityWallet команды игрока у AutobattleActivity и DistanceActivity. Не менять форму бота и не обходить проверку доступности форм.
2. **Доска — нет первого хода.** В runtime BoardTurnToken=0 и Experience=0. BoardPopulationObjective неактивен: он требует BoardTurnToken≥1 и пустую доску. TurnTokenObjective ждёт входной Experience для существующего рецепта (первый выпуск стоит 6); это ожидание видно в процессе зависимости. Ни один Activity seed не выдаёт стартовый BoardTurnToken. В исходном BoardUi.OnMMEvent(LevelLoadEvent) первый RefreshBoard вызывается при загрузке (строка 110). **Исправить authoring для сохранения первоначального заполнения:** выдать игроку BoardTurnToken ×1 в ActivityWallet при старте обоих уровней, далее сохранить обычный путь опыта и оплаты хода. Это не возвращает гарантии конкретного содержимого первого заполнения.
3. **Враги — повторные отмены назначения.** В последовательных снимках количество Cancelled назначений Level01 Population росло; отдельный снимок содержит 5506 отмен с точным сообщением `Orchestration process dependency was detached.` и одно текущее Preparing. Его производитель доступен, очередь пуста; текущий Population ещё собирает кандидатов (Pending, LastWorkCount=256). **Verified:** выполнена ветка TryCancelDetachedProcesses в OrchestrationProcessRuntime, которая публикует именно это сообщение. **Inference:** изменение ревизии составного факта прогресса переводит родительский процесс без endpoint из WaitingDependencies в Planning через ApplyUnsatisfiedFactRevision; повторный выбор кандидата снимает ещё нужную зависимость Population через TryPrepareNextCandidate → TryReleaseDependencies → ClearDependencies. Точный инициатор повторного планирования в этом запуске ещё не зафиксирован; причина цикла не считается установленной. Не менять Orchestration на основе одной гипотезы.

**Existing System Fit:** ActivityService владеет seed и запуском/закрытием; Economy — доступностью форм и ресурсов; Population — фиксацией состава, расчётом размещений и передачей заказов; Production — принятыми заказами; Activity materialization — появлением объектов. ObjectiveService владеет активацией требований. OrchestrationProcessRuntime/ProcessGraph владеют зависимостями и отменой своих назначений; AgentService исполняет эту отмену и освобождает Population reads/markers/доступ к регионам. GameFlow/UIFlow уже завершили подготовку. MMSoundManager вне работы.

**Best Practices:** восстановить стартовые входы существующего pipeline через seed, не добавлять обходную выдачу героя или прямое заполнение доски. Источники: PopulationAgent.TryResolveShapes, SpatialPopulationDistributionAlgorithmData.Run, PopulationProductionSession.Advance, BoardPopulationObjective.asset и исходный BoardUi.OnMMEvent(LevelLoadEvent). Для отмен различать обновление наблюдения и изменение требуемого результата: проверять конкретный инициатор по существующим событиям/трассировке до изменения lifecycle; источники OrchestrationProcessRuntime.ApplyUnsatisfiedFactRevision/TryPrepareNextCandidate/TryCancelDetachedProcesses и OrchestrationProcessGraph.ClearDependencies.

**Verified — причина отмен установлена по существующей истории событий.** `OrchestrationStrategyViewerRuntimeTracker` уже записывал последние 256 переходов; прочитан его `GetEntries`, без включения диагностики и новых подписчиков. Фрагмент одного пользовательского запуска (участник 5):

| CaptureIndex | Процесс | Переход | Ревизия | Результат наблюдения |
|---|---|---|---|---|
| 213919 | 2:44607, Population | Starting → Running | 0 | false; endpoint 2:agent-assignment:8915 |
| 213924 | 2:42541, составной факт | WaitingDependencies → Planning | 414 | false; endpoint отсутствует; сообщение пустое |
| 213929 | 2:44607, Population | Running → Cancelled | 0 | false |
| 213930 | 2:42541, составной факт | Planning → WaitingDependencies | 414 | false; ожидание зависимостей |
| 213943 | 2:44612, новый Population | Starting → Running | 0 | false; endpoint 2:agent-assignment:8916 |
| 213948 | 2:42541, тот же составной факт | WaitingDependencies → Planning | 415 | false; endpoint отсутствует; сообщение пустое |
| 213953 | 2:44612, Population | Running → Cancelled | 0 | false |

Та же последовательность сохранена для ревизий 416–420. В `ApplyUnsatisfiedFactRevision` именно ветка WaitingDependencies без собственного endpoint вызывает SetFactRevision → Planning с пустым сообщением. Следующий TryPrepareNextCandidate снимает незавершённую зависимость и TryCancelDetachedProcesses отменяет её назначение. Ближайшее конкурирующее объяснение — отказ/блокировка дочернего процесса — не соответствует событиям: ребёнок Running, его ревизия 0, а родитель переходит с пустым сообщением; штатная ветка Blocked публикует `Selected candidate dependency is blocked.`. Изменение самого требуемого факта также не объясняет этот фрагмент: идентичность родителя сохраняется. Предыдущая гипотеза выше теперь подтверждена в части обработки ревизии и отмены; конкретный источник каждой инвалидации отдельно этой историей не записан.

**Working model — пользователь согласовал все три исправления и остановку Play Mode; правки внесены:**

1. В `Scripts/Core/Orchestration/Runtime/OrchestrationProcessRuntime.cs`, `ApplyUnsatisfiedFactRevision`: для WaitingDependencies подтверждать новую ревизию через AcknowledgeFactRevision и сохранять выбранного кандидата/зависимости независимо от наличия собственного endpoint. Обычное TryReconcileDependencies продолжает наблюдать выполнение факта, завершение/ошибку/блокировку детей. Изменение семантического ключа требования, его снятие, Reset и явная отмена сохраняют нынешний lifecycle. Не менять Production, Population, Objectives, ProcessGraph и бюджет ради устранения этого цикла.
2. В `Assets/Game/Activities/Autobattle/Definition/AutobattleActivity.asset` и `DistanceActivity.asset` добавить в ActivityWallet команды игрока SpawnArea ×1 и BoardTurnToken ×1, обе записи Stack, materialization None. Форма позволяет существующему Population разместить выбранного героя; токен активирует первое заполнение через существующий Board Objective. Кошельки бота не менять.
3. В существующем `Tools/Authoring/ChainRushMetaUIAuthoring.cs` добавить явную команду этих двух seed-правок и использовать ту же настройку в ConnectRosterAndHeroProduction, чтобы повторное authoring сохраняло полный стартовый набор. Это редакторская команда, не исправление данных при запуске игры. Сохранение assets выполняет Unity после выхода из Play Mode.

**Existing System Fit — уточнение исправления:** обработкой новой ревизии владеет OrchestrationProcessRuntime; ProcessRecord уже предоставляет AcknowledgeFactRevision без сброса выбранного метода. TryReconcileDependencies уже использует его для невыполненного факта; выровнять поведение ReconcileRootDemands/TryAddDependency через общий ApplyUnsatisfiedFactRevision. Отмена ненужных процессов остаётся у ProcessGraph/ProcessRuntime; принятые заказы и компенсация — у Production. Начальные ресурсы остаются authored seed ActivityService.

**Best Practices — конкретный образец:** сохранять выполняемую работу при обновлении наблюдения того же требования. В этом коде так уже устроены Running/Starting/AwaitingObservation в ApplyUnsatisfiedFactRevision и прямое обновление ревизии в TryReconcileDependencies. Существующие проверки `ProcessRuntime_RunningAttemptPreservesCorrelationAcrossUnsatisfiedFactRevision`, `ProcessRuntime_WaitingProcessCompletesWhenDesiredFactIsSatisfiedExternally` и `ProcessRuntime_BlockedDependencySelectsNextCandidateAndCancelsDetachedSubgraph` просмотрены как описание соседних контрактов; они не запускались. Исправление переносит существующее правило на ожидающий метод без собственного endpoint, сохраняя обработку действительно заблокированной зависимости.

**Статус после реализации:** по явному разрешению пользователя Play Mode остановлен. В ApplyUnsatisfiedFactRevision ветка WaitingDependencies объединена с Running/Starting/AwaitingObservation и вызывает AcknowledgeFactRevision без сброса метода. Явная команда `ChainRushMetaUIAuthoring.SetInitialGameplaySeed` выполнена через Unity; обе Activity сохранены. Её общий helper ConfigureInitialGameplaySeed также подключён к ConnectRosterAndHeroProduction. Сравнение с копиями файлов непосредственно перед исправлением показало ровно две новые записи seed в каждом уровне, правку одной ветки Orchestration и изменения существующего authoring-файла. Кошельки бота и остальные параметры уровней не менялись. Диагностические профили не менялись. Предложение включать OrchestrationTrace снято: необходимая история уже существовала.

Тесты и повторный запуск игры не выполнялись. Свидетельства до исправления сохранены выше; успешный запуск после исправления ещё не подтверждён.

### Следующий пользовательский запуск — теги зон героя, 2026-09-28

**Запрос, дословно:** «Смотри логи запуска».

**Verified:** открытый процесс ChainRush пишет в `~/Library/Logs/Unity/Editor-prev.log` (файл переименован при запуске другого редактора; текущий Editor.log относится к BaseRun). В свежем запуске Autobattle получил ActivityReady (15800), FrameworkGameplay загружена (16003), Board получил ActivityReady (16444). Далее Production.CompleteYield → ProductionTokenOutputPreparedEvent → ActivityProductionOutputMaterializationModule → ActivityService.ApplyMaterialization → ProjectionBindingController.NotifyBound дошёл до компонентов зон героя.

**Verified — три отказа:** строки 16526 и 16561: SpatialMarkerProviderController не смог привязать `hero-water-production` и `hero-cola-production`, поскольку WaterHeroProductionZone и ColaHeroProductionZone не зарегистрированы. Строка 16596: SpaceRegionController не смог зарегистрировать объём с EnemyHeroProductionZone по той же причине. У всех трёх assets корректная ссылка на семейство `chainrush.autobattle.marker`; их GUID отсутствуют в `Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset`, который подключён к ChainRushGameRuntimeProfile. Эти же GUID используются в префабах Perfume/Tabasco. Ближайшая альтернатива — несовпадение ссылки на тег у потребителя — не соответствует проверенным ссылкам. TaxonomyRuntimeInstallerData регистрирует только authored список terms; TaxonomyRegistry.ValidateSelection отклоняет отсутствующие определения. Причина этих трёх сообщений — пропущенные регистрации существующих тегов.

**Конкретное исправление данных:** добавить WaterHeroProductionZone, ColaHeroProductionZone и EnemyHeroProductionZone в terms существующего ChainRushTaxonomyRuntimeInstaller. Сохранить те же assets, идентичности, семейство и ссылки префабов. Runtime-авторегистрация, новые теги, новые сервисы и изменение materialization не требуются.

**Existing System Fit:** TaxonomyRuntimeInstallerData/TaxonomyService владеют регистрацией определений; SpatialMarkerProviderController/SpatialShapeProviderController через существующий adapter публикуют зоны и маркеры; SpaceRegionController через запрос регистрирует объём в SpaceRegionService. Отказ проверки возвращается этим компонентам, они освобождают неудачную привязку или сохраняют невалидный handle. Projection уже дошёл до их привязки; это не отказ загрузки сцены.

**Границы свидетельств:** лог подтверждает прохождение старта дальше прежнего состояния до материализации героя, но сам по себе не подтверждает завершение заполнения Board и исправление всех назначений врагов. При первом чтении Play Mode был активен, при следующем чтении список Activity уже пуст; агент игру не останавливал и не перезапускал. MMSoundManager по-прежнему исключён из работы. В этом шаге игровые данные и runtime-код не менялись; тесты не запускались.

**Исправление выполнено после команды пользователя «Внеси»:** через Unity в Edit Mode добавлены три существующие ссылки в terms ChainRushTaxonomyRuntimeInstaller. Сравнение с копией до изменения показало ровно три добавленные строки; runtime-код, префабы и идентичности тегов не менялись. Unity сохранила asset. Тесты и игра не запускались; исчезновение ошибок в следующем пользовательском запуске ещё не подтверждено.

### ProfilerCaptures: после доски нет продолжения, низкая частота кадров — 2026-09-28

**Симптом, дословно:** «Ошибка ушла, но дальше доски ничего не происходит плюс всё сильно тормозит на уровне. Смотри ProfilerCaptures по этому поводу».

**Источник:** `ProfilerCaptures/Slime Lords_2026-09-28_19-44-26.data` и готовая `.morboo-profiler-diagnostic` выгрузка. Manifest: Completed, 788 кадров, 28 299 466 записанных samples. Исследован существующий захват; новая запись, запуск игры и тесты агентом не выполнялись. Через RawFrameDataView прочитан уже загруженный кадр 700; состояние Profiler и игры не менялось.

**Verified — время кадра:** участок 660–786 (127 кадров после появления доски) имеет среднее 112,123 мс, медиану 102,32 мс, p95 160,93 мс, среднюю частоту 8,92 FPS. Усреднение всего захвата даёт 24,2 FPS и скрывает этот участок более быстрыми кадрами меню. Время относится к записи в редакторе, включая накладные расходы Profiler.

**Verified — кадр 700, основной поток:**

| Наблюдение | Значение |
|---|---:|
| Длительность кадра | 103,4215 мс |
| OrchestrationService.ProcessPass, 2 вызова | 84,5897 мс |
| Внутри него Admission, 2 вызова | 67,2765 мс |
| IssueEntries.Execute, 2 вызова | 38,6508 мс |
| KnowledgeResourceSourceHandler на EconomyEntryChangeEvent, 2 вызова | 17,0566 мс |
| QueryWallet, 26 вызовов | 16,1369 мс |
| GC.Alloc | 146 670 выделений, 14 498 074 байта |
| GC.Collect | 0 вызовов |

Вложенные времена нельзя складывать. По ближайшему родительскому sample: 6 260 020 байт выделены внутри IssueEntries.Execute, 2 860 752 — внутри QueryWallet. Для просмотренного GC.Alloc стек вызовов не записан; точная строка доминирующих выделений из этих данных не установлена. Во всём захвате 28 111 379 GC.Alloc. Это свидетельство интенсивного создания временных объектов, а не доказательство утечки удерживаемой памяти.

**Проверенные конкурирующие объяснения:** на кадре 733 главный поток занят Update 175,307 мс, Orchestration — 173,246 мс; render thread ожидает команды главного потока. При этом отдельные большие кадры загрузки действительно включают RenderLoop/Profiler.SyncGlobalData. Для постоянных тормозов после загрузки подтверждена нагрузка игрового CPU-пути. Прежний бесконечный deferred-цикл не соответствует участку 660–786: максимальный flush 0,15 мс, после раунда очередь пуста, возраст работы 0 кадров.

**Verified — код вокруг дорогого пути:**

- EconomyOrchestrationEndpointRuntime.Start вызывает существующую Economy-операцию; IssueEntriesInternalCore → TryExecutePlain → ResourceEconomyModule.Commit уведомляет ResourceWatchHub, затем завершается выдача и публикуется EconomyEntryChangeEvent.
- EconomyServiceCore.SelectionQuerySubscription.RebuildBalanceWatch строит наблюдение по GetRegisteredAssetsSorted для каждого выбранного кошелька даже при заданном конкретном asset. Изменение баланса вызывает Notify → QuerySelection. Подходящее EconomyEntryChangeEvent отдельно перестраивает watch и ещё раз вызывает Notify.
- KnowledgeResourceSourceHandler.OnEvent передаёт дальше только владельца и признак изменения модификатора; ChangedAssets и конкретный Wallet не ограничивают RefreshOwnerSlices. Обновляются все известные кошельки этого владельца, в каждом Stack и Token, затем заменяются resource record sets.
- EconomyServiceCore.QueryWallet запрашивает балансы всех зарегистрированных assets и разрешает их taxonomy; exact-asset путь QuerySelection исследуется отдельно. Эти полные обходы объясняют возможное усиление нагрузки от частой выдачи одного ресурса. Какой именно участок создаёт основную часть 6,26 МБ внутри IssueEntries.Execute, пока является Inference; отдельное исправление runtime по одной этой гипотезе не вносить.
- SurvivePlayerAnalytics и EnemyAnalytics имеют reviewInterval=1. Их прогрессивные требования могут часто выдавать LevelProgress обоим участникам; принадлежность двух операций кадра 700 именно этому ресурсу не записана в Profiler и пока не считается Verified.

**Verified — граница диагноза отсутствия боя:** в выгрузке 17 CompleteYield, 73 SelectEntityTargetByQuery и 161 SelectActivityTarget; маркеров исполнения действий UseSkill/движения нет. Причину отсутствия подходящей цели, выпуска врагов или перехода мозга эти счётчики не различают. При чтении после запроса пользователя Play Mode уже false, Activity runtime отсутствует. Cause not established для «дальше доски ничего не происходит»; требуются снимки Objectives, Orchestration, Population и AIBrain из пользовательского запуска.

**Existing System Fit:** временные метрики принадлежат Analytics, желаемые значения — Objective, назначение операций — Orchestration, балансы и подписки — Economy, обновление наблюдаемых resource-срезов — Knowledge. Размещение — Population/SpaceRegion; поведение и исполнение атак — AIBrain/Skills. Оптимизации этих путей и изменение интервала ещё не согласованы и не внесены. Новый сервис, прямое начисление прогресса или обход кошельков не предлагаются.

**Приоритет пользователя:** «Сначала убери причины фризов». Диагностика отсутствия боя отложена; повторный ручной запуск для неё сейчас не требуется.

**Working model — ограниченная внутренняя оптимизация существующего чтения:**

1. `Scripts/Core/Economy/Internal/EconomyServiceCore.cs`: в QuerySelectionInternal перед чтением балансов ограничить определения существующими Asset/RequiredAssetTags запроса. Вынести нынешнюю сборку wallet items в общий приватный метод и использовать его для полного QueryWallet и отфильтрованного QuerySelection. Полный QueryWallet сохраняет расчёт вместимости и занятости всего кошелька; selection не строит ненужный результат вместимости. Stack/Token, runtime assets/tags, Available/Committed, сортировка, aggregation и handles проходят один прежний алгоритм сборки. Новый публичный контракт или отдельный упрощённый вычислитель не вводить.
2. В существующем SelectionQuerySubscription.RebuildBalanceWatch использовать тот же набор подходящих определений вместо всех зарегистрированных assets. Сохранять нынешние события, порядок уведомлений, переподключение и Dispose. Фильтр runtime tags не сужает наблюдение сырых балансов: смена runtime tags должна по-прежнему обновлять выборку.
3. `Scripts/Core/Knowledge/Internal/Sources/KnowledgeResourceSourceHandler.cs`: передавать Wallet уже существующих EconomyEntryChangeEvent/EconomyTagChangeEvent в обработку изменения владельца. По существующему ключу owner + wallet обновлять соответствующий CorpusSliceState и заменять только его record set. Изменение KnowledgeModifier сохраняет отдельную инвалидацию модификаторов. Открытие domain/регистрация Entity продолжают создавать полные начальные срезы; удаление, external key mappings и очистка остаются у нынешних владельцев.

**Existing System Fit — реализация:** EconomyServiceCore остаётся единственным сборщиком экономических выборок; приватное выделение общего кода не меняет API EconomyService, модули, транзакции или резервирование. Knowledge использует уже имеющиеся SlicesByKey и ReplacePartitionRecordSets. ActivityAnalytics, Objective, Orchestration, Production, ResourceWatchHub, GameFlow и игровые assets для этой оптимизации не меняются.

**Best Practices:** ограничивать чтение фильтром запроса до построения результата и обновлять только затронутую часть read model. В проекте точечный набор assets уже использует EconomyAssetWatchSubscription, а частичную замену с сохранением остальных resource-срезов — KnowledgeSourceHandlerContext.ReplacePartitionRecordSets. Эти существующие механизмы переиспользуются; новый кеш-сервис, отложенные уведомления и изменение частоты публикации прогресса не добавляются. Результаты после изменений требуют нового пользовательского профиля; нельзя обещать измеренное улучшение до его получения. Тесты не запускать без отдельного разрешения.

**Реализация выполнена по команде об устранении фризов:** изменены только EconomyServiceCore и KnowledgeResourceSourceHandler. GetSelectionAssets применяет существующие фильтры до запроса балансов и построения подписки. QuerySelectionInternal для ограниченного набора assets вызывает общий BuildWalletQueryItems; чтение без фильтра assets сохраняет QueryWallet и прежний краткоживущий кеш. QueryWallet сохраняет полный расчёт вместимости. Тело построения items от разрешения taxonomy до сортировки перенесено дословно, что подтверждено сравнением исходного текста до/после; это не запуск теста. Knowledge обновляет один существующий срез по BuildSliceKey, сохраняет external key mappings, замену record set и отдельную инвалидацию модификаторов. Общие события и порядок commit/уведомлений не менялись.

**Проверено чтением diff:** нет новых публичных типов/полей/контрактов, изменений игровых assets или интервала Analytics. Предыдущие staged-изменения EconomyServiceCore сохранены. Diff не содержит ошибок пробелов. Игра, профилирование и тесты агентом не запускались; фактические FPS/аллокации после оптимизации ещё не измерены. Отсутствие боя остаётся отдельным незавершённым диагнозом по решению пользователя сначала устранить фризы.

### Пользовательский Play Mode: исчезновение пространства и остановка выбора Heal, 2026-09-28

**Симптом, дословно:** «Ошибка ушла, но дальше доски ничего не происходит плюс всё сильно тормозит на уровне». После внутренних оптимизаций пользователь сообщил: «запустил плеймод». Выполнено только чтение его работающей сессии; запуск, пауза, остановка игры и тесты не выполнялись.

**Verified — текущее состояние:** AutobattleActivity (domain 2) и BoardActivity (domain 3) Running. Начальное Objective героя Completed, Perfume Entity 233 существует. Первое заполнение и выбор доски Completed. `board-selected-heal:economy:0` Blocked с сообщением `Process planning produced no executable candidates`. Производители включены и принимают заказы; очереди пусты. В снимке назначений врагов 290 Completed, 2 Cancelled, одно Preparing. История показывает завершение Population без прогресса факта, исключение кандидата, переход к другой ветке OR и повторную попытку после изменения наблюдения. Это не прежняя отмена работающего Population при каждой ревизии: в сохранённой последовательности сначала Running → AwaitingObservation → Planning (`Process made no strict fact progress`).

**Verified — пространство:** ActivityPrefabSpaceProvider хранит `_ready=True`, `_cancelled=False`, управляемую ссылку `_preparedRoot`, для которой Unity возвращает destroyed=True. Сохранённая `_preparedScene` имеет handle -57922, IsValid=False, isLoaded=False. ActivityService по-прежнему сообщает SpaceStatus Ready. Единственная загруженная обычная сцена — FrameworkGameplay. В ней нет SurviveSpace/SurviveWall; из активных объёмных зон осталась только EnemyHeroProductionZone героя. Чтение штатного запроса Level01PopulationAgent даёт Ready и **0 регионов**. В исходном SurviveSpace asset стена присутствует, её SpaceRegionController тоже присутствует; prefab не пустой.

**Verified — причинный путь:** ActivityPrefabSpaceProvider.TryInstantiateAndBind помещает root в текущую сцену через MoveGameObjectToScene. В этом запуске пространство подготовлено до перехода в Gameplay: ActivityReadyEvent записан в Editor-prev.log:23130, загрузка FrameworkGameplay — :23333. SurviveFlow открывает Gameplay с LoadSceneMode.Single, UIFlowServiceCore выполняет SceneManager.LoadScene с этим режимом. Сцена подготовки выгружается вместе с root; SpaceRegionController.OnDisable освобождает регион. Activity не закрыта, provider не отменён и продолжает хранить уничтоженную ссылку. PopulationRegionSession.Select при отсутствии регионов возвращает Pending; PopulationAgent в AllowPartialVolume закрывает приём без выпусков. Совокупность сохранённой уничтоженной ссылки, выгруженной сцены, порядка событий и пустого штатного запроса подтверждает потерю пространства при смене UI-сцены.

**Проверенные конкурирующие объяснения:** у EnemySpawner есть SpawnArea ×1; все шесть записей EnemyWaveCatalog успешно разрешаются существующим TryReadCurrentMethod; производитель enabled/acceptsOrders, queue=0. Причина не в пропущенной форме, выключенном производителе или неподходящем выходе рецепта. Наличие нужного региона в исходном prefab исключает отсутствие authored зоны. Повторные попытки подтверждены; их отдельный вклад в время кадра после оптимизации не измерялся.

**Verified — Heal:** BoardHost Entity 227 содержит семь зарегистрированных навыков, включая IssueHeal. В BoardOrchestration подключены только BoardEconomyState, BoardProductionState, BoardProjectionState. Runtime ModuleResults домена Board также не содержит ISkillHostStateModuleResult. BoardBrain назначает SkillDecompOpData для выбранных Heal Tokens; этот оператор возвращает false до построения вариантов, если результата Skills-модуля нет. Поэтому навык зарегистрирован, но планировщику недоступен. Это объясняет Blocked после выбора; отсутствие целей лечения в Autobattle не является причиной данного отказа Board.

**Working model — конкретные исправления, до согласования lifecycle код не менять:**

1. **Изменить** `MorbooFrameworkPackage/Scripts/Core/Activities/Internal/ActivityPrefabSpaceProvider.cs`: в Play Mode после Instantiate сохранить root через Unity DontDestroyOnLoad до привязки consumers. Lifetime prefab-пространства принадлежит Activity, поэтому смена UI-сцены не уничтожает его. Вне Play Mode сохранить существующее размещение в активной сцене. Удалить `_preparedScene` и его присваивания: поле нигде не читается. Новых менеджеров, authored режимов и зависимостей от GameRuntimeHost не добавлять.
2. **Сохранить** нынешние CancelPreparedSpace/ReleasePreparedSpace и DestroyPreparedRoot: отвязать consumers и navigation, освободить marker composition, уничтожить root, освободить Addressables handle. Эти пути продолжают вызываться при ошибке подготовки, отмене и закрытии Activity. UIFlow, GameFlow и ActivityUISpaceProvider не менять.
3. **Добавить asset существующего типа** SkillsOrchestrationModuleData: `Assets/Game/Activities/Board/Orchestration/Modules/BoardSkillsState.asset`. **Добавить ссылку** на него в modules `BoardOrchestration.asset`. Создание и сохранение через Unity, `.meta` создаёт Unity. SkillDecompOpData, Skills, BoardHost и рецепты ради этого не менять.
4. После реализации зафиксировать diff. Самостоятельно игру и тесты не запускать. Результат следующего пользовательского запуска и повторный профиль будут отдельными свидетельствами; текущий диагноз не означает проверенного устранения всех проблем боя и производительности.

**Existing System Fit:** ActivityServiceCore владеет подготовкой/commit/закрытием пространства и вызывает существующий IActivitySpaceProvider. ActivityPrefabSpaceProvider владеет prefab root, Addressables handle, marker composition, navigation и binding consumers. SpaceRegionController/SpaceRegionService владеют регистрацией и освобождением зон. UIFlow владеет только показом и сменой UI-сцен. ActivityUISpaceProvider имеет отдельный lifecycle через UIHandle и остаётся вне этой правки. За чтение зарегистрированных навыков для планирования отвечает существующий SkillsOrchestrationModule; SkillDecompOpData выбирает способ исполнения, SkillOrchestrationEndpointRuntime и Skills исполняют и завершают навык. Никакой новый исполнитель для Heal не требуется.

**Best Practices:** ресурс должен жить до завершения операции владельца, а освобождаться тем же владельцем. В проекте это уже выражено IActivitySpaceProvider.CancelPreparedSpace/ReleasePreparedSpace и ActivityServiceCore.ReleaseSpaceState; root должен выдерживать переходы UI до этих вызовов. Существующий GameRuntimeHost.Awake применяет тот же встроенный Unity-механизм DontDestroyOnLoad для объекта с жизнью дольше сцены. Подключение Skills-модуля использует штатные ActivityOrchestrationConfigData.Modules → OrchestrationParticipantRuntime.RebuildModuleResults → SkillsOrchestrationModule. Это исправление композиции существующих механизмов, а не отдельный путь обработки Heal.

**Граница согласования:** сохранение root между сценами меняет lifecycle общего ActivityPrefabSpaceProvider и не было явно описано ранее. По правилу 11 PROJECT_RULES.md требуется явный ответ до этой правки. Для реализации также требуется разрешение остановить текущий пользовательский Play Mode; повторный запуск и тесты в это разрешение не входят.

**Реализация выполнена после явного согласования обоих исправлений:** пользователь затем сообщил, что уже остановил Play Mode; перед authoring подтверждено `isPlaying=False`, `isPlayingOrWillChangePlaymode=False`. В ActivityPrefabSpaceProvider root сохраняется через DontDestroyOnLoad только в Play Mode, до binding; вне Play Mode остаётся MoveGameObjectToScene. Неиспользуемое поле `_preparedScene` удалено. Существующие отвязка, уничтожение и освобождение Addressables не изменены.

Unity создала `BoardSkillsState.asset` и его `.meta` (GUID fb8202b90bc1f4592850d6d606a15482), сохранила ссылку четвёртым модулем BoardOrchestration. При сохранении Unity также записала пустые существующие поля externalFactBindings и references; значения прочих настроек сохранены. Сравнение с копиями до правки и `git diff --check` выполнены; это чтение изменений, не тест. Игра, тесты и новая запись Profiler не запускались. Исполнение после правок и устранение оставшихся задержек ещё не подтверждены пользовательским запуском.

## Единая подготовка кошельков участников — 2026-10-06

Implementation decision: **working model**. Реализован согласованный общий путь в package. Primary owner после Definitions/Storage регистрируется из RegisteredOwnerState; штатные боты — из AuthoredSeed исходного шаблона. PlayerService владеет одним binding на runtime-игрока и ожидаемым RetireAsync; Activity проверяет готовность участника и монтирует только собственные кошельки. Board и Autobattle сохраняют общий participant owner. Штатный состав: локальные → внешние → боты; внешний участник не обязан иметь PlayerData, локальное присутствие, Storage или Rewards.

Сохранение пишет PlayerSnapshot.EconomyOwner напрямую с прежними ключами и фильтром Persistent. EconomyOwnerStateTransfer и копирование в Activity удалены. Общий seed initializer сохраняет формы и теги; Selected не превращает Stack в Token. Очистка созданных кошельков ждёт удерживающие операции независимо от residency. Сеть, новые правила прототипов и перенос незавершённых заказов в Storage не добавлены.

Владельцы и механизм описаны в [общем документе package](/Users/ionrain/MorbooFrameworkPackage/Documentation~/CapabilityHostPrototypes.md#unified-participant-wallet-preparation--2026-10-06). ChainRush использует тот же linked package и общий GameRuntime. Игровые прогоны и тесты не запускались; приёмка требует отдельного разрешения после полной реализации.
