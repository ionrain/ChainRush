# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
| `BoardEvent` | Cell grid setup |
| `CellEvent` | Cell tap/open/reveal |
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

- **LevelData** - Level config with stages, board size, enemy waves
- **UnitData** - Character stats, skills, merge states
- **LocationData** - Groups of levels with progression
- **ResourcesData** - Economy with production system

### Board/Cell System

`Assets/Game/Scripts/Core/Board/`

- **Board.cs** - Grid management, input handling, flood fill
- **Cell.cs** - Individual cell with types: Empty, Trap, Unit, Booster, Loot, Block, Open
- **CellUnit/CellTrap/CellItem** - Specialized cell behaviors

Cell positions use `Vector2Int`. Neighbor detection via `IsNear()` (8-directional).

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

## Patterns to Follow

1. **New systems**: Create event struct, use MMEventListener pattern
2. **New managers**: Inherit from `MMSingleton<T>`
3. **Persistence**: Extend `GameData`, hook into `GameSettingsEvent`
4. **Game content**: Use ScriptableObjects, not hardcoded values
5. **Resource tracking**: Use `ResourceSource`/`ResourceTarget` enums for analytics
