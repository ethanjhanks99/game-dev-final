using Godot;
using System.Collections.Generic;

public partial class MoveHighlightOverlay : Node2D
{
	[Export]
	public int TilePixelSize { get; set; } = 64;

	[Export]
	public Color MoveColor { get; set; } = new Color(0.2f, 0.8f, 0.3f, 0.35f);

	[Export]
	public Color BorderColor { get; set; } = new Color(0.1f, 0.4f, 0.2f, 0.9f);

	private readonly HashSet<Vector2I> _highlightedTiles = new HashSet<Vector2I>();

	public void SetHighlightedTiles(IEnumerable<Vector2I> tiles)
	{
		_highlightedTiles.Clear();
		foreach (Vector2I tile in tiles)
		{
			_highlightedTiles.Add(tile);
		}

		QueueRedraw();
	}

	public void ClearHighlights()
	{
		_highlightedTiles.Clear();
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (Vector2I tile in _highlightedTiles)
		{
			Rect2 rect = new Rect2(tile.X * TilePixelSize, tile.Y * TilePixelSize, TilePixelSize, TilePixelSize);
			DrawRect(rect, MoveColor, true);
			DrawRect(rect, BorderColor, false, 2f);
		}
	}
}
