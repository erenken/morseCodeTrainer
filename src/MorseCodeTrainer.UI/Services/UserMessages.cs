// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.Core;

namespace MorseCodeTrainer.UI.Services;

/// <summary>Only deliberate validation guidance belongs in alerts; unexpected failures use an operation-specific fallback.</summary>
public static class UserMessages
{
	public static string FromException(Exception error, string fallback) => error switch
	{
		InputValidationException validation => validation.UserMessage,
		ArgumentOutOfRangeException range => range.ParamName switch
		{
			"length" => "Choose a group length from 1 to 20 characters.",
			"wordsPerMinute" => "Choose a character speed from 5 to 60 WPM.",
			"effectiveWordsPerMinute" => "Choose an effective speed between 5 WPM and the character speed.",
			"frequency" => "Choose a tone between 200 and 1500 Hz.",
			"volume" => "Choose a volume between 0% and 100%.",
			"low" => "Choose a detection threshold above 0 and below 1.",
			_ => fallback
		},
		UnauthorizedAccessException => "Permission was denied. Check this app's permissions in your browser or device settings, then try again.",
		_ => fallback
	};
}
