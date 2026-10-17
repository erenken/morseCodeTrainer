// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

public interface IRandomSource
{
	int Next(int exclusiveMaximum);
}

public sealed class RandomSource : IRandomSource
{
	public int Next(int exclusiveMaximum) => Random.Shared.Next(exclusiveMaximum);
}

public sealed record TrainingResult(string Expected, string Answer, int CorrectCharacters, int TotalCharacters)
{
	public bool IsCorrect => Expected == Answer;
	public double Accuracy => TotalCharacters == 0 ? 100 : 100.0 * CorrectCharacters / TotalCharacters;
}

public sealed class TrainingSession(IRandomSource random)
{
	public string CreateChallenge(string alphabet, int length = 5)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(alphabet);
		if (length is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(length));
		var characters = MorseCodec.Normalize(alphabet).Replace(" ", "").Distinct().ToArray();
		if (characters.Any(c => !MorseAlphabet.Symbols.ContainsKey(c)))
			throw new InputValidationException("Use supported Morse characters only.", nameof(alphabet));
		return string.Concat(Enumerable.Range(0, length).Select(_ => characters[random.Next(characters.Length)]));
	}

	public TrainingResult Grade(string expected, string answer)
	{
		var normalizedExpected = MorseCodec.Normalize(expected);
		var normalizedAnswer = MorseCodec.Normalize(answer);
		var correct = normalizedExpected.Zip(normalizedAnswer).Count(pair => pair.First == pair.Second);
		return new(normalizedExpected, normalizedAnswer, correct, Math.Max(normalizedExpected.Length, normalizedAnswer.Length));
	}
}
