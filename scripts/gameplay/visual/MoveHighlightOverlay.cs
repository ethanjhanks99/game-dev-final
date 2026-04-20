using Godot;
using System.Collections.Generic;

public partial class MoveHighlightOverlay : Node2D
{
	[Export]
	public int TilePixelSize { get; set; } = 64;

	// Color for frontier tiles (the 1–3 options the player can step to next).
	[Export]
	public Color FrontierColor { get; set; } = new Color(0.2f, 0.8f, 0.3f, 0.35f);

	[Export]
	public Color FrontierBorderColor { get; set; } = new Color(0.1f, 0.4f, 0.2f, 0.9f);

	// Color for tiles already committed to in the path.
	[Export]
	public Color PathColor { get; set; } = new Color(0.2f, 0.55f, 0.95f, 0.45f);

	[Export]
	public Color PathBorderColor { get; set; } = new Color(0.1f, 0.25f, 0.7f, 0.9f);

	// Legacy single-set highlight support (used when not in path-building mode).
	[Export]
	public Color MoveColor { get; set; } = new Color(0.2f, 0.8f, 0.3f, 0.35f);

	[Export]
	public Color BorderColor { get; set; } = new Color(0.1f, 0.4f, 0.2f, 0.9f);

	private readonly HashSet<Vector2I> _highlightedTiles = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _pathTiles = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _frontierTiles = new HashSet<Vector2I>();

	// ---- Legacy API (retained for compatibility) ----

	public void SetHighlightedTiles(IEnumerable<Vector2I> tiles)
	{
		_highlightedTiles.Clear();
		_pathTiles.Clear();
		_frontierTiles.Clear();
		foreach (Vector2I tile in tiles)
		{
			_highlightedTiles.Add(tile);
		}
		QueueRedraw();
	}

	public void ClearHighlights()
	{
		_highlightedTiles.Clear();
		_pathTiles.Clear();
		_frontierTiles.Clear();
		QueueRedraw();
	}

	// ---- Path-building API ----

	/// <summary>
	/// Sets the currently committed path tiles and the frontier (next-step options)
	/// for path-building movement mode.
	/// </summary>
	public void SetPathHighlights(IEnumerable<Vector2I> pathTiles, IEnumerable<Vector2I> frontierTiles)
	{
		_highlightedTiles.Clear();
		_pathTiles.Clear();
		_frontierTiles.Clear();

		foreach (Vector2I tile in pathTiles)
		{
			_pathTiles.Add(tile);
		}
		foreach (Vector2I tile in frontierTiles)
		{
			_frontierTiles.Add(tile);
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		// Legacy single-set highlight mode.
		foreach (Vector2I tile in _highlightedTiles)
		{
			Rect2 rect = new Rect2(tile.X * TilePixelSize, tile.Y * TilePixelSize, TilePixelSize, TilePixelSize);
			DrawRect(rect, MoveColor, true);
			DrawRect(rect, BorderColor, false, 2f);
		}

		// Path-building: committed path tiles (blue).
		foreach (Vector2I tile in _pathTiles)
		{
			Rect2 rect = new Rect2(tile.X * TilePixelSize, tile.Y * TilePixelSize, TilePixelSize, TilePixelSize);
			DrawRect(rect, PathColor, true);
			DrawRect(rect, PathBorderColor, false, 2f);
		}

		// Path-building: frontier tiles (green).
		foreach (Vector2I tile in _frontierTiles)
		{
			Rect2 rect = new Rect2(tile.X * TilePixelSize, tile.Y * TilePixelSize, TilePixelSize, TilePixelSize);
			DrawRect(rect, FrontierColor, true);
			DrawRect(rect, FrontierBorderColor, false, 2f);
		}
	}
}