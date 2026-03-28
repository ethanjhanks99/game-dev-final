using Godot;

public partial class Lobby : Control
{
	Button _StartGame;
	Button _MainMenu;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_StartGame = GetNode<Button>("CenterContainer/VBoxContainer/StartGame");
		_MainMenu = GetNode<Button>("CenterContainer/VBoxContainer/MainMenu");

		_StartGame.Pressed += _on_StartGame_pressed;
		_MainMenu.Pressed += _on_MainMenu_pressed;

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_StartGame_pressed()
	{
		GameManager.Instance.LoadGame();
	}

	private void _on_MainMenu_pressed()
	{
		GameManager.Instance.LoadMainMenu();
	}
}
