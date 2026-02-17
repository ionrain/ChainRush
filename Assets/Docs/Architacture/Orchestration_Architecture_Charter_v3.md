# Orchestration Architecture Charter

*(Спецификация платформы принятия решений --- уровень Core Platform)*

Версия: 3.0\
Статус: Normative\
Связан с документами:\
- Game Systems Architecture Framework\
- Architecture Compliance Standard

------------------------------------------------------------------------

# 1. Назначение и уровень ответственности (Purpose & Level of Responsibility)

Данный документ описывает **Платформу принятия решений (Decision &
Orchestration Platform)** как компонент уровня **Core Platform**, а не
как фундамент всей архитектуры.

Оркестрация:

-   реализует координацию Proposal → Arbitration → Execution,
-   не определяет Layer Model (он задаётся Framework),
-   не определяет Dependency Rules (они задаются Compliance Standard),
-   не содержит доменных правил.

Оркестрация является инфраструктурным механизмом, а не источником
игровой логики.

------------------------------------------------------------------------

# 2. Архитектурный контекст (Architectural Context)

Оркестрация функционирует внутри следующих границ:

## 2.1 Зависимости (Allowed Dependencies)

Оркестрация MAY зависеть от:

-   Core Contracts
-   World Model (только через IWorldQuery / WorldSnapshot)
-   Execution abstractions

Оркестрация MUST NOT зависеть от:

-   Domain Systems
-   Integration Layer
-   Presentation Layer
-   Конкретных игровых типов

------------------------------------------------------------------------

# 3. Границы ответственности (Responsibility Boundaries)

Оркестрация отвечает за:

1.  Координацию Decision Loop
2.  Управление Scheduler
3.  Вызов Arbitration
4.  Дисциплину Command pipeline
5.  Интеграцию middleware (логирование, профилирование)

Оркестрация НЕ отвечает за:

-   Боевые формулы
-   Выбор целей
-   Экономические правила
-   Спавн-алгоритмы
-   UI-логику

------------------------------------------------------------------------

# 4. Decision Loop Specification

## 4.1 Стандартный цикл (Standard Decision Loop)

1.  Получение WorldSnapshot
2.  Сбор Proposal от всех зарегистрированных IProposalSource
3.  Передача списка Proposal в IArbiter
4.  Получение Decision
5.  Передача Decision в Execution Layer
6.  Генерация ICommand
7.  Публикация IDomainEvent

Этот цикл MUST быть единственным способом изменения состояния через
оркестрацию.

------------------------------------------------------------------------

# 5. Scheduler Abstraction

Оркестрация MUST поддерживать абстрактный Scheduler:

-   Realtime Scheduler
-   Turn-Based Scheduler
-   Event-Driven Scheduler
-   Deterministic Scheduler

Scheduler является политикой выполнения, а не частью доменной логики.

------------------------------------------------------------------------

# 6. Контракты уровня платформы (Platform-Level Contracts)

## 6.1 IProposalSource

MUST: - быть side-effect free - работать только на Snapshot - не
обращаться к ICommandBus

------------------------------------------------------------------------

## 6.2 IArbiter

MUST: - принимать только список Proposal - возвращать Decision - быть
pluggable

MUST NOT: - выполнять команды - обращаться к Integration

------------------------------------------------------------------------

## 6.3 Execution Boundary

Execution Layer:

-   MUST быть единственной точкой применения ICommand
-   MUST публиковать IDomainEvent
-   MUST NOT выполнять Arbitration

------------------------------------------------------------------------

# 7. Middleware & Extensibility

Оркестрация MAY поддерживать middleware:

-   Logging
-   Telemetry
-   Profiling
-   Metrics
-   Debug Visualization

Middleware MUST NOT:

-   изменять Decision
-   мутировать состояние

------------------------------------------------------------------------

# 8. Инварианты оркестрации (Orchestration Invariants)

1.  Единственный Decision Loop.
2.  Невозможность обхода Arbitration.
3.  Отсутствие доменных зависимостей.
4.  Отсутствие состояния мира внутри оркестрации.
5.  Совместимость с Core Contracts.

------------------------------------------------------------------------

# 9. Взаимодействие с Domain Systems

Domain Systems:

-   предоставляют IProposalSource
-   реализуют IExecutor (если необходимо)
-   не знают о внутреннем устройстве оркестрации

Оркестрация не знает о конкретных доменных системах.

------------------------------------------------------------------------

# 10. Интеграция с Framework и Compliance

Оркестрация MUST соответствовать:

-   Layer Model (Framework)
-   Dependency Rules (Compliance Standard)
-   Architectural Invariants

Любые изменения оркестрации MUST проверяться Architectural Fitness
Functions.

------------------------------------------------------------------------

# 11. Версионирование

Изменения в оркестрации:

-   MUST быть обратно совместимы по Core Contracts
-   MUST не требовать переписывания Domain Systems
-   MUST сопровождаться обновлением версии документа

------------------------------------------------------------------------

# 12. Позиционирование в иерархии документов

1.  Game Systems Architecture Framework\
2.  Architecture Compliance Standard\
3.  Orchestration Architecture Charter\
4.  Domain System Specifications\
5.  Project Extensions

------------------------------------------------------------------------

Оркестрация является инфраструктурной платформой принятия решений и не
определяет правила игры --- она лишь обеспечивает их корректное
исполнение.
