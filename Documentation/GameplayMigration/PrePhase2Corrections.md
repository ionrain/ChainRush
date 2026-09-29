# Промежуточный план: исправления части 1 перед фазой 2

Дата: 2026-09-23.

**Implementation decision: working model. Статус: реализация шагов 1–5 завершена; приёмка ожидает отдельного разрешения.** Пользователь установил обязательную границу: ко второй фазе нельзя переходить, пока эти исправления не завершены. Этот документ заменяет решения о втором launch/seed payload, прямой записи прогресса через Activity feature, создании регионов через Activity feature и следовании производственных hosts за героем в D1, D2, D10 и D12 прежнего [плана подключений](ReworkConnections.md).

Цель — вернуть стартовые данные в существующий seed, описание мест выпуска в префабы, наблюдение времени и перемещения в Analytics, а запись прогресса в существующий путь Objective → Orchestration → Economy. Перенос тех же крупных исполнителей под новыми именами запрещён.

Прежнее утверждение о принятой части 1 не восстановлено: завершена реализация этого промежуточного плана, отдельный шаг вместимости реализован по последующему решению пользователя, приёмка остаётся открытой. Игровые сценарии и тесты этих исправлений не выполнялись.

## Результат реализации — 2026-09-23

- **Шаг 1 выполнен:** второй launch/seed runtime и четыре payload assets удалены. Четыре startup plan/action выбирают обычные GameFlow templates; развитие и loadout выданы через существующий Activity seed, без runtime-обращений к старым менеджерам.
- **Шаг 2 выполнен:** Host/External подключён через существующий lookup SpatialMarkerService, отдельный marker scope в materialization, индексы, ожидание и cleanup. Production variants используют общие каталоги/рецепты. SurviveWall/SurviveSpace и два Distance hero prefab содержат Cells и Volume. Удалены восемь pose/region assets, соответствующие features и четыре вспомогательных producer prefab. Производственные hosts сохранены в NonSpatial seed.
- **R1/R1.1 выполнены:** SpaceRegionController выбирает ActivitySpace/ProjectionEntity; запрос несёт anchor и локальную позу. VolumeRotation сохраняет начальную ориентацию; общий GetVolumeBox учитывает её. Отдельного движения компонента или второй реализации Volume нет.
- **Шаги 3–4 выполнены:** Analytics измеряет время Activity и перемещение Entity с выбором политики; последовательные позиции поступают через Knowledge.Invalidated и адресный индекс. Resource/Analytics-порог Objective передаёт Pending/Ready/Error в наблюдение и extraction. Три progress Objectives и обычные Issue/Consume operators игрока записывают LevelProgress. Частота, бюджеты и допустимая задержка сохранены. Прежние progress features и общий удалённый binding больше не используются.
- **Шаг 5 выполнен:** исходники затронутых проверок seed/composition/materialization/Analytics/порогов обновлены, включая Host/External и неоднозначность источников, начальный поворот и две политики пути. Все 48 исходных записей ContentRegistry и извлечённые параметры сохранены; целевые пути перепривязаны. D1/D2/D10/D12 документов описывают текущее подключение.
- Unity создала `.meta`; подключены все 67 подготовленных ссылок на новые scripts/assets. Выполнен обычный import/compile; результаты и логи записаны в ImplementationStatus.md. Test Runner, PlayMode, package suites и FullUnitChain не запускались.

Это статус внесения реализации и компиляции, не доказательство игрового поведения. Последующий отдельный шаг вместимости реализован по решению «0 = без лимита»; вторая фаза закрыта до приёмки.

## Границы

- Включены два настроенных уровня и четыре существующих варианта уровня/героя. Loc001Lvl03–05 остаются исключёнными.
- В новом игровом контуре ChainRush остаются assets, префабы, настройки сцены и специфические тесты. Общие возможности дополняются у существующих владельцев в package; новые игровые runtime/editor-исполнители не добавляются.
- Основной старый запуск не переключать. Новый контракт динамической меты не изобретать вместо удаляемого launch payload; подключение внешней меты остаётся фазой 2.
- Цели всегда Entity; геометрия — 3D. Связь Board и Autobattle сохраняется только через кошельки.
- Сохранить согласованные Skills, список Entity-целей, Heal/Buff/Gold, атомарность операторов, cooldown Production, конечный выпуск и AND/OR. Их повторная переделка в этот план не входит.
- Сохранить Spatial pose binding для carrier навыков, включая настраиваемый Entity-якорь и локальную позу. Удаляется его использование для перемещения производственных hosts.
- Строй, слоты, отход/возврат, автоматический маршрут героя и физика отбрасывания остаются отложенными. Опыт продолжает лететь к UI-шкале.
- Не добавлять автоматические миграции, ремонт assets, fallback на старый код или новые ограничения игры. Данные менять явными authoring-операциями; `.meta` создаёт Unity.

## Existing System Fit

| Ответственность | Проверенный владелец и переиспользование |
|---|---|
| Стартовый состав кошелька | `ActivityTeamWalletData.Seed`, `ActivityWalletSeedEntryData`, `SeedEntry`; внутренние кошельки host — `WalletEntry.Seed` |
| Mount, наследование, выдача seed и освобождение | `ActivityServiceCore.SeedWallets`, `TryAcquireMountedParticipantWallet`, `SeedMountedWallet`; сохранение порядка начальной выдачи и materialization |
| Запуск выбранного варианта | `GameStartupPlanData`, `AddGameFlowRuntimeActionData.template`, существующие GameFlow/Activity assets; PRNG seed остаётся у GameRuntime |
| Источник геометрии и тегов в префабе | `SpatialShapeProviderController` и `SpatialMarkerProviderController`; подключение/снятие через Projection binding |
| Регион, его поза, маркеры и доступ | `SpaceRegionService`, `SpatialMarkerService`, Spatial; Entity источника не обязана быть производителем |
| Выбор размещения и появление output | Population/Orchestration, `ActivityMaterializationEndpointRunner`, `ActivityProductionOutputMaterializationModule`; явный маркер, reservations, leases и подтверждение output |
| Время забега | Scheduling и lifecycle Activity; Analytics уже получает `stepIndex` и `elapsedSeconds` через `Tick` |
| Позиция наблюдаемой Entity | Spatial публикует изменение; `KnowledgeEntitySourceHandler` обновляет Position facet; Knowledge сохраняет правила доступности сведений |
| Расчёт наблюдаемого значения | `ActivityAnalyticsDomainState`, `ActivityAnalyticsCalculation`, metric data/modules и `ActivityAnalyticsRead` с ревизиями, бюджетом и подписками |
| Требуемый баланс прогресса | `ObjectiveConditionEconomyMetric`, `ObjectiveLongTargetProgressionData`, `ObjectiveNumericTargets`; вычисленное значение принадлежит runtime Objective |
| Исполнение записи | `EconomyOperationDecompOpData`, `EconomyOrchestrationEndpointRuntime`, Economy; endpoint перечитывает баланс перед операцией |

До этой переделки обычный production materialization fallback искал provider относительно producer; явный marker endpoint допускает источник другой Entity в той же Activity. `SpatialShapeProviderController` публикует Cells, а отвергаемая region feature использует Volume. Перенос одного компонента без согласования запросов, геометрии и тегов не завершает исправление.

До этой переделки Analytics поддерживала HostValue, Resource и EntityCount, а числовой порог Objective получал аргумент только из Economy. В рамках шагов 3–4 добавлены два вида наблюдений и Analytics-источник порога; существующая бюджетируемая AfterProduction-публикация сохранена.

## Best Practices

- **Композиция через данные существующего владельца.** `ActivityTeamWalletData` и `WalletEntry` уже описывают seed; `SpatialShapeProviderController` — геометрию префаба. Переиспользование этих контрактов сохраняет один lifecycle регистрации, наследования и cleanup. Второй payload с теми же записями и второй исполнитель геометрии создают дополнительное состояние, которое приходится согласовывать. Источники: [ActivityTeamWalletData](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Activities/ActivityTeamWalletData.cs), [WalletEntry](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Economy/WalletEntry.cs), [SpatialShapeProviderController](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/World/Spatial/SpatialShapeProviderController.cs).
- **Наблюдение отделено от записи.** В [CQRS](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs) модель чтения предоставляет наблюдения, а команды проходят проверки владельца изменений. Здесь Analytics измеряет время/смещение, Objective задаёт баланс, Economy исполняет операцию. Такая граница сохраняет правила кошелька и повторного исполнения. Задержка модели чтения должна быть явной; ссылка на CQRS не разрешает менять порядок simulation молча.
- **Повторное исполнение сверяется с текущим состоянием.** Существующий [Economy endpoint](/Users/ionrain/MorbooFrameworkPackage/Scripts/Core/Economy/Orchestration/EconomyOrchestrationEndpointRuntime.cs) перечитывает баланс и различает попытки исполнения. Следует передавать требуемое абсолютное значение, а не повторно выдавать накопленную дельту из собственного счётчика feature.

## Порядок и решения перед изменением зависимого кода

Выполнить шаги 1–5, затем отдельный ранее согласованный шаг вместимости, затем приёмку. Ответы пользователя от 2026-09-23 внесены ниже. Решения Q1–Q5 приняты. Подключение последовательных наблюдений Knowledge описано в шаге 3. Дополнение R1 к существующему SpaceRegionController согласовано пользователем и включено в реализацию. Открытых вопросов по текущему плану нет; новые расширения предъявлять отдельно. Не задавать повторно уже решённые вопросы.

| ID | Предмет | Решение и оставшаяся граница |
|---|---|---|
| Q1 | Источники зон | **Решено:** Survive использует зоны префаба стены; Distance — зоны префаба героя. Добавить `MaterializationMarkerProviderType { Host, External }`; существующий тег в ProductionData переименовать в `MaterializationMarkerProvider`. Host ищет по Entity производителя и тегу; External — по тегу в текущей Activity без Entity-фильтра. В обоих случаях требуется единственный поставщик, несколько совпадений — ошибка. Варианты production assets разделяют каталоги/рецепты и выбираются seed уровня. Начальное появление героя сохранить через Activity seed и его seed-маркер. |
| Q2 | Метрика перемещения | **Решено:** общая метрика поддерживает выбор политики. Для ChainRush выбрать неотрицательное смещение от старта вдоль authored 3D-оси: 10 вперёд и 10 назад дают 0. **Форма реализации:** одна метрика с двумя политиками; общие Entity binding, origin, подписки, ревизии и cleanup не дублировать. **Сумма пути:** учитывать каждое последовательное изменение позиции, включая телепортацию; скачок на 10 добавляет 10. Причину перемещения не определять, новый признак телепортации не добавлять. |
| Q3 | Частота и задержка | **Решено:** использовать существующие `ActivityAnalyticsConfigData.reviewInterval`, `maxPerspectivesPerReview`, `workBudget` и бюджеты Orchestration. Отдельный интервал записи не добавлять. Сохранить существующий порядок стадий; задержка допустима, обновление в том же simulation step не гарантируется. Существующий Reset и его стоимость учитывать; новый lifecycle Objective не добавлять. |
| Q4 | Округление | **Решено:** использовать существующее округление LongProgression; изменение результата относительно прежнего Floor принято явно. Новую политику округления не добавлять. |
| Q5 | Время Activity | **Решено:** метрика считает simulation-время той Activity, в настройках которой включена. В ChainRush её настроить для Autobattle. Общий код не знает об Autobattle, Board или названиях игры. Пауза не учитывается; Reset Objective не меняет начало времени Activity. |

**Дополнительная граница шага 2:** если действующие запросы не позволяют выбрать согласованный источник по тегам/scope, сначала предъявить точное изменение существующего запроса и всех его потребителей. Не создавать вместо этого proxy host, follower, новый Activity feature или вторую materialization-систему.

### Q1 — Host/External для источника маркеров

**Implementation decision: working model. Статус: решение пользователя реализовано в коде и assets; тесты не запускались.** Генератор может находиться в префабе барака, стены или героя. ProductionAgent не выбирает генератор самостоятельно; настройку читает существующий путь materialization.

**Authoring ProductionData:**

- Добавить enum `MaterializationMarkerProviderType { Host, External }` и serialized-поле `materializationMarkerProviderType` с публичным свойством `MaterializationMarkerProviderType`.
- Переименовать нынешнее serialized-поле `materializationProviderType` в `materializationMarkerProvider`, свойство `MaterializationProviderType` — в `MaterializationMarkerProvider`. Это по-прежнему TaxonomyTermData — тег/тип генератора, а не ссылка на конкретный экземпляр.
- В Host передавать в поиск EntityId производителя вместе с тегом. В External искать по тегу в текущей Activity без Entity-фильтра. External не исключает генератор самого производителя из совпадений; отсутствие Entity-фильтра не означает поиск в других Activity.
- В обоих режимах ноль совпадений означает недоступный источник и обычное ожидание; больше одного — ошибку неоднозначной настройки. Не объединять маркеры нескольких генераторов и не выбирать первый.
- Обычные экземпляры бараков используют Host, поэтому одинаковые теги на разных бараках не конфликтуют. ChainRush использует External: Survive — тип генератора стены, Distance — героя. При необходимости создать варианты ProductionData/host seed с общими ссылками на каталоги и рецепты; производственные очереди не объединять.

**Проверенный текущий путь:** ActivityProductionOutputMaterializationModule передаёт ProductionEntityId как SourceEntityId. ActivityServiceCore.TryResolveMaterializationScopeEntity превращает его в scope; SpatialMarkerService.TryResolveProviderKey ищет по ActivityId + scope Entity + provider type. SpatialMarkerProviderController.OnProjectionBound регистрирует provider со scope и anchor Entity собственного префаба. Host сохраняет этот выбор; External снимает только Entity-фильтр запроса.

**Конкретные действия package:**

1. **Обновить ProductionData и всех читателей переименованного свойства.** Проверки необходимости подтверждения output и preload продолжают читать тот же настроенный тег; переименование не меняет lifecycle. Обновить ProductionRuntime, accepted-output snapshots, ProductionStateOrchestrationModule, production decomposition, ProductionAgentWorkAdmission и materialization admission.
2. **Передавать режим поиска через существующие Activity materialization contracts.** В ActivityProductionOutputMaterializationModule отделить Entity производителя, нужную для корреляции/cleanup, от необязательного Entity-фильтра поиска. В ActivityMaterializationData, ActivityServiceCore и Spatial staging не подставлять producer/ActivityRoot вместо отсутствующего фильтра External. Для точного размещения сохранить реальную Entity выбранного маркера, reservation и lease; режим поиска не заменяет уже указанное размещение.
3. **Переписать существующий lookup SpatialMarkerService и его индексы.** Host разрешает прежний точный ключ. External использует Activity + provider type и проверяет единственность среди разных Entity. Регистрация/удаление обновляют оба вида поиска. После выбора использовать полный ProviderKey найденного источника — его реальный scope, provider id, геометрию и usage policy. Отсутствующий фильтр нельзя передавать дальше как идентичность выбранного источника.
4. **Согласованно обновить проверки доступности и ожидание.** GetProviderState, разрешение маркеров и связанные preflight/dependency consumers используют одинаковые правила Host/External. Регистрация внешнего источника пробуждает подходящие ожидающие outputs по Activity и тегу; Host дополнительно проверяет Entity. Удаление источника и cleanup остаются у действующих владельцев. Чужая Entity источника не означает исчезновения производителя или отмены его очереди.
5. **Обновить assets и fixtures явно.** Переписать старое имя serialized-поля и явно задать режим у всех ProductionData в package fixtures, ChainRush и MorbooFramework. Не добавлять FormerlySerializedAs, автоматическую миграцию, fallback или inferred режим. Unity создаёт `.meta` нового enum. В источниках генераторов сохранить текущие поля provider type/identity: их массовое переименование не запрошено.

**Сохранить без смены контракта:** подтверждение outputs, reservations/leases, compensation, cooldown, конечный выпуск, ручные заказы, исходные seed-маркеры и жизненный цикл Activity. Предыдущий вопрос о переносе момента завершения заказа отозван; выбранный Host/External не разрешает такую переделку.

**Файлы:** `Production/Authoring/ProductionData.cs` и новый enum рядом с production authoring; `Activities/Contracts/ActivityMaterializationData.cs`; `Activities/Internal/ActivityServiceCore.cs`; `Activities/ActivityProductionOutputMaterializationModule.cs` и его части; `Activities/Materialization/ActivityMaterializationEndpointRunner.cs`; `Activities/Orchestration/MaterializationOrchestrationEndpointRuntime.cs`; существующие Spatial staging/query contracts и `World/Spatial/SpatialMarkerService.cs`; существующие consumers ProductionData в Production/Orchestration, runtime и Knowledge; ProductionData assets/fixtures и authoring этих данных во всех трёх репозиториях. Новые сервисы, специальные Agents и игровые C#-ветки не добавлять.

**Existing System Fit:** ProductionData хранит выбранный режим и тег; Activity materialization применяет их при подключении output; SpatialMarkerService владеет поиском и индексами; provider/SpaceRegion/Spatial владеют геометрией и её Entity-привязкой. Production продолжает владеть заказом, не выбирая маркеры; Projection сохраняет регистрацию и снятие генераторов через binding.

**Best Practices:** фильтр поиска отделён от идентичности найденного объекта. Это сохраняет независимость нескольких экземпляров барака и позволяет использовать внешний генератор без переноса его к производителю. Используются существующие ProviderTypeKey/ProviderKey и подтверждение output; второй реестр или второй lifecycle не появляются. Проверенные источники: SpatialMarkerService.TryResolveProviderKey/ResolveAvailableMarkerSnapshot/GetProviderState, SpatialMarkerProviderController.OnProjectionBound и ActivityProductionOutputMaterializationModule.

## Шаг 1. Удалить второй launch/seed слой

1. **Переписать данные четырёх вариантов запуска** на существующие `ActivityTeamWalletData.Seed`. Перенести из `Level01PerfumeLaunch`, `Level01TabascoLaunch`, `Level02PerfumeLaunch`, `Level02TabascoLaunch` выбранный состав, развитие и остальные начальные entries без изменения количества, формы, тегов и признаков materialization. Внутренние возможности Entity продолжать задавать через `WalletEntry.Seed`.
2. **Настроить запуск через `AddGameFlowRuntimeActionData.template`** и существующие startup actions/plans. Четыре варианта должны оставаться явно выбираемыми через данные. Не изменять общий asset при старте и не читать старые игровые менеджеры.
3. **Удалить** `ActivityInputWallet`, `ActivityParticipantInput`, `ActivityParticipantInputData`, `ActivityLaunchInput`, `GameRuntimeLaunchData`, `GameRuntimeLaunchSnapshot`. **Удалить** четыре заменённых `*Launch.asset` после переноса их содержимого.
4. **Удалить только ветки этого payload:** `useLaunchInput` в AddGameFlow action; `GameStartupPlanData.launchInput`; `GameRuntimeContext.LaunchInput/SetLaunchInput`; `GameRuntimeHost.InitializeWithInput`, поле и capture-ветки; передачу payload в GameFlow/Activity requests, snapshots и runtime state. PRNG seed, roster, owner bindings и обычный путь запуска сохранить.
5. **Переписать `ActivityServiceCore.PrepareWalletSeed`** на единственный authored seed. Удалить `ValidateLaunchInput`, присоединение `inputWallet.CopySeed()` и запрет повторного mount, введённый исключительно ради launch payload. Не откатывать весь файл к checkpoint.
6. **Сохранить** существующие mount/refcount/inheritance/rollback и гарантированный порядок: стартовые данные развития и состава доступны до регистрации зависимых hosts. Двухпроходную начальную выдачу не удалять только потому, что она появилась рядом с launch payload. Runtime копирует entries на существующей границе выдачи; новый набор snapshot-классов не создавать.

**Файлы package:** `Scripts/Core/Activities/Activity*Input*.cs`; `GameRuntime/GameRuntimeLaunch*.cs`, `GameStartupPlanData.cs`, `GameRuntimeContext.cs`; `Runtime/GameRuntimeHost.cs`; `GameFlow/GameRuntime/AddGameFlowRuntimeActionData.cs`, `GameFlowContracts.cs`, `Internal/GameFlowServiceCore.cs`; `Activities/ActivityLaunchContracts.cs`, `Contracts/ActivityStartRequest.cs`, `Contracts/ActivityRuntimeSnapshot.cs`, `Internal/ActivityLauncherCore.cs`, `Internal/ActivityServiceCore.cs`.

**Assets ChainRush:** `Assets/Game/Runtime/Startup`; существующие четыре GameFlow/Activity варианта; исходные данные внутренних кошельков hosts. Точный состав перенесённых записей включить в ContentRegistry.

**Критерий:** выбранный вариант запускается штатными GameFlow/Activity запросами и единственным seed-путём; содержимое и развитие сохранены; второй payload и все его потребители удалены. Этот шаг не объявляет подключённой внешнюю мету фазы 2.

## Шаг 2. Источники размещения задать в префабах

1. **Настроить существующие provider-компоненты:** Survive — в префабе стены; Distance — в префабах героев. Форма, локальные размеры/положение, provider type, region tags, cell tags и marker tags задаются в данных. Разные источники различаются тегами. Применить согласованные Host/External и тег источника из Q1. Начальное появление героя сохранить через действующий Activity seed и его `materializationMarkerTags`; не включать его в зависимость от production-зон собственного prefab.
2. **Переписать выбор размещения** у соответствующих Population/Production подключений на эти источники. Для Population использовать существующий region query и его scope/tag-фильтры. Для выпуска в выбранный маркер использовать существующий exact-marker materialization path; геометрия не должна зависеть от положения producer.
3. **Разделить scope поиска и Entity-якорь геометрии.** Provider в Projection сейчас получает scope и anchor от Entity префаба. Существующие запросы ActivityRoot нельзя оставить без изменения, если источник опубликован в scope героя или стены. Применить режим Host/External из Q1; в External отсутствие Entity-фильтра не меняет реальную Entity-якорь источника. Несколько совпадений — ошибка; в Host сохранить фильтр производителя.

   **Для предложенных пользователем вариантов производственных юнитов:** каталоги и рецепты разделять ссылками на общие assets; различающиеся настройки провайдера задавать в соответствующих вариантах ProductionData/host seed. Не копировать рецепты и не добавлять C#-ветку по типу уровня. До фиксации варианта показать весь путь от его seed до выбранного маркера; если понадобятся изменения сверх согласованных Host/External, предъявить их отдельно и не объединять независимые производственные очереди переносом всех Production capabilities на героя.
4. **Сохранить размерность и форму размещения.** Явно описать Cells/Volume для каждой зоны. Не заменять Volume произвольной сеткой и не обнулять ось ради совместимости с текущим provider. Если согласованную геометрию нельзя описать существующим компонентом, отдельно согласовать расширение именно этого компонента перед кодом.
5. **Сохранить** регистрацию через Projection binding, обновление региона через SpaceRegion/Spatial, reservations/leases и освобождение при удалении источника/закрытии Activity. Исчезнувший источник делает размещение недоступным; producer не перемещается к запасной точке.
6. **Удалить** `ActivityEntityPoseFeatureData`, `ActivitySpaceRegionFeatureData`, четыре `*Poses.asset`, четыре `*Regions.asset` из `Autobattle/Bindings` и ссылки на них. **Удалить** provider-компоненты из префабов производственных hosts после переноса геометрии. Производственные capabilities и заказы сохранить; Entity не удалять только по прежнему имени Spawner/DeploymentHost.
7. **Удалить** постоянные смещения PlayerSpawner/WaterDeploymentHost/ColaDeploymentHost относительно героя. Мозги для следования им не назначать. Удалить их отдельные пространственные/визуальные объекты и seed-маркеры, служившие только переносчиками зон; способность Production сохранить в штатном host. Если у объекта остаётся другая функция, сохранить эту функцию и её необходимое представление, без follow-привязки.

**Файлы/данные:** `Activities/Features/ActivityEntityPoseFeatureData.cs`, `ActivitySpaceRegionFeatureData.cs`; ChainRush `Autobattle/Bindings`, `Projection/{PlayerSpawner,WaterDeploymentHost,ColaDeploymentHost,EnemySpawner,Perfume,Tabasco}.prefab`, выбранный prefab стены, Activity/Population/Production assets. `SpatialShapeProviderController`, `SpatialMarkerProviderController`, `SpaceRegionService`, `SpatialMarkerService`, `ActivityMaterializationEndpointRunner` и Production materialization переиспользовать; их новые изменения разрешаются только в явно согласованной границе выше.

**Критерий:** выпуск выбирает зоны стены/героя по данным; движение героя меняет его собственный источник, а не положение производственных hosts. При нескольких источниках выбор остаётся однозначным по согласованным фильтрам. Удаление Activity features не оставляет вторую регистрацию тех же зон.

### R1 — привязать существующий Volume-компонент к Entity префаба

**Implementation decision: working model. Статус: согласованное дополнение реализовано и скомпилировано; тесты не запускались.** Исправить прежнюю неполную проверку владельцев: Volume уже публикует `SpaceRegionController`; расширять `SpatialShapeProviderController` ради второго представления Volume не нужно.

1. **Оставить Cells** у существующих `SpatialShapeProviderController`/`SpatialShapeProviderData`. Их геометрию и публикацию маркеров не менять ради Volume.
2. **Расширить существующий SpaceRegionController:** добавить явный режим привязки `SpaceRegionBindingType { ActivitySpace, ProjectionEntity }`. ActivitySpace сохраняет текущий вызов через IActivitySpaceBindingConsumer. ProjectionEntity использует существующий IProjectionBindingConsumer и Entity из ProjectionBindingContext. Компонент исполняет только выбранный режим; не выбирать источник по наличию другого компонента и не регистрировать регион дважды.
3. **Сохранить существующие поля** regionId, localCenter, size, tags, stateType и planningAvailabilityType. В ProjectionEntity положение и поворот компонента относительно корня проекции задают локальную позу региона; размер учитывает масштаб префаба. Entity остаётся якорем, локальная поза — данными региона. Не заменять ориентированный 3D-объём охватывающим мировым AABB. ActivitySpace сохраняет прежнее поведение, пока отдельно не согласовано его изменение.
4. **Расширить существующий SpaceRegionVolumeRegisterRequest** явной регистрацией относительно Entity: anchor Entity, локальные смещение/поворот и размер. Сохранить прежний запрос абсолютного объёма для ActivitySpace. В существующем обработчике SpaceRegionService разрешить авторитетную Spatial-позу Entity и построить уже имеющиеся SpaceRegionRegistration/SpaceRegionGeometryDefinition.Volume. Controller отправляет запрос через EventBus; прямых вызовов сервисов и отдельного адаптера/регистратора не добавлять.
5. **Переиспользовать движение и cleanup SpaceRegionService:** он уже индексирует AnchorEntityId и слушает EntitySpatialPoseChangedEvent/удаление позиции/закрытие Activity. Компонент не подписывается на каждое движение героя. На unbind/disable освободить единственный handle через существующий SpaceRegionReleaseRequest; повторное освобождение безопасно и не создаёт новую регистрацию.
6. **Authoring:** Volume-зоны задать этим компонентом на нужных префабах, с явным режимом привязки. Cells-генераторы маркеров остаются на существующих компонентах. Population размещает объекты в Volume через действующий пространственный расчёт и точные маркеры; не объявлять Volume автоматически генератором фиксированной сетки маркеров. После переноса удалить ActivitySpaceRegionFeatureData и её assets по основному шагу 2.

**Файлы:** `World/Spatial/SpaceRegionController.cs`, новый enum рядом с ним, `SpaceRegionVolumeRegisterRequest.cs`, существующий listener в `SpaceRegionService.cs`, связанные префабы и исходники проверок. `ActivityPrefabSpaceProvider` и `ProjectionBindingController` уже вызывают соответствующие интерфейсы; новые обходы и привязчики не добавлять. Все существующие экземпляры компонента явно настроить в ActivitySpace, новые привязанные к Entity — в ProjectionEntity; автоматическую миграцию не вводить.

**Existing System Fit:** ActivityPrefabSpaceProvider и ProjectionBindingController доставляют разные существующие события привязки; SpaceRegionController описывает Volume; EventBus передаёт запрос; SpaceRegionService владеет регистрацией, Spatial-якорем и cleanup. **Best Practices:** дополнить существующего владельца объёмной геометрии вместо второй реализации Volume; разделить локальные данные префаба и авторитетную позу Entity. Источники: SpaceRegionController.BindActivitySpace; SpaceRegionService.Listener и IndexEntity; ProjectionBindingController; SpaceRegionGeometryDefinition.Volume.

### R1.1 — начальный поворот Volume: согласовано

**Implementation decision: working model. Пользователь согласовал дополнение; изменения внесены и компилируются. Тесты не запускались.** В утверждённом R1 было указано сохранение ориентированного 3D-объёма через существующий `SpaceRegionGeometryDefinition.Volume`. Чтение прежнего конечного расчёта показало, что ему недостаёт начальной ориентации объёма.

**Verified до изменения:** `SpaceRegionGeometryDefinition.Volume` принимал только bounds. `SpaceRegionGeometry.GetVolumeBox` рассчитывает поворот как `текущий Anchor.Rotation × inverse(начальный Anchor.Rotation)`. При регистрации эти повороты совпадают, поэтому оси объёма остаются мировыми независимо от переданного начального поворота. `GetBounds`, contains и overlap используют этот же `GetVolumeBox`. Это результат чтения кода; причинный игровой сценарий не запускался.

**Конкретное дополнение:**

1. **Добавить** начальный поворот объёма в существующий `SpaceRegionGeometryDefinition`: свойство `VolumeRotation` и вариант создания `Volume(bounds, rotation)`. Существующий `Volume(bounds)` сохраняет явно заданную мировую ориентацию identity.
2. **Передать** разрешённый начальный поворот из существующего обработчика `SpaceRegionVolumeRegisterRequest` в эту геометрию. Префаб задаёт его своим Transform; отдельного игрового поля и исполнителя нет.
3. **Переписать** `SpaceRegionGeometry.GetVolumeBox`: оси рассчитываются как `изменение поворота якоря × VolumeRotation`. Центр и размеры сохраняют существующую семантику. Проверять конечность и ненулевую длину Quaternion при построении геометрии, без автоматического ремонта authored данных.
4. **Переиспользовать** существующие bounds/contains/overlap, которые уже вызывают `GetVolumeBox`. Cells, Production и lifecycle регистрации не менять.

**Existing System Fit:** SpaceRegionController извлекает локальную позу из префаба; listener SpaceRegionService разрешает Entity-якорь; SpaceRegionGeometryDefinition хранит геометрию; SpaceRegionGeometry отвечает за её запросы. Изменения нужны в последних двух владельцах и уже изменяемом listener, новый сервис не требуется.

**Best Practices:** исходная ориентация геометрии и изменение позы якоря — разные входы. Их явное хранение сохраняет повёрнутый прямоугольный объём и единый расчёт всех пространственных запросов. В этой системе тот же принцип уже применяется для Cells: локальная геометрия хранится относительно начального якоря. Точные источники: `World/Spatial/SpaceRegionGeometryDefinition.cs`, `SpaceRegionGeometry.GetVolumeBox/GetBox/GetBounds`, `SpaceRegionService.Listener.OnEvent(SpaceRegionVolumeRegisterRequest)`.

Пользователь отдельно согласовал изменение `SpaceRegionGeometryDefinition`, `SpaceRegionGeometry` и обработчика регистрации. Оно включено в текущую реализацию R1.

## Шаг 3. Время и перемещение представить метриками Analytics

1. **Добавить два общих определения метрик:** `ElapsedSimulationActivityAnalyticsMetricData` и `EntityMovementActivityAnalyticsMetricData`. У метрики перемещения задать enum `EntityMovementCalculationType` с политиками `DisplacementAlongAxis` и `PathLength`. Первая возвращает неотрицательную проекцию текущего смещения от origin на заданную 3D-ось; вторая накапливает длины последовательных перемещений в 3D. Для ChainRush выбрать первую политику. Ось используется только первой политикой. Один общий runtime хранит выбранную Entity, origin/последнюю позицию, состояние готовности и подписки; различаются расчёт и накопление. Не создавать две параллельные системы наблюдения Entity.
2. **Время читать из существующего simulation-контекста Activity, в которой настроена метрика.** Второй таймер, wall clock и собственный update-loop не создавать. Начало/конец принадлежат этой Activity; Reset Objective не меняет начало измерения. В ChainRush metric asset включается в Analytics-настройки Autobattle; никаких проверок имени/типа Autobattle в общем коде нет.
3. **Entity выбирать через существующие Knowledge-запросы и фильтры** по участнику и данным содержимого, затем сохранять её Entity identity в runtime измерения. Для героя разрешать одну Entity; неоднозначность не заменять выбором первой. Не переносить `ActivityMaterializedEntityBindingData` в Analytics под другим именем.
4. **Стартовую позу фиксировать один раз до учёта первого перемещения выбранной Entity.** Использовать существующий lifecycle регистрации/позиционных наблюдений. Отложенное чтение из-за бюджета не должно превращать позднюю позицию в стартовую. До готовности входов выдавать Pending, а не нулевое измерение. Потерю обязательного источника показывать читателю явно; не переключаться молча на другую Entity и не начинать измерение заново.
5. **Расчёт и состояние разместить в существующем Analytics runtime**, расширив metric/module contracts и `ActivityAnalyticsCalculation/DomainState` для этих входов. Время не рассчитывать обходом записей каждого host. Для перемещения запрашивать Position facet нужной Entity; не обходить видимость Knowledge прямым поиском всех Spatial Entity.

   **Интервал расчёта не должен терять путь.** Для PathLength учитывать последовательные позиционные наблюдения до объединения обновлений, а публикацию результата выполнять по существующему `reviewInterval`. Суммировать 3D-расстояния между последовательными позициями независимо от причины их изменения: телепортация также увеличивает путь. Первая зарегистрированная позиция задаёт начало и не считается перемещением от мирового нуля. Не рассчитывать длину только между редкими опубликованными точками и не называть её полным путём. До кода предъявить конкретное подключение последовательных наблюдений через существующего владельца Knowledge; самостоятельный игровой трекер, признаки телепортации и эвристики скачков не создавать.
6. **Переиспользовать `ActivityAnalyticsRead`, SourceRevision/ComputedRevision, Watch и cleanup.** Идентичность расчёта включает domain, источник Entity/Activity и параметры измерения, включая ось/начало. Несколько читателей одного измерения используют один расчёт. Переоткрытие read и Reset Objective не сбрасывают origin; закрытие Activity завершает измерение и снимает подписки.
7. **Не помещать в метрики** LevelProgress, длительность конкретного уровня, целевой кошелёк, Issue/Consume, правила волн или остановки забега. Это входы следующего шага либо фазы 2.

**Файлы package:** `Activities/Analytics/Authoring`, `ActivityAnalyticsContracts.cs`, `ActivityAnalyticsSourceKey.cs`, `Modules`, `Internal/ActivityAnalyticsCalculation.cs`, `Internal/ActivityAnalyticsDomainState.cs`, связанные validation/read contracts. Существующий `KnowledgeEntitySourceHandler` и Activity tick — источники для подключения, не место игрового расчёта прогресса. Если для фиксации origin понадобится новый контракт наблюдения, сначала предъявить его поля, владельца и порядок событий; прямой обход Knowledge не использовать как временную замену.

**Конкретное подключение последовательных наблюдений (в рамках согласованной метрики):**

- `KnowledgeEntitySourceHandler.OnEvent(EntityPositionChangedEvent)` сначала обновляет записи через `KnowledgeSourceHandlerContext.UpdateEntityPositions`, затем вызывает `KnowledgeBroker.NotifyChanged`. У Broker уже есть синхронный `Invalidated`, который срабатывает до объединения публичных KnowledgeChangedEvent; `ActivityAnalyticsService` уже подписан на него. Переиспользовать эту цепочку, новый stream/event и отдельный трекер не создавать.
- В существующем Analytics domain направлять позиционную invalidation только измерениям соответствующей Entity. Считать очередной вклад сразу до возврата из уведомления, а публикацию и остальные вычисления оставлять под reviewInterval/workBudget. Не запускать полный пересчёт Analytics на каждом перемещении.
- Позицию читать через Knowledge с текущими правилами видимости и фильтрами authored-запроса. Для адресного чтения использовать существующий индекс RecordsByTargetEntityValue и существующую проекцию записи TryProjectCurrent; оформить адресное чтение в существующем KnowledgeService.Reads, не обходить всю базу на каждом перемещении и не читать Spatial напрямую. Повторные записи одной Entity и повторные invalidation не являются дополнительными перемещениями.
- Инициализировать authored-измерения при Open Analytics, независимо от внешних readers. Для уже доступной Entity сразу зафиксировать исходное наблюдение; для поздней Entity — при первом доступном позиционном уведомлении после регистрации. Сохранять origin, предыдущую позицию и накопленное расстояние в существующем runtime расчёта, пока Activity открыта. Новая подписка читателя и Reset Objective не начинают новую историю.
- Регистрация/изменение состава Knowledge разрешает Entity по заданным фильтрам; неоднозначность даёт явную ошибку. Потеря позиции/доступности прерывает готовность измерения, а не засчитывает неизвестный интервал как нулевой путь. Не переключать Entity и не сбрасывать origin автоматически. Закрытие Activity удаляет расчёты и их адресные индексы; общий subscription Analytics остаётся у существующего владельца.

**Проверенные источники:** `KnowledgeBroker.NotifyChanged/Invalidated`, `KnowledgeEntitySourceHandler.PublishPosition`, `KnowledgeSourceHandlerContext.UpdateEntityPositions`, `KnowledgeService.Reads.TryProjectCurrent`, `ActivityAnalyticsService.EnsureListenerRegistered/MarkKnowledgeDirty`. Это повторное использование существующего немедленного уведомления, а не новый контракт доставки событий. Подключение реализовано; тесты не запускались.

**Критерий:** метрики доступны независимо от наличия ресурса LevelProgress и Objective; значения не зависят от числа читателей, повторной подписки и бюджета вычисления. История измерения не обнуляется из-за Reset требования.

## Шаг 4. Записывать прогресс через существующий Economy endpoint Orchestration

1. **Расширить существующий `ObjectiveLongTargetProgressionData` выбором источника аргумента Resource/Analytics**. Для Analytics задать metric, measure и перспективу чтения; преобразование аргумента и итоговый объём описывать существующей `LongProgressionData`. Не создавать отдельный ProgressObjective, AnalyticsProgressAgent или записывающий feature.
2. **Переписать `ObjectiveNumericTargets`** для подключения обоих источников, освобождения чтения и подписки, обработки Pending/Ready/Error. Пересчитывать только runtime-порог. Pending не означает target=0 и не допускает запись Economy. Это состояние необходимо согласованно передавать оценке условия и извлечению факта; не ограничиваться добавлением serialized поля.
3. **Согласованно обновить** `IObjectiveProgressiveLongCondition`, runtime target snapshot, `ObjectiveOrchestrationFactContainer` и их потребителей, если им требуется состояние готовности. Сохранить `ObjectiveRequirementsChangedEvent` и обычный refresh Orchestration. Готовый планировочный факт остаётся EconomyAmount; новый планировщик аналитики для этого пути не добавлять.
4. **Задать в assets обоих участников обычное Economy-требование:** `LevelProgress == рассчитанный порог`, с существующей политикой повторной активации Reset. Настроить существующие `EconomyOperationDecompOpData` для Issue и Consume только нужного ресурса/кошелька. Одно изменившееся наблюдение не создаёт независимые параллельные требования записи одного баланса.
5. **Переиспользовать `EconomyOrchestrationEndpointRuntime`**: актуальный баланс читается перед операцией; учитываются текущая разница и идентичность попытки. Не хранить `_published`/неприменённую дельту в новом посреднике. При временной недоступности действует существующее ожидание; при новом целевом значении требование обновляется обычным путём Orchestration.
6. **Настроить уровни явно:** Survive — время Activity/300; Distance — неотрицательное смещение героя вдоль оси/150. Результат в шкале 0…1 000 000 с существующим округлением LongProgression; отличие от прежнего Floor принято пользователем. Не использовать `NormalizedRelativeAmount`: сейчас он нормализует величину относительно других источников Analytics, а не относительно цели уровня.
7. **Сохранить одного писателя LevelProgress на каждый кошелёк участника.** По решению пользователя от 2026-09-28 прогресс начисляется и игроку, и боту; оба читают время Activity либо смещение одного героя. У каждого свой экземпляр Objective с ContextOwner. Ресурсная LevelProgressMetric использует scope Participant и не суммирует две копии. Операторы начисления бота выбираются только для сравнения Equal, через существующий CompareOperationDecisionConditionData. Существующий `LevelProgressMetric.asset` читает ресурс и может остаться downstream-метрикой. Его запрещено использовать входом порога, который записывает тот же ресурс. Board и прочие Activity продолжают получать прогресс только через кошелёк.
8. **Частоту задавать существующим `ActivityAnalyticsConfigData.reviewInterval`.** Сохранить `maxPerspectivesPerReview`, `workBudget`, `AfterProduction` и очереди Orchestration. Отдельный таймер/интервал записи прогресса не добавлять. Объединять уведомления и не менять требование при неизменном итоговом long. Не вызывать полную декомпозицию для каждого изменения положения всех Entity. Задержка записи допустима по явному решению пользователя; обновление в том же шаге не гарантируется. Порядок стадий и lifecycle Objective ради этого пути не переделывать.
9. **Удалить** `ActivityNumericEconomyFeatureData.cs`, `ActivityNumericSourceData.cs`, `ActivityElapsedSimulationSourceData.cs`, `ActivityEntityDisplacementSourceData.cs`; `RunProgressFeature.asset`, `DistanceProgressFeature.asset`, `TabascoDistanceProgressFeature.asset` и их ссылки. После удаления всех трёх потребителей **удалить** `ActivityMaterializedEntityBindingData.cs`.

**Файлы package:** `Objectives/Conditions/ObjectiveLongTargetProgressionData.cs`, `IObjectiveProgressiveLongCondition.cs`, `ObjectiveConditionEconomyMetric.cs`, общий condition численности при изменении интерфейса; `Objectives/Internal/ObjectiveNumericTargets.cs`, `ObjectiveNumericTargetBindingSnapshot.cs`, `Objectives/Orchestration/ObjectiveOrchestrationFactContainer.cs`. Economy operator/endpoint переиспользовать; новый исполнитель не требуется.

**Assets:** новые metric definitions/configs и Economy Objective/операторы игрока; четыре Activity варианта; удаляемые `Assets/Game/Runtime/Run/*ProgressFeature.asset`. Population-прогрессии и условия врагов продолжают читать тот же ресурс.

**Критерий:** время/смещение → Analytics → числовой порог → Orchestration → Economy работает одним путём. Обратное движение обрабатывается по согласованной семантике; временно отсутствующая метрика не обнуляет ресурс; повторное уведомление не выдаёт прогресс повторно.

## Шаг 5. Завершить связанный код, assets и исходники проверок

1. **Удалить все ставшие неиспользуемыми типы, поля, вызовы, assets и fixtures**, относящиеся к четырём исправленным соединениям. Проверить package, ChainRush и host MorbooFramework. Не сохранять мёртвые пути ради компиляции старого теста; тест чисто удалённой функциональности удалить.
2. **Переписать исходники существующих проверок** seed, composition, регионов, Analytics и прогрессивных требований под итоговые контракты. Для ChainRush сохранить сценарии четырёх вариантов запуска и полного gameplay pipeline. Тесты на наличие конкретной отвергнутой feature не заменяют проверку результата.
3. **Обновить ContentRegistry и статус реализации** по реально подключённым assets. Сохранить извлечённые параметры контента. В ReworkConnections/ReworkDisposition убрать действующие указания на отвергнутые соединения; историю явно отделить от текущих действий.
4. **Завершить authoring и обычный Unity import/compile**, не вызывая тестовые команды. Проверить исходники и ссылки, однозначность provider tags/scopes, отсутствие второй регистрации зон и второго писателя одного и того же баланса прогресса. `.meta` получает Unity.
5. **Отчитаться о полном завершении реализации промежуточного плана** только после шагов 1–5 и разрешения затрагивающих их вопросов. Не называть успешную компиляцию проверенным игровым поведением.

**Критерий:** ни один из удалённых путей не используется в текущем запуске, package и fixtures; authored контент подключён к итоговой модели, а документы не объявляют невыполненные исправления завершёнными.

## Отдельный шаг после реализации, до тестов: вместимость

**Выполнен отдельно после шагов 1–5 по явному решению пользователя:** добавить `maxCapacity = 0` в общий Pooling и Projection authoring; выбрать 0 у 38 используемых игровых пулов. Начальное заполнение и игровые правила сохранены. [ContentCapacity.md](ContentCapacity.md) содержит контракт и текущую таблицу. Решение о максимуме больше не является открытым вопросом; производительность и память остаются предметом разрешённой приёмки. Сам этот шаг не разрешает запуск тестов.

## Приёмка и запрет перехода ко второй фазе

**Ни одного теста до полной реализации всего утверждённого плана и отдельного явного разрешения пользователя.** Это относится к диагностическим, focused, smoke, package, integration и FullUnitChain запускам, в том числе косвенным. До этого разрешены чтение кода/данных, правка исходников и обычная компиляция. Не запрашивать разрешение на тесты после отдельного шага.

После полного завершения реализации и отдельного разрешения проверить:

- Четыре старта через существующий seed, сохранение развития/loadout, отсутствие повторной выдачи при mount/inheritance, cleanup и повторный запуск.
- Выбор зон стены/героя по тегам, несколько источников, появление/удаление источника, движение героя, занятость места, ожидание и отмена materialization без follower-hosts.
- Время с паузой и повторным стартом; две политики перемещения в 3D: для 10 вперёд и 10 назад смещение равно 0, путь равен 20; телепортация на 10 добавляет 10 к пути. Позднее появление/потеря Entity; несколько readers; малый бюджет, отсутствие смещения origin и потерь промежуточных позиционных изменений.
- Прогресс через Economy: повторные уведомления, неизменный порог, рост/уменьшение, границы округления, Pending/Error, временная недоступность записи, обновление требования во время ожидания и согласованная задержка.
- Сохранение цепочки `бой → опыт → BoardTurnToken → доска → выбор → Stack/выпуск`, carrier-привязок и подбора опыта к UI.
- Затронутые package suites и ChainRush integration; FullUnitChain после изменений общих контрактов. Конкретный перечень запусков предъявить вместе с отчётом о завершении, не запускать автоматически.

**Фаза 2 остаётся закрытой**, пока исправления не реализованы, связанные решения не разрешены, отдельный шаг вместимости не закрыт согласованным способом и приёмка не завершена. До разрешения тестов статус — «реализация завершена, приёмка ожидает разрешения», а не «часть 1 принята».

Во второй фазе по-прежнему остаются окончательная остановка/завершение забега, подтверждение очистки после закрытия выпуска, награды внешней меты, завершённые HUD/результат, основной запуск и его финальная очистка. Эти задачи не используются как обход незаконченных исправлений данного документа.
