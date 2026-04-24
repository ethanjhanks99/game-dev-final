using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BoardGame : Node2D
{
	private const int TilePixelSize = 28;
	private const double TurnTimeLimitSeconds = 30.0;

	private readonly Vector2 _boardOrigin = new Vector2(350, 220);
	private readonly PlayerSide[] _turnOrder = { PlayerSide.One, PlayerSide.Two, PlayerSide.Three, PlayerSide.Four };

	private GridCombatController _controller;
	private MoveHighlightOverlay _overlay;
	private Label _statusLabel;
	private Label _timerLabel;
	private Label _purchaseLabel;
	private RichTextLabel _logLabel;
	private Button _buyInfantryButton;
	private Button _buyArcherButton;
	private Button _buyCavalryButton;
	private Button _confirmPlacementButton;

	// Grass tile extracted from the top-left 32x32 region of Tileset.png.
	private Texture2D _grassTexture;
	private static readonly Rect2 GrassSrcRegion = new Rect2(0, 0, 32, 32);
	private static readonly Rect2 MtnSrcRegion = new Rect2(32, 0, 32, 32);
	// Palace tiles extracted from Palace.png
	private Texture2D _palaceTexture;
	private static readonly Rect2[] PalaceSrcRegion = {new Rect2(0, 0, 128, 96), new Rect2(96, 128, 96, 128), 
		new Rect2(128, 0, 128, 96), new Rect2(96, 0, 96, 128)};



	private Texture2D _infantryTexture;
	private Texture2D _archerTexture;
	private Texture2D _calvaryTexture;

	private readonly HashSet<PlayerSide> _lockedPlayers = new HashSet<PlayerSide>();
	private readonly Dictionary<PlayerSide, PlayerTurnSelection> _pendingSelections = new Dictionary<PlayerSide, PlayerTurnSelection>();
	private readonly Dictionary<PlayerSide, int> _unitPointsByPlayer = new Dictionary<PlayerSide, int>();
	private readonly Dictionary<PlayerSide, int> _turnsTakenByPlayer = new Dictionary<PlayerSide, int>();
	private readonly Dictionary<PlayerSide, int> _nextSpawnIndexByPlayer = new Dictionary<PlayerSide, int>();
	private readonly IReadOnlyDictionary<PlayerSide, HashSet<Vector2I>> _baseAreas = BaseAreaDefinitions.BuildBaseAreas();

	// Per-unit committed path states for the active player (persists until deselected or turn resolves).
	private readonly Dictionary<string, PathMovementState> _unitPaths = new Dictionary<string, PathMovementState>();

	private PlayerSide _activePlayer = PlayerSide.One;
	private string _selectedUnitId;

	// The active path-building state for the currently selected unit (null when nothing is selected).
	private PathMovementState _activePath;
	private UnitType? _pendingPlacementType;
	private Vector2I? _pendingPlacementTile;
	private readonly List<Vector2I> _pendingPlacementCandidates = new List<Vector2I>();

	// Turn timer state.
	private double _timeRemaining;
	private bool _timerRunning;

	public override void _Ready()
	{
		_controller = GetNode<GridCombatController>("GridCombatController");
		_overlay = GetNode<MoveHighlightOverlay>("MoveHighlightOverlay");
		_statusLabel = GetNode<Label>("CanvasLayer/UI/Margin/VBox/StatusLabel");
		_timerLabel = GetNode<Label>("CanvasLayer/UI/Margin/VBox/TimerLabel");
		_purchaseLabel = GetNode<Label>("CanvasLayer/UI/Margin/VBox/PurchaseLabel");
		_logLabel = GetNode<RichTextLabel>("CanvasLayer/UI/Margin/VBox/LogLabel");
		_buyInfantryButton = GetNode<Button>("CanvasLayer/UI/Margin/VBox/PurchaseButtons/BuyInfantryButton");
		_buyArcherButton = GetNode<Button>("CanvasLayer/UI/Margin/VBox/PurchaseButtons/BuyArcherButton");
		_buyCavalryButton = GetNode<Button>("CanvasLayer/UI/Margin/VBox/PurchaseButtons/BuyCavalryButton");
		_confirmPlacementButton = GetNode<Button>("CanvasLayer/UI/Margin/VBox/ConfirmPlacementButton");

		GetNode<Button>("CanvasLayer/UI/Margin/VBox/EndTurnButton").Pressed += OnEndTurnPressed;
		GetNode<Button>("CanvasLayer/UI/Margin/VBox/ResolveTurnButton").Pressed += OnResolveTurnPressed;
		GetNode<Button>("CanvasLayer/UI/Margin/VBox/MainMenuButton").Pressed += OnMainMenuPressed;
		_buyInfantryButton.Pressed += () => TryPurchaseAndPlaceUnit(UnitType.Infantry);
		_buyArcherButton.Pressed += () => TryPurchaseAndPlaceUnit(UnitType.Archer);
		_buyCavalryButton.Pressed += () => TryPurchaseAndPlaceUnit(UnitType.Cavalry);
		_confirmPlacementButton.Pressed += OnConfirmPlacementPressed;

		_overlay.TilePixelSize = TilePixelSize;
		_overlay.Position = _boardOrigin;

		// Load the tileset and cut out just the base grass tile from the top-left corner.
		_grassTexture = GD.Load<Texture2D>("res://assets/textures/Tiles/Tileset.png");
		_palaceTexture = GD.Load<Texture2D>("res://assets/textures/Tiles/Palace.png");

		_infantryTexture = GD.Load<Texture2D>("res://assets/textures/UnitSheets/CompleteInfantry.png");
		_archerTexture = GD.Load<Texture2D>("res://assets/textures/UnitSheets/CompleteArcher.png");
		_calvaryTexture = GD.Load<Texture2D>("res://assets/textures/UnitSheets/CompleteKnight.png");

		ResetTurnPlanning();
		InitializeUnitEconomy();
		OnPlayerTurnStarted(_activePlayer);
		UpdateStatusText();
		StartTurnTimer();
		AppendLog("Board initialized. Left-click your unit to select, then click highlighted tiles to build a path. Right-click to cancel. Click unit again or click away to commit path.");
		AppendLog("Unit purchases: [I] Infantry (1), [A] Archer (2), [C] Cavalry (3). Each player starts with 12 unit points.");
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!_timerRunning) return;

		_timeRemaining -= delta;

		if (_timeRemaining <= 0.0)
		{
			_timeRemaining = 0.0;
			OnTurnTimerExpired();
		}

		UpdateTimerLabel();
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
			else if (keyEvent.Keycode == Key.I)
			{
				TryPurchaseAndPlaceUnit(UnitType.Infantry);
			}
			else if (keyEvent.Keycode == Key.A)
			{
				TryPurchaseAndPlaceUnit(UnitType.Archer);
			}
			else if (keyEvent.Keycode == Key.C)
			{
				TryPurchaseAndPlaceUnit(UnitType.Cavalry);
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
		foreach (Vector2I tile in GridTypes.GetAllBorderTiles())
		{
			Rect2 rect = new Rect2(TileToScreen(tile), new Vector2(TilePixelSize, TilePixelSize));

			// Draw the base grass texture stretched to tile size.
			if (_grassTexture != null)
			{
				DrawTextureRectRegion(_grassTexture, rect, MtnSrcRegion);
			}
			else
			{
				DrawRect(rect, new Color(0.4f, 0.4f, 0.4f), true);
			}
		}
		Rect2 palaceRect = new Rect2(TileToScreen(new Vector2I(2, -6)), new Vector2(TilePixelSize * 4, TilePixelSize * 3));
		DrawTextureRectRegion(_palaceTexture, palaceRect, PalaceSrcRegion[0]);
		Image image = _palaceTexture.GetImage();
		for (int i = 0; i < 3; i++)
		{
        	image.Rotate90(ClockDirection.Clockwise);
        	Texture2D rotatedTexture = ImageTexture.CreateFromImage(image);

			switch(i)
			{
				case 0:
					palaceRect = new Rect2(TileToScreen(new Vector2I(11, 2)), new Vector2(TilePixelSize * 3, TilePixelSize * 4));
					break;
				case 1:
					palaceRect = new Rect2(TileToScreen(new Vector2I(2, 11)), new Vector2(TilePixelSize * 4, TilePixelSize * 3));
					break;
				case 2:
					palaceRect = new Rect2(TileToScreen(new Vector2I(-6, 2)), new Vector2(TilePixelSize * 3, TilePixelSize * 4));
					break;
				default:
					break;
			}
			DrawTextureRectRegion(rotatedTexture, palaceRect, PalaceSrcRegion[1 + i]);
		}
		

		
		//GD.Print(new Rect2(PalaceSrcRegion.Position.X + 128, PalaceSrcRegion.Position.Y, 128, 96));
	}

	private void DrawUnits()
	{
		foreach (BoardUnit unit in _controller.Board.Units)
		{
			PlayerSide player = unit.Owner;
			Rect2 spriteLoc;
			switch (player)
			{
				case PlayerSide.One:
					spriteLoc = new Rect2(0,0,32,32);
					break;
				case PlayerSide.Two:
					spriteLoc = new Rect2(32,32,32,32);
					break;
				case PlayerSide.Three:
					spriteLoc = new Rect2(64,64,32,32);
					break;
				case PlayerSide.Four:
					spriteLoc = new Rect2(96,96,32,32);
					break;
				default:
					spriteLoc = new Rect2(0,0,32,32);
					break;
			}
			Rect2 rect = new Rect2(TileToScreen(unit.Position), new Vector2(TilePixelSize, TilePixelSize));
			if (unit is InfantryUnit)
			{
				DrawTextureRectRegion(_infantryTexture, rect, spriteLoc);
			} else if (unit is ArcherUnit)
			{
				DrawTextureRectRegion(_archerTexture, rect, spriteLoc);
			} else if (unit is CavalryUnit)
			{
				DrawTextureRectRegion(_calvaryTexture, rect, spriteLoc);
			}

			Vector2 center = TileToScreen(unit.Position) + new Vector2(TilePixelSize * 0.5f, TilePixelSize * 0.5f);
			//Color unitColor = GetPlayerColor(unit.Owner);
			//DrawCircle(center, TilePixelSize * 0.32f, unitColor);

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
		if (_pendingPlacementType.HasValue)
		{
			HandlePlacementTileSelection(tile);
			return;
		}

		// --- Case 1: Clicked on a unit ---
		if (_controller.Board.TryGetUnitAt(tile, out BoardUnit unit))
		{
			if (unit.Owner != _activePlayer)
			{
				AppendLog($"It is {PlayerName(_activePlayer)}'s turn. You cannot select {PlayerName(unit.Owner)} units now.");
				return;
			}

			// Clicking the already-selected unit trims the path back to origin (full reset).
			// If there was no path built yet, deselect instead.
			if (unit.Id == _selectedUnitId)
			{
				if (_activePath != null && _activePath.HasPath)
				{
					_activePath.TryTrimToTile(_activePath.Origin);
					AppendLog($"Path for {_selectedUnitId} reset to origin.");
					RefreshPathOverlay();
					UpdateStatusText();
				}
				else
				{
					CommitActivePathAndDeselect();
				}
				return;
			}

			// Switching to a different unit — commit current path first.
			if (_activePath != null)
			{
				CommitCurrentPath();
			}

			SelectUnit(unit);
			return;
		}

		// --- Case 2: Clicked on a frontier tile (extend path) ---
		if (_activePath != null)
		{
			IReadOnlyList<Vector2I> frontier = _activePath.GetFrontierTiles();
			if (frontier.Contains(tile))
			{
				_activePath.TryExtendPath(tile);
				RefreshPathOverlay();

				if (_activePath.IsComplete)
				{
					AppendLog($"Path for {_selectedUnitId} is complete (movement exhausted). Path locked in.");
					CommitCurrentPath();
				}
				return;
			}

			// --- Case 3: Clicked on a tile already in the path (backtrack) ---
			// This includes clicking the origin tile to reset the whole path.
			if (_activePath.IsPathTile(tile))
			{
				bool trimmed = _activePath.TryTrimToTile(tile);
				if (trimmed)
				{
					string label = tile == _activePath.Origin ? "origin (full reset)" : tile.ToString();
					AppendLog($"Path for {_selectedUnitId} trimmed back to {label}.");
					RefreshPathOverlay();
					UpdateStatusText();
				}
				return;
			}
		}

		// --- Case 4: Clicked on empty non-frontier, non-path tile — deselect ---
		CommitActivePathAndDeselect();
	}

	private void HandleRightClick(Vector2I tile)
	{
		if (_pendingPlacementType.HasValue)
		{
			CancelPendingPlacement("Placement selection cancelled.");
			return;
		}

		// Right-click cancels the current unit selection and discards the in-progress path.
		if (!string.IsNullOrWhiteSpace(_selectedUnitId))
		{
			AppendLog($"Cancelled path for {_selectedUnitId}.");
			// Remove any previously committed path for this unit from pending selections.
			if (_unitPaths.ContainsKey(_selectedUnitId))
			{
				_unitPaths.Remove(_selectedUnitId);
				PlayerTurnSelection selection = _pendingSelections[_activePlayer];
				selection.Moves.RemoveAll(move => move.UnitId == _selectedUnitId);
			}
		}
		ClearSelection();
	}

	// ---- Path-building helpers ----

	/// <summary>Selects a unit and initializes a fresh (or resumable) path state for it.</summary>
	private void SelectUnit(BoardUnit unit)
	{
		_selectedUnitId = unit.Id;

		// Resume an existing in-progress path if the player re-selects the unit.
		if (_unitPaths.TryGetValue(unit.Id, out PathMovementState existing))
		{
			_activePath = existing;
		}
		else
		{
			_activePath = new PathMovementState(unit, _controller.Board.SnapshotOccupancy());
			// Don't store in _unitPaths yet — only store once the player commits at least one step.
		}

		RefreshPathOverlay();
		UpdateStatusText();
		AppendLog($"Selected {unit.Id}. Click highlighted tiles to build a path. Right-click to cancel.");
	}

	/// <summary>Refreshes the overlay to show the current path and frontier.</summary>
	private void RefreshPathOverlay()
	{
		if (_activePath == null)
		{
			_overlay.ClearHighlights();
			QueueRedraw();
			return;
		}

		_overlay.SetPathHighlights(_activePath.CommittedPath, _activePath.GetFrontierTiles());
		QueueRedraw();
	}

	/// <summary>
	/// Writes the active path into the pending move selection, stores it in _unitPaths,
	/// but keeps the unit selected (player can keep extending if they re-select later).
	/// </summary>
	private void CommitCurrentPath()
	{
		if (_activePath == null || !_activePath.HasPath) return;

		string unitId = _selectedUnitId;
		Vector2I destination = _activePath.GetFinalDestination();

		PlayerTurnSelection selection = _pendingSelections[_activePlayer];
		selection.Moves.RemoveAll(move => move.UnitId == unitId);
		selection.Moves.Add(new MoveOrder(unitId, destination));

		_unitPaths[unitId] = _activePath;
		AppendLog($"Path committed for {unitId}: destination {destination}.");
		UpdateStatusText();
	}

	/// <summary>Commits the active path (if any) and then clears the selection.</summary>
	private void CommitActivePathAndDeselect()
	{
		CommitCurrentPath();
		ClearSelection();
	}

	private void OnEndTurnPressed()
	{
		if (_pendingPlacementType.HasValue)
		{
			CancelPendingPlacement("Pending placement was cleared when ending turn.");
		}

		if (_lockedPlayers.Contains(_activePlayer))
		{
			AppendLog($"{PlayerName(_activePlayer)} is already locked in.");
			return;
		}

		// Auto-commit any path the player was mid-building before locking in.
		if (_activePath != null && _activePath.HasPath)
		{
			CommitCurrentPath();
		}

		_lockedPlayers.Add(_activePlayer);
		AppendLog($"{PlayerName(_activePlayer)} locked in orders.");

		if (_lockedPlayers.Count >= _turnOrder.Length)
		{
			ResolveAllLockedTurns();
			return;
		}

		AdvanceToNextUnlockedPlayer();
		OnPlayerTurnStarted(_activePlayer);
		ClearSelection();
		RestartTurnTimer();
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
		if (_pendingPlacementType.HasValue)
		{
			CancelPendingPlacement("Pending placement cleared before turn resolution.");
		}

		StopTurnTimer();

		IEnumerable<PlayerTurnSelection> selections = _turnOrder.Select(side => _pendingSelections[side]);
		TurnResolutionReport report = _controller.ResolveTurn(selections);

		AppendLog($"Resolved turn: {report.MoveResults.Count} move checks, {report.AttackResults.Count} attack checks, {report.RemovedUnits.Count} unit removals.");
		foreach (string removed in report.RemovedUnits)
		{
			AppendLog($"Removed unit: {removed}");
		}

		ResetTurnPlanning();
		_activePlayer = PlayerSide.One;
		OnPlayerTurnStarted(_activePlayer);
		ClearSelection();
		RestartTurnTimer();
		UpdateStatusText();
		QueueRedraw();
	}

	// ---- Turn timer ----

	private void StartTurnTimer()
	{
		_timeRemaining = TurnTimeLimitSeconds;
		_timerRunning = true;
		UpdateTimerLabel();
	}

	private void RestartTurnTimer()
	{
		_timeRemaining = TurnTimeLimitSeconds;
		_timerRunning = true;
		UpdateTimerLabel();
	}

	private void StopTurnTimer()
	{
		_timerRunning = false;
		UpdateTimerLabel();
	}

	private void OnTurnTimerExpired()
	{
		_timerRunning = false;
		AppendLog($"{PlayerName(_activePlayer)}'s time ran out — turn skipped (no moves locked in).");

		if (_pendingPlacementType.HasValue)
		{
			CancelPendingPlacement("Pending placement was cleared when time expired.");
		}

		// Lock the player in with whatever they have (possibly nothing).
		if (_activePath != null && _activePath.HasPath)
		{
			CommitCurrentPath();
		}

		_lockedPlayers.Add(_activePlayer);

		if (_lockedPlayers.Count >= _turnOrder.Length)
		{
			ResolveAllLockedTurns();
			return;
		}

		AdvanceToNextUnlockedPlayer();
		OnPlayerTurnStarted(_activePlayer);
		ClearSelection();
		RestartTurnTimer();
		UpdateStatusText();
	}

	private void UpdateTimerLabel()
	{
		if (_timerLabel == null) return;

		int seconds = (int)Math.Ceiling(_timeRemaining);
		_timerLabel.Text = $"Time: {seconds}s";

		// Turn the label red in the final 10 seconds.
		if (_timeRemaining <= 10.0 && _timerRunning)
		{
			float urgency = 1.0f - (float)(_timeRemaining / 10.0);
			_timerLabel.Modulate = new Color(1.0f, 1.0f - urgency * 0.8f, 1.0f - urgency * 0.8f);
		}
		else
		{
			_timerLabel.Modulate = new Color(1f, 1f, 1f);
		}
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
		_unitPaths.Clear();
		foreach (PlayerSide side in _turnOrder)
		{
			_pendingSelections[side] = new PlayerTurnSelection(side);
		}
	}

	private void InitializeUnitEconomy()
	{
		_unitPointsByPlayer.Clear();
		_turnsTakenByPlayer.Clear();
		_nextSpawnIndexByPlayer.Clear();

		foreach (PlayerSide side in _turnOrder)
		{
			_unitPointsByPlayer[side] = UnitPointSystem.StartingUnitPoints;
			_turnsTakenByPlayer[side] = 0;
			_nextSpawnIndexByPlayer[side] = 1;
		}
	}

	private void OnPlayerTurnStarted(PlayerSide side)
	{
		if (!_turnsTakenByPlayer.ContainsKey(side))
		{
			return;
		}

		if (_turnsTakenByPlayer[side] > 0 && _controller.Board.CountAliveUnits(side) > 0)
		{
			_unitPointsByPlayer[side] += UnitPointSystem.UnitPointGainPerTurn;
			AppendLog($"{PlayerName(side)} gains {UnitPointSystem.UnitPointGainPerTurn} unit point (now {_unitPointsByPlayer[side]}).");
		}

		_turnsTakenByPlayer[side] += 1;
	}

	private void TryPurchaseAndPlaceUnit(UnitType type)
	{
		if (_lockedPlayers.Contains(_activePlayer))
		{
			AppendLog($"{PlayerName(_activePlayer)} is already locked and cannot buy units this turn.");
			return;
		}

		int cost = UnitPointSystem.GetUnitCost(type);
		int available = _unitPointsByPlayer.TryGetValue(_activePlayer, out int points) ? points : 0;

		if (available < cost)
		{
			AppendLog($"Not enough unit points for {type}. Need {cost}, have {available}.");
			return;
		}

		List<Vector2I> availableTiles = GetAvailableSpawnTiles(_activePlayer);
		if (availableTiles.Count == 0)
		{
			AppendLog($"No open base tile available for {PlayerName(_activePlayer)}.");
			return;
		}

		if (_activePath != null && _activePath.HasPath)
		{
			CommitCurrentPath();
		}

		_selectedUnitId = string.Empty;
		_activePath = null;
		_pendingPlacementType = type;
		_pendingPlacementTile = null;
		_pendingPlacementCandidates.Clear();
		_pendingPlacementCandidates.AddRange(availableTiles);
		_overlay.SetHighlightedTiles(_pendingPlacementCandidates);
		AppendLog($"Placement started for {type}. Click an open tile in your base, then press Confirm Placement.");
		UpdateStatusText();
		QueueRedraw();
	}

	private void OnConfirmPlacementPressed()
	{
		if (!_pendingPlacementType.HasValue)
		{
			AppendLog("No placement is pending. Choose a unit type first.");
			return;
		}

		if (!_pendingPlacementTile.HasValue)
		{
			AppendLog("Select a home-base tile before confirming placement.");
			return;
		}

		UnitType type = _pendingPlacementType.Value;
		Vector2I tile = _pendingPlacementTile.Value;
		int cost = UnitPointSystem.GetUnitCost(type);
		int available = _unitPointsByPlayer.TryGetValue(_activePlayer, out int points) ? points : 0;
		if (available < cost)
		{
			AppendLog($"Placement failed: not enough points for {type}. Need {cost}, have {available}.");
			CancelPendingPlacement();
			return;
		}

		if (!_baseAreas.TryGetValue(_activePlayer, out HashSet<Vector2I> area) || !area.Contains(tile))
		{
			AppendLog("Placement failed: selected tile is no longer in your base area.");
			CancelPendingPlacement();
			return;
		}

		if (_controller.Board.TryGetUnitAt(tile, out _))
		{
			AppendLog("Placement failed: selected tile is occupied.");
			_pendingPlacementTile = null;
			_overlay.SetHighlightedTiles(GetAvailableSpawnTiles(_activePlayer));
			UpdateStatusText();
			return;
		}

		string id = BuildPurchasedUnitId(_activePlayer, type);
		FacingDirection facing = GetDefaultFacing(_activePlayer);
		if (!_controller.SpawnUnit(type, id, _activePlayer, tile, facing, out string reason))
		{
			AppendLog($"Failed to spawn purchased unit {id}: {reason}");
			return;
		}

		_unitPointsByPlayer[_activePlayer] = available - cost;
		AppendLog($"{PlayerName(_activePlayer)} placed {type} ({id}) at {tile} for {cost} point(s). Remaining: {_unitPointsByPlayer[_activePlayer]}.");
		CancelPendingPlacement();
		UpdateStatusText();
		QueueRedraw();
	}

	private bool TryGetNextSpawnTile(PlayerSide side, out Vector2I tile)
	{
		tile = new Vector2I(0, 0);
		if (!_baseAreas.TryGetValue(side, out HashSet<Vector2I> area))
		{
			return false;
		}

		Vector2I anchor = GridTypes.BaseTiles.TryGetValue(side, out Vector2I baseTile)
			? baseTile
			: area.First();

		List<Vector2I> ordered = area
			.OrderBy(t => GridTypes.ManhattanDistance(t, anchor))
			.ThenBy(t => t.Y)
			.ThenBy(t => t.X)
			.ToList();

		foreach (Vector2I candidate in ordered)
		{
			if (!_controller.Board.TryGetUnitAt(candidate, out _))
			{
				tile = candidate;
				return true;
			}
		}

		return false;
	}

	private List<Vector2I> GetAvailableSpawnTiles(PlayerSide side)
	{
		List<Vector2I> tiles = new List<Vector2I>();
		if (!_baseAreas.TryGetValue(side, out HashSet<Vector2I> area))
		{
			return tiles;
		}

		Vector2I anchor = GridTypes.BaseTiles.TryGetValue(side, out Vector2I baseTile)
			? baseTile
			: area.First();

		IEnumerable<Vector2I> ordered = area
			.OrderBy(t => GridTypes.ManhattanDistance(t, anchor))
			.ThenBy(t => t.Y)
			.ThenBy(t => t.X);

		foreach (Vector2I candidate in ordered)
		{
			if (!_controller.Board.TryGetUnitAt(candidate, out _))
			{
				tiles.Add(candidate);
			}
		}

		return tiles;
	}

	private void HandlePlacementTileSelection(Vector2I tile)
	{
		if (!_pendingPlacementCandidates.Contains(tile))
		{
			AppendLog($"{tile} is not a valid open tile in {PlayerName(_activePlayer)}'s base.");
			return;
		}

		_pendingPlacementTile = tile;
		List<Vector2I> remaining = _pendingPlacementCandidates.Where(candidate => candidate != tile).ToList();
		_overlay.SetPathHighlights(new[] { tile }, remaining);
		AppendLog($"Selected placement tile {tile}. Press Confirm Placement to spawn.");
		UpdateStatusText();
	}

	private void CancelPendingPlacement(string reason = null)
	{
		if (!string.IsNullOrWhiteSpace(reason))
		{
			AppendLog(reason);
		}

		_pendingPlacementType = null;
		_pendingPlacementTile = null;
		_pendingPlacementCandidates.Clear();
		_overlay.ClearHighlights();
		UpdateStatusText();
	}

	private string BuildPurchasedUnitId(PlayerSide side, UnitType type)
	{
		int index = _nextSpawnIndexByPlayer.TryGetValue(side, out int known) ? known : 1;
		_nextSpawnIndexByPlayer[side] = index + 1;

		string prefix = type == UnitType.Infantry
			? "INF"
			: (type == UnitType.Cavalry ? "CAV" : "ARC");

		return $"P{(int)side}-{prefix}-{index:00}";
	}

	private FacingDirection GetDefaultFacing(PlayerSide side)
	{
		switch (side)
		{
			case PlayerSide.One:
				return FacingDirection.South;
			case PlayerSide.Two:
				return FacingDirection.West;
			case PlayerSide.Three:
				return FacingDirection.North;
			case PlayerSide.Four:
				return FacingDirection.East;
			default:
				return FacingDirection.South;
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
		_activePath = null;
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
	// Note: path and frontier tile highlighting is handled by MoveHighlightOverlay,
	// so this method only needs to handle base tiles and field ownership tints.
	private Color GetTileOverlayColor(Vector2I tile)
	{
		if (_controller.Board.IsBaseTile(tile))
		{
			// Gold tint for base tiles.
			return new Color(0.95f, 0.78f, 0.25f, 0.55f);
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
		int unitPoints = _unitPointsByPlayer.TryGetValue(_activePlayer, out int points) ? points : 0;
		int aliveUnits = _controller.Board.CountAliveUnits(_activePlayer);

		string pathInfo = "";
		if (_activePath != null)
		{
			int steps = _activePath.CommittedPath.Count;
			int frontier = _activePath.GetFrontierTiles().Count;
			pathInfo = $" | Path steps: {steps} | Next options: {frontier}";
		}

		string placementInfo = "";
		if (_pendingPlacementType.HasValue)
		{
			string tileLabel = _pendingPlacementTile.HasValue ? _pendingPlacementTile.Value.ToString() : "none";
			placementInfo = $" | Placement: {_pendingPlacementType.Value} @ {tileLabel}";
		}

		_statusLabel.Text = $"Active: {PlayerName(_activePlayer)} | Units: {aliveUnits} | UnitPts: {unitPoints} | Selected: {selectionLabel}{pathInfo}{placementInfo} | Queued Moves: {selection.Moves.Count} | Locked: {_lockedPlayers.Count}/4";
		UpdatePurchasePanel();
	}

	private void UpdatePurchasePanel()
	{
		if (_purchaseLabel == null || _buyInfantryButton == null || _buyArcherButton == null || _buyCavalryButton == null)
		{
			return;
		}

		int points = _unitPointsByPlayer.TryGetValue(_activePlayer, out int known) ? known : 0;
		bool canPurchase = !_lockedPlayers.Contains(_activePlayer);
		bool isPlacementPending = _pendingPlacementType.HasValue;
		int infantryCost = UnitPointSystem.GetUnitCost(UnitType.Infantry);
		int archerCost = UnitPointSystem.GetUnitCost(UnitType.Archer);
		int cavalryCost = UnitPointSystem.GetUnitCost(UnitType.Cavalry);

		if (isPlacementPending)
		{
			string selectedTile = _pendingPlacementTile.HasValue ? _pendingPlacementTile.Value.ToString() : "none";
			_purchaseLabel.Text = $"Place {_pendingPlacementType.Value}: selected tile {selectedTile} | points {points}";
		}
		else
		{
			_purchaseLabel.Text = $"Purchase: {PlayerName(_activePlayer)} has {points} point(s)";
		}

		_buyInfantryButton.Text = $"Buy Infantry ({infantryCost})";
		_buyArcherButton.Text = $"Buy Archer ({archerCost})";
		_buyCavalryButton.Text = $"Buy Cavalry ({cavalryCost})";

		_buyInfantryButton.Disabled = !canPurchase || points < infantryCost;
		_buyArcherButton.Disabled = !canPurchase || points < archerCost;
		_buyCavalryButton.Disabled = !canPurchase || points < cavalryCost;

		_confirmPlacementButton.Disabled = !isPlacementPending || !_pendingPlacementTile.HasValue || !canPurchase;
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