using Godot;
using System.Collections.Generic;

public sealed class MoveOrder
{
	public MoveOrder(string unitId, Vector2I destination)
	{
		UnitId = unitId;
		Destination = destination;
	}

	public string UnitId { get; }
	public Vector2I Destination { get; }
}

public sealed class AttackOrder
{
	public AttackOrder(string attackerUnitId, string targetUnitId)
	{
		AttackerUnitId = attackerUnitId;
		TargetUnitId = targetUnitId;
	}

	public string AttackerUnitId { get; }
	public string TargetUnitId { get; }
}

public sealed class PlayerTurnSelection
{
	public PlayerTurnSelection(PlayerSide player)
	{
		Player = player;
	}

	public PlayerSide Player { get; }
	public List<MoveOrder> Moves { get; } = new List<MoveOrder>();
	public List<AttackOrder> Attacks { get; } = new List<AttackOrder>();
}

public sealed class UnitMoveResolution
{
	public UnitMoveResolution(string unitId, Vector2I start, Vector2I destination, bool applied, string reason)
	{
		UnitId = unitId;
		Start = start;
		Destination = destination;
		Applied = applied;
		Reason = reason;
	}

	public string UnitId { get; }
	public Vector2I Start { get; }
	public Vector2I Destination { get; }
	public bool Applied { get; }
	public string Reason { get; }
}

public sealed class UnitAttackResolution
{
	public UnitAttackResolution(string attackerId, string targetId, int damage, bool applied, string reason)
	{
		AttackerId = attackerId;
		TargetId = targetId;
		Damage = damage;
		Applied = applied;
		Reason = reason;
	}

	public string AttackerId { get; }
	public string TargetId { get; }
	public int Damage { get; }
	public bool Applied { get; }
	public string Reason { get; }
}

public sealed class TurnResolutionReport
{
	public List<UnitMoveResolution> MoveResults { get; } = new List<UnitMoveResolution>();
	public List<UnitAttackResolution> AttackResults { get; } = new List<UnitAttackResolution>();
	public List<string> RemovedUnits { get; } = new List<string>();

	// Players who lost all their units this turn and are now eliminated.
	public List<PlayerSide> EliminatedPlayers { get; } = new List<PlayerSide>();
}