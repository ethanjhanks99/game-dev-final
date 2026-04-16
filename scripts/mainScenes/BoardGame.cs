using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BoardGame : Node2D
{
	private const int TilePixelSize = 28;
	private readonly Vector2 _boardOrigin = new Vector2(350, 220);
	private readonly PlayerSide[] _turnOrder = { PlayerSide.One, PlayerSide.Two, PlayerSide.Three, PlayerSide.Four };

	private GridCombatController _controller;
	private MoveHighlightOverlay _overlay;
	private Label _statusLabel;
	private RichTextLabel _logLabel;

	// Grass tile extracted from the top-left 32x32 region of Tileset.png.
	private Texture2D _grassTexture;
	private static readonly Rect2 GrassSrcRegion = new Rect2(0, 0, 32, 32);

	private readonly HashSet<Vector2I> _reachableTiles = new HashSet<Vector2I>();
	private readonly HashSet<PlayerSide> _lockedPlayers = new HashSet<PlayerSide>();
	private readonly Dictionary<PlayerSide, PlayerTurnSelection> _pendingSelections = new Dictionary<PlayerSide, PlayerTurnSelection>();

	private PlayerSide _activePlayer = PlayerSide.One;
	private string _selectedUnitId;

	public override void _Ready()
	{
		_controller = GetNode<GridCombatController>("GridCombatController");
		_overlay = GetNode<MoveHighlightOverlay>("MoveHighlightOverlay");
		_statusLabel = GetNode<Label>("CanvasLayer/UI/Margin/VBox/StatusLabel");
		_logLabel = GetNode<RichTextLabel>("CanvasLayer/UI/Margin/VBox/LogLabel");

		GetNode<Button>("CanvasLayer/UI/Margin/VBox/EndTurnButton").Pressed += OnEndTurnPressed;
		GetNode<Button>("CanvasLayer/UI/Margin/VBox/ResolveTurnButton").Pressed += OnResolveTurnPressed;
		GetNode<Button>("CanvasLayer/UI/Margin/VBox/MainMenuButton").Pressed += OnMainMenuPressed;

		_overlay.TilePixelSize = TilePixelSize;
		_overlay.Position = _boardOrigin;

		// Load the tileset and cut out just the base grass tile from the top-left corner.
		_grassTexture = GD.Load<Texture2D>("res://assets/textures/Tileset.png");

		ResetTurnPlanning();
		SpawnDemoArmies();
		UpdateStatusText();
		AppendLog("Board initialized. Select your own unit with left-click.");
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawBoardTiles();
		DrawUnits();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			Vector2I tile = ScreenToTile(mouseButton.Position);
			if (!GridTypes.IsPlayableTile(tile))
			{
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				HandleLeftClick(tile);
			}
			else if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				HandleRightClick(tile);
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)
			{
				OnEndTurnPressed();
			}
			else if (keyEvent.Keycode == Key.R)
			{
				OnResolveTurnPressed();
			}
		}
	}

	private void DrawBoardTiles()
	{
		foreach (Vector2I tile in GridTypes.GetAllPlayableTiles())
		{
			Rect2 rect = new Rect2(TileToScreen(tile), new Vector2(TilePixelSize, TilePixelSize));

			// Draw the base grass texture stretched to tile size.
			if (_grassTexture != null)
			{
				DrawTextureRectRegion(_grassTexture, rect, GrassSrcRegion);
			}
			else
			{
				DrawRect(rect, new Color(0.45f, 0.65f, 0.35f), true);
			}

			// Draw a semi-transparent color overlay to distinguish base tiles,
			// player fields, reachable tiles, etc.
			Color overlay = GetTileOverlayColor(tile);
			if (overlay.A > 0f)
			{
				DrawRect(rect, overlay, true);
			}

			DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 0.5f), false, 1.0f);
		}
	}

	private void DrawUnits()
	{
		foreach (BoardUnit unit in _controller.Board.Units)
		{
			Vector2 center = TileToScreen(unit.Position) + new Vector2(TilePixelSize * 0.5f, TilePixelSize * 0.5f);
			Color unitColor = GetPlayerColor(unit.Owner);
			DrawCircle(center, TilePixelSize * 0.32f, unitColor);

			if (unit.Id == _selectedUnitId)
			{
				DrawArc(center, TilePixelSize * 0.4f, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f), 2f);
			}

			Vector2I facing = GridTypes.FacingToVector(unit.Facing);
			Vector2 facingVector = new Vector2(facing.X, facing.Y) * (TilePixelSize * 0.22f);
			DrawLine(center, center + facingVector, new Color(0.05f, 0.05f, 0.05f), 2f);
		}
	}

	private void HandleLeftClick(Vector2I tile)
	{
		if (!_controller.Board.TryGetUnitAt(tile, out BoardUnit unit))
		{
			ClearSelection();
			return;
		}

		if (unit.Owner != _activePlayer)
		{
			AppendLog($"It is {PlayerName(_activePlayer)}'s turn. You cannot select {PlayerName(unit.Owner)} units now.");
			return;
		}

		_selectedUnitId = unit.Id;
		_reachableTiles.Clear();
		foreach (Vector2I coord in _controller.ComputeReachableTiles(unit.Id))
		{
			_reachableTiles.Add(coord);
		}

		_overlay.SetHighlightedTiles(_reachableTiles);
		UpdateStatusText();
		QueueRedraw();
	}

	private void HandleRightClick(Vector2I tile)
	{
		if (string.IsNullOrWhiteSpace(_selectedUnitId))
		{
			AppendLog("Select a unit first.");
			return;
		}

		if (!_controller.Board.TryGetUnit(_selectedUnitId, out BoardUnit selected) || selected.Owner != _activePlayer)
		{
			AppendLog("Selected unit is no longer valid for this player.");
			ClearSelection();
			return;
		}

		PlayerTurnSelection selection = _pendingSelections[_activePlayer];
		if (_controller.Board.TryGetUnitAt(tile, out BoardUnit target))
		{
			TryQueueAttack(selection, selected, target);
			return;
		}

		if (!_reachableTiles.Contains(tile))
		{
			AppendLog("Target tile is not in valid movement range.");
			return;
		}

		selection.Moves.RemoveAll(move => move.UnitId == selected.Id);
		selection.Attacks.RemoveAll(attack => attack.AttackerUnitId == selected.Id);
		selection.Moves.Add(new MoveOrder(selected.Id, tile));
		AppendLog($"Queued move for {selected.Id} to {tile}.");
		UpdateStatusText();
	}

	private void TryQueueAttack(PlayerTurnSelection selection, BoardUnit attacker, BoardUnit target)
	{
		if (attacker.Owner == target.Owner)
		{
			AppendLog("Friendly fire is disabled.");
			return;
		}

		int distance = GridTypes.ManhattanDistance(attacker.Position, target.Position);
		if (distance < 1 || distance > attacker.Stats.AttackRange)
		{
			AppendLog("Target is out of attack range.");
			return;
		}

		selection.Moves.RemoveAll(move => move.UnitId == attacker.Id);
		selection.Attacks.RemoveAll(attack => attack.AttackerUnitId == attacker.Id);
		selection.Attacks.Add(new AttackOrder(attacker.Id, target.Id));
		AppendLog($"Queued attack: {attacker.Id} -> {target.Id}.");
		UpdateStatusText();
	}

	private void OnEndTurnPressed()
	{
		if (_lockedPlayers.Contains(_activePlayer))
		{
			AppendLog($"{PlayerName(_activePlayer)} is already locked in.");
			return;
		}

		_lockedPlayers.Add(_activePlayer);
		AppendLog($"{PlayerName(_activePlayer)} locked in orders.");

		if (_lockedPlayers.Count >= _turnOrder.Length)
		{
			ResolveAllLockedTurns();
			return;
		}

		AdvanceToNextUnlockedPlayer();
		ClearSelection();
		UpdateStatusText();
	}

	private void OnResolveTurnPressed()
	{
		if (_lockedPlayers.Count == 0)
		{
			AppendLog("No players locked yet. Press End Player Turn first.");
			return;
		}

		ResolveAllLockedTurns();
	}

	private void ResolveAllLockedTurns()
	{
		IEnumerable<PlayerTurnSelection> selections = _turnOrder.Select(side => _pendingSelections[side]);
		TurnResolutionReport report = _controller.ResolveTurn(selections);

		AppendLog($"Resolved turn: {report.MoveResults.Count} move checks, {report.AttackResults.Count} attack checks, {report.RemovedUnits.Count} unit removals.");
		foreach (string removed in report.RemovedUnits)
		{
			AppendLog($"Removed unit: {removed}");
		}

		ResetTurnPlanning();
		_activePlayer = PlayerSide.One;
		ClearSelection();
		UpdateStatusText();
		QueueRedraw();
	}

	private void OnMainMenuPressed()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.LoadMainMenu();
		}
	}

	private void AdvanceToNextUnlockedPlayer()
	{
		int currentIndex = Array.IndexOf(_turnOrder, _activePlayer);
		for (int step = 1; step <= _turnOrder.Length; step++)
		{
			PlayerSide candidate = _turnOrder[(currentIndex + step) % _turnOrder.Length];
			if (!_lockedPlayers.Contains(candidate))
			{
				_activePlayer = candidate;
				return;
			}
		}
	}

	private void ResetTurnPlanning()
	{
		_lockedPlayers.Clear();
		_pendingSelections.Clear();
		foreach (PlayerSide side in _turnOrder)
		{
			_pendingSelections[side] = new PlayerTurnSelection(side);
		}
	}

	private void SpawnDemoArmies()
	{
		// Starting units are placed near each player field entrance to speed up local gameplay testing.
		SpawnPlayerSet(PlayerSide.One,
			(UnitType.Infantry, "P1-INF", new Vector2I(3, -1), FacingDirection.South),
			(UnitType.Cavalry, "P1-CAV", new Vector2I(4, -2), FacingDirection.South),
			(UnitType.Archer, "P1-ARC", new Vector2I(2, -2), FacingDirection.South));

		SpawnPlayerSet(PlayerSide.Two,
			(UnitType.Infantry, "P2-INF", new Vector2I(8, 3), FacingDirection.West),
			(UnitType.Cavalry, "P2-CAV", new Vector2I(9, 4), FacingDirection.West),
			(UnitType.Archer, "P2-ARC", new Vector2I(9, 2), FacingDirection.West));

		SpawnPlayerSet(PlayerSide.Three,
			(UnitType.Infantry, "P3-INF", new Vector2I(4, 8), FacingDirection.North),
			(UnitType.Cavalry, "P3-CAV", new Vector2I(3, 9), FacingDirection.North),
			(UnitType.Archer, "P3-ARC", new Vector2I(5, 9), FacingDirection.North));

		SpawnPlayerSet(PlayerSide.Four,
			(UnitType.Infantry, "P4-INF", new Vector2I(-1, 4), FacingDirection.East),
			(UnitType.Cavalry, "P4-CAV", new Vector2I(-2, 3), FacingDirection.East),
			(UnitType.Archer, "P4-ARC", new Vector2I(-2, 5), FacingDirection.East));
	}

	private void SpawnPlayerSet(PlayerSide side, params (UnitType type, string id, Vector2I pos, FacingDirection facing)[] units)
	{
		foreach ((UnitType type, string id, Vector2I pos, FacingDirection facing) in units)
		{
			if (!_controller.SpawnUnit(type, id, side, pos, facing, out string reason))
			{
				AppendLog($"Failed to spawn {id}: {reason}");
			}
		}
	}

	private void ClearSelection()
	{
		_selectedUnitId = string.Empty;
		_reachableTiles.Clear();
		_overlay.ClearHighlights();
		UpdateStatusText();
		QueueRedraw();
	}

	private Vector2I ScreenToTile(Vector2 screenPosition)
	{
		Vector2 local = (screenPosition - _boardOrigin) / TilePixelSize;
		return new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
	}

	private Vector2 TileToScreen(Vector2I tile)
	{
		return _boardOrigin + new Vector2(tile.X * TilePixelSize, tile.Y * TilePixelSize);
	}

	// Returns a semi-transparent overlay drawn on top of the grass texture.
	// Transparent (alpha=0) means no overlay — just the grass shows through.
	private Color GetTileOverlayColor(Vector2I tile)
	{
		if (_controller.Board.IsBaseTile(tile))
		{
			// Gold tint for base tiles.
			return new Color(0.95f, 0.78f, 0.25f, 0.55f);
		}

		if (_reachableTiles.Contains(tile))
		{
			// Green tint for valid movement tiles.
			return new Color(0.42f, 0.78f, 0.38f, 0.50f);
		}

		PlayerSide owner = GridTypes.GetFieldOwner(tile);
		if (owner != PlayerSide.None)
		{
			// Subtle player-color tint for field ownership.
			Color ownerColor = GetPlayerColor(owner);
			return new Color(ownerColor.R, ownerColor.G, ownerColor.B, 0.22f);
		}

		// Core board: no overlay, plain grass shows through.
		return new Color(0f, 0f, 0f, 0f);
	}

	private Color GetPlayerColor(PlayerSide side)
	{
		switch (side)
		{
			case PlayerSide.One:
				return new Color(0.24f, 0.64f, 0.95f);
			case PlayerSide.Two:
				return new Color(0.92f, 0.34f, 0.30f);
			case PlayerSide.Three:
				return new Color(0.24f, 0.74f, 0.38f);
			case PlayerSide.Four:
				return new Color(0.93f, 0.63f, 0.22f);
			default:
				return new Color(0.7f, 0.7f, 0.7f);
		}
	}

	private string PlayerName(PlayerSide side)
	{
		switch (side)
		{
			case PlayerSide.One:
				return "Player 1";
			case PlayerSide.Two:
				return "Player 2";
			case PlayerSide.Three:
				return "Player 3";
			case PlayerSide.Four:
				return "Player 4";
			default:
				return "Unknown";
		}
	}

	private void UpdateStatusText()
	{
		PlayerTurnSelection selection = _pendingSelections[_activePlayer];
		string selectionLabel = string.IsNullOrWhiteSpace(_selectedUnitId) ? "none" : _selectedUnitId;
		_statusLabel.Text = $"Active: {PlayerName(_activePlayer)} | Selected: {selectionLabel} | Queued Moves: {selection.Moves.Count} | Queued Attacks: {selection.Attacks.Count} | Locked: {_lockedPlayers.Count}/4";
	}

	private void AppendLog(string message)
	{
		if (_logLabel.Text.Length > 3000)
		{
			_logLabel.Text = _logLabel.Text.Substring(_logLabel.Text.Length - 2500);
		}

		_logLabel.Text += $"\n[{Time.GetTimeStringFromSystem()}] {message}";
		_logLabel.ScrollToLine(_logLabel.GetLineCount());
	}
}
