# Core Gameplay Spec

## Screen Layout
- Top: auto-battle lane (player side on left, enemies enter from right)
- Bottom: square grid (e.g. 4x4 or 6x6) with timed refresh

## Battle Setup (Core)
- Player selects:
  - 1 Hero
  - up to 4 Units (roster/carry limit for the run)
- Units + hero fight automatically.
- Protected object is located on the far left. It defines defeat.

## Grid Refresh
- All grid cells refresh simultaneously after a countdown.
- During countdown, player can select tiles and claim rewards.
- After claiming a reward, a popup explains the reward; after confirmation, the grid refresh timer begins (or continues, depending on UX decision).

## Tile Selection Rules
- Player starts selection with a tile (tap or swipe start).
- Swipe can extend only through tiles with the SAME content as the start tile.
- Selection breaks immediately if player:
  - touches a different content tile,
  - revisits an already selected tile,
  - violates movement constraints (TBD: allow diagonals or not).
- On break/end, player receives reward based on number of selected tiles.

## Reward Scaling by Chain Length
- If chainLength = 1 → base tier reward
- If chainLength >= 2..maxTier → higher tier reward
- If chainLength > maxTier:
  - reward becomes: maxTier reward (N) + remainder
  - (Example: "Two Level 3 units" + Level 2 unit)