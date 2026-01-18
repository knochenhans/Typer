using System.Linq;
using System.Threading.Tasks;

using Godot;
using Godot.Collections;

public partial class TyperNode : PanelContainer
{
    #region [Fields and Properties]
    [Signal] public delegate void FinishedEventHandler();
    [Signal] public delegate void SetupFinishedEventHandler();

    [Export] public TyperResource Resource;

    TyperCore TyperCore;

    AudioStreamPlayer TypingSoundNode => GetNode<AudioStreamPlayer>("TypingSound");
    #endregion

    #region [Godot]
    public override async void _Ready()
    {
        SetupFinished += OnSetupFinished;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        ((AudioStreamRandomizer)TypingSoundNode.Stream).AddStream(-1, Resource.TypingSound);

        TyperCore = new TyperCore(Resource, this, TypingSoundNode);
        TyperCore.Updated += () => Redraw();
        TyperCore.Finished += () => EmitSignal(SignalName.Finished);
        EmitSignal(SignalName.SetupFinished);
    }

    private void Redraw()
    {
        if (TyperCore.CurrentState != TyperCore.StateEnum.Typing)
            return;

        QueueRedraw();
        CustomMinimumSize = CalculateGetMinimumSize();
    }

    public override void _Draw()
    {
        if (TyperCore == null)
        {
            base._Draw();
            return;
        }

        var state = TyperCore.CurrentState;
        if (state == TyperCore.StateEnum.Started)
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
    public async void Start() => await TyperCore.Start();
    public void Stop() => TyperCore.Stop();

    public void Reset()
    {
        QueueRedraw();
        Hide();
    }
    #endregion

    #region [Public]
    public void PushText(string text) => _ = TyperCore.PushText(text);
    public Task PushTextAsync(string text)
    {
        var tcs = new TaskCompletionSource();

        void OnFinished()
        {
            Finished -= OnFinished;
            tcs.TrySetResult();
        }

        Finished += OnFinished;
        _ = TyperCore.PushText(text);

        return tcs.Task;
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
    }

    public Vector2 CalculateGetMinimumSize()
    {
        //TODO: Ignores Resource.LineSpacing for now, as this represents not the inter-line spacing but added to height at which each line is drawn.
        if (TyperCore == null)
            return Vector2.Zero;

        float lineHeight = Resource.FontSize;
        int lineCount = TyperCore.CurrentLastLineIdx + 1;

        return new Vector2(0, lineCount * lineHeight);
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