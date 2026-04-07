using System.Linq;
using System.Threading.Tasks;

using Godot;
using Godot.Collections;

[GlobalClass]
public partial class TyperNode : Control
{
    #region [Fields and Properties]
    [Signal] public delegate void FinishedEventHandler();
    [Signal] public delegate void StoppedEventHandler();
    [Signal] public delegate void SetupFinishedEventHandler();

    [Export] public TyperResource Resource;

    TyperCore TyperCore;

    AudioStreamPlayer TypingSoundNode => GetNode<AudioStreamPlayer>("TypingSound");
    #endregion

    #region [Godot]
    public override void _Ready()
    {
        SetupFinished += OnSetupFinished;

        if (TypingSoundNode.Stream is AudioStreamRandomizer randomizer && Resource.TypingSound != null)
        {
            randomizer = (AudioStreamRandomizer)randomizer.Duplicate();
            TypingSoundNode.Stream = randomizer;
            randomizer.AddStream(-1, Resource.TypingSound);
        }

        TyperCore = new TyperCore(Resource, this, TypingSoundNode);
        TyperCore.Updated += () => Redraw();
        TyperCore.Finished += () => EmitSignal(SignalName.Finished);
        EmitSignal(SignalName.SetupFinished);
    }

    public override void _ExitTree()
    {
        TyperCore?.Stop();
    }

    public override void _Process(double delta)
    {
        TyperCore?.Update(delta);
    }

    private void Redraw()
    {
        QueueRedraw();
    }

    public override Vector2 _GetMinimumSize()
    {
        return CalculateGetMinimumSize();
    }

    public override void _Draw()
    {
        if (TyperCore == null)
        {
            base._Draw();
            return;
        }

        var state = TyperCore.CurrentState;
        if (state == TyperCore.StateEnum.Idle || state == TyperCore.StateEnum.StartDelay || state == TyperCore.StateEnum.Finished)
        {
            base._Draw();
            return;
        }

        if (TyperCore.CurrentLine != null)
        {
            var pos = Vector2.Zero;
            var printedLine = "";

            for (int lineIdx = 0; lineIdx <= TyperCore.CurrentLastLineIdx; lineIdx++)
            {
                if (lineIdx < TyperCore.Lines.Length)
                    DrawTextLine(out pos, out printedLine, lineIdx);
            }

            DrawCaret(pos, printedLine);
        }
    }
    #endregion

    #region [Lifecycle]
    public void Start()
    {
        TyperCore.Start();
    }

    public void Stop()
    {
        TyperCore?.Stop();
        EmitSignal(SignalName.Stopped);
    }

    public void Reset()
    {
        QueueRedraw();
    }
    #endregion

    #region [Public]
    public void PushText(string text)
    {
        GD.Print($"Pushing text to Typer: {text}");
        TyperCore.PushText(text);
        TyperCore.Finished += OnFinished;
    }

    public Task PushTextAsync(string text)
    {
        var tcs = new TaskCompletionSource();

        void OnFinished()
        {
            Finished -= OnFinished;
            tcs.TrySetResult();

            GD.Print($"Finished displaying text: {text}");
        }

        Finished += OnFinished;
        TyperCore.PushText(text);

        return tcs.Task;
    }

    public void OnFinished()
    {
        EmitSignal(SignalName.Finished);

        GD.Print($"Finished displaying text: {TyperCore.RawText}");
    }

    public async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), "timeout");
    public void ClearText()
    {
        TyperCore?.ClearText();
    }
    #endregion

    #region [Events]
    public void OnSetupFinished() => TyperCore.Init(Size.X);
    #endregion

    #region [Utility]
    private void DrawTextLine(out Vector2 pos, out string printedLine, int lineIdx)
    {
        var currentLine = TyperCore.Lines[lineIdx];

        if (lineIdx < TyperCore.CurrentLastLineIdx)
            printedLine = currentLine;
        else
            printedLine = currentLine[..TyperCore.CurrentLastCharIdx];

        pos = new Vector2(0, Resource.FontSize + (Resource.LineSpacing * lineIdx));

        if (Resource.CenterHorizontally)
            pos.X += (Size.X / 2) - (TyperCore.LinesWidth[lineIdx] / 2);

        if (Resource.CenterVertically)
            pos.Y += (Size.Y / 2) - (TyperCore.Height / 2);

        DrawString(Resource.Font, pos, printedLine, fontSize: Resource.FontSize, modulate: Resource.FontColor);

        UpdateMinimumSize();
    }

    public Vector2 CalculateGetMinimumSize()
    {
        //TODO: Ignores Resource.LineSpacing for now, as this represents not the inter-line spacing but added to height at which each line is drawn.
        if (TyperCore == null)
            return Vector2.Zero;

        float lineHeight = Resource.FontSize;
        int lineCount = TyperCore.CurrentLastLineIdx + 1;

        var ControlWidth = TyperCore.LinesWidth.Length > 0 ? TyperCore.LinesWidth.Max() : 0;

        return new Vector2(ControlWidth, lineCount * lineHeight);
    }

    private void DrawCaret(Vector2 pos, string printedLine)
    {
        if (TyperCore.CurrentFinalCaretBlinkTime % 2 == 0 && Resource.Caret != "")
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