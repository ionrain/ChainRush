# New System Requirements Template

Date: 2026-02-20  
Status: Normative template (must be filled before implementation)

## 1) Purpose

Единый шаблон требований для создания новой системы без эмерджентной архитектуры, прямых связей между системами и file-sprawl.

Шаблон обязателен для любой новой системы или крупного рефактора существующей.

## 2) How To Use

1. Скопировать шаблон в отдельный документ: `Assets/Docs/Architacture/System_Blueprint_<SystemName>.md`.
2. Заполнить все секции до начала кодинга.
3. Приложить ссылку на заполненный blueprint в PR.
4. Если какая-то секция не применима, явно написать `N/A` с причиной.

## 3) System Blueprint (Fill-In Template)

## 3.1 System Passport

1. `System Name`:
2. `Owner`:
3. `Target Phase` (from `Master_Migration_Roadmap.md`):
4. `Scope Type`:
   - `new system`
   - `major refactor`
   - `extraction/modularization`
5. `Behavior Impact`:
   - `none`
   - `controlled`
   - `expected`

## 3.2 Problem / Outcome

1. `Problem Statement`:
2. `Business/Game Outcome`:
3. `In Scope`:
4. `Out of Scope`:

## 3.3 Architecture Archetype (Analogy)

Выбрать базовый архетип и указать отличия:

1. `Kernel Service` (flow/objective/outcome/rulebook style)
2. `Simulation Domain` (domain + policies + execution style)
3. `Runtime Platform Host` (loop/router/cache style)
4. `Integration Adapter` (bridge/adapters/content wiring style)

Заполнить:

1. `Selected Archetype`:
2. `Why this archetype`:
3. `What differs from reference`:

## 3.4 Layer & Package Placement

Разложить части системы по слоям:

1. `Any game abstractions/contracts` -> `Packages/com.morboo.framework`
2. `Any game runtime infra` -> `Packages/com.morboo.systems`
3. `Cross-genre kernel contracts/models` -> `Packages/com.morboo.core`
4. `Cross-genre host execution` -> `Packages/com.morboo.runtimehost`
5. `Genre-specific` -> `Packages/com.morboo.integration.strategycombat`
6. `Project-specific glue/content` -> `Assets/Scripts/MorbooBridge` + `Assets/Scripts/Game`

Заполнить таблицу (текстом):

1. `Component` -> `Target package` -> `Reason` -> `Reuse level`

## 3.5 Folder Topology (Inside Layer)

1. System-local code: `<Layer>/<SystemName>/...`
2. Shared code for 2+ systems only: `<Layer>/Common/...`
3. `Common` only after proven reuse with named consumers.

Заполнить:

1. `Planned folders`:
2. `Initial Common candidates`:
3. `Proof of multi-system reuse`:

## 3.6 Communication Contract (No Direct Concrete Coupling)

Разрешено:

1. commands/events via bus contracts
2. query/read via interfaces (`I*Query`/`I*Provider`)
3. explicit public API contracts

Запрещено:

1. direct concrete runtime calls from `SystemA` to `SystemB` internals
2. hidden shared mutable state across systems

Заполнить:

1. `Inbound commands/events/queries`:
2. `Outbound commands/events/queries`:
3. `Bridge points` (if any):
4. `Forbidden direct deps`:

## 3.6.1 Typed Reference Policy (No Untyped Scene Refs)

Правило:

1. Не использовать нетипизированные ссылки (`GameObject`/`MonoBehaviour`/`Component` как dependency holder), если цель — выйти на нужный интерфейс/сервис через `GetComponent`.
2. Для runtime-зависимостей использовать типизированные ссылки:
   - конкретный required component type,
   - typed provider interface/adapter,
   - explicit registration/composition.
3. Допускаются scene/view ссылки на `GameObject` только для чисто визуального контента (prefab roots/UI nodes), без роли service locator.

Заполнить:

1. `Typed dependency references used`:
2. `Untyped refs kept (if any) + justification`:
3. `Plan to remove untyped refs`:

## 3.7 Reuse & Common Extraction Audit

Перед созданием новых типов зафиксировать:

1. что можно переиспользовать из текущих систем
2. что нужно вынести в общий уровень до дублирования
3. что останется system-local и почему
4. какие существующие contracts/patterns/extension points были рассмотрены до добавления нового локального решения

Заполнить:

1. `Reused existing contracts/components`:
2. `New shared extraction candidates`:
3. `Deferred extractions + rationale`:
4. `Architecture-first decision record` (reuse path vs new path + why):

## 3.8 File-Sprawl Control (Onboarding/Fan-Out)

Обязательные метрики:

1. `Entity/Feature onboarding touchpoints`:
   - сколько файлов нужно изменить, чтобы добавить новый тип сущности/фичи
2. `Domain wiring fan-out`:
   - сколько обязательных файлов требуется для минимального рабочего домена
3. `Data-vs-Code delta`:
   - какие вариации реализуются данными (`assets/config/maps/policies`), а какие требуют нового кода

Принцип приоритета:

1. Сначала `data-driven` вариация (политики/настройки/мапы/контентные id), и только потом новый кодовый путь.
2. Новый кодовый branch допустим, когда различие нельзя выразить существующей моделью данных/политик.

Заполнить:

1. `Baseline touchpoints`:
2. `Target touchpoints`:
3. `Baseline fan-out`:
4. `Target fan-out`:
5. `Budget threshold` (max allowed):
6. `Mitigation plan if threshold exceeded`:
7. `Data-driven variation model`:
8. `Code branches introduced (count + why data was insufficient)`:

## 3.9 Data/Editor Policy (Odin)

1. Odin допустим в Unity editor/data authoring.
2. Odin не должен быть required runtime dependency для `framework/systems/core/runtimehost`.

Заполнить:

1. `Where Odin is used`:
2. `Why runtime layers remain Odin-free`:

## 3.10 State Ownership & Invariants

Заполнить:

1. `Source of truth state`:
2. `State owner`:
3. `Write paths`:
4. `Read paths`:
5. `Critical invariants`:

## 3.11 Testing & Fitness Gates

Нужно определить:

1. architecture tests (layering/dependency/coupling)
2. behavior regression tests
3. onboarding/fan-out guard checks (automated or PR checklist)

Заполнить:

1. `New/updated architecture tests`:
2. `Behavior tests`:
3. `Performance/load checks` (if needed):

## 3.12 ADR Triggers

ADR обязателен, если:

1. вводится новое семейство пакетов
2. ломается dependency direction
3. меняется owner/source-of-truth
4. бюджет file-sprawl превышается и требуется исключение

Заполнить:

1. `ADR required?`:
2. `ADR link`:

## 3.13 Rollout / Rollback

Заполнить:

1. `Commit slicing plan`:
2. `Rollback-safe checkpoints`:
3. `Migration risks`:

## 3.14 Definition Of Done

Система считается готовой, если:

1. размещение по слоям соответствует package policy
2. direct concrete coupling отсутствует
3. folder topology (`System` vs `Common`) соблюдена
4. file-sprawl метрики в бюджете или есть approved ADR
5. архитектурные и регрессионные тесты зелёные
6. документация обновлена и соответствует коду
7. различия между сценариями/доменами приоритетно заданы данными, а не дублированием кода

## 4) Quick PR Checklist

1. Есть ссылка на заполненный `System_Blueprint_<SystemName>.md`.
2. Указан `System Owner`.
3. Указаны разрешённые каналы коммуникации.
4. Приложены onboarding/fan-out метрики до/после.
5. Нет новых нетипизированных dependency refs (`GameObject`/`MonoBehaviour`/`Component` как service locator).
6. Добавлены/обновлены fitness tests.
7. Показано, какие различия реализованы data-driven и какие потребовали новый код.
8. Добавлен architecture-first note: что переиспользовано из существующих контрактов/паттернов, что не подошло и почему.
9. Если нужны исключения, приложен ADR.
