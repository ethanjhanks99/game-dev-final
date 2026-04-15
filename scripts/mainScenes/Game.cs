using Godot;
using System;

public partial class Game : Control
{
	Button _Lobby;
	Button _MainMenu;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_Lobby = GetNode<Button>("CenterContainer/VBoxContainer/Lobby");
		_MainMenu = GetNode<Button>("CenterContainer/VBoxContainer/MainMenu");

		_Lobby.Pressed += _on_Lobby_pressed;
		_MainMenu.Pressed += _on_MainMenu_pressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_Lobby_pressed()
	{
		GameManager.Instance.LoadLobby();
	}

	private void _on_MainMenu_pressed()
	{
		GameManager.Instance.LoadMainMenu();
	}
}
