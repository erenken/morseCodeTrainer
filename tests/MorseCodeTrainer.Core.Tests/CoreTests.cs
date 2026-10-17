// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.Core;
using NSubstitute;

namespace MorseCodeTrainer.Core.Tests;

[TestClass]
public sealed class CodecTests
{
	private readonly MorseCodec _codec = new();
	public static IEnumerable<object[]> Alphabet => MorseAlphabet.Symbols.Select(p => new object[] { p.Key.ToString(), p.Value });

	[TestMethod, DynamicData(nameof(Alphabet))]
	public void EverySymbolRoundTrips(string text, string expected)
	{
		Assert.AreEqual(expected, _codec.Encode(text));
		Assert.AreEqual(text, _codec.Decode(expected));
	}

	[TestMethod]
	[DataRow("sos", "... --- ...")]
	[DataRow("  Hello\n\t world ", ".... . .-.. .-.. --- / .-- --- .-. .-.. -..")]
	[DataRow("", "")]
	[DataRow("  \t", "")]
	public void EncodesAndNormalizes(string text, string morse) => Assert.AreEqual(morse, _codec.Encode(text));

	[TestMethod]
	public void DecodesWhitespaceAndWordSeparators() => Assert.AreEqual("SOS SOS", _codec.Decode(" / ...  ---\t... / ... --- ... / "));

	[TestMethod]
	public void RejectsUnsupportedInput()
	{
		Assert.ThrowsExactly<InputValidationException>(() => _codec.Encode("CAFÉ"));
		Assert.ThrowsExactly<InputValidationException>(() => _codec.Decode("........"));
		Assert.ThrowsExactly<InputValidationException>(() => _codec.Decode("SOS"));
		Assert.ThrowsExactly<ArgumentNullException>(() => _codec.Encode(null!));
		Assert.ThrowsExactly<ArgumentNullException>(() => _codec.Decode(null!));
		Assert.AreEqual("", _codec.Decode(" \t "));
	}

	[TestMethod]
	public void KeepsValidationGuidanceSeparateFromParameterDiagnostics()
	{
		var error = Assert.ThrowsExactly<InputValidationException>(() => _codec.Decode("HELLO"));
		Assert.AreEqual("'HELLO' is not a recognized Morse symbol. Separate letters with spaces and words with /.", error.UserMessage);
		Assert.AreEqual("morse", error.ParamName);
		StringAssert.Contains(error.Message, error.ParamName);
	}
}

[TestClass]
public sealed class TransmissionTests
{
	[TestMethod]
	public void UsesInternationalElementLetterAndWordTiming()
	{
		var segments = new MorseTransmission(new MorseCodec()).Create("A E", new(20));
		CollectionAssert.AreEqual(new[] { new SignalSegment(true, 60), new(false, 60), new(true, 180), new(false, 420), new(true, 60) }, segments.ToArray());
		var letters = new MorseTransmission(new MorseCodec()).Create("EE", new(20));
		Assert.AreEqual(180, letters[1].DurationMilliseconds, 0.001);
	}

	[TestMethod]
	public void ParisSpeedIncludesTheFinalWordGap()
	{
		var timing = new MorseTiming(20);
		var segments = new MorseTransmission(new MorseCodec()).Create("PARIS", timing);
		Assert.AreEqual(3000, segments.Sum(s => s.DurationMilliseconds) + timing.SpacingUnitMilliseconds * 7, 0.001);
	}

	[TestMethod]
	public void FarnsworthStretchesSpacingOnly()
	{
		var timing = new MorseTiming(20, 10);
		var segments = new MorseTransmission(new MorseCodec()).Create("PARIS", timing);
		Assert.AreEqual(6000, segments.Sum(s => s.DurationMilliseconds) + timing.SpacingUnitMilliseconds * 7, 0.001);
		Assert.AreEqual(20, timing.WordsPerMinute);
		Assert.AreEqual(10, timing.EffectiveWordsPerMinute);
		Assert.IsTrue(segments.Where(s => s.IsOn).All(s => s.DurationMilliseconds is 60 or 180));
	}

	[TestMethod]
	public void DelegatesEncodingAndHandlesEmptyInput()
	{
		var codec = Substitute.For<IMorseCodec>();
		codec.Encode("message").Returns(".");
		var transmission = new MorseTransmission(codec);
		Assert.HasCount(1, transmission.Create("message", new()));
		codec.Received(1).Encode("message");
		Assert.IsEmpty(new MorseTransmission(new MorseCodec()).Create("", new()));
		Assert.ThrowsExactly<ArgumentNullException>(() => transmission.Create("message", null!));
	}

	[TestMethod]
	[DataRow(0.0, 5.0)] [DataRow(61.0, 5.0)] [DataRow(double.NaN, 5.0)]
	[DataRow(double.PositiveInfinity, 5.0)] [DataRow(20.0, 21.0)] [DataRow(20.0, 4.0)]
	[DataRow(20.0, double.NaN)] [DataRow(20.0, double.NegativeInfinity)]
	public void RejectsInvalidTiming(double speed, double effective) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MorseTiming(speed, effective));
}

[TestClass]
public sealed class DecoderTests
{
	[TestMethod]
	[DataRow(5.0, 5.0, 1.0)] [DataRow(15.0, 12.0, 1.0)] [DataRow(40.0, 40.0, 1.0)]
	[DataRow(20.0, 20.0, 0.85)] [DataRow(20.0, 20.0, 1.15)] [DataRow(60.0, 60.0, 1.0)]
	public void RecoversMessagesWithTimingVariation(double speed, double effective, double variation)
	{
		const string MESSAGE = "SOS CQ TEST 123, OK?";
		var timing = new MorseTiming(speed, effective);
		var decoder = Feed(MESSAGE, timing, variation);
		Assert.AreEqual(MESSAGE, decoder.Text);
		Assert.AreEqual("", decoder.PendingSymbol);
	}

	[TestMethod]
	public void RecoversAllAlphabetSymbols() => Assert.AreEqual(string.Join(' ', MorseAlphabet.Symbols.Keys), Feed(string.Join(' ', MorseAlphabet.Symbols.Keys), new(20)).Text);

	[TestMethod]
	public void IgnoresBriefGlitchesAndLeadingSilence()
	{
		var decoder = new StreamingMorseDecoder(new(20));
		decoder.AddSample(false, 0); decoder.AddSample(true, 100); decoder.AddSample(false, 104);
		decoder.AddSample(false, 1000); decoder.Finish(1100);
		Assert.AreEqual("", decoder.Text);
	}

	[TestMethod]
	public void FlushesTheLastLetterWithoutWaitingForAnotherSignal()
	{
		var decoder = new StreamingMorseDecoder(new(20));
		decoder.AddSample(true, 0); decoder.AddSample(true, 10); decoder.AddSample(false, 60);
		decoder.Finish(65);
		Assert.AreEqual("E", decoder.Text);
		decoder.Finish(65);
		Assert.AreEqual("E", decoder.Text);
	}

	[TestMethod]
	public void ReportsUnknownLongPulses()
	{
		var decoder = new StreamingMorseDecoder(new(20));
		decoder.AddSample(true, 0); decoder.AddSample(true, 20); decoder.Finish(600);
		Assert.AreEqual("�", decoder.Text);
	}

	[TestMethod]
	public void BoundsTheTranscriptAndUnknownSymbols()
	{
		Assert.AreEqual(4096, Feed(new string('E', 4200), new(60)).Text.Length);
		var decoder = new StreamingMorseDecoder(new(20));
		for (var i = 0; i < 15; i++)
		{
			decoder.AddSample(true, i * 120); decoder.AddSample(true, i * 120 + 10);
			decoder.AddSample(false, i * 120 + 60); decoder.AddSample(false, i * 120 + 70);
		}
		decoder.Finish(1800);
		Assert.AreEqual("�", decoder.Text);
	}

	[TestMethod]
	public void RejectsNonMonotonicAndNonFiniteSamples()
	{
		var decoder = new StreamingMorseDecoder(new());
		decoder.AddSample(false, 5);
		foreach (var timestamp in new[] { 4.0, -1, double.NaN, double.PositiveInfinity })
			Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => decoder.AddSample(true, timestamp));
	}

	internal static StreamingMorseDecoder Feed(string message, MorseTiming timing, double variation = 1)
	{
		var decoder = new StreamingMorseDecoder(timing);
		double at = 100;
		foreach (var segment in new MorseTransmission(new MorseCodec()).Create(message, timing).Append(new(false, timing.SpacingUnitMilliseconds * 8)))
		{
			for (double t = 0; t < segment.DurationMilliseconds * variation; t += timing.DotMilliseconds / 20)
				decoder.AddSample(segment.IsOn, at + t);
			at += segment.DurationMilliseconds * variation;
		}
		decoder.Finish(at);
		return decoder;
	}
}

[TestClass]
public sealed class ThresholdAndTrainingTests
{
	[TestMethod]
	public void HysteresisRejectsThresholdChatter()
	{
		var gate = new SignalThreshold(0.3, 0.6);
		CollectionAssert.AreEqual(new[] { false, true, true, false, false }, new[] { 0.4, 0.6, 0.4, 0.3, 0.4 }.Select(gate.Process).ToArray());
	}

	[TestMethod]
	[DataRow(-0.1, 0.6)] [DataRow(0.3, 1.1)] [DataRow(0.6, 0.3)] [DataRow(0.3, 0.3)]
	[DataRow(double.NaN, 0.5)] [DataRow(0.2, double.NaN)]
	public void RejectsInvalidThresholds(double low, double high) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SignalThreshold(low, high));

	[TestMethod]
	public void RejectsInvalidLevels()
	{
		var gate = new SignalThreshold(0.3, 0.6);
		foreach (var value in new[] { -0.1, 1.1, double.NaN, double.PositiveInfinity })
			Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => gate.Process(value));
	}

	[TestMethod]
	public void ChallengesUseTheChosenAlphabetAndRandomSource()
	{
		var random = Substitute.For<IRandomSource>(); random.Next(2).Returns(0, 1, 1);
		Assert.AreEqual("ETT", new TrainingSession(random).CreateChallenge("e e t", 3));
		random.Received(3).Next(2);
		var real = new RandomSource();
		Assert.IsTrue(Enumerable.Range(0, 100).All(_ => real.Next(2) is >= 0 and < 2));
	}

	[TestMethod]
	public void RejectsInvalidChallenges()
	{
		var session = new TrainingSession(new RandomSource());
		Assert.ThrowsExactly<ArgumentNullException>(() => session.CreateChallenge(null!));
		Assert.ThrowsExactly<ArgumentException>(() => session.CreateChallenge("  "));
		Assert.ThrowsExactly<InputValidationException>(() => session.CreateChallenge("🙂"));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.CreateChallenge("ET", 0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.CreateChallenge("ET", 21));
	}

	[TestMethod]
	[DataRow("SOS", " sos ", 3, 3, true)] [DataRow("SOS", "SO", 2, 3, false)]
	[DataRow("SOS", "SOSE", 3, 4, false)] [DataRow("", "", 0, 0, true)] [DataRow("ABC", "AXC", 2, 3, false)]
	public void GradesMissingExtraAndIncorrectCharacters(string expected, string answer, int correct, int total, bool exact)
	{
		var result = new TrainingSession(new RandomSource()).Grade(expected, answer);
		Assert.AreEqual(correct, result.CorrectCharacters); Assert.AreEqual(total, result.TotalCharacters);
		Assert.AreEqual(exact, result.IsCorrect);
		Assert.AreEqual(total == 0 ? 100 : 100.0 * correct / total, result.Accuracy, 0.001);
	}
}
