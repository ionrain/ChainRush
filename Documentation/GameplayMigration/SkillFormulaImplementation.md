# Единая формула числовых эффектов Skills

**Implementation decision: working model.** Реализация согласованного контракта внесена 2026-09-29. Компиляция, тесты и игровые запуски не выполнялись. Тесты разрешены только после отдельного явного согласия пользователя.

## Existing System Fit

| Владелец | Что выполняет |
|---|---|
| SkillNumericEffectData / SkillEffectFormulaData | Единственное authored-выражение числового эффекта |
| InMemorySkillRuntime | Проверяет и подготавливает формулы при регистрации, хранит их до ClearDefinitions; сохраняет lifecycle исполнения |
| SkillEffectFormulaEvaluator | Исполняет подготовленное выражение; знает арифметику, точность и источники значений |
| SkillValueResolver | Разрешает Entity и кошельки; сохраняет снимок ресурсов на действие и конечный результат на эффект/целевую Entity |
| Существующие HostValue, Economy, AttributeModifier и Move executors | Используют один конечный результат при оценке и применении |
| KnowledgeCapabilityHostSourceHandler | Продолжает получать оценку и leases наблюдения через Skills; отдельного вычислителя нет |
| LongProgressionData и Economy | Сохраняют собственные вычисление прогрессий, чтение, подписки и операции |

SpawnCarrier, EconomyEntry и RuntimeTag остаются нечисловыми эффектами. Производство, выбор целей, AIBrain, приём заказов и игровой lifecycle этой правкой не меняются. Новых сервисов и исполнителей нет.

## Best Practices

Разбор authored-выражения отделён от исполнения: `SkillEffectFormulaEvaluator.Prepare` создаёт внутренние узлы один раз при регистрации, аналогично уже существующей подготовке селекторов в `SkillEconomyFormulaQuery`. Это убирает разбор сериализуемых типов и построение ключей из каждого действия.

Оценка и применение используют `SkillValueResolver.TryResolveEffectValue`; предварительный проход `InMemorySkillRuntime.TryApplyAction` сохраняет снимок до мутаций. Поэтому потребление Stack первым эффектом не обнуляет величину Heal следующего эффекта. Это сохранение существующего контракта действия, а не отдельная транзакционная модель.

Каждый inline-узел принадлежит только одному месту authored-выражения. Ссылки на игровые definitions остаются внешними asset-ссылками. Общие inline-объекты и циклы отклоняются при подготовке формул; правило 20 PROJECT_RULES.md сохраняется.

## Сериализуемый контракт

Общие поля `SkillEffectData`: `recipient`, `repeatType`, `requiredTags`. Числовые эффекты наследуют `SkillNumericEffectData`, который добавляет только `formula`. В `SkillEffectFormulaData` — только `[SerializeReference] root`.

| Узел | Все сериализуемые поля |
|---|---|
| Progression | `[SerializeReference] progression`, `[SerializeReference] argument`, `precision` |
| Attribute | `parameterTarget`, `attribute`, `allowMissingAttribute`, `missingAttributeValue` |
| HostValue | `parameterTarget`, `hostValue` |
| Economy | `parameterTarget`, `ownerType`, `selection` |
| Binary | `operation`, `[SerializeReference] left`, `[SerializeReference] right` |
| Conditional | `comparison`, `[SerializeReference] left`, `[SerializeReference] right`, `[SerializeReference] whenTrue`, `[SerializeReference] whenFalse` |

Остальные поля таблицы используют SerializeField. Абстрактный SkillEffectFormulaNodeData полей не содержит. Существующие AttributeSelectorData, EconomyEntrySelectionData и структуры LongProgressionData не изменены.

Удалены внешние `value`, `valueArgument`, `multiplier`; SkillEffectFormulaTermData, SkillEffectValueSourceData и SkillEffectFormulaOperandType; раздельные расчёты base/delta. Автоматических преобразований, совместимого чтения прежних полей и исправлений в OnValidate нет. Assets переписаны явно. Новые `.meta` создаст Unity при импорте; вручную они не создавались.

## Вычисление

Каждый узел возвращает raw-значение и точность. Константа — LongFlatProgressionData без аргумента. Остальным прогрессиям нужен явный аргумент; он передаётся с учётом своей точности. Округление LongProgressionData сохранено.

- Add, Subtract, Minimum и Maximum приводят правый операнд к точности левого с отсечением дробной raw-части к нулю.
- Multiply: `left.raw × right.raw / 10^right.precision`.
- Divide: `left.raw × 10^right.precision / right.raw`. При нулевом делителе возвращается левый операнд, как согласовано.
- Результат Binary имеет точность левого операнда. Промежуточное переполнение означает неуспешный расчёт.
- Conditional сравнивает численные значения с учётом обеих точностей и вычисляет только выбранную ветвь. Результат сохраняет её точность.
- Корень приводится к точности адресата: HostValue, Stack asset, Attribute либо 0 для Token и бюджета движения.
- Отсутствующий обязательный источник и ошибка прогрессии возвращают неуспешный расчёт. Для Knowledge это недоступное значение, а не ноль.

Неявного модуля, восстановления знака, общего ограничения минимумом или скрытого множителя больше нет. Наблюдение ресурсов охватывает обе ветви Conditional; освобождение подписок остаётся у прежних владельцев.

## Контент

Переписаны **84 assets ChainRush и 9 assets MorbooFramework**: 107 числовых эффектов (27 Move, 38 HostValue, 42 Economy). У 23 нечисловых эффектов удалены лишние числовые поля. Параметры carrier, геометрия, требования, задержки, цели, таблицы прогрессии и ссылки на внешний контент сохранены.

Обозначение `C(raw, precision)` означает Flat-прогрессию. Повторение выражения в записи ниже означает отдельное authored-поддерево, без общей managed-reference идентичности.

| Использование | Выражение |
|---|---|
| Постоянный эффект | `C(raw, точность адресата)` |
| Движение с масштабом скорости | `C(бюджет, 0) × Attribute(Owner, Speed)` |
| Урон | `D = C(база, 3) × Power(Owner)`. Если `D ≤ 0`, результат 0. Иначе при положительной Defense результат `−max(D − Defense, C(1000, 3))`; без положительной Defense — `−D` |
| Жизнь carrier | `C(-1, 0)` |
| Потребление выбранных клеток или накопленного Stack | При `N > 0` — `C(-1, 0) × N`, иначе 0 |
| Выпуск Heal с доски | При `N > 0` — `C(1, 0) × N`, иначе 0 |
| Heal в Autobattle | При `N > 0` — `C(100, 3) × N × Health(Target)`, иначе 0 |
| Выпуск баффа с доски | Progression с аргументом количества выбранных Token; прежняя таблица `2/5/10/15/20/25/30` |
| Применение баффа | При `B > 0` — `C(1, 2) × B`, иначе 0; raw 10 остаётся бонусом 10% |
| Выбор формы LightningBolt | Прежние шесть прогрессий интервалов с аргументом количества выбранных клеток |
| Потребление LightningBolt | `C(-1, 0)` соответствующего Stack |
| MineGold / DepositGold | Прежние постоянные количества с прежними получателями |
| Ресурс Distance в демонстрационных move skills | `C(1, 0) × MovementSpeed(Owner)` |

Gold доски остаётся рецептом Production; Skills его не вычисляет. Health для Heal вычисляется отдельно для каждой Entity. Потребление ресурса выполняется один раз, в том числе при разрешённом исполнении без целей; целевой эффект без целей пропускается.

Команды ChainRushAutobattleVerticalSliceAuthoring и Content создают новый контракт. Старые fixtures обновлены; добавлены сценарии арифметики и точности, условного чтения, прогрессии, ошибок вычисления, недопустимых деревьев, Heal нескольким целям/без целей и подписок ресурсов.

## Состояние проверки

Выполнено только статическое чтение исходников, diff и сериализованных ссылок. В 93 переписанных assets нет отсутствующих managed-reference ID, общих inline-записей и удалённых внешних полей эффекта. Эти проверки не подтверждают компиляцию, импорт Unity или игровое исполнение.

Тесты подготовлены, но не запускались. Проверка компиляции и поведения остаётся до разрешённого пользователем запуска.
