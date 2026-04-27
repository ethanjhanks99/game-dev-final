using Godot;
using System;
using System.Collections.Generic;

public partial class Telecom : Node
{
	BoardGame _boardGame;

	public override void _Ready()
	{
	   	_boardGame = GetParent<BoardGame>();
	}

	//inputs: troop type, troop position, troop team
	//behaviour: receives data from sender method, reconstructs native datatypes, calls boardgame's placement method.
	//if server, relay data to clients via the same method.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void ReceivePlacement(string playerNumber, string unitType, int tileX, int tileY) 
	{
		PlayerSide player = Enum.Parse<PlayerSide>(playerNumber);
		UnitType type = Enum.Parse<UnitType>(unitType);
		Vector2I tile = new Vector2I(tileX, tileY);
		_boardGame.PlaceUnit(player, type, tile);

		if(Multiplayer.IsServer())
		{
			foreach(long peerId in Multiplayer.GetPeers())
			{
				SendPlacement(peerId, player, type, tile);
			}
		}
	}

	//inputs: unit type, tile value, player
	//behaviour: convert inputs into simple datatypes to send to receiver function on server end.
	public void SendPlacement(long receiverId, PlayerSide player, UnitType type, Vector2I tile)
	{
		RpcId(receiverId, nameof(ReceivePlacement), player.ToString(), type.ToString(), tile[0], tile[1]);
	}


	//inputs:
	//behaviour: reconstructs move order and adds it to the local boardgame's pendingSelections
	//if server, send back to clients (except the client that sent it) using this method
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void ReceiveMove(long senderId, string playerNumber, string unitId, int x, int y)
	{
		PlayerSide player = Enum.Parse<PlayerSide>(playerNumber);
		Vector2I destination = new Vector2I(x, y);
		MoveOrder order = new MoveOrder(unitId, destination);
		if(senderId!=Multiplayer.GetUniqueId()) //make sure server doesn't add moves to itself that it already has.
		{
			_boardGame.AddPendingMove(player, order);
		}

		if(Multiplayer.IsServer())
		{
			foreach(long peerId in Multiplayer.GetPeers())
			{
				if(peerId!=senderId)
				{
					RpcId(peerId, nameof(ReceiveMove), Multiplayer.GetUniqueId(), playerNumber, unitId, x, y);
				}
			}
		}
	}

	//inputs: 
	//behaviour: reconstructs attack order and adds it to the local boardgame's pendingSelections
	//if server, send back to clients (except the client that sent it) using this method
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void ReceiveAttack(long senderId, string playerNumber, string attackerId, string targetId)
	{
		PlayerSide player = Enum.Parse<PlayerSide>(playerNumber);
		AttackOrder order = new AttackOrder(attackerId, targetId);
		if(senderId!=Multiplayer.GetUniqueId())
		{
			_boardGame.AddPendingAttack(player, order);
		}
		
		if(Multiplayer.IsServer())
		{
			foreach(long peerId in Multiplayer.GetPeers())
			{
				if(peerId!=senderId)
				{
					RpcId(peerId, nameof(ReceiveAttack), Multiplayer.GetUniqueId(), playerNumber, attackerId, targetId);
				}
			}
		}
	}

	//inputs: all pending selections on this machine
	//behaviour: break down selections into simple datatypes and send to ReceiveMove() on server.
	public void SendSelections(long receiverId, Dictionary<PlayerSide, PlayerTurnSelection> selections)
	{
		foreach(KeyValuePair<PlayerSide, PlayerTurnSelection> kvp in selections)
		{
			string side = kvp.Key.ToString();
			PlayerTurnSelection selection = kvp.Value;
			foreach(MoveOrder move in selection.Moves)
			{
				RpcId(receiverId, nameof(ReceiveMove), Multiplayer.GetUniqueId(), side, move.UnitId, move.Destination[0], move.Destination[1]);
			}
			foreach(AttackOrder attack in selection.Attacks)
			{
				RpcId(receiverId, nameof(ReceiveAttack), Multiplayer.GetUniqueId(), side, attack.AttackerUnitId, attack.TargetUnitId);
			}
		}
	}

	//inputs: string containing player's number
	//behaviour: converts back to enum and adds that player to lockedplayers in boardgame
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void ReceiveLockedStatus(string playerNumber)
	{
		PlayerSide player = Enum.Parse<PlayerSide>(playerNumber);
		_boardGame.AddLockedPlayer(player);
	}

	//inputs: a playerside
	//behaviour: sends player number to server as string
	public void SendLockedStatus(PlayerSide player)
	{
		RpcId(1, nameof(ReceiveLockedStatus), player.ToString());
	}

	//behaviour: triggers resolution locally and on all clients, with a brief delay to allow all selections to get sent to all machines.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public async void TriggerResolve()
	{
		await ToSignal(GetTree().CreateTimer(0.25f), "timeout");
		_boardGame.ResolveAllLockedTurns();
		if(Multiplayer.IsServer())
		{
			foreach(long peerId in Multiplayer.GetPeers())
			{
				RpcId(peerId, nameof(TriggerResolve));
			}
		}
	}

	//behaviour: start timer in boardgame. if server, tell clients to do the same via this method.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void TriggerStartTimer()
	{
		_boardGame.StartTurnTimer();
		if(Multiplayer.IsServer())
		{
			foreach(long peerId in Multiplayer.GetPeers())
			{
				RpcId(peerId, nameof(TriggerStartTimer));
			}
		}
	}

	//behaviour: tell boardgame to increment readyplayers (server side only)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void ReceiveReadyStatus()
	{
		_boardGame.UpdateReadyPlayers();
	}

	//behaviour: tell the server that user has started the game
	public void SendReadyStatus()
	{
		RpcId(1, nameof(ReceiveReadyStatus));
	}
}
