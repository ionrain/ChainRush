# System Blueprint: Orchestration

Date: 2026-02-20  
Template: `Assets/Docs/Architacture/New_System_Requirements_Template.md`  
Related:
1. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Implementation_Audit_2026-02-19.md`
2. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Remediation_Backlog_By_Commits.md`
3. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
4. `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestration_Spatial_Dimensionality_3DFirst_2026-02-21.md`

## 1) System Passport

1. `System Name`: Orchestration Platform
2. `Owner`: Architecture/Gameplay (назначается в PR owner field)
3. `Target Phase`: Phase 4 (`C02..C07` + `C04A`), Phase 8 (`C08..C10`)
4. `Scope Type`: major refactor
5. `Behavior Impact`: controlled

## 2) Problem / Outcome

1. `Problem Statement`: текущая оркестрация работает как vertical slice для `Combat/Idle`, но проблема не ограничивается только domain-coupling; есть несколько системных разрывов, которые блокируют reusable platform-модель.
2. `Business/Game Outcome`: быстрый onboarding новых доменов без регрессий базовой боевой логики.
3. `In Scope`:
   - proposal-driven arbitration seam,
   - low-friction domain onboarding (`C04A`),
   - event/capability integration,
   - чистый `IWorldQuery` boundary,
   - переразмещение ответственности между `RuntimeHost` и `Integration.StrategyCombat`.
4. `Out of Scope`:
   - полный геймдизайн новых доменов,
   - полная замена боевых/idle правил,
   - TDE exit (это отдельный backlog).

## 2.1) Known Gaps (System-Level)

1. Runtime pipeline жёстко привязан к `Combat/Idle` моделям и их payload-типам.
2. Proposal/event seams частично задекларированы, но неполноценно используются как canonical runtime path.
3. `Capabilities` в основном registered-only: мало реальных decision/execution consumers.
4. Domain logic местами downcast-ит query boundary до concrete cache.
5. Есть нетипизированные serialized dependency holders в критических orchestration wiring точках.
6. Domain onboarding даёт file-sprawl (много host touchpoints вне папки домена).
7. В коде присутствуют переходные/legacy ветки (`Intent/Instruction`) без чёткой роли в целевом потоке.
8. Границы ответственности между `RuntimeHost` и `Integration.StrategyCombat` исторически размыты (часть host-инфры мигрировала в genre-layer).

## 2.2) Backlog Traceability (Gap -> Commits)

1. Domain-coupling + host branching -> `C03`, `C04`, `C04A`.
2. Proposal seam activation -> `C03`, `C04`.
3. Event pipeline activation -> `C05`.
4. Capabilities runtime usage -> `C06`.
5. Query downcast cleanup -> `C07`.
6. Typed dependency cleanup -> `C04A`.
7. RuntimeHost/integration responsibility normalization -> `C02`.
8. Legacy branch finalization (`Intent/Instruction`) -> `C09`.
9. Final architecture locks and all future-gates on -> `C10`.

## 3) Architecture Archetype (Analogy)

1. `Selected Archetype`: Runtime Platform Host + Simulation Domain
2. `Why this archetype`: orchestration координирует домены через общий execution loop/arbiter, а конкретная доменная логика должна жить в integration layer.
3. `What differs from reference`:
   - сейчас часть host-кода ещё в StrategyCombat (план: вернуть в RuntimeHost),
   - proposal/event contracts частично декларативны (план: активировать runtime usage).

## 4) Layer & Package Placement

1. `Proposal/IArbiter/IProposalSource/IDomainEvent/ICommandBus` base contracts -> `Packages/com.morboo.framework` -> универсальные инварианты -> Any game.
2. `RealtimeScheduler/InProcessBus` generic infra -> `Packages/com.morboo.systems` -> reusable runtime infra -> Any game runtime infra.
3. `Orchestration core contracts/capability contracts` -> `Packages/com.morboo.core` -> cross-genre domain contracts -> Cross-genre kernel.
4. `Loop/Arbiter/ExecutionRouter/World cache host seams` -> `Packages/com.morboo.runtimehost` -> host execution infra -> Cross-genre host execution.
5. `Combat/Idle domains, policies, executors, adapters` -> `Packages/com.morboo.integration.strategycombat` -> genre implementation -> Genre layer.
6. `Scene/project wiring and content glue` -> `Assets/Scripts/MorbooBridge` + `Assets/Scripts/Game` -> concrete game -> Project layer.

## 5) Folder Topology (Inside Layer)

1. `Planned folders`:
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Host`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/World`
   - `Packages/com.morboo.runtimehost/Runtime/Orchestration/Common`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/<DomainName>`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Common`
2. `Initial Common candidates`:
   - domain module registration contract,
   - proposal normalization/selection helpers,
   - shared domain diagnostics/asserts.
3. `Proof of multi-system reuse`:
   - для переноса в `Common` требуется минимум 2 named consumers (например Combat + Idle, позже Goals).

## 6) Communication Contract (No Direct Concrete Coupling)

1. `Inbound commands/events/queries`:
   - `ITickSource`,
   - `IWorldQuery`/world snapshot query interfaces,
   - domain proposal producers (`IProposalSource` equivalent).
2. `Outbound commands/events/queries`:
   - `ICommandBus.Publish(...)` dispatch payloads,
   - `IEventBus.Publish(...)` orchestration lifecycle/domain events.
3. `Bridge points`:
   - StrategyCombat adapters (`CombatCommandAdapter`, `IdleCommandAdapter`),
   - project bridge consumers only in `Assets/Scripts/MorbooBridge`.
4. `Forbidden direct deps`:
   - `Domain -> concrete OrchestrationWorldCache cast`,
   - `Domain -> EntityTransformResolver`,
   - `RuntimeHost -> Game.Runtime/MorbooBridge`,
   - direct concrete runtime calls between independent systems,
   - untyped dependency holder refs (`GameObject`/`MonoBehaviour`/`Component`) used to resolve runtime services.

## 7) Reuse & Common Extraction Audit

1. `Reused existing contracts/components`:
   - `IArbiter`, `Proposal`, `IProposalSource`,
   - `ICommandBus`, `IEventBus`,
   - `ITickSource`, `IWorldQuery`.
2. `New shared extraction candidates`:
   - `DomainModule/DomainRegistration` seam,
   - common domain onboarding pipeline (registration + proposal source hook),
   - shared fan-out diagnostics.
3. `Deferred extractions + rationale`:
   - `Intent/Instruction` branch (`C09`) deferred until proposal/event path stabilized.

## 8) File-Sprawl Control (Onboarding/Fan-Out)

1. `Baseline touchpoints` (из audit):
   - при добавлении нового домена нужно трогать минимум `ArbitrationInput`, `OrchestrationArbiterProposals`, `OrchestrationArbiter`, `ExecutionContext`, `ExecutionRouter`, dispatch payloads/adapters, domain keys.
2. `Target touchpoints`:
   - `0` правок в `Morboo.RuntimeHost` для стандартного нового домена,
   - максимум `1` registration touchpoint вне папки домена,
   - остальное внутри доменной папки.
3. `Baseline fan-out`:
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration`: `72` `.cs`,
   - `.../Domains`: `14` `.cs` (`Combat`: `7`, `Idle`: `6`, `Common`: `1`).
4. `Target fan-out`:
   - минимальный новый домен подключается через `<= 5` новых/изменённых файлов вне тестов.
5. `Budget threshold`:
   - `outside-domain touchpoints <= 1`,
   - `host-runtime touchpoints == 0` (для стандартного сценария).
6. `Mitigation plan if threshold exceeded`:
   - блокирующий ADR с обоснованием,
   - extraction PR до merge функционала (сначала снизить fan-out, потом добавлять домен).
7. `Current untyped refs to eliminate`:
   - `OrchestrationLoop.tickSourceComponent` (`[SerializeField] MonoBehaviour`),
   - `CombatCommandAdapter.orchestrationLoopComponent` (`[SerializeField] MonoBehaviour`),
   - `IdleCommandAdapter.orchestrationLoopComponent` (`[SerializeField] MonoBehaviour`),
   - `UnitCombatTargetSelector.worldQueryProvider` (`[SerializeField] MonoBehaviour`).
8. `Data-driven-first target`:
   - вариация домена задаётся policy/config/assets,
   - новый код вне папки домена добавляется только при технической невозможности data-driven выражения.

## 9) Data/Editor Policy (Odin)

1. `Where Odin is used`: только editor/data authoring в integration/project слоях при необходимости.
2. `Why runtime layers remain Odin-free`: runtime-зависимости `framework/systems/core/runtimehost` должны быть engine-agnostic и без Sirenix runtime coupling.

## 10) State Ownership & Invariants

1. `Source of truth state`:
   - orchestration world read model/snapshot для текущего тика,
   - domain decisions как proposal records.
2. `State owner`:
   - host runtime (`Morboo.RuntimeHost`) для orchestration pipeline state,
   - integration domains владеют только domain-specific policy/config.
3. `Write paths`:
   - domains produce proposals,
   - router emits dispatch commands/events,
   - adapters/executors применяют команды к game side.
4. `Read paths`:
   - domains читают только через query interfaces/snapshots.
5. `Critical invariants`:
   - arbiter не содержит fixed branching по именам доменов,
   - добавление домена не требует правок loop/router/arbiter host-кода,
   - capabilities реально влияют на decision path,
   - нет concrete downcasts из domain logic,
   - package-level spatial seam is `3D-first`,
   - hard planar policies/types are explicit `2D` specializations behind projection adapters.

## 11) Testing & Fitness Gates

1. `New/updated architecture tests`:
   - `Packages/com.morboo.architecture.tests/Tests/Editor/OrchestrationImplementationFitnessTests.cs` (C01 gates + future gates),
   - onboarding/fan-out gate checklist (C04A),
   - runtimehost no domain-name specific branching (future gate),
   - spatial dimensionality gates (`3D-first` boundary + `2D` specialization naming rule).
2. `Behavior tests`:
   - regression smoke: combat/idle parity after C03/C04/C04A,
   - capability-driven variation tests after C06.
3. `Performance/load checks`:
   - tick-time baseline/after check on representative combat scene.

## 12) ADR Triggers

1. `ADR required?`: yes, if any of below occurs:
   - new package family,
   - dependency direction break,
   - host-runtime touchpoint needed for each new domain,
   - file-sprawl budget exceeded.
2. `ADR link`: TBD (add when triggered).

## 13) Rollout / Rollback

1. `Commit slicing plan`:
   - C01-C02-C02A,
   - C03-C04-C04A,
   - C05-C07,
   - C08-C10.
2. `Rollback-safe checkpoints`:
   - compile-green after each commit group,
   - architecture tests green for active (non-ignored) gates.
3. `Migration risks`:
   - hidden behavior drift in arbitration priority,
   - partial migration leaving duplicate paths,
   - accidental RuntimeHost -> Integration/Game coupling.

## 14) Definition Of Done

1. package placement соответствует policy (`framework -> systems -> core -> runtimehost -> integration -> project`).
2. direct concrete coupling между системами отсутствует.
3. onboarding нового домена укладывается в budget (`host-runtime == 0 touchpoints`).
4. proposal/event/capability seams реально участвуют в runtime path.
5. архитектурные и регрессионные тесты зелёные.
6. docs/backlog/audit синхронизированы с фактическим кодом.
