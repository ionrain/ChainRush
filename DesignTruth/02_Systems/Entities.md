# Entities

## Hero
- Exists: exactly 1 per run
- Participates in auto-battle
- Has abilities (some can appear as grid tiles)
- Has stats (HP/ATK/DEF/etc.) affected by upgrades

## Player Unit (Slime-based)
- Up to 4 slots in run roster (units can be spawned/leveled via grid)
- Unit tier/level is driven by chain selection
- Each unit has:
  - Role (tank/dps/support)
  - Attack type (melee/ranged/magic)
  - Stats (HP/ATK/AS/Range/Crit/etc.)
  - AI state machine (separate spec)

## Enemy
- Spawns in waves from the right
- Moves left towards player side / protected object
- Has archetype behavior (separate spec)

## Protected Object (Base/Target)
- Located on the far left
- Loss condition: destroyed

## Grid Tile Object Types
- Unit tile
- Upgrade tile (stat buff)
- Hero ability tile
- Booster tile (time slow, heal, screen nuke, etc.)
- Gold tile