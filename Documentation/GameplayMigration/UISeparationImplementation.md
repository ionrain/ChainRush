# Отделение UI от сервисов

Implementation decision: **working model**. Реализован план, утверждённый пользователем 29 сентября 2026. Runtime-приёмка ещё не проводилась.

## Existing System Fit

Economy владеет записями и подписками; Production — admission, ценой, резервированием и выпуском; Rewards — выдачей и журналом. GameFlow владеет последовательностью атомарных действий, Activity — игровым пространством, UIFlow — представлениями. CapabilityHosts разрешает источники характеристик; существующий SkillHostProjectionCore вычисляет значения. GameRuntime подключает UI и запускает сохранение.

## Best Practices

Это направление зависимостей соответствует Ports and Adapters: внешний адаптер обращается к приложению, сервис не создаёт UI-клиента. Sources сохраняют состояние представления по Presentation Model: виджеты используют один snapshot и отправляют намерения, а вычисления остаются у доменных владельцев. В реализации это выражено отдельным UI-installer, явным owner-контекстом и атомарным шагом GameFlow, который обращается к существующему Production.

- https://alistair.cockburn.us/hexagonal-architecture
- https://martinfowler.com/eaaDev/PresentationModel.html

## Выполненные изменения кода

- Удалены InstallUIAdapter и поля адаптеров из EconomyService, ProductionService и RewardService. Доменные installers больше не создают UI.
- Добавлен UIRuntimeInstallerData; GameRuntimeContext владеет IDisposable-регистрациями. Host освобождает их при завершении, неудачной инициализации и перед повторной попыткой.
- UI-адаптеры и их сообщения перемещены в UI/Economy, UI/Production и UI/Rewards. Rewards-адаптер сохранён.
- UI-space Activity, его context, cell и region reader перемещены в UI/Activities. Контракт IActivitySpaceProvider сохранён.
- GameFlow использует GameFlowPresentationHandle. Конкретные UI presentation/provider/context/scene condition находятся в UI/GameFlow. Общие условия поддерживают runtime-проверку с подпиской и IDisposable.
- GameFlowActivityLaunchObserveRequest адресуется runtime/step/request id, без UIHandle. Подпиской Loading владеет UI.
- Владельца UI определяет контекст представления. PlayerData разрешается в действующего Player в UI-provider. Адаптеры принимают явного владельца.
- Покупка передаёт обычный ProductionOrderRequest как вход GameFlow. Атомарный Production executor возвращает OrderId; следующий шаг наблюдает его состояние. Runtime-событие публикует корреляцию, владельца и результаты шагов.
- GameRuntimeSavePolicy наблюдает результаты покупок и команд тегов независимо от панели. UI-адаптеры не запускают сохранение.
- EconomyNumericQueryData, enum Resource/Attribute и прежний numeric transport удалены. EconomyValueUIField использует EconomyEntrySelectionData; Source отправляет один пакет selectors.
- CapabilityHostAttributeSources переиспользуется живым host и просмотром. В просмотре список source ids явный; participant-development задан двум панелям ChainRush. Арифметика остаётся в SkillHostProjectionCore.
- EconomyItemUIData объединяет данные каталога/цены/награды. EconomyAssetData больше не реализует IListViewItemData. Дубли EconomyAmountListView/Item/UIData удалены; общий список содержит optional amount/balance presentation и Append.

## Authoring

Существующие script files перемещены с существующими .meta; GUID не изменялись. Ссылки сериализованных типов, UnityEvents и дублирующих компонентов явно обновлены в затронутых assets обоих проектов. 12 числовых полей FrameworkMain переведены на существующие selectors.

Через Unity созданы и подключены:

- `Assets/Game/Runtime/Installers/ChainRushUIRuntimeInstaller.asset` в профиле ChainRush.
- `Assets/Game/Resources/GameRuntimeHost/Installers/UIRuntimeInstallerData.asset` в профиле MorbooFramework.
- `Assets/Game/FrameworkUI/Data/GameFlow/PurchaseFlow.asset`: шаг `order` возвращает настоящий OrderId; шаг `result` ожидает Completed, а Cancelled/Failed завершают сценарий ошибкой. Терминальная политика удаляет экземпляр после уведомления.
- Все четыре Purchase assets используют этот сценарий. Он не добавляется в startup и создаётся для конкретной команды.

Команды воспроизводятся из `Tools/Authoring/UISeparationAuthoring.cs`: методы `ChainRush` и `Framework` выполняются в соответствующем проекте. Для исполнения файл временно помещается в Editor-папку; временные копии удалены. Новые `.meta` создал Unity.

Исходные сцены ChainRush Start/Main/Loading не изменялись этим рефакторингом. Изменены только Framework-копии и их префабы; в MorbooFramework обновлена существующая Main.

## Fixtures и статус

- Existing Economy UI fixtures переведены на явный контекст и EconomyItemUIData; проверка некорректного authored owner заменена отсутствующим владельцем контекста.
- GameFlow presentation fixture регистрирует игрока перед открытием UI.
- Добавлена проверка обратного, однократного освобождения ресурсов GameRuntime, включая исключение одного из владельцев.
- Добавлен сценарий атомарного admission Production и отдельного ожидания результата: сохранение подтверждённого выпуска без views, в том числе после снятия наблюдения GameFlow; терминальное событие до удаления экземпляра.
- Реализация и authoring завершены. Тестовые сценарии написаны, но не запускались.

## Проверки

**Тесты и Play Mode не запускались.** Их запуск разрешён только после завершения всего плана и отдельного явного разрешения пользователя. Компиляция, необходимая Unity для authoring и импорта, не является игровым прогоном или запуском тестов; результаты runtime-проверок здесь не заявляются.

Unity authoring завершился успешно в обоих проектах; последний импорт/компиляция MorbooFramework, включая package fixtures, прошёл без ошибок компиляции. Статический поиск не находит прежних numeric-типов, amount-списков, UI-адаптеров в сервисах, UIHandle/UIFlow в Economy/Production/Rewards/GameFlow/Activities и старых сериализованных имён в assets обоих проектов.

После разрешения: runtime с UI/без UI; ошибки/повторная инициализация; покупки и закрытая панель; выбор/замена runtime tags; цены/награды через общий список; совпадение характеристик и адресные обновления; loading/activity cleanup; отсутствие старых путей.
