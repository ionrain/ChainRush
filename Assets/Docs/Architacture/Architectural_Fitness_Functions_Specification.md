# Architectural Fitness Functions Specification

*(Спецификация архитектурных тестов для автоматической проверки
стандарта)*

------------------------------------------------------------------------

## 1. Назначение (Purpose)

Данный документ является частью:

> Game Systems Architecture Framework

Он формализует набор автоматических архитектурных проверок
(Architectural Fitness Functions), обеспечивающих соблюдение:

-   Layer Model
-   Core Contracts
-   Dependency Rules
-   Architectural Invariants
-   Orchestration Discipline

Все правила предназначены для автоматического исполнения в CI/CD.

------------------------------------------------------------------------

# 2. Категории архитектурных тестов

Архитектурные тесты подразделяются на:

1.  Dependency Rules Tests
2.  Layer Integrity Tests
3.  Orchestration Integrity Tests
4.  Core Contracts Stability Tests
5.  Presentation Isolation Tests
6.  Data-Driven Discipline Tests
7.  Versioning & Migration Tests
8.  Deterministic Mode Tests (опционально)

------------------------------------------------------------------------

# 3. Dependency Rules Tests

## ARCH.DEP.001 --- Directed Acyclic Graph

Граф зависимостей сборок (Assemblies) обязан быть ациклическим.

Проверяется: - отсутствие циклов между asmdef

Ошибка: - обнаружен цикл зависимостей

------------------------------------------------------------------------

## ARCH.DEP.002 --- Core Isolation

Core-сборки не должны зависеть от:

-   Integration Layer
-   Presentation Layer

Ошибка: - обнаружена запрещённая ссылка

------------------------------------------------------------------------

## ARCH.DEP.003 --- Domain Independence

Доменные системы не должны иметь прямых зависимостей друг от друга, если
это не разрешено через Core Contracts.

Ошибка: - обнаружена прямая cross-domain зависимость

------------------------------------------------------------------------

# 4. Layer Integrity Tests

## ARCH.LAYER.001 --- Sensing is Read-Only

Sensing Layer:

-   не создаёт ICommand
-   не вызывает ICommandBus
-   не изменяет состояние

------------------------------------------------------------------------

## ARCH.LAYER.002 --- Proposal Does Not Mutate State

Proposal Layer:

-   не изменяет состояние мира
-   не вызывает Execution Layer

------------------------------------------------------------------------

## ARCH.LAYER.003 --- Arbitration Does Not Execute

Arbitration Layer:

-   не вызывает ICommandBus
-   не выполняет команды
-   возвращает только Decision

------------------------------------------------------------------------

## ARCH.LAYER.004 --- Execution Does Not Decide

Execution Layer:

-   не содержит логики выбора
-   не использует scoring или priority-политику

------------------------------------------------------------------------

# 5. Orchestration Integrity Tests

## ARCH.ORCH.001 --- Command Pipeline Only

Все изменения состояния проходят исключительно через Command pipeline.

Нарушением считается: - прямой вызов изменения состояния вне Execution
Layer

------------------------------------------------------------------------

## ARCH.ORCH.002 --- No Arbitration Bypass

Ни одна система не может обойти Arbitration Layer.

------------------------------------------------------------------------

## ARCH.ORCH.003 --- Orchestrator Is Domain-Agnostic

Orchestration Layer:

-   не зависит от конкретных игровых типов
-   не знает о Unit, Enemy, Skill и др.

------------------------------------------------------------------------

## ARCH.ORCH.004 --- Event Emission Discipline

IDomainEvent публикуются только через Execution Layer.

------------------------------------------------------------------------

# 6. Core Contracts Stability Tests

## ARCH.CONTRACT.001 --- Public API Stability

Публичные интерфейсы Core Contracts:

-   не изменяются без версии
-   фиксируются snapshot-механизмом

------------------------------------------------------------------------

## ARCH.CONTRACT.002 --- No Engine Leakage

Core-сборки не содержат зависимостей от:

-   UnityEngine
-   MonoBehaviour
-   Transform
-   GameObject

------------------------------------------------------------------------

# 7. Presentation Isolation Tests

## ARCH.UI.001 --- UI Does Not Execute Commands

Presentation Layer:

-   не создаёт ICommand
-   не вызывает ICommandBus

------------------------------------------------------------------------

## ARCH.UI.002 --- UI Produces Only Player Intent

UI может генерировать только:

-   PlayerIntent
-   Proposal

------------------------------------------------------------------------

# 8. Data-Driven Discipline Tests

## ARCH.DATA.001 --- No Magic Numbers

Балансные значения не должны быть зашиты в коде.

Допускаются только:

-   0
-   1
-   -1

Остальные значения должны приходить из конфигурации.

------------------------------------------------------------------------

## ARCH.DATA.002 --- ContentId-Only Access

Генераторы (Director, Loot):

-   используют только ContentId
-   не зависят от конкретных Prefab или игровых типов

------------------------------------------------------------------------

## ARCH.DATA.003 --- Schema Versioning Required

Все сериализуемые состояния:

-   обязаны иметь SchemaVersion

------------------------------------------------------------------------

# 9. Versioning & Migration Tests

## ARCH.VERSION.001 --- Migration Compatibility

Сейвы предыдущих версий:

-   должны успешно загружаться
-   проходить миграцию

------------------------------------------------------------------------

## ARCH.VERSION.002 --- Serialization Stability

Save → Load → Save должен давать эквивалентный результат.

------------------------------------------------------------------------

# 10. Deterministic Mode Tests (Optional)

## ARCH.DET.001 --- Deterministic Replay

При одинаковом seed и одинаковых входных данных:

-   состояние мира должно совпадать

------------------------------------------------------------------------

## ARCH.DET.002 --- No Non-Deterministic APIs

Core не должен использовать:

-   DateTime.Now
-   Guid.NewGuid()
-   Random без seed

------------------------------------------------------------------------

# 11. Требования к CI

Каждый архитектурный тест обязан:

-   иметь уникальный Rule ID
-   выводить Assembly / файл / строку нарушения
-   ссылаться на раздел Framework
-   блокировать merge при нарушении

------------------------------------------------------------------------

# 12. Иерархия документов

1.  Game Systems Architecture Framework
2.  Orchestration Architecture Charter
3.  Architectural Fitness Functions Specification
4.  Domain System Specifications
5.  Project-Specific Extensions

------------------------------------------------------------------------

Документ предназначен для использования в автоматических проверках CI/CD
и является обязательным к соблюдению для всех проектов.
