// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.Logging;
using MorseCodeTrainer.UI;
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Maui;

public partial class MainPage : ContentPage
{
	private readonly SignalSettings _settings;

	public MainPage() : this("Practice", false, new SignalSettings()) { }

	public MainPage(string section, bool nativeNavigation, SignalSettings settings)
	{
		_settings = settings;
		InitializeComponent();
		Title = section;
		blazorWebView.RootComponents[0].Parameters = new Dictionary<string, object?>
		{
			[nameof(Trainer.InitialTab)] = section,
			[nameof(Trainer.UseNativeNavigation)] = nativeNavigation,
			[nameof(Trainer.Settings)] = settings
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Refresh shared signal settings without replacing the WebView or losing the tab's work.
		_settings.NotifyChanged();
	}

	public async Task<bool> StopMediaAsync()
	{
		try
		{
			// Dispatch into this WebView's scope; tabs must not leave a hidden receiver running.
			await MediaSessionDispatcher.SuspendAsync(blazorWebView.TryDispatchAsync);
			return true;
		}
		catch (Exception error)
		{
			Handler?.MauiContext?.Services.GetService<ILogger<MainPage>>()?.LogWarning(error, "Could not stop media before changing tabs.");
			await DisplayAlertAsync("Signal still active", "Couldn't stop the current signal. Try Stop again before changing tabs.", "OK");
			return false;
		}
	}

	private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
	{
#if ANDROID
		e.WebView.SetWebChromeClient(new CaptureWebChromeClient());
#endif
	}
}
