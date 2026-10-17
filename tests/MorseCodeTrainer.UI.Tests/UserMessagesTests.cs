// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.JSInterop;
using MorseCodeTrainer.Core;
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.UI.Tests;

[TestClass]
public sealed class UserMessagesTests
{
	[TestMethod]
	public void DisplaysMorseGuidanceWithoutExceptionSuffixes()
	{
		var codec = new MorseCodec();
		var decodeError = Assert.ThrowsExactly<InputValidationException>(() => codec.Decode("HELLO"));
		Assert.AreEqual("'HELLO' is not a recognized Morse symbol. Separate letters with spaces and words with /.", UserMessages.FromException(decodeError, "Couldn't convert."));
		var encodeError = Assert.ThrowsExactly<InputValidationException>(() => codec.Encode("CAFÉ"));
		Assert.AreEqual("The character 'É' is not in the supported Morse alphabet.", UserMessages.FromException(encodeError, "Couldn't convert."));
	}

	[TestMethod]
	public void DoesNotExposeRuntimeResourcesStacksOrNativeDiagnostics()
	{
		const string FALLBACK = "Couldn't start receiving. Please try again.";
		Exception[] errors =
		[
			new ArgumentException("Arg_ParamName_Name, morse", "morse"),
			new ArgumentNullException("internalBuffer"),
			new ArgumentOutOfRangeException("internalBuffer", 42, "ArgumentOutOfRange_NeedNonNegNum"),
			new JSException("NotReadableError: driver failure\n at startCapture (https://localhost/media.js:123:4)"),
			new InvalidOperationException("AVFoundationErrorDomain -11800"),
			new ObjectDisposedException("Microsoft.JSInterop.DotNetObjectReference")
		];
		foreach (var error in errors)
			Assert.AreEqual(FALLBACK, UserMessages.FromException(error, FALLBACK));
	}

	[TestMethod]
	[DataRow("length", "Choose a group length from 1 to 20 characters.")]
	[DataRow("wordsPerMinute", "Choose a character speed from 5 to 60 WPM.")]
	[DataRow("effectiveWordsPerMinute", "Choose an effective speed between 5 WPM and the character speed.")]
	[DataRow("frequency", "Choose a tone between 200 and 1500 Hz.")]
	[DataRow("volume", "Choose a volume between 0% and 100%.")]
	[DataRow("low", "Choose a detection threshold above 0 and below 1.")]
	public void ExplainsInvalidSettingsUsingTheirVisibleNames(string parameter, string expected)
	{
		Assert.AreEqual(expected, UserMessages.FromException(new ArgumentOutOfRangeException(parameter, -1, "Internal runtime details"), "Try again."));
	}

	[TestMethod]
	public void ExplainsPermissionFailuresWithoutRepeatingSystemMessages()
	{
		Assert.AreEqual("Permission was denied. Check this app's permissions in your browser or device settings, then try again.",
			UserMessages.FromException(new UnauthorizedAccessException("Internal device path and error code"), "Couldn't start receiving."));
	}
}
