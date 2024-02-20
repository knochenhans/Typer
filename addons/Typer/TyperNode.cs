using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class TyperNode : TextureRect
{
	[Export]
	TyperResource Resource { get; set; }

	string Text { get; set; }

	int CurrentFinalCaretBlinkTimes { get; set; }
	int CurrentFinalCaretBlinkTime { get; set; }

	string[] Lines { get; set; }
	string CurrentLine { get; set; }
	int CurrentLastLineIdx { get; set; }
	int CurrentLastCharIdx { get; set; }

	float[] LinesWidth { get; set; }
	Dictionary<int, List<(int Position, int Value)>> Pauses { get; set; }

	float Height { get; set; }

	public enum StateEnum
	{
		Typing,
		Waiting,
		Finished
	}

	StateEnum State { get; set; }
	AnimationPlayer AnimationPlayer { get; set; }
	Timer TypeTimer { get; set; }
	Timer CaretBlinkTimer { get; set; }
	Timer StartDelayTimer { get; set; }
	AudioStreamPlayer TypingSoundNode { get; set; }

	float FadeoutSpeedScale { get; set; }

	public override void _Ready()
	{
		TypeTimer = GetNode<Timer>("TypeTimer");
		CaretBlinkTimer = GetNode<Timer>("CaretBlinkTimer");
		StartDelayTimer = GetNode<Timer>("StartDelayTimer");
		AnimationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		TypingSoundNode = GetNode<AudioStreamPlayer>("TypingSound");
	}

	public void Reset()
	{
		Texture = null;
		QueueRedraw();
		Hide();
	}

	public void Init(string text, float startDelay = 0.001f, float fadeoutSpeedScale = 1.0f)
	{
		Pauses = new();
		State = StateEnum.Typing;
		CurrentFinalCaretBlinkTime = 0;
		CurrentFinalCaretBlinkTimes = 0;
		CurrentLine = "";
		CurrentLastCharIdx = 0;
		CurrentLastLineIdx = 0;

		Lines = text.Split(new[] { System.Environment.NewLine }, StringSplitOptions.None);

		LinesWidth = new float[Lines.Length];

		AnimationPlayer.Play("RESET");
		// TypeTimer.Stop();
		// CaretBlinkTimer.Stop();
		// StartDelayTimer.Stop();

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

		FadeoutSpeedScale = fadeoutSpeedScale;
		StartDelayTimer.WaitTime = startDelay;
		Show();
	}

	public void Start() => StartDelayTimer.Start();

	static List<(int Position, int Value)> ExtractPauses(ref string input)
	{
		List<(int Position, int Value)> tags = new();

		// Match tags using regular expression
		string pattern = @"(?<!\\)\[([^\]]+)\]";

		do
		{
			Match match = Regex.Match(input, pattern);

			if (match.Success)
			{
				input = input.Remove(match.Index, match.Length);
				tags.Add((match.Index, match.Groups[1].Value.ToInt()));
			}
			else
				break;

		} while (true);

		return tags.OrderBy(tag => tag.Value).ToList();
	}

	public void _OnTypeTimerTimeout()
	{
		if (CurrentLastLineIdx >= Lines.Length)
		{
			CurrentFinalCaretBlinkTimes = Resource.FinalCaretBlinkTimes;
			SwitchState(StateEnum.Waiting);
			return;
		}

		if (State != StateEnum.Typing)
			return;

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
						SwitchState(StateEnum.Waiting);
						pausePositions.Remove((position, value));
						return;
					}
				}
			}

			CurrentLastCharIdx++;
			QueueRedraw();
			TypingSoundNode.Play();
		}
		else
		{
			CurrentLastLineIdx++;
			CurrentLastCharIdx = 0;
		}

		TypeTimer.Start();
	}

	public override void _Draw()
	{
		base._Draw();

		if (CurrentLine != null)
		{
			var pos = Vector2.Zero;

			var printedLine = "";

			for (int lineIdx = 0; lineIdx <= CurrentLastLineIdx; lineIdx++)
			{
				if (lineIdx < Lines.Length)
				{
					var currentLine = Lines[lineIdx];

					if (lineIdx < CurrentLastLineIdx)
						printedLine = currentLine;
					else
						printedLine = currentLine[..CurrentLastCharIdx];

					printedLine = printedLine.ReplaceN(@"\\", "");

					pos = new Vector2(0, Resource.FontSize + Resource.LineSpacing * lineIdx);

					if (Resource.CenterHorizontally)
						pos.X += Size.X / 2 - LinesWidth[lineIdx] / 2;

					if (Resource.CenterVertically)
						pos.Y += Size.Y / 2 - Height / 2;

					DrawString(Resource.Font, pos, printedLine, fontSize: Resource.FontSize);
				}
			}

			// Draw caret
			if (CurrentFinalCaretBlinkTime % 2 == 0 && Resource.Caret != "")
				DrawChar(Resource.Font, pos + new Vector2(Resource.Font.GetStringSize(printedLine, fontSize: Resource.FontSize).X, 0), Resource.Caret, fontSize: Resource.FontSize);
		}
	}

	public void _OnCaretBlinkTimerTimeout()
	{
		// Let the caret blink
		if (CurrentFinalCaretBlinkTime < (CurrentFinalCaretBlinkTimes * 2) - 1)
		{
			CurrentFinalCaretBlinkTime++;
			QueueRedraw();
			CaretBlinkTimer.Start();
		}
		else
		{
			if (CurrentLastLineIdx == Lines.Length)
				SwitchState(StateEnum.Finished);
			else
				SwitchState(StateEnum.Typing);
		}
	}

	public void _OnStartDelayTimerTimeout() => TypeTimer.Start();

	public void SwitchState(StateEnum newState)
	{
		switch (newState)
		{
			case StateEnum.Typing:
				TypeTimer.Start();
				CurrentFinalCaretBlinkTime = 0;
				break;
			case StateEnum.Waiting:
				CaretBlinkTimer.Start();
				break;
			case StateEnum.Finished:
				AnimationPlayer.SpeedScale = FadeoutSpeedScale;
				AnimationPlayer.Play("Fadeout");
				await ToSignal(AnimationPlayerNode, "animation_finished");
				Reset();
				break;
		}
		State = newState;
	}
}
