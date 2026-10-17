// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Core;

/// <summary>Hysteresis prevents noise near the threshold from creating spurious edges.</summary>
public sealed class SignalThreshold
{
	private readonly double _low;
	private readonly double _high;
	private bool _isOn;

	public SignalThreshold(double low, double high)
	{
		if (!double.IsFinite(low) || !double.IsFinite(high) || low < 0 || high > 1 || low >= high)
			throw new ArgumentOutOfRangeException(nameof(low), "Require 0 ≤ low < high ≤ 1.");
		_low = low;
		_high = high;
	}

	public bool Process(double level)
	{
		if (!double.IsFinite(level) || level is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(level));
		if (level >= _high) _isOn = true;
		else if (level <= _low) _isOn = false;
		return _isOn;
	}
}
