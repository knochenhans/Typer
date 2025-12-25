using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Godot;
using Godot.Collections;

#nullable enable

public partial class TyperCore(TyperResource resource, Control target, Action? playTypingSound = null) : GodotObject
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
    public Action? PlayTypingSound = playTypingSound;

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

    SimpleStateManager<StateEnum> StateManager = new(StateEnum.Started);

    readonly System.Collections.Generic.Dictionary<int, List<(int Position, int Value)>> Pauses = [];

    public event Action? Updated;
    public event Action? Finished;
    #endregion

    #region [Lifecycle]
    public void Init(float width, string text = "")
    {
        ControlWidth = width;
        Reset();

        if (string.IsNullOrEmpty(text))
            text = Resource.Text;
        _ = PushText(text);
    }

    public async Task Start()
    {
        await Task.Delay((int)(Resource.StartDelay * 1000));
        await SwitchState(StateEnum.Typing);
    }

    private async Task TypeLoop()
    {
        while (true)
        {
            if (CurrentLastLineIdx >= Lines.Length)
            {
                CurrentFinalCaretBlinkTimes = Resource.FinalCaretBlinkTimes;
                await SwitchState(StateEnum.Pause);
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
                            await SwitchState(StateEnum.Pause);
                            return;
                        }
                    }
                }

                CurrentLastCharIdx++;
                Updated?.Invoke();
                PlayTypingSound?.Invoke();
            }
            else
            {
                CurrentLastLineIdx++;
                CurrentLastCharIdx = 0;
            }

            await Task.Delay((int)(Resource.TypingSpeed * 1000));
        }
    }

    public StateEnum CurrentState => StateManager.CurrentState;

    private async Task SwitchState(StateEnum newState)
    {
        StateManager.CurrentState = newState;

        switch (newState)
        {
            case StateEnum.Started:
                GD.Print("TyperCore: Started");
                break;
            case StateEnum.Typing:
                CurrentFinalCaretBlinkTime = 0;
                await TypeLoop();
                break;
            case StateEnum.Pause:
                while (CurrentFinalCaretBlinkTime < (CurrentFinalCaretBlinkTimes * 2) - 1)
                {
                    CurrentFinalCaretBlinkTime++;
                    Updated?.Invoke();
                    await Task.Delay((int)(Resource.CaretBlinkTime * 1000));
                }
                if (CurrentLastLineIdx == Lines.Length)
                    await SwitchState(StateEnum.Finished);
                else
                    await SwitchState(StateEnum.Typing);
                break;
            case StateEnum.Finished:
                if (Resource.PreFadeoutTime > 0)
                    await Task.Delay((int)(Resource.PreFadeoutTime * 1000));

                if (Resource.FadeoutTime > 0)
                    await FadeHelper.TweenFadeModulate(Target, FadeHelper.FadeDirectionEnum.Out, Resource.FadeoutTime, targetOpacity: 0f);

                Finished?.Invoke();
                break;
        }
    }

    public void Stop()
    {
        StateManager.CurrentState = StateEnum.Finished;
        Reset();
        Updated?.Invoke();
    }

    public void Reset()
    {
        Lines = [];
        LinesWidth = [];
        Height = 0;
        CurrentLine = "";
        CurrentLastLineIdx = 0;
        CurrentLastCharIdx = 0;
        CurrentFinalCaretBlinkTime = 0;
        CurrentFinalCaretBlinkTimes = 0;
        Pauses.Clear();
        // Updated?.Invoke();
    }
    #endregion

    #region [Public]
    public async Task PushText(string text)
    {
        // Reset();
        RawText = text;
        RebuildLayout();
        await SwitchState(StateEnum.Typing);
        Updated?.Invoke();
    }

    public void DrawPreview(string text)
    {
        Init(ControlWidth, text);
        CurrentLastLineIdx = Lines.Length;
        CurrentLastCharIdx = Lines.LastOrDefault()?.Length ?? 0;
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
        Lines = [.. Lines, .. WrapText(RawText, Resource.Font, Resource.FontSize)];
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