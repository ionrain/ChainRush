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

## ChainRush startup: null skill effects — diagnosis 2026-10-06

User request, verbatim: «Смотри логи запуска проект ChainRush». Only `/Users/ionrain/Library/Logs/Unity/ChainRush.log` and the explicitly addressed ChainRush Editor were inspected. The Editor is stopped, is not compiling and reports no script compilation failure. No gameplay run or tests were started.

**Verified:** the latest two launches terminate in Definitions at ChainRushGameplaySkillsInstaller. The last trace, log lines 6443–6457, reaches InMemorySkillRuntime.ValidateDefinition with `WaterUnitApproach.Effects[0] == null`; GameRuntimeHost.ExecuteStep marks initialization failed and rethrows. InitializeRetiredRuntime consequently does not reach player registration, startup actions or successful driver attachment and invokes its existing retirement path. This establishes the immediate startup blocker, rather than a later Activity or scene-loading failure.

Read-only typed inspection of the installer finds 84 non-null SkillData references. Of those, 54 contain null effects; the other 30 do not. All 54 affected files contain literal `!!null` text in the type metadata of the null managed reference (`rid: -2`), and none of the 30 unaffected files do. In WaterUnitApproach the authored movement effect and formula entries are present on disk. The corresponding SkillMoveToTargetEffectData and SkillEffectBinaryNodeData types resolve in the current Core assembly; Unity reports no missing managed-reference types for the inspected skills. Readbacks are `/tmp/chainrush-water-approach-readback-20261006.json` and `/tmp/chainrush-skill-catalog-read-20261006.json`.

**Inference:** malformed null-reference metadata is the leading explanation for Unity discarding the managed-reference graph. Its causal effect has not been demonstrated by a permitted before/after authoring readback yet; stale imported state remains a competing explanation. Do not change Skills validation or suppress the null-effect failure. The separate MMSoundManager SerializationException is outside this task, as previously requested by the user.

**Existing System Fit:** GameRuntimeHost owns initialization and failure cleanup; GameplaySkillsInstallerData owns adapter/definition installation. SkillService delegates to InMemorySkillRuntime, which validates definitions before registration. SkillData owns the serialized effect graph; Unity owns import and managed-reference serialization. Preserve these owners and all existing runtime paths.

**Best Practices:** preserve authored graph data while resolving the serialization defect at its source; do not silently drop invalid effects or make runtime guess an effect. The existing ValidateDefinition null guard is the relevant contract. The previous explicitly approved five-asset null-metadata correction is documented in package Documentation~/CapabilityHostPrototypes.md, preserving current formulas and using explicit Unity import/serialization without automatic runtime repair.

### Approved authoring action — implemented

**Implementation decision: working model.** Explicitly correct the null managed-reference type metadata and import the affected skill assets through Unity. Start with WaterUnitApproach and read back its authored movement effect and formula without executing them. Proceed to the remaining named assets only after that readback resolves the null effect; otherwise preserve evidence and stop before rewriting any graph. Preserve all non-null reference IDs, node types, effect parameters, formulas, asset GUIDs and references. Do not save a null loaded graph over the file, recreate the effect, add a migration/fallback or change package runtime. Read back the installer after authoring. No Play Mode or tests.

Scope is exactly the following 54 installer-referenced skills; unrelated wallet/host assets are not included:

- `Assets/Game/Activities/Autobattle/Skills/WaterUnitApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit2Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit3Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit4Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnitHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnitApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit2Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit2Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit3Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit3Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit4Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit4Approach.asset`
- `Assets/Game/Activities/Autobattle/Skills/PerfumeHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugBrownSmallContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugBrownSmallApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenSmallContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenSmallApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugBrownMediumContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugBrownMediumApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleSmallContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleSmallApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenMediumContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenMediumApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleMediumContact.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleMediumApproach.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenSmallWeaponHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugGreenMediumWeaponHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleSmallWeaponHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/BugPurpleMediumWeaponHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt1Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt2Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt3Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt4Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt5Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/LightningBolt6Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/Heal.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnitFollow.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit2Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit3Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit4Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnitFollow.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit2Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit3Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/ColaUnit4Follow.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnitHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit2Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit3Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/WaterUnit4Hit.asset`
- `Assets/Game/Activities/Autobattle/Skills/TabascoHit.asset`
- `Assets/Game/Activities/Autobattle/Skills/PowerBuff.asset`
- `Assets/Game/Activities/Autobattle/Skills/DefenseBuff.asset`
- `Assets/Game/Activities/Autobattle/Skills/HealthBuff.asset`
- `Assets/Game/Activities/Autobattle/Skills/SpeedBuff.asset`
- `Assets/Game/Activities/Autobattle/Skills/SkillSpeedBuff.asset`

Rule 11 required approval for this explicit serialization correction; reading the log alone did not authorize rewriting the authored assets. The user explicitly answered «Согласовать исправление по описанному плану» before the implementation.

**Implementation result:** the stopped ChainRush Editor was verified before changes. In WaterUnitApproach only the malformed type metadata under `rid: -2` was replaced with Unity's empty null-type metadata. Explicit Unity import restored SkillMoveToTargetEffectData and its SkillEffectBinaryNodeData formula root, without missing managed-reference types. Readback is `/tmp/chainrush-water-approach-after-fix-20261006.json`. This satisfied the approved prerequisite for the remaining 53 skills.

The same exact one-line correction was applied to the other 53 named assets and each was imported explicitly through the ChainRush Editor. All 54 now load with non-null effects and without missing managed-reference types. The existing installer still contains 84 non-null skills, and its full readback finds no null effects, null requirements or missing managed-reference types. This is authoring readback, not an execution of registration, effects, gameplay or tests.

Backups and the exact asset/metadata hash manifest are `/tmp/chainrush-null-reference-fix-20261006/`. Comparison against those backups confirms that every one of the 54 files changed only the approved null-type metadata line. Non-null reference IDs, effect parameters, formulas, references and all `.meta` files are unchanged. No imported skill is dirty. Targeted whitespace checks passed. Final import readback is `/tmp/chainrush-import-approved-skills-20261006.json`; full installer readback is `/tmp/chainrush-skill-catalog-after-fix-20261006.json`.

The approved implementation is complete. Package runtime, other asset categories and MMSoundManager were not changed. The earlier logged null-effect blocker is absent from the authored objects now read by Unity; successful startup still requires a later user-authorized game run. Play Mode and tests were not started.

## ChainRush startup: unresolved economic destinations — diagnosis 2026-10-06

Reported symptom, verbatim: «смотри логи запуска, игра зависает на старте». Only the dedicated ChainRush log and explicitly addressed ChainRush Editor were inspected. The Editor responds and reports stopped Play Mode, FrameworkStart, no active compilation and no script compilation failure. No game run, tests or asset reimports were initiated in this observation.

**Verified:** the latest launch reaches Definitions and fails with `Numeric Economy effect requires a destination.` Log lines 7006–7023 trace SkillEffectFormulaEvaluator's constructor → PrepareEffectFormulas → RegisterDefinitions → GameplaySkillsInstallerData → GameRuntimeHost. The constructor throws this exact message when SkillEconomyEffectData.Asset is null. ExecuteStep marks startup failed; InitializeRetiredRuntime invokes its existing cleanup instead of reaching successful player registration/startup actions. The log proves initialization failure; an infinite runtime loop has not been demonstrated.

Typed readback of all 84 installer skills finds 52 numeric economic effects, six of them with null Asset references:

| Source skill | Effect index | Authored destination, existing GUID resolves to |
|---|---|---|
| Board/Skills/IssueHeal.asset | 1 | Autobattle/Skills/Heal.asset |
| Board/Skills/IssuePowerBuff.asset | 0 | Board/Economy/PowerBoardBase.asset |
| Board/Skills/IssuePowerBuff.asset | 1 | Autobattle/Skills/PowerBuff.asset |
| Board/Skills/IssueDefenseBuff.asset | 0 | Board/Economy/DefenseBoardBase.asset |
| Board/Skills/IssueSpeedBuff.asset | 1 | Autobattle/Skills/SpeedBuff.asset |
| Board/Skills/IssueSkillSpeedBuff.asset | 0 | Board/Economy/SkillSpeedBoardBase.asset |

All five source files contain nonzero authored GUID references at these exact effect entries. Direct Unity reads resolve all six destinations to existing EconomyAssetData descendants without missing managed-reference types, but the loaded source effects remain null even after those reads. Thus absent source files, unresolved script types and an intentionally empty authored destination are excluded by current readback. The earlier null-effect graph failure is absent here.

**Inference:** these loaded source graphs retain unresolved object references from the previous import state. Explicit source reload is needed to establish whether the existing authored references resolve; do not infer a new destination or add runtime fallback. IssueHeal is the earliest source with a null destination in installer order (index 59). The log's exception does not name the skill, so that mapping uses order and readback rather than a per-effect trace. MMSoundManager remains outside scope.

Evidence: `/tmp/chainrush-hang-editor-state-20261006.json`, `/tmp/chainrush-numeric-destination-read-20261006.json`, `/tmp/chainrush-destination-assets-read-20261006.json` and `/tmp/chainrush-startup-registered-skills-20261006.json`.

**Existing System Fit:** GameRuntimeHost owns startup/failure cleanup; GameplaySkillsInstallerData and SkillService install definitions; SkillEffectFormulaEvaluator checks numeric destination precision. Unity owns asset import and reference resolution. Reuse this path; do not change economy ownership, formulas, validation, Skills execution, GameFlow or Activity.

**Best Practices:** repair the explicit authoring/reference boundary rather than silently substituting a destination in runtime. The existing constructor's Asset-null guard preserves the economic output contract. The previously approved Sandbox source reload in package CapabilityHostPrototypes.md demonstrates the same explicit Editor-only reload approach; no automatic repair pass is proposed.

### Approved explicit source reload — implemented

**Implementation decision: working model.** Through the stopped ChainRush Editor, explicitly import the five Board skill sources listed above and read back their destination references. If import retains null fields, unload/reload those exact sources and their existing GameplaySkillsInstaller asset through Unity, without rewriting effects, selectors, GUIDs or formulas. Preserve original bytes/hashes before the action; do not save a null loaded graph. Read back every installer reference and numeric destination. If the references remain null, stop before substituting or reconstructing them. No package code changes, persistent data changes, new source files, automatic reloads, game runs or tests.

The five sources were outside the prior 54-skill correction, so rule 11 required explicit approval before this additional reload action. The user answered «Согласовать перезагрузку по описанному плану».

**Verified implementation:** the ChainRush Editor was stopped before the action. The five source assets were explicitly imported; all six null destinations remained null. Readback is `/tmp/chainrush-board-source-import-20261006.json`. The approved second branch then unloaded the five source assets and the existing GameplaySkillsInstaller, loaded the five sources again from their unchanged authored files, and imported/loaded the existing installer. All ten economic effects of these five sources now resolve their destinations, including all six previously missing references. The installer retains 84 non-null skill references. Readback is `/tmp/chainrush-board-source-reload-20261006.json`.

The final full installer readback finds all 84 skills available, all 52 numeric Economy destinations resolved, and no null effects, null requirements or missing managed-reference types. There are no dirty skill/installer assets, active compilation or script compilation failures; Play Mode remains stopped. Evidence is `/tmp/chainrush-final-destination-read-20261006.json`.

The exact source/installer backups and hashes are `/tmp/chainrush-board-source-reload-20261006/`. Comparison confirms no changes to any of the five source files, the installer or their six `.meta` files. No effect, formula, selector or GUID was rewritten, and no runtime fallback or automatic reload was added. These before/after reads establish that the immediate missing destinations were in the loaded object state; the earlier event that caused that stale state remains unknown.

The approved reload action is complete. Startup after this action is not yet established: no game run or tests were started. The earlier constructor refusal is no longer present in its destination inputs as read from the installer.


## ChainRush level preparation: unresolved Objective assets — diagnosis 2026-10-06

Reported symptom, verbatim: «Уровень не загружается». Inspected only `/Users/ionrain/Library/Logs/Unity/ChainRush.log` and the explicitly addressed ChainRush Editor. The Editor reports stopped Play Mode, FrameworkStart, no compilation and no script compilation failure. No game run, tests, imports or reloads were started during this diagnosis.

**Verified:** the new launch passes runtime initialization, prepares the local player and bot roster, launches Autobattle Activity 2 and materializes its seven starting hosts. Log line 10435 records refusal of preparation: `ParticipantRequirementsNotMet`, space status Failed, phase Ready, with `ObjectiveConditionEconomyMetric requires an asset, required asset tags or required runtime tags.` GameFlow fails `await-autobattle-ready` and restarts the parent meta flow; the subsequent Main is a result of that failure path.

The authoritative path is ActivityServiceCore.InitializeRuntimeContentAsync → RegisterObjectives → RegisterTeamObjectives → ObjectiveService.AddObjective → ObjectiveServiceCore.AddObjective → ObjectiveConditionPipeline.BuildIndexes → ObjectiveEconomyMetricPlugin.Index. The plugin rejects a metric with null Asset and empty required asset/runtime tags. Runtime cloning uses ObjectiveCondition.MemberwiseClone and preserves those authored selector fields; it does not substitute or clear Asset. The exception is returned as runtime preparation failure. TryAdvanceSpacePreparation emits ActivityPreparationFailedEvent and ActivityLifecycleEvent(Ready, Fail); GameFlow reads that lifecycle, fails the waiting step and performs existing cleanup/restart. Objective registration failure detaches its partial subscriptions; Activity and GameFlow retain cleanup ownership.

Typed readback covers all 52 team Objective entries of the two Activity definitions, including nested conditions, target nodes and reset conditions (98 metric observations, with common templates appearing in both levels). Exactly two distinct templates contain invalid economic selectors:

| Existing source | Condition | Existing authored destination |
|---|---|---|
| `Assets/Game/Activities/Autobattle/Objectives/HealApplicationObjective.asset` | `apply-heal/activate[0]`, `apply-heal/success[0]` | `Assets/Game/Activities/Autobattle/Skills/Heal.asset`, GUID `7bed56cc886944c76a7f8ecd10cffe43` |
| `Assets/Game/Activities/Autobattle/Objectives/PowerBuffApplicationObjective.asset` | `apply-power-buff/activate[0]`, `apply-power-buff/success[0]` | `Assets/Game/Activities/Autobattle/Skills/PowerBuff.asset`, GUID `839c1d8a662844abaa8388a2950bf11a` |

Both templates are shared by Survive and Distance. The first invalid template in team registration order is HealApplicationObjective at team 0, index 2. The log does not name the template; this identification combines the rejection message, loaded inputs and checked registration order. Both source files contain `_asset` with Odin external-reference index 1 and two ReferencedUnityObjects, including the matching skill GUID. The loaded serialization reference list contains only its wallet tag (one entry), and both metrics read Asset=null. Direct loading resolves both destination skills as EconomyAssetData descendants but does not restore the metrics. Neither template is dirty.

**Inference:** the loaded Objective graphs/reference tables are stale relative to their authored files. Explicit import/reload will determine whether those unchanged references resolve. The event that caused that discrepancy is not established. Do not infer an absent destination, rewrite selectors or weaken Objective validation. Competing explanations checked: missing roster, absent source/destination assets, intentionally empty authored selection and runtime clone clearing the selector do not explain the observed inputs. The older null skill-effect/installer failure is absent from this launch. Separate BattleUI owner/levelcatalog errors exist in the log, but the Activity failure records this Objective validation refusal; those UI errors have not been shown to cause it. MMSoundManager remains out of scope.

Evidence: `/tmp/chainrush-level-editor-state-20261006.json`, `/tmp/chainrush-level-objective-read-20261006.json`, `/tmp/chainrush-objective-reference-read-20261006.json`. Read-only inspection scripts are outside the project in `/tmp`.

**Existing System Fit:** Unity/Odin own loading the authored Objective graph and external object references. Objectives own metric validation, indexing and subscription cleanup. Activity owns preparation and cancellation; GameFlow owns the waiting step, presentation cleanup and restart. Reuse these owners. Economy, Skills execution, wallet preparation, prototype propagation, Production and UI adapters are outside the proposed action.

**Best Practices:** preserve the validated selector contract and correct the explicit asset-loading boundary rather than injecting a resource at runtime. ObjectiveEconomyMetricPlugin.Index is the existing invariant check (`MorbooFrameworkPackage/Scripts/Core/Objectives/Internal/Plugins/ObjectiveEconomyMetricPlugin.cs:14–24`); ObjectiveCondition.CloneCore preserves the input (`Scripts/Core/Objectives/Conditions/ObjectiveCondition.cs:42–45`). Use the same explicitly approved Editor import/reload mechanism recorded in the preceding section, with byte/hash preservation and typed readback. Do not save a partially loaded Odin graph, because its reduced reference table would overwrite valid authoring.

### Approved explicit import/reload — implemented

**Implementation decision: working model.** In the stopped ChainRush Editor:

1. Preserve source and `.meta` bytes/hashes for the two Objective templates above and their two containing definitions: `Assets/Game/Activities/Autobattle/Definition/AutobattleActivity.asset` and `DistanceActivity.asset`.
2. Explicitly import only the two Objective templates through Unity and read back Asset in their activation/success metrics and external reference tables.
3. If the metrics remain null, unload those two templates and two containing Activity definitions through Unity. Load the two templates from their unchanged files, then reload the two Activity definitions so their team Objective references address the refreshed objects.
4. Read both definitions' team templates and all economic selectors again; verify the four affected conditions reference the exact existing Heal/PowerBuff assets and that no dirty source assets or file/GUID changes were introduced.
5. If import/reload does not restore these references, preserve evidence and stop before substituting or reconstructing data.

No runtime code, formulas, condition semantics, economic ownership, selectors or GUIDs change. No new permanent script, automatic reload/repair, game run or tests. The previous approval covered five Board skills and their installer; it did not cover these Objective/Activity sources. Rule 11 requires explicit approval for this additional action.


The user answered «Согласовать импорт и перезагрузку по описанному плану (рекомендую)». The Editor remained stopped. Explicit import preserved the null metrics (`/tmp/chainrush-objective-source-import-20261006.json`); the approved unload/reload of the two templates and two containing Activity definitions restored all four Asset references and both Odin reference tables to two entries (`/tmp/chainrush-objective-source-reload-20261006.json`). Full readback of both levels now finds 52 non-null team template entries, 98 metric observations and no invalid selectors or dirty observed templates (`/tmp/chainrush-level-objective-after-reload-20261006.json`). Exact comparison against `/tmp/chainrush-objective-source-reload-20261006/manifest.json` confirms all four source files and their `.meta` files are unchanged. No code or authoring was rewritten, and no game/test run was started.

### Additional loaded Flow references — approved action executed; cleanup incomplete

**Verified:** subsequent readback found null Activity references in the already loaded Odin roots and ActivityLifecycle conditions of all four level Flow templates: the two integration flows and the two Framework UI flows. The authored Flow files retain nonzero Activity GUID references. The actual failed launch used the Framework UI Survive flow; that flow is also affected, so merely checking restored Objectives is insufficient to declare startup ready. Evidence: `/tmp/chainrush-level-flow-reference-read-20261006.json` and `/tmp/chainrush-ui-level-flow-reference-read-20261006.json`.

**Inference:** the loaded Flow graphs retained the previous Activity objects across the approved Activity reload. Their pre-action loaded state was not captured, so the precise moment they lost the references is not independently established. This is an omitted dependency in the reload plan. Do not claim a new game failure has been reproduced or modify GameFlow runtime behavior.

**Existing System Fit:** four GameFlowTemplateData Odin graphs own these direct Activity references. Their consumers are the existing GameFlowDefinitionsInstallerData and four AddGameFlowRuntimeActionData assets; reference searches find no other authored consumers of these four Flow identities. The native references of these five consumers are currently intact, as are ActivityRuntimeInstaller, EconomyDefinitionsInstaller and FrameworkProfile references to the refreshed Activity definitions. Evidence: `/tmp/chainrush-level-native-reference-read-20261006.json`. MetaFlow and BoardFlow are not direct consumers of these four Flow identities and remain outside reload scope.

**Best Practices:** complete dependency refresh at the authoring/loading boundary and preserve runtime invariants. GameFlowServiceCore validates non-null ActivityFlowContainerData.Activity at Scripts/Core/GameFlow/Internal/GameFlowServiceCore.cs:1606–1607; weakening that check would hide an invalid execution context. Restore the existing authored graph through Unity rather than assign a replacement at runtime or write a graph with missing references. Native consumer readback verifies identity continuity after a source reload.

**Implementation decision: working model.** Additional exact source list:

- `Assets/Game/Activities/Autobattle/GameFlow/AutobattleFlow.asset`
- `Assets/Game/Activities/Autobattle/GameFlow/DistanceFlow.asset`
- `Assets/Game/FrameworkUI/Data/GameFlow/SurviveFlow.asset`
- `Assets/Game/FrameworkUI/Data/GameFlow/DistanceFlow.asset`

Preserve these files and `.meta` hashes, explicitly import them, and read their root/condition Activity references. If references remain null, unload/reload only these four Flow sources. Load them against the restored Activity definitions. Read every GameFlowDefinitionsInstaller template and the four action template fields. If a native consumer retains a missing reference, explicitly reimport only the corresponding existing consumer from the following bounded list, without unloading further authored graphs:

- `Assets/Game/Runtime/Installers/ChainRushGameFlowDefinitionsInstaller.asset`
- `Assets/Game/Runtime/Startup/StartChainRushLevel.asset`
- `Assets/Game/Runtime/Startup/StartDistanceLevel.asset`
- `Assets/Game/FrameworkUI/Data/GameFlow/AddSurviveFlow.asset`
- `Assets/Game/FrameworkUI/Data/GameFlow/AddDistanceFlow.asset`

Preserve those five native consumer files/hashes too. Final readback checks both levels' Objectives, all four Flow Activity references, their authored non-null ActivityLifecycle condition references, installer/action references, dirty states and unchanged source/meta bytes. If references remain unresolved, preserve evidence and stop before altering data. No runtime/code/condition changes, GUID changes, game runs, tests, new permanent tooling or automatic repairs. This dependency refresh was omitted from the preceding four-file plan; rule 11 requires explicit approval before this additional action.


The user approved «Согласовать обновление всей перечисленной цепочки (рекомендую)». The four Flow imports retained null Activity references (`/tmp/chainrush-level-flow-import-20261006.json`). Explicit unload/reload restored all four root Activity references and all ten explicitly authored ActivityLifecycle condition references (`/tmp/chainrush-level-flow-reload-20261006.json`). Backups are `/tmp/chainrush-level-flow-reload-20261006/`; all nine sources and their `.meta` files retain identical bytes/hashes. Final Objective readback still finds no invalid metrics (`/tmp/chainrush-level-objective-final-20261006.json`).

**Verified limitation:** SerializedObject readback reports valid references in native consumers, but it does not prove their existing C# fields hold usable objects. Reading the actual private `templates` and `template` fields used by Install/Execute finds the four old Flow objects invalid under Unity object equality. The approved conditional import of all five affected consumers did not restore those fields. Before/after evidence is `/tmp/chainrush-level-consumer-field-read-20261006.json`, `/tmp/chainrush-level-consumer-import-20261006.json` and `/tmp/chainrush-level-consumer-field-after-import-20261006.json`. The same actual-field check finds the old Activity objects invalid in ActivityRuntimeInstaller, EconomyDefinitionsInstaller and FrameworkProfile seed. Evidence: `/tmp/chainrush-level-pre-restart-read-20261006.json`. Their on-disk references are intact.

The approved file-level reload actions are executed, but the complete editor-loaded dependency chain is not ready. The proposed unload mechanism omitted these retained C# consumer references. Do not report startup readiness from Inspector/SerializedObject alone, save a graph containing invalid references or keep unloading ancestors without defining the whole cleanup boundary.

### Finish loaded-state cleanup by restarting ChainRush Editor — awaiting approval

**Implementation decision: working model.** Close and reopen only the ChainRush Editor, without starting Play Mode or tests and without rewriting authoring/runtime code. This is completion of the explicit reference refresh; no permanent restart/repair mechanism is added.

**Existing System Fit:** Unity owns the lifetime and reconstruction of all editor-loaded Unity objects and managed references. GameRuntime installers and startup actions consume ordinary C# fields, so final verification must read those actual fields. Economy/Activity/Skills/GameFlow runtime behavior and their lifecycle contracts remain out of scope. A project-specific Editor restart refreshes the entire reference chain from the unchanged files rather than requiring a cascade of ancestor unloads.

**Best Practices:** [Unity Resources.UnloadAsset documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.UnloadAsset.html) states that an unloaded asset object becomes invalid; a later load creates a new object that is not connected to the previous instance. Therefore loading the leaf and checking its native GUID does not repair consumer-held old C# references. Reconstruct all consumers across the Editor lifecycle boundary, preserve authored data, and inspect the same fields that product entrypoints read. This addresses the observed identity problem without weakening validation or injecting fallback resources.

Before restart the Editor reports no dirty open scene, no dirty persistent ScriptableObject, stopped Play Mode and no compilation. Recheck these conditions immediately before closing. If user changes appear, do not discard or save them implicitly; stop at that boundary. Preserve the dedicated ChainRush log locally before restart so the original failure evidence remains available. Use graceful Editor exit, not forced termination. Reopen the exact `/Users/ionrain/ChainRush` project with its existing version and dedicated `-logFile /Users/ionrain/Library/Logs/Unity/ChainRush.log`; no other Unity project is closed or selected.

After connection is ready, read without running the game: actual installer/action C# fields (all GameFlow templates and four start actions), Activity/Economy definition references, FrameworkProfile seed, all four Flow roots/ActivityLifecycle references, both levels' Objective selectors, and the existing skill installer references/economic destinations. Check dirty states, source/meta hashes and stopped Play Mode. If a reference remains unresolved, preserve evidence and stop before further changes. Do not save any partially loaded object graph. No tests, Play Mode or data writes are authorized by this restart.

Rule 11 requires approval for this project-level Editor restart, which was not part of the file-level action. The current source and `.meta` files were not changed by the approved imports/reloads; documentation is the only repository file changed in this diagnostic turn.


### Restart observation after interrupted turn

The user approved the ChainRush-only Editor restart. Graceful exit was scheduled in process 6614; an open attempt was made while that process still existed. The turn was interrupted before restart completion could be verified. The next dedicated log and explicit Editor connection now identify fresh process 9274; neither 6614 nor 8801 remains. The user has manually launched and stopped Play Mode in the new process. No agent-started Play Mode or tests occurred.

Reading the actual GameFlow installer and four startup-action C# fields in this fresh Editor finds all six templates, all roots and their Activity references valid. Evidence: `/tmp/chainrush-level-consumer-after-editor-restart-20261006.json`. The current startup refusal is a different earlier boundary, described below; the preceding project-wide readback was not completed before the interruption and must not be claimed as fully passed.

## ChainRush startup: missing resource selectors in buff formulas — diagnosis 2026-10-06

Reported symptom, verbatim: «Смотри логи, снова игра зависает на стартовом экране». Only the dedicated ChainRush log and the explicitly addressed ChainRush Editor were inspected. Fresh Editor PID 9274 reports stopped Play Mode; the user performed the recorded launch. No agent game/test run, import, reload or data modification was performed for this diagnosis.

**Verified:** log lines 1155–1213 record `Installer ChainRushGameplaySkillsInstaller [Definitions] failed. A skill resource formula requires an exact asset and one Stack or Token form.` The stack reaches SkillEconomyFormulaQuery.BuildSelectionKey:43 through SkillEffectFormulaEvaluator.Prepare → PrepareEffectFormulas → SkillService.RegisterDefinitions → GameplaySkillsInstallerData.Install → GameRuntimeHost.ExecuteStep. ExecuteStep marks initialization failed; initialization cleanup retires the incomplete GameRuntimeContext. Startup does not reach successful player registration/PostWorld flow startup. An infinite loop is not demonstrated; the reported start screen persists after initialization refusal.

Readback uses the actual installer `skills` C# field, not only SerializedObject. All 84 definitions and effects are non-null. Traversal of every numeric formula finds 62 resource-read nodes; nine nodes have a null ExactAsset, all in three Board skills. Their form lists each contain exactly Token, wallet tags exist and selectors are not null. Three associated output effects also have null Asset. The first invalid resource formula in installer order is IssuePowerBuff at index 74.

| Skill source | Existing intended board asset | Missing fields per source |
|---|---|---|
| `Assets/Game/Activities/Board/Skills/IssuePowerBuff.asset` | `Assets/Game/Activities/Board/Economy/PowerBoardBase.asset` (`cced08679ec404428989e44de6409afa`) | Effect 0 Asset; effect 0 formula root/left ExactAsset; effect 0 root/true/left/right ExactAsset; effect 1 root/argument ExactAsset |
| `Assets/Game/Activities/Board/Skills/IssueDefenseBuff.asset` | `Assets/Game/Activities/Board/Economy/DefenseBoardBase.asset` (`caaa4c89409854fb4add5101720d6531`) | Same four fields |
| `Assets/Game/Activities/Board/Skills/IssueSkillSpeedBuff.asset` | `Assets/Game/Activities/Board/Economy/SkillSpeedBoardBase.asset` (`8ead9d1d4d59d4048b3e7c869a0acda5`) | Same four fields |

The three authored source files contain those exact nonzero GUID references. All three destinations load as existing CapabilityHostData/EconomyAssetData, with correct GUID and local fileID 11400000; direct destination loading does not restore the source selectors. These missing references are actual CLR null, not merely previously unloaded Unity objects comparing equal to null. The erroneous form-count/type alternative is excluded by typed readback. Earlier null-effect and Objective failures do not describe this launch's causal branch. MMSoundManager remains outside scope.

**Inference:** the source import/serialization state has lost these references relative to the authored files. The upstream event that produced the discrepancy is not established. The prior five-source reload restored loaded destination fields, but that observation did not establish their persistence after a new Editor/process load; this new evidence shows it is insufficient as the complete solution. Three destination files also contain malformed null-type metadata; that is a separate observation, not a demonstrated cause of this selector failure, and is not included in the corrective action below.

Evidence: `/tmp/chainrush-skill-formula-selector-read-20261006.json` and `/tmp/chainrush-board-formula-target-read-20261006.json`. Inspection scripts remain temporary files outside the project.

**Existing System Fit:** GameRuntimeHost owns startup/failure retirement. GameplaySkillsInstallerData/SkillService install definitions. InMemorySkillRuntime prepares formula nodes and shared query identities; SkillEconomyFormulaQuery owns validation and selection identity; SkillEffectFormulaEvaluator owns precision/formula preparation. SkillValueResolver and existing Economy watches consume the resulting selections during execution. Unity owns authored SkillData graphs and external asset references. Restore their existing inputs; do not weaken validation, extend formula semantics or add another query/runtime path. Economy, Production, Objectives, GameFlow, Activity and UI behavior remain outside this correction.

**Best Practices:** explicitly bind and serialize the existing asset references at the authoring boundary, then verify them in a fresh process. Keep SkillEconomyFormulaQuery.BuildSelectionKey's invariant intact (`MorbooFrameworkPackage/Scripts/Core/Skills/SkillEconomyFormulaQuery.cs:39–45`). Avoid unloading individual leaves and retaining consumer C# objects: [Unity Resources.UnloadAsset](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.UnloadAsset.html) documents that later loads create distinct instances rather than reconnecting old objects. Fresh-process readback of the actual consumer fields is necessary; Inspector GUIDs alone were insufficient in the preceding step.

### Proposed explicit authoring rebind — awaiting approval

**Implementation decision: working model.** For exactly the three source skills and twelve fields listed above:

1. Preserve source and `.meta` files/hashes and capture a complete serialized field snapshot of each graph before authoring. Confirm stopped Play Mode and no unrelated dirty changes.
2. Load the exact three existing board assets through Unity. Explicitly assign them to the three output Asset fields and nine formula selector ExactAsset fields. Use Unity authoring and save only these three skills; no automatic migration or persistent editor/runtime repair code.
3. Preserve selector wallet/runtime tags, form Token, owner/level/recipient settings, formulas, progressions, counts, precision, timing and non-null managed-reference identities. Compare serialized fields before/after with only the twelve named reference changes allowed. Do not rewrite missing effects or unrelated definitions.
4. Read all 84 skill definitions, all numeric destinations and every resource formula selector. Preserve `.meta`/GUIDs. Restore no reference by runtime fallback and do not touch the separately observed destination null-type metadata.
5. Close/reopen only ChainRush Editor after confirming no unsaved user changes, preserving its dedicated log and retaining project-specific logging. Do not unload assets individually. Read the actual installer C# fields, all 84 skill definitions/formulas, level Flow/Activity/Objective references and the profile again without starting Play Mode or tests. Require the twelve references to remain valid in this fresh process.
6. If rebind/save or fresh-process readback does not retain the references, preserve evidence and stop before another repair or scope change. Do not declare game startup successful without a separately authorized/manual run.

No new assets, GUIDs, recipes, formulas, authored strings, runtime code or policy changes. This is explicit authoring of the existing references rather than the previously approved in-memory-only reload. Rule 11 requires approval before assignment/save of these fields and the subsequent Editor restart.

### Explicit authoring rebind — approved execution and failed persistence check

The user explicitly approved assigning the twelve references, saving the three SkillData sources, restarting only ChainRush and reading the complete chain without a game/test run.

**Verified execution:** source/target files and their `.meta` files were preserved under `/tmp/chainrush-explicit-board-rebind-20261006/`. The initial Editor was already closed. A CLI-opened process exited before tools connected; no authoring was performed there. ChainRush was then opened with CLI `--wait` to retain the launched session. Authoring occurred in PID 10150 after project/stopped/idle/clean guards. Twelve fields were explicitly assigned through Unity; a complete reflection snapshot of serialized fields showed exactly four reference changes per source. Non-null managed-reference IDs and all other values matched. Only IssuePowerBuff, IssueDefenseBuff and IssueSkillSpeedBuff were saved with SaveAssetIfDirty. Unity emitted its standard field order/format and already-effective zero/null fields (action protection, ownerLevel and recipientLevel); their runtime values did not change. All `.meta` files and the three BoardBase destination files are byte-identical to the backup.

Actual installer readback immediately after save: 84 definitions; 62 resource formula nodes with no invalid selector; 52 economic outputs with no missing destination; no null requirements/effects/missing managed-reference types. This was data inspection, not invocation of Skills execution or a test.

The stopped/clean Editor was closed using EditorApplication.Exit(0). Its exit was verified before the next open. Dedicated logs were preserved. In fresh ChainRush PID 10373, Play Mode remained stopped and compilation had no failure.

**Verified failure:** all nine targeted formula ExactAsset fields and all three targeted outputs are null again in the actual installer C# graph. Saved source files still contain the correct destination GUIDs. Their non-null managed-reference IDs survived unchanged. This authoring/save action therefore did **not** resolve the startup refusal; do not mark the startup correction complete. The earlier post-save in-memory read is not evidence of durable recovery.

Other fresh readback: all ten GameFlow consumers (six definitions, four startup/add actions), their Activity roots/lifecycle conditions, and all nine Activity/profile references are valid. All 52 Activity/team Objective entries have templates. Among 98 Economy metric observations, sixteen are invalid across four shared deployment Objective files (two conditions each, repeated in two Activities): PlayerDeploymentObjective, WaterUnit4DeploymentObjective, ColaUnit2DeploymentObjective and ColaUnit3DeploymentObjective. These are additional data observations; this turn did not execute their runtime branch and they do not explain the earlier Definitions installer failure.

Evidence: `assignment-result.json`, `field-diff.json`, `save-result.json`, `catalog-after-save.json`, `yaml-field-comparison.json`, `complete-fresh-read-decoded.json`, `summary.json` and before/after/fresh graph snapshots in the backup directory. No runtime code, gameplay/test run, automatic migration, broad SaveAssets, individual asset unload or additional repair was performed. The failed fresh-process check closes the approved rebind attempt and stops further mutation at its stated boundary.

### Referenced definition null-type metadata — bounded proposal awaiting approval

**Implementation decision: working model.** Explicit authoring correction of invalid null managed-reference metadata in seven existing CapabilityHostData assets; no runtime fallback or permanent editor repair pass.

**Verified:** the three failing Board skill destinations and all four destinations of the newly observed invalid deployment metrics contain a `rid: -2` record whose class/ns/asm are the literal malformed quoted `!!null` strings. The original destination GUIDs exist in the consumers and match the destination `.meta` files. This is the same malformed record representation already explicitly corrected in the 54 SkillData sources; current Unity saves represent that null record as `type: {class: , ns: , asm: }`.

**Inference / cause not established:** the malformed destination metadata may explain why consumer references become null at a fresh load despite valid authored GUIDs. The source-only rebind/save and cold read excluded insufficient authoring of those twelve source fields. It did not prove which importer/deserializer branch drops the destination. Do not describe the seven-record correction as a confirmed startup fix before observing its effect.

| Referenced definition to correct | Consumer graph to inspect/import |
|---|---|
| `Assets/Game/Activities/Board/Economy/PowerBoardBase.asset` | `Assets/Game/Activities/Board/Skills/IssuePowerBuff.asset` |
| `Assets/Game/Activities/Board/Economy/DefenseBoardBase.asset` | `Assets/Game/Activities/Board/Skills/IssueDefenseBuff.asset` |
| `Assets/Game/Activities/Board/Economy/SkillSpeedBoardBase.asset` | `Assets/Game/Activities/Board/Skills/IssueSkillSpeedBuff.asset` |
| `Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset` | `Assets/Game/Activities/Autobattle/Objectives/PlayerDeploymentObjective.asset` |
| `Assets/Game/Activities/Shared/Units/Water/WaterUnit4.asset` | `Assets/Game/Activities/Autobattle/Objectives/WaterUnit4DeploymentObjective.asset` |
| `Assets/Game/Activities/Shared/Units/Cola/ColaUnit2.asset` | `Assets/Game/Activities/Autobattle/Objectives/ColaUnit2DeploymentObjective.asset` |
| `Assets/Game/Activities/Shared/Units/Cola/ColaUnit3.asset` | `Assets/Game/Activities/Autobattle/Objectives/ColaUnit3DeploymentObjective.asset` |

**Existing System Fit:** Unity authoring/import owns these serialized graph records and external references. CapabilityHostData owns existing definitions; SkillData and ObjectiveTemplateData consume those definitions, while installers/Activity/Flow expose them to startup. SkillEconomyFormulaQuery and Objectives retain their current input validation. GameRuntime startup and retirement remain unchanged. Reuse explicit import, the existing definitions, the actual-field read scripts and the dedicated ChainRush log. No replacement owner, cache registry or service repair is introduced.

**Best Practices:** repair the erroneous serialized input at its authoring boundary, preserve file identities and every non-null graph entry, and establish persistence in a fresh process. This avoids suppressing a validated invariant in SkillEconomyFormulaQuery.BuildSelectionKey or reattaching leaves while their consumers retain old objects. Precise project precedents are the approved null-record correction above (§ "ChainRush startup: null skill effects — diagnosis 2026-10-06") and the null record emitted by Unity in the three SkillData assets saved in the preceding action; Unity's [UnloadAsset contract](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.UnloadAsset.html) also explains why individual unloading is excluded.

Bounded sequence:
1. Preserve the seven definitions, seven consumers, two containing Activities and all their `.meta` files; capture their fields. Require stopped/idle Editor and clean scenes/assets.
2. First correct **only** the malformed `rid: -2` type line in PowerBoardBase to the standard empty class/ns/asm representation. Import PowerBoardBase and IssuePowerBuff through Unity; do not rewrite source selectors or recreate graphs. Keep all GUIDs, non-null managed-reference IDs, capabilities, wallets, seeds, formula parameters and other values unchanged.
3. Close/reopen only ChainRush after clean-state checks. Read actual installer fields: require all four PowerBoardBase references in IssuePowerBuff to survive in the fresh process. If they do not, preserve evidence and stop; do not touch the other six definitions or add another repair. No game or diagnostic test is run.
4. Only if the representative cold-read succeeds, make the same one-line correction in the six remaining definitions. Explicitly import these definitions and their six consumers, plus AutobattleActivity and DistanceActivity as the containing Objective graphs. Import does not authorize saving/rebinding those consumer graphs.
5. Restart only ChainRush again after clean-state checks, then read all 84 skills, 62 formula resource nodes, 52 economic outputs, all Flow/Activity/profile references and 98 Objective metric observations. Require the identified twelve skill references and eight unique deployment-condition references to remain available. Preserve the result and any remaining discrepancy; no broader repairs.

The only authored changes proposed are seven existing null-record type lines. No new assets, `.meta`/GUID edits, runtime/service changes, default injection, game/test run or unrelated metadata correction. Rule 11 requires explicit approval: the previous rebind proposal deliberately excluded destination null-type metadata and these four deployment definitions.

### User-raised alternative: Unity reserialization versus replacement identities — awaiting decision

The user asked: «А если не получится? Не проще переделать ресурсы и перененазначить?» This is a question about the proposed approach, not approval to mutate the seven destinations. The null-type correction proposal remains unexecuted.

**Implementation decision: working model.** Prefer explicit Unity serialization of the existing definition assets, retaining identities, before considering new assets/GUIDs. The seven inspected CapabilityHostData definitions use ordinary serialized fields (CapabilityHostData/BaseData, CapabilityEntry, WalletEntry, SeedEntry); their current configuration graph has no SerializeReference fields. The erroneous null managed-reference table remains in their authored files. This observation does not by itself establish the importer branch causing dropped external references.

**Existing System Fit / additional consumers found:** the reference search identified 25 unique authored consumer files, not just the seven initially inspected failing graphs. They include ChainRushEconomyDefinitionsInstaller, BoardPopulation/selection Objectives, three BoardBase recipes, four deployment recipes, merge recipes and the Water/Cola definition wallets in addition to the three buff Skills/four deployment Objectives. Exact mapping: `/tmp/chainrush-explicit-board-rebind-20261006/reference-fanout.json`. Preserve the responsibilities and data flow of Economy, Production, Skills, Objectives and CapabilityHosts. New GUIDs would require explicit rebinding in all 25 files; existing-GUID reserialization does not require that architectural/data-identity change. Imports and fresh reads should account for these existing consumers.

**Best Practices:** [Unity AssetDatabase.ForceReserializeAssets](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.ForceReserializeAssets.html) provides explicit load/write of a chosen asset list and an option to serialize assets separately from their `.meta` files. Use it only as an explicitly approved authoring action on the seven named files, not an automatic callback or project-wide upgrade. Compare serialized field values and original nonzero authored references before/after; retaining identity avoids unnecessary routing changes. Its success at removing obsolete metadata or retaining consumer references must be observed, not assumed.

Alternative bounded action to the previous handwritten one-line correction:
1. Preserve all seven definitions and all 25 consumer files/metas, plus AutobattleActivity/DistanceActivity and metas. Require stopped/idle Editor and no unsaved scenes/assets. Capture complete target field snapshots; compare loaded reference fields against original authored nonzero GUIDs before any write. If a target's own meaningful input is already missing, stop before serializing that incomplete state.
2. First invoke ForceReserializeAssets with **only PowerBoardBase** and **ForceReserializeAssetsOptions.ReserializeAssets**. Let Unity emit its current serialized representation, including removal/rewriting of unused metadata. Do not touch `.meta`, create new GUIDs, assign new defaults or change gameplay fields. Compare all actual serialized configuration values and source GUID references; only Unity's standard file formatting, persistence of already-effective zero/null fields and unused reference-table output may differ.
3. Explicitly import PowerBoardBase and its five existing consumers from the recorded mapping. Restart only ChainRush with clean-state guards and dedicated logging. Read the real fields of the installer, Skill, recipe and the two relevant Objective graphs. Require the four IssuePowerBuff references and all inspected references to PowerBoardBase to remain valid in the fresh process. If this fails, preserve evidence and stop this alternative without writing the remaining six definitions.
4. Only after the representative fresh read succeeds, perform the same guarded Unity serialization of the other six definitions. Import the six destinations, their recorded consumers (the complete 25-file mapping accounts for overlap) and the two containing Activities. Consumer import is not permission to save/rebind their content.
5. Restart only ChainRush and perform the complete readback of Skills/resource formulas/outputs, Flow/Activity/profile, Objectives and the recorded recipe/definition consumers. Preserve identities, field values and evidence. Do not run the game/tests or add importer/runtime repair behavior.

If this bounded attempt fails, the next proposal is to recreate one PowerBoardBase definition through Unity with a new identity, explicitly rebind its five consumers and check the fresh graph before extending replacement to the other six. That replacement is a separate authoring scope and is **not** authorized by this alternative. It needs a concrete field/rebinding plan and approval. Broad replacement of all seven resources is not undertaken merely because reserialization failed.

### Unity reserialization — approved representative attempt did not recover references

The user approved the existing-identity reserialization alternative above. Backups preserve 34 assets and 34 `.meta` files under `/tmp/chainrush-definition-reserialize-20261006/` (seven definitions, 25 consumers, two containing Activities). Complete actual-field snapshots were captured for all seven definitions before any write. Every nonzero authored object GUID/fileID reference matched both the native serialized properties and the actual C# configuration graph: Board definitions each have six authored references including their MonoScript; WaterUnit has twenty, the other three units twenty-one. Meaningful target configuration was not already missing.

**Verified execution:** in stopped/idle/clean ChainRush PID 10373, ForceReserializeAssets was called for PowerBoardBase only, with ReserializeAssets (no metadata serialization). The API completed and all definition fields matched the snapshot. The source file remained byte-identical, including its malformed/unused null-reference table. PowerBoardBase and its five recorded consumers were explicitly imported. The clean Editor was gracefully closed, its exit verified and its dedicated log preserved before opening a new process. No game/tests were run.

**Verified failed fresh read:** stopped/idle ChainRush PID 11097 retains the ordinary Economy definitions installer reference to PowerBoardBase at assets[106]. Both native fields and actual C# graphs lack all PowerBoardBase references in BoardPopulationObjective, PowerSelectionObjective, IssuePowerBuff and PowerBoardBaseRecipe. In particular all four targeted IssuePowerBuff references are absent. The source GUIDs remain nonzero/correct on disk. The remaining six definitions were not reserialized. All 68 preserved project files remain byte-identical to the backup.

Evidence: definitions-before-read.json, reference-preconditions.json, all seven before-fields/editor/native-ref snapshots, power-reserialize.json, power-import-consumers.json, power-consumer-fresh-read-decoded.json and power-cold-reference-summary.json in the backup directory. This attempt did not resolve startup; no conclusion that current APIs discarded the erroneous table is warranted, because that table remained unchanged. Cause of the consumer-reference loss remains unestablished. Stop further mutation at the approved representative boundary.

### Clean definition body with retained identity — proposed next authoring step

**Implementation decision: working model.** Recreate the serialized body of an existing definition through a fresh Unity object while retaining its original `.meta`/GUID and main-object fileID. This is explicit authoring, not runtime repair or automatic migration. It avoids rebinding 25 consumers to new GUIDs.

**Existing System Fit:** the seven known definitions and 25 consumers remain the exact assets mapped above. Unity creates the temporary definition and its `.meta`; the current CapabilityHostData schema defines its fields. Economy/Production/Skills/Objectives retain existing asset identities and semantics. Installers and Activity own their existing consumers; import/fresh read is used rather than per-object unloading. No persistent helper, altered definition type or runtime service code is introduced.

**Best Practices:** [EditorUtility.CopySerializedManagedFieldsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.CopySerializedManagedFieldsOnly.html) copies current serializable managed configuration to a new managed object. Using a fresh object and Unity-created asset separates current configuration from the old native serialized record. Compare every field and every external reference rather than assuming the copy is lossless. Preserve identity metadata and let Unity own creation/import/deletion of temporary assets; never invent or edit GUIDs.

Bounded sequence, requiring approval:
1. Preserve the seven sources/25 consumers/Activities and metadata; retain existing snapshots and repeat stopped/idle/clean guards. Require the source definition's actual meaningful fields/references to match the frozen authored input.
2. Start with PowerBoardBase only. Create a fresh CapabilityHostData through ScriptableObject.CreateInstance. Copy its validated serializable managed fields using CopySerializedManagedFieldsOnly. Preserve name/hide flags and compare all metadata, id/domain, precision, permissions, tags, footprint, propagation policy, capabilities, wallet definitions/seeds/tags, usage policy, prefab reference and pool settings with the original snapshot. Do not instantiate a runtime host or call Economy preparation.
3. Create a temporary `Assets/Game/Activities/Board/Economy/PowerBoardBaseRebuild.asset` through AssetDatabase.CreateAsset; Unity generates the temporary `.meta`. Save this temporary asset only. Require its current-field snapshot and referenced GUID/fileIDs to match the source, its main object fileID to equal the original 11400000, and its generated file to omit the malformed null-type record. If the serializer reproduces the bad record or fields differ, preserve evidence and stop before replacing the original.
4. Replace **only the original `.asset` body** with the verified bytes emitted for the temporary asset; leave the original `.meta` byte-identical. This file replacement is explicitly part of the proposed authoring action. Import the original and its five consumers; verify original GUID/main-fileID and configuration again. Remove the temporary asset/meta through AssetDatabase.DeleteAsset after preserving its generated contents outside the project. No individual Resources.UnloadAsset calls.
5. Restart only ChainRush after clean-state checks and dedicated-log preservation. Read actual installer/Skill/recipe/Objective fields and require the PowerBoardBase references to persist. If the representative fails, preserve evidence and stop before replacing another resource, changing identities or rebinding consumers.
6. Only if it succeeds, recreate the bodies of the remaining six definitions in the same way, using same-folder temporary `*Rebuild.asset` files created/deleted by Unity. Preserve their existing metadata/main-fileIDs and all validated fields. Explicitly import the remaining destinations and the recorded consumers plus containing Activities, then restart only ChainRush and perform complete readback of the 84 skills, 62 resource-formula nodes, 52 outputs, Flow/Activity/profile, 98 Objective metric observations and all recorded recipe/definition consumers.

The result must be seven current-schema asset bodies with the original identities/values and durable consumer references. No new gameplay identity, recipe/seed change, permanent repair pass, string contract, runtime code, unrelated asset mutation or game/test run. New temporary `.meta` files are created and deleted only by Unity. Rule 11 requires approval because the prior action authorized ForceReserializeAssets, not fresh temporary object creation and verified replacement of the asset body.

### Clean definition body — approved execution and remaining dependency

The user approved the retained-GUID clean-body proposal. **Implementation decision: working model.** Evidence and the unchanged original inputs are preserved under `/tmp/chainrush-definition-rebuild-20261006/`.

**Verified execution:** stopped/idle ChainRush PID 11097 created PowerBoardBaseRebuild through Unity. CopySerializedManagedFieldsOnly preserved every current managed configuration field. CreateAsset assigned the temporary filename as its name; the explicitly required original name was assigned again and saved before replacement. The generated asset body matched the original byte-for-byte up to the unused managed-reference registry; Unity emitted `RefIds: []` instead of the malformed null-type record. The verified body replaced the original `.asset`; its `.meta` and main fileID 11400000 were preserved. Unity imported the five consumers and deleted the temporary asset/meta.

**Verified representative fresh read:** after a clean restart, ChainRush PID 11782 retained PowerBoardBase in all five consumers, including all four IssuePowerBuff references. The Objective stores one external Odin reference used by both its activation and success conditions; both actual fields were valid. Thus the approved condition for proceeding with the other six resources was satisfied.

**Verified remaining authoring:** the same guarded fresh-object operation was performed for DefenseBoardBase, SkillSpeedBoardBase, WaterUnit, WaterUnit4, ColaUnit2 and ColaUnit3. Every temporary definition matched the frozen configuration and the original authored body outside the unused reference table. Only the seven original definition bodies changed; all 34 original metadata files and all 25 consumer files/two containing Activities stayed byte-identical. Temporary assets and their metadata were deleted through Unity. The 25 consumers and containing Activities were imported, and stopped/clean ChainRush was restarted again.

**Verified fresh graph in PID 11973:** all 35 recorded consumer/target pairs across 25 files retain the seven targets (48 native external-reference slots, 55 actual C# fields). All 84 skill definitions, 62 resource-formula nodes and 52 economic outputs are available; the three explicitly saved skill snapshots now match their complete previously saved graphs. Ten Flow consumers, nine Activity/profile references and all 98 economy Objective metric observations are valid. No runtime/game/test run was performed and startup success is not claimed from these data reads.

**Remaining discrepancy:** full target field comparison after the fresh launch found one previously loaded dependency missing in two definitions: FormHealthScale (GUID `18efa7a01b037401b98a874f176fb6d4`) is null in WaterUnit4's Stack seed amount 3000 and ColaUnit3's Stack seed amount 1000. Both authored references/amounts remain unchanged on disk. The five other rebuilt definitions match their frozen field snapshots completely. Direct loading FormHealthScale provides valid AttributeData, yet it does not reattach those two consumer fields. Other inspected FormHealthScale consumers retain the reference. Its source file contains the same malformed unused null managed-reference record. This is a verified remaining reference loss; the importer branch causing it is not established. No eighth asset mutation is authorized by the seven-resource plan.

### FormHealthScale clean body — proposed bounded follow-up

**Implementation decision: working model.** Apply the already demonstrated explicit clean-body authoring operation to the one additional observed dependency; retain its identity, attribute parameters and all consumer authoring. Await approval before mutation.

**Existing System Fit:** `Core.Attributes.AttributeData` inherits EconomyAssetData and declares valueType, preferenceType and referenceValue using ordinary SerializeField. Its current configuration has no SerializeReference graph. Economy definitions own registration; DerivedAttributes consumes the scale; six unit definitions hold it as an economic seed. The exact eight existing consumers are:

- `Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset`;
- `Assets/Game/Runtime/Installers/ChainRushDerivedAttributesInstaller.asset`;
- `Assets/Game/Activities/Shared/Units/Water/WaterUnit2.asset`, `WaterUnit3.asset`, `WaterUnit4.asset`;
- `Assets/Game/Activities/Shared/Units/Cola/ColaUnit2.asset`, `ColaUnit3.asset`, `ColaUnit4.asset`.

Authoritative calculation/registration owners remain unchanged; there are no new runtime listeners, providers or repair mechanisms. Read-only source/consumer evidence and backups are in `FormHealthScale-follow-up/` under the execution evidence directory.

**Best Practices:** reuse the explicitly approved and demonstrated fresh-managed-object technique, with Unity-owned temporary asset creation, field/reference comparisons and retained original identity. [CopySerializedManagedFieldsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.CopySerializedManagedFieldsOnly.html) copies current managed serialized fields rather than the old native serialized registry. Exact current configuration and a fresh-process read are required; no automatic blanket rewrite is proposed.

Bounded sequence requiring approval:
1. Repeat stopped/idle/clean guards and preserve FormHealthScale plus its eight consumers/metas. Compare its actual complete serialized configuration with the captured authored input. Preserve Ratio value type, HigherIsBetter preference, referenceValue 1, precision 3, id, permissions, name and all other metadata/values.
2. Create a fresh AttributeData, copy validated current managed fields, create `Assets/Game/Activities/Shared/Attributes/FormHealthScaleRebuild.asset` through Unity, then restore original name/hide flags and save only the temporary asset. Require identical configuration, original main fileID 11400000 and a clean unused reference table. If any condition fails, stop before replacing the original.
3. Replace only the original `.asset` body with the verified Unity-generated body; original `.meta`/GUID remains byte-identical. Import the original and all eight named consumers. Preserve the temporary output outside the project and delete its asset/meta through Unity. No consumer save/rebinding and no individual unloading.
4. Gracefully restart only ChainRush after clean-state guards. Read all eight actual consumer graphs and require FormHealthScale references to persist. Repeat complete seven-definition field comparisons, 25-consumer reads and the Skills/Flow/Activity/profile/Objectives graph reads above. Preserve any remaining discrepancy and stop further mutation; no broader repair is authorized.

No additional asset identity, runtime code, script, parameter/default change, migration, game run or test run. Rule 11 requires a separate explicit answer because FormHealthScale was outside the seven named resources in the approved authoring scope.

### FormHealthScale follow-up — approved and completed

The user explicitly approved the bounded follow-up above. **Implementation decision: working model.** In stopped/idle/clean ChainRush PID 11973, Unity created a fresh AttributeData and temporary FormHealthScaleRebuild asset. Its complete managed fields, original name/hide flags, original main fileID and authored body before the unused reference registry matched the preserved input. Only the verified `.asset` body replaced FormHealthScale; the original `.meta`/GUID remained byte-identical. Unity imported the eight named consumers and deleted the temporary asset/meta. The eight consumer files/metas were unchanged during this follow-up. Evidence: `FormHealthScale-follow-up/replaced.json`, the original/generated bodies, field snapshots, manifest and authoring tool output under `/tmp/chainrush-definition-rebuild-20261006/`.

**Verified fresh-process result:** after a guarded graceful restart of only ChainRush, stopped/idle PID 12410 reads FormHealthScale correctly in all eight native and actual consumer fields. WaterUnit4 retains its original Stack seed 3000; ColaUnit3 retains its original Stack seed 1000. All seven previously rebuilt definitions now match their complete frozen serialized field snapshots, and FormHealthScale matches its own full frozen snapshot. All original eight GUIDs and main fileIDs 11400000 are retained. The only change in each of the eight asset bodies is Unity's empty unused managed-reference table replacing the malformed null-type record.

**Complete readback retained:** 25 recorded consumers/35 target pairs still provide all seven original targets (48 native external-reference slots, 55 actual C# fields); 84 skill definitions, 62 resource-formula nodes and 52 economic outputs are available; the three buff-skill graph snapshots match their prior saved inputs. Ten Flow consumers, nine Activity/profile references and all 98 economy Objective metric observations are valid. Script compilation failure is false, all inspected assets are clean, and no temporary rebuild assets remain. The recorded reference discrepancies are resolved in this fresh process. Details: `FormHealthScale-follow-up/completion-summary.json` and corresponding full read outputs.

The approved authoring scope is complete. Runtime/service code, gameplay parameters, consumer configuration and original Unity identities were not changed. The original files, metadata, dedicated logs and snapshots are preserved outside the project. The Editor remains open in stopped FrameworkStart; no game or tests were run. Successful gameplay startup is still to be observed in an explicitly authorized/manual run and is not inferred from asset readback.

## Units not created — live diagnosis 2026-10-06

Reported symptom, verbatim: «Юниты не создаются». The user manually started ChainRush and answered «готово». Read-only inspection targeted ChainRush PID 12410 and its dedicated log. No Play Mode, test, planning method, import or mutation was started by the agent. Evidence is preserved under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`.

**Verified:** Autobattle and Board reached Running. Autobattle contains seven nonspatial service hosts and no materialized hero/enemies; its producers have empty queues. Board created sixteen spatial items and completed its Population assignment. The player's actual wallet contains Perfume ×1 with Available/Entitled/Selected, selected Water/Cola, and SpawnArea ×1. HeroProductionHost is registered and the hero Population agent retains its criteria, SpawnArea and HeroDeployment references. Absence of selected hero, initial shape or producer is not the observed obstruction.

**Verified causal boundary for hero:** the `PlayerBrain` instance used by the live player's `OrchestrationParticipantRuntime` contains 24 null Operators and 26 null DecisionGraph nodes. The actual native SerializedObject agrees: all operator references have id -2 and no type, although the file declares 24 operator references, 26 node references and 106 unique registry records. The initial-hero process is Blocked, attempt 0, with no candidates/exclusions and message «Process planning produced no executable candidates». ActivityDecompositionService skips null operators; OrchestrationProcessPlanner consequently cannot select an executable method. No order for the hero reaches Production. Both enemy brains and BoardBrain have non-null operator/node graphs. Only explicit editor authoring commands modify Brain.Operators; no runtime clearing path was found.

**Verified additional authoring discrepancies:** both executor and target criteria of Level01PopulationAgent have null EnemySpawner definitions; the equivalent two references in Level02PopulationAgent have null EnemySpawnerDistance definitions. The files still contain the original GUIDs. Both source definitions have the malformed unused null managed-reference record previously found in the eight restored definitions. Loading EnemySpawner itself exposes its current CapabilityHostData configuration, including Production seed; its consumers remain null. Restoring these intended references is an authoring correction, but their causal contribution to missing enemies is **not established** by this run. A null criterion definition broadens the existing filter, rather than itself rejecting every candidate. The bot's missing profile is also not a proven blocker: IsParticipantRuntimeEligible explicitly permits ProfileRequiredMissing, and the bot has a process runtime. Enemy processes seen before progress completion exhausted their alternatives; no detailed causal history for that branch is retained in the available log. Do not change planning, profile requirements or selection semantics based on these observations.

**Inference:** PlayerBrain's malformed null-reference metadata is the leading serialization explanation for the discarded graph. As in the earlier approved skill correction, an explicit import/readback must demonstrate restoration; do not save the currently empty in-memory graph over the authored file.

### Bounded authoring proposal — awaiting approval

**Implementation decision: working model.** Restore the existing authored configuration; introduce no runtime repair mechanism.

**Existing System Fit:** Activity opens the existing Orchestration domain; ActivityDecompositionService builds the operator registry; OrchestrationProcessPlanner and ProcessRuntime own planning/state transitions; AgentService/Population own selection and placement; Production owns accepted orders; Activity materializes confirmed outputs. Unity owns deserialization and external asset references. None of these runtime owners, listeners, executors, budgets or gameplay rules is modified. The task is scoped to PlayerBrain and the two existing spawner definitions and their explicit consumers.

**Best Practices:** preserve identity and authored intent, repair the data at the serialization boundary, and compare actual fields after a fresh Editor load. Reuse the already approved skill null-metadata correction and the retained-GUID fresh-managed-body procedure documented immediately above. Do not add fallback operators, synthesize defaults or bypass invalid configuration. Current Unity-owned creation/copy/import/delete operations are the same ones used for the eight preceding definitions; the parameters and metadata comparisons are mandatory before replacing a body.

Proposed sequence:
1. Preserve live snapshots, originals/metas and clean-state evidence. Stop only the current ChainRush Play Mode after explicit approval; do not start another run.
2. In `Assets/Game/Activities/Autobattle/Orchestration/PlayerBrain.asset`, correct only the one malformed type record for rid -2 to `{class: , ns: , asm: }`. The reviewable one-line proposal is preserved outside the project as `PlayerBrain.asset.proposed`. Preserve every non-null registry record, parameter, operator/node list, main fileID and meta. Import PlayerBrain and PlayerOrchestration and read the real 24 operators/26 nodes. If the graph is not restored, stop further mutation and retain evidence.
3. For `Assets/Game/Activities/Autobattle/Economy/EnemySpawner.asset` and `EnemySpawnerDistance.asset`, use the same verified fresh-managed-body procedure as the preceding eight restored definitions: create same-folder temporary `*Rebuild.asset` objects through Unity, copy current managed settings, restore original name/hide flags, compare all serialized settings and external references with frozen authored inputs, and require the generated body to differ only in the unused malformed reference registry. Preserve original meta/GUID/main fileID. Only after those checks replace each original body and delete the temporary asset/meta through Unity. No consumer configuration is rewritten.
4. Explicitly import the exact affected consumers: PlayerOrchestration, EnemyWaveAgent, Level01PopulationAgent, Level02PopulationAgent, AutobattleActivity, DistanceActivity and ChainRushEconomyDefinitionsInstaller. Import named parents only; no blanket project rewrite or individual object unloading.
5. After idle/clean-scene guards, gracefully restart only the ChainRush Editor. Read PlayerBrain's full graph and consumer reference, both spawner definitions and all original GUID-based consumer slots. Require all four intended Population criteria to point to their original definitions. Compare original settings/metadata and the previously restored startup graph. Any remaining discrepancy is recorded; do not expand scope silently. Gameplay success remains unverified until a separate user run.

This proposal needs a separate explicit answer under rule 11 because these three assets and stopping the current Play Mode were not in the preceding eight-resource authoring scope. It does not authorize game or test execution.

### Bounded authoring restoration — approved and completed

The user explicitly approved stopping the current Play Mode and the three-asset restoration. **Implementation decision: working model.** Read-only live evidence, twenty original files/metas, the dedicated project log and the one-line PlayerBrain proposal were preserved under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`. Play Mode was stopped in ChainRush PID 12410; subsequent authoring guards required stopped/idle status and no unsaved scenes/assets.

Correcting only the malformed rid -2 type record in PlayerBrain and importing it restored all 24 actual/native operators and 26 decision nodes. PlayerOrchestration retained its link. The file differed by exactly the approved one line; all 105 non-null registry records, parameters, node lists and original metadata remained unchanged. This before/after import readback supports the serialization explanation for the discarded graph; it does not constitute a gameplay run.

For EnemySpawner and EnemySpawnerDistance, Unity created fresh temporary CapabilityHostData assets. Complete managed fields, name/hide flags, external references and the authored text before the unused registry matched the frozen originals. Only verified generated bodies replaced the originals. Unity deleted both temporary assets/metas. The change in each original body is solely an empty unused reference table replacing the malformed null-type record. All original GUIDs/main fileIDs 11400000 remained unchanged. The seven specified consumers were explicitly imported; their files and all original metadata were not rewritten.

After clean-state guards, only ChainRush was gracefully restarted. Fresh PID 13983, stopped in FrameworkStart, reads all three complete configurations identically to their frozen pre-restart/current-definition snapshots. PlayerBrain retains its 24 operators and 26 nodes and its consumer link. Both executor/target references of each Population agent retain the intended original spawner definitions. All nine authored external-reference slots across the seven named consumer files match both native and actual C# fields, including EnemyWaveAgent, the two Activity seeds and the Economy installer.

Previously repaired startup data also remains available: all eight complete definition configurations match the preceding frozen inputs; 84 skills, 62 resource-formula nodes and 52 economic outputs have no recorded null-definition/formula/output discrepancies; the three saved buff graphs match their frozen snapshots; ten Flow consumers, nine Activity/profile references and 98 Objective economic observations are valid. Original consumer files/metas remain byte-identical. There are no temporary rebuild assets, dirty inspected assets/scenes or script compilation failure. Details are in `completion-summary.json`, `fresh-final-read.json`, `consumer-fresh-read.json`, `previous-definitions-final-read.json` and `startup-chain-fresh-read.json`.

The approved authoring scope is complete. Runtime/service code and gameplay rules were not changed. The recovered planning graph removes the observed authoring obstruction to hero planning, but successful hero/allied/enemy materialization is still to be observed in the user's next manual run. The enemy-specific planning failure has not been established from the retained execution evidence and no speculative runtime change was made. The Editor remains open in stopped FrameworkStart. No game or tests were run.


## Units not created — next manual run after graph restoration

Reported symptom remains «Юниты не создаются». The user manually launched ChainRush and answered «готово». Reads targeted the same dedicated project log and PID 13983; no game or tests were started by the agent. Evidence is preserved in `deployment-recipe.json`, `after-fix-live-state.json`, `remaining-unit-consumers.json`, `remaining-unit-missing-reference-pairs.json`, `remaining-definition-fields.json` and `late-live-state.json` under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`.

**Verified causal boundary:** Hero Population now starts (assignment 1, node initial-hero). The log records its terminal failure: «Production recipe input '0' is invalid: Production input requires an asset». The selected HeroProductionHost has the intended HeroDeployment catalog; its first recipe is PerfumeDeployment. Both actual and native inputs[0].asset and outputs[0].asset are null in PerfumeDeployment; the same two fields are null in TabascoDeployment. Disk authoring retains the original GUIDs. PopulationProductionCandidates reads each catalog method through ProductionStateOrchestrationModule.TryReadCurrentMethod; ProductionProgressionResolver resolves the recipe; ProductionInputData.TryResolve rejects its null asset before amount calculation. Population reports Invalid and PopulationAgent fails the assignment. This explains why no hero order reaches admission in this run. The prior empty Brain explanation no longer applies: the new trace reaches Population. Selected Perfume, producer, catalog and flat amount progression are available; this is not the absence of a selection, an amount-policy rejection, or an unregistered producer.

**Verified related reference loss:** nine consumer/target pairs contain twelve authored external-reference slots missing from actual fields. These are:

- `Assets/Game/FrameworkUI/Data/Production/ColaUpgrade.asset` → `Assets/Game/Activities/Shared/Units/Cola/Cola.asset` (1 authored slots).
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe1.asset` → `Assets/Game/Activities/Shared/Units/Cola/ColaUnit.asset` (1 authored slots).
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe3.asset` → `Assets/Game/Activities/Shared/Units/Water/WaterUnit3.asset` (1 authored slots).
- `Assets/Game/FrameworkUI/Data/Production/WaterUpgrade.asset` → `Assets/Game/Activities/Shared/Units/Water/Water.asset` (1 authored slots).
- `Assets/Game/Activities/Autobattle/Production/WaterUnit2DeploymentRecipe.asset` → `Assets/Game/Activities/Shared/Units/Water/WaterUnit2.asset` (2 authored slots).
- `Assets/Game/FrameworkUI/Data/Production/PerfumeDeployment.asset` → `Assets/Game/Activities/Shared/Units/Perfume/Perfume.asset` (2 authored slots).
- `Assets/Game/FrameworkUI/Data/Production/PerfumeUpgrade.asset` → `Assets/Game/Activities/Shared/Units/Perfume/Perfume.asset` (1 authored slots).
- `Assets/Game/FrameworkUI/Data/Production/TabascoDeployment.asset` → `Assets/Game/Activities/Shared/Units/Tabasco/Tabasco.asset` (2 authored slots).
- `Assets/Game/FrameworkUI/Data/Production/TabascoUpgrade.asset` → `Assets/Game/Activities/Shared/Units/Tabasco/Tabasco.asset` (1 authored slots).

**Verified source inspection:** eight remaining definitions in the current Hero/Water/Cola catalog/form chain contain the malformed unused null managed-reference type record. All eight directly load as CapabilityHostData with current serialized fields and non-null authored external references; they are not dirty. Seven are targets of the missing references above. ColaUnit4 currently retains its consumer references; no failing branch involving it has been observed. It is included in the bounded proposal because it is the existing fourth form in the same inspected gameplay chain and has the same malformed unused table, not because a failure is claimed for it.

**Inference:** the malformed unused source registry is the leading serialization explanation, based on the successful retained-GUID fresh-body restoration of the preceding ten definitions. This run proves invalid recipe references and their effect on hero Population; it does not prove the internal Unity importer branch. Do not add runtime recovery or change Production validation to mask the null.

**Enemy status:** the initial captured bot replenishment process waited on a Composite dependency while progressing; its causal history is not retained. By the later read the time progress Objective was Completed and no replenishment processes remained. Cause of absent enemies is not established; do not change Composite planning, Population or enemy authoring rules on that evidence. Board still contains sixteen spatial cards; that does not imply successful hero/allied/enemy deployment.

### Bounded catalog/form source restoration — awaiting explicit approval

**Implementation decision: working model.** Explicitly restore the existing eight definition bodies and read all named consumers; no automatic migration or runtime repair mechanism.

**Existing System Fit:** Unity owns creation/deserialization/import of these ScriptableObject definitions and their persistent references. Economy definitions and the FrameworkProfile own catalog registration/initial content. Board recipes emit selected forms; Objectives request their materialization. Orchestration selects the existing Population path; Production resolves input/output definitions and validates/adopts orders; Activity owns materialization. These authoritative owners, callbacks, simulation budgets, tags, forms, policies and gameplay parameters remain unchanged. Reuse the demonstrated Unity fresh managed-object procedure and original identities. No new service, selector, profile or execution path.

**Best Practices:** Unity's [CopySerializedManagedFieldsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.CopySerializedManagedFieldsOnly.html) copies current managed serialized fields, allowing a fresh object to avoid carrying the malformed native reference registry. [AssetDatabase.CreateAsset](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.CreateAsset.html) owns temporary assets and their identities. Here the method is deliberately bounded by frozen field/reference comparisons, unchanged authored text before the unused registry and retained original metadata. A fresh Editor-process read checks persistent references rather than trusting same-process object assignment. This is the explicit operation already demonstrated above, not a general automatic rewrite.

Exact eight original definitions:

- `Assets/Game/Activities/Shared/Units/Cola/Cola.asset`.
- `Assets/Game/Activities/Shared/Units/Cola/ColaUnit4.asset`.
- `Assets/Game/Activities/Shared/Units/Cola/ColaUnit.asset`.
- `Assets/Game/Activities/Shared/Units/Water/WaterUnit3.asset`.
- `Assets/Game/Activities/Shared/Units/Water/Water.asset`.
- `Assets/Game/Activities/Shared/Units/Water/WaterUnit2.asset`.
- `Assets/Game/Activities/Shared/Units/Perfume/Perfume.asset`.
- `Assets/Game/Activities/Shared/Units/Tabasco/Tabasco.asset`.

Exact 26 unique consumers (two are also original definitions above):

- `Assets/Game/Activities/Autobattle/Objectives/ColaUnit4DeploymentObjective.asset`.
- `Assets/Game/Activities/Autobattle/Objectives/ColaUnitDeploymentObjective.asset`.
- `Assets/Game/Activities/Autobattle/Objectives/WaterUnit2DeploymentObjective.asset`.
- `Assets/Game/Activities/Autobattle/Objectives/WaterUnit3DeploymentObjective.asset`.
- `Assets/Game/Activities/Autobattle/Production/ColaUnit4DeploymentRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/ColaUnitDeploymentRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/WaterUnit2DeploymentRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/WaterUnit3DeploymentRecipe.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe2.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe3.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe1.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe4.asset`.
- `Assets/Game/Activities/Shared/Units/Cola/Cola.asset`.
- `Assets/Game/Activities/Shared/Units/Water/Water.asset`.
- `Assets/Game/FrameworkUI/Data/FrameworkProfile.asset`.
- `Assets/Game/FrameworkUI/Data/Production/ColaPurchase.asset`.
- `Assets/Game/FrameworkUI/Data/Production/ColaUpgrade.asset`.
- `Assets/Game/FrameworkUI/Data/Production/PerfumeDeployment.asset`.
- `Assets/Game/FrameworkUI/Data/Production/PerfumePurchase.asset`.
- `Assets/Game/FrameworkUI/Data/Production/PerfumeUpgrade.asset`.
- `Assets/Game/FrameworkUI/Data/Production/TabascoDeployment.asset`.
- `Assets/Game/FrameworkUI/Data/Production/TabascoPurchase.asset`.
- `Assets/Game/FrameworkUI/Data/Production/TabascoUpgrade.asset`.
- `Assets/Game/FrameworkUI/Data/Production/WaterPurchase.asset`.
- `Assets/Game/FrameworkUI/Data/Production/WaterUpgrade.asset`.
- `Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset`.

Approved-boundary sequence to perform only after explicit answer:
1. Preserve the current live trace, eight complete field snapshots, all original definition/consumer files and metadata. Read-only backups already include 64 files in `remaining-unit-originals/` and hashes in `remaining-unit-original-manifest.json`. Stop only the current ChainRush Play Mode after approval. Require stopped/idle status and no unsaved scenes/assets before mutation.
2. For Perfume first, create a same-folder temporary PerfumeRebuild CapabilityHostData through Unity. Copy current managed serialized fields, explicitly restore original name/hide flags, and compare every setting/reference to the frozen source. Require its generated body to match original authored text before the unused reference registry and contain only the intended empty unused table in place of the malformed record. Replace only the original .asset body with the verified Unity-generated body; retain .meta, GUID and main fileID 11400000. Delete the temporary asset/meta through Unity. Do not save the currently null consumer fields over their original authoring.
3. Only after the representative field/body checks succeed, apply that same guarded operation to the other seven named definitions. Any disagreement stops further mutation with evidence; no defaults, inferred settings, new GUIDs or ad hoc reference rebinding.
4. Explicitly import all 26 named consumers. Their authored files and original metadata must stay unchanged. Do not unload individual assets or rewrite a whole project. Preserve the dedicated log and gracefully restart only ChainRush after clean-state guards.
5. In the fresh Editor, compare all eight complete configurations and metadata; read every authored consumer/target pair natively and through actual C# fields. Require restoration of the twelve currently missing slots and retention of all previously valid links. Recheck the previously restored planning, spawner and startup graphs. Leave Editor stopped; no Play Mode or tests are started. Gameplay success remains to be observed in a user run.

Separate approval is required under rule 11 PROJECT_RULES.md because these eight definition bodies, their consumer imports and stopping/restarting the current Editor session were not part of the completed PlayerBrain/two-spawner authoring scope.


### Catalog/form source restoration — approved and completed

The user explicitly approved restoration of the eight definitions and stopping the current Play Mode. **Implementation decision: working model.** ChainRush PID 13983 was stopped; all subsequent authoring required stopped/idle state and clean scenes/assets. The current run, nine missing consumer/target pairs, complete definition settings and 64 original files/metadata were preserved outside the project.

Perfume was recreated first through a Unity-owned temporary CapabilityHostData. Its entire managed configuration and all external references matched the frozen source before and after CreateAsset/save; the generated body matched original authoring before the unused managed-reference registry. Its original .meta, GUID and main fileID 11400000 were retained. The only body difference was the empty unused reference table replacing malformed null-type metadata. Unity deleted the temporary asset and its metadata. Those successful representative checks allowed the same guarded operation for Tabasco, Water, Cola, WaterUnit2, WaterUnit3, ColaUnit and ColaUnit4.

All 26 named consumers were explicitly imported. Across the 64 preserved files, exactly the eight approved original definition bodies changed; all original metadata and the 24 other consumer bodies remained byte-identical. Water and Cola are both definitions and consumers, so their only changes are their separately approved body restorations. No consumer reference assignment, gameplay parameter change or runtime code edit was made.

After clean-state checks and dedicated-log preservation, only ChainRush was gracefully restarted. Fresh PID 14756 is stopped. All eight complete field snapshots match the frozen pre-operation inputs. All 38 consumer/target pairs now resolve through both native SerializedObject and actual C# fields (44 external-reference slots, 48 actual fields). All twelve previously missing slots are present; all previously available references remain. All eight original identities/main fileIDs are retained and no temporary rebuild asset/meta remains.

Previously restored data also survives: PlayerBrain has 24 non-null operators and 26 non-null decision nodes and its consumer link; all four spawner criteria point to their intended original definitions. Full configurations of PlayerBrain/two spawners and the preceding eight restored resources match their frozen inputs. The startup read retains 84 skills, 62 resource-formula nodes, 52 economic outputs, three unchanged saved buff graphs, ten Flow consumers, nine Activity/profile references and 98 Objective economic observations. No inspected asset is dirty and script compilation failure is false.

Evidence: `remaining-unit-restoration/completion-summary.json`, `fresh-consumers.json`, `fresh-fields.json`, `previous-planning.json`, `previous-definitions.json`, `startup.json`, generated-body snapshots and preserved originals. This explicit before/after fresh-process read demonstrates restoration of the catalog/form references; the internal importer implementation was not traced. The approved authoring scope is complete. Gameplay materialization and the enemy-specific planning branch still require observation in the user's next manual run; they are not inferred from static readback. No game or tests were started by the agent.

## Enemy content references — manual run after catalog/form restoration

Reported symptom remains «Юниты не создаются». During the post-restart reads the user manually entered Play Mode in ChainRush PID 14756. All subsequent live reads targeted that project and its dedicated log. The agent did not start Play Mode or tests.

**Verified:** the selected Perfume hero is now spatial Entity 240 in Activity 2, with prototype 239. Initial-hero Population assignment 1 completed. Board has sixteen spatial cards and its initial fill completed. The previous null deployment input is no longer blocking this run. The bot replenishment root selected the progress branch of its OR requirement after excluding the materialized-enemy branch; no enemy Population assignment was admitted in the retained snapshots.

**Verified authoring:** Level01PopulationAgent and Level02PopulationAgent retain four releases, their intended shares, flat volumes, spatial distribution and work budget. All eight PopulationAssetContentSourceData objects in each agent exist, but their actual Asset fields are null (sixteen slots total). Their authored GUIDs remain on disk. Four recipe outputs also have null references: EnemyWaveRecipe → BugBrownSmall, BugGreenSmallRecipe → BugGreenSmall, BugPurpleSmallRecipe → BugPurpleSmall and BugPurpleMediumRecipe → BugPurpleMedium. The other two recipe output references and all six Economy installer references are currently available. All six existing enemy definitions load directly with complete settings, but contain the same malformed unused null managed-reference type record as the previously restored definitions. No duplicate registry IDs were found in the eleven consumers; no graph rewrite is proposed.

**Verified ownership/call path:** OrchestrationProcessPlanner evaluates the bot graph; AgentDecompOpData matches the materialized-entity requirement, checks applicability and asks PopulationAgentFactory.TryBuildAssignmentDependencies to prepare the assignment. That method first validates PopulationAgentData, its releases, rules and content sources. PopulationAssetContentSourceData.TryValidate rejects a null asset before assignment creation. Production recipe resolution also requires valid output definitions. The registered agent applicability has no recorded failure; bot graph operators and agent links are present. Original Loc01Lvl01/Loc01Lvl02 authoring retains the same enemy composition as the current four releases.

**Inference:** the null content sources are a concrete invalid input and are consistent with rejection before assignment admission. The retained trace does not identify the exact rejected child validation branch, so the runtime cause of absent enemies is not established. The malformed unused source registry is the leading explanation for persistent reference loss, supported by the preceding successful retained-GUID restorations; Unity's internal importer branch was not traced. No runtime validation, planning or gameplay behavior will be changed on this inference.

Evidence is preserved under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`: `enemy-consumers.json`, `enemy-definition-fields.json`, six `*.enemy-before-fields.json` complete snapshots, `enemy-definition-inventory.json`, `original-enemy-composition.json`, `remaining-unit-restoration/new-live-state.json`, `new-replenishment-facts.json`, `new-planning-detail.json`, `enemy-agent-authoring.json`, the dedicated log and 34 original asset/meta files in `enemy-definition-originals/`, with `enemy-definition-original-manifest.json`.

### Bounded enemy definition restoration — awaiting explicit approval

**Implementation decision: working model.** Restore only the six existing definition bodies, retain their identities and read back the existing consumers. No automatic repair mechanism, new content or runtime change.

**Existing System Fit:** Unity owns serialized definition bodies and persistent-reference import. Economy owns definition registration; Population owns the existing composition and placement rules; Orchestration owns assignment planning; Production owns recipe validation/admission/output; Activity owns materialization and cleanup. Reuse the demonstrated Unity fresh managed-object procedure. These domain owners, their events, budgets, policies, tags and current content remain unchanged. The previously restored hero/board chain is read again to detect regression, not reauthored.

**Best Practices:** Unity's [CopySerializedManagedFieldsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.CopySerializedManagedFieldsOnly.html) supplies the current managed settings to a fresh object without carrying the original native reference registry. [AssetDatabase.CreateAsset](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.CreateAsset.html) owns temporary asset creation and identity. Apply this only to the named sources, gated by complete field/reference comparisons and unchanged authored body before the unused registry; retain original metadata. Fresh-process reads check persistence beyond same-process assignments.

Exact source definitions, all under `Assets/Game/Activities/Autobattle/Economy/`:

- `BugBrownSmall.asset`.
- `BugGreenSmall.asset`.
- `BugBrownMedium.asset`.
- `BugPurpleSmall.asset`.
- `BugGreenMedium.asset`.
- `BugPurpleMedium.asset`.

Exact eleven unique consumers:

- `Assets/Game/Activities/Autobattle/Agents/EnemyWaveAgent.asset`.
- `Assets/Game/Activities/Autobattle/Agents/Level01PopulationAgent.asset`.
- `Assets/Game/Activities/Autobattle/Agents/Level02PopulationAgent.asset`.
- `Assets/Game/Activities/Autobattle/Objectives/EnemyWaveObjective.asset`.
- `Assets/Game/Activities/Autobattle/Production/BugBrownMediumRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/BugGreenMediumRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/BugGreenSmallRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/BugPurpleMediumRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/BugPurpleSmallRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Production/EnemyWaveRecipe.asset`.
- `Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset`.

After explicit approval:

1. Stop only the current ChainRush Play Mode. Require stopped/idle status and clean scenes/assets before authoring. Preserve the live evidence and original files already captured; never save null consumer fields over their authored references.
2. Start with BugBrownSmall. Unity creates a same-folder temporary CapabilityHostData, copies current serialized managed fields, restores original name/hide flags and saves it. Compare all settings/references with the frozen source and require unchanged authored text before the unused registry. Replace only the original .asset body with the verified generated body; retain original .meta, GUID and main fileID 11400000. Delete the temporary asset/meta through Unity. A disagreement stops further mutation with evidence.
3. Only after representative comparisons pass, apply the same guarded operation to the other five sources. No parameter changes, new GUIDs or inferred reference rebinding.
4. Explicitly import the eleven consumers; require their authored bodies and original metadata to remain byte-identical. Preserve the dedicated log; after clean-state guards gracefully restart only ChainRush.
5. In the fresh stopped Editor compare all six full configurations/identities, all original file hashes and every consumer/source pair through native serialized fields and actual C# fields. Require restoration of the twenty currently missing slots and retention of valid links. Read the previous repaired definition/planning/startup chain again. Leave Editor stopped; no game or tests are started. Enemy materialization remains to be observed in a user run.

Separate approval is required by rule 11 PROJECT_RULES.md: these six definition bodies and their eleven consumer imports were not in the completed eight-definition catalog/form restoration. Approval of that completed scope does not authorize stopping the user's new run or another Editor restart.

### Enemy definition restoration — approved and completed

The user explicitly approved stopping Play Mode and restoring all six sources. **Implementation decision: working model.** Only ChainRush PID 14756 was stopped. Stopped/idle and clean-scene/asset guards preceded all authoring. Original files, complete snapshots and dedicated logs were preserved outside the project.

BugBrownSmall passed the representative comparison. Unity created a fresh temporary CapabilityHostData with identical managed fields, external references, name/hide flags and authored text before the unused registry. Its verified generated body replaced only the original .asset body, retaining original .meta, GUID and main fileID 11400000. Unity deleted the temporary asset/meta. The other five named sources passed the same checks. The only changes in all six bodies replace malformed unused null-type metadata with an empty unused reference table. All eleven consumer bodies and all original metadata remain byte-identical. No consumer fields, wave composition, source graphs, runtime code or gameplay rules were rewritten.

All eleven consumers were explicitly imported. After clean-state guards and dedicated-log preservation, only ChainRush was gracefully restarted. Fresh PID 15793 is stopped and clean. All six complete configurations match the frozen inputs; all identities/main fileIDs remain original; no temporary rebuild asset/meta remains. All 26 consumer/source pairs resolve natively and through actual C# fields: 30 authored external-reference slots and 31 actual fields. All twenty previously missing slots are restored: sixteen Population content assets and four recipe outputs. Previously valid links remain present.

The previous repairs also survive fresh-process reads. The eight catalog/form configurations match their frozen settings, with all 38 consumer/source pairs, 44 native slots and 48 actual fields present. PlayerBrain retains 24 non-null operators and 26 non-null decision nodes and its consumer link; both spawner configurations and all four Population spawner criteria remain correct. The preceding eight resource/form configurations match their frozen inputs. The startup graph retains 84 skills, 62 resource-formula observations, 52 economic outputs, three saved buff graphs, ten Flow consumers, nine Activity/profile references and 98 Objective economic observations without the inspected discrepancies. Script compilation failure is false; inspected assets/scenes are clean.

Evidence: `enemy-definition-restoration/completion-summary.json`, `fresh-fields.json`, `fresh-consumers.json`, `final-identity.json`, `catalog-fields.json`, `catalog-consumers.json`, `previous-planning.json`, `previous-definitions.json`, `startup.json`, generated bodies/diffs, preserved originals and dedicated logs. The approved authoring scope is complete. Fresh-process readback proves restoration of the intended references, but does not prove enemy/allied-unit materialization in a new game run. No game or tests were started by the agent. Further runtime changes require new execution evidence; no speculative runtime fix is included.


## Drop and Board references — new manual run after enemy restoration

Reported symptom remains «Юниты не создаются». The user manually launched ChainRush and answered «Готово; Play Mode оставлен включённым». All reads targeted PID 15793, the explicit project path and its dedicated log; no Play Mode or tests were started by the agent.

**Verified gameplay progress:** the new snapshot contains one spatial Perfume and six spatial BrownSmall enemies. Initial-hero and repeated replenishment Population assignments completed; Autobattle and Board are Running. Thus enemy materialization now reaches completion, beyond the previously excluded candidate branch. Sixteen initial Board cards remain, but no allied Water/Cola materialization has been observed in the retained snapshot.

**Verified causal boundary for experience:** the dedicated log reports Drop requests 1 and 2 rejected with «Production recipe output '0' is invalid: Production output requires an asset». Actual ExperienceDropRecipe.Outputs[0].Asset is null; the authored GUID points to the existing ExperienceDrop definition. Recipe, flat progression, Token form and profile preparation are present. The call path is DropAIBrainActionData → DropService.TryStart → DropRuntime.TryInitialize/ContinuePreparation → StartContainerProduction → TryValidateContainerProduction → ProductionProgressionResolver.TryEvaluateYield → ProductionRecipeData.TryResolveOutputs → ProductionOutputData.TryResolve. The asset-null branch rejects before amount evaluation/order creation. Rollback/compensation completes the rejected result, removes active indices, releases retained wallet resources and publishes DropResultEvent; AI logs that result. Readback retains rejected results with the same failure. The nearby competing amount/form and missing recipe/profile explanations do not fit this reached branch. No placement, materialization or collection success is claimed for this run.

**Verified Board state:** the turn-token requirement remains WaitingDependencies on Experience. The Water/Cola deployment Objectives are Inactive. Broader read-only inspection of the remaining malformed linked definitions in the current Activity/UI/runtime content found 25 sources and 110 consumers. Only thirteen sources have currently missing consumer references: 28 authored external-reference slots are absent from native and actual fields. This includes ExperienceDrop output and the collection skill selector, PlayerSpawner in TurnTokenProductionAgent, BoardHost in Board seed/agent criteria, three Board producer seeds, seven card types in Objectives and Water/Cola card recipes. The other twelve inspected source definitions have available consumer references and are outside the proposed restoration scope. All thirteen directly load with complete serialized configurations and are not dirty.

**Inference:** missing Board definitions/criteria/seeds are invalid authored inputs and can obstruct intended selection, production and initial content. Their individual runtime consequences were not all traced; do not describe the entire Board failure as a confirmed single cause. The malformed unused source registries remain the leading serialization explanation, based on repeated fresh-body repairs whose references survived Editor restart. Unity's internal importer branch was not traced. No runtime fallback or validation change is proposed.

**Existing System Fit:** Unity owns definition import and persistent references. Activity owns existing seed and content lifecycle; Economy owns wallets/definition registration; Objectives and Orchestration own requests and assignment selection; Population/Production own existing card selection, recipe validation and issuance; Drops owns container preparation/transfer/placement, compensation and terminal results; Skills owns experience collection. Repair their existing authored references rather than adding a parallel path or changing those lifecycle owners. Retain all current quantities, forms, costs, sources, tags, budgets, pools, recipes, capabilities and gameplay rules.

### Bounded Drop/Board source restoration — awaiting explicit approval

During the approved authoring operation the user additionally reported: «Еще не создаются карточки юнитов для доски». Keep this symptom distinct from allied-unit materialization. WaterBoardBase/ColaBoardBase, their recipe outputs, the Board seed and agent criteria are already in the approved thirteen-source/forty-consumer scope. Their missing references are verified above, but the complete runtime cause of absent unit cards is not established from that alone. After restoration, inspect unit-card generation and selection in the user's next manual run; do not declare card generation fixed from persistent-reference readback alone.

**Implementation decision: working model.** Explicitly restore only the thirteen named source bodies. This is authored-content restoration using Unity-generated bodies, not an automatic migration or runtime repair mechanism.

**Best Practices:** reuse Unity's [CopySerializedManagedFieldsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.CopySerializedManagedFieldsOnly.html) and [AssetDatabase.CreateAsset](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.CreateAsset.html). A fresh managed object separates the current settings from the malformed native unused registry; Unity owns temporary creation/identities. Require frozen field/reference equivalence, unchanged authored text before that registry and unchanged original metadata. A new Editor process then checks persistent references beyond same-process caches. This is the same bounded procedure demonstrated by the preceding restorations, applied to concrete current gameplay content.

Exact thirteen source definitions:

- `Assets/Game/Activities/Board/Economy/LightningBoltBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/SpeedBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/BoardHost.asset`.
- `Assets/Game/Activities/Board/Economy/SkillsPopulationProducer.asset`.
- `Assets/Game/Activities/Board/Economy/HealBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/GoldBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/BoostersPopulationProducer.asset`.
- `Assets/Game/Activities/Board/Economy/WaterBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/BoardPopulationProducer.asset`.
- `Assets/Game/Activities/Board/Economy/HealthBoardBase.asset`.
- `Assets/Game/Activities/Board/Economy/ColaBoardBase.asset`.
- `Assets/Game/Activities/Autobattle/Economy/PlayerSpawner.asset`.
- `Assets/Game/Activities/Autobattle/Economy/ExperienceDrop.asset`.

Exact forty unique consumers:

- `Assets/Game/Activities/Autobattle/Agents/TurnTokenProductionAgent.asset`.
- `Assets/Game/Activities/Autobattle/Definition/AutobattleActivity.asset`.
- `Assets/Game/Activities/Autobattle/Definition/DistanceActivity.asset`.
- `Assets/Game/Activities/Autobattle/Production/ExperienceDropRecipe.asset`.
- `Assets/Game/Activities/Autobattle/Skills/ExperienceCollectionSkill.asset`.
- `Assets/Game/Activities/Board/Agents/BoardPopulationAgent.asset`.
- `Assets/Game/Activities/Board/Agents/BoardSelectionAgent.asset`.
- `Assets/Game/Activities/Board/Definition/BoardActivity.asset`.
- `Assets/Game/Activities/Board/Objectives/BoardMergeObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/BoardPopulationObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/ColaSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/GoldSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/HealSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/HealthSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/LightningBoltSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Objectives/SpeedSelectionObjective.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe1.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe2.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe3.asset`.
- `Assets/Game/Activities/Board/Production/BoardMergeRecipe4.asset`.
- `Assets/Game/Activities/Board/Production/ColaBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe1.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe2.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe3.asset`.
- `Assets/Game/Activities/Board/Production/ColaMergeRecipe4.asset`.
- `Assets/Game/Activities/Board/Production/GoldBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/GoldSelectionRecipe.asset`.
- `Assets/Game/Activities/Board/Production/HealBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/HealthBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/LightningBoltBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/SpeedBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Production/WaterBoardBaseRecipe.asset`.
- `Assets/Game/Activities/Board/Skills/IssueHeal.asset`.
- `Assets/Game/Activities/Board/Skills/IssueHealthBuff.asset`.
- `Assets/Game/Activities/Board/Skills/IssueLightningBolt.asset`.
- `Assets/Game/Activities/Board/Skills/IssueSpeedBuff.asset`.
- `Assets/Game/Activities/Shared/Units/Cola/Cola.asset`.
- `Assets/Game/Activities/Shared/Units/Perfume/Perfume.asset`.
- `Assets/Game/Activities/Shared/Units/Water/Water.asset`.
- `Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset`.

Approved-boundary sequence, only after an explicit answer:

1. Preserve the live snapshot/log, 28 missing slots, thirteen complete field snapshots and the original 106 source/consumer files and metadata. Stop only the current ChainRush Play Mode. Require stopped/idle state and clean scenes/assets before mutation. Never save null consumer fields over the on-disk authored references.
2. Start with ExperienceDrop. Create a same-folder temporary CapabilityHostData through Unity; copy its current managed serialized fields, restore original name/hide flags and compare the whole configuration/reference graph to the frozen source. Require generated authored text before the unused registry to remain identical. Replace only the original .asset body with the verified Unity-generated body; preserve original .meta, GUID and main fileID 11400000. Delete the temporary asset/meta through Unity. Stop further mutation if any comparison fails.
3. Only after the representative passes, restore the other twelve named sources with the same guards/comparisons. Their source configurations do not reference another source being rebuilt. No defaults, inferred source binding, new identities or parameter changes.
4. Explicitly import all forty named consumers. Their bodies and all original metadata must remain byte-identical. Preserve the dedicated log and gracefully restart only ChainRush after clean-state guards.
5. In the fresh stopped Editor compare all thirteen complete settings/identities, all preserved file hashes, every named source/consumer pair and the 28 previously missing slots through native and actual fields. Require valid references to remain present. Read the previously repaired hero, enemy, Board-resource and startup chain again. Leave Editor stopped; no game or tests are started. Drop collection, Board selection/merge and allied deployment still require observation in a user run.

Evidence under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`: `enemy-definition-restoration/manual-run-state.json`, `drop-confirmed-boundary.json`, `remaining-malformed-linked-definitions-inventory.json`, `remaining-linked-consumers.json`, `remaining-linked-missing-pairs.json`, `remaining-board-drop-definition-inventory.json`, `board-drop-definition-fields.json`, thirteen `*.board-drop-before-fields.json` snapshots, `board-drop-originals/`, `board-drop-original-manifest.json` and the dedicated log. The 106 original files have been backed up; this is read-only preservation, not authorization for repair.

Separate approval is required by rule 11 PROJECT_RULES.md: these thirteen definitions, forty consumer imports and stopping/restarting the new run were not covered by the completed six-enemy-definition restoration. Runtime code and game rules remain outside this proposed authoring change.

### Drop/Board source restoration — approved and completed

The user explicitly approved all thirteen sources, forty consumer imports and stopping the current Play Mode. **Implementation decision: working model.** ChainRush PID 15793 was stopped. Stopped/idle and clean-scene/asset guards preceded authoring. ExperienceDrop passed the representative Unity-generated-body, full-field/reference and authored-prefix comparisons; the same procedure then succeeded for the other twelve sources.

Only the thirteen original definition bodies changed, replacing malformed unused null-type metadata with empty unused reference tables. The original .meta files, GUIDs, main fileIDs 11400000 and all serialized parameters remain unchanged. Unity created and deleted every temporary rebuild asset/meta. All forty consumers were explicitly imported. Across 106 preserved files, all consumer bodies and original metadata remain byte-identical. No output, selector, seed, recipe, amount, runtime tag, pool, capability, gameplay rule or runtime code was reauthored.

After dedicated-log preservation and clean-state guards, only ChainRush was gracefully restarted. Fresh PID 16784 is stopped in FrameworkStart, clean and idle. All thirteen complete configurations match the frozen pre-operation snapshots, all original identities/main fileIDs remain and no temporary rebuild asset/meta exists. All 61 consumer/source pairs resolve in native serialized fields and actual C# fields: 79 native external-reference slots and 86 actual fields. All twenty-eight previously missing slots are restored, including ExperienceDropRecipe output, ExperienceCollectionSkill selector, Water/Cola card recipe outputs, Board seed/criteria and Objectives, and the PlayerSpawner criterion.

Previous restorations also survive: six enemy and eight catalog/form configurations match their frozen snapshots and all their consumer/source pairs remain valid; the preceding eight resources/forms and PlayerBrain/two spawners retain their full settings. PlayerBrain has 24 non-null operators and 26 non-null decision nodes and its consumer link; all four Population spawner criteria remain correct. Startup reads retain 84 skills, 62 resource-formula observations, 52 economic outputs, three unchanged saved buff graphs, ten Flow consumers, nine Activity/profile references and 98 Objective economic observations. No inspected discrepancy or script compilation failure was reported.

The broader previously inspected cohort was also read in the fresh process: all 180 source/consumer pairs across 25 source definitions and 110 consumer files now resolve without the inspected discrepancies. The twelve sources outside the approved restoration remain unchanged. Results are in `all-inspected-links.json` and `all-inspected-links-summary.json`; these are reference reads, not gameplay runs.

Current evidence is stored in `board-drop-restoration/`: `completion-summary.json`, `fresh-fields.json`, `fresh-consumers.json`, `final-identity.json`, `enemy-fields.json`, `enemy-consumers.json`, `catalog-fields.json`, `catalog-consumers.json`, `previous-planning.json`, `previous-definitions.json`, `startup.json`, the generated-body files and logs. A readback helper initially wrote the new PID 16784 results into the previous enemy-restoration evidence directory; those outputs were copied to this current directory and the overwrite is explicitly recorded in `enemy-definition-restoration/readback-location-note.json`. Original backups, frozen definition snapshots, generated bodies, manifests and the prior completion summary remain; never interpret the later readback PID as the earlier verification session.

The approved restoration scope is complete. These reads prove persistence of the intended references. They do not by themselves prove Drop collection, unit-card generation, selection/merge or allied materialization. The added literal unit-card symptom remains part of the next manual-run observation. No game or tests were started by the agent; no speculative runtime change was made.

### Allied unit disappears immediately — manual-run evidence and proposed diagnostic

Reported symptom, literally: «Готово. Теперь юниты создаются но сразу исчезают». The separate preceding report «Еще не создаются карточки юнитов для доски» remains recorded; these are different lifecycles.

**Verified:** read-only inspection of ChainRush PID 16784, in the user's running Play Mode, found a completed ColaUnit3DeploymentRecipe order `2:4`, one completed yield, confirmed admission and no Production failure. Its caller is `orchestration-process:2:1162:1`; its retained `OutputSourcesByIndex` is empty. Existing Projection pool history records ColaUnit3 Entity 471 bound successfully at frame 3971, EntityRemoved at frame 3972 and BindingRemoved afterwards. There is no preceding BackingTokenRemoved record for this unit. This disproves the nearest competing explanation that a failed pool rent or presentation-only disappearance prevented its creation. The three Cola cards instead record BackingTokenRemoved → EntityRemoved → BindingRemoved at frame 3968, matching completed ColaMergeRecipe3 order `3:17`.

**Verified:** the surviving prototype 470 is an isolated ColaUnit3 realization, with only FormHealthScale = 1000 in its original Attributes, Health current = 0 and maximum = 0. The separate Cola prototype 445 has Health = 100000 and the remaining character Attributes. Runtime asset records identify the deployed Token's source as the authored ColaUnit3 asset and its origin as null; the existing correctly sourced Cola card Tokens retain the runtime Cola origin. Thus the deployed form did not receive the character's base Attributes through the selected-source path. Actual runtime records, rather than only YAML, establish this distinction.

**Inference:** a Health <= 0 transition may execute RemoveEntityAIBrainActionData immediately. The unit brain contains that transition and removal action, but the existing pool history does not retain the caller of Entity removal or the instance's value immediately before removal. BehaviorViewer capture was disabled and has no retained trace. The removal cause is therefore **not established**; do not change AI, Health initialization, production source routing or add character stats to the form based solely on this inference.

The input-consumption source path also requires follow-up: its eligible canonical recipe methods have OutputIndex = -1, while its current source-selection branch tests Method.OutputAsset and uses Method.OutputIndex for a single binding. Materialization payloads do not carry the optional output source either. These are inspected code gaps, not permission for a new multi-output contract. Inspect and explicitly discuss the concrete correction after observing the causal branch.

**Existing System Fit:** Orchestration selects recipe execution and its economic source; Production owns admission, reservation, the retained order and issuance; Economy owns runtime asset source/origin identities; CapabilityHostService prepares/reuses the Activity prototype and composes instance state; CapabilityHostProjectionController and HostValueService calculate Attributes and values before capabilities attach. AIBrainService evaluates transitions and executes RemoveEntityAIBrainActionData; CapabilityHostService/EntityService remove the Entity, Activity tracks the materialized Token and Projection returns its view to the existing pool. All these paths were inspected. No new removal owner, pool, event bus, health policy or source inference is proposed.

**Best Practices:** diagnose at the existing action-execution boundary before cleanup erases the instance state. Use a bounded diagnostic rather than continuous per-frame output, following the bounded-history approach already used by ProjectionPoolDiagnostics and BoundedDiagnosticHistory. Unity's existing warning channel supports the diagnostic message and object context: [Debug.LogWarning](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Debug.LogWarning.html). Warning text is evidence only and is never consumed for execution.

**Approved diagnostic (minimum seam; temporary):** stop only the current ChainRush Play Mode, then change only `MorbooFrameworkPackage/Scripts/Core/AI/AIBrainService.cs`. Immediately before executing an existing RemoveEntityAIBrainActionData, record Entity, frame/tick, brain, current node/state, CapabilityHost definition/prototype and the current/minimum/maximum snapshots for the HostValue conditions leading to that state. Include the call stack in the same warning. Emit at most once per brain identity and at most 32 warnings per runtime session; clear the bounded diagnostic set in the existing AI Reset lifecycle. No extra subscription, timer, polling, gameplay mutation or new authored setting. After the user's next manual run, preserve the causal evidence and remove this temporary diagnostic. Do not start Play Mode or tests.

Evidence: `/tmp/chainrush-unit-spawn-diagnosis-20261006/unit-disappearance-state.json`, `disappearance-history.json`, `disappearance-prototype.json`, `disappearance-route.json` and their tool envelopes. The first live snapshot was captured at frame 9139; later reads at frames 16656, 18067 and 22319 belong to the same continuously running user session. The hero subsequently disappeared at frame 12238, so later absence must not be attributed to initial launch failure. No assets or runtime behavior were changed in this observation.

### Removal diagnostic — approved and installed

The user explicitly approved the bounded diagnostic and stopping the current ChainRush Play Mode. PID 16784 was stopped; the dedicated log and the pre-edit AIBrainService source were preserved. Only AIBrainService received the temporary diagnostic: one warning per brain identity, at most 32 per AI runtime session, immediately before the existing removal action. The diagnostic only reads state and cannot prevent removal if its own read fails. No gameplay settings or authored assets changed.

Unity compiled the amended script while stopped. Two namespace-resolution errors in the first diagnostic draft were corrected within the same approved file; final readback reports scriptCompilationFailed = false, isPlaying = false, isCompiling = false and isUpdating = false. Reflection confirms the compiled TraceRemovalAction and limit 32. One read command crossed the assembly reload and returned Connection reset by server; the subsequent read completed successfully. Evidence is under `unit-removal-diagnostic/`, including `diagnostic-only.patch`, `source-identity.json` and `final-compiled-read-tool.json`.

The diagnostic is ready for the user's manual reproduction; no game or tests were started. This completes installation only. The exact removal branch and the source-routing correction remain unresolved until that run supplies evidence; no AI, production-source or Health behavior was changed.

### Allied deployment source — confirmed removal, proposed correction

Reported symptom, literally: «Готово. Теперь юниты создаются но сразу исчезают».

**Verified:** in the next user-started run, PID 16784, ColaUnit3 Entity 471 binds successfully at frame 727. At frame 728, tick 111, the approved warning records brain `chainrush.ai.colaunit3`, combat node, `defeat`, `OnEnter`, instance level and prototype 470. Its Health exists, with Current = 0, Minimum = 0 and Maximum = 0; the transition condition is LessOrEqual 0. The stack reaches the existing pending-enter/action execution boundary immediately before RemoveEntityAIBrainActionData. Existing pool history records EntityRemoved and then BindingRemoved at the same frame, with no preceding BackingTokenRemoved. The cause of this immediate removal is established: the unit enters defeat with zero Health and AI deletes its Entity. Pool failure or consumption of the unit's backing Token did not trigger this removal.

**Verified:** completed order `2:4` is ColaUnit3DeploymentRecipe, one confirmed yield and no failure, with caller `orchestration-process:2:430:1` and no retained output sources. The issued asset has authored ColaUnit3 as source and null origin. Prototype 470 consequently has only FormHealthScale = 1000, with derived Health = 0. The separately prepared Cola prototype 445 has the character's Attributes, including Health = 100000. Its runtime realization contains ColaUnit3 in Forms. Thus the available character state was not used by this deployment.

**Checked code invariant:** ProductionStateOrchestrationModule emits one canonical consumption method per recipe, OutputIndex = -1, OutputAsset = null. ProductionInputConsumptionEligibilityEvaluator accepts only that canonical method. ProductionInputConsumptionDecompOpData resolves the authored selected-owner/form selectors, but then checks method.OutputAsset to build a binding. That check can never succeed for an eligible canonical consumption method. Production receives no binding and its existing issuance path uses the authored output, as recorded in this order. Existing source validation and prototype composition already support the explicit selected realization/form; no new inference is needed there. The historical selected planning candidate has already been released, so no retained candidate dump is claimed; the invariant, completed order, issuance identities, prototype values and removal trace are separately preserved evidence.

**Existing System Fit:** Orchestration's input-consumption operator owns source selection and executes a recipe once, independent of its number of outputs. ProductionDecompOpUtility owns prerequisites and endpoint payload construction. ProductionExecutionEndpointPayload and ProductionOrchestrationEndpointRuntime carry the command to the existing ProductionOrderRequest.OutputBindings list. ProductionRuntime.OutputSources owns exact-handle validation and resolution; Economy preserves source/origin; CapabilityHostService and CapabilityHostPrototypeState compose the prepared character prototype with the selected form; projection/HostValues initialize the instance. AI, Entity and Projection perform normal defeat cleanup. Preserve all these owners. Materialization endpoint, Population, Skills, UI purchases and Rewards were inspected; none receives this operator's configured output source. Their execution contracts are outside this bounded correction, and no new source-selection path is added to them.

**Implementation decision: working model, explicitly approved.** Preserve the canonical method and current authoring. For eligible input-consumption recipes, select sources by their real recipe outputs and carry all configured output bindings through the existing production endpoint. Do not fabricate Health, copy character stats into form assets or change defeat behavior.

| File in MorbooFrameworkPackage | Concrete change |
|---|---|
| Scripts/Core/Production/Orchestration/ProductionInputConsumptionDecompOpData.cs | Use method.Recipe.Outputs, preserving each real output index, to match sourceOutputTags. Resolve each matching output against the already captured sourceOwners/sourceEntries map; missing or ambiguous configured sources do not create an unsourced option. Outputs outside the configured tags keep their existing authored issuance. Produce one binding list and one option for the canonical recipe, not one order per output. |
| Scripts/Core/Production/Orchestration/ProductionDecompOpUtility.cs | Accept the binding list for production options. Add the existing prototype preparation requirement once per exact source realization, even when several outputs use it. Carry the list in the production payload. Keep the materialization entrypoint's existing no-binding path. |
| Scripts/Core/Production/Orchestration/ProductionExecutionEndpointPayload.cs | Replace the single nullable OutputSource with an explicit list of the existing ProductionOrderOutputBinding type. No new DTO or authoring field. |
| Scripts/Core/Production/Orchestration/ProductionOrchestrationEndpointRuntime.cs | Forward the payload list into ProductionOrderRequest.OutputBindings. Existing preflight, admission, payment, asynchronous waits, terminal events and cancellation remain authoritative. |
| Tests/EditMode/AgentAssignmentEditModeTests.cs | Update the existing canonical-consumption fixture and add assertions for selected sources, actual output indices and preserving one recipe option. Update any existing payload/source assertions affected by the contract. Do not execute tests. |
| Scripts/Core/AI/AIBrainService.cs | Remove the complete approved temporary diagnostic after preserving the manual-run evidence; retain normal AI behavior. |

**Best Practices:** retain explicit dependencies and encapsulation: the orchestration command carries the exact source handles; Production validates them through its existing boundary, and the existing prototype implementation calculates state. This avoids another stat resolver, automatic parent inference or a second issuance path. These are concrete uses of the explicit-dependency, encapsulation and DRY principles in [Microsoft architectural principles](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles).

**Cost and failure boundaries:** resolve the source owner/form selections once per existing planning call, reuse its definition-to-handle map and traverse only the current recipe's outputs. Deduplicate preparation facts by exact realization identity. No frame polling, wallet reads per output beyond the captured map, cloned graphs, new registries, subscriptions or diagnostic retention after the observation. Production continues to reject a stale source at admission; no fallback to authored unsourced issuance for a configured source. Runtime source validation, Economy, prototype propagation, Attributes/HostValues and gameplay assets do not change. Current Water/Cola recipes already have matching Unit tags and selected Forms authoring, so no scene, prefab or asset mutation is proposed.

After approval, stop only the current ChainRush Play Mode, preserve the log/readbacks, apply the listed correction and remove the diagnostic. Check static references and Unity compilation while stopped. No game or tests are launched by the agent. Successful survival with the intended character prototype must be observed in the user's subsequent manual run; compilation alone is not gameplay acceptance.

Evidence: `unit-removal-diagnostic/manual-run.log`, `removal-warnings.txt`, `manual-route.json`, `manual-history.json`, `manual-state.json`, `manual-prototype.json` and their tool envelopes under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`. The manual-run frames differ from the preceding session because the user started a new Play Mode run; do not merge their timelines.

Separate approval is required by rule 11 PROJECT_RULES.md: the previous answer authorized only the temporary removal diagnostic and stopping the previous Play Mode; it did not authorize changing the production endpoint's source payload or stopping this new run.

### Allied deployment source correction — approved and implemented

The user explicitly approved the listed correction and stopping the current ChainRush Play Mode. PID 16784 was stopped. The causal warning, live readbacks and dedicated log were preserved before cleanup. **Implementation decision: working model.** All four named Production/Orchestration files now use a list of existing ProductionOrderOutputBinding entries. The input-consumption operator inspects the real recipe outputs, captures exact configured source entries at their true output indices and builds one canonical recipe option. A missing source prevents that option; duplicate source definitions retain the existing refusal. Unmatched output tags preserve authored issuance. ProductionDecompOpUtility adds one preparation fact per exact CapabilityHost realization, using the existing reference comparer; the endpoint forwards the list to ProductionOrderRequest. No second order execution or source inference was added.

The existing canonical-consumption fixture now prepares a real producer and the selected character/forms through existing Economy setup. Its four authored fixture cases cover no configured source, valid selected sources, a missing form and ambiguous selected owners. Assertions retain one recipe option, check output indices 1/2 after an unbound output, exact runtime source handles and a single prototype preparation requirement for the shared source. Tests were not executed. ChainRush does not declare this package in testables, so its Editor compilation is not claimed to compile or validate that fixture.

The complete temporary AI diagnostic was removed. AIBrainService is byte-identical to its pre-diagnostic backup. No game asset, prefab, scene, parameter, `.meta`, definition GUID, HostValue/Attribute rule, defeat action, Economy runtime or prototype implementation changed. Static search across package code/tests and the two projects' gameplay code found no old single-source endpoint accessor or argument. Static whitespace checks passed.

Unity compiled the runtime correction in stopped ChainRush. Final readback reports isPlaying = false, isCompiling = false, isUpdating = false and scriptCompilationFailed = false; reflection verifies OutputBindings is a List<ProductionOrderOutputBinding>, OutputSource is absent and TraceRemovalAction is absent. Original metadata for all six touched files remains unchanged. The existing unchanged three-argument payload caller needs no conversion, because it has no configured source; no compatibility overload was retained.

Evidence is under `/tmp/chainrush-unit-spawn-diagnosis-20261006/unit-source-correction/`: `before-manifest.json`, `before/`, `before-change.log`, `approved-correction.patch`, `import-tool.json`, `compiled-read-tool.json`, `completion-summary.json` and `after-compile.log`. The agreed implementation scope is complete; no game or tests were started by the agent. The next user-started run must establish the resulting unit's source/origin, character prototype, positive Health and survival beyond its initial frame. No gameplay acceptance is claimed from compilation alone.

### Allied deployment source — manual follow-up and exact realization handle

Reported symptom remains «Готово. Теперь юниты создаются но сразу исчезают». After the completed correction the user answered «Готово» to the requested manual run. This answer does not assert survival of the unit.

**Verified:** read-only inspection of the user's new ChainRush run, PID 16784, finds ColaMergeRecipe3 order `3:17` completed and three Cola board Tokens consumed at frame 566. The deploy-colaunit3 Objective remains Active; process `2:558` for participant 1 is WaitingInvalidation with no candidates or endpoint. No ColaUnit3 deployment order or bound unit is recorded. This run therefore does not reproduce the previous immediate removal: issuance has not started. At frame 6865 the hero is removed after surviving from frame 442; that later removal is not evidence of a launch failure or of allied unit removal. No game or tests were started by the agent; Play Mode remains running.

**Verified:** the actual sourceOwners selector returns the selected runtime Cola and Water records; sourceEntries returns all four runtime Forms for each, with valid exact handles and no ambiguity. Cola prototype 445 exists with Health current/maximum 100000. Read-only calls to the existing EconomyService.TryResolveBackingEntry return false for both source realizations. Thus missing selected character, missing ColaUnit3 form and zero Health of the character prototype are competing explanations disproved by current state. The completed source-binding implementation discarded the selected character handle before ProductionDecompOpUtility constructed the prototype prerequisite and instead used this unsuccessful second lookup.

**Inference; causal branch not yet established:** ProductionDecompOpUtility's return-false branch after TryResolveBackingEntry may prevent the otherwise sourced recipe option. Current retained process snapshots do not preserve which rejection branch executed. Do not change runtime behavior until the existing branch is observed. The diagnostic evidence gate remains in effect.

**Existing System Fit:** EconomyEntrySelectionResolver supplies the exact selected realization record and its Forms. ProductionInputConsumptionDecompOpData owns that selection; ProductionDecompOpUtility builds the existing PrototypeOrchestrationQueryData prerequisite, which requires the realization's catalog handle, not a form handle. PrototypePreparationDecompOpData and PrototypeExecutionEndpointRuntime retain their current validation and preparation. ProductionOrderOutputBinding continues to carry the form handle for issuance; it is not extended. No change to Player preparation, Economy ownership indexes, prototype arithmetic, Health or defeat behavior belongs to this follow-up.

**Proposed diagnostic (minimum seam, temporary):** after approval, stop only the current ChainRush Play Mode. In the already changed ProductionDecompOpUtility.cs, emit a warning immediately before the existing refusal when a sourced CapabilityHost realization has no resolvable backing entry. Include source identity, recipe, domain, desired fact, selected form handles and the existing stack. Limit output to once per exact source identity and at most 32 warnings over the diagnostic's lifetime. No polling, subscription, authored field or gameplay change. Compile while stopped. The user's next manual run must supply the branch evidence; preserve it before removing the diagnostic.

**Proposed correction (working model), conditional on that evidence:** capture the exact selected character handle alongside the already captured source/form map in ProductionInputConsumptionDecompOpData. Pass a reference-identity keyed map of source CapabilityHost realization to catalog handle to ProductionDecompOpUtility for the same planning call. Construct each existing prototype preparation requirement from this captured handle; retain one requirement per exact realization. Do not rediscover a parent, use a form handle as a character handle, silently skip preparation, or substitute the authored definition. The existing prerequisite checks that the captured source record is still current. Keep the four already approved endpoint/payload changes unchanged; no new classes or authored fields.

Update the existing canonical-consumption fixture in AgentAssignmentEditModeTests.cs to assert the prototype prerequisite uses the actual selected catalog handle as well as the correct implementation, while its output bindings continue to use the distinct exact Forms handles. No tests are executed. Remove the complete temporary diagnostic after preserving evidence; perform only static checks and stopped Editor compilation. Runtime source validation, payment, admission, prototype composition and AI remain unchanged.

**Optimization:** reuse handles already returned by the one owner selection; a reference-identity map is scoped to the existing planning call. Remove the failed second backing-entry query instead of adding graph walks or a global reverse index. No additional wallet reads per output, frame polling, new subscriptions, persistent graph copies or new authoring contracts.

**Best Practices:** explicit dependencies and encapsulation apply concretely: selection owns the exact identities; prerequisite construction receives those identities, while the existing preparation and issuance boundaries validate them. This preserves one source resolution and avoids implicit owner inference, following the principles cited in [Microsoft architectural principles](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles).

Evidence: `unit-source-correction/manual-run.log`, `manual-route.json`, `manual-history.json`, `manual-prototype.json`, `manual-state.json`, `manual-planning.json`, `manual-source-state.json`, `manual-source-state4.json` and their tool envelopes under `/tmp/chainrush-unit-spawn-diagnosis-20261006/`. Read frames belong to this run only; do not merge with the pre-correction deletion trace. The external read script initially failed to compile/access private fields; its failed envelopes are retained and do not constitute gameplay changes or test runs.

Rule 11 PROJECT_RULES.md requires explicit approval for the additional diagnostic and changed prerequisite input; rule 24 requires observing the actual refusal before applying the conditional correction. The previous approval covered forwarding output bindings, not this missing catalog-handle propagation. After approval of this complete bounded step, no additional approval is required merely to perform its already described conditional correction once the branch is verified. No test or game execution is authorized by it.

### Exact realization handle follow-up — approved diagnostic installed

The user explicitly approved the entire diagnostic and conditional correction described above. ChainRush PID 16784 was stopped; a separate read verified isPlaying = false before editing. The log and original utility source were preserved under `/tmp/chainrush-unit-spawn-diagnosis-20261006/realization-handle-followup/`.

Only ProductionDecompOpUtility.cs received the temporary minimum seam: ProductionPrototypeSourceDiagnostic is emitted immediately before the existing refusal after TryResolveBackingEntry returns false. It records source, recipe, domain, desired fact, configured form handle identities and stack; output is limited to one warning per exact source and 32 warnings for the diagnostic's lifetime. No subscriptions, polling, authoring fields or execution changes. Original metadata is unchanged; the diagnostic diff is preserved separately.

Stopped Editor readback verifies compilationFailed = false, isCompiling = false, isUpdating = false and TraceMissingSource is present in the compiled assembly. A first read crossed domain reload and returned a network error; the next read succeeded. No game or tests were launched. The conditional runtime correction is still pending the user's manual branch evidence. The user is asked to stop Play Mode after that attempt so the already approved correction can be compiled while stopped.

### Exact realization handle — causal branch verified and correction completed

**Verified:** the user's subsequent manual run emitted exactly one ProductionPrototypeSourceDiagnostic for runtime Cola, recipe `chainrush.production.deploy.colaunit3`, domain 2 and fact `deploy-colaunit3:economy:0`. Output index 0 references the exact ColaUnit3 Stack in the runtime Cola Forms wallet, with HandleValid = true. The preserved stack passes through ProductionInputConsumptionDecompOpData → ProductionDecompOpUtility.TryBuildProductionOption → TryBuildOption → TraceMissingSource, immediately before the existing return false after backing-entry lookup failed. This proves the causal rejection branch; the earlier absence of a deployment order and candidate follows from the refusal to build that option. Missing selected source/form, ambiguity and missing character prototype had already been checked separately. The user stopped this run, and stopped Editor state was independently verified before the correction.

**Implemented working model:** ProductionInputConsumptionDecompOpData now retains the exact selected catalog handle in a reference-identity keyed map for CapabilityHost source realizations. It passes the map alongside the existing output-binding list to ProductionDecompOpUtility. The utility constructs the same deduplicated prototype preparation prerequisite from the captured catalog handle, without the failed second backing-entry lookup. Forms handles remain exclusively the sources of their corresponding outputs. Missing/invalid captured handles refuse the option; the existing prototype preparation and production validation retain their freshness/ownership checks. No authored field, class, source inference, economic reverse index or alternate issuance path was added.

The existing fixture additionally asserts that the preparation handle belongs to the selecting owner, differs from both Forms handles and satisfies the existing HasCurrentSource check. Tests were not executed; ChainRush runtime compilation is not claimed as compilation of the package test fixture. The endpoint payload/list implementation from the preceding correction remains unchanged.

The complete temporary utility diagnostic, including its bounded identity set, was removed after preserving the warning and stack. Static whitespace/reference checks passed. Stopped ChainRush readback verifies isPlaying = false, isCompiling = false, isUpdating = false, compilationFailed = false, no TraceMissingSource or diagnostic set in the compiled assembly, and the exact source-owner map parameter in TryBuildProductionOption. Original touched-file metadata is unchanged. No assets, scenes, gameplay stats, AI behavior, Economy ownership or other project's Editor changed. No game or tests were launched by the agent.

The entire approved diagnostic/conditional correction step is complete. Gameplay survival after this final correction has not yet been observed; that requires the user's next manual run. Evidence: `manual-diagnostic-run.log`, `source-refusal-warnings.txt`, `manual-stopped-tool.json`, `handle-correction.patch`, `corrected-source-identity.json`, `import-correction-tool.json`, `compiled-correction-tool.json`, `after-correction-compile.log` and `completion-summary.json` under `/tmp/chainrush-unit-spawn-diagnosis-20261006/realization-handle-followup/`.

### Drop pickup follow-up — 2026-10-06

**Reported symptom (literal):** «дроп не собирается. не улетает». The user reports that unit creation now works.

**Verified:** the dedicated ChainRush Editor (project `/Users/ionrain/ChainRush`, PID 16784) is in the user's paused Play Mode, frame 2576. Drop requests 1 and 2 completed successfully, without skipped entries. World drop Entities 477 and 478 are materialized. Collector Entity 206 has its enabled/unlocked collection skill, selected Entity 477 and an active PendingStart execution 16 with six units of start delay remaining. No change or advancement of Play Mode was performed by the agent.

**Verified — presentation:** the collector's direct projection request remains pending in ready Activity 2. There are no registered projection targets. The active ExperienceUI target has ActivityId Invalid. Its selector requires `chainrush.activity.autobattle` plus `chainrush.activity.runtime.integration-autobattle`; the running Autobattle Activity has no runtime tags, so the actual selector matches no current Activity. The log also orders Activity 2 Ready before the `open-gameplay` presentation step. `UIProjectionContextController.OnEnable` currently subscribes only to future lifecycle events. These are separate configuration and initial-observation concerns; changing only a tag does not address the latter.

**Verified — economic state:** each actual world token's runtime definition owns Experience Stack ×3. Its registered live CapabilityHost Instance wallet and its resolved implementation wallet contain no Experience. `ExperienceCollectionSkill` reads the target's Host at Instance level. `CapabilityHostService.TryResolveInstancePrototype` derives the initial copy from `prototype.ReadInstanceState(selectedForm)`; the issued token's independently populated DropContents wallet is not that instance state.

**Inference:** after the remaining start delay, the collection action is expected to reject its empty Instance selection. This specific execution has not yet reached that branch in the captured paused state; cause of the reported collection failure is not established solely by a predicted rejection. Do not change the economic routing or prototype initialization based only on this inference.

**Existing System Fit:** DropRuntime owns selection, container production, content transfer, world transfer, materialization and compensation. Production/Economy own issuance and exact holdings. Activity/CapabilityHosts own the live instance and inherited state. AIBrain selects a real Entity and invokes the ordinary collection skill; Skills owns its effects and terminal result. `SkillTargetProjectionController` forwards the skill's start-delay progress into Projection; UIProjectionContext/Target register the presentation receiver. Reuse `BehaviorViewerRuntimeTracker` for already emitted AI action/state events; no new trace service, per-frame query or runtime diagnostic class is needed. Unit deployment corrections, MMSoundManager and shutdown cleanup are outside this diagnostic step.

**Next bounded diagnostic step, awaiting approval:** enable the existing Behaviour Viewer live capture only for the user's current run; have the user resume and pause manually after a collection attempt; read the retained Collect-state completion/action trace and the unchanged exact drop/owner state; preserve the evidence and disable capture. No source/assets, game rules, tests or game starts are changed in this step. An implementation proposal follows the captured result, with explicit files and economic ownership rather than an inferred fallback.

**Best Practices:** inspect the existing state machine and recorded domain events before modifying execution. Here that means reusing `Scripts/Core/EditorTools/BehaviorViewer/BehaviorViewerRuntimeTracker.cs` (`SetLiveCaptureEnabled`, AI action/state listeners) and comparing the authoritative holdings with the already issued request. This keeps observation separate from mutation and satisfies PROJECT_RULES.md diagnostic evidence gate 24 without creating another tracing implementation.

Evidence: `/tmp/chainrush-drop-pickup-diagnosis-20261006/` — `manual-paused-run.log`, authoring read, paused state/detail/balance/wallet tool envelopes and decoded snapshots. The earlier exploratory balance query without form types was refused; its error envelope is preserved and its empty result is not used as economic evidence. The corrected read explicitly queried Stack and Token and succeeded.

#### Existing trace completed; next bounded correction proposed

**Verified:** the user approved existing Behaviour Viewer capture and manually resumed the game. At frame 4396 the collector's Collect state had 25 visits, all with the recorded action result `UseSkillAIBrainActionData returned Failed`. The same target Entity 477 remained selected and present. The capture was disabled at frame 5798; both the tool envelope and the post-attempt state/log are preserved. No Play Mode transition was performed by the agent. The later read at frame 6957 found the user's Play Mode paused.

**Verified:** Entity 206 (collector) and Entity 477 (drop) resolve ordinary Host / Instance owners and backing entries successfully in the same runtime domain 2. The exact issued drop owns Experience Stack ×3; Instance and resolved Implementation do not. The actual skill has two valid effects: transfer Experience from Host/Instance, then destroy the exact backing token. The first effect's current selection is empty. The AI trace proves failure during collection but does not record the internal estimate-rejection branch; that final causal gate remains open. This is not an asset-null, disabled-skill, missing-target or cross-domain diagnosis.

**Verified:** Projection has no registered viewport. Both active camera controllers have invalid registered Activity IDs and require the old integration-only runtime tag. The UI receiver similarly has no Activity binding. Activity 2 reached Ready before FrameworkGameplay and ExperienceUI were enabled. Existing controllers observe only future lifecycle events. Consequently the pending collector projection cannot attach to its UI target, and the flight cannot obtain its camera. These presentation blockers are independent of the economic selection failure.

**Implementation decision: working model — proposed, not implemented.** Keep contents in the exact produced economic container, where DropRuntime has already moved them. Do not copy Experience into every instance, mutate the prototype, create a prototype per drop, or change container production/compensation. Add an explicit read of a backing entry's owned wallets to the existing entry-effect executor. This is a selectable economic source, not a Drop-specific executor or inferred fallback.

1. **Close the remaining evidence gate.** Temporarily instrument the existing empty-selection return in `Scripts/Core/Skills/SkillEconomyEntryEffectExecutor.cs`: one bounded warning with execution Entity, source Entity, addressed owner, source type/level, selector and selected entry count. No repeated per-frame logs, new subscriptions or activation path. The user runs the game manually. Preserve the actual branch evidence before changing collection behavior. If that branch is not reached, do not apply the economic correction below on inference alone.
2. **Read the existing container contents.** Add `BackingEntryWallet` to `Scripts/Core/Skills/SkillEconomyEntrySourceType.cs`, preserving values 0 and 1. In `SkillEconomyEntryEffectExecutor.cs`, resolve the authored level as now, obtain its exact backing handle using the existing helper, resolve that exact entry and require its Asset to implement `IEconomyAssetOwner`. Apply the same `EconomyEntrySelectionResolver` to that owner's registered wallets. Preserve Entity/domain checks, handle deduplication, ordinary Move, price-free effect semantics, async Economy completion and the second effect's exact backing-token Destroy. Unavailable backing entry or owner rejects the effect; there is no fallback to Host/Prototype/Implementation. `SkillEconomyEntryEffectData` keeps its existing fields. Only the first effect in `Assets/Game/Activities/Autobattle/Skills/ExperienceCollectionSkill.asset` changes to this source; its selector remains DropContents / Stack / Experience. The second effect remains BackingEntry / Token / Destroy. DropRuntime, Production and prototype arithmetic are unchanged.
3. **Attach presentations to already running Activity.** Add a general `ActivityRuntimeSnapshotReadRequest` event under `Scripts/Core/Activities/Events`, with an explicit completion callback receiving the existing snapshot list. `ActivityServiceCore` handles it using its existing snapshot read; it has no dependency on views, Projection, UI or concrete CapabilityHosts for this request. `ActivityRuntimeSelector` accepts one or several existing `ActivityRuntimeSelectorData` criteria and can initialize its matching IDs from that snapshot. Both `UIProjectionContextController` and `ActivityViewportController` subscribe to lifecycle events, request the initial read once on enable, then continue with existing events. Disable clears the selection; callbacks are tied to the current enable generation. Several matches continue to block binding. No polling or replay of global Ready/registration events.
4. **Explicit authoring for both levels.** `UIProjectionContextController` replaces its single serialized selector with `List<ActivityRuntimeSelectorData> activitySelectors`. In ExperienceUI prefab author two entries: AutobattleActivity and DistanceActivity, each without the obsolete integration-only tag. In `Assets/Game/FrameworkUI/Scenes/Integration/FrameworkGameplay.unity`, clear that tag from the existing two camera selectors, retaining their distinct Activity definitions and camera/view settings. No new taxonomy tags, Flow launch variants or authored strings. The original UI scenes stay unchanged; the original integration scene's singular viewport selectors remain valid. This does not require changing `ActivityRuntimeSelectorData` or migrating its definition field.
5. **Finish the bounded step.** Update the existing `GameplayFoundationEditModeTests` entry-effect fixture and the existing `ActivityLauncherEditModeTests` selector fixture for the new supported source and initial/multiple-criteria observation; do not execute them. Remove the temporary diagnostic after preserving evidence. Verify compilation only with Play Mode stopped. New `.meta` belongs to Unity. Game runs and tests remain manual/separately authorized.

**Existing System Fit:** reuse the exact `IEntityEconomyContext.TryResolveBackingEntry`, `EconomyEntrySelectionResolver.TryResolveExact/TryResolve`, ordinary Economy transfer/removal and Skills effect executor. Activity remains the authority for current runtime snapshots; the selector owns matching, Projection owns bindings and transitions. Use the existing EventBus instead of views calling services. DropRuntime retains its sole content-transfer/compensation ownership. CapabilityHost initialization, prototype propagation, unit spawning, HostValues, Flow launch and Storage are out of this correction. The ExperienceUI progress counter also has a late initial-state concern; it is recorded separately from the receiver/flight binding and is not silently redesigned here.

**Best Practices:** preserve one economic holding and address it by the existing stable handle, as already done by `SkillEconomyEntryEffectExecutor.TryResolveBackingEntry` and `DropRuntime.CompleteContainerAsync`. The collection command consumes that holding rather than copying state into a second owner. For lifecycle observation, take an initial snapshot and then process notifications; this pattern already exists in the UI Economy Sources. Applied here, it closes the missed-Ready gap without broadcasting fake lifecycle events or querying every frame. These are precise reuse points, not a second Economy or tracing system.

**Cost:** the existing preflight and application boundaries each perform ordinary resolution using Economy's query cache; the new source adds exact backing-entry resolution to these boundaries, not continuous reads. One Activity snapshot read per controller enable, then existing events. No new per-drop prototype, copied content graph, per-field query, timer or recurring full scan. Evidence includes `viewports-tool.json` / decoded `viewports.json`, `post-attempt-state.json`, `drop-origin.json` and the successful trace-disable envelope.

**Approval boundary:** only existing trace capture was approved so far. The temporary branch diagnostic, new source enum case, initial Activity read and the listed authored UI changes require one explicit answer before implementation; after that, the user supplies the manual diagnostic run. Stopping the current Play Mode is included in the proposed step.

**Approved and implemented before the manual branch read:** the user explicitly approved the entire described step and stopping Play Mode. ChainRush was stopped through the guarded Editor operation. The bounded empty-selection warning is installed. Initial Activity snapshot reading, selector alternatives and both Projection controllers are implemented. ExperienceUI now explicitly selects either level without the obsolete tag; both FrameworkGameplay cameras retain their authored definitions and have that obsolete tag removed. Prefab and scene were authored and saved through Unity; their diffs contain only these fields and preserve identities. The existing UI authoring writer and ChainRush content fixture were updated for the same renamed selector field; the existing ActivityLauncher selector fixture now covers initial observation, alternatives, duplicate matching, ambiguity and lifecycle removal. No tests or game runs were performed by the agent.

The first UI-authoring request lost its connection during script reload. A separate read established that no authoring had occurred (zero selectors, Gameplay scene unloaded and clean), and compilation had finished without failure. The subsequent guarded authoring request succeeded. Envelopes are retained alongside the original failure. The conditional collection-source correction still awaits the user's manual branch evidence; it does not require another design approval if that branch is confirmed.

**Verified — manual branch evidence, 2026-10-07:** the user reports that Experience now flies, reaches its destination, then returns to its drop position without awarding value; Play Mode was left paused. Dedicated ChainRush log lines 31373–31456 record the installed `EmptyEntrySelection` warning three times for collector 206 / target 477 / Host Instance owner `capability-host:477` / Experience Stack / DropContents / selectedCount 0. Its stack passes through `TryCollectActionEstimates` → `TryApplyAdmittedActionEffects` → `TryProgressExecution`. This proves the actual first-effect rejection branch before any transfer or backing-token destruction. At frame 2455, a separate successful authoritative read still finds Experience ×3 in each exact issued token, and no Experience in the target Instance or Implementation. Context/target registration is now Activity 2 with one match and one UI target, agreeing with the user's observed flight. The remaining return follows the existing failed-skill transition cancellation, rather than a new issuance of the drop. The conditional implementation gate is closed; perform the already approved source correction and remove its diagnostic.

Evidence preserved before mutation: `empty-selection-manual-run-20261007.log`, `confirmed-branch-state-current-tool.json` and decoded `confirmed-branch-state-current.json`. The superseded external read script used the old selector field and failed; its envelope is not used as evidence. No test was executed.

**Implemented working model — exact container contents:** the already approved conditional correction is implemented in the existing `SkillEconomyEntryEffectExecutor`. `SkillEconomyEntrySourceType.BackingEntryWallet = 2` resolves the Entity's exact backing entry with the existing level/context checks, obtains that entry's economic owner, and applies the existing selector to its registered wallets. The first collection effect now selects Experience from this exact container and queues the ordinary Economy Move. The second effect still destroys the same backing Token through the existing path. No content is copied into the drop Instance or prototype, and no alternative transfer, pool or issuance path was added. Existing enum values are unchanged.

The existing transfer fixture now covers both Wallet and BackingEntryWallet using the same skill execution contract; the content fixture and collection authoring writer use the new source. Fixtures were updated but not run. The collection asset was changed through Unity: its only serialized change is `sourceType: 0 → 2` on the first effect. Its GUID and complete destruction effect were checked unchanged. The bounded diagnostic was removed after preserving causal evidence. Existing UI/camera corrections described above remain in place.

Stopped ChainRush Editor readback verifies the new source in the loaded skill, unchanged Instance level and Experience selector, unchanged BackingEntry/Destroy effect, and compilation without failure. Static whitespace checks passed; no existing `.meta` changed. Runtime compilation is not claimed as test-fixture execution or gameplay verification. No tests or game runs were performed by the agent. The entire approved diagnostic/conditional collection correction is complete; collection after this correction requires the user's next manual run. Evidence: `collection-authoring-tool.json`, `ExperienceCollectionSkill-before.asset` and `source-correction-compiled-tool.json` in `/tmp/chainrush-drop-pickup-diagnosis-20261006/`.

### Experience progress UI — 2026-10-07

**Reported symptom (literal):** «Опыт прилетает и зачисляется, но прогрессбар опыта в UI не показывает прогресс, он всегда пустой».

**Verified:** the user's manual paused run in ChainRush, frame 2582, has one active ExperienceUI in FrameworkGameplay. Its Activity and Projection Activity are both 2. The existing PlayerSpawner Instance is Entity 204 in Activity 2, owned by `player:FrameworkPrototypesProfile`, with Experience ×3. Production reports zero completed yields for ExperienceToTurnTokenRecipe. The view has `_playerSpawnerEntityId = Invalid`, `_playerOwnerStableKey = null`, `_experienceAmount = 0`, `_completedTurnTokens = 0`; its valid Slider/Fill display 0/6 with a zero fill anchor. Therefore this is a missing UI binding, not absent awarded resources or a missing Slider reference.

**Verified call path:** ExperienceUIController only assigns its producer/owner from future CapabilityHostRegisteredEvent. OnEnable/ApplyActivityBinding never reads existing hosts, balances or recipe progress. Its resource-change handler returns before updating the amount when the owner key is null. The run log places Activity 2 Ready at line 37360 before the Gameplay scene load at line 37563; Activity initializes its seed/content before Ready. The existing producer consequently predates this UI subscription. The view's observed empty binding and zero amount agree with that lifecycle path. The nearest competing explanations, zero remaining Experience and missing visual references, are excluded by the paused read. Progress happens to be zero in this sample; the same initial read must also preserve already completed recipe yields on a later UI attachment.

Evidence preserved in `/tmp/chainrush-experience-ui-diagnosis-20261007/`: `manual-progress-run.log`, successful `paused-ui-state-read-tool.json` and decoded `paused-ui-state.json`. Earlier external read attempts either failed compilation or returned only an object summary; they are not used as evidence. The agent did not advance or stop Play Mode and did not run tests.

**Proposed implementation decision: working model.** Initial read followed by the existing notifications; no new gameplay manager, progress calculation or service-owned UI adapter.

**Existing System Fit:** CapabilityHostService owns the producer identity, Activity and root economic owner; Production owns completed recipe yields; Economy owns the Experience balance. The existing ProductionUIAdapter remains the mediated UI boundary and is composed/disposed by UIRuntimeInstallerData. ExperienceUIController owns visual state and existing lifecycle/resource/yield subscriptions. Reuse EconomyUIReadRequest/Response for the one initial balance read. Collection, DropRuntime, prototypes, formula arithmetic, UIFlow and Activity preparation remain outside this correction.

| File | Concrete change |
|---|---|
| New package `Scripts/Core/UI/Production/ProductionUIProgressReadRequest.cs` | One read-only EventBus request: current ActivityId, existing producer CapabilityHost definition, recipe and completion callback carrying the actual CapabilityHostSnapshot and completed-yield count. No Experience-specific field or authored string. |
| Package `Scripts/Core/UI/Production/ProductionUIAdapter.cs` | Register/unregister that request on the existing adapter lifecycle. Select the existing Instance of the requested definition in the exact Activity; preserve the view's existing lowest-Entity rule. Read recipe progress through TryGetRecipeProgress and return the actual snapshot/count. Missing producer returns no snapshot, allowing the existing registration event to supply a later producer. Do not create hosts, owners or production orders. |
| ChainRush `Assets/Game/Activities/Autobattle/Runtime/UI/ExperienceUIController.cs` | On valid Activity binding, request existing producer/progress. Apply the same binding path used by future registration events; initialize the existing turn counter from Production. Request the actual owner's Experience through existing EconomyUIReadRequest with Watch=false, aggregate the returned Stack rows and update Slider/text. Keep existing resource/yield events for subsequent changes. Invalidate request identity/version on disable, unregistration and Activity change; ignore responses for old bindings. Keep current authored fields, recipe threshold calculation and visual references. |

**Exact read contract:** `ProductionUIProgressReadRequest` has readonly `ActivityId`, `CapabilityHostBaseData HostDefinition`, `ProductionRecipeData Recipe` and `Action<CapabilityHostSnapshot, int> Completion`. The integer is the existing completed-yield count, not a new progress measure. The adapter reads the snapshot of the matching Instance and its existing recipe progress. An absent producer returns an invalid snapshot; the view keeps waiting for its existing registration notification. The adapter does not cache producer selection or retain callbacks. Its request listener is installed and removed together with the existing ProductionUIAdapter, without changing any domain installer or service.

**View data flow:** register the existing notifications and balance-response listener before the initial request; capture the current binding version in its callback. Accept the result only while enabled and still bound to the same Activity/version. Establish producer identity/root owner and initialize `_completedTurnTokens` from the returned count. Send one correlated EconomyUIReadRequest for that explicit owner, Experience and Stack with Watch=false. Sum its returned amounts and render the current recipe threshold with the existing calculation. Future producer-registration events use the same read/binding path. Future EconomyResourceChangedEvent and ProductionOrderYieldedEvent retain their present filtering/update responsibilities. Disable, producer unregistration and Activity change invalidate pending progress/balance reads and clear visual state; old responses cannot populate a new binding.

For the captured state the expected initial display is **3 / 6**, normalized fill **0.5**. This is an expectation for the next authorized/manual verification, not a test result. If Experience is subsequently consumed by the existing token recipe, the resource event updates the remaining balance and the yield event updates the existing turn counter/threshold.

**Best Practices:** read existing state before relying on future notifications, and keep reads behind the already composed UI boundary. The local examples are `EconomyUIAdapter.OnEvent(EconomyUIReadRequest)` (initial Publish, optional watch, one-shot cleanup at lines 24–54) and `UIProjectionContextController.OnEnable` (initial snapshot plus stale-callback guard at lines 27–37). Reusing these patterns addresses late attachment and cleanup without fake lifecycle events, direct view-to-service calls or a second balance cache in a service.

**Cost and cleanup:** one producer scan/recipe read and one cached Economy query per binding, not per frame or per Experience event. Later updates use current subscriptions. Watch=false creates no persistent balance lease or duplicate watch. Adapter request subscription is released by its existing Dispose; the view invalidates outstanding callbacks when detached. New metadata is generated by Unity. No prefab fields or gameplay assets need conversion.

**Approval boundary:** this numeric progress correction was expressly outside the earlier approved flight/container step. Before implementation, obtain permission for the listed files and stopping this paused Play Mode. After permission, implement the entire correction and check compilation while stopped. Do not run the game or tests; a subsequent manual run verifies the displayed progress. The existing original UI scenes remain unchanged.

The user requested the concrete correction plan in response to the implementation question. This is a request to describe the plan, not approval to change runtime code or stop Play Mode. The plan above is prepared for review; implementation has not started.

**Approved and implemented — 2026-10-08, working model:** the user's subsequent «делай» approves the concrete plan, including stopping Play Mode if necessary. The guarded ChainRush operation found Play Mode already stopped. Implemented exactly the listed UI request, existing-adapter handler and ExperienceUIController integration; no domain service, installer, recipe, prefab or scene changed in this step.

ProductionUIAdapter registers/unregisters the new one-shot read on its existing lifecycle, selects the lowest matching Instance in the exact Activity and reads the existing completed-yield count. It neither retains callbacks nor creates producers/orders. ExperienceUIController performs this initial read after applying Activity binding, uses the returned real owner for a correlated Watch=false EconomyUIReadRequest, sums the Experience Stack rows and renders its existing threshold. Future matching registrations use that same path; existing resource/yield events continue subsequent updates. Disable, producer removal and Activity change invalidate pending reads. A newer matching resource notification also invalidates the pending initial balance response, preventing an older response from replacing the newer displayed amount. No view-to-service calls, persistent balance leases or periodic reads were added.

Unity generated the new request's `.meta`. Static whitespace/reference checks passed and stopped ChainRush reported isCompiling=false, isUpdating=false, scriptCompilationFailed=false after refresh/reload. Compilation is the validation performed; no tests or game runs were executed by the agent. The entire approved implementation is complete. The previously captured 3/6 state remains the expected manual-run result, not a post-fix gameplay observation. Evidence: `implementation-stop-tool.json`, `implementation-stopped-state-tool.json`, `implementation-refresh-tool.json`, `implementation-compile-state-tool.json` and `implementation-compiled-readback-tool.json` under `/tmp/chainrush-experience-ui-diagnosis-20261007/`.


## Spawn-area distribution — 2026-10-08

**Reported symptom (literal):** «есть область спавна юнитов и область спавна монстров. Так вот спавн происходит почему-то не равномерно в этой области а в нижней части, монстры вообще спавнятся ниже области спавна как я могу понять». The user left ChainRush playing and paused. All reads were explicitly addressed to ChainRush; frame 8584 remained unchanged. No Play Mode changes, game runs, tests or gameplay edits were made.

**Verified — unit placement:** the active wall Water/Cola providers each publish 96 cells. Both are authored Next / ReuseAllowed. The actual marker-selection cursor is 1 for each provider, and each surviving unit is at its provider's exact first marker: WaterUnit2 Entity 508 at (8.35, 0, 2.25), ColaUnit3 Entity 471 at (4.3, 0, 2.27). Marker indices increase from the lower Z edge. The placement path uses that ordered selection and commits the cursor; the active WorldProjectionTarget applies the authoritative coordinates directly. This establishes sequential first-cell placement, rather than a random distribution, for these two deployments. Existing Random authoring is the intended bounded correction; no new selection service is needed.

**Verified — monster inputs/current state:** Level01PopulationAgent has completed three progress observations and uses SpatialPopulationDistributionAlgorithmData. Its release selects EnemySpawn regions in Activity scope. Two available Volume regions share that tag: wall scope 7 (X 27.4–37.4) and hero scope 249 (X 17.4–27.4); both have Z -3.8–16.2. Their registered bounds agree with the live controller transforms and settings. Surviving BugBrownSmall current Z values are -1.957, -0.707 and 1.2, inside these Z bounds; current X has already changed through movement. The current view/Spatial difference is movement interpolation (under 0.06), not a constant transform offset. No current Population execution or pending materialization retains initial placements in this paused frame.

**Verified — algorithm, not initial-spawn trace:** the Volume branch of SpatialPopulationDistributionAlgorithmData.Origins (lines 342–388) scans its lattice from minimum right/forward coordinates (and minimum X/Y/Z in 3D). Only its Cells branch uses a random start. Its Run accepts the first fitting candidate. This gives the Volume path a lower-edge search preference. PopulationRegionSession freezes selected regions; PopulationProductionSession registers an exact transient marker for the chosen pose; ActivityProductionOutputMaterializationModule passes that marker to Activity materialization. Geometry containment and occupancy checks remain separate authorities.

**Inference / cause not established:** this lower-edge traversal likely explains the reported monster clustering, but the initial selected region and initial registered pose for the observed monsters are no longer retained. Spawning outside the *actual monster region* has not been demonstrated. The two equal-tag regions differ only in X, so their existence alone cannot explain a vertical Z offset. Do not move/resize regions or change the Volume algorithm based solely on current moving monsters. Record the exact planned region/pose and accepted materialized pose before movement to close this evidence gap.

**Existing System Fit:** SpatialShapeProviderController and SpatialMarkerService own authored unit cells/selection; SpaceRegionController and SpaceRegionService own monster Volume geometry and scope; Population owns distributed candidate planning, Production owns orders, Activity owns materialization, CapabilityHosts/Spatial own registered instances, Projection owns view placement and Movement owns subsequent position changes. Reuse all existing geometry, occupancy, markers, reservations and registration paths. Prototype propagation, economy, combat, drops, original UI and MMSoundManager are outside this issue. Tests inspected: PopulationDistributionEditModeTests and existing Population fixtures in AgentAssignmentEditModeTests / ProductionCapabilityHostEditModeTests; none run.

**Best Practices:** use the existing deterministic Random marker policy when gameplay requires random placement (SpatialMaterializationTypes.cs:20–51; SpatialMarkerService.cs:1150–1162), while preserving admission/occupancy validation. For unresolved placement offsets, observe the planned and committed poses at the same spawn boundary before movement; final moving positions cannot establish initial placement. This is consistent with the mandatory diagnostic evidence gate and the existing reservation-to-materialization ownership.

**Proposed diagnostic boundary, not implemented:** stop the current ChainRush Play Mode with permission; add bounded temporary warnings to PopulationProductionSession for the exact chosen region, frozen bounds, candidate pose, marker and accepted order, and ActivityServiceCore after successful spatial host registration for the same marker/Entity and actual pose. Limit output to the first 16 relevant spawns per Activity/session; clear on existing session/Activity cleanup. The user manually runs the next attempt; preserve the trace and remove temporary diagnostics afterwards. No runtime distribution or region-boundary correction is approved by this proposal. Separately, changing the six existing unit-zone providers in SurviveWall, Perfume and Tabasco to the existing Random selection policy requires approval of those authoring edits; GUIDs, usage geometry and original UI remain unchanged.

Evidence: /tmp/chainrush-spawn-area-diagnosis-20261008/ChainRush-paused.log, runtime.json, materializations.json and their successful Unity tool envelopes. Failed read-script compilation envelopes are diagnostic tooling failures, not evidence of product behavior.


**Approved and prepared for the manual diagnostic run — 2026-10-08:** the user expressly approved the six unit-zone Random settings, temporary monster diagnostics and stopping the current ChainRush Play Mode. The guarded operation stopped Play Mode; a separate read confirmed stopped state before edits.

Through Unity PrefabUtility/SerializedObject, changed only usagePolicy.selectionType from Next to existing Random for Water/Cola in SurviveWall, Perfume and Tabasco. ReuseAllowed, geometry, tags, references and all object identities are unchanged. Unity reordered the existing SurviveWall YAML documents during save; comparison by object identity establishes that only its two selection fields changed. All five existing source/prefab .meta hashes are unchanged.

Temporary PopulationProductionSession diagnostics read the frozen Volume region after submitting the ordinary placement order and publish Planned with region scope/bounds, candidate, exact marker/position, footprint and OrderId. The first 16 Volume placements of the existing per-Activity agent progress are logged; its weak-key counter survives successive releases and does not retain that progress after cleanup. ActivityServiceCore records the first 16 explicit non-cell-marker materializations per Activity, including marker position, resolved coordinates, registered Entity and actual Spatial pose. Its counter belongs to ActivityRuntimeState and is released with that Activity. In the current ChainRush path this observes monster Volume placements; cell-based hero/unit placements and ordinary automatic drop-marker selection do not consume that materialization counter. There is no polling, additional event subscription, new authored setting or distribution/bounds change.

Stopped ChainRush reports isCompiling=false, isUpdating=false and scriptCompilationFailed=false; reflection readback confirms both new diagnostic methods loaded and all six saved providers read Random. No game run or tests were launched. The user must now manually start a level and leave it paused after the first monsters appear. Preserve the matching Planned/Materialized trace before removing these temporary diagnostics. The monster cause/behavior correction remains unapproved and unimplemented; the approved temporary-diagnostic step is not complete until that trace is captured and diagnostic code removed.

Implementation evidence: implementation-stop-tool.json, implementation-stopped-tool.json, before-manifest.json, implementation-before/, authoring-tool.json, refresh-tool.json, compiled-read-tool.json and compiled-read.json under /tmp/chainrush-spawn-area-diagnosis-20261008/.

**Manual run, evidence captured and diagnostic cleanup complete — 2026-10-08:** the user manually started ChainRush and left Play Mode paused. At frame 2027 the trace records two BugBrownSmall orders, 2:2 and 2:3, selecting wall Volume handle 4 / scope 7 / revision 1. Its bounds are X 27.4–37.4, Z -3.8–16.2. Their planned positions are (36.4, 0, -2.8) and (36.4, 0, -0.8). Markers 247/466, resolved materialization coordinates and actual registered Entity 443/467 coordinates match exactly. This proves the observed placements follow the lower-edge Volume search; no materialization offset or placement outside these actual bounds is present in these two spawns. The competing transform/registration explanation is rejected for this captured execution. Earlier moving positions and the visual estimate of region bounds were insufficient on their own.

The same paused read captures ColaUnit3 at (7.3, 0, 19.27), showing the existing Random cell setting can choose a position away from the lower edge. This single observation is not a statistical distribution test. All six providers still read Random / ReuseAllowed.

Stopped only ChainRush under the approved diagnostic lifecycle. Removed both diagnostic calls, methods and counters; the two source files exactly match their pre-diagnostic contents, including pre-existing work. A subsequent stopped-Editor read reports isCompiling=false, isUpdating=false, scriptCompilationFailed=false and neither diagnostic method loaded. No gameplay or tests were started by the agent. The approved unit authoring and temporary diagnostic step are complete; the Volume correction below is proposed separately.

Evidence: manual-run.log, spawn-warnings.txt, manual-runtime.json, cleanup-stop-tool.json, cleanup-refresh-tool.json, cleanup-compiled-read-tool.json and cleanup-compiled-read.json in /tmp/chainrush-spawn-area-diagnosis-20261008/.

**Volume correction — approved working model.** Change only `Scripts/Core/Orchestration/Agents/Population/SpatialPopulationDistributionAlgorithmData.cs`, the planar Volume branch of `Session.Origins`, used by ChainRush's Level01/Level02 population. Replace minimum-to-maximum traversal with a deterministic random permutation of the same candidate lattice. Both valid placements and failed candidate probes retain the existing shape, containment, occupancy, conflict and admission checks. RequiredMarker remains exact; Cells keep their existing policy. The three-dimensional branch is explicitly outside this correction to the observed ChainRush path; do not add a capacity restriction or redesign unrelated 3D traversal here.

Mechanism: compute the same row/column counts, address each point by a long index, and draw indices without replacement with a sparse Fisher–Yates mapping. Each iteration draws one index from the remaining range, swaps its virtual value with the last remaining index and decodes the chosen index back into the existing right/forward coordinates. Row/column counts are already limited to int.MaxValue, so their inclusive-count product fits long without a new lattice-size limit. Generate the bounded long draw from the existing Pcg32Random.NextUInt stream with rejection sampling, following its NextInt rule; do not introduce another random source or public PRNG API. A fixed seed and unchanged inputs determine the order independently of Advance's budget. The order and PRNG consumption intentionally differ from the old sequential path.

**Existing System Fit:** candidate ordering belongs to the existing Spatial Population Session. Its current Advance/Run iterator retains work budgeting. SpaceRegionFootprintQuery owns geometric containment, SpatialPlacementQuery owns occupancy, PopulationProductionSession owns marker/order submission, and Activity owns final materialization; reuse those owners without changes. Production, region geometry, projection transforms, Movement, authored bounds, prefab identities and original UI are outside the correction. Existing PopulationDistributionEditModeTests and the Spatial fixtures in AgentAssignmentEditModeTests / ProductionCapabilityHostEditModeTests were inspected; no test-only API or new subsystem is required.

**Best Practices:** sampling without replacement avoids both repeated random retries and a persistent preference for an edge. A lazy Fisher–Yates mapping has the same draw/swap rule as a full shuffle while allocating only for visited candidates; a rejected point is not revisited within that traversal. The deterministic random stream and unbiased bounded-draw pattern are already owned by Pcg32Random.NextUInt / NextInt (Scripts/Core/Determinism/Pcg32Random.cs:29–65). Work remains inside Session.Advance/Run rather than an unbudgeted full-grid preparation pass. Placement correctness stays at the existing footprint/occupancy boundaries; ordering never bypasses them.

**Cost and lifecycle:** no list of all grid points, full-grid shuffle, new polling, subscription or authored field. Expected constant work per index draw and O(k) temporary mapping storage for k visited candidates, rather than allocation for every possible point. The mapping belongs to the current Origins iterator and is released when that traversal finishes, is accepted early or is disposed with the Session. Existing footprint probes and scheduling budgets remain unchanged. Random placement spreads attempts over the available lattice; it does not promise a fixed visual spacing when occupancy or shape constraints exclude positions.

After approval implement that one runtime-file change, update this section, refresh/verify compilation in stopped ChainRush and leave gameplay/tests for separate user permission. Do not change bounds or the monster assets to mask the search preference. Approval is required by PROJECT_RULES.md rule 11 because the previous step expressly authorized diagnostics, not a Volume algorithm change.

**Approved and fully implemented — 2026-10-08:** the user explicitly selected «Согласовать исправление и выполнить». A guarded Editor read verified stopped ChainRush before editing. Replaced only the planar Volume traversal with the proposed sparse permutation, and added its private bounded-long draw in the same existing Session. The iterator drops its mapping reference in finally on normal completion or early disposal; it does not clear a potentially large map synchronously. No additional authoring, types, subscriptions, managers or capacity limits were introduced. The 3D, RequiredMarker, Cells and placement-check paths are unchanged.

Static source/document whitespace checks passed. Stopped ChainRush compiled and reloaded the revised type: isCompiling=false, isUpdating=false, scriptCompilationFailed=false, and reflection finds the new private lattice draw. All six unit-zone settings still read Random / ReuseAllowed; neither diagnostic method remains loaded. The existing source `.meta` hash is unchanged. The entire approved implementation is complete. Post-correction gameplay distribution and tests have not been run; compilation and static review are the validation performed. Await separate user permission before any test execution.

Implementation evidence: volume-algorithm-before.cs, volume-before-manifest.json, volume-before-editor-tool.json, volume-refresh-tool.json, volume-compiled-read-tool.json and volume-compiled-read.json in /tmp/chainrush-spawn-area-diagnosis-20261008/.

### Unit target-search authoring — 2026-10-08

**Implementation decision: working model; requested authoring fully implemented.** The user requested an enemy-search radius of 1.5 times attack range for ranged units and four times attack range for melee units. After discussing the two existing distance checks, the user explicitly retained search around the hero and the existing distance limits from each unit. These are independent checks: the range query uses the hero as AllyAnchor; distance ranking additionally rejects targets whose position is farther than maximumOwnerDistance from the unit.

| Brain | Existing attack range | New SearchEnemy radius | Preserved maximumOwnerDistance |
|---|---:|---:|---:|
| WaterUnitBrain | 2000 | 8000 | 6000 |
| WaterUnit2Brain / WaterUnit3Brain / WaterUnit4Brain | 4000 | 16000 | 6000 |
| ColaUnitBrain / ColaUnit2Brain / ColaUnit3Brain / ColaUnit4Brain | 10000 | 15000 | 5500 |

All values are topology units. The eight assets are under `Assets/Game/Activities/Autobattle/AI`; both game modes use these existing brains. SearchEnemy preserves NamedTargetOrOwner / AllyAnchor. No new radius formula, authored field, source binding or implicit runtime retuning was added. Attack definitions and weapon-query radii are unchanged.

**Spatial-marker exclusion through existing authoring:** in all six SelectEntityTargetByQuery actions of each of these eight brains (48 queries), explicitly set targetGeometryType to Interaction and targetInteractionTags to the existing CombatantRole asset. This covers enemy acquisition, weapon selection and support selection. Preserve required/blocked target tags and states, diplomacy, candidate limits, ranking weights, locking and recent-damage settings. WorldTargetResolutionService rejects an Entity without matching interaction geometry before adding it to range results; thus those markers do not enter the AI candidate limit. The query still performs its existing spatial broad phase. Distances to a selected target use its CombatantRole interaction geometry; attack requirements retain their existing Spatial metric. No global marker blacklist or change to marker registration was introduced.

**Verified evidence:** paused ChainRush at frame 4211 contained ColaUnit3 Entity 471, Perfume Entity 249 and two BugBrownSmall instances, Entities 467 and 479. All four had matching CombatantRole interaction geometry. The first ten captured spatial candidates around the hero (10, 266, 265, 267, 264, 268, 263, 269, 290, 289) had none. The unit had an empty CombatTarget and no active Approach/Follow; its movement profile, capabilities, skills and positive Speed were present. No eligible enemy was captured beyond the candidate cutoff, so candidate starvation was not established as the cause of the earlier symptom. This change implements the user's new authoring requirements; successful movement after it is not claimed. Ordinary idle follow/formation/return remains the previously deferred feature.

**Existing System Fit:** CapabilityHost supplies the existing body bindings and movement/skill capabilities; InteractionGeometryService owns the registered combat geometry and matching tags. WorldTargetResolutionService owns range results and geometric distance. SelectEntityTargetByQueryAIBrainActionData owns semantic eligibility, ranking and its candidate limit. AIBrainService owns transitions and pending actions; Skills and Movement own actual movement execution. SpatialMarkerService retains placement-marker lifecycle and Entity identities. Reuse these owners and their current contracts; package runtime, original ChainRush controller/profile, other brains, attack skills, projection prefabs and the deferred follow/formation path are outside this authoring change.

**Best Practices:** use the existing positive geometry/tag filter at query formation, before the AI's bounded selection, rather than teaching generic spatial infrastructure which concrete objects are combatants. This retains Entity targets and allows the same markers to remain valid placement targets. The existing implementation is explicit in WorldTargetResolutionService.TryCreateGeometryContext (Scripts/Core/World/Targets/WorldTargetResolutionService.cs:609–643), its range result insertion (:194–207), and the AI candidate loop (Scripts/Core/AI/Actions/SelectEntityTargetByQueryAIBrainActionData.cs:132–156). InteractionGeometryService already caches maximum extent by Activity/tag set and registration revision (Scripts/Core/World/Interaction/InteractionGeometryService.cs:383–431); this step adds no polling, per-unit subscription or separate registry.

Stopped only ChainRush before authoring. The stop command timed out during Editor scene restoration, but a separate successful read established Playing=false, Changing=false and Compiling=false before any asset modification. Applied and saved the eight assets through Unity SerializedObject / SaveAssetIfDirty. Comparison of Unity-serialized snapshots shows exactly 13 field changes per brain: one SearchEnemy radius, six query geometry types and six explicit tag lists. All eight .meta hashes are unchanged. A separate stopped-Editor read confirmed every radius, preserved center/limit and all 48 filters. Unity's save also changed insignificant YAML whitespace. No product C# code, compatibility path, automatic migration or new .meta was added. Gameplay and tests were not run.

Evidence: /tmp/chainrush-unit-movement-diagnosis-20261008/diagnosis.txt and runtime.json; /tmp/chainrush-unit-search-authoring-20261008/read.json, interaction.json, before-manifest.json, before/, author-tool.json, stopped-read-tool.json, comparison.json and after-read.json.

### Unit movement when enemies approach — diagnostic follow-up, 2026-10-08

**Reported symptom, literally:** «Юниты продолжают стоять на месте, плеймод на паузе». The user subsequently clarified: «Юниты подходили к ним ближе, они не двигались». Do not replace that reported interval with the later paused distances or treat the absence of ordinary idle follow as its established cause.

**Verified:** explicit read of paused ChainRush still returns frame 2252. ColaUnit3 (471) and WaterUnit3 (511) have empty CombatTarget and CombatNode IdleAlly. Their current query results and distance limits describe this frame only. The surviving terminal observations include two completed ColaUnit3Attack executions; completion observations for Approach/Follow are absent. UseSkillAIBrainActionData consumes terminal observations for OnExecutionComplete, so their absence is not evidence that no earlier attempt occurred. Target-control policies have no retained navigation rejections. The registered Approach definitions have the movement effect and formula graph in their Unity serialization. Existing Behavior Viewer capture is disabled and has no recorded state history for the reported interval. No Movement/UseSkill failure message for that interval was found in the dedicated ChainRush log.

**Inference / cause not established:** an earlier failure of target selection, transition to ChaseEnemy, skill activation, or movement execution remains possible. The later absence of nearby enemies cannot distinguish those explanations. No runtime or authoring correction is justified by this snapshot alone.

**Proposed diagnostic step, awaiting explicit approval; minimum seam:** enable only the existing BehaviorViewerRuntimeTracker.SetLiveCaptureEnabled in this paused ChainRush run. Preserve its previous capture setting. The user resumes manually, observes an enemy approaching a unit that remains stationary, and pauses manually. Read the captured SearchEnemy / ChaseEnemy / support-state transitions and failures, exact selected Entity, existing skill observations, active execution/movement state and current distances. Save the evidence before restoring the previous capture setting; disabling capture clears the tracker. Do not add a listener, polling, C# diagnostics, authored changes, or Skills/Movement debug flags. Do not change Play Mode, restart the Editor, move entities, issue skills, or run tests. If the run has been stopped or the relevant encounter was not captured, report that missing evidence without changing behavior.

**Existing System Fit:** AIBrainService publishes existing transition, completed-state and debug events; BehaviorViewerRuntimeTracker owns their optional Editor capture and bounded per-owner/per-state summaries. SelectEntityTargetByQueryAIBrainActionData and WorldTargetResolutionService own target acquisition; UseSkillAIBrainActionData owns activation and terminal observation consumption; Skills and Movement own movement execution. Reuse these existing boundaries. The original AI controller, deferred formation/return, authoring radii, marker lifecycle, and product runtime changes remain outside this diagnostic step.

**Best Practices:** observe the executing state transitions before changing the behavior, and retain the distinction between a current snapshot and past execution. The existing tracker registers three EventBus listeners only while capture is enabled, stores one StateTrace per owner/brain/state, and releases listeners on disable (Scripts/Core/EditorTools/BehaviorViewer/BehaviorViewerRuntimeTracker.cs:107–189). This reuses the current observability boundary without per-frame reads, additional runtime subscriptions, or a parallel diagnostic implementation. Save first because SetLiveCaptureEnabled(false) resets traces.

Read-only evidence: runtime-closer-report-tool.json / runtime-closer-report.json, navigation-history-tool.json / navigation-history.json, last-observations-named.json and the dedicated-log snapshots in /tmp/chainrush-unit-movement-diagnosis-20261008/. Product code, assets, Play Mode and tests have not been changed by this follow-up.

**Approved capture completed — 2026-10-08:** the user explicitly approved the existing trace, then manually resumed and paused ChainRush. Capture started at frame 2252 / simulation step 941 and the user paused at frame 3114 / step 1303. Saved the tracker summaries and current runtime before restoring capture from true to its previous false setting. The restore succeeded while Play Mode remained paused. No product code, assets, debug flags, tests, or automated gameplay operations were added.

The user's additional observations are retained literally: «Юниты теперь пошли к герою, когда к тому начали приближаться враги. Хотя должны были атаковать врагов, когда те приближаются к ним»; «Далее они подходят к герою слишком близко, игнорируя преграду в виде ворот»; «Герой должен иметь возможность находиться внутри ворот»; «Также враги должны атаковать ворота, а урон должен наноситься герою». The user also proposed moving coordinated defence to a group brain and considered a separate CapabilityHost for the gate with damage forwarding. These are design inputs for the next correction; the trace approval does not authorize their implementation.

**Verified AI execution:** both recorded brains visited SearchEnemy thirteen times with failed target selection. Neither ChaseEnemy nor EngageEnemy was visited in this interval. FindHeroDefend then selected Hero Entity 249; Cola entered DefendHero at step 1169, Water at 1172. Both remained there at step 1303 with AllySupportTarget=249. WaterUnit3Follow was actively executing against 249 with positive route progress; Cola had moved from (7.3, 0, 19.27) to (1.25, 0, 16.25). This proves the recorded movement was following the protected hero, not chasing an enemy.

**Verified reason that this defence branch does not reacquire enemies:** in the actual definitions, DefendHero ticks UseSkill(Follow, AllySupportTarget, OnExecutionComplete). Its return-to-AcquireAnchor conditions are target absent, or TargetBrainState(required=[], blocked=[DefeatState], shouldMatch=false). The latter is true when the protected target is defeated, not while it remains alive. TargetBrainStateAIBrainConditionData and TargetStatesMatch confirm this interpretation. Hero 249 is alive and in CombatStateB / WaitingState, so both return conditions are false in the captured execution. This branch does not periodically reevaluate the recent-damage query or enemy acquisition. Successful Follow completion does not supply another authored exit. Thus remaining in defence and bypassing later enemy acquisition is established for this interval. The individual WeaponNode still runs independently; defence does not stop it. Why the earlier SearchEnemy queries failed during an allegedly close encounter remains unestablished by the per-state summaries, which do not retain distances at each past query.

**Verified gate integration:** NavigationService has one Surface registration in Autobattle Activity 2 and no Obstacle registration. SpatialService also has no static occupying Entity in that Activity. The live SurviveWall hierarchy contains the existing enabled, non-trigger BoxCollider2D on Gate (1), visual Spine components, production-zone providers and the enemy region; it has no NavigationObstacleController. Thus the gate collider is not registered as an obstacle in the route system actually used by these skills. Hero 249 has a separate 1000×1000 spatial footprint and an existing GateHeroBody interaction shape (CombatantRole, 7079×27292, offset 794/-1392). That shape represents the gate's combat area and damage is directed to the Hero Entity. Its bounds are close to, but do not exactly match, the live collider: collider X [-3.205, 3.87352657], Z [0.521183, 27.2535858], while the authored interaction area is X [-2.7455, 4.3335], Z [-0.038, 27.254]. Do not treat those different geometry responsibilities as interchangeable.

**Existing System Fit for the requested next work:** ActivityPrefabSpaceProvider already binds every NavigationGeometryController below its prepared root to Activity/root Entity, and unbinds them on cleanup. NavigationObstacleController extracts the existing Collider2D geometry and publishes the existing registration contracts; NavigationService owns route avoidance. Spatial occupancy and navigation geometry remain separate owners. Hero interaction geometry already gives Skills an Entity recipient while allowing the visible hit area to be the gate. SkillHostValueEffectExecutor applies damage directly through HostValueService; an extra gate-health mutation followed by a brain-mediated hero mutation is not necessary for that existing representation. GroupRuntimeService and GroupHostData exist, but no group is registered in this ChainRush run; repository searches find no product call to TryRegisterGroup. Current Orchestration group-definition fields supply control priority to contributor orders, not a composed running group brain. Moving coordinated defence into a real group therefore requires an explicit composition/lifecycle plan; merely assigning GroupHostData would not implement it.

**Working-model direction, not implemented:** retain individual enemy acquisition/chasing/attacking, make coordinated defence an explicit group responsibility, and remove the current indefinite hero-follow behavior from the individual policy as part of that agreed integration. Register the gate's existing collider through NavigationObstacleController and align its authored combat area with the obstacle. The hero may be initially materialized inside that navigation obstacle: current registration/materialization validates spatial occupancy, not a prohibition on being seeded inside navigation geometry; ordinary movers must route outside it. Preserve direct damage to the Hero Entity through its gate-shaped interaction geometry. A separate gate CapabilityHost and damage-forwarding brain would introduce a second value/lifecycle and an additional mutation rather than reuse the existing single recipient. No changes in this direction have been authorized or applied yet.

**Best Practices:** assign coordination and individual combat separate control responsibilities and ensure a temporary control mode has a defined release back to autonomous targeting. Reuse the existing obstacle registration/cleanup boundary instead of adding collision checks to each brain; ActivityPrefabSpaceProvider's bind/unbind path is the current lifecycle owner. Keep one authoritative damage application through SkillHostValueEffectExecutor/HostValueService instead of recording damage twice and trying to relay it after the fact. These choices preserve Entity targets, existing preview/events, and route geometry revision caching, without per-frame gate queries or new service dependencies.

Evidence: encounter-trace-details-tool.json / encounter-trace-details.json, encounter-runtime-restored-tool.json / encounter-runtime-restored.json, wall-defense-read-tool.json / wall-defense.json, gate-registration-tool.json / gate-registration.json, trace-previous-setting.json in /tmp/chainrush-unit-movement-diagnosis-20261008/. Play Mode remains paused; tests and a post-correction gameplay run have not been performed.

**Gate targeting clarification — 2026-10-08.** Reported literally: «А почему враги обходят ворота и идут напрямую к герою, если должны бить ворота? Я честно говоря вообще не видел, что ворота интересовали вргов».

**Verified:** a separate read of the unchanged paused ChainRush run at frame 3114 confirms that all six loaded enemy Approach definitions use Interaction / CombatantRole. Three active BugBrownSmallApproach executions target Hero Entity 249. Their requested Entity position is (0, 0, 15), but their actual resolved route endpoints are (4.833, 0, 8.2), (4.833, 0, 3.2) and (4.833, 0, -0.538). MovementTargetResolver.Continue starts an Entity approach search with that explicit interaction geometry; EntityApproachPositionMatcher accepts contact with the shape rather than requiring the Entity centre. The active executions have already stored the respective resolved endpoints. These captured routes therefore approach the gate-shaped hero area, not the hero's centre. Enemy 489 is at (4.833, 0, -0.538) and has selected 249 in ContactNode as well as CombatTarget; this proves contact target selection near the lower corner, not successful damage application.

**Verified / limitation:** the gate is not a separate AI target or a registered navigation obstacle. The hero-owned combat shape and visible collider are offset from each other, including a lower combat boundary at Z=-0.038 versus collider Z=0.521183. The earlier statement “the enemy attacks the gate” describes the proposed representation too categorically: currently the AI selects the hero Entity and uses its gate-shaped interaction area. That alone neither blocks travel through the visible gate nor proves a contact attack succeeded or was visibly presented. Traversal all the way to the hero's centre in another interval has not been established by these captured routes; it remains a distinct reported observation. Do not substitute registering an obstacle as a proven fix for an unobserved attack failure.

The previously proposed working model remains unimplemented: align the existing attack/contact geometry with the gate and register its navigation obstacle, retaining one direct Health recipient. Existing System Fit remains AIBrain target selection → Skills MoveToTarget → MovementTargetResolver / WorldTargetResolutionService → Navigation, with the independent contact query → Skills HostValue effect. Preserve both chains rather than treating collision, approach and damage as one operation. Best Practices: validate the actual resolved route endpoint and attack result separately from the requested Entity position; the existing MovementState already records both positions and requires no new trace, poll or subscription for this read.

Evidence: read-enemy-gate-approach.cs, enemy-gate-approach-tool.json and enemy-gate-approach.json in /tmp/chainrush-unit-movement-diagnosis-20261008/. The target envelope explicitly identifies ChainRush. No product code, asset, Play Mode, capture setting or test execution was changed.
