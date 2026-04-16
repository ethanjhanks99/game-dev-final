using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BoardState
{
	private readonly Dictionary<string, BoardUnit> _unitsById = new Dictionary<string, BoardUnit>();
	private readonly Dictionary<Vector2I, string> _occupancy = new Dictionary<Vector2I, string>();
	private readonly HashSet<Vector2I> _baseTiles = new HashSet<Vector2I>();

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
		if (!_occupancy.TryGetValue(coord, out string id))
		{
			return false;
		}

		return _unitsById.TryGetValue(id, out unit);
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

		if (_occupancy.ContainsKey(unit.Position))
		{
			reason = $"Tile {unit.Position} is occupied.";
			return false;
		}

		_unitsById[unit.Id] = unit;
		_occupancy[unit.Position] = unit.Id;
		return true;
	}

	public bool RemoveDeadUnit(string id)
	{
		if (!_unitsById.TryGetValue(id, out BoardUnit unit))
		{
			return false;
		}

		_unitsById.Remove(id);
		_occupancy.Remove(unit.Position);
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

		if (_occupancy.ContainsKey(target))
		{
			reason = "Target tile is occupied.";
			return false;
		}

		ISet<Vector2I> reachable = GetReachableTiles(unitId, includeOccupiedTiles: false);
		if (!reachable.Contains(target))
		{
			reason = "Target is not reachable by movement rules.";
			return false;
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

		_occupancy.Remove(oldPos);
		_occupancy[target] = unit.Id;
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

				if (nextCost > unit.Stats.MovementRange || !GridTypes.IsInBounds(next))
				{
					continue;
				}

				if (bestCost.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
				{
					continue;
				}

				bool occupied = _occupancy.ContainsKey(next) && _occupancy[next] != unit.Id;
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
		return _occupancy.ToDictionary(entry => entry.Key, entry => entry.Value);
	}

	public IEnumerable<BoardUnit> GetUnitsForPlayer(PlayerSide player)
	{
		return _unitsById.Values.Where(unit => unit.Owner == player && unit.IsAlive);
	}

	public int CountAliveUnits(PlayerSide player)
	{
		return _unitsById.Values.Count(unit => unit.Owner == player && unit.IsAlive);
	}
}
