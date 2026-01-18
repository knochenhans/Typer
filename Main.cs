using Godot;
using System;

public partial class Main : Node
{
    public override void _Ready()
    {
        var typerNode = GetNode<TyperNode>("%Typer");
        typerNode.PushText("Hello, World! Hello World! Hello\\, World! Hello World! Hello\\, World!");
    }
}
