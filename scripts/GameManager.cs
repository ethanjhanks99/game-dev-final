using Godot;

public partial class GameManager : Node2D
{
	public static GameManager Instance { get; private set; }

	private PackedScene _MainMenu;
	private PackedScene _Game;
	private PackedScene _Lobby;
	public override void _EnterTree()
	{
		Instance = this;

		_MainMenu = GD.Load<PackedScene>("res://scenes/mainScenes/MainMenu.tscn");
		_Game = GD.Load<PackedScene>("res://scenes/mainScenes/Game.tscn");
		_Lobby = GD.Load<PackedScene>("res://scenes/mainScenes/Lobby.tscn");


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
