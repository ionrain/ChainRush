# Unity Repository Template

*(Структура репозитория / папок + asmdef rules для чистого Core)*

Версия: 1.0\
Статус: Recommended (SHOULD), с обязательными ограничениями (MUST) из
Compliance Standard\
Связан с документами:\
- Game Systems Architecture Framework\
- Orchestration Architecture Charter\
- Architecture Compliance Standard\
- Architectural Fitness Functions Specification

------------------------------------------------------------------------

## 1. Цели шаблона (Goals)

Шаблон предназначен для:

-   строгого отделения Core (движково-агностичный код) от
    Unity-специфики
-   enforce-правил зависимостей через asmdef
-   удобной масштабируемости под несколько игр / модулей
-   поддержки CI архитектурными тестами

------------------------------------------------------------------------

# 2. Предлагаемая структура репозитория (Repository Layout)

Рекомендуемая структура (Assets + Packages, без зависимости Core от
Unity):

    /Assets
      /_Project
        /Bootstrap
        /Scenes
        /Settings
        /Addressables
        /Resources
        /StreamingAssets
        /ThirdParty

      /Game
        /Presentation
          /UI
          /VFX
          /Audio
          /Animation
          /DebugHUD
          /Presentation.asmdef

        /Integration
          /UnityAdapters
            /Input
            /Physics
            /Navigation
            /Time
            /Persistence
            /UnityAdapters.asmdef

          /UnityExecution
            /Executors
            /CommandBusUnity
            /UnityExecution.asmdef

        /Tests
          /EditMode
          /PlayMode
          /Architecture
          /Tests.asmdef

    /Packages
      /com.studio.core
        /Runtime
          /Core
            /Contracts
              /Core.Contracts.asmdef
            /World
              /Core.World.asmdef
            /Sensing
              /Core.Sensing.asmdef
            /Proposals
              /Core.Proposals.asmdef
            /Arbitration
              /Core.Arbitration.asmdef
            /Execution
              /Core.Execution.asmdef
            /Orchestration
              /Core.Orchestration.asmdef

          /Data
            /Schema
            /Configs
            /Core.Data.asmdef

          /Shared
            /Diagnostics
            /Telemetry
            /Core.Shared.asmdef

        /Tests
          /Core.Tests.asmdef

      /com.morboo.tools
        /Editor
          /ArchitectureValidation
            /AsmdefGraphChecker
            /ForbiddenReferencesChecker
            /Tools.ArchitectureValidation.asmdef

Пояснение: - Core размещён в Packages, чтобы подчеркнуть его
независимость от проекта. - Unity-специфика находится в Assets/Game. -
Presentation и Integration разделены отдельными asmdef.

------------------------------------------------------------------------

# 3. Правила asmdef (Assembly Definitions Rules)

## 3.1 Naming Convention

-   Core.\* --- движково-агностичный слой (MUST)
-   Game.\* --- проектный слой
-   Tools.\* --- инструменты и проверки
-   Presentation.\* --- UI/VFX/Audio/Animation

------------------------------------------------------------------------

## 3.2 Разделение по слоям (MUST)

Каждый слой Framework MUST иметь отдельный asmdef:

-   Core.Contracts
-   Core.World
-   Core.Sensing
-   Core.Proposals
-   Core.Arbitration
-   Core.Execution
-   Core.Orchestration
-   Core.Data (опционально отдельным блоком)
-   Core.Shared (утилиты, логирование, диагностика)

------------------------------------------------------------------------

# 4. Граф зависимостей asmdef (Allowed Dependencies)

Ниже --- рекомендуемый граф (ребро означает "может ссылаться на"):

## 4.1 Core

-   Core.Contracts -\> (нет)
-   Core.World -\> Core.Contracts
-   Core.Sensing -\> Core.World, Core.Contracts
-   Core.Proposals -\> Core.World, Core.Contracts
-   Core.Arbitration -\> Core.Proposals, Core.Contracts
-   Core.Execution -\> Core.World, Core.Contracts
-   Core.Orchestration -\> Core.Arbitration, Core.Execution,
    Core.Contracts

## 4.2 Data / Shared

-   Core.Data -\> Core.Contracts
-   Core.Shared -\> Core.Contracts

Core.\* MAY ссылаться на Core.Data и Core.Shared, если это не нарушает
Layer Model.

------------------------------------------------------------------------

# 5. Запрещённые зависимости (Forbidden Dependencies)

## 5.1 Core Isolation (MUST)

-   Core.\* MUST NOT ссылаться на UnityEngine
-   Core.\* MUST NOT ссылаться на Presentation.\*
-   Core.\* MUST NOT ссылаться на Game.Integration.\*

## 5.2 Presentation Isolation (MUST)

-   Presentation.\* MUST NOT ссылаться на Core.Execution
-   Presentation.\* MUST NOT ссылаться на ICommandBus (если он не
    разрешён контрактом)

Presentation SHOULD работать через: - WorldSnapshot - IDomainEvent -
PlayerIntent

## 5.3 Integration Isolation

-   Game.Integration.\* MAY ссылаться на Core.\*
-   Core.\* MUST NOT ссылаться на Game.Integration.\*

------------------------------------------------------------------------

# 6. Проектные сборки (Project Assemblies)

Рекомендуемые asmdef в Assets/Game:

-   Presentation.asmdef
-   UnityAdapters.asmdef
-   UnityExecution.asmdef

### 6.1 Presentation.asmdef

MAY зависеть от: - Core.Contracts - Core.World (через query/snapshot) -
Core.Shared (telemetry interfaces)

MUST NOT зависеть от: - Core.Execution (если Execution публикует события
напрямую) - Core.Orchestration (если UI не должен знать orchestration)

------------------------------------------------------------------------

### 6.2 UnityAdapters.asmdef

MAY зависеть от: - Core.Contracts - Core.World - Core.Sensing (если
адаптер поставляет факты)

Назначение: - адаптеры ввода/физики/навигации/времени - преобразование
Unity API в Core-абстракции

------------------------------------------------------------------------

### 6.3 UnityExecution.asmdef

MUST зависеть от: - Core.Execution - Core.Orchestration -
Core.Contracts - UnityAdapters (если нужно)

Назначение: - реализация ICommandBus в Unity - executors, которые
применяют команды к Unity runtime

------------------------------------------------------------------------

# 7. Тестовые сборки (Tests Assemblies)

## 7.1 Core.Tests.asmdef (Packages/com.studio.core/Tests)

MAY зависеть от: - Core.\*

MUST NOT зависеть от UnityEngine (по возможности).

------------------------------------------------------------------------

## 7.2 Tests.asmdef (Assets/Game/Tests)

-   EditMode тесты (архитектурные, контракты, asmdef граф)
-   PlayMode тесты (runtime инварианты, deterministic replay при
    необходимости)

------------------------------------------------------------------------

# 8. Автоматические архитектурные проверки (CI Gate)

Проект SHOULD включать Tools.ArchitectureValidation, реализующий:

-   проверку DAG asmdef
-   проверку forbidden references
-   проверку Layer Integrity (по правилам Fitness Functions)
-   проверку Public API snapshot (Core.Contracts)

CI MUST блокировать merge при нарушении MUST-правил из Compliance
Standard.

------------------------------------------------------------------------

# 9. Практические соглашения (Practical Conventions)

-   Все входы игрока идут как PlayerIntent (не команды)
-   Все изменения состояния --- только ICommand
-   Все события UI --- только IDomainEvent / snapshots
-   Конфиги и баланс --- в Core.Data с версионированием

------------------------------------------------------------------------

# 10. Минимальный стартовый набор модулей (MVP)

Для маленького проекта достаточно:

-   Core.Contracts
-   Core.World
-   Core.Proposals
-   Core.Arbitration
-   Core.Execution
-   Core.Orchestration
-   Presentation
-   UnityAdapters
-   UnityExecution

Sensing и Data могут быть добавлены по мере роста.

------------------------------------------------------------------------

# 11. Приложение: Рекомендуемые namespace prefixes

-   Studio.Core.\*
-   Studio.Core.World.\*
-   Studio.Core.Sensing.\*
-   Studio.Core.Proposals.\*
-   Studio.Core.Arbitration.\*
-   Studio.Core.Execution.\*
-   Studio.Core.Orchestration.\*
-   Game.Presentation.\*
-   Game.Integration.\*
-   Tools.ArchitectureValidation.\*
