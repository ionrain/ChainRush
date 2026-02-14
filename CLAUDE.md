# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.


## Code Documentation Expectations

Document **invariants and reasoning**, not trivial implementation details.

### Where comments are required
- New public types and interfaces: add short XML summaries explaining purpose and key invariants.
- Data-contract structs (IDs, snapshots, commands): document default values and constraints (e.g., `DeadlineSeconds = -1` means “no deadline”; IDs are case-sensitive).
- Planners/arbiters/scoring code: include `RATIONALE:` comments explaining why a heuristic exists and what problem it prevents (e.g., ping-pong, clumping).
- Adapters/Executors: must document **ownership** (which system owns target/movement/steering) and how conflicts are avoided.

### Comment tags (preferred)
- `IMPORTANT:` invariants, ownership rules, dependency boundaries.
- `RATIONALE:` heuristics, design trade-offs, why-not alternatives.
- `PERF:` performance constraints (no LINQ, no per-frame allocations, linear scans by design).

### Style constraints
- Keep comments short and actionable.
- Don’t comment obvious lines (null-checks, simple setters).
- If a rule is critical (e.g., “Core must not reference Domains”), repeat it in the relevant files as `IMPORTANT:`.

### Refactor hygiene
- Keep compilation green at each step.
- Prefer: add/replace/update references first → delete old types last.



## Project Overview

ChainRush is a Unity 6 mobile game (iOS/Android) using URP. It combines a minesweeper-like board mechanic with auto-battler combat. The codebase extends MoreMountains' TopDownEngine framework.

## Key Commands

```bash
# Open project in Unity (requires Unity 6 with URP)
# Use Unity Hub or: /Applications/Unity/Hub/Editor/6000.0.x/Unity.app/Contents/MacOS/Unity -projectPath .

# Generate IDE project files
# Unity Editor: Edit > Preferences > External Tools > Regenerate project files
```

No command-line build system - builds are done through Unity Editor (File > Build Settings).

## Architecture

### Event-Driven Communication

All systems communicate via **MMEventManager** (MoreMountains pattern). Events are structs with static `Trigger()` methods:

```csharp
// Trigger an event
LevelLoadEvent.Trigger(EventStage.Start, levelData);

// Listen to events - implement MMEventListener<T>
public class MyClass : MonoBehaviour, MMEventListener<LevelLoadEvent> {
    void OnEnable() => this.MMEventStartListening<LevelLoadEvent>();
    void OnDisable() => this.MMEventStopListening<LevelLoadEvent>();
    public void OnMMEvent(LevelLoadEvent e) { /* handle */ }
}
```

**EventStage** enum: `Start` → `Process` → `End` (3-phase pattern for all events)

### Core Events

| Event | When |
|-------|------|
| `LevelLoadEvent` | Level initialization |
| `LevelStageStateEvent` | Stage transitions (Start/Battle/ClearBonus/Complete) |
| `LevelResultEvent` | Level success/failure |
| `LevelProgressEvent` | Level time progress (0-1) for dynamic difficulty |
| `BoardEvent` | Cell grid setup (Core Board) |
| `BoardUiEvent` | UI board setup/items (SetupCells, SetupItems, Ready) |
| `CellEvent` | Cell tap/open/reveal |
| `CellUiItemSelectEvent` | Player selects cells in UI (Item + Count) |
| `InputEvent` | Press/Release/Tap/Move |
| `PartyUnitEvent` | Unit create/merge/death |
| `EnemySpawnEvent` | Enemy wave spawning |
| `EarnResourceEvent` / `SpendResourceEvent` | Economy |

### Manager Singletons

14 managers inherit from `MMSingleton<T>`:

- **GameManager** - Save/load, global settings
- **LevelManager** - Level loading, results
- **LevelStageManager** - Stage state machine (17 listeners depend on it)
- **PartyManager** - Player units, buffs/debuffs
- **EnemyManager** - Enemy spawning, waves
- **GameResourcesManager** - Currency (Soft/Hard/Energy/Cards/Bolts)

### Data Layer

All game content is ScriptableObject-based in `Assets/Game/Scripts/Data/`:

- **LevelData** - Level config with stages, board size, enemy waves, difficulty reference
- **LevelDifficultyData** - Board refresh settings, patterns, item distribution
- **UnitData** - Character stats, skills, merge states
- **LocationData** - Groups of levels with progression
- **ResourcesData** - Economy with production system
- **BuffsData** - Buff grades per attribute
- **BoostersData** - Booster types with multipliers

### Board/Cell System

`Assets/Game/Scripts/Core/Board/`

- **Board.cs** - Grid management, input handling, flood fill
- **Cell.cs** - Individual cell with types: Empty, Trap, Unit, Booster, Loot, Block, Open
- **CellUnit/CellTrap/CellItem** - Specialized cell behaviors

Cell positions use `Vector2Int`. Neighbor detection via `IsNear()` (8-directional).

### BoardUI System

`Assets/Game/Scripts/UI/BattleUI/`

- **BoardUI.cs** - UI grid with pattern generation and item distribution
- **CellUI.cs** - Individual UI cell with drag/swipe selection

**Pattern System:**
- Patterns (SelectOne, Line, Corner, Box, Zigzag) defined in `LevelDifficultyData.cellPatterns`
- All cells in a pattern share the same `CellUiItem`
- Players swipe to select adjacent cells with matching items

**Item Distribution (`ApplyPatternsToBoard`):**
1. `alwaysAvailableOnRefresh` - guaranteed fractions (e.g., Unit=0.5 means 50% patterns get Unit)
2. `refreshCooldowns` - types with cooldowns fill remaining patterns when ready
3. Fallback to `alwaysAvailableOnRefresh` types if no cooldown-ready types

**Cell Selection Events:**
```csharp
// CellUiItemSelectEvent triggered when player completes selection
public struct CellUiItemSelectEvent {
    public CellUiItem Item;  // Type, Icon, Id
    public int Count;        // Number of selected cells
}
```

**PartyManager handles selection:**
- `CellItemType.Unit` → Creates units with `UnitMergeState` based on count (5 cells = Fifth)
- `CellItemType.Buff` → Applies buff with `Grade` based on count (7+ cells = Divine)

### Level Flow

1. `LevelLoadEvent(Start)` → Initialize managers
2. `LevelStageStateEvent(Start)` → Board setup
3. `LevelStageStateEvent(Battle)` → Combat phase, board hides
4. `LevelStageStateEvent(ClearBonus)` → Optional bonus
5. `LevelStageStateEvent(Complete)` → Stage done
6. `LevelResultEvent` → Final result with rewards

### Goal System

`Assets/Game/Scripts/Core/LevelGoals/`

- **LevelGoalManager** - Tracks goal completion
- **LevelGoalType** enum: `Traps`, `Survive`, `Stage`
- Goals trigger `LevelGoalResultEvent` on completion

## Key Directories

```
Assets/Game/Scripts/
├── Core/Board/          # Grid/cell mechanics
├── Core/Characters/     # Unit and Enemy classes
├── Core/LevelGoals/     # Win conditions
├── Core/Skills/         # Skill system
├── Data/                # ScriptableObjects (46 classes)
├── Managers/            # 14 singleton managers
├── UI/                  # 62 UI scripts
├── Input/               # InputManager, InputEvent
└── TopDownEngineExt/    # Framework extensions
```

## Dependencies

- **MoreMountains**: TopDownEngine, Feedbacks, Tools, Interface, InventoryEngine
- **Spine**: 2D skeletal animation
- **Odin Inspector**: Enhanced serialization (`SerializedScriptableObject`)
- **DOTween**: Animation tweening
- **Addressables**: Asset streaming
- **Unity Localization**: Multi-language support

## Key Enums

```csharp
// Cell item types for BoardUI
public enum CellItemType { None, Unit, Buff, Booster, SoftCurrency }

// Pattern shapes for cell generation
public enum CellSelectPatternType { None, SelectOne, Line, Corner, Box, Zigzag }

// Unit power levels (5 tiers, index 0-4)
public enum UnitMergeState { First, Second, Third, Forth, Fifth }

// Buff/item quality grades (7 tiers, index 0-6)
public enum Grade { Common, Uncommon, Rare, Epic, Legendary, Mythical, Divine }
```

## LevelDifficultyData Structure

```csharp
public class LevelDifficultyData : SerializedScriptableObject {
    // Global board refresh interval (AnimationCurve based on level progress 0-1)
    public AnimationCurve refreshInterval;

    // Pattern types and their max sizes
    public Dictionary<CellSelectPatternType, int> cellPatterns;

    // Per-type cooldowns (AnimationCurve based on progress)
    public Dictionary<CellItemType, AnimationCurve> refreshCooldowns;

    // Guaranteed fractions (0.5 = 50% of patterns get this type)
    public Dictionary<CellItemType, float> alwaysAvailableOnRefresh;
}
```

## Patterns to Follow

1. **New systems**: Create event struct, use MMEventListener pattern
2. **New managers**: Inherit from `MMSingleton<T>`
3. **Persistence**: Extend `GameData`, hook into `GameSettingsEvent`
4. **Game content**: Use ScriptableObjects, not hardcoded values
5. **Resource tracking**: Use `ResourceSource`/`ResourceTarget` enums for analytics
6. **BoardUI items**: Add type to `CellItemType`, handle in `CreateItemForType()` and `PartyManager`
