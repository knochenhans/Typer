using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

#nullable enable

public partial class TyperCore : GodotObject
{
    public enum StateEnum
    {
        Started,
        Typing,
        Pause,
        Finished
    }

    public TyperResource Resource { get; private set; }
    public TextureRect Target { get; private set; }
    public Action? PlayTypingSound { get; private set; }

    public string[] Lines { get; private set; } = [];
    public string? CurrentLine { get; private set; }
    public int CurrentLastLineIdx { get; private set; }
    public int CurrentLastCharIdx { get; private set; }
    public float[] LinesWidth { get; private set; } = [];
    public float Height { get; private set; }
    public int CurrentFinalCaretBlinkTimes { get; private set; }
    public int CurrentFinalCaretBlinkTime { get; private set; }

    SimpleStateManager<StateEnum> StateManager;

    readonly Dictionary<int, List<(int Position, int Value)>> Pauses;

    public event Action? Updated;
    public event Action? Finished;

    public TyperCore(TyperResource resource, TextureRect target, Action? playTypingSound = null)
    {
        Resource = resource;
        Target = target;
        PlayTypingSound = playTypingSound;
        StateManager = new SimpleStateManager<StateEnum>(StateEnum.Started);
        Pauses = [];
    }

    public void Init(string text = "")
    {
        Pauses.Clear();
        CurrentFinalCaretBlinkTime = 0;
        CurrentFinalCaretBlinkTimes = 0;
        CurrentLine = "";
        CurrentLastCharIdx = 0;
        CurrentLastLineIdx = 0;
        Height = 0;

        if (string.IsNullOrEmpty(text))
            text = Resource.Text;

        Lines = text.Split([System.Environment.NewLine], StringSplitOptions.None);
        LinesWidth = new float[Lines.Length];

        for (int i = 0; i < Lines.Length; i++)
        {
            var size = Resource.Font.GetStringSize(Lines[i], fontSize: Resource.FontSize);
            LinesWidth[i] = size.X;
            Height += size.Y;

            if (i < Lines.Length - 1)
                Height += Resource.LineSpacing;

            var pauses = ExtractPauses(ref Lines[i]);
            if (pauses.Count > 0)
                Pauses.Add(i, pauses);
        }

        Updated?.Invoke();
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

    static List<(int Position, int Value)> ExtractPauses(ref string input)
    {
        var tags = new List<(int Position, int Value)>();

        string pattern = @"(?<!\\)\[([^\]]+)\]";

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

    public StateEnum CurrentState => StateManager.CurrentState;

    private async Task SwitchState(StateEnum newState)
    {
        StateManager.CurrentState = newState;

        switch (newState)
        {
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

                Reset();
                Finished?.Invoke();
                break;
        }
    }

    public void Stop() => StateManager.CurrentState = StateEnum.Finished;

    public void Reset()
    {
        Lines = [];
        LinesWidth = [];
        Height = 0;
        CurrentLine = "";
        CurrentLastLineIdx = 0;
        CurrentLastCharIdx = 0;
        Pauses.Clear();
        Updated?.Invoke();
    }
}