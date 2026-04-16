using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GridCombatController : Node
{
	[Signal]
	public delegate void TurnResolvedEventHandler(Godot.Collections.Dictionary summary);

	[Signal]
	public delegate void ReachableTilesComputedEventHandler(string unitId, Godot.Collections.Array<Vector2I> tiles);

	public BoardState Board { get; private set; } = new BoardState();

	public override void _Ready()
	{
		// Intentionally empty. Wiring to scene/UI/networking can call public methods below.
	}

	public bool SpawnUnit(UnitType type, string id, PlayerSide owner, Vector2I position, FacingDirection facing, out string reason)
	{
		BoardUnit unit = UnitCatalog.CreateUnit(type, id, owner, position, facing);
		return Board.TryPlaceUnit(unit, out reason);
	}

	public Godot.Collections.Array<Vector2I> ComputeReachableTiles(string unitId)
	{
		ISet<Vector2I> reachable = Board.GetReachableTiles(unitId, includeOccupiedTiles: false);
		Godot.Collections.Array<Vector2I> payload = new Godot.Collections.Array<Vector2I>(reachable.ToArray());
		EmitSignal(SignalName.ReachableTilesComputed, unitId, payload);
		return payload;
	}

	public TurnResolutionReport ResolveTurn(IEnumerable<PlayerTurnSelection> selections)
	{
		TurnResolutionReport report = TurnResolver.ResolveSimultaneousTurn(Board, selections);
		EmitSignal(SignalName.TurnResolved, BuildSummaryDictionary(report));
		return report;
	}

	private Godot.Collections.Dictionary BuildSummaryDictionary(TurnResolutionReport report)
	{
		Godot.Collections.Array moves = new Godot.Collections.Array();
		foreach (UnitMoveResolution move in report.MoveResults)
		{
			moves.Add(new Godot.Collections.Dictionary
			{
				{ "unitId", move.UnitId },
				{ "start", move.Start },
				{ "destination", move.Destination },
				{ "applied", move.Applied },
				{ "reason", move.Reason },
			});
		}

		Godot.Collections.Array attacks = new Godot.Collections.Array();
		foreach (UnitAttackResolution attack in report.AttackResults)
		{
			attacks.Add(new Godot.Collections.Dictionary
			{
				{ "attackerId", attack.AttackerId },
				{ "targetId", attack.TargetId },
				{ "damage", attack.Damage },
				{ "applied", attack.Applied },
				{ "reason", attack.Reason },
			});
		}

		Godot.Collections.Array removed = new Godot.Collections.Array();
		foreach (string removedUnitId in report.RemovedUnits)
		{
			removed.Add(removedUnitId);
		}

		return new Godot.Collections.Dictionary
		{
			{ "moves", moves },
			{ "attacks", attacks },
			{ "removed", removed },
		};
	}
}
