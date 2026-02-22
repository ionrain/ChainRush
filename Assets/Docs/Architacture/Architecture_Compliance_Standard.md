# Architecture Compliance Standard

*(Стандарт архитектурного соответствия --- MUST / SHOULD / MAY)*

------------------------------------------------------------------------

## 1. Назначение (Purpose)

Данный документ является обязательным нормативным расширением:

> Game Systems Architecture Framework\
> Orchestration Architecture Charter

Он определяет требования соответствия архитектуре (Architecture
Compliance), которые автоматически проверяются через Architectural
Fitness Functions.

В документе используются уровни обязательности:

-   MUST --- обязательное требование (нарушение блокирует CI)
-   SHOULD --- рекомендуемое требование (нарушение требует обоснования)
-   MAY --- допустимая практика

------------------------------------------------------------------------

# 2. Область действия (Scope)

Стандарт распространяется на:

-   Core
-   Domain Systems
-   Orchestration Platform
-   Integration Layer
-   Presentation Layer
-   Data / Content
-   Save / Load
-   Multiplayer (если применимо)

------------------------------------------------------------------------

# 3. Dependency Compliance

## 3.1 Directed Acyclic Graph

-   Архитектура сборок MUST быть ациклической.
-   Любой цикл зависимостей MUST приводить к ошибке CI.

------------------------------------------------------------------------

## 3.2 Core Isolation

-   Core MUST не зависеть от Integration Layer.
-   Core MUST не зависеть от Presentation Layer.
-   Core MUST не ссылаться на UnityEngine.

------------------------------------------------------------------------

## 3.3 Domain Independence

-   Доменные системы SHOULD не зависеть друг от друга напрямую.
-   Если зависимость необходима, она MUST проходить через Core
    Contracts.

------------------------------------------------------------------------

## 3.4 Package / Project Boundary (Morboo Layering)

-   `com.morboo.*` packages MUST NOT зависеть от project-layer assemblies
    (`Game.Runtime`, `Morboo.Bridge`, `Integration.Project`).
-   Project glue / scene wiring / legacy adaptation MUST находиться в
    `Assets/Scripts/MorbooBridge` (или эквивалентном project bridge
    слое), а не в package runtime слоях.
-   Migration-only переходные формы (legacy keys, compat enums, shim DTOs)
    MUST быть изолированы в `MorbooBridge` и MUST NOT протекать в
    `Framework/Core/RuntimeHost/Systems`.

------------------------------------------------------------------------

## 3.5 Inter-System Communication Discipline

-   Системы MUST NOT общаться direct concrete-to-concrete runtime calls.
-   Межсистемное взаимодействие MUST проходить через:
    -   contracts / interfaces
    -   commands / events / queries
    -   bridge/adapters (на project boundary)
-   Любой direct call bypass (временный) SHOULD иметь ADR + план удаления.

------------------------------------------------------------------------

## 3.6 Typed Dependency References

-   Нетипизированные dependency-holder ссылки (`GameObject`,
    `MonoBehaviour`, `Component` как service locator input) MUST NOT
    использоваться в новом runtime architecture code.
-   Если цель — доступ к сервису/контракту, MUST использоваться typed
    dependency (интерфейс, typed provider, explicit adapter).
-   `GameObject`/`Transform` ссылки MAY использоваться только как
    view/content references (prefab roots, UI nodes, anchors), но не как
    способ runtime service resolution.

------------------------------------------------------------------------

# 4. Layer Compliance

## 4.1 Sensing Layer

Sensing Layer:

-   MUST быть read-only.
-   MUST NOT создавать ICommand.
-   MUST NOT изменять состояние.

------------------------------------------------------------------------

## 4.2 Proposal Layer

Proposal Layer:

-   MUST генерировать только Proposal.
-   MUST NOT изменять состояние.
-   MUST NOT вызывать Execution Layer.

------------------------------------------------------------------------

## 4.3 Arbitration Layer

Arbitration Layer:

-   MUST возвращать только Decision.
-   MUST NOT выполнять команды.
-   MUST NOT иметь side effects.
-   MUST NOT содержать domain-name specific branching в основной
    arbitration loop / selection logic (например, `if Domain == Combat`).
-   Если временно требуется domain-specific классификация во время
    миграции, она MUST быть:
    -   локализована в одном explicit classifier seam (helper/policy),
    -   помечена как transitional,
    -   покрыта allowlist/architecture test,
    -   иметь зафиксированный план замены на metadata/policy-driven
        классификацию.

------------------------------------------------------------------------

## 4.4 Execution Layer

Execution Layer:

-   MUST преобразовывать Decision в ICommand.
-   MUST NOT принимать решения.
-   SHOULD быть максимально детерминированным.

------------------------------------------------------------------------

## 4.5 System Module Placement (Inside Layer)

-   Артефакты, используемые только одной системой, MUST лежать внутри
    папки этой системы.
-   Артефакты, используемые несколькими системами на одном слое, SHOULD
    выноситься в `Common` (или эквивалентный shared module) с явным
    owner/назначением.
-   Перед добавлением нового системного модуля MUST быть выполнена
    проверка на существующую абстракцию/контракт/паттерн для reuse.
-   Дублирование логики между системами SHOULD устраняться до третьей
    копии через общий контракт/модуль/data-driven выражение различий.

------------------------------------------------------------------------

# 5. Orchestration Compliance

## 5.1 Command Pipeline

-   Все изменения состояния MUST проходить через Command pipeline.
-   Прямое изменение состояния MUST считаться критической ошибкой.

------------------------------------------------------------------------

## 5.2 Arbitration Discipline

-   Ни одна система MUST NOT обходить Arbitration Layer.
-   Все действия MUST иметь источник Proposal.

------------------------------------------------------------------------

## 5.3 Orchestrator Isolation

Оркестрация:

-   MUST быть доменно-агностичной.
-   MUST NOT зависеть от игровых типов.
-   MUST NOT хранить состояние мира.
-   RuntimeHost orchestration wiring (`Arbiter/Loop/Router`) MUST NOT
    расширяться через ручные domain-name ветвления при добавлении нового
    домена; расширение должно происходить через registration/contracts.
-   Любой временный exception (migration seam) MUST быть явно
    задокументирован в backlog/ADR и ограничен allowlist-ом.

------------------------------------------------------------------------

# 6. Core Contracts Compliance

## 6.1 API Stability

-   Core Contracts MUST быть стабильными.
-   Любое изменение публичного API MUST сопровождаться версией.

------------------------------------------------------------------------

## 6.2 Engine Leakage

-   Core MUST NOT использовать MonoBehaviour.
-   Core MUST NOT использовать Transform или GameObject.
-   Core MAY использовать платформенные абстракции через интерфейсы.

------------------------------------------------------------------------

## 6.3 Canonical Representation & Source-of-Truth

-   Один факт/атрибут в одном contract surface (interface/struct/public
    DTO) MUST иметь одну каноническую форму представления.
-   Дублирование одной и той же информации в разных формах внутри одного
    contract surface (например `Float2` + `Float3` для одной позиции /
    якоря) MUST NOT использоваться как постоянное решение.
-   Временная dual-representation форма MAY использоваться только как
    migration compatibility и MUST:
    -   иметь explicit allowlist
    -   иметь план удаления / phase target
    -   не расширяться без отдельного решения
-   Внутренние hot-path caches MAY хранить несколько представлений одного
    факта (например 2D+3D) для performance, но MUST NOT утекать в public
    package contracts без явного решения.
-   Source-of-truth для состояния MUST быть единственным в рамках
    migrated path; read/write обходы SHOULD считаться архитектурным
    нарушением.

------------------------------------------------------------------------

## 6.4 Transitional Form Isolation

-   Любые transition-only формы (legacy trait keys, compat fields, bridge
    DTO) MUST быть локализованы в boundary/adaptation слое.
-   Пакетные слои MUST NOT становиться местом хранения временной
    совместимости "по привычке".
-   Если transition форма появляется выше `MorbooBridge`, MUST быть
    добавлено явное нарушение/исключение в backlog и тестах с дедлайном
    удаления.

------------------------------------------------------------------------

# 7. Presentation Compliance

-   UI MUST NOT создавать ICommand.
-   UI MUST NOT изменять состояние.
-   UI SHOULD генерировать только PlayerIntent.

------------------------------------------------------------------------

# 8. Data & Content Compliance

## 8.1 Data-Driven

-   Балансные значения MUST храниться в конфигурации.
-   Magic numbers SHOULD быть запрещены.

------------------------------------------------------------------------

## 8.2 ContentId Discipline

-   Генераторы MUST использовать ContentId.
-   Прямые ссылки на Prefab MUST NOT использоваться в Core.

------------------------------------------------------------------------

## 8.3 Schema Versioning

-   Все сериализуемые состояния MUST иметь SchemaVersion.
-   Миграции MUST быть реализованы для несовместимых изменений.

------------------------------------------------------------------------

## 8.4 Data-Driven-First Variability

-   Вариативность фич/доменов SHOULD сначала выражаться данными
    (policies/maps/config/content ids), а не новыми кодовыми ветками.
-   Новый кодовый путь MAY добавляться только если data-driven выражение
    различий недостаточно/нецелесообразно (должно быть кратко обосновано).
-   При onboarding нового домена/системы MUST быть проверено, какие
    различия можно выразить через существующие абстракции и данные.

------------------------------------------------------------------------

## 8.5 File-Sprawl / Onboarding Fan-Out Control

-   Добавление новой системы/домена SHOULD иметь компактную структуру и
    контролируемый fan-out.
-   "Файловый взрыв" (много мелких классов/интерфейсов/политик без
    достаточной data-driven модели) SHOULD рассматриваться как smell и
    требовать redesign-review.
-   Для новых систем SHOULD фиксироваться onboarding budget / fan-out
    метрики (минимум на уровне PR checklist или phase backlog).

------------------------------------------------------------------------

## 8.6 Odin / Editor Data Authoring Policy

-   `Sirenix.Odin` MAY использоваться для UnityEditor/data authoring
    workflows.
-   `Sirenix.Odin` MUST NOT становиться required runtime dependency для
    `framework/systems/core/runtimehost` слоёв.
-   Использование Odin в package runtime слоях SHOULD считаться
    нарушением, если нет явного approved exception.

------------------------------------------------------------------------

# 9. Save / Load Compliance

-   Загрузка старых версий MUST поддерживаться.
-   Save → Load → Save SHOULD быть эквивалентным.

------------------------------------------------------------------------

# 10. Determinism Compliance (если применимо)

-   Deterministic mode MUST обеспечивать одинаковый результат при
    одинаковом seed.
-   Core MUST NOT использовать недетерминированные API.

------------------------------------------------------------------------

# 11. CI Enforcement

Каждое правило MUST иметь:

-   уникальный Rule ID
-   автоматическую проверку
-   отчёт о нарушении

CI MUST блокировать merge при нарушении MUST-правил.

Дополнительно:

-   Финальное закрытие задачи MUST включать semantic review (чеклист в PR):
    проверка смысла размещения по слоям, source-of-truth и недопущение
    migration-only утечек выше MorbooBridge.
-   PR/commit на новый функционал SHOULD фиксировать:
    -   выбранный слой и причину выбора по уровню переиспользования
    -   owner source-of-truth
    -   почему существующие контракты/паттерны не подошли (если добавлен
        новый)
    -   что выражено data-driven, а что потребовало новый код
-   Новые architecture exceptions MUST сопровождаться сроком/фазой
    удаления и тестовым/документным следом (allowlist / ADR / backlog).
-   Для orchestration host-runtime SHOULD существовать отдельный
    future-gate/architecture test на domain-name specific branching
    (`Combat/Idle/...`) вне allowlisted transitional seams.

------------------------------------------------------------------------

# 12. Эволюция стандарта

-   Изменения стандарта MUST проходить ревью.
-   Изменения MUST быть обратно совместимы либо сопровождаться планом
    миграции.

------------------------------------------------------------------------

# 13. Иерархия документов

1.  Game Systems Architecture Framework
2.  Orchestration Architecture Charter
3.  Architecture Compliance Standard
4.  Domain System Specifications
5.  Project-Specific Extensions

------------------------------------------------------------------------

Документ обязателен для всех игровых проектов и подлежит автоматической
проверке.
