using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class TurnResolver
{
	public static TurnResolutionReport ResolveSimultaneousTurn(BoardState board, IEnumerable<PlayerTurnSelection> selections)
	{
		TurnResolutionReport report = new TurnResolutionReport();
		Dictionary<string, AttackSnapshot> attackSnapshots = board.Units.ToDictionary(
			unit => unit.Id,
			unit => new AttackSnapshot(unit.Owner, unit.Stats.Type, unit.Position));
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
		ResolveMeleeConflicts(board, report);
		ApplySimultaneousAttacks(board, allAttacks, movedUnits, attackSnapshots, report);
		RemoveDeadUnits(board, report);
		return report;
	}

	private static HashSet<string> ApplySimultaneousMoves(BoardState board, Dictionary<string, MoveOrder> allMoves, TurnResolutionReport report)
	{
		Dictionary<Vector2I, Dictionary<PlayerSide, List<string>>> destinationClaimsByOwner = new Dictionary<Vector2I, Dictionary<PlayerSide, List<string>>>();
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
				report.MoveResults.Add(new UnitMoveResolution(move.UnitId, unit.Position, move.Destination, false, reason));
				invalidMoves.Add(move.UnitId);
				continue;
			}

			if (!destinationClaimsByOwner.TryGetValue(move.Destination, out Dictionary<PlayerSide, List<string>> claimsByOwner))
			{
				claimsByOwner = new Dictionary<PlayerSide, List<string>>();
				destinationClaimsByOwner[move.Destination] = claimsByOwner;
			}

			if (!claimsByOwner.TryGetValue(unit.Owner, out List<string> claimants))
			{
				claimants = new List<string>();
				claimsByOwner[unit.Owner] = claimants;
			}

			claimants.Add(move.UnitId);
		}

		HashSet<string> blockedByFriendlyConflict = new HashSet<string>();
		foreach (KeyValuePair<Vector2I, Dictionary<PlayerSide, List<string>>> entry in destinationClaimsByOwner)
		{
			foreach (KeyValuePair<PlayerSide, List<string>> claims in entry.Value)
			{
				if (claims.Value.Count <= 1)
				{
					continue;
				}

				foreach (string unitId in claims.Value)
				{
					blockedByFriendlyConflict.Add(unitId);
				}
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

			if (blockedByFriendlyConflict.Contains(move.UnitId))
			{
				report.MoveResults.Add(new UnitMoveResolution(unit.Id, unit.Position, move.Destination, false, "Movement conflict: multiple friendly units selected the same tile."));
				continue;
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

	private static void ResolveMeleeConflicts(BoardState board, TurnResolutionReport report)
	{
		List<Vector2I> occupiedTiles = board.GetOccupiedTiles().ToList();
		foreach (Vector2I tile in occupiedTiles)
		{
			if (!board.TryGetUnitsAt(tile, out List<BoardUnit> unitsAtTile))
			{
				continue;
			}

			List<BoardUnit> contenders = unitsAtTile.Where(unit => unit.IsAlive).ToList();
			if (contenders.Count < 2)
			{
				continue;
			}

			if (contenders.Select(unit => unit.Owner).Distinct().Count() < 2)
			{
				continue;
			}

			while (contenders.Count > 1)
			{
				contenders = contenders
					.Where(unit => unit.IsAlive)
					.OrderByDescending(unit => unit.CurrentHealth)
					.ThenByDescending(unit => unit.Stats.Attack)
					.ThenBy(unit => unit.Id)
					.ToList();

				if (contenders.Count < 2)
				{
					break;
				}

				if (contenders.Select(unit => unit.Owner).Distinct().Count() < 2)
				{
					break;
				}

				BoardUnit left = contenders[0];
				BoardUnit right = contenders[1];
				ResolveMeleeDuel(left, right, report);
			}
		}
	}

	private static void ResolveMeleeDuel(BoardUnit left, BoardUnit right, TurnResolutionReport report)
	{
		bool leftArcher = left.Stats.Type == UnitType.Archer;
		bool rightArcher = right.Stats.Type == UnitType.Archer;

		if (leftArcher && rightArcher)
		{
			left.SetCurrentHealth(0f);
			right.SetCurrentHealth(0f);
			report.AttackResults.Add(new UnitAttackResolution(left.Id, right.Id, 0, true, "Melee tie: both archers were eliminated."));
			report.AttackResults.Add(new UnitAttackResolution(right.Id, left.Id, 0, true, "Melee tie: both archers were eliminated."));
			return;
		}

		if (leftArcher || rightArcher)
		{
			BoardUnit archer = leftArcher ? left : right;
			BoardUnit winner = leftArcher ? right : left;
			archer.SetCurrentHealth(0f);
			report.AttackResults.Add(new UnitAttackResolution(winner.Id, archer.Id, 0, true, "Archer lost melee instantly."));
			return;
		}

		if (Mathf.IsEqualApprox(left.CurrentHealth, right.CurrentHealth))
		{
			if (left.Stats.Attack == right.Stats.Attack)
			{
				left.SetCurrentHealth(0f);
				right.SetCurrentHealth(0f);
				report.AttackResults.Add(new UnitAttackResolution(left.Id, right.Id, 0, true, "Melee tie: equal health and power, both units eliminated."));
				report.AttackResults.Add(new UnitAttackResolution(right.Id, left.Id, 0, true, "Melee tie: equal health and power, both units eliminated."));
				return;
			}

			BoardUnit winnerByPower = left.Stats.Attack > right.Stats.Attack ? left : right;
			BoardUnit loserByPower = winnerByPower == left ? right : left;
			loserByPower.SetCurrentHealth(0f);
			winnerByPower.SetCurrentHealth(winnerByPower.CurrentHealth / 2f);
			report.AttackResults.Add(new UnitAttackResolution(winnerByPower.Id, loserByPower.Id, 0, true, "Melee resolved by higher power; winner health halved."));
			return;
		}

		BoardUnit winnerByHealth = left.CurrentHealth > right.CurrentHealth ? left : right;
		BoardUnit loserByHealth = winnerByHealth == left ? right : left;
		loserByHealth.SetCurrentHealth(0f);
		winnerByHealth.SetCurrentHealth(winnerByHealth.CurrentHealth / 2f);
		report.AttackResults.Add(new UnitAttackResolution(winnerByHealth.Id, loserByHealth.Id, 0, true, "Melee resolved by higher health; winner health halved."));
	}

	private static void ApplySimultaneousAttacks(BoardState board, List<AttackOrder> allAttacks, ISet<string> movedUnits, IReadOnlyDictionary<string, AttackSnapshot> attackSnapshots, TurnResolutionReport report)
	{
		Dictionary<string, float> incomingDamage = new Dictionary<string, float>();

		foreach (AttackOrder attack in allAttacks)
		{
			if (!attackSnapshots.TryGetValue(attack.AttackerUnitId, out AttackSnapshot attackerSnapshot))
			{
				report.AttackResults.Add(new UnitAttackResolution(attack.AttackerUnitId, attack.TargetUnitId, 0, false, "Attacker is missing or dead."));
				continue;
			}

			if (!board.TryGetUnit(attack.AttackerUnitId, out BoardUnit attacker))
			{
				report.AttackResults.Add(new UnitAttackResolution(attack.AttackerUnitId, attack.TargetUnitId, 0, false, "Attacker is missing or dead."));
				continue;
			}

			if (attackerSnapshot.Type != UnitType.Archer)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Only archers can queue ranged attacks."));
				continue;
			}

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

			if (!attackSnapshots.TryGetValue(attack.TargetUnitId, out AttackSnapshot targetSnapshot))
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Target is missing or dead."));
				continue;
			}

			if (!board.TryGetUnit(attack.TargetUnitId, out BoardUnit target) || !target.IsAlive)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, attack.TargetUnitId, 0, false, "Target is missing or dead."));
				continue;
			}

			if (attackerSnapshot.Owner == targetSnapshot.Owner)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 0, false, "Friendly fire is disabled."));
				continue;
			}

			// Range is validated from pre-move positions so queued shots still land if either unit moved.
			int distance = GridTypes.ManhattanDistance(attackerSnapshot.Position, targetSnapshot.Position);
			if (distance < 1 || distance > 3)
			{
				report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 0, false, "Target out of attack range."));
				continue;
			}

			const float damage = 1f;

			if (!incomingDamage.ContainsKey(target.Id))
			{
				incomingDamage[target.Id] = 0f;
			}

			incomingDamage[target.Id] += damage;
			report.AttackResults.Add(new UnitAttackResolution(attacker.Id, target.Id, 1, true, "Archer ranged hit for 1 damage."));

			Vector2I lookDir = target.Position - attacker.Position;
			attacker.Facing = GridTypes.VectorToFacing(lookDir);
		}

		foreach (KeyValuePair<string, float> damageEntry in incomingDamage)
		{
			if (board.TryGetUnit(damageEntry.Key, out BoardUnit target))
			{
				target.ApplyDamage(damageEntry.Value);
			}
		}
	}

	private readonly struct AttackSnapshot
	{
		public AttackSnapshot(PlayerSide owner, UnitType type, Vector2I position)
		{
			Owner = owner;
			Type = type;
			Position = position;
		}

		public PlayerSide Owner { get; }
		public UnitType Type { get; }
		public Vector2I Position { get; }
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
