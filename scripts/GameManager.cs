using Godot;

public partial class GameManager : Node2D
{
	public static GameManager Instance { get; private set; }


	public bool server = false;
	public string host;

	private PackedScene _MainMenu;
	private PackedScene _Game;
	private PackedScene _Lobby;
	public override void _EnterTree()
	{
		Instance = this;

		_MainMenu = GD.Load<PackedScene>("res://scenes/mainScenes/MainMenu.tscn");
		_Game = GD.Load<PackedScene>("res://scenes/mainScenes/BoardGame.tscn");
		_Lobby = GD.Load<PackedScene>("res://scenes/mainScenes/Lobby.tscn");


	}

	public void StartServer()
	{
		this.server = true;
		ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
		peer.CreateServer(8000, 64);
		Multiplayer.MultiplayerPeer = peer;
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
}
