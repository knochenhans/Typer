#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

public partial class TyperCore(TyperResource resource, Control target, AudioStreamPlayer typingSoundPlayer) : GodotObject
{
    [Signal] public delegate void UpdatedEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    public enum StateEnum
    {
        Idle,
        StartDelay,
        Typing,
        Pause,
        Fadeout,
        Finished
    }

    public TyperResource Resource = resource;
    public Control Target = target;
    private AudioStreamPlayer TypingSoundPlayer = typingSoundPlayer;

    public string RawText = string.Empty;
    public string[] Lines = [];
    public string? CurrentLine;
    public int CurrentLastLineIdx;
    public int CurrentLastCharIdx;
    public float[] LinesWidth = [];
    public float Height;
    public float ControlWidth;

    private double fadeElapsed;

    public int CurrentFinalCaretBlinkTimes;
    public int CurrentFinalCaretBlinkTime;

    private double timer;
    private StateEnum state = StateEnum.Idle;

    private readonly Dictionary<int, List<(int Position, int Value)>> Pauses = [];

    public StateEnum CurrentState => state;

    public void Init(float width)
    {
        ControlWidth = width;
        Reset();
    }

    public void Start()
    {
        state = StateEnum.StartDelay;
        timer = Resource.StartDelay;
    }

    public void Stop()
    {
        state = StateEnum.Finished;
        TypingSoundPlayer?.Stop();
    }

    public void Update(double delta)
    {
        if (!IsInstanceValid(Target))
            return;

        switch (state)
        {
            case StateEnum.StartDelay:
                timer -= delta;
                if (timer <= 0)
                {
                    state = StateEnum.Typing;
                    timer = Resource.TypingSpeed;
                }
                break;

            case StateEnum.Typing:
                UpdateTyping(delta);
                break;

            case StateEnum.Pause:
                UpdatePause(delta);
                break;

            case StateEnum.Fadeout:
                UpdateFadeout(delta);
                break;
        }
    }

    private void UpdateTyping(double delta)
    {
        timer -= delta;

        if (timer > 0)
            return;

        timer = Resource.TypingSpeed;

        if (CurrentLastLineIdx >= Lines.Length)
        {
            CurrentFinalCaretBlinkTimes = Resource.FinalCaretBlinkTimes;
            state = StateEnum.Pause;
            return;
        }

        CurrentLine = Lines[CurrentLastLineIdx];

        if (CurrentLastCharIdx < CurrentLine.Length)
        {
            if (Pauses.TryGetValue(CurrentLastLineIdx, out var pauses))
            {
                foreach (var (pos, value) in pauses.ToList())
                {
                    if (CurrentLastCharIdx == pos)
                    {
                        CurrentFinalCaretBlinkTimes = value;
                        pauses.Remove((pos, value));
                        state = StateEnum.Pause;
                        return;
                    }
                }
            }

            CurrentLastCharIdx++;
            EmitSignal(SignalName.Updated);

            if (!Resource.LoopTypingSound)
                TypingSoundPlayer?.Play();
        }
        else
        {
            CurrentLastLineIdx++;
            CurrentLastCharIdx = 0;
        }
    }

    private void UpdatePause(double delta)
    {
        timer -= delta;

        if (timer > 0)
            return;

        timer = Resource.CaretBlinkTime;

        CurrentFinalCaretBlinkTime++;
        EmitSignal(SignalName.Updated);

        if (CurrentFinalCaretBlinkTime >= (CurrentFinalCaretBlinkTimes * 2))
        {
            if (CurrentLastLineIdx >= Lines.Length)
            {
                state = StateEnum.Fadeout;
                timer = Resource.PreFadeoutTime;
            }
            else
            {
                state = StateEnum.Typing;
                timer = Resource.TypingSpeed;
            }
        }
    }

    private void UpdateFadeout(double delta)
    {
        fadeElapsed += delta;

        float t = Mathf.Clamp((float)(fadeElapsed / Resource.FadeoutTime), 0f, 1f);
        Target.Modulate = new Color(1, 1, 1, 1 - t);

        if (fadeElapsed >= Resource.FadeoutTime)
        {
            state = StateEnum.Finished;
            EmitSignal(SignalName.Finished);
        }
    }

    public void PushText(string text)
    {
        Reset();

        RawText = text;
        RebuildLayout();
        Start();

        EmitSignal(SignalName.Updated);
    }

    public void ClearText()
    {
        Reset();
        EmitSignal(SignalName.Updated);
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

        state = StateEnum.Idle;
        timer = 0;
    }

    static List<(int Position, int Value)> ExtractPauses(ref string input)
    {
        var tags = new List<(int Position, int Value)>();
        const string pattern = @"(?<!\\)\[([^\]]+)\]";

        while (true)
        {
            var match = Regex.Match(input, pattern);
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
        var lines = new List<string>();

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrEmpty(para))
            {
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

                float width = font.GetStringSize(testLine, fontSize: fontSize).X;

                if (width > ControlWidth && currentLine.Length > 0)
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
                Pauses[i] = pauses;
        }
    }
}