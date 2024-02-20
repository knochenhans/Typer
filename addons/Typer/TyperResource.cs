using Godot;

public partial class TyperResource : Resource
{
    [Export]
    public Font Font { get; set; } = new Control().GetThemeDefaultFont();

    [Export]
    public int LineSpacing { get; set; } = 30;

    [Export]
    public bool CenterHorizontally { get; set; }

    [Export]
    public bool CenterVertically { get; set; }

    [Export]
    public int FontSize { get; set; } = 16;

    [Export]
    public string Caret { get; set; } = "";

    [Export]
    public int FinalCaretBlinkTimes { get; set; } = 3;
}