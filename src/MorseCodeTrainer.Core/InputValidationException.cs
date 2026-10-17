// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

/// <summary>An expected input error with guidance that is separate from exception diagnostics.</summary>
public sealed class InputValidationException(string userMessage, string? paramName = null) : ArgumentException(userMessage, paramName)
{
	public string UserMessage { get; } = userMessage;
}
