public static class UnitPointSystem
{
	public const int StartingUnitPoints = 12;
	public const int UnitPointGainPerTurn = 1;

	public static int GetUnitCost(UnitType type)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return 1;
			case UnitType.Cavalry:
				return 3;
			case UnitType.Archer:
				return 2;
			default:
				return 1;
		}
	}
}