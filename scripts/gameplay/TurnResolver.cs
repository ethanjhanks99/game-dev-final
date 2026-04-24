using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class TurnResolver
{
	public static TurnResolutionReport ResolveSimultaneousTurn(BoardState board, IEnumerable<PlayerTurnSelection> selections)
	{
		TurnResolutionReport report = new TurnResolutionReport();
		Dictionary<string, MoveOrder> allMoves = new Dictionary<string, MoveOrder>();
		List<AttackOrder> allAttacks = new List<AttackOrder>();

		foreach (PlayerTurnSelection playerSelection in selections)
		{
			foreach (MoveOrder move in playerSelection.Moves)
			{
				if (!allMoves.ContainsKey(move.UnitId))
				{
					allMoves[move.UnitId] = move;
				}
			}

			allAttacks.AddRange(playerSelection.Attacks);
		}

		HashSet<string> movedUnits = ApplySimultaneousMoves(board, allMoves, report);
		ApplySimultaneousAttacks(board, allAttacks, movedUnits, report);
		RemoveDeadUnits(board, report);
		return report;
	}

	private static HashSet<string> ApplySimultaneousMoves(BoardState board, Dictionary<string, MoveOrder> allMoves, TurnResolutionReport report)
	{
		Dictionary<string, Vector2I> startPositions = board.Units.ToDictionary(unit => unit.Id, unit => unit.Position);
		Dictionary<Vector2I, List<string>> destinationClaims = new Dictionary<Vector2I, List<string>>();
		HashSet<string> invalidMoves = new HashSet<string>();
		HashSet<string> movedUnits = new HashSet<string>();

		foreach (MoveOrder move in allMoves.Values)
		{
			if (!board.TryGetUnit(move.UnitId, out BoardUnit unit))
			{
				report.MoveResults.Add(new UnitMoveResolution(move.UnitId, new Vector2I(-1, -1), move.Destination, false, "Unit does not exist."));
				invalidMoves.Add(move.UnitId);
				continue;
			}

			if (!board.CanMoveUnit(move.UnitId, move.Destination, out string reason))
			{
				if (reason == "Target tile is occupied." && TryResolveMeleeContact(board, unit, move, report))
				{
					invalidMoves.Add(move.UnitId);
					continue;
				}

				report.MoveResults.Add(new UnitMoveResolution(move.UnitId, unit.Position, move.Destination, false, reason));
				invalidMoves.Add(move.UnitId);
				continue;
			}

			if (!destinationClaims.TryGetValue(move.Destination, out List<string> claimants))
			{
				claimants = new List<string>();
				destinationClaims[move.Destination] = claimants;
			}

			claimants.Add(move.UnitId);
		}

		HashSet<string> blockedByConflict = new HashSet<string>();
		foreach (KeyValuePair<Vector2I, List<string>> entry in destinationClaims)
		{
			if (entry.Value.Count > 1)
			{
				foreach (string unitId in entry.Value)
				{
					blockedByConflict.Add(unitId);
				}
			}
		}

		HashSet<string> blockedBySwap = new HashSet<string>();
		foreach (MoveOrder move in allMoves.Values)
		{
			if (invalidMoves.Contains(move.UnitId) || blockedByConflict.Contains(move.UnitId))
			{
				continue;
			}

			if (!startPositions.TryGetValue(move.UnitId, out Vector2I moverStart))
			{
				continue;
			}

			if (!board.TryGetUnitAt(move.Destination, out BoardUnit occupant))
			{
				continue;
			}

			if (!allMoves.TryGetValue(occupant.Id, out MoveOrder occupantMove))
			{
				continue;
			}

			if (occupantMove.Destination == moverStart)
			{
				blockedBySwap.Add(move.UnitId);
				blockedBySwap.Add(occupant.Id);
			}
		}

		foreach (MoveOrder move in allMoves.Values)
		{
			if (!board.TryGetUnit(move.UnitId, out BoardUnit unit))
			{
				continue;
			}

			if (invalidMoves.Contains(move.UnitId))
			{
				continue;
			}

			if (blockedByConflict.Contains(move.UnitId))
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, unit.Position, move.Destination, false, "Movement conflict: multiple units selected the same tile."));
				continue;
			}

			if (blockedBySwap.Contains(move.UnitId))
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, unit.Position, move.Destination, false, "Movement conflict: direct swap is blocked."));
				continue;
			}

			// A destination can only be entered if the current occupant is leaving successfully.
			if (board.TryGetUnitAt(move.Destination, out BoardUnit occupant))
			{
				bool occupantLeaves = allMoves.TryGetValue(occupant.Id, out MoveOrder occupantMove)
					&& !invalidMoves.Contains(occupant.Id)
					&& !blockedByConflict.Contains(occupant.Id)
					&& !blockedBySwap.Contains(occupant.Id);

				if (!occupantLeaves)
				{
					report.MoveResults.Add(new UnitMoveResolution(unit.Id, unit.Position, move.Destination, false, "Destination remained occupied."));
					continue;
				}
			}

			Vector2I start = unit.Position;
			if (board.MoveUnit(unit.Id, move.Destination, out string reason))
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, start, move.Destination, true, string.Empty));
				movedUnits.Add(unit.Id);
			}
			else
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, start, move.Destination, false, reason));
			}
		}

		return movedUnits;
	}

	private static void ApplySimultaneousAttacks(BoardState board, List<AttackOrder> allAttacks, ISet<string> movedUnits, TurnResolutionReport report)
	{
		Dictionary<string, int> incomingDamage = new Dictionary<string, int>();

		foreach (AttackOrder attack in allAttacks)
		{
			if (!board.TryGetUnit(attack.AttackerUnitId, out BoardUnit attacker) || !attacker.IsAlive)
			{
				report.AttackResults.Add(new UnitAttackResolution(attack.AttackerUnitId, attack.TargetUnitId, 0, false, "Attacker is missing or dead."));
				continue;
			}

			if (attacker.Stats.Type == UnitType.Archer)
			{
				if (movedUnits.Contains(attacker.Id))
				{
					report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Archers cannot move and attack on the same turn."));
					continue;
				}

				int requiredAttackPoints = MovementPointSystem.GetAttackCost(attacker.Stats.Type);
				int availableAttackPoints = MovementPointSystem.TotalMovementPointsPerTurn;
				if (requiredAttackPoints > availableAttackPoints)
				{
					report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Archer attack failed: insufficient movement points to pay attack cost."));
					continue;
				}
			}

			if (!board.TryGetUnit(attack.TargetUnitId, out BoardUnit target) || !target.IsAlive)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Target is missing or dead."));
				continue;
			}

			if (attacker.Owner == target.Owner)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 0, false, "Friendly fire is disabled."));
				continue;
			}

			int distance = GridTypes.ManhattanDistance(attacker.Position, target.Position);
			if (distance < 1 || distance > attacker.Stats.AttackRange)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 0, false, "Target out of attack range."));
				continue;
			}

			int effectiveDefense = GetDirectionalDefense(target, attacker.Position);
			int damage = Math.Max(1, attacker.Stats.Attack - effectiveDefense);

			if (!incomingDamage.ContainsKey(target.Id))
			{
				incomingDamage[target.Id] = 0;
			}

			incomingDamage[target.Id] += damage;
			report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, damage, true, string.Empty));

			Vector2I lookDir = target.Position - attacker.Position;
			attacker.Facing = GridTypes.VectorToFacing(lookDir);
		}

		foreach (KeyValuePair<string, int> damageEntry in incomingDamage)
		{
			if (board.TryGetUnit(damageEntry.Key, out BoardUnit target))
			{
				target.ApplyDamage(damageEntry.Value);
			}
		}
	}

	private static bool TryResolveMeleeContact(BoardState board, BoardUnit attacker, MoveOrder move, TurnResolutionReport report)
	{
		bool isMeleeUnit = attacker.Stats.Type == UnitType.Cavalry || attacker.Stats.Type == UnitType.Infantry;
		if (!isMeleeUnit)
		{
			return false;
		}

		if (!board.TryGetUnitAt(move.Destination, out BoardUnit target))
		{
			return false;
		}

		if (target.Owner == attacker.Owner)
		{
			report.MoveResults.Add(new UnitMoveResolution(attacker.Id, attacker.Position, move.Destination, false, "Cannot initiate melee on a friendly unit tile."));
			return true;
		}

		ISet<Vector2I> attackReach = board.GetReachableTiles(attacker.Id, includeOccupiedTiles: true);
		if (!attackReach.Contains(move.Destination))
		{
			report.MoveResults.Add(new UnitMoveResolution(attacker.Id, attacker.Position, move.Destination, false, "Target tile is not reachable for melee contact."));
			return true;
		}

		report.MoveResults.Add(new UnitMoveResolution(attacker.Id, attacker.Position, move.Destination, false, "Melee contact initiated (combat resolution not implemented for tile-sharing)."));
		report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 0, true, "Melee contact registered."));
		return true;
	}

	private static int GetDirectionalDefense(BoardUnit target, Vector2I attackerPosition)
	{
		Vector2I toAttacker = attackerPosition - target.Position;
		FacingDirection attackDirection = GridTypes.VectorToFacing(toAttacker);
		int baseDefense = target.Stats.Defense;

		if (attackDirection == target.Facing)
		{
			return baseDefense;
		}

		FacingDirection opposite = GetOpposite(target.Facing);
		if (attackDirection == opposite)
		{
			return Math.Max(0, baseDefense - 2);
		}

		return Math.Max(0, baseDefense - 1);
	}

	private static FacingDirection GetOpposite(FacingDirection direction)
	{
		switch (direction)
		{
			case FacingDirection.North:
				return FacingDirection.South;
			case FacingDirection.East:
				return FacingDirection.West;
			case FacingDirection.South:
				return FacingDirection.North;
			default:
				return FacingDirection.East;
		}
	}

	private static void RemoveDeadUnits(BoardState board, TurnResolutionReport report)
	{
		List<string> deadUnits = board.Units
			.Where(unit => !unit.IsAlive)
			.Select(unit => unit.Id)
			.ToList();

		foreach (string deadId in deadUnits)
		{
			if (board.RemoveDeadUnit(deadId))
			{
				report.RemovedUnits.Add(deadId);
			}
		}
	}
}
