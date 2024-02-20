using Godot;
using System;

public partial class Main : Node
{
	public override void _Ready()
	{
		var typerNode = GetNode<TyperNode>("Typer");
		typerNode.Init("Hello, World!");
		typerNode.Start();
	}
}
