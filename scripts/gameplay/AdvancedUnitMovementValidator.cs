using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AdvancedUnitMovementValidator
{
	private readonly FatigueTracker _fatigue;
	private readonly BoardState _board;

	public AdvancedUnitMovementValidator(BoardState board, FatigueTracker fatigue)
	{
		_board = board;
		_fatigue = fatigue;
	}

	public bool CanMoveWithPointLimit(string unitId, Vector2I target, out string reason)
	{
		reason = string.Empty;
		if (!_board.TryGetUnit(unitId, out BoardUnit unit) || !unit.IsAlive)
		{
			reason = "Unit is missing or dead.";
			return false;
		}

		int distance = GridTypes.ManhattanDistance(unit.Position, target);
		int costPerTile = MovementPointSystem.GetMovementCostPerTile(unit.Stats.Type);
		int totalCost = distance * costPerTile;

		if (totalCost > MovementPointSystem.TotalMovementPointsPerTurn)
		{
			reason = $"Insufficient movement points: need {totalCost}, have {MovementPointSystem.TotalMovementPointsPerTurn}.";
			return false;
		}

		return _board.CanMoveUnit(unitId, target, out reason);
	}

	public ISet<Vector2I> GetReachableTilesWithPointLimit(string unitId)
	{
		HashSet<Vector2I> reachable = new HashSet<Vector2I>();
		if (!_board.TryGetUnit(unitId, out BoardUnit unit) || !unit.IsAlive)
		{
			return reachable;
		}

		int costPerTile = MovementPointSystem.GetMovementCostPerTile(unit.Stats.Type);
		int maxTiles = (costPerTile > 0) ? MovementPointSystem.TotalMovementPointsPerTurn / costPerTile : 0;

		Queue<(Vector2I coord, int tilesUsed)> frontier = new Queue<(Vector2I coord, int tilesUsed)>();
		Dictionary<Vector2I, int> bestCost = new Dictionary<Vector2I, int>();

		frontier.Enqueue((unit.Position, 0));
		bestCost[unit.Position] = 0;

		while (frontier.Count > 0)
		{
			(Vector2I coord, int tilesUsed) = frontier.Dequeue();

			foreach (Vector2I dir in GridTypes.CardinalDirections)
			{
				Vector2I next = coord + dir;
				int nextTiles = tilesUsed + 1;

				if (nextTiles > maxTiles || !GridTypes.IsPlayableTile(next))
				{
					continue;
				}

				if (bestCost.TryGetValue(next, out int knownCost) && knownCost <= nextTiles)
				{
					continue;
				}

				bool occupied = _board.TryGetUnitAt(next, out BoardUnit occupant) && occupant.Id != unit.Id;
				if (occupied)
				{
					continue;
				}

				bestCost[next] = nextTiles;
				reachable.Add(next);
				frontier.Enqueue((next, nextTiles));
			}
		}

		reachable.Remove(unit.Position);
		return reachable;
	}

	public void TrackMovement(string unitId, Vector2I from, Vector2I to)
	{
		if (!_board.TryGetUnit(unitId, out BoardUnit unit))
		{
			return;
		}

		int distance = GridTypes.ManhattanDistance(from, to);
		_fatigue.TrackUnitMovement(unitId, unit.Stats.Type, distance);
	}
}
