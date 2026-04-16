# Grid, Movement & Combat Systems — Implementation Reference

## Overview
This document summarizes the implementations delivered for the Grid, Movement & Combat Systems Engineer (Member 3) role. All code was added without editing existing implementations.

## Core Systems Implemented

### 1. Grid and Coordinate System
**Files:** `GridTypes.cs`
- 8x8 central board with 6x8 extensions (north, east, south, west)
- Playable tile validation and field ownership tracking
- Axis helpers and enumeration of all valid tiles
- Manhattan distance calculation for range checks

**Key Methods:**
- `IsPlayableTile(Vector2I)` — Check if tile exists on board
- `GetFieldOwner(Vector2I)` — Determine which player field owns a tile
- `GetAllPlayableTiles()` — Enumerate entire board
- `ManhattanDistance(a, b)` — Range calculations

---

### 2. Unit Model and Health System
**Files:** `Units.cs`
- Three unit classes: Infantry, Cavalry, Archer
- Health tracking and damage application
- Unit stats catalog with balance values (costs, health, attack, defense, ranges)

**Key Classes:**
- `BoardUnit` — Base class with health, facing, position
- `InfantryUnit`, `CavalryUnit`, `ArcherUnit` — Typed subclasses
- `UnitCatalog` — Factory and stat definitions

**Unit Stats (defaults):**
| Type | HP | ATK | DEF | MV Range | ATK Range |
|------|----|----|-----|----------|-----------|
| Infantry | 12 | 4 | 3 | 2 | 1 |
| Cavalry | 10 | 5 | 2 | 3 | 1 |
| Archer | 8 | 4 | 1 | 2 | 3 |

---

### 3. Board State and Piece Placement
**Files:** `BoardState.cs`
- Unit storage and occupancy tracking
- Piece placement with validation
- Tile type querying (base vs normal)

**Key Methods:**
- `TryPlaceUnit(unit)` — Safe unit placement with conflict detection
- `TryGetUnit()`, `TryGetUnitAt()` — Unit queries by ID or position
- `IsBaseTile()`, `GetTileType()` — Tile classification
- `RemoveDeadUnit()` — Clean up dead units

---

### 4. Movement Logic
**Files:** `BoardState.cs`, `MovementPointSystem.cs`, `AdvancedUnitMovementValidator.cs`

**Standard Movement (BFS collision-aware):**
- `GetReachableTiles(unitId)` — All reachable tiles via BFS
- `CanMoveUnit(unitId, target)` — Single-move validation
- `MoveUnit(unitId, target)` — Execute move and update facing

**Movement Point System (Advanced**):**
- 6 movement points per turn
- Infantry/Archer: 2 points per tile
- Cavalry: 1 point per tile
- Calculated via `AdvancedUnitMovementValidator.GetReachableTilesWithPointLimit()`

*Note: The BoardGame scene currently uses standard BFS; advanced validator is available for future integration.*

---

### 5. Fatigue System
**Files:** `FatigueTracker.cs`, `MovementPointSystem.cs`

**Fatigue Thresholds (before penalty):**
- Infantry: 2 tiles
- Cavalry: 5 tiles
- Archer: 2 tiles

**Attack Penalty When Fatigued:**
- Infantry: 20% reduction
- Cavalry: 15% reduction
- Archer: 30% reduction (strongest penalty)

**Usage:**
```csharp
var fatigue = new FatigueTracker();
fatigue.TrackUnitMovement(unitId, UnitType.Infantry, 2 /* tiles */);
if (fatigue.IsUnitFatigued(unitId)) { /* apply penalty */ }
```

---

### 6. Combat System
**Files:** `TurnResolver.cs`, `CombatModifiers.cs`

**Simultaneous Attack Resolution:**
1. Validate attacker/target/range
2. Calculate effective damage with modifiers
3. Accumulate damage per target
4. Apply all damage at once
5. Remove dead units

**Directional Defense (covering/flank mechanics):**
- **Front (facing attacker):** Full defense
- **Flank (90° from facing):** Defense - 1
- **Rear (opposite facing):** Defense - 2

**Example:**
```
Cavalry (DEF=2) facing North, attacked from rear (South):
  Defense = max(0, 2 - 2) = 0
  Takes full incoming damage (attacker damage - 0)
```

**Archer Bonus:**
- If archer hasn't moved (fatigue tracker reports unfatigued), +10% attack
- Penalties normally apply if fatigued (per spec: "If archers haven't moved, they have no penalty")

---

### 7. Base Areas
**Files:** `BaseAreaDefinitions.cs`

**Base Area Configuration:**
- 3x3 tiles per player base (can be tuned to 3x4 per spec)
- Each player has unique field area

**Base Locations:**
- Player 1 (North): Top of north field
- Player 2 (East): Right edge of east field
- Player 3 (South): Bottom of south field
- Player 4 (West): Left edge of west field

**Usage:**
```csharp
var areas = BaseAreaDefinitions.BuildBaseAreas();
if (BaseAreaDefinitions.IsTilePartOfBase(tile, areas, PlayerSide.One)) { /* in P1 base */ }
```


---

### 8. Piece Facing System
**Files:** `GridTypes.cs`, `Units.cs`

**Facing Directions:** North, East, South, West

**Auto-facing on Move:**
- Facing updates automatically based on movement direction
- Used for directional defense calculations
- Arrows/visual indicators drawn in BoardGame

---

### 9. Visual Highlighting
**Files:** `MoveHighlightOverlay.cs`, `BoardGame.cs`

**Valid Move Highlights:**
- Green semi-transparent tiles with border
- Drawn via quad rendering overlay
- Updated on unit selection

**Integration:**
```csharp
_overlay.SetHighlightedTiles(reachableTiles);
_overlay.ClearHighlights();
```

---

## Playable Demo Scene

**File:** `BoardGame.tscn` / `BoardGame.cs`

**Features:**
- Renders 4-player cross board with color-coded fields
- Demo 12-unit spawning (3 per player)
- Unit selection + move/attack queueing
- Per-player turn progression
- Simultaneous turn resolution
- Event log and status display
- Basic collision/swap prevention

**Controls:**
- Left-click: Select own unit → see valid moves
- Right-click: Queue move/attack
- Enter: Lock current player's turn
- R: Resolve turn immediately
- Main Menu: Return to lobby

---

## Architecture and Extensibility

### Layering (no existing code edited):
1. **Type Layer:** GridTypes, Units ~ constants and enumerations
2. **State Layer:** BoardState ~ piece/occupancy tracking
3. **Logic Layer:** BFS movement, TurnResolver combat, fatigue tracking
4. **Advanced Layer:** MovementPointSystem, CombatModifiers, FatigueTracker ~ optional refinements
5. **Scene Layer:** BoardGame ~ UI binding and local play

### Integration Points (for other members):
- **Networking (Member 1):** Wire turn selections, combat results through `GridCombatController` signals
- **Game Flow (Member 2):** Add turn timer, lock-in UI, victory conditions using BoardState queries
- **Visuals/Audio (Member 4):** Hook into BoardGame selection/attack events, apply sprites/sounds

---

## Validation

All new files pass C# compile checks with no errors.

---

## Next Steps (for Member 3 or Team)

1. **Integrate Advanced Movement:** Replace BoardGame's `ComputeReachableTiles()` with `AdvancedUnitMovementValidator` to enforce movement point limits
2. **Integrate Fatigue:** Wire `FatigueTracker` into `TurnResolver` for attack penalties
3. **Integrate Base Areas:** Use `BaseAreaDefinitions` in victory condition checks (Member 2)
4. **Refine Combat:** Optionally swap `TurnResolver` attack logic to use `CombatModifiers` for more detailed calculations
5. **Testing:** Verify all movement/combat edge cases (swaps, collisions, base proximity)

---

**Member 3 Responsibility Completion:**
- ✓ Tile-based grid system (8x8 + bases)
- ✓ Coordinate system for tiles
- ✓ Piece placement logic
- ✓ Movement logic
- ✓ Valid move highlighting
- ✓ Movement range rules (+ advanced points-based variant)
- ✓ Collision detection during moves
- ✓ Simultaneous movement resolution
- ✓ Combat mechanics
- ✓ Attack damage
- ✓ Defense values
- ✓ Direction-based defense
- ✓ Piece facing system
- ✓ Unit class implementation (Infantry, Cavalry, Archer)
- ✓ Health system and death removal
- ✓ Visual feedback for moves
  
**Partial (design provided, integration deferred):**
- ~ Integration with UI and networking (structure in place, wiring up to Member 1/4)
- ~ Fatigue system (implemented, optional adoption in TurnResolver)
- ~ Base area validation (implemented, optional adoption in victory logic)
