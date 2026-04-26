using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BoardState
{
	private readonly Dictionary<string, BoardUnit> _unitsById = new Dictionary<string, BoardUnit>();
	private readonly Dictionary<Vector2I, HashSet<string>> _occupancy = new Dictionary<Vector2I, HashSet<string>>();
	private readonly HashSet<Vector2I> _baseTiles = new HashSet<Vector2I>();
	// Tracks which player sides have ever had at least one unit placed, so we can distinguish
	// "not yet in the game" from "was eliminated this turn".
	private readonly HashSet<PlayerSide> _sidesWithUnitsEver = new HashSet<PlayerSide>();

	/// <summary>Returns true if this player side has ever had at least one unit placed on the board.</summary>
	public bool HasEverHadUnits(PlayerSide side) => _sidesWithUnitsEver.Contains(side);

	public BoardState()
	{
		var baseAreas = BaseAreaDefinitions.BuildBaseAreas();
		foreach (var baseArea in baseAreas.Values)
		{
			foreach (Vector2I tile in baseArea)
			{
				_baseTiles.Add(tile);
			}
		}
	}

	public IEnumerable<BoardUnit> Units => _unitsById.Values;

	public bool IsBaseTile(Vector2I coord)
	{
		return _baseTiles.Contains(coord);
	}

	public TileType GetTileType(Vector2I coord)
	{
		return IsBaseTile(coord) ? TileType.Base : TileType.Normal;
	}

	public bool TryGetUnit(string id, out BoardUnit unit)
	{
		return _unitsById.TryGetValue(id, out unit);
	}

	public bool TryGetUnitAt(Vector2I coord, out BoardUnit unit)
	{
		unit = null;
		if (!_occupancy.TryGetValue(coord, out HashSet<string> ids) || ids.Count == 0)
		{
			return false;
		}

		string firstId = ids.First();
		return _unitsById.TryGetValue(firstId, out unit);
	}

	public bool TryGetUnitsAt(Vector2I coord, out List<BoardUnit> units)
	{
		units = new List<BoardUnit>();
		if (!_occupancy.TryGetValue(coord, out HashSet<string> ids) || ids.Count == 0)
		{
			return false;
		}

		foreach (string id in ids)
		{
			if (_unitsById.TryGetValue(id, out BoardUnit unit))
			{
				units.Add(unit);
			}
		}

		return units.Count > 0;
	}

	public IEnumerable<Vector2I> GetOccupiedTiles()
	{
		return _occupancy.Keys;
	}

	public bool TryPlaceUnit(BoardUnit unit, out string reason)
	{
		reason = string.Empty;
		if (unit == null)
		{
			reason = "Unit is null.";
			return false;
		}

		if (_unitsById.ContainsKey(unit.Id))
		{
			reason = $"Unit id '{unit.Id}' already exists.";
			return false;
		}

		if (!GridTypes.IsInBounds(unit.Position))
		{
			reason = $"Position {unit.Position} is out of bounds.";
			return false;
		}

		if (_occupancy.TryGetValue(unit.Position, out HashSet<string> ids) && ids.Count > 0)
		{
			reason = $"Tile {unit.Position} is occupied.";
			return false;
		}

		_unitsById[unit.Id] = unit;
		AddOccupancy(unit.Position, unit.Id);
		_sidesWithUnitsEver.Add(unit.Owner);
		return true;
	}

	public bool RemoveDeadUnit(string id)
	{
		if (!_unitsById.TryGetValue(id, out BoardUnit unit))
		{
			return false;
		}

		_unitsById.Remove(id);
		RemoveOccupancy(unit.Position, id);
		return true;
	}

	/// <summary>
	/// Forcibly removes any unit by ID regardless of alive status.
	/// Used to clear an eliminated player's remaining pieces from the board.
	/// </summary>
	public bool ForceRemoveUnit(string id)
	{
		if (!_unitsById.TryGetValue(id, out BoardUnit unit))
		{
			return false;
		}

		_unitsById.Remove(id);
		RemoveOccupancy(unit.Position, id);
		return true;
	}

	public bool CanMoveUnit(string unitId, Vector2I target, out string reason)
	{
		reason = string.Empty;
		if (!TryGetUnit(unitId, out BoardUnit unit))
		{
			reason = "Unit does not exist.";
			return false;
		}

		if (!unit.IsAlive)
		{
			reason = "Unit is dead.";
			return false;
		}

		if (!GridTypes.IsInBounds(target))
		{
			reason = "Target is out of bounds.";
			return false;
		}

		if (target == unit.Position)
		{
			reason = "Target equals current position.";
			return false;
		}

		ISet<Vector2I> reachable = GetReachableTiles(unitId, includeOccupiedTiles: true);
		if (!reachable.Contains(target))
		{
			reason = "Target is not reachable by movement rules.";
			return false;
		}

		if (_occupancy.TryGetValue(target, out HashSet<string> occupantIds) && occupantIds.Count > 0)
		{
			foreach (string occupantId in occupantIds)
			{
				if (!_unitsById.TryGetValue(occupantId, out BoardUnit occupant) || !occupant.IsAlive)
				{
					continue;
				}

				if (occupant.Owner == unit.Owner)
				{
					reason = "Target tile has a friendly unit.";
					return false;
				}
			}

			if (unit.Stats.Type == UnitType.Archer)
			{
				reason = "Archers cannot move onto occupied tiles.";
				return false;
			}
		}

		return true;
	}

	public bool MoveUnit(string unitId, Vector2I target, out string reason)
	{
		if (!CanMoveUnit(unitId, target, out reason))
		{
			return false;
		}

		BoardUnit unit = _unitsById[unitId];
		Vector2I oldPos = unit.Position;
		Vector2I delta = target - oldPos;
		unit.Position = target;
		unit.Facing = GridTypes.VectorToFacing(delta);

		RemoveOccupancy(oldPos, unit.Id);
		AddOccupancy(target, unit.Id);
		return true;
	}

	// Breadth-first search for movement-range rules with collision checking.
	public ISet<Vector2I> GetReachableTiles(string unitId, bool includeOccupiedTiles)
	{
		HashSet<Vector2I> reachable = new HashSet<Vector2I>();
		if (!TryGetUnit(unitId, out BoardUnit unit) || !unit.IsAlive)
		{
			return reachable;
		}

		int maxTiles = MovementPointSystem.GetMaxTilesPerTurn(unit.Stats.Type);

		Queue<(Vector2I coord, int cost)> frontier = new Queue<(Vector2I coord, int cost)>();
		Dictionary<Vector2I, int> bestCost = new Dictionary<Vector2I, int>();

		frontier.Enqueue((unit.Position, 0));
		bestCost[unit.Position] = 0;

		while (frontier.Count > 0)
		{
			(Vector2I coord, int cost) = frontier.Dequeue();

			foreach (Vector2I dir in GridTypes.CardinalDirections)
			{
				Vector2I next = coord + dir;
				int nextCost = cost + 1;

				if (nextCost > maxTiles || !GridTypes.IsInBounds(next))
				{
					continue;
				}

				if (bestCost.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
				{
					continue;
				}

				bool occupied = IsOccupiedByOtherUnit(next, unit.Id);
				if (occupied && !includeOccupiedTiles)
				{
					continue;
				}

				bestCost[next] = nextCost;
				reachable.Add(next);

				if (!occupied)
				{
					frontier.Enqueue((next, nextCost));
				}
			}
		}

		reachable.Remove(unit.Position);
		return reachable;
	}

	public IReadOnlyDictionary<Vector2I, string> SnapshotOccupancy()
	{
		Dictionary<Vector2I, string> snapshot = new Dictionary<Vector2I, string>();
		foreach (KeyValuePair<Vector2I, HashSet<string>> entry in _occupancy)
		{
			if (entry.Value.Count > 0)
			{
				snapshot[entry.Key] = entry.Value.First();
			}
		}

		return snapshot;
	}

	public IEnumerable<BoardUnit> GetUnitsForPlayer(PlayerSide player)
	{
		return _unitsById.Values.Where(unit => unit.Owner == player && unit.IsAlive);
	}

	public int CountAliveUnits(PlayerSide player)
	{
		return _unitsById.Values.Count(unit => unit.Owner == player && unit.IsAlive);
	}

	private bool IsOccupiedByOtherUnit(Vector2I coord, string unitId)
	{
		if (!_occupancy.TryGetValue(coord, out HashSet<string> ids) || ids.Count == 0)
		{
			return false;
		}

		return ids.Any(id => id != unitId);
	}

	private void AddOccupancy(Vector2I coord, string unitId)
	{
		if (!_occupancy.TryGetValue(coord, out HashSet<string> ids))
		{
			ids = new HashSet<string>();
			_occupancy[coord] = ids;
		}

		ids.Add(unitId);
	}

	private void RemoveOccupancy(Vector2I coord, string unitId)
	{
		if (!_occupancy.TryGetValue(coord, out HashSet<string> ids))
		{
			return;
		}

		ids.Remove(unitId);
		if (ids.Count == 0)
		{
			_occupancy.Remove(coord);
		}
	}
}