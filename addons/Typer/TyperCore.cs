using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Godot;
using Godot.Collections;

#nullable enable

public partial class TyperCore(TyperResource resource, Control target, AudioStreamPlayer typingSoundPlayer) : GodotObject
{
    #region [Fields and Properties]
    public enum StateEnum
    {
        Started,
        Typing,
        Pause,
        Finished
    }

    public TyperResource Resource = resource;
    public Control Target = target;

    public string RawText = string.Empty;
    public string[] Lines = [];
    public string? CurrentLine;
    public int CurrentLastLineIdx;
    public int CurrentLastCharIdx;
    public float[] LinesWidth = [];
    public float Height;
    public float ControlWidth;
    public int CurrentFinalCaretBlinkTimes;
    public int CurrentFinalCaretBlinkTime;

    AudioStreamPlayer TypingSoundPlayer = typingSoundPlayer;

    Color OriginalModulate;

    SimpleStateManager<StateEnum> StateManager = new(StateEnum.Started);

    readonly System.Collections.Generic.Dictionary<int, List<(int Position, int Value)>> Pauses = [];
    private CancellationTokenSource? cts;

    public event Action? Updated;
    public event Action? Finished;
    #endregion

    #region [Lifecycle]
    public void Init(float width)
    {
        ControlWidth = width;
        OriginalModulate = Target.Modulate;
        Reset();
    }

    public async Task Start()
    {
        cts = new CancellationTokenSource();
        await Task.Delay((int)(Resource.StartDelay * 1000), cts.Token);
        await SwitchState(StateEnum.Typing, cts.Token);
    }

    private async Task TypeLoop(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (CurrentLastLineIdx >= Lines.Length)
            {
                CurrentFinalCaretBlinkTimes = Resource.FinalCaretBlinkTimes;
                await SwitchState(StateEnum.Pause, cancellationToken);
                break;
            }

            if (StateManager.CurrentState != StateEnum.Typing)
                break;

            CurrentLine = Lines[CurrentLastLineIdx];

            if (CurrentLastCharIdx < CurrentLine.Length)
            {
                if (Pauses.TryGetValue(CurrentLastLineIdx, out var pausePositions))
                {
                    foreach (var (position, value) in pausePositions.ToList())
                    {
                        if (CurrentLastCharIdx == position)
                        {
                            CurrentFinalCaretBlinkTimes = value;
                            // remove this pause and go to pause state
                            pausePositions.Remove((position, value));
                            await SwitchState(StateEnum.Pause, cancellationToken);
                            return;
                        }
                    }
                }

                CurrentLastCharIdx++;
                Updated?.Invoke();

                if (!Resource.LoopTypingSound)
                    TypingSoundPlayer.Play();
            }
            else
            {
                CurrentLastLineIdx++;
                CurrentLastCharIdx = 0;
            }

            await Task.Delay((int)(Resource.TypingSpeed * 1000), cancellationToken);
        }
    }

    public StateEnum CurrentState => StateManager.CurrentState;

    private async Task SwitchState(StateEnum newState, CancellationToken cancellationToken = default)
    {
        StateManager.CurrentState = newState;

        switch (newState)
        {
            case StateEnum.Started:
                // TODO: This is never used
                GD.Print("TyperCore: Started");
                break;
            case StateEnum.Typing:
                if (Resource.LoopTypingSound)
                    TypingSoundPlayer.Play();
                CurrentFinalCaretBlinkTime = 0;
                await TypeLoop(cancellationToken);
                break;
            case StateEnum.Pause:
                while (CurrentFinalCaretBlinkTime < (CurrentFinalCaretBlinkTimes * 2) - 1)
                {
                    CurrentFinalCaretBlinkTime++;
                    Updated?.Invoke();
                    await Task.Delay((int)(Resource.CaretBlinkTime * 1000), cancellationToken);
                }
                if (CurrentLastLineIdx == Lines.Length)
                    await SwitchState(StateEnum.Finished, cancellationToken);
                else
                    await SwitchState(StateEnum.Typing, cancellationToken);
                break;
            case StateEnum.Finished:
                if (Resource.LoopTypingSound)
                    TypingSoundPlayer.Stop();

                if (Resource.PreFadeoutTime > 0)
                    await Task.Delay((int)(Resource.PreFadeoutTime * 1000), cancellationToken);

                if (Resource.FadeoutTime > 0)
                    await FadeHelper.TweenFadeModulate(Target, FadeHelper.FadeDirectionEnum.Out, Resource.FadeoutTime, targetOpacity: 0f);

                Finished?.Invoke();
                break;
        }
    }

    public void Stop()
    {
        cts?.Cancel();
        StateManager.CurrentState = StateEnum.Finished;
    }

    public void Reset()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();

        Lines = [];
        LinesWidth = [];
        Height = 0;
        CurrentLine = "";
        CurrentLastLineIdx = 0;
        CurrentLastCharIdx = 0;
        CurrentFinalCaretBlinkTime = 0;
        CurrentFinalCaretBlinkTimes = 0;
        Pauses.Clear();
        Target.Modulate = OriginalModulate;
    }
    #endregion

    #region [Public]
    public async Task PushText(string text)
    {
        Stop();
        Reset();

        RawText = text;
        RebuildLayout();

        cts = new CancellationTokenSource();
        await SwitchState(StateEnum.Typing, cts.Token);

        Updated?.Invoke();
    }

    public void ClearText()
    {
        Reset();
        Updated?.Invoke();
    }
    #endregion

    #region [Utility]
    static List<(int Position, int Value)> ExtractPauses(ref string input)
    {
        var tags = new List<(int Position, int Value)>();

        const string pattern = @"(?<!\\)\[([^\]]+)\]";

        while (true)
        {
            Match match = Regex.Match(input, pattern);
            if (!match.Success)
                break;

            input = input.Remove(match.Index, match.Length);
            if (int.TryParse(match.Groups[1].Value, out int v))
                tags.Add((match.Index, v));
        }

        return [.. tags.OrderBy(tag => tag.Value)];
    }

    private string[] WrapText(string text, Font font, int fontSize)
    {
        var paragraphs = text.Replace("\r\n", "\n").Split('\n');
        var lines = new Array<string>();

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrEmpty(para))
            {
                // Preserve explicit blank lines
                lines.Add("");
                continue;
            }

            var words = para.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = currentLine.Length == 0
                    ? word
                    : currentLine + " " + word;

                float testWidth = font.GetStringSize(
                    testLine,
                    fontSize: fontSize
                ).X;

                if (testWidth > ControlWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);
        }

        return [.. lines];
    }

    private void RebuildLayout()
    {
        Lines = WrapText(RawText, Resource.Font, Resource.FontSize);
        LinesWidth = [.. Lines.Select(l => Resource.Font.GetStringSize(l, fontSize: Resource.FontSize).X)];

        Height =
            (Lines.Length * Resource.FontSize) +
            ((Lines.Length - 1) * Resource.LineSpacing);

        for (int i = 0; i < Lines.Length; i++)
        {
            var pauses = ExtractPauses(ref Lines[i]);
            if (pauses.Count > 0)
                Pauses.Add(i, pauses);
            Lines[i] = Lines[i].ReplaceN(@"\\", "");
        }
    }
    #endregion
}