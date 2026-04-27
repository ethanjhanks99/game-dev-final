using Godot;
using System;

public partial class GameManager : Node2D
{
	public static GameManager Instance { get; private set; }


	private bool server = false;
	private string host;
	private int players;
	private int thisPlayer; //player number either assigned to client or set to 1 for server

	private PackedScene _MainMenu;
	private PackedScene _Game;
	private PackedScene _Lobby;
	public override void _EnterTree()
	{
		Instance = this;

		_MainMenu = GD.Load<PackedScene>("res://scenes/mainScenes/MainMenu.tscn");
		_Game = GD.Load<PackedScene>("res://scenes/mainScenes/BoardGame.tscn");
		_Lobby = GD.Load<PackedScene>("res://scenes/mainScenes/Lobby.tscn");

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;

		this.players = 0;
	}

	private void OnPeerConnected(long id)
	{
		if (!Multiplayer.IsServer()) return;

		GD.Print($"Peer connected: {id}");
		players++;
		int playerNumber = players;
		RpcId(id, nameof(ReceivePlayerNumber), playerNumber);
		GD.Print($"Peer {id} assigned player number {playerNumber}.");

	}

	private void OnPeerDisconnected(long id)
	{
		if (!Multiplayer.IsServer()) return;
		players--;
		GD.Print($"Peer disconnected: {id}");
	}

	[Rpc]
	private void ReceivePlayerNumber(int playerNumber)
	{
		this.thisPlayer = playerNumber;
		GD.Print($"Received player number: {playerNumber}");
	}

	public void StartServer()
	{
		this.server = true;
		ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
		peer.CreateServer(8000, 3);
		Multiplayer.MultiplayerPeer = peer;
		players++;
		thisPlayer = 1;
		GD.Print("Server Started");
		LoadLobby();
	}

	public void JoinServer(string host = "")
	{
		this.server = false;
		this.host = host;
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        peer.CreateClient(host, 8000);
        Multiplayer.MultiplayerPeer = peer;
		LoadLobby();
	}


	public void LoadMainMenu()
	{
		GetTree().ChangeSceneToPacked(_MainMenu);
	}

	public void LoadGame()
	{
		GetTree().ChangeSceneToPacked(_Game);
	}

	public void LoadLobby()
	{
		GetTree().ChangeSceneToPacked(_Lobby);
	}

	public int GetPlayerNumber() 
	{
		return thisPlayer;
	}

	public string GetHost() 
	{
		return host;
	}
}
