# C04B — Multi-Scope / Multi-Arbiter Orchestration Host Restructure

Date: 2026-02-23  
Phase: `Phase 4` (`Orchestration Platform Remediation`)  
Type: structural refactor (scene-breaking allowed)  
Status: `in progress` (`Faction-first` start selected. `B2-B4` are in code: `OrchestrationPipeline` runtime container extracted in `Morboo.RuntimeHost`, `OrchestrationLoop` now hosts ordered `OrchestrationPipelineComponent[]` and ticks them, and per-pipeline domain composition moved under pipeline component owner. `B5` is in code: per-pipeline `Faction` + host-global `Relations` composition propagates into arbiter/runtime contexts. `B6` pivoted: targeting ownership moves to domain-owned `CombatTargetProvider` / `IdleTargetProvider` in `Morboo.Integration.StrategyCombat`; temporary RuntimeHost pipeline `CombatTargetSet` seam is removed.)

## Why This Step Exists

Текущая форма orchestration (`OrchestrationLoop` + один `OrchestrationArbiter` + список `domainOrchestrators`) хорошо работает как `single orchestration scope` (одна оркестрируемая когорта), но начинает ломаться как архитектурная модель для multi-faction / multi-cohort сценариев.

Симптомы:

1. Один `OrchestrationLoop` = один arbiter decision stream, что плохо масштабируется на независимые фракции (`Player`, `Enemy`, ...).
2. Появляется несколько мест, где задаётся один и тот же смысл (`Faction` / scope / доменный состав), что создаёт dual source-of-truth.
3. Добавление второй фракции ведёт к дублированию scene-структуры и ручного wiring, а не к расширению платформенного host.

## Goal

Перейти от модели:

1. `Loop -> Arbiter + Domains`

к модели:

1. `OrchestrationLoopHost -> OrchestrationPipeline[]`
2. Каждый `OrchestrationPipeline` владеет своим:
   - `Arbiter`
   - `Router`
   - `CommandBus`
   - `DomainOrchestrators[]`
   - `OrchestrationScope` (например faction/cohort filter)

## Non-Goals (For C04B)

1. Не переписывать `Combat/Idle` доменную логику “в ноль”.
2. Не завершать финальную унификацию `*Lite` orchestrators.
3. Не менять route-body ownership (оно уже должно оставаться в `Morboo.Integration.StrategyCombat`).
4. Не вводить полноценную multi-scene composition систему.

## Target Architecture (C04B End State)

## 1. RuntimeHost

`RuntimeHost` становится owner-ом host-пайплайнов, а не single-arbiter сцепки.

Основные роли:

1. `OrchestrationLoopHost` (или эволюция текущего `OrchestrationLoop`)
   - тикает набор `OrchestrationPipeline`
   - не владеет напрямую списком доменов одного arbiter-а
2. `OrchestrationPipeline`
   - runtime container одного orchestration scope
   - единственный владелец `Arbiter/Router/Bus/Domains` этого scope
3. `OrchestrationScope` (target concept, deferred)
   - runtime scope contract (faction/team/cohort identity + optional tags/filters)
   - вводится только если/когда `Faction-first` path станет недостаточен

## 2. Core / Contracts

Верхнеуровневые контракты должны выражать `scope`, а не только “один глобальный мир”.

Минимум для C04B:

1. На старте `C04B` использовать `FactionAsset` как scope identity (`Faction-first`) вместо нового `int`-based scope contract.
2. Убрать implicit dependence на “global single faction” из package seams.
3. Typed `OrchestrationScope` contract остаётся опцией более позднего подшага, если `Faction-first` path окажется недостаточным.

Примечание:

1. `Faction` как gameplay-концепт не обязан быть единственным типом scope.
2. `Scope` должен быть host/runtime-канонической формой для multi-pipeline orchestration.
3. Если общий `Scope` contract всё же вводится позже (после `Faction-first` этапа), он НЕ должен быть raw `int` seam в package interfaces.
4. Предпочтительная форма общего scope identity:
   - asset-based typed identity (например `OrchestrationCollectiveAssetBase` / эквивалент),
   - где `FactionAsset` становится частным случаем (наследник/реализация общего контракта),
   - а `GroupAsset` / `CohortAsset` могут добавляться позже без смены runtime seam на raw ids.

## 3. Integration.StrategyCombat

`Morboo.Integration.StrategyCombat` остаётся owner-ом domain-specific behavior, но начинает работать scope-aware:

1. `Combat/Idle` domains читают scope из context (а не из нескольких разрозненных настроек).
2. `CombatTargetSet`/targeting queries становятся scope-aware (или per-pipeline), а не неявно global.
3. Route execution presets/policies могут применяться per-pipeline.

## 4. MorbooBridge (Project Composition)

`MorbooBridge` задаёт composition конкретной игры:

1. Какие pipelines есть в сцене (`Player`, `Enemy`, ...).
2. Какой `Scope` назначен каждому pipeline.
3. Какие домены включены в каждом pipeline.
4. Какие preset/policy assets назначены каждому pipeline.

`MorbooBridge` не должен дублировать domain list отдельно от pipeline source-of-truth.

## Single Source-Of-Truth Rules (C04B)

После `C04B`:

1. Состав доменов принадлежит `OrchestrationPipeline` (не host-loop и не отдельному bridge-реестру).
2. `Scope/Faction` принадлежит `OrchestrationPipeline` (не задаётся повторно в `Loop`, `Arbiter`, `Domain`, `TargetSet` как независимые настройки).
3. `OrchestrationLoopHost` знает только список pipelines и tick sequencing.
4. Домены читают scope из runtime context/pipeline, а не из ad-hoc scene refs.

## Migration Plan (Commit Slices)

Scene-breaking changes are allowed for this step.

## B1 — Faction-First Scope Freeze (No New Scope Type Yet)

1. Зафиксировать `FactionAsset` как текущий orchestration scope identity для multi-pipeline migration start.
2. Добавить tests/checklist, что `C04B` старт не вводит новый `int`-based scope seam в package interfaces.
3. Подготовить pipeline ownership refactor так, чтобы typed scope contract можно было добавить позже без повторного размазывания identity по слоям.
4. Если позже потребуется generalized scope contract, вводить его только как asset-based typed identity (base asset/contract), а не как `int Id` в package boundary.

Acceptance:

1. Compile green.
2. Existing single-scope scene работает без новых scope-type полей/ID seam.
3. `C04B` pipeline-host migration стартует на `Faction-first` semantics.

## B2 — Extract Pipeline Runtime Container

1. Ввести `OrchestrationPipeline` runtime container (`Arbiter`, `Router`, `Bus`, `Domains`, `Scope`).
2. Перенести current single-scope wiring в один pipeline instance.
3. `OrchestrationLoop` временно становится host для одного pipeline.

Acceptance:

1. Поведение current scene не меняется (один pipeline).
2. `OrchestrationLoop` больше не владеет directly `arbiter + domains` как primary model.

## B3 — LoopHost Ticks Multiple Pipelines

1. `OrchestrationLoop` эволюционирует в `LoopHost` (или остаётся именем `OrchestrationLoop`, но по роли это host).
2. Поддерживает `pipelines[]` (ordered).
3. Тикает каждый pipeline независимо.

Acceptance:

1. Возможен runtime path с `2` pipelines (даже если второй dummy).
2. Host не читает domain list напрямую.

## B4 — Move Domain Composition Under Pipeline

1. Domain source-of-truth переносится в `OrchestrationPipeline`.
2. Удаляются/обесцениваются старые loop-level domain composition fields.
3. Bridge preset/seams читают domains через pipeline(s), не через host-global list.

Acceptance:

1. Один source-of-truth для domains на pipeline.
2. Нет второго списка доменов в bridge для того же pipeline.

## B5 — Scope Propagation End-To-End

1. `Arbiter` и domain contexts получают scope из pipeline.
2. Убираются дублирующие faction/scope настройки, где они становятся redundant.
3. Добавить tests на scope propagation до domain execution path.

Acceptance:

1. Domain route/execution path может читать pipeline scope.
2. Нет ручной прокидки faction из нескольких точек для одного и того же pipeline.

## B6 — StrategyCombat Scope-Aware Targeting / Target Set Ownership

1. StrategyCombat targeting seam становится scope-aware через domain-owned providers (`CombatTargetProvider` / `IdleTargetProvider`), а не через доменно-типизированные поля в `RuntimeHost` pipeline.
   - legacy/registry fallback path для target set НЕ допускается как параллельный слой поведения; missing owner-path должен диагностироваться явно (fail-fast / explicit error).
2. Исключить глобально-неявный target set для multi-faction orchestration.
3. Зафиксировать, где находится source-of-truth target selection context.

Acceptance:

1. Два pipeline-а не конфликтуют через общий targeting state.
2. StrategyCombat domain logic не полагается на “single global faction”.

## B7 — Scene Migration (Player + Enemy Pipelines)

1. Собрать core scene на `2` pipelines (`Player`, `Enemy`) как reference composition.
2. Назначить presets/policies per pipeline.
3. Обновить playtest checklist / debug checklist.
4. Зафиксировать ограничение этапа: enemy pipeline на этом шаге валидируется как host/path integration (`Faction`, domains, routing, tick, dispatch), а не как full behavior parity по role/policy mapping (`UnitClass`-oriented semantics).
5. Обеспечить adapter compatibility для multi-pipeline host/path: shared loop command bus + per-flush dispatch-context override (`CurrentWorld` / `CurrentExecContext`) без дублирования adapter stack.

Acceptance:

1. Two-pipeline orchestration работает без дублирования host-кода.
2. Добавление третьего pipeline не требует правок `RuntimeHost` core logic.
3. `Player + Enemy` scene migration проходит как reference host/path composition даже если enemy domain behavior временно неполон из-за различий `Unit` / `Enemy` actor shape.
4. Полная two-faction behavioral parity (включая обобщённый class/role mapping вместо `UnitClass` assumptions) явно отложена в `C04C`, а не закрывается ad-hoc bridge fallback’ами в `C04B`.
5. Multi-pipeline command dispatch сохраняет совместимость текущих adapter'ов (общий loop `CommandBus`, корректный per-flush `CurrentWorld/CurrentExecContext`).

## Tests / Gates To Add During C04B

1. `OrchestrationLoop` (host) does not own a single direct domain list as primary composition model.
2. `OrchestrationPipeline` is the single owner of `domainOrchestrators` for that scope.
3. `Scope` identity is single-source-of-truth per pipeline (initially `Faction-first`; typed scope contract may be added later if needed).
4. Bridge composition does not duplicate domain list outside pipeline for the same pipeline.
5. Multi-pipeline smoke test:
   - two pipelines ticked
   - independent arbiter decisions can coexist.

## Risks / Tradeoffs

1. `Scene-breaking` риск высокий: inspector wiring изменится.
2. Временный рост числа runtime containers (`Pipeline`) увеличит surface area, но это нормальная цена за устранение semantic duplication.
3. Нельзя смешивать `C04B` с “еще одним быстрым bridge workaround” — иначе снова появится двойной source-of-truth.
4. Если перенос общей абстракции в правильный верхний слой блокируется конкретным слабым звеном (тип/seam/component), сначала абстрагировать слабое звено; не снижать цель `C04B` под локальное ограничение без явного согласования и фиксации решения.

## Execution Order Recommendation

1. Finish current `C04A` route-policy/preset checkpoint (already in progress).
2. Start `C04B` as the next structural slice before broad multi-faction orchestration rollout.
3. Only after `C04B` continue adding more scopes/factions/domain variants in scene.

## Decision Record (Current Position)

1. Для текущей одной core-scene временно допустим single-pipeline path.
2. Для `Player + Enemy` orchestration текущая single-arbiter loop-модель считается transitional and insufficient.
3. `C04B` is the planned remediation path (not a workaround).
4. `Faction-first` выбран как старт `C04B`; generalized scope contract (если появится) должен быть asset-based typed identity, а не raw `int` seam.
