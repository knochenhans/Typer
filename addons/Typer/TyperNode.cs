using System.Linq;

using Godot;
using Godot.Collections;

public partial class TyperNode : Control
{
    #region [Fields and Properties]
    [Signal] public delegate void FinishedEventHandler();

    [Export] public TyperResource Resource;

    // Replace the previous internal typing state with a Typer instance
    TyperCore TyperInstance;

    AudioStreamPlayer TypingSoundNode => GetNode<AudioStreamPlayer>("TypingSound");
    #endregion

    #region [Godot]
    public override void _Ready()
    {
        ((AudioStreamRandomizer)TypingSoundNode.Stream).AddStream(-1, Resource.TypingSound);

        TyperInstance = new TyperCore(Resource, this, () => TypingSoundNode.Play());
        TyperInstance.Updated += () =>
        {
            QueueRedraw();
            CustomMinimumSize = CalculateGetMinimumSize();
        };
        TyperInstance.Finished += () => EmitSignal(SignalName.Finished);
    }

    public override void _Draw()
    {
        var state = TyperInstance.CurrentState;
        if (state == TyperCore.StateEnum.Started)
        {
            base._Draw();
            return;
        }

        if (TyperInstance.CurrentLine != null)
        {
            var pos = Vector2.Zero;
            var printedLine = "";

            for (int lineIdx = 0; lineIdx <= TyperInstance.CurrentLastLineIdx; lineIdx++)
            {
                if (lineIdx < TyperInstance.Lines.Length)
                    DrawLine(out pos, out printedLine, lineIdx);
            }

            DrawCaret(pos, printedLine);
        }
    }
    #endregion

    #region [Lifecycle]
    public void Init(string text = "")
    {
        TyperInstance.Init(Size.X, text);
    }

    public async void Start() => await TyperInstance.Start();
    public void Stop() => TyperInstance.Stop();

    public void Reset()
    {
        QueueRedraw();
        Hide();
    }
    #endregion

    #region [Public]
    public void DrawPreview(string text) => TyperInstance.DrawPreview(text);
    public void PushText(string text) => TyperInstance.PushText(text);
    #endregion

    #region [Utility]
    private void DrawLine(out Vector2 pos, out string printedLine, int lineIdx)
    {
        var currentLine = TyperInstance.Lines[lineIdx];

        if (lineIdx < TyperInstance.CurrentLastLineIdx)
            printedLine = currentLine;
        else
            printedLine = currentLine[..TyperInstance.CurrentLastCharIdx];

        pos = new Vector2(0, Resource.FontSize + (Resource.LineSpacing * lineIdx));

        if (Resource.CenterHorizontally)
            pos.X += (Size.X / 2) - (TyperInstance.LinesWidth[lineIdx] / 2);

        if (Resource.CenterVertically)
            pos.Y += (Size.Y / 2) - (TyperInstance.Height / 2);

        DrawString(Resource.Font, pos, printedLine, fontSize: Resource.FontSize, modulate: Resource.FontColor);
    }

    public Vector2 CalculateGetMinimumSize()
    {
        //TODO: Ignores Resource.LineSpacing for now, as this represents not the inter-line spacing but added to height at which each line is drawn.
        if (TyperInstance == null)
            return Vector2.Zero;

        float lineHeight = Resource.FontSize;
        int lineCount = TyperInstance.CurrentLastLineIdx + 1;

        return new Vector2(0, lineCount * lineHeight);
    }

    private void DrawCaret(Vector2 pos, string printedLine)
    {
        if (TyperInstance.CurrentFinalCaretBlinkTime % 2 == 0 && Resource.Caret != "")
        {
            DrawChar(
                Resource.Font,
                pos + new Vector2(Resource.Font.GetStringSize(printedLine, fontSize: Resource.FontSize).X, 0),
                Resource.Caret,
                fontSize: Resource.FontSize,
                modulate: Resource.FontColor
            );
        }
    }
    #endregion
}