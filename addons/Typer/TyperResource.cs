using Godot;

[GlobalClass]
public partial class TyperResource : Resource
{
    [ExportGroup("Font")]
    [Export] public Font Font = new Control().GetThemeDefaultFont();
    [Export] public int FontSize = 16;
    [Export] public Color FontColor = Colors.White;

    [ExportGroup("Layout")]
    [Export] public int LineSpacing = 30;
    [Export] public bool CenterHorizontally = false;
    [Export] public bool CenterVertically = false;

    [ExportGroup("Caret")]
    [Export] public string Caret = "";
    [Export] public float CaretBlinkTime = 0.2f;
    [Export] public int FinalCaretBlinkTimes = 3;

    [ExportGroup("Sound")]
    [Export] public AudioStream TypingSound = null;

    [ExportGroup("Timing")]
    [Export] public float TypingSpeed = 0.05f;
    [Export] public float StartDelay = 1.0f;
    [Export] public float PreFadeoutTime = 1.0f;
    [Export] public float FadeoutTime = 1.0f;
}
