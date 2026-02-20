# TopDownEngine Exit Migration Backlog (MMFeedbacks stays)

Date: 2026-02-19
Owner: Game.Runtime + MorbooBridge layers

## Goal

Remove runtime dependency on `MoreMountains.TopDownEngine` while keeping:

- `MoreMountains.Feedbacks` in game layer (`Assets/Scripts/Game/**`)
- current architecture boundaries (packages stay package-only, project gameplay stays in `Assets/Scripts/Game`, glue stays in `Assets/Scripts/MorbooBridge`)

## Non-Negotiable Constraints

1. `Packages/com.morboo.*` must not depend on project-specific gameplay implementation details.
2. No behavior changes inside one migration slice.
3. No parallel long-term architecture:
   One active gameplay path, old path removed immediately after slice validation.
4. All prefab/script moves via `git mv` with `.meta` preserved.

## Ownership & Phase Mapping (Phase 0 baseline)

1. `Slice 0` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 1`
2. `Slice 1` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 1`
3. `Slice 2` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 5`
4. `Slice 3` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 5`
5. `Slice 4` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 7`
6. `Slice 5` -> Owner: `Experience & Bridge Owner` -> Target phase: `Phase 7`
7. `Slice 6` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 7`
8. `Slice 7` -> Owner: `Engine Adapter Owner` -> Target phase: `Phase 8` (optional)

## Current State Snapshot (measured)

### Direct `TopDownEngine` code usage

27 files (runtime code):

- `Assets/Scripts/Game/Core/Characters/DamageOnTouchController.cs`
- `Assets/Scripts/Game/Core/Characters/Enemy.cs`
- `Assets/Scripts/Game/Core/Characters/EnemyProjectileWeapon.cs`
- `Assets/Scripts/Game/Core/Characters/Unit.cs`
- `Assets/Scripts/Game/Core/Characters/UnitAIController.cs`
- `Assets/Scripts/Game/Core/Characters/WeaponManager.cs`
- `Assets/Scripts/Game/Core/EnemyRemover.cs`
- `Assets/Scripts/Game/Core/Skills/AttackSkill.cs`
- `Assets/Scripts/Game/Core/Skills/DistantAttackSkill.cs`
- `Assets/Scripts/Game/Core/Skills/DistantWeapon.cs`
- `Assets/Scripts/Game/Core/Skills/MeleeAttackSkill.cs`
- `Assets/Scripts/Game/Data/ElementsData.cs`
- `Assets/Scripts/Game/Data/EnemyData.cs`
- `Assets/Scripts/Game/Data/SkillData.cs`
- `Assets/Scripts/Game/Data/UnitData.cs`
- `Assets/Scripts/Game/MyExtension.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionMoveInDirection2D.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionMovementSpeed.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionTeleportToTarget.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionCharacterHit.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionDash2DReady.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionHealthPercent.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionMovementState.cs`
- `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionWeaponReady.cs`
- `Assets/Scripts/Game/TopDownEngineExt/LaserWeapon.cs`
- `Assets/Scripts/Game/UI/BattleUI/GameSettingsPopup.cs`
- `Assets/Scripts/Game/UI/LevelUI/LevelCheatsPanel.cs`

### MMTools event framework usage

59 files use `MMEventManager` / `MMEventListener` / `MMSingleton`.

### Content assets tied to TDE script GUIDs

41 assets (`prefab/scene/asset/controller`) reference TDE script GUIDs, including:

- Units: `Assets/Game/Prefabs/Units/Unit.prefab`, `Assets/Game/Prefabs/Units/NormalHero.prefab`, `Assets/Game/Prefabs/Units/GateHero.prefab`
- Enemies: `Assets/Game/Prefabs/Enemies/*.prefab`, `Assets/Game/Prefabs/Enemies/Brain.prefab`
- Weapons/skills: `Assets/Game/Prefabs/Enemies/Weapons/*.prefab`, `Assets/Game/Prefabs/Skills/*.prefab`
- Scene: `Assets/Game/Scenes/Main.unity`
- Damage type assets: `Assets/Game/Resources/DamageTypes/*.asset`

### TDE-specific MMFeedback types in content

17 prefabs use `MMF_TopDownEngineFloatingText`:

- `Assets/Game/Prefabs/Units/Unit.prefab`
- `Assets/Game/Prefabs/Units/NormalHero.prefab`
- `Assets/Game/Prefabs/Units/GateHero.prefab`
- `Assets/Game/Prefabs/Enemies/*.prefab` (boss/chieftain/medium/small variants)

### Assembly references now

- `Assets/Scripts/Game/Game.Runtime.asmdef` references `MoreMountains.TopDownEngine`, `MoreMountains.Tools`, `MoreMountains.InventoryEngine`, `MoreMountains.Interface`.
- `Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef` references `MoreMountains.TopDownEngine` and `MoreMountains.Tools`.

## Target End State

1. `Game.Runtime` has no reference to `MoreMountains.TopDownEngine`.
2. `Morboo.Integration.StrategyCombat` has no reference to `MoreMountains.TopDownEngine`.
3. `MMFeedbacks` remains available in `Assets/Scripts/Game/**`.
4. Gameplay runtime uses internal abstractions for:
   agent, movement, health, damage, weapon, AI state.
5. TDE-specific content types replaced, no missing scripts in scenes/prefabs.

## Execution Plan (commit slices)

## Slice 0: Baseline + Guard Rails

Scope:

1. Add architecture checks (or update existing tests) for:
   - no `MoreMountains.TopDownEngine` in packages
   - no `Morboo.Bridge` references in `Assets/Scripts/Game/**`
2. Capture baseline playtest scenarios:
   - unit follow/anchor behavior
   - enemy targeting and attack
   - level success/fail loop

Acceptance:

- Compile green.
- Tests green.
- Baseline video/checklist captured.

## Slice 1: Zero-risk cleanup (no behavior change)

Scope:

1. Remove unused TDE `using` from:
   - `Assets/Scripts/Game/UI/BattleUI/GameSettingsPopup.cs`
   - `Assets/Scripts/Game/UI/LevelUI/LevelCheatsPanel.cs`
   - `Assets/Scripts/Game/Data/EnemyData.cs` (if unused import remains)
2. In `Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef`, remove `MoreMountains.TopDownEngine` reference if compile remains green.

Acceptance:

- Compile green.
- Runtime behavior unchanged.

## Slice 2: Decouple data model from TDE enums/classes

Scope:

1. Replace TDE enum references in data:
   - `Assets/Scripts/Game/Data/UnitData.cs`
     - replace `MoreMountains.TopDownEngine.Weapon.WeaponStates attackState`
       with game-owned enum (`GameWeaponState`).
   - `Assets/Scripts/Game/Data/SkillData.cs`
     - replace `CharacterStates.CharacterConditions forcedState`
       with game-owned enum (`GameConditionState`).
2. Replace TDE `DamageType` in:
   - `Assets/Scripts/Game/Data/ElementsData.cs`
   - `Assets/Scripts/Game/MyExtension.cs` (typed damage helper signatures)
3. Add migration mapper for old assets to new fields (editor utility).

Acceptance:

- All `ScriptableObject` assets deserialize with no data loss.
- No TDE type names remain in these data files.

## Slice 3: Introduce gameplay runtime interfaces (adapter seam)

Scope:

1. Create game-owned interfaces in `Assets/Scripts/Game/Runtime/Abstractions/`:
   - `IAgentBrainTarget`
   - `IAgentMovement`
   - `IAgentHealth`
   - `IWeaponRuntime`
   - `IDamageRuntime`
2. Implement temporary TDE-backed adapters in `Assets/Scripts/Game/Runtime/TDEAdapters/`.
3. Refactor call sites to depend on interfaces, not TDE concrete classes:
   - `Assets/Scripts/Game/Core/Characters/Unit.cs`
   - `Assets/Scripts/Game/Core/Characters/Enemy.cs`
   - `Assets/Scripts/Game/Core/Characters/WeaponManager.cs`
   - `Assets/Scripts/Game/Core/Characters/UnitAIController.cs`
   - `Assets/Scripts/Game/Core/Characters/DamageOnTouchController.cs`
   - `Assets/Scripts/Game/Core/Characters/EnemyProjectileWeapon.cs`
   - `Assets/Scripts/Game/Core/EnemyRemover.cs`
   - `Assets/Scripts/Game/Core/Skills/AttackSkill.cs`
   - `Assets/Scripts/Game/Core/Skills/DistantAttackSkill.cs`
   - `Assets/Scripts/Game/Core/Skills/DistantWeapon.cs`
   - `Assets/Scripts/Game/Core/Skills/MeleeAttackSkill.cs`

Acceptance:

- Compile green.
- No behavior changes in baseline scenarios.
- No direct TDE references in above files (except adapter folder).

## Slice 4: Replace TopDownEngineExt AI nodes

Scope:

1. Replace AIAction/AIDecision-based nodes with game-owned AI logic.
2. Migrate files:
   - `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionMoveInDirection2D.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionMovementSpeed.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Actions/AIActionTeleportToTarget.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionCharacterHit.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionDash2DReady.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionHealthPercent.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionMovementState.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/Decisions/AIDecisionWeaponReady.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/LaserWeapon.cs`
   - `Assets/Scripts/Game/TopDownEngineExt/MyAIBrain.cs`
3. Keep orchestration command flow unchanged (`UnitCombatCommandExecutor`, `UnitIdleCommandExecutor`, `EnemyCombatCommandExecutor`).

Acceptance:

- Enemy and unit AI transitions behave as before.
- No `TopDownEngineExt` class depends on TDE.

## Slice 5: Content migration (prefabs/scenes)

Scope:

1. Replace TDE components on assets incrementally:
   - Units prefabs
   - Enemies prefabs
   - Skills/weapon prefabs
   - `Assets/Game/Scenes/Main.unity`
2. Replace `MMF_TopDownEngineFloatingText` with game/MMFeedback alternatives.
3. Migrate damage type assets from TDE `DamageType` to game-owned asset type.

Acceptance:

- No missing scripts in prefabs/scenes.
- Playtest scenarios green.
- UI floating text/feedback still works.

## Slice 6: Assembly cutover

Scope:

1. Remove `MoreMountains.TopDownEngine` from:
   - `Assets/Scripts/Game/Game.Runtime.asmdef`
   - `Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef`
2. Keep `MoreMountains.Tools` and `MoreMountains.Feedbacks` only if still required.
3. Remove obsolete TDE adapter code after cutover.

Acceptance:

- Compile green with no TDE assembly references in game/package asmdefs.
- Runtime smoke tests green.

## Slice 7: MMTools event bus optional migration (separate project)

Scope:

1. Replace MM event system usage (59 files) with game event bus.
2. Replace `MMSingleton` usage with explicit composition/lifetime ownership.

Acceptance:

- No `MMEventManager` / `MMEventListener` usage in gameplay code.
- Deterministic startup/lifecycle.

## Checks to run after each slice

1. Code scans:

```bash
rg -n "MoreMountains\.TopDownEngine" Assets/Scripts Packages/com.morboo.* --glob "*.cs"
rg -n "MMF_TopDownEngine" Assets/Game --glob "*.prefab" --glob "*.asset" --glob "*.unity"
rg -n "MoreMountains\.TopDownEngine" Assets/Scripts/Game/Game.Runtime.asmdef Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef
```

2. Scene/prefab health:

- Open `Assets/Game/Scenes/Main.unity`
- Check Unit/Enemy prefabs for missing scripts

3. Gameplay smoke:

- Units keep formation/attraction points
- Enemy targeting + attacks
- Level win/lose transitions

## Recommended immediate first PR

Small PR, low risk:

1. Slice 1 only (unused imports + asmdef cleanup).
2. Add this backlog file.
3. Add/adjust architecture test for package-level TDE dependency ban.
