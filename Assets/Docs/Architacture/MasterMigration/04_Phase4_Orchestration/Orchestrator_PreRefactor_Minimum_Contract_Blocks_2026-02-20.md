# Orchestrator Pre-Refactor Minimum Contract Blocks (Layer-First Commit Plan)

Date: 2026-02-20  
Status: Draft gate definition (must be completed before orchestration refactor commits C03+)  
Scope: minimal final interface blocks for systems interacting with Orchestrator, executed strictly by layer hierarchy.

Related:
1. `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`
2. `Assets/Docs/Architacture/System_Blueprint_Actor.md`
3. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md`
4. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
5. `Assets/Docs/Architacture/System_Interaction_Contract_Actor_Orchestrator.md`

## 1) Purpose

Перед рефакторингом оркестратора зафиксировать минимальный финальный контур взаимодействия с внешними системами так, чтобы:
1. не менять API каждый спринт;
2. не рефакторить host/runtime и внешние домены одновременно;
3. выполнить переход текущей игры только через `MorbooBridge`;
4. пройти подготовку к C03+ без повторного открытия границ слоёв.

## 2) Hard Gate Policy

1. `C03+` (proposal/arbitration/runtimehost refactor) начинается только после выполнения всех коммитов этого документа или явного defer через ADR.
2. Migration-only transitional forms (legacy keys/compat enums/temporary shim DTOs) разрешены только в `Assets/Scripts/MorbooBridge`, запрещены во всех `com.morboo.*` пакетах.
3. Коммиты идут строго сверху вниз по иерархии слоёв. Пропуск слоя запрещён.
4. Сразу после C07 обязателен post-refactor cleanup gate: убрать hard Actor↔Combat/Idle связи и зафиксировать компактный доменный onboarding.

## 3) Layer Hierarchy (Execution Order)

1. `L1` `com.morboo.framework`
2. `L2` `com.morboo.core`
3. `L3` `com.morboo.systems`
4. `L4` `com.morboo.runtimehost`
5. `L5` `com.morboo.integration.strategycombat`
6. `L6` `Assets/Scripts/MorbooBridge`

## 4) Contract Blocks (What must be stabilized)

1. `Actor Block`: identity, state read path, command write path, capability/faction/role boundaries.
2. `Faction/Relations Block`: friendliness checks and relation policy resolution.
3. `Role/Capabilities Block`: role routing and capability consumption.
4. `Spatial/Targeting Block`: world-query snapshot read model, no concrete cache downcasts.
5. `Execution Block`: dispatch via contracts, no direct concrete apply.
6. `Tick/Loop/Bus Block`: typed loop dependencies, stable tick/context contracts.

## 5) Commit Plan By Layer

### C4.P0 - Contract Freeze Baseline (Docs + Gates)
Layer: `Program docs + architecture tests` (prep)

Changes:
1. Freeze this plan and link it from roadmap/backlog.
2. Lock actor interaction contract (`System_Interaction_Contract_Actor_Orchestrator.md`).
3. Register mandatory architecture/future-gate tests for blocks in section 4.

Acceptance:
1. Документы ссылаются друг на друга без битых путей.
2. Есть явный список тестов-гейтов на C4.P7.

### C4.P1 - Framework Surface Freeze
Layer: `L1` (`com.morboo.framework`)

Changes:
1. Validate only universal orchestration seams remain in Framework (ids, command/event/query primitives).
2. Remove/forbid any domain or transition-specific tokens from Framework.

Acceptance:
1. Framework не знает про orchestration domain specifics.
2. Нет переходных форм в Framework.

### C4.P2 - Core Contract Cleanup (Actor/Faction/Role/Capabilities)
Layer: `L2` (`com.morboo.core`)

Changes:
1. Freeze generic contracts for Actor/Faction/Role/Capabilities read/write boundaries.
2. Удалить из Core strategy/project/transitional taxonomy (включая legacy key sets).
3. Зафиксировать, что read-side оркестратора адресуется по `EntityId` и snapshot/query контрактам.

Acceptance:
1. Core содержит только cross-genre контракты/модели.
2. Все strategy/project keys вытеснены ниже Core.
3. Core не содержит transition-only формы.

### C4.P3 - Systems Runtime Projections
Layer: `L3` (`com.morboo.systems`)

Changes:
1. Stabilize runtime implementations for tick/bus/query projection seams, без project-type payload.
2. Ensure loop dependencies are typed and stable for host consumption.

Acceptance:
1. Systems runtime не зависит от StrategyCombat/Bridge.
2. Tick/Bus contracts готовы для RuntimeHost без конкретики игры.

### C4.P4 - RuntimeHost Host-Seam Hardening
Layer: `L4` (`com.morboo.runtimehost`)

Changes:
1. Enforce router path: `Decision -> dispatch contracts` only (no direct concrete apply).
2. Remove concrete world cache downcasts from orchestration runtime path.
3. Freeze loop context and proposal input seam for domains.

Acceptance:
1. RuntimeHost остается domain-agnostic infra.
2. Execution/Spatial blocks закрыты на типизированных seam-контрактах.
3. Нет project refs и transition-only форм.

### C4.P5 - StrategyCombat Binding
Layer: `L5` (`com.morboo.integration.strategycombat`)

Changes:
1. Stabilize strategy-specific payloads/adapters/policies against frozen host/core seams.
2. Ensure role/capability/faction/spatial strategy policies resolved inside StrategyCombat layer.

Acceptance:
1. Вся strategycombat-специфика живет в этом слое.
2. RuntimeHost не требует ссылок на проектный слой.

### C4.P6 - MorbooBridge Legacy Mapping Isolation
Layer: `L6` (`Assets/Scripts/MorbooBridge`)

Changes:
1. Keep legacy Unit/Enemy mappings only as bridge adapters.
2. All transitional compatibility forms are isolated here with explicit removal gate.

Acceptance:
1. Legacy integration не поднимается выше Bridge.
2. Bridge не становится owner переносимой логики.

### C4.P7 - Gate Activation + Sign-Off
Layer: `Architecture tests + smoke`

Changes:
1. Enable/adjust tests for all six contract blocks.
2. Run smoke path:
   - actor state -> world snapshot -> arbiter/router;
   - dispatch command -> adapter -> actor handler.
3. Run semantic closure check (layer meaning, single source of truth, no transitional leakage).

Acceptance:
1. Все formal gates зеленые.
2. Semantic closure check зафиксирован в PR.
3. C03+ разрешён без переоткрытия границ.

## 6) Distribution Matrix (Block -> Layer Owner)

1. `Actor Block`: Core contracts -> Systems projections -> RuntimeHost read/write seams -> StrategyCombat policy binding -> Bridge legacy adapters.
2. `Faction/Relations Block`: Core relation contracts -> RuntimeHost relation queries -> StrategyCombat relation policies -> Bridge content mapping only.
3. `Role/Capabilities Block`: Core role/capability contracts -> RuntimeHost consumption seam -> StrategyCombat policy/materialization -> Bridge legacy mapping.
4. `Spatial/Targeting Block`: Framework/Core query primitives -> Systems projection runtime -> RuntimeHost orchestration reads -> StrategyCombat targeting policies.
5. `Execution Block`: Framework command/event primitives -> RuntimeHost dispatch orchestration -> StrategyCombat handlers -> Bridge legacy receiver adapters.
6. `Tick/Loop/Bus Block`: Systems runtime infra -> RuntimeHost loop orchestration.

## 7) Exit Condition

Pre-refactor stage is complete when:
1. C4.P0..C4.P7 completed (or explicitly deferred by ADR);
2. no critical ADR debt remains open for blocks in section 4;
3. orchestration refactor can proceed without reopening Actor/Faction/Role/Spatial/Execution/Tick boundaries.

## 8) Mandatory Immediate Post-Refactor Cleanup (C4.P8)

Applies immediately after orchestration C07 and before adding new domains/features on top.

Changes:
1. Remove Actor-boundary hard references to concrete `Combat/Idle` domain contracts in package layers.
2. Keep `Combat/Idle` as StrategyCombat domain modules/adapters behind generic orchestration/actor seams.
3. Introduce compact domain module pattern:
   - one domain descriptor/registration entry,
   - one domain handler entry point,
   - data-driven policies/assets for variation,
   - avoid interface/class fan-out by default.
4. Activate file-sprawl guard for domain onboarding with explicit budget.

Acceptance:
1. No hard `Combat/Idle` coupling in actor-orchestrator package boundary.
2. Adding one new domain module stays within agreed onboarding fan-out budget (tracked in PR).
3. Architecture tests and semantic closure note confirm compact, reusable domain structure.
