using Godot;
using System;

public partial class Main : Node
{
    Button Button => GetNode<Button>("Button");

    public override void _Ready()
    {
        Button.Pressed += OnButtonPressed;
    }

    public void OnButtonPressed()
    {
        var typerNode = GetNode<TyperNode>("%Typer");
        typerNode.PushText("Hello, World! Hello World! Hello,\nWorld! Hello World! Hello,\nWorld!");
    }
}
