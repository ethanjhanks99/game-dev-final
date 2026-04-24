using Godot;

public sealed class UnitStats
{
	public UnitStats(UnitType type, int maxHealth, int attack, int defense, int movementRange, int attackRange)
	{
		Type = type;
		MaxHealth = maxHealth;
		Attack = attack;
		Defense = defense;
		MovementRange = movementRange;
		AttackRange = attackRange;
	}

	public UnitType Type { get; }
	public int MaxHealth { get; }
	public int Attack { get; }
	public int Power => Attack;
	public int Defense { get; }
	public int MovementRange { get; }
	public int AttackRange { get; }
}

public abstract class BoardUnit
{
	protected BoardUnit(string id, PlayerSide owner, Vector2I position, FacingDirection facing, UnitStats stats)
	{
		Id = id;
		Owner = owner;
		Position = position;
		Facing = facing;
		Stats = stats;
		CurrentHealth = stats.MaxHealth;
	}

	public string Id { get; }
	public PlayerSide Owner { get; }
	public Vector2I Position { get; set; }
	public FacingDirection Facing { get; set; }
	public UnitStats Stats { get; }
	public float CurrentHealth { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	public void ApplyDamage(float amount)
	{
		if (amount <= 0 || !IsAlive)
		{
			return;
		}

		CurrentHealth -= amount;
		if (CurrentHealth < 0)
		{
			CurrentHealth = 0;
		}
	}

	public void SetCurrentHealth(float value)
	{
		CurrentHealth = Mathf.Max(0f, value);
	}
}

public sealed class InfantryUnit : BoardUnit
{
	public InfantryUnit(string id, PlayerSide owner, Vector2I position, FacingDirection facing)
		: base(id, owner, position, facing, UnitCatalog.Infantry)
	{
	}
}

public sealed class CavalryUnit : BoardUnit
{
	public CavalryUnit(string id, PlayerSide owner, Vector2I position, FacingDirection facing)
		: base(id, owner, position, facing, UnitCatalog.Cavalry)
	{
	}
}

public sealed class ArcherUnit : BoardUnit
{
	public ArcherUnit(string id, PlayerSide owner, Vector2I position, FacingDirection facing)
		: base(id, owner, position, facing, UnitCatalog.Archer)
	{
	}
}

public static class UnitCatalog
{
	// Balance values are defaults and should be tuned with playtests.
	public static readonly UnitStats Infantry = new UnitStats(UnitType.Infantry, maxHealth: 2, attack: 2, defense: 3, movementRange: 1, attackRange: 1);
	public static readonly UnitStats Cavalry = new UnitStats(UnitType.Cavalry, maxHealth: 2, attack: 3, defense: 2, movementRange: 3, attackRange: 1);
	public static readonly UnitStats Archer = new UnitStats(UnitType.Archer, maxHealth: 2, attack: 1, defense: 1, movementRange: 1, attackRange: 3);

	public static BoardUnit CreateUnit(UnitType type, string id, PlayerSide owner, Vector2I position, FacingDirection facing)
	{
		switch (type)
		{
			case UnitType.Infantry:
				return new InfantryUnit(id, owner, position, facing);
			case UnitType.Cavalry:
				return new CavalryUnit(id, owner, position, facing);
			default:
				return new ArcherUnit(id, owner, position, facing);
		}
	}
}
