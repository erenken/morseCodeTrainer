// Copyright © 2026 myNOC LLC. All rights reserved.
using System.Text;

namespace MorseCodeTrainer.Core;

/// <summary>Decodes timestamped signal levels. Call continuously, including during silence.</summary>
public sealed class StreamingMorseDecoder(MorseTiming timing)
{
	private readonly StringBuilder _text = new();
	private readonly StringBuilder _symbol = new();
	private bool _on;
	private bool? _candidate;
	private double _candidateAt;
	private double _edgeAt;
	private double _lastAt = -1;
	private bool _wordEnded = true;
	public string Text => _text.ToString().TrimEnd();
	public string PendingSymbol => _symbol.ToString();

	public void AddSample(bool isOn, double timestampMilliseconds)
	{
		ValidateTimestamp(timestampMilliseconds);
		if (isOn == _on) _candidate = null;
		else if (_candidate != isOn)
		{
			_candidate = isOn;
			_candidateAt = timestampMilliseconds;
		}
		else if (timestampMilliseconds - _candidateAt >= timing.DotMilliseconds * 0.15)
		{
			Transition(isOn, _candidateAt);
			_candidate = null;
		}
		if (!_on) CompleteGaps(_candidate == true ? _candidateAt : timestampMilliseconds);
	}

	public void Finish(double timestampMilliseconds)
	{
		ValidateTimestamp(timestampMilliseconds);
		// Preserve a final falling edge that has not yet passed the debounce window.
		if (_on) Transition(false, _candidate == false ? _candidateAt : timestampMilliseconds);
		CompleteLetter();
		_candidate = null;
	}

	private void ValidateTimestamp(double timestamp)
	{
		if (!double.IsFinite(timestamp) || timestamp < 0 || timestamp < _lastAt)
			throw new ArgumentOutOfRangeException(nameof(timestamp), "Timestamps must be finite, nonnegative and monotonic.");
		_lastAt = timestamp;
	}

	private void Transition(bool isOn, double at)
	{
		if (isOn) CompleteGaps(at);
		else
		{
			var units = (at - _edgeAt) / timing.DotMilliseconds;
			if (_symbol.Length < 9) _symbol.Append(units < 2 ? '.' : units <= 4.5 ? '-' : '?');
			_wordEnded = false;
		}
		_on = isOn;
		_edgeAt = at;
	}

	private void CompleteGaps(double at)
	{
		var gap = at - _edgeAt;
		if (gap >= 2 * timing.SpacingUnitMilliseconds) CompleteLetter();
		if (!_wordEnded && gap >= 5 * timing.SpacingUnitMilliseconds)
		{
			_text.Append(' ');
			_wordEnded = true;
		}
	}

	private void CompleteLetter()
	{
		if (_symbol.Length == 0) return;
		_text.Append(MorseAlphabet.Characters.TryGetValue(_symbol.ToString(), out var character) ? character : '�');
		_symbol.Clear();
		// A receiver can remain open indefinitely; keep the most recent transcript bounded.
		if (_text.Length > 4096) _text.Remove(0, _text.Length - 4096);
	}
}
