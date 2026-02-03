# Input → Effect Mapping

## Selection Adjacency Rule
- Allowed directions: up, down, left, right
- Diagonal connections are NOT allowed
- Each tile can be selected only once per chain

## A) Tap single tile
Input:
- Tap one tile

Effect:
- Claim reward at tier = 1
- All the field is dimmed until refresh [TBD]

Feedback:
- Immediate tile highlight + “claimed” animation
- Popup with reward description (must be skippable quickly)

## B) Swipe chain
Input:
- Start on a tile, swipe across adjacent tiles
- Only same-content tiles can be included

Constraints:
- Movement adjacency: 4-neighbor or 8-neighbor [TBD]
- No revisiting a tile
- Break on mismatch

Effect:
- Claim reward with tier based on chainLength (see balance rules)
- All the field is dimmed until refresh [TBD]

Feedback:
- Chain path render
- On break/end: reward burst + popup