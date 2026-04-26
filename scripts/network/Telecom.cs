using Godot;
using System;

public partial class Telecom : Node
{
	BoardGame _boardGame;

	public override void _Ready()
	{
	   	_boardGame = GetParent<BoardGame>();
	}

	//inputs: troop type, troop position, troop team
	//outputs: none
	//behaviour: receives data from sender method, reconstructs native datatypes, calls boardgame's placement method.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void ReceivePlacement(string playerNumber, string unitType, int tileX, int tileY) 
	{
		PlayerSide player = Enum.Parse<PlayerSide>(playerNumber);
		UnitType type = Enum.Parse<UnitType>(unitType);
		Vector2I tile = new Vector2I(tileX, tileY);
		_boardGame.PlaceUnit(player, type, tile);

		if(Multiplayer.IsServer())
		{
			foreach (long peerId in Multiplayer.GetPeers())
			{
				SendPlacement(peerId, player, type, tile);
			}
		}
	}

	//inputs: unit type, tile value, player
	//outputs: none
	//behaviour: convert inputs into simple datatypes to send to receiver function on server end.
	public void SendPlacement(long receiverId, PlayerSide player, UnitType type, Vector2I tile)
	{
		RpcId(receiverId, nameof(ReceivePlacement), player.ToString(), type.ToString(), tile[0], tile[1]);
	}
}
