# Game Runtime System Decomposition And Layer Mapping (No UI/Board)

Date: 2026-02-20  
Status: Draft (analysis baseline for system-by-system planning)  
Scope: `Assets/Scripts/Game/**` excluding `UI/**` and `Core/Board/**`

Related:
1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
3. `Assets/Docs/Architacture/Architecture_Layers_Reference.md`
4. `Assets/Docs/Architacture/Architecture_Compliance_Standard.md`
5. `Assets/Docs/Architacture/System_Blueprint_Orchestration.md`

## 1) Purpose

Зафиксировать целевую декомпозицию игровых систем и подсистем, чтобы дальнейшая миграция шла сверху-вниз от владельцев состояния и контрактов, а не от текущей раскладки файлов.

## 2) Hard Constraints

1. Слои: `Framework -> Systems -> Core -> RuntimeHost -> Integration.StrategyCombat -> MorbooBridge -> Game.Runtime`.
2. `com.morboo.*` packages не зависят от project assemblies (`Morboo.Bridge`, `Game.Runtime`).
3. Системы не общаются concrete-to-concrete напрямую; только events/commands/queries/API contracts.
4. Data-driven в приоритете для вариативности.
5. Нет нетипизированных dependency-holder ссылок для runtime service resolution.
6. `Game.Runtime` на этом этапе рассматривается как legacy runtime adapter layer, а не целевой владелец kernel truth.

## 3) Current Code Clusters (No UI/Board)

Measured folder groups inside `Assets/Scripts/Game`:
1. `Core` (31 files): Characters, Skills, ItemDrop, LevelGoals.
2. `Data` (26 files): Unit/Enemy/Skill/Level/Resources/Item/... data assets.
3. `Managers` (17 files): lifecycle, flow, resources, rewards, analytics, tutorial.
4. `TopDownEngineExt` (15 files): TDE AI actions/decisions/ext.
5. `Triggers` (9 files), `Rewards` (7 files), `Input` (2 files), misc runtime helpers.

## 4) Target System Catalog For This Scope

This section defines systems first, then maps current files to them.

### 4.1 Kernel.FlowScenario

Purpose:
1. Own run/session flow states and scenario/level transitions.

Primary current files:
1. `Assets/Scripts/Game/Managers/LevelManager.cs`
2. `Assets/Scripts/Game/Data/AllLocationsData.cs`
3. `Assets/Scripts/Game/Data/LocationData.cs`
4. `Assets/Scripts/Game/Data/LevelData.cs`
5. `Assets/Scripts/Game/Data/LevelDifficultyData.cs`

Target ownership by layer:
1. `Core`: flow/scenario contracts and immutable flow state models.
2. `Systems`: default in-memory flow/scenario runtime implementations.
3. `RuntimeHost`: optional host coordination for tick/scenario transitions (no game types).
4. `MorbooBridge`: scene/startup wiring and translation to project events.
5. `Game.Runtime`: temporary adapter MonoBehaviours until full migration.

### 4.2 Kernel.ObjectiveOutcome

Purpose:
1. Own objective scopes and progression.
2. Own win/lose outcome calculation via rulebook.

Primary current files:
1. `Assets/Scripts/Game/Managers/LevelGoalManager.cs`
2. `Assets/Scripts/Game/Core/LevelGoals/LevelGoal.cs`
3. `Assets/Scripts/Game/Core/LevelGoals/AmountLevelGoal.cs`
4. `Assets/Scripts/Game/Core/LevelGoals/CollectLevelGoal.cs`
5. `Assets/Scripts/Game/Core/LevelGoals/EnemyLevelGoal.cs`
6. `Assets/Scripts/Game/Core/LevelGoals/EnemyTypeLevelGoal.cs`
7. `Assets/Scripts/Game/Core/LevelGoals/InventoryLevelGoal.cs`
8. Outcome parts currently mixed in `Assets/Scripts/Game/Managers/LevelManager.cs`.

Target ownership by layer:
1. `Core`: `IObjectiveService`, `IOutcomeService`, scope models, contracts.
2. `Systems`: objective tracker/outcome resolver implementations.
3. `RuntimeHost`: orchestration-facing objective/outcome query seams if needed.
4. `MorbooBridge`: project mapping from level goals to generic objective refs.
5. `Game.Runtime`: temporary adapters only.

### 4.3 Kernel.SessionProfileSave

Purpose:
1. Own profile/session mutable state.
2. Own save/load lifecycle and game settings persistence.

Primary current files:
1. `Assets/Scripts/Game/Managers/GameManager.cs`
2. `Assets/Scripts/Game/Data/AllUnitsData.cs`
3. `Assets/Scripts/Game/Data/AllItemsData.cs`
4. `Assets/Scripts/Game/Data/AllLocationsData.cs`
5. `Assets/Scripts/Game/Data/ResourcesData.cs`
6. `Assets/Scripts/Game/Data/DailyRewardsData.cs`

Target ownership by layer:
1. `Core`: contracts (`ISessionStateStore`, `IProfileStateStore`, `ISaveLoadService`).
2. `Systems`: runtime store implementations.
3. `MorbooBridge`: serialization adapters (`GameData` compatibility), bootstrap.
4. `Game.Runtime`: temporary persistence façade until cutover.

### 4.4 EconomyReward

Purpose:
1. Own resources, transactions, reward transfer and claim.

Primary current files:
1. `Assets/Scripts/Game/Managers/GameResourcesManager.cs`
2. `Assets/Scripts/Game/Managers/RewardManager.cs`
3. `Assets/Scripts/Game/Managers/LootManager.cs`
4. `Assets/Scripts/Game/Rewards/Reward.cs`
5. `Assets/Scripts/Game/Rewards/ResourceReward.cs`
6. `Assets/Scripts/Game/Rewards/InventoryReward.cs`
7. `Assets/Scripts/Game/Rewards/UnitReward.cs`
8. `Assets/Scripts/Game/Rewards/UnitCardReward.cs`
9. `Assets/Scripts/Game/Data/ResourcesData.cs`
10. `Assets/Scripts/Game/Data/RewardsData.cs`
11. `Assets/Scripts/Game/Data/BankData.cs`
12. `Assets/Scripts/Game/Data/BankItemData.cs`

Target ownership by layer:
1. `Core`: `IEconomyLedger`, `IRewardService`, transaction contracts.
2. `Systems`: in-memory/default runtime implementations.
3. `RuntimeHost`: no domain formulas; only host-level dispatch seams if needed.
4. `Integration.StrategyCombat`: only genre-specific reward policies, no project assets.
5. `MorbooBridge`: conversion adapters from project reward assets and item/resource ids.
6. `Game.Runtime`: temporary adapters only.

### 4.5 Actor System (Identity/Stats/Abilities/Control)

Purpose:
1. Unify Unit/Enemy around actor model.
2. Own actor identity/faction/role/capabilities/traits boundary.
3. Introduce generic stats system so HP and similar values are not hardcoded taxonomy leaks.

Primary current files:
1. `Assets/Scripts/Game/Core/Characters/Unit.cs`
2. `Assets/Scripts/Game/Core/Characters/Enemy.cs`
3. `Assets/Scripts/Game/Managers/UnitManager.cs`
4. `Assets/Scripts/Game/Managers/EnemyManager.cs`
5. `Assets/Scripts/Game/Data/UnitData.cs`
6. `Assets/Scripts/Game/Data/EnemyData.cs`
7. `Assets/Scripts/Game/Data/AttributesData.cs`
8. `Assets/Scripts/Game/Data/AllUnitsData.cs`
9. `Assets/Scripts/Game/Data/AllEnemiesData.cs`

Critical correction (already identified):
1. `Packages/com.morboo.core/Runtime/Entity/EntityStateTraitKeys.cs` currently contains project/genre keys (`state.hp01`, `unit.class`, `enemy.type`) and must be split by abstraction level.

Target ownership by layer:
1. `Framework`: generic ids/value-types only.
2. `Core`: actor/trait/stats contracts that are cross-genre.
3. `Systems`: runtime state stores and actor/stat runtime helpers.
4. `RuntimeHost`: host execution/query seams; no `Unit/Enemy` direct knowledge.
5. `Integration.StrategyCombat`: combat-oriented actor classifiers and domain-specific traits.
6. `MorbooBridge`: project-level trait key mapping and concrete data-to-actor binding.
7. `Game.Runtime`: temporary concrete actor MB implementations.

### 4.6 AbilitiesCombatExecution

Purpose:
1. Own abilities/skills and combat execution behavior.
2. Keep orchestration host domain-agnostic while StrategyCombat stays genre-specific.

Primary current files:
1. `Assets/Scripts/Game/Core/Skills/Skill.cs`
2. `Assets/Scripts/Game/Core/Skills/AttackSkill.cs`
3. `Assets/Scripts/Game/Core/Skills/MeleeAttackSkill.cs`
4. `Assets/Scripts/Game/Core/Skills/DistantAttackSkill.cs`
5. `Assets/Scripts/Game/Core/Skills/SupportSkill.cs`
6. `Assets/Scripts/Game/Core/Characters/WeaponManager.cs`
7. `Assets/Scripts/Game/Core/Characters/UnitAIController.cs`
8. `Assets/Scripts/Game/Data/SkillData.cs`
9. `Assets/Scripts/Game/Data/ElementsData.cs`
10. `Assets/Scripts/Game/Data/BuffsData.cs`
11. `Assets/Scripts/Game/Data/FormationProfile.cs`
12. `Assets/Scripts/Game/Data/UnitAIProfile.cs`

Target ownership by layer:
1. `Core`: ability/combat contracts only, no project payloads.
2. `RuntimeHost`: proposal/arbitration/execution host seams, no concrete unit/enemy types.
3. `Integration.StrategyCombat`: combat/idle domain policies and orchestrators.
4. `MorbooBridge`: adapters to concrete game actor components and scene data.
5. `Game.Runtime`: temporary behavior owners until engine/TDE exit slices land.

### 4.7 SpawnEncounter

Purpose:
1. Own wave/fill/trigger spawn lifecycle and encounter pacing.

Primary current files:
1. `Assets/Scripts/Game/Managers/EnemyManager.cs`
2. `Assets/Scripts/Game/Data/EnemyGenerationData.cs`
3. `Assets/Scripts/Game/Core/Characters/IPostSpawnSetup.cs`
4. `Assets/Scripts/Game/Triggers/*` (spawn-related only)

Target ownership by layer:
1. `Core`: spawn/encounter contracts and event payload contracts.
2. `Systems`: generic scheduler/runtime infra.
3. `RuntimeHost`: encounter host coordination seams.
4. `Integration.StrategyCombat`: strategycombat spawn policies.
5. `MorbooBridge`: concrete scene trigger mapping and prefab wiring.
6. `Game.Runtime`: temporary concrete manager implementations.

### 4.8 PlatformAdaptersLiveOps (Project-leaning)

Purpose:
1. Encapsulate analytics/social/notifications/tutorial/platform hooks.

Primary current files:
1. `Assets/Scripts/Game/Managers/MyAnalyticsManager.cs`
2. `Assets/Scripts/Game/Managers/SocialManager.cs`
3. `Assets/Scripts/Game/Managers/LocalNotificationsManager.cs`
4. `Assets/Scripts/Game/Managers/TutorialManager.cs`
5. `Assets/Scripts/Game/Managers/GameNotificationManager.cs`
6. `Assets/Scripts/Game/Managers/RewardFlyManager.cs`
7. `Assets/Scripts/Game/TinySaucePreloader.cs`

Target ownership by layer:
1. `Core/Systems`: only abstract telemetry/liveops contracts when required.
2. `MorbooBridge`: real owner of project SDK adapters and scene integration.
3. `Game.Runtime`: temporary implementation location until bridge extraction completes.

### 4.9 Engine Anti-Corruption (TDE)

Purpose:
1. Remove direct TDE ownership from gameplay kernel and orchestration packages.

Primary current files:
1. `Assets/Scripts/Game/TopDownEngineExt/*`
2. TDE references in `Assets/Scripts/Game/Core/Characters/*` and `Assets/Scripts/Game/Core/Skills/*`.
3. TDE bridge code in `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/Combat/TDE/*`.

Target ownership by layer:
1. `Integration.StrategyCombat`: no direct `Game.Runtime` dependency in final architecture.
2. `MorbooBridge`: concrete engine adapter layer for this project.
3. `Game.Runtime`: temporary hosts while adapters are extracted.

## 5) Layer Mapping Matrix (Normative)

Use this for every new component introduced during migration:
1. `Any game`: `com.morboo.framework`.
2. `Any game runtime infra`: `com.morboo.systems`.
3. `Cross-genre kernel contracts/models`: `com.morboo.core`.
4. `Cross-genre host execution`: `com.morboo.runtimehost`.
5. `Genre-specific StrategyCombat`: `com.morboo.integration.strategycombat`.
6. `Project wiring/content/glue`: `Assets/Scripts/MorbooBridge`.
7. `Legacy runtime behavior while migrating`: `Assets/Scripts/Game`.

## 6) Immediate Risk Flags (Must Address Early)

1. `EntityStateTraitKeys` contains domain/project keys in Core.
2. `Morboo.Integration.StrategyCombat` currently references `Game.Runtime` and TDE directly.
3. `Morboo.RuntimeHost` is underutilized; host responsibilities are still concentrated in StrategyCombat layer.
4. `Game.Runtime` still owns multiple system truths and mixes kernel + domain + adapters.

## 7) Migration Order (Top-Down, No Parallel Architecture)

### Slice M0: Contract And Guardrail Freeze
1. Freeze system owners and interaction contracts for all systems in this document.
2. Add/extend architecture tests for package boundaries and project leaks.

### Slice M1: Trait Taxonomy Cleanup
1. Split generic vs strategycombat vs project trait keys.
2. Remove project/genre keys from `Morboo.Core`.

### Slice M2: Stats Foundation
1. Introduce generic stats contracts and runtime service.
2. Move HP-like semantics to stats model, not hardcoded trait constants.

### Slice M3: Kernel Flow/Objective/Outcome Hardening
1. Separate ownership of flow, objective, outcome from `Game.Runtime` managers.
2. Keep MonoBehaviour adapters behavior-neutral during transition.

### Slice M4: Actors/Spawn Unification
1. Start converging Unit/Enemy ownership into actor-oriented model.
2. Move shared lifecycle and spawn contracts to Core/Systems/RuntimeHost seams.

### Slice M5: Economy/Reward Normalization
1. Consolidate resource and reward transfer through kernel contracts.
2. Keep conversion from current assets in bridge adapters.

### Slice M6: Orchestration Host Normalization
1. Move host concerns from StrategyCombat package into `Morboo.RuntimeHost`.
2. Keep StrategyCombat package focused on genre policies/domains.

### Slice M7: Engine Anti-Corruption Consolidation
1. Move TDE concrete bindings behind project bridge adapters.
2. Remove direct `Game.Runtime` reference from StrategyCombat package.

### Slice M8: Legacy Drain
1. Drain remaining system truth from `Assets/Scripts/Game` to package/bridge target owners.
2. Keep `Game.Runtime` as thin adapters only.

## 8) Review Workflow (Per-System Deep Dive)

For each system/subsystem review session:
1. Confirm owner and source-of-truth.
2. Confirm inbound/outbound contracts.
3. Confirm layer/package placement for each block.
4. Confirm migration slice and rollback checkpoint.
5. Confirm architecture tests to prevent regressions.

This document is the global map; detailed execution for each system should be split into dedicated blueprints/backlogs.
