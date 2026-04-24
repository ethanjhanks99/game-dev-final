using Godot;
using System;
using System.Collections.Generic;

public static class MovementPointSystem
{
	public const int TotalMovementPointsPerTurn = 12;
	public const int ArcherAttackCost = 4;

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

	public static int GetAttackCost(UnitType type)
	{
		switch (type)
		{
			case UnitType.Archer:
				return ArcherAttackCost;
			default:
				return 0;
		}
	}

	public static int GetMaxTilesBeforeFatigue(UnitType type)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return 1;
			case UnitType.Cavalry:
				return 3;
			case UnitType.Archer:
				return 1;
			default:
				return 1;
		}
	}

	public static int GetMaxTilesPerTurn(UnitType type)
	{
		int byMovementPoints = CalculateReachableTilesWithPointLimit(type);
		int byFatigue = GetMaxTilesBeforeFatigue(type);
		return Math.Min(byMovementPoints, byFatigue);
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
