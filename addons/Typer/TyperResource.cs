using Godot;

[GlobalClass]
public partial class TyperResource : Resource
{
    [ExportCategory("Font")]
    [Export] public Font Font { get; set; } = new Control().GetThemeDefaultFont();
    [Export] public int FontSize { get; set; } = 16;

    [ExportCategory("Layout")]
    [Export] public int LineSpacing { get; set; } = 30;
    [Export] public bool CenterHorizontally { get; set; }
    [Export] public bool CenterVertically { get; set; }

    [ExportCategory("Caret")]
    [Export] public string Caret { get; set; } = "";
    [Export] public float CaretBlinkTime { get; set; } = 0.2f;
    [Export] public int FinalCaretBlinkTimes { get; set; } = 3;

    [ExportCategory("Sound")]
    [Export] public AudioStream TypingSound { get; set; }

    [ExportCategory("Timing")]
    [Export] public float TypingSpeed { get; set; } = 0.05f;
    [Export] public float StartDelay { get; set; } = 1.0f;
    [Export] public float PreFadeoutTime { get; set; } = 1.0f;
    [Export] public float FadeoutTime { get; set; } = 1.0f;

    [ExportCategory("Text")]
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";

}
