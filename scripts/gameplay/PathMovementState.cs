using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tracks the in-progress path a player is building for a single unit during
/// the Movement Selection phase.
///
/// Usage flow:
///   1. Create with the unit being moved and the current board occupancy.
///   2. Call GetFrontierTiles() to get the highlighted "next step" options.
///   3. Call TryExtendPath(tile) when the player clicks a frontier tile.
///   4. Check IsComplete to know if the unit can no longer move.
///   5. Call GetFinalDestination() to get where the unit ends up (last tile in path).
///   6. The path can be cancelled/reset externally by discarding this object.
/// </summary>
public sealed class PathMovementState
{
	private readonly BoardUnit _unit;
	private readonly IReadOnlyDictionary<Vector2I, string> _boardOccupancy;

	// Ordered list of tiles the player has committed to so far (not including starting tile).
	private readonly List<Vector2I> _path = new List<Vector2I>();

	// Movement-point budget remaining.
	private int _movementPointsRemaining;

	// Tiles-moved count for fatigue tracking.
	private int _tilesMoved;

	public PathMovementState(BoardUnit unit, IReadOnlyDictionary<Vector2I, string> boardOccupancy)
	{
		_unit = unit;
		_boardOccupancy = boardOccupancy;
		_movementPointsRemaining = MovementPointSystem.TotalMovementPointsPerTurn;
		_tilesMoved = 0;
	}

	/// <summary>The starting tile of the unit (never changes).</summary>
	public Vector2I Origin => _unit.Position;

	/// <summary>The current tip of the built path (or origin if no steps yet).</summary>
	public Vector2I CurrentTip => _path.Count > 0 ? _path[_path.Count - 1] : Origin;

	/// <summary>All tiles the player has already committed to (does not include origin).</summary>
	public IReadOnlyList<Vector2I> CommittedPath => _path;

	/// <summary>True when no more steps can be added to the path.</summary>
	public bool IsComplete => GetFrontierTiles().Count == 0;

	/// <summary>
	/// Returns the 1–3 immediately adjacent tiles the player can step to next.
	/// Excludes:
	///   - The tile the unit just came from (no backtracking).
	///   - Out-of-bounds tiles.
	///   - Occupied tiles.
	///   - Tiles that would cost more movement points than remain.
	///   - Any tile if the unit has already hit its fatigue cap.
	/// </summary>
	public IReadOnlyList<Vector2I> GetFrontierTiles()
	{
		// Can't move any further if out of points or at fatigue limit.
		int costPerTile = MovementPointSystem.GetMovementCostPerTile(_unit.Stats.Type);
		int fatigueLimit = MovementPointSystem.GetMaxTilesBeforeFatigue(_unit.Stats.Type);

		if (_movementPointsRemaining < costPerTile || _tilesMoved >= fatigueLimit)
		{
			return System.Array.Empty<Vector2I>();
		}

		// The tile we just came from — block this direction to prevent backtracking.
		Vector2I? previousTile = _path.Count >= 2
			? _path[_path.Count - 2]
			: (_path.Count == 1 ? Origin : (Vector2I?)null);

		List<Vector2I> frontier = new List<Vector2I>(4);
		foreach (Vector2I dir in GridTypes.CardinalDirections)
		{
			Vector2I candidate = CurrentTip + dir;

			// No backtracking.
			if (previousTile.HasValue && candidate == previousTile.Value)
			{
				continue;
			}

			// Must be a valid playable tile.
			if (!GridTypes.IsPlayableTile(candidate))
			{
				continue;
			}

			// Cannot step onto an occupied tile (other units block passage).
			if (_boardOccupancy.ContainsKey(candidate) && _boardOccupancy[candidate] != _unit.Id)
			{
				bool isMeleeUnit = _unit.Stats.Type == UnitType.Cavalry || _unit.Stats.Type == UnitType.Infantry;
				if (!isMeleeUnit)
				{
					continue;
				}
			}

			frontier.Add(candidate);
		}

		return frontier;
	}

	/// <summary>
	/// Attempts to add the given tile as the next step in the path.
	/// Returns true if the tile was a valid frontier tile and was added.
	/// </summary>
	public bool TryExtendPath(Vector2I tile)
	{
		IReadOnlyList<Vector2I> frontier = GetFrontierTiles();
		if (!frontier.Contains(tile))
		{
			return false;
		}

		int costPerTile = MovementPointSystem.GetMovementCostPerTile(_unit.Stats.Type);
		_path.Add(tile);
		_movementPointsRemaining -= costPerTile;
		_tilesMoved += 1;
		return true;
	}

	/// <summary>
	/// Returns true if the given tile is the origin or any tile in the committed path.
	/// </summary>
	public bool IsPathTile(Vector2I tile)
	{
		return tile == Origin || _path.Contains(tile);
	}

	/// <summary>
	/// Trims the path back so that <paramref name="tile"/> becomes the new tip.
	/// If <paramref name="tile"/> is the origin, the entire path is cleared.
	/// Movement points and fatigue steps are refunded for every removed step.
	/// Returns true if any trimming occurred.
	/// </summary>
	public bool TryTrimToTile(Vector2I tile)
	{
		// Trim to origin — clear everything.
		if (tile == Origin)
		{
			int costPerTile = MovementPointSystem.GetMovementCostPerTile(_unit.Stats.Type);
			_movementPointsRemaining += costPerTile * _path.Count;
			_tilesMoved -= _path.Count;
			if (_tilesMoved < 0) _tilesMoved = 0;
			_path.Clear();
			return true;
		}

		int index = _path.IndexOf(tile);
		if (index < 0)
		{
			return false; // tile is not in the path
		}

		// Remove everything after this index.
		int stepsToRemove = _path.Count - 1 - index;
		if (stepsToRemove <= 0)
		{
			return false; // already the tip, nothing to trim
		}

		int costPerTile2 = MovementPointSystem.GetMovementCostPerTile(_unit.Stats.Type);
		_movementPointsRemaining += costPerTile2 * stepsToRemove;
		_tilesMoved -= stepsToRemove;
		if (_tilesMoved < 0) _tilesMoved = 0;
		_path.RemoveRange(index + 1, stepsToRemove);
		return true;
	}

	/// <summary>
	/// Returns the final destination tile (last tile in the committed path),
	/// or the unit's origin if no steps have been made.
	/// </summary>
	public Vector2I GetFinalDestination()
	{
		return _path.Count > 0 ? _path[_path.Count - 1] : Origin;
	}

	/// <summary>True if at least one step has been committed.</summary>
	public bool HasPath => _path.Count > 0;
}