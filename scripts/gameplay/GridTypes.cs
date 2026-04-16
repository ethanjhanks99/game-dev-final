using Godot;
using System;
using System.Collections.Generic;

public enum PlayerSide
{
	None = 0,
	One = 1,
	Two = 2,
	Three = 3,
	Four = 4,
}

public enum TileType
{
	Normal,
	Base,
}

public enum UnitType
{
	Infantry,
	Cavalry,
	Archer,
}

public enum FacingDirection
{
	North,
	East,
	South,
	West,
}

public static class GridTypes
{
	public const int CoreBoardWidth = 8;
	public const int CoreBoardHeight = 8;
	public const int ExtensionDepth = 6;

	public const int CoreMinX = 0;
	public const int CoreMaxX = CoreMinX + CoreBoardWidth - 1;
	public const int CoreMinY = 0;
	public const int CoreMaxY = CoreMinY + CoreBoardHeight - 1;

	public const int MinX = CoreMinX - ExtensionDepth;
	public const int MaxX = CoreMaxX + ExtensionDepth;
	public const int MinY = CoreMinY - ExtensionDepth;
	public const int MaxY = CoreMaxY + ExtensionDepth;

	// Each player's base is placed at the far edge of their unique field.
	public static readonly IReadOnlyDictionary<PlayerSide, Vector2I> BaseTiles = new Dictionary<PlayerSide, Vector2I>
	{
		{ PlayerSide.One, new Vector2I(3, MinY) },
		{ PlayerSide.Two, new Vector2I(MaxX, 3) },
		{ PlayerSide.Three, new Vector2I(4, MaxY) },
		{ PlayerSide.Four, new Vector2I(MinX, 4) },
	};

	public static readonly Vector2I[] CardinalDirections =
	{
		new Vector2I(0, -1),
		new Vector2I(1, 0),
		new Vector2I(0, 1),
		new Vector2I(-1, 0),
	};

	public static bool IsInBounds(Vector2I coord)
	{
		return IsPlayableTile(coord);
	}

	public static bool IsPlayableTile(Vector2I coord)
	{
		bool inCore = coord.X >= CoreMinX && coord.X <= CoreMaxX && coord.Y >= CoreMinY && coord.Y <= CoreMaxY;
		if (inCore)
		{
			return true;
		}

		bool inNorthField = coord.X >= CoreMinX && coord.X <= CoreMaxX && coord.Y >= MinY && coord.Y < CoreMinY;
		bool inSouthField = coord.X >= CoreMinX && coord.X <= CoreMaxX && coord.Y > CoreMaxY && coord.Y <= MaxY;
		bool inWestField = coord.X >= MinX && coord.X < CoreMinX && coord.Y >= CoreMinY && coord.Y <= CoreMaxY;
		bool inEastField = coord.X > CoreMaxX && coord.X <= MaxX && coord.Y >= CoreMinY && coord.Y <= CoreMaxY;

		return inNorthField || inSouthField || inWestField || inEastField;
	}

	public static PlayerSide GetFieldOwner(Vector2I coord)
	{
		if (!IsPlayableTile(coord))
		{
			return PlayerSide.None;
		}

		if (coord.X >= CoreMinX && coord.X <= CoreMaxX && coord.Y >= MinY && coord.Y < CoreMinY)
		{
			return PlayerSide.One;
		}

		if (coord.X > CoreMaxX && coord.X <= MaxX && coord.Y >= CoreMinY && coord.Y <= CoreMaxY)
		{
			return PlayerSide.Two;
		}

		if (coord.X >= CoreMinX && coord.X <= CoreMaxX && coord.Y > CoreMaxY && coord.Y <= MaxY)
		{
			return PlayerSide.Three;
		}

		if (coord.X >= MinX && coord.X < CoreMinX && coord.Y >= CoreMinY && coord.Y <= CoreMaxY)
		{
			return PlayerSide.Four;
		}

		return PlayerSide.None;
	}

	public static IEnumerable<Vector2I> GetAllPlayableTiles()
	{
		for (int y = MinY; y <= MaxY; y++)
		{
			for (int x = MinX; x <= MaxX; x++)
			{
				Vector2I coord = new Vector2I(x, y);
				if (IsPlayableTile(coord))
				{
					yield return coord;
				}
			}
		}
	}

	public static int ManhattanDistance(Vector2I a, Vector2I b)
	{
		return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
	}

	public static FacingDirection VectorToFacing(Vector2I delta)
	{
		if (Math.Abs(delta.X) > Math.Abs(delta.Y))
		{
			return delta.X >= 0 ? FacingDirection.East : FacingDirection.West;
		}

		if (delta.Y >= 0)
		{
			return FacingDirection.South;
		}

		return FacingDirection.North;
	}

	public static Vector2I FacingToVector(FacingDirection facing)
	{
		switch (facing)
		{
			case FacingDirection.North:
				return new Vector2I(0, -1);
			case FacingDirection.East:
				return new Vector2I(1, 0);
			case FacingDirection.South:
				return new Vector2I(0, 1);
			default:
				return new Vector2I(-1, 0);
		}
	}
}
