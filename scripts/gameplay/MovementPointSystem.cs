using Godot;
using System;
using System.Collections.Generic;

public static class MovementPointSystem
{
	public const int TotalMovementPointsPerTurn = 6;

	public static int GetMovementCostPerTile(UnitType type)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return 2;
			case UnitType.Cavalry:
				return 1;
			case UnitType.Archer:
				return 2;
			default:
				return 2;
		}
	}

	public static int GetMaxTilesBeforeFatigue(UnitType type)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return 2;
			case UnitType.Cavalry:
				return 5;
			case UnitType.Archer:
				return 2;
			default:
				return 2;
		}
	}

	public static float GetFatigueAttackPenalty(UnitType type)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return 0.8f;
			case UnitType.Cavalry:
				return 0.85f;
			case UnitType.Archer:
				return 0.7f;
			default:
				return 1f;
		}
	}

	public static int CalculateReachableTilesWithPointLimit(UnitType type)
	{
		int costPerTile = GetMovementCostPerTile(type);
		return (costPerTile > 0) ? TotalMovementPointsPerTurn / costPerTile : 0;
	}
}
