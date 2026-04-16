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

		ApplySimultaneousMoves(board, allMoves, report);
		ApplySimultaneousAttacks(board, allAttacks, report);
		RemoveDeadUnits(board, report);
		return report;
	}

	private static void ApplySimultaneousMoves(BoardState board, Dictionary<string, MoveOrder> allMoves, TurnResolutionReport report)
	{
		Dictionary<string, Vector2I> startPositions = board.Units.ToDictionary(unit => unit.Id, unit => unit.Position);
		Dictionary<Vector2I, List<string>> destinationClaims = new Dictionary<Vector2I, List<string>>();
		HashSet<string> invalidMoves = new HashSet<string>();

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
			}
			else
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, start, move.Destination, false, reason));
			}
		}
	}

	private static void ApplySimultaneousAttacks(BoardState board, List<AttackOrder> allAttacks, TurnResolutionReport report)
	{
		Dictionary<string, int> incomingDamage = new Dictionary<string, int>();

		foreach (AttackOrder attack in allAttacks)
		{
			if (!board.TryGetUnit(attack.AttackerUnitId, out BoardUnit attacker) || !attacker.IsAlive)
			{
				report.AttackResults.Add(new UnitAttackResolution(attack.AttackerUnitId, attack.TargetUnitId, 0, false, "Attacker is missing or dead."));
				continue;
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
