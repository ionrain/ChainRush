# Точность derived attributes и процентные баффы

Статус: **согласовано пользователем; общий числовой контракт и игровые баффы подключены в коде и assets**. Implementation decision: **working model**. Оба проекта компилируются; тесты не запускались. Статус всего плана ведётся в [ImplementationStatus.md](ImplementationStatus.md).

Это уточнение D4/D9. Оно заменяет прежнее предложение raw-умножения с повышением precision результата до p + q. Полный план части 1 остаётся в работе; тесты не разрешены.

## Existing System Fit

Проверено чтением кода:

- `EconomyAssetData.Precision`, унаследованный `AttributeData`, задаёт количество десятичных знаков: физическое значение = raw / 10^precision. `AttributeValueType.Ratio` уже позволяет описать коэффициент атрибутом.
- `DerivedAttributeScaleData` уже выбирает Constant либо AttributeSelector. Сейчас у константы есть только long constant; у выбранного атрибута точность уже существует в его definition.
- `DerivedAttributeRegistryService` регистрирует правила, запрещает несколько правил с одинаковым target и сортирует зависимости. Ссылка правила на собственный target исключается из межправильных зависимостей.
- `SkillHostProjectionCore.BuildEffectiveAttributes` копирует значения из projection, применяет временные modifiers, затем derived rules. `ResolveRuleDelta` сейчас суммирует baseConstant + sourceRaw × scaleRaw, игнорируя точность операндов. Вклад целого правила вычисляется до записи его target. Итоговые derived attributes не записываются обратно в исходные wallets.
- `CapabilityHostSkillController` — единственный найденный production caller BuildEffectiveAttributes. Он собирает local/nested и external Attribute sources, наблюдает их wallets, пересчитывает effective attributes и передаёт их HostValues. Watches и cleanup остаются у него.
- Skills уже учитывает точность при чтении Attribute/HostValue/EconomyAmount в `SkillEffectFormulaEvaluator`. Постоянный бафф из D4 выдаётся Economy effect в кошелёк участника; существующий временный `SkillAttributeModifierEffectExecutor` остаётся отдельным источником той же проекции.

Новых владельцев баффов, новых игровых классов и отдельной арифметики для ChainRush не добавлять. Economy хранит числовые записи, derived projection рассчитывает итог, HostValues владеет изменением current/max Health.

## Согласованный контракт

1. **Оставить** единственным источником точности атрибута `AttributeData.Precision`. Точность результата брать из target.Attribute.Precision; value source и коэффициента-атрибута — из соответствующих definitions. Поля переопределения этих значений на Buff или на ссылке-селекторе не вводить.
2. **Добавить** `constantPrecision` в существующий `DerivedAttributeScaleData`, только для режима Constant. Значение по умолчанию 0: constant 1 означает ×1. Например, constant 125 и constantPrecision 2 означают ×1,25. Для AttributeSelector поле не участвует в расчёте: точность принадлежит выбранному атрибуту. Это единственное новое authored поле.
3. **Переписать** вычисление каждого term: сначала умножить сырые значения, затем привести произведение из точности sourcePrecision + scalePrecision в targetPrecision. Когда точности совпадают, пересчёт равен нынешнему умножению. При уменьшении точности отбросить остаток к нулю после умножения; при увеличении — домножить на степень 10. baseConstant уже выражен в raw-единицах target и сохраняет этот смысл.
4. **Сохранить** additive rule: к исходному значению target прибавляется вклад правила. Внутри одного правила значения источников читаются до записи результата target. Между правилами сохраняется существующий порядок зависимостей.
5. **Проверять** точность участвующих definitions и constantPrecision при регистрации: поддерживаемая точность long — 0..18. Проверять enum, ссылки и возможность вычислить масштаб. Переполнение промежуточного long-произведения или конечной суммы остаётся явной ошибкой, как при нынешнем checked-расчёте; не подменять результат нулём, насыщением или float. Это не обещание представимости любого произведения двух long даже при последующем делении.
6. **Сохранить** нулевой вклад отсутствующего runtime-атрибута по существующей семантике проекции. Отсутствие накопленного бонуса даёт нулевую прибавку. Невалидная authored ссылка — ошибка конфигурации.

Общее выражение для одного слагаемого:

`termRaw = truncateTowardZero(sourceRaw × scaleRaw × 10^(targetPrecision − sourcePrecision − scalePrecision))`

Это математическая запись масштаба. Реализация выбирает целочисленное умножение либо деление; дробная степень или floating point в runtime не нужны.

## Как задать бафф в assets

- Сохранить текущий Power/Defense/Health/Speed/SkillSpeed с их точностью. Базовые параметры и развитие продолжают поступать в обычные источники этих атрибутов.
- Для накопленных процентных бонусов задать Attribute definitions с ValueType Ratio и, например, Precision 2. В таком кошельке raw 20 означает бонус 0,20, то есть +20%. Значения исходного контента авторить явно в выбранной точности.
- Навык исполнителя выдаёт бонус в общий Attribute wallet через уже согласованные Economy effect/formula и commit. External source D4 фильтрует применимость к host и передаёт бонус в его проекцию. Размер выдачи берётся из согласованных данных контента; этот документ не меняет соответствие длины цепочки величине баффа.
- В existing derived rule для Power: target = Power, baseConstant = 0, один term с valueSource = Power и scale = AttributeSelector(PowerBonus). То есть Power += Power × PowerBonus. Для остальных параметров использовать такое же правило с их selector. Бонусы суммируются в источнике до умножения.
- Не создавать отдельные BasePower/PowerMultiplier только ради процентной операции. Для SkillSpeed так же заменить предложенную в D9 цепочку дополнительных base/multiplier attributes одним вкладом SkillSpeed × SkillSpeedBonus. Согласованная формула baseRate × (1 + сумма бонусов) сохраняется.

Пример: Power raw 12500 при Precision 3 означает 12,5. PowerBonus raw 20 при Precision 2 означает 0,20. Вклад = 12500 × 20 / 100 = 2500; итог raw 15000, то есть 15. Два бонуса по 20 дают суммарный bonusRaw 40 и итог 17,5. Повторная проекция с теми же входами снова даёт 17,5, потому что вход восстанавливается из projection, а не из прошлого effective результата.

SkillSpeed raw 1000 при Precision 3 и bonusRaw 40 при Precision 2 дают raw 1400 при Precision 3: Rate 1,4. Численный эффект усилений и положительная длительность в Skills сохраняются. Current/max Health обновляется ранее согласованной политикой D4; точность Health и его потребителей ради баффа менять не требуется.

## Файлы и оптимизация

Реализовано в общем коде; игровые assets остаются в работе:

- `MorbooFrameworkPackage/Scripts/Core/Attributes/DerivedAttributeScaleData.cs` — constantPrecision и его свойство.
- `MorbooFrameworkPackage/Scripts/Core/Attributes/DerivedAttributeRegistryService.cs` — валидация числового контракта; изменить GetRules на возврат IReadOnlyList через одну read-only обёртку существующего упорядоченного списка, без копирования на каждый host. Сохранить существующие регистрацию, порядок и ClearRules.
- `MorbooFrameworkPackage/Scripts/Core/Skills/SkillHostProjectionCore.cs` — масштабирование произведения в точность target, существующая checked-арифметика и чтение правил без копии. Степени 10 брать из заранее подготовленной приватной таблицы; в пересчёте не строить таблицы, не создавать селекторы ради precision и не вычислять степени циклом.
- ChainRush: authored Attribute бонусов, их wallet/Skill выдачи, existing derived installer, external source bindings и D4/D9 в документах. Числовой контракт не добавляет классов в ChainRush.
- Соответствующие тестовые исходники обновить под общую семантику точности. Запускать только после полного плана и отдельного разрешения.

`DerivedAttributeTermData`, `DerivedAttributeRuleData`, Economy storage, `SkillAttributeModifierEffectExecutor`, AIBrain, Movement и lifecycle HostValues для этой арифметической правки не расширять. Формулы Heal уже используют точность operands; их контракт не меняется. Удаление игровых features и оставшийся authoring продолжаются по исходному плану.

В игровые assets явно записаны пять Bonus Attribute с precision 2, derived rules, installer и внешние wallet-источники. Power/Defense используют канонические definitions и Element qualifiers; развитие поступает через снимок запуска. Автоматические преобразования не добавлены. Соответствие поведения исходной игре ожидает разрешённого запуска.

## Best Practices

Умножение чисел с фиксированным десятичным масштабом требует сложить масштабы операндов и затем привести результат к масштабу получателя. Такое правило явно используется для decimal-умножения в [SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/data-types/precision-scale-and-length-transact-sql?view=sql-server-ver17). В проекте поле Precision означает именно десятичный масштаб, соответствующий SQL scale. Применяем правило интерпретации числа; тип хранения long и обработка переполнения остаются проектными.

Округление требуется зафиксировать, чтобы один и тот же отрицательный или дробный вклад не менялся между исполнителями. Используется отсечение к нулю, уже используемое integer division в [C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators#integer-division) и формулах Skills. Коэффициенты бонусов сначала суммируются как точные raw-значения, затем выполняется одно умножение, поэтому разбиение +40% на два источника +20% не добавляет промежуточного округления.

Пересчёт производных данных из authoritative sources вместо изменения предыдущего итога уже реализован в `SkillHostProjectionCore.BuildEffectiveAttributes:85–100`. Он предотвращает повторное начисление на refresh и позволяет существующим и новым hosts получить один результат. Вся арифметика остаётся у общего владельца проекции.

## Состояние реализации

Пользователь согласовал этот контракт указанием продолжать план с учётом обсуждения точности. Внесены constantPrecision, проверка выбранных definitions/scale при регистрации, одна read-only обёртка OrderedRules и целочисленное приведение произведения к precision target. При уменьшении масштаба на 19 и более знаков любое представимое long-произведение даёт 0; переполнение самого произведения по-прежнему выбрасывает ошибку до масштабирования.

Добавлены исходники проверок арифметики, отрицательного округления, переполнения, регистрации и повторной проекции без накопления результата. Проверки не запускались. Подключены пять Bonus Attribute, навыки выдачи и применения, derived installer и внешние источники wallet; удалены старый обработчик выбора Buff и игровое обслуживание Health. Развитие подключено через стартовый wallet, element selectors переведены на канонические definitions и qualifiers. В согласованном реестре нет пассивного Support skill: отдельный lifecycle поддержки не добавлялся. Это состояние подключения, а не результат игровых проверок; завершение всего плана не заявляется.
