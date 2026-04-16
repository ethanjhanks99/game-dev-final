using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class BaseAreaDefinitions
{
	public const int BaseWidth = 3;
	public const int BaseHeight = 4;

	public static IReadOnlyDictionary<PlayerSide, HashSet<Vector2I>> BuildBaseAreas()
	{
		Dictionary<PlayerSide, HashSet<Vector2I>> areas = new Dictionary<PlayerSide, HashSet<Vector2I>>();

		areas[PlayerSide.One] = CreateNorthBaseArea();
		areas[PlayerSide.Two] = CreateEastBaseArea();
		areas[PlayerSide.Three] = CreateSouthBaseArea();
		areas[PlayerSide.Four] = CreateWestBaseArea();

		return areas;
	}

	private static HashSet<Vector2I> CreateNorthBaseArea()
	{
		HashSet<Vector2I> tiles = new HashSet<Vector2I>();
		int centerX = 3;
		int centerY = GridTypes.MinY;

		for (int x = centerX; x < centerX + BaseWidth; x++)
		{
			for (int y = centerY; y < centerY + BaseHeight; y++)
			{
				Vector2I tile = new Vector2I(x, y);
				if (GridTypes.IsPlayableTile(tile))
				{
					tiles.Add(tile);
				}
			}
		}

		return tiles;
	}

	private static HashSet<Vector2I> CreateEastBaseArea()
	{
		HashSet<Vector2I> tiles = new HashSet<Vector2I>();
		int centerX = GridTypes.MaxX - BaseWidth + 1;
		int centerY = 3;

		for (int x = centerX; x <= GridTypes.MaxX; x++)
		{
			for (int y = centerY; y < centerY + BaseHeight; y++)
			{
				Vector2I tile = new Vector2I(x, y);
				if (GridTypes.IsPlayableTile(tile))
				{
					tiles.Add(tile);
				}
			}
		}

		return tiles;
	}

	private static HashSet<Vector2I> CreateSouthBaseArea()
	{
		HashSet<Vector2I> tiles = new HashSet<Vector2I>();
		int centerX = 4;
		int centerY = GridTypes.MaxY - BaseHeight + 1;

		for (int x = centerX; x < centerX + BaseWidth; x++)
		{
			for (int y = centerY; y <= GridTypes.MaxY; y++)
			{
				Vector2I tile = new Vector2I(x, y);
				if (GridTypes.IsPlayableTile(tile))
				{
					tiles.Add(tile);
				}
			}
		}

		return tiles;
	}

	private static HashSet<Vector2I> CreateWestBaseArea()
	{
		HashSet<Vector2I> tiles = new HashSet<Vector2I>();
		int centerX = GridTypes.MinX;
		int centerY = 4;

		for (int x = centerX; x < centerX + BaseWidth; x++)
		{
			for (int y = centerY; y < centerY + BaseHeight; y++)
			{
				Vector2I tile = new Vector2I(x, y);
				if (GridTypes.IsPlayableTile(tile))
				{
					tiles.Add(tile);
				}
			}
		}

		return tiles;
	}

	public static bool IsTilePartOfBase(Vector2I tile, IReadOnlyDictionary<PlayerSide, HashSet<Vector2I>> areas, PlayerSide expectedOwner)
	{
		if (!areas.TryGetValue(expectedOwner, out HashSet<Vector2I> baseArea))
		{
			return false;
		}

		return baseArea.Contains(tile);
	}

	public static PlayerSide GetBaseOwnerAtTile(Vector2I tile, IReadOnlyDictionary<PlayerSide, HashSet<Vector2I>> areas)
	{
		foreach (KeyValuePair<PlayerSide, HashSet<Vector2I>> entry in areas)
		{
			if (entry.Value.Contains(tile))
			{
				return entry.Key;
			}
		}

		return PlayerSide.None;
	}
}
