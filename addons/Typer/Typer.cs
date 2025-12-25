#if TOOLS
using Godot;

using System;

[Tool]
public partial class Typer : EditorPlugin
{
    private const string AutoloadName = "TyperNode";

    public override void _EnterTree() => AddAutoloadSingleton(AutoloadName, "res://addons/Typer/TyperNode.tscn");
    public override void _ExitTree() => RemoveAutoloadSingleton(AutoloadName);
}
#endif
