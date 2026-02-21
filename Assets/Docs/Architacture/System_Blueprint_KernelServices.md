# System Blueprint: Kernel Services

Date: 2026-02-21  
Template: `Assets/Docs/Architacture/New_System_Requirements_Template.md`  
Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
3. `Packages/com.morboo.core/Runtime/Kernel/KernelContracts.cs`
4. `Packages/com.morboo.systems/Runtime/Kernel/InMemoryKernelServices.cs`
5. `Packages/com.morboo.systems/Runtime/State/InMemorySessionStateStore.cs`
6. `Packages/com.morboo.systems/Runtime/State/InMemoryProfileStateStore.cs`
7. `Packages/com.morboo.architecture.tests/Tests/Editor/Phase2KernelContractSmokeTests.cs`
8. `Packages/com.morboo.architecture.tests/Tests/Editor/Phase2KernelServiceSmokeTests.cs`
9. `Packages/com.morboo.architecture.tests/Tests/Editor/Phase2KernelRuntimeStoreSmokeTests.cs`

## 1) System Passport

1. `System Name`: Kernel Services
2. `Owner`: Kernel Systems Owner
3. `Target Phase`: Phase 2 (`C2.1-C2.3`)
4. `Scope Type`: new system + extraction/modularization
5. `Behavior Impact`: controlled

## 2) Problem / Outcome

1. `Problem Statement`:
   - до Phase 2 lifecycle/objective/outcome/economy/session state были неявными и размазанными по gameplay-коду;
   - отсутствовал явный kernel-level API для межсистемного взаимодействия без прямых concrete зависимостей.
2. `Business/Game Outcome`:
   - стабильные контракты управления flow/scenario/objective/outcome/rulebook/state/economy/reward;
   - единая точка расширения для будущих игр и систем;
   - минимальные runtime имплементации для пошаговой миграции.
3. `In Scope`:
   - kernel service contracts;
   - objective scope model;
   - in-memory baseline implementations;
   - contract/runtime smoke tests.
4. `Out of Scope`:
   - production persistence backends;
   - content authoring pipelines и UI интеграция;
   - orchestration domain behavior.

## 3) Architecture Archetype (Analogy)

1. `Selected Archetype`: Kernel Service
2. `Why this archetype`:
   - сервисы владеют верхнеуровневым game/session lifecycle и кросс-системными состояниями.
3. `What differs from reference`:
   - текущий этап фиксирует contract-first + in-memory baseline, без финальных persistence providers.

## 4) Layer & Package Placement

1. `Framework`:
   - не содержит Kernel Services контрактов.
2. `Core`:
   - `KernelContracts.cs`: `IGameFlowService`, `IScenarioService`, `IObjectiveService`, `IOutcomeService`, `IRulebookProvider`, `ISessionStateStore`, `IProfileStateStore`, `ISaveLoadService`, `IEconomyLedger`, `IRewardService`, `ObjectiveRef`, `ObjectiveScope`.
3. `Systems`:
   - `InMemoryKernelServices.cs`: in-memory runtime реализации сервисов;
   - `InMemorySessionStateStore.cs`, `InMemoryProfileStateStore.cs`: runtime store реализации.
4. `RuntimeHost`:
   - не владеет kernel contracts; только потребляет через контракты при необходимости.
5. `Integration.StrategyCombat`:
   - не является owner для Kernel Services.
6. `MorbooBridge`:
   - может композировать/подключать сервисы, но не определяет контракты.

## 5) Folder Topology (Inside Layer)

1. `Core`: `Packages/com.morboo.core/Runtime/Kernel/*`
2. `Systems`: `Packages/com.morboo.systems/Runtime/Kernel/*`, `Packages/com.morboo.systems/Runtime/State/*`
3. `Tests`: `Packages/com.morboo.architecture.tests/Tests/Editor/Phase2Kernel*`

## 6) Communication Contract (No Direct Concrete Coupling)

1. `Inbound`:
   - вызовы через kernel interfaces из runtime host/bridge/game orchestration wiring.
2. `Outbound`:
   - query/mutation результаты через те же контракты и типизированные value objects.
3. `Forbidden`:
   - прямые concrete-to-concrete вызовы между gameplay systems в обход kernel contracts.

## 7) State Ownership & Invariants

1. `Source of truth state`:
   - in-memory service instances, подключаемые composition root.
2. `State owner`:
   - соответствующий kernel service (`IGameFlowService`, `IObjectiveService`, `IEconomyLedger`, и т.д.).
3. `Write paths`:
   - только методы сервисных контрактов.
4. `Read paths`:
   - query-методы сервисных контрактов.
5. `Critical invariants`:
   - все kernel контракты объявлены в `Morboo.Core`;
   - runtime baseline реализации находятся в `Morboo.Systems`;
   - контракты не содержат Unity-specific типов;
   - package graph не нарушает направление зависимостей.

## 8) Testing & Fitness Gates

1. `Contract gates`:
   - `Phase2KernelContractSmokeTests` проверяют наличие обязательных контрактов и их сигнатуры.
2. `Runtime service smoke`:
   - `Phase2KernelServiceSmokeTests` проверяют минимальную семантику in-memory сервисов.
3. `Runtime store smoke`:
   - `Phase2KernelRuntimeStoreSmokeTests` проверяют session/profile/entity store базовые сценарии.

## 9) Current Implementation Status

1. `Contracts`: implemented.
2. `Baseline runtime implementations`: implemented.
3. `Smoke tests`: implemented.
4. `Phase status`: closed (Phase 2).

## 10) Definition Of Done

1. Контракты Kernel Services определены в `Morboo.Core` и стабильны.
2. Минимальные runtime реализации доступны в `Morboo.Systems`.
3. Архитектурные/смоук-тесты покрывают наличие контрактов и базовую работоспособность.
4. Система документирована в `System_Blueprint_Index.md` со статусом `Ready`.
