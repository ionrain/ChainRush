# Heal: формулы ресурсов и обработка выбора без Production

Implementation decision: **working model**. Пользователь согласовал реализацию и оптимизацию. Этот документ заменяет в D3 схему Heal1..16, единичного списания и HealSelectionRecipe1..16. Остальная часть утверждённого плана сохраняется.

## Existing System Fit

- Selection отмечает выбранные Tokens. BoardHost исполняет один навык через атомарный SkillDecompOpData.
- Skills рассчитывает эффекты; Economy владеет балансом, leases и общим commit списания Tokens с выдачей Stack.
- Autobattle получает только ресурс через общий кошелёк участника. Orchestration назначает authored состояние постоянному исполнителю; AIBrain выбирает Entity-цели.
- HostValue executor применяет лечение. Activity владеет составом Entity и cleanup. Новых игровых исполнителей, менеджеров и межактивностных сообщений нет.

## Изменения

1. Переименовать существующие SkillHostValueFormulaData/TermData/OperandType/Evaluator в SkillEffectFormula…; использовать одну структуру и вычислитель для HostValue и Economy effects. Сохранить исходные математические операции и знак эффекта. Добавить EconomyAmount с Entity-источником Owner/Target, экономическим владельцем Host/Root и существующим EconomyEntrySelectionData. Для числового операнда задавать один конкретный asset и одну форму. Отсутствующий баланс равен нулю; невалидная конфигурация не исправляется автоматически.
2. При регистрации Skills проверить формулы и подготовить постоянные коэффициенты точности и описание запросов. На одно действие кешировать запрос по фактическому владельцу и содержимому селектора, чтобы одинаковые независимо authored источники читались один раз. Оценка и исполнение используют общий расчёт, но исполнение заново получает актуальные значения. Кеш не переносит баланс между действиями.
3. Перед изменениями состояния вычислить все величины. Передавать текущую Entity-получателя непосредственно вычислителю, без одноэлементного SkillTarget и копирования списка на каждую цель. Рассчитать каждую delta один раз; предварительная проверка и применение используют её повторно. Буферы принадлежат runtime, а не ScriptableObject.
4. Заменить запрет Target-формул при нескольких целях: эффекты на Target рассчитываются отдельно для каждой Entity. Формулы одиночного эффекта на Owner/Root не могут неявно выбрать одну из нескольких целей. ExecuteWithoutTarget пропускает зависимые от Target эффекты; расход ресурса выполняется.
5. Добавить runtime-tag фильтр числовому Economy effect. Передавать его через executor, accumulator, estimates, funding facts и outcome projector. В SkillDecompOpData заменить запрет тегированных фактов сопоставлением селекторов. Выбранные Tokens не смешивать с невыбранными.
6. Последовательные числовые Economy effects одного action применять одним commit. Использовать существующие TryAcquireEntryLease, TryCommitEntryLeasesAndProvisionalEntries и TryReleaseEntryLease. Существующий EconomyTxBuilder содержит Stack-изменения, leases — точно выбранные Tokens. При отказе освободить leases; AppliedEffects отмечать после commit всей группы. Lifecycle Production и новый транзакционный сервис не добавлять.
7. BoardHost получает SkillOwner/ControlOwner и один навык выдачи Heal. M — количество HealBoardBase Tokens с Selected в Board wallet. Навык расходует именно M выбранных Tokens и выдаёт M Stack единственного Heal в shared participant wallet одним commit. BoardBrain использует существующий SkillDecompOpData. После commit продолжаются BoardTurnToken, очистка и заполнение. Рецепт появления Heal-клетки при заполнении доски сохраняется.
8. Один Heal в Autobattle: первый эффект списывает −N из общего кошелька, второй лечит каждую цель на 10% × N × её maximum Health. N — весь доступный Stack Heal, прочитанный перед действием. При накоплении 3+5 до расчёта используется 8; поступление после расчёта остаётся на следующее исполнение. Границы выборов Board не сохраняются. Локальный seed навыка исполнителя не является расходуемым источником. Без целей расход выполняется один раз. Завершение Objective по нулевому остатку не прерывает исполнение.
9. В SelectActivityTargetAIBrainActionData использовать ActivityService.CollectEntities и CapabilityHostService.TryGet вместо GetAll и повторных сортировок. Activity уже предоставляет Entity в стабильном порядке. Запрос выполняется при выборе целей, дополнительный опрос всех Entity каждый такт не вводится.
10. Свести Heal skills, состояния, Objectives и операторы к одному комплекту; удалить HealSelectionRecipe1..16 и ссылки на них. Обновить BoardHost, BoardBrain, PlayerBrain, SkillExecutorBrain, seed, четыре Activity-варианта и installers. Переименовать ссылки на формулы в существующем authoring и тестовых исходниках явно. Новых ChainRush runtime/editor классов нет. Unity создаёт новые .meta; автоматических миграций и изменения GUID нет.

## Best Practices

Связанные списание и выдача коммитятся вместе: это предотвращает оплату без выдачи и выдачу без оплаты. Принцип атомарности описан в [PostgreSQL Transactions](https://www.postgresql.org/docs/current/tutorial-transactions.html); в проекте используется уже существующая реализация Economy, которую вызывает ProductionRuntime.

Уменьшение временных коллекций и повторных запросов следует [Unity GC best practices](https://docs.unity3d.com/cn/2022.3/Manual/performance-garbage-collection-best-practices.html). Для Heal устранить создание SkillTarget на каждую цель, повторное чтение общего N и глобальную сортировку кандидатов. Отсутствие аллокаций во всём pipeline и численное ускорение не заявляются без измерений.

## Статус и проверка

Изменения этого уточнения внесены в код и assets:

- Общие SkillEffectFormulaData/TermData/Evaluator используются HostValue и Economy effects. Источники EconomyAmount и селекторы подготовлены при регистрации; значения и владельцы кешируются на одно действие. Target-формулы рассчитываются отдельно для каждой Entity.
- Numeric Economy effects передают RequiredRuntimeTags в исполнение, estimates и факты Orchestration. Последовательные Economy effects используют один commit; точные Tokens закрепляются существующими leases. При отказе временные outputs откатываются, leases освобождаются.
- `Board/Skills/IssueHeal.asset`: Root-владелец — участник; из его Board wallet списываются выбранные HealBoardBase Tokens и в его shared wallet выдаётся столько же Stack Heal. Навык закреплён за BoardHost; BoardBrain использует SkillDecompOpData.
- `Autobattle/Skills/Heal.asset`: один навык расходует −N и лечит каждую Entity на 10% × N × её Health attribute. Без целей расход остаётся. Локальная копия навыка в seed исполнителя отделена адресом кошелька от расходуемого общего ресурса.
- Оставлены один HealState, HealApplyOperator и HealApplicationObjective. Обновлены оба мозга, SkillExecutor, четыре Activity-варианта и installers. Удалены 15 дополнительных комплектов Heal и все 16 HealSelectionRecipe; рецепт появления HealBoardBase сохранён.
- Поиск целей использует упорядоченный состав Activity. Одноэлементные SkillTarget для расчёта по получателю больше не создаются. Старые имена формул в исходниках authoring и тестов заменены.

Unity создала `.meta` IssueHeal и нового общего файла при импорте. Импорт остановился на `OrchestrationPlanningFactRecordFactory.cs:49`: прежнее обращение `release.ReleaseId` не соответствует уже изменённому D11-контракту. В этом месте теперь используется согласованный `release.Scope`, содержащий domain, Objective, node, generation и fact key. Повторная компиляция не запускалась; успешная сборка не заявляется.

Полный план части 1 остаётся незавершённым. Buff/HeroSkill, оставшиеся замены D1–D13 и их authoring продолжаются по ReworkConnections. Тесты, включая диагностические, smoke, package suites и FullUnitChain, **не запускались**. Они разрешены только после полной реализации всего утверждённого плана и отдельного явного разрешения пользователя. Измерения производительности и выполнение игрового сценария ещё не проводились.
