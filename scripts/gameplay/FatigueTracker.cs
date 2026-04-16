using Godot;
using System;
using System.Collections.Generic;

public sealed class UnitFatigueState
{
	public UnitFatigueState(string unitId)
	{
		UnitId = unitId;
	}

	public string UnitId { get; }
	public int TilesMoved { get; set; }
	public bool IsFatigued { get; set; }
	public int MovementPointsSpent { get; set; }

	public void Reset()
	{
		TilesMoved = 0;
		IsFatigued = false;
		MovementPointsSpent = 0;
	}

	public override string ToString()
	{
		return $"UnitFatigueState(id={UnitId}, tiles={TilesMoved}, fatigued={IsFatigued}, mpSpent={MovementPointsSpent})";
	}
}

public sealed class FatigueTracker
{
	private readonly Dictionary<string, UnitFatigueState> _fatigueByUnit = new Dictionary<string, UnitFatigueState>();

	public void TrackUnitMovement(string unitId, UnitType type, int tileDistance)
	{
		if (!_fatigueByUnit.TryGetValue(unitId, out UnitFatigueState state))
		{
			state = new UnitFatigueState(unitId);
			_fatigueByUnit[unitId] = state;
		}

		int costPerTile = MovementPointSystem.GetMovementCostPerTile(type);
		state.MovementPointsSpent += costPerTile * tileDistance;
		state.TilesMoved += tileDistance;

		int maxTilesBeforeFatigue = MovementPointSystem.GetMaxTilesBeforeFatigue(type);
		if (state.TilesMoved >= maxTilesBeforeFatigue)
		{
			state.IsFatigued = true;
		}
	}

	public bool IsUnitFatigued(string unitId)
	{
		return _fatigueByUnit.TryGetValue(unitId, out UnitFatigueState state) && state.IsFatigued;
	}

	public int GetMovementPointsSpent(string unitId)
	{
		return _fatigueByUnit.TryGetValue(unitId, out UnitFatigueState state) ? state.MovementPointsSpent : 0;
	}

	public IEnumerable<UnitFatigueState> GetAllFatigueStates()
	{
		return _fatigueByUnit.Values;
	}

	public void ResetTurn()
	{
		foreach (UnitFatigueState state in _fatigueByUnit.Values)
		{
			state.Reset();
		}
	}

	public void ResetUnit(string unitId)
	{
		if (_fatigueByUnit.TryGetValue(unitId, out UnitFatigueState state))
		{
			state.Reset();
		}
	}
}
