// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Maui;

// Shell uses UIKit tabs on iOS and Material bottom navigation on Android.
// Leave native bar backgrounds unset so the OS owns glass, contrast, and accessibility.
public sealed class MobileShell : Shell
{
	private bool _changingTab;

	public MobileShell()
	{
		FlyoutBehavior = FlyoutBehavior.Disabled;
		var settings = new SignalSettings();
		var tabs = new TabBar();
		foreach (var section in new[] { "Practice", "Translate", "Receive", "Reference", "About" })
		{
			var tab = new Tab { Title = section, Icon = $"tab_{section.ToLowerInvariant()}.png", Route = section.ToLowerInvariant() };
			tab.Items.Add(new ShellContent
			{
				Title = section,
				ContentTemplate = new DataTemplate(() => new MainPage(section, true, settings))
			});
			tabs.Items.Add(tab);
		}
		Items.Add(tabs);
	}

	protected override async void OnNavigating(ShellNavigatingEventArgs args)
	{
		base.OnNavigating(args);
		if (!args.CanCancel || CurrentPage is not MainPage page) return;
		if (_changingTab) { args.Cancel(); return; }
		_changingTab = true;
		var deferral = args.GetDeferral();
		try
		{
			if (!await page.StopMediaAsync()) args.Cancel();
		}
		finally
		{
			_changingTab = false;
			deferral.Complete();
		}
	}
}
