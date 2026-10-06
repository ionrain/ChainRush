# Отделение HostValues от Skills: подключение ChainRush

**Implementation decision: working model.** 2026-10-02. Изменения кода и подключение профилей внесены. Компиляция, тесты и игровые прогоны этого этапа не запускались; приёмка остаётся отдельным действием после явного разрешения.

## Внесённые изменения

- Обычные CapabilityHosts получают HostValues независимо от SkillOwner; carriers используют тот же HostValueService.
- Источники атрибутов, derived rules и численные политики сохранены. Их рассчитывает AttributeProjectionCore; Skills отвечает только за навыки и эффекты.
- Runtime-профиль ChainRush устанавливает GameplayHostValuesInstaller перед GameplaySkillsInstaller. При завершении общий Foundation cleanup удаляет обычные hosts, Skills — carriers, затем освобождается остаточное состояние HostValues.
- Игровые определения Health, Lives и других значений, границы, seeds и параметры carrier сохранены. Новых переключателей в определениях игровых объектов нет.
- Существующие fixtures ChainRushActivityCompositionPlayModeTests, Effects, CombatLifecycle и LevelPopulation используют HostValueService. Они не запускались.

Подробные владельцы, API, порядок регистрации и cleanup описаны в package: `Documentation~/HostValues.md`. Документ доступен в Unity-проекте через `Packages/com.morboo.framework/Documentation~/HostValues.md`.

## Граница этапа

CapabilityHostAttributeSourceData сохранён. Исправление схемы прокачки, удаление этого класса и переход баффов Power/Defense/Speed/SkillSpeed на HostValues не входят в это разделение. Этот этап предоставляет для следующего плана общий механизм состояния.

Оригинальные игровые сцены и UI не изменены. Новые .meta создаёт Unity; существующие перемещённые файлы package сохранили .meta и GUID. Автоматические миграции не добавлены.

## Проверка после разрешения

Выполнить сценарии утверждённого плана: host без Skills; политики seed/границ/максимума; сохранение повреждений после пересчёта; снятие SkillOwner; эффекты по нескольким Entity; carrier Lives/cleanup; читатели значений; закрытие Activity и повторный запуск. Проверить отсутствие чтения кошельков и полной проекции при уроне и лечении. Пока ни один результат прогона этого этапа не заявляется.
