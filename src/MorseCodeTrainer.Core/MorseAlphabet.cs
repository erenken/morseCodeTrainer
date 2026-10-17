// Copyright © 2026 myNOC LLC. All rights reserved.
using System.Collections.Frozen;

namespace MorseCodeTrainer.Core;

public static class MorseAlphabet
{
	public static IReadOnlyDictionary<char, string> Symbols { get; } = new Dictionary<char, string>
	{
		['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".",
		['F'] = "..-.", ['G'] = "--.", ['H'] = "....", ['I'] = "..", ['J'] = ".---",
		['K'] = "-.-", ['L'] = ".-..", ['M'] = "--", ['N'] = "-.", ['O'] = "---",
		['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.", ['S'] = "...", ['T'] = "-",
		['U'] = "..-", ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-", ['Y'] = "-.--", ['Z'] = "--..",
		['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-",
		['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
		['.'] = ".-.-.-", [','] = "--..--", ['?'] = "..--..", ['\''] = ".----.", ['!'] = "-.-.--",
		['/'] = "-..-.", ['('] = "-.--.", [')'] = "-.--.-", ['&'] = ".-...", [':'] = "---...",
		[';'] = "-.-.-.", ['='] = "-...-", ['+'] = ".-.-.", ['-'] = "-....-", ['_'] = "..--.-",
		['\"'] = ".-..-.", ['$'] = "...-..-", ['@'] = ".--.-."
	}.ToFrozenDictionary();

	internal static IReadOnlyDictionary<string, char> Characters { get; } =
		Symbols.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);
}
