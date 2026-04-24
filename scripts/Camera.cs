using Godot;
using System;

public partial class Camera : Camera2D
{
	[Export] float zoomScale = 0.1f;
	private bool isDragging = false;
	private Vector2 dragStartMousePos;
	private Vector2 dragStartCameraPos;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Camera2D camera = this;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("zoom_in"))
		{
			Zoom = Zoom * (1 + zoomScale);
		} else if (@event.IsActionPressed("zoom_out"))
		{
			Zoom = Zoom * (1 - zoomScale);
		}

		if (!isDragging && @event.IsActionPressed("drag"))
		{
			isDragging = true;
			dragStartMousePos = GetViewport().GetMousePosition();
			dragStartCameraPos = Position;
		}
		if (isDragging && @event.IsActionReleased("drag"))
		{
			isDragging = false;
		}

		if (isDragging && @event is InputEventMouseMotion mouseMotion)
		{
			Vector2 moveDelta = GetViewport().GetMousePosition() - dragStartMousePos;
			Position = dragStartCameraPos - moveDelta * 1/Zoom.X;
		}
    }

}
