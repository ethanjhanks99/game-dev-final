using Godot;
using System;

public enum CombatContext
{
	MovingPieceAttacking,
	StationaryPieceAttacking,
	Defending,
}

public static class CombatModifiers
{
	public static int CalculateDamageFromAttack(BoardUnit attacker, BoardUnit target, bool attackerIsMoving, FatigueTracker fatigue)
	{
		int baseDamage = attacker.Stats.Attack;

		if (fatigue != null && fatigue.IsUnitFatigued(attacker.Id))
		{
			float penalty = MovementPointSystem.GetFatigueAttackPenalty(attacker.Stats.Type);
			baseDamage = Mathf.FloorToInt(baseDamage * penalty);
		}

		if (attacker.Stats.Type == UnitType.Archer)
		{
			if (fatigue == null || !fatigue.IsUnitFatigued(attacker.Id))
			{
				baseDamage = Mathf.FloorToInt(baseDamage * 1.1f);
			}
		}

		return baseDamage;
	}

	public static int CalculateRetaliation(BoardUnit defender, BoardUnit attacker)
	{
		Vector2I toAttacker = attacker.Position - defender.Position;
		FacingDirection attackDirection = GridTypes.VectorToFacing(toAttacker);
		int baseDefense = defender.Stats.Defense;

		if (attackDirection == defender.Facing)
		{
			return baseDefense;
		}

		FacingDirection opposite = GetOpposite(defender.Facing);
		if (attackDirection == opposite)
		{
			return Mathf.Max(0, baseDefense - 2);
		}

		return Mathf.Max(0, baseDefense - 1);
	}

	public static int GetEffectiveDamageAgainstTarget(int incomingDamage, int defenseValue)
	{
		return Mathf.Max(1, incomingDamage - defenseValue);
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
}
