// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

/// <summary>Measures the selected frequency in mono PCM with a Hann window and Goertzel filter.</summary>
public static class ToneDetector
{
	public static double Measure(ReadOnlySpan<float> samples, double sampleRate, double frequency)
	{
		if (!double.IsFinite(sampleRate) || !double.IsFinite(frequency) || sampleRate <= 0 || frequency <= 0 || frequency >= sampleRate / 2)
			throw new ArgumentOutOfRangeException(nameof(frequency));
		if (samples.Length < 2) return 0;
		var coefficient = 2 * Math.Cos(2 * Math.PI * frequency / sampleRate);
		double previous = 0, beforePrevious = 0, windowSum = 0;
		for (var i = 0; i < samples.Length; i++)
		{
			var window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (samples.Length - 1));
			var current = samples[i] * window + coefficient * previous - beforePrevious;
			beforePrevious = previous;
			previous = current;
			windowSum += window;
		}
		var power = Math.Max(0, previous * previous + beforePrevious * beforePrevious - coefficient * previous * beforePrevious);
		var amplitude = 2 * Math.Sqrt(power) / Math.Max(1, windowSum);
		return Math.Clamp((20 * Math.Log10(Math.Max(1e-10, amplitude)) + 90) / 70, 0, 1);
	}
}
