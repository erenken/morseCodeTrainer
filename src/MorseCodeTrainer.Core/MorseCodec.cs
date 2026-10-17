// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

public interface IMorseCodec
{
	string Encode(string text);
	string Decode(string morse);
}

public sealed class MorseCodec : IMorseCodec
{
	public static string Normalize(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return string.Join(' ', text.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	public string Encode(string text)
	{
		var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return string.Join(" / ", words.Select(word => string.Join(' ', word.Select(character =>
			MorseAlphabet.Symbols.TryGetValue(character, out var symbol) ? symbol :
			throw new InputValidationException($"The character '{character}' is not in the supported Morse alphabet.", nameof(text))))));
	}

	public string Decode(string morse)
	{
		ArgumentNullException.ThrowIfNull(morse);
		var words = morse.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		return string.Join(' ', words.Select(word => string.Concat(word.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
			.Select(symbol => MorseAlphabet.Characters.TryGetValue(symbol, out var character) ? character :
			throw new InputValidationException($"'{symbol}' is not a recognized Morse symbol. Separate letters with spaces and words with /.", nameof(morse))))));
	}
}
