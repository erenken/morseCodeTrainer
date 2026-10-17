// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.Core;

namespace MorseCodeTrainer.Core.Tests;

[TestClass]
public sealed class ToneDetectorTests
{
	[TestMethod]
	public void MeasuresSelectedToneAndRejectsOffFrequencyEnergy()
	{
		var tone = Enumerable.Range(0, 1024).Select(i => (float)(0.2 * Math.Sin(2 * Math.PI * 600 * i / 48000))).ToArray();
		Assert.IsGreaterThan(0.9, ToneDetector.Measure(tone, 48000, 600));
		Assert.IsLessThan(0.2, ToneDetector.Measure(tone, 48000, 1400));
		Assert.AreEqual(0, ToneDetector.Measure(new float[1024], 48000, 600));
		Assert.AreEqual(0, ToneDetector.Measure([], 48000, 600));
		Assert.AreEqual(0, ToneDetector.Measure([0], 48000, 600));
	}

	[TestMethod]
	[DataRow(0.0, 600.0)] [DataRow(48000.0, 0.0)] [DataRow(1000.0, 600.0)]
	[DataRow(double.NaN, 600.0)] [DataRow(48000.0, double.PositiveInfinity)]
	public void RejectsInvalidFrequencies(double rate, double frequency) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ToneDetector.Measure([], rate, frequency));

}
