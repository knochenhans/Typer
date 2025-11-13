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

	string Text;

	int CurrentFinalCaretBlinkTimes;
	int CurrentFinalCaretBlinkTime;

	string[] Lines;
	string CurrentLine;
	int CurrentLastLineIdx;
	int CurrentLastCharIdx;

	float[] LinesWidth;
	Dictionary<int, List<(int Position, int Value)>> Pauses;
	float Height;

	public enum StateEnum
	{
		Started,
		Typing,
		Pause,
		Finished
	}

	StateEnum State = StateEnum.Started;

	AnimationPlayer AnimationPlayerNode => GetNode<AnimationPlayer>("AnimationPlayer");
	AudioStreamPlayer TypingSoundNode => GetNode<AudioStreamPlayer>("TypingSound");

    public override void _Ready() => ((AudioStreamRandomizer)TypingSoundNode.Stream).AddStream(-1, Resource.TypingSound);

    public void Reset()
	{
		Texture = null;
		QueueRedraw();
		Hide();
	}

	public void Init(string text = "")
	{
		Pauses = [];
		CurrentFinalCaretBlinkTime = 0;
		CurrentFinalCaretBlinkTimes = 0;
		CurrentLine = "";
		CurrentLastCharIdx = 0;
		CurrentLastLineIdx = 0;

		if (text == string.Empty)
			text = Resource.Text;

		Lines = text.Split([System.Environment.NewLine], StringSplitOptions.None);

		LinesWidth = new float[Lines.Length];

		AnimationPlayerNode.Play("RESET");

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

		Show();
	}

	public async void Start()
	{
		await Task.Delay((int)(Resource.StartDelay * 1000));
		SwitchState(StateEnum.Typing);
		await TypeLoop();
	}

	private async Task TypeLoop()
	{
		while (true)
		{
			if (CurrentLastLineIdx >= Lines.Length)
			{
				CurrentFinalCaretBlinkTimes = Resource.FinalCaretBlinkTimes;
				SwitchState(StateEnum.Pause);
				break;
			}

			if (State != StateEnum.Typing)
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
							SwitchState(StateEnum.Pause);
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

			await Task.Delay((int)(Resource.TypingSpeed * 1000));
		}
	}

	static List<(int Position, int Value)> ExtractPauses(ref string input)
	{
		List<(int Position, int Value)> tags = [];

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

		return [.. tags.OrderBy(tag => tag.Value)];
	}

	public override void _Draw()
	{
		if (State == StateEnum.Started)
		{
			base._Draw();
			return;
		}

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

	public async void SwitchState(StateEnum newState)
	{
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
					QueueRedraw();
					await Task.Delay((int)(Resource.CaretBlinkTime * 1000));
				}
				if (CurrentLastLineIdx == Lines.Length)
					SwitchState(StateEnum.Finished);
				else
					SwitchState(StateEnum.Typing);
				break;
			case StateEnum.Finished:
				if (Resource.PreFadeoutTime > 0)
					await Task.Delay((int)(Resource.PreFadeoutTime * 1000));
				AnimationPlayerNode.SpeedScale /= Resource.FadeoutTime;
				AnimationPlayerNode.Play("Fadeout");
				await ToSignal(AnimationPlayerNode, "animation_finished");
				Reset();
				EmitSignal(SignalName.Finished);
				break;
		}
		State = newState;
	}

    public void Stop()
    {
        State = StateEnum.Finished;
    }
}
