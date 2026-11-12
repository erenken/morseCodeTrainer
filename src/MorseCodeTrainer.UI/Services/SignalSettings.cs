// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.UI.Services;

// One instance is shared by the native tabs; the SPA owns one for its single trainer.
public sealed class SignalSettings
{
	public bool Sound { get; set; } = true;
	public bool Light { get; set; }
	public int WordsPerMinute { get; set; } = 15;
	public int EffectiveWordsPerMinute { get; set; } = 12;
	public int Frequency { get; set; } = 600;
	public double Volume { get; set; } = 0.2;

	public event Action? Changed;
	public void NotifyChanged() => Changed?.Invoke();

	public void ClampEffectiveSpeed() => EffectiveWordsPerMinute = Math.Min(EffectiveWordsPerMinute, WordsPerMinute);
}
