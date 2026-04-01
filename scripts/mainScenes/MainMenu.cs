using Godot;
using System;

public partial class MainMenu : Control
{

	Button _HostServer;
	LineEdit _ServerAddress;
	Button _JoinLobby;
	Button _Quit;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_HostServer = GetNode<Button>("CenterContainer/VBoxContainer/Host");
		_ServerAddress = GetNode<LineEdit>("CenterContainer/VBoxContainer/LobbyAddress");
		_JoinLobby = GetNode<Button>("CenterContainer/VBoxContainer/Lobby");
		_Quit = GetNode<Button>("CenterContainer/VBoxContainer/Quit");

		_HostServer.Pressed += _on_HostServer_pressed;
		_JoinLobby.Pressed += _on_JoinLobby_pressed;
		_Quit.Pressed += _on_Quit_pressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_HostServer_pressed()
	{
		GameManager.Instance.StartServer();

	}

	private void _on_JoinLobby_pressed()
	{
		//join game in client mode, print out connection message.
		GameManager.Instance.JoinServer(_ServerAddress.Text);
	}

	private void _on_Quit_pressed()
	{
		GetTree().Quit();
	}
}
