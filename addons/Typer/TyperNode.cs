using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class TyperNode : TextureRect
{
    [Signal] public delegate void FinishedEventHandler();

    [Export] public TyperResource Resource;

    // Replace the previous internal typing state with a Typer instance
    TyperCore TyperInstance;

    AudioStreamPlayer TypingSoundNode => GetNode<AudioStreamPlayer>("TypingSound");

    public override void _Ready()
    {
        ((AudioStreamRandomizer)TypingSoundNode.Stream).AddStream(-1, Resource.TypingSound);

        // create and configure Typer
        TyperInstance = new TyperCore(Resource, this, () => TypingSoundNode.Play());
        TyperInstance.Updated += () => QueueRedraw();
        TyperInstance.Finished += () => EmitSignal(SignalName.Finished);
    }

    public void Reset()
    {
        Texture = null;
        QueueRedraw();
        Hide();
    }

    public void Init(string text = "") => TyperInstance.Init(text);

    public async void Start()
    {
        await TyperInstance.Start();
    }

    public void Stop() => TyperInstance.Stop();

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
                {
                    var currentLine = TyperInstance.Lines[lineIdx];

                    if (lineIdx < TyperInstance.CurrentLastLineIdx)
                        printedLine = currentLine;
                    else
                        printedLine = currentLine[..TyperInstance.CurrentLastCharIdx];

                    printedLine = printedLine.ReplaceN(@"\\", "");

                    pos = new Vector2(0, Resource.FontSize + Resource.LineSpacing * lineIdx);

                    if (Resource.CenterHorizontally)
                        pos.X += Size.X / 2 - TyperInstance.LinesWidth[lineIdx] / 2;

                    if (Resource.CenterVertically)
                        pos.Y += Size.Y / 2 - TyperInstance.Height / 2;

                    DrawString(Resource.Font, pos, printedLine, fontSize: Resource.FontSize);
                }
            }

            // Draw caret
            if (TyperInstance.CurrentFinalCaretBlinkTime % 2 == 0 && Resource.Caret != "")
                DrawChar(Resource.Font, pos + new Vector2(Resource.Font.GetStringSize(printedLine, fontSize: Resource.FontSize).X, 0), Resource.Caret, fontSize: Resource.FontSize);
        }
    }
}
