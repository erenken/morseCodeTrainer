// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

public sealed record MorseTiming
{
	public MorseTiming(double wordsPerMinute = 15, double? effectiveWordsPerMinute = null)
	{
		if (!double.IsFinite(wordsPerMinute) || wordsPerMinute is < 5 or > 60)
			throw new ArgumentOutOfRangeException(nameof(wordsPerMinute), "Choose 5–60 words per minute.");
		var effective = effectiveWordsPerMinute ?? wordsPerMinute;
		if (!double.IsFinite(effective) || effective < 5 || effective > wordsPerMinute)
			throw new ArgumentOutOfRangeException(nameof(effectiveWordsPerMinute), "Effective speed must be between 5 and character speed.");
		WordsPerMinute = wordsPerMinute;
		EffectiveWordsPerMinute = effective;
	}

	public double WordsPerMinute { get; }
	public double EffectiveWordsPerMinute { get; }
	public double DotMilliseconds => 1200 / WordsPerMinute;
	// PARIS has 31 element units and 19 spacing units. Stretch only inter-letter/word gaps.
	public double SpacingUnitMilliseconds => (60000 / EffectiveWordsPerMinute - 31 * DotMilliseconds) / 19;
}

public sealed record SignalSegment(bool IsOn, double DurationMilliseconds);

public sealed class MorseTransmission(IMorseCodec codec)
{
	public IReadOnlyList<SignalSegment> Create(string text, MorseTiming timing)
	{
		ArgumentNullException.ThrowIfNull(timing);
		var encoded = codec.Encode(text);
		var result = new List<SignalSegment>();
		var words = encoded.Split(" / ", StringSplitOptions.RemoveEmptyEntries);
		for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
		{
			if (wordIndex > 0) result.Add(new(false, 7 * timing.SpacingUnitMilliseconds));
			var letters = words[wordIndex].Split(' ');
			for (var letterIndex = 0; letterIndex < letters.Length; letterIndex++)
			{
				if (letterIndex > 0) result.Add(new(false, 3 * timing.SpacingUnitMilliseconds));
				var letter = letters[letterIndex];
				for (var index = 0; index < letter.Length; index++)
				{
					if (index > 0) result.Add(new(false, timing.DotMilliseconds));
					result.Add(new(true, timing.DotMilliseconds * (letter[index] == '.' ? 1 : 3)));
				}
			}
		}
		return result.AsReadOnly();
	}
}
